using System;
using System.Collections.Generic;
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

    internal V5RouteDecision SelectBest(
        IReadOnlyList<PlatformCandidate> candidates,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        float lastBounceY,
        bool highAltitude,
        bool allowEmergency,
        int excludedTargetId,
        int failedTargetId,
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
                    failedTargetId,
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

        int continuingCandidates = 0;

        foreach (V5RouteDecision decision in forward)
        {
            if (decision.SuccessorCount > 0)
            {
                continuingCandidates++;
            }
        }

        if (highAltitude &&
            continuingCandidates > 0)
        {
            bool constrainedChain =
                continuingCandidates <= 2;

            foreach (V5RouteDecision decision in forward)
            {
                if (decision.SuccessorCount == 0)
                {
                    decision.Score -= 1400f;
                    continue;
                }

                if (constrainedChain)
                {
                    decision.Score += 320f;
                }
            }
        }
        else if (highAltitude)
        {
            // The camera has not revealed a confirmed successor for any
            // candidate yet. Prefer a short, stable provisional landing over
            // a three-second breakable leap that can be recycled mid-flight.
            foreach (V5RouteDecision decision in forward)
            {
                decision.Score -=
                    decision.LandingTime * 240f;

                if (decision.Candidate.Type ==
                    PlatformType.Breakable)
                {
                    decision.Score -= 360f;
                }
            }
        }

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

        float centerDistance =
            Mathf.Abs(
                landingX -
                playerPosition.x
            );

        float requiredDistance =
            Mathf.Max(
                0f,
                centerDistance -
                    safeHalfWidth
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

        float reachRatio =
            requiredDistance /
            maximumReach;

        float maximumReachRatio = emergencyTarget
            ? EmergencyAbsoluteReachRatio
            : highAltitude
                ? HighAltitudeAbsoluteReachRatio
                : LowAltitudeAbsoluteReachRatio;

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
                Mathf.Abs(landingX) -
                    edgeStart
            );

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
            edgeExposure * 360f -
            Mathf.Abs(landingX) * 12f -
            emergencyPenalty -
            movingPenalty -
            longFlightPenalty;

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
        int totalThirdStepOptions = 0;
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

            if (!TryEvaluateFutureLeg(
                    source.CurrentPosition.y,
                    sourceDecision.LandingX,
                    sourceDecision.LandingTime,
                    launchVelocity,
                    successor,
                    highAltitude,
                    out successorTime,
                    out successorX,
                    out successorRatio))
            {
                continue;
            }

            successorCount++;

            int thirdOptions = CountThirdStepOptions(
                successor,
                successorX,
                sourceDecision.LandingTime +
                    successorTime,
                candidates,
                highAltitude
            );

            totalThirdStepOptions +=
                Mathf.Min(thirdOptions, 3);

            float gain =
                successor.CurrentPosition.y -
                source.CurrentPosition.y;

            float successorScore =
                gain * 28f +
                (1f - Mathf.Clamp01(successorRatio)) *
                    260f +
                GetTypeBonus(successor.Type) * 0.45f +
                Mathf.Min(thirdOptions, 3) * 65f -
                Mathf.Max(
                    0f,
                    Mathf.Abs(successorX) - 3.10f
                ) * 220f;

            bestSuccessorScore =
                Mathf.Max(
                    bestSuccessorScore,
                    successorScore
                );
        }

        sourceDecision.SuccessorCount =
            successorCount;

        sourceDecision.ThirdStepOptionCount =
            totalThirdStepOptions;

        if (successorCount == 0)
        {
            sourceDecision.RouteScore =
                highAltitude
                    ? -850f
                    : -350f;
            return;
        }

        sourceDecision.RouteScore =
            Mathf.Min(successorCount, 4) * 115f +
            Mathf.Min(totalThirdStepOptions, 8) * 42f +
            bestSuccessorScore * 0.50f;
    }

    private int CountThirdStepOptions(
        PlatformCandidate source,
        float sourceLandingX,
        float elapsedFromNow,
        IReadOnlyList<PlatformCandidate> candidates,
        bool highAltitude)
    {
        int options = 0;
        float launchVelocity =
            GetExpectedBounceVelocity(source);

        foreach (PlatformCandidate successor in candidates)
        {
            if (successor == null ||
                successor.InstanceId ==
                    source.InstanceId ||
                !IsRealPlatformType(successor.Type))
            {
                continue;
            }

            float landingTime;
            float landingX;
            float reachRatio;

            if (!TryEvaluateFutureLeg(
                    source.CurrentPosition.y,
                    sourceLandingX,
                    elapsedFromNow,
                    launchVelocity,
                    successor,
                    highAltitude,
                    out landingTime,
                    out landingX,
                    out reachRatio))
            {
                continue;
            }

            options++;

            if (options >= 4)
            {
                break;
            }
        }

        return options;
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
        out float reachRatio)
    {
        landingTime = -1f;
        landingX = 0f;
        reachRatio = float.MaxValue;

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

        landingX = PredictPlatformX(
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
                        landingTime
                    )
            );

        float requiredDistance =
            Mathf.Max(
                0f,
                Mathf.Abs(landingX - sourceX) -
                    safeHalfWidth
            );

        float maximumReach =
            HorizontalMoveSpeed *
                landingTime +
            HorizontalReachTolerance;

        if (maximumReach <= 0.01f)
        {
            return false;
        }

        reachRatio =
            requiredDistance /
            maximumReach;

        float maximumRatio = highAltitude
            ? 0.95f
            : 1.02f;

        return reachRatio <= maximumRatio &&
               Mathf.Abs(landingX) <=
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
        int failedTargetId,
        bool failedTargetBlocked)
    {
        if (candidate == null ||
            candidate.InstanceId == excludedTargetId ||
            !IsRealPlatformType(candidate.Type))
        {
            return false;
        }

        return !failedTargetBlocked ||
               candidate.InstanceId !=
                   failedTargetId;
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
        V5RouteDecision best = null;

        foreach (V5RouteDecision decision in decisions)
        {
            if (best == null ||
                decision.Score > best.Score)
            {
                best = decision;
            }
        }

        return best;
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
