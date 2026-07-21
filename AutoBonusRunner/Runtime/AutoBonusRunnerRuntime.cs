using AutoBonusRunner.Control;
using AutoBonusRunner.Detection;
using AutoBonusRunner.Diagnostics;
using AutoBonusRunner.Physics;
using AutoBonusRunner.Routing;
using Il2Cpp;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace AutoBonusRunner.Runtime;

internal enum WallActionPhase
{
    None,
    EnteringTrench,
    ApproachJumpInFlight,
    AwaitingWallContact,
    WallJumpPhaseOne,
    AwaitingNextWallPress,
    WallJumpPhaseTwo,
    AttachedObjectiveDescent,
    ExitFlight,
    Completed,
    Failed
}

public sealed class AutoBonusRunnerRuntime : MonoBehaviour
{
    private const float DiagnosticIntervalSeconds = 0.35f;
    private const float WallStallConfirmationSeconds = 0.01f;
    private const float FallbackWallRecoveryHoldSeconds = 0.115f;
    // Successful manual wall presses in the captured runs were roughly
    // 0.118-0.160s. A forced 0.180s press climbed far above the lip and
    // landed 6+ units beyond the planned tower, so wall recovery has its own
    // demonstrated range rather than blindly using the normal-jump cap.
    private const float MinimumWallRecoveryHoldSeconds = 0.115f;
    private const float MaximumWallRecoveryHoldSeconds = 0.165f;
    // Landing-targeted wall presses need the full native range. A short press
    // can clear a low lip and settle on its top, while a longer press can
    // transfer directly to the following platform. The narrower recovery
    // range above remains the fallback for a genuinely multi-bounce climb.
    private const float MinimumWallLandingHoldSeconds = 0.020f;
    private const float MaximumWallLandingHoldSeconds = 0.180f;
    // A two-unit wall top cannot retain a normal released wall jump: as soon
    // as the body clears the lip, horizontal speed resumes and carries it off
    // the far edge.  When neither the wall top nor the downstream support is
    // reachable by the first press, use the shortest hold supported by the
    // captured successful wall presses and solve the real exit from the next
    // contact. A 0.060s release proved too short to preserve attachment;
    // 0.115s is the lower bound currently supported by manual evidence.
    private const float MinimumAttachedWallPulseHoldSeconds = 0.075f;
    private const float MaximumAttachedWallPulseHoldSeconds = 0.135f;
    private const float StagedWallTopMaximumWidth = 4.25f;
    // A released wall pulse is still a live vertical impulse.  The V0.27
    // trace repeatedly re-pressed at VY=+17.253 while only 0.31-0.57 units
    // below the release height, resetting VY to 20 and skipping the next
    // trench.  Manual multi-pulse evidence instead waits through the apex and
    // sends the next DOWN only while descending.  Keep this wait bounded so a
    // frozen/stale velocity sample cannot own control forever.
    private const float WallResidualRiseVelocityThreshold = 0.25f;
    private const float WallResidualRiseSafetyMargin = 0.08f;
    private const float WallResidualRiseMaximumWaitSeconds = 0.45f;
    private const float MandatoryWallFaceSetupSafetyMargin = 0.30f;
    private const float MandatoryWallFaceSetupMaximumWaitSeconds = 1.20f;
    private const float MandatoryWallFaceMinimumContactOffset = 2.50f;
    private const float MandatoryWallFaceMaximumContactTopClearance = 0.55f;
    private const float MandatoryWallFacePreferredContactOffset = 3.30f;
    private const float MandatoryWallFaceHorizontalResumeVelocity = 0.35f;
    private const float Ground3ObjectiveFaceHeight = 5.0f;
    private const float MandatoryWallFacePhysicalBottomClearance = 0.10f;
    private const float MandatoryWallFacePhysicalTopClearance = 0.15f;
    private const float MandatoryWallFaceRayXMatchTolerance = 0.35f;
    private const float MandatoryWallFaceBodyXMatchTolerance = 0.16f;
    private const float MandatoryWallFaceContactWatchSeconds = 1.25f;
    // Separation is already enforced by synthetic UP plus a real FixedUpdate
    // barrier. A second 0.20s timer kept the runner attached long enough to
    // miss the legal second press and slide off narrow walls. Retain a tiny
    // post-classification gap without discarding the observed contact window.
    private const float WallRecoveryCooldownSeconds = 0.015f;
    // A demonstrated section-two pillar climb used three distinct light
    // presses (0.132s, 0.132s, 0.090s). The game accepts a pulse train whose
    // count and hold depend on remaining height; cap it generously enough for
    // authored tall walls while still preventing unbounded death-wall spam.
    private const int MaximumWallRecoveriesPerAirborneSequence = 6;
    private const float WallAttachmentVelocityTolerance = 3.0f;
    private const float WallAttachmentPositionTolerance = 0.55f;
    private const float PitDescentYThreshold = -3.50f;
    private const float PitDescentVelocityThreshold = -8.00f;
    private const int PitDescentConfirmationFixedSteps = 2;
    private const float RecoverableWallProbeDistance = 1.20f;
    private readonly BonusStageDetector detector = new();
    private readonly JumpController jumpController = new();
    private readonly CompletionRewardController completionRewardController =
        new();
    private readonly BonusPlatformScanner platformScanner = new();
    private readonly BonusHazardScanner hazardScanner = new();
    private readonly BonusWallDetector wallDetector = new();
    private readonly BonusJumpPlanner jumpPlanner = new();
    private readonly JumpPhysicsFeedback jumpPhysicsFeedback = new();
    private bool automationEnabled;
    private KeyCode toggleKey = KeyCode.U;
    private bool previousBonusState;
    private int previousSectionIndex = -1;
    private int previousPlayerInstanceId;
    private float nextDiagnosticTime;
    private float nextErrorLogTime;
    private float nextMapRegistryRefreshTime;
    private int lastMapRegistryGeneration = -1;
    private string lastMapRegistryStatus = string.Empty;
    private bool movementInitialized;
    private Vector3 previousMovementPosition;
    private float previousMovementObservedAt;
    private bool bonusGameplayStarted;
    private bool previousGrounded;
    private float previousVelocityY;
    private bool previousObservedJumpInput;
    private float observedJumpInputStartedAt;
    private bool automaticJumpArmed = true;
    private bool airborneAfterAutomaticJump;
    private float automaticJumpRequestedAt;
    private bool automaticJumpVelocityConfirmed;
    private float nextAutomaticAttemptTime;
    private bool pitDescentGuardActive;
    private long pitRespawnLastFixedStep = -1;
    private int pitRespawnStableFixedSteps;
    private long pitDescentCandidateLastFixedStep = -1;
    private int pitDescentCandidateFixedSteps;
    private float nextPitDescentEvidenceTime;
    private float wallStallStartedAt = -1f;
    private float nextWallRecoveryTime;
    private float nextWallProbeLogTime;
    private int wallRecoveryAttempts;
    private bool wallRecoveryContactLatched;
    private bool wallRecoverySawUpwardMotion;
    private float wallRecoveryContactX;
    private float wallRecoveryContactY;
    private float wallRecoveryCommitmentUntil;
    private float wallRecoveryImpulseStartY;
    private float wallRecoveryImpulseStartVelocityY;
    private float wallRecoveryLastObservedY;
    private float nextWallClimbFrameLogTime;
    private int wallRecoveryUpwardPhysicsSteps;
    private bool wallRecoveryImpulseConfirmed;
    private bool wallRecoveryImpulseFailureLogged;
    private bool wallRecoveryPrematureReleaseLogged;
    private bool wallRecoveryLipCrossed;
    private float wallRecoveryRequiredReleaseY;
    private bool wallExitTargetActive;
    private BonusBoardSegment wallExitTarget;
    private float wallExitPredictedLandingX;
    private float wallExitPredictedTravel;
    private float wallExitPredictedFlightSeconds;
    private string wallExitPlanSummary = string.Empty;
    private bool wallExitTransferCommitted;
    private bool wallLandingFlightCommitted;
    private bool wallTopLandingSequenceCommitted;
    private bool wallExitContactWatchActive;
    private bool wallResidualRiseWaitActive;
    private float wallResidualRiseWaitStartedAt;
    private float wallResidualRiseWaitStartFeetY;
    private float wallResidualRiseWaitTargetLeft;
    private float nextWallResidualRiseLogTime;
    private bool wallExitFaceContactRequired;
    private int wallExitObjectiveCountAtCapture;
    private float wallExitObjectiveMinimumY = float.NaN;
    private float wallExitObjectiveMaximumY = float.NaN;
    private bool wallMandatoryFaceSetupActive;
    private float wallMandatoryFaceSetupDeadline;
    private long wallMandatoryFaceSetupDeadlineFixedStep = -1;
    private bool wallMandatoryFaceInterceptCommitted;
    private float wallMandatoryFaceInterceptStartedAt;
    private long wallMandatoryFaceInterceptStartedFixedStep = -1;
    private long wallMandatoryFaceContactWatchDeadlineFixedStep = -1;
    private float wallMandatoryFaceTargetContactX = float.NaN;
    private float wallMandatoryFacePredictedTopClearFeetY = float.NaN;
    private float wallMandatoryFacePredictedContactFeetY = float.NaN;
    private float wallMandatoryFacePredictedContactVelocityY = float.NaN;
    private float wallMandatoryFacePredictedContactSeconds;
    private float wallMandatoryFaceReleasePredictedContactFeetY = float.NaN;
    private float wallMandatoryFaceReleasePredictedContactVelocityY = float.NaN;
    private float wallMandatoryFaceReleaseFeetY = float.NaN;
    private float wallMandatoryFaceReleaseVelocityY = float.NaN;
    private long wallMandatoryFaceContactObservedFixedStep = -1;
    private float nextMandatoryFaceWindowLogTime;
    private long wallReleaseObservedFixedStep = -1;
    private long wallDetachedLastFixedStep = -1;
    private int wallDetachedConfirmationSteps;
    private bool passiveWallApproachActive;
    private WallActionPhase wallActionPhase;
    private float lastManualInputTime = -10f;
    private float lastReliableHorizontalSpeed = 9.5f;
    private float sectionCruiseHorizontalSpeed;
    private float sectionCruiseCandidateSpeed;
    private long sectionCruiseCandidateLastFixedStep = -1;
    private int sectionCruiseCandidateFixedSteps;
    private bool wallRouteSpeedLatched;
    private float wallRouteHorizontalSpeed;
    private float nextSpeedObservationLogTime;
    private string lastSpeedObservationDecision = string.Empty;
    private long speedPlanningResumeFixedStep = -1;
    private long completionDashPlanningResumeFixedStep = -1;
    private float nextSpeedPlanningBarrierLogTime;
    private float nextCompletionDashDeferralLogTime;
    private bool attachedObjectiveDescentActive;
    private float attachedObjectiveDescentDeadline;
    private long attachedObjectiveDescentDeadlineFixedStep = -1;
    private float attachedObjectiveDescentTargetFeetY;
    private int attachedObjectiveDescentSphereCount;
    private float nextAttachedObjectiveDescentLogTime;
    private float nextRouteLogTime;
    private float noSupportStallStartedAt = -1f;
    private float nextNoSupportStallLogTime;
    private float nextHazardLogTime;
    private string lastHazardSignature = string.Empty;
    private string lastRouteSignature = string.Empty;
    private string lastBoostRouteSelection = string.Empty;
    private bool routePlanLocked;
    private int routeLockSection;
    private float routeLockSpeed;
    private int routeLockPhysicsRevision;
    private int routeLockHazardId;
    private BonusBoardSegment lockedRouteTarget;
    private BonusJumpPlan lockedRoutePlan;
    private JumpPhysicsSnapshot latestPhysicsSnapshot;
    private BonusStageState latestState;
    private bool learningSampleActive;
    private bool learningMayLearnGroundKinematics;
    private long nextLearningSampleId;
    private long learningSampleId;
    private long nextRouteDecisionId;
    private long activeRouteDecisionId;
    private long automaticAttemptId;
    private string learningSource;
    private string learningMap;
    private int learningSection;
    private float learningInputDownTime;
    private float learningInputUpTime;
    private float learningTakeoffTime;
    private Vector3 learningTriggerPosition;
    private Vector3 learningTakeoffPosition;
    private Vector2 learningTakeoffVelocity;
    private Vector3 learningApexPosition;
    private float learningMaximumY;
    private float learningPreviousVelocityY;
    private Vector3 learningLastObservedPosition;
    private float learningLastObservedAt;
    private bool learningTookOff;
    private bool learningFirstApexCaptured;
    private bool learningInputReleased;
    private long landingCandidateLastFixedStep = -1;
    private int landingCandidateStableFixedSteps;
    private float landingCandidateTop;
    private int landingCandidateColliderId;
    private bool automaticPredictionActive;
    private float automaticPredictedLandingX;
    private float automaticPredictedFlightSeconds;
    private float automaticPredictedHorizontalTravel;
    private float automaticPredictionLaunchFeetY;
    private float automaticPlannedTravelScale;
    private float automaticPlannedHold;
    private float automaticTriggerSpeed;
    private float automaticTargetHeightDelta;
    private int automaticPhysicsRevision;
    private float automaticTargetSafeLeft;
    private float automaticTargetSafeRight;
    private float automaticTargetLeft;
    private float automaticTargetRight;
    private float automaticTargetTop;
    private int automaticTargetColliderId;
    private string automaticTargetColliderName;
    private string automaticTargetMapPieceName;
    private float automaticTargetMapPieceOriginX;
    private int automaticTargetMapPieceInstanceId;
    private int automaticTargetRegistryGeneration;
    private int automaticTargetStaticSurfaceIndex;
    private string automaticPlanReason;
    private BonusManeuverKind automaticManeuver;
    private Vector3 automaticPlanTriggerPosition;
    private float automaticPlannedLaunchX;
    private float automaticLaunchWindowLeft;
    private float automaticLaunchWindowRight;
    private BonusBoardSegment automaticSourceSegment;
    private string automaticHazardAtPlan;
    private int automaticSphereCountAtPlan = -1;
    private int automaticExpectedSphereHits;
    private string automaticSpheresAtPlan = "Unavailable";
    private bool automaticControlSuspended;
    private JumpPhysicsSnapshot automaticPlanPhysicsSnapshot;
    private bool automaticTrajectoryCompatible;
    private float nextTrajectoryMonitorLogTime;
    private float nextDynamicPlanLogTime;
    private bool secondStagePreviewActive;
    private BonusBoardSegment secondStageExpectedSupport;
    private float secondStageExpectedLandingX;
    private BonusBoardScanResult secondStageProjectedScan;
    private BonusJumpPlan secondStageProjectedPlan;
    private string secondStageSource;
    private string secondStageSignature;
    private float nextSecondStageRefreshTime;
    private bool secondStageObservedAirborne;
    private bool manualWallSequenceActive;
    private int manualWallJumpCount;
    private float manualWallJumpDownTime;
    private float manualWallJumpHold;
    private Vector3 manualWallJumpPosition;
    private Vector2 manualWallJumpStartVelocity;
    private Vector2 manualWallPoint;
    private float manualWallLastPulseTime = -10f;
    private long nextManualWallSequenceId;
    private long manualWallSequenceId;
    private int manualWallColliderId;
    private string manualWallColliderName = string.Empty;
    private bool manualDemonstrationActive;
    private long nextManualDemonstrationId;
    private long manualDemonstrationId;
    private int manualDemonstrationMousePresses;
    private float manualDemonstrationStartedAt;
    private Vector3 manualDemonstrationStartPosition;
    private int manualDemonstrationStartSection;
    private int manualDemonstrationStartSpheres = -1;
    private bool manualDemonstrationPitLogged;
    private long manualPitCandidateLastFixedStep = -1;
    private int manualPitCandidateFixedSteps;
    private float nextManualPitEvidenceTime;
    private bool previousObservedMouseInput;
    private float nextManualDemonstrationFrameTime;
    private string lastManualDemonstrationSignature = string.Empty;
    private long lastEvidenceFixedStep = -1;
    private string lastControlGateSignature = string.Empty;
    private string lastEvidenceSignature = string.Empty;
    private float nextGeneralEvidenceTime;
    private float nextCompletionNavigationLogTime;
    private long completionInvalidRouteLastFixedStep = -1;
    private int completionInvalidRouteStableFixedSteps;

    public void Start()
    {
        jumpPhysicsFeedback.Attach();
        automationEnabled = Plugin.Config?.EnabledOnStartup?.Value == true;
        string configuredKey = Plugin.Config?.ToggleKey?.Value;
        if (!string.IsNullOrWhiteSpace(configuredKey) &&
            System.Enum.TryParse(configuredKey.Trim(), true, out KeyCode parsedKey) &&
            parsedKey != KeyCode.None)
        {
            toggleKey = parsedKey;
        }
        else
        {
            BonusRunnerLog.Warning($"Invalid Toggle Key '{configuredKey}'. Using U.");
        }

        BonusRunnerLog.User(
            $"AutoBonusRunner runtime ready; automation is {(automationEnabled ? "enabled" : "disabled")}. " +
            $"Press {toggleKey} to toggle automatic control. Detection and manual-jump learning stay active. " +
            $"CompletionRewardActions={Plugin.Config?.CompletionRewardActions?.Value == true}, " +
            $"CompletionWindDash={Plugin.Config?.CompletionWindDash?.Value == true}.");
    }

    public void Update()
    {
        try
        {
            if (Input.GetKeyDown(toggleKey))
            {
                automationEnabled = !automationEnabled;
                jumpController.Release();
                if (!automationEnabled &&
                    learningSampleActive &&
                    learningSource == "Automatic" &&
                    latestState.HasPlayer)
                {
                    FinishLearningSample(latestState, "AutomationDisabled");
                }
                ResetAutomaticControlState();
                completionRewardController.Reset(
                    automationEnabled
                        ? "AutomationEnabled"
                        : "AutomationDisabled");
                if (automationEnabled)
                {
                    nextAutomaticAttemptTime = 0f;
                    EndManualDemonstration(
                        latestState,
                        "AutomationEnabled");
                }
                string message = automationEnabled
                    ? "Auto Bonus Runner Enabled!"
                    : "Auto Bonus Runner Disabled!";
                BonusRunnerLog.User(message);
                Plugin.ModHelperInstance?.ShowNotification(message, automationEnabled);
            }

            BonusStageState state = detector.Capture();
            latestState = state;
            RefreshMapRegistry(state);
            ObserveReliableHorizontalSpeed(state);
            if (state.IsBonusStage != previousBonusState)
            {
                previousBonusState = state.IsBonusStage;
                BonusRunnerLog.User(state.IsBonusStage
                    ? $"Bonus Stage detected: Map={state.MapName}, Section={state.SectionIndex}."
                    : "Bonus Stage ended.");
                previousSectionIndex = state.SectionIndex;
                previousPlayerInstanceId = state.PlayerInstanceId;
                if (state.IsBonusStage)
                {
                    movementInitialized = false;
                    pitDescentGuardActive = false;
                    pitRespawnLastFixedStep = -1;
                    pitRespawnStableFixedSteps = 0;
                    lastReliableHorizontalSpeed = 9.5f;
                    ResetSectionCruiseSpeed();
                    speedPlanningResumeFixedStep = -1;
                    completionDashPlanningResumeFixedStep = -1;
                    nextAutomaticAttemptTime = 0f;
                    lastRouteSignature = string.Empty;
                    jumpPhysicsFeedback.ResetRouteCalibration(
                        "BonusStageEntered:Section0");
                    BonusStageInspector.LogControllerSnapshot("BonusStageEntered");
                }
            }

            if (!state.IsBonusStage)
            {
                EndManualDemonstration(state, "StageEnded");
                FinishLearningSample(state, "StageEnded");
                jumpController.Release();
                jumpPhysicsFeedback.ResetTransient("StageEnded");
                ResetAutomaticControlState();
                completionRewardController.Reset("StageEnded");
                movementInitialized = false;
                bonusGameplayStarted = false;
                ResetSectionCruiseSpeed();
                speedPlanningResumeFixedStep = -1;
                completionDashPlanningResumeFixedStep = -1;
                return;
            }

            if (state.IsActiveGameplay)
                bonusGameplayStarted = true;

            PlayerMovement observedPlayer = PlayerMovement.instance;
            if (state.HasPlayer && observedPlayer != null)
            {
                latestPhysicsSnapshot = jumpPhysicsFeedback.CaptureSnapshot(
                    observedPlayer);
            }

            if (state.SectionIndex != previousSectionIndex)
            {
                EndManualDemonstration(state, "SectionChanged");
                BonusRunnerLog.User($"Bonus Stage section changed: {previousSectionIndex} -> {state.SectionIndex}.");
                // Active-play bookkeeping is section-scoped. Completion
                // traversal is deliberately not gated by this flag: during
                // the real reward road the section index advances first while
                // the completed quota remains visible (for example 38/38).
                // The new section's 0/N quota naturally ends completion mode.
                bonusGameplayStarted = false;
                FinishLearningSample(state, "SectionChanged");
                jumpController.Release();
                jumpPhysicsFeedback.ResetTransient("SectionChanged");
                jumpPhysicsFeedback.ResetRouteCalibration(
                    $"SectionChanged:{previousSectionIndex}->{state.SectionIndex}");
                ResetAutomaticControlState();
                completionRewardController.Reset("SectionChanged");
                nextAutomaticAttemptTime = 0f;
                movementInitialized = false;
                pitDescentGuardActive = false;
                pitRespawnLastFixedStep = -1;
                pitRespawnStableFixedSteps = 0;
                ResetSectionCruiseSpeed();
                speedPlanningResumeFixedStep = -1;
                completionDashPlanningResumeFixedStep = -1;
                previousSectionIndex = state.SectionIndex;
                BonusStageInspector.LogControllerSnapshot("SectionChanged");
            }

            if (state.PlayerInstanceId != 0 && state.PlayerInstanceId != previousPlayerInstanceId)
            {
                EndManualDemonstration(state, "PlayerInstanceChanged");
                BonusRunnerLog.Debug(
                    $"Player instance changed: {previousPlayerInstanceId} -> {state.PlayerInstanceId}. This may indicate spawn or recovery.",
                    "Detection");
                FinishLearningSample(state, "PlayerInstanceChanged");
                jumpController.Release();
                jumpPhysicsFeedback.ResetTransient("PlayerInstanceChanged");
                ResetAutomaticControlState();
                completionRewardController.Reset("PlayerInstanceChanged");
                nextAutomaticAttemptTime = 0f;
                previousPlayerInstanceId = state.PlayerInstanceId;
                movementInitialized = false;
                lastRouteSignature = string.Empty;
            }

            LogMovementEvents(state);
            UpdateLearningSample(state);
            ValidateAutomaticJumpResponse(state);

            if (BonusRunnerLog.IsDebugMode && Time.unscaledTime >= nextDiagnosticTime)
            {
                nextDiagnosticTime = Time.unscaledTime + DiagnosticIntervalSeconds;
                BonusRunnerLog.Debug(
                    $"State={state.GameStateName}, Map={state.MapName}, Section={state.SectionIndex}, " +
                    $"PlayerId={state.PlayerInstanceId}, TimeScale={Time.timeScale:F2}, " +
                    $"Player={(state.HasPlayer ? $"({state.PlayerPosition.x:F2},{state.PlayerPosition.y:F2})" : "Unavailable")}, " +
                    $"Velocity=({state.PlayerVelocity.x:F2},{state.PlayerVelocity.y:F2}), Grounded={state.IsGrounded}, " +
                    $"Spheres={(state.HasSphereProgress ? $"{state.CollectedSpheres}/{Math.Ceiling(state.RequiredSpheres):F0}" : "Unavailable")}, " +
                    $"RequiredRaw={(state.HasSphereProgress ? state.RequiredSpheres.ToString("F3") : "Unavailable")}, " +
                    $"Remaining={state.RemainingRequiredSpheres}, Timer={state.CurrentTime:F2}/{state.MaximumTime:F2}, " +
                    $"TimerVisible={state.IsTimerVisible}, SupportedMap={state.IsSupportedBonusMap}, " +
                    $"ActiveGameplay={state.IsActiveGameplay}, FellOff={state.CharacterFellOff}, " +
                    $"SpiritBoost={state.SpiritBoostEnabled}",
                    "Detection");
            }
        }
        catch (System.Exception exception)
        {
            jumpController.Release();
            jumpPhysicsFeedback.ResetTransient("RuntimeException");
            ResetAutomaticControlState();
            nextAutomaticAttemptTime = Time.unscaledTime + 0.50f;
            if (Time.unscaledTime >= nextErrorLogTime)
            {
                nextErrorLogTime = Time.unscaledTime + 5f;
                BonusRunnerLog.Error($"Runtime observation failed safely: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    public void OnEnable() => jumpPhysicsFeedback.Attach();

    public void OnDisable()
    {
        jumpController.Release();
        completionRewardController.Reset("RuntimeDisabled");
        jumpPhysicsFeedback.ResetTransient("RuntimeDisabled");
        jumpPhysicsFeedback.Detach();
    }

    public void OnDestroy()
    {
        jumpController.Release();
        completionRewardController.Reset("RuntimeDestroyed");
        jumpPhysicsFeedback.ResetTransient("RuntimeDestroyed");
        jumpPhysicsFeedback.Detach();
    }

    private void RefreshMapRegistry(BonusStageState state)
    {
        if (!state.IsBonusStage || state.SectionIndex < 0)
        {
            if (lastMapRegistryGeneration >= 0)
                platformScanner.ResetStaticMap("OutsideBonusStage");
            lastMapRegistryGeneration = -1;
            lastMapRegistryStatus = string.Empty;
            nextMapRegistryRefreshTime = 0f;
            return;
        }

        BonusMapPieceRegistry registry = platformScanner.MapRegistry;
        bool sectionChanged = registry.SectionIndex != state.SectionIndex;
        if (!sectionChanged &&
            registry.State == BonusMapPieceRegistryState.Ready &&
            Time.unscaledTime < nextMapRegistryRefreshTime)
            return;

        bool ready = platformScanner.RefreshStaticMap(state.SectionIndex);
        nextMapRegistryRefreshTime = Time.unscaledTime + (ready ? 0.25f : 0.05f);
        string status = $"{registry.State}:{registry.StatusReason}:" +
                        $"Section={registry.SectionIndex}:Pieces={registry.PieceCount}";
        if (registry.Generation == lastMapRegistryGeneration &&
            string.Equals(status, lastMapRegistryStatus, StringComparison.Ordinal))
        {
            return;
        }

        bool generationChanged =
            lastMapRegistryGeneration >= 0 &&
            registry.Generation != lastMapRegistryGeneration;
        BonusRunnerLog.Debug(
            $"StaticMapRegistry State={registry.State}, " +
            $"Section={registry.SectionIndex}, Generation={registry.Generation}, " +
            $"Pieces={registry.PieceCount}, Reason={registry.StatusReason}, " +
            $"GenerationChanged={generationChanged}. " +
            (ready
                ? "Ground clone transforms are aligned with the embedded static map."
                : "Static routing is pending; live geometry remains the fallback."),
            "Map");
        // Pool recycling behind the player changes the registry generation,
        // but it does not invalidate an unchanged live target ahead. Route
        // locks already revalidate target bounds, height, collider/static
        // identity, speed and physics every frame. Clearing them globally on
        // every remote clone hand-off repeatedly erased launch windows and
        // wall follow-through previews.
        lastMapRegistryGeneration = registry.Generation;
        lastMapRegistryStatus = status;
    }

    private void ObserveReliableHorizontalSpeed(BonusStageState state)
    {
        if (!state.IsBonusStage || !state.HasPlayer)
            return;

        float observed = Mathf.Abs(state.PlayerVelocity.x);
        bool inRange = observed > 1f && observed < 80f;
        bool wallControlActive =
            passiveWallApproachActive ||
            attachedObjectiveDescentActive ||
            wallExitContactWatchActive ||
            wallActionPhase != WallActionPhase.None &&
            wallActionPhase != WallActionPhase.Completed &&
            wallActionPhase != WallActionPhase.Failed;
        bool collisionLikeAirborneDrop =
            !state.IsGrounded &&
            lastReliableHorizontalSpeed > 2f &&
            observed < lastReliableHorizontalSpeed * 0.65f;
        bool legitimateWallBoostIncrease =
            wallControlActive &&
            wallRouteSpeedLatched &&
            state.SpiritBoostEnabled &&
            inRange &&
            observed > wallRouteHorizontalSpeed + 1.0f &&
            observed > lastReliableHorizontalSpeed + 1.0f;
        bool completionTraversal = IsSuccessfulCompletionTraversal(state);
        bool routeMotionActive =
            state.IsActiveGameplay || completionTraversal;
        bool accepted =
            legitimateWallBoostIncrease ||
            inRange &&
            routeMotionActive &&
            !pitDescentGuardActive &&
            !wallControlActive &&
            !collisionLikeAirborneDrop;

        string decision = legitimateWallBoostIncrease
            ? "AcceptedWallSpiritBoostIncrease"
            : accepted
            ? completionTraversal
                ? "AcceptedCompletionTraversal"
                : "AcceptedStableRun"
            : !inRange
                ? "RejectedOutOfRange"
                : !routeMotionActive
                    ? "RejectedInactiveGameplay"
                    : pitDescentGuardActive
                        ? "RejectedPitLifecycle"
                        : wallControlActive
                            ? "RejectedWallAction"
                            : collisionLikeAirborneDrop
                                ? "RejectedAirborneCollisionDrop"
                                : "RejectedUnknown";
        float prior = lastReliableHorizontalSpeed;
        if (accepted)
        {
            lastReliableHorizontalSpeed = observed;
            if (legitimateWallBoostIncrease)
                wallRouteHorizontalSpeed = observed;

            // A large one-physics-step velocity discontinuity is an ability or
            // pickup transition, not evidence that an already calculated
            // launch window remains valid.  Recompute immediately, but defer
            // an actual ground-jump DOWN until one subsequent FixedUpdate has
            // exposed the new velocity.  Passive wall-contact routes remain
            // legal because they send no airborne input.
            float discontinuity = Mathf.Abs(observed - prior);
            float discontinuityThreshold = Mathf.Max(4.0f, prior * 0.20f);
            if (prior > 1f && discontinuity >= discontinuityThreshold)
            {
                speedPlanningResumeFixedStep = Math.Max(
                    speedPlanningResumeFixedStep,
                    JumpPhysicsFeedback.FixedStepSequence + 1);
            }
        }
        ObserveSectionCruiseSpeed(
            state,
            observed,
            accepted && !legitimateWallBoostIncrease && !wallControlActive);

        bool decisionChanged = !string.Equals(
            decision,
            lastSpeedObservationDecision,
            StringComparison.Ordinal);
        if (BonusRunnerLog.IsDebugMode &&
            (decisionChanged || Time.unscaledTime >= nextSpeedObservationLogTime))
        {
            lastSpeedObservationDecision = decision;
            nextSpeedObservationLogTime = Time.unscaledTime + 0.50f;
            BonusRunnerLog.Debug(
                $"HorizontalSpeedObservation ObservedVX={observed:F3}, " +
                $"PriorRunSpeed={prior:F3}, RunSpeedEstimate=" +
                $"{lastReliableHorizontalSpeed:F3}, WallEntrySpeed=" +
                $"{(wallRouteSpeedLatched ? wallRouteHorizontalSpeed.ToString("F3") : "Unavailable")}, " +
                $"SectionCruiseVX=" +
                $"{(sectionCruiseHorizontalSpeed > 1f ? sectionCruiseHorizontalSpeed.ToString("F3") : "Unresolved")}, " +
                $"CruiseCandidate={sectionCruiseCandidateSpeed:F3}/" +
                $"{sectionCruiseCandidateFixedSteps}, " +
                $"SpeedUpdateAccepted={accepted}, Decision={decision}, " +
                $"RouteMotionActive={routeMotionActive}, " +
                $"JumpResumeFixedStep={speedPlanningResumeFixedStep}, " +
                $"Grounded={state.IsGrounded}, SpiritBoost=" +
                $"{state.SpiritBoostEnabled}, WallPhase={wallActionPhase}.",
                "Physics");
        }
    }

    private void ObserveSectionCruiseSpeed(
        BonusStageState state,
        float observed,
        bool acceptedStableRun)
    {
        bool eligible =
            acceptedStableRun &&
            state.IsActiveGameplay &&
            state.IsGrounded &&
            Mathf.Abs(state.PlayerVelocity.y) <= 2.5f &&
            observed > 1f && observed < 80f;
        long fixedStep = JumpPhysicsFeedback.FixedStepSequence;
        if (!eligible)
        {
            if (sectionCruiseCandidateLastFixedStep >= 0 &&
                fixedStep - sectionCruiseCandidateLastFixedStep > 2)
            {
                sectionCruiseCandidateSpeed = 0f;
                sectionCruiseCandidateFixedSteps = 0;
                sectionCruiseCandidateLastFixedStep = -1;
            }
            return;
        }

        if (fixedStep == sectionCruiseCandidateLastFixedStep)
            return;

        const float plateauTolerance = 0.04f;
        bool samePlateau =
            sectionCruiseCandidateFixedSteps > 0 &&
            Mathf.Abs(observed - sectionCruiseCandidateSpeed) <=
                plateauTolerance;
        if (!samePlateau)
        {
            sectionCruiseCandidateSpeed = observed;
            sectionCruiseCandidateFixedSteps = 1;
        }
        else
        {
            sectionCruiseCandidateSpeed =
                (sectionCruiseCandidateSpeed *
                    sectionCruiseCandidateFixedSteps + observed) /
                (sectionCruiseCandidateFixedSteps + 1);
            sectionCruiseCandidateFixedSteps++;
        }
        sectionCruiseCandidateLastFixedStep = fixedStep;

        if (sectionCruiseCandidateFixedSteps < 3)
            return;

        bool firstResolution = sectionCruiseHorizontalSpeed <= 1f;
        bool sameEstablishedTier =
            !firstResolution &&
            Mathf.Abs(
                sectionCruiseCandidateSpeed - sectionCruiseHorizontalSpeed) <=
                0.20f;
        if (!firstResolution && !sameEstablishedTier)
            return;

        sectionCruiseHorizontalSpeed = firstResolution
            ? sectionCruiseCandidateSpeed
            : Mathf.Lerp(
                sectionCruiseHorizontalSpeed,
                sectionCruiseCandidateSpeed,
                0.25f);
        if (firstResolution)
        {
            BonusRunnerLog.Debug(
                $"SectionCruiseSpeedEstablished Section={state.SectionIndex}, " +
                $"CruiseVX={sectionCruiseHorizontalSpeed:F3}, " +
                $"CandidateFixedSteps={sectionCruiseCandidateFixedSteps}, " +
                $"ObservedVX={observed:F3}, SpiritBoostFlag=" +
                $"{state.SpiritBoostEnabled}. Exact grounded plateaus, not " +
                "transient boost or collision samples, define the section " +
                "deceleration floor.",
                "Physics");
        }
    }

    private void ResetSectionCruiseSpeed()
    {
        sectionCruiseHorizontalSpeed = 0f;
        sectionCruiseCandidateSpeed = 0f;
        sectionCruiseCandidateLastFixedStep = -1;
        sectionCruiseCandidateFixedSteps = 0;
    }

    private void LatchWallRouteSpeed(BonusStageState state, string source)
    {
        float observed = Mathf.Abs(state.PlayerVelocity.x);
        bool observedUsable = observed > 1f && observed < 80f &&
            (lastReliableHorizontalSpeed <= 1f ||
             observed >= lastReliableHorizontalSpeed * 0.65f ||
             observed > lastReliableHorizontalSpeed);
        wallRouteHorizontalSpeed = observedUsable
            ? observed
            : Mathf.Max(1f, lastReliableHorizontalSpeed);
        wallRouteSpeedLatched = true;
        BonusRunnerLog.Debug(
            $"WallRouteSpeedLatched Source={source}, ObservedVX={observed:F3}, " +
            $"ReliableVX={lastReliableHorizontalSpeed:F3}, " +
            $"WallEntrySpeed={wallRouteHorizontalSpeed:F3}, " +
            $"ObservedAccepted={observedUsable}, SpiritBoost=" +
            $"{state.SpiritBoostEnabled}. Collision/stall samples cannot " +
            "change this route's horizontal prediction until verified landing.",
            "Physics");
    }

    private float GetWallRouteSpeed() => wallRouteSpeedLatched
        ? Mathf.Max(1f, wallRouteHorizontalSpeed)
        : Mathf.Max(1f, lastReliableHorizontalSpeed);

    [HideFromIl2Cpp]
    private static JumpPhysicsSnapshot BuildPlanningPhysics(
        int sectionIndex,
        JumpPhysicsSnapshot observed,
        float liveRunSpeed,
        bool useSectionCruiseFloor)
    {
        // Route calibration is reset at every section boundary, so these
        // profiles contain only same-section observations. Keep the helper at
        // every planning boundary to make that invariant explicit and to
        // prevent future callers from bypassing the section-scoped snapshot.
        _ = sectionIndex;
        if (!useSectionCruiseFloor ||
            liveRunSpeed <= 1f || liveRunSpeed >= 80f)
        {
            return observed;
        }

        // Section 3's normal authored run speed is 16.9 even when the global
        // feedback median still says 11.9.  Treat a verified, non-Spirit
        // active-gameplay speed as this section's cruise floor; otherwise the
        // integrator invents a 5 u/s^2 slowdown that the trace does not have.
        return observed with
        {
            BaseHorizontalSpeed = Mathf.Max(
                observed.BaseHorizontalSpeed,
                liveRunSpeed)
        };
    }

    [HideFromIl2Cpp]
    private static JumpPhysicsSnapshot BuildWallPlanningPhysics(
        JumpPhysicsSnapshot observed)
    {
        float wallJumpVelocity = Mathf.Clamp(observed.RawJumpForce, 5f, 50f);
        return observed with
        {
            JumpVelocity = wallJumpVelocity,
            VelocitySampleCount = 0,
            VelocitySource = "WallJumpForce",
            InputDelaySeconds = Mathf.Clamp(
                observed.FixedDeltaTime,
                0f,
                0.25f),
            DelaySampleCount = 0,
            DelaySource = "WallFixedStep",
            HorizontalTravelScale = 1f,
            LandingSampleCount = 0,
            TravelProfile = default,
            LandingErrorProfile = default
        };
    }

    [HideFromIl2Cpp]
    private static JumpPhysicsSnapshot BuildWallExitPlanningPhysics(
        JumpPhysicsSnapshot wallPhysics,
        float postLipHorizontalSpeed,
        float sectionCruiseSpeed,
        bool usePostLipAsCruiseFloor)
    {
        // Wall contact has two horizontal phases: VX is physically zero while
        // attached, then the pre-contact run speed resumes after lip clearance.
        // Wall flight timing is independent of the ground-jump flight-scale
        // calibration, and the normal Section-3 trace proves that resumed
        // 16.9 speed stays constant through landing. Spirit Boost and
        // completion abilities are transient, however, so their observed
        // speed must retain the model's deceleration toward BaseVX.
        float resolvedBaseSpeed = wallPhysics.BaseHorizontalSpeed;
        if (sectionCruiseSpeed > 1f && sectionCruiseSpeed < 80f)
        {
            resolvedBaseSpeed = Mathf.Max(
                resolvedBaseSpeed,
                sectionCruiseSpeed);
        }
        if (usePostLipAsCruiseFloor)
        {
            resolvedBaseSpeed = Mathf.Max(
                resolvedBaseSpeed,
                Mathf.Clamp(postLipHorizontalSpeed, 1f, 80f));
        }

        return wallPhysics with
        {
            BaseHorizontalSpeed = resolvedBaseSpeed,
            FlightTimeScale = 1f
        };
    }

    [HideFromIl2Cpp]
    private static JumpPhysicsSnapshot BuildMandatoryFacePlanningPhysics(
        JumpPhysicsSnapshot observed)
    {
        JumpPhysicsSnapshot wallPhysics = BuildWallPlanningPhysics(observed);
        float fixedStep = Mathf.Clamp(
            wallPhysics.FixedDeltaTime,
            0.005f,
            0.05f);
        float heldStepVelocity = Mathf.Clamp(
            wallPhysics.RawJumpForce -
                wallPhysics.GravityMagnitude * fixedStep,
            5f,
            50f);
        return wallPhysics with
        {
            JumpVelocity = heldStepVelocity,
            VelocitySampleCount = 0,
            VelocitySource = "WallHeldFixedStep"
        };
    }

    public void LateUpdate()
    {
        try
        {
            if (!automationEnabled || !latestState.IsBonusStage)
            {
                LogControlGateEvidence(
                    latestState,
                    !automationEnabled
                        ? "AutomationDisabled"
                        : "OutsideBonusStage");
                jumpController.Release();
                completionRewardController.Reset(
                    !automationEnabled
                        ? "AutomationDisabled"
                        : "OutsideBonusStage");
                return;
            }

            if (Time.unscaledTime - lastManualInputTime < 0.40f)
            {
                LogControlGateEvidence(latestState, "ManualInputCooldown");
                jumpController.Release();
                return;
            }

            LogControlGateEvidence(latestState, "ControlEligible");
            UpdateAutomaticJump(latestState);
            LogActionEvidence(latestState, PlayerMovement.instance);
        }
        catch (System.Exception exception)
        {
            jumpController.Release();
            jumpPhysicsFeedback.ResetTransient("LateUpdateException");
            ResetAutomaticControlState();
            nextAutomaticAttemptTime = Time.unscaledTime + 0.50f;
            if (Time.unscaledTime >= nextErrorLogTime)
            {
                nextErrorLogTime = Time.unscaledTime + 5f;
                BonusRunnerLog.Error(
                    $"LateUpdate jump control failed safely: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    private void LogControlGateEvidence(
        BonusStageState state,
        string gate)
    {
        if (!BonusRunnerLog.IsDebugMode)
            return;

        string signature =
            $"{gate}:{state.GameStateName}:{state.SectionIndex}:" +
            $"{state.RemainingRequiredSpheres}:{automationEnabled}:" +
            $"{Plugin.Config?.AutomaticJumping?.Value == true}:" +
            $"{completionRewardController.IsTraversalActive}:" +
            $"{Plugin.Config?.CompletionRewardActions?.Value == true}:" +
            $"{Plugin.Config?.CompletionWindDash?.Value == true}";
        if (string.Equals(
                signature,
                lastControlGateSignature,
                StringComparison.Ordinal))
        {
            return;
        }

        lastControlGateSignature = signature;
        BonusRunnerLog.Debug(
            $"ControlGate Gate={gate}, Frame={Time.frameCount}, " +
            $"FixedStep={JumpPhysicsFeedback.FixedStepSequence}, " +
            $"AutomationEnabled={automationEnabled}, " +
            $"AutomaticJumping={Plugin.Config?.AutomaticJumping?.Value == true}, " +
            $"CompletionTraversal={completionRewardController.IsTraversalActive}, " +
            $"CompletionRewardActions={Plugin.Config?.CompletionRewardActions?.Value == true}, " +
            $"CompletionWindDash={Plugin.Config?.CompletionWindDash?.Value == true}, " +
            $"GameState={state.GameStateName}, Map={state.MapName}, " +
            $"Section={state.SectionIndex}, HasPlayer={state.HasPlayer}, " +
            $"BonusGameplayStarted={bonusGameplayStarted}, " +
            $"Spheres={(state.HasSphereProgress ? $"{state.CollectedSpheres}/{Math.Ceiling(state.RequiredSpheres):F0}" : "Unavailable")}, " +
            $"Remaining={state.RemainingRequiredSpheres}, " +
            $"FellOff={state.CharacterFellOff}, " +
            $"ManualCooldownRemaining={Mathf.Max(0f, 0.40f - (Time.unscaledTime - lastManualInputTime)):F3}s, " +
            $"Holding={jumpController.IsHoldingJump}, " +
            $"LearningActive={learningSampleActive}, " +
            $"LearningSource={learningSource ?? "None"}, " +
            $"PredictionActive={automaticPredictionActive}, " +
            $"WallPhase={wallActionPhase}.",
            "Evidence");
    }

    private void LogActionEvidence(
        BonusStageState state,
        PlayerMovement player)
    {
        if (!BonusRunnerLog.IsDebugMode || player == null || !state.HasPlayer)
            return;

        bool completionTraversal = IsSuccessfulCompletionTraversal(state);
        bool wallActionActive =
            wallActionPhase != WallActionPhase.None &&
            wallActionPhase != WallActionPhase.Completed &&
            wallActionPhase != WallActionPhase.Failed;
        bool actionActive =
            wallActionActive || completionTraversal ||
            automaticPredictionActive || jumpController.IsHoldingJump ||
            (learningSampleActive && learningSource == "Automatic");
        if (!actionActive)
            return;

        long fixedStep = JumpPhysicsFeedback.FixedStepSequence;
        BonusWallContact wall = wallDetector.Detect(
            player,
            GetWallRouteSpeed());
        string signature =
            $"{wallActionPhase}:{wallRecoveryAttempts}:" +
            $"{jumpController.IsHoldingJump}:{state.IsGrounded}:" +
            $"{wall.IsDetected}:{wall.IsTouching}:" +
            $"{wallRecoveryLipCrossed}:{wallResidualRiseWaitActive}:" +
            $"{wallExitFaceContactRequired}:" +
            $"{wallMandatoryFaceSetupActive}:" +
            $"{wallMandatoryFaceInterceptCommitted}:" +
            $"{automaticAttemptId}:" +
            $"{state.GameStateName}:{state.RemainingRequiredSpheres}";
        bool eventChanged = !string.Equals(
            signature,
            lastEvidenceSignature,
            StringComparison.Ordinal);
        bool shouldLog = wallActionActive
            ? fixedStep != lastEvidenceFixedStep
            : eventChanged || Time.unscaledTime >= nextGeneralEvidenceTime;
        if (!shouldLog)
            return;

        lastEvidenceFixedStep = fixedStep;
        lastEvidenceSignature = signature;
        nextGeneralEvidenceTime = Time.unscaledTime + 0.25f;

        BonusBoardScanResult scan = default;
        string scanSummary;
        try
        {
            scan = platformScanner.Scan(
                state.PlayerPosition,
                player,
                Mathf.Max(
                    Mathf.Abs(state.PlayerVelocity.x),
                    lastReliableHorizontalSpeed));
            scanSummary = DescribeEvidenceScan(scan);
        }
        catch (System.Exception exception)
        {
            scanSummary = $"ScanFailed:{exception.GetType().Name}:{exception.Message}";
        }

        Collider2D playerCollider = player.playerCollider;
        string playerBounds = playerCollider != null
            ? $"Center=({playerCollider.bounds.center.x:F3},{playerCollider.bounds.center.y:F3})," +
              $"Min=({playerCollider.bounds.min.x:F3},{playerCollider.bounds.min.y:F3})," +
              $"Max=({playerCollider.bounds.max.x:F3},{playerCollider.bounds.max.y:F3})," +
              $"Extents=({playerCollider.bounds.extents.x:F3},{playerCollider.bounds.extents.y:F3})"
            : "Unavailable";
        string wallSummary = wall.IsDetected
            ? $"Detected=True,Touching={wall.IsTouching},Distance={wall.Distance:F3}," +
              $"BodyGap={wall.BodyGap:F3},FaceX={wall.FaceX:F3}," +
              $"Point=({wall.Point.x:F3},{wall.Point.y:F3})," +
              $"Normal=({wall.Normal.x:F3},{wall.Normal.y:F3})," +
              $"Collider={wall.ColliderInstanceId}:{wall.ColliderName},Reason={wall.Reason}"
            : $"Detected=False,Reason={wall.Reason}";
        string exitTarget = wallExitTargetActive
            ? DescribeEvidenceSegment(wallExitTarget)
            : "None";
        string target = automaticPredictionActive || wallActionActive
            ? $"Raw=[{automaticTargetLeft:F3},{automaticTargetRight:F3}]" +
              $" Safe=[{automaticTargetSafeLeft:F3},{automaticTargetSafeRight:F3}]" +
              $" Top={automaticTargetTop:F3} Collider=" +
              $"{automaticTargetColliderId}:{automaticTargetColliderName}" +
              $" Piece={automaticTargetMapPieceName}#" +
              $"{automaticTargetMapPieceInstanceId}/G" +
              $"{automaticTargetRegistryGeneration}/S" +
              $"{automaticTargetStaticSurfaceIndex}"
            : "None";
        float holdElapsed = jumpController.LastPressStartedAt >= 0f
            ? Mathf.Max(0f, Time.unscaledTime - jumpController.LastPressStartedAt)
            : -1f;
        float commitmentRemaining = Mathf.Max(
            0f,
            wallRecoveryCommitmentUntil - Time.unscaledTime);

        BonusRunnerLog.Debug(
            $"ActionPhysicsFrame EventChanged={eventChanged}, " +
            $"Frame={Time.frameCount}, FixedStep={fixedStep}, " +
            $"UnscaledTime={Time.unscaledTime:F3}, TimeScale={Time.timeScale:F3}, " +
            $"RouteId={activeRouteDecisionId}, AttemptId={automaticAttemptId}, " +
            $"LearningId={learningSampleId}, GameState={state.GameStateName}, " +
            $"Map={state.MapName}, Section={state.SectionIndex}, " +
            $"CompletionTraversal={completionTraversal}, " +
            $"BonusGameplayStarted={bonusGameplayStarted}, " +
            $"Spheres={(state.HasSphereProgress ? $"{state.CollectedSpheres}/{Math.Ceiling(state.RequiredSpheres):F0}" : "Unavailable")}, " +
            $"Remaining={state.RemainingRequiredSpheres}, FellOff={state.CharacterFellOff}, " +
            $"SpiritBoost={state.SpiritBoostEnabled}, Timer={state.CurrentTime:F3}/{state.MaximumTime:F3}, " +
            $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
            $"Grounded={state.IsGrounded}, PlayerBounds[{playerBounds}], " +
            $"PlayerJump[IsJumping={player.isJumping},JumpTime={player.jumpTime:F3}," +
            $"Counter={player.jumpTimeCounter:F3},Force={player.jumpForce:F3}], " +
            $"Input[ControllerHolding={jumpController.IsHoldingJump}," +
            $"HoldElapsed={holdElapsed:F3},LastDown={jumpController.LastPressStartedAt:F3}," +
            $"LastUp={jumpController.LastReleaseAt:F3},PanelDown={JumpPanel.jumpDown}," +
            $"PanelPressed={JumpPanel.jumpPressed},PanelUp={JumpPanel.jumpUp}," +
            $"HeldPointers={(JumpPanel.instance != null ? JumpPanel.instance.heldPointerCount : -1)}], " +
            $"Plan[Maneuver={automaticManeuver},Reason={automaticPlanReason ?? "None"}," +
            $"Trigger=({automaticPlanTriggerPosition.x:F3},{automaticPlanTriggerPosition.y:F3})," +
            $"Launch={automaticPlannedLaunchX:F3}," +
            $"Window=[{automaticLaunchWindowLeft:F3},{automaticLaunchWindowRight:F3}]," +
            $"Hold={automaticPlannedHold:F3},PredictedLandingX={automaticPredictedLandingX:F3}," +
            $"PredictedFlight={automaticPredictedFlightSeconds:F3}," +
            $"Target={target}], " +
            $"Wall[Phase={wallActionPhase},Attempts={wallRecoveryAttempts}/" +
            $"{MaximumWallRecoveriesPerAirborneSequence},ContactLatched={wallRecoveryContactLatched}," +
            $"Contact=({wallRecoveryContactX:F3},{wallRecoveryContactY:F3})," +
            $"SawUpward={wallRecoverySawUpwardMotion},UpwardSteps={wallRecoveryUpwardPhysicsSteps}," +
            $"ImpulseConfirmed={wallRecoveryImpulseConfirmed},LipCrossed={wallRecoveryLipCrossed}," +
            $"RequiredReleaseY={wallRecoveryRequiredReleaseY:F3}," +
            $"CommitRemaining={commitmentRemaining:F3}," +
            $"ReleaseObservedStep={wallReleaseObservedFixedStep}," +
            $"DetachedLastStep={wallDetachedLastFixedStep}," +
            $"DetachedConfirmSteps={wallDetachedConfirmationSteps}," +
            $"TopSequenceCommitted={wallTopLandingSequenceCommitted}," +
            $"ExitContactWatch={wallExitContactWatchActive}," +
            $"ResidualRiseWait={wallResidualRiseWaitActive}," +
            $"ExitFaceContactRequired={wallExitFaceContactRequired}," +
            $"ExitObjectiveCount={wallExitObjectiveCountAtCapture}," +
            $"ExitObjectiveY=[{wallExitObjectiveMinimumY:F3}," +
            $"{wallExitObjectiveMaximumY:F3}]," +
            $"MandatoryFaceSetupActive=" +
            $"{wallMandatoryFaceSetupActive}," +
            $"MandatoryFaceSetupRemainingFixedSteps=" +
            $"{(wallMandatoryFaceSetupActive ? Math.Max(0L, wallMandatoryFaceSetupDeadlineFixedStep - JumpPhysicsFeedback.FixedStepSequence) : 0L)}," +
            $"MandatoryFaceInterceptCommitted=" +
            $"{wallMandatoryFaceInterceptCommitted}," +
            $"MandatoryFacePrediction[TargetX=" +
            $"{wallMandatoryFaceTargetContactX:F3},TopClearY=" +
            $"{wallMandatoryFacePredictedTopClearFeetY:F3},ContactY=" +
            $"{wallMandatoryFacePredictedContactFeetY:F3},ContactVY=" +
            $"{wallMandatoryFacePredictedContactVelocityY:F3},ContactT=" +
            $"{wallMandatoryFacePredictedContactSeconds:F3}]," +
            $"ExitTransferCommitted={wallExitTransferCommitted}," +
            $"LandingFlightCommitted={wallLandingFlightCommitted}," +
            $"ExitTarget={exitTarget},Probe={wallSummary}], " +
            $"Scan[{scanSummary}], Physics[{latestPhysicsSnapshot.Summary}]",
            "Evidence");
    }

    private static string DescribeEvidenceScan(BonusBoardScanResult scan)
    {
        if (!scan.IsValid)
            return $"Valid=False,Reason={scan.Reason}";

        string next = scan.HasNext
            ? DescribeEvidenceSegment(scan.Next)
            : "None";
        string intermediate = scan.HasIntermediate
            ? DescribeEvidenceSegment(scan.Intermediate)
            : "None";
        string alternatives = scan.Alternatives == null || scan.Alternatives.Length == 0
            ? "None"
            : string.Join("|", scan.Alternatives.Select(DescribeEvidenceSegment));
        return $"Valid=True,Reason={scan.Reason},Current={DescribeEvidenceSegment(scan.Current)}," +
               $"Next={next},Intermediate={intermediate}," +
               $"EdgeDistance={scan.DistanceToCurrentEdge:F3},Gap={scan.Gap:F3}," +
               $"DeltaY={scan.HeightDelta:F3},Alternatives={alternatives}";
    }

    private static string DescribeEvidenceSegment(BonusBoardSegment segment) =>
        $"[{segment.Left:F3},{segment.Right:F3}]" +
        $"Safe[{segment.SafeLeft:F3},{segment.SafeRight:F3}]" +
        $"@{segment.Top:F3}:C{segment.ColliderInstanceId}:{segment.ColliderName}:" +
        $"P{segment.MapPieceName}#{segment.MapPieceInstanceId}/" +
        $"G{segment.RegistryGeneration}/S{segment.StaticSurfaceIndex}";

    private void LogMovementEvents(BonusStageState state)
    {
        if (!state.HasPlayer)
            return;

        bool observedMouseInput = Input.GetMouseButton(0);
        bool observedJumpInput = Input.GetKey(KeyCode.Space) || observedMouseInput;
        if (observedJumpInput)
            lastManualInputTime = Time.unscaledTime;

        if (observedJumpInput != previousObservedJumpInput)
        {
            if (observedJumpInput)
            {
                observedJumpInputStartedAt = Time.unscaledTime;
                if (jumpController.IsHoldingJump)
                {
                    jumpController.Release();
                    BonusRunnerLog.Debug(
                        "Physical jump input overrode the active automatic hold.",
                        "Input");
                }
                if (learningSampleActive && learningSource == "Automatic")
                    FinishLearningSample(state, "ManualOverride");
                ResetAutomaticControlState();
                ObserveManualWallJumpDown(state);
                if (state.IsGrounded && !learningSampleActive)
                    StartLearningSample(
                        state,
                        "Manual",
                        IsManualGroundLearningEligible(state));
                if (state.IsGrounded)
                    LogManualJumpContext(state);
                BonusRunnerLog.Debug(
                    $"Observed jump input DOWN: Frame={Time.frameCount}, X={state.PlayerPosition.x:F3}, Y={state.PlayerPosition.y:F3}, " +
                    $"VX={state.PlayerVelocity.x:F3}, VY={state.PlayerVelocity.y:F3}, Grounded={state.IsGrounded}",
                    "Input");
            }
            else
            {
                if (learningSampleActive && learningSource == "Manual" &&
                    !learningInputReleased)
                {
                    learningInputUpTime = Time.unscaledTime;
                    learningInputReleased = true;
                }
                ObserveManualWallJumpUp(state);
                BonusRunnerLog.Debug(
                    $"Observed jump input UP: Frame={Time.frameCount}, Held={Time.unscaledTime - observedJumpInputStartedAt:F3}s, " +
                    $"X={state.PlayerPosition.x:F3}, Y={state.PlayerPosition.y:F3}, VX={state.PlayerVelocity.x:F3}, VY={state.PlayerVelocity.y:F3}",
                    "Input");
            }
            previousObservedJumpInput = observedJumpInput;
        }

        if (observedMouseInput != previousObservedMouseInput)
        {
            if (observedMouseInput && !automationEnabled)
            {
                BeginManualDemonstration(state);
                manualDemonstrationMousePresses++;
                LogManualDemonstrationEvidence(
                    state,
                    "MouseDown",
                    force: true);
            }
            else if (!observedMouseInput && manualDemonstrationActive)
            {
                LogManualDemonstrationEvidence(
                    state,
                    "MouseUp",
                    force: true);
            }
            previousObservedMouseInput = observedMouseInput;
        }

        if (manualDemonstrationActive)
        {
            PlayerMovement manualPlayer = PlayerMovement.instance;
            bool lowDescending =
                !state.IsGrounded &&
                state.PlayerPosition.y < PitDescentYThreshold &&
                state.PlayerVelocity.y <= PitDescentVelocityThreshold;
            bool recoverableWall = HasRecoverableWallAhead(
                state,
                manualPlayer,
                requirePlannedTarget: false,
                out BonusWallContact manualWall,
                out string manualWallEvidence);
            bool manualPitCandidate =
                lowDescending &&
                !observedMouseInput &&
                !recoverableWall;
            bool confirmedManualPit = UpdateManualPitConfirmation(
                manualPitCandidate);

            if (lowDescending && recoverableWall &&
                Time.unscaledTime >= nextManualPitEvidenceTime)
            {
                nextManualPitEvidenceTime = Time.unscaledTime + 0.25f;
                BonusRunnerLog.Debug(
                    $"ManualPitThresholdSuppressed Position=" +
                    $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                    $"Velocity=({state.PlayerVelocity.x:F3}," +
                    $"{state.PlayerVelocity.y:F3}), Wall=" +
                    $"{manualWallEvidence}, FaceX={manualWall.FaceX:F3}, " +
                    $"Distance={manualWall.Distance:F3}, Touching=" +
                    $"{manualWall.IsTouching}. The low position is a " +
                    "recoverable authored trench, not a demonstrated death.",
                    "Demonstration");
            }

            if (confirmedManualPit && !manualDemonstrationPitLogged)
            {
                manualDemonstrationPitLogged = true;
                LogManualDemonstrationEvidence(
                    state,
                    "PitDescent",
                    force: true);
                if (learningSampleActive && learningSource == "Manual")
                    FinishLearningSample(state, "ManualPitDescent");
                EndManualDemonstration(state, "PitDescent");
            }
            else
            {
                LogManualDemonstrationEvidence(
                    state,
                    observedMouseInput ? "MouseHeld" : "Trajectory",
                    force: false);
            }
        }

        if (!movementInitialized)
        {
            movementInitialized = true;
            previousMovementPosition = state.PlayerPosition;
            previousMovementObservedAt = Time.unscaledTime;
            previousGrounded = state.IsGrounded;
            previousVelocityY = state.PlayerVelocity.y;
            return;
        }

        float movementDeltaSeconds = Mathf.Max(
            0.001f,
            Time.unscaledTime - previousMovementObservedAt);
        Vector3 movementDelta =
            state.PlayerPosition - previousMovementPosition;
        float expectedHorizontalDelta = Mathf.Max(
                Mathf.Abs(state.PlayerVelocity.x),
                lastReliableHorizontalSpeed) *
            movementDeltaSeconds + 1.50f;
        float expectedVerticalDelta = Mathf.Max(
                Mathf.Abs(state.PlayerVelocity.y),
                Mathf.Abs(previousVelocityY)) *
            movementDeltaSeconds + 0.75f;
        bool globalPositionDiscontinuity =
            automationEnabled &&
            bonusGameplayStarted &&
            (Mathf.Abs(movementDelta.x) > expectedHorizontalDelta ||
             Mathf.Abs(movementDelta.y) > Mathf.Max(1.25f, expectedVerticalDelta));
        previousMovementPosition = state.PlayerPosition;
        previousMovementObservedAt = Time.unscaledTime;
        if (globalPositionDiscontinuity)
        {
            BonusRunnerLog.Warning(
                $"GlobalPositionDiscontinuity Delta=" +
                $"({movementDelta.x:F3},{movementDelta.y:F3}), " +
                $"Dt={movementDeltaSeconds:F3}s, Position=" +
                $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"Velocity=({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), LearningActive=" +
                $"{learningSampleActive}. FailureDomain=Lifecycle; all " +
                "automatic input is blocked until a stable respawn.");
            jumpController.Release();
            if (learningSampleActive && learningSource == "Automatic")
                FinishLearningSample(state, "PositionDiscontinuity");
            ResetAutomaticControlState();
            pitDescentGuardActive = true;
            pitRespawnLastFixedStep = -1;
            pitRespawnStableFixedSteps = 0;
            nextAutomaticAttemptTime = Time.unscaledTime + 0.45f;
            previousGrounded = state.IsGrounded;
            previousVelocityY = state.PlayerVelocity.y;
            return;
        }

        if (previousGrounded && !state.IsGrounded)
        {
            BonusRunnerLog.Debug(
                $"Takeoff: Frame={Time.frameCount}, Time={Time.time:F3}, X={state.PlayerPosition.x:F3}, Y={state.PlayerPosition.y:F3}, " +
                $"VX={state.PlayerVelocity.x:F3}, VY={state.PlayerVelocity.y:F3}, InputHeld={observedJumpInput}",
                "Movement");
        }
        else if (!previousGrounded && state.IsGrounded)
        {
            BonusRunnerLog.Debug(
                $"Landing: Frame={Time.frameCount}, Time={Time.time:F3}, X={state.PlayerPosition.x:F3}, Y={state.PlayerPosition.y:F3}, " +
                $"VX={state.PlayerVelocity.x:F3}, VY={state.PlayerVelocity.y:F3}",
                "Movement");
            LogManualDemonstrationEvidence(
                state,
                "Landing",
                force: true);
        }

        if (previousVelocityY > 0f && state.PlayerVelocity.y <= 0f && !state.IsGrounded)
        {
            BonusRunnerLog.Debug(
                $"Apex: Frame={Time.frameCount}, Time={Time.time:F3}, X={state.PlayerPosition.x:F3}, Y={state.PlayerPosition.y:F3}, " +
                $"VX={state.PlayerVelocity.x:F3}, VY={state.PlayerVelocity.y:F3}",
                "Movement");
            LogManualDemonstrationEvidence(
                state,
                "Apex",
                force: true);
        }

        previousGrounded = state.IsGrounded;
        previousVelocityY = state.PlayerVelocity.y;
    }

    private void LogManualJumpContext(BonusStageState state)
    {
        PlayerMovement player = PlayerMovement.instance;
        if (player == null)
            return;

        float observedHorizontalSpeed = Mathf.Abs(state.PlayerVelocity.x);
        bool observedPlanningSpeedUsable =
            observedHorizontalSpeed > 1f && observedHorizontalSpeed < 80f;
        float planningHorizontalSpeed = observedPlanningSpeedUsable
            ? observedHorizontalSpeed
            : Mathf.Max(1f, lastReliableHorizontalSpeed);
        string planningSpeedSource = observedPlanningSpeedUsable
            ? "LiveRigidbody"
            : "ReliableRunFallback";

        BonusBoardScanResult scan = platformScanner.Scan(
            state.PlayerPosition,
            player,
            planningHorizontalSpeed);
        JumpPhysicsSnapshot observedPhysics =
            jumpPhysicsFeedback.CaptureSnapshot(player);
        latestPhysicsSnapshot = observedPhysics;
        JumpPhysicsSnapshot physics = BuildPlanningPhysics(
            state.SectionIndex,
            observedPhysics,
            planningHorizontalSpeed,
            state.IsActiveGameplay && !state.SpiritBoostEnabled);
        BonusJumpPlan plan = jumpPlanner.Plan(
            scan,
            state.PlayerPosition,
            new Vector2(planningHorizontalSpeed, state.PlayerVelocity.y),
            physics,
            hazardScanner.FindNearest(state.PlayerPosition));

        if (!scan.IsValid)
        {
            BonusRunnerLog.Debug(
                $"ManualJumpContext AttemptId={learningSampleId}, X={state.PlayerPosition.x:F3}, " +
                $"AutomationEnabled={automationEnabled}, Scan={scan.Reason}",
                "Learning");
            return;
        }

        string next = scan.HasNext
            ? $"Next=[{scan.Next.Left:F3},{scan.Next.Right:F3}] " +
              $"Safe=[{scan.Next.SafeLeft:F3},{scan.Next.SafeRight:F3}]@{scan.Next.Top:F3}"
            : "Next=Unavailable";
        string intermediate = scan.HasIntermediate
            ? $", Intermediate=[{scan.Intermediate.Left:F3}," +
              $"{scan.Intermediate.Right:F3}] " +
              $"Safe=[{scan.Intermediate.SafeLeft:F3}," +
              $"{scan.Intermediate.SafeRight:F3}]@{scan.Intermediate.Top:F3}"
            : string.Empty;
        BonusRunnerLog.Debug(
            $"ManualJumpContext AttemptId={learningSampleId}, " +
            $"X={state.PlayerPosition.x:F3}, VX={lastReliableHorizontalSpeed:F3}, " +
            $"AutomationEnabled={automationEnabled}, " +
            $"Current=[{scan.Current.Left:F3},{scan.Current.Right:F3}] " +
            $"Safe=[{scan.Current.SafeLeft:F3},{scan.Current.SafeRight:F3}]@{scan.Current.Top:F3}, " +
            $"{next}{intermediate}, Gap={scan.Gap:F3}, " +
            $"DeltaY={scan.HeightDelta:F3}, ScanReason={scan.Reason}, " +
            $"SuggestedHold={plan.HoldSeconds:F3}s, PlannedLaunch={plan.PlannedLaunchX:F3}, " +
            $"Reason={plan.Reason}; Physics[{physics.Summary}]; " +
            $"Candidates: {plan.CandidateSummary}",
            "Learning");
    }

    private void BeginManualDemonstration(BonusStageState state)
    {
        if (manualDemonstrationActive || automationEnabled ||
            !state.IsBonusStage)
        {
            return;
        }

        manualDemonstrationActive = true;
        manualDemonstrationId = ++nextManualDemonstrationId;
        manualDemonstrationMousePresses = 0;
        manualDemonstrationStartedAt = Time.unscaledTime;
        manualDemonstrationStartPosition = state.PlayerPosition;
        manualDemonstrationStartSection = state.SectionIndex;
        manualDemonstrationStartSpheres =
            BonusStageInspector.TryGetBonusSphereCount(out int sphereCount)
                ? sphereCount
                : -1;
        manualDemonstrationPitLogged = false;
        ResetManualPitConfirmation();
        nextManualDemonstrationFrameTime = 0f;
        lastManualDemonstrationSignature = string.Empty;
        BonusRunnerLog.Debug(
            $"ManualDemonstrationStart SessionId=" +
            $"{manualDemonstrationId}, Map={state.MapName}, " +
            $"Section={state.SectionIndex}, Position=" +
            $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Velocity=({state.PlayerVelocity.x:F3}," +
            $"{state.PlayerVelocity.y:F3}), Spheres=" +
            $"{manualDemonstrationStartSpheres}, " +
            "AutomationEnabled=False. Map perception and shadow planning " +
            "remain active, but no automatic input will be sent.",
            "Demonstration");
    }

    private void EndManualDemonstration(
        BonusStageState state,
        string outcome)
    {
        if (!manualDemonstrationActive)
            return;

        LogManualDemonstrationEvidence(
            state,
            $"SessionEnd:{outcome}",
            force: true);
        int endSpheres =
            BonusStageInspector.TryGetBonusSphereCount(out int sphereCount)
                ? sphereCount
                : -1;
        string sphereDelta =
            manualDemonstrationStartSpheres >= 0 && endSpheres >= 0
                ? $"{manualDemonstrationStartSpheres}->{endSpheres} " +
                  $"Delta={endSpheres - manualDemonstrationStartSpheres}"
                : $"Unavailable(Start={manualDemonstrationStartSpheres}," +
                  $"End={endSpheres})";
        BonusRunnerLog.Debug(
            $"ManualDemonstrationEnd SessionId=" +
            $"{manualDemonstrationId}, Outcome={outcome}, " +
            $"Duration={Mathf.Max(0f, Time.unscaledTime - manualDemonstrationStartedAt):F3}s, " +
            $"StartSection={manualDemonstrationStartSection}, " +
            $"EndSection={state.SectionIndex}, MousePresses=" +
            $"{manualDemonstrationMousePresses}, Start=" +
            $"({manualDemonstrationStartPosition.x:F3}," +
            $"{manualDemonstrationStartPosition.y:F3}), End=" +
            $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"SphereProgress={sphereDelta}.",
            "Demonstration");
        manualDemonstrationActive = false;
        manualDemonstrationId = 0;
        manualDemonstrationMousePresses = 0;
        manualDemonstrationPitLogged = false;
        ResetManualPitConfirmation();
        nextManualDemonstrationFrameTime = 0f;
        lastManualDemonstrationSignature = string.Empty;
    }

    private void LogManualDemonstrationEvidence(
        BonusStageState state,
        string eventName,
        bool force)
    {
        if (!manualDemonstrationActive || !BonusRunnerLog.IsDebugMode ||
            !state.IsBonusStage || !state.HasPlayer)
        {
            return;
        }

        PlayerMovement player = PlayerMovement.instance;
        if (player == null)
            return;

        BonusWallContact wall = wallDetector.Detect(
            player,
            lastReliableHorizontalSpeed);
        string signature =
            $"{eventName}:{state.SectionIndex}:{state.IsGrounded}:" +
            $"{Input.GetMouseButton(0)}:{wall.IsDetected}:" +
            $"{wall.IsTouching}:{state.CharacterFellOff}";
        bool eventChanged = !string.Equals(
            signature,
            lastManualDemonstrationSignature,
            StringComparison.Ordinal);
        float interval = state.IsGrounded && !Input.GetMouseButton(0)
            ? 0.20f
            : 0.08f;
        if (!force && !eventChanged &&
            Time.unscaledTime < nextManualDemonstrationFrameTime)
        {
            return;
        }

        lastManualDemonstrationSignature = signature;
        nextManualDemonstrationFrameTime = Time.unscaledTime + interval;

        BonusBoardScanResult scan = default;
        BonusHazard hazard = default;
        BonusJumpPlan shadowPlan = default;
        string scanSummary;
        string shadowSummary;
        try
        {
            scan = platformScanner.Scan(
                state.PlayerPosition,
                player,
                Mathf.Max(
                    Mathf.Abs(state.PlayerVelocity.x),
                    lastReliableHorizontalSpeed));
            scanSummary = DescribeEvidenceScan(scan);
            hazard = hazardScanner.FindNearest(state.PlayerPosition);
            if (scan.IsValid)
            {
                JumpPhysicsSnapshot physics =
                    jumpPhysicsFeedback.CaptureSnapshot(player);
                latestPhysicsSnapshot = physics;
                shadowPlan = jumpPlanner.Plan(
                    scan,
                    state.PlayerPosition,
                    state.PlayerVelocity,
                    physics,
                    hazard);
                shadowSummary =
                    $"Valid={shadowPlan.IsValid}," +
                    $"ShouldJumpNow={shadowPlan.ShouldJumpNow}," +
                    $"Maneuver={shadowPlan.Maneuver}," +
                    $"Reason={shadowPlan.Reason}," +
                    $"Hold={shadowPlan.HoldSeconds:F3}," +
                    $"Launch={shadowPlan.PlannedLaunchX:F3}," +
                    $"Window=[{shadowPlan.LaunchWindowLeft:F3}," +
                    $"{shadowPlan.LaunchWindowRight:F3}]," +
                    $"PredictedLandingX=" +
                    $"{shadowPlan.PredictedLandingX:F3}," +
                    $"PredictedFlight=" +
                    $"{shadowPlan.PredictedFlightSeconds:F3}" +
                    (force
                        ? $",Candidates={shadowPlan.CandidateSummary}"
                        : string.Empty);
            }
            else
            {
                shadowSummary = "Unavailable:InvalidScan";
            }
        }
        catch (System.Exception exception)
        {
            scanSummary =
                $"ScanFailed:{exception.GetType().Name}:" +
                $"{exception.Message}";
            shadowSummary = "Unavailable:Exception";
        }

        string hazardSummary = hazard.IsValid
            ? $"[{hazard.Left:F3},{hazard.Right:F3}]" +
              $"@{hazard.Top:F3}:" +
              $"{hazard.InstanceId}:{hazard.Name}:" +
              $"{hazard.ComponentPath}"
            : "None";
        string wallSummary = wall.IsDetected
            ? $"Detected=True,Touching={wall.IsTouching}," +
              $"Distance={wall.Distance:F3},BodyGap=" +
              $"{wall.BodyGap:F3},FaceX={wall.FaceX:F3}," +
              $"Point=({wall.Point.x:F3},{wall.Point.y:F3})," +
              $"Normal=({wall.Normal.x:F3},{wall.Normal.y:F3})," +
              $"Collider={wall.ColliderInstanceId}:" +
              $"{wall.ColliderName},Reason={wall.Reason}"
            : $"Detected=False,Reason={wall.Reason}";
        Collider2D collider = player.playerCollider;
        string bounds = collider != null
            ? $"Min=({collider.bounds.min.x:F3}," +
              $"{collider.bounds.min.y:F3}),Max=" +
              $"({collider.bounds.max.x:F3}," +
              $"{collider.bounds.max.y:F3}),Extents=" +
              $"({collider.bounds.extents.x:F3}," +
              $"{collider.bounds.extents.y:F3})"
            : "Unavailable";

        BonusRunnerLog.Debug(
            $"ManualDemonstrationFrame SessionId=" +
            $"{manualDemonstrationId}, Event={eventName}, " +
            $"MousePress={manualDemonstrationMousePresses}, " +
            $"Elapsed={Time.unscaledTime - manualDemonstrationStartedAt:F3}s, " +
            $"Frame={Time.frameCount}, FixedStep=" +
            $"{JumpPhysicsFeedback.FixedStepSequence}, " +
            $"Map={state.MapName}, Section={state.SectionIndex}, " +
            $"GameState={state.GameStateName}, " +
            $"SpiritBoost={state.SpiritBoostEnabled}, " +
            $"Timer={state.CurrentTime:F3}/{state.MaximumTime:F3}, " +
            $"Spheres={(state.HasSphereProgress ? $"{state.CollectedSpheres}/{Math.Ceiling(state.RequiredSpheres):F0}" : "Unavailable")}, " +
            $"FellOff={state.CharacterFellOff}, Position=" +
            $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Velocity=({state.PlayerVelocity.x:F3}," +
            $"{state.PlayerVelocity.y:F3}), Grounded={state.IsGrounded}, " +
            $"MouseHeld={Input.GetMouseButton(0)}, " +
            $"PlayerJump[IsJumping={player.isJumping}," +
            $"JumpTime={player.jumpTime:F3},Counter=" +
            $"{player.jumpTimeCounter:F3},Force={player.jumpForce:F3}], " +
            $"JumpPanel[Down={JumpPanel.jumpDown}," +
            $"Pressed={JumpPanel.jumpPressed},Up={JumpPanel.jumpUp}," +
            $"HeldPointers={(JumpPanel.instance != null ? JumpPanel.instance.heldPointerCount : -1)}], " +
            $"PlayerBounds[{bounds}], Wall[{wallSummary}], " +
            $"Hazard[{hazardSummary}], Scan[{scanSummary}], " +
            $"ShadowPlan[{shadowSummary}], " +
            $"ManualLearning[Active={learningSampleActive}," +
            $"AttemptId={(learningSampleActive && learningSource == "Manual" ? learningSampleId : 0)}," +
            $"WallSequenceId={manualWallSequenceId}," +
            $"WallPulse={manualWallJumpCount}], " +
            $"Physics[{latestPhysicsSnapshot.Summary}]",
            "Demonstration");
    }

    private void UpdateAutomaticJump(BonusStageState state)
    {
        PlayerMovement player = PlayerMovement.instance;
        bool successfulCompletionTraversal =
            IsSuccessfulCompletionTraversal(state);
        bool automaticJumpingEnabled =
            Plugin.Config?.AutomaticJumping?.Value == true;
        if (player != null &&
            jumpController.IsHoldingJump &&
            automaticPredictionActive &&
            learningSampleActive &&
            learningSource == "Automatic" &&
            state.HasPlayer &&
            (state.IsActiveGameplay ||
             successfulCompletionTraversal))
        {
            MonitorCommittedJump(state, player);
        }
        jumpController.Update(player);
        completionRewardController.ObserveTraversal(
            automaticJumpingEnabled && successfulCompletionTraversal,
            state);
        if (!successfulCompletionTraversal)
            ResetCompletionTerminalCandidate();

        if (!automaticJumpingEnabled ||
            !state.HasPlayer || player == null ||
            (!state.IsActiveGameplay &&
              !successfulCompletionTraversal) ||
            !state.IsSupportedBonusMap)
        {
            bool completingCommittedTrajectory =
                Plugin.Config?.AutomaticJumping?.Value == true &&
                state.HasPlayer && player != null &&
                state.IsSupportedBonusMap &&
                learningSampleActive &&
                learningSource == "Automatic" &&
                learningTookOff &&
                automaticPredictionActive &&
                !state.IsGrounded;
            if (completingCommittedTrajectory)
            {
                if (!automaticControlSuspended)
                {
                    automaticControlSuspended = true;
                    BonusRunnerLog.Debug(
                        $"CommittedJumpCarryThrough AttemptId={automaticAttemptId}, " +
                        $"GameState={state.GameStateName}, " +
                        $"Elapsed={Time.unscaledTime - learningInputDownTime:F3}s, " +
                        $"PlannedHold={automaticPlannedHold:F3}s, " +
                        $"ControllerHolding={jumpController.IsHoldingJump}. " +
                        "No new route will be started, but the accepted jump will not be truncated.",
                        "Control");
                }
                return;
            }

            if (!automaticControlSuspended)
            {
                automaticControlSuspended = true;
                BonusRunnerLog.Debug(
                    $"AutomaticControlSuspended GameState={state.GameStateName}, " +
                    $"Map={state.MapName}, HasPlayer={state.HasPlayer}. " +
                    "All jump input has been released; death/transition movement is game-controlled.",
                    "Control");
            }
            jumpController.Release();
            ResetAutomaticControlState();
            return;
        }
        if (automaticControlSuspended)
        {
            automaticControlSuspended = false;
            BonusRunnerLog.Debug(
                $"AutomaticControlResumed GameState={state.GameStateName}, " +
                $"Map={state.MapName}, Section={state.SectionIndex}.",
                "Control");
        }

        if (successfulCompletionTraversal)
        {
            bool dashControlIdle = IsCompletionDashControlIdle();
            if (!dashControlIdle &&
                Plugin.Config?.CompletionWindDash?.Value == true &&
                BonusRunnerLog.IsDebugMode &&
                Time.unscaledTime >= nextCompletionDashDeferralLogTime)
            {
                nextCompletionDashDeferralLogTime =
                    Time.unscaledTime + 0.25f;
                BonusRunnerLog.Debug(
                    $"CompletionWindDashDeferred Reason=OwnedTrajectory, " +
                    $"Holding={jumpController.IsHoldingJump}, " +
                    $"AutomaticArmed={automaticJumpArmed}, Prediction=" +
                    $"{automaticPredictionActive}, Learning=" +
                    $"{learningSampleActive}/{learningSource}, PassiveWall=" +
                    $"{passiveWallApproachActive}, WallPhase=" +
                    $"{wallActionPhase}, ExitWatch=" +
                    $"{wallExitContactWatchActive}. The existing route keeps " +
                    "input ownership until it finishes.",
                    "Completion");
            }
            bool windDashActivated = dashControlIdle &&
                completionRewardController.TickGroundWindDash(
                    state,
                    player,
                    Plugin.Config?.CompletionWindDash?.Value == true);
            if (windDashActivated)
            {
                completionDashPlanningResumeFixedStep = Math.Max(
                    completionDashPlanningResumeFixedStep,
                    JumpPhysicsFeedback.FixedStepSequence + 2);
                ClearRoutePlanLock();
                BonusRunnerLog.Debug(
                    $"CompletionWindDashPlanningBarrier ActivatedAtFixedStep=" +
                    $"{JumpPhysicsFeedback.FixedStepSequence}, ResumeAt=" +
                    $"{completionDashPlanningResumeFixedStep}, " +
                    $"CapturedVX={Mathf.Abs(state.PlayerVelocity.x):F3}. " +
                    "Route input waits for two physics steps so the dash's " +
                    "actual Rigidbody velocity is observed before replanning.",
                    "Completion");
            }

            if (JumpPhysicsFeedback.FixedStepSequence <
                completionDashPlanningResumeFixedStep)
            {
                // Activation is allowed only while no route owns input.  Do
                // not issue an unconditional UP here: if state changes during
                // the barrier, an already committed trajectory still owns its
                // press/release deadline.
                return;
            }
        }

        // characterFellOff can remain true for the rest of a protected run,
        // and authored trenches can legitimately descend below Y=-5. A low
        // Y value is therefore not a death state by itself. Confirm sustained
        // downward motion only when no planned, reachable wall face exists.
        // This preserves a live trench/wall route while still closing the
        // protector-teleport window after a real fall.
        bool lowDescending =
            !state.IsGrounded &&
            state.PlayerPosition.y < PitDescentYThreshold &&
            state.PlayerVelocity.y <= PitDescentVelocityThreshold;
        bool recoverableWall = HasRecoverableWallAhead(
            state,
            player,
            requirePlannedTarget: true,
            out BonusWallContact pitWall,
            out string pitWallEvidence);
        bool confirmedPitDescent = UpdateAutomaticPitConfirmation(
            lowDescending && !recoverableWall);
        if (lowDescending && recoverableWall &&
            Time.unscaledTime >= nextPitDescentEvidenceTime)
        {
            nextPitDescentEvidenceTime = Time.unscaledTime + 0.25f;
            BonusRunnerLog.Debug(
                $"PitThresholdSuppressed Position=" +
                $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"Velocity=({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), Wall={pitWallEvidence}, " +
                $"FaceX={pitWall.FaceX:F3}, Distance={pitWall.Distance:F3}, " +
                $"Touching={pitWall.IsTouching}, Maneuver=" +
                $"{automaticManeuver}, Phase={wallActionPhase}. " +
                "The planned wall route retains input authority.",
                "Lifecycle");
        }
        if (lowDescending && !recoverableWall && !confirmedPitDescent)
        {
            // Do not start a fresh route during the short confirmation
            // interval. Keep the existing wall target intact in case the
            // next physics step establishes valid face contact.
            jumpController.Release();
            return;
        }
        if (confirmedPitDescent)
        {
            if (!pitDescentGuardActive)
            {
                pitDescentGuardActive = true;
                pitRespawnLastFixedStep = -1;
                pitRespawnStableFixedSteps = 0;
                BonusRunnerLog.Warning(
                    $"PitDescentDetected Position=({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), Velocity=" +
                    $"({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                    $"AttemptId={automaticAttemptId}, ConfirmedFixedSteps=" +
                    $"{pitDescentCandidateFixedSteps}, WallEvidence=" +
                    $"{pitWallEvidence}. FailureDomain=Lifecycle; " +
                    "all input is released until a stable respawn landing.");
            }
            jumpController.Release();
            if (learningSampleActive && learningSource == "Automatic")
                FinishLearningSample(state, "PitDescentDetected");
            ResetAutomaticControlState();
            nextAutomaticAttemptTime = Time.unscaledTime + 0.45f;
            return;
        }
        if (pitDescentGuardActive)
        {
            bool stableRespawnFrame =
                state.IsGrounded &&
                Mathf.Abs(state.PlayerVelocity.y) <= 2.50f;
            if (!stableRespawnFrame)
            {
                pitRespawnLastFixedStep = -1;
                pitRespawnStableFixedSteps = 0;
                jumpController.Release();
                return;
            }

            long fixedStep = JumpPhysicsFeedback.FixedStepSequence;
            if (fixedStep != pitRespawnLastFixedStep)
            {
                pitRespawnLastFixedStep = fixedStep;
                pitRespawnStableFixedSteps++;
            }
            if (pitRespawnStableFixedSteps < 2 ||
                Time.unscaledTime < nextAutomaticAttemptTime)
            {
                jumpController.Release();
                return;
            }

            pitDescentGuardActive = false;
            ResetAutomaticPitConfirmation();
            BonusRunnerLog.Debug(
                $"PitDescentGuardReleased Position=({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Velocity=" +
                $"({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                $"StableFixedSteps={pitRespawnStableFixedSteps}. " +
                "A stable grounded respawn was observed; route planning resumes.",
                "Lifecycle");
            pitRespawnLastFixedStep = -1;
            pitRespawnStableFixedSteps = 0;
        }

        if (TryWallRecoveryJump(state, player))
            return;

        // An unresolved downstream-face watch or objective descent owns the
        // frame even when a render-frame ray briefly misses. Letting grounded
        // routing continue here can erase the watch/speed latch immediately
        // before the next physics step confirms the wall contact.
        if (wallExitContactWatchActive ||
            attachedObjectiveDescentActive ||
            wallMandatoryFaceSetupActive ||
            wallMandatoryFaceInterceptCommitted)
        {
            if (attachedObjectiveDescentActive)
                jumpController.Release();
            return;
        }

        // Unity reports a short Grounded pulse while the player is still
        // sliding/climbing against the vertical face. It is not a platform
        // landing. Keep the wall sequence alive until UpdateLearningSample
        // verifies the target top or the independent climb deadline expires.
        if (IsCommittedWallClimbActive() && state.IsGrounded)
        {
            if (BonusRunnerLog.IsDebugMode &&
                Time.unscaledTime >= nextWallProbeLogTime)
            {
                nextWallProbeLogTime = Time.unscaledTime + 0.10f;
                BonusRunnerLog.Debug(
                    $"WallClimbGroundPulseIgnored AttemptId={automaticAttemptId}, " +
                    $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                    $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                    $"ControllerHolding={jumpController.IsHoldingJump}, " +
                    $"Attempts={wallRecoveryAttempts}/{MaximumWallRecoveriesPerAirborneSequence}, " +
                    $"CommitRemaining={wallRecoveryCommitmentUntil - Time.unscaledTime:F3}s. " +
                    "Normal landing/reset logic is intentionally bypassed.",
                    "Recovery");
            }
            return;
        }

        if (!state.IsGrounded)
        {
            if (!automaticJumpArmed)
                airborneAfterAutomaticJump = true;
            MonitorAutomaticTrajectory(state);
            RefreshSecondStagePreview(state, player);
            return;
        }

        // UpdateLearningSample owns the grounded action until it has observed
        // two stable physics steps on one support. Without this gate,
        // LateUpdate could arm a new jump during the one-frame confirmation
        // window and interrupt the sample that was introduced specifically to
        // reject wall-contact Grounded pulses.
        if (learningSampleActive)
            return;

        if (!passiveWallApproachActive &&
            !jumpController.IsHoldingJump)
        {
            ResetWallRecoveryAfterLanding();
        }

        if (airborneAfterAutomaticJump)
        {
            automaticJumpArmed = true;
            airborneAfterAutomaticJump = false;
        }

        BonusMapController controller = BonusMapController.instance;
        if (controller == null ||
            !automaticJumpArmed ||
            jumpController.IsHoldingJump ||
            Time.unscaledTime < nextAutomaticAttemptTime)
            return;

        float observedHorizontalSpeed = Mathf.Abs(state.PlayerVelocity.x);
        bool observedPlanningSpeedUsable =
            observedHorizontalSpeed > 1f && observedHorizontalSpeed < 80f;
        float planningHorizontalSpeed = observedPlanningSpeedUsable
            ? observedHorizontalSpeed
            : Mathf.Max(1f, lastReliableHorizontalSpeed);
        string planningSpeedSource = observedPlanningSpeedUsable
            ? "LiveRigidbody"
            : "ReliableRunFallback";

        BonusBoardScanResult scan = platformScanner.Scan(
            state.PlayerPosition,
            player,
            planningHorizontalSpeed);
        scan = BonusJumpPlanner.SelectLowerRouteWhenItContinues(scan);
        BonusHazard hazard = hazardScanner.FindNearest(state.PlayerPosition);
        JumpPhysicsSnapshot observedPhysics =
            jumpPhysicsFeedback.CaptureSnapshot(player);
        latestPhysicsSnapshot = observedPhysics;
        JumpPhysicsSnapshot physics = BuildPlanningPhysics(
            state.SectionIndex,
            observedPhysics,
            planningHorizontalSpeed,
            state.IsActiveGameplay && !state.SpiritBoostEnabled);
        Vector2 planningVelocity = new(
            planningHorizontalSpeed,
            state.PlayerVelocity.y);
        bool speedAdaptiveRoutingRequired =
            state.SpiritBoostEnabled ||
            planningHorizontalSpeed > physics.BaseHorizontalSpeed + 1.0f;
        if (speedAdaptiveRoutingRequired)
        {
            scan = jumpPlanner.SelectBoostReachableRoute(
                scan,
                state.PlayerPosition,
                planningVelocity,
                physics,
                hazard,
                out string boostSelection);
            if (boostSelection != lastBoostRouteSelection)
            {
                lastBoostRouteSelection = boostSelection;
                BonusRunnerLog.Debug(
                    $"SpeedAdaptiveRoute X={state.PlayerPosition.x:F3}, " +
                    $"Speed={planningHorizontalSpeed:F3}, " +
                    $"SpeedSource={planningSpeedSource}, " +
                    $"SpiritBoost={state.SpiritBoostEnabled}, " +
                    $"Selection={boostSelection}, ScanReason={scan.Reason}.",
                    "Routing");
            }
        }
        else
        {
            lastBoostRouteSelection = string.Empty;
        }
        ObserveNoSupportStall(state, scan);
        ConfirmOrRejectSecondStagePreview(state, scan);
        float sphereLookAhead = Mathf.Clamp(
            30f + Mathf.Max(0f, planningHorizontalSpeed - 18f) * 1.4f,
            30f,
            80f);
        Vector2[] routeSphereObjectives =
            state.HasSphereProgress && state.RemainingRequiredSpheres > 0
                ? BonusStageInspector.GetActiveSpherePositions(
                    state.PlayerPosition.x - 1.0f,
                    state.PlayerPosition.x + sphereLookAhead)
                : Array.Empty<Vector2>();
        BonusJumpPlan plan = GetStableRoutePlan(
            state,
            scan,
            planningVelocity,
            physics,
            hazard,
            routeSphereObjectives);
        bool usingLockedTarget =
            routePlanLocked &&
            (plan.Reason == "LockedLaunchWindow" ||
             plan.Reason == "LockedLateLandingRecovery" ||
             plan.Reason == "LockedApproach");
        BonusBoardSegment plannedTarget =
            usingLockedTarget
                ? lockedRouteTarget
                : plan.Maneuver == BonusManeuverKind.SphereCollectionJump
                    ? scan.Current
                    : scan.Next;

        // A spike is allowed to create its own jump only when it is on the
        // current continuous support. A later spike must never replace the
        // route to the immediate next platform.
        bool sameSurfaceHazard = hazard.IsValid && scan.IsValid &&
            hazard.Left >= scan.Current.Left &&
            hazard.Right <= scan.Current.Right;
        if (!plan.IsValid && sameSurfaceHazard)
        {
            ClearRoutePlanLock();
            plan = jumpPlanner.PlanSameSurfaceHazard(
                scan, hazard, state.PlayerPosition, planningVelocity, physics);
            plannedTarget = scan.Current;
        }

        if (hazard.IsValid)
        {
            string hazardSignature =
                $"{hazard.InstanceId}:{plan.Reason}:{plan.PlannedLaunchX:F2}";
            if (hazardSignature != lastHazardSignature ||
                Time.unscaledTime >= nextHazardLogTime)
            {
                lastHazardSignature = hazardSignature;
                nextHazardLogTime = Time.unscaledTime + 0.25f;
                BonusRunnerLog.Debug(
                    $"HazardProbe X={state.PlayerPosition.x:F3}, " +
                    $"Hazard=[{hazard.Left:F3},{hazard.Right:F3}] Top={hazard.Top:F3}, " +
                    $"OnCurrentSurface={sameSurfaceHazard}, " +
                    $"Collider={hazard.InstanceId}:{hazard.Name}, Path={hazard.ComponentPath}, " +
                    $"Decision={plan.Reason}; {plan.CandidateSummary}",
                    "Hazard");
            }
        }

        if (!plan.IsValid && plan.Reason == "IntentionalDrop" && scan.HasNext)
        {
            float fallSeconds = plan.PredictedFlightSeconds;
            float expectedLandingX = plan.PredictedLandingX;
            secondStageObservedAirborne = false;
            automaticTrajectoryCompatible = true;
            PrepareSecondStagePreview(
                state,
                scan.Next,
                expectedLandingX,
                physics,
                "IntentionalDrop");
            BonusRunnerLog.Debug(
                $"IntentionalDropPlan From=[{scan.Current.Left:F3}," +
                $"{scan.Current.Right:F3}]@{scan.Current.Top:F3}, " +
                $"LandingSupport=[{scan.Next.Left:F3},{scan.Next.Right:F3}]" +
                $"@{scan.Next.Top:F3}, Fall={fallSeconds:F3}s, " +
                $"ExpectedLandingX={expectedLandingX:F3}. No jump input sent. " +
                $"Calculation={plan.CandidateSummary}",
                "Lookahead");
        }
        LogRouteDiagnostics(
            state,
            scan,
            plan,
            planningHorizontalSpeed,
            physics);
        if (successfulCompletionTraversal &&
            Time.unscaledTime >= nextCompletionNavigationLogTime)
        {
            nextCompletionNavigationLogTime = Time.unscaledTime + 0.25f;
            BonusRunnerLog.Debug(
                $"CompletionTraversalNavigation Section=" +
                $"{state.SectionIndex}, Position=" +
                $"({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Velocity=" +
                $"({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), ScanValid=" +
                $"{scan.IsValid}, HasNext={scan.HasNext}, " +
                $"PlanValid={plan.IsValid}, ShouldJump=" +
                $"{plan.ShouldJumpNow}, Maneuver={plan.Maneuver}, " +
                $"Reason={plan.Reason}. Normal dynamic terrain planning " +
                "continues after the sphere quota; reward actions remain " +
                "blocked while a downstream route exists.",
                "Completion");
        }

        bool stationaryBeforeRoute =
            state.IsGrounded && observedHorizontalSpeed <= 1f && scan.HasNext;
        bool speedTransitionBarrier =
            plan.IsValid && plan.ShouldJumpNow &&
            JumpPhysicsFeedback.FixedStepSequence <
                speedPlanningResumeFixedStep;
        bool completionDashBarrier =
            successfulCompletionTraversal &&
            JumpPhysicsFeedback.FixedStepSequence <
                completionDashPlanningResumeFixedStep;
        if (stationaryBeforeRoute ||
            speedTransitionBarrier || completionDashBarrier)
        {
            if (Time.unscaledTime >= nextSpeedPlanningBarrierLogTime)
            {
                nextSpeedPlanningBarrierLogTime = Time.unscaledTime + 0.10f;
                BonusRunnerLog.Debug(
                    $"JumpPlanningDeferred Position=" +
                    $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                    $"ObservedVX={observedHorizontalSpeed:F3}, " +
                    $"ReliableVX={lastReliableHorizontalSpeed:F3}, " +
                    $"PlanningVX={planningHorizontalSpeed:F3}, " +
                    $"SpeedSource={planningSpeedSource}, Grounded=" +
                    $"{state.IsGrounded}, HasNext={scan.HasNext}, " +
                    $"Plan={plan.Reason}/{plan.Maneuver}, " +
                    $"CurrentFixedStep=" +
                    $"{JumpPhysicsFeedback.FixedStepSequence}, " +
                    $"SpeedResumeFixedStep={speedPlanningResumeFixedStep}, " +
                    $"DashResumeFixedStep=" +
                    $"{completionDashPlanningResumeFixedStep}, Reason=" +
                    (stationaryBeforeRoute
                        ? "LiveHorizontalVelocityZero"
                        : completionDashBarrier
                            ? "CompletionWindDashVelocityPending"
                            : "VelocityDiscontinuityPendingPhysicsStep") +
                    ". No DOWN is sent from a stale horizontal model.",
                    "Physics");
            }
            return;
        }

        if (plan.IsValid &&
            plan.Maneuver == BonusManeuverKind.EnterTrenchThenWallJump &&
            scan.HasNext &&
            state.PlayerPosition.x >= plan.PlannedLaunchX - 0.16f)
        {
            BeginPassiveWallApproach(
                state,
                plan,
                scan,
                scan.Next,
                hazard,
                physics);
            return;
        }

        if (!plan.IsValid || !plan.ShouldJumpNow)
        {
            // The shared jump/attack pulse is only a true end-of-route fallback.
            // A valid route that is waiting for its launch window, and an
            // IntentionalDrop route, must remain completely input-free here.
            bool noTraversalRoute =
                successfulCompletionTraversal &&
                IsConfirmedCompletionTerminalRoute(state, scan);
            if (noTraversalRoute)
                TryCompletionRewardActions(state, player);
            return;
        }

        BonusRunnerLog.Debug(
            $"JumpDecision X={state.PlayerPosition.x:F3}, Hold={plan.HoldSeconds:F3}s, " +
            $"EffectiveHold={Mathf.Min(plan.HoldSeconds, physics.EffectiveHoldCapSeconds):F3}s, " +
            $"CandidateRange=[0.020,0.180]s, NativeCap={physics.EffectiveHoldCapSeconds:F3}s, " +
            $"Flight={plan.PredictedFlightSeconds:F3}s, Travel={plan.HorizontalTravel:F3}, " +
            $"Window=[{plan.LaunchWindowLeft:F3},{plan.LaunchWindowRight:F3}], " +
            $"TargetSafe=[{plannedTarget.SafeLeft:F3},{plannedTarget.SafeRight:F3}]@{plannedTarget.Top:F3}, " +
            $"PredictedLandingX={plan.PredictedLandingX:F3}, " +
            $"Maneuver={plan.Maneuver}, Reason={plan.Reason}, " +
            $"Physics[{physics.Summary}]",
            "Routing");
        jumpController.Press(
            player,
            plan.HoldSeconds,
            $"BoardPlan: Gap={scan.Gap:F2}, DeltaY={scan.HeightDelta:F2}, " +
            $"Window=[{plan.LaunchWindowLeft:F2},{plan.LaunchWindowRight:F2}], " +
            $"Landing={plan.PredictedLandingX:F2}, {plan.Reason}");
        MarkAutomaticJumpRequested(
            state,
            plan,
            plannedTarget,
            scan,
            hazard,
            physics);
        if (!automaticJumpArmed)
            ClearRoutePlanLock();
    }

    private bool IsSuccessfulCompletionTraversal(
        BonusStageState state)
    {
        BonusMapController controller = BonusMapController.instance;
        return state.GameStateName == "PreBonusMode" &&
            controller != null &&
            state.HasSphereProgress &&
            state.CollectedSpheres >=
                (int)Math.Ceiling(state.RequiredSpheres);
    }

    private bool IsCompletionDashControlIdle() =>
        automaticJumpArmed &&
        !jumpController.IsHoldingJump &&
        !automaticPredictionActive &&
        !(learningSampleActive && learningSource == "Automatic") &&
        !passiveWallApproachActive &&
        !wallExitContactWatchActive &&
        !attachedObjectiveDescentActive &&
        !wallMandatoryFaceSetupActive &&
        !wallMandatoryFaceInterceptCommitted &&
        wallActionPhase == WallActionPhase.None;

    private bool IsConfirmedCompletionTerminalRoute(
        BonusStageState state,
        BonusBoardScanResult scan)
    {
        if (scan.IsValid)
        {
            ResetCompletionTerminalCandidate();
            return !scan.HasNext;
        }

        if (!state.IsGrounded)
        {
            ResetCompletionTerminalCandidate();
            return false;
        }

        long fixedStep = JumpPhysicsFeedback.FixedStepSequence;
        if (fixedStep != completionInvalidRouteLastFixedStep)
        {
            completionInvalidRouteLastFixedStep = fixedStep;
            completionInvalidRouteStableFixedSteps++;
        }

        bool confirmed = completionInvalidRouteStableFixedSteps >= 3;
        if (completionInvalidRouteStableFixedSteps == 1 ||
            completionInvalidRouteStableFixedSteps == 3)
        {
            BonusRunnerLog.Debug(
                $"CompletionTerminalCandidate Section=" +
                $"{state.SectionIndex}, ScanReason={scan.Reason}, " +
                $"StableFixedSteps=" +
                $"{completionInvalidRouteStableFixedSteps}/3, " +
                $"Position=({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Grounded=" +
                $"{state.IsGrounded}, Confirmed={confirmed}. " +
                (confirmed
                    ? "Persistent missing traversal geometry authorizes " +
                      "the terminal reward fallback."
                    : "Reward input remains blocked while the map scanner " +
                      "gets two more fixed-step observations."),
                "Completion");
        }
        return confirmed;
    }

    private void ResetCompletionTerminalCandidate()
    {
        completionInvalidRouteLastFixedStep = -1;
        completionInvalidRouteStableFixedSteps = 0;
    }

    private bool TryCompletionRewardActions(
        BonusStageState state,
        PlayerMovement player)
    {
        if (Plugin.Config?.AutomaticJumping?.Value != true ||
            !state.HasPlayer ||
            player == null ||
            state.GameStateName != "PreBonusMode" ||
            !state.IsSupportedBonusMap)
        {
            return false;
        }

        if (!IsSuccessfulCompletionTraversal(state))
            return false;

        if (!state.IsGrounded)
        {
            return true;
        }

        return completionRewardController.TryTerminalRewardActions(
            state,
            player,
            jumpController,
            Plugin.Config?.CompletionRewardActions?.Value == true);
    }

    private void ObserveNoSupportStall(
        BonusStageState state,
        BonusBoardScanResult scan)
    {
        bool invalidSupport = !scan.IsValid;
        bool terminalRouteEnd =
            scan.IsValid && !scan.HasNext;
        if ((!invalidSupport && !terminalRouteEnd) ||
            !state.IsGrounded ||
            Mathf.Abs(state.PlayerVelocity.x) > 0.50f ||
            !state.IsActiveGameplay)
        {
            noSupportStallStartedAt = -1f;
            return;
        }

        if (noSupportStallStartedAt < 0f)
        {
            noSupportStallStartedAt = Time.unscaledTime;
            nextNoSupportStallLogTime = Time.unscaledTime + 0.40f;
            return;
        }

        float stalledFor = Time.unscaledTime - noSupportStallStartedAt;
        if (stalledFor < 0.40f ||
            Time.unscaledTime < nextNoSupportStallLogTime)
        {
            return;
        }

        nextNoSupportStallLogTime = Time.unscaledTime + 1.0f;
        BonusMapPieceRegistry registry = platformScanner.MapRegistry;
        Vector2[] sectionActive = BonusStageInspector.GetActiveSpherePositions(
            -100000f,
            100000f,
            500);
        int activeBehind = sectionActive.Count(
            sphere => sphere.x < state.PlayerPosition.x - 1.0f);
        int activeAhead = sectionActive.Length - activeBehind;
        string classification =
            state.HasSphereProgress && state.RemainingRequiredSpheres > 0
                ? "TerminalOrRouteEndWithSphereDeficit"
                : "PerceptionOrTerminalWait";
        string failureDomain = registry.State !=
            BonusMapPieceRegistryState.Ready
                ? "Perception"
                : state.HasSphereProgress &&
                  state.RemainingRequiredSpheres > activeAhead
                    ? "Route:QuotaDeficit"
                    : invalidSupport
                        ? "PerceptionOrTerminal"
                        : "TerminalWait";
        BonusRunnerLog.Warning(
            $"NoSupportStall Classification={classification}, " +
            $"Duration={stalledFor:F3}s, Section={state.SectionIndex + 1}, " +
            $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
            $"Scan={scan.Reason}, StaticMap={registry.State}/" +
            $"{registry.StatusReason}/G{registry.Generation}, " +
            $"SphereProgress={(state.HasSphereProgress ? $"{state.CollectedSpheres}/{Math.Ceiling(state.RequiredSpheres):F0}" : "Unavailable")}, " +
            $"RequiredRaw={(state.HasSphereProgress ? state.RequiredSpheres.ToString("F3") : "Unavailable")}, " +
            $"RemainingRequired={state.RemainingRequiredSpheres}, " +
            $"ActiveSpheres[Total={sectionActive.Length}," +
            $"Behind={activeBehind},Ahead={activeAhead}], " +
            $"Timer={state.CurrentTime:F3}/{state.MaximumTime:F3}. " +
            $"FailureDomain={failureDomain}, Action=None, " +
            "SafetyPolicy=DoNotBlindJump. " +
            "The input is released while the route is reacquired or the " +
            "game resolves the terminal state.");
    }

    private void MonitorCommittedJump(
        BonusStageState state,
        PlayerMovement player)
    {
        float elapsed = Mathf.Max(
            0f,
            Time.unscaledTime - jumpController.LastPressStartedAt);

        // During the first half of a wall route, touching the intended face is
        // more authoritative than the precomputed hold deadline. Release this
        // press immediately so the next frame can issue the independent climb
        // press with a newly calculated duration.
        if (automaticManeuver ==
            BonusManeuverKind.ApproachJumpThenWallJump)
        {
            BonusWallContact wall = wallDetector.Detect(
                player,
                GetWallRouteSpeed());
            bool intendedWall = wall.IsDetected &&
                wall.IsTouching &&
                wall.Point.x >= automaticTargetLeft - 0.80f &&
                wall.Point.x <= automaticTargetRight + 0.80f;
            if (intendedWall)
            {
                BonusRunnerLog.Debug(
                    $"FramePlan WallContactEarlyRelease Frame={Time.frameCount}, " +
                    $"Elapsed={elapsed:F3}s, Position=" +
                    $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                    $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                    $"WallPoint=({wall.Point.x:F3},{wall.Point.y:F3}), " +
                    $"Collider={wall.ColliderInstanceId}:{wall.ColliderName}.",
                    "FramePlan");
                jumpController.Release();
                wallReleaseObservedFixedStep =
                    JumpPhysicsFeedback.FixedStepSequence;
                wallActionPhase = WallActionPhase.AwaitingWallContact;
                learningInputUpTime = Time.unscaledTime;
                learningInputReleased = true;
            }
            return;
        }

        // Mandatory Ground 3 face interception ends the powered phase at the
        // first observed old-wall detachment. Waiting until the whole body has
        // crossed the platform (the generic WallLipCleared boundary) keeps
        // resetting VY while horizontal travel has already resumed and moves
        // the S3 intersection above its finite top.
        if (automaticManeuver == BonusManeuverKind.WallJumpClimb &&
            wallMandatoryFaceInterceptCommitted &&
            wallExitFaceContactRequired &&
            wallExitTargetActive)
        {
            float currentFeetY = player.playerCollider != null
                ? player.playerCollider.bounds.min.y
                : state.PlayerPosition.y - 0.27f;
            float currentHalfWidth = player.playerCollider != null
                ? Mathf.Max(0.15f, player.playerCollider.bounds.extents.x)
                : 0.60f;
            BonusWallContact releaseWall = wallDetector.Detect(
                player,
                GetWallRouteSpeed());
            float movedFromContact =
                state.PlayerPosition.x - wallRecoveryContactX;
            bool horizontalResumed =
                state.PlayerVelocity.x >=
                    MandatoryWallFaceHorizontalResumeVelocity &&
                movedFromContact >= 0.030f &&
                currentFeetY >= wallRecoveryRequiredReleaseY - 0.15f;
            // A single composite-ray miss is not detachment evidence. The
            // old fallback released valid pulses below the lip while VX was
            // still zero. The fixed-step actuator is the safe ceiling when a
            // render frame does not observe horizontal resume in time.
            if (horizontalResumed)
            {
                float targetContactX = float.IsNaN(
                    wallMandatoryFaceTargetContactX)
                    ? wallExitTarget.Left - currentHalfWidth
                    : wallMandatoryFaceTargetContactX;
                JumpPhysicsSnapshot facePhysics =
                    BuildMandatoryFacePlanningPhysics(
                        jumpPhysicsFeedback.CaptureSnapshot(player));
                bool hasReleasePrediction =
                    jumpPlanner.TryPredictReleasedWallFaceContact(
                        state.PlayerPosition.x,
                        currentFeetY,
                        state.PlayerVelocity.y,
                        targetContactX,
                        GetWallRouteSpeed(),
                        facePhysics,
                        out int predictionSteps,
                        out float predictionSeconds,
                        out float predictionFeetY,
                        out float predictionVelocityY);
                wallMandatoryFaceReleaseFeetY = currentFeetY;
                wallMandatoryFaceReleaseVelocityY = state.PlayerVelocity.y;
                wallMandatoryFaceReleasePredictedContactFeetY =
                    hasReleasePrediction ? predictionFeetY : float.NaN;
                wallMandatoryFaceReleasePredictedContactVelocityY =
                    hasReleasePrediction ? predictionVelocityY : float.NaN;
                BonusRunnerLog.Debug(
                    $"MandatoryFaceInterceptEarlyRelease Trigger=" +
                    $"HorizontalResume, " +
                    $"Position=({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), FeetY={currentFeetY:F3}, " +
                    $"Velocity=({state.PlayerVelocity.x:F3}," +
                    $"{state.PlayerVelocity.y:F3}), " +
                    $"MovedFromContact={movedFromContact:F3}, " +
                    $"WallTouching={releaseWall.IsTouching}, " +
                    $"TargetContactX={targetContactX:F3}, " +
                    $"ReleasePrediction=" +
                    $"{(hasReleasePrediction ? $"Steps={predictionSteps},T={predictionSeconds:F3},FeetY={predictionFeetY:F3},VY={predictionVelocityY:F3}" : "Unavailable")}, " +
                    $"PlanPrediction[FeetY=" +
                    $"{wallMandatoryFacePredictedContactFeetY:F3},VY=" +
                    $"{wallMandatoryFacePredictedContactVelocityY:F3},T=" +
                    $"{wallMandatoryFacePredictedContactSeconds:F3}]. " +
                    "Action=UP now and lock all further DOWN until physical " +
                    "contact with the mapped S3 face.",
                    "Recovery");
                jumpController.Release();
                learningInputUpTime = Time.unscaledTime;
                learningInputReleased = true;
                wallRecoveryLipCrossed = true;
                wallRecoveryContactLatched = false;
                wallRecoverySawUpwardMotion = false;
                wallReleaseObservedFixedStep =
                    JumpPhysicsFeedback.FixedStepSequence;
                wallActionPhase = WallActionPhase.ExitFlight;
                ResetWallResidualRiseWait();
                bool watchArmed = TryArmWallExitContactWatch(
                    state,
                    "MandatoryFaceHorizontalResume");
                if (!watchArmed)
                {
                    BonusRunnerLog.Warning(
                        $"MandatoryFaceInterceptWatchArmFailed Position=" +
                        $"({state.PlayerPosition.x:F3}," +
                        $"{state.PlayerPosition.y:F3}), Target=" +
                        $"[{wallExitTarget.Left:F3}," +
                        $"{wallExitTarget.Right:F3}]@" +
                        $"{wallExitTarget.Top:F3}. No additional DOWN will " +
                        "be sent for the stale face-intercept plan. " +
                        "FailureDomain=RouteState; the commitment is reset " +
                        "atomically.");
                    automaticTrajectoryCompatible = false;
                    if (learningSampleActive)
                    {
                        FinishLearningSample(
                            state,
                            "MandatoryFaceWatchArmFailed");
                    }
                    ResetAutomaticControlState();
                    nextAutomaticAttemptTime =
                        Time.unscaledTime + 0.20f;
                }
                return;
            }
        }

        // Input duration is now committed once DOWN is sent. Render-frame
        // velocity samples can briefly report VX=0 and vertical position only
        // changes on physics frames; using either to shorten the active press
        // caused severe oscillation and 0.02s accidental jumps. Continue to
        // rescan the target for diagnostics, but apply corrections to the next
        // action rather than destabilising the current one.
        BonusBoardSegment expectedTarget = new(
            automaticTargetLeft,
            automaticTargetRight,
            automaticTargetTop,
            automaticTargetSafeLeft,
            automaticTargetSafeRight,
            automaticTargetColliderId,
            automaticTargetColliderName,
            automaticTargetMapPieceName,
            automaticTargetMapPieceOriginX,
            automaticTargetMapPieceInstanceId,
            automaticTargetRegistryGeneration,
            automaticTargetStaticSurfaceIndex);
        bool refreshed = platformScanner.TryRefreshExpectedSurface(
            expectedTarget,
            automaticPredictedLandingX,
            player,
            out BonusBoardSegment liveTarget,
            out string refreshReason);
        if (!refreshed)
            liveTarget = expectedTarget;
        else
        {
            bool expectedHasMappedIdentity =
                expectedTarget.MapPieceInstanceId != 0 &&
                expectedTarget.StaticSurfaceIndex >= 0;
            bool liveHasMappedIdentity =
                liveTarget.MapPieceInstanceId != 0 &&
                liveTarget.StaticSurfaceIndex >= 0;
            bool contradictoryMappedIdentity =
                expectedHasMappedIdentity &&
                liveHasMappedIdentity &&
                (liveTarget.MapPieceInstanceId !=
                    expectedTarget.MapPieceInstanceId ||
                 liveTarget.StaticSurfaceIndex !=
                    expectedTarget.StaticSurfaceIndex);
            if (contradictoryMappedIdentity &&
                wallExitFaceContactRequired)
            {
                FailMandatoryFacePlan(
                    state,
                    "CommittedCurrentSurfaceIdentityChanged",
                    $"Expected={expectedTarget.MapPieceName}#" +
                    $"{expectedTarget.MapPieceInstanceId}/S" +
                    $"{expectedTarget.StaticSurfaceIndex}/G" +
                    $"{expectedTarget.RegistryGeneration}, Live=" +
                    $"{liveTarget.MapPieceName}#" +
                    $"{liveTarget.MapPieceInstanceId}/S" +
                    $"{liveTarget.StaticSurfaceIndex}/G" +
                    $"{liveTarget.RegistryGeneration}, Refresh=" +
                    refreshReason);
                return;
            }
            if (contradictoryMappedIdentity)
            {
                refreshed = false;
                liveTarget = expectedTarget;
            }

            automaticTargetLeft = liveTarget.Left;
            automaticTargetRight = liveTarget.Right;
            automaticTargetTop = liveTarget.Top;
            automaticTargetSafeLeft = liveTarget.SafeLeft;
            automaticTargetSafeRight = liveTarget.SafeRight;
            if (liveHasMappedIdentity)
            {
                automaticTargetMapPieceName = liveTarget.MapPieceName;
                automaticTargetMapPieceOriginX = liveTarget.MapPieceOriginX;
                automaticTargetMapPieceInstanceId =
                    liveTarget.MapPieceInstanceId;
                automaticTargetRegistryGeneration =
                    liveTarget.RegistryGeneration;
                automaticTargetStaticSurfaceIndex =
                    liveTarget.StaticSurfaceIndex;
            }
            else
            {
                // A composite-collider probe can verify live bounds while its
                // static annotation is temporarily Unknown#0/S-1. Preserve
                // the already verified Ground3 identity; otherwise the next
                // frame silently drops the dedicated solver and re-enters the
                // old generic DOWN path.
                automaticTargetMapPieceName = expectedTarget.MapPieceName;
                automaticTargetMapPieceOriginX =
                    expectedTarget.MapPieceOriginX;
                automaticTargetMapPieceInstanceId =
                    expectedTarget.MapPieceInstanceId;
                automaticTargetRegistryGeneration =
                    expectedTarget.RegistryGeneration;
                automaticTargetStaticSurfaceIndex =
                    expectedTarget.StaticSurfaceIndex;
                liveTarget = liveTarget with
                {
                    MapPieceName = expectedTarget.MapPieceName,
                    MapPieceOriginX = expectedTarget.MapPieceOriginX,
                    MapPieceInstanceId = expectedTarget.MapPieceInstanceId,
                    RegistryGeneration = expectedTarget.RegistryGeneration,
                    StaticSurfaceIndex = expectedTarget.StaticSurfaceIndex
                };
            }
        }
        if (Time.unscaledTime >= nextDynamicPlanLogTime)
        {
            nextDynamicPlanLogTime = Time.unscaledTime + 0.08f;
            BonusRunnerLog.Debug(
                $"CommittedJumpFrame Frame={Time.frameCount}, " +
                $"AttemptId={automaticAttemptId}, " +
                $"Elapsed={elapsed:F3}s, Position=" +
                $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"Velocity=" +
                $"({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                $"Target=[{liveTarget.SafeLeft:F3},{liveTarget.SafeRight:F3}]" +
                $"@{liveTarget.Top:F3}, Refreshed={refreshed}, " +
                $"Refresh={refreshReason}, CommittedHold=" +
                $"{automaticPlannedHold:F3}s, " +
                $"RemainingHold={Mathf.Max(0f, automaticPlannedHold - elapsed):F3}s. " +
                "Policy=NeverShortenAfterInputDown",
                "FramePlan");
        }
    }

    [HideFromIl2Cpp]
    private BonusJumpPlan GetStableRoutePlan(
        BonusStageState state,
        BonusBoardScanResult scan,
        Vector2 planningVelocity,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        IReadOnlyList<Vector2> sphereObjectives)
    {
        if (routePlanLocked &&
            lockedRoutePlan.Maneuver == BonusManeuverKind.SphereCollectionJump)
        {
            // Sphere state can change without geometry, speed or hazard
            // changing. Never carry an optional pickup plan across frames.
            ClearRoutePlanLock();
        }

        if (routePlanLocked)
        {
            bool lockedToCurrentSupport =
                lockedRoutePlan.Maneuver ==
                BonusManeuverKind.SphereCollectionJump;
            BonusBoardSegment liveRouteTarget = lockedToCurrentSupport
                ? scan.Current
                : scan.Next;
            bool targetStillMatches =
                scan.IsValid &&
                (lockedToCurrentSupport || scan.HasNext) &&
                state.SectionIndex == routeLockSection &&
                StaticOrColliderIdentityMatches(
                    liveRouteTarget,
                    lockedRouteTarget) &&
                Mathf.Abs(liveRouteTarget.Left - lockedRouteTarget.Left) <= 0.20f &&
                Mathf.Abs(liveRouteTarget.Right - lockedRouteTarget.Right) <= 0.20f &&
                Mathf.Abs(liveRouteTarget.Top - lockedRouteTarget.Top) <= 0.25f &&
                Mathf.Abs(liveRouteTarget.SafeLeft - lockedRouteTarget.SafeLeft) <= 0.20f &&
                Mathf.Abs(liveRouteTarget.SafeRight - lockedRouteTarget.SafeRight) <= 0.20f &&
                Mathf.Abs(planningVelocity.x - routeLockSpeed) <= 0.35f &&
                physics.ModelRevision == routeLockPhysicsRevision &&
                hazard.InstanceId == routeLockHazardId;
            bool windowStillAvailable =
                state.PlayerPosition.x <=
                lockedRoutePlan.LaunchWindowRight + 0.16f;

            if (targetStillMatches && windowStillAvailable)
            {
                bool trajectorySafe = jumpPlanner.IsTrajectorySafe(
                    hazard,
                    state.PlayerPosition.x,
                    state.PlayerPosition.x + lockedRoutePlan.HorizontalTravel,
                    scan.Current.Top,
                    lockedRoutePlan.HoldSeconds,
                    lockedRoutePlan.PredictedFlightSeconds,
                    physics,
                    out string trajectoryCheck);
                bool lateLandingRecovery =
                    state.PlayerPosition.x > lockedRoutePlan.LaunchWindowRight;
                const float landingRecoveryTolerance = 0.14f;
                bool shouldJump =
                    state.PlayerPosition.x >=
                    lockedRoutePlan.PlannedLaunchX - 0.16f &&
                    state.PlayerPosition.x + lockedRoutePlan.HorizontalTravel >=
                    lockedRouteTarget.SafeLeft - landingRecoveryTolerance &&
                    state.PlayerPosition.x + lockedRoutePlan.HorizontalTravel <=
                    lockedRouteTarget.SafeRight + landingRecoveryTolerance &&
                    trajectorySafe;
                float predictedLandingX = shouldJump
                    ? state.PlayerPosition.x + lockedRoutePlan.HorizontalTravel
                    : lockedRoutePlan.PlannedLaunchX + lockedRoutePlan.HorizontalTravel;
                return lockedRoutePlan with
                {
                    ShouldJumpNow = shouldJump,
                    PredictedLandingX = predictedLandingX,
                    Reason = shouldJump
                        ? lateLandingRecovery
                            ? "LockedLateLandingRecovery"
                            : "LockedLaunchWindow"
                        : trajectorySafe
                            ? "LockedApproach"
                            : "LockedTrajectoryUnsafe",
                    CandidateSummary = lockedRoutePlan.CandidateSummary +
                        $" | LiveCheck:{trajectoryCheck}"
                };
            }

            BonusRunnerLog.Debug(
                $"RouteLock released at X={state.PlayerPosition.x:F3}: " +
                $"TargetMatch={targetStillMatches}, WindowAvailable={windowStillAvailable}, " +
                $"LockedPhysicsRev={routeLockPhysicsRevision}, CurrentPhysicsRev={physics.ModelRevision}.",
                "Routing");
            ClearRoutePlanLock();
        }

        BonusJumpPlan terrainPlan = jumpPlanner.Plan(
            scan,
            state.PlayerPosition,
            planningVelocity,
            physics,
            hazard,
            Array.Empty<Vector2>());
        BonusJumpPlan sphereAwarePlan =
            sphereObjectives != null && sphereObjectives.Count > 0
                ? jumpPlanner.Plan(
                    scan,
                    state.PlayerPosition,
                    planningVelocity,
                    physics,
                    hazard,
                    sphereObjectives)
                : terrainPlan;

        // Terrain geometry owns every ordinary crossing decision. Sphere data
        // may create a dedicated same-surface opportunity, but it must never
        // change the hold, launch point or landing target of a required route.
        BonusJumpPlan plan =
            sphereAwarePlan.Maneuver == BonusManeuverKind.SphereCollectionJump
                ? sphereAwarePlan
                : terrainPlan;
        if (plan.Maneuver != BonusManeuverKind.SphereCollectionJump &&
            sphereAwarePlan.ExpectedSphereHits > 0 &&
            TerrainCommandMatches(plan, sphereAwarePlan))
        {
            plan = plan with
            {
                ExpectedSphereHits = sphereAwarePlan.ExpectedSphereHits,
                CandidateSummary = plan.CandidateSummary +
                    $" | SphereMetadataOnly[ExpectedHits=" +
                    $"{sphereAwarePlan.ExpectedSphereHits}]"
            };
        }
        if (plan.Maneuver == BonusManeuverKind.SphereCollectionJump &&
            !SphereOpportunityPreservesTerrainDeadline(
                scan,
                terrainPlan,
                plan,
                planningVelocity.x,
                out string sphereDeadlineCheck))
        {
            plan = terrainPlan with
            {
                CandidateSummary = terrainPlan.CandidateSummary +
                    $" | SphereOpportunityRejected[{sphereDeadlineCheck}]"
            };
        }
        bool planTargetsCurrentSupport =
            plan.Maneuver == BonusManeuverKind.SphereCollectionJump;
        BonusBoardSegment planTarget = planTargetsCurrentSupport
            ? scan.Current
            : scan.Next;
        if (plan.IsValid &&
            !plan.ShouldJumpNow &&
            (planTargetsCurrentSupport || scan.HasNext) &&
            plan.Maneuver != BonusManeuverKind.ApproachJumpThenWallJump &&
            plan.Maneuver != BonusManeuverKind.EnterTrenchThenWallJump &&
            plan.Maneuver != BonusManeuverKind.SphereCollectionJump)
        {
            routePlanLocked = true;
            routeLockSection = state.SectionIndex;
            routeLockSpeed = planningVelocity.x;
            routeLockPhysicsRevision = physics.ModelRevision;
            routeLockHazardId = hazard.InstanceId;
            lockedRouteTarget = planTarget;
            lockedRoutePlan = plan;
            BonusRunnerLog.Debug(
                $"RouteLock acquired: Target=[{planTarget.Left:F3},{planTarget.Right:F3}]@{planTarget.Top:F3}, " +
                $"MapPiece={planTarget.MapPieceName}, Generation={planTarget.RegistryGeneration}, " +
                $"Hold={plan.HoldSeconds:F3}s, Launch={plan.PlannedLaunchX:F3}, " +
                $"Window=[{plan.LaunchWindowLeft:F3},{plan.LaunchWindowRight:F3}], " +
                $"PhysicsRev={physics.ModelRevision}.",
                "Routing");
        }

        return plan;
    }

    private static bool TerrainCommandMatches(
        BonusJumpPlan terrainPlan,
        BonusJumpPlan sphereAwarePlan)
    {
        return terrainPlan.IsValid == sphereAwarePlan.IsValid &&
            terrainPlan.ShouldJumpNow == sphereAwarePlan.ShouldJumpNow &&
            terrainPlan.Maneuver == sphereAwarePlan.Maneuver &&
            Mathf.Abs(terrainPlan.HoldSeconds - sphereAwarePlan.HoldSeconds) <=
                0.0001f &&
            Mathf.Abs(
                terrainPlan.PlannedLaunchX -
                sphereAwarePlan.PlannedLaunchX) <= 0.001f &&
            Mathf.Abs(
                terrainPlan.PredictedLandingX -
                sphereAwarePlan.PredictedLandingX) <= 0.001f;
    }

    private static bool SphereOpportunityPreservesTerrainDeadline(
        BonusBoardScanResult scan,
        BonusJumpPlan terrainPlan,
        BonusJumpPlan spherePlan,
        float horizontalSpeed,
        out string check)
    {
        if (!scan.HasNext)
        {
            check = "NoImmediateTerrainTransition";
            return true;
        }

        bool hasTerrainDeadline = terrainPlan.IsValid ||
            terrainPlan.Reason == "IntentionalDrop";
        if (!hasTerrainDeadline)
        {
            bool continuous = terrainPlan.Reason == "ContinuousSurface";
            check = continuous
                ? "ContinuousSurfaceUsesReservedRunway"
                : $"BaseTerrainPlanUnavailable:{terrainPlan.Reason}";
            return continuous;
        }

        float reactionMargin = Mathf.Clamp(
            Mathf.Abs(horizontalSpeed) * 0.04f,
            0.45f,
            2.00f);
        float latestSafeLanding =
            terrainPlan.LaunchWindowRight - reactionMargin;
        bool preserves =
            spherePlan.PredictedLandingX <= latestSafeLanding;
        check =
            $"SphereLanding={spherePlan.PredictedLandingX:F3}," +
            $"TerrainWindowRight={terrainPlan.LaunchWindowRight:F3}," +
            $"ReactionMargin={reactionMargin:F3}," +
            $"LatestSafeLanding={latestSafeLanding:F3}," +
            $"BaseReason={terrainPlan.Reason},Preserves={preserves}";
        return preserves;
    }

    private void LogRouteDiagnostics(
        BonusStageState state,
        BonusBoardScanResult scan,
        BonusJumpPlan plan,
        float horizontalSpeed,
        JumpPhysicsSnapshot physics)
    {
        if (!BonusRunnerLog.IsDebugMode)
            return;

        string signature = scan.IsValid && scan.HasNext
            ? $"{state.SectionIndex}:{scan.Current.Top:F1}:" +
              $"{scan.Next.Left:F1}:{scan.Next.Right:F1}:{scan.Next.Top:F1}:" +
              $"{plan.HoldSeconds:F2}:{plan.Reason}:{plan.ShouldJumpNow}:" +
              $"S{state.CollectedSpheres}:P{physics.ModelRevision}"
            : scan.IsValid
                ? $"{state.SectionIndex}:NoNext:{scan.Current.Top:F1}:{scan.Reason}"
                : $"{state.SectionIndex}:Invalid:{scan.Reason}";
        bool changed = signature != lastRouteSignature;
        if (!changed && Time.unscaledTime < nextRouteLogTime)
            return;

        lastRouteSignature = signature;
        nextRouteLogTime = Time.unscaledTime + 0.75f;

        if (!scan.IsValid)
        {
            BonusRunnerLog.Debug(
                $"SurfaceMap invalid at X={state.PlayerPosition.x:F3}: {scan.Reason}",
                "Routing");
            return;
        }

        string nextBoard = scan.HasNext
            ? $"Next=[{scan.Next.Left:F3},{scan.Next.Right:F3}] " +
              $"Safe=[{scan.Next.SafeLeft:F3},{scan.Next.SafeRight:F3}] " +
              $"Top={scan.Next.Top:F3} Collider={scan.Next.ColliderInstanceId}:{scan.Next.ColliderName} " +
              $"MapPiece={scan.Next.MapPieceName}#{scan.Next.MapPieceInstanceId} " +
              $"Generation={scan.Next.RegistryGeneration} Surface={scan.Next.StaticSurfaceIndex} " +
              $"Local=[{scan.Next.LocalLeft:F2},{scan.Next.LocalRight:F2}]"
            : "Next=Unavailable";
        string intermediateBoard = scan.HasIntermediate
            ? $"; Intermediate=[{scan.Intermediate.Left:F3}," +
              $"{scan.Intermediate.Right:F3}] " +
              $"Safe=[{scan.Intermediate.SafeLeft:F3}," +
              $"{scan.Intermediate.SafeRight:F3}] " +
              $"Top={scan.Intermediate.Top:F3} Collider=" +
              $"{scan.Intermediate.ColliderInstanceId}:" +
              $"{scan.Intermediate.ColliderName}"
            : string.Empty;
        string alternativeBoards =
            scan.Alternatives != null && scan.Alternatives.Length > 0
                ? "; Alternatives=" + string.Join(
                    "|",
                    scan.Alternatives.Take(12).Select(segment =>
                        $"[{segment.Left:F2},{segment.Right:F2}]" +
                        $"@{segment.Top:F2}:{segment.MapPieceName}"))
                : string.Empty;
        Vector2[] sectionActiveSpheres =
            BonusStageInspector.GetActiveSpherePositions(
                -100000f,
                100000f,
                500);
        int activeBehind = sectionActiveSpheres.Count(
            sphere => sphere.x < state.PlayerPosition.x - 1.0f);
        int activeAhead = sectionActiveSpheres.Length - activeBehind;
        string quotaStatus = !state.HasSphereProgress
            ? "Unavailable"
            : state.RemainingRequiredSpheres <= 0
                ? "Met"
                : activeAhead >= state.RemainingRequiredSpheres
                    ? "VisibleAheadUpperBoundSufficient"
                    : "VisibleAheadDeficitRisk";
        BonusRunnerLog.Debug(
            $"SurfaceMap X={state.PlayerPosition.x:F3}, Scanner=AllLayersStandable; " +
            $"Current=[{scan.Current.Left:F3},{scan.Current.Right:F3}] " +
            $"Safe=[{scan.Current.SafeLeft:F3},{scan.Current.SafeRight:F3}] " +
            $"Top={scan.Current.Top:F3} Collider={scan.Current.ColliderInstanceId}:{scan.Current.ColliderName} " +
            $"MapPiece={scan.Current.MapPieceName}#{scan.Current.MapPieceInstanceId} " +
            $"Generation={scan.Current.RegistryGeneration} Surface={scan.Current.StaticSurfaceIndex} " +
            $"Local=[{scan.Current.LocalLeft:F2},{scan.Current.LocalRight:F2}]; " +
            $"{nextBoard}{intermediateBoard}{alternativeBoards}; " +
            $"SafeEdgeDistance={scan.DistanceToCurrentEdge:F3}, " +
            $"Gap={scan.Gap:F3}, DeltaY={scan.HeightDelta:F3}, Reason={scan.Reason}, " +
            $"SphereProgress={(state.HasSphereProgress ? $"{state.CollectedSpheres}/{Math.Ceiling(state.RequiredSpheres):F0}" : "Unavailable")}, " +
            $"RequiredRaw={(state.HasSphereProgress ? state.RequiredSpheres.ToString("F3") : "Unavailable")}, " +
            $"Remaining={state.RemainingRequiredSpheres}, " +
            $"SphereInventory[Active={sectionActiveSpheres.Length}," +
            $"Behind={activeBehind},Ahead={activeAhead},Status={quotaStatus}]",
            "Routing");

        BonusRunnerLog.Debug(
            $"RouteEval X={state.PlayerPosition.x:F3}, VX={horizontalSpeed:F3}, " +
            $"Valid={plan.IsValid}, Action={(plan.ShouldJumpNow ? "Jump" : "Wait")}, " +
            $"ChosenHold={plan.HoldSeconds:F3}s, Flight={plan.PredictedFlightSeconds:F3}s, " +
            $"Travel={plan.HorizontalTravel:F3}, PlannedLaunch={plan.PlannedLaunchX:F3}, " +
            $"Window=[{plan.LaunchWindowLeft:F3},{plan.LaunchWindowRight:F3}], " +
            $"PredictedLanding={plan.PredictedLandingX:F3}, " +
            $"Maneuver={plan.Maneuver}, ExpectedSphereHits={plan.ExpectedSphereHits}, " +
            $"Reason={plan.Reason}; " +
            $"DecisionClass={ClassifyRouteDecision(plan, scan)}, " +
            $"FailureDomain={ClassifyRouteFailureDomain(plan, scan)}; " +
            $"Physics[{physics.Summary}]; " +
            $"Candidates: {plan.CandidateSummary}",
            "Routing");
    }

    private static string ClassifyRouteDecision(
        BonusJumpPlan plan,
        BonusBoardScanResult scan)
    {
        if (plan.IsValid)
            return plan.ShouldJumpNow ? "ExecutableNow" : "ApproachOrSafetyWait";
        if (plan.Reason == "IntentionalDrop")
            return "IntentionalCoast";
        if (plan.Reason == "ContinuousSurface")
            return "WalkForward";
        if (plan.Reason == "NextBoardUnavailable")
            return "ObserveRouteEnd";
        if (plan.Reason == "HorizontalSpeedTooLow")
            return "AwaitHorizontalMotion";
        return scan.IsValid ? "NoExecutableRoute" : "PerceptionPending";
    }

    private static string ClassifyRouteFailureDomain(
        BonusJumpPlan plan,
        BonusBoardScanResult scan)
    {
        if (plan.IsValid ||
            plan.Reason == "IntentionalDrop" ||
            plan.Reason == "ContinuousSurface" ||
            plan.Reason == "NextBoardUnavailable" ||
            plan.Reason == "HorizontalSpeedTooLow")
        {
            return "None";
        }

        return scan.IsValid ? "Route" : "Perception";
    }

    [HideFromIl2Cpp]
    private int GetPhysicsStepBudget(float seconds)
    {
        float fixedStep = latestPhysicsSnapshot.FixedDeltaTime;
        if (float.IsNaN(fixedStep) ||
            float.IsInfinity(fixedStep) ||
            fixedStep <= 0f)
        {
            fixedStep = Time.fixedDeltaTime;
        }
        fixedStep = Mathf.Clamp(fixedStep, 0.005f, 0.05f);
        return Mathf.Max(
            1,
            Mathf.CeilToInt(Mathf.Max(0.02f, seconds) / fixedStep));
    }

    [HideFromIl2Cpp]
    private static bool IsVerifiedGround3MandatoryFaceGeometry(
        BonusStageState state,
        BonusBoardSegment currentWall,
        BonusBoardSegment targetFace,
        out string reason)
    {
        float gap = targetFace.Left - currentWall.Right;
        float topDelta = targetFace.Top - currentWall.Top;
        bool sectionMatch = state.SectionIndex == 1;
        bool currentRole = string.Equals(
                currentWall.MapPieceName,
                "Ground 3",
                StringComparison.OrdinalIgnoreCase) &&
            currentWall.StaticSurfaceIndex == 2;
        bool targetRole = string.Equals(
                targetFace.MapPieceName,
                "Ground 3",
                StringComparison.OrdinalIgnoreCase) &&
            targetFace.StaticSurfaceIndex == 3;
        bool sameMappedInstance =
            currentWall.MapPieceInstanceId != 0 &&
            currentWall.MapPieceInstanceId ==
                targetFace.MapPieceInstanceId;
        bool sameCapturedGeneration =
            currentWall.RegistryGeneration <= 0 ||
            targetFace.RegistryGeneration <= 0 ||
            currentWall.RegistryGeneration ==
                targetFace.RegistryGeneration;
        bool originCompatible =
            !float.IsNaN(currentWall.MapPieceOriginX) &&
            !float.IsNaN(targetFace.MapPieceOriginX) &&
            Mathf.Abs(
                currentWall.MapPieceOriginX -
                targetFace.MapPieceOriginX) <= 0.15f;
        bool geometryMatch =
            gap >= 2.50f && gap <= 3.50f &&
            Mathf.Abs(topDelta) <= 0.15f;
        bool valid =
            sectionMatch &&
            currentRole &&
            targetRole &&
            sameMappedInstance &&
            originCompatible &&
            geometryMatch;
        reason =
            $"Section={state.SectionIndex + 1}/Expected2," +
            $"Current={currentWall.MapPieceName}#" +
            $"{currentWall.MapPieceInstanceId}/G" +
            $"{currentWall.RegistryGeneration}/S" +
            $"{currentWall.StaticSurfaceIndex}@" +
            $"[{currentWall.Left:F3},{currentWall.Right:F3}]/" +
            $"Y{currentWall.Top:F3}/O{currentWall.MapPieceOriginX:F3}," +
            $"Target={targetFace.MapPieceName}#" +
            $"{targetFace.MapPieceInstanceId}/G" +
            $"{targetFace.RegistryGeneration}/S" +
            $"{targetFace.StaticSurfaceIndex}@" +
            $"[{targetFace.Left:F3},{targetFace.Right:F3}]/" +
            $"Y{targetFace.Top:F3}/O{targetFace.MapPieceOriginX:F3}," +
            $"Gap={gap:F3},TopDelta={topDelta:F3},Checks[" +
            $"Section={sectionMatch},CurrentRole={currentRole}," +
            $"TargetRole={targetRole},SameInstance=" +
            $"{sameMappedInstance},SameCapturedGeneration=" +
            $"{sameCapturedGeneration}(DiagnosticOnly),Origin=" +
            $"{originCompatible}," +
            $"Geometry={geometryMatch}]";
        return valid;
    }

    [HideFromIl2Cpp]
    private bool TryValidateMandatoryFaceRouteIdentity(
        BonusStageState state,
        PlayerMovement player,
        out string reason)
    {
        if (!wallExitFaceContactRequired || !wallExitTargetActive)
        {
            reason =
                $"ContractRequired={wallExitFaceContactRequired}," +
                $"TargetActive={wallExitTargetActive}";
            return false;
        }

        BonusBoardSegment currentWall = BuildAutomaticTargetSegment();
        if (!IsVerifiedGround3MandatoryFaceGeometry(
                state,
                currentWall,
                wallExitTarget,
                out reason))
        {
            return false;
        }

        float halfWidth = player?.playerCollider != null
            ? Mathf.Max(0.15f, player.playerCollider.bounds.extents.x)
            : 0.60f;
        float expectedContactX = wallExitTarget.Left - halfWidth;
        bool contactXCompatible =
            !wallMandatoryFaceInterceptCommitted ||
            float.IsNaN(wallMandatoryFaceTargetContactX) ||
            Mathf.Abs(
                wallMandatoryFaceTargetContactX - expectedContactX) <= 0.20f;
        reason +=
            $",TargetContactX[Stored=" +
            $"{wallMandatoryFaceTargetContactX:F3},Expected=" +
            $"{expectedContactX:F3},Compatible={contactXCompatible}]";
        return contactXCompatible;
    }

    [HideFromIl2Cpp]
    private bool FailMandatoryFacePlan(
        BonusStageState state,
        string reason,
        string details)
    {
        jumpController.Release();
        BonusRunnerLog.Warning(
            $"MandatoryFacePlanFailed Reason={reason}, " +
            $"Position=({state.PlayerPosition.x:F3}," +
            $"{state.PlayerPosition.y:F3}), Velocity=" +
            $"({state.PlayerVelocity.x:F3}," +
            $"{state.PlayerVelocity.y:F3}), ActionPhase=" +
            $"{wallActionPhase}, FixedStep=" +
            $"{JumpPhysicsFeedback.FixedStepSequence}, Details[{details}]. " +
            "FailureDomain=RouteExecution; Action=UP and all mandatory " +
            "setup/intercept/watch state is cleared atomically. No generic " +
            "fallback DOWN is allowed for this failed contract.");
        automaticTrajectoryCompatible = false;
        if (learningSampleActive && learningSource == "Automatic")
            FinishLearningSample(state, $"MandatoryFace:{reason}");
        ResetAutomaticControlState();
        nextAutomaticAttemptTime = Time.unscaledTime + 0.20f;
        return true;
    }

    [HideFromIl2Cpp]
    private bool HandleMandatoryFaceContactWatch(
        BonusStageState state,
        PlayerMovement player)
    {
        if (!wallExitTargetActive ||
            !wallExitFaceContactRequired ||
            !wallExitContactWatchActive)
        {
            return FailMandatoryFacePlan(
                state,
                "ContactWatchNotArmed",
                $"TargetActive={wallExitTargetActive}," +
                $"ContractRequired={wallExitFaceContactRequired}," +
                $"WatchActive={wallExitContactWatchActive}");
        }

        long currentFixedStep = JumpPhysicsFeedback.FixedStepSequence;
        if (wallMandatoryFaceContactWatchDeadlineFixedStep < 0)
        {
            wallMandatoryFaceContactWatchDeadlineFixedStep =
                currentFixedStep +
                GetPhysicsStepBudget(
                    MandatoryWallFaceContactWatchSeconds);
        }

        BonusBoardSegment target = wallExitTarget;
        BonusWallContact wall = wallDetector.Detect(
            player,
            GetWallRouteSpeed());
        float halfWidth = player.playerCollider != null
            ? Mathf.Max(0.15f, player.playerCollider.bounds.extents.x)
            : 0.60f;
        float feetY = player.playerCollider != null
            ? player.playerCollider.bounds.min.y
            : state.PlayerPosition.y - 0.27f;
        float bodyRight = state.PlayerPosition.x + halfWidth;
        float targetContactCenterX = target.Left - halfWidth;
        float faceBottomY = target.Top - Ground3ObjectiveFaceHeight;
        float physicalMinimumFeetY =
            faceBottomY + MandatoryWallFacePhysicalBottomClearance;
        float physicalMaximumFeetY =
            target.Top - MandatoryWallFacePhysicalTopClearance;
        bool insideFiniteFace =
            feetY >= physicalMinimumFeetY &&
            feetY <= physicalMaximumFeetY;
        bool nativeWallEvidence =
            wall.Reason?.IndexOf(
                "NativeIsWalled=True",
                StringComparison.Ordinal) >= 0;
        bool sideNormal = wall.Normal.x <= -0.35f;
        bool bodyAtTargetFace =
            Mathf.Abs(bodyRight - target.Left) <=
                MandatoryWallFaceBodyXMatchTolerance;
        bool rayAtTargetFace =
            wall.ColliderInstanceId != 0 &&
            Mathf.Abs(wall.FaceX - target.Left) <=
                MandatoryWallFaceRayXMatchTolerance &&
            wall.Point.y >= faceBottomY - 0.15f &&
            wall.Point.y <= target.Top + 0.15f;
        bool exactTargetFaceContact =
            wall.IsDetected &&
            wall.IsTouching &&
            insideFiniteFace &&
            sideNormal &&
            (rayAtTargetFace ||
             nativeWallEvidence && bodyAtTargetFace);
        bool kinematicPersistence =
            wallMandatoryFaceContactObservedFixedStep >= 0 &&
            insideFiniteFace &&
            Mathf.Abs(state.PlayerVelocity.x) <=
                WallAttachmentVelocityTolerance &&
            Mathf.Abs(
                state.PlayerPosition.x - targetContactCenterX) <=
                MandatoryWallFaceBodyXMatchTolerance &&
            bodyRight >= target.Left - 0.20f &&
            bodyRight <= target.Left + 0.12f;
        bool hasCurrentContactEvidence =
            exactTargetFaceContact || kinematicPersistence;

        if (wallMandatoryFaceContactObservedFixedStep >= 0 &&
            currentFixedStep >
                wallMandatoryFaceContactObservedFixedStep)
        {
            if (hasCurrentContactEvidence)
            {
                long firstContactStep =
                    wallMandatoryFaceContactObservedFixedStep;
                bool usedKinematicPersistence =
                    !exactTargetFaceContact && kinematicPersistence;
                jumpController.Release();
                BonusRunnerLog.Debug(
                    $"MandatoryFaceInterceptObserved Position=" +
                    $"({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), FeetY={feetY:F3}, " +
                    $"Velocity=({state.PlayerVelocity.x:F3}," +
                    $"{state.PlayerVelocity.y:F3}), TargetFace=" +
                    $"X={target.Left:F3},Y=[{faceBottomY:F3}," +
                    $"{target.Top:F3}], Evidence=" +
                    $"{(usedKinematicPersistence ? "KinematicPersistenceAfterExactContact" : "ExactRayOrNative")}, " +
                    $"FixedSteps[First={firstContactStep},Current=" +
                    $"{currentFixedStep},Delta=" +
                    $"{currentFixedStep - firstContactStep}], " +
                    $"Normal=({wall.Normal.x:F3},{wall.Normal.y:F3}), " +
                    $"WallReason={wall.Reason}, ElapsedFromDown=" +
                    $"{Time.unscaledTime - wallMandatoryFaceInterceptStartedAt:F3}s, " +
                    $"PlanPrediction[FeetY=" +
                    $"{wallMandatoryFacePredictedContactFeetY:F3},VY=" +
                    $"{wallMandatoryFacePredictedContactVelocityY:F3},T=" +
                    $"{wallMandatoryFacePredictedContactSeconds:F3}," +
                    $"ErrorY={feetY - wallMandatoryFacePredictedContactFeetY:F3}], " +
                    $"ReleasePrediction[ReleaseFeetY=" +
                    $"{wallMandatoryFaceReleaseFeetY:F3},ReleaseVY=" +
                    $"{wallMandatoryFaceReleaseVelocityY:F3},ContactFeetY=" +
                    $"{wallMandatoryFaceReleasePredictedContactFeetY:F3}," +
                    $"ContactVY=" +
                    $"{wallMandatoryFaceReleasePredictedContactVelocityY:F3}," +
                    $"ErrorY=" +
                    $"{feetY - wallMandatoryFaceReleasePredictedContactFeetY:F3}]. " +
                    "Result=ConfirmedFiniteTargetSideFaceContact.",
                    "Recovery");

                BonusWallContact confirmedWall = exactTargetFaceContact
                    ? wall
                    : new BonusWallContact(
                        true,
                        true,
                        halfWidth,
                        0f,
                        target.Left,
                        new Vector2(target.Left, feetY),
                        Vector2.left,
                        0,
                        "ConfirmedMappedGround3S3Face",
                        "TwoFixedStepContact:ExactThenKinematicPersistence");
                int capturedObjectiveCount =
                    wallExitObjectiveCountAtCapture;
                float capturedObjectiveMinimumY =
                    wallExitObjectiveMinimumY;
                float capturedObjectiveMaximumY =
                    wallExitObjectiveMaximumY;
                if (!TryPromoteChainedWallTarget(
                        state,
                        allowLevelFace: true))
                {
                    return FailMandatoryFacePlan(
                        state,
                        "ConfirmedContactPromotionFailed",
                        $"Target=[{target.Left:F3}," +
                        $"{target.Right:F3}]@{target.Top:F3}, " +
                        $"FeetY={feetY:F3}, Evidence=" +
                        $"{confirmedWall.Reason}");
                }

                BonusRunnerLog.Debug(
                    $"WallExitContactIntercepted Position=" +
                    $"({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), FeetY={feetY:F3}, " +
                    $"Evidence={confirmedWall.Reason}. The finite S3 side " +
                    "face is now authoritative; objective descent is tried " +
                    "before any fresh climb input.",
                    "Recovery");
                if (TryManageAttachedObjectiveDescent(
                        state,
                        confirmedWall,
                        feetY,
                        capturedObjectiveCount,
                        capturedObjectiveMinimumY,
                        capturedObjectiveMaximumY))
                {
                    return true;
                }

                // A confirmed mandatory-face handoff may coincide with a
                // transient sphere-registry refresh. Never recurse into the
                // generic wall executor on that same physics observation: a
                // fresh DOWN here would undo the finite-face contact we just
                // proved. The next update can re-evaluate the promoted face.
                jumpController.Release();
                wallActionPhase = WallActionPhase.AwaitingWallContact;
                wallStallStartedAt = -1f;
                nextWallRecoveryTime = 0f;
                passiveWallApproachActive = true;
                BonusRunnerLog.Debug(
                    "MandatoryFaceHandoffBarrier Action=UP; objective " +
                    "descent was not armed on the confirmation frame, so " +
                    "generic wall input is deferred until a later update.",
                    "Recovery");
                return true;
            }

            BonusRunnerLog.Debug(
                $"MandatoryFaceContactConfirmationLost FirstStep=" +
                $"{wallMandatoryFaceContactObservedFixedStep}, " +
                $"CurrentStep={currentFixedStep}, Position=" +
                $"({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), FeetY={feetY:F3}, " +
                $"WallDetected={wall.IsDetected}, " +
                $"Touching={wall.IsTouching}, Exact=" +
                $"{exactTargetFaceContact}, Persistence=" +
                $"{kinematicPersistence}. The observed physics step did not " +
                "retain target-face contact; a later exact contact must start " +
                "a new confirmation.",
                "Recovery");
            wallMandatoryFaceContactObservedFixedStep = -1;
        }

        bool physicsBudgetExpired =
            currentFixedStep >
                wallMandatoryFaceContactWatchDeadlineFixedStep;
        if (wallMandatoryFaceContactObservedFixedStep < 0 &&
            physicsBudgetExpired)
        {
            return FailMandatoryFacePlan(
                state,
                "ContactWatchPhysicsBudgetExpired",
                $"FixedStep={currentFixedStep}, DeadlineStep=" +
                $"{wallMandatoryFaceContactWatchDeadlineFixedStep}, " +
                $"FirstExactContact={exactTargetFaceContact}. A first " +
                "contact after the fixed-step budget cannot revive the " +
                "expired mandatory route.");
        }

        if (wallMandatoryFaceContactObservedFixedStep < 0 &&
            exactTargetFaceContact)
        {
            wallMandatoryFaceContactObservedFixedStep = currentFixedStep;
            wallMandatoryFaceContactWatchDeadlineFixedStep = Math.Max(
                wallMandatoryFaceContactWatchDeadlineFixedStep,
                currentFixedStep + 2);
            jumpController.Release();
            BonusRunnerLog.Debug(
                $"MandatoryFaceContactConfirmationStarted FixedStep=" +
                $"{currentFixedStep}, Position=" +
                $"({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), FeetY={feetY:F3}, " +
                $"Velocity=({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), Face=" +
                $"({wall.Point.x:F3},{wall.Point.y:F3}), FaceBand=" +
                $"[{physicalMinimumFeetY:F3}," +
                $"{physicalMaximumFeetY:F3}], BodyRight=" +
                $"{bodyRight:F3}, TargetX={target.Left:F3}, " +
                $"Normal=({wall.Normal.x:F3},{wall.Normal.y:F3}), " +
                $"Evidence={wall.Reason}. Action=UP; require a later " +
                "physics step on the same finite side face. A top/corner " +
                "landing cannot satisfy this contract.",
                "Recovery");
            return true;
        }

        if (wallMandatoryFaceContactObservedFixedStep >= 0 &&
            currentFixedStep ==
                wallMandatoryFaceContactObservedFixedStep)
        {
            jumpController.Release();
            return true;
        }

        bool passedTargetFace =
            state.PlayerPosition.x > target.Left + 0.35f;
        bool fellBelowFiniteFace =
            feetY < faceBottomY - 0.35f;
        if (physicsBudgetExpired ||
            passedTargetFace ||
            fellBelowFiniteFace)
        {
            return FailMandatoryFacePlan(
                state,
                physicsBudgetExpired
                    ? "ContactWatchPhysicsBudgetExpired"
                    : passedTargetFace
                        ? "PassedTargetFaceWithoutContact"
                        : "FellBelowTargetFace",
                $"FixedStep={currentFixedStep}, DeadlineStep=" +
                $"{wallMandatoryFaceContactWatchDeadlineFixedStep}, " +
                $"Target=[{target.Left:F3},{target.Right:F3}]@" +
                $"{target.Top:F3}, FeetY={feetY:F3}, " +
                $"Wall[Detected={wall.IsDetected},Touching=" +
                $"{wall.IsTouching},FaceX={wall.FaceX:F3}," +
                $"PointY={wall.Point.y:F3},NormalX=" +
                $"{wall.Normal.x:F3},Reason={wall.Reason}], Checks[" +
                $"FiniteY={insideFiniteFace},BodyAtFace=" +
                $"{bodyAtTargetFace},RayAtFace={rayAtTargetFace}," +
                $"SideNormal={sideNormal},Native=" +
                $"{nativeWallEvidence}]");
        }

        if (BonusRunnerLog.IsDebugMode &&
            Time.unscaledTime >= nextMandatoryFaceWindowLogTime)
        {
            nextMandatoryFaceWindowLogTime = Time.unscaledTime + 0.08f;
            BonusRunnerLog.Debug(
                $"MandatoryFaceInterceptContactWatch Position=" +
                $"({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), FeetY={feetY:F3}, " +
                $"Velocity=({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), TargetFace=" +
                $"X={target.Left:F3},Y=[{faceBottomY:F3}," +
                $"{target.Top:F3}], PhysicalFeetBand=" +
                $"[{physicalMinimumFeetY:F3}," +
                $"{physicalMaximumFeetY:F3}], FixedSteps[Current=" +
                $"{currentFixedStep},Deadline=" +
                $"{wallMandatoryFaceContactWatchDeadlineFixedStep}," +
                $"Remaining={Math.Max(0L, wallMandatoryFaceContactWatchDeadlineFixedStep - currentFixedStep)}], " +
                $"Wall[Detected={wall.IsDetected},Touching=" +
                $"{wall.IsTouching},Face=({wall.Point.x:F3}," +
                $"{wall.Point.y:F3}),Normal=({wall.Normal.x:F3}," +
                $"{wall.Normal.y:F3}),Reason={wall.Reason}], Checks[" +
                $"FiniteY={insideFiniteFace},BodyAtFace=" +
                $"{bodyAtTargetFace},RayAtFace={rayAtTargetFace}," +
                $"SideNormal={sideNormal},Exact=" +
                $"{exactTargetFaceContact},Persistence=" +
                $"{kinematicPersistence}]. Action=UP; no DOWN is legal " +
                "until confirmed finite S3 side contact.",
                "Recovery");
        }
        jumpController.Release();
        return true;
    }

    private bool TryWallRecoveryJump(
        BonusStageState state,
        PlayerMovement player)
    {
        // A wall jump is valid only while an automatic board trajectory is
        // actively trying to reach its recorded target. Pit/death walls must
        // never manufacture a new route by themselves.
        bool activeAutomaticJumpRoute =
            automaticPredictionActive &&
            learningSampleActive &&
            learningSource == "Automatic";
        bool unresolvedWallRoute =
            wallExitContactWatchActive ||
            attachedObjectiveDescentActive ||
            wallMandatoryFaceSetupActive ||
            wallMandatoryFaceInterceptCommitted;
        if (!passiveWallApproachActive &&
            !activeAutomaticJumpRoute &&
            !unresolvedWallRoute)
        {
            wallStallStartedAt = -1f;
            return false;
        }

        if ((wallMandatoryFaceSetupActive ||
             wallMandatoryFaceInterceptCommitted) &&
            !TryValidateMandatoryFaceRouteIdentity(
                state,
                player,
                out string mandatoryIdentityReason))
        {
            return FailMandatoryFacePlan(
                state,
                "RouteIdentityChanged",
                mandatoryIdentityReason);
        }

        bool mandatoryFaceSetupPhysicsExpired =
            wallMandatoryFaceSetupActive &&
            wallMandatoryFaceSetupDeadlineFixedStep >= 0 &&
            JumpPhysicsFeedback.FixedStepSequence >
                wallMandatoryFaceSetupDeadlineFixedStep;
        if (mandatoryFaceSetupPhysicsExpired)
        {
            BonusWallContact expiryWall = wallDetector.Detect(
                player,
                GetWallRouteSpeed());
            float expiryHalfWidth = player.playerCollider != null
                ? Mathf.Max(0.15f, player.playerCollider.bounds.extents.x)
                : 0.60f;
            float expectedCurrentFaceCenterX =
                automaticTargetLeft - expiryHalfWidth;
            bool exactCurrentFace =
                expiryWall.IsDetected &&
                expiryWall.IsTouching &&
                Mathf.Abs(expiryWall.FaceX - automaticTargetLeft) <= 0.45f;
            bool kinematicCurrentFace =
                Mathf.Abs(state.PlayerVelocity.x) <=
                    WallAttachmentVelocityTolerance &&
                Mathf.Abs(
                    state.PlayerPosition.x - expectedCurrentFaceCenterX) <=
                    WallAttachmentPositionTolerance;
            if (!exactCurrentFace && !kinematicCurrentFace)
            {
                return FailMandatoryFacePlan(
                    state,
                    "SetupPhysicsBudgetExpiredWithoutOldFaceContact",
                    $"Position=({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), Velocity=" +
                    $"({state.PlayerVelocity.x:F3}," +
                    $"{state.PlayerVelocity.y:F3}), FixedStep=" +
                    $"{JumpPhysicsFeedback.FixedStepSequence}, " +
                    $"DeadlineStep=" +
                    $"{wallMandatoryFaceSetupDeadlineFixedStep}, " +
                    $"Wall[Detected={expiryWall.IsDetected}," +
                    $"Touching={expiryWall.IsTouching},FaceX=" +
                    $"{expiryWall.FaceX:F3},Reason={expiryWall.Reason}]");
            }
        }

        if (wallMandatoryFaceInterceptCommitted &&
            wallActionPhase != WallActionPhase.ExitFlight &&
            wallMandatoryFaceContactWatchDeadlineFixedStep >= 0 &&
            JumpPhysicsFeedback.FixedStepSequence >
                wallMandatoryFaceContactWatchDeadlineFixedStep)
        {
            return FailMandatoryFacePlan(
                state,
                "InterceptNeverEnteredContactWatch",
                $"StartedStep=" +
                $"{wallMandatoryFaceInterceptStartedFixedStep}, " +
                $"CurrentStep={JumpPhysicsFeedback.FixedStepSequence}, " +
                $"DeadlineStep=" +
                $"{wallMandatoryFaceContactWatchDeadlineFixedStep}, " +
                $"Holding={jumpController.IsHoldingJump}, Phase=" +
                $"{wallActionPhase}");
        }

        // Mandatory S3 contact owns the frame before any generic timeout,
        // speed, phase, stall or ray-miss gate. A valid contact on the final
        // deadline frame must be observed before timeout is considered.
        if (wallMandatoryFaceInterceptCommitted &&
            wallActionPhase == WallActionPhase.ExitFlight)
        {
            return HandleMandatoryFaceContactWatch(state, player);
        }

        if (wallExitContactWatchActive &&
            !wallMandatoryFaceInterceptCommitted &&
            (Time.unscaledTime > wallRecoveryCommitmentUntil ||
             state.PlayerPosition.x > wallExitTarget.Right + 1.0f))
        {
            bool mandatoryContactExpired =
                wallExitFaceContactRequired;
            BonusRunnerLog.Warning(
                $"WallExitContactWatchExpired Position=" +
                $"({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), ExitTarget=" +
                $"[{wallExitTarget.Left:F3}," +
                $"{wallExitTarget.Right:F3}]@" +
                $"{wallExitTarget.Top:F3}, TimeRemaining=" +
                $"{wallRecoveryCommitmentUntil - Time.unscaledTime:F3}s, " +
                $"ContactRequired={mandatoryContactExpired}, " +
                $"ObjectiveCount={wallExitObjectiveCountAtCapture}. " +
                (mandatoryContactExpired
                    ? "No mandatory downstream face contact was observed; " +
                      "FailureDomain=RouteExecution and the stale wall route " +
                      "is reset."
                    : "No downstream contact was observed; the exit target " +
                      "and all related commitments are cleared atomically."));
            if (mandatoryContactExpired)
            {
                automaticTrajectoryCompatible = false;
                if (learningSampleActive &&
                    learningSource == "Automatic")
                {
                    FinishLearningSample(
                        state,
                        "MandatoryWallFaceContactWatchExpired");
                }
                ResetAutomaticControlState();
                nextAutomaticAttemptTime = Time.unscaledTime + 0.20f;
                return true;
            }

            wallExitContactWatchActive = false;
            wallExitTargetActive = false;
            wallExitTarget = default;
            wallExitFaceContactRequired = false;
            wallExitObjectiveCountAtCapture = 0;
            wallExitObjectiveMinimumY = float.NaN;
            wallExitObjectiveMaximumY = float.NaN;
            ResetMandatoryFacePlanState();
            wallExitTransferCommitted = false;
            wallLandingFlightCommitted = false;
            wallTopLandingSequenceCommitted = false;
            wallExitPredictedLandingX = 0f;
            wallExitPredictedTravel = 0f;
            wallExitPredictedFlightSeconds = 0f;
            wallExitPlanSummary = string.Empty;
        }

        if (!attachedObjectiveDescentActive &&
            passiveWallApproachActive &&
            (Time.unscaledTime - automaticJumpRequestedAt > 1.50f ||
             state.PlayerPosition.x > automaticTargetRight + 1.0f))
        {
            BonusRunnerLog.Warning(
                $"WallDropRouteAborted Position=({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Elapsed=" +
                $"{Time.unscaledTime - automaticJumpRequestedAt:F3}s, " +
                $"Target=[{automaticTargetLeft:F3},{automaticTargetRight:F3}]" +
                $"@{automaticTargetTop:F3}. No confirmed wall contact was " +
                "observed; the passive route is cleared instead of jumping " +
                "against an unrelated death wall.");
            ResetAutomaticControlState();
            return false;
        }

        if (attachedObjectiveDescentActive)
        {
            BonusWallContact descentWall = wallDetector.Detect(
                player,
                GetWallRouteSpeed());
            float descentFeetY = player.playerCollider != null
                ? player.playerCollider.bounds.min.y
                : state.PlayerPosition.y - 0.27f;
            if (TryManageAttachedObjectiveDescent(
                    state,
                    descentWall,
                    descentFeetY))
            {
                return true;
            }
        }

        float horizontalSpeed = Mathf.Abs(state.PlayerVelocity.x);
        bool committedWallClimb = IsCommittedWallClimbActive();
        bool belowPlannedWallTop =
            committedWallClimb &&
            state.PlayerPosition.y < automaticTargetTop + 0.35f;
        bool transientWallGrounding =
            state.IsGrounded &&
            (state.PlayerVelocity.y < -2f || belowPlannedWallTop);
        float inferredPlayerHalfWidth = Mathf.Max(
            0.15f,
            automaticTargetSafeLeft - automaticTargetLeft - 0.15f);
        float plannedWallContactCenterX =
            automaticTargetLeft - inferredPlayerHalfWidth;
        bool hasPlannedTargetGeometry =
            automaticTargetRight > automaticTargetLeft + 0.05f &&
            automaticTargetTop > state.PlayerPosition.y - 0.50f;
        bool plannedWallApproach =
            passiveWallApproachActive ||
            automaticManeuver == BonusManeuverKind.ApproachJumpThenWallJump &&
                hasPlannedTargetGeometry &&
                state.PlayerPosition.y < automaticTargetTop - 0.10f;
        // The wider ray is useful for seeing the upcoming face, but it is not
        // proof that the game will accept a wall press. In the latest run the
        // first press was sent 0.31 units before body contact and produced no
        // upward impulse. Only bypass stall confirmation once the player
        // centre is at the physical contact centre (within one frame).
        bool plannedBodyContactReady =
            plannedWallApproach &&
            state.PlayerPosition.x >= plannedWallContactCenterX - 0.06f &&
            state.PlayerPosition.x <= plannedWallContactCenterX + 0.14f &&
            horizontalSpeed <= 1.25f;

        // A landing-targeted press normally owns the action through landing.
        // Keep one evidence-based escape hatch: if its upward motion has
        // actually exhausted while the body is still attached below the lip,
        // the model was optimistic and another separated wall press is legal.
        // Merely being attached for one post-UP physics step is not enough.
        if (wallLandingFlightCommitted &&
            wallActionPhase == WallActionPhase.ExitFlight &&
            !wallRecoveryLipCrossed)
        {
            BonusWallContact committedWall = wallDetector.Detect(
                player,
                GetWallRouteSpeed());
            bool stillAttachedBelowLip =
                committedWall.IsDetected &&
                committedWall.IsTouching &&
                committedWall.Point.x >= automaticTargetLeft - 0.80f &&
                committedWall.Point.x <= automaticTargetRight + 0.80f &&
                horizontalSpeed <= WallAttachmentVelocityTolerance &&
                state.PlayerPosition.y < automaticTargetTop - 0.20f;
            bool riseExhausted =
                state.PlayerVelocity.y <= -1.0f;
            if (!stillAttachedBelowLip || !riseExhausted)
                return false;

            if (wallRecoveryAttempts >=
                MaximumWallRecoveriesPerAirborneSequence)
            {
                BonusRunnerLog.Warning(
                    $"CommittedWallLandingFailed Reason=RiseExhaustedBelowLip, " +
                    $"Position=({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), Velocity=" +
                    $"({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                    $"Attempts={wallRecoveryAttempts}/" +
                    $"{MaximumWallRecoveriesPerAirborneSequence}. " +
                    "FailureDomain=Execution; retry limit reached.");
                FinishLearningSample(
                    state,
                    "CommittedWallLandingRetryLimitReached");
                ResetAutomaticControlState();
                nextAutomaticAttemptTime = Time.unscaledTime + 0.25f;
                return false;
            }

            wallLandingFlightCommitted = false;
            wallExitTransferCommitted = false;
            wallRecoveryContactLatched = false;
            wallRecoverySawUpwardMotion = false;
            wallReleaseObservedFixedStep = -1;
            wallDetachedLastFixedStep = -1;
            wallDetachedConfirmationSteps = 0;
            wallActionPhase = WallActionPhase.AwaitingNextWallPress;
            wallStallStartedAt =
                Time.unscaledTime - WallStallConfirmationSeconds;
            nextWallRecoveryTime = 0f;
            BonusRunnerLog.Warning(
                $"CommittedWallLandingFallback Reason=RiseExhaustedBelowLip, " +
                $"Position=({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Velocity=" +
                $"({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                $"Attempts={wallRecoveryAttempts}/" +
                $"{MaximumWallRecoveriesPerAirborneSequence}, " +
                $"Collider={committedWall.ColliderInstanceId}:" +
                $"{committedWall.ColliderName}. The solved landing did not " +
                "reach the lip; a separated recovery press is re-authorized.");
        }
        if (wallRecoveryContactLatched &&
            state.PlayerVelocity.y > 2f &&
            state.PlayerPosition.y >= wallRecoveryContactY + 0.30f)
        {
            wallRecoverySawUpwardMotion = true;
        }
        // A wall press is one discrete launch. Only after its UP event and at
        // least one physics frame can we classify the result. Continued
        // contact authorizes another pulse eventually; it does not authorize
        // one while the prior pulse is still rising. The latest manual trace
        // stayed attached through the apex and successfully pressed again on
        // descent, while immediate +17.253-VY re-presses caused every repeated
        // Ground 3 overshoot.
        bool wallPressReleased =
            wallRecoveryContactLatched &&
            wallRecoverySawUpwardMotion &&
            !jumpController.IsHoldingJump &&
            jumpController.LastReleaseAt >= 0f;
        if (wallPressReleased && wallReleaseObservedFixedStep < 0)
        {
            wallReleaseObservedFixedStep =
                JumpPhysicsFeedback.FixedStepSequence;
            wallDetachedLastFixedStep = -1;
            wallDetachedConfirmationSteps = 0;
            return false;
        }
        bool releasedAfterWallPress =
            wallPressReleased &&
            JumpPhysicsFeedback.FixedStepSequence >
                wallReleaseObservedFixedStep;
        if (releasedAfterWallPress)
        {
            BonusWallContact releaseWall = wallDetector.Detect(
                player,
                GetWallRouteSpeed());
            bool sameWallGeometry = releaseWall.IsDetected &&
                releaseWall.Point.x >= automaticTargetLeft - 0.80f &&
                releaseWall.Point.x <= automaticTargetRight + 0.80f;
            bool raycastAttached =
                horizontalSpeed <= WallAttachmentVelocityTolerance &&
                Mathf.Abs(state.PlayerPosition.x - wallRecoveryContactX) <=
                    WallAttachmentPositionTolerance &&
                releaseWall.IsTouching &&
                sameWallGeometry;
            // Physics2D can miss the wall for one frame when the player's
            // collider overlaps the face after a wall-jump impulse.  The
            // failed run showed exactly that at X=1022.495: VX remained zero
            // and the body moved only 0.294 units, but the forward ray
            // returned no hit and the controller incorrectly entered
            // ExitFlight.  Treat the same immobile, near-face motion as
            // continuous attachment while still below the planned lip.
            bool bodyStillNearPlannedFace =
                state.PlayerPosition.x >= automaticTargetLeft - 1.00f &&
                state.PlayerPosition.x <= automaticTargetLeft + 0.25f;
            bool kinematicallyAttached =
                horizontalSpeed <= WallAttachmentVelocityTolerance &&
                Mathf.Abs(state.PlayerPosition.x - wallRecoveryContactX) <=
                    WallAttachmentPositionTolerance &&
                bodyStillNearPlannedFace &&
                !wallRecoveryLipCrossed &&
                state.PlayerPosition.y < automaticTargetTop - 0.20f;
            bool stillAttached =
                raycastAttached || kinematicallyAttached;
            if (!stillAttached &&
                wallDetachedLastFixedStep !=
                    JumpPhysicsFeedback.FixedStepSequence)
            {
                wallDetachedLastFixedStep =
                    JumpPhysicsFeedback.FixedStepSequence;
                wallDetachedConfirmationSteps++;
            }
            if (!stillAttached && wallDetachedConfirmationSteps < 2)
            {
                return false;
            }
            float inputGap =
                Time.unscaledTime - jumpController.LastReleaseAt;
            float executedPulseHold = Mathf.Max(
                0f,
                jumpController.LastReleaseAt -
                    jumpController.LastPressStartedAt);
            bool stillBelowWallLip =
                !wallRecoveryLipCrossed &&
                state.PlayerPosition.y < automaticTargetTop - 0.20f;
            bool needsAnotherWallPress =
                stillAttached &&
                stillBelowWallLip;

            BonusRunnerLog.Debug(
                $"WallPulseResult Pulse={wallRecoveryAttempts}/" +
                $"{MaximumWallRecoveriesPerAirborneSequence}, Hold=" +
                $"{executedPulseHold:F3}s, " +
                $"HeldFixedSteps={jumpController.LastReleaseHeldFixedSteps}, " +
                $"Start=({wallRecoveryContactX:F3}," +
                $"{wallRecoveryContactY:F3}), " +
                $"End=({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Delta=" +
                $"({state.PlayerPosition.x - wallRecoveryContactX:F3}," +
                $"{state.PlayerPosition.y - wallRecoveryContactY:F3}), " +
                $"EndVelocity=({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), StillAttached=" +
                $"{stillAttached}, BelowLip={stillBelowWallLip}, " +
                $"Target=[{automaticTargetLeft:F3}," +
                $"{automaticTargetRight:F3}]@{automaticTargetTop:F3}, " +
                $"WallDetected={releaseWall.IsDetected}, " +
                $"Touching={releaseWall.IsTouching}, " +
                $"SameWall={sameWallGeometry}, BodyGap=" +
                $"{releaseWall.BodyGap:F3}.",
                "Recovery");

            bool completedOldWall = !stillBelowWallLip;
            float classifiedFeetY = player.playerCollider != null
                ? player.playerCollider.bounds.min.y
                : state.PlayerPosition.y - 0.27f;
            if (wallMandatoryFaceSetupActive &&
                (!stillAttached || completedOldWall))
            {
                return FailMandatoryFacePlan(
                    state,
                    "SetupEnvelopeMiss",
                    $"FeetY={classifiedFeetY:F3},Velocity=" +
                    $"({state.PlayerVelocity.x:F3}," +
                    $"{state.PlayerVelocity.y:F3}),StillAttached=" +
                    $"{stillAttached},CompletedOldWall=" +
                    $"{completedOldWall},ReleaseY=" +
                    $"{wallRecoveryRequiredReleaseY:F3},Wall=" +
                    $"{releaseWall.Reason}");
            }
            if (!stillAttached || completedOldWall)
            {
                // Residual rise belongs to the wall that produced it. Once
                // the body leaves that face OR reaches its release height,
                // downstream contact/watch logic must run immediately.
                // Raycasts can keep reporting the old face for one or more
                // frames after the feet have crossed the lip; carrying the
                // wait through that state can hide the next mandatory face.
                ResetWallResidualRiseWait();
            }
            else if (needsAnotherWallPress)
            {
                float releaseFeetY = player.playerCollider != null
                    ? player.playerCollider.bounds.min.y
                    : state.PlayerPosition.y - 0.27f;
                JumpPhysicsSnapshot releasePhysics =
                    BuildWallPlanningPhysics(
                        jumpPhysicsFeedback.CaptureSnapshot(player));
                if (TryDeferWallPressForResidualRise(
                        state,
                        releaseFeetY,
                        releasePhysics,
                        "ReleasedSameFacePulse"))
                {
                    return true;
                }
            }

            if (wallLandingFlightCommitted && !needsAnotherWallPress)
            {
                wallActionPhase = WallActionPhase.ExitFlight;
                wallRecoveryContactLatched = false;
                wallRecoverySawUpwardMotion = false;
                wallReleaseObservedFixedStep = -1;
                wallDetachedLastFixedStep = -1;
                wallDetachedConfirmationSteps = 0;
                passiveWallApproachActive = false;
                BonusRunnerLog.Debug(
                    $"WallPostBounceClassified Result=" +
                    $"{(stillAttached ? "CommittedLandingStillAttached" : "CommittedLandingDetached")}, " +
                    $"Position=({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), Velocity=" +
                    $"({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                    $"Attempt={wallRecoveryAttempts}/" +
                    $"{MaximumWallRecoveriesPerAirborneSequence}, " +
                    $"InputGap={inputGap:F3}s, ExitTransfer=" +
                    $"{wallExitTransferCommitted}, BodyGap=" +
                    $"{releaseWall.BodyGap:F3}. NextState=CommittedLandingFlight; " +
                    "no additional wall press may alter the solved hold or landing.",
                    "Recovery");
                return false;
            }
            if (wallLandingFlightCommitted && needsAnotherWallPress)
            {
                // A predicted wall-top/exit landing must not suppress the
                // game's actual wall-jump sequence.  If the released press
                // leaves the body attached below the lip, it was an
                // intermediate climb press.  Re-authorize a fresh DOWN after
                // this real UP/fixed-step boundary and solve the exit again
                // from the new observed height.
                wallLandingFlightCommitted = false;
                wallExitTransferCommitted = false;
                BonusRunnerLog.Debug(
                    $"WallLandingCommitReopened Result=StillAttachedBelowLip, " +
                    $"Position=({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), TargetTop=" +
                    $"{automaticTargetTop:F3}, Attempt=" +
                    $"{wallRecoveryAttempts}/" +
                    $"{MaximumWallRecoveriesPerAirborneSequence}. " +
                    "The prior press is treated as an intermediate wall " +
                    "climb; another discrete click is allowed.",
                    "Recovery");
            }
            if (needsAnotherWallPress &&
                wallRecoveryAttempts >=
                    MaximumWallRecoveriesPerAirborneSequence)
            {
                wallActionPhase = WallActionPhase.Failed;
                BonusRunnerLog.Warning(
                    $"WallPostBounceClassified Result=BounceLimitWhileAttached, " +
                    $"Position=({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), Attempts=" +
                    $"{wallRecoveryAttempts}/" +
                    $"{MaximumWallRecoveriesPerAirborneSequence}, " +
                    $"BodyGap={releaseWall.BodyGap:F3}, " +
                    $"InputGap={inputGap:F3}s. ActionStatus=Failed, " +
                    "FailureDomain=Execution; no blind extra press is sent.");
                FinishLearningSample(
                    state,
                    "WallBounceLimitReachedWhileAttached");
                ResetAutomaticControlState();
                nextAutomaticAttemptTime = Time.unscaledTime + 0.20f;
                return false;
            }
            if (needsAnotherWallPress &&
                wallRecoveryAttempts <
                    MaximumWallRecoveriesPerAirborneSequence)
            {
                wallRecoveryContactLatched = false;
                wallReleaseObservedFixedStep = -1;
                wallDetachedLastFixedStep = -1;
                wallDetachedConfirmationSteps = 0;
                wallActionPhase = WallActionPhase.AwaitingNextWallPress;
                BonusRunnerLog.Debug(
                    $"WallPostBounceClassified Result=StillAttached, " +
                    $"X={state.PlayerPosition.x:F3}, Y={state.PlayerPosition.y:F3}, " +
                    $"Attempt={wallRecoveryAttempts}/" +
                    $"{MaximumWallRecoveriesPerAirborneSequence}, " +
                    $"RiseSinceContact=" +
                    $"{state.PlayerPosition.y - wallRecoveryContactY:F3}, " +
                    $"InputGap={inputGap:F3}s, " +
                    $"WallDeltaX={state.PlayerPosition.x - wallRecoveryContactX:F3}, " +
                    $"Face=({releaseWall.Point.x:F3},{releaseWall.Point.y:F3}). " +
                    (wallMandatoryFaceSetupActive
                        ? "ActionStatus=Success; MandatoryFaceSetup remains " +
                          "released. The next DOWN is not authorized until a " +
                          "live descent-frame face-intercept candidate is safe."
                        : "ActionStatus=Success; the next wall press is " +
                          "authorized by observed continuous contact."),
                    "Recovery");
            }
            else
            {
                if (!stillAttached && TryPromoteChainedWallTarget(state))
                    return false;

                // "Still attached" is not the same as "still climbing this
                // wall".  At the release height the body ray can still touch
                // the old face while horizontal motion has already begun.
                // Arm the mapped downstream face from that observed boundary
                // so Ground 3 S2 -> S3 cannot silently lose its mandatory
                // contact contract.
                bool watchingDownstreamWall =
                    (!stillAttached || completedOldWall) &&
                    TryArmWallExitContactWatch(
                        state,
                        completedOldWall && stillAttached
                            ? "PostBounceReleaseHeightWhileTouchingOldFace"
                            : "PostBounceExit");

                wallActionPhase = WallActionPhase.ExitFlight;
                wallRecoveryContactLatched = false;
                wallRecoverySawUpwardMotion = false;
                wallReleaseObservedFixedStep = -1;
                wallDetachedLastFixedStep = -1;
                wallDetachedConfirmationSteps = 0;
                if (!watchingDownstreamWall)
                    wallRecoveryCommitmentUntil = 0f;
                passiveWallApproachActive = false;
                BonusRunnerLog.Debug(
                    "WallPostBounceClassified Result=ExitFlight, " +
                    $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                    $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                    $"Attempt={wallRecoveryAttempts}/" +
                    $"{MaximumWallRecoveriesPerAirborneSequence}, " +
                    $"InputGap={inputGap:F3}s, WallDetected={releaseWall.IsDetected}, " +
                    $"SameWall={sameWallGeometry}. ActionStatus=Success, " +
                    $"NextState={(watchingDownstreamWall ? "ExitTargetContactWatch" : "ExitFlight")}; " +
                    (watchingDownstreamWall
                        ? "the mapped downstream face remains armed until " +
                          "physical contact or a safe landing."
                        : "downstream landing will be replanned from " +
                          "observed position and velocity."),
                    "Recovery");
                if (!watchingDownstreamWall)
                    FinishLearningSample(state, "WallBounceExitFlight");
                // A bounce can put the player onto a lower ledge of the same
                // authored wall. If that frame is grounded, permit a fresh
                // route decision; otherwise re-arm on the observed landing.
                automaticJumpArmed = state.IsGrounded;
                airborneAfterAutomaticJump = !state.IsGrounded;
                nextAutomaticAttemptTime = Time.unscaledTime + 0.10f;
                return false;
            }
        }
        if (!wallRecoveryContactLatched &&
            wallReleaseObservedFixedStep >= 0)
        {
            if (JumpPhysicsFeedback.FixedStepSequence <=
                wallReleaseObservedFixedStep)
            {
                return false;
            }
            wallReleaseObservedFixedStep = -1;
        }
        if (horizontalSpeed > WallAttachmentVelocityTolerance &&
            !transientWallGrounding &&
            !plannedBodyContactReady)
        {
            wallStallStartedAt = -1f;
            // Keep the completed press pending until its synthetic UP has
            // crossed the fixed-frame release barrier. Horizontal separation
            // is itself the evidence for ExitFlight; clearing the latch here
            // made post-bounce classification unreachable.
            return false;
        }

        if (jumpController.IsHoldingJump ||
            Time.unscaledTime < nextWallRecoveryTime ||
            wallRecoveryContactLatched ||
            wallRecoveryAttempts >= MaximumWallRecoveriesPerAirborneSequence)
        {
            return false;
        }

        bool downstreamWallContactWatch =
            wallExitContactWatchActive &&
            (wallActionPhase == WallActionPhase.ExitFlight ||
             wallActionPhase == WallActionPhase.AttachedObjectiveDescent);
        if (wallRecoveryAttempts > 0 &&
            wallActionPhase != WallActionPhase.AwaitingNextWallPress &&
            wallActionPhase != WallActionPhase.AwaitingWallContact &&
            wallActionPhase != WallActionPhase.AttachedObjectiveDescent &&
            !downstreamWallContactWatch)
        {
            return false;
        }

        float stalledFor = 0f;
        if (!transientWallGrounding && !plannedBodyContactReady)
        {
            if (wallStallStartedAt < 0f)
            {
                wallStallStartedAt = Time.unscaledTime;
                return false;
            }

            stalledFor = Time.unscaledTime - wallStallStartedAt;
            if (stalledFor < WallStallConfirmationSeconds)
                return false;
        }

        BonusWallContact wall = wallDetector.Detect(
            player,
            GetWallRouteSpeed());
        // After one confirmed wall press, the player's collider can remain
        // overlapped with the face and make Physics2D.RaycastAll return no
        // hit for several consecutive frames.  Post-release classification
        // already accepts this kinematic evidence; the next-press gate must
        // accept the same evidence or it authorizes phase two and then waits
        // forever for a ray that cannot see out of the overlap.
        bool inferredContinuousWallContact =
            wallRecoveryAttempts > 0 &&
            wallActionPhase == WallActionPhase.AwaitingNextWallPress &&
            horizontalSpeed <= WallAttachmentVelocityTolerance &&
            Mathf.Abs(state.PlayerPosition.x - wallRecoveryContactX) <=
                WallAttachmentPositionTolerance &&
            state.PlayerPosition.x >= automaticTargetLeft - 1.00f &&
            state.PlayerPosition.x <= automaticTargetLeft + 0.25f &&
            !wallRecoveryLipCrossed &&
            state.PlayerPosition.y < automaticTargetTop - 0.20f;
        if (BonusRunnerLog.IsDebugMode &&
            Time.unscaledTime >= nextWallProbeLogTime)
        {
            nextWallProbeLogTime = Time.unscaledTime + 0.50f;
            BonusRunnerLog.Debug(
                $"WallRecoveryProbe X={state.PlayerPosition.x:F3}, Y={state.PlayerPosition.y:F3}, " +
                $"VX={state.PlayerVelocity.x:F3}, VY={state.PlayerVelocity.y:F3}, " +
                $"Grounded={state.IsGrounded}, TransientGrounding={transientWallGrounding}, " +
                $"PlannedApproach={plannedWallApproach}, " +
                $"BodyContactReady={plannedBodyContactReady}, " +
                $"ActionPhase={wallActionPhase}, " +
                $"PlannedContactX={plannedWallContactCenterX:F3}, " +
                $"StalledFor={stalledFor:F3}s, Attempt={wallRecoveryAttempts + 1}/" +
                $"{MaximumWallRecoveriesPerAirborneSequence}, Detected={wall.IsDetected}, " +
                $"Distance={wall.Distance:F3}, Point=({wall.Point.x:F3},{wall.Point.y:F3}), " +
                $"Touching={wall.IsTouching}, BodyGap={wall.BodyGap:F3}, FaceX={wall.FaceX:F3}, " +
                $"Normal=({wall.Normal.x:F3},{wall.Normal.y:F3}), " +
                $"Collider={wall.ColliderInstanceId}:{wall.ColliderName}, Reason={wall.Reason}",
                "Recovery");
        }
        if (!wall.IsDetected && !inferredContinuousWallContact)
            return false;

        if (!wall.IsTouching &&
            !inferredContinuousWallContact)
        {
            // Predicted body position is only a cue to probe more closely.
            // It is not permission to consume a DOWN before the game has
            // established contact. A pre-contact DOWN can be accepted as an
            // ordinary airborne jump and leave no fresh edge for wall climb.
            return false;
        }

        bool touchingWatchedExitFace =
            downstreamWallContactWatch &&
            wallExitTargetActive &&
            wall.IsDetected &&
            wall.IsTouching &&
            wall.Point.x >= wallExitTarget.Left - 0.80f &&
            wall.Point.x <= wallExitTarget.Right + 0.80f;
        bool watchedExitIsAlreadyActiveTarget =
            touchingWatchedExitFace &&
            Mathf.Abs(wallExitTarget.Left - automaticTargetLeft) <= 0.20f &&
            Mathf.Abs(wallExitTarget.Right - automaticTargetRight) <= 0.20f &&
            Mathf.Abs(wallExitTarget.Top - automaticTargetTop) <= 0.35f;
        if (watchedExitIsAlreadyActiveTarget)
        {
            BonusBoardSegment contactedExit = wallExitTarget;
            bool satisfiedMandatoryContact =
                wallExitFaceContactRequired;
            wallExitContactWatchActive = false;
            wallExitTargetActive = false;
            wallExitTarget = default;
            wallExitFaceContactRequired = false;
            wallExitObjectiveCountAtCapture = 0;
            wallExitObjectiveMinimumY = float.NaN;
            wallExitObjectiveMaximumY = float.NaN;
            ResetMandatoryFacePlanState();
            wallExitTransferCommitted = false;
            wallLandingFlightCommitted = false;
            wallTopLandingSequenceCommitted = false;
            wallRecoveryContactLatched = false;
            wallRecoverySawUpwardMotion = false;
            wallRecoveryLipCrossed = false;
            wallReleaseObservedFixedStep = -1;
            wallDetachedLastFixedStep = -1;
            wallDetachedConfirmationSteps = 0;
            wallRecoveryRequiredReleaseY = automaticTargetTop - 0.20f;
            wallStallStartedAt =
                Time.unscaledTime - WallStallConfirmationSeconds;
            nextWallRecoveryTime = 0f;
            passiveWallApproachActive = true;
            automaticJumpRequestedAt = Time.unscaledTime;
            wallActionPhase = WallActionPhase.AwaitingWallContact;
            automaticPlanReason = "CommittedExitFaceContactFallback";
            automaticManeuver =
                BonusManeuverKind.ApproachJumpThenWallJump;
            BonusRunnerLog.Warning(
                $"CommittedExitContactFallback Position=" +
                $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"Velocity=({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), Face=" +
                $"({wall.Point.x:F3},{wall.Point.y:F3}), Target=" +
                $"[{contactedExit.Left:F3},{contactedExit.Right:F3}]" +
                $"@{contactedExit.Top:F3}, Collider=" +
                $"{wall.ColliderInstanceId}:{wall.ColliderName}. " +
                "ExpectedResult=direct landing; ActualResult=physical face " +
                "contact. The observed collision overrides the optimistic " +
                "landing prediction and re-enters bounded wall control." +
                (satisfiedMandatoryContact
                    ? " MandatoryFaceContact=Satisfied."
                    : string.Empty));
        }
        else if (touchingWatchedExitFace &&
                 TryPromoteChainedWallTarget(
                     state,
                     allowLevelFace: true))
        {
            BonusRunnerLog.Debug(
                $"WallExitContactIntercepted Position=" +
                $"({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Face=" +
                $"({wall.Point.x:F3},{wall.Point.y:F3}), " +
                $"Collider={wall.ColliderInstanceId}:" +
                $"{wall.ColliderName}. The downstream wall is now the " +
                "active climb target; input is solved from this contact.",
                "Recovery");
            return TryWallRecoveryJump(state, player);
        }

        bool wallBelongsToPlannedTarget =
            inferredContinuousWallContact ||
            wall.Point.x >= automaticTargetLeft - 0.80f &&
            wall.Point.x <= automaticTargetRight + 0.80f;
        if (!wallBelongsToPlannedTarget)
        {
            BonusRunnerLog.Debug(
                $"WallRecoveryRejected Reason=OutsidePlannedTarget, " +
                $"WallX={wall.Point.x:F3}, TargetRaw=" +
                $"[{automaticTargetLeft:F3},{automaticTargetRight:F3}], " +
                $"AttemptId={automaticAttemptId}.",
                "Recovery");
            wallStallStartedAt = -1f;
            return false;
        }

        float objectiveContactFeetY = player.playerCollider != null
            ? player.playerCollider.bounds.min.y
            : state.PlayerPosition.y - 0.27f;
        if (TryManageAttachedObjectiveDescent(
                state,
                wall,
                objectiveContactFeetY))
        {
            return true;
        }

        if (learningSampleActive && learningSource != "Automatic")
            return false;

        CaptureWallExitTargetFromPreview();
        CaptureChainedWallTargetFromStaticMap(inferredPlayerHalfWidth);
        CaptureSectionThreeWallExitTargetFromStaticMap(
            inferredPlayerHalfWidth);

        JumpPhysicsSnapshot observedWallPhysics =
            jumpPhysicsFeedback.CaptureSnapshot(player);
        JumpPhysicsSnapshot wallPhysics = BuildWallPlanningPhysics(
            observedWallPhysics);
        float postLipHorizontalSpeed = GetWallRouteSpeed();
        JumpPhysicsSnapshot wallExitPhysics =
            BuildWallExitPlanningPhysics(
                wallPhysics,
                postLipHorizontalSpeed,
                sectionCruiseHorizontalSpeed,
                state.IsActiveGameplay && !state.SpiritBoostEnabled);
        float physicalReleaseFaceX = wall.IsDetected
            ? wall.FaceX
            : automaticTargetLeft;
        float wallReleaseTravelBias = Mathf.Clamp(
            physicalReleaseFaceX - state.PlayerPosition.x,
            0.14f,
            1.0f);
        JumpPhysicsSnapshot mandatoryFacePhysics =
            BuildMandatoryFacePlanningPhysics(observedWallPhysics);
        float contactFeetY = player.playerCollider != null
            ? player.playerCollider.bounds.min.y
            : state.PlayerPosition.y - 0.27f;
        float playerHeight = player.playerCollider != null
            ? Mathf.Clamp(player.playerCollider.bounds.size.y, 0.75f, 1.75f)
            : 1.18f;
        bool hasKnownWallTarget =
            automaticTargetRight > automaticTargetLeft + 0.05f &&
            automaticTargetTop > contactFeetY + 0.10f;
        bool isGround3ObjectiveFace =
            state.SectionIndex == 1 &&
            string.Equals(
                automaticTargetMapPieceName,
                "Ground 3",
                StringComparison.OrdinalIgnoreCase) &&
            (automaticTargetStaticSurfaceIndex == 3 ||
             automaticTargetStaticSurfaceIndex < 0 &&
             !float.IsNaN(automaticTargetMapPieceOriginX) &&
             Mathf.Abs(
                 automaticTargetLeft -
                  (automaticTargetMapPieceOriginX + 1f)) <= 0.30f);
        BonusBoardSegment mandatoryCurrentWall =
            BuildAutomaticTargetSegment();
        string mandatoryRouteIdentityReason = "ContractNotActive";
        bool isGround3MandatoryFaceRoute =
            wallExitFaceContactRequired &&
            wallExitTargetActive &&
            IsVerifiedGround3MandatoryFaceGeometry(
                state,
                mandatoryCurrentWall,
                wallExitTarget,
                out mandatoryRouteIdentityReason);
        float remainingRise = hasKnownWallTarget
            ? automaticTargetTop - contactFeetY
            : 0f;
        int wallSphereCount = 0;
        float wallSphereMaximumY = float.NegativeInfinity;
        bool hasWallSpheres = hasKnownWallTarget &&
            BonusStageInspector.TryGetActiveSphereVerticalBounds(
                automaticTargetLeft - 1.55f,
                automaticTargetRight + 0.8f,
                out wallSphereCount,
                out _,
                out wallSphereMaximumY);
        // Spheres are route objectives, not merely diagnostics. On scoring
        // walls (notably section three) extend the climb only as far as the
        // highest active row requires. A small pickup allowance avoids
        // demanding that the player's feet reach the sphere centre.
        float sphereRequiredRise = hasWallSpheres
            ? Mathf.Max(0f, wallSphereMaximumY - 0.35f - contactFeetY)
            : 0f;
        float plannedWallRise = Mathf.Max(remainingRise, sphereRequiredRise);
        wallRecoveryRequiredReleaseY = hasWallSpheres
            ? Mathf.Max(automaticTargetTop - 0.20f, wallSphereMaximumY - 0.35f)
            : automaticTargetTop - 0.20f;

        if ((wallMandatoryFaceSetupActive ||
             wallMandatoryFaceInterceptCommitted) &&
            !isGround3MandatoryFaceRoute)
        {
            return FailMandatoryFacePlan(
                state,
                "RouteIdentityChangedAfterTargetCapture",
                mandatoryRouteIdentityReason);
        }
        if (wallExitFaceContactRequired &&
            wallExitTargetActive &&
            !isGround3MandatoryFaceRoute)
        {
            return FailMandatoryFacePlan(
                state,
                "MandatoryContractGeometryInvalid",
                mandatoryRouteIdentityReason);
        }

        // Contact alone is not vertical stall.  V0.27 issued repeated wall
        // DOWN edges at VY=+17.253, even when passive rise would cross the lip
        // in roughly one physics step.  The manual reference keeps the input
        // released until the player reaches the lip or actually starts to
        // descend.  Apply that invariant to both same-face continuations and
        // newly contacted downstream faces before closing the prior sample.
        if (TryDeferWallPressForResidualRise(
                state,
                contactFeetY,
                wallPhysics,
                "ConfirmedWallContact"))
        {
            return true;
        }

        if (isGround3MandatoryFaceRoute &&
            wallMandatoryFaceInterceptCommitted &&
            contactFeetY < wallRecoveryRequiredReleaseY - 0.01f)
        {
            BonusRunnerLog.Warning(
                $"MandatoryFaceInterceptFailed Reason=" +
                $"ApexOrDescentStillBelowOldLip, Position=" +
                $"({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), FeetY={contactFeetY:F3}, " +
                $"Velocity=({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), ReleaseY=" +
                $"{wallRecoveryRequiredReleaseY:F3}, PlannedContact=" +
                $"[Y={wallMandatoryFacePredictedContactFeetY:F3}," +
                $"VY={wallMandatoryFacePredictedContactVelocityY:F3}," +
                $"T={wallMandatoryFacePredictedContactSeconds:F3}]. " +
                "FailureDomain=ActionDelivery; a third blind DOWN is forbidden.");
            automaticTrajectoryCompatible = false;
            FinishLearningSample(state, "MandatoryFaceInterceptBelowLip");
            ResetAutomaticControlState();
            nextAutomaticAttemptTime = Time.unscaledTime + 0.20f;
            return true;
        }

        float predictedMaximumRise = 0f;
        float strongestSinglePhaseRise = 0f;
        jumpPlanner.ChooseWallRecoveryHold(
            float.MaxValue,
            wallPhysics,
            out strongestSinglePhaseRise,
            MinimumWallRecoveryHoldSeconds,
            MaximumWallRecoveryHoldSeconds);
        // Phase count is deliberately not precomputed. After every released
        // press, observed wall attachment decides whether another bounce is
        // legal. The old height-derived requirement planned two phases but
        // then silently kept the first commitment alive after contact was
        // already lost.
        float phasePlannedRise = plannedWallRise;
        float wallHold = hasKnownWallTarget
            ? jumpPlanner.ChooseWallRecoveryHold(
                phasePlannedRise,
                wallPhysics,
                out predictedMaximumRise,
                MinimumWallRecoveryHoldSeconds,
                MaximumWallRecoveryHoldSeconds)
            : FallbackWallRecoveryHoldSeconds;
        if (!hasKnownWallTarget)
        {
            jumpPlanner.ChooseWallRecoveryHold(
                0f, wallPhysics, out predictedMaximumRise);
        }

        bool wallExitTransferSelected = false;
        bool wallTopLandingSelected = false;
        bool mandatoryFaceSetupSelected = false;
        bool mandatoryFaceContactPulseSelected = false;
        bool wallLandingPredictionSelected = false;
        float wallLandingPredictedX = 0f;
        float wallLandingPredictedTravel = 0f;
        float wallLandingPredictedFlightSeconds = 0f;
        string wallTopPlanSummary = string.Empty;
        wallExitPredictedLandingX = 0f;
        wallExitPredictedTravel = 0f;
        wallExitPredictedFlightSeconds = 0f;
        wallExitPlanSummary = string.Empty;
        wallExitTransferCommitted = false;
        wallLandingFlightCommitted = false;
        float mandatoryFaceLipSeconds = 0f;
        float mandatoryFaceTopClearSeconds = 0f;
        float mandatoryFaceContactSeconds = 0f;
        float mandatoryFaceTopClearFeetY = float.NaN;
        float mandatoryFaceContactFeetY = float.NaN;
        float mandatoryFaceContactVelocityY = float.NaN;
        int mandatorySetupMinimumSteps = 0;
        int mandatorySetupMaximumSteps = 0;
        float mandatorySetupMinimumApexFeetY = float.NaN;
        float mandatorySetupMaximumApexFeetY = float.NaN;
        string mandatoryFacePlanSummary = string.Empty;
        float mandatoryFaceBottomY = wallExitTargetActive
            ? wallExitTarget.Top - Ground3ObjectiveFaceHeight
            : float.NaN;
        float mandatoryFaceMinimumFeetY = mandatoryFaceBottomY +
            MandatoryWallFaceMinimumContactOffset;
        float mandatoryFaceMaximumFeetY = wallExitTargetActive
            ? wallExitTarget.Top -
                MandatoryWallFaceMaximumContactTopClearance
            : float.NaN;
        float mandatoryFacePreferredFeetY = mandatoryFaceBottomY +
            MandatoryWallFacePreferredContactOffset;

        if (isGround3MandatoryFaceRoute)
        {
            BonusBoardSegment currentWall = mandatoryCurrentWall;
            mandatoryFaceContactPulseSelected =
                jumpPlanner.TryChooseWallFaceInterceptHold(
                    state.PlayerPosition.x,
                    contactFeetY,
                    currentWall,
                    wallRecoveryRequiredReleaseY,
                    wallExitTarget,
                    inferredPlayerHalfWidth,
                    GetWallRouteSpeed(),
                    mandatoryFaceMinimumFeetY,
                    mandatoryFaceMaximumFeetY,
                    mandatoryFacePreferredFeetY,
                    mandatoryFacePhysics,
                    MinimumWallLandingHoldSeconds,
                    MaximumWallLandingHoldSeconds,
                    out float faceInterceptHold,
                    out mandatoryFaceLipSeconds,
                    out mandatoryFaceTopClearSeconds,
                    out mandatoryFaceContactSeconds,
                    out mandatoryFaceTopClearFeetY,
                    out mandatoryFaceContactFeetY,
                    out mandatoryFaceContactVelocityY,
                    out mandatoryFacePlanSummary);
            if (mandatoryFaceContactPulseSelected)
            {
                wallHold = faceInterceptHold;
                wallTopLandingSequenceCommitted = false;
                wallLandingPredictionSelected = false;
                predictedMaximumRise = 0f;
                jumpPlanner.ChooseWallRecoveryHold(
                    plannedWallRise,
                    mandatoryFacePhysics,
                    out predictedMaximumRise,
                    faceInterceptHold,
                    faceInterceptHold);
            }
            else if (wallMandatoryFaceSetupActive)
            {
                if (mandatoryFaceSetupPhysicsExpired)
                {
                    return FailMandatoryFacePlan(
                        state,
                        "SetupPhysicsBudgetExpired",
                        $"Position=({state.PlayerPosition.x:F3}," +
                        $"{state.PlayerPosition.y:F3}), FeetY=" +
                        $"{contactFeetY:F3}, Velocity=" +
                        $"({state.PlayerVelocity.x:F3}," +
                        $"{state.PlayerVelocity.y:F3}), FixedStep=" +
                        $"{JumpPhysicsFeedback.FixedStepSequence}, " +
                        $"DeadlineStep=" +
                        $"{wallMandatoryFaceSetupDeadlineFixedStep}, " +
                        $"Candidates[{mandatoryFacePlanSummary}]");
                }
                if (BonusRunnerLog.IsDebugMode &&
                    Time.unscaledTime >= nextMandatoryFaceWindowLogTime)
                {
                    nextMandatoryFaceWindowLogTime =
                        Time.unscaledTime + 0.08f;
                    BonusRunnerLog.Debug(
                        $"MandatoryFaceInterceptWindowWait Position=" +
                        $"({state.PlayerPosition.x:F3}," +
                        $"{state.PlayerPosition.y:F3}), FeetY=" +
                        $"{contactFeetY:F3}, Velocity=" +
                        $"({state.PlayerVelocity.x:F3}," +
                        $"{state.PlayerVelocity.y:F3}), OldWall=" +
                        $"[{currentWall.Left:F3}," +
                        $"{currentWall.Right:F3}]@" +
                        $"{currentWall.Top:F3}, TargetFace=" +
                        $"X={wallExitTarget.Left - inferredPlayerHalfWidth:F3}," +
                        $"Y=[{mandatoryFaceBottomY:F3}," +
                        $"{wallExitTarget.Top:F3}], SafeFeet=" +
                        $"[{mandatoryFaceMinimumFeetY:F3}," +
                        $"{mandatoryFaceMaximumFeetY:F3}], Preferred=" +
                        $"{mandatoryFacePreferredFeetY:F3}, PlayerHeight=" +
                        $"{playerHeight:F3}, RemainingFixedSteps=" +
                        $"{Math.Max(0L, wallMandatoryFaceSetupDeadlineFixedStep - JumpPhysicsFeedback.FixedStepSequence)}, " +
                        $"Candidates[{mandatoryFacePlanSummary}]. " +
                        "Action=UP; wait on the confirmed old face and solve " +
                        "again from the next observed descent frame.",
                        "Recovery");
                }
                wallStallStartedAt =
                    Time.unscaledTime - WallStallConfirmationSeconds;
                jumpController.Release();
                return true;
            }
            else
            {
                mandatoryFaceSetupSelected =
                    jumpPlanner.TryChooseWallFaceSetupHold(
                        contactFeetY,
                        wallRecoveryRequiredReleaseY,
                        mandatoryFacePhysics,
                        MinimumAttachedWallPulseHoldSeconds,
                        MaximumAttachedWallPulseHoldSeconds,
                        MandatoryWallFaceSetupSafetyMargin,
                        out float setupHold,
                        out mandatorySetupMinimumSteps,
                        out mandatorySetupMaximumSteps,
                        out mandatorySetupMinimumApexFeetY,
                        out mandatorySetupMaximumApexFeetY,
                        out string setupSummary);
                mandatoryFacePlanSummary =
                    $"Direct[{mandatoryFacePlanSummary}] Setup[{setupSummary}]";
                if (!mandatoryFaceSetupSelected)
                {
                    BonusRunnerLog.Warning(
                        $"MandatoryFacePlanUnavailable Position=" +
                        $"({state.PlayerPosition.x:F3}," +
                        $"{state.PlayerPosition.y:F3}), FeetY=" +
                        $"{contactFeetY:F3}, Velocity=" +
                        $"({state.PlayerVelocity.x:F3}," +
                        $"{state.PlayerVelocity.y:F3}), ReleaseY=" +
                        $"{wallRecoveryRequiredReleaseY:F3}, FaceWindow=" +
                        $"[{mandatoryFaceMinimumFeetY:F3}," +
                        $"{mandatoryFaceMaximumFeetY:F3}], " +
                        $"Candidates[{mandatoryFacePlanSummary}]. " +
                        "FailureDomain=Planner; no blind fallback pulse is sent.");
                    automaticTrajectoryCompatible = false;
                    FinishLearningSample(
                        state,
                        "MandatoryFacePlanUnavailable");
                    ResetAutomaticControlState();
                    nextAutomaticAttemptTime = Time.unscaledTime + 0.20f;
                    return true;
                }

                wallHold = setupHold;
                wallTopLandingSequenceCommitted = false;
                wallLandingPredictionSelected = false;
                predictedMaximumRise =
                    mandatorySetupMaximumApexFeetY - contactFeetY;
            }
        }
        float holdUntilWallLip = Mathf.Clamp(
            wallPhysics.InputDelaySeconds +
            Mathf.Max(0f, remainingRise - 0.20f) /
                Mathf.Max(1f, wallPhysics.JumpVelocity),
            MinimumWallLandingHoldSeconds,
            MaximumWallLandingHoldSeconds);
        // Any attached wall press can be the exit press. In the captured
        // failure the first press already crossed a four-unit lip; waiting for
        // a nominal "phase two" meant the runner had already flown beyond the
        // wall. Prefer a direct transfer to the authored downstream support.
        if (wallExitTargetActive &&
            !wallExitFaceContactRequired &&
            !wallTopLandingSequenceCommitted &&
            (!isGround3ObjectiveFace || remainingRise <= 2.25f))
        {
            float transferHold;
            wallExitTransferSelected =
                jumpPlanner.TryChooseWallExitTransferHold(
                    state.PlayerPosition.x,
                    contactFeetY,
                    wallRecoveryRequiredReleaseY,
                    wallExitTarget,
                    GetWallRouteSpeed(),
                    wallExitPhysics,
                    wallReleaseTravelBias,
                    MinimumWallLandingHoldSeconds,
                    MaximumWallLandingHoldSeconds,
                    out transferHold,
                    out wallExitPredictedFlightSeconds,
                    out wallExitPredictedTravel,
                    out wallExitPredictedLandingX,
                    out wallExitPlanSummary);

            BonusBoardSegment currentWall = BuildAutomaticTargetSegment();
            bool sectionThreeNarrowWall =
                state.SectionIndex == 3 &&
                string.Equals(
                    currentWall.MapPieceName,
                    "Ground 7",
                    StringComparison.OrdinalIgnoreCase) &&
                currentWall.StaticSurfaceIndex == 2;
            if (!wallExitTransferSelected && sectionThreeNarrowWall)
            {
                string nearestTargetSummary = wallExitPlanSummary;
                BonusBoardSegment[] speedCandidates =
                    platformScanner.GetWallExitLandingCandidates(
                        currentWall,
                        inferredPlayerHalfWidth,
                        postLipHorizontalSpeed);
                foreach (BonusBoardSegment candidate in speedCandidates)
                {
                    bool sameAsCurrentExit =
                        Mathf.Abs(candidate.Left - wallExitTarget.Left) <= 0.12f &&
                        Mathf.Abs(candidate.Right - wallExitTarget.Right) <= 0.12f &&
                        Mathf.Abs(candidate.Top - wallExitTarget.Top) <= 0.12f;
                    if (sameAsCurrentExit)
                        continue;

                    bool candidateSelected =
                        jumpPlanner.TryChooseWallExitTransferHold(
                            state.PlayerPosition.x,
                            contactFeetY,
                            wallRecoveryRequiredReleaseY,
                            candidate,
                            postLipHorizontalSpeed,
                            wallExitPhysics,
                            wallReleaseTravelBias,
                            MinimumWallLandingHoldSeconds,
                            MaximumWallLandingHoldSeconds,
                            out float candidateHold,
                            out float candidateFlight,
                            out float candidateTravel,
                            out float candidateLandingX,
                            out string candidateSummary);
                    nearestTargetSummary +=
                        $" | AlternateTarget=[{candidate.Left:F3}," +
                        $"{candidate.Right:F3}]@{candidate.Top:F3}" +
                        $"[{candidateSummary}]";
                    if (!candidateSelected)
                        continue;

                    if (!ConfigureWallExitRouteContract(
                            currentWall,
                            candidate,
                            "Section3SpeedReachableAlternate"))
                    {
                        continue;
                    }

                    BonusBoardSegment previousExit = wallExitTarget;
                    wallExitTarget = candidate;
                    wallExitTargetActive = true;
                    transferHold = candidateHold;
                    wallExitPredictedFlightSeconds = candidateFlight;
                    wallExitPredictedTravel = candidateTravel;
                    wallExitPredictedLandingX = candidateLandingX;
                    wallExitTransferSelected = true;
                    BonusRunnerLog.Debug(
                        $"WallExitTargetSpeedPromoted Section={state.SectionIndex}, " +
                        $"PostLipVX={postLipHorizontalSpeed:F3}, From=" +
                        $"[{previousExit.Left:F3},{previousExit.Right:F3}]" +
                        $"@{previousExit.Top:F3}, To=[{candidate.Left:F3}," +
                        $"{candidate.Right:F3}]@{candidate.Top:F3}, Hold=" +
                        $"{candidateHold:F3}s, PredictedLanding=" +
                        $"{candidateLandingX:F3}. The nearest platform has no " +
                        "safe hold at the current speed; the first statically " +
                        "reachable downstream support becomes authoritative.",
                        "Routing");
                    break;
                }
                wallExitPlanSummary = nearestTargetSummary;
            }
            if (wallExitTransferSelected)
            {
                wallHold = transferHold;
                wallLandingPredictionSelected = true;
                wallLandingPredictedX = wallExitPredictedLandingX;
                wallLandingPredictedTravel = wallExitPredictedTravel;
                wallLandingPredictedFlightSeconds =
                    wallExitPredictedFlightSeconds;
                predictedMaximumRise = 0f;
                jumpPlanner.ChooseWallRecoveryHold(
                    plannedWallRise,
                    wallPhysics,
                    out predictedMaximumRise,
                    transferHold,
                    transferHold);
            }
        }

        // The next platform is not always reachable directly. Ground 5's
        // first four-unit box is followed by a real gap: a 0.12s wall press
        // landed just beyond the box at x=502.318, while a shorter press can
        // settle safely on the box and let the normal planner handle the next
        // jump. Solve that wall-top landing explicitly before falling back to
        // a blind maximum-rise bounce.
        if (!wallExitTransferSelected &&
            hasKnownWallTarget &&
            !isGround3ObjectiveFace &&
            !wallExitFaceContactRequired)
        {
            BonusBoardSegment wallTopTarget = BuildAutomaticTargetSegment();
            wallTopLandingSelected =
                jumpPlanner.TryChooseWallExitTransferHold(
                    state.PlayerPosition.x,
                    contactFeetY,
                    wallRecoveryRequiredReleaseY,
                    wallTopTarget,
                    GetWallRouteSpeed(),
                    wallExitPhysics,
                    wallReleaseTravelBias,
                    MinimumWallLandingHoldSeconds,
                    MaximumWallLandingHoldSeconds,
                    out float wallTopHold,
                    out wallLandingPredictedFlightSeconds,
                    out wallLandingPredictedTravel,
                    out wallLandingPredictedX,
                    out wallTopPlanSummary);
            if (wallTopLandingSelected)
            {
                wallHold = wallTopHold;
                wallTopLandingSequenceCommitted = true;
                wallLandingPredictionSelected = true;
                predictedMaximumRise = 0f;
                jumpPlanner.ChooseWallRecoveryHold(
                    plannedWallRise,
                    wallPhysics,
                    out predictedMaximumRise,
                    wallTopHold,
                    wallTopHold);
            }
        }

        float wallTopWidth = automaticTargetRight - automaticTargetLeft;
        bool stagedAttachedBounceSelected =
            hasKnownWallTarget &&
            !wallExitFaceContactRequired &&
            (wallTopWidth <= StagedWallTopMaximumWidth ||
             isGround3ObjectiveFace) &&
            !wallExitTransferSelected &&
            !mandatoryFaceContactPulseSelected &&
            (!wallTopLandingSelected || remainingRise > 2.25f);
        if (stagedAttachedBounceSelected)
        {
            // This is not a landing attempt. Its job is to make a controlled
            // attached climb segment
            // toward/along the narrow wall edge, collect the edge spheres,
            // and preserve another wall press. Do this for every intermediate
            // contact, not only the first one. The failed section-two route
            // used 0.060s and released below the lip. Direct observation
            // confirmed that releasing that early loses wall attachment, so
            // each pulse is solved from the remaining rise. The captured
            // manual sequence used two approximately 0.132s pulses followed
            // by a 0.090s pulse, proving that neither pulse count nor weight
            // is fixed. The Ground 3 S2 -> S3 objective route is handled by
            // the separate fixed-step setup/face-intercept solver above.
            wallTopLandingSelected = false;
            wallLandingPredictionSelected = false;
            wallHold = jumpPlanner.ChooseWallRecoveryHold(
                plannedWallRise,
                wallPhysics,
                out predictedMaximumRise,
                MinimumAttachedWallPulseHoldSeconds,
                MaximumAttachedWallPulseHoldSeconds);
            wallTopLandingSequenceCommitted = true;
        }

        // The generic hold-to-lip floor is only a fallback for a blind attached
        // climb. Target-aware landing prediction already includes the airborne
        // continuation after release, and staged bounces deliberately preserve
        // a second wall press. Overriding either hold caused the observed
        // 0.090s safe wall-top plan to become 0.180s and overshoot the target.
        if (hasKnownWallTarget &&
            !wallLandingPredictionSelected &&
            !stagedAttachedBounceSelected &&
            !mandatoryFaceSetupSelected &&
            !mandatoryFaceContactPulseSelected)
            wallHold = Mathf.Max(wallHold, holdUntilWallLip);

        BonusRunnerLog.Debug(
            $"WallRecoveryPlan CurrentY={state.PlayerPosition.y:F3}, " +
            $"ContactFeetY={contactFeetY:F3}, " +
            $"TargetKnown={hasKnownWallTarget}, TargetTop={automaticTargetTop:F3}, " +
            $"RemainingRise={remainingRise:F3}, ChosenHold={wallHold:F3}s, " +
            $"HoldUntilLip={holdUntilWallLip:F3}s, " +
            $"SphereObjective[Found={hasWallSpheres},Count={wallSphereCount}," +
            $"MaxY={(hasWallSpheres ? wallSphereMaximumY : float.NaN):F3}," +
            $"RequiredRise={sphereRequiredRise:F3}], " +
            $"PlannedRise={plannedWallRise:F3}, " +
            $"PhasePlannedRise={phasePlannedRise:F3}, " +
            $"ReleaseY={wallRecoveryRequiredReleaseY:F3}, " +
            $"PhasePolicy=DynamicPostBounce, " +
            $"PriorBounces={wallRecoveryAttempts}, " +
            $"StrongestSinglePhaseRise={strongestSinglePhaseRise:F3}, " +
            $"PredictedMaxRise={predictedMaximumRise:F3}, " +
            $"ExitTarget={(wallExitTargetActive ? $"[{wallExitTarget.Left:F3},{wallExitTarget.Right:F3}]@{wallExitTarget.Top:F3}" : "None")}, " +
            $"ExitTransferSelected={wallExitTransferSelected}, " +
            $"WallTopLandingSelected={wallTopLandingSelected}, " +
            $"MandatoryFaceContact={wallExitFaceContactRequired}, " +
            $"MandatoryFaceSetup={mandatoryFaceSetupSelected}, " +
            $"MandatoryFaceContactPulse={mandatoryFaceContactPulseSelected}, " +
            $"MandatoryFaceState[SetupActive=" +
            $"{wallMandatoryFaceSetupActive},InterceptCommitted=" +
            $"{wallMandatoryFaceInterceptCommitted}], " +
            $"MandatoryFaceGeometry[Bottom={mandatoryFaceBottomY:F3}," +
            $"SafeFeet=[{mandatoryFaceMinimumFeetY:F3}," +
            $"{mandatoryFaceMaximumFeetY:F3}],Preferred=" +
            $"{mandatoryFacePreferredFeetY:F3},PlayerHeight=" +
            $"{playerHeight:F3}], " +
            $"MandatorySetupPrediction[HeldSteps=" +
            $"[{mandatorySetupMinimumSteps},{mandatorySetupMaximumSteps}]," +
            $"Apex=[{mandatorySetupMinimumApexFeetY:F3}," +
            $"{mandatorySetupMaximumApexFeetY:F3}]], " +
            $"MandatoryInterceptPrediction[LipT=" +
            $"{mandatoryFaceLipSeconds:F3},ClearT=" +
            $"{mandatoryFaceTopClearSeconds:F3},ClearY=" +
            $"{mandatoryFaceTopClearFeetY:F3},ContactT=" +
            $"{mandatoryFaceContactSeconds:F3},ContactY=" +
            $"{mandatoryFaceContactFeetY:F3},ContactVY=" +
            $"{mandatoryFaceContactVelocityY:F3}], " +
            $"Ground3ObjectiveFace={isGround3ObjectiveFace}, " +
            $"WallTopSequenceCommitted={wallTopLandingSequenceCommitted}, " +
            $"StagedAttachedBounce={stagedAttachedBounceSelected}, " +
            $"StagedHoldRange=" +
            $"{(stagedAttachedBounceSelected ? "0.075-0.135" : "NotApplicable")}, " +
            $"WallExitKinematics[AttachedVX=" +
            $"{Mathf.Abs(state.PlayerVelocity.x):F3},PostLipVX=" +
            $"{postLipHorizontalSpeed:F3},ReleaseTravelBias=" +
            $"{wallReleaseTravelBias:F3},BaseVX=" +
            $"{wallExitPhysics.BaseHorizontalSpeed:F3},SectionCruiseVX=" +
            $"{(sectionCruiseHorizontalSpeed > 1f ? sectionCruiseHorizontalSpeed.ToString("F3") : "Unresolved")},SpeedMode=" +
            $"{(state.IsActiveGameplay && !state.SpiritBoostEnabled ? "SectionCruiseFloor" : "TransientDecay")},FlightScale=" +
            $"{wallExitPhysics.FlightTimeScale:F3}], " +
            $"ExitPrediction={(wallExitTransferSelected ? $"X={wallExitPredictedLandingX:F3},D={wallExitPredictedTravel:F3},T={wallExitPredictedFlightSeconds:F3}" : "Unavailable")}, " +
            $"ExitCandidates[{wallExitPlanSummary}], " +
            $"MandatoryFaceCandidates[{mandatoryFacePlanSummary}], " +
            $"WallTopCandidates[{wallTopPlanSummary}]; " +
            $"Physics[{wallPhysics.Summary}], ExitPhysics=" +
            $"[{wallExitPhysics.Summary}], FacePhysics=" +
            $"[{mandatoryFacePhysics.Summary}]",
            "Recovery");

        float phaseSeparationSeconds =
            wallRecoveryAttempts > 0 &&
            jumpController.LastReleaseAt >= 0f
                ? Mathf.Max(
                    0f,
                    Time.unscaledTime - jumpController.LastReleaseAt)
                : float.NaN;
        if (learningSampleActive)
        {
            FinishLearningSample(
                state,
                wallRecoveryImpulseConfirmed
                    ? "WallRecoveryPhaseComplete"
                    : "WallRecoveryRetry");
        }
        int mandatoryFixedStepHoldLimit =
            mandatoryFaceSetupSelected ||
            mandatoryFaceContactPulseSelected
                ? Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        wallHold /
                        Mathf.Clamp(
                            mandatoryFacePhysics.FixedDeltaTime,
                            0.005f,
                            0.05f) -
                        0.0001f))
                : 0;
        ResetWallResidualRiseWait();
        jumpController.Press(
            player,
            wallHold,
            $"WallRecovery: Stall={stalledFor:F2}s, " +
            $"Contact={(inferredContinuousWallContact ? "Kinematic" : "Raycast")}, " +
            $"Collider={wall.ColliderInstanceId}:{wall.ColliderName}",
            mandatoryFixedStepHoldLimit);
        if (!jumpController.IsHoldingJump)
        {
            nextWallRecoveryTime =
                Time.unscaledTime + WallRecoveryCooldownSeconds;
            return false;
        }

        wallExitTransferCommitted = wallExitTransferSelected;
        wallLandingFlightCommitted = wallLandingPredictionSelected;
        if (mandatoryFaceSetupSelected)
        {
            wallMandatoryFaceSetupActive = true;
            wallMandatoryFaceSetupDeadline =
                Time.unscaledTime +
                MandatoryWallFaceSetupMaximumWaitSeconds;
            wallMandatoryFaceSetupDeadlineFixedStep =
                JumpPhysicsFeedback.FixedStepSequence +
                GetPhysicsStepBudget(
                    MandatoryWallFaceSetupMaximumWaitSeconds);
            wallMandatoryFaceInterceptCommitted = false;
            wallMandatoryFaceInterceptStartedAt = 0f;
            wallMandatoryFaceInterceptStartedFixedStep = -1;
            wallMandatoryFaceContactWatchDeadlineFixedStep = -1;
            wallMandatoryFaceTargetContactX = float.NaN;
            wallMandatoryFacePredictedTopClearFeetY = float.NaN;
            wallMandatoryFacePredictedContactFeetY = float.NaN;
            wallMandatoryFacePredictedContactVelocityY = float.NaN;
            wallMandatoryFacePredictedContactSeconds = 0f;
            wallMandatoryFaceReleasePredictedContactFeetY = float.NaN;
            wallMandatoryFaceReleasePredictedContactVelocityY = float.NaN;
            wallMandatoryFaceReleaseFeetY = float.NaN;
            wallMandatoryFaceReleaseVelocityY = float.NaN;
            wallMandatoryFaceContactObservedFixedStep = -1;
        }
        else if (mandatoryFaceContactPulseSelected)
        {
            wallMandatoryFaceSetupActive = false;
            wallMandatoryFaceSetupDeadline = 0f;
            wallMandatoryFaceSetupDeadlineFixedStep = -1;
            wallMandatoryFaceInterceptCommitted = true;
            wallMandatoryFaceInterceptStartedAt = Time.unscaledTime;
            wallMandatoryFaceInterceptStartedFixedStep =
                JumpPhysicsFeedback.FixedStepSequence;
            wallMandatoryFaceContactWatchDeadlineFixedStep =
                wallMandatoryFaceInterceptStartedFixedStep +
                GetPhysicsStepBudget(
                    MandatoryWallFaceContactWatchSeconds);
            wallMandatoryFaceTargetContactX =
                wallExitTarget.Left - inferredPlayerHalfWidth;
            wallMandatoryFacePredictedTopClearFeetY =
                mandatoryFaceTopClearFeetY;
            wallMandatoryFacePredictedContactFeetY =
                mandatoryFaceContactFeetY;
            wallMandatoryFacePredictedContactVelocityY =
                mandatoryFaceContactVelocityY;
            wallMandatoryFacePredictedContactSeconds =
                mandatoryFaceContactSeconds;
            wallMandatoryFaceReleasePredictedContactFeetY = float.NaN;
            wallMandatoryFaceReleasePredictedContactVelocityY = float.NaN;
            wallMandatoryFaceReleaseFeetY = float.NaN;
            wallMandatoryFaceReleaseVelocityY = float.NaN;
            wallMandatoryFaceContactObservedFixedStep = -1;
        }
        wallReleaseObservedFixedStep = -1;
        wallDetachedLastFixedStep = -1;
        wallDetachedConfirmationSteps = 0;
        passiveWallApproachActive = false;
        wallRecoveryAttempts++;
        wallActionPhase = wallRecoveryAttempts == 1
            ? WallActionPhase.WallJumpPhaseOne
            : WallActionPhase.WallJumpPhaseTwo;
        wallRecoveryContactLatched = true;
        wallRecoverySawUpwardMotion = false;
        wallRecoveryContactX = state.PlayerPosition.x;
        wallRecoveryContactY = state.PlayerPosition.y;
        wallStallStartedAt = -1f;
        nextWallRecoveryTime =
            Time.unscaledTime + WallRecoveryCooldownSeconds;
        automaticJumpArmed = false;
        airborneAfterAutomaticJump = true;
        automaticJumpRequestedAt = Time.unscaledTime;
        automaticJumpVelocityConfirmed = false;
        automaticPredictionActive = true;
        automaticPlanReason =
            $"WallRecoveryPhase{wallRecoveryAttempts}" +
            (wallExitTransferSelected
                ? ":ExitTransfer"
                : wallTopLandingSelected
                    ? ":WallTopLanding"
                    : mandatoryFaceSetupSelected
                        ? ":MandatoryFaceSetup"
                    : mandatoryFaceContactPulseSelected
                        ? ":MandatoryFaceIntercept"
                    : stagedAttachedBounceSelected
                        ? ":StagedAttachedBounce"
                    : ":AttachedClimb");
        // The approach press and the attached climb press have different
        // release rules. Leaving this as ApproachJumpThenWallJump caused
        // MonitorCommittedJump to see the same wall and release the actual
        // climb press on its very first render frame (0.009s in the captured
        // run instead of the planned 0.120s).
        automaticManeuver = BonusManeuverKind.WallJumpClimb;
        StartLearningSample(
            state,
            "Automatic",
            learnGroundKinematics: false);
        automaticAttemptId = learningSampleId;
        wallRecoveryImpulseStartY = state.PlayerPosition.y;
        wallRecoveryImpulseStartVelocityY = state.PlayerVelocity.y;
        wallRecoveryLastObservedY = state.PlayerPosition.y;
        nextWallClimbFrameLogTime = 0f;
        wallRecoveryUpwardPhysicsSteps = 0;
        wallRecoveryImpulseConfirmed = false;
        wallRecoveryImpulseFailureLogged = false;
        wallRecoveryPrematureReleaseLogged = false;
        wallRecoveryLipCrossed = false;
        learningInputUpTime =
            learningInputDownTime + wallHold;
        learningInputReleased = true;
        automaticPlannedHold = wallHold;
        automaticPlanPhysicsSnapshot =
            mandatoryFaceSetupSelected ||
            mandatoryFaceContactPulseSelected
                ? mandatoryFacePhysics
                : wallExitTransferSelected
                    ? wallExitPhysics
                : wallPhysics;
        automaticPhysicsRevision = automaticPlanPhysicsSnapshot.ModelRevision;
        automaticPlanTriggerPosition = state.PlayerPosition;
        automaticPlannedLaunchX = state.PlayerPosition.x;
        automaticLaunchWindowLeft = state.PlayerPosition.x;
        automaticLaunchWindowRight = state.PlayerPosition.x;
        float mandatoryFaceContactCenterX =
            mandatoryFaceContactPulseSelected
                ? wallExitTarget.Left - inferredPlayerHalfWidth
                : 0f;
        float mandatoryFaceContactTravel =
            mandatoryFaceContactPulseSelected
                ? Mathf.Max(
                    0f,
                    mandatoryFaceContactCenterX -
                        state.PlayerPosition.x)
                : 0f;
        automaticTargetHeightDelta =
            mandatoryFaceContactPulseSelected
                ? mandatoryFaceContactFeetY - contactFeetY
                : wallExitTransferSelected
                    ? wallExitTarget.Top - contactFeetY
                    : remainingRise;
        automaticPredictedHorizontalTravel =
            mandatoryFaceContactPulseSelected
                ? mandatoryFaceContactTravel
                : wallLandingPredictionSelected
            ? wallLandingPredictedTravel
            : Mathf.Max(
                0f,
                automaticTargetSafeLeft - state.PlayerPosition.x);
        automaticPredictedLandingX =
            mandatoryFaceContactPulseSelected
                ? mandatoryFaceContactCenterX
                : wallLandingPredictionSelected
            ? wallLandingPredictedX
            : automaticTargetSafeLeft;
        automaticPredictionLaunchFeetY = contactFeetY;
        automaticPlannedTravelScale =
            automaticPlanPhysicsSnapshot.HorizontalTravelScale;
        automaticPredictedFlightSeconds =
            mandatoryFaceContactPulseSelected
                ? mandatoryFaceContactSeconds
                : wallLandingPredictionSelected
            ? wallLandingPredictedFlightSeconds
            : jumpPlanner.PredictRawInputToLandingSeconds(
                wallHold,
                remainingRise,
                wallPhysics);
        if (automaticPredictedFlightSeconds <= 0.05f)
            automaticPredictedFlightSeconds = 1.0f;
        automaticTrajectoryCompatible = true;
        wallRecoveryCommitmentUntil = Time.unscaledTime + Mathf.Clamp(
            automaticPredictedFlightSeconds + 0.40f,
            0.80f,
            1.50f);
        nextTrajectoryMonitorLogTime = 0f;
        ClearSecondStagePreview();
        ClearRoutePlanLock();

        BonusRunnerLog.Debug(
            $"WallRecoveryDecision Attempt={wallRecoveryAttempts}/" +
            $"{MaximumWallRecoveriesPerAirborneSequence}, " +
            $"ActionPhase={wallActionPhase}, " +
            $"TriggerMode={(plannedBodyContactReady ? "BodyContact" : "ConfirmedStall")}, " +
            $"ContactEvidence={(inferredContinuousWallContact ? "Kinematic" : "Raycast")}, " +
            $"PhaseSeparation={(float.IsNaN(phaseSeparationSeconds) ? "FirstPhase" : $"{phaseSeparationSeconds:F3}s")}, " +
            $"Hold={wallHold:F3}s, X={state.PlayerPosition.x:F3}, " +
            $"Y={state.PlayerPosition.y:F3}, FeetY={contactFeetY:F3}, " +
            $"RemainingRise={remainingRise:F3}, PredictedMaxRise={predictedMaximumRise:F3}, " +
            $"PredictedLandingTime={automaticPredictedFlightSeconds:F3}s, " +
            $"PredictedLandingX={automaticPredictedLandingX:F3}, " +
            $"ExitTransfer={wallExitTransferSelected}, " +
            $"WallTopLanding={wallTopLandingSelected}, " +
            $"MandatoryFaceSetup={mandatoryFaceSetupSelected}, " +
            $"MandatoryFaceIntercept={mandatoryFaceContactPulseSelected}, " +
            $"MandatoryFacePrediction[ClearY=" +
            $"{mandatoryFaceTopClearFeetY:F3},ContactY=" +
            $"{mandatoryFaceContactFeetY:F3},ContactVY=" +
            $"{mandatoryFaceContactVelocityY:F3},ContactT=" +
            $"{mandatoryFaceContactSeconds:F3}], " +
            $"CommitUntil={wallRecoveryCommitmentUntil:F3}, " +
            $"Collider={wall.ColliderInstanceId}:{wall.ColliderName}.",
            "Recovery");
        return true;
    }

    [HideFromIl2Cpp]
    private bool TryManageAttachedObjectiveDescent(
        BonusStageState state,
        BonusWallContact wall,
        float contactFeetY,
        int capturedObjectiveCount = 0,
        float capturedObjectiveMinimumY = float.NaN,
        float capturedObjectiveMaximumY = float.NaN)
    {
        bool isGround3 = string.Equals(
            automaticTargetMapPieceName,
            "Ground 3",
            StringComparison.OrdinalIgnoreCase);
        bool isSecondRaisedFace =
            automaticTargetStaticSurfaceIndex == 3 ||
            automaticTargetStaticSurfaceIndex < 0 &&
            !float.IsNaN(automaticTargetMapPieceOriginX) &&
            Mathf.Abs(
                automaticTargetLeft -
                (automaticTargetMapPieceOriginX + 1f)) <= 0.30f;
        if (state.SectionIndex != 1 || !isGround3 || !isSecondRaisedFace)
        {
            return false;
        }

        float mappedFaceBottomY =
            automaticTargetTop - Ground3ObjectiveFaceHeight;
        bool raycastContact =
            wall.IsDetected &&
            wall.IsTouching &&
            Mathf.Abs(wall.FaceX - automaticTargetLeft) <=
                MandatoryWallFaceRayXMatchTolerance &&
            wall.Normal.x <= -0.25f &&
            contactFeetY >= mappedFaceBottomY - 0.25f &&
            contactFeetY <= automaticTargetTop + 0.45f;
        bool kinematicContact =
            attachedObjectiveDescentActive &&
            Mathf.Abs(state.PlayerVelocity.x) <=
                WallAttachmentVelocityTolerance + 0.75f &&
            state.PlayerPosition.x >= automaticTargetLeft - 1.05f &&
            state.PlayerPosition.x <= automaticTargetLeft + 0.30f &&
            contactFeetY <= automaticTargetTop + 0.45f;
        if (!raycastContact && !kinematicContact)
        {
            if (!attachedObjectiveDescentActive)
                return false;

            // The kinematic fallback above already covers a one-frame raycast
            // miss while the body remains slow and inside the mapped face
            // corridor. If both tests fail, this is real detachment evidence,
            // not permission to keep releasing input for the rest of the
            // 0.90-second descent window. Relinquish exclusive descent
            // ownership immediately so a newly observed wall contact can
            // authorize a fresh pulse and the pit lifecycle guard can act if
            // no recoverable face remains.
            BonusRunnerLog.Warning(
                $"AttachedObjectiveDescentAborted Reason=ContactLost, " +
                $"Position=({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), FeetY={contactFeetY:F3}, " +
                $"TargetFeetY={attachedObjectiveDescentTargetFeetY:F3}, " +
                $"Velocity=({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), TargetFaceX=" +
                $"{automaticTargetLeft:F3}, RayDetected={wall.IsDetected}, " +
                $"RayTouching={wall.IsTouching}. " +
                "ExpectedResult=remain attached during released descent; " +
                "ActualResult=neither raycast nor near-face kinematics " +
                "confirm attachment. FailureDomain=Execution; " +
                "NextBehavior=await a new physical contact without " +
                "suppressing lifecycle or airborne control.");
            attachedObjectiveDescentActive = false;
            attachedObjectiveDescentDeadline = 0f;
            attachedObjectiveDescentDeadlineFixedStep = -1;
            attachedObjectiveDescentSphereCount = 0;
            nextAttachedObjectiveDescentLogTime = 0f;
            wallActionPhase = WallActionPhase.AwaitingWallContact;
            wallStallStartedAt = -1f;
            nextWallRecoveryTime = 0f;
            passiveWallApproachActive = true;
            jumpController.Release();
            return false;
        }

        if (!attachedObjectiveDescentActive)
        {
            bool hasLiveLaneObjectives =
                BonusStageInspector.TryGetActiveSphereVerticalBounds(
                    automaticTargetLeft - 1.55f,
                    automaticTargetLeft + 0.70f,
                    out int sphereCount,
                    out float minimumSphereY,
                    out float maximumSphereY,
                    out bool sphereScanSucceeded);
            bool hasCapturedLaneObjectives =
                !sphereScanSucceeded &&
                capturedObjectiveCount > 0 &&
                !float.IsNaN(capturedObjectiveMinimumY) &&
                !float.IsNaN(capturedObjectiveMaximumY);
            if (!hasLiveLaneObjectives && hasCapturedLaneObjectives)
            {
                sphereCount = capturedObjectiveCount;
                minimumSphereY = capturedObjectiveMinimumY;
                maximumSphereY = capturedObjectiveMaximumY;
                BonusRunnerLog.Debug(
                    $"AttachedObjectiveDescentUsingCapturedObjectives " +
                    $"Count={sphereCount}, Y=[{minimumSphereY:F3}," +
                    $"{maximumSphereY:F3}]. The live sphere scan was " +
                    "temporarily unavailable on the confirmed-face " +
                    "handoff frame.",
                    "Routing");
            }

            bool hasLaneObjectives =
                hasLiveLaneObjectives || hasCapturedLaneObjectives;
            if (!hasLaneObjectives ||
                minimumSphereY >= contactFeetY - 0.45f)
            {
                return false;
            }

            float knownFaceBottom = automaticTargetTop - 5.0f;
            attachedObjectiveDescentActive = true;
            attachedObjectiveDescentDeadline = Time.unscaledTime + 0.90f;
            attachedObjectiveDescentDeadlineFixedStep =
                JumpPhysicsFeedback.FixedStepSequence +
                GetPhysicsStepBudget(0.90f);
            attachedObjectiveDescentTargetFeetY = Mathf.Max(
                knownFaceBottom + 0.45f,
                minimumSphereY - 0.35f);
            attachedObjectiveDescentSphereCount = sphereCount;
            wallExitObjectiveCountAtCapture = 0;
            wallExitObjectiveMinimumY = float.NaN;
            wallExitObjectiveMaximumY = float.NaN;
            nextAttachedObjectiveDescentLogTime = 0f;
            wallExitTransferCommitted = false;
            wallLandingFlightCommitted = false;
            wallTopLandingSequenceCommitted = false;
            wallRecoveryContactLatched = false;
            wallRecoverySawUpwardMotion = false;
            wallRecoveryLipCrossed = false;
            wallReleaseObservedFixedStep = -1;
            wallDetachedLastFixedStep = -1;
            wallDetachedConfirmationSteps = 0;
            passiveWallApproachActive = true;
            automaticJumpRequestedAt = Time.unscaledTime;
            wallActionPhase = WallActionPhase.AttachedObjectiveDescent;
            wallRecoveryCommitmentUntil =
                attachedObjectiveDescentDeadline + 0.40f;
            automaticPlanReason = "Ground3AttachedObjectiveDescent";
            automaticManeuver =
                BonusManeuverKind.ApproachJumpThenWallJump;
            jumpController.Release();
            if (learningSampleActive && learningSource == "Automatic")
                FinishLearningSample(state, "AttachedObjectiveDescentStarted");

            BonusRunnerLog.Debug(
                $"AttachedObjectiveDescentArmed MapPiece=" +
                $"{automaticTargetMapPieceName}#" +
                $"{automaticTargetMapPieceInstanceId}/S" +
                $"{automaticTargetStaticSurfaceIndex}, FaceX=" +
                $"{automaticTargetLeft:F3}, FaceY=[{knownFaceBottom:F3}," +
                $"{automaticTargetTop:F3}], ContactFeetY=" +
                $"{contactFeetY:F3}, ObjectiveSpheres={sphereCount}, " +
                $"SphereY=[{minimumSphereY:F3},{maximumSphereY:F3}], " +
                $"TargetFeetY={attachedObjectiveDescentTargetFeetY:F3}, " +
                $"Deadline={attachedObjectiveDescentDeadline:F3}, " +
                $"DeadlineFixedStep=" +
                $"{attachedObjectiveDescentDeadlineFixedStep}. " +
                "ExpectedBehavior=remain released and slide down the same " +
                "face through the trench pickups before issuing a new " +
                "wall-jump press.",
                "Routing");
        }

        bool reachedObjectiveBand =
            contactFeetY <= attachedObjectiveDescentTargetFeetY + 0.12f;
        bool deadlineExpired =
            attachedObjectiveDescentDeadlineFixedStep >= 0 &&
            JumpPhysicsFeedback.FixedStepSequence >
                attachedObjectiveDescentDeadlineFixedStep;
        if (!reachedObjectiveBand && !deadlineExpired)
        {
            jumpController.Release();
            passiveWallApproachActive = true;
            wallActionPhase = WallActionPhase.AttachedObjectiveDescent;
            wallStallStartedAt = -1f;
            if (BonusRunnerLog.IsDebugMode &&
                Time.unscaledTime >= nextAttachedObjectiveDescentLogTime)
            {
                nextAttachedObjectiveDescentLogTime =
                    Time.unscaledTime + 0.08f;
                BonusRunnerLog.Debug(
                    $"AttachedObjectiveDescentFrame Position=" +
                    $"({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), FeetY=" +
                    $"{contactFeetY:F3}, TargetFeetY=" +
                    $"{attachedObjectiveDescentTargetFeetY:F3}, " +
                    $"Velocity=({state.PlayerVelocity.x:F3}," +
                    $"{state.PlayerVelocity.y:F3}), WallFaceX=" +
                    $"{wall.FaceX:F3}, RemainingFixedSteps=" +
                    $"{Math.Max(0L, attachedObjectiveDescentDeadlineFixedStep - JumpPhysicsFeedback.FixedStepSequence)}, " +
                    $"ObjectiveSpheresAtArm=" +
                    $"{attachedObjectiveDescentSphereCount}. Action=ReleaseAndSlide.",
                    "Routing");
            }
            return true;
        }

        string completionReason = reachedObjectiveBand
            ? "ObjectiveBandReached"
            : "BoundedDeadlineReached";
        BonusRunnerLog.Debug(
            $"AttachedObjectiveDescentComplete Reason={completionReason}, " +
            $"Position=({state.PlayerPosition.x:F3}," +
            $"{state.PlayerPosition.y:F3}), FeetY={contactFeetY:F3}, " +
            $"TargetFeetY={attachedObjectiveDescentTargetFeetY:F3}, " +
            $"Velocity=({state.PlayerVelocity.x:F3}," +
            $"{state.PlayerVelocity.y:F3}), FaceX={wall.FaceX:F3}. " +
            "NextBehavior=solve a fresh bounded wall pulse from the actual " +
            "low contact state.",
            "Routing");
        attachedObjectiveDescentActive = false;
        attachedObjectiveDescentDeadline = 0f;
        attachedObjectiveDescentDeadlineFixedStep = -1;
        attachedObjectiveDescentSphereCount = 0;
        nextAttachedObjectiveDescentLogTime = 0f;
        wallActionPhase = WallActionPhase.AwaitingNextWallPress;
        wallStallStartedAt =
            Time.unscaledTime - WallStallConfirmationSeconds;
        nextWallRecoveryTime = 0f;
        return false;
    }

    [HideFromIl2Cpp]
    private bool TryDeferWallPressForResidualRise(
        BonusStageState state,
        float contactFeetY,
        JumpPhysicsSnapshot wallPhysics,
        string context)
    {
        float remainingToRelease =
            wallRecoveryRequiredReleaseY - contactFeetY;
        float upwardVelocity = Mathf.Max(0f, state.PlayerVelocity.y);
        float gravity = Mathf.Max(1f, wallPhysics.GravityMagnitude);
        float passiveRise = upwardVelocity * upwardVelocity / (2f * gravity);
        float predictedApexFeetY = contactFeetY + passiveRise;
        bool passiveRiseClearsRelease =
            predictedApexFeetY >=
                wallRecoveryRequiredReleaseY +
                WallResidualRiseSafetyMargin;
        bool sameTargetWait =
            wallResidualRiseWaitActive &&
            Mathf.Abs(
                wallResidualRiseWaitTargetLeft -
                automaticTargetLeft) <= 0.20f;
        bool shouldWait =
            remainingToRelease > 0.01f &&
            state.PlayerVelocity.y >
                WallResidualRiseVelocityThreshold;

        if (!shouldWait)
        {
            if (wallResidualRiseWaitActive)
            {
                string result = remainingToRelease <= 0.01f
                    ? "ReleaseHeightReached"
                    : "ApexOrDescentObserved";
                BonusRunnerLog.Debug(
                    $"WallResidualRiseWaitComplete Result={result}, " +
                    $"Context={context}, Position=" +
                    $"({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), FeetY=" +
                    $"{contactFeetY:F3}, Velocity=" +
                    $"({state.PlayerVelocity.x:F3}," +
                    $"{state.PlayerVelocity.y:F3}), ReleaseY=" +
                    $"{wallRecoveryRequiredReleaseY:F3}, Remaining=" +
                    $"{remainingToRelease:F3}, Waited=" +
                    $"{Time.unscaledTime - wallResidualRiseWaitStartedAt:F3}s, " +
                    $"FixedSteps=" +
                    $"{JumpPhysicsFeedback.FixedStepSequence}. " +
                    "A new DOWN is legal only if contact remains confirmed " +
                    "and the lip has not been reached.",
                    "Recovery");
                if (remainingToRelease > 0.01f)
                {
                    // The prior staged pulse has now completed without
                    // crossing the lip. Re-solving from this observed
                    // apex/descent may legitimately choose the mapped exit;
                    // retaining the old sequence commitment would force the
                    // minimum staged pulse forever and ignore that target.
                    wallTopLandingSequenceCommitted = false;
                }
            }
            ResetWallResidualRiseWait();
            return false;
        }

        if (!sameTargetWait)
        {
            ResetWallResidualRiseWait();
            wallResidualRiseWaitActive = true;
            wallResidualRiseWaitStartedAt = Time.unscaledTime;
            wallResidualRiseWaitStartFeetY = contactFeetY;
            wallResidualRiseWaitTargetLeft = automaticTargetLeft;
            nextWallResidualRiseLogTime = Time.unscaledTime;
            BonusRunnerLog.Debug(
                $"WallResidualRiseWaitStarted Context={context}, Position=" +
                $"({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), FeetY=" +
                $"{contactFeetY:F3}, Velocity=" +
                $"({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), Target=" +
                $"[{automaticTargetLeft:F3}," +
                $"{automaticTargetRight:F3}]@" +
                $"{automaticTargetTop:F3}, ReleaseY=" +
                $"{wallRecoveryRequiredReleaseY:F3}, Remaining=" +
                $"{remainingToRelease:F3}, Gravity={gravity:F3}, " +
                $"PassiveRise={passiveRise:F3}, PredictedApexFeetY=" +
                $"{predictedApexFeetY:F3}, " +
                $"CanReachRelease={passiveRiseClearsRelease}, " +
                $"Attempt={wallRecoveryAttempts}/" +
                $"{MaximumWallRecoveriesPerAirborneSequence}. " +
                "Action=keep UP released; do not reset a live upward " +
                "impulse with another wall DOWN.",
                "Recovery");
        }

        float waited = Time.unscaledTime - wallResidualRiseWaitStartedAt;
        if (waited > WallResidualRiseMaximumWaitSeconds)
        {
            BonusRunnerLog.Warning(
                $"WallResidualRiseWaitExpired Context={context}, Position=" +
                $"({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), FeetY=" +
                $"{contactFeetY:F3}, Velocity=" +
                $"({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), ReleaseY=" +
                $"{wallRecoveryRequiredReleaseY:F3}, Remaining=" +
                $"{remainingToRelease:F3}, Waited={waited:F3}s. " +
                "The bounded wait expired; contact-gated wall planning may " +
                "resume instead of hanging indefinitely.");
            ResetWallResidualRiseWait();
            return false;
        }

        if (BonusRunnerLog.IsDebugMode &&
            Time.unscaledTime >= nextWallResidualRiseLogTime)
        {
            nextWallResidualRiseLogTime = Time.unscaledTime + 0.08f;
            BonusRunnerLog.Debug(
                $"WallResidualRiseWaitFrame Context={context}, Position=" +
                $"({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), FeetY=" +
                $"{contactFeetY:F3}, VelocityY=" +
                $"{state.PlayerVelocity.y:F3}, RiseSinceWait=" +
                $"{contactFeetY - wallResidualRiseWaitStartFeetY:F3}, " +
                $"Remaining={remainingToRelease:F3}, PassiveRise=" +
                $"{passiveRise:F3}, PredictedApexFeetY=" +
                $"{predictedApexFeetY:F3}, " +
                $"CanReachRelease={passiveRiseClearsRelease}, " +
                $"Elapsed={waited:F3}s. Action=NoDown.",
                "Recovery");
        }

        // Horizontal contact was already established by the caller. Preserve
        // that evidence while the independent vertical-state gate waits for
        // either the lip or a real apex/descending sample.
        wallStallStartedAt =
            Time.unscaledTime - WallStallConfirmationSeconds;
        jumpController.Release();
        return true;
    }

    private void ResetWallResidualRiseWait()
    {
        wallResidualRiseWaitActive = false;
        wallResidualRiseWaitStartedAt = 0f;
        wallResidualRiseWaitStartFeetY = 0f;
        wallResidualRiseWaitTargetLeft = 0f;
        nextWallResidualRiseLogTime = 0f;
    }

    private void ResetMandatoryFacePlanState()
    {
        wallMandatoryFaceSetupActive = false;
        wallMandatoryFaceSetupDeadline = 0f;
        wallMandatoryFaceSetupDeadlineFixedStep = -1;
        wallMandatoryFaceInterceptCommitted = false;
        wallMandatoryFaceInterceptStartedAt = 0f;
        wallMandatoryFaceInterceptStartedFixedStep = -1;
        wallMandatoryFaceContactWatchDeadlineFixedStep = -1;
        wallMandatoryFaceTargetContactX = float.NaN;
        wallMandatoryFacePredictedTopClearFeetY = float.NaN;
        wallMandatoryFacePredictedContactFeetY = float.NaN;
        wallMandatoryFacePredictedContactVelocityY = float.NaN;
        wallMandatoryFacePredictedContactSeconds = 0f;
        wallMandatoryFaceReleasePredictedContactFeetY = float.NaN;
        wallMandatoryFaceReleasePredictedContactVelocityY = float.NaN;
        wallMandatoryFaceReleaseFeetY = float.NaN;
        wallMandatoryFaceReleaseVelocityY = float.NaN;
        wallMandatoryFaceContactObservedFixedStep = -1;
        nextMandatoryFaceWindowLogTime = 0f;
    }

    private bool IsCommittedWallClimbActive()
    {
        return learningSampleActive &&
            learningSource == "Automatic" &&
            !wallRecoveryLipCrossed &&
            automaticPlanReason.StartsWith(
                "WallRecovery",
                System.StringComparison.Ordinal) &&
            Time.unscaledTime < wallRecoveryCommitmentUntil;
    }

    // This helper returns managed route records and diagnostic strings via
    // out parameters. AutoBonusRunnerRuntime is injected as an IL2CPP type;
    // ClassInjector cannot convert this managed-only signature.
    [HideFromIl2Cpp]
    private bool HasRecoverableWallAhead(
        BonusStageState state,
        PlayerMovement player,
        bool requirePlannedTarget,
        out BonusWallContact wall,
        out string evidence)
    {
        wall = wallDetector.Detect(player, GetWallRouteSpeed());
        if (!wall.IsDetected)
        {
            evidence = wall.Reason ?? "WallNotDetected";
            return false;
        }
        if (wall.Distance > RecoverableWallProbeDistance)
        {
            evidence =
                $"WallTooFar(Distance={wall.Distance:F3}," +
                $"Limit={RecoverableWallProbeDistance:F3})";
            return false;
        }
        if (!requirePlannedTarget)
        {
            evidence = wall.IsTouching
                ? "ObservedTouchingWall"
                : "ObservedReachableWall";
            return true;
        }

        bool plannedWallRoute =
            passiveWallApproachActive ||
            automaticPredictionActive &&
                (automaticManeuver ==
                    BonusManeuverKind.ApproachJumpThenWallJump ||
                 automaticManeuver ==
                    BonusManeuverKind.EnterTrenchThenWallJump ||
                 automaticManeuver == BonusManeuverKind.WallJumpClimb);
        if (!plannedWallRoute)
        {
            evidence = "DetectedWallHasNoPlannedWallRoute";
            return false;
        }

        bool targetGeometryAvailable =
            automaticTargetRight > automaticTargetLeft + 0.05f;
        bool targetFaceMatches =
            targetGeometryAvailable &&
            wall.FaceX >= automaticTargetLeft - 0.80f &&
            wall.FaceX <= automaticTargetRight + 0.80f &&
            state.PlayerPosition.x <= automaticTargetRight + 0.80f &&
            state.PlayerPosition.y < automaticTargetTop + 0.50f;
        evidence = targetFaceMatches
            ? "PlannedTargetWallMatch"
            : $"DetectedWallTargetMismatch(Target=[{automaticTargetLeft:F3}," +
              $"{automaticTargetRight:F3}]@{automaticTargetTop:F3})";
        return targetFaceMatches;
    }

    private bool UpdateAutomaticPitConfirmation(bool isCandidate)
    {
        if (!isCandidate)
        {
            ResetAutomaticPitConfirmation();
            return false;
        }

        long fixedStep = JumpPhysicsFeedback.FixedStepSequence;
        if (fixedStep != pitDescentCandidateLastFixedStep)
        {
            pitDescentCandidateLastFixedStep = fixedStep;
            pitDescentCandidateFixedSteps++;
        }
        return pitDescentCandidateFixedSteps >=
            PitDescentConfirmationFixedSteps;
    }

    private void ResetAutomaticPitConfirmation()
    {
        pitDescentCandidateLastFixedStep = -1;
        pitDescentCandidateFixedSteps = 0;
    }

    private bool UpdateManualPitConfirmation(bool isCandidate)
    {
        if (!isCandidate)
        {
            ResetManualPitConfirmation();
            return false;
        }

        long fixedStep = JumpPhysicsFeedback.FixedStepSequence;
        if (fixedStep != manualPitCandidateLastFixedStep)
        {
            manualPitCandidateLastFixedStep = fixedStep;
            manualPitCandidateFixedSteps++;
        }
        return manualPitCandidateFixedSteps >=
            PitDescentConfirmationFixedSteps;
    }

    private void ResetManualPitConfirmation()
    {
        manualPitCandidateLastFixedStep = -1;
        manualPitCandidateFixedSteps = 0;
    }

    private void ResetWallRecoveryAfterLanding()
    {
        wallStallStartedAt = -1f;
        wallRecoveryAttempts = 0;
        wallRecoveryContactLatched = false;
        wallRecoverySawUpwardMotion = false;
        wallRecoveryContactX = 0f;
        wallRecoveryContactY = 0f;
        wallRecoveryCommitmentUntil = 0f;
        wallRecoveryImpulseStartY = 0f;
        wallRecoveryImpulseStartVelocityY = 0f;
        wallRecoveryLastObservedY = 0f;
        nextWallClimbFrameLogTime = 0f;
        wallRecoveryUpwardPhysicsSteps = 0;
        wallRecoveryImpulseConfirmed = false;
        wallRecoveryImpulseFailureLogged = false;
        wallRecoveryPrematureReleaseLogged = false;
        wallRecoveryLipCrossed = false;
        wallRecoveryRequiredReleaseY = 0f;
        wallExitTargetActive = false;
        wallExitTarget = default;
        wallExitPredictedLandingX = 0f;
        wallExitPredictedTravel = 0f;
        wallExitPredictedFlightSeconds = 0f;
        wallExitPlanSummary = string.Empty;
        wallExitTransferCommitted = false;
        wallLandingFlightCommitted = false;
        wallTopLandingSequenceCommitted = false;
        wallExitContactWatchActive = false;
        ResetWallResidualRiseWait();
        wallExitFaceContactRequired = false;
        wallExitObjectiveCountAtCapture = 0;
        wallExitObjectiveMinimumY = float.NaN;
        wallExitObjectiveMaximumY = float.NaN;
        ResetMandatoryFacePlanState();
        attachedObjectiveDescentActive = false;
        attachedObjectiveDescentDeadline = 0f;
        attachedObjectiveDescentDeadlineFixedStep = -1;
        attachedObjectiveDescentTargetFeetY = 0f;
        attachedObjectiveDescentSphereCount = 0;
        nextAttachedObjectiveDescentLogTime = 0f;
        wallReleaseObservedFixedStep = -1;
        wallDetachedLastFixedStep = -1;
        wallDetachedConfirmationSteps = 0;
        wallActionPhase = WallActionPhase.None;
        wallRouteSpeedLatched = false;
        wallRouteHorizontalSpeed = 0f;
    }

    private void ResetWallRecoveryState()
    {
        ResetWallRecoveryAfterLanding();
        passiveWallApproachActive = false;
        nextWallRecoveryTime = 0f;
        nextWallProbeLogTime = 0f;
    }

    private void ResetAutomaticControlState()
    {
        automaticJumpArmed = true;
        airborneAfterAutomaticJump = false;
        automaticJumpVelocityConfirmed = false;
        automaticPredictionActive = false;
        activeRouteDecisionId = 0;
        automaticPlanReason = string.Empty;
        automaticManeuver = BonusManeuverKind.None;
        automaticTargetColliderId = 0;
        automaticTargetColliderName = string.Empty;
        automaticTargetMapPieceName = string.Empty;
        automaticTargetMapPieceOriginX = float.NaN;
        automaticTargetMapPieceInstanceId = 0;
        automaticTargetRegistryGeneration = 0;
        automaticTargetStaticSurfaceIndex = -1;
        automaticTargetTop = 0f;
        automaticSphereCountAtPlan = -1;
        automaticExpectedSphereHits = 0;
        automaticSpheresAtPlan = "Unavailable";
        automaticTrajectoryCompatible = false;
        nextDynamicPlanLogTime = 0f;
        noSupportStallStartedAt = -1f;
        nextNoSupportStallLogTime = 0f;
        lastRouteSignature = string.Empty;
        lastBoostRouteSelection = string.Empty;
        ResetWallRecoveryState();
        ClearRoutePlanLock();
        ClearSecondStagePreview();
    }

    private void ClearRoutePlanLock()
    {
        routePlanLocked = false;
        routeLockPhysicsRevision = 0;
        routeLockHazardId = 0;
        lockedRouteTarget = default;
        lockedRoutePlan = default;
    }

    private BonusBoardSegment BuildAutomaticTargetSegment() => new(
        automaticTargetLeft,
        automaticTargetRight,
        automaticTargetTop,
        automaticTargetSafeLeft,
        automaticTargetSafeRight,
        automaticTargetColliderId,
        automaticTargetColliderName,
        automaticTargetMapPieceName,
        automaticTargetMapPieceOriginX,
        automaticTargetMapPieceInstanceId,
        automaticTargetRegistryGeneration,
        automaticTargetStaticSurfaceIndex);

    private static bool StaticOrColliderIdentityMatches(
        BonusBoardSegment live,
        BonusBoardSegment expected)
    {
        // The bonus stage uses one CompositeCollider2D across many prefab
        // seams. A continuous live segment can legitimately change its
        // single static annotation as the probe crosses that seam. Bounds and
        // top are checked independently by every caller, so matching live
        // collider geometry is authoritative and static identity is only an
        // additional signal when both sides can actually provide it.
        if (live.ColliderInstanceId != 0 &&
            expected.ColliderInstanceId != 0 &&
            live.ColliderInstanceId == expected.ColliderInstanceId)
        {
            return true;
        }

        bool liveHasStaticIdentity =
            live.MapPieceInstanceId != 0 &&
            live.StaticSurfaceIndex >= 0;
        bool expectedHasStaticIdentity =
            expected.MapPieceInstanceId != 0 &&
            expected.StaticSurfaceIndex >= 0;
        if (liveHasStaticIdentity && expectedHasStaticIdentity)
        {
            return live.MapPieceInstanceId == expected.MapPieceInstanceId &&
                   live.StaticSurfaceIndex == expected.StaticSurfaceIndex;
        }

        // Identity is unavailable on one side (for example a static preview
        // versus the same live composite surface). Do not turn a verified
        // bounds/top match into a false WrongSupport result.
        return true;
    }

    private void BeginPassiveWallApproach(
        BonusStageState state,
        BonusJumpPlan plan,
        BonusBoardScanResult scan,
        BonusBoardSegment target,
        BonusHazard hazard,
        JumpPhysicsSnapshot planningPhysics)
    {
        ResetWallRecoveryAfterLanding();
        LatchWallRouteSpeed(state, "PassiveWallApproach");
        passiveWallApproachActive = true;
        activeRouteDecisionId = ++nextRouteDecisionId;
        wallActionPhase = WallActionPhase.EnteringTrench;
        automaticJumpArmed = false;
        automaticAttemptId = 0;
        automaticJumpRequestedAt = Time.unscaledTime;
        airborneAfterAutomaticJump = false;
        automaticJumpVelocityConfirmed = false;
        automaticPredictionActive = true;
        automaticPlanReason = plan.Reason;
        automaticManeuver = BonusManeuverKind.EnterTrenchThenWallJump;
        automaticTargetSafeLeft = target.SafeLeft;
        automaticTargetSafeRight = target.SafeRight;
        automaticTargetLeft = target.Left;
        automaticTargetRight = target.Right;
        automaticTargetTop = target.Top;
        automaticTargetColliderId = target.ColliderInstanceId;
        automaticTargetColliderName = target.ColliderName;
        automaticTargetMapPieceName = target.MapPieceName;
        automaticTargetMapPieceOriginX = target.MapPieceOriginX;
        automaticTargetMapPieceInstanceId = target.MapPieceInstanceId;
        automaticTargetRegistryGeneration = target.RegistryGeneration;
        automaticTargetStaticSurfaceIndex = target.StaticSurfaceIndex;
        automaticSourceSegment = scan.Current;
        automaticPredictionLaunchFeetY = scan.Current.Top;
        automaticTargetHeightDelta = target.Top - scan.Current.Top;
        automaticPlanTriggerPosition = state.PlayerPosition;
        automaticPlannedLaunchX = plan.PlannedLaunchX;
        automaticLaunchWindowLeft = plan.LaunchWindowLeft;
        automaticLaunchWindowRight = plan.LaunchWindowRight;
        automaticPredictedFlightSeconds = plan.PredictedFlightSeconds;
        automaticPredictedHorizontalTravel = plan.HorizontalTravel;
        automaticPredictedLandingX = plan.PredictedLandingX;
        automaticPlannedHold = 0f;
        automaticPlanPhysicsSnapshot = planningPhysics;
        automaticPhysicsRevision = planningPhysics.ModelRevision;
        automaticTriggerSpeed = Mathf.Max(
            1f,
            Mathf.Abs(state.PlayerVelocity.x));
        automaticPlannedTravelScale = planningPhysics.HorizontalTravelScale;
        automaticTrajectoryCompatible = true;
        automaticHazardAtPlan = hazard.IsValid
            ? $"[{hazard.Left:F3},{hazard.Right:F3}]@{hazard.Top:F3}," +
              $"Id={hazard.InstanceId},Path={hazard.ComponentPath}"
            : "None";
        automaticSphereCountAtPlan =
            BonusStageInspector.TryGetBonusSphereCount(out int sphereCount)
                ? sphereCount
                : -1;
        automaticExpectedSphereHits = 0;
        automaticSpheresAtPlan = BonusStageInspector.DescribeActiveSpheres(
            state.PlayerPosition.x - 1.0f,
            target.Right + 2.0f);
        ClearRoutePlanLock();
        ClearSecondStagePreview();
        PrepareSecondStagePreview(
            state,
            target,
            Mathf.Clamp(
                plan.PredictedLandingX,
                target.SafeLeft,
                target.SafeRight),
            planningPhysics,
            "WallDropApproach");

        BonusRunnerLog.Debug(
            $"WallDropRouteArmed RouteId={activeRouteDecisionId}, Frame={Time.frameCount}, " +
            $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
            $"Source=[{scan.Current.Left:F3},{scan.Current.Right:F3}]" +
            $"@{scan.Current.Top:F3}, Target=[{target.Left:F3}," +
            $"{target.Right:F3}]@{target.Top:F3}, " +
            $"ArmX={plan.PlannedLaunchX:F3}, " +
            $"ExpectedWallContactX={plan.PredictedLandingX:F3}, " +
            $"ExpectedContactTime={plan.PredictedFlightSeconds:F3}s, " +
            $"Hazard={automaticHazardAtPlan}, " +
            $"RouteSpheres[{automaticSpheresAtPlan}]. No input has been sent; " +
            "waiting for physical wall contact before wall-jump phase one.",
            "Recovery");
    }

    private void MarkAutomaticJumpRequested(
        BonusStageState state,
        BonusJumpPlan plan,
        BonusBoardSegment target,
        BonusBoardScanResult scan,
        BonusHazard hazard,
        JumpPhysicsSnapshot planningPhysics)
    {
        if (!jumpController.IsHoldingJump) return;
        if (plan.Maneuver == BonusManeuverKind.ApproachJumpThenWallJump)
            LatchWallRouteSpeed(state, "ActiveWallApproach");
        activeRouteDecisionId = ++nextRouteDecisionId;
        automaticJumpArmed = false;
        automaticJumpRequestedAt = Time.unscaledTime;
        automaticJumpVelocityConfirmed = false;
        StartLearningSample(
            state,
            "Automatic",
            IsGroundLearningManeuver(plan.Maneuver));
        automaticAttemptId = learningSampleId;
        learningInputUpTime = learningInputDownTime + plan.HoldSeconds;
        learningInputReleased = true;
        automaticPredictionActive = true;
        automaticPredictedLandingX = plan.PredictedLandingX;
        automaticPredictedFlightSeconds = plan.PredictedFlightSeconds;
        automaticPredictedHorizontalTravel = plan.HorizontalTravel;
        automaticPredictionLaunchFeetY = scan.Current.Top;
        automaticPlannedTravelScale = planningPhysics.HorizontalTravelScale;
        automaticPlannedHold = plan.HoldSeconds;
        automaticTriggerSpeed = Mathf.Max(1f, Mathf.Abs(state.PlayerVelocity.x));
        automaticTargetHeightDelta = target.Top - state.PlayerPosition.y;
        automaticPhysicsRevision = planningPhysics.ModelRevision;
        automaticTargetSafeLeft = target.SafeLeft;
        automaticTargetSafeRight = target.SafeRight;
        automaticTargetLeft = target.Left;
        automaticTargetRight = target.Right;
        automaticTargetTop = target.Top;
        automaticTargetColliderId = target.ColliderInstanceId;
        automaticTargetColliderName = target.ColliderName;
        automaticTargetMapPieceName = target.MapPieceName;
        automaticTargetMapPieceOriginX = target.MapPieceOriginX;
        automaticTargetMapPieceInstanceId = target.MapPieceInstanceId;
        automaticTargetRegistryGeneration = target.RegistryGeneration;
        automaticTargetStaticSurfaceIndex = target.StaticSurfaceIndex;
        automaticPlanReason = plan.Reason;
        automaticManeuver = plan.Maneuver;
        if (plan.Maneuver == BonusManeuverKind.ApproachJumpThenWallJump)
            wallActionPhase = WallActionPhase.ApproachJumpInFlight;
        automaticPlanPhysicsSnapshot = planningPhysics;
        automaticTrajectoryCompatible = true;
        nextTrajectoryMonitorLogTime = 0f;
        automaticPlanTriggerPosition = state.PlayerPosition;
        automaticPlannedLaunchX = plan.PlannedLaunchX;
        automaticLaunchWindowLeft = plan.LaunchWindowLeft;
        automaticLaunchWindowRight = plan.LaunchWindowRight;
        automaticSourceSegment = scan.Current;
        automaticHazardAtPlan = hazard.IsValid
            ? $"[{hazard.Left:F3},{hazard.Right:F3}]@{hazard.Top:F3}," +
              $"Id={hazard.InstanceId},Path={hazard.ComponentPath}"
            : "None";
        automaticSphereCountAtPlan =
            BonusStageInspector.TryGetBonusSphereCount(out int sphereCount)
                ? sphereCount
                : -1;
        automaticExpectedSphereHits = plan.ExpectedSphereHits;
        automaticSpheresAtPlan = BonusStageInspector.DescribeActiveSpheres(
            state.PlayerPosition.x - 1.0f,
            Mathf.Max(target.Right, plan.PredictedLandingX) + 2.0f);

        BonusRunnerLog.Debug(
            $"JumpAttemptPlan RouteId={activeRouteDecisionId}, AttemptId={automaticAttemptId}, Frame={Time.frameCount}, " +
            $"Map={state.MapName}, Section={state.SectionIndex}, " +
            $"Maneuver={plan.Maneuver}, Plan={plan.Reason}, " +
            $"Trigger=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"TriggerVelocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
            $"SourceRaw=[{scan.Current.Left:F3},{scan.Current.Right:F3}] " +
            $"SourceSafe=[{scan.Current.SafeLeft:F3},{scan.Current.SafeRight:F3}] " +
            $"SourceTop={scan.Current.Top:F3} SourceCollider=" +
            $"{scan.Current.ColliderInstanceId}:{scan.Current.ColliderName}, " +
            $"TargetRaw=[{target.Left:F3},{target.Right:F3}] " +
            $"TargetSafe=[{target.SafeLeft:F3},{target.SafeRight:F3}] " +
            $"TargetCenter={(target.SafeLeft + target.SafeRight) * 0.5f:F3} " +
            $"TargetSafeWidth={target.SafeRight - target.SafeLeft:F3} " +
            $"TargetTop={target.Top:F3} TargetCollider={target.ColliderInstanceId}:{target.ColliderName}, " +
            $"Gap={scan.Gap:F3}, DeltaY={target.Top - scan.Current.Top:F3}, " +
            $"ScanReason={scan.Reason}, " +
            (scan.HasIntermediate
                ? $"IntermediateRaw=[{scan.Intermediate.Left:F3}," +
                  $"{scan.Intermediate.Right:F3}]@{scan.Intermediate.Top:F3}, "
                : string.Empty) +
            $"Hold={plan.HoldSeconds:F3}s, PredictedFlight={plan.PredictedFlightSeconds:F3}s, " +
            $"PredictedTravel={plan.HorizontalTravel:F3}, PlannedLaunch={plan.PlannedLaunchX:F3}, " +
            $"LaunchWindow=[{plan.LaunchWindowLeft:F3},{plan.LaunchWindowRight:F3}], " +
            $"PredictedLanding=({plan.PredictedLandingX:F3},{target.Top:F3}), " +
            $"Hazard={automaticHazardAtPlan}, " +
            $"SphereCountAtPlan={automaticSphereCountAtPlan}, " +
            $"ExpectedSphereHits={automaticExpectedSphereHits}, " +
            $"RouteSpheres[{automaticSpheresAtPlan}], " +
            $"PhysicsRev={automaticPhysicsRevision}",
            "Attempt");

        secondStageObservedAirborne = false;
        PrepareSecondStagePreview(
            state,
            target,
            plan.PredictedLandingX,
            planningPhysics,
            $"JumpAttempt:{automaticAttemptId}");
    }

    private void PrepareSecondStagePreview(
        BonusStageState state,
        BonusBoardSegment expectedSupport,
        float expectedLandingX,
        JumpPhysicsSnapshot physics,
        string source)
    {
        PlayerMovement player = PlayerMovement.instance;
        if (player == null || expectedSupport.Width <= 0.05f)
        {
            ClearSecondStagePreview();
            return;
        }

        BonusBoardScanResult projectedScan =
            platformScanner.ScanProjectedSupport(
                expectedSupport,
                expectedLandingX,
                player,
                GetWallRouteSpeed());
        projectedScan =
            BonusJumpPlanner.SelectLowerRouteWhenItContinues(projectedScan);
        BonusHazard projectedHazard = hazardScanner.FindNearest(
            new Vector3(expectedLandingX, expectedSupport.Top, 0f));
        BonusJumpPlan projectedPlan = jumpPlanner.Plan(
            projectedScan,
            new Vector3(expectedLandingX, expectedSupport.Top, 0f),
            new Vector2(GetWallRouteSpeed(), 0f),
            physics,
            projectedHazard);

        string signature = projectedScan.IsValid
            ? projectedScan.HasNext
                ? $"{projectedScan.Current.Left:F2}:{projectedScan.Current.Right:F2}:" +
                  $"{projectedScan.Current.Top:F2}>{projectedScan.Next.Left:F2}:" +
                  $"{projectedScan.Next.Right:F2}:{projectedScan.Next.Top:F2}:" +
                  $"{projectedPlan.HoldSeconds:F2}:{projectedPlan.Reason}"
                : $"{projectedScan.Current.Left:F2}:{projectedScan.Current.Right:F2}:NoNext"
            : $"Invalid:{projectedScan.Reason}";
        bool changed = signature != secondStageSignature;

        secondStagePreviewActive = true;
        secondStageExpectedSupport = expectedSupport;
        secondStageExpectedLandingX = expectedLandingX;
        secondStageProjectedScan = projectedScan;
        secondStageProjectedPlan = projectedPlan;
        secondStageSource = source;
        secondStageSignature = signature;
        nextSecondStageRefreshTime = Time.unscaledTime + 0.10f;

        if (changed)
        {
            string next = projectedScan.IsValid && projectedScan.HasNext
                ? $"[{projectedScan.Next.Left:F3},{projectedScan.Next.Right:F3}] " +
                  $"Safe=[{projectedScan.Next.SafeLeft:F3}," +
                  $"{projectedScan.Next.SafeRight:F3}]@{projectedScan.Next.Top:F3}"
                : "Unavailable";
            BonusRunnerLog.Debug(
                $"SecondStagePreview Source={source}, " +
                $"ExpectedLandingX={expectedLandingX:F3}, " +
                $"ExpectedSupport=[{expectedSupport.Left:F3}," +
                $"{expectedSupport.Right:F3}]@{expectedSupport.Top:F3}, " +
                $"ProjectedCurrent=" +
                $"[{projectedScan.Current.Left:F3},{projectedScan.Current.Right:F3}]" +
                $"@{projectedScan.Current.Top:F3}, Next={next}, " +
                $"PreparedAction={(projectedPlan.ShouldJumpNow ? "Jump" : "Wait")}, " +
                $"PreparedHold={projectedPlan.HoldSeconds:F3}s, " +
                $"PreparedWindow=[{projectedPlan.LaunchWindowLeft:F3}," +
                $"{projectedPlan.LaunchWindowRight:F3}], " +
                $"PreparedLanding={projectedPlan.PredictedLandingX:F3}, " +
                $"Reason={projectedPlan.Reason}.",
                "Lookahead");
        }
    }

    private void RefreshSecondStagePreview(
        BonusStageState state,
        PlayerMovement player)
    {
        if (!secondStagePreviewActive || player == null)
            return;

        secondStageObservedAirborne = true;
        if (!automaticTrajectoryCompatible ||
            Time.unscaledTime < nextSecondStageRefreshTime)
            return;

        float previewHorizontalSpeed = Mathf.Abs(state.PlayerVelocity.x);
        if (previewHorizontalSpeed <= 1f || previewHorizontalSpeed >= 80f)
            previewHorizontalSpeed = Mathf.Max(1f, lastReliableHorizontalSpeed);
        JumpPhysicsSnapshot physics = BuildPlanningPhysics(
            state.SectionIndex,
            jumpPhysicsFeedback.CaptureSnapshot(player),
            previewHorizontalSpeed,
            state.IsActiveGameplay && !state.SpiritBoostEnabled);
        PrepareSecondStagePreview(
            state,
            secondStageExpectedSupport,
            secondStageExpectedLandingX,
            physics,
            secondStageSource);
    }

    private void MonitorAutomaticTrajectory(BonusStageState state)
    {
        if (!automaticPredictionActive || !learningTookOff ||
            !automaticTrajectoryCompatible)
            return;

        // A wall action intentionally has zero horizontal motion while
        // attached and resumes forward speed only at the lip. Linear
        // trigger-to-landing interpolation therefore reports a false
        // deviation and drowns the useful WallClimbFrame/landing diagnostics.
        if (automaticManeuver == BonusManeuverKind.WallJumpClimb)
            return;

        float elapsed = Mathf.Max(
            0f,
            Time.unscaledTime - automaticJumpRequestedAt);
        float progress = Mathf.Clamp01(
            elapsed /
            Mathf.Max(0.05f, automaticPredictedFlightSeconds));
        float expectedX = Mathf.Lerp(
            automaticPlanTriggerPosition.x,
            automaticPredictedLandingX,
            progress);
        float expectedY = jumpPlanner.PredictVerticalYAtTime(
            automaticPredictionLaunchFeetY,
            automaticPlannedHold,
            elapsed,
            automaticPlanPhysicsSnapshot);
        float errorX = state.PlayerPosition.x - expectedX;
        float errorY = state.PlayerPosition.y - expectedY;
        bool compatible =
            Mathf.Abs(errorX) <= 0.65f &&
            Mathf.Abs(errorY) <= 0.90f;

        if (Time.unscaledTime >= nextTrajectoryMonitorLogTime || !compatible)
        {
            nextTrajectoryMonitorLogTime = Time.unscaledTime + 0.12f;
            BonusRunnerLog.Debug(
                $"TrajectoryMonitor AttemptId={automaticAttemptId}, " +
                $"Elapsed={elapsed:F3}s, Progress={progress:F3}, " +
                $"Expected=({expectedX:F3},{expectedY:F3}), " +
                $"Actual=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"Error=({errorX:F3},{errorY:F3}), " +
                $"Velocity=({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}), Compatible={compatible}.",
                "Trajectory");
        }

        if (!compatible)
        {
            automaticTrajectoryCompatible = false;
            BonusRunnerLog.Debug(
                $"SecondStageInvalidated AttemptId={automaticAttemptId}, " +
                $"Reason=TrajectoryDeviation, ErrorX={errorX:F3}, " +
                $"ErrorY={errorY:F3}. Landing geometry will be rescanned.",
                "Lookahead");
        }
    }

    private void ConfirmOrRejectSecondStagePreview(
        BonusStageState state,
        BonusBoardScanResult liveScan)
    {
        if (!secondStagePreviewActive || !secondStageObservedAirborne)
            return;

        bool supportMatch = liveScan.IsValid &&
            Mathf.Abs(liveScan.Current.Top -
                secondStageExpectedSupport.Top) <= 0.35f &&
            state.PlayerPosition.x >= secondStageExpectedSupport.Left - 0.15f &&
            state.PlayerPosition.x <= secondStageExpectedSupport.Right + 0.15f;
        float landingError =
            state.PlayerPosition.x - secondStageExpectedLandingX;
        bool landingMatch = Mathf.Abs(landingError) <= 0.55f;
        bool geometryMatch = supportMatch &&
            liveScan.HasNext == secondStageProjectedScan.HasNext;
        if (geometryMatch && liveScan.HasNext)
        {
            geometryMatch =
                Mathf.Abs(liveScan.Next.Left -
                    secondStageProjectedScan.Next.Left) <= 0.25f &&
                Mathf.Abs(liveScan.Next.Right -
                    secondStageProjectedScan.Next.Right) <= 0.25f &&
                Mathf.Abs(liveScan.Next.Top -
                    secondStageProjectedScan.Next.Top) <= 0.35f;
        }

        bool confirmed =
            automaticTrajectoryCompatible &&
            supportMatch && landingMatch && geometryMatch;
        BonusRunnerLog.Debug(
            $"SecondStage{(confirmed ? "Confirmed" : "Rejected")} " +
            $"Source={secondStageSource}, ActualLanding=" +
            $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"ExpectedLandingX={secondStageExpectedLandingX:F3}, " +
            $"LandingErrorX={landingError:F3}, SupportMatch={supportMatch}, " +
            $"GeometryMatch={geometryMatch}, " +
            $"TrajectoryCompatible={automaticTrajectoryCompatible}, " +
            $"PreparedPlan={secondStageProjectedPlan.Reason}. " +
            "The live landing scan is authoritative.",
            "Lookahead");
        ClearSecondStagePreview();
    }

    private void ClearSecondStagePreview()
    {
        secondStagePreviewActive = false;
        secondStageObservedAirborne = false;
        secondStageExpectedSupport = default;
        secondStageProjectedScan = default;
        secondStageProjectedPlan = default;
        secondStageExpectedLandingX = 0f;
        secondStageSource = string.Empty;
        secondStageSignature = string.Empty;
        nextSecondStageRefreshTime = 0f;
    }

    private void CaptureWallExitTargetFromPreview()
    {
        if (wallExitTargetActive ||
            !secondStagePreviewActive ||
            !secondStageProjectedScan.IsValid ||
            !secondStageProjectedScan.HasNext)
        {
            return;
        }

        bool previewDescribesPlannedWall =
            Mathf.Abs(secondStageExpectedSupport.Left -
                automaticTargetLeft) <= 0.25f &&
            Mathf.Abs(secondStageExpectedSupport.Right -
                automaticTargetRight) <= 0.25f &&
            Mathf.Abs(secondStageExpectedSupport.Top -
                automaticTargetTop) <= 0.35f;
        BonusBoardSegment candidate = secondStageProjectedScan.Next;
        if (!previewDescribesPlannedWall ||
            candidate.Right <= automaticTargetRight + 0.10f)
        {
            return;
        }

        BonusBoardSegment completedWall = BuildAutomaticTargetSegment();
        if (!ConfigureWallExitRouteContract(
                completedWall,
                candidate,
                "SecondStagePreview"))
        {
            // Do not publish a rejected preview.  In particular, an
            // unverified Ground 3 S2 successor must not block the static-map
            // capture below and then fall through to the generic top-landing
            // executor.
            wallExitTargetActive = false;
            wallExitTarget = default;
            return;
        }

        wallExitTargetActive = true;
        wallExitTarget = candidate;
        BonusRunnerLog.Debug(
            $"WallExitTargetCaptured Wall=[{automaticTargetLeft:F3}," +
            $"{automaticTargetRight:F3}]@{automaticTargetTop:F3}, " +
            $"Exit=[{candidate.Left:F3},{candidate.Right:F3}] " +
            $"Safe=[{candidate.SafeLeft:F3},{candidate.SafeRight:F3}]" +
            $"@{candidate.Top:F3}, Source={secondStageSource}, " +
            $"MapPiece={candidate.MapPieceName}#" +
            $"{candidate.MapPieceInstanceId}/G{candidate.RegistryGeneration}/" +
            $"S{candidate.StaticSurfaceIndex}.",
            "Recovery");
    }

    private void CaptureChainedWallTargetFromStaticMap(float playerHalfWidth)
    {
        if (wallExitTargetActive)
            return;

        BonusBoardSegment currentWall = BuildAutomaticTargetSegment();
        bool risingChain = platformScanner.TryFindChainedWallStep(
            currentWall,
            playerHalfWidth,
            out BonusBoardSegment candidate);
        if (!risingChain &&
            !platformScanner.TryFindWallRouteContinuation(
                currentWall,
                playerHalfWidth,
                out candidate))
        {
            return;
        }

        bool sameAuthoredPiece =
            currentWall.MapPieceInstanceId != 0 &&
            currentWall.MapPieceInstanceId ==
                candidate.MapPieceInstanceId;
        bool ground3LevelObjectiveFace =
            sameAuthoredPiece &&
            string.Equals(
                currentWall.MapPieceName,
                "Ground 3",
                StringComparison.OrdinalIgnoreCase) &&
            currentWall.StaticSurfaceIndex == 2 &&
            candidate.StaticSurfaceIndex == 3 &&
            Mathf.Abs(candidate.Top - currentWall.Top) <= 0.35f;
        bool ground3DownstreamExit =
            sameAuthoredPiece &&
            string.Equals(
                currentWall.MapPieceName,
                "Ground 3",
                StringComparison.OrdinalIgnoreCase) &&
            currentWall.StaticSurfaceIndex == 3 &&
            candidate.StaticSurfaceIndex == 1 &&
            candidate.Top < currentWall.Top - 0.35f;
        bool ground5DownstreamExit =
            sameAuthoredPiece &&
            string.Equals(
                currentWall.MapPieceName,
                "Ground 5",
                StringComparison.OrdinalIgnoreCase) &&
            currentWall.StaticSurfaceIndex == 4 &&
            candidate.StaticSurfaceIndex == 1 &&
            candidate.Top < currentWall.Top - 0.35f;
        bool knownNonRisingContinuation =
            ground3LevelObjectiveFace ||
            ground3DownstreamExit ||
            ground5DownstreamExit;
        if (!risingChain && !knownNonRisingContinuation)
        {
            BonusRunnerLog.Debug(
                $"WallRouteContinuationRejected Reason=" +
                $"UnknownNonRisingRouteRole, Current=" +
                $"[{currentWall.Left:F3},{currentWall.Right:F3}]@" +
                $"{currentWall.Top:F3}/{currentWall.MapPieceName}#" +
                $"{currentWall.MapPieceInstanceId}/S" +
                $"{currentWall.StaticSurfaceIndex}, Candidate=" +
                $"[{candidate.Left:F3},{candidate.Right:F3}]@" +
                $"{candidate.Top:F3}/{candidate.MapPieceName}#" +
                $"{candidate.MapPieceInstanceId}/S" +
                $"{candidate.StaticSurfaceIndex}. " +
                "Only mapped Ground3 S2->S3, Ground3 S3->S1, and " +
                "Ground5 S4->S1 non-rising wall continuations are legal.",
                "Recovery");
            return;
        }

        if (!ConfigureWallExitRouteContract(
                currentWall,
                candidate,
                risingChain
                    ? "StaticRisingChain"
                    : "StaticForwardContinuation"))
        {
            wallExitTargetActive = false;
            wallExitTarget = default;
            return;
        }

        wallExitTargetActive = true;
        wallExitTarget = candidate;
        BonusRunnerLog.Debug(
            $"WallChainTargetCaptured Source=StaticMap, Role=" +
            $"{(risingChain ? "RisingChain" : ground3LevelObjectiveFace ? "MandatoryLevelContact" : "MappedDownstreamExit")}, Wall=" +
            $"[{currentWall.Left:F3},{currentWall.Right:F3}]" +
            $"@{currentWall.Top:F3}, Next=" +
            $"[{candidate.Left:F3},{candidate.Right:F3}]" +
            $"@{candidate.Top:F3}, Gap=" +
            $"{candidate.Left - currentWall.Right:F3}, Rise=" +
            $"{candidate.Top - currentWall.Top:F3}, MapPiece=" +
            $"{candidate.MapPieceName}#{candidate.MapPieceInstanceId}/" +
            $"G{candidate.RegistryGeneration}/S" +
            $"{candidate.StaticSurfaceIndex}.",
            "Recovery");
    }

    private void CaptureSectionThreeWallExitTargetFromStaticMap(
        float playerHalfWidth)
    {
        if (wallExitTargetActive || latestState.SectionIndex != 3)
            return;

        BonusBoardSegment currentWall = BuildAutomaticTargetSegment();
        bool mappedGround7NarrowWall =
            string.Equals(
                currentWall.MapPieceName,
                "Ground 7",
                StringComparison.OrdinalIgnoreCase) &&
            currentWall.StaticSurfaceIndex == 2;
        if (!mappedGround7NarrowWall)
            return;

        BonusBoardSegment candidate =
            platformScanner.GetWallExitLandingCandidates(
                    currentWall,
                    playerHalfWidth,
                    GetWallRouteSpeed())
                .FirstOrDefault();
        if (candidate.Width < 0.75f ||
            !ConfigureWallExitRouteContract(
                currentWall,
                candidate,
                "Section3StaticExitFallback"))
        {
            return;
        }

        wallExitTargetActive = true;
        wallExitTarget = candidate;
        BonusRunnerLog.Debug(
            $"WallExitTargetCaptured Wall=[{currentWall.Left:F3}," +
            $"{currentWall.Right:F3}]@{currentWall.Top:F3}, Exit=" +
            $"[{candidate.Left:F3},{candidate.Right:F3}] Safe=" +
            $"[{candidate.SafeLeft:F3},{candidate.SafeRight:F3}]" +
            $"@{candidate.Top:F3}, Source=Section3StaticExitFallback, " +
            $"PostLipVX={GetWallRouteSpeed():F3}, MapPiece=" +
            $"{candidate.MapPieceName}#{candidate.MapPieceInstanceId}/G" +
            $"{candidate.RegistryGeneration}/S" +
            $"{candidate.StaticSurfaceIndex}.",
            "Recovery");
    }

    [HideFromIl2Cpp]
    private bool ConfigureWallExitRouteContract(
        BonusBoardSegment completedWall,
        BonusBoardSegment candidate,
        string source)
    {
        wallExitFaceContactRequired = false;
        wallExitObjectiveCountAtCapture = 0;
        wallExitObjectiveMinimumY = float.NaN;
        wallExitObjectiveMaximumY = float.NaN;
        ResetMandatoryFacePlanState();

        bool verifiedMandatoryGeometry =
            IsVerifiedGround3MandatoryFaceGeometry(
                latestState,
                completedWall,
                candidate,
                out string geometryReason);
        int objectiveCount = 0;
        float minimumObjectiveY = float.NaN;
        float maximumObjectiveY = float.NaN;
        bool hasLowerLaneObjectives =
            verifiedMandatoryGeometry &&
            BonusStageInspector.TryGetActiveSphereVerticalBounds(
                candidate.Left - 1.55f,
                candidate.Left + 0.70f,
                out objectiveCount,
                out minimumObjectiveY,
                out maximumObjectiveY) &&
            minimumObjectiveY < candidate.Top - 0.45f;
        if (!verifiedMandatoryGeometry &&
            string.Equals(
                completedWall.MapPieceName,
                "Ground 3",
                StringComparison.OrdinalIgnoreCase) &&
            completedWall.StaticSurfaceIndex == 2)
        {
            BonusRunnerLog.Debug(
                $"WallExitRouteContractRejected Source={source}, " +
                $"Reason={geometryReason}, Completed=" +
                $"{completedWall.MapPieceName}#" +
                $"{completedWall.MapPieceInstanceId}/G" +
                $"{completedWall.RegistryGeneration}/S" +
                $"{completedWall.StaticSurfaceIndex}@" +
                $"[{completedWall.Left:F3}," +
                $"{completedWall.Right:F3}]/Y{completedWall.Top:F3}, " +
                $"Candidate={candidate.MapPieceName}#" +
                $"{candidate.MapPieceInstanceId}/G" +
                $"{candidate.RegistryGeneration}/S" +
                $"{candidate.StaticSurfaceIndex}@" +
                $"[{candidate.Left:F3},{candidate.Right:F3}]/" +
                $"Y{candidate.Top:F3}. Candidate is not published; the " +
                "static-map resolver may replace it in the same frame.",
                "Routing");
            return false;
        }

        // A verified S2 -> S3 route with no remaining lower-lane objective is
        // still a valid optional/top route.  Other mapped wall continuations
        // also remain valid without the mandatory face-contact contract.
        if (!hasLowerLaneObjectives)
            return true;

        wallExitFaceContactRequired = true;
        wallExitObjectiveCountAtCapture = objectiveCount;
        wallExitObjectiveMinimumY = minimumObjectiveY;
        wallExitObjectiveMaximumY = maximumObjectiveY;
        BonusRunnerLog.Debug(
            $"WallExitRouteContract Source={source}, Contract=" +
            $"MandatoryFaceContact, Completed=" +
            $"[{completedWall.Left:F3},{completedWall.Right:F3}]@" +
            $"{completedWall.Top:F3}/S{completedWall.StaticSurfaceIndex}, " +
            $"Target=[{candidate.Left:F3},{candidate.Right:F3}]@" +
            $"{candidate.Top:F3}/S{candidate.StaticSurfaceIndex}, " +
            $"ObjectiveCount={objectiveCount}, ObjectiveY=" +
            $"[{minimumObjectiveY:F3},{maximumObjectiveY:F3}]. " +
            "A top landing is not route success while the lower objective " +
            "lane remains active; physical face contact must precede the " +
            "attached descent.",
            "Routing");
        return true;
    }

    private bool TryArmWallExitContactWatch(
        BonusStageState state,
        string source)
    {
        if (!wallExitTargetActive)
            return false;

        BonusBoardSegment completedWall = BuildAutomaticTargetSegment();
        float gap = wallExitTarget.Left - completedWall.Right;
        float rise = wallExitTarget.Top - completedWall.Top;
        bool canBecomeContactTarget =
            gap >= 0.10f && gap <= 5.25f &&
            rise >= -0.35f && rise <= 7.50f &&
            state.PlayerPosition.x < wallExitTarget.Left + 0.20f;
        if (!canBecomeContactTarget)
            return false;

        wallExitContactWatchActive = true;
        wallRecoveryCommitmentUntil = Time.unscaledTime + 1.25f;
        if (wallMandatoryFaceInterceptCommitted &&
            wallExitFaceContactRequired)
        {
            wallMandatoryFaceContactWatchDeadlineFixedStep =
                JumpPhysicsFeedback.FixedStepSequence +
                GetPhysicsStepBudget(
                    MandatoryWallFaceContactWatchSeconds);
        }
        BonusRunnerLog.Debug(
            $"WallExitContactWatchArmed Source={source}, Position=" +
            $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Completed=[{completedWall.Left:F3}," +
            $"{completedWall.Right:F3}]@{completedWall.Top:F3}, " +
            $"Exit=[{wallExitTarget.Left:F3}," +
            $"{wallExitTarget.Right:F3}]@{wallExitTarget.Top:F3}, " +
            $"Gap={gap:F3}, Rise={rise:F3}, Deadline=" +
            $"{wallRecoveryCommitmentUntil:F3}, ContactRequired=" +
            $"{wallExitFaceContactRequired}, DeadlineFixedStep=" +
            $"{wallMandatoryFaceContactWatchDeadlineFixedStep}, " +
            $"ObjectiveCount=" +
            $"{wallExitObjectiveCountAtCapture}. " +
            (wallExitFaceContactRequired
                ? "A top landing would skip the active lower objective lane; " +
                  "physical face contact is mandatory."
                : "Safe landing is allowed; physical contact with this face " +
                  "will re-enter wall climbing."),
            "Recovery");
        return true;
    }

    private bool TryPromoteChainedWallTarget(
        BonusStageState state,
        bool allowLevelFace = false)
    {
        if (!wallExitTargetActive)
            return false;

        BonusBoardSegment completedWall = BuildAutomaticTargetSegment();
        BonusBoardSegment nextWall = wallExitTarget;
        bool satisfiedMandatoryContact = wallExitFaceContactRequired;
        float gap = nextWall.Left - completedWall.Right;
        float rise = nextWall.Top - completedWall.Top;
        float minimumRise = allowLevelFace ? -0.35f : 0.30f;
        bool isForwardRisingStep =
            gap >= 0.10f && gap <= 5.25f &&
            rise >= minimumRise && rise <= 7.50f &&
            state.PlayerPosition.x < nextWall.Left + 0.20f;
        if (!isForwardRisingStep)
            return false;

        if (learningSampleActive)
            FinishLearningSample(state, "WallPulseChainTransfer");

        wallExitTargetActive = false;
        wallExitTarget = default;
        wallExitFaceContactRequired = false;
        wallExitObjectiveCountAtCapture = 0;
        wallExitObjectiveMinimumY = float.NaN;
        wallExitObjectiveMaximumY = float.NaN;
        ResetMandatoryFacePlanState();
        wallExitContactWatchActive = false;
        automaticTargetSafeLeft = nextWall.SafeLeft;
        automaticTargetSafeRight = nextWall.SafeRight;
        automaticTargetLeft = nextWall.Left;
        automaticTargetRight = nextWall.Right;
        automaticTargetTop = nextWall.Top;
        automaticTargetColliderId = nextWall.ColliderInstanceId;
        automaticTargetColliderName = nextWall.ColliderName;
        automaticTargetMapPieceName = nextWall.MapPieceName;
        automaticTargetMapPieceOriginX = nextWall.MapPieceOriginX;
        automaticTargetMapPieceInstanceId = nextWall.MapPieceInstanceId;
        automaticTargetRegistryGeneration = nextWall.RegistryGeneration;
        automaticTargetStaticSurfaceIndex = nextWall.StaticSurfaceIndex;
        automaticTargetHeightDelta =
            nextWall.Top - state.PlayerPosition.y;

        // Keep the route-wide pulse count. Promoting a new face changes the
        // contact target, not the bounded climb action.
        wallRecoveryContactLatched = false;
        wallRecoverySawUpwardMotion = false;
        wallRecoveryLipCrossed = false;
        wallRecoveryRequiredReleaseY = nextWall.Top - 0.20f;
        wallRecoveryCommitmentUntil = Time.unscaledTime + 1.50f;
        wallReleaseObservedFixedStep = -1;
        wallDetachedLastFixedStep = -1;
        wallDetachedConfirmationSteps = 0;
        wallExitTransferCommitted = false;
        wallLandingFlightCommitted = false;
        wallTopLandingSequenceCommitted = false;
        wallStallStartedAt = -1f;
        nextWallRecoveryTime = 0f;
        passiveWallApproachActive = true;
        wallActionPhase = WallActionPhase.AwaitingWallContact;

        automaticJumpArmed = false;
        airborneAfterAutomaticJump = true;
        automaticJumpRequestedAt = Time.unscaledTime;
        automaticJumpVelocityConfirmed = true;
        automaticPredictionActive = true;
        automaticTrajectoryCompatible = true;
        automaticPlanReason = "ChainedWallApproach";
        automaticManeuver = BonusManeuverKind.ApproachJumpThenWallJump;
        automaticPlanTriggerPosition = state.PlayerPosition;
        automaticPlannedLaunchX = state.PlayerPosition.x;
        automaticLaunchWindowLeft = state.PlayerPosition.x;
        automaticLaunchWindowRight = nextWall.Left;
        automaticPredictedLandingX = nextWall.SafeLeft;
        automaticPredictedHorizontalTravel = Mathf.Max(
            0f,
            nextWall.SafeLeft - state.PlayerPosition.x);
        automaticPredictedFlightSeconds = 1.0f;

        BonusRunnerLog.Debug(
            $"WallChainTargetPromoted Position=" +
            $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Velocity=({state.PlayerVelocity.x:F3}," +
            $"{state.PlayerVelocity.y:F3}), Completed=" +
            $"[{completedWall.Left:F3},{completedWall.Right:F3}]" +
            $"@{completedWall.Top:F3}, Next=" +
            $"[{nextWall.Left:F3},{nextWall.Right:F3}]" +
            $"@{nextWall.Top:F3}, Gap={gap:F3}, Rise={rise:F3}, " +
            $"PromotionMode={(allowLevelFace ? "PhysicalExitFaceContact" : "RisingStaticChain")}. " +
            $"MandatoryFaceContact=" +
            $"{(satisfiedMandatoryContact ? "Satisfied" : "NotRequired")}. " +
            "NextState=AwaitingWallContact; leaving the old face does not " +
            "end the multi-pillar climb.",
            "Recovery");
        return true;
    }

    private void AdoptWallExitTargetAfterLip(BonusStageState state)
    {
        if (!wallExitTargetActive)
            return;

        BonusBoardSegment exit = wallExitTarget;
        automaticTargetSafeLeft = exit.SafeLeft;
        automaticTargetSafeRight = exit.SafeRight;
        automaticTargetLeft = exit.Left;
        automaticTargetRight = exit.Right;
        automaticTargetTop = exit.Top;
        automaticTargetColliderId = exit.ColliderInstanceId;
        automaticTargetColliderName = exit.ColliderName;
        automaticTargetMapPieceName = exit.MapPieceName;
        automaticTargetMapPieceOriginX = exit.MapPieceOriginX;
        automaticTargetMapPieceInstanceId = exit.MapPieceInstanceId;
        automaticTargetRegistryGeneration = exit.RegistryGeneration;
        automaticTargetStaticSurfaceIndex = exit.StaticSurfaceIndex;
        if (wallExitPredictedFlightSeconds > 0.05f)
        {
            automaticPredictedFlightSeconds =
                wallExitPredictedFlightSeconds;
            automaticPredictedHorizontalTravel = wallExitPredictedTravel;
            automaticPredictedLandingX = wallExitPredictedLandingX;
        }
        else
        {
            automaticPredictedLandingX = Mathf.Clamp(
                state.PlayerPosition.x +
                    Mathf.Max(0f, exit.SafeLeft - state.PlayerPosition.x),
                exit.SafeLeft,
                exit.SafeRight);
            automaticPredictedHorizontalTravel = Mathf.Max(
                0f,
                automaticPredictedLandingX - automaticPlanTriggerPosition.x);
        }

        // A solved transfer is still only a prediction. Keep the same mapped
        // face armed until a stable landing is verified: if the character
        // collides below the lip, that physical contact is authoritative and
        // TryWallRecoveryJump reopens bounded wall control for this target.
        wallExitContactWatchActive = true;
        wallRecoveryCommitmentUntil = Mathf.Max(
            wallRecoveryCommitmentUntil,
            Time.unscaledTime + 1.25f);

        BonusRunnerLog.Debug(
            $"WallExitTransferActivated AttemptId={automaticAttemptId}, " +
            $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
            $"Target=[{exit.Left:F3},{exit.Right:F3}] " +
            $"Safe=[{exit.SafeLeft:F3},{exit.SafeRight:F3}]@{exit.Top:F3}, " +
            $"PredictedLandingX={automaticPredictedLandingX:F3}, " +
            $"PredictedTravel={automaticPredictedHorizontalTravel:F3}, " +
            $"PredictedFlight={automaticPredictedFlightSeconds:F3}s, " +
            $"ContactFallbackDeadline={wallRecoveryCommitmentUntil:F3}. " +
            "The downstream support is the expected landing objective, but " +
            "its physical face remains armed as an authoritative wall-contact " +
            "fallback until stable landing.",
            "Recovery");
    }

    private void ValidateAutomaticJumpResponse(BonusStageState state)
    {
        bool passiveNoInputIntent =
            passiveWallApproachActive &&
            automaticManeuver == BonusManeuverKind.EnterTrenchThenWallJump &&
            automaticAttemptId == 0 &&
            automaticPlannedHold <= 0f &&
            !jumpController.IsHoldingJump;
        if (automaticJumpArmed ||
            automaticJumpVelocityConfirmed ||
            passiveNoInputIntent ||
            !state.HasPlayer)
            return;

        if (state.PlayerVelocity.y > 5f)
        {
            automaticJumpVelocityConfirmed = true;
            BonusRunnerLog.Debug(
                $"Automatic jump accepted by game: VY={state.PlayerVelocity.y:F3}, X={state.PlayerPosition.x:F3}.",
                "Control");
            return;
        }

        if (Time.unscaledTime - automaticJumpRequestedAt >= 0.20f && state.IsGrounded)
        {
            BonusRunnerLog.Warning(
                $"Automatic jump was not accepted within 0.20s. " +
                $"JumpPanel: Down={JumpPanel.jumpDown}, Pressed={JumpPanel.jumpPressed}, Up={JumpPanel.jumpUp}, " +
                $"HeldPointers={JumpPanel.instance?.heldPointerCount ?? -1}, VY={state.PlayerVelocity.y:F3}.");
            jumpController.Release();
            FinishLearningSample(state, "InputNotAcceptedByGame");
            ResetAutomaticControlState();
            nextAutomaticAttemptTime = Time.unscaledTime + 0.35f;
        }
    }

    private void ResetLandingConfirmation()
    {
        landingCandidateLastFixedStep = -1;
        landingCandidateStableFixedSteps = 0;
        landingCandidateTop = 0f;
        landingCandidateColliderId = 0;
    }

    private static bool IsGroundLearningManeuver(
        BonusManeuverKind maneuver) =>
        maneuver == BonusManeuverKind.GroundJumpToLanding ||
        maneuver == BonusManeuverKind.ApproachJumpThenWallJump ||
        maneuver == BonusManeuverKind.SphereCollectionJump ||
        maneuver == BonusManeuverKind.HazardClearanceJump;

    private bool IsManualGroundLearningEligible(BonusStageState state)
    {
        PlayerMovement player = PlayerMovement.instance;
        if (player == null || !state.IsGrounded)
            return false;

        BonusWallContact wall = wallDetector.Detect(
            player,
            Mathf.Max(1f, lastReliableHorizontalSpeed));
        bool physicallyAttached =
            wall.IsDetected &&
            wall.IsTouching &&
            Mathf.Abs(state.PlayerVelocity.x) <=
                WallAttachmentVelocityTolerance;
        return !physicallyAttached;
    }

    private void StartLearningSample(
        BonusStageState state,
        string source,
        bool learnGroundKinematics)
    {
        if (learningSampleActive)
            FinishLearningSample(state, "InterruptedByNewInput");

        learningSampleActive = true;
        learningMayLearnGroundKinematics = learnGroundKinematics;
        learningSampleId = ++nextLearningSampleId;
        learningSource = source;
        learningMap = state.MapName;
        learningSection = state.SectionIndex;
        learningInputDownTime = Time.unscaledTime;
        learningInputUpTime = 0f;
        learningTakeoffTime = 0f;
        learningTriggerPosition = state.PlayerPosition;
        learningTakeoffPosition = default;
        learningTakeoffVelocity = state.PlayerVelocity;
        learningApexPosition = state.PlayerPosition;
        learningMaximumY = state.PlayerPosition.y;
        learningPreviousVelocityY = state.PlayerVelocity.y;
        learningLastObservedPosition = state.PlayerPosition;
        learningLastObservedAt = Time.unscaledTime;
        learningTookOff = false;
        learningFirstApexCaptured = false;
        learningInputReleased = false;
        ResetLandingConfirmation();
        if (source == "Manual" && learnGroundKinematics)
            ResetManualWallSequence();

        PlayerMovement player = PlayerMovement.instance;
        if (player != null)
            jumpPhysicsFeedback.BeginInput(
                source,
                player,
                learnGroundKinematics);

        BonusRunnerLog.Debug(
            $"JumpSampleStart AttemptId={learningSampleId}, Source={source}, Map={learningMap}, Section={learningSection}, " +
            $"Frame={Time.frameCount}, X={state.PlayerPosition.x:F3}, Y={state.PlayerPosition.y:F3}, " +
            $"VX={state.PlayerVelocity.x:F3}, VY={state.PlayerVelocity.y:F3}, " +
            $"LearnGroundKinematics={learnGroundKinematics}",
            "Learning");
    }

    private void UpdateLearningSample(BonusStageState state)
    {
        if (!learningSampleActive || !state.HasPlayer)
            return;

        float observationDelta = Mathf.Max(
            0.001f,
            Time.unscaledTime - learningLastObservedAt);
        Vector3 frameDelta =
            state.PlayerPosition - learningLastObservedPosition;
        float maximumExpectedHorizontalDelta =
            Mathf.Max(
                Mathf.Abs(state.PlayerVelocity.x),
                Mathf.Abs(lastReliableHorizontalSpeed)) *
            observationDelta + 1.25f;
        bool positionDiscontinuity =
            Mathf.Abs(frameDelta.y) > 5.50f ||
            Mathf.Abs(frameDelta.x) > maximumExpectedHorizontalDelta + 1.25f;
        learningLastObservedPosition = state.PlayerPosition;
        learningLastObservedAt = Time.unscaledTime;
        if (positionDiscontinuity)
        {
            bool automaticLifecycleTransition =
                learningSource == "Automatic";
            BonusRunnerLog.Warning(
                $"ActionPositionDiscontinuity AttemptId={learningSampleId}, " +
                $"Source={learningSource}, Delta=({frameDelta.x:F3},{frameDelta.y:F3}), " +
                $"Dt={observationDelta:F3}s, Position=" +
                $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}). " +
                "FailureDomain=Lifecycle, FailureReason=TeleportOrRespawn; " +
                "this frame is excluded from jump and wall-flight learning.");
            jumpController.Release();
            FinishLearningSample(state, "PositionDiscontinuity");
            ResetAutomaticControlState();
            if (automaticLifecycleTransition)
            {
                pitDescentGuardActive = true;
                pitRespawnLastFixedStep = -1;
                pitRespawnStableFixedSteps = 0;
                nextAutomaticAttemptTime = Time.unscaledTime + 0.45f;
            }
            else
            {
                nextAutomaticAttemptTime = Time.unscaledTime + 0.15f;
            }
            return;
        }

        float timeSinceInput = Time.unscaledTime - learningInputDownTime;
        MonitorWallRecoveryImpulse(state, timeSinceInput);
        bool validUpwardTakeoff =
            !state.IsGrounded &&
            state.PlayerVelocity.y > 5f &&
            timeSinceInput <= 0.30f &&
            state.PlayerPosition.y >= learningTriggerPosition.y - 0.75f;
        if (!learningTookOff && validUpwardTakeoff)
        {
            learningTookOff = true;
            learningTakeoffTime = Time.unscaledTime;
            learningTakeoffPosition = state.PlayerPosition;
            learningTakeoffVelocity = state.PlayerVelocity;
            learningPreviousVelocityY = state.PlayerVelocity.y;
            if (state.PlayerPosition.y > learningMaximumY)
            {
                learningMaximumY = state.PlayerPosition.y;
                learningApexPosition = state.PlayerPosition;
            }
            jumpPhysicsFeedback.ObserveTakeoff(
                learningInputDownTime,
                learningTakeoffVelocity);
            BonusRunnerLog.Debug(
                $"JumpAttemptTakeoff AttemptId={learningSampleId}, Source={learningSource}, " +
                $"InputToTakeoff={learningTakeoffTime - learningInputDownTime:F3}s, " +
                $"Position=({learningTakeoffPosition.x:F3},{learningTakeoffPosition.y:F3}), " +
                $"Velocity=({learningTakeoffVelocity.x:F3},{learningTakeoffVelocity.y:F3}), " +
                $"TriggerDelta=({learningTakeoffPosition.x - learningTriggerPosition.x:F3}," +
                $"{learningTakeoffPosition.y - learningTriggerPosition.y:F3})",
                "Attempt");
        }

        if (learningTookOff && !learningFirstApexCaptured)
        {
            if (state.PlayerPosition.y > learningMaximumY)
            {
                learningMaximumY = state.PlayerPosition.y;
                learningApexPosition = state.PlayerPosition;
            }

            if (learningPreviousVelocityY > 0f &&
                state.PlayerVelocity.y <= 0f &&
                !state.IsGrounded)
            {
                learningFirstApexCaptured = true;
                BonusRunnerLog.Debug(
                    $"JumpAttemptApex AttemptId={learningSampleId}, Source={learningSource}, " +
                    $"X={learningApexPosition.x:F3}, Y={learningApexPosition.y:F3}, " +
                    $"RiseFromTrigger={learningApexPosition.y - learningTriggerPosition.y:F3}, " +
                    $"RiseFromTakeoff={learningApexPosition.y - learningTakeoffPosition.y:F3}, " +
                    $"HorizontalFromTakeoff={learningApexPosition.x - learningTakeoffPosition.x:F3}, " +
                    $"CrossingVY={state.PlayerVelocity.y:F3}. Later pit bounces will be ignored.",
                    "Attempt");
            }

            learningPreviousVelocityY = state.PlayerVelocity.y;
        }

        bool committedWallClimbActive = IsCommittedWallClimbActive();
        float wallPlayerHalfWidth = Mathf.Max(
            0.15f,
            automaticTargetSafeLeft - automaticTargetLeft - 0.15f);
        float wallPhysicalHoldElapsed = Mathf.Max(
            0f,
            Time.unscaledTime - jumpController.LastPressStartedAt);
        bool requiredWallPhasesComplete = wallRecoveryAttempts >= 1;
        bool wallLipCleared =
            committedWallClimbActive &&
            !wallRecoveryLipCrossed &&
            requiredWallPhasesComplete &&
            state.PlayerPosition.x >=
                automaticTargetLeft + wallPlayerHalfWidth - 0.08f &&
            state.PlayerPosition.x <=
                automaticTargetRight + wallPlayerHalfWidth + 0.15f &&
            state.PlayerPosition.y >= automaticTargetTop - 0.20f &&
            Mathf.Abs(state.PlayerVelocity.x) > 1.25f;
        if (wallLipCleared)
        {
            wallRecoveryLipCrossed = true;
            wallActionPhase = WallActionPhase.ExitFlight;
            BonusRunnerLog.Debug(
                $"WallLipCleared AttemptId={automaticAttemptId}, " +
                $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                $"PhysicalHold={wallPhysicalHoldElapsed:F3}s, " +
                $"RequiredReleaseY={wallRecoveryRequiredReleaseY:F3}, " +
                $"RequiredCenterX={automaticTargetLeft + wallPlayerHalfWidth - 0.08f:F3}, " +
                $"TargetRaw=[{automaticTargetLeft:F3},{automaticTargetRight:F3}]" +
                $"@{automaticTargetTop:F3}, ExitTransferCommitted=" +
                $"{wallExitTransferCommitted}. The body, not merely its edge, " +
                "has crossed the lip. A target-solved transfer keeps its " +
                "planned hold through the controller deadline; an unsolved " +
                "climb releases at this observed boundary.",
                "Recovery");
            if (jumpController.IsHoldingJump && !wallExitTransferCommitted)
            {
                jumpController.Release();
                learningInputUpTime = Time.unscaledTime;
                learningInputReleased = true;
            }
            else if (jumpController.IsHoldingJump)
            {
                BonusRunnerLog.Debug(
                    $"WallExitHoldPreserved AttemptId={automaticAttemptId}, " +
                    $"PhysicalHold={wallPhysicalHoldElapsed:F3}s, " +
                    $"PlannedHold={automaticPlannedHold:F3}s, " +
                    $"RemainingHold=" +
                    $"{Mathf.Max(0f, automaticPlannedHold - wallPhysicalHoldElapsed):F3}s. " +
                    "The solved downstream landing, not lip clearance alone, " +
                    "owns the remaining press duration.",
                    "Recovery");
            }
            wallRecoveryContactLatched = false;
            wallRecoverySawUpwardMotion = false;
            if (wallExitTransferCommitted)
            {
                AdoptWallExitTargetAfterLip(state);
            }
            else if (TryPromoteChainedWallTarget(state))
            {
                // The old lip is only an intermediate edge. The newly
                // promoted higher face owns subsequent contact and pulse
                // decisions, so the old learning sample must not continue
                // through this frame under stale target geometry.
                return;
            }
            else
            {
                TryArmWallExitContactWatch(
                    state,
                    "WallLipCleared");
            }
            committedWallClimbActive = false;
        }
        BonusBoardScanResult landingScan = default;
        bool landingSupportConfirmed = false;
        if (state.IsGrounded)
        {
            try
            {
                PlayerMovement landingPlayer = PlayerMovement.instance;
                if (landingPlayer != null)
                {
                    landingScan = platformScanner.Scan(
                        state.PlayerPosition,
                        landingPlayer,
                        Mathf.Max(
                            1f,
                            Mathf.Abs(state.PlayerVelocity.x)));
                    landingSupportConfirmed =
                        landingScan.IsValid &&
                        state.PlayerPosition.x >=
                            landingScan.Current.Left - 0.20f &&
                        state.PlayerPosition.x <=
                            landingScan.Current.Right + 0.20f &&
                        Mathf.Abs(
                            state.PlayerPosition.y -
                            landingScan.Current.Top) <= 0.60f;
                }
            }
            catch
            {
                landingSupportConfirmed = false;
            }
        }

        bool risingGroundPulse =
            state.PlayerVelocity.y > 2.50f ||
            jumpController.IsHoldingJump;
        bool stableLandingFrame =
            state.IsGrounded &&
            landingSupportConfirmed &&
            !risingGroundPulse &&
            Mathf.Abs(state.PlayerVelocity.y) <= 2.50f;
        if (stableLandingFrame)
        {
            int supportColliderId =
                landingScan.Current.ColliderInstanceId;
            bool sameCandidate = landingCandidateStableFixedSteps > 0 &&
                Mathf.Abs(
                    landingScan.Current.Top - landingCandidateTop) <= 0.35f &&
                (landingCandidateColliderId == 0 ||
                 supportColliderId == 0 ||
                 landingCandidateColliderId == supportColliderId);
            if (!sameCandidate)
            {
                landingCandidateLastFixedStep = -1;
                landingCandidateStableFixedSteps = 0;
                landingCandidateTop = landingScan.Current.Top;
                landingCandidateColliderId = supportColliderId;
            }

            long fixedStep = JumpPhysicsFeedback.FixedStepSequence;
            if (fixedStep != landingCandidateLastFixedStep)
            {
                landingCandidateLastFixedStep = fixedStep;
                landingCandidateStableFixedSteps++;
            }
        }
        else
        {
            ResetLandingConfirmation();
        }
        bool verifiedLanding =
            stableLandingFrame &&
            landingCandidateStableFixedSteps >= 2;
        bool landingSupportMatchesTarget =
            landingSupportConfirmed &&
            Mathf.Abs(landingScan.Current.Top - automaticTargetTop) <= 0.35f &&
            state.PlayerPosition.x >=
                automaticTargetLeft + wallPlayerHalfWidth - 0.08f &&
            state.PlayerPosition.x <= automaticTargetRight + 0.25f;
        bool landingMatchesWatchedExit =
            wallExitTargetActive &&
            landingSupportConfirmed &&
            Mathf.Abs(
                landingScan.Current.Top -
                wallExitTarget.Top) <= 0.35f &&
            state.PlayerPosition.x >= wallExitTarget.Left - 0.20f &&
            state.PlayerPosition.x <= wallExitTarget.Right + 0.20f;
        if (wallExitContactWatchActive &&
            verifiedLanding &&
            landingMatchesWatchedExit)
        {
            if (wallExitFaceContactRequired)
            {
                BonusRunnerLog.Warning(
                    $"MandatoryWallFaceContactMissed Position=" +
                    $"({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), LandedSupport=" +
                    $"[{landingScan.Current.Left:F3}," +
                    $"{landingScan.Current.Right:F3}]@" +
                    $"{landingScan.Current.Top:F3}, RequiredFace=" +
                    $"[{wallExitTarget.Left:F3}," +
                    $"{wallExitTarget.Right:F3}]@" +
                    $"{wallExitTarget.Top:F3}, ObjectiveCountAtCapture=" +
                    $"{wallExitObjectiveCountAtCapture}. " +
                    "ExpectedResult=physical face contact followed by " +
                    "attached objective descent; ActualResult=top landing. " +
                    "FailureDomain=RouteExecution; this landing is not " +
                    "reported as successful while the lower lane was skipped.");
                automaticTrajectoryCompatible = false;
                FinishLearningSample(
                    state,
                    "MandatoryWallFaceContactMissed");
                ResetAutomaticControlState();
                nextAutomaticAttemptTime = Time.unscaledTime + 0.10f;
                return;
            }

            wallExitContactWatchActive = false;
            wallExitTargetActive = false;
            wallExitTarget = default;
            wallExitFaceContactRequired = false;
            wallExitObjectiveCountAtCapture = 0;
            wallExitObjectiveMinimumY = float.NaN;
            wallExitObjectiveMaximumY = float.NaN;
            ResetMandatoryFacePlanState();
            BonusRunnerLog.Debug(
                $"WallExitContactWatchResolved Result=VerifiedTargetLanding, " +
                $"Position=({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Support=" +
                $"[{landingScan.Current.Left:F3}," +
                $"{landingScan.Current.Right:F3}]@" +
                $"{landingScan.Current.Top:F3}. No face fallback is needed; " +
                "normal landing reset may release wall-route ownership.",
                "Recovery");
        }
        bool confirmedTargetTopLanding =
            committedWallClimbActive &&
            requiredWallPhasesComplete &&
            verifiedLanding &&
            landingSupportMatchesTarget;
        if (learningTookOff && confirmedTargetTopLanding)
        {
            BonusRunnerLog.Debug(
                $"WallClimbStableLanding AttemptId={automaticAttemptId}, " +
                $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                $"ImpulseConfirmed={wallRecoveryImpulseConfirmed}, " +
                $"LipCrossed={wallRecoveryLipCrossed}, " +
                $"TargetRaw=[{automaticTargetLeft:F3},{automaticTargetRight:F3}]" +
                $"@{automaticTargetTop:F3}. Releasing wall hold at the action boundary.",
                "Recovery");
            wallRecoveryCommitmentUntil = 0f;
            jumpController.Release();
            FinishLearningSample(state, "Landed");
            nextAutomaticAttemptTime = Time.unscaledTime + 0.10f;
            return;
        }
        bool possiblePlannedWallContact =
            committedWallClimbActive &&
            learningSource == "Automatic" &&
            automaticPredictionActive &&
            state.IsGrounded &&
            (risingGroundPulse || !landingSupportConfirmed);
        if (learningTookOff && verifiedLanding && !possiblePlannedWallContact)
        {
            if (automaticPlanReason.StartsWith(
                    "WallRecovery",
                    System.StringComparison.Ordinal))
            {
                BonusRunnerLog.Debug(
                    $"WallClimbLandingObserved AttemptId={automaticAttemptId}, " +
                    $"Position=({state.PlayerPosition.x:F3}," +
                    $"{state.PlayerPosition.y:F3}), Velocity=" +
                    $"({state.PlayerVelocity.x:F3}," +
                    $"{state.PlayerVelocity.y:F3}), " +
                    $"ImpulseConfirmed={wallRecoveryImpulseConfirmed}, " +
                    $"LipCrossed={wallRecoveryLipCrossed}, " +
                    $"Target=[{automaticTargetLeft:F3}," +
                    $"{automaticTargetRight:F3}]@{automaticTargetTop:F3}.",
                    "Recovery");
            }
            if (learningSource == "Automatic")
                jumpController.Release();
            FinishLearningSample(state, "Landed");
            return;
        }

        if (learningTookOff && state.IsGrounded && !verifiedLanding)
        {
            BonusRunnerLog.Debug(
                $"LandingDeferredForWallCheck AttemptId={automaticAttemptId}, " +
                $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                $"RisingGroundPulse={risingGroundPulse}, " +
                $"SupportConfirmed={landingSupportConfirmed}, " +
                $"StableFixedSteps={landingCandidateStableFixedSteps}/2, " +
                $"SupportReason={(landingScan.Reason ?? "Unavailable")}, " +
                $"TargetRaw=[{automaticTargetLeft:F3},{automaticTargetRight:F3}]" +
                $"@{automaticTargetTop:F3}.",
                "Recovery");
        }

        if (!learningTookOff &&
            Time.unscaledTime - learningInputDownTime >= 0.40f)
        {
            bool automaticSample = learningSource == "Automatic";
            if (automaticSample)
                jumpController.Release();
            FinishLearningSample(state, "InputNotAccepted");
            if (automaticSample)
            {
                ResetAutomaticControlState();
                nextAutomaticAttemptTime = Time.unscaledTime + 0.35f;
            }
            return;
        }

        if (learningTookOff &&
            Time.unscaledTime - learningTakeoffTime >= 4f)
        {
            bool automaticSample = learningSource == "Automatic";
            FinishLearningSample(state, "NoLandingWithin4s");
            if (automaticSample)
            {
                jumpController.Release();
                bool likelyPitLifecycle =
                    state.PlayerPosition.y < -3.50f;
                pitDescentGuardActive = likelyPitLifecycle;
                if (likelyPitLifecycle)
                {
                    pitRespawnLastFixedStep = -1;
                    pitRespawnStableFixedSteps = 0;
                }
                ResetAutomaticControlState();
                nextAutomaticAttemptTime = Time.unscaledTime +
                    (likelyPitLifecycle ? 0.45f : 0.25f);
            }
        }
    }

    private void MonitorWallRecoveryImpulse(
        BonusStageState state,
        float timeSinceInput)
    {
        if (learningSource != "Automatic" ||
            !automaticPlanReason.StartsWith(
                "WallRecovery",
                System.StringComparison.Ordinal))
        {
            return;
        }

        float stepRise =
            state.PlayerPosition.y - wallRecoveryLastObservedY;
        if (stepRise >= 0.02f && state.PlayerVelocity.y > 2f)
            wallRecoveryUpwardPhysicsSteps++;
        if (Mathf.Abs(stepRise) >= 0.005f)
            wallRecoveryLastObservedY = state.PlayerPosition.y;

        float totalRise =
            state.PlayerPosition.y - wallRecoveryImpulseStartY;
        if (!wallRecoveryPrematureReleaseLogged &&
            automaticManeuver == BonusManeuverKind.WallJumpClimb &&
            !wallRecoveryLipCrossed &&
            !jumpController.IsHoldingJump &&
            timeSinceInput + 0.015f < automaticPlannedHold)
        {
            wallRecoveryPrematureReleaseLogged = true;
            BonusRunnerLog.Warning(
                $"WallClimbHoldTruncated AttemptId={automaticAttemptId}, " +
                $"Elapsed={timeSinceInput:F3}s, PlannedHold=" +
                $"{automaticPlannedHold:F3}s, LastPress=" +
                $"{jumpController.LastPressStartedAt:F3}, LastRelease=" +
                $"{jumpController.LastReleaseAt:F3}, Maneuver=" +
                $"{automaticManeuver}, Position=" +
                $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"Velocity=({state.PlayerVelocity.x:F3}," +
                $"{state.PlayerVelocity.y:F3}).");
        }
        if (!wallRecoveryImpulseConfirmed &&
            wallRecoveryUpwardPhysicsSteps >= 2 &&
            totalRise >= 0.30f &&
            state.PlayerVelocity.y > 5f)
        {
            wallRecoveryImpulseConfirmed = true;
            BonusRunnerLog.Debug(
                $"WallClimbImpulseConfirmed AttemptId={automaticAttemptId}, " +
                $"Elapsed={timeSinceInput:F3}s, PhysicsRiseSteps=" +
                $"{wallRecoveryUpwardPhysicsSteps}, " +
                $"StartY={wallRecoveryImpulseStartY:F3}, " +
                $"CurrentY={state.PlayerPosition.y:F3}, " +
                $"DeltaY={totalRise:F3}, StartVY=" +
                $"{wallRecoveryImpulseStartVelocityY:F3}, " +
                $"CurrentVY={state.PlayerVelocity.y:F3}. " +
                "The second press produced sustained upward movement.",
                "Recovery");
        }

        if (BonusRunnerLog.IsDebugMode &&
            Time.unscaledTime >= nextWallClimbFrameLogTime &&
            timeSinceInput <= 0.55f)
        {
            nextWallClimbFrameLogTime = Time.unscaledTime + 0.04f;
            BonusRunnerLog.Debug(
                $"WallClimbFrame AttemptId={automaticAttemptId}, " +
                $"Elapsed={timeSinceInput:F3}s, " +
                $"Phase={(wallRecoveryLipCrossed ? "LipCrossed" : wallRecoveryImpulseConfirmed ? "Rising" : "AwaitingImpulse")}, " +
                $"Position=({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Velocity=" +
                $"({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                $"DeltaY={totalRise:F3}, PhysicsRiseSteps=" +
                $"{wallRecoveryUpwardPhysicsSteps}, " +
                $"Grounded={state.IsGrounded}, " +
                $"Holding={jumpController.IsHoldingJump}, " +
                $"PlannedHold={automaticPlannedHold:F3}s, " +
                $"Maneuver={automaticManeuver}, " +
                $"Target=[{automaticTargetLeft:F3}," +
                $"{automaticTargetRight:F3}]@{automaticTargetTop:F3}",
                "Recovery");
        }

        if (!wallRecoveryImpulseConfirmed &&
            !wallRecoveryImpulseFailureLogged &&
            timeSinceInput >= 0.14f)
        {
            wallRecoveryImpulseFailureLogged = true;
            BonusRunnerLog.Warning(
                $"WallClimbImpulseRejected AttemptId={automaticAttemptId}: " +
                $"the second press did not produce two upward physics steps " +
                $"within {timeSinceInput:F3}s. StartY=" +
                $"{wallRecoveryImpulseStartY:F3}, CurrentY=" +
                $"{state.PlayerPosition.y:F3}, DeltaY={totalRise:F3}, " +
                $"StartVY={wallRecoveryImpulseStartVelocityY:F3}, " +
                $"CurrentVY={state.PlayerVelocity.y:F3}. " +
                "The hold is released and the contact may retry after a " +
                "real physics-frame barrier; this press consumes one retry " +
                "slot and is not a successful climb.");
            jumpController.Release();
            wallReleaseObservedFixedStep =
                JumpPhysicsFeedback.FixedStepSequence;
            if (wallRecoveryAttempts >=
                MaximumWallRecoveriesPerAirborneSequence)
            {
                wallActionPhase = WallActionPhase.Failed;
                FinishLearningSample(
                    state,
                    "WallImpulseRetryLimitReached");
                ResetAutomaticControlState();
                nextAutomaticAttemptTime = Time.unscaledTime + 0.25f;
                return;
            }
            wallRecoveryContactLatched = false;
            wallRecoverySawUpwardMotion = false;
            wallActionPhase = WallActionPhase.AwaitingWallContact;
            nextWallRecoveryTime = Time.unscaledTime + 0.01f;
        }
    }

    private void FinishLearningSample(BonusStageState state, string outcome)
    {
        if (!learningSampleActive)
            return;

        float holdSeconds = learningInputReleased
            ? Mathf.Max(0f, learningInputUpTime - learningInputDownTime)
            : Mathf.Max(0f, Time.unscaledTime - learningInputDownTime);
        string holdMeasurement = learningSource == "Automatic"
            ? "Planned"
            : "PhysicalInput";
        if (learningSource == "Automatic" &&
            Mathf.Abs(jumpController.LastPressStartedAt - learningInputDownTime) <= 0.10f)
        {
            float actualReleaseTime =
                jumpController.LastReleaseAt >= jumpController.LastPressStartedAt
                    ? jumpController.LastReleaseAt
                    : Time.unscaledTime;
            holdSeconds = Mathf.Max(
                0f,
                actualReleaseTime - jumpController.LastPressStartedAt);
            holdMeasurement = "ControllerActual";
        }
        float flightSeconds = learningTookOff
            ? Mathf.Max(0f, Time.unscaledTime - learningTakeoffTime)
            : 0f;
        float horizontalDistance = learningTookOff
            ? state.PlayerPosition.x - learningTakeoffPosition.x
            : 0f;
        float heightGain = learningMaximumY - learningTriggerPosition.y;
        float takeoffToApexGain = learningTookOff
            ? learningMaximumY - learningTakeoffPosition.y
            : 0f;
        string landingSurface = "NotLanded";
        bool landingSurfaceValid = false;
        float observedLandingTop = state.PlayerPosition.y;
        if (outcome == "Landed" && state.HasPlayer)
        {
            try
            {
                PlayerMovement landingPlayer = PlayerMovement.instance;
                if (landingPlayer != null)
                {
                    BonusBoardScanResult observedLanding = platformScanner.Scan(
                        state.PlayerPosition,
                        landingPlayer,
                        Mathf.Abs(state.PlayerVelocity.x));
                    landingSurfaceValid = observedLanding.IsValid;
                    if (landingSurfaceValid)
                        observedLandingTop = observedLanding.Current.Top;
                    landingSurface = observedLanding.IsValid
                        ? $"Raw=[{observedLanding.Current.Left:F3}," +
                          $"{observedLanding.Current.Right:F3}] " +
                          $"Safe=[{observedLanding.Current.SafeLeft:F3}," +
                          $"{observedLanding.Current.SafeRight:F3}] " +
                          $"Top={observedLanding.Current.Top:F3} Collider=" +
                          $"{observedLanding.Current.ColliderInstanceId}:" +
                          $"{observedLanding.Current.ColliderName}"
                        : $"ScanInvalid:{observedLanding.Reason}";
                }
            }
            catch (System.Exception exception)
            {
                landingSurface = $"ScanFailed:{exception.GetType().Name}";
            }
        }
        string physicsFeedback = jumpPhysicsFeedback.CompleteSample(
            holdSeconds,
            learningTookOff,
            learningFirstApexCaptured,
            learningTakeoffPosition,
            learningApexPosition);

        float inputToLandingSeconds = Mathf.Max(
            0f,
            Time.unscaledTime - learningInputDownTime);
        float inputToTakeoffSeconds = learningTookOff
            ? Mathf.Max(0f, learningTakeoffTime - learningInputDownTime)
            : float.PositiveInfinity;
        float triggerToTakeoffX = learningTookOff
            ? Mathf.Abs(learningTakeoffPosition.x - learningTriggerPosition.x)
            : float.PositiveInfinity;
        bool cleanFlightTimingSample =
            learningMayLearnGroundKinematics &&
            outcome == "Landed" &&
            landingSurfaceValid &&
            learningTookOff &&
            learningFirstApexCaptured &&
            inputToTakeoffSeconds <= 0.08f &&
            triggerToTakeoffX <= Mathf.Max(
                0.80f,
                Mathf.Abs(learningTakeoffVelocity.x) * 0.08f);
        if (cleanFlightTimingSample)
        {
            JumpPhysicsSnapshot timingPhysics =
                jumpPhysicsFeedback.CaptureSnapshot(PlayerMovement.instance);
            float timingHeightDelta =
                observedLandingTop - learningTriggerPosition.y;
            float rawPredictedFlight =
                jumpPlanner.PredictRawInputToLandingSeconds(
                    holdSeconds,
                    timingHeightDelta,
                    timingPhysics);
            if (rawPredictedFlight > 0f)
            {
                jumpPhysicsFeedback.ObserveFlightTiming(
                    rawPredictedFlight,
                    inputToLandingSeconds,
                    learningSource,
                    holdSeconds,
                    timingHeightDelta);
            }
        }
        else
        {
            BonusRunnerLog.Debug(
                $"FlightTimingRejected AttemptId={learningSampleId}, " +
                $"Source={learningSource}, Outcome={outcome}, " +
                $"LandingSurfaceValid={landingSurfaceValid}, " +
                $"TookOff={learningTookOff}, ApexCaptured=" +
                $"{learningFirstApexCaptured}, InputToTakeoff=" +
                $"{inputToTakeoffSeconds:F3}s, TriggerToTakeoffX=" +
                $"{triggerToTakeoffX:F3}, LearnGroundKinematics=" +
                $"{learningMayLearnGroundKinematics}.",
                "Physics");
        }

        BonusRunnerLog.Debug(
            $"JumpSample AttemptId={learningSampleId}, Source={learningSource}, Outcome={outcome}, Map={learningMap}, Section={learningSection}, " +
            $"Hold={holdSeconds:F3}s, HoldMeasurement={holdMeasurement}, " +
            $"Trigger=({learningTriggerPosition.x:F3},{learningTriggerPosition.y:F3}), " +
            $"Takeoff=({learningTakeoffPosition.x:F3},{learningTakeoffPosition.y:F3}), " +
            $"TakeoffVelocity=({learningTakeoffVelocity.x:F3},{learningTakeoffVelocity.y:F3}), " +
            $"FirstApex=({learningApexPosition.x:F3},{learningApexPosition.y:F3}), " +
            $"FirstApexCaptured={learningFirstApexCaptured}, " +
            $"TriggerHeightGain={heightGain:F3}, TakeoffToApexGain={takeoffToApexGain:F3}, " +
            $"End=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"EndVelocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
            $"HorizontalDistance={horizontalDistance:F3}, Flight={flightSeconds:F3}s; " +
            $"LandingSurface[{landingSurface}]; " +
            $"{physicsFeedback}",
            "Learning");

        if (learningSource == "Manual" && manualWallSequenceActive)
        {
            BonusRunnerLog.Debug(
                $"ManualWallSequence AttemptId={learningSampleId}, Outcome={outcome}, " +
                $"WallJumps={manualWallJumpCount}, LastWallDown=" +
                $"({manualWallJumpPosition.x:F3},{manualWallJumpPosition.y:F3}), " +
                $"LastWallHold={manualWallJumpHold:F3}s, Collider=" +
                $"{manualWallColliderId}:{manualWallColliderName}, " +
                $"End=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                $"LandingSurface[{landingSurface}]",
                "Recovery");
            ResetManualWallSequence();
        }

        if (learningSource == "Automatic" && automaticPredictionActive)
        {
            int sphereCountAtEnd =
                BonusStageInspector.TryGetBonusSphereCount(out int observedSphereCount)
                    ? observedSphereCount
                    : -1;
            string sphereProgress =
                automaticSphereCountAtPlan >= 0 && sphereCountAtEnd >= 0
                    ? $"{automaticSphereCountAtPlan}->{sphereCountAtEnd} " +
                      $"Delta={sphereCountAtEnd - automaticSphereCountAtPlan}"
                    : $"Unavailable(Plan={automaticSphereCountAtPlan}," +
                      $"End={sphereCountAtEnd})";
            int actualSphereHits =
                automaticSphereCountAtPlan >= 0 && sphereCountAtEnd >= 0
                    ? Mathf.Max(
                        0,
                        sphereCountAtEnd - automaticSphereCountAtPlan)
                    : -1;
            string sphereOutcome = automaticExpectedSphereHits <= 0
                ? "NoPlannedPickup"
                : actualSphereHits < 0
                    ? "Unverified"
                    : actualSphereHits >= automaticExpectedSphereHits
                        ? "Met"
                        : "BelowPrediction";
            if (outcome == "Landed" && state.HasPlayer)
            {
                float landingError =
                    state.PlayerPosition.x - automaticPredictedLandingX;
                bool withinTargetX =
                    state.PlayerPosition.x >= automaticTargetSafeLeft &&
                    state.PlayerPosition.x <= automaticTargetSafeRight;
                bool supportMatched = false;
                bool actualSupportAvailable = false;
                BonusBoardSegment actualSupport = default;
                string supportCheck = "Unavailable";
                try
                {
                    PlayerMovement player = PlayerMovement.instance;
                    if (player != null)
                    {
                        BonusBoardScanResult landingScan = platformScanner.Scan(
                            state.PlayerPosition,
                            player,
                            Mathf.Abs(state.PlayerVelocity.x));
                        actualSupportAvailable = landingScan.IsValid;
                        if (actualSupportAvailable)
                            actualSupport = landingScan.Current;
                        bool topMatched = landingScan.IsValid &&
                            Mathf.Abs(
                                landingScan.Current.Top - automaticTargetTop) <= 0.35f;
                        bool rawBoundsMatched =
                            state.PlayerPosition.x >= automaticTargetLeft - 0.10f &&
                            state.PlayerPosition.x <= automaticTargetRight + 0.10f;
                        BonusBoardSegment expectedSupport = new(
                            automaticTargetLeft,
                            automaticTargetRight,
                            automaticTargetTop,
                            automaticTargetSafeLeft,
                            automaticTargetSafeRight,
                            automaticTargetColliderId,
                            automaticTargetColliderName,
                            automaticTargetMapPieceName,
                            automaticTargetMapPieceOriginX,
                            automaticTargetMapPieceInstanceId,
                            automaticTargetRegistryGeneration,
                            automaticTargetStaticSurfaceIndex);
                        bool identityMatched = landingScan.IsValid &&
                            StaticOrColliderIdentityMatches(
                                landingScan.Current,
                                expectedSupport);
                        supportMatched =
                            topMatched && rawBoundsMatched && identityMatched;
                        supportCheck = landingScan.IsValid
                            ? $"TopMatch={topMatched},BoundsMatch={rawBoundsMatched}," +
                              $"IdentityMatch={identityMatched},ActualSupport=" +
                              $"{landingScan.Current.ColliderInstanceId}:{landingScan.Current.ColliderName}," +
                              $"MapPiece={landingScan.Current.MapPieceName}#" +
                              $"{landingScan.Current.MapPieceInstanceId}," +
                              $"Generation={landingScan.Current.RegistryGeneration}," +
                              $"Surface={landingScan.Current.StaticSurfaceIndex}"
                            : $"ScanInvalid:{landingScan.Reason}";
                    }
                }
                catch (System.Exception exception)
                {
                    supportCheck = $"CheckFailed:{exception.GetType().Name}";
                }
                bool landedOnTarget = withinTargetX && supportMatched;
                float actualTriggerTravel =
                    state.PlayerPosition.x - learningTriggerPosition.x;
                float actualTakeoffTravel = learningTookOff
                    ? state.PlayerPosition.x - learningTakeoffPosition.x
                    : 0f;
                float errorFromPredictedX =
                    state.PlayerPosition.x - automaticPredictedLandingX;
                float targetLeftMargin =
                    state.PlayerPosition.x - automaticTargetSafeLeft;
                float targetRightMargin =
                    automaticTargetSafeRight - state.PlayerPosition.x;
                float actualSupportTopError = actualSupportAvailable
                    ? actualSupport.Top - automaticTargetTop
                    : float.NaN;
                bool wallAction = automaticPlanReason.StartsWith(
                    "WallRecovery",
                    System.StringComparison.Ordinal);
                string resultClass =
                    wallAction && wallRecoveryLipCrossed
                        ? landedOnTarget
                            ? "WallActionSuccess"
                            : "WallLipClearedDifferentSupport"
                        : landedOnTarget
                            ? "Success"
                            : !withinTargetX
                                ? state.PlayerPosition.x < automaticTargetSafeLeft
                                    ? "ShortOfSafeRange"
                                    : "BeyondSafeRange"
                                : "WrongSupport";
                string executionStatus =
                    wallAction && wallRecoveryLipCrossed
                        ? landedOnTarget
                            ? "Success:LipClearedAndTargetReached"
                            : "Failure:WrongSupportAfterLip"
                        : learningTookOff
                            ? "InputAccepted"
                            : "InputNotConfirmed";
                string routeStatus = landedOnTarget
                    ? "TargetReached"
                    : wallAction && wallRecoveryLipCrossed
                        ? "TargetMissedAfterWallExit"
                        : "TargetMissed";
                string failureDomain = landedOnTarget
                        ? "None"
                        : wallAction && wallRecoveryLipCrossed
                            ? actualSupportAvailable
                                ? "ExecutionModel"
                                : "Execution:NoStableSupport"
                        : supportMatched
                            ? "ExecutionModel"
                            : "RouteOrExecution";
                string actualSupportDescription = actualSupportAvailable
                    ? $"Raw=[{actualSupport.Left:F3},{actualSupport.Right:F3}] " +
                      $"Safe=[{actualSupport.SafeLeft:F3},{actualSupport.SafeRight:F3}] " +
                      $"Top={actualSupport.Top:F3} Collider=" +
                      $"{actualSupport.ColliderInstanceId}:{actualSupport.ColliderName} " +
                      $"MapPiece={actualSupport.MapPieceName}#" +
                      $"{actualSupport.MapPieceInstanceId}/G" +
                      $"{actualSupport.RegistryGeneration}/S" +
                      $"{actualSupport.StaticSurfaceIndex}"
                    : "Unavailable";
                // Touching the raw collider while the player's center is
                // outside the safe interval is an unstable edge catch, not a
                // successful route. Do not let it reinforce the same
                // oversized jump on repeated three-pillar patterns.
                if (supportMatched && !landedOnTarget)
                {
                    jumpPhysicsFeedback.ObserveLandingError(
                        automaticPredictedHorizontalTravel,
                        actualTriggerTravel,
                        automaticTargetHeightDelta,
                        automaticPlannedHold,
                        automaticPlanReason);
                }
                if (supportMatched &&
                    !automaticPlanReason.StartsWith(
                        "WallRecovery",
                        System.StringComparison.Ordinal))
                {
                    jumpPhysicsFeedback.ObserveSuccessfulLanding(
                        automaticPredictedHorizontalTravel,
                        actualTriggerTravel,
                        automaticPlannedTravelScale,
                        automaticPlannedHold,
                        automaticTriggerSpeed,
                        automaticTargetHeightDelta);
                }
                BonusRunnerLog.Debug(
                    $"JumpAttemptResult RouteId={activeRouteDecisionId}, AttemptId={automaticAttemptId}, Outcome={outcome}, " +
                    $"ResultClass={resultClass}, Plan={automaticPlanReason}, " +
                    $"ExecutionStatus={executionStatus}, RouteStatus={routeStatus}, " +
                    $"FailureDomain={failureDomain}, " +
                    $"PlanTrigger=({automaticPlanTriggerPosition.x:F3},{automaticPlanTriggerPosition.y:F3}), " +
                    $"ActualTakeoff=({learningTakeoffPosition.x:F3},{learningTakeoffPosition.y:F3}), " +
                    $"Apex=({learningApexPosition.x:F3},{learningApexPosition.y:F3}), " +
                    $"ActualLanding=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
                    $"SourceRaw=[{automaticSourceSegment.Left:F3},{automaticSourceSegment.Right:F3}] " +
                    $"SourceSafe=[{automaticSourceSegment.SafeLeft:F3},{automaticSourceSegment.SafeRight:F3}] " +
                    $"SourceTop={automaticSourceSegment.Top:F3} SourceCollider=" +
                    $"{automaticSourceSegment.ColliderInstanceId}:{automaticSourceSegment.ColliderName}, " +
                    $"TargetRaw=[{automaticTargetLeft:F3},{automaticTargetRight:F3}] " +
                    $"TargetSafe=[{automaticTargetSafeLeft:F3},{automaticTargetSafeRight:F3}] " +
                    $"TargetTop={automaticTargetTop:F3} TargetCollider=" +
                    $"{automaticTargetColliderId}:{automaticTargetColliderName}, " +
                    $"ActualSupport[{actualSupportDescription}], " +
                    $"PlannedLaunch={automaticPlannedLaunchX:F3} " +
                    $"LaunchWindow=[{automaticLaunchWindowLeft:F3},{automaticLaunchWindowRight:F3}], " +
                    $"PredictedLanding=({automaticPredictedLandingX:F3},{automaticTargetTop:F3}), " +
                    $"PredictedFlight={automaticPredictedFlightSeconds:F3}s " +
                    $"ActualFlight={flightSeconds:F3}s FlightError={flightSeconds - automaticPredictedFlightSeconds:F3}s, " +
                    $"PlannedHold={automaticPlannedHold:F3}s " +
                    $"PlannedEffectiveHold={Mathf.Min(automaticPlannedHold, automaticPlanPhysicsSnapshot.EffectiveHoldCapSeconds):F3}s " +
                    $"ActualHold={holdSeconds:F3}s " +
                    $"ActualEffectiveHold={Mathf.Min(holdSeconds, automaticPlanPhysicsSnapshot.EffectiveHoldCapSeconds):F3}s " +
                    $"ControllerHoldError={holdSeconds - automaticPlannedHold:F3}s, " +
                    $"PredictedTravel={automaticPredictedHorizontalTravel:F3} " +
                    $"ActualTriggerTravel={actualTriggerTravel:F3} " +
                    $"ActualTakeoffTravel={actualTakeoffTravel:F3}, " +
                    $"LandingErrorX={landingError:F3} PredictedToActualErrorX={errorFromPredictedX:F3}, " +
                    $"SafeMarginLeft={targetLeftMargin:F3} SafeMarginRight={targetRightMargin:F3}, " +
                    $"SupportTopErrorY={actualSupportTopError:F3}, " +
                    $"WithinTargetX={withinTargetX}, SupportMatched={supportMatched}, " +
                    $"LandedOnTarget={landedOnTarget}, HazardAtPlan={automaticHazardAtPlan}, " +
                    $"SphereProgress={sphereProgress}, " +
                    $"ExpectedSphereHits={automaticExpectedSphereHits}, " +
                    $"ActualSphereHits={actualSphereHits}, SphereOutcome={sphereOutcome}, " +
                    $"SupportCheck={supportCheck}, PhysicsRevAtPlan={automaticPhysicsRevision}",
                    "Attempt");
            }
            else
            {
                bool wallExitFlight = string.Equals(
                    outcome,
                    "WallBounceExitFlight",
                    StringComparison.Ordinal);
                string resultFailureDomain = wallExitFlight
                    ? "None"
                    : string.Equals(
                        outcome,
                        "PositionDiscontinuity",
                        StringComparison.Ordinal)
                        ? "Lifecycle"
                        : "ExecutionOrTransition";
                BonusRunnerLog.Debug(
                    $"JumpAttemptResult RouteId={activeRouteDecisionId}, AttemptId={automaticAttemptId}, Outcome={outcome}, " +
                    $"ResultClass={outcome}, Plan={automaticPlanReason}, " +
                    $"ExecutionStatus={(wallExitFlight ? "Success:ExitFlight" : outcome)}, " +
                    $"RouteStatus={(wallExitFlight ? "ReplanFromObservedFlight" : "NotApplicable")}, " +
                    $"FailureDomain={resultFailureDomain}, " +
                    $"PlanTrigger=({automaticPlanTriggerPosition.x:F3},{automaticPlanTriggerPosition.y:F3}), " +
                    $"PlannedLaunch={automaticPlannedLaunchX:F3} " +
                    $"LaunchWindow=[{automaticLaunchWindowLeft:F3},{automaticLaunchWindowRight:F3}], " +
                    $"PredictedFlight={automaticPredictedFlightSeconds:F3}s, " +
                    $"TargetRaw=[{automaticTargetLeft:F3},{automaticTargetRight:F3}] " +
                    $"TargetSafe=[{automaticTargetSafeLeft:F3},{automaticTargetSafeRight:F3}] " +
                    $"TargetTop={automaticTargetTop:F3}, " +
                    $"ResultPosition={(state.HasPlayer ? $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3})" : "Unavailable")}, " +
                    $"HazardAtPlan={automaticHazardAtPlan}, " +
                    $"SphereProgress={sphereProgress}, " +
                    $"ExpectedSphereHits={automaticExpectedSphereHits}, " +
                    $"ActualSphereHits={actualSphereHits}, SphereOutcome={sphereOutcome}, " +
                    "LandingMetrics=NotApplicable",
                    "Attempt");
            }

            automaticPredictionActive = false;
        }

        learningSampleActive = false;
        learningMayLearnGroundKinematics = false;
        ResetLandingConfirmation();
    }

    private void ObserveManualWallJumpDown(BonusStageState state)
    {
        if (Mathf.Abs(state.PlayerVelocity.x) >
                WallAttachmentVelocityTolerance)
            return;

        PlayerMovement player = PlayerMovement.instance;
        if (player == null)
            return;

        BonusWallContact wall = wallDetector.Detect(
            player,
            lastReliableHorizontalSpeed);
        if (!wall.IsDetected || !wall.IsTouching)
            return;

        bool newSequence =
            !manualWallSequenceActive ||
            Time.unscaledTime - manualWallLastPulseTime > 1.25f ||
            manualWallJumpCount > 0 &&
                Mathf.Abs(wall.Point.x - manualWallPoint.x) > 1.00f;
        if (newSequence)
        {
            manualWallSequenceActive = true;
            manualWallSequenceId = ++nextManualWallSequenceId;
            manualWallJumpCount = 0;
        }

        manualWallSequenceActive = true;
        manualWallJumpCount++;
        manualWallJumpDownTime = Time.unscaledTime;
        manualWallJumpHold = 0f;
        manualWallJumpPosition = state.PlayerPosition;
        manualWallJumpStartVelocity = state.PlayerVelocity;
        manualWallPoint = wall.Point;
        manualWallLastPulseTime = Time.unscaledTime;
        manualWallColliderId = wall.ColliderInstanceId;
        manualWallColliderName = wall.ColliderName;
        BonusRunnerLog.Debug(
            $"ManualWallPulseDown SequenceId={manualWallSequenceId}, " +
            $"AttemptId={(learningSampleActive && learningSource == "Manual" ? learningSampleId : 0)}, " +
            $"Pulse={manualWallJumpCount}, " +
            $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
            $"WallPoint=({wall.Point.x:F3},{wall.Point.y:F3}), " +
            $"Distance={wall.Distance:F3}, Collider=" +
            $"{wall.ColliderInstanceId}:{wall.ColliderName}",
            "Recovery");
    }

    private void ObserveManualWallJumpUp(BonusStageState state)
    {
        if (!manualWallSequenceActive || manualWallJumpDownTime <= 0f)
            return;

        manualWallJumpHold = Mathf.Max(
            0f,
            Time.unscaledTime - manualWallJumpDownTime);
        BonusWallContact wall = default;
        PlayerMovement player = PlayerMovement.instance;
        if (player != null)
        {
            wall = wallDetector.Detect(
                player,
                lastReliableHorizontalSpeed);
        }
        float rise = state.PlayerPosition.y - manualWallJumpPosition.y;
        float travelX = state.PlayerPosition.x - manualWallJumpPosition.x;
        bool sameWall = wall.IsDetected &&
            Mathf.Abs(wall.Point.x - manualWallPoint.x) <= 1.00f;
        BonusRunnerLog.Debug(
            $"ManualWallPulseUp SequenceId={manualWallSequenceId}, " +
            $"AttemptId={(learningSampleActive && learningSource == "Manual" ? learningSampleId : 0)}, " +
            $"Pulse={manualWallJumpCount}, Hold={manualWallJumpHold:F3}s, " +
            $"Delta=({travelX:F3},{rise:F3}), " +
            $"StartVelocity=({manualWallJumpStartVelocity.x:F3}," +
            $"{manualWallJumpStartVelocity.y:F3}), " +
            $"Position=({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Velocity=({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
            $"WallAfterRelease[Detected={wall.IsDetected}," +
            $"Touching={wall.IsTouching},SameWall={sameWall}," +
            $"Point=({wall.Point.x:F3},{wall.Point.y:F3})," +
            $"BodyGap={wall.BodyGap:F3}], Collider=" +
            $"{manualWallColliderId}:{manualWallColliderName}",
            "Recovery");
        manualWallJumpDownTime = 0f;
        manualWallLastPulseTime = Time.unscaledTime;
    }

    private void ResetManualWallSequence()
    {
        manualWallSequenceActive = false;
        manualWallJumpCount = 0;
        manualWallJumpDownTime = 0f;
        manualWallJumpHold = 0f;
        manualWallJumpPosition = default;
        manualWallJumpStartVelocity = default;
        manualWallPoint = default;
        manualWallLastPulseTime = -10f;
        manualWallSequenceId = 0;
        manualWallColliderId = 0;
        manualWallColliderName = string.Empty;
    }
}
