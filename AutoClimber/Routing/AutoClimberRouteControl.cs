using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using AutoClimber.Diagnostics;
using IdleSlayerMods.Common.Extensions;
using Il2Cpp;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace AutoClimber;

public sealed partial class AutoClimberRuntime
{
    private void DetectBounceEveryFrame(
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        float currentVelocityY =
            playerVelocity.y;

        if (!velocityInitialized)
        {
            velocityInitialized = true;
            previousVelocityY =
                currentVelocityY;

            return;
        }

        bool bounced =
            previousVelocityY < -0.5f &&
            currentVelocityY > 1f;

        if (!bounced)
        {
            previousVelocityY =
                currentVelocityY;

            return;
        }

        bool goldenBounce =
            currentVelocityY >= 75f;

        bool strongBounce =
            currentVelocityY >= 40f;

        string bounceType =
            goldenBounce
                ? "Golden"
                : strongBounce
                    ? "Strong"
                    : "Normal";

        bool hadLockedTarget =
            currentTargetId != 0;

        bool landedTargetWasLifecycleFallback =
            currentTargetIsLifecycleFallback;

        int landedTargetId =
            currentTargetId;

        PlatformType landedTargetType =
            currentTargetType;

        float landedTargetPredictedX =
            currentTargetPredictedX;

        float landedTargetY =
            currentTargetY;

        bool landedOnLockedTarget =
            hadLockedTarget &&
            Mathf.Abs(
                playerPosition.y -
                landedTargetY
            ) <=
            ConfirmedLandingYTolerance;

        if (strongBounce)
        {
            runStrongBounces++;
        }
        else
        {
            runNormalBounces++;
        }

        if (hadLockedTarget &&
            (landedTargetType ==
                PlatformType.Strong ||
             landedTargetType ==
                PlatformType.Golden))
        {
            runStrongTargetAttempts++;

            if (strongBounce)
            {
                runStrongTargetHits++;
            }
        }

        if (landedTargetWasLifecycleFallback &&
            landedOnLockedTarget)
        {
            runLifecycleFallbackLandings++;
        }

        UpdateTargetlessAirborneTracking(false);

        string landingDiagnostic =
            hadLockedTarget
                ? $", TargetId={landedTargetId}, " +
                  $"TargetType={landedTargetType}, " +
                  $"TargetPredictedX=" +
                  $"{landedTargetPredictedX:F2}, " +
                  $"LandingErrorX=" +
                  $"{Mathf.Abs(playerPosition.x - landedTargetPredictedX):F2}, " +
                  $"TargetY={landedTargetY:F2}, " +
                  $"LandingErrorY=" +
                  $"{playerPosition.y - landedTargetY:F2}, " +
                  $"LockedTargetYMatched={landedOnLockedTarget}"
                : ", Target=None";

        RecordFailureTrace(
            $"Bounce Type={bounceType}, " +
            $"X={playerPosition.x:F2}, Y={playerPosition.y:F2}, " +
            $"VelocityY={currentVelocityY:F2}, " +
            $"TargetSprite={currentTargetSpriteName}" +
            landingDiagnostic
        );

        LogVerbose(
            $"Bounce detected: " +
            $"Type={bounceType}, " +
            $"X={playerPosition.x:F2}, " +
            $"Y={playerPosition.y:F2}, " +
            $"PreviousVelocityY=" +
            $"{previousVelocityY:F2}, " +
            $"CurrentVelocityY=" +
            $"{currentVelocityY:F2}" +
            landingDiagnostic
        );

        if (currentTargetId != 0 &&
            !landedOnLockedTarget &&
            currentTargetY >
                playerPosition.y +
                MissedTargetVerticalMargin)
        {
            TemporarilyBlockCurrentTarget(
                "The player bounced below the locked target."
            );
        }

        if (playerPosition.y >=
            lastMeaningfulProgressY +
            MeaningfulProgressHeight)
        {
            lastMeaningfulProgressY =
                playerPosition.y;

            lastMeaningfulProgressTime =
                Time.time;

            bouncesWithoutProgress = 0;
        }
        else
        {
            bouncesWithoutProgress++;
        }

        bool stuckByBounces =
            bouncesWithoutProgress >=
            StuckBounceLimit;

        bool stuckByTime =
            Time.time -
                lastMeaningfulProgressTime >=
            StuckTimeoutSeconds;

        if (stuckByBounces ||
            stuckByTime)
        {
            recoverySelectionRequested = true;
            bouncesWithoutProgress = 0;
            lastMeaningfulProgressTime = Time.time;

            if (currentTargetId != 0)
            {
                TemporarilyBlockCurrentTarget(
                    "The route made no meaningful vertical progress."
                );
            }

            forcedRecoveryDirection =
                ChooseEmergencyEscapeDirection(
                    playerPosition
                );

            forcedRecoveryUntilTime =
                Time.time +
                RecoveryForcedControlSeconds;

            ClearCurrentTarget();

            LogVerbose(
                "Stuck detector activated. " +
                "Recovery will force an alternate horizontal route."
            );
        }

        lastBounceY = playerPosition.y;
        lastBounceX = playerPosition.x;

        currentJumpMode =
            strongBounce
                ? JumpModeStrong
                : JumpModeNormal;

        currentJumpStartTime =
            Time.time;

        currentLaunchVelocityY =
            currentVelocityY;

        currentJumpWasGolden =
            goldenBounce;

        currentJumpRetargetCount = 0;
        nextV5RetargetTime = 0f;
        retentionProvisionalLogged = false;

        ClearLifecycleHazards();

        lastBlockedUpgradeCandidateId = 0;

        strongTargetUpgradeUntilTime =
            strongBounce
                ? Time.time +
                  StrongTargetUpgradeWindowSeconds
                : 0f;

        // A bounce above finishAtDistance is not completion. Continue routing
        // until the spawned finish platform is located and landed on.
        ClearCurrentTarget();

        nextPlatformScanTime = 0f;

        previousVelocityY =
            currentVelocityY;
    }

    private void UpdateCurrentTargetV5(
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        V5RouteDecision currentDecision = null;
        bool targetLostThisFrame = false;
        bool targetTemporarilyUnobservedThisFrame = false;
        bool lifecycleTargetLostThisFrame = false;
        int lifecycleTargetId = 0;
        int lifecycleTargetGeneration = 0;
        PlatformType lifecycleTargetType =
            PlatformType.Unknown;
        float lifecycleTargetY = 0f;
        string lifecycleFailureReason = "";

        if (currentTargetId != 0)
        {
            string targetRefreshFailure;

            bool targetTracked =
                TryRefreshPersistentTargetCandidate(
                    out targetRefreshFailure
                );

            if (!targetTracked)
            {
                targetTracked =
                    TryRebindPersistentTargetCandidate(
                        targetRefreshFailure
                    );
            }

            if (!targetTracked)
            {
                lifecycleTargetId = currentTargetId;
                lifecycleTargetGeneration =
                    currentTargetGeneration;
                lifecycleTargetType = currentTargetType;
                lifecycleTargetY = currentTargetY;
                lifecycleFailureReason =
                    targetRefreshFailure;

                targetTemporarilyUnobservedThisFrame =
                    ShouldKeepTemporarilyUnobservedTarget(
                        targetRefreshFailure,
                        playerPosition,
                        playerVelocity
                    );

                if (!targetTemporarilyUnobservedThisFrame)
                {
                    if (!currentTargetObservationLost)
                    {
                        runLifecycleLosses++;
                    }

                    RememberLifecycleHazard(
                        currentTargetY,
                        currentTargetType
                    );

                    RecordFailureTrace(
                        $"V5TargetLost Id={currentTargetId}, " +
                        $"Generation={currentTargetGeneration}, " +
                        $"Type={currentTargetType}, " +
                        $"Reason={targetRefreshFailure}, " +
                        $"ExpectedX={currentTargetPredictedX:F2}, " +
                        $"ExpectedY={currentTargetY:F2}, " +
                        $"RemainingTime=" +
                        $"{Mathf.Max(0f, currentTargetExpectedLandingAt - Time.time):F2}"
                    );

                    // A pooled Normal/Strong/Golden object is not evidence
                    // that its logical landing height is unsafe. V5.1
                    // replanned immediately in this situation. Preserve the
                    // blacklist only for a consumed breakable platform; for
                    // all other lifecycle losses, release the object identity
                    // and let the current scan choose a replacement.
                    if (currentTargetType ==
                        PlatformType.Breakable)
                    {
                        TemporarilyBlockCurrentTarget(
                            "The breakable target disappeared before landing."
                        );
                    }
                    else
                    {
                        RecordFailureTrace(
                            $"V5LifecycleReleaseWithoutBlock " +
                            $"Id={currentTargetId}, " +
                            $"Generation={currentTargetGeneration}, " +
                            $"Type={currentTargetType}, " +
                            $"Reason={targetRefreshFailure}"
                        );
                    }

                    targetLostThisFrame = true;
                    lifecycleTargetLostThisFrame = true;
                    ClearCurrentTarget();
                }
            }
            else
            {
                if (currentTargetObservationLost)
                {
                    RecordFailureTrace(
                        $"V5TargetObservationRecovered Id={currentTargetId}, " +
                        $"Type={currentTargetType}, " +
                        $"Reason={currentTargetObservationLostReason}, " +
                        $"HiddenFor={Time.time - currentTargetObservationLostAt:F2}"
                    );

                    ClearTemporaryTargetObservationState();
                }

                bool targetPassed =
                    playerVelocity.y < -1f &&
                    playerPosition.y <
                        currentTargetY -
                        MissedTargetVerticalMargin;

                if (targetPassed)
                {
                    RecordV5MissSnapshot(
                        "TargetPassed",
                        playerPosition,
                        playerVelocity
                    );

                    TemporarilyBlockCurrentTarget(
                        "V5 confirmed that the locked platform was passed while descending."
                    );

                    targetLostThisFrame = true;
                    ClearCurrentTarget();
                }
                else
                {
                    currentDecision =
                        v5RoutePlanner
                            .EvaluatePersistentTarget(
                                currentTargetCandidate,
                                planner.Candidates,
                                playerPosition,
                                playerVelocity,
                                lastBounceY,
                                IsHighAltitudeMode(),
                                currentTargetIsRescue,
                                finishHeight
                            );

                    if (currentDecision.Feasible)
                    {
                        UpdateV5TargetPrediction(
                            currentDecision
                        );
                    }
                    else
                    {
                        if (ShouldPreserveCommittedTargetNearLanding(
                                currentDecision,
                                playerPosition,
                                playerVelocity))
                        {
                            return;
                        }

                        RecordV5MissSnapshot(
                            "RouteInvalid-" +
                                currentDecision.RejectionReason,
                            playerPosition,
                            playerVelocity
                        );

                        TemporarilyBlockCurrentTarget(
                            "V5 released a committed target whose physical or horizontal route became invalid."
                        );

                        targetLostThisFrame = true;
                        ClearCurrentTarget();
                        currentDecision = null;
                    }
                }
            }
        }

        bool failedTargetBlocked =
            failedTargetId != 0 &&
            Time.time < failedTargetUntilTime;

        ApplyLifecycleHazardPenalties();

        int forwardFeasibleCount;

        V5RouteDecision bestDecision =
            v5RoutePlanner.SelectBest(
                planner.Candidates,
                playerPosition,
                playerVelocity,
                lastBounceY,
                IsHighAltitudeMode(),
                false,
                currentTargetId,
                currentTargetGeneration,
                failedTargetId,
                failedTargetGeneration,
                failedTargetBlocked,
                finishHeight,
                out forwardFeasibleCount
            );

        if (currentTargetId != 0 &&
            currentDecision != null &&
            !targetTemporarilyUnobservedThisFrame)
        {
            if (!ShouldUpgradeV5Target(
                    currentDecision,
                    bestDecision,
                    playerVelocity))
            {
                return;
            }

            LockV5Target(
                bestDecision,
                playerPosition,
                true,
                forwardFeasibleCount
            );

            return;
        }

        float jumpAge =
            Mathf.Max(
                0f,
                Time.time -
                    currentJumpStartTime
            );

        bool allowEmergency =
            jumpAge >=
                HighAltitudeEarlyRescueDelaySeconds &&
            playerVelocity.y <=
                HighAltitudeEarlyRescueMaximumVelocityY;

        if (bestDecision == null &&
            allowEmergency)
        {
            bestDecision =
                v5RoutePlanner.SelectBest(
                    planner.Candidates,
                    playerPosition,
                    playerVelocity,
                    lastBounceY,
                    IsHighAltitudeMode(),
                    true,
                    currentTargetId,
                    currentTargetGeneration,
                    failedTargetId,
                    failedTargetGeneration,
                    failedTargetBlocked,
                    finishHeight,
                    out forwardFeasibleCount
                );
        }

        if (targetLostThisFrame ||
            targetTemporarilyUnobservedThisFrame)
        {
            int excludedLifecycleTargetId =
                lifecycleTargetId != 0
                    ? lifecycleTargetId
                    : failedTargetId;

            int excludedLifecycleTargetGeneration =
                lifecycleTargetId != 0
                    ? lifecycleTargetGeneration
                    : failedTargetGeneration;

            bool excludedLifecycleTargetActive =
                lifecycleTargetId != 0 ||
                failedTargetBlocked;

            bestDecision = ChooseSaferLateReplanDecision(
                bestDecision,
                playerPosition,
                playerVelocity,
                excludedLifecycleTargetId,
                excludedLifecycleTargetGeneration,
                excludedLifecycleTargetActive,
                allowEmergency,
                lifecycleTargetY,
                lifecycleTargetType
            );
        }

        bool shouldUseRetentionProvisional =
            bestDecision != null &&
            !bestDecision.RetentionSafe &&
            !bestDecision.IsFinishApproach &&
            !bestDecision.IsEmergency &&
            (currentJumpWasGolden ||
             currentLaunchVelocityY >= 40f) &&
            playerVelocity.y /
                GravityMagnitude >
                RetentionProvisionalMinimumTimeToApex;

        if (shouldUseRetentionProvisional)
        {
            if (!retentionProvisionalLogged)
            {
                retentionProvisionalLogged = true;
                runRetentionProvisionalSelections++;

                RecordFailureTrace(
                    $"V5RetentionProvisional CandidateId=" +
                    $"{bestDecision.Candidate.InstanceId}, " +
                    $"Generation={bestDecision.Candidate.Generation}, " +
                    $"ApexDrop={bestDecision.ApexOvershoot:F2}, " +
                    $"LandingTime={bestDecision.LandingTime:F2}, " +
                    $"TimeToApex=" +
                    $"{playerVelocity.y / GravityMagnitude:F2}"
                );
            }

            // Keep steering toward the only feasible route while later scans
            // search for a retention-safe upgrade. Returning here with no
            // target used to center the player away from edge routes.
        }

        if (targetTemporarilyUnobservedThisFrame)
        {
            if (bestDecision == null)
            {
                // Keep steering toward the logical target only when this scan
                // has no safe replacement. The next 0.10-second scan retries
                // both object rebinding and fallback planning in parallel.
                return;
            }

            PlatformCandidate fallbackCandidate =
                bestDecision.Candidate;

            string fallbackMessage =
                $"V5LifecycleFallback OldId={lifecycleTargetId}, " +
                $"OldGeneration={lifecycleTargetGeneration}, " +
                $"OldType={lifecycleTargetType}, " +
                $"OldY={lifecycleTargetY:F2}, " +
                $"Reason={lifecycleFailureReason}, " +
                $"NewId={fallbackCandidate.InstanceId}, " +
                $"NewGeneration={fallbackCandidate.Generation}, " +
                $"NewType={fallbackCandidate.Type}, " +
                $"NewY={fallbackCandidate.CurrentPosition.y:F2}, " +
                $"ReachRatio={bestDecision.ReachRatio:F2}, " +
                $"Margin={bestDecision.LandingMargin:F2}, " +
                $"ApexDrop={bestDecision.ApexOvershoot:F2}, " +
                $"LifecycleRisk={bestDecision.LifecycleRisk:F0}, " +
                $"StableScans={fallbackCandidate.ConsecutiveObservations}, " +
                $"CenterReturns={bestDecision.CenterReturnSuccessorCount}";

            RecordFailureTrace(fallbackMessage);
            LogVerbose(fallbackMessage);

            ClearCurrentTarget();

            LockV5Target(
                bestDecision,
                playerPosition,
                false,
                forwardFeasibleCount,
                true
            );

            return;
        }

        if (bestDecision == null)
        {
            RecordTargetlessSnapshot(
                playerPosition,
                playerVelocity
            );
            return;
        }

        if (lifecycleTargetLostThisFrame)
        {
            string replanMessage =
                $"V5LifecycleReplan OldId={lifecycleTargetId}, " +
                $"OldGeneration={lifecycleTargetGeneration}, " +
                $"OldType={lifecycleTargetType}, " +
                $"OldY={lifecycleTargetY:F2}, " +
                $"Reason={lifecycleFailureReason}, " +
                $"NewId={bestDecision.Candidate.InstanceId}, " +
                $"NewGeneration={bestDecision.Candidate.Generation}, " +
                $"NewType={bestDecision.Candidate.Type}, " +
                $"NewY={bestDecision.Candidate.CurrentPosition.y:F2}, " +
                $"ApexDrop={bestDecision.ApexOvershoot:F2}, " +
                $"LifecycleRisk={bestDecision.LifecycleRisk:F0}";

            RecordFailureTrace(replanMessage);
            LogVerbose(replanMessage);
        }

        LockV5Target(
            bestDecision,
            playerPosition,
            false,
            forwardFeasibleCount,
            lifecycleTargetLostThisFrame
        );
    }

    [HideFromIl2Cpp]
    private bool ShouldUpgradeV5Target(
        V5RouteDecision currentDecision,
        V5RouteDecision proposedDecision,
        Vector2 playerVelocity)
    {
        if (currentDecision == null ||
            proposedDecision == null ||
            proposedDecision.Candidate == null ||
            (proposedDecision.Candidate.InstanceId ==
                 currentTargetId &&
             proposedDecision.Candidate.Generation ==
                 currentTargetGeneration) ||
            currentTargetIsRescue ||
            playerVelocity.y <= 5f)
        {
            return false;
        }

        bool boostJump =
            currentJumpWasGolden ||
            currentLaunchVelocityY >= 40f;

        bool retentionUpgrade =
            !currentDecision.IsFinishApproach &&
            !currentDecision.RetentionSafe &&
            proposedDecision.RetentionSafe;

        bool finishUpgrade =
            proposedDecision.IsFinishApproach &&
            !currentDecision.IsFinishApproach;

        bool fastBoostUpgrade =
            proposedDecision.RetentionSafe &&
            proposedDecision.EdgeSafe &&
            (proposedDecision.Candidate.Type ==
                 PlatformType.Golden ||
             proposedDecision.Candidate.Type ==
                 PlatformType.Strong) &&
            GetPlatformTypeRank(
                proposedDecision.Candidate.Type
            ) > GetPlatformTypeRank(currentTargetType);

        int maximumRetargets =
            boostJump
                ? 2
                : 1;

        if (currentJumpRetargetCount >=
                maximumRetargets ||
            Time.time < nextV5RetargetTime)
        {
            return false;
        }

        float upgradeWindow =
            currentJumpWasGolden ||
            currentLaunchVelocityY >= 75f
                ? 1.45f
                : currentLaunchVelocityY >= 40f
                    ? 0.95f
                    : 0.42f;

        if (!retentionUpgrade &&
            !finishUpgrade &&
            !fastBoostUpgrade &&
            Time.time - currentJumpStartTime >
                upgradeWindow)
        {
            return false;
        }

        bool physicallyComfortable =
            proposedDecision.LandingMargin >=
                LateReplanMinimumLandingMargin &&
            proposedDecision.ReachRatio <=
                (IsHighAltitudeMode()
                    ? LateReplanHighAltitudeMaximumReachRatio
                    : LateReplanLowAltitudeMaximumReachRatio);

        if (retentionUpgrade ||
            finishUpgrade ||
            fastBoostUpgrade)
        {
            return physicallyComfortable;
        }

        bool meaningfulHeightGain =
            proposedDecision.Candidate
                .CurrentPosition.y >=
            currentTargetY + 1.00f;

        if (!meaningfulHeightGain)
        {
            return false;
        }

        if (proposedDecision.LandingMargin <
            currentDecision.LandingMargin - 0.35f)
        {
            return false;
        }

        float requiredImprovement =
            IsHighAltitudeMode()
                ? 240f
                : 180f;

        return proposedDecision.Score >=
            currentDecision.Score +
                requiredImprovement;
    }

    [HideFromIl2Cpp]
    private void LockV5Target(
        V5RouteDecision decision,
        Vector3 playerPosition,
        bool isUpgrade,
        int forwardFeasibleCount,
        bool lifecycleFallback = false)
    {
        if (decision == null ||
            decision.Candidate == null)
        {
            return;
        }

        PlatformCandidate candidate =
            decision.Candidate;

        runTargetSelections++;

        if (candidate.Type == PlatformType.Strong ||
            candidate.Type == PlatformType.Golden)
        {
            runStrongTargets++;
        }
        else if (candidate.Type ==
                 PlatformType.Breakable)
        {
            runBreakableTargets++;
        }
        else
        {
            runNormalTargets++;
        }

        if (Mathf.Abs(
                decision.CenterwardLandingX
            ) >
            PreferredLandingWorldLimit)
        {
            runEdgeTargets++;
        }

        currentTargetId =
            candidate.InstanceId;

        currentTargetGeneration =
            candidate.Generation;

        currentTargetType =
            candidate.Type;

        currentTargetSpriteName =
            candidate.SpriteName ?? "";

        currentTargetCandidate =
            candidate;

        currentTargetAnchorY =
            candidate.CurrentPosition.y;

        currentTargetAnchorType =
            candidate.Type;

        currentTargetY =
            candidate.CurrentPosition.y;

        currentTargetPredictedX =
            decision.CenterwardLandingX;

        currentTargetLandingOffsetX =
            decision.CenterwardLandingX -
            decision.LandingX;

        currentTargetSafeHalfWidth =
            decision.ControlHalfWidth;

        currentTargetIsRescue =
            decision.IsEmergency;

        currentTargetIsLifecycleFallback =
            lifecycleFallback;

        retentionProvisionalLogged =
            false;

        if (lifecycleFallback)
        {
            runLifecycleFallbackSelections++;
        }

        if (decision.IsEdgeLastResort)
        {
            runEdgeLastResorts++;
        }

        if (decision.RetentionSafe)
        {
            runRetentionSafeTargets++;
        }
        else
        {
            runRetentionRiskyTargets++;
        }

        runMaximumLockedApexDrop =
            Mathf.Max(
                runMaximumLockedApexDrop,
                decision.ApexOvershoot
            );

        if (!decision.RetentionSafe)
        {
            LogVerbose(
                $"V5RiskyRetentionLastResort Id={candidate.InstanceId}, " +
                $"Generation={candidate.Generation}, " +
                $"Type={candidate.Type}, " +
                $"Y={candidate.CurrentPosition.y:F2}, " +
                $"LandingTime={decision.LandingTime:F2}, " +
                $"ApexDrop={decision.ApexOvershoot:F2}, " +
                $"StableScans={candidate.ConsecutiveObservations}"
            );
        }

        if (decision.IsEdgeLastResort)
        {
            LogVerbose(
                $"V5EdgeLastResort Id={candidate.InstanceId}, " +
                $"Generation={candidate.Generation}, " +
                $"LandingX={decision.LandingX:F2}, " +
                $"CenterwardX={decision.CenterwardLandingX:F2}, " +
                $"EdgeReserve={decision.EdgeReserve:F2}, " +
                $"CenterReturns=" +
                $"{decision.CenterReturnSuccessorCount}"
            );
        }

        currentTargetExpectedLandingAt =
            Time.time +
            decision.LandingTime;

        currentTargetRouteScore =
            decision.Score;

        currentTargetReachRatio =
            decision.ReachRatio;

        currentTargetLastSeenTime =
            Time.time;

        currentTargetObservedX =
            candidate.CurrentPosition.x;

        currentTargetObservedTime =
            Time.time;

        currentTargetPersistedOffScanLogged =
            false;

        emergencyPlatformRescanPending =
            false;

        ClearTemporaryTargetObservationState();
        nearLandingCommitmentLogged = false;

        currentTargetColliderOffset =
            Vector2.zero;

        try
        {
            if (candidate.GameObject != null)
            {
                Vector3 objectPosition =
                    candidate.GameObject
                        .transform.position;

                currentTargetColliderOffset =
                    candidate.CurrentPosition -
                    new Vector2(
                        objectPosition.x,
                        objectPosition.y
                    );
            }
        }
        catch
        {
            currentTargetColliderOffset =
                Vector2.zero;
        }

        targetLockedTime =
            Time.time;

        float initialError =
            currentTargetPredictedX -
            playerPosition.x;

        targetInitialDirection =
            initialError > currentTargetSafeHalfWidth
                ? 1
                : initialError <
                    -currentTargetSafeHalfWidth
                    ? -1
                    : 0;

        targetMinimumHoldUntil =
            Time.time +
            (decision.IsEmergency
                ? 0.30f
                : NormalInitialHoldSeconds);

        targetControlPhase =
            decision.IsEmergency
                ? 2
                : 0;

        if (isUpgrade)
        {
            currentJumpRetargetCount++;
        }

        nextV5RetargetTime =
            Time.time + 0.25f;

        if (recoverySelectionRequested)
        {
            recoverySelectionRequested = false;
            forcedRecoveryDirection = 0;
            forcedRecoveryUntilTime = 0f;
        }

        RecordFailureTrace(
            $"TargetLocked Model=V5, Id={currentTargetId}, " +
            $"Generation={currentTargetGeneration}, " +
            $"Type={currentTargetType}, X={currentTargetPredictedX:F2}, " +
            $"PlatformCenterX={decision.LandingX:F2}, " +
            $"ControlHalfWidth={decision.ControlHalfWidth:F2}, " +
            $"InwardLanding={decision.InwardLandingPlanned}, " +
            $"EnemyPlatform={candidate.HasEnemy}, " +
            $"Y={currentTargetY:F2}, LandingTime={decision.LandingTime:F2}, " +
            $"ImpactSpeed={decision.DescendingSpeed:F2}, " +
            $"ReachRatio={decision.ReachRatio:F2}, " +
            $"Margin={decision.LandingMargin:F2}, " +
            $"Successors={decision.SuccessorCount}, " +
            $"CenterReturns={decision.CenterReturnSuccessorCount}, " +
            $"ApexDrop={decision.ApexOvershoot:F2}, " +
            $"LifecycleRisk={decision.LifecycleRisk:F0}, " +
            $"RetentionSafe={decision.RetentionSafe}, " +
            $"EdgeReserve={decision.EdgeReserve:F2}, " +
            $"StableScans={candidate.ConsecutiveObservations}, " +
            $"EdgeLastResort={decision.IsEdgeLastResort}, " +
            $"ForwardOptions={forwardFeasibleCount}, " +
            $"Score={decision.Score:F1}, " +
            $"Emergency={decision.IsEmergency}, " +
            $"Upgrade={isUpgrade}, " +
            $"LaunchV={currentLaunchVelocityY:F2}"
        );
    }

    private bool TryRefreshPersistentTargetCandidate()
    {
        string ignoredReason;

        return TryRefreshPersistentTargetCandidate(
            out ignoredReason
        );
    }

    private bool TryRefreshPersistentTargetCandidate(
        out string failureReason)
    {
        failureReason = "Unknown";

        PlatformCandidate observedCandidate =
            planner.FindCandidateById(
                currentTargetId
            );

        if (observedCandidate != null &&
            observedCandidate.Generation !=
                currentTargetGeneration)
        {
            failureReason =
                "RecycledGenerationChanged";
            return false;
        }

        if (observedCandidate != null)
        {
            currentTargetCandidate =
                observedCandidate;
        }

        if (currentTargetCandidate == null ||
            currentTargetCandidate.GameObject == null)
        {
            failureReason = "MissingObjectReference";
            return false;
        }

        Vector2 currentPosition;

        try
        {
            if (!currentTargetCandidate
                    .GameObject.activeInHierarchy)
            {
                failureReason = "GameObjectInactive";
                return false;
            }

            if (currentTargetCandidate.Collider != null)
            {
                if (!currentTargetCandidate
                        .Collider.enabled)
                {
                    failureReason =
                        "ColliderDisabled";
                    return false;
                }

                Bounds bounds =
                    currentTargetCandidate
                        .Collider.bounds;

                currentPosition =
                    new Vector2(
                        bounds.center.x,
                        bounds.center.y
                    );

                currentTargetCandidate.ColliderSize =
                    new Vector2(
                        bounds.size.x,
                        bounds.size.y
                    );
            }
            else
            {
                Vector3 objectPosition =
                    currentTargetCandidate
                        .GameObject.transform.position;

                currentPosition =
                    new Vector2(
                        objectPosition.x,
                        objectPosition.y
                    ) +
                    currentTargetColliderOffset;
            }
        }
        catch
        {
            failureReason = "TargetReadException";
            return false;
        }

        if (Mathf.Abs(
                currentPosition.y -
                currentTargetAnchorY
            ) > RecycledTargetHeightTolerance)
        {
            failureReason =
                "RecycledHeightChanged";
            return false;
        }

        float observationDeltaTime =
            Time.time -
            currentTargetObservedTime;

        float observationDeltaX =
            currentPosition.x -
            currentTargetObservedX;

        if (observationDeltaTime > 0.001f &&
            Mathf.Abs(observationDeltaX) > 0.015f)
        {
            float measuredVelocity =
                observationDeltaX /
                observationDeltaTime;

            float direction =
                Mathf.Sign(measuredVelocity);

            currentTargetCandidate.IsMoving = true;
            currentTargetCandidate.MovementDirectionX =
                direction;

            if (Mathf.Abs(measuredVelocity) >= 0.50f &&
                Mathf.Abs(measuredVelocity) <= 6.00f)
            {
                currentTargetCandidate.PlatformVelocityX =
                    Mathf.Lerp(
                        currentTargetCandidate.PlatformVelocityX,
                        measuredVelocity,
                        0.35f
                    );
            }
            else
            {
                currentTargetCandidate.PlatformVelocityX =
                    direction * 3.00f;
            }
        }

        currentTargetObservedX =
            currentPosition.x;

        currentTargetObservedTime =
            Time.time;

        currentTargetCandidate.CurrentPosition =
            currentPosition;

        currentTargetY =
            currentPosition.y;

        currentTargetLastSeenTime =
            Time.time;

        if (!currentTargetPersistedOffScanLogged &&
            planner.FindCandidate(
                currentTargetId,
                currentTargetGeneration) == null)
        {
            currentTargetPersistedOffScanLogged =
                true;

            RecordFailureTrace(
                $"V5TargetPersistedOffScan Id={currentTargetId}, " +
                $"Type={currentTargetType}, Y={currentTargetY:F2}"
            );
        }

        failureReason = "";
        emergencyPlatformRescanPending = false;
        return true;
    }

    private bool TryRebindPersistentTargetCandidate(
        string originalFailureReason)
    {
        PlatformCandidate bestMatch = null;
        float bestMatchScore = float.MaxValue;
        float remainingLandingTime =
            Mathf.Max(
                0f,
                currentTargetExpectedLandingAt - Time.time
            );

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (candidate == null ||
                candidate.InstanceId ==
                    currentTargetId ||
                !candidate.GenerationStable ||
                candidate.RecentlyRecycled ||
                !IsBaseCandidateUsable(candidate))
            {
                continue;
            }

            float heightDistance =
                Mathf.Abs(
                    candidate.CurrentPosition.y -
                    currentTargetAnchorY
                );

            if (heightDistance >
                LogicalTargetRebindHeightTolerance)
            {
                continue;
            }

            float candidateLandingX =
                ApplyCurrentTargetLandingBias(
                    v5RoutePlanner.PredictPlatformX(
                        candidate,
                        remainingLandingTime
                    )
                );

            float landingDistance =
                Mathf.Abs(
                    candidateLandingX -
                    currentTargetPredictedX
                );

            if (landingDistance >
                LogicalTargetRebindMaximumLandingDistance)
            {
                continue;
            }

            bool compatibleType =
                IsLogicalTargetTypeCompatible(
                    currentTargetAnchorType,
                    candidate.Type
                );

            bool breakableTypeMismatch =
                currentTargetAnchorType != candidate.Type &&
                (currentTargetAnchorType ==
                    PlatformType.Breakable ||
                 candidate.Type ==
                    PlatformType.Breakable);

            if (breakableTypeMismatch)
            {
                continue;
            }

            if (!compatibleType &&
                (heightDistance >
                    LogicalTargetMismatchedTypeHeightTolerance ||
                 landingDistance >
                    LogicalTargetMismatchedTypeLandingDistance))
            {
                continue;
            }

            float typePenalty =
                compatibleType
                    ? candidate.Type ==
                        currentTargetAnchorType
                        ? 0f
                        : 0.35f
                    : 0.90f;

            float matchScore =
                heightDistance * 1.75f +
                landingDistance +
                typePenalty;

            if (bestMatch == null ||
                matchScore < bestMatchScore)
            {
                bestMatch = candidate;
                bestMatchScore = matchScore;
            }
        }

        if (bestMatch == null)
        {
            return false;
        }

        int oldTargetId =
            currentTargetId;

        currentTargetCandidate =
            bestMatch;

        currentTargetId =
            bestMatch.InstanceId;

        currentTargetGeneration =
            bestMatch.Generation;

        currentTargetType =
            bestMatch.Type;

        currentTargetSpriteName =
            bestMatch.SpriteName ?? "";

        currentTargetY =
            bestMatch.CurrentPosition.y;

        currentTargetAnchorY =
            bestMatch.CurrentPosition.y;

        currentTargetAnchorType =
            bestMatch.Type;

        currentTargetObservedX =
            bestMatch.CurrentPosition.x;

        currentTargetObservedTime =
            Time.time;

        currentTargetLastSeenTime =
            Time.time;

        currentTargetPersistedOffScanLogged =
            false;

        currentTargetColliderOffset =
            Vector2.zero;

        try
        {
            if (bestMatch.GameObject != null)
            {
                Vector3 objectPosition =
                    bestMatch.GameObject
                        .transform.position;

                currentTargetColliderOffset =
                    bestMatch.CurrentPosition -
                    new Vector2(
                        objectPosition.x,
                        objectPosition.y
                    );
            }
        }
        catch
        {
            currentTargetColliderOffset =
                Vector2.zero;
        }

        RecordFailureTrace(
            $"V5TargetRebound OldId={oldTargetId}, " +
            $"NewId={currentTargetId}, " +
            $"Generation={currentTargetGeneration}, " +
            $"Type={currentTargetType}, " +
            $"Y={currentTargetY:F2}, " +
            $"MatchScore={bestMatchScore:F2}, " +
            $"OriginalReason={originalFailureReason}"
        );

        return true;
    }

    [HideFromIl2Cpp]
    private bool IsLogicalTargetTypeCompatible(
        PlatformType anchorType,
        PlatformType candidateType)
    {
        if (anchorType == candidateType)
        {
            return true;
        }

        bool anchorIsBoost =
            anchorType == PlatformType.Strong ||
            anchorType == PlatformType.Golden;

        bool candidateIsBoost =
            candidateType == PlatformType.Strong ||
            candidateType == PlatformType.Golden;

        return anchorIsBoost && candidateIsBoost;
    }

    [HideFromIl2Cpp]
    private bool ShouldKeepTemporarilyUnobservedTarget(
        string failureReason,
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        bool transientFailure =
            failureReason == "GameObjectInactive" ||
            failureReason == "ColliderDisabled" ||
            failureReason == "RecycledHeightChanged" ||
            failureReason == "RecycledGenerationChanged" ||
            failureReason == "TargetReadException";

        if (!transientFailure ||
            currentTargetType == PlatformType.Breakable)
        {
            return false;
        }

        bool targetAlreadyPassed =
            playerVelocity.y < -1f &&
            playerPosition.y <
                currentTargetY -
                MissedTargetVerticalMargin;

        if (targetAlreadyPassed)
        {
            return false;
        }

        if (!currentTargetObservationLost)
        {
            currentTargetObservationLost = true;
            currentTargetObservationLostAt = Time.time;
            currentTargetObservationLostReason =
                failureReason;

            runLifecycleLosses++;

            RememberLifecycleHazard(
                currentTargetY,
                currentTargetType
            );

            RecordFailureTrace(
                $"V5TargetTemporarilyUnobserved Id={currentTargetId}, " +
                $"Type={currentTargetType}, " +
                $"Reason={failureReason}, " +
                $"Grace={LogicalTargetObservationGraceSeconds:F2}, " +
                $"RemainingTime=" +
                $"{Mathf.Max(0f, currentTargetExpectedLandingAt - Time.time):F2}"
            );
        }

        return Time.time -
            currentTargetObservationLostAt <=
            LogicalTargetObservationGraceSeconds;
    }

    private void ClearTemporaryTargetObservationState()
    {
        currentTargetObservationLost = false;
        currentTargetObservationLostAt = 0f;
        currentTargetObservationLostReason = "";
    }

    [HideFromIl2Cpp]
    private bool ShouldPreserveCommittedTargetNearLanding(
        V5RouteDecision decision,
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        if (decision == null)
        {
            return false;
        }

        bool transientRejection =
            decision.RejectionReason ==
                "NoDescendingIntersection" ||
            decision.RejectionReason ==
                "HorizontalReachExceeded";

        float remainingLandingTime =
            Mathf.Max(
                0f,
                currentTargetExpectedLandingAt - Time.time
            );

        bool preserve =
            transientRejection &&
            remainingLandingTime <=
                NearLandingCommitmentSeconds &&
            playerVelocity.y <= 5f &&
            playerPosition.y >=
                currentTargetY -
                NearLandingCommitmentVerticalTolerance;

        if (preserve &&
            !nearLandingCommitmentLogged)
        {
            nearLandingCommitmentLogged = true;

            RecordFailureTrace(
                $"V5NearLandingCommitment Id={currentTargetId}, " +
                $"Type={currentTargetType}, " +
                $"Reason={decision.RejectionReason}, " +
                $"RemainingTime={remainingLandingTime:F2}"
            );
        }

        return preserve;
    }

    [HideFromIl2Cpp]
    private V5RouteDecision ChooseSaferLateReplanDecision(
        V5RouteDecision proposedDecision,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        int blockedTargetId,
        int blockedTargetGeneration,
        bool blockedTargetActive,
        bool allowEmergency,
        float previousTargetY,
        PlatformType previousTargetType)
    {
        float maximumReachRatio =
            IsHighAltitudeMode()
                ? LateReplanHighAltitudeMaximumReachRatio
                : LateReplanLowAltitudeMaximumReachRatio;

        float preferredHeightGain =
            previousTargetType == PlatformType.Golden
                ? LifecycleFallbackPreferredGoldenGain
                : previousTargetType == PlatformType.Strong ||
                  currentLaunchVelocityY >= 40f
                    ? LifecycleFallbackPreferredStrongGain
                    : LifecycleFallbackPreferredNormalGain;

        float preferredWorldLimit =
            IsHighAltitudeMode()
                ? HighAltitudePreferredWorldLimit
                : PreferredLandingWorldLimit;

        float absoluteWorldLimit =
            IsHighAltitudeMode()
                ? HighAltitudeAbsoluteWorldLimit
                : HorizontalWorldLimit;

        V5RouteDecision safestAlternative = null;
        float safestAlternativeScore =
            float.MinValue;

        V5RouteDecision physicalFallback = null;
        float physicalFallbackScore =
            float.MinValue;

        V5RouteDecision edgeSafePhysicalFallback = null;
        float edgeSafePhysicalFallbackScore =
            float.MinValue;

        V5RouteDecision retentionSafePhysicalFallback = null;
        float retentionSafePhysicalFallbackScore =
            float.MinValue;

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (!IsBaseCandidateUsable(candidate) ||
                (blockedTargetActive &&
                 candidate.InstanceId == blockedTargetId &&
                 candidate.Generation ==
                    blockedTargetGeneration))
            {
                continue;
            }

            V5RouteDecision decision =
                v5RoutePlanner.EvaluateLateReplanCandidate(
                    candidate,
                    planner.Candidates,
                    playerPosition,
                    playerVelocity,
                    lastBounceY,
                    IsHighAltitudeMode(),
                    allowEmergency,
                    finishHeight
                );

            if (!decision.Feasible ||
                decision.ReachRatio > maximumReachRatio ||
                decision.LandingMargin <
                    LateReplanMinimumLandingMargin)
            {
                continue;
            }

            float heightGain =
                previousTargetY > 0f
                    ? candidate.CurrentPosition.y -
                        previousTargetY
                    : preferredHeightGain;

            float fallbackScore =
                decision.Score -
                Mathf.Max(
                    0f,
                    preferredHeightGain -
                        heightGain
                ) * 420f -
                Mathf.Max(
                    0f,
                    decision.LandingTime -
                        LateReplanPreferredLandingTime
                ) * 700f +
                decision.CenterReturnSuccessorCount *
                    260f;

            if (candidate.GenerationStable)
            {
                fallbackScore += 220f;
            }
            else
            {
                fallbackScore -= 420f;
            }

            if (heightGain < 3f)
            {
                fallbackScore -= 750f;
            }

            float absoluteLandingX =
                Mathf.Abs(
                    decision.CenterwardLandingX
                );

            bool edgeSafe =
                decision.IsFinishApproach ||
                absoluteLandingX <=
                    preferredWorldLimit ||
                (absoluteLandingX <= absoluteWorldLimit &&
                 decision.InwardLandingPlanned &&
                 decision.CenterReturnSuccessorCount > 0);

            bool stableEnough =
                candidate.GenerationStable &&
                !candidate.RecentlyRecycled &&
                (!candidate.IsMoving &&
                 candidate.Type != PlatformType.Breakable &&
                 decision.ApexOvershoot <=
                    RetentionPreferredApexOvershoot ||
                 candidate.ConsecutiveObservations >= 3);

            bool landingTimeSafe =
                decision.LandingTime <=
                    LateReplanPreferredLandingTime ||
                (decision.LandingTime <=
                    LateReplanMaximumSafeLandingTime &&
                 decision.ApexOvershoot <= 6.50f);

            bool retentionSafe =
                decision.IsFinishApproach ||
                (decision.ApexOvershoot <=
                    RetentionMaximumSafeApexOvershoot &&
                 landingTimeSafe &&
                 stableEnough);

            decision.RetentionSafe =
                retentionSafe;

            decision.EdgeSafe =
                edgeSafe;

            decision.IsEdgeLastResort =
                !edgeSafe;

            if (edgeSafe &&
                retentionSafe &&
                (safestAlternative == null ||
                 fallbackScore >
                    safestAlternativeScore))
            {
                safestAlternative = decision;
                safestAlternativeScore =
                    fallbackScore;
            }

            if (retentionSafe &&
                (retentionSafePhysicalFallback == null ||
                 fallbackScore >
                    retentionSafePhysicalFallbackScore))
            {
                retentionSafePhysicalFallback = decision;
                retentionSafePhysicalFallbackScore =
                    fallbackScore;
            }

            if (physicalFallback == null ||
                fallbackScore >
                    physicalFallbackScore)
            {
                physicalFallback = decision;
                physicalFallbackScore =
                    fallbackScore;
            }

            if (edgeSafe &&
                (edgeSafePhysicalFallback == null ||
                 fallbackScore >
                    edgeSafePhysicalFallbackScore))
            {
                edgeSafePhysicalFallback = decision;
                edgeSafePhysicalFallbackScore =
                    fallbackScore;
            }
        }

        V5RouteDecision selected =
            safestAlternative;

        if (selected == null)
        {
            selected =
                retentionSafePhysicalFallback ??
                edgeSafePhysicalFallback ??
                physicalFallback;
        }

        if (selected == null)
        {
            return null;
        }

        bool sameAsProposed =
            proposedDecision != null &&
            proposedDecision.Candidate != null &&
            selected.Candidate.InstanceId ==
                proposedDecision.Candidate.InstanceId &&
            selected.Candidate.Generation ==
                proposedDecision.Candidate.Generation;

        if (sameAsProposed)
        {
            return selected;
        }

        RecordFailureTrace(
            $"V5LateReplanSurvivalChoice " +
            $"OldId={blockedTargetId}, " +
            $"OldGeneration={blockedTargetGeneration}, " +
            $"OldY={previousTargetY:F2}, " +
            $"NewId={selected.Candidate.InstanceId}, " +
            $"NewGeneration={selected.Candidate.Generation}, " +
            $"NewY={selected.Candidate.CurrentPosition.y:F2}, " +
            $"NewReach={selected.ReachRatio:F2}, " +
            $"NewMargin={selected.LandingMargin:F2}, " +
            $"ApexDrop={selected.ApexOvershoot:F2}, " +
            $"StableScans={selected.Candidate.ConsecutiveObservations}, " +
            $"StrictSafe={safestAlternative != null}"
        );

        return selected;
    }

    private void RecordV5MissSnapshot(
        string reason,
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        float platformX =
            currentTargetCandidate != null
                ? currentTargetCandidate
                    .CurrentPosition.x
                : 0f;

        float platformVelocityX =
            currentTargetCandidate != null
                ? currentTargetCandidate
                    .PlatformVelocityX
                : 0f;

        bool colliderEnabled = false;
        bool objectActive = false;

        try
        {
            objectActive =
                currentTargetCandidate != null &&
                currentTargetCandidate.GameObject != null &&
                currentTargetCandidate
                    .GameObject.activeInHierarchy;

            colliderEnabled =
                currentTargetCandidate != null &&
                currentTargetCandidate.Collider != null &&
                currentTargetCandidate
                    .Collider.enabled;
        }
        catch
        {
            objectActive = false;
            colliderEnabled = false;
        }

        RecordFailureTrace(
            $"V5Miss Reason={reason}, " +
            $"Id={currentTargetId}, Type={currentTargetType}, " +
            $"PlayerX={playerPosition.x:F2}, " +
            $"PlayerY={playerPosition.y:F2}, " +
            $"PlayerVX={playerVelocity.x:F2}, " +
            $"PlayerVY={playerVelocity.y:F2}, " +
            $"PlatformX={platformX:F2}, " +
            $"PredictedX={currentTargetPredictedX:F2}, " +
            $"PlatformVX={platformVelocityX:F2}, " +
            $"TargetY={currentTargetY:F2}, " +
            $"ObjectActive={objectActive}, " +
            $"ColliderEnabled={colliderEnabled}, " +
            $"RemainingTime=" +
            $"{Mathf.Max(0f, currentTargetExpectedLandingAt - Time.time):F2}"
        );
    }

    [HideFromIl2Cpp]
    private void UpdateV5TargetPrediction(
        V5RouteDecision decision)
    {
        currentTargetPredictedX =
            decision.CenterwardLandingX;

        currentTargetLandingOffsetX =
            decision.CenterwardLandingX -
            decision.LandingX;

        currentTargetY =
            decision.Candidate
                .CurrentPosition.y;

        currentTargetSafeHalfWidth =
            decision.ControlHalfWidth;

        currentTargetExpectedLandingAt =
            Time.time +
            decision.LandingTime;

        currentTargetRouteScore =
            decision.Score;

        currentTargetReachRatio =
            decision.ReachRatio;
    }

    private float ApplyCurrentTargetLandingBias(
        float platformCenterX)
    {
        float offsetMagnitude =
            Mathf.Abs(
                currentTargetLandingOffsetX
            );

        float absoluteCenterX =
            Mathf.Abs(platformCenterX);

        if (offsetMagnitude <= 0.001f ||
            absoluteCenterX <= 0.001f)
        {
            return platformCenterX;
        }

        float appliedOffset =
            Mathf.Min(
                offsetMagnitude,
                absoluteCenterX * 0.50f
            );

        return platformCenterX -
               Mathf.Sign(platformCenterX) *
                   appliedOffset;
    }

    private void UpdateCurrentTarget(
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        PlatformCandidate existingTarget =
            currentTargetId != 0
                ? planner.FindCandidateById(
                    currentTargetId
                )
                : null;

        if (existingTarget != null &&
            IsFakeCandidate(existingTarget))
        {
            RejectCandidate(
                existingTarget,
                "The platform sprite is explicitly marked fake."
            );

            ClearCurrentTarget();
            existingTarget = null;
        }

        if (existingTarget != null &&
            !IsSameLockedPlatform(existingTarget))
        {
            LogVerbose(
                $"Locked target {currentTargetId} was recycled or replaced. " +
                "Selecting a new target."
            );

            ClearCurrentTarget();
            existingTarget = null;
        }

        if (existingTarget != null)
        {
            UpdateTargetSnapshot(
                existingTarget
            );

            bool targetPassed =
                playerVelocity.y < -1f &&
                playerPosition.y <
                    currentTargetY -
                    MissedTargetVerticalMargin;

            if (targetPassed)
            {
                TemporarilyBlockCurrentTarget(
                    "The locked platform was passed while descending."
                );

                ClearCurrentTarget();
                existingTarget = null;
            }
        }

        PlatformCandidate bestCandidate =
            FindCandidateForCurrentJump(
                playerPosition
            );

        if (existingTarget != null)
        {
            bool strongUpgradeCandidate =
                currentJumpMode ==
                    JumpModeStrong &&
                Time.time <
                    strongTargetUpgradeUntilTime &&
                playerVelocity.y > 20f &&
                bestCandidate != null &&
                bestCandidate.InstanceId !=
                    currentTargetId &&
                bestCandidate.CurrentPosition.y >=
                    currentTargetY +
                    StrongTargetUpgradeHeight;

            bool initialUpgradeCandidate =
                currentJumpMode == JumpModeInitial &&
                playerVelocity.y > 5f &&
                bestCandidate != null &&
                bestCandidate.InstanceId != currentTargetId &&
                (GetPlatformTypeRank(bestCandidate.Type) >
                    GetPlatformTypeRank(currentTargetType) ||
                 bestCandidate.CurrentPosition.y >=
                    currentTargetY + 3f);

            bool upgradeCandidateAvailable =
                strongUpgradeCandidate ||
                initialUpgradeCandidate;

            string blockedUpgradeReason = "";

            bool upgradeIsSafe =
                upgradeCandidateAvailable &&
                IsTargetUpgradeSafe(
                    existingTarget,
                    bestCandidate,
                    out blockedUpgradeReason
                );

            if (!upgradeIsSafe)
            {
                if (upgradeCandidateAvailable)
                {
                    LogBlockedTargetUpgrade(
                        bestCandidate,
                        blockedUpgradeReason
                    );
                }

                return;
            }

            LogVerbose(
                $"Jump target upgraded from " +
                $"{currentTargetId} at Y={currentTargetY:F2} " +
                $"to {bestCandidate.InstanceId} at " +
                $"Y={bestCandidate.CurrentPosition.y:F2}."
            );

            ClearCurrentTarget();
        }
        else if (currentTargetId != 0 &&
                 Time.time -
                 currentTargetLastSeenTime <=
                    MissingTargetGraceSeconds)
        {
            return;
        }
        else if (currentTargetId != 0)
        {
            LogVerbose(
                "Locked target disappeared. Selecting a new target."
            );

            ClearCurrentTarget();
        }

        bool rescueTarget = false;

        bool allowHighAltitudeEarlyRescue =
            IsHighAltitudeMode() &&
            Time.time - currentJumpStartTime >=
                HighAltitudeEarlyRescueDelaySeconds &&
            playerVelocity.y <=
                HighAltitudeEarlyRescueMaximumVelocityY;

        if (bestCandidate == null &&
            (playerVelocity.y < -1f ||
             allowHighAltitudeEarlyRescue))
        {
            bestCandidate =
                FindRescueCandidate(
                    playerPosition,
                    playerVelocity
                );

            rescueTarget =
                bestCandidate != null;
        }

        if (bestCandidate == null)
        {
            RecordTargetlessSnapshot(
                playerPosition,
                playerVelocity
            );
            return;
        }

        LockTarget(
            bestCandidate,
            rescueTarget,
            playerPosition,
            playerVelocity
        );

        if (recoverySelectionRequested)
        {
            LogVerbose(
                $"Recovery route selected: " +
                $"Id={bestCandidate.InstanceId}, " +
                $"Y={bestCandidate.CurrentPosition.y:F2}."
            );

            recoverySelectionRequested = false;
            forcedRecoveryDirection = 0;
            forcedRecoveryUntilTime = 0f;
        }

        if (rescueTarget)
        {
            LogVerbose(
                "No planned landing platform was available. " +
                "Using a validated rescue trajectory while descending."
            );
        }

        LogSelectedTarget(
            bestCandidate
        );
    }

    [HideFromIl2Cpp]
    private PlatformCandidate FindCandidateForCurrentJump(
        Vector3 playerPosition)
    {
        if (recoverySelectionRequested)
        {
            PlatformCandidate recoveryCandidate =
                FindRecoveryCandidate(
                    playerPosition
                );

            if (recoveryCandidate != null)
            {
                return recoveryCandidate;
            }
        }

        if (currentJumpMode ==
            JumpModeStrong)
        {
            return FindStrongJumpCandidate(
                playerPosition
            );
        }

        if (currentJumpMode ==
            JumpModeNormal)
        {
            PlatformCandidate normalCandidate =
                FindNormalJumpCandidate(
                    playerPosition
                );

            if (normalCandidate != null ||
                !IsHighAltitudeMode())
            {
                return normalCandidate;
            }

            return FindHighAltitudeNormalFallbackCandidate(
                playerPosition
            );
        }

        return FindInitialLaunchCandidate(
            playerPosition
        );
    }

    [HideFromIl2Cpp]
    private PlatformCandidate FindInitialLaunchCandidate(
        Vector3 playerPosition)
    {
        PlatformCandidate best = null;
        float bestScore =
            float.MinValue;

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (!IsBaseCandidateUsable(
                    candidate) ||
                !candidate.Reachable ||
                candidate.CurrentPosition.y <=
                    playerPosition.y + 0.50f)
            {
                continue;
            }

            float horizontalDistance =
                Mathf.Abs(
                    GetCandidateLandingX(
                    candidate
                ) -
                    playerPosition.x
                );

            float score =
                GetPlatformTypeRank(
                    candidate.Type) * 100000f -
                horizontalDistance * 4f +
                candidate.CurrentPosition.y * 100f +
                -GetEdgeLandingPenalty(candidate) +
                GetRouteLookAheadScore(
                    candidate
                );

            if (best == null ||
                score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    [HideFromIl2Cpp]
    private PlatformCandidate FindNormalJumpCandidate(
        Vector3 playerPosition)
    {
        PlatformCandidate best = null;
        float bestScore =
            float.MinValue;

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (!IsBaseCandidateUsable(
                    candidate))
            {
                continue;
            }

            float heightGain =
                candidate.CurrentPosition.y -
                lastBounceY;

            float minimumHeightGain =
                IsHighAltitudeMode()
                    ? HighAltitudeMinimumHeightGain
                    : NormalJumpMinimumHeightGain;

            if (heightGain < minimumHeightGain)
            {
                continue;
            }

            float safeMaximumHeightGain =
                NormalBounceVelocity *
                    NormalBounceVelocity /
                (2f * GravityMagnitude) -
                NormalJumpApexSafetyMargin;

            if (heightGain >
                safeMaximumHeightGain)
            {
                continue;
            }

            float deltaX =
                GetCandidateLandingX(
                    candidate
                ) -
                lastBounceX;

            float radialDistance =
                Mathf.Sqrt(
                    deltaX * deltaX +
                    heightGain * heightGain
                );

            if (radialDistance >
                NormalJumpReachRadius)
            {
                continue;
            }

            float reachRatio =
                GetReachRatio(candidate);

            float maximumReachRatio =
                IsHighAltitudeMode()
                    ? HighAltitudeNormalReachRatio
                    : NormalJumpMaximumReachRatio;

            if (reachRatio >
                    maximumReachRatio ||
                !IsHighAltitudeCandidateSafe(
                    candidate,
                    maximumReachRatio
                ))
            {
                continue;
            }

            // Height is the primary objective. A strong platform receives
            // a modest bonus only after it is already inside the safe circle.
            float score =
                heightGain * 100f +
                GetPlatformTypeRank(
                    candidate.Type) * 100000f -
                radialDistance * 4f -
                reachRatio * 20f +
                -GetEdgeLandingPenalty(candidate) +
                GetRouteLookAheadScore(
                    candidate
                );

            if (best == null ||
                score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    [HideFromIl2Cpp]
    private PlatformCandidate FindHighAltitudeNormalFallbackCandidate(
        Vector3 playerPosition)
    {
        PlatformCandidate best = null;
        float bestScore = float.MinValue;

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (!IsBaseCandidateUsable(candidate))
            {
                continue;
            }

            float heightGain =
                candidate.CurrentPosition.y -
                lastBounceY;

            float safeMaximumHeightGain =
                NormalBounceVelocity *
                    NormalBounceVelocity /
                (2f * GravityMagnitude) -
                NormalJumpApexSafetyMargin;

            if (heightGain < HighAltitudeMinimumHeightGain ||
                heightGain > safeMaximumHeightGain)
            {
                continue;
            }

            float landingX =
                GetCandidateLandingX(candidate);

            float deltaX = landingX - lastBounceX;

            float radialDistance =
                Mathf.Sqrt(
                    deltaX * deltaX +
                    heightGain * heightGain
                );

            float reachRatio =
                GetReachRatio(candidate);

            if (radialDistance > NormalJumpReachRadius ||
                reachRatio > NormalJumpMaximumReachRatio ||
                Mathf.Abs(landingX) > 4.40f)
            {
                continue;
            }

            // This pass exists only to prevent Target=None. Unlike the normal
            // pass, center distance and reach margin can outweigh platform
            // type, so an edge Strong cannot beat a central Normal solely by
            // its type rank.
            float typeBonus =
                candidate.Type == PlatformType.Strong
                    ? 450f
                    : candidate.Type == PlatformType.Normal
                        ? 300f
                        : 0f;

            float score =
                typeBonus +
                heightGain * 50f -
                reachRatio * 600f -
                Mathf.Abs(landingX) * 80f -
                GetEdgeLandingPenalty(candidate) +
                GetRouteLookAheadScore(candidate);

            if (best == null ||
                score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        if (best != null)
        {
            RecordFailureTrace(
                $"HighAltitudeFallback Id={best.InstanceId}, " +
                $"Type={best.Type}, " +
                $"X={GetCandidateLandingX(best):F2}, " +
                $"Y={best.CurrentPosition.y:F2}, " +
                $"ReachRatio={GetReachRatio(best):F2}"
            );
        }

        return best;
    }

    [HideFromIl2Cpp]
    private PlatformCandidate FindStrongJumpCandidate(
        Vector3 playerPosition)
    {
        PlatformCandidate preferred =
            FindStrongJumpCandidatePass(
                playerPosition,
                true
            );

        if (preferred != null)
        {
            return preferred;
        }

        PlatformCandidate safeFallback =
            FindStrongJumpCandidatePass(
                playerPosition,
                false
            );

        if (safeFallback != null)
        {
            return safeFallback;
        }

        // Higher platforms can appear slightly after a strong bounce as the
        // camera moves upward. Wait briefly before accepting a low platform.
        if (Time.time -
            currentJumpStartTime <
            0.45f)
        {
            return null;
        }

        return FindStrongEmergencyCandidate(
            playerPosition
        );
    }

    [HideFromIl2Cpp]
    private PlatformCandidate FindStrongJumpCandidatePass(
        Vector3 playerPosition,
        bool preferredHeightOnly)
    {
        PlatformCandidate best = null;
        float bestScore =
            float.MinValue;

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (!IsBaseCandidateUsable(
                    candidate))
            {
                continue;
            }

            float heightGain =
                candidate.CurrentPosition.y -
                lastBounceY;

            float minimumHeight =
                preferredHeightOnly
                    ? StrongJumpPreferredHeightGain
                    : StrongJumpMinimumHeightGain;

            if (heightGain <
                    minimumHeight ||
                heightGain >
                    StrongJumpMaximumHeightGain)
            {
                continue;
            }

            float reachRatio =
                GetReachRatio(candidate);

            float maximumReachRatio =
                IsHighAltitudeMode()
                    ? HighAltitudeStrongReachRatio
                    : StrongJumpMaximumReachRatio;

            float reachMargin =
                candidate.MaximumHorizontalReach -
                Mathf.Abs(
                    GetCandidateLandingX(
                        candidate
                    ) -
                    lastBounceX
                );

            if (reachRatio >
                    maximumReachRatio ||
                reachMargin <
                    StrongJumpMinimumReachMargin ||
                !IsHighAltitudeCandidateSafe(
                    candidate,
                    maximumReachRatio
                ))
            {
                continue;
            }

            // A strong bounce should gain meaningful height. Horizontal
            // safety is the second priority; target platform type is only
            // a tie-breaker.
            float score =
                heightGain * 100f -
                reachRatio * 35f +
                reachMargin * 2f +
                GetPlatformTypeRank(
                    candidate.Type) * 100000f +
                -GetEdgeLandingPenalty(candidate) +
                GetRouteLookAheadScore(
                    candidate
                );

            if (best == null ||
                score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    [HideFromIl2Cpp]
    private PlatformCandidate FindStrongEmergencyCandidate(
        Vector3 playerPosition)
    {
        PlatformCandidate best = null;
        float bestScore =
            float.MinValue;

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (!IsBaseCandidateUsable(
                    candidate))
            {
                continue;
            }

            float heightGain =
                candidate.CurrentPosition.y -
                lastBounceY;

            float minimumHeightGain =
                IsHighAltitudeMode()
                    ? HighAltitudeMinimumHeightGain
                    : NormalJumpMinimumHeightGain;

            if (heightGain < minimumHeightGain ||
                heightGain >
                    StrongJumpMaximumHeightGain)
            {
                continue;
            }

            float reachRatio =
                GetReachRatio(candidate);

            float reachMargin =
                candidate.MaximumHorizontalReach -
                Mathf.Abs(
                    GetCandidateLandingX(
                        candidate
                    ) -
                    lastBounceX
                );

            float maximumReachRatio =
                IsHighAltitudeMode()
                    ? 0.78f
                    : 0.75f;

            if (reachRatio > maximumReachRatio ||
                reachMargin <
                    1.25f ||
                (IsHighAltitudeMode() &&
                 Mathf.Abs(GetCandidateLandingX(candidate)) >
                    4.35f))
            {
                continue;
            }

            float score =
                heightGain * 100f -
                reachRatio * 50f +
                GetPlatformTypeRank(
                    candidate.Type) * 100000f +
                -GetEdgeLandingPenalty(candidate) +
                GetRouteLookAheadScore(
                    candidate
                );

            if (best == null ||
                score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    [HideFromIl2Cpp]
    private bool IsBaseCandidateUsable(
        PlatformCandidate candidate)
    {
        if (candidate == null ||
            IsFakeCandidate(candidate) ||
            IsRejectedCandidate(candidate) ||
            GetPlatformTypeRank(
                candidate.Type) <= 0)
        {
            return false;
        }

        if (candidate.InstanceId ==
                failedTargetId &&
            candidate.Generation ==
                failedTargetGeneration &&
            Time.time <
                failedTargetUntilTime)
        {
            return false;
        }

        return true;
    }

    [HideFromIl2Cpp]
    private bool IsFakeCandidate(
        PlatformCandidate candidate)
    {
        if (candidate == null ||
            string.IsNullOrEmpty(
                candidate.SpriteName))
        {
            return false;
        }

        return candidate.SpriteName.Equals(
            FakePlatformSpriteName,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [HideFromIl2Cpp]
    private bool IsRejectedCandidate(
        PlatformCandidate candidate)
    {
        if (candidate == null)
        {
            return true;
        }

        if (rejectedTargetKeys.Contains(
                GetPlatformIdentityKey(
                    candidate.InstanceId,
                    candidate.Generation
                )))
        {
            return true;
        }

        for (int index = 0;
             index < rejectedTargetYs.Count;
             index++)
        {
            if (rejectedTargetTypes[index] ==
                    candidate.Type &&
                Mathf.Abs(
                    rejectedTargetYs[index] -
                    candidate.CurrentPosition.y
                ) <=
                RejectedPlatformYTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private void RejectCurrentTarget(
        string reason)
    {
        TemporarilyBlockCurrentTarget(
            reason
        );
    }

    private void TemporarilyBlockCurrentTarget(
        string reason)
    {
        if (currentTargetId == 0)
        {
            return;
        }

        failedTargetId =
            currentTargetId;

        failedTargetGeneration =
            currentTargetGeneration;

        failedTargetUntilTime =
            Time.time +
            FailedTargetCooldownSeconds;

        RecordFailureTrace(
            $"TargetBlocked Id={currentTargetId}, " +
            $"Generation={currentTargetGeneration}, " +
            $"Type={currentTargetType}, Y={currentTargetY:F2}, " +
            $"Reason={reason}"
        );

        LogVerbose(
            $"Route attempt temporarily blocked: " +
            $"Id={currentTargetId}, " +
            $"Type={currentTargetType}, " +
            $"Y={currentTargetY:F2}. " +
            $"{reason}"
        );
    }

    [HideFromIl2Cpp]
    private void RejectCandidate(
        PlatformCandidate candidate,
        string reason)
    {
        if (candidate == null)
        {
            return;
        }

        rejectedTargetKeys.Add(
            GetPlatformIdentityKey(
                candidate.InstanceId,
                candidate.Generation
            )
        );

        AddRejectedSignature(
            candidate.CurrentPosition.y,
            candidate.Type
        );

        LogVerbose(
            $"Route node rejected: " +
            $"Id={candidate.InstanceId}, " +
            $"Generation={candidate.Generation}, " +
            $"Type={candidate.Type}, " +
            $"Sprite={candidate.SpriteName}, " +
            $"Y={candidate.CurrentPosition.y:F2}. " +
            $"{reason}"
        );
    }

    private void AddRejectedSignature(
        float platformY,
        PlatformType platformType)
    {
        for (int index = 0;
             index < rejectedTargetYs.Count;
             index++)
        {
            if (rejectedTargetTypes[index] ==
                    platformType &&
                Mathf.Abs(
                    rejectedTargetYs[index] -
                    platformY
                ) <=
                RejectedPlatformYTolerance)
            {
                return;
            }
        }

        rejectedTargetYs.Add(
            platformY
        );

        rejectedTargetTypes.Add(
            platformType
        );
    }

    [HideFromIl2Cpp]
    private PlatformCandidate FindRecoveryCandidate(
        Vector3 playerPosition)
    {
        float preferredReachRatio =
            IsHighAltitudeMode()
                ? HighAltitudeRecoveryPreferredReachRatio
                : RecoveryPreferredReachRatio;

        float absoluteReachRatio =
            IsHighAltitudeMode()
                ? HighAltitudeRecoveryAbsoluteReachRatio
                : RecoveryAbsoluteReachRatio;

        PlatformCandidate preferred =
            FindRecoveryCandidatePass(
                playerPosition,
                preferredReachRatio,
                true
            );

        if (preferred != null)
        {
            return preferred;
        }

        return FindRecoveryCandidatePass(
            playerPosition,
            absoluteReachRatio,
            false
        );
    }

    [HideFromIl2Cpp]
    private PlatformCandidate FindRecoveryCandidatePass(
        Vector3 playerPosition,
        float maximumReachRatio,
        bool requireSeparation)
    {
        PlatformCandidate best = null;
        float bestScore =
            float.MinValue;

        float launchVelocity =
            currentJumpMode ==
                JumpModeStrong
                    ? StrongBounceVelocity
                    : NormalBounceVelocity;

        float maximumHeightGain =
            currentJumpMode ==
                JumpModeStrong
                    ? StrongJumpMaximumHeightGain
                    : NormalJumpReachRadius + 0.75f;

        float minimumHeightGain =
            IsHighAltitudeMode()
                ? HighAltitudeMinimumHeightGain
                : NormalJumpMinimumHeightGain;

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (!IsBaseCandidateUsable(
                    candidate))
            {
                continue;
            }

            float heightGain =
                candidate.CurrentPosition.y -
                lastBounceY;

            if (heightGain < minimumHeightGain ||
                heightGain >
                    maximumHeightGain)
            {
                continue;
            }

            float landingTime =
                EstimateAscendingLandingTime(
                    launchVelocity,
                    heightGain
                );

            if (landingTime <= 0f)
            {
                continue;
            }

            float landingX =
                PredictPlatformLandingX(
                    candidate,
                    landingTime
                );

            float horizontalDistance =
                Mathf.Abs(
                    landingX -
                    lastBounceX
                );

            float maximumReach =
                HorizontalMoveSpeed *
                landingTime;

            if (maximumReach <= 0.01f)
            {
                continue;
            }

            float reachRatio =
                horizontalDistance /
                maximumReach;

            if (reachRatio >
                    maximumReachRatio ||
                !IsHighAltitudeCandidateSafe(
                    candidate,
                    maximumReachRatio
                ))
            {
                continue;
            }

            float horizontalSeparation =
                Mathf.Abs(
                    landingX -
                    lastBounceX
                );

            if (requireSeparation &&
                horizontalSeparation <
                    RecoveryMinimumHorizontalSeparation)
            {
                continue;
            }

            float safetyMargin =
                maximumReach -
                horizontalDistance;

            float score =
                heightGain * 90f +
                horizontalSeparation * 18f +
                safetyMargin * 6f -
                reachRatio * 45f +
                GetPlatformTypeRank(
                    candidate.Type) * 100000f +
                -GetEdgeLandingPenalty(candidate) +
                GetRouteLookAheadScore(
                    candidate
                );

            if (candidate.Type ==
                PlatformType.Breakable)
            {
                score -= 35f;
            }

            if (best == null ||
                score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    [HideFromIl2Cpp]
    private float GetRouteLookAheadScore(
        PlatformCandidate candidate)
    {
        if (candidate == null)
        {
            return -RouteDeadEndPenalty;
        }

        bool hasKnownFinish =
            !float.IsInfinity(
                finishHeight
            ) &&
            !float.IsNaN(
                finishHeight
            ) &&
            finishHeight > 0f;

        float finishApproachMargin =
            hasKnownFinish
                ? Mathf.Clamp(
                    finishHeight * 0.04f,
                    MinimumFinishApproachMargin,
                    MaximumFinishApproachMargin
                )
                : 0f;

        bool candidateFinishesRun =
            hasKnownFinish &&
            candidate.CurrentPosition.y >=
                finishHeight -
                FinishLandingTolerance;

        if (candidateFinishesRun)
        {
            return candidate.Type ==
                    PlatformType.Breakable
                        ? 210f
                        : 300f;
        }

        bool candidateApproachesFinish =
            hasKnownFinish &&
            candidate.CurrentPosition.y >=
                finishHeight -
                finishApproachMargin;

        int routeOptions = 0;
        float bestFutureGain = 0f;
        float bestFutureSafety = 0f;

        foreach (PlatformCandidate successor
                 in planner.Candidates)
        {
            if (successor == null ||
                successor.InstanceId ==
                    candidate.InstanceId ||
                !IsBaseCandidateUsable(
                    successor))
            {
                continue;
            }

            float safetyRatio;

            if (!CanReachHypotheticalSuccessor(
                    candidate,
                    successor,
                    out safetyRatio))
            {
                continue;
            }

            routeOptions++;

            float futureGain =
                successor.CurrentPosition.y -
                candidate.CurrentPosition.y;

            bestFutureGain =
                Mathf.Max(
                    bestFutureGain,
                    futureGain
                );

            bestFutureSafety =
                Mathf.Max(
                    bestFutureSafety,
                    1f - safetyRatio
                );
        }

        if (routeOptions == 0)
        {
            return candidateApproachesFinish
                ? -35f
                : -RouteDeadEndPenalty;
        }

        float approachBonus =
            candidateApproachesFinish
                ? 45f
                : 0f;

        return
            Mathf.Min(routeOptions, 4) *
                RouteOptionBonus +
            bestFutureGain *
                RouteBestGainBonus +
            bestFutureSafety * 25f +
            approachBonus;
    }

    [HideFromIl2Cpp]
    private bool CanReachHypotheticalSuccessor(
        PlatformCandidate source,
        PlatformCandidate successor,
        out float horizontalRatio)
    {
        horizontalRatio =
            float.MaxValue;

        float heightGain =
            successor.CurrentPosition.y -
            source.CurrentPosition.y;

        float minimumHeightGain =
            IsHighAltitudeMode()
                ? HighAltitudeMinimumHeightGain
                : NormalJumpMinimumHeightGain;

        if (heightGain < minimumHeightGain)
        {
            return false;
        }

        float launchVelocity =
            source.Type ==
                PlatformType.Golden
                    ? GoldenBounceVelocity
                    : source.Type ==
                        PlatformType.Strong
                            ? StrongBounceVelocity
                            : NormalBounceVelocity;

        float discriminant =
            launchVelocity *
                launchVelocity -
            2f *
                GravityMagnitude *
                heightGain;

        if (discriminant <= 0f)
        {
            return false;
        }

        float landingTime =
            (launchVelocity +
             Mathf.Sqrt(discriminant)) /
            GravityMagnitude;

        if (landingTime <= 0f)
        {
            return false;
        }

        float sourceX =
            GetCandidateLandingX(
                source
            );

        float successorX =
            PredictPlatformLandingX(
                successor,
                landingTime
            );

        float maximumHorizontalReach =
            HorizontalMoveSpeed *
            landingTime *
            HypotheticalReachSafetyRatio;

        if (maximumHorizontalReach <=
            0.01f)
        {
            return false;
        }

        horizontalRatio =
            Mathf.Abs(
                successorX -
                sourceX
            ) /
            maximumHorizontalReach;

        return horizontalRatio <= 1f;
    }

    private float EstimateAscendingLandingTime(
        float launchVelocity,
        float heightGain)
    {
        float discriminant =
            launchVelocity *
                launchVelocity -
            2f *
                GravityMagnitude *
                heightGain;

        if (discriminant <= 0f)
        {
            return -1f;
        }

        return
            (launchVelocity +
             Mathf.Sqrt(discriminant)) /
            GravityMagnitude;
    }

    [HideFromIl2Cpp]
    private float PredictPlatformLandingX(
        PlatformCandidate candidate,
        float landingTime)
    {
        if (candidate == null)
        {
            return 0f;
        }

        if (!candidate.IsMoving ||
            Mathf.Abs(
                candidate.PlatformVelocityX
            ) < 0.01f ||
            landingTime <= 0f)
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
            landingTime
        );
    }

    [HideFromIl2Cpp]
    private float GetCandidateLandingX(
        PlatformCandidate candidate)
    {
        if (candidate == null)
        {
            return 0f;
        }

        float landingTime =
            candidate.EstimatedLandingTime;

        if (landingTime <= 0f ||
            float.IsInfinity(landingTime) ||
            float.IsNaN(landingTime))
        {
            return Mathf.Clamp(
                candidate.CurrentPosition.x,
                -HorizontalWorldLimit,
                HorizontalWorldLimit
            );
        }

        return PredictPlatformLandingX(
            candidate,
            landingTime
        );
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

        if (width <= 0.01f)
        {
            return Mathf.Clamp(
                currentX,
                minimumX,
                maximumX
            );
        }

        float raw =
            currentX -
            minimumX +
            velocityX *
            Mathf.Max(0f, time);

        float period =
            width * 2f;

        raw =
            raw % period;

        if (raw < 0f)
        {
            raw += period;
        }

        if (raw > width)
        {
            raw =
                period -
                raw;
        }

        return minimumX + raw;
    }

    private float GetLandingSafeHalfWidth(
        PlatformType type)
    {
        if (type == PlatformType.Strong ||
            type == PlatformType.Golden)
        {
            return StrongLandingSafeHalfWidth;
        }

        if (type == PlatformType.Breakable)
        {
            return BreakableLandingSafeHalfWidth;
        }

        return NormalLandingSafeHalfWidth;
    }

    [HideFromIl2Cpp]
    private float GetReachRatio(
        PlatformCandidate candidate)
    {
        if (candidate == null ||
            candidate.MaximumHorizontalReach <=
                0.01f)
        {
            return float.MaxValue;
        }

        float effectiveDistance =
            Mathf.Abs(
                GetCandidateLandingX(
                    candidate
                ) -
                lastBounceX
            );

        return
            effectiveDistance /
            candidate.MaximumHorizontalReach;
    }

    [HideFromIl2Cpp]
    private float GetEdgeLandingPenalty(
        PlatformCandidate candidate)
    {
        float preferredWorldLimit =
            GetPreferredLandingWorldLimit();

        float edgeOverflow =
            Mathf.Abs(GetCandidateLandingX(candidate)) -
            preferredWorldLimit;

        float penaltyMultiplier =
            IsHighAltitudeMode()
                ? 1.75f
                : 1f;

        return Mathf.Max(0f, edgeOverflow) *
            EdgeLandingPenaltyPerUnit *
            penaltyMultiplier;
    }

    private bool IsHighAltitudeMode()
    {
        return highestObservedPlayerY >=
            HighAltitudeStartY;
    }

    private float GetPreferredLandingWorldLimit()
    {
        return IsHighAltitudeMode()
            ? HighAltitudePreferredWorldLimit
            : PreferredLandingWorldLimit;
    }

    [HideFromIl2Cpp]
    private bool IsHighAltitudeCandidateSafe(
        PlatformCandidate candidate,
        float maximumReachRatio)
    {
        if (!IsHighAltitudeMode())
        {
            return true;
        }

        if (candidate == null ||
            Mathf.Abs(GetCandidateLandingX(candidate)) >
                HighAltitudeAbsoluteWorldLimit)
        {
            return false;
        }

        return GetReachRatio(candidate) <=
            maximumReachRatio;
    }

    [HideFromIl2Cpp]
    private bool IsTargetUpgradeSafe(
        PlatformCandidate existingTarget,
        PlatformCandidate proposedTarget,
        out string blockedReason)
    {
        blockedReason = "";

        if (existingTarget == null ||
            proposedTarget == null)
        {
            blockedReason = "MissingTarget";
            return false;
        }

        int existingRank =
            GetPlatformTypeRank(existingTarget.Type);

        int proposedRank =
            GetPlatformTypeRank(proposedTarget.Type);

        if (proposedRank < existingRank)
        {
            blockedReason = "PlatformTypeDowngrade";
            return false;
        }

        // A real platform-type upgrade remains authoritative. This preserves
        // the requested Strong > Normal > Breakable ordering.
        if (proposedRank > existingRank)
        {
            return true;
        }

        float existingLandingX =
            GetCandidateLandingX(existingTarget);

        float proposedLandingX =
            GetCandidateLandingX(proposedTarget);

        float preferredWorldLimit =
            GetPreferredLandingWorldLimit();

        float existingEdgeExposure =
            Mathf.Max(
                0f,
                Mathf.Abs(existingLandingX) -
                    preferredWorldLimit
            );

        float proposedEdgeExposure =
            Mathf.Max(
                0f,
                Mathf.Abs(proposedLandingX) -
                    preferredWorldLimit
            );

        if (proposedEdgeExposure >
            existingEdgeExposure +
                UpgradeMaximumAdditionalEdgeExposure)
        {
            blockedReason = "HigherEdgeExposure";
            return false;
        }

        float existingReachRatio =
            GetReachRatio(existingTarget);

        float proposedReachRatio =
            GetReachRatio(proposedTarget);

        if (IsHighAltitudeMode() &&
            proposedReachRatio >
                existingReachRatio - 0.08f &&
            proposedEdgeExposure >=
                existingEdgeExposure - 0.15f)
        {
            blockedReason = "HighAltitudeTargetStability";
            return false;
        }

        if (proposedReachRatio > 0.55f &&
            proposedReachRatio >
                existingReachRatio +
                    UpgradeMaximumReachRatioIncrease)
        {
            blockedReason = "HigherReachRisk";
            return false;
        }

        float existingRouteScore =
            GetRouteLookAheadScore(existingTarget);

        float proposedRouteScore =
            GetRouteLookAheadScore(proposedTarget);

        if (proposedRouteScore +
                UpgradeRouteScoreTolerance <
            existingRouteScore)
        {
            blockedReason = "LowerRouteSafety";
            return false;
        }

        return true;
    }

    [HideFromIl2Cpp]
    private void LogBlockedTargetUpgrade(
        PlatformCandidate proposedTarget,
        string blockedReason)
    {
        if (proposedTarget == null ||
            proposedTarget.InstanceId ==
                lastBlockedUpgradeCandidateId)
        {
            return;
        }

        lastBlockedUpgradeCandidateId =
            proposedTarget.InstanceId;

        if (blockedReason ==
            "PlatformTypeDowngrade")
        {
            runBlockedDowngrades++;
        }
        else
        {
            runBlockedUnsafeUpgrades++;
        }

        LogVerbose(
            $"Target upgrade blocked: " +
            $"FromId={currentTargetId}, " +
            $"FromType={currentTargetType}, " +
            $"FromX={currentTargetPredictedX:F2}, " +
            $"ToId={proposedTarget.InstanceId}, " +
            $"ToType={proposedTarget.Type}, " +
            $"ToX={GetCandidateLandingX(proposedTarget):F2}, " +
            $"Reason={blockedReason}."
        );
    }

    [HideFromIl2Cpp]
    private bool IsSameLockedPlatform(
        PlatformCandidate candidate)
    {
        if (candidate == null ||
            IsFakeCandidate(candidate) ||
            IsRejectedCandidate(candidate))
        {
            return false;
        }

        bool sameHeight =
            Mathf.Abs(
                candidate.CurrentPosition.y -
                currentTargetAnchorY
            ) <=
            RecycledTargetHeightTolerance;

        bool sameType =
            candidate.Type ==
            currentTargetAnchorType;

        return sameHeight && sameType;
    }

    [HideFromIl2Cpp]
    private void LockTarget(
        PlatformCandidate candidate,
        bool rescueTarget,
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        runTargetSelections++;

        if (candidate.Type ==
                PlatformType.Strong ||
            candidate.Type ==
                PlatformType.Golden)
        {
            runStrongTargets++;
        }
        else if (candidate.Type ==
                 PlatformType.Breakable)
        {
            runBreakableTargets++;
        }
        else
        {
            runNormalTargets++;
        }

        if (Mathf.Abs(
                GetCandidateLandingX(candidate)
            ) > PreferredLandingWorldLimit)
        {
            runEdgeTargets++;
        }

        currentTargetId =
            candidate.InstanceId;

        currentTargetAnchorY =
            candidate.CurrentPosition.y;

        currentTargetAnchorType =
            candidate.Type;

        currentTargetIsRescue =
            rescueTarget;

        currentTargetSafeHalfWidth =
            GetLandingSafeHalfWidth(
                candidate.Type
            );

        targetLockedTime =
            Time.time;

        UpdateTargetSnapshot(
            candidate
        );

        float initialError =
            currentTargetPredictedX -
            playerPosition.x;

        targetInitialDirection =
            initialError > currentTargetSafeHalfWidth
                ? 1
                : initialError < -currentTargetSafeHalfWidth
                    ? -1
                    : 0;

        targetMinimumHoldUntil =
            Time.time +
            (rescueTarget ||
             recoverySelectionRequested
                ? RecoveryInitialHoldSeconds
                : NormalInitialHoldSeconds);

        targetControlPhase =
            rescueTarget ||
            recoverySelectionRequested
                ? 2
                : 0;

        if (rescueTarget)
        {
            currentTargetPredictedX =
                CalculateRescueLandingX(
                    candidate,
                    playerPosition,
                    playerVelocity
                );
        }

        RecordFailureTrace(
            $"TargetLocked Id={currentTargetId}, " +
            $"Type={currentTargetType}, " +
            $"X={currentTargetPredictedX:F2}, " +
            $"Y={currentTargetY:F2}, " +
            $"ReachRatio={GetReachRatio(candidate):F2}, " +
            $"Rescue={rescueTarget}, " +
            $"HighAltitude={IsHighAltitudeMode()}"
        );
    }

    [HideFromIl2Cpp]
    private PlatformCandidate FindRescueCandidate(
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        PlatformCandidate best = null;
        float bestScore =
            float.MinValue;

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (!IsBaseCandidateUsable(
                    candidate))
            {
                continue;
            }

            float candidateY =
                candidate.CurrentPosition.y;

            float verticalDrop =
                playerPosition.y -
                candidateY;

            float maximumUpwardTarget =
                IsHighAltitudeMode()
                    ? HighAltitudeRescueMaximumUpwardTarget
                    : 0.25f;

            if (verticalDrop < -maximumUpwardTarget ||
                verticalDrop >
                    RescueMaximumDrop)
            {
                continue;
            }

            float fallTime =
                EstimateDescendingTime(
                    verticalDrop,
                    playerVelocity.y
                );

            if (fallTime <= 0f ||
                fallTime > 2.5f)
            {
                continue;
            }

            float predictedX =
                CalculateRescueLandingX(
                    candidate,
                    playerPosition,
                    playerVelocity
                );

            float horizontalDistance =
                Mathf.Abs(
                    predictedX -
                    playerPosition.x
                );

            float maximumReach =
                HorizontalMoveSpeed *
                fallTime *
                (IsHighAltitudeMode()
                    ? 0.82f
                    : RescueHorizontalSafetyRatio) +
                0.65f;

            if (horizontalDistance >
                    maximumReach ||
                (IsHighAltitudeMode() &&
                 Mathf.Abs(predictedX) >
                    HighAltitudeAbsoluteWorldLimit))
            {
                continue;
            }

            float routeScore =
                GetRouteLookAheadScore(
                    candidate
                );

            float score =
                candidateY * 100f -
                horizontalDistance * 14f +
                routeScore +
                GetPlatformTypeRank(
                    candidate.Type) * 100000f -
                GetEdgeLandingPenalty(candidate);

            if (best == null ||
                score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    [HideFromIl2Cpp]
    private float CalculateRescueLandingX(
        PlatformCandidate candidate,
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        float verticalDrop =
            playerPosition.y -
            candidate.CurrentPosition.y;

        float fallTime =
            EstimateDescendingTime(
                verticalDrop,
                playerVelocity.y
            );

        return PredictPlatformLandingX(
            candidate,
            fallTime
        );
    }

    private float EstimateDescendingTime(
        float verticalDrop,
        float velocityY)
    {
        float discriminant =
            velocityY *
                velocityY +
            2f *
                GravityMagnitude *
                verticalDrop;

        if (discriminant < 0f)
        {
            return -1f;
        }

        return
            (velocityY +
             Mathf.Sqrt(discriminant)) /
            GravityMagnitude;
    }

    private int GetPlatformTypeRank(
        PlatformType type)
    {
        if (type == PlatformType.Golden)
        {
            return 4;
        }

        if (type == PlatformType.Strong)
        {
            return 3;
        }

        if (type == PlatformType.Normal)
        {
            return 2;
        }

        if (type == PlatformType.Breakable)
        {
            return 1;
        }

        return 0;
    }

    [HideFromIl2Cpp]
    private void UpdateTargetSnapshot(
        PlatformCandidate candidate)
    {
        currentTargetPredictedX =
            GetCandidateLandingX(
                candidate
            );

        currentTargetY =
            candidate.CurrentPosition.y;

        currentTargetType =
            candidate.Type;

        currentTargetSpriteName =
            candidate.SpriteName;

        currentTargetLastSeenTime =
            Time.time;
    }

    [HideFromIl2Cpp]
    private void LogSelectedTarget(
        PlatformCandidate candidate)
    {
        if (!ClimberLog.IsDeveloperMode)
        {
            return;
        }

        string direction =
            GetDirectionText(
                candidate.MovementDirectionX
            );

        LogVerbose(
            $"Target selected: " +
            $"Id={candidate.InstanceId}, " +
            $"Generation={candidate.Generation}, " +
            $"Type={candidate.Type}, " +
            $"Sprite={candidate.SpriteName}, " +
            $"CurrentX=" +
            $"{candidate.CurrentPosition.x:F2}, " +
            $"Y={candidate.CurrentPosition.y:F2}, " +
            $"Moving={candidate.IsMoving}, " +
            $"Direction={direction}, " +
            $"PlatformVelocityX=" +
            $"{candidate.PlatformVelocityX:F2}, " +
            $"LandingTime=" +
            $"{candidate.EstimatedLandingTime:F2}, " +
            $"RawPredictedX=" +
            $"{candidate.PredictedLandingX:F2}, " +
            $"EffectiveLandingX=" +
            $"{GetCandidateLandingX(candidate):F2}, " +
            $"SafeHalfWidth=" +
            $"{GetLandingSafeHalfWidth(candidate.Type):F2}, " +
            $"RequiredDistance=" +
            $"{candidate.RequiredHorizontalDistance:F2}, " +
            $"MaximumReach=" +
            $"{candidate.MaximumHorizontalReach:F2}, " +
            $"Priority=" +
            $"{candidate.PriorityScore:F2}, " +
            $"StableScans={candidate.ConsecutiveObservations}"
        );
    }

    private void LogTopCandidates(
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        List<PlatformCandidate> reachable =
            new List<PlatformCandidate>();

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (candidate.Reachable &&
                IsBaseCandidateUsable(
                    candidate))
            {
                reachable.Add(candidate);
            }
        }

        reachable.Sort(
            (left, right) =>
                right.PriorityScore.CompareTo(
                    left.PriorityScore
                )
        );

        LogVerbose(
            $"Candidate snapshot: " +
            $"Player=({playerPosition.x:F2}," +
            $"{playerPosition.y:F2}), " +
            $"Velocity=({playerVelocity.x:F2}," +
            $"{playerVelocity.y:F2}), " +
            $"Total={planner.Candidates.Count}, " +
            $"Reachable={reachable.Count}, " +
            $"LockedTarget=" +
            $"{(currentTargetId != 0 ? currentTargetId.ToString() : "None")}"
        );

        int numberToLog =
            Mathf.Min(
                5,
                reachable.Count
            );

        for (int index = 0;
             index < numberToLog;
             index++)
        {
            PlatformCandidate candidate =
                reachable[index];

            string direction =
                GetDirectionText(
                    candidate.MovementDirectionX
                );

            LogVerbose(
                $"Top candidate {index + 1}: " +
                $"Id={candidate.InstanceId}, " +
                $"Generation={candidate.Generation}, " +
                $"Type={candidate.Type}, " +
                $"CurrentX=" +
                $"{candidate.CurrentPosition.x:F2}, " +
                $"Y={candidate.CurrentPosition.y:F2}, " +
                $"Direction={direction}, " +
                $"LandingTime=" +
                $"{candidate.EstimatedLandingTime:F2}, " +
                $"PredictedX=" +
                $"{GetCandidateLandingX(candidate):F2}, " +
                $"RequiredDistance=" +
                $"{candidate.RequiredHorizontalDistance:F2}, " +
                $"MaximumReach=" +
                $"{candidate.MaximumHorizontalReach:F2}, " +
                $"Priority=" +
                $"{candidate.PriorityScore:F2}"
            );
        }

        if (numberToLog == 0)
        {
            LogVerbose(
                "Candidate snapshot: " +
                (currentTargetId != 0
                    ? "no new reachable candidates; " +
                      "the locked target remains active."
                    : "no reachable candidates and no locked target.")
            );
        }
    }

    [HideFromIl2Cpp]
    private void RememberLifecycleHazard(
        float platformY,
        PlatformType platformType)
    {
        float expiresAt =
            Time.time +
            LifecycleHazardLifetimeSeconds;

        for (int index = 0;
             index < lifecycleHazardYs.Count;
             index++)
        {
            if (Mathf.Abs(
                    lifecycleHazardYs[index] -
                    platformY
                ) <= 0.50f &&
                IsLogicalTargetTypeCompatible(
                    lifecycleHazardTypes[index],
                    platformType
                ))
            {
                lifecycleHazardExpiresAt[index] =
                    expiresAt;
                return;
            }
        }

        lifecycleHazardYs.Add(platformY);
        lifecycleHazardTypes.Add(platformType);
        lifecycleHazardExpiresAt.Add(expiresAt);

        while (lifecycleHazardYs.Count > 8)
        {
            lifecycleHazardYs.RemoveAt(0);
            lifecycleHazardTypes.RemoveAt(0);
            lifecycleHazardExpiresAt.RemoveAt(0);
        }
    }

    private void ClearLifecycleHazards()
    {
        lifecycleHazardYs.Clear();
        lifecycleHazardTypes.Clear();
        lifecycleHazardExpiresAt.Clear();
    }

    private void ApplyLifecycleHazardPenalties()
    {
        for (int index =
                 lifecycleHazardYs.Count - 1;
             index >= 0;
             index--)
        {
            if (Time.time <=
                lifecycleHazardExpiresAt[index])
            {
                continue;
            }

            lifecycleHazardYs.RemoveAt(index);
            lifecycleHazardTypes.RemoveAt(index);
            lifecycleHazardExpiresAt.RemoveAt(index);
        }

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (candidate == null)
            {
                continue;
            }

            candidate.LifecycleHazardPenalty = 0f;

            for (int index = 0;
                 index < lifecycleHazardYs.Count;
                 index++)
            {
                float heightDistance =
                    Mathf.Abs(
                        candidate.CurrentPosition.y -
                        lifecycleHazardYs[index]
                    );

                if (heightDistance >
                    LifecycleHazardHeightTolerance)
                {
                    continue;
                }

                float lifetimeRatio =
                    Mathf.Clamp01(
                        (lifecycleHazardExpiresAt[index] -
                         Time.time) /
                        LifecycleHazardLifetimeSeconds
                    );

                float typeFactor =
                    IsLogicalTargetTypeCompatible(
                        lifecycleHazardTypes[index],
                        candidate.Type
                    )
                        ? 1f
                        : 0.70f;

                float penalty =
                    1200f *
                    (1f -
                     heightDistance /
                        LifecycleHazardHeightTolerance) *
                    lifetimeRatio *
                    typeFactor;

                candidate.LifecycleHazardPenalty =
                    Mathf.Max(
                        candidate.LifecycleHazardPenalty,
                        penalty
                    );
            }
        }
    }

    private void ObservePlatformGenerationResets()
    {
        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (candidate == null ||
                !candidate.RecentlyRecycled)
            {
                continue;
            }

            long key = GetPlatformIdentityKey(
                candidate.InstanceId,
                candidate.Generation
            );

            if (!observedGenerationResetKeys.Add(key))
            {
                continue;
            }

            runGenerationResets++;
        }
    }

    private long GetPlatformIdentityKey(
        int instanceId,
        int generation)
    {
        return ((long)instanceId << 32) ^
               (uint)generation;
    }

    private void UpdateAutomaticHorizontalControl(
        PlayerMovement playerMovement,
        Rigidbody2D playerRigidbody,
        Vector3 playerPosition)
    {
        if (currentTargetId == 0)
        {
            bool targetlessAirborne =
                !playerMovement.IsGrounded();

            UpdateTargetlessAirborneTracking(
                targetlessAirborne
            );

            if (Mathf.Abs(playerPosition.x) >=
                EmergencyEdgeEscapeX)
            {
                desiredHorizontalDirection =
                    playerPosition.x < 0f
                        ? 1f
                        : -1f;

                ApplyHorizontalControl(
                    playerMovement,
                    playerRigidbody,
                    desiredHorizontalDirection
                );

                return;
            }

            if (recoverySelectionRequested)
            {
                if (Time.time >
                    forcedRecoveryUntilTime)
                {
                    forcedRecoveryDirection =
                        ChooseEmergencyEscapeDirection(
                            playerPosition
                        );

                    forcedRecoveryUntilTime =
                        Time.time +
                        RecoveryForcedControlSeconds;
                }

                int escapeDirection =
                    forcedRecoveryDirection != 0
                        ? forcedRecoveryDirection
                        : ChooseEmergencyEscapeDirection(
                            playerPosition
                        );

                desiredHorizontalDirection =
                    escapeDirection;

                ApplyHorizontalControl(
                    playerMovement,
                    playerRigidbody,
                    desiredHorizontalDirection
                );

                return;
            }

            // The camera can need a few frames to reveal the next platforms
            // after a high bounce. Keep drifting toward the safe center while
            // no target exists instead of stopping just inside the emergency
            // edge threshold and falling vertically.
            if (targetlessAirborne &&
                Mathf.Abs(playerPosition.x) >
                    TargetlessCenteringDeadZone)
            {
                desiredHorizontalDirection =
                    playerPosition.x < 0f
                        ? 1f
                        : -1f;

                ApplyHorizontalControl(
                    playerMovement,
                    playerRigidbody,
                    desiredHorizontalDirection
                );

                return;
            }

            desiredHorizontalDirection = 0f;

            ApplyHorizontalControl(
                playerMovement,
                playerRigidbody,
                0f
            );

            return;
        }

        UpdateTargetlessAirborneTracking(false);

        float remainingLandingTime =
            Mathf.Max(
                0f,
                currentTargetExpectedLandingAt -
                    Time.time
            );

        string refreshFailureReason;

        if (TryRefreshPersistentTargetCandidate(
                out refreshFailureReason))
        {
            currentTargetPredictedX =
                ApplyCurrentTargetLandingBias(
                    v5RoutePlanner.PredictPlatformX(
                        currentTargetCandidate,
                        remainingLandingTime
                    )
                );
        }
        else
        {
            if (!emergencyPlatformRescanPending)
            {
                nextPlatformScanTime = 0f;
                emergencyPlatformRescanPending = true;
                runEmergencyRescans++;

                RecordFailureTrace(
                    $"V5EmergencyRescan Id={currentTargetId}, " +
                    $"Generation={currentTargetGeneration}, " +
                    $"Reason={refreshFailureReason}"
                );
            }
        }

        float projectedPlayerX =
            playerPosition.x +
            playerRigidbody.velocity.x *
            HorizontalVelocityLookAheadSeconds;

        float leftBoundary =
            currentTargetPredictedX -
            currentTargetSafeHalfWidth;

        float rightBoundary =
            currentTargetPredictedX +
            currentTargetSafeHalfWidth;

        float enemyInterceptDirection;

        if (TryGetEnemyInterceptDirection(
                playerMovement,
                playerRigidbody,
                playerPosition,
                projectedPlayerX,
                leftBoundary,
                rightBoundary,
                remainingLandingTime,
                out enemyInterceptDirection))
        {
            desiredHorizontalDirection =
                enemyInterceptDirection;

            ApplyHorizontalControl(
                playerMovement,
                playerRigidbody,
                desiredHorizontalDirection
            );

            return;
        }

        float direction =
            CalculateIntervalSteeringDirection(
                projectedPlayerX,
                leftBoundary,
                rightBoundary,
                playerRigidbody.velocity.y,
                remainingLandingTime
            );

        desiredHorizontalDirection =
            direction;

        ApplyHorizontalControl(
            playerMovement,
            playerRigidbody,
            desiredHorizontalDirection
        );
    }

    private bool TryGetEnemyInterceptDirection(
        PlayerMovement playerMovement,
        Rigidbody2D playerRigidbody,
        Vector3 playerPosition,
        float projectedPlayerX,
        float landingLeft,
        float landingRight,
        float remainingLandingTime,
        out float direction)
    {
        direction = 0f;

        PlatformCandidate candidate = currentTargetCandidate;

        if (!ClimberLog.IsEnemyTargetingEnabled ||
            candidate == null ||
            !candidate.HasEnemy ||
            playerMovement.IsGrounded() ||
            remainingLandingTime <= 0.45f ||
            EnemyDiagnosticsBridge.WasHit(candidate.EnemyInstanceId))
        {
            return false;
        }

        float enemyX =
            candidate.CurrentPosition.x +
            candidate.EnemyOffsetX;

        float enemyY =
            candidate.CurrentPosition.y +
            candidate.EnemyOffsetY;

        float verticalOffset =
            enemyY - playerPosition.y;

        // Begin the sidestep only near the enemy. This avoids spending the
        // early jump committed to an optional waypoint.
        if (verticalOffset < -3.0f ||
            verticalOffset > 10.0f)
        {
            return false;
        }

        float returnDistance =
            enemyX < landingLeft
                ? landingLeft - enemyX
                : enemyX > landingRight
                    ? enemyX - landingRight
                    : 0f;

        float returnReserve =
            returnDistance / 10f + 0.30f;

        float interceptDistance =
            Mathf.Abs(enemyX - projectedPlayerX);

        if (interceptDistance / 10f + returnReserve >=
            remainingLandingTime)
        {
            return false;
        }

        float hitHalfWidth =
            Mathf.Clamp(
                candidate.EnemyWidth * 0.50f + 0.15f,
                0.22f,
                0.50f
            );

        direction = CalculateIntervalSteeringDirection(
            projectedPlayerX,
            enemyX - hitHalfWidth,
            enemyX + hitHalfWidth,
            playerRigidbody.velocity.y,
            remainingLandingTime
        );

        if (attemptedEnemyInterceptIds.Add(
                candidate.EnemyInstanceId))
        {
            LogVerbose(
                $"Enemy intercept started: EnemyId={candidate.EnemyInstanceId}, " +
                $"PlatformId={candidate.InstanceId}, " +
                $"EnemyX={enemyX:F2}, EnemyY={enemyY:F2}, " +
                $"Landing=[{landingLeft:F2},{landingRight:F2}], " +
                $"Remaining={remainingLandingTime:F2}."
            );
        }

        return true;
    }

    private void UpdateTargetlessAirborneTracking(
        bool active)
    {
        if (active ==
            targetlessAirborneActive)
        {
            return;
        }

        if (active)
        {
            targetlessAirborneActive = true;
            targetlessAirborneStartedAt = Time.time;
            return;
        }

        runTargetlessAirborneSeconds +=
            Mathf.Max(
                0f,
                Time.time -
                    targetlessAirborneStartedAt
            );

        targetlessAirborneActive = false;
        targetlessAirborneStartedAt = 0f;
    }

    private float CalculateIntervalSteeringDirection(
        float projectedPlayerX,
        float leftBoundary,
        float rightBoundary,
        float velocityY,
        float remainingLandingTime)
    {
        bool insideSafeInterval =
            projectedPlayerX >=
                leftBoundary &&
            projectedPlayerX <=
                rightBoundary;

        bool forcedInitialHold =
            targetInitialDirection != 0 &&
            Time.time <
                targetMinimumHoldUntil;

        if (forcedInitialHold &&
            !insideSafeInterval)
        {
            targetControlPhase =
                currentTargetIsRescue
                    ? 2
                    : 0;

            return targetInitialDirection;
        }

        bool descendingOrLate =
            velocityY <= 0f ||
            (remainingLandingTime > 0f &&
             remainingLandingTime < 0.45f);

        if (descendingOrLate)
        {
            targetControlPhase = 1;
        }

        float extraTolerance =
            targetControlPhase == 1
                ? 0.08f
                : 0f;

        if (projectedPlayerX <
            leftBoundary -
                extraTolerance)
        {
            return 1f;
        }

        if (projectedPlayerX >
            rightBoundary +
                extraTolerance)
        {
            return -1f;
        }

        return 0f;
    }

    private int ChooseEmergencyEscapeDirection(
        Vector3 playerPosition)
    {
        float leftScore = 0f;
        float rightScore = 0f;

        float maximumHeight =
            lastBounceY +
            (currentJumpMode ==
                JumpModeStrong
                    ? StrongJumpMaximumHeightGain
                    : NormalJumpReachRadius + 1f);

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (!IsBaseCandidateUsable(
                    candidate))
            {
                continue;
            }

            float candidateY =
                candidate.CurrentPosition.y;

            if (candidateY <=
                    lastBounceY +
                    NormalJumpMinimumHeightGain ||
                candidateY >
                    maximumHeight)
            {
                continue;
            }

            float landingX =
                GetCandidateLandingX(
                    candidate
                );

            float heightGain =
                candidateY -
                lastBounceY;

            float weight =
                1f +
                heightGain * 0.15f +
                GetPlatformTypeRank(
                    candidate.Type
                ) * 0.35f;

            if (landingX >
                playerPosition.x +
                    0.20f)
            {
                rightScore += weight;
            }
            else if (landingX <
                playerPosition.x -
                    0.20f)
            {
                leftScore += weight;
            }
        }

        if (rightScore >
            leftScore + 0.10f)
        {
            return 1;
        }

        if (leftScore >
            rightScore + 0.10f)
        {
            return -1;
        }

        // When route information is incomplete, move back toward the center
        // instead of remaining motionless.
        return playerPosition.x <= 0f
            ? 1
            : -1;
    }
}
