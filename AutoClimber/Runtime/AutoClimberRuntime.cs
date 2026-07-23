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

public sealed partial class AutoClimberRuntime : MonoBehaviour
{
    private const float PlatformScanIntervalSeconds =
        0.10f;

    private const float EnemyScanIntervalSeconds =
        0.20f;

    private const float CandidateLogIntervalSeconds =
        1.00f;

    private const float RuntimeLogIntervalSeconds =
        1.00f;

    private const float InputDebugIntervalSeconds =
        1.00f;

    private const float InputStateChangeLogDebounceSeconds =
        0.12f;

    private const byte VirtualKeyA = 0x41;
    private const byte VirtualKeyD = 0x44;
    private const uint KeyEventKeyUp = 0x0002;

    private const float HorizontalMoveSpeed = 10.00f;

    // The player moves at approximately 10 world units per second while
    // A or D is held. The game stops horizontal speed almost immediately
    // after releasing the key, so only a very small velocity look-ahead is
    // useful. Larger look-ahead values cause rapid Press/Stop oscillation.
    private const float HorizontalVelocityLookAheadSeconds = 0.015f;
    private const float MinimumMovementStateSeconds = 0.13f;

    // Platforms are landing intervals, not single points. These conservative
    // half-widths keep the player away from platform edges.
    private const float NormalLandingSafeHalfWidth = 0.72f;
    private const float StrongLandingSafeHalfWidth = 0.64f;
    private const float BreakableLandingSafeHalfWidth = 0.55f;
    private const float BoostFinalCenterHalfWidth = 0.30f;
    private const float IncidentalEnemyMaximumHorizontalDistance = 3.00f;
    private const float IncidentalEnemyMaximumReturnDistance = 2.20f;
    private const float IncidentalEnemyReturnReserveSeconds = 0.45f;

    // Moving platforms reverse at the horizontal play-area boundaries.
    // Raw linear prediction can produce impossible positions such as X=8.
    private const float HorizontalWorldLimit = 4.65f;
    private const float PreferredLandingWorldLimit = 3.50f;
    private const float EdgeLandingPenaltyPerUnit = 1200f;
    private const float EmergencyEdgeEscapeX = 3.80f;
    private const float TargetlessCenteringDeadZone = 0.50f;

    // Long 2000+ runs need a lower per-jump risk after the midpoint. Unsafe
    // strong platforms are filtered before the existing Strong > Normal >
    // Breakable ordering is applied.
    private const float HighAltitudeStartY = 1000f;
    private const float HighAltitudePreferredWorldLimit = 3.10f;
    private const float HighAltitudeAbsoluteWorldLimit = 4.10f;
    private const float HighAltitudeNormalReachRatio = 0.72f;
    private const float HighAltitudeStrongReachRatio = 0.64f;
    private const float HighAltitudeRecoveryPreferredReachRatio = 0.82f;
    private const float HighAltitudeRecoveryAbsoluteReachRatio = 1.05f;
    private const float HighAltitudeMinimumHeightGain = 0.10f;
    private const float HighAltitudeRescueMaximumUpwardTarget = 0.50f;
    private const float HighAltitudeEarlyRescueMaximumVelocityY = 12.00f;
    private const float HighAltitudeEarlyRescueDelaySeconds = 0.30f;
    private const int FailureTraceCapacity = 24;
    private const float AutoRetryPollSeconds = 0.25f;

    // A same-tier target upgrade must preserve route quality. Higher platform
    // types are still allowed immediately so Strong remains the hard priority.
    private const float UpgradeRouteScoreTolerance = 20.00f;
    private const float UpgradeMaximumAdditionalEdgeExposure = 0.25f;
    private const float UpgradeMaximumReachRatioIncrease = 0.15f;

    // Keep an initial steering command long enough to build displacement.
    // Recovery uses a longer commitment so it cannot return to Target=None
    // while repeatedly bouncing in the same place.
    private const float NormalInitialHoldSeconds = 0.16f;
    private const float RecoveryInitialHoldSeconds = 0.38f;
    private const float RecoveryForcedControlSeconds = 0.55f;

    private const int JumpModeInitial = 0;
    private const int JumpModeNormal = 1;
    private const int JumpModeStrong = 2;

    private const float NormalJumpReachRadius = 7.35f;
    private const float NormalJumpMinimumHeightGain = 0.50f;
    private const float NormalJumpMaximumReachRatio = 0.80f;
    // Do not target platforms at the mathematical apex. Collider-center
    // offsets, scan timing and small velocity variations make those targets
    // look reachable even though the player's feet never get high enough.
    private const float NormalJumpApexSafetyMargin = 0.65f;

    private const float StrongJumpMinimumHeightGain = 8.00f;
    private const float StrongJumpPreferredHeightGain = 15.00f;
    private const float StrongJumpMaximumHeightGain = 35.00f;
    private const float StrongJumpMaximumReachRatio = 0.70f;
    private const float StrongJumpMinimumReachMargin = 2.00f;
    private const float StrongTargetUpgradeWindowSeconds = 0.80f;
    private const float StrongTargetUpgradeHeight = 4.00f;

    private const float MissingTargetGraceSeconds = 0.35f;
    private const float FailedTargetCooldownSeconds = 3.00f;
    private const float RecycledTargetHeightTolerance = 1.50f;

    // A platform pool can briefly disable or replace the object backing a
    // route node while the logical platform is still the intended landing.
    // Allow one scan to rebind by spatial signature. If a physical fallback
    // exists, switch immediately instead of steering at a stale pooled object.
    private const float LogicalTargetObservationGraceSeconds = 0.15f;
    private const float LogicalTargetRebindHeightTolerance = 2.25f;
    private const float LogicalTargetRebindMaximumLandingDistance = 1.75f;
    private const float LogicalTargetMismatchedTypeHeightTolerance = 0.75f;
    private const float LogicalTargetMismatchedTypeLandingDistance = 0.90f;
    private const float NearLandingCommitmentSeconds = 0.18f;
    private const float NearLandingCommitmentVerticalTolerance = 1.35f;
    private const float LateReplanHighAltitudeMaximumReachRatio = 0.90f;
    private const float LateReplanLowAltitudeMaximumReachRatio = 0.94f;
    private const float LateReplanMinimumLandingMargin = 0.40f;
    private const float RetentionPreferredApexOvershoot = 7.25f;
    private const float RetentionMaximumSafeApexOvershoot = 8.50f;
    private const float LateReplanPreferredLandingTime = 1.25f;
    private const float LateReplanMaximumSafeLandingTime = 1.60f;
    private const float LifecycleHazardHeightTolerance = 2.25f;
    private const float LifecycleHazardLifetimeSeconds = 4.00f;
    private const float LifecycleFallbackPreferredNormalGain = 3.00f;
    private const float LifecycleFallbackPreferredStrongGain = 6.00f;
    private const float LifecycleFallbackPreferredGoldenGain = 8.00f;
    private const float RetentionProvisionalMinimumTimeToApex = 0.35f;

    // The visual scanner has already shown that fake platforms use the
    // literal sprite name "fake". They must never become route nodes.
    private const string FakePlatformSpriteName = "fake";

    // Landing and failure recognition.
    private const float ConfirmedLandingYTolerance = 1.15f;
    private const float MissedTargetVerticalMargin = 0.65f;
    private const float RejectedPlatformYTolerance = 0.35f;

    // Stuck detection. A route is considered stuck when several bounces occur
    // without a meaningful height increase.
    private const float MeaningfulProgressHeight = 2.25f;
    private const float StuckTimeoutSeconds = 5.00f;
    private const int StuckBounceLimit = 4;

    // Hypothetical two-step route planning.
    private const float GravityMagnitude = 49.05f;
    private const float NormalBounceVelocity = 25.00f;
    private const float StrongBounceVelocity = 60.00f;
    private const float GoldenBounceVelocity = 90.00f;
    private const float HypotheticalReachSafetyRatio = 0.74f;
    private const float RouteDeadEndPenalty = 180.00f;
    private const float RouteOptionBonus = 28.00f;
    private const float RouteBestGainBonus = 4.00f;

    // Rescue and finish handling.
    private const float RescueMaximumDrop = 28.00f;
    private const float RescueHorizontalSafetyRatio = 0.90f;
    private const float FinishLandingTolerance = 3.00f;
    private const float FinishDetectorVerticalTolerance = 6.00f;
    private const float FinishGroundedConfirmationSeconds = 0.20f;
    private const float FinishDistanceWorldScale = 2.00f;
    private const float FinishPlatformWorldOffset = 47.00f;
    private const float FinishDetectorSearchIntervalSeconds = 0.50f;

    // Recovery is intentionally less conservative than normal routing.
    // A difficult route is better than an infinite bounce loop.
    private const float RecoveryPreferredReachRatio = 0.92f;
    private const float RecoveryAbsoluteReachRatio = 1.18f;
    private const float RecoveryMinimumHorizontalSeparation = 0.85f;

    // The finish distance is map-specific. Never assume 800.
    private const float MinimumFinishApproachMargin = 25.00f;
    private const float MaximumFinishApproachMargin = 60.00f;

    private bool stateInitialized;
    private bool previousAscendingState;
    private bool automationEnabled;
    private KeyCode automationToggleKey = KeyCode.Y;
    private bool climbingConfirmed;
    private bool movementControlSessionActive;
    private bool autoRetryPending;
    private float autoRetryAtRealtime;

    private bool velocityInitialized;
    private float previousVelocityY;

    // Reaching finishAtDistance is only a score threshold. It is not proof
    // that the minigame has actually finished.
    private bool finishHeightDetected;
    private bool finishExitStarted;
    private float finishHeight;
    private float finishGroundedSince;
    private bool finishMapSpawned;
    private bool finishPlatformLocated;
    private float finishPlatformWorldY;
    private Transform finishLineDetectorTransform;
    private float nextFinishDetectorSearchTime;
    private bool finishDetectorSearchWarningLogged;

    private float desiredHorizontalDirection;

    private bool aKeyHeld;
    private bool dKeyHeld;
    private int appliedMovementDirection;
    private float nextAllowedMovementStateChangeTime;
    private bool focusPauseLogged;

    private float nextPlatformScanTime;
    private float nextCandidateLogTime;
    private float nextRuntimeLogTime;
    private float nextInputDebugTime;
    private float nextEnemyScanTime;
    private float nextTargetlessTraceTime;
    private float lastInputDebugTime;
    private int lastLoggedInputDirection;
    private bool lastLoggedInputGrounded;
    private bool inputDebugStateInitialized;

    private int currentTargetId;
    private int currentTargetGeneration;
    private PlatformType currentTargetType;
    private string currentTargetSpriteName = "";
    private float currentTargetPredictedX;
    private float currentTargetLandingOffsetX;
    private float currentTargetY;
    private float currentTargetLastSeenTime;
    private bool currentTargetIsRescue;
    private float currentTargetSafeHalfWidth;
    private float targetLockedTime;
    private float targetMinimumHoldUntil;
    private int targetInitialDirection;
    private int targetControlPhase;

    // V5 tracks a locked platform directly. The target therefore remains
    // valid while it is below the discovery scan during a strong or golden
    // jump instead of being replaced by a mid-air rescue target.
    private PlatformCandidate currentTargetCandidate;
    private Vector2 currentTargetColliderOffset;
    private float currentTargetObservedX;
    private float currentTargetObservedTime;
    private float currentTargetExpectedLandingAt;
    private float currentTargetRouteScore;
    private float currentTargetReachRatio;
    private bool currentTargetPersistedOffScanLogged;
    private bool currentTargetObservationLost;
    private float currentTargetObservationLostAt;
    private string currentTargetObservationLostReason = "";
    private bool nearLandingCommitmentLogged;
    private bool currentTargetIsLifecycleFallback;
    private bool retentionProvisionalLogged;
    private int currentJumpRetargetCount;
    private float nextV5RetargetTime;

    private float lastBounceY;
    private float lastBounceX;
    private int currentJumpMode;
    private float currentJumpStartTime;
    private float currentLaunchVelocityY;
    private bool currentJumpWasGolden;
    private float strongTargetUpgradeUntilTime;
    private int lastBlockedUpgradeCandidateId;

    private int failedTargetId;
    private int failedTargetGeneration;
    private float failedTargetUntilTime;

    private float currentTargetAnchorY;
    private PlatformType currentTargetAnchorType;

    // Permanent rejection is reserved for confirmed fake platforms.
    // Normal misses use only a short cooldown because moving platforms can
    // become safe again on a later bounce.
    private readonly HashSet<long> rejectedTargetKeys =
        new HashSet<long>();

    private readonly List<float> rejectedTargetYs =
        new List<float>();

    private readonly List<PlatformType> rejectedTargetTypes =
        new List<PlatformType>();

    private readonly List<float> lifecycleHazardYs =
        new List<float>();

    private readonly List<PlatformType> lifecycleHazardTypes =
        new List<PlatformType>();

    private readonly List<float> lifecycleHazardExpiresAt =
        new List<float>();

    private float lastMeaningfulProgressY;
    private float lastMeaningfulProgressTime;
    private int bouncesWithoutProgress;
    private bool recoverySelectionRequested;
    private int forcedRecoveryDirection;
    private float forcedRecoveryUntilTime;

    private int activePlayerInstanceId;
    private float highestObservedPlayerY;

    // Per-run diagnostics. These counters are deliberately independent from
    // route decisions so future enemy/item extensions can reuse the summary.
    private float runStartTime;
    private int runTargetSelections;
    private int runStrongTargets;
    private int runNormalTargets;
    private int runBreakableTargets;
    private int runEdgeTargets;
    private int runNormalBounces;
    private int runStrongBounces;
    private int runStrongTargetAttempts;
    private int runStrongTargetHits;
    private int runBlockedDowngrades;
    private int runBlockedUnsafeUpgrades;
    private int runLifecycleLosses;
    private int runLifecycleFallbackSelections;
    private int runLifecycleFallbackLandings;
    private int runGenerationResets;
    private int runEdgeLastResorts;
    private int runEmergencyRescans;
    private int runRetentionProvisionalSelections;
    private int runRetentionSafeTargets;
    private int runRetentionRiskyTargets;
    private float runMaximumLockedApexDrop;
    private float runTargetlessAirborneSeconds;
    private float targetlessAirborneStartedAt;
    private bool targetlessAirborneActive;
    private bool emergencyPlatformRescanPending;
    private bool runSummaryLogged;
    private int runEnemiesDetected;

    private int sessionSuccessCount;
    private int sessionFailureCount;
    private int sessionChallengeCount;
    private int sessionCompletedChallengeCount;
    private int sessionFirstTrySuccessCount;
    private int sessionRevivedSuccessCount;
    private bool currentRunIsRevive;
    private bool currentRunUsesQuickSkip;
    private bool nextRunIsRevive;

    private bool enemyMemberSearchCompleted;
    private bool enemyDiagnosticWarningLogged;
    private MemberInfo platformEnemyObjectMember;
    private MemberInfo platformEnemyColliderMember;

    private readonly HashSet<long> detectedEnemyIds =
        new HashSet<long>();

    private readonly HashSet<long> attemptedEnemyInterceptIds =
        new HashSet<long>();

    private long activeAirborneEnemyInterceptKey;

    private readonly HashSet<long> observedGenerationResetKeys =
        new HashSet<long>();

    private readonly HashSet<string> observedUnknownPlatformSprites =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly Queue<string> failureTrace =
        new Queue<string>();

    private FieldInfo currentSpeedField;
    private FieldInfo isMovingField;
    private FieldInfo forcedIdleField;

    private PropertyInfo currentSpeedProperty;
    private PropertyInfo isMovingProperty;
    private PropertyInfo forcedIdleProperty;

    private bool movementMembersInitialized;
    private bool movementMembersLogged;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(
        byte virtualKey,
        byte scanCode,
        uint flags,
        UIntPtr extraInfo
    );

    private readonly PlatformScanner planner =
        new PlatformScanner();

    private readonly ClimbRoutePlanner v5RoutePlanner =
        new ClimbRoutePlanner();

    private readonly AscendingHeightsSliderSkip sliderSkip =
        new AscendingHeightsSliderSkip();

    public void Start()
    {
        InitializeAutomationSettings();

        ClimberLog.User(
            $"AutoClimber {(automationEnabled ? "enabled" : "disabled")}: " +
            $"hotkey={automationToggleKey}; " +
            $"mode={AutoClimberQuestMode.ConfiguredMode}; " +
            $"skipStartSlider=" +
            $"{(AutoClimberPlugin.Config?.SkipStartSlider?.Value == true).ToString().ToLowerInvariant()}; " +
            $"targetEnemies=" +
            $"{ClimberLog.IsEnemyTargetingEnabled.ToString().ToLowerInvariant()}.");
        ClimberLog.Debug(
            "Route planner initialized: generationAwarePooling=true; " +
            "apexRetention=true; centerReturn=true.",
            "Runtime");

        previousAscendingState =
            automationEnabled &&
            GameState.IsAscendingHeights();

        stateInitialized = true;

        ResetRuntimeState();
    }

    private static void LogVerbose(
        string message)
    {
        ClimberLog.Debug(message);
    }

    public void Update()
    {
        // Slider handling is a separately configured launch helper. It must
        // keep observing the modal even when route control is toggled off.
        sliderSkip.Tick(Time.unscaledTime);

        if (Input.GetKeyDown(automationToggleKey))
        {
            SetAutomationEnabled(
                !automationEnabled
            );
        }

        if (!automationEnabled)
        {
            return;
        }

        bool currentAscendingState =
            GameState.IsAscendingHeights();

        UpdateQuickSkipFinishDistance(
            currentAscendingState
        );

        if (!stateInitialized)
        {
            previousAscendingState =
                currentAscendingState;

            stateInitialized = true;
            return;
        }

        // The retry prompt is the most reliable failure boundary. It can be
        // raised just before the Ascending Heights state changes, so settle
        // the run here while all diagnostics are still available.
        if (climbingConfirmed &&
            AscendingHeightsRetryBridge
                .TryConsumePromptShownSignal())
        {
            LogRunSummary(
                "RetryPromptShown"
            );
        }

        HandleAscendingStateChange(
            currentAscendingState
        );

        if (!currentAscendingState)
        {
            TryAutoRetry();
            return;
        }

        if (!climbingConfirmed)
        {
            TryConfirmGameplayStarted();

            if (!climbingConfirmed)
            {
                return;
            }
        }

        PlayerMovement playerMovement =
            PlayerMovement.instance;

        if (playerMovement == null)
        {
            return;
        }

        Rigidbody2D playerRigidbody;

        try
        {
            playerRigidbody =
                playerMovement
                    .GetComponent<Rigidbody2D>();
        }
        catch (Exception exception)
        {
            ClimberLog.Exception("Player Rigidbody lookup", exception);

            return;
        }

        if (playerRigidbody == null)
        {
            return;
        }

        Vector3 playerPosition =
            playerMovement.transform.position;

        Vector2 playerVelocity =
            playerRigidbody.velocity;

        if (DetectAndResetForNewRun(
                playerMovement,
                playerPosition,
                playerVelocity))
        {
            return;
        }

        LogMovementInputState(
            playerMovement,
            playerRigidbody
        );

        AscendingHeightsController controller =
            AscendingHeightsController.instance;

        if (controller != null &&
            controller.currentAscendingHeightsMap != null)
        {
            finishHeight =
                GetExpectedFinishPlatformWorldY(
                    controller.currentAscendingHeightsMap
                );
        }

        bool finishThresholdReached =
            controller != null &&
            controller.FinishHeightReached();

        if (finishThresholdReached &&
            !finishHeightDetected)
        {
            finishHeightDetected = true;

            LogVerbose(
                "Map score threshold reached. " +
                "This is informational only and does not control the finish exit."
            );
        }

        finishMapSpawned =
            IsFinishPlatformSpawned(controller);

        float detectedFinishPlatformY;

        if (finishMapSpawned &&
            TryGetFinishPlatformY(
                controller,
                out detectedFinishPlatformY))
        {
            finishPlatformLocated = true;
            finishPlatformWorldY =
                detectedFinishPlatformY;
        }

        bool nearFinishPlatform =
            finishPlatformLocated
                ? Mathf.Abs(
                      playerPosition.y -
                      finishPlatformWorldY
                  ) <= FinishDetectorVerticalTolerance
                : finishMapSpawned &&
                  !float.IsInfinity(finishHeight) &&
                  !float.IsNaN(finishHeight) &&
                  Mathf.Abs(
                      playerPosition.y -
                      finishHeight
                  ) <= FinishDetectorVerticalTolerance;

        // Quick-skip changes the map threshold after the game's finish UI and
        // detector coordinates have already been prepared. In that mode the
        // controller's spawned-finish flag is authoritative; reusing the
        // normal map-Y comparison can leave the player idle on the real exit
        // platform. Grounded confirmation still prevents an airborne turn to
        // the right while approaching the finish map.
        bool stableOnFinishGround =
            finishMapSpawned &&
            (nearFinishPlatform ||
             ClimberLog.IsQuickSkipModeEnabled) &&
            playerMovement.IsGrounded() &&
            Mathf.Abs(playerVelocity.y) <= 0.10f;

        if (stableOnFinishGround)
        {
            if (finishGroundedSince <= 0f)
            {
                finishGroundedSince = Time.time;
            }
        }
        else
        {
            finishGroundedSince = 0f;
        }

        bool finalLandingConfirmed =
            stableOnFinishGround &&
            Time.time - finishGroundedSince >=
                FinishGroundedConfirmationSeconds;

        if (finalLandingConfirmed &&
            !finishExitStarted)
        {
            finishExitStarted = true;
            ClearCurrentTarget();
            desiredHorizontalDirection = 1f;

            LogVerbose(
                "Spawned finish-platform landing confirmed. " +
                $"PlayerY={playerPosition.y:F2}, " +
                $"FinishY={(finishPlatformLocated ? finishPlatformWorldY : finishHeight):F2}. " +
                "Holding right until the flag ends the minigame."
            );
        }

        DetectBounceEveryFrame(
            playerPosition,
            playerVelocity
        );

        if (finishExitStarted)
        {
            HandleFinishedState(
                playerMovement,
                playerRigidbody
            );

            LogRuntimeWhenNeeded(
                playerPosition,
                playerRigidbody.velocity,
                true
            );

            return;
        }

        if (Time.time >=
            nextPlatformScanTime)
        {
            nextPlatformScanTime =
                Time.time +
                PlatformScanIntervalSeconds;

            planner.Scan(
                playerPosition,
                playerVelocity
            );

            ObservePlatformGenerationResets();

            bool recordEnemyDiagnostics =
                Time.time >= nextEnemyScanTime;

            if (ClimberLog.IsEnemyTargetingEnabled ||
                recordEnemyDiagnostics)
            {
                try
                {
                    // The reflection-backed scan is the reliable source for
                    // live enemy objects. Annotate candidates before route
                    // selection so enemy routing does not depend on the
                    // stale public platform.enemyObject reference.
                    ScanVisibleEnemies(false);
                }
                catch (Exception exception)
                {
                    ClimberLog.Exception("Enemy association scan", exception);
                }
            }

            UpdateCurrentTargetV5(
                playerPosition,
                playerVelocity
            );

            if (recordEnemyDiagnostics)
            {
                nextEnemyScanTime =
                    Time.time +
                    EnemyScanIntervalSeconds;

                try
                {
                    ScanAllActiveEnemies();
                    ScanUnknownPlatforms();
                }
                catch (Exception exception)
                {
                    ClimberLog.Exception("Enemy diagnostic scan", exception);
                }
            }
        }

        UpdateAutomaticHorizontalControl(
            playerMovement,
            playerRigidbody,
            playerPosition
        );

        if (ClimberLog.IsDebugMode &&
            Time.time >=
            nextCandidateLogTime)
        {
            nextCandidateLogTime =
                Time.time +
                CandidateLogIntervalSeconds;

            LogTopCandidates(
                playerPosition,
                playerVelocity
            );
        }

        LogRuntimeWhenNeeded(
            playerPosition,
            playerVelocity,
            finishHeightDetected
        );
    }

    private void InitializeAutomationSettings()
    {
        automationEnabled =
            AutoClimberPlugin.Config?
                .EnabledOnStartup?.Value != false;

        string configuredKey =
            AutoClimberPlugin.Config?
                .ToggleKey?.Value;

        if (!string.IsNullOrWhiteSpace(configuredKey) &&
            Enum.TryParse(
                configuredKey.Trim(),
                true,
                out KeyCode parsedKey) &&
            parsedKey != KeyCode.None)
        {
            automationToggleKey = parsedKey;
            return;
        }

        automationToggleKey = KeyCode.Y;

        ClimberLog.Warning(
            $"Invalid Toggle Key '{configuredKey}'. Using Y."
        );
    }

    private void SetAutomationEnabled(
        bool enabled)
    {
        if (automationEnabled == enabled)
        {
            return;
        }

        automationEnabled = enabled;
        autoRetryPending = false;
        nextRunIsRevive = false;
        AscendingHeightsRetryBridge.Reset();
        RestoreQuickSkipFinishDistance();
        ResetRuntimeState();

        previousAscendingState =
            enabled &&
            GameState.IsAscendingHeights();

        stateInitialized = true;

        string message = enabled
            ? $"AutoClimber enabled: hotkey={automationToggleKey}."
            : "AutoClimber disabled.";

        ClimberLog.User(message);
        AutoClimberPlugin.ModHelperInstance?
            .ShowNotification(message, enabled);
    }

    public void FixedUpdate()
    {
        if (!movementControlSessionActive)
        {
            return;
        }

        ApplyStoredHorizontalDirection();
    }

    public void LateUpdate()
    {
        if (!movementControlSessionActive)
        {
            return;
        }

        ApplyStoredHorizontalDirection();
    }

    private void ApplyStoredHorizontalDirection()
    {
        if (!climbingConfirmed ||
            !GameState.IsAscendingHeights())
        {
            StopMovementControlSession();
            return;
        }

        float direction = finishExitStarted
            ? 1f
            : desiredHorizontalDirection;

        SetHorizontalDirection(direction);
    }

    private void HandleAscendingStateChange(
        bool currentAscendingState)
    {
        if (currentAscendingState ==
            previousAscendingState)
        {
            return;
        }

        previousAscendingState =
            currentAscendingState;

        if (currentAscendingState)
        {
            autoRetryPending = false;
            AscendingHeightsRetryBridge.Reset();

            LogVerbose(
                "Ascending Heights state detected. " +
                "Waiting for gameplay confirmation."
            );

            ResetRuntimeState();
        }
        else
        {
            if (climbingConfirmed)
            {
                bool failedRun = !finishExitStarted;

                LogRunSummary(
                    "AscendingStateEnded"
                );

                if (failedRun)
                {
                    autoRetryPending = true;
                    autoRetryAtRealtime =
                        Time.realtimeSinceStartup +
                        AutoRetryPollSeconds;
                }

                LogVerbose(
                    "Ascending Heights ended."
                );

                RestoreQuickSkipFinishDistance();
                AutoClimberQuestMode.EndRunDecision();
            }
            else
            {
                LogVerbose(
                    "Ascending Heights temporary state ended " +
                    "before gameplay confirmation."
                );
            }

            ResetRuntimeState();
        }
    }

    private void TryAutoRetry()
    {
        if (!autoRetryPending ||
            Time.realtimeSinceStartup <
                autoRetryAtRealtime)
        {
            return;
        }

        SecondWindAscendingHeights prompt;

        if (!AscendingHeightsRetryBridge
                .TryTakeReadyPrompt(out prompt))
        {
            autoRetryAtRealtime =
                Time.realtimeSinceStartup +
                AutoRetryPollSeconds;
            return;
        }

        try
        {
            autoRetryPending = false;
            bool continueChallenge =
                AutoClimberPlugin.Config?
                    .EnableAutoRetry?.Value == true;

            if (continueChallenge)
            {
                // A quick-skip retry is never part of the V5 statistics.
                // If the setting was switched off while the prompt was open,
                // the following normal run starts as a fresh initial attempt.
                nextRunIsRevive =
                    !ClimberLog.IsQuickSkipModeEnabled;
                prompt._SecondWindSuggest_b__7_0();

                if (!ClimberLog.IsQuickSkipModeEnabled)
                {
                    ClimberLog.User(
                        "Auto retry: Continue Challenge confirmed."
                    );
                }
            }
            else
            {
                nextRunIsRevive = false;
                prompt.OnClose();

                if (!ClimberLog.IsQuickSkipModeEnabled)
                {
                    ClimberLog.User(
                        "Auto retry: No confirmed; challenge exited."
                    );
                }
            }
        }
        catch (Exception exception)
        {
            nextRunIsRevive = false;
            autoRetryPending = true;
            AscendingHeightsRetryBridge.MarkPromptShown(
                prompt
            );
            autoRetryAtRealtime =
                Time.realtimeSinceStartup +
                AutoRetryPollSeconds;

            ClimberLog.Exception("Auto retry", exception);
        }
    }

    private void TryConfirmGameplayStarted()
    {
        PlayerMovement playerMovement =
            PlayerMovement.instance;

        AscendingHeightsController controller =
            AscendingHeightsController.instance;

        if (playerMovement == null ||
            controller == null)
        {
            return;
        }

        Rigidbody2D rigidbody;

        try
        {
            rigidbody =
                playerMovement
                    .GetComponent<Rigidbody2D>();
        }
        catch
        {
            return;
        }

        if (rigidbody == null)
        {
            return;
        }

        Vector3 position =
            playerMovement.transform.position;

        Vector2 velocity =
            rigidbody.velocity;

        bool validPosition =
            position.y > 0f;

        bool playerMoving =
            Mathf.Abs(velocity.x) > 0.1f ||
            Mathf.Abs(velocity.y) > 0.1f;

        if (!validPosition ||
            !playerMoving)
        {
            return;
        }

        climbingConfirmed = true;
        movementControlSessionActive = true;

        currentRunIsRevive =
            nextRunIsRevive;

        currentRunUsesQuickSkip =
            ClimberLog.IsQuickSkipModeEnabled;

        nextRunIsRevive = false;

        if (!currentRunUsesQuickSkip &&
            !currentRunIsRevive)
        {
            sessionChallengeCount++;
        }

        runStartTime = Time.time;
        runSummaryLogged = false;
        runEnemiesDetected = 0;
        detectedEnemyIds.Clear();
        attemptedEnemyInterceptIds.Clear();
        activeAirborneEnemyInterceptKey = 0L;
        EnemyDiagnosticsBridge.BeginRun();

        velocityInitialized = true;
        previousVelocityY = velocity.y;

        currentLaunchVelocityY =
            Mathf.Max(
                NormalBounceVelocity,
                velocity.y
            );

        currentJumpWasGolden =
            currentLaunchVelocityY >= 75f;

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

        if (controller.currentAscendingHeightsMap != null)
        {
            finishHeight =
                GetExpectedFinishPlatformWorldY(
                    controller.currentAscendingHeightsMap
                );
        }

        activePlayerInstanceId =
            playerMovement.GetInstanceID();

        highestObservedPlayerY =
            position.y;

        lastBounceY = position.y;
        lastBounceX = position.x;

        lastMeaningfulProgressY =
            position.y;

        lastMeaningfulProgressTime =
            Time.time;

        bouncesWithoutProgress = 0;
        recoverySelectionRequested = false;

        currentJumpMode = JumpModeInitial;
        currentJumpStartTime = Time.time;
        strongTargetUpgradeUntilTime = 0f;

        nextPlatformScanTime = 0f;
        nextCandidateLogTime = 0f;
        nextRuntimeLogTime = 0f;
        nextInputDebugTime = 0f;
        nextEnemyScanTime = 0f;

        LogVerbose(
            "Ascending Heights gameplay confirmed. " +
            "Interval-based trajectory planning is active. " +
            $"SegmentType={(currentRunIsRevive ? "Revive" : "Initial")}."
        );

        LogVerbose(
            $"Current map finish distance: " +
            $"{controller.currentAscendingHeightsMap?.finishAtDistance:F2}, " +
            $"expected finish-platform world Y: {finishHeight:F2}"
        );
    }

    private void ScanVisibleEnemies(
        bool recordDiagnostics)
    {
        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (candidate == null ||
                candidate.GameObject == null)
            {
                continue;
            }

            AscendingHeightsPlatform platform = null;

            try
            {
                platform = candidate.GameObject
                    .GetComponent<AscendingHeightsPlatform>();

                if (platform == null)
                {
                    platform = candidate.GameObject
                        .GetComponentInParent<AscendingHeightsPlatform>();
                }
            }
            catch
            {
                continue;
            }

            if (platform == null)
            {
                continue;
            }

            InitializeEnemyMembers(platform);

            object enemyValue = ReadEnemyMember(
                platform,
                platformEnemyObjectMember
            );

            object colliderValue = ReadEnemyMember(
                platform,
                platformEnemyColliderMember
            );

            GameObject enemyObject =
                enemyValue as GameObject;

            Component enemyComponent =
                enemyValue as Component;

            Collider2D enemyCollider =
                colliderValue as Collider2D;

            if (enemyObject == null &&
                enemyComponent != null)
            {
                enemyObject = enemyComponent.gameObject;
            }

            if (enemyObject == null &&
                enemyCollider != null)
            {
                enemyObject = enemyCollider.gameObject;
            }

            if (enemyObject == null ||
                !enemyObject.activeInHierarchy ||
                (enemyCollider != null &&
                 !enemyCollider.enabled))
            {
                continue;
            }

            Vector3 enemyPosition =
                enemyObject.transform.position;

            Vector2 enemySize = Vector2.zero;

            if (enemyCollider != null)
            {
                Bounds enemyBounds = enemyCollider.bounds;
                enemyPosition = enemyBounds.center;
                enemySize = enemyBounds.size;
            }

            int enemyId = enemyComponent != null
                ? enemyComponent.GetInstanceID()
                : enemyObject.GetInstanceID();

            candidate.HasEnemy = true;
            candidate.EnemyInstanceId = enemyId;
            candidate.EnemyLogicalKey =
                EnemyDiagnosticsBridge.Observe(
                    enemyId,
                    candidate.InstanceId,
                    candidate.Generation,
                    enemyPosition
                );
            candidate.EnemyOffsetX =
                enemyPosition.x -
                candidate.CurrentPosition.x;
            candidate.EnemyOffsetY =
                enemyPosition.y -
                candidate.CurrentPosition.y;
            candidate.EnemyWidth = enemySize.x;

            if (!recordDiagnostics)
            {
                continue;
            }

            if (!detectedEnemyIds.Add(
                    candidate.EnemyLogicalKey))
            {
                continue;
            }

            runEnemiesDetected++;

            ClimberLog.Debug(
                $"Enemy detected: Id={enemyId}, " +
                $"Key={candidate.EnemyLogicalKey}, " +
                $"Name={enemyObject.name}, " +
                $"X={enemyPosition.x:F2}, " +
                $"Y={enemyPosition.y:F2}, " +
                $"Width={enemySize.x:F2}, " +
                $"Height={enemySize.y:F2}, " +
                $"PlatformId={candidate.InstanceId}, " +
                $"PlatformType={candidate.Type}, " +
                $"RunEnemiesDetected={runEnemiesDetected}"
            );
        }
    }

    private void ScanAllActiveEnemies()
    {
        foreach (EnemyGameObject enemy
                 in UnityEngine.Object
                     .FindObjectsOfType<EnemyGameObject>())
        {
            if (enemy == null ||
                enemy.gameObject == null ||
                !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 enemyPosition =
                enemy.transform.position;

            // Exclude unrelated active enemies retained by other gameplay
            // systems while Ascending Heights is running.
            if (Mathf.Abs(enemyPosition.x) > 10f ||
                enemyPosition.y < -10f ||
                (finishHeight > 0f &&
                 enemyPosition.y > finishHeight + 100f))
            {
                continue;
            }

            Collider2D enemyCollider = null;

            try
            {
                enemyCollider =
                    enemy.gameObject.GetComponent<Collider2D>();
            }
            catch
            {
                // The transform still provides a valid global observation.
            }

            if (enemyCollider != null &&
                !enemyCollider.enabled)
            {
                continue;
            }

            Vector2 enemySize = Vector2.zero;

            if (enemyCollider != null)
            {
                Bounds bounds = enemyCollider.bounds;
                enemyPosition = bounds.center;
                enemySize = bounds.size;
            }

            PlatformCandidate nearestPlatform = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (PlatformCandidate platform
                     in planner.Candidates)
            {
                if (platform == null)
                {
                    continue;
                }

                float distance =
                    Vector2.SqrMagnitude(
                        new Vector2(
                            enemyPosition.x,
                            enemyPosition.y
                        ) - platform.CurrentPosition
                    );

                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestPlatform = platform;
            }

            // The platform scanner is local while this scan is global. Do
            // not associate a distant global enemy with an arbitrary nearby
            // route platform; its binding will be filled once it enters the
            // local platform scan.
            if (nearestDistance > 9f)
            {
                nearestPlatform = null;
            }

            int enemyId = enemy.GetInstanceID();
            long logicalKey =
                EnemyDiagnosticsBridge.Observe(
                    enemyId,
                    nearestPlatform != null
                        ? nearestPlatform.InstanceId
                        : 0,
                    nearestPlatform != null
                        ? nearestPlatform.Generation
                        : 0,
                    enemyPosition
                );

            if (!detectedEnemyIds.Add(logicalKey))
            {
                continue;
            }

            runEnemiesDetected++;

            ClimberLog.Debug(
                $"Enemy detected: Id={enemyId}, " +
                $"Key={logicalKey}, Source=Global, " +
                $"Name={enemy.gameObject.name}, " +
                $"X={enemyPosition.x:F2}, Y={enemyPosition.y:F2}, " +
                $"Width={enemySize.x:F2}, Height={enemySize.y:F2}, " +
                $"PlatformId={(nearestPlatform != null ? nearestPlatform.InstanceId : 0)}, " +
                $"PlatformType={(nearestPlatform != null ? nearestPlatform.Type.ToString() : "None")}, " +
                $"RunEnemiesDetected={runEnemiesDetected}"
            );
        }
    }

    private void ScanUnknownPlatforms()
    {
        foreach (PlatformCandidate candidate
                 in planner.Candidates)
        {
            if (candidate == null ||
                candidate.Type != PlatformType.Unknown)
            {
                continue;
            }

            string spriteName =
                string.IsNullOrEmpty(candidate.SpriteName)
                    ? "unknown"
                    : candidate.SpriteName;

            if (!observedUnknownPlatformSprites.Add(
                    spriteName))
            {
                continue;
            }

            string objectName =
                candidate.GameObject != null
                    ? candidate.GameObject.name
                    : "unknown";

            ClimberLog.Debug(
                $"Unknown platform detected: " +
                $"Sprite={spriteName}, " +
                $"Object={objectName}, " +
                $"X={candidate.CurrentPosition.x:F2}, " +
                $"Y={candidate.CurrentPosition.y:F2}, " +
                $"Width={candidate.ColliderSize.x:F2}, " +
                $"Height={candidate.ColliderSize.y:F2}"
            );
        }
    }

    private void InitializeEnemyMembers(
        AscendingHeightsPlatform platform)
    {
        if (enemyMemberSearchCompleted ||
            platform == null)
        {
            return;
        }

        enemyMemberSearchCompleted = true;

        Type platformType = platform.GetType();

        platformEnemyObjectMember = FindEnemyMember(
            platformType,
            "enemyObject"
        );

        platformEnemyColliderMember = FindEnemyMember(
            platformType,
            "enemyBoxCollider"
        );

        if (platformEnemyObjectMember == null &&
            platformEnemyColliderMember == null)
        {
            LogEnemyDiagnosticWarningOnce(
                "Enemy diagnostics could not locate enemyObject or " +
                "enemyBoxCollider on AscendingHeightsPlatform."
            );
        }
    }

    [HideFromIl2Cpp]
    private MemberInfo FindEnemyMember(
        Type ownerType,
        string memberName)
    {
        if (ownerType == null)
        {
            return null;
        }

        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.IgnoreCase;

        PropertyInfo property = ownerType.GetProperty(
            memberName,
            flags
        );

        if (property != null &&
            property.CanRead)
        {
            return property;
        }

        return ownerType.GetField(
            memberName,
            flags
        );
    }

    [HideFromIl2Cpp]
    private object ReadEnemyMember(
        object owner,
        MemberInfo member)
    {
        if (owner == null ||
            member == null)
        {
            return null;
        }

        try
        {
            if (member is PropertyInfo property &&
                property.CanRead)
            {
                return property.GetValue(owner);
            }

            if (member is FieldInfo field)
            {
                return field.GetValue(owner);
            }
        }
        catch (Exception exception)
        {
            ClimberLog.Exception(
                $"Enemy diagnostic member read ({member.Name})",
                exception);
        }

        return null;
    }

    private void LogEnemyDiagnosticWarningOnce(
        string message)
    {
        if (enemyDiagnosticWarningLogged)
        {
            return;
        }

        enemyDiagnosticWarningLogged = true;
        ClimberLog.Warning(message);
    }

}
