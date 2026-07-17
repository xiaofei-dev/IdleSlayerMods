using System;
using System.Collections.Generic;
using AutoClimber.Diagnostics;
using UnityEngine;

namespace AutoClimber;

// V5 deliberately treats PlatformScanner as a sensor only. Every route mode
// uses this single kinematic model, including normal jumps, boost jumps,
// golden jumps and late descending rescues.
internal sealed class ClimbRoutePlanner
{
    private const float GravityMagnitude = 49.05f;
    private const float HorizontalMoveSpeed = 10.00f;
    private const float HorizontalWorldLimit = 4.65f;

    private const float NormalBounceVelocity = 25.00f;
    private const float StrongBounceVelocity = 60.00f;
    private const float GoldenBounceVelocity = 90.00f;

    private const float NormalSafeHalfWidth = 0.72f;
    private const float StrongSafeHalfWidth = 0.76f;
    private const float GoldenSafeHalfWidth = 0.78f;
    private const float BreakableSafeHalfWidth = 0.55f;

    private const float LowAltitudeMinimumProgress = 0.35f;
    private const float HighAltitudeMinimumProgress = 0.10f;
    private const float MaximumEmergencyDrop = 28.00f;
    private const float MaximumLandingTime = 4.50f;
    // A mathematical intersection at the apex is not a reliable collision.
    // Require meaningful downward speed when a route is first selected.
    private const float MinimumStaticDescendingSpeed = 8.00f;
    private const float MinimumRiskyDescendingSpeed = 10.00f;

    private const float HorizontalReachTolerance = 0.35f;
    private const float HighAltitudeAbsoluteReachRatio = 0.98f;
    private const float LowAltitudeAbsoluteReachRatio = 1.05f;
    private const float EmergencyAbsoluteReachRatio = 1.08f;

    // Runtime evidence shows a sharp lifecycle boundary: successful locked
    // landings stay at or below roughly 8.6 world units beneath the apex,
    // while failed pooled targets begin around 8.7. Treat 7.25 as preferred
    // and 8.50 as the safe upper tier. Risky routes remain available only as
    // a last resort so unique layouts are not filtered out.
    private const float PreferredApexOvershoot = 7.25f;
    private const float MaximumSafeApexOvershoot = 8.50f;
    private const float PreferredLifecycleLandingTime = 2.20f;
    private const float MaximumSafeLifecycleLandingTime = 2.65f;
    private const float MaximumSafeFutureLifecycleRisk = 1050f;

    private const float LowAltitudePreferredWorldLimit = 3.50f;
    private const float HighAltitudePreferredWorldLimit = 3.10f;
    private const float LowAltitudeAbsoluteWorldLimit = 4.35f;
    private const float HighAltitudeAbsoluteWorldLimit = 4.10f;

    internal V5RouteDecision SelectBest(
        IReadOnlyList<PlatformCandidate> candidates,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        float lastBounceY,
        bool highAltitude,
        bool allowEmergency,
        int excludedTargetId,
        int excludedTargetGeneration,
        int failedTargetId,
        int failedTargetGeneration,
        bool failedTargetBlocked,
        float finishHeight,
        out int forwardFeasibleCount)
    {
        List<V5RouteDecision> forward =
            new List<V5RouteDecision>();

        List<V5RouteDecision> emergency =
            new List<V5RouteDecision>();

        foreach (PlatformCandidate candidate in candidates)
        {
            if (!IsCandidateUsable(
                    candidate,
                    excludedTargetId,
                    excludedTargetGeneration,
                    failedTargetId,
                    failedTargetGeneration,
                    failedTargetBlocked))
            {
                continue;
            }

            V5RouteDecision decision = Evaluate(
                candidate,
                candidates,
                playerPosition,
                playerVelocity,
                lastBounceY,
                highAltitude,
                false,
                finishHeight
            );

            if (decision.Feasible)
            {
                forward.Add(decision);
                continue;
            }

            if (!allowEmergency)
            {
                continue;
            }

            decision = Evaluate(
                candidate,
                candidates,
                playerPosition,
                playerVelocity,
                lastBounceY,
                highAltitude,
                true,
                finishHeight
            );

            if (decision.Feasible &&
                decision.IsEmergency)
            {
                emergency.Add(decision);
            }
        }

        forwardFeasibleCount = forward.Count;

        ApplySurvivalTiers(
            forward,
            highAltitude
        );

        ApplySurvivalTiers(
            emergency,
            highAltitude
        );

        V5RouteDecision best = FindHighestScore(forward);

        if (best != null)
        {
            return best;
        }

        if (!allowEmergency)
        {
            return null;
        }

        return FindHighestScore(emergency);
    }

    internal V5RouteDecision EvaluatePersistentTarget(
        PlatformCandidate candidate,
        IReadOnlyList<PlatformCandidate> candidates,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        float lastBounceY,
        bool highAltitude,
        bool targetWasEmergency,
        float finishHeight)
    {
        return Evaluate(
            candidate,
            candidates,
            playerPosition,
            playerVelocity,
            lastBounceY,
            highAltitude,
            targetWasEmergency,
            finishHeight,
            true
        );
    }

    internal V5RouteDecision EvaluateLateReplanCandidate(
        PlatformCandidate candidate,
        IReadOnlyList<PlatformCandidate> candidates,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        float lastBounceY,
        bool highAltitude,
        bool allowEmergency,
        float finishHeight)
    {
        // This is a newly selected rescue route, not an already committed
        // target. Keep the V5.1 minimum descending-impact checks active and
        // respect the caller's timing gate before allowing a controlled drop.
        return Evaluate(
            candidate,
            candidates,
            playerPosition,
            playerVelocity,
            lastBounceY,
            highAltitude,
            allowEmergency,
            finishHeight,
            false
        );
    }

    internal float PredictPlatformX(
        PlatformCandidate candidate,
        float secondsFromNow)
    {
        if (candidate == null)
        {
            return 0f;
        }

        if (!candidate.IsMoving ||
            Mathf.Abs(candidate.PlatformVelocityX) < 0.01f ||
            secondsFromNow <= 0f)
        {
            return Mathf.Clamp(
                candidate.CurrentPosition.x,
                -HorizontalWorldLimit,
                HorizontalWorldLimit
            );
        }

        return ReflectHorizontalPosition(
            candidate.CurrentPosition.x,
            candidate.PlatformVelocityX,
            secondsFromNow
        );
    }

    internal float GetExpectedBounceVelocity(
        PlatformCandidate candidate)
    {
        if (candidate == null)
        {
            return NormalBounceVelocity;
        }

        if (candidate.Type == PlatformType.Golden ||
            (!string.IsNullOrEmpty(candidate.SpriteName) &&
             candidate.SpriteName.IndexOf(
                 "golden",
                 StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return GoldenBounceVelocity;
        }

        return candidate.Type == PlatformType.Strong
            ? StrongBounceVelocity
            : NormalBounceVelocity;
    }

    private V5RouteDecision Evaluate(
        PlatformCandidate candidate,
        IReadOnlyList<PlatformCandidate> candidates,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        float lastBounceY,
        bool highAltitude,
        bool allowEmergency,
        float finishHeight,
        bool persistentTarget = false)
    {
        V5RouteDecision decision =
            new V5RouteDecision
            {
                Candidate = candidate,
                RejectionReason = "Unknown"
            };

        if (candidate == null ||
            !IsRealPlatformType(candidate.Type))
        {
            decision.RejectionReason = "InvalidPlatform";
            return decision;
        }

        float minimumProgress = highAltitude
            ? HighAltitudeMinimumProgress
            : LowAltitudeMinimumProgress;

        float progress =
            candidate.CurrentPosition.y -
            lastBounceY;

        bool forwardTarget =
            progress >= minimumProgress;

        bool emergencyTarget =
            !forwardTarget;

        if (emergencyTarget &&
            !allowEmergency &&
            !persistentTarget)
        {
            decision.RejectionReason = "NoForwardProgress";
            return decision;
        }

        if (emergencyTarget)
        {
            float verticalDrop =
                playerPosition.y -
                candidate.CurrentPosition.y;

            if (verticalDrop < -0.50f ||
                verticalDrop > MaximumEmergencyDrop)
            {
                decision.RejectionReason = "EmergencyHeightOutsideRange";
                return decision;
            }
        }

        float landingTime;
        float descendingSpeed;

        if (!TryCalculateDescendingLandingTime(
                playerPosition.y,
                playerVelocity.y,
                candidate.CurrentPosition.y,
                out landingTime,
                out descendingSpeed))
        {
            decision.RejectionReason = "NoDescendingIntersection";
            return decision;
        }

        bool finishApproach =
            IsFinishApproach(
                candidate.CurrentPosition.y,
                finishHeight
            );

        decision.IsFinishApproach =
            finishApproach;

        float minimumDescendingSpeed =
            candidate.IsMoving ||
            candidate.Type == PlatformType.Breakable
                ? MinimumRiskyDescendingSpeed
                : MinimumStaticDescendingSpeed;

        if (!persistentTarget &&
            descendingSpeed < minimumDescendingSpeed)
        {
            decision.RejectionReason =
                "InsufficientDescendingSpeed";
            return decision;
        }

        float landingX =
            PredictPlatformX(candidate, landingTime);

        float movementUncertainty =
            GetMovementUncertainty(
                candidate,
                landingTime
            );

        float safeHalfWidth =
            Mathf.Max(
                0.30f,
                GetBaseSafeHalfWidth(candidate.Type) -
                    movementUncertainty
            );

        float maximumReach =
            HorizontalMoveSpeed *
                landingTime +
            HorizontalReachTolerance;

        if (maximumReach <= 0.01f)
        {
            decision.RejectionReason = "NoHorizontalTime";
            return decision;
        }

        float maximumReachRatio = emergencyTarget
            ? EmergencyAbsoluteReachRatio
            : highAltitude
                ? HighAltitudeAbsoluteReachRatio
                : LowAltitudeAbsoluteReachRatio;

        bool inwardLandingPlanned =
            !emergencyTarget &&
            ShouldPlanInwardLanding(
                landingX,
                highAltitude
            );

        float plannedLandingX;
        float controlHalfWidth;
        float requiredDistance;

        CalculateControlLandingInterval(
            landingX,
            safeHalfWidth,
            playerPosition.x,
            inwardLandingPlanned,
            out plannedLandingX,
            out controlHalfWidth,
            out requiredDistance
        );

        float reachRatio =
            requiredDistance /
            maximumReach;

        // An inward half-platform landing is preferred at the boundary. If it
        // is not physically reachable, retain the full platform only as a
        // last-resort route instead of turning a saveable jump into Target=None.
        if (reachRatio > maximumReachRatio &&
            inwardLandingPlanned)
        {
            inwardLandingPlanned = false;

            CalculateControlLandingInterval(
                landingX,
                safeHalfWidth,
                playerPosition.x,
                false,
                out plannedLandingX,
                out controlHalfWidth,
                out requiredDistance
            );

            reachRatio =
                requiredDistance /
                maximumReach;
        }

        if (reachRatio > maximumReachRatio)
        {
            decision.RejectionReason = "HorizontalReachExceeded";
            return decision;
        }

        if (Mathf.Abs(landingX) >
            HorizontalWorldLimit + 0.01f)
        {
            decision.RejectionReason = "OutsideWorldBounds";
            return decision;
        }

        decision.Feasible = true;
        decision.IsEmergency = emergencyTarget;
        decision.LandingTime = landingTime;
        decision.LandingX = landingX;
        decision.DescendingSpeed =
            descendingSpeed;
        decision.EffectiveSafeHalfWidth = safeHalfWidth;
        decision.RequiredHorizontalDistance = requiredDistance;
        decision.MaximumHorizontalReach = maximumReach;
        decision.ReachRatio = reachRatio;
        decision.LandingMargin =
            maximumReach -
            requiredDistance;

        decision.CenterwardLandingX =
            plannedLandingX;

        decision.ControlHalfWidth =
            controlHalfWidth;

        decision.InwardLandingPlanned =
            inwardLandingPlanned;

        decision.EdgeReserve =
            HorizontalWorldLimit -
            Mathf.Abs(
                decision.CenterwardLandingX
            );

        // Retention risk is about how long the platform must remain alive
        // from now until contact. Once the player is descending, the historic
        // apex is no longer future exposure and must not make a near-contact
        // rescue look unsafe.
        decision.PredictedApexY =
            playerPosition.y +
            (playerVelocity.y > 0f
                ? playerVelocity.y * playerVelocity.y /
                    (2f * GravityMagnitude)
                : 0f);

        decision.ApexOvershoot =
            Mathf.Max(
                0f,
                decision.PredictedApexY -
                    candidate.CurrentPosition.y
            );

        decision.GenerationStable =
            candidate.GenerationStable;

        decision.LifecycleRisk =
            GetLifecycleRisk(
                candidate,
                landingTime,
                decision.ApexOvershoot,
                landingTime
            );

        if (finishApproach)
        {
            decision.LifecycleRisk = 0f;
        }

        EvaluateRouteContinuity(
            decision,
            candidates,
            highAltitude,
            finishHeight
        );

        float edgeStart = highAltitude
            ? 3.10f
            : 3.50f;

        float edgeExposure =
            Mathf.Max(
                0f,
                Mathf.Abs(decision.CenterwardLandingX) -
                    edgeStart
            );

        decision.EdgeExposure = edgeExposure;

        decision.RetentionSafe =
            IsRetentionSafeDecision(decision);

        float preferredWorldLimit = highAltitude
            ? HighAltitudePreferredWorldLimit
            : LowAltitudePreferredWorldLimit;

        float absoluteWorldLimit = highAltitude
            ? HighAltitudeAbsoluteWorldLimit
            : LowAltitudeAbsoluteWorldLimit;

        float absoluteCenterwardLandingX =
            Mathf.Abs(
                decision.CenterwardLandingX
            );

        decision.EdgeSafe =
            finishApproach ||
            absoluteCenterwardLandingX <=
                preferredWorldLimit ||
            (absoluteCenterwardLandingX <=
                absoluteWorldLimit &&
             decision.InwardLandingPlanned &&
             decision.CenterReturnSuccessorCount > 0);

        decision.IsEdgeLastResort =
            !decision.EdgeSafe;

        float safetyScore =
            (1f - Mathf.Clamp01(reachRatio)) *
                520f +
            Mathf.Min(decision.LandingMargin, 3f) *
                35f;

        float scoredProgress =
            Mathf.Clamp(
                progress,
                emergencyTarget ? -8f : 0f,
                24f
            );

        float progressScore =
            scoredProgress *
            (emergencyTarget ? 22f : 60f);

        float emergencyPenalty = emergencyTarget
            ? Mathf.Abs(
                  playerPosition.y -
                  candidate.CurrentPosition.y
              ) * 22f + 120f
            : 0f;

        float movingPenalty =
            movementUncertainty * 170f;

        float longFlightPenalty =
            Mathf.Max(
                0f,
                landingTime - 2.20f
            ) * 320f;

        if (candidate.Type ==
                PlatformType.Breakable &&
            landingTime > 1.80f)
        {
            longFlightPenalty +=
                (landingTime - 1.80f) *
                    260f;
        }

        decision.Score =
            progressScore +
            safetyScore +
            GetTypeBonus(candidate.Type) +
            decision.RouteScore -
            edgeExposure * 520f -
            Mathf.Abs(decision.CenterwardLandingX) * 12f -
            emergencyPenalty -
            movingPenalty -
            longFlightPenalty -
            decision.LifecycleRisk;

        decision.RejectionReason = "";
        return decision;
    }

    private void EvaluateRouteContinuity(
        V5RouteDecision sourceDecision,
        IReadOnlyList<PlatformCandidate> candidates,
        bool highAltitude,
        float finishHeight)
    {
        PlatformCandidate source =
            sourceDecision.Candidate;

        if (IsFinishApproach(
                source.CurrentPosition.y,
                finishHeight))
        {
            sourceDecision.RouteScore = 650f;
            return;
        }

        float launchVelocity =
            GetExpectedBounceVelocity(source);

        int successorCount = 0;
        int centerReturnSuccessorCount = 0;
        float bestSuccessorScore =
            float.MinValue;

        foreach (PlatformCandidate successor in candidates)
        {
            if (successor == null ||
                successor.InstanceId ==
                    source.InstanceId ||
                !IsRealPlatformType(successor.Type))
            {
                continue;
            }

            float successorTime;
            float successorX;
            float successorRatio;
            float successorLifecycleRisk;

            if (!TryEvaluateFutureLeg(
                    source.CurrentPosition.y,
                    sourceDecision.CenterwardLandingX,
                    sourceDecision.LandingTime,
                    launchVelocity,
                    successor,
                    highAltitude,
                    out successorTime,
                    out successorX,
                    out successorRatio,
                    out successorLifecycleRisk))
            {
                continue;
            }

            successorCount++;

            float preferredWorldLimit =
                highAltitude
                    ? HighAltitudePreferredWorldLimit
                    : LowAltitudePreferredWorldLimit;

            bool entersPreferredLane =
                Mathf.Abs(successorX) <=
                    preferredWorldLimit;

            bool movesTowardCenter =
                Mathf.Abs(successorX) <=
                    Mathf.Abs(sourceDecision.CenterwardLandingX) -
                        0.80f;

            if (entersPreferredLane ||
                movesTowardCenter)
            {
                centerReturnSuccessorCount++;
            }

            float gain =
                successor.CurrentPosition.y -
                source.CurrentPosition.y;

            float successorScore =
                gain * 10f +
                (1f - Mathf.Clamp01(successorRatio)) *
                    60f +
                GetTypeBonus(successor.Type) * 0.10f -
                successorLifecycleRisk * 0.08f -
                Mathf.Max(
                    0f,
                    Mathf.Abs(successorX) - 3.10f
                ) * 60f;

            bestSuccessorScore =
                Mathf.Max(
                    bestSuccessorScore,
                    successorScore
                );
        }

        sourceDecision.SuccessorCount =
            successorCount;

        sourceDecision.CenterReturnSuccessorCount =
            centerReturnSuccessorCount;

        if (successorCount == 0)
        {
            bool sourceIsBoost =
                source.Type == PlatformType.Golden ||
                source.Type == PlatformType.Strong;

            sourceDecision.RouteScore =
                sourceIsBoost
                    ? -20f
                    : highAltitude
                        ? -100f
                        : -60f;
            return;
        }

        sourceDecision.RouteScore =
            Mathf.Min(successorCount, 3) * 25f +
            Mathf.Min(centerReturnSuccessorCount, 1) * 35f +
            Mathf.Clamp(bestSuccessorScore, -80f, 80f) * 0.35f;
    }

    private bool TryEvaluateFutureLeg(
        float sourceY,
        float sourceX,
        float elapsedBeforeLaunch,
        float launchVelocity,
        PlatformCandidate target,
        bool highAltitude,
        out float landingTime,
        out float landingX,
        out float reachRatio,
        out float lifecycleRisk)
    {
        landingTime = -1f;
        landingX = 0f;
        reachRatio = float.MaxValue;
        lifecycleRisk = float.MaxValue;

        float minimumProgress = highAltitude
            ? HighAltitudeMinimumProgress
            : LowAltitudeMinimumProgress;

        if (target.CurrentPosition.y <
            sourceY + minimumProgress)
        {
            return false;
        }

        float descendingSpeed;

        if (!TryCalculateDescendingLandingTime(
                sourceY,
                launchVelocity,
                target.CurrentPosition.y,
                out landingTime,
                out descendingSpeed))
        {
            return false;
        }

        if (landingTime >
            MaximumSafeLifecycleLandingTime)
        {
            return false;
        }

        float minimumDescendingSpeed =
            target.IsMoving ||
            target.Type == PlatformType.Breakable
                ? MinimumRiskyDescendingSpeed
                : MinimumStaticDescendingSpeed;

        if (descendingSpeed <
            minimumDescendingSpeed)
        {
            return false;
        }

        float platformCenterX = PredictPlatformX(
            target,
            elapsedBeforeLaunch +
                landingTime
        );

        float safeHalfWidth =
            Mathf.Max(
                0.30f,
                GetBaseSafeHalfWidth(target.Type) -
                    GetMovementUncertainty(
                        target,
                        elapsedBeforeLaunch +
                            landingTime
                    )
            );

        float maximumReach =
            HorizontalMoveSpeed *
                landingTime +
            HorizontalReachTolerance;

        if (maximumReach <= 0.01f)
        {
            return false;
        }

        float maximumRatio = highAltitude
            ? 0.95f
            : 1.02f;

        float ignoredControlHalfWidth;
        float requiredDistance;
        bool inwardLandingPlanned =
            ShouldPlanInwardLanding(
                platformCenterX,
                highAltitude
            );

        CalculateControlLandingInterval(
            platformCenterX,
            safeHalfWidth,
            sourceX,
            inwardLandingPlanned,
            out landingX,
            out ignoredControlHalfWidth,
            out requiredDistance
        );

        reachRatio =
            requiredDistance /
            maximumReach;

        if (reachRatio > maximumRatio &&
            inwardLandingPlanned)
        {
            CalculateControlLandingInterval(
                platformCenterX,
                safeHalfWidth,
                sourceX,
                false,
                out landingX,
                out ignoredControlHalfWidth,
                out requiredDistance
            );

            reachRatio =
                requiredDistance /
                maximumReach;
        }

        float apexOvershoot =
            descendingSpeed * descendingSpeed /
                (2f * GravityMagnitude);

        lifecycleRisk =
            GetLifecycleRisk(
                target,
                landingTime,
                apexOvershoot,
                elapsedBeforeLaunch +
                    landingTime
            );

        return reachRatio <= maximumRatio &&
               lifecycleRisk <=
                    MaximumSafeFutureLifecycleRisk &&
               Mathf.Abs(platformCenterX) <=
                    HorizontalWorldLimit + 0.01f;
    }

    private bool TryCalculateDescendingLandingTime(
        float sourceY,
        float velocityY,
        float targetY,
        out float landingTime,
        out float descendingSpeed)
    {
        landingTime = -1f;
        descendingSpeed = 0f;

        float heightGain =
            targetY -
            sourceY;

        float discriminant =
            velocityY * velocityY -
            2f * GravityMagnitude *
                heightGain;

        if (discriminant < 0f)
        {
            return false;
        }

        descendingSpeed =
            Mathf.Sqrt(discriminant);

        landingTime =
            (velocityY +
             descendingSpeed) /
            GravityMagnitude;

        return landingTime > 0.015f &&
               landingTime <= MaximumLandingTime;
    }

    private bool ShouldPlanInwardLanding(
        float platformCenterX,
        bool highAltitude)
    {
        float preferredWorldLimit = highAltitude
            ? HighAltitudePreferredWorldLimit
            : LowAltitudePreferredWorldLimit;

        return Mathf.Abs(platformCenterX) >
               preferredWorldLimit;
    }

    private void CalculateControlLandingInterval(
        float platformCenterX,
        float safeHalfWidth,
        float sourceX,
        bool inwardLanding,
        out float plannedLandingX,
        out float controlHalfWidth,
        out float requiredDistance)
    {
        float leftBoundary =
            platformCenterX -
            safeHalfWidth;

        float rightBoundary =
            platformCenterX +
            safeHalfWidth;

        if (inwardLanding)
        {
            if (platformCenterX > 0f)
            {
                rightBoundary =
                    platformCenterX;
            }
            else if (platformCenterX < 0f)
            {
                leftBoundary =
                    platformCenterX;
            }
        }

        plannedLandingX =
            (leftBoundary + rightBoundary) *
            0.50f;

        controlHalfWidth =
            Mathf.Max(
                0.08f,
                (rightBoundary - leftBoundary) *
                    0.50f
            );

        requiredDistance =
            sourceX < leftBoundary
                ? leftBoundary - sourceX
                : sourceX > rightBoundary
                    ? sourceX - rightBoundary
                    : 0f;
    }

    private void ApplySurvivalTiers(
        List<V5RouteDecision> decisions,
        bool highAltitude)
    {
        if (decisions == null ||
            decisions.Count == 0)
        {
            return;
        }

        float preferredWorldLimit = highAltitude
            ? HighAltitudePreferredWorldLimit
            : LowAltitudePreferredWorldLimit;

        float absoluteWorldLimit = highAltitude
            ? HighAltitudeAbsoluteWorldLimit
            : LowAltitudeAbsoluteWorldLimit;

        foreach (V5RouteDecision decision in decisions)
        {
            decision.RetentionSafe =
                IsRetentionSafeDecision(decision);

            float absoluteLandingX =
                Mathf.Abs(decision.CenterwardLandingX);

            bool insidePreferredLane =
                absoluteLandingX <=
                    preferredWorldLimit;

            bool hasCenterReturn =
                decision.CenterReturnSuccessorCount > 0;

            decision.EdgeSafe =
                decision.IsFinishApproach ||
                insidePreferredLane ||
                (absoluteLandingX <= absoluteWorldLimit &&
                 decision.InwardLandingPlanned &&
                 hasCenterReturn);

            decision.IsEdgeLastResort =
                !decision.EdgeSafe;
        }
    }

    private bool IsRetentionSafeDecision(
        V5RouteDecision decision)
    {
        if (decision == null ||
            decision.Candidate == null)
        {
            return false;
        }

        if (!decision.GenerationStable ||
            decision.Candidate.RecentlyRecycled)
        {
            return false;
        }

        if (decision.IsFinishApproach)
        {
            return true;
        }

        if (decision.ApexOvershoot >
                MaximumSafeApexOvershoot ||
            decision.LandingTime >
                MaximumSafeLifecycleLandingTime)
        {
            return false;
        }

        bool needsExtraObservation =
            decision.Candidate.IsMoving ||
            decision.Candidate.Type ==
                PlatformType.Breakable ||
            decision.ApexOvershoot >
                PreferredApexOvershoot;

        return !needsExtraObservation ||
               decision.Candidate
                   .ConsecutiveObservations >= 3;
    }

    private float GetLifecycleRisk(
        PlatformCandidate candidate,
        float landingTime,
        float apexOvershoot,
        float totalForecastTime)
    {
        float risk =
            Mathf.Max(
                0f,
                apexOvershoot -
                    PreferredApexOvershoot
            ) * 550f;

        risk +=
            Mathf.Max(
                0f,
                landingTime -
                    PreferredLifecycleLandingTime
            ) * 500f;

        risk +=
            Mathf.Max(
                0f,
                totalForecastTime -
                    PreferredLifecycleLandingTime
            ) * 300f;

        if (apexOvershoot >
            MaximumSafeApexOvershoot)
        {
            risk += 900f;
        }

        if (landingTime >
            MaximumSafeLifecycleLandingTime)
        {
            risk += 900f;
        }

        if (candidate != null)
        {
            if (!candidate.GenerationStable)
            {
                risk +=
                    totalForecastTime > 0.90f
                        ? 420f
                        : 160f;
            }

            if (candidate.RecentlyRecycled)
            {
                risk +=
                    totalForecastTime > 0.90f
                        ? 650f
                        : 240f;
            }

            if ((candidate.IsMoving ||
                 candidate.Type ==
                    PlatformType.Breakable ||
                 apexOvershoot >
                    PreferredApexOvershoot) &&
                candidate.ConsecutiveObservations < 3)
            {
                risk += 500f;
            }

            risk +=
                Mathf.Max(
                    0f,
                    candidate.LifecycleHazardPenalty
                );
        }

        return risk;
    }

    private float GetMovementUncertainty(
        PlatformCandidate candidate,
        float landingTime)
    {
        if (candidate == null ||
            !candidate.IsMoving)
        {
            return 0.04f;
        }

        return Mathf.Clamp(
            0.10f + landingTime * 0.045f,
            0.12f,
            0.30f
        );
    }

    private float GetBaseSafeHalfWidth(
        PlatformType type)
    {
        switch (type)
        {
            case PlatformType.Golden:
                return GoldenSafeHalfWidth;

            case PlatformType.Strong:
                return StrongSafeHalfWidth;

            case PlatformType.Breakable:
                return BreakableSafeHalfWidth;

            default:
                return NormalSafeHalfWidth;
        }
    }

    private float GetTypeBonus(
        PlatformType type)
    {
        switch (type)
        {
            case PlatformType.Golden:
                return 440f;

            case PlatformType.Strong:
                return 280f;

            case PlatformType.Normal:
                return 110f;

            case PlatformType.Breakable:
                return -180f;

            default:
                return -1000f;
        }
    }

    private bool IsCandidateUsable(
        PlatformCandidate candidate,
        int excludedTargetId,
        int excludedTargetGeneration,
        int failedTargetId,
        int failedTargetGeneration,
        bool failedTargetBlocked)
    {
        if (candidate == null ||
            (candidate.InstanceId == excludedTargetId &&
             candidate.Generation ==
                excludedTargetGeneration) ||
            !IsRealPlatformType(candidate.Type))
        {
            return false;
        }

        return !failedTargetBlocked ||
               candidate.InstanceId != failedTargetId ||
               candidate.Generation !=
                   failedTargetGeneration;
    }

    private bool IsRealPlatformType(
        PlatformType type)
    {
        return type == PlatformType.Normal ||
               type == PlatformType.Strong ||
               type == PlatformType.Golden ||
               type == PlatformType.Breakable;
    }

    private bool IsFinishApproach(
        float candidateY,
        float finishHeight)
    {
        return !float.IsInfinity(finishHeight) &&
               !float.IsNaN(finishHeight) &&
               finishHeight > 0f &&
               candidateY >= finishHeight - 3.00f;
    }

    private V5RouteDecision FindHighestScore(
        List<V5RouteDecision> decisions)
    {
        V5RouteDecision finishRoute = null;
        V5RouteDecision preferredBoost = null;
        int preferredBoostRank = 0;

        // Fast completion has one deliberate hard preference: a physically
        // stable Golden/Strong platform beats ordinary routing. Everything
        // else uses the single score below; edge and one-step look-ahead data
        // remain small tie-breakers instead of extra selection gates.
        foreach (V5RouteDecision decision in decisions)
        {
            if (decision.IsFinishApproach &&
                decision.RetentionSafe &&
                (finishRoute == null ||
                 decision.Score > finishRoute.Score))
            {
                finishRoute = decision;
            }

            int boostRank =
                GetBoostRank(decision);

            if (boostRank <= 0)
            {
                continue;
            }

            if (preferredBoost == null ||
                boostRank > preferredBoostRank ||
                (boostRank == preferredBoostRank &&
                 decision.Score > preferredBoost.Score))
            {
                preferredBoost = decision;
                preferredBoostRank = boostRank;
            }
        }

        if (finishRoute != null)
        {
            return finishRoute;
        }

        if (preferredBoost != null)
        {
            return preferredBoost;
        }

        V5RouteDecision best = null;
        V5RouteDecision safeEnemyAlternative = null;

        foreach (V5RouteDecision decision in decisions)
        {
            if (best == null ||
                decision.Score > best.Score)
            {
                best = decision;
            }
        }

        if (best == null ||
            best.Candidate.HasEnemy)
        {
            return best;
        }

        foreach (V5RouteDecision decision in decisions)
        {
            if (!IsSafeEnemyAlternative(
                    decision,
                    best))
            {
                continue;
            }

            if (safeEnemyAlternative == null ||
                decision.Score >
                    safeEnemyAlternative.Score)
            {
                safeEnemyAlternative = decision;
            }
        }

        if (safeEnemyAlternative != null)
        {
            return safeEnemyAlternative;
        }

        return best;
    }

    private bool IsSafeEnemyAlternative(
        V5RouteDecision decision,
        V5RouteDecision best)
    {
        if (!ClimberLog.IsEnemyTargetingEnabled ||
            decision == null ||
            decision.Candidate == null ||
            !decision.Candidate.HasEnemy ||
            decision.Candidate.Type == PlatformType.Breakable ||
            decision.IsEmergency ||
            !decision.GenerationStable ||
            !decision.RetentionSafe ||
            !decision.EdgeSafe ||
            decision.SuccessorCount <= 0 ||
            decision.ReachRatio > 0.72f ||
            decision.LandingMargin < 2.50f ||
            decision.LandingTime > PreferredLifecycleLandingTime ||
            decision.ApexOvershoot > PreferredApexOvershoot ||
            decision.LifecycleRisk > 400f)
        {
            return false;
        }

        // Never trade meaningful progress, route continuity or landing
        // reserve merely to collect an enemy. This makes enemy selection a
        // tie-break between already-safe routes, not a new routing objective.
        return decision.Candidate.CurrentPosition.y >=
                   best.Candidate.CurrentPosition.y - 0.25f &&
               decision.Score >= best.Score - 60f &&
               decision.LandingMargin >=
                   best.LandingMargin - 0.35f &&
               decision.SuccessorCount >=
                   best.SuccessorCount;
    }

    private int GetBoostRank(
        V5RouteDecision decision)
    {
        if (decision == null ||
            decision.Candidate == null ||
            !decision.RetentionSafe ||
            !decision.EdgeSafe)
        {
            return 0;
        }

        return decision.Candidate.Type ==
                PlatformType.Golden
            ? 2
            : decision.Candidate.Type ==
                PlatformType.Strong
                ? 1
                : 0;
    }

    private float ReflectHorizontalPosition(
        float currentX,
        float velocityX,
        float time)
    {
        float minimumX =
            -HorizontalWorldLimit;

        float maximumX =
            HorizontalWorldLimit;

        float width =
            maximumX -
            minimumX;

        float raw =
            currentX -
            minimumX +
            velocityX *
                Mathf.Max(0f, time);

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
}
