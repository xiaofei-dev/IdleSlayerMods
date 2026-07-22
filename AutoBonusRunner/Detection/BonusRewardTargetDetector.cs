using Il2Cpp;
using UnityEngine;

namespace AutoBonusRunner.Detection;

internal enum BonusRewardTargetKind
{
    None,
    RandomBox,
    Coin,
    Ruby,
    Sapphire,
    Emerald,
    Diamond,
    Zynium
}

internal readonly record struct BonusRewardTargetObservation(
    bool ScanPerformed,
    bool ScanSucceeded,
    bool CandidateQualified,
    bool IsLatched,
    bool LatchStarted,
    BonusRewardTargetKind Kind,
    int InstanceId,
    string ObjectPath,
    Vector3 Position,
    float RelativeX,
    float RelativeY,
    int ConsecutiveObservations,
    string Reason)
{
    internal string Describe() =>
        $"ScanPerformed={ScanPerformed},ScanSucceeded=" +
        $"{ScanSucceeded},CandidateQualified=" +
        $"{CandidateQualified},Latched={IsLatched}," +
        $"LatchStarted={LatchStarted},Kind={Kind}," +
        $"InstanceId={InstanceId},Path={ObjectPath},Position=" +
        $"({Position.x:F2},{Position.y:F2}),Relative=" +
        $"({RelativeX:F2},{RelativeY:F2}),Consecutive=" +
        $"{ConsecutiveObservations}/" +
        $"{BonusRewardTargetDetector.RequiredConsecutiveObservations}," +
        $"Reason={Reason}";
}

/// <summary>
/// Detects an actually spawned, interactable reward target. It deliberately
/// does not inspect GameObject names, renderer visibility callbacks, sphere
/// quota, or BonusMapController reward flags. Those signals either include
/// pooled/static false positives or stop updating while the game is in the
/// background.
/// </summary>
internal sealed class BonusRewardTargetDetector
{
    internal const int RequiredConsecutiveObservations = 2;
    internal const int RequiredRetiredInstanceAbsenceScans = 2;

    // A boundary snapshot plus one later complete inventory (or two later
    // complete inventories when the snapshot was unavailable) establishes a
    // stable epoch. Objects visible during that short quarantine are treated
    // as belonging to the retiring stream, not as newly spawned rewards.
    private const int RequiredCompleteEpochInventoryScans = 2;

    // The reward stream is spawned ahead of the runner. Keep a small rear
    // allowance so a box crossed between render frames can still authorize
    // the handoff, without accepting unrelated active pool objects elsewhere.
    private const float MaximumDistanceBehindPlayer = 4f;
    private const float MaximumDistanceAheadOfPlayer = 45f;
    private const float MaximumVerticalDistanceFromPlayer = 18f;
    private const float MinimumSpriteAlpha = 0.01f;
    private const float ScanIntervalSeconds = 0.035f;

    private int pendingInstanceId;
    private int pendingObservationCount;
    private int pendingLastObservedFrame = -1;
    private float nextScanAt;
    private bool latched;
    private RewardTargetCandidate latchedTarget;
    private readonly HashSet<int> lastQualifiedInstanceIds = new();
    private readonly HashSet<int> retiredEpochInstanceIds = new();
    private readonly Dictionary<int, int> retiredEpochAbsenceScans = new();
    private bool epochInventoryIncomplete;
    private bool epochInventoryStabilizationRequired;
    private int completeEpochInventoryScans;
    private int lastCompleteEpochInventoryFrame = -1;
    private BonusRewardTargetObservation lastObservation = new(
        false,
        false,
        false,
        false,
        false,
        BonusRewardTargetKind.None,
        0,
        string.Empty,
        default,
        0f,
        0f,
        0,
        "NotObserved");

    internal bool IsLatched => latched;

    internal bool IsEpochInventoryIncomplete =>
        epochInventoryIncomplete;

    internal BonusRewardTargetObservation LastObservation =>
        lastObservation;

    /// <summary>
    /// Observes active reward components and latches only after the same
    /// native instance qualifies on two distinct render frames. The caller
    /// owns lifecycle eligibility through <paramref name="observationAllowed"/>
    /// and explicitly clears a completed latch with <see cref="Reset"/>.
    /// </summary>
    internal BonusRewardTargetObservation Observe(
        BonusStageState state,
        bool observationAllowed)
    {
        if (latched)
        {
            lastObservation = CreateObservation(
                latchedTarget,
                scanPerformed: false,
                scanSucceeded: true,
                candidateQualified: true,
                isLatched: true,
                latchStarted: false,
                RequiredConsecutiveObservations,
                "LatchedUntilExplicitReset");
            return lastObservation;
        }

        if (!observationAllowed)
        {
            ClearPendingCandidate();
            nextScanAt = 0f;
            lastObservation = EmptyObservation(
                scanSucceeded: true,
                "ObservationNotAuthorizedByRuntimeLifecycle");
            return lastObservation;
        }

        if (!state.IsBonusStage || !state.IsSupportedBonusMap)
        {
            ClearPendingCandidate();
            nextScanAt = 0f;
            lastObservation = EmptyObservation(
                scanSucceeded: true,
                "OutsideSupportedBonusMap");
            return lastObservation;
        }

        if (!state.HasPlayer)
        {
            ClearPendingCandidate();
            nextScanAt = 0f;
            lastObservation = EmptyObservation(
                scanSucceeded: true,
                "PlayerUnavailable");
            return lastObservation;
        }

        if (Time.unscaledTime < nextScanAt)
        {
            lastObservation = lastObservation with
            {
                ScanPerformed = false,
                LatchStarted = false
            };
            return lastObservation;
        }
        nextScanAt = Time.unscaledTime + ScanIntervalSeconds;

        try
        {
            List<RewardTargetCandidate> candidates = new();
            int skippedWrappers = CollectQualifiedCandidates(
                state.PlayerPosition,
                candidates);
            int rawCandidateCount = candidates.Count;
            bool inventoryStabilizing = UpdateEpochInventory(
                candidates,
                skippedWrappers);

            // Inventory completeness is part of the proof. A valid candidate
            // from one wrapper cannot authorize reward ownership while any
            // sibling wrapper raced or failed to read in the same scan.
            if (skippedWrappers > 0)
            {
                ClearPendingCandidate();
                lastObservation = EmptyObservation(
                    scanSucceeded: false,
                    "IncompleteTypedScan:SkippedWrappers=" +
                    skippedWrappers,
                    scanPerformed: true);
                return lastObservation;
            }

            if (candidates.Count == 0)
            {
                ClearPendingCandidate();
                if (inventoryStabilizing)
                {
                    lastObservation = EmptyObservation(
                        scanSucceeded: true,
                        "EpochInventoryStabilizing:Complete=" +
                        $"{completeEpochInventoryScans}/" +
                        $"{RequiredCompleteEpochInventoryScans}:" +
                        $"Retired={retiredEpochInstanceIds.Count}",
                        scanPerformed: true);
                    return lastObservation;
                }
                if (rawCandidateCount > 0)
                {
                    lastObservation = EmptyObservation(
                        scanSucceeded: true,
                        $"OnlyRetiredEpochTargets:Active=" +
                        $"{rawCandidateCount},Tracked=" +
                        $"{retiredEpochInstanceIds.Count}",
                        scanPerformed: true);
                    return lastObservation;
                }
                lastObservation = EmptyObservation(
                    scanSucceeded: true,
                    "NoQualifiedActiveRewardTarget",
                    scanPerformed: true);
                return lastObservation;
            }

            RewardTargetCandidate selected = SelectCandidate(candidates);
            int frame = Time.frameCount;
            if (selected.InstanceId != pendingInstanceId)
            {
                pendingInstanceId = selected.InstanceId;
                pendingObservationCount = 1;
                pendingLastObservedFrame = frame;
            }
            else if (frame != pendingLastObservedFrame)
            {
                pendingObservationCount++;
                pendingLastObservedFrame = frame;
            }

            bool latchStarted =
                pendingObservationCount >=
                RequiredConsecutiveObservations;
            if (latchStarted)
            {
                latched = true;
                latchedTarget = selected;
                pendingObservationCount =
                    RequiredConsecutiveObservations;
            }

            lastObservation = CreateObservation(
                selected,
                scanPerformed: true,
                scanSucceeded: true,
                candidateQualified: true,
                isLatched: latched,
                latchStarted,
                pendingObservationCount,
                latchStarted
                    ? "SameQualifiedInstanceConfirmed"
                    : "AwaitingSecondObservation");
            return lastObservation;
        }
        catch (Exception exception)
        {
            // A wrapper read or scene-pool race must fail closed. Runtime can
            // rate-limit/report Describe() without losing normal navigation.
            ClearPendingCandidate();
            lastObservation = EmptyObservation(
                scanSucceeded: false,
                $"ScanFailed:{exception.GetType().Name}:" +
                exception.Message,
                scanPerformed: true);
            return lastObservation;
        }
    }

    internal void Reset(string reason)
    {
        latched = false;
        latchedTarget = default;
        ClearPendingCandidate();
        lastQualifiedInstanceIds.Clear();
        retiredEpochInstanceIds.Clear();
        retiredEpochAbsenceScans.Clear();
        epochInventoryIncomplete = false;
        epochInventoryStabilizationRequired = false;
        completeEpochInventoryScans = 0;
        lastCompleteEpochInventoryFrame = -1;
        nextScanAt = 0f;
        lastObservation = EmptyObservation(
            scanSucceeded: true,
            $"Reset:{reason}");
    }

    /// <summary>
    /// Starts a new terrain/reward epoch without relying on a global empty
    /// frame. Every currently known or currently scannable typed target is
    /// retired. A distinct target can still latch; a retired instance becomes
    /// eligible again only after two consecutive complete scans have observed
    /// it absent.
    /// </summary>
    internal void BeginEpoch(BonusStageState state, string reason)
    {
        if (pendingInstanceId != 0)
            RetireInstance(pendingInstanceId);
        if (latchedTarget.InstanceId != 0)
            RetireInstance(latchedTarget.InstanceId);
        foreach (int instanceId in lastQualifiedInstanceIds)
            RetireInstance(instanceId);

        int skippedWrappers = 0;
        bool snapshotSucceeded = false;
        if (state.IsBonusStage &&
            state.IsSupportedBonusMap &&
            state.HasPlayer)
        {
            try
            {
                List<RewardTargetCandidate> candidates = new();
                skippedWrappers = CollectQualifiedCandidates(
                    state.PlayerPosition,
                    candidates);
                lastQualifiedInstanceIds.Clear();
                foreach (RewardTargetCandidate candidate in candidates)
                {
                    RetireInstance(candidate.InstanceId);
                    lastQualifiedInstanceIds.Add(candidate.InstanceId);
                }
                snapshotSucceeded = skippedWrappers == 0;
            }
            catch
            {
                snapshotSucceeded = false;
            }
        }

        // If the boundary snapshot was unavailable or partial, the first
        // later complete inventory is itself retired before any target may
        // qualify. Even a successful boundary snapshot requires one later
        // complete inventory so a late activation from the retiring stream
        // cannot immediately masquerade as a fresh reward. This fails closed
        // without using a blind timeout.
        epochInventoryIncomplete = !snapshotSucceeded;
        epochInventoryStabilizationRequired = true;
        completeEpochInventoryScans = snapshotSucceeded ? 1 : 0;
        lastCompleteEpochInventoryFrame = snapshotSucceeded
            ? Time.frameCount
            : -1;
        latched = false;
        latchedTarget = default;
        ClearPendingCandidate();
        nextScanAt = 0f;
        lastObservation = EmptyObservation(
            scanSucceeded: snapshotSucceeded,
            $"EpochReset:{reason}:Retired=" +
            $"{retiredEpochInstanceIds.Count}:SnapshotComplete=" +
            $"{snapshotSucceeded}:Skipped={skippedWrappers}:" +
            $"StableInventory={completeEpochInventoryScans}/" +
            $"{RequiredCompleteEpochInventoryScans}");
    }

    internal string Describe() => lastObservation.Describe();

    private int CollectQualifiedCandidates(
        Vector3 playerPosition,
        List<RewardTargetCandidate> candidates)
    {
        int skippedWrappers = 0;
        AddQualifiedRandomBoxes(
            playerPosition,
            candidates,
            ref skippedWrappers);
        AddQualifiedCollectables(
            playerPosition,
            candidates,
            ref skippedWrappers);
        return skippedWrappers;
    }

    private bool UpdateEpochInventory(
        List<RewardTargetCandidate> candidates,
        int skippedWrappers)
    {
        HashSet<int> currentInstanceIds = new();
        foreach (RewardTargetCandidate candidate in candidates)
            currentInstanceIds.Add(candidate.InstanceId);

        bool completeScan = skippedWrappers == 0;
        bool quarantineCurrentInventory =
            epochInventoryStabilizationRequired &&
            completeEpochInventoryScans <
            RequiredCompleteEpochInventoryScans;
        if (epochInventoryIncomplete || quarantineCurrentInventory)
        {
            foreach (int instanceId in currentInstanceIds)
                RetireInstance(instanceId);
            if (completeScan)
                epochInventoryIncomplete = false;
        }

        if (completeScan)
        {
            List<int> absenceConfirmed = new();
            foreach (int instanceId in retiredEpochInstanceIds)
            {
                if (currentInstanceIds.Contains(instanceId))
                {
                    retiredEpochAbsenceScans[instanceId] = 0;
                    continue;
                }

                retiredEpochAbsenceScans.TryGetValue(
                    instanceId,
                    out int absenceScans);
                absenceScans++;
                if (absenceScans >=
                    RequiredRetiredInstanceAbsenceScans)
                {
                    absenceConfirmed.Add(instanceId);
                }
                else
                {
                    retiredEpochAbsenceScans[instanceId] = absenceScans;
                }
            }

            foreach (int instanceId in absenceConfirmed)
            {
                retiredEpochInstanceIds.Remove(instanceId);
                retiredEpochAbsenceScans.Remove(instanceId);
            }

            if (epochInventoryStabilizationRequired &&
                Time.frameCount != lastCompleteEpochInventoryFrame)
            {
                lastCompleteEpochInventoryFrame = Time.frameCount;
                completeEpochInventoryScans = Math.Min(
                    RequiredCompleteEpochInventoryScans,
                    completeEpochInventoryScans + 1);
                if (completeEpochInventoryScans >=
                    RequiredCompleteEpochInventoryScans)
                {
                    epochInventoryStabilizationRequired = false;
                }
            }
        }

        lastQualifiedInstanceIds.Clear();
        foreach (int instanceId in currentInstanceIds)
            lastQualifiedInstanceIds.Add(instanceId);
        candidates.RemoveAll(
            candidate =>
                retiredEpochInstanceIds.Contains(candidate.InstanceId));
        return quarantineCurrentInventory;
    }

    private void RetireInstance(int instanceId)
    {
        if (instanceId == 0)
            return;

        retiredEpochInstanceIds.Add(instanceId);
        retiredEpochAbsenceScans[instanceId] = 0;
    }

    private void AddQualifiedRandomBoxes(
        Vector3 playerPosition,
        List<RewardTargetCandidate> candidates,
        ref int skippedWrappers)
    {
        RandomBox[] boxes =
            UnityEngine.Object.FindObjectsOfType<RandomBox>();
        foreach (RandomBox box in boxes)
        {
            try
            {
                if (box == null ||
                    !box.isActiveAndEnabled ||
                    box.gameObject == null ||
                    !box.gameObject.activeInHierarchy ||
                    box.isHitted ||
                    !IsQualifiedCollider(box.col2D) ||
                    !TryGetQualifiedSprite(
                        box,
                        box.spriteRenderer,
                        out _))
                {
                    continue;
                }

                TryAddCandidate(
                    box,
                    BonusRewardTargetKind.RandomBox,
                    playerPosition,
                    candidates);
            }
            catch
            {
                // An IL2CPP pool can invalidate one wrapper while the global
                // result array is being enumerated. Skip only that raced
                // instance; another qualified object may still be valid.
                skippedWrappers++;
            }
        }
    }

    private void AddQualifiedCollectables(
        Vector3 playerPosition,
        List<RewardTargetCandidate> candidates,
        ref int skippedWrappers)
    {
        CollectableGameObject[] collectables =
            UnityEngine.Object.FindObjectsOfType<CollectableGameObject>();
        foreach (CollectableGameObject collectableObject in collectables)
        {
            try
            {
                if (collectableObject == null ||
                    !collectableObject.isActiveAndEnabled ||
                    collectableObject.gameObject == null ||
                    !collectableObject.gameObject.activeInHierarchy ||
                    collectableObject.pickedUp ||
                    collectableObject.collectable == null ||
                    !TryGetCollectableKind(
                        collectableObject.collectable.collectableReward,
                        out BonusRewardTargetKind kind) ||
                    !HasQualifiedCollider(collectableObject) ||
                    !TryGetQualifiedSprite(
                        collectableObject,
                        preferred: null,
                        out _))
                {
                    continue;
                }

                TryAddCandidate(
                    collectableObject,
                    kind,
                    playerPosition,
                    candidates);
            }
            catch
            {
                // See RandomBox enumeration above.
                skippedWrappers++;
            }
        }
    }

    private static void TryAddCandidate(
        Component component,
        BonusRewardTargetKind kind,
        Vector3 playerPosition,
        List<RewardTargetCandidate> candidates)
    {
        Vector3 position = component.transform.position;
        float relativeX = position.x - playerPosition.x;
        float relativeY = position.y - playerPosition.y;
        if (relativeX < -MaximumDistanceBehindPlayer ||
            relativeX > MaximumDistanceAheadOfPlayer ||
            Mathf.Abs(relativeY) > MaximumVerticalDistanceFromPlayer)
        {
            return;
        }

        candidates.Add(new RewardTargetCandidate(
            kind,
            component.GetInstanceID(),
            GetPath(component.transform),
            position,
            relativeX,
            relativeY));
    }

    private RewardTargetCandidate SelectCandidate(
        List<RewardTargetCandidate> candidates)
    {
        if (pendingInstanceId != 0)
        {
            foreach (RewardTargetCandidate candidate in candidates)
            {
                if (candidate.InstanceId == pendingInstanceId)
                    return candidate;
            }
        }

        RewardTargetCandidate selected = candidates[0];
        float selectedScore = CandidateScore(selected);
        for (int index = 1; index < candidates.Count; index++)
        {
            RewardTargetCandidate candidate = candidates[index];
            float score = CandidateScore(candidate);
            if (score < selectedScore ||
                (Mathf.Approximately(score, selectedScore) &&
                 candidate.InstanceId < selected.InstanceId))
            {
                selected = candidate;
                selectedScore = score;
            }
        }

        return selected;
    }

    private static float CandidateScore(
        RewardTargetCandidate candidate)
    {
        // Prefer an ahead target. A just-crossed target remains eligible, but
        // loses deterministic selection to any target still approaching.
        float behindPenalty = candidate.RelativeX < 0f ? 100f : 0f;
        return behindPenalty + Mathf.Abs(candidate.RelativeX) +
            (Mathf.Abs(candidate.RelativeY) * 0.01f);
    }

    private static bool HasQualifiedCollider(Component owner)
    {
        Collider2D[] colliders =
            owner.GetComponentsInChildren<Collider2D>(false);
        foreach (Collider2D collider in colliders)
        {
            if (IsQualifiedCollider(collider))
                return true;
        }

        return false;
    }

    private static bool IsQualifiedCollider(Collider2D collider) =>
        collider != null &&
        collider.enabled &&
        collider.gameObject != null &&
        collider.gameObject.activeInHierarchy;

    private static bool TryGetQualifiedSprite(
        Component owner,
        SpriteRenderer preferred,
        out SpriteRenderer sprite)
    {
        if (IsQualifiedSprite(preferred))
        {
            sprite = preferred;
            return true;
        }

        SpriteRenderer[] renderers =
            owner.GetComponentsInChildren<SpriteRenderer>(false);
        foreach (SpriteRenderer candidate in renderers)
        {
            if (!IsQualifiedSprite(candidate))
                continue;

            sprite = candidate;
            return true;
        }

        sprite = null;
        return false;
    }

    private static bool IsQualifiedSprite(SpriteRenderer sprite) =>
        sprite != null &&
        sprite.enabled &&
        sprite.gameObject != null &&
        sprite.gameObject.activeInHierarchy &&
        sprite.sprite != null &&
        sprite.color.a > MinimumSpriteAlpha;

    private static bool TryGetCollectableKind(
        CollectableTypes collectableType,
        out BonusRewardTargetKind kind)
    {
        kind = collectableType switch
        {
            CollectableTypes.Coin => BonusRewardTargetKind.Coin,
            CollectableTypes.Ruby => BonusRewardTargetKind.Ruby,
            CollectableTypes.Sapphire => BonusRewardTargetKind.Sapphire,
            CollectableTypes.Emerald => BonusRewardTargetKind.Emerald,
            CollectableTypes.Diamond => BonusRewardTargetKind.Diamond,
            CollectableTypes.Zynium => BonusRewardTargetKind.Zynium,
            _ => BonusRewardTargetKind.None
        };
        return kind != BonusRewardTargetKind.None;
    }

    private static string GetPath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;
        int depth = 0;
        while (parent != null && depth++ < 8)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }

        return path;
    }

    private void ClearPendingCandidate()
    {
        pendingInstanceId = 0;
        pendingObservationCount = 0;
        pendingLastObservedFrame = -1;
    }

    private BonusRewardTargetObservation EmptyObservation(
        bool scanSucceeded,
        string reason,
        bool scanPerformed = false) =>
        new(
            scanPerformed,
            scanSucceeded,
            false,
            latched,
            false,
            BonusRewardTargetKind.None,
            0,
            string.Empty,
            default,
            0f,
            0f,
            0,
            reason);

    private static BonusRewardTargetObservation CreateObservation(
        RewardTargetCandidate candidate,
        bool scanPerformed,
        bool scanSucceeded,
        bool candidateQualified,
        bool isLatched,
        bool latchStarted,
        int consecutiveObservations,
        string reason) =>
        new(
            scanPerformed,
            scanSucceeded,
            candidateQualified,
            isLatched,
            latchStarted,
            candidate.Kind,
            candidate.InstanceId,
            candidate.ObjectPath,
            candidate.Position,
            candidate.RelativeX,
            candidate.RelativeY,
            consecutiveObservations,
            reason);

    private readonly record struct RewardTargetCandidate(
        BonusRewardTargetKind Kind,
        int InstanceId,
        string ObjectPath,
        Vector3 Position,
        float RelativeX,
        float RelativeY);
}
