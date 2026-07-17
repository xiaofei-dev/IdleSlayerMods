using System;
using System.Collections.Generic;
using AutoAdventurer.Diagnostics;
using AutoAdventurer.World;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace AutoAdventurer.Quests;

internal sealed class QuestTravelService
{
    private const float ScanIntervalSeconds = 5f;
    private const float PortalRequestTimeoutSeconds = 60f;
    private const float CharacterSwitchStabilitySeconds = 0.5f;
    private const float EventCheckIntervalSeconds = 0.5f;

    private readonly QuestDiscoveryService discovery = new();
    private readonly QuestTargetResolver resolver = new();
    private readonly SoulFarmingResolver soulFarming = new();
    private readonly QuestCharacterService characters = new();
    private readonly WorldInterruptionService interruptions = new();
    private float nextScanTime;
    private float portalRequestedAt;
    private float lastAutomaticArrivalAt = -1f;
    private string requestedMapId = string.Empty;
    private string requestedQuestId = string.Empty;
    private string requestedQuestKey = string.Empty;
    private string lastSelection = string.Empty;
    private string lockedQuestId = string.Empty;
    private string lockedQuestKey = string.Empty;
    private string lockedQuestDisplayName = string.Empty;
    private string lockedTargetMapId = string.Empty;
    private bool lockedQuestIsDaily;
    private bool lockedQuestSuppressesRage;
    private bool lockedQuestNeedsCharacterSwitch;
    private float characterSwitchStableAt;
    private string characterWaitLoggedForQuestKey = string.Empty;
    private bool lastQuestSnapshotAvailable;
    private bool requestedForSoulFallback;
    private string lastSoulFallbackMapId = string.Empty;
    private int sessionDailyQuestsCompleted;
    private int sessionNormalQuestsCompleted;
    private float nextEventCheckTime;
    private bool activeRandomEvent;
    private string activeRandomEventName = string.Empty;
    private bool activeRandomBox;
    private string activeRandomBoxDescription = string.Empty;

    private bool HasMapActivityBlocker =>
        activeRandomEvent || activeRandomBox;

    internal bool SuppressAutomaticRage =>
        !HasMapActivityBlocker &&
        (!string.IsNullOrEmpty(requestedMapId) || lockedQuestSuppressesRage ||
         lockedQuestNeedsCharacterSwitch ||
         Time.unscaledTime < characterSwitchStableAt);

    internal void Tick(float now, bool enabled)
    {
        if (!enabled) return;

        try
        {
            UpdateRandomEventState(now);
            if (HasMapActivityBlocker)
            {
                if (!string.IsNullOrEmpty(requestedMapId))
                {
                    string blocker = activeRandomEvent
                        ? activeRandomEventName
                        : activeRandomBoxDescription;
                    AdventurerLog.QuestDebug(
                        $"Travel intent released: map activity blocker={blocker}; quest lock retained; automatic Rage suppression released.");
                    ClearPendingTravel();
                }
                return;
            }

            if (!string.IsNullOrEmpty(requestedMapId))
            {
                ObservePendingTravel(now);
                return;
            }

            if (now < nextScanTime) return;
            nextScanTime = now + ScanIntervalSeconds;
            if (!IsSupportedRunnerState() || !IsMapStable()) return;

            QuestTargetSelection selection = SelectOrTrackLockedQuest();
            if (selection == null)
            {
                if (lastQuestSnapshotAvailable &&
                    string.IsNullOrEmpty(lockedQuestKey))
                    TryHandleSoulFallback(now);
                return;
            }

            LogSelection(selection);
            if (!EnsureRequiredCharacter(selection, now)) return;
            BaseMap currentMap = MapController.instance?.selectedMap;
            if (currentMap != null && string.Equals(currentMap.name,
                    selection.MapId, StringComparison.Ordinal)) return;
            if (!MinimumStayElapsed(now)) return;
            if (!CanPreparePortalTravel()) return;

            BeginTravelIntent(selection);

            RageModeManager rage = RageModeManager.instance;
            if (rage != null &&
                rage.currentState == RageModeManager.RageModeStates.Execution)
            {
                AdventurerLog.User(
                    $"Quest travel is waiting for Rage Mode to end naturally before travelling to {selection.MapId}.");
                return;
            }

            TryRequestPortal(selection, now);
        }
        catch (Exception exception)
        {
            AdventurerLog.Error($"Quest Automation failed safely: {exception}");
            ClearPendingTravel();
            nextScanTime = now + ScanIntervalSeconds;
        }
    }

    private void ObservePendingTravel(float now)
    {
        if (!IsSupportedRunnerState() || !IsMapStable()) return;

        BaseMap current = MapController.instance?.selectedMap;
        if (current != null && string.Equals(current.name, requestedMapId,
                StringComparison.Ordinal))
        {
            string travelType = requestedForSoulFallback
                ? "Soul fallback travel"
                : "Quest travel";
            AdventurerLog.User($"{travelType} arrived in {requestedMapId}.");
            lastAutomaticArrivalAt = now;
            ClearPendingTravel();
            discovery.Reset();
            nextScanTime = now + ScanIntervalSeconds;
            return;
        }

        RageModeManager rage = RageModeManager.instance;
        if (portalRequestedAt <= 0f && rage != null &&
            rage.currentState == RageModeManager.RageModeStates.Execution)
            return;

        if (portalRequestedAt <= 0f)
        {
            if (requestedForSoulFallback)
            {
                ObserveSoulFallbackIntent(now, current);
                return;
            }

            // A pending quest travel can wait here for Rage to end naturally.
            // Keep live Daily reroll validation, but never rescan every frame.
            if (now < nextScanTime) return;
            nextScanTime = now + ScanIntervalSeconds;

            QuestTargetSelection selection = SelectOrTrackLockedQuest();
            if (!string.Equals(lockedQuestKey, requestedQuestKey,
                    StringComparison.Ordinal))
            {
                AdventurerLog.QuestDebug(
                    "Travel intent: locked quest changed; released the stale travel lock for recalculation.");
                ClearPendingTravel();
                return;
            }

            // A locked target may be temporarily unresolvable during the
            // Rage-to-Runner transition. Keep suppressing Rage and retry.
            if (selection == null) return;

            if (!EnsureRequiredCharacter(selection, now)) return;

            if (!string.Equals(selection.MapId, requestedMapId,
                    StringComparison.Ordinal))
            {
                AdventurerLog.QuestDebug(
                    "Travel intent: target map changed; released the stale travel lock for recalculation.");
                ClearPendingTravel();
                return;
            }

            TryRequestPortal(selection, now);
            return;
        }

        if (now - portalRequestedAt >= PortalRequestTimeoutSeconds)
        {
            string travelType = requestedForSoulFallback
                ? "Soul fallback travel"
                : "Quest travel";
            AdventurerLog.Warning(
                $"{travelType} to {requestedMapId} timed out; the request was released safely.");
            ClearPendingTravel();
        }
    }

    private QuestTargetSelection SelectOrTrackLockedQuest()
    {
        List<Quest> quests = discovery.SnapshotActiveIncomplete(lockedQuestKey);
        lastQuestSnapshotAvailable = discovery.LastSnapshotAvailable;
        if (!lastQuestSnapshotAvailable) return null;

        if (discovery.ActiveDailySetChanged &&
            !string.IsNullOrEmpty(lockedQuestKey))
        {
            bool completed = discovery.WatchedQuestCompleted;
            AdventurerLog.QuestDebug(
                $"Quest lock released: quest={lockedQuestDisplayName} [{lockedQuestId}]; " +
                $"reason={(completed ? "completed while the active Daily quest set changed" : "active Daily quest set changed")}; " +
                "a fresh selection will be made.");
            if (completed)
                RecordCompletedQuest();
            ClearQuestLock();
        }

        if (!string.IsNullOrEmpty(lockedQuestKey))
        {
            Quest locked = FindQuest(quests, lockedQuestKey);
            if (locked == null)
            {
                bool completed = discovery.WatchedQuestCompleted;
                string releaseReason = completed
                    ? "completed"
                    : "removed from the active list";
                AdventurerLog.QuestDebug(
                    $"Quest lock released: quest={lockedQuestDisplayName} [{lockedQuestId}]; reason={releaseReason}; rageSuppression={lockedQuestSuppressesRage}.");
                if (completed)
                    RecordCompletedQuest();
                ClearQuestLock();
            }
            else
            {
                // Keep the lock even when its target is temporarily
                // unreachable. Do not silently switch to a different quest.
                lockedQuestSuppressesRage =
                    locked.questType == QuestType.KillEnemiesWithArrows;
                QuestTargetSelection tracked = resolver.ResolveLocked(
                    locked, lockedTargetMapId);
                lockedQuestNeedsCharacterSwitch =
                    characters.RequiresSwitch(locked);
                if (tracked != null && !string.Equals(tracked.MapId,
                        lockedTargetMapId, StringComparison.Ordinal))
                {
                    AdventurerLog.QuestDebug(
                        $"Quest target map changed: quest={lockedQuestDisplayName} [{lockedQuestId}]; " +
                        $"previousMap={lockedTargetMapId}; newMap={tracked.MapId}; " +
                        "reason=previous map no longer has a valid unlocked target.");
                    lockedTargetMapId = tracked.MapId;
                }
                return tracked;
            }
        }

        QuestTargetSelection selection = resolver.Select(quests);
        if (selection == null) return null;

        lockedQuestId = selection.QuestId;
        lockedQuestKey = selection.LockKey;
        lockedQuestDisplayName = selection.QuestDisplayName;
        lockedTargetMapId = selection.MapId;
        lockedQuestIsDaily = IsDailyQuest(selection.Quest);
        lockedQuestSuppressesRage = selection.SuppressesAutomaticRage;
        lockedQuestNeedsCharacterSwitch = characters.RequiresSwitch(selection);
        lastSoulFallbackMapId = string.Empty;
        return selection;
    }

    private static Quest FindQuest(List<Quest> quests, string lockKey)
    {
        foreach (Quest quest in quests)
        {
            if (quest != null && string.Equals(
                    QuestTargetSelection.BuildLockKey(quest), lockKey,
                    StringComparison.Ordinal))
                return quest;
        }

        return null;
    }

    private void RecordCompletedQuest()
    {
        if (lockedQuestIsDaily)
            sessionDailyQuestsCompleted++;
        else
            sessionNormalQuestsCompleted++;

        int total = sessionDailyQuestsCompleted + sessionNormalQuestsCompleted;
        string message =
            $"Quest completed! Session total: {total} " +
            $"(Daily: {sessionDailyQuestsCompleted}, Normal: {sessionNormalQuestsCompleted}).";
        AdventurerLog.User(message);
        if (Plugin.Config.QuestCompletionNotifications.Value)
            Plugin.ModHelperInstance?.ShowNotification(
                $"Quests completed: {total}", true);
    }

    private static bool IsDailyQuest(Quest quest)
    {
        if (quest is DailyQuest) return true;
        string runtimeType = quest?.GetIl2CppType()?.Name ?? string.Empty;
        return string.Equals(runtimeType, nameof(DailyQuest),
            StringComparison.Ordinal);
    }

    internal void ObserveClaimedQuest(ClaimedQuestEvent claimed, bool enabled)
    {
        if (!enabled || string.IsNullOrEmpty(lockedQuestKey) ||
            !string.Equals(lockedQuestKey, claimed.LockKey,
                StringComparison.Ordinal)) return;

        AdventurerLog.QuestDebug(
            $"Quest lock released: quest={lockedQuestDisplayName} [{lockedQuestId}]; reason=claimed after completion; rageSuppression={lockedQuestSuppressesRage}.");
        RecordCompletedQuest();
        ClearQuestLock();
        ClearPendingTravel();
        nextScanTime = 0f;
    }

    private bool EnsureRequiredCharacter(
        QuestTargetSelection selection, float now)
    {
        if (selection?.RequiredCharacter == null)
        {
            lockedQuestNeedsCharacterSwitch = false;
            return now >= characterSwitchStableAt;
        }

        lockedQuestNeedsCharacterSwitch = characters.RequiresSwitch(selection);
        if (!lockedQuestNeedsCharacterSwitch)
            return now >= characterSwitchStableAt;

        if (GameState.current != GameStates.RunnerMode)
        {
            if (GameState.current == GameStates.RageMode && !string.Equals(
                    characterWaitLoggedForQuestKey, selection.LockKey,
                    StringComparison.Ordinal))
            {
                characterWaitLoggedForQuestKey = selection.LockKey;
                AdventurerLog.QuestDebug(
                    $"Character switch pending: quest={selection.QuestDisplayName} [{selection.QuestId}]; " +
                    $"requiredCharacter={selection.RequiredCharacterId}; waiting for Rage Mode to end naturally.");
            }
            return false;
        }

        if (!IsMapStable() || interruptions.TryGetBlocker(out _))
            return false;
        if (!characters.TryApply(selection)) return false;

        lockedQuestNeedsCharacterSwitch = false;
        characterWaitLoggedForQuestKey = string.Empty;
        characterSwitchStableAt = now + CharacterSwitchStabilitySeconds;
        return false;
    }

    private void BeginTravelIntent(QuestTargetSelection selection)
    {
        requestedForSoulFallback = false;
        requestedQuestId = selection.QuestId;
        requestedQuestKey = selection.LockKey;
        requestedMapId = selection.MapId;
        portalRequestedAt = 0f;
        AdventurerLog.QuestDebug(
            $"Travel lock acquired: quest={selection.QuestDisplayName} [{requestedQuestId}]; targetMap={requestedMapId}; automaticRageSuppressed=true.");
    }

    private void TryRequestPortal(QuestTargetSelection selection, float now)
    {
        PortalButton portal = PortalButton.instance;
        if (!CanClickPortalButton(portal)) return;
        if (interruptions.TryGetBlocker(out _)) return;
        if (selection.Map == null || !selection.Map.IsAvailable()) return;

        portalRequestedAt = now;
        portal.SpawnPortal(selection.Map, false);
        AdventurerLog.User(
            $"Quest Automation opened a portal to {selection.MapId} for {selection.EnemyId} ({selection.QuestId}).");
    }

    private void TryHandleSoulFallback(float now)
    {
        SoulFarmingSelection selection = soulFarming.SelectBestAvailable();
        if (selection == null) return;

        LogSoulFallbackSelection(selection);
        if (string.Equals(selection.CurrentMapId, selection.MapId,
                StringComparison.Ordinal)) return;
        if (!MinimumStayElapsed(now)) return;
        if (!CanPreparePortalTravel()) return;

        BeginSoulFallbackTravelIntent(selection);

        RageModeManager rage = RageModeManager.instance;
        if (rage != null &&
            rage.currentState == RageModeManager.RageModeStates.Execution)
        {
            AdventurerLog.QuestDebug(
                $"Soul fallback: waiting for Rage Mode to end naturally before travelling to {selection.MapId}.");
            return;
        }

        TryRequestSoulPortal(selection, now);
    }

    private void ObserveSoulFallbackIntent(float now, BaseMap currentMap)
    {
        QuestTargetSelection questSelection = SelectOrTrackLockedQuest();
        if (questSelection != null)
        {
            AdventurerLog.QuestDebug(
                $"Soul fallback: executable quest became available; cancelled travel to {requestedMapId}.");
            ClearPendingTravel();
            nextScanTime = now;
            return;
        }
        if (!lastQuestSnapshotAvailable || !string.IsNullOrEmpty(lockedQuestKey))
            return;

        SoulFarmingSelection selection = soulFarming.SelectBestAvailable();
        if (selection == null) return;
        if (currentMap != null && string.Equals(currentMap.name,
                selection.MapId, StringComparison.Ordinal))
        {
            AdventurerLog.QuestDebug(
                $"Soul fallback: current map is now optimal; cancelled travel to {requestedMapId}.");
            ClearPendingTravel();
            return;
        }
        if (!string.Equals(selection.MapId, requestedMapId,
                StringComparison.Ordinal))
        {
            AdventurerLog.QuestDebug(
                $"Soul fallback: best map changed from {requestedMapId} to {selection.MapId}; released the stale travel lock.");
            ClearPendingTravel();
            nextScanTime = now;
            return;
        }

        TryRequestSoulPortal(selection, now);
    }

    private void BeginSoulFallbackTravelIntent(SoulFarmingSelection selection)
    {
        requestedForSoulFallback = true;
        requestedQuestId = string.Empty;
        requestedQuestKey = string.Empty;
        requestedMapId = selection.MapId;
        portalRequestedAt = 0f;
        AdventurerLog.QuestDebug(
            $"Soul fallback travel lock acquired: targetMap={selection.MapId}; soulScore={selection.SoulScore:0.###}; automaticRageSuppressed=true.");
    }

    private void TryRequestSoulPortal(SoulFarmingSelection selection, float now)
    {
        PortalButton portal = PortalButton.instance;
        if (!CanClickPortalButton(portal)) return;
        if (interruptions.TryGetBlocker(out _)) return;
        if (selection.Map == null || !selection.Map.IsAvailable()) return;

        portalRequestedAt = now;
        portal.SpawnPortal(selection.Map, false);
        AdventurerLog.User(
            $"Quest Automation opened a soul-fallback portal to {selection.MapId} (soulScore={selection.SoulScore:0.###}).");
    }

    private void LogSoulFallbackSelection(SoulFarmingSelection selection)
    {
        if (string.Equals(selection.MapId, lastSoulFallbackMapId,
                StringComparison.Ordinal)) return;

        lastSoulFallbackMapId = selection.MapId;
        AdventurerLog.QuestDebug(
            $"Soul fallback selected: no executable kill quest; bestMap={selection.MapId}; " +
            $"bestSoulScore={selection.SoulScore:0.###}; currentMap={selection.CurrentMapId}; " +
            $"currentSoulScore={selection.CurrentSoulScore:0.###}.");
    }

    private bool CanPreparePortalTravel()
    {
        PortalButton portal = PortalButton.instance;
        return portal != null && portal.currentCd <= 0d &&
               !HasMapActivityBlocker &&
               !interruptions.TryGetBlocker(out _);
    }

    private void UpdateRandomEventState(float now)
    {
        if (now < nextEventCheckTime) return;
        nextEventCheckTime = now + EventCheckIntervalSeconds;

        bool wasActive = activeRandomEvent;
        string previousName = activeRandomEventName;
        bool boxWasActive = activeRandomBox;
        string previousBox = activeRandomBoxDescription;
        activeRandomEvent =
            interruptions.TryGetActiveRandomEvent(out string eventName);
        activeRandomEventName = activeRandomEvent ? eventName : string.Empty;
        activeRandomBox =
            interruptions.TryGetActiveRandomBox(out string boxDescription);
        activeRandomBoxDescription = activeRandomBox
            ? boxDescription
            : string.Empty;

        if (activeRandomEvent && (!wasActive || !string.Equals(previousName,
                activeRandomEventName, StringComparison.Ordinal)))
        {
            AdventurerLog.QuestDebug(
                $"Map event detected: event={activeRandomEventName}; dimension travel paused; automatic Rage suppression released.");
        }
        else if (!activeRandomEvent && wasActive)
        {
            AdventurerLog.QuestDebug(
                $"Map event ended: event={previousName}; quest and Portal conditions will be re-evaluated.");
            nextScanTime = 0f;
        }

        if (activeRandomBox && (!boxWasActive || !string.Equals(previousBox,
                activeRandomBoxDescription, StringComparison.Ordinal)))
        {
            AdventurerLog.QuestDebug(
                $"Random Box detected: box={activeRandomBoxDescription}; dimension travel paused while its result is determined; automatic Rage suppression released.");
        }
        else if (!activeRandomBox && boxWasActive && !activeRandomEvent)
        {
            AdventurerLog.QuestDebug(
                $"Random Box cleared: box={previousBox}; no active map event detected; quest and Portal conditions will be re-evaluated.");
            nextScanTime = 0f;
        }
    }

    private static bool CanClickPortalButton(PortalButton portal)
    {
        if (portal == null || portal.currentCd > 0d ||
            !portal.isActiveAndEnabled || portal.gameObject == null ||
            !portal.gameObject.activeInHierarchy)
            return false;

        Button button = portal.GetComponent<Button>();
        return button != null && button.isActiveAndEnabled &&
               button.gameObject.activeInHierarchy && button.interactable;
    }

    private void LogSelection(QuestTargetSelection selection)
    {
        string key = $"{selection.LockKey}|{selection.EnemyId}|{selection.MapId}";
        if (string.Equals(key, lastSelection, StringComparison.Ordinal)) return;

        lastSelection = key;
        AdventurerLog.QuestDebug(
            $"Quest target locked: quest={selection.QuestDisplayName} [{selection.QuestId}]; " +
            $"questType={selection.QuestTypeId}; objective={selection.ObjectiveId}; " +
            $"matchedEnemy={selection.EnemyId}; targetMap={selection.MapId}; " +
            $"progress={selection.CurrentKills:0.##}/{selection.RequiredKills:0.##}; " +
            $"remainingKills={selection.RemainingKills:0.##}; " +
            $"requiredCharacter={selection.RequiredCharacterId}; " +
            $"automaticRageSuppressed={lockedQuestSuppressesRage || lockedQuestNeedsCharacterSwitch}.");
    }

    private bool MinimumStayElapsed(float now)
    {
        if (lastAutomaticArrivalAt < 0f) return true;
        float seconds = Math.Max(0f,
            Plugin.Config.MinimumDimensionStayMinutes.Value) * 60f;
        return now - lastAutomaticArrivalAt >= seconds;
    }

    private static bool IsSupportedRunnerState() =>
        GameState.current is GameStates.RunnerMode or GameStates.RageMode;

    private static bool IsMapStable()
    {
        MapController maps = MapController.instance;
        return maps != null && maps.initialized && !maps.changingMap;
    }

    private void ClearPendingTravel()
    {
        requestedForSoulFallback = false;
        requestedMapId = string.Empty;
        requestedQuestId = string.Empty;
        requestedQuestKey = string.Empty;
        portalRequestedAt = 0f;
    }

    private void ClearQuestLock()
    {
        lockedQuestId = string.Empty;
        lockedQuestKey = string.Empty;
        lockedQuestDisplayName = string.Empty;
        lockedTargetMapId = string.Empty;
        lockedQuestIsDaily = false;
        lockedQuestSuppressesRage = false;
        lockedQuestNeedsCharacterSwitch = false;
        characterSwitchStableAt = 0f;
        characterWaitLoggedForQuestKey = string.Empty;
        lastSelection = string.Empty;
    }

    internal void Reset()
    {
        nextScanTime = 0f;
        lastAutomaticArrivalAt = -1f;
        lastQuestSnapshotAvailable = false;
        lastSoulFallbackMapId = string.Empty;
        nextEventCheckTime = 0f;
        activeRandomEvent = false;
        activeRandomEventName = string.Empty;
        activeRandomBox = false;
        activeRandomBoxDescription = string.Empty;
        ClearQuestLock();
        ClearPendingTravel();
        discovery.Reset();
        soulFarming.Reset();
    }

    internal void BeginSession()
    {
        sessionDailyQuestsCompleted = 0;
        sessionNormalQuestsCompleted = 0;
        Reset();
        AdventurerLog.QuestDebug(
            "Quest completion session started; counters reset to zero.");
    }

    internal void EndSession()
    {
        int total = sessionDailyQuestsCompleted +
                    sessionNormalQuestsCompleted;
        string message =
            $"Quest Automation session ended. Completed: {total} " +
            $"(Daily: {sessionDailyQuestsCompleted}, Normal: {sessionNormalQuestsCompleted}).";
        AdventurerLog.User(message);
        if (Plugin.Config.QuestCompletionNotifications.Value)
            Plugin.ModHelperInstance?.ShowNotification(
                $"Session quests completed: {total}", false);
    }

    internal void PauseForSceneTransition()
    {
        discovery.InvalidateSceneObjects();
        soulFarming.InvalidateSceneObjects();

        // If no automation Portal was spawned, another transition (Bonus
        // Stage, village, boss, etc.) interrupted preparation. Preserve any
        // task lock but release the travel lock and recalculate on return.
        if (!string.IsNullOrEmpty(requestedMapId) && portalRequestedAt <= 0f)
        {
            AdventurerLog.QuestDebug(
                "Travel intent interrupted by another scene; retained the quest lock and released only the travel lock for recalculation after return.");
            ClearPendingTravel();
        }
    }
}
