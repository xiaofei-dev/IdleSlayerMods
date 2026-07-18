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
    private bool DetectAndResetForNewRun(
        PlayerMovement playerMovement,
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        int playerInstanceId =
            playerMovement.GetInstanceID();

        if (activePlayerInstanceId != 0 &&
            playerInstanceId !=
                activePlayerInstanceId)
        {
            LogVerbose(
                "New PlayerMovement instance detected. " +
                "Resetting AutoClimber session state."
            );

            LogRunSummary(
                "PlayerInstanceChanged"
            );

            ResetRuntimeState();
            return true;
        }

        activePlayerInstanceId =
            playerInstanceId;

        highestObservedPlayerY =
            Mathf.Max(
                highestObservedPlayerY,
                playerPosition.y
            );

        bool restartedAtBottom =
            climbingConfirmed &&
            highestObservedPlayerY > 50f &&
            playerPosition.y < 20f &&
            playerVelocity.y > 20f;

        if (!restartedAtBottom)
        {
            return false;
        }

        LogVerbose(
            "A new Ascending Heights run was detected " +
            "without a clean state transition. Resetting now."
        );

        LogRunSummary(
            "RestartDetected"
        );

        ResetRuntimeState();
        return true;
    }

    private void LogRunSummary(
        string endReason)
    {
        if (!climbingConfirmed ||
            runSummaryLogged ||
            currentRunUsesQuickSkip)
        {
            return;
        }

        bool runSucceeded =
            finishExitStarted &&
            endReason ==
                "AscendingStateEnded";

        bool countableResult =
            endReason == "AscendingStateEnded" ||
            endReason == "RetryPromptShown" ||
            endReason == "RestartDetected" ||
            endReason == "PlayerInstanceChanged";

        if (!countableResult)
        {
            return;
        }

        UpdateTargetlessAirborneTracking(false);

        // Mark the result only after confirming this is a real run boundary.
        // OnDisable/OnDestroy may happen before the failure screen and must
        // never suppress the later countable result.
        runSummaryLogged = true;

        if (runSucceeded)
        {
            sessionSuccessCount++;
            sessionCompletedChallengeCount++;

            if (currentRunIsRevive)
            {
                sessionRevivedSuccessCount++;
            }
            else
            {
                sessionFirstTrySuccessCount++;
            }
        }
        else
        {
            sessionFailureCount++;
        }

        int sessionRunCount =
            sessionSuccessCount +
            sessionFailureCount;

        float sessionPassRate =
            sessionRunCount > 0
                ? (float)sessionSuccessCount /
                  sessionRunCount * 100f
                : 0f;

        float firstTryPassRate =
            sessionChallengeCount > 0
                ? (float)sessionFirstTrySuccessCount /
                  sessionChallengeCount * 100f
                : 0f;

        float challengeCompletionRate =
            sessionChallengeCount > 0
                ? (float)sessionCompletedChallengeCount /
                  sessionChallengeCount * 100f
                : 0f;

        ClimberLog.User(
            $"Run count: Total={sessionRunCount}, " +
            $"Success={sessionSuccessCount}, " +
            $"Failure={sessionFailureCount}, " +
            $"PassRate={sessionPassRate:F1}%, " +
            $"Challenges={sessionChallengeCount}, " +
            $"FirstTrySuccess={sessionFirstTrySuccessCount}, " +
            $"RevivedSuccess={sessionRevivedSuccessCount}, " +
            $"Completed={sessionCompletedChallengeCount}, " +
            $"FirstTryRate={firstTryPassRate:F1}%, " +
            $"CompletionRate={challengeCompletionRate:F1}%, " +
            $"SegmentType={(currentRunIsRevive ? "Revive" : "Initial")}, " +
            $"LastResult={(runSucceeded ? "Success" : "Failure")}, " +
            $"EndReason={endReason}, " +
            $"EnemiesDetected={runEnemiesDetected}, " +
            $"EnemyHits=" +
            $"{EnemyDiagnosticsBridge.RunConfirmedDeaths}"
        );

        if (!runSucceeded)
        {
            LogFailureTrace(endReason);
        }

        float duration = runStartTime > 0f
            ? Mathf.Max(0f, Time.time - runStartTime)
            : 0f;

        float strongHitRate = runStrongTargetAttempts > 0
            ? (float)runStrongTargetHits /
              runStrongTargetAttempts * 100f
            : 0f;

        LogVerbose(
            $"Run diagnostics: EndReason={endReason}, " +
            $"Duration={duration:F2}, " +
            $"MaxY={highestObservedPlayerY:F2}, " +
            $"Targets={runTargetSelections}, " +
            $"StrongTargets={runStrongTargets}, " +
            $"NormalTargets={runNormalTargets}, " +
            $"BreakableTargets={runBreakableTargets}, " +
            $"EdgeTargets={runEdgeTargets}, " +
            $"StrongBounces={runStrongBounces}, " +
            $"NormalBounces={runNormalBounces}, " +
            $"StrongTargetHits={runStrongTargetHits}/" +
            $"{runStrongTargetAttempts}, " +
            $"StrongHitRate={strongHitRate:F1}, " +
            $"BlockedDowngrades={runBlockedDowngrades}, " +
            $"BlockedUnsafeUpgrades={runBlockedUnsafeUpgrades}, " +
            $"LifecycleLosses={runLifecycleLosses}, " +
            $"LifecycleFallbacks={runLifecycleFallbackSelections}, " +
            $"LifecycleFallbackHits={runLifecycleFallbackLandings}/" +
            $"{runLifecycleFallbackSelections}, " +
            $"GenerationResets={runGenerationResets}, " +
            $"EdgeLastResorts={runEdgeLastResorts}, " +
            $"EmergencyRescans={runEmergencyRescans}, " +
            $"RetentionProvisionals={runRetentionProvisionalSelections}, " +
            $"RetentionTargets={runRetentionSafeTargets}/" +
            $"{runRetentionSafeTargets + runRetentionRiskyTargets}, " +
            $"RiskyRetentionTargets={runRetentionRiskyTargets}, " +
            $"MaxLockedApexDrop={runMaximumLockedApexDrop:F2}, " +
            $"TargetlessAirTime={runTargetlessAirborneSeconds:F2}"
        );
    }

    private void RecordFailureTrace(
        string message)
    {
        if (currentRunUsesQuickSkip)
        {
            return;
        }

        float elapsed = runStartTime > 0f
            ? Mathf.Max(0f, Time.time - runStartTime)
            : 0f;

        failureTrace.Enqueue(
            $"T+{elapsed:F2} {message}"
        );

        while (failureTrace.Count >
               FailureTraceCapacity)
        {
            failureTrace.Dequeue();
        }
    }

    private void RecordTargetlessSnapshot(
        Vector3 playerPosition,
        Vector2 playerVelocity)
    {
        if (Time.time < nextTargetlessTraceTime)
        {
            return;
        }

        nextTargetlessTraceTime = Time.time + 0.75f;

        int usableCount = 0;
        PlatformCandidate nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (!IsBaseCandidateUsable(candidate))
            {
                continue;
            }

            usableCount++;

            float distance =
                Mathf.Abs(
                    candidate.CurrentPosition.y -
                    playerPosition.y
                ) +
                Mathf.Abs(
                    GetCandidateLandingX(candidate) -
                    playerPosition.x
                ) * 0.25f;

            if (nearest == null ||
                distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        V5RouteDecision nearestDecision =
            nearest != null
                ? v5RoutePlanner
                    .EvaluatePersistentTarget(
                        nearest,
                        planner.Candidates,
                        playerPosition,
                        playerVelocity,
                        lastBounceY,
                        IsHighAltitudeMode(),
                        true,
                        finishHeight
                    )
                : null;

        string nearestText = nearest != null
            ? $"NearestId={nearest.InstanceId}, " +
              $"Type={nearest.Type}, " +
              $"Sprite={nearest.SpriteName}, " +
              $"X={nearest.CurrentPosition.x:F2}, " +
              $"Y={nearest.CurrentPosition.y:F2}, " +
              $"DeltaY={nearest.CurrentPosition.y - playerPosition.y:F2}, " +
              $"V5Feasible={nearestDecision.Feasible}, " +
              $"V5ReachRatio=" +
              $"{(nearestDecision.Feasible ? nearestDecision.ReachRatio : float.MaxValue):F2}, " +
              $"V5Reason={nearestDecision.RejectionReason}"
            : "Nearest=None";

        RecordFailureTrace(
            $"Targetless X={playerPosition.x:F2}, " +
            $"Y={playerPosition.y:F2}, " +
            $"VelocityY={playerVelocity.y:F2}, " +
            $"Candidates={planner.Candidates.Count}, " +
            $"Usable={usableCount}, {nearestText}"
        );
    }

    private void LogFailureTrace(
        string endReason)
    {
        if (currentRunUsesQuickSkip)
        {
            return;
        }

        ClimberLog.User(
            $"Failure trace: EndReason={endReason}, " +
            $"MaxY={highestObservedPlayerY:F2}, " +
            $"Entries={failureTrace.Count}"
        );

        if (!ClimberLog.IsDebugMode)
        {
            return;
        }

        foreach (string entry in failureTrace)
        {
            ClimberLog.Debug(
                "Failure trace: " + entry
            );
        }
    }

    private void ResetRuntimeState()
    {
        StopMovementControlSession();

        climbingConfirmed = false;
        currentRunIsRevive = false;
        currentRunUsesQuickSkip = false;

        velocityInitialized = false;
        previousVelocityY = 0f;

        finishHeightDetected = false;
        finishExitStarted = false;
        finishHeight = float.PositiveInfinity;
        finishGroundedSince = 0f;
        finishMapSpawned = false;
        finishPlatformLocated = false;
        finishPlatformWorldY = float.PositiveInfinity;
        finishLineDetectorTransform = null;
        nextFinishDetectorSearchTime = 0f;
        finishDetectorSearchWarningLogged = false;

        lastBounceY = 0f;
        lastBounceX = 0f;

        currentJumpMode =
            JumpModeInitial;

        currentJumpStartTime = 0f;
        currentLaunchVelocityY =
            NormalBounceVelocity;
        currentJumpWasGolden = false;
        currentJumpRetargetCount = 0;
        nextV5RetargetTime = 0f;
        retentionProvisionalLogged = false;
        strongTargetUpgradeUntilTime = 0f;
        lastBlockedUpgradeCandidateId = 0;

        failedTargetId = 0;
        failedTargetGeneration = 0;
        failedTargetUntilTime = 0f;

        rejectedTargetKeys.Clear();
        rejectedTargetYs.Clear();
        rejectedTargetTypes.Clear();
        ClearLifecycleHazards();

        lastMeaningfulProgressY = 0f;
        lastMeaningfulProgressTime = 0f;
        bouncesWithoutProgress = 0;
        recoverySelectionRequested = false;
        forcedRecoveryDirection = 0;
        forcedRecoveryUntilTime = 0f;

        currentTargetAnchorY = 0f;
        currentTargetAnchorType =
            PlatformType.Unknown;

        nextAllowedMovementStateChangeTime = 0f;
        focusPauseLogged = false;

        activePlayerInstanceId = 0;
        highestObservedPlayerY = 0f;

        runStartTime = 0f;
        runTargetSelections = 0;
        runStrongTargets = 0;
        runNormalTargets = 0;
        runBreakableTargets = 0;
        runEdgeTargets = 0;
        runNormalBounces = 0;
        runStrongBounces = 0;
        runStrongTargetAttempts = 0;
        runStrongTargetHits = 0;
        runBlockedDowngrades = 0;
        runBlockedUnsafeUpgrades = 0;
        runLifecycleLosses = 0;
        runLifecycleFallbackSelections = 0;
        runLifecycleFallbackLandings = 0;
        runGenerationResets = 0;
        runEdgeLastResorts = 0;
        runEmergencyRescans = 0;
        runRetentionProvisionalSelections = 0;
        runRetentionSafeTargets = 0;
        runRetentionRiskyTargets = 0;
        runMaximumLockedApexDrop = 0f;
        runTargetlessAirborneSeconds = 0f;
        targetlessAirborneStartedAt = 0f;
        targetlessAirborneActive = false;
        emergencyPlatformRescanPending = false;
        runSummaryLogged = false;
        runEnemiesDetected = 0;

        enemyMemberSearchCompleted = false;
        platformEnemyObjectMember = null;
        platformEnemyColliderMember = null;
        detectedEnemyIds.Clear();
        attemptedEnemyInterceptIds.Clear();
        observedGenerationResetKeys.Clear();
        failureTrace.Clear();

        nextPlatformScanTime = 0f;
        nextCandidateLogTime = 0f;
        nextRuntimeLogTime = 0f;
        nextInputDebugTime = 0f;
        nextEnemyScanTime = 0f;
        nextTargetlessTraceTime = 0f;
        lastInputDebugTime = 0f;
        lastLoggedInputDirection = 0;
        lastLoggedInputGrounded = false;
        inputDebugStateInitialized = false;

        ClearCurrentTarget();

        planner.Reset();
    }

    public void OnApplicationFocus(
        bool hasFocus)
    {
        if (!movementControlSessionActive ||
            !climbingConfirmed ||
            !GameState.IsAscendingHeights())
        {
            return;
        }

        if (!hasFocus)
        {
            ReleaseAllMovementKeys();
            return;
        }

        focusPauseLogged = false;
        nextPlatformScanTime = 0f;

        LogVerbose(
            "Game focus restored. AutoClimber route control resumed."
        );
    }

    public void OnDisable()
    {
        LogRunSummary(
            "BehaviourDisabled"
        );

        RestoreQuickSkipFinishDistance();
        StopMovementControlSession();
    }

    public void OnDestroy()
    {
        LogRunSummary(
            "BehaviourDestroyed"
        );

        RestoreQuickSkipFinishDistance();
        StopMovementControlSession();
    }

}
