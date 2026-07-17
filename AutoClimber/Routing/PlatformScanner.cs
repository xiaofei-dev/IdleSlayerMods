using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoClimber;

internal sealed class PlatformScanner
{
    private const float PlatformScanWidth = 18f;
    // V5 keeps discovery broad enough to see rescue platforms below the
    // player and golden-bounce successors far above the player. A locked
    // target is tracked separately and no longer depends on this scan.
    private const float PlatformScanHeight = 150f;
    private const float PlatformScanCenterOffsetY = 35f;

    // Observed player horizontal movement speed.
    private const float PlayerHorizontalSpeed = 10f;

    // Fixed moving-platform speed.
    // Adjust this value if later logs show a different exact speed.
    private const float MovingPlatformSpeed = 3f;
    private const float HorizontalWorldLimit = 4.65f;

    // Pooled platform objects keep their InstanceId when reused. A large
    // vertical/type change or impossible horizontal teleport denotes a new
    // logical generation; a scan gap alone only clears motion/stability
    // history because boost targets can temporarily leave the scan box.
    private const float GenerationHeightTolerance = 1.50f;
    private const float GenerationObservationGapSeconds = 0.28f;
    private const float GenerationStabilitySeconds = 0.18f;
    private const int GenerationStabilityObservations = 2;
    private const int StationaryObservationsToClearMovement = 2;

    // Approximate vertical gravity observed in runtime logs.
    private const float GravityMagnitude = 50f;

    private const float MinimumTargetDeltaY = 0.35f;

    // Small tolerance for collision width and imperfect timing.
    private const float HorizontalReachTolerance = 0.45f;

    // Ignore extremely long predictions.
    private const float MaximumLandingTime = 3.5f;

    private readonly Dictionary<int, PlatformTrack> platformTracks =
        new Dictionary<int, PlatformTrack>();

    private readonly List<PlatformCandidate> candidates =
        new List<PlatformCandidate>();

    public IReadOnlyList<PlatformCandidate> Candidates =>
        candidates;

    public void Reset()
    {
        candidates.Clear();
        platformTracks.Clear();
    }

    public void Scan(
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        candidates.Clear();

        Vector2 scanCenter =
            new Vector2(
                playerPosition.x,
                playerPosition.y +
                PlatformScanCenterOffsetY
            );

        Vector2 scanSize =
            new Vector2(
                PlatformScanWidth,
                PlatformScanHeight
            );

        Collider2D[] colliders =
            Physics2D.OverlapBoxAll(
                scanCenter,
                scanSize,
                0f
            );

        HashSet<int> processedObjects =
            new HashSet<int>();

        foreach (Collider2D collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            GameObject gameObject =
                collider.gameObject;

            if (gameObject == null ||
                !gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!SafeCompareTag(
                    gameObject,
                    "Platform"))
            {
                continue;
            }

            int instanceId =
                gameObject.GetInstanceID();

            if (!processedObjects.Add(instanceId))
            {
                continue;
            }

            PlatformCandidate candidate =
                BuildCandidate(
                    gameObject,
                    collider,
                    playerPosition,
                    playerVelocity
                );

            candidates.Add(candidate);
        }
    }

    public PlatformCandidate FindBestCandidate()
    {
        PlatformCandidate bestCandidate = null;
        float bestScore = float.MinValue;

        foreach (PlatformCandidate candidate
                 in candidates)
        {
            if (!candidate.Reachable)
            {
                continue;
            }

            if (candidate.PriorityScore >
                bestScore)
            {
                bestScore =
                    candidate.PriorityScore;

                bestCandidate =
                    candidate;
            }
        }

        return bestCandidate;
    }

    public PlatformCandidate FindCandidateById(
        int instanceId)
    {
        foreach (PlatformCandidate candidate
                 in candidates)
        {
            if (candidate.InstanceId ==
                instanceId)
            {
                return candidate;
            }
        }

        return null;
    }

    public PlatformCandidate FindCandidate(
        int instanceId,
        int generation)
    {
        foreach (PlatformCandidate candidate
                 in candidates)
        {
            if (candidate.InstanceId == instanceId &&
                candidate.Generation == generation)
            {
                return candidate;
            }
        }

        return null;
    }

    private PlatformCandidate BuildCandidate(
        GameObject gameObject,
        Collider2D collider,
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        SpriteRenderer spriteRenderer =
            gameObject.GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer =
                gameObject
                    .GetComponentInChildren<SpriteRenderer>();
        }

        string spriteName =
            spriteRenderer != null &&
            spriteRenderer.sprite != null
                ? spriteRenderer.sprite.name
                : "unknown";

        PlatformType platformType =
            ClassifyPlatform(spriteName);

        Bounds bounds =
            collider.bounds;

        Vector2 currentPosition =
            new Vector2(
                bounds.center.x,
                bounds.center.y
            );

        Vector2 colliderSize =
            new Vector2(
                bounds.size.x,
                bounds.size.y
            );

        PlatformTrack track =
            UpdatePlatformTrack(
                gameObject.GetInstanceID(),
                currentPosition,
                platformType
            );

        float directionX =
            track.DirectionX;

        bool isMoving =
            Mathf.Abs(track.VelocityX) > 0.50f;

        float platformVelocityX =
            isMoving
                ? track.VelocityX
                : 0f;

        float deltaY =
            currentPosition.y -
            playerPosition.y;

        PlatformCandidate candidate =
            new PlatformCandidate
            {
                GameObject = gameObject,
                Collider = collider,
                InstanceId =
                    gameObject.GetInstanceID(),

                Generation = track.Generation,

                Type = platformType,
                SpriteName = spriteName,

                CurrentPosition =
                    currentPosition,

                ColliderSize =
                    colliderSize,

                IsMoving = isMoving,
                MovementDirectionX =
                    directionX,

                PlatformVelocityX =
                    platformVelocityX,

                ObservationAge =
                    Mathf.Max(
                        0f,
                        Time.time -
                            track.GenerationObservedSince
                    ),

                ConsecutiveObservations =
                    track.ConsecutiveObservations,

                GenerationStable =
                    track.ConsecutiveObservations >=
                        GenerationStabilityObservations ||
                    Time.time -
                        track.GenerationObservedSince >=
                        GenerationStabilitySeconds,

                RecentlyRecycled =
                    track.WasRecycled &&
                    Time.time -
                        track.GenerationChangedAt < 0.45f,

                LifecycleHazardPenalty = 0f,

                DeltaY = deltaY,

                EstimatedLandingTime = -1f,
                PredictedLandingX =
                    currentPosition.x,

                RequiredHorizontalDistance =
                    float.PositiveInfinity,

                MaximumHorizontalReach = 0f,

                VerticallyReachable = false,
                HorizontallyReachable = false,
                Reachable = false,

                PriorityScore =
                    float.MinValue,

                RejectionReason = ""
            };

        EvaluateCandidate(
            candidate,
            playerPosition,
            playerVelocity
        );

        return candidate;
    }

    private PlatformTrack UpdatePlatformTrack(
        int instanceId,
        Vector2 currentPosition,
        PlatformType platformType)
    {
        float currentTime =
            Time.time;

        if (!platformTracks.TryGetValue(
                instanceId,
                out PlatformTrack track))
        {
            track =
                new PlatformTrack
                {
                    InstanceId = instanceId,
                    Generation = 1,
                    LastObservedX = currentPosition.x,
                    LastObservedY = currentPosition.y,
                    LastObservedTime = currentTime,
                    GenerationObservedSince = currentTime,
                    GenerationChangedAt = currentTime,
                    LastObservedType = platformType,
                    ConsecutiveObservations = 1,
                    StationaryObservations = 0,
                    DirectionX = 0f,
                    VelocityX = 0f,
                    HasPreviousObservation = true,
                    WasRecycled = false
                };

            platformTracks.Add(
                instanceId,
                track
            );

            return track;
        }

        float deltaTime =
            currentTime -
            track.LastObservedTime;

        float deltaX =
            currentPosition.x -
            track.LastObservedX;

        bool horizontalTeleport =
            deltaTime > 0.001f &&
            Mathf.Abs(deltaX) >
                MovingPlatformSpeed *
                    deltaTime +
                0.20f;

        bool generationChanged =
            Mathf.Abs(
                currentPosition.y -
                track.LastObservedY
            ) > GenerationHeightTolerance ||
            platformType != track.LastObservedType ||
            horizontalTeleport;

        bool observationSequenceReset =
            !generationChanged &&
            deltaTime >
                GenerationObservationGapSeconds;

        if (generationChanged)
        {
            track.Generation++;
            track.GenerationObservedSince = currentTime;
            track.GenerationChangedAt = currentTime;
            track.ConsecutiveObservations = 1;
            track.StationaryObservations = 0;
            track.DirectionX = 0f;
            track.VelocityX = 0f;
            track.HasPreviousObservation = true;
            track.WasRecycled = true;
        }
        else if (observationSequenceReset)
        {
            // The broad scanner can temporarily lose a still-live target
            // below its box during a boost jump. Keep the logical generation
            // when height/type still match, but discard stale motion history.
            track.GenerationObservedSince = currentTime;
            track.ConsecutiveObservations = 1;
            track.StationaryObservations = 0;
            track.DirectionX = 0f;
            track.VelocityX = 0f;
            track.HasPreviousObservation = true;
            track.WasRecycled = false;
        }
        else
        {
            track.ConsecutiveObservations++;

            if (track.HasPreviousObservation &&
                deltaTime > 0.001f &&
                Mathf.Abs(deltaX) > 0.015f)
            {
                float measuredVelocity =
                    deltaX / deltaTime;

                float direction =
                    Mathf.Sign(measuredVelocity);

                track.DirectionX = direction;
                track.StationaryObservations = 0;

                if (Mathf.Abs(measuredVelocity) >= 0.50f &&
                    Mathf.Abs(measuredVelocity) <= 6.00f)
                {
                    track.VelocityX =
                        Mathf.Abs(track.VelocityX) >= 0.50f &&
                        Mathf.Sign(track.VelocityX) ==
                            direction
                            ? Mathf.Lerp(
                                track.VelocityX,
                                measuredVelocity,
                                0.45f
                            )
                            : measuredVelocity;
                }
                else
                {
                    track.VelocityX =
                        direction * MovingPlatformSpeed;
                }
            }
            else
            {
                track.StationaryObservations++;

                if (track.StationaryObservations >=
                    StationaryObservationsToClearMovement)
                {
                    track.DirectionX = 0f;
                    track.VelocityX = 0f;
                }
            }

            track.HasPreviousObservation = true;
        }

        track.LastObservedX =
            currentPosition.x;

        track.LastObservedY =
            currentPosition.y;

        track.LastObservedTime =
            currentTime;

        track.LastObservedType =
            platformType;

        return track;
    }

    private void EvaluateCandidate(
        PlatformCandidate candidate,
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        if (candidate.Type ==
            PlatformType.Fake)
        {
            candidate.RejectionReason =
                "Fake platform";

            return;
        }

        if (candidate.Type ==
            PlatformType.Unknown)
        {
            candidate.RejectionReason =
                "Unknown platform type";

            return;
        }

        if (candidate.DeltaY <
            MinimumTargetDeltaY)
        {
            candidate.RejectionReason =
                "Platform is not above player";

            return;
        }

        float landingTime =
            CalculateDescendingLandingTime(
                playerVelocity.y,
                candidate.DeltaY
            );

        if (landingTime <= 0f ||
            landingTime >
            MaximumLandingTime)
        {
            candidate.RejectionReason =
                "No valid vertical landing time";

            return;
        }

        candidate.VerticallyReachable = true;
        candidate.EstimatedLandingTime =
            landingTime;

        float predictedLandingX =
            ReflectHorizontalPosition(
                candidate.CurrentPosition.x,
                candidate.PlatformVelocityX,
                landingTime
            );

        candidate.PredictedLandingX =
            predictedLandingX;

        float requiredHorizontalDistance =
            Mathf.Abs(
                predictedLandingX -
                playerPosition.x
            );

        candidate.RequiredHorizontalDistance =
            requiredHorizontalDistance;

        float maximumHorizontalReach =
            PlayerHorizontalSpeed *
            landingTime +
            HorizontalReachTolerance;

        candidate.MaximumHorizontalReach =
            maximumHorizontalReach;

        if (requiredHorizontalDistance >
            maximumHorizontalReach)
        {
            candidate.RejectionReason =
                "Not horizontally reachable";

            return;
        }

        candidate.HorizontallyReachable = true;
        candidate.Reachable = true;

        candidate.PriorityScore =
            CalculatePriorityScore(candidate);

        candidate.RejectionReason = "";
    }

    private float CalculateDescendingLandingTime(
        float currentVelocityY,
        float deltaY)
    {
        // deltaY = velocityY * t - 0.5 * gravity * t^2
        //
        // 0.5 * gravity * t^2
        // - velocityY * t
        // + deltaY = 0

        float discriminant =
            currentVelocityY *
            currentVelocityY -
            2f *
            GravityMagnitude *
            deltaY;

        if (discriminant < 0f)
        {
            return -1f;
        }

        float squareRoot =
            Mathf.Sqrt(discriminant);

        float firstRoot =
            (currentVelocityY -
             squareRoot) /
            GravityMagnitude;

        float secondRoot =
            (currentVelocityY +
             squareRoot) /
            GravityMagnitude;

        // The larger positive root is the descending intersection.
        float landingTime =
            Mathf.Max(
                firstRoot,
                secondRoot
            );

        if (landingTime <= 0f)
        {
            return -1f;
        }

        return landingTime;
    }

    private float CalculatePriorityScore(
        PlatformCandidate candidate)
    {
        float typePriority =
            GetTypePriority(candidate.Type);

        // Platform type is the hard priority:
        // Strong > Normal > Breakable.
        //
        // Within the same platform type, prefer:
        // 1. Higher platforms
        // 2. Shorter horizontal movement
        float verticalValue =
            candidate.DeltaY * 100f;

        float horizontalPenalty =
            candidate
                .RequiredHorizontalDistance *
            10f;

        return
            typePriority +
            verticalValue -
            horizontalPenalty;
    }

    private float ReflectHorizontalPosition(
        float currentX,
        float velocityX,
        float time)
    {
        float minimumX = -HorizontalWorldLimit;
        float maximumX = HorizontalWorldLimit;
        float width = maximumX - minimumX;

        float raw =
            currentX - minimumX +
            velocityX * Mathf.Max(0f, time);

        float period = width * 2f;
        raw %= period;

        if (raw < 0f)
        {
            raw += period;
        }

        if (raw > width)
        {
            raw = period - raw;
        }

        return minimumX + raw;
    }

    private float GetTypePriority(
        PlatformType type)
    {
        switch (type)
        {
            case PlatformType.Golden:
                return 400000f;

            case PlatformType.Strong:
                return 300000f;

            case PlatformType.Normal:
                return 200000f;

            case PlatformType.Breakable:
                return 100000f;

            default:
                return -100000f;
        }
    }

    private PlatformType ClassifyPlatform(
        string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName))
        {
            return PlatformType.Unknown;
        }

        if (spriteName.Equals(
                "fake",
                StringComparison.OrdinalIgnoreCase))
        {
            return PlatformType.Fake;
        }

        if (spriteName.Equals(
                "breakable-platform",
                StringComparison.OrdinalIgnoreCase))
        {
            return PlatformType.Breakable;
        }

        if (spriteName.StartsWith(
                "super-platform-sheet_",
                StringComparison.OrdinalIgnoreCase))
        {
            return PlatformType.Strong;
        }

        // Golden boost platforms use a separate animated sprite sequence.
        // They are real high-power route nodes, not unknown decoration.
        if (spriteName.StartsWith(
                "super-platform-sheet-golden_",
                StringComparison.OrdinalIgnoreCase))
        {
            return PlatformType.Golden;
        }

        if (spriteName.Equals(
                "platform",
                StringComparison.OrdinalIgnoreCase))
        {
            return PlatformType.Normal;
        }

        return PlatformType.Unknown;
    }

    private bool SafeCompareTag(
        GameObject gameObject,
        string expectedTag)
    {
        try
        {
            return gameObject.CompareTag(
                expectedTag
            );
        }
        catch
        {
            try
            {
                return gameObject.tag ==
                       expectedTag;
            }
            catch
            {
                return false;
            }
        }
    }
}
