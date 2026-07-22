using AutoBonusRunner.Diagnostics;
using AutoBonusRunner.Control;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace AutoBonusRunner;

internal enum BonusStageRetryPhase
{
    Idle,
    PromptObserved,
    PopupPresented,
    AwaitingContinueReward,
    AwaitingBonusResume,
    AwaitingExit,
    Completed,
    Failed
}

/// <summary>
/// Owns the failure-dialog lifecycle without assuming that GameState remains
/// Bonus while the native Second Wind coroutine waits for another popup to
/// close.  A retry action is legal only after the exact popup produced by the
/// observed SecondWind instance is visible and its real UI button is ready.
/// </summary>
internal static class BonusStageRetryBridge
{
    private const int MaximumInvocationAttempts = 3;
    private const float PromptTimeoutSeconds = 30f;
    private const float PopupReadyTimeoutSeconds = 30f;
    private const float ContinueAcknowledgementTimeoutSeconds = 120f;
    private const float ContinueCloseGraceSeconds = 3f;
    private const float BonusResumeTimeoutSeconds = 15f;
    private const float ExitAcknowledgementTimeoutSeconds = 20f;
    private const float InvocationRetryDelaySeconds = 0.50f;

    private static BonusStageRetryPhase phase;
    private static SecondWind pendingPrompt;
    private static Popup pendingPopup;
    private static Sprite pendingIcon;
    private static int promptInstanceId;
    private static int popupInstanceId;
    private static long sequenceCounter;
    private static long activeSequence;
    private static int invocationAttempts;
    private static int readyFrame;
    private static float phaseStartedAtRealtime;
    private static float nextInvocationAtRealtime;
    private static float nextGateLogAtRealtime;
    private static bool continueInvocationOwned;
    private static bool forceCancelAfterRetryFailure;
    private static bool fallbackCancelAttempted;
    private static bool exitIsFailureFallback;
    private static bool lastRequestedContinue;
    private static float continueCloseObservedAt = -1f;
    private static string fallbackFailureDetail = string.Empty;
    private static bool lastObservedBonusState;
    private static bool lastObservedActiveGameplay;
    private static bool lastObservedHasPlayer;
    private static bool lastObservedCharacterFellOff;
    private static string lastObservedGameState = "Unknown";
    private static int resumeEvidenceFrames;
    private static int lastResumeEvidenceFrame = -1;
    private static bool outcomeSucceeded;
    private static bool outcomeWarning;
    private static bool outcomeReported;
    private static string outcomeDetail = string.Empty;
    private static bool patchInventoryChecked;
    private static bool patchInventoryReady;
    private static string patchInventoryEvidence = "NotChecked";

    internal static bool BlocksTerrainControl =>
        phase is BonusStageRetryPhase.PromptObserved or
            BonusStageRetryPhase.PopupPresented or
            BonusStageRetryPhase.AwaitingContinueReward or
            BonusStageRetryPhase.AwaitingBonusResume or
            BonusStageRetryPhase.AwaitingExit or
            BonusStageRetryPhase.Failed;

    internal static string ControlGateSummary =>
        $"Phase={phase},Sequence={activeSequence},Attempts=" +
        $"{invocationAttempts},Prompt={promptInstanceId}," +
        $"Popup={popupInstanceId},GameState={lastObservedGameState}," +
        $"IsBonus={lastObservedBonusState},Active=" +
        $"{lastObservedActiveGameplay},HasPlayer={lastObservedHasPlayer}," +
        $"FellOff={lastObservedCharacterFellOff},ResumeEvidence=" +
        $"{resumeEvidenceFrames}/2,PatchReady={patchInventoryReady}";

    internal static void SetPatchInventoryReady(
        bool ready,
        string evidence)
    {
        patchInventoryChecked = true;
        patchInventoryReady = ready;
        patchInventoryEvidence = string.IsNullOrWhiteSpace(evidence)
            ? "Unavailable"
            : evidence;
    }

    internal static void ReportHarmonyCallbackFailure(
        string callback,
        System.Exception exception,
        bool createOwnershipIfIdle = false)
    {
        try
        {
            if (phase == BonusStageRetryPhase.Idle &&
                createOwnershipIfIdle)
            {
                activeSequence = ++sequenceCounter;
                phase = BonusStageRetryPhase.PromptObserved;
                phaseStartedAtRealtime = Time.realtimeSinceStartup;
            }
            if (phase != BonusStageRetryPhase.Idle &&
                phase != BonusStageRetryPhase.Failed)
            {
                JumpController.ReleaseOwnedInputForRetryModal();
                Fail(
                    $"Harmony retry callback {callback} failed: " +
                    $"{exception.GetType().Name}:{exception.Message}. " +
                    "No unverified UI action was dispatched; terrain " +
                    "control remains fail-closed until native state is safe.");
            }

            BonusRunnerLog.Error(
                $"RetryHarmonyCallbackFailed Callback={callback}, " +
                $"Sequence={activeSequence}, Phase={phase}, Error=" +
                $"{exception.GetType().Name}:{exception.Message}.");
        }
        catch
        {
            // A Harmony diagnostic must never break the original game method.
        }
    }

    internal static void MarkPromptShown(SecondWind prompt)
    {
        if (prompt == null)
        {
            BonusRunnerLog.Warning(
                "RetryPromptRejected Reason=NullSecondWindInstance.");
            return;
        }

        int instanceId = SafeInstanceId(prompt);
        bool samePromptAlreadyOwned =
            phase is BonusStageRetryPhase.PromptObserved or
                BonusStageRetryPhase.PopupPresented or
                BonusStageRetryPhase.AwaitingContinueReward or
                BonusStageRetryPhase.AwaitingExit;
        if (samePromptAlreadyOwned &&
            instanceId != 0 &&
            instanceId == promptInstanceId)
        {
            BonusRunnerLog.Debug(
                $"RetryPromptDuplicate Sequence={activeSequence}, " +
                $"Phase={phase}, Prompt={instanceId}. Existing retry " +
                "ownership was preserved.",
                "Retry");
            return;
        }

        if (instanceId == 0)
        {
            ClearActiveState();
            activeSequence = ++sequenceCounter;
            pendingPrompt = prompt;
            phase = BonusStageRetryPhase.PromptObserved;
            phaseStartedAtRealtime = Time.realtimeSinceStartup;
            JumpController.ReleaseOwnedInputForRetryModal();
            Fail(
                $"Retry prompt identity could not be read. Sequence=" +
                $"{activeSequence}. Automatic popup input is disabled and " +
                "terrain remains fail-closed until native state is safe.");
            return;
        }

        if (phase is BonusStageRetryPhase.Completed or
            BonusStageRetryPhase.Failed)
        {
            BonusRunnerLog.Debug(
                $"RetryOutcomeSuperseded PriorSequence={activeSequence}, " +
                $"PriorPhase={phase}. A newer native prompt is authoritative.",
                "Retry");
        }

        ClearActiveState();
        activeSequence = ++sequenceCounter;
        pendingPrompt = prompt;
        promptInstanceId = instanceId;
        try
        {
            pendingIcon = prompt.secondWindIcon;
        }
        catch (System.Exception exception)
        {
            pendingIcon = null;
            BonusRunnerLog.Warning(
                $"RetryPromptIconReadFailed Sequence={activeSequence}, " +
                $"Prompt={promptInstanceId}, Error=" +
                $"{exception.GetType().Name}:{exception.Message}.");
        }

        phase = BonusStageRetryPhase.PromptObserved;
        phaseStartedAtRealtime = Time.realtimeSinceStartup;
        nextGateLogAtRealtime = 0f;
        JumpController.ReleaseOwnedInputForRetryModal();
        if (SafeInstanceId(pendingIcon) == 0)
        {
            Fail(
                $"Retry prompt icon identity could not be verified. " +
                $"Sequence={activeSequence}, Prompt={promptInstanceId}. " +
                "No popup action was dispatched.");
        }
        if (patchInventoryChecked && !patchInventoryReady)
        {
            Fail(
                $"Retry prompt was observed, but verified Harmony inventory " +
                $"is incomplete: {patchInventoryEvidence}. No automatic " +
                "popup action was dispatched.");
        }
        BonusRunnerLog.Debug(
            $"RetryPromptObserved Sequence={activeSequence}, Frame=" +
            $"{Time.frameCount}, Prompt={promptInstanceId}, Icon=" +
            $"{SafeInstanceId(pendingIcon)}, NativeUsed=" +
            $"{SafeSecondWindUsed(prompt)}, GameState=" +
            $"{SafeCurrentGameState()}, IsBonusAtObservation=" +
            $"{SafeIsBonus()}. The prompt remains owned across native " +
            "failure-state transitions until its exact popup appears.",
            "Retry");
    }

    internal static void ObservePopupShown(
        Popup popup,
        PopupData data,
        bool overrideShow,
        bool wasVisible)
    {
        if (phase != BonusStageRetryPhase.PromptObserved ||
            pendingPrompt == null)
        {
            return;
        }

        bool newlyPresented = overrideShow || !wasVisible;
        bool hasActions = false;
        bool iconMatches = false;
        int dataIconId = 0;
        try
        {
            hasActions = data != null &&
                data.confirmAction != null &&
                data.cancelAction != null;
            Sprite dataIcon = data?.sprite;
            dataIconId = SafeInstanceId(dataIcon);
            iconMatches = pendingIcon != null &&
                dataIcon != null &&
                dataIcon == pendingIcon;
        }
        catch (System.Exception exception)
        {
            BonusRunnerLog.Warning(
                $"RetryPopupMatchFailed Sequence={activeSequence}, " +
                $"Error={exception.GetType().Name}:{exception.Message}. " +
                "The popup was not claimed.");
            return;
        }

        int observedPopupId = SafeInstanceId(popup);
        int expectedIconId = SafeInstanceId(pendingIcon);
        if (popup == null ||
            observedPopupId == 0 ||
            !newlyPresented ||
            !hasActions ||
            !iconMatches ||
            dataIconId == 0 ||
            expectedIconId == 0)
        {
            BonusRunnerLog.Debug(
                $"RetryPopupRejected Sequence={activeSequence}, Frame=" +
                $"{Time.frameCount}, Popup={observedPopupId}, " +
                $"NewlyPresented={newlyPresented}, HasActions=" +
                $"{hasActions}, IconMatches={iconMatches}, DataIcon=" +
                $"{dataIconId}, ExpectedIcon={expectedIconId}, " +
                $"OverrideShow={overrideShow}, WasVisible={wasVisible}.",
                "Retry");
            return;
        }

        pendingPopup = popup;
        popupInstanceId = observedPopupId;
        phase = BonusStageRetryPhase.PopupPresented;
        phaseStartedAtRealtime = Time.realtimeSinceStartup;
        nextInvocationAtRealtime = phaseStartedAtRealtime;
        readyFrame = Time.frameCount + 1;
        nextGateLogAtRealtime = 0f;
        BonusRunnerLog.Debug(
            $"RetryPopupMatched Sequence={activeSequence}, Frame=" +
            $"{Time.frameCount}, ReadyFrame={readyFrame}, Prompt=" +
            $"{promptInstanceId}, Popup={popupInstanceId}, Icon=" +
            $"{SafeInstanceId(pendingIcon)}, OverrideShow={overrideShow}. " +
            "The next frame must revalidate the visible popup and its " +
            "interactable native button before input is dispatched.",
            "Retry");
    }

    internal static void ObserveStageState(
        bool isBonusStage,
        bool isActiveGameplay,
        bool hasPlayer,
        bool characterFellOff,
        string gameStateName)
    {
        lastObservedBonusState = isBonusStage;
        lastObservedActiveGameplay = isActiveGameplay;
        lastObservedHasPlayer = hasPlayer;
        lastObservedCharacterFellOff = characterFellOff;
        lastObservedGameState = string.IsNullOrWhiteSpace(gameStateName)
            ? "Unknown"
            : gameStateName;

        if (phase == BonusStageRetryPhase.Failed)
        {
            bool popupVisibilityReadable =
                TryGetPopupVisibility(out bool modalStillVisible);
            if (outcomeReported &&
                popupVisibilityReadable &&
                !modalStillVisible &&
                (!isBonusStage ||
                 isActiveGameplay && hasPlayer))
            {
                BonusRunnerLog.Debug(
                    $"RetryTerminalGateReleased Sequence={activeSequence}, " +
                    $"IsBonus={isBonusStage}, Active={isActiveGameplay}, " +
                    $"HasPlayer={hasPlayer}, PopupVisible=" +
                    $"{modalStillVisible}, PopupVisibilityReadable=" +
                    $"{popupVisibilityReadable}. Native state is safe for a fresh " +
                    "lifecycle decision.",
                    "Retry");
                ClearActiveState();
            }
            return;
        }

        bool popupVisibilityAvailable =
            TryGetPopupVisibility(out bool popupVisible);
        if (phase == BonusStageRetryPhase.AwaitingExit &&
            !isBonusStage &&
            popupVisibilityAvailable &&
            !popupVisible)
        {
            if (exitIsFailureFallback)
            {
                Complete(
                    false,
                    true,
                    fallbackFailureDetail +
                    $" Native No fallback exited the Bonus Stage safely. " +
                    $"Sequence={activeSequence}, ContinueAttempts=" +
                    $"{invocationAttempts}, GameState=" +
                    $"{lastObservedGameState}.");
            }
            else
            {
                Complete(
                    true,
                    false,
                    $"No acknowledged: Bonus Stage exited after native " +
                    $"cancel action. Sequence={activeSequence}, Attempts=" +
                    $"{invocationAttempts}, GameState=" +
                    $"{lastObservedGameState}.");
            }
            return;
        }

        if (phase == BonusStageRetryPhase.AwaitingBonusResume)
        {
            bool resumeEvidence =
                isBonusStage &&
                isActiveGameplay &&
                hasPlayer &&
                popupVisibilityAvailable &&
                !popupVisible;
            if (!resumeEvidence)
            {
                resumeEvidenceFrames = 0;
                lastResumeEvidenceFrame = -1;
            }
            else if (Time.frameCount != lastResumeEvidenceFrame)
            {
                lastResumeEvidenceFrame = Time.frameCount;
                resumeEvidenceFrames++;
                BonusRunnerLog.Debug(
                    $"RetryResumeEvidence Sequence={activeSequence}, " +
                    $"Frame={Time.frameCount}, StableFrames=" +
                    $"{resumeEvidenceFrames}/2, GameState=" +
                    $"{lastObservedGameState}, IsBonus={isBonusStage}, " +
                    $"Active={isActiveGameplay}, HasPlayer={hasPlayer}, " +
                    $"FellOff={characterFellOff}, PopupVisible=" +
                    $"{popupVisible}, PopupVisibilityReadable=" +
                    $"{popupVisibilityAvailable}.",
                    "Retry");
            }

            if (resumeEvidenceFrames >= 2)
            {
                Complete(
                    true,
                    outcomeWarning,
                    outcomeDetail +
                    $" Bonus gameplay resumed for two distinct frames in " +
                    $"GameState={lastObservedGameState}.");
                return;
            }
        }

        float elapsed = Time.realtimeSinceStartup - phaseStartedAtRealtime;
        if (phase == BonusStageRetryPhase.AwaitingContinueReward &&
            continueCloseObservedAt >= 0f &&
            Time.realtimeSinceStartup - continueCloseObservedAt >=
                ContinueCloseGraceSeconds)
        {
            string closeFailure =
                $"Second Wind closed without RewardForShowing or OnError " +
                $"within {ContinueCloseGraceSeconds:F1}s. Sequence=" +
                $"{activeSequence}, Attempt={invocationAttempts}.";
            continueInvocationOwned = false;
            continueCloseObservedAt = -1f;
            if (invocationAttempts >= MaximumInvocationAttempts)
            {
                BeginFallbackCancel(closeFailure);
            }
            else
            {
                RearmPopupInvocation(closeFailure);
            }
            return;
        }

        switch (phase)
        {
            case BonusStageRetryPhase.PromptObserved
                when elapsed >= PromptTimeoutSeconds:
                Fail(
                    $"Retry prompt timed out before the exact Second Wind " +
                    $"popup was observed. Sequence={activeSequence}, " +
                    $"Prompt={promptInstanceId}, Elapsed={elapsed:F2}s, " +
                    $"GameState={lastObservedGameState}.");
                break;
            case BonusStageRetryPhase.PopupPresented
                when elapsed >= PopupReadyTimeoutSeconds:
                string popupFailure =
                    $"Retry popup never became actionable. Sequence=" +
                    $"{activeSequence}, Popup={popupInstanceId}, Attempts=" +
                    $"{invocationAttempts}, Elapsed={elapsed:F2}s.";
                if (lastRequestedContinue &&
                    !forceCancelAfterRetryFailure)
                {
                    BeginFallbackCancel(popupFailure);
                }
                else
                {
                    Fail(popupFailure);
                }
                break;
            case BonusStageRetryPhase.AwaitingContinueReward
                when elapsed >= ContinueAcknowledgementTimeoutSeconds:
                BeginFallbackCancel(
                    $"Continue request received no RewardForShowing " +
                    $"acknowledgement. Sequence={activeSequence}, Prompt=" +
                    $"{promptInstanceId}, Attempts={invocationAttempts}, " +
                    $"Elapsed={elapsed:F2}s. The one-use flag was not " +
                    "modified because native retry success was not proven.");
                break;
            case BonusStageRetryPhase.AwaitingBonusResume
                when elapsed >= BonusResumeTimeoutSeconds:
                Fail(
                    outcomeDetail +
                    $" Native reward succeeded, but no Bonus Stage resume " +
                    $"was observed within {elapsed:F2}s; terrain control " +
                    "remained blocked throughout the boundary.");
                break;
            case BonusStageRetryPhase.AwaitingExit
                when elapsed >= ExitAcknowledgementTimeoutSeconds:
                Fail(
                    $"Native cancel action did not produce a Bonus Stage " +
                    $"exit acknowledgement. Sequence={activeSequence}, " +
                    $"Attempts={invocationAttempts}, Elapsed={elapsed:F2}s, " +
                    $"GameState={lastObservedGameState}.");
                break;
        }
    }

    internal static bool TryBeginInvocation(
        bool continueRequested,
        out Popup popup,
        out long sequence,
        out int attempt,
        out bool actualContinueRequested,
        out string evidence)
    {
        popup = null;
        sequence = activeSequence;
        attempt = invocationAttempts;
        actualContinueRequested = false;
        evidence = string.Empty;
        if (phase != BonusStageRetryPhase.PopupPresented)
            return false;

        if (!patchInventoryChecked || !patchInventoryReady)
        {
            Fail(
                $"Retry action blocked because Harmony inventory is " +
                $"{(!patchInventoryChecked ? "not checked" : "incomplete")}: " +
                $"{patchInventoryEvidence}.");
            return false;
        }

        lastRequestedContinue = continueRequested;
        bool dispatchContinue =
            continueRequested &&
            !forceCancelAfterRetryFailure;

        if (Time.frameCount < readyFrame ||
            Time.realtimeSinceStartup < nextInvocationAtRealtime)
        {
            return false;
        }

        if (dispatchContinue &&
            invocationAttempts >= MaximumInvocationAttempts)
        {
            BeginFallbackCancel(
                $"Retry invocation limit reached before dispatch. " +
                $"Sequence={activeSequence}, Attempts={invocationAttempts}.");
            return false;
        }

        if (forceCancelAfterRetryFailure && fallbackCancelAttempted)
            return false;

        if (!TryValidatePopupAction(
                dispatchContinue,
                out string validationEvidence))
        {
            if (Time.realtimeSinceStartup >= nextGateLogAtRealtime)
            {
                nextGateLogAtRealtime =
                    Time.realtimeSinceStartup + 1f;
                BonusRunnerLog.Debug(
                    $"RetryActionGate Sequence={activeSequence}, Frame=" +
                    $"{Time.frameCount}, RequestedContinue=" +
                    $"{continueRequested}, DispatchContinue=" +
                    $"{dispatchContinue}, FallbackCancel=" +
                    $"{forceCancelAfterRetryFailure}, " +
                    $"Evidence={validationEvidence}.",
                    "Retry");
            }
            return false;
        }

        if (forceCancelAfterRetryFailure)
        {
            fallbackCancelAttempted = true;
            exitIsFailureFallback = true;
            attempt = invocationAttempts + 1;
        }
        else
        {
            invocationAttempts++;
            attempt = invocationAttempts;
            exitIsFailureFallback = false;
        }
        popup = pendingPopup;
        sequence = activeSequence;
        actualContinueRequested = dispatchContinue;
        evidence = validationEvidence;
        continueInvocationOwned = dispatchContinue;
        continueCloseObservedAt = -1f;
        phase = dispatchContinue
            ? BonusStageRetryPhase.AwaitingContinueReward
            : BonusStageRetryPhase.AwaitingExit;
        phaseStartedAtRealtime = Time.realtimeSinceStartup;
        nextGateLogAtRealtime = 0f;
        return true;
    }

    internal static void ReportInvocationFailure(
        long sequence,
        string error,
        out bool willRetry)
    {
        willRetry = false;
        if (sequence != activeSequence ||
            phase is not (BonusStageRetryPhase.AwaitingContinueReward or
                BonusStageRetryPhase.AwaitingExit))
        {
            BonusRunnerLog.Warning(
                $"RetryInvocationExceptionIgnored Sequence={sequence}, " +
                $"ActiveSequence={activeSequence}, Phase={phase}, " +
                $"Error={error}.");
            return;
        }

        bool failedFallbackCancel =
            phase == BonusStageRetryPhase.AwaitingExit &&
            exitIsFailureFallback;
        continueInvocationOwned = false;
        if (failedFallbackCancel)
        {
            Fail(
                fallbackFailureDetail +
                $" Native No fallback invocation threw {error}; terrain " +
                "control remains fail-closed while the modal is visible.");
            return;
        }
        if (invocationAttempts >= MaximumInvocationAttempts)
        {
            BeginFallbackCancel(
                $"Retry invocation failed after {invocationAttempts} " +
                $"attempts. Sequence={activeSequence}, Error={error}.");
            return;
        }

        RearmPopupInvocation(
            $"Retry invocation threw {error}; the popup will be " +
            "revalidated before another attempt.");
        willRetry = true;
    }

    internal static void MarkContinueError(SecondWind prompt)
    {
        int instanceId = SafeInstanceId(prompt);
        if (phase != BonusStageRetryPhase.AwaitingContinueReward ||
            !continueInvocationOwned ||
            instanceId == 0 ||
            instanceId != promptInstanceId)
        {
            return;
        }

        continueInvocationOwned = false;
        if (invocationAttempts >= MaximumInvocationAttempts)
        {
            BeginFallbackCancel(
                $"Native Second Wind reported an error after " +
                $"{invocationAttempts} attempts. Sequence={activeSequence}.");
            return;
        }

        RearmPopupInvocation(
            $"Native Second Wind reported an error. Sequence=" +
            $"{activeSequence}, Attempt={invocationAttempts}.");
        BonusRunnerLog.Warning(
            $"RetryNativeError Sequence={activeSequence}, Attempt=" +
            $"{invocationAttempts}. The same verified popup will be " +
            $"revalidated before retrying; limit={MaximumInvocationAttempts}.");
    }

    internal static void MarkContinueClosed(SecondWind prompt)
    {
        int instanceId = SafeInstanceId(prompt);
        if (phase != BonusStageRetryPhase.AwaitingContinueReward ||
            !continueInvocationOwned ||
            instanceId == 0 ||
            instanceId != promptInstanceId)
        {
            return;
        }

        if (continueCloseObservedAt < 0f)
        {
            continueCloseObservedAt = Time.realtimeSinceStartup;
            BonusRunnerLog.Debug(
                $"RetryNativeCloseObserved Sequence={activeSequence}, " +
                $"Attempt={invocationAttempts}, Prompt={promptInstanceId}. " +
                $"RewardForShowing or OnError retains priority for " +
                $"{ContinueCloseGraceSeconds:F1}s before this is treated " +
                "as an unacknowledged ad close.",
                "Retry");
        }
    }

    internal static void MarkRewardGranted(SecondWind prompt)
    {
        int instanceId = SafeInstanceId(prompt);
        if (phase != BonusStageRetryPhase.AwaitingContinueReward ||
            !continueInvocationOwned ||
            instanceId == 0 ||
            instanceId != promptInstanceId)
        {
            BonusRunnerLog.Debug(
                $"RetryRewardUnowned Prompt={instanceId}, ExpectedPrompt=" +
                $"{promptInstanceId}, Sequence={activeSequence}, Phase=" +
                $"{phase}. Native/manual rewards are not modified.",
                "Retry");
            return;
        }

        // RewardForShowing has completed at this postfix, so native retry
        // success is proven. Second Wind is a native one-use opportunity:
        // observe its consumed flag, but never rewrite it. Invocation retries
        // above are only for an unacknowledged click on this same popup; they
        // must not manufacture another gameplay retry after success.
        bool nativeUsedAfterAcknowledgement = SafeSecondWindUsed(prompt);

        continueInvocationOwned = false;
        continueCloseObservedAt = -1f;
        phase = BonusStageRetryPhase.AwaitingBonusResume;
        phaseStartedAtRealtime = Time.realtimeSinceStartup;
        resumeEvidenceFrames = 0;
        lastResumeEvidenceFrame = -1;
        outcomeSucceeded = true;
        outcomeWarning = !nativeUsedAfterAcknowledgement;
        outcomeDetail =
            $"Continue acknowledged by RewardForShowing. Sequence=" +
            $"{activeSequence}, Attempt={invocationAttempts}, Prompt=" +
            $"{promptInstanceId}, NativeUsedAfterAcknowledgement=" +
            $"{nativeUsedAfterAcknowledgement}, Rearmed=False. The native " +
            "one-use opportunity remains consumed.";
        BonusRunnerLog.Debug(
            outcomeDetail +
            " Terrain input stays blocked until detector evidence confirms " +
            "that Bonus gameplay resumed.",
            "Retry");
    }

    internal static bool TryTakeOutcome(
        out bool succeeded,
        out bool warning,
        out string detail)
    {
        succeeded = false;
        warning = false;
        detail = string.Empty;
        if (phase is not (BonusStageRetryPhase.Completed or
            BonusStageRetryPhase.Failed))
        {
            return false;
        }
        if (phase == BonusStageRetryPhase.Failed && outcomeReported)
            return false;

        succeeded = outcomeSucceeded;
        warning = outcomeWarning;
        detail = outcomeDetail;
        if (phase == BonusStageRetryPhase.Failed)
            outcomeReported = true;
        else
            ClearActiveState();
        return true;
    }

    internal static void Reset(string reason)
    {
        if (phase != BonusStageRetryPhase.Idle)
        {
            BonusRunnerLog.Debug(
                $"RetryBridgeReset Reason={reason}, Sequence=" +
                $"{activeSequence}, Phase={phase}, Attempts=" +
                $"{invocationAttempts}.",
                "Retry");
        }
        ClearActiveState();
    }

    private static bool TryValidatePopupAction(
        bool continueRequested,
        out string evidence)
    {
        evidence = string.Empty;
        try
        {
            int currentPromptId = SafeInstanceId(pendingPrompt);
            if (pendingPrompt == null ||
                promptInstanceId == 0 ||
                currentPromptId == 0 ||
                currentPromptId != promptInstanceId)
            {
                evidence = "PromptUnavailableOrIdentityChanged";
                return false;
            }

            int currentPopupId = SafeInstanceId(pendingPopup);
            if (pendingPopup == null ||
                popupInstanceId == 0 ||
                currentPopupId == 0 ||
                currentPopupId != popupInstanceId)
            {
                evidence = "PopupUnavailableOrIdentityChanged";
                return false;
            }

            if (!pendingPopup.IsVisible())
            {
                evidence = "PopupNotVisible";
                return false;
            }

            int expectedIconId = SafeInstanceId(pendingIcon);
            int visibleIconId = SafeInstanceId(
                pendingPopup.image?.sprite);
            if (pendingPopup.image == null ||
                pendingPopup.image.sprite == null ||
                pendingIcon == null ||
                expectedIconId == 0 ||
                visibleIconId == 0 ||
                pendingPopup.image.sprite != pendingIcon)
            {
                evidence =
                    $"VisibleIconMismatch(Display=" +
                    $"{visibleIconId},Expected={expectedIconId})";
                return false;
            }

            var button = continueRequested
                ? pendingPopup.confirmButton
                : pendingPopup.cancelButton;
            if (button == null)
            {
                evidence = continueRequested
                    ? "ConfirmButtonUnavailable"
                    : "CancelButtonUnavailable";
                return false;
            }

            if (!button.gameObject.activeInHierarchy)
            {
                evidence = continueRequested
                    ? "ConfirmButtonInactive"
                    : "CancelButtonInactive";
                return false;
            }

            if (!button.interactable)
            {
                evidence = continueRequested
                    ? "ConfirmButtonNotInteractable"
                    : "CancelButtonNotInteractable";
                return false;
            }

            evidence =
                $"Popup={popupInstanceId},Prompt={promptInstanceId}," +
                $"Icon={SafeInstanceId(pendingIcon)},Visible=True," +
                $"Button={(continueRequested ? "Confirm" : "Cancel")}," +
                "Active=True,Interactable=True";
            return true;
        }
        catch (System.Exception exception)
        {
            evidence =
                $"ValidationException={exception.GetType().Name}:" +
                exception.Message;
            return false;
        }
    }

    private static void Complete(
        bool succeeded,
        bool warning,
        string detail)
    {
        phase = succeeded
            ? BonusStageRetryPhase.Completed
            : BonusStageRetryPhase.Failed;
        outcomeSucceeded = succeeded;
        outcomeWarning = warning;
        outcomeDetail = detail;
        phaseStartedAtRealtime = Time.realtimeSinceStartup;
    }

    private static void RearmPopupInvocation(string reason)
    {
        phase = BonusStageRetryPhase.PopupPresented;
        phaseStartedAtRealtime = Time.realtimeSinceStartup;
        nextInvocationAtRealtime =
            phaseStartedAtRealtime + InvocationRetryDelaySeconds;
        readyFrame = Time.frameCount + 1;
        nextGateLogAtRealtime = 0f;
        continueCloseObservedAt = -1f;
        BonusRunnerLog.Debug(
            $"RetryPopupRearmed Sequence={activeSequence}, Attempts=" +
            $"{invocationAttempts}, Reason={reason}",
            "Retry");
    }

    private static void BeginFallbackCancel(string reason)
    {
        continueInvocationOwned = false;
        continueCloseObservedAt = -1f;
        forceCancelAfterRetryFailure = true;
        fallbackFailureDetail = reason;
        if (pendingPopup == null)
        {
            Fail(
                reason +
                " No verified popup remains for the native No fallback; " +
                "terrain control remains fail-closed.");
            return;
        }

        phase = BonusStageRetryPhase.PopupPresented;
        phaseStartedAtRealtime = Time.realtimeSinceStartup;
        nextInvocationAtRealtime = phaseStartedAtRealtime;
        readyFrame = Time.frameCount + 1;
        nextGateLogAtRealtime = 0f;
        BonusRunnerLog.Warning(
            $"RetryFallbackCancelArmed Sequence={activeSequence}, " +
            $"ContinueAttempts={invocationAttempts}, Reason={reason} " +
            "The exact popup will be revalidated, then its real No button " +
            "will exit safely instead of resuming terrain behind a modal.");
    }

    private static void Fail(string detail)
    {
        continueInvocationOwned = false;
        Complete(false, true, detail);
    }

    private static void ClearActiveState()
    {
        phase = BonusStageRetryPhase.Idle;
        pendingPrompt = null;
        pendingPopup = null;
        pendingIcon = null;
        promptInstanceId = 0;
        popupInstanceId = 0;
        activeSequence = 0;
        invocationAttempts = 0;
        readyFrame = 0;
        phaseStartedAtRealtime = 0f;
        nextInvocationAtRealtime = 0f;
        nextGateLogAtRealtime = 0f;
        continueInvocationOwned = false;
        forceCancelAfterRetryFailure = false;
        fallbackCancelAttempted = false;
        exitIsFailureFallback = false;
        lastRequestedContinue = false;
        continueCloseObservedAt = -1f;
        fallbackFailureDetail = string.Empty;
        resumeEvidenceFrames = 0;
        lastResumeEvidenceFrame = -1;
        outcomeReported = false;
        outcomeSucceeded = false;
        outcomeWarning = false;
        outcomeDetail = string.Empty;
    }

    private static int SafeInstanceId(UnityEngine.Object value)
    {
        try
        {
            return value != null ? value.GetInstanceID() : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static bool SafeSecondWindUsed(SecondWind prompt)
    {
        try
        {
            return prompt != null && prompt.secondWindUsed;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetPopupVisibility(out bool visible)
    {
        visible = true;
        try
        {
            if (pendingPopup == null)
            {
                visible = false;
                return true;
            }
            visible = pendingPopup.IsVisible();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string SafeCurrentGameState()
    {
        try
        {
            return GameState.current.ToString();
        }
        catch
        {
            return "Unavailable";
        }
    }

    private static bool SafeIsBonus()
    {
        try
        {
            return GameState.IsBonus();
        }
        catch
        {
            return false;
        }
    }
}

[HarmonyPatch(typeof(SecondWind), nameof(SecondWind.SecondWindSuggest))]
internal static class BonusStageSecondWindPromptPatch
{
    [HarmonyPostfix]
    private static void Postfix(SecondWind __instance)
    {
        try
        {
            BonusStageRetryBridge.MarkPromptShown(__instance);
        }
        catch (System.Exception exception)
        {
            BonusStageRetryBridge.ReportHarmonyCallbackFailure(
                "SecondWindSuggest.Postfix",
                exception,
                true);
        }
    }
}

[HarmonyPatch(
    typeof(Popup),
    nameof(Popup.Show),
    new System.Type[] { typeof(PopupData), typeof(bool) })]
internal static class BonusStageRetryPopupPatch
{
    [HarmonyPrefix]
    private static void Prefix(Popup __instance, out bool __state)
    {
        try
        {
            __state = __instance != null && __instance.IsVisible();
        }
        catch
        {
            __state = false;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(
        Popup __instance,
        PopupData __0,
        bool __1,
        bool __state)
    {
        try
        {
            BonusStageRetryBridge.ObservePopupShown(
                __instance,
                __0,
                __1,
                __state);
        }
        catch (System.Exception exception)
        {
            BonusStageRetryBridge.ReportHarmonyCallbackFailure(
                "Popup.Show.Postfix",
                exception);
        }
    }
}

[HarmonyPatch(typeof(SecondWind), nameof(SecondWind.RewardForShowing))]
internal static class BonusStageSecondWindRewardPatch
{
    [HarmonyPostfix]
    private static void Postfix(SecondWind __instance)
    {
        try
        {
            BonusStageRetryBridge.MarkRewardGranted(__instance);
        }
        catch (System.Exception exception)
        {
            BonusStageRetryBridge.ReportHarmonyCallbackFailure(
                "SecondWind.RewardForShowing.Postfix",
                exception);
        }
    }
}

[HarmonyPatch(typeof(SecondWind), nameof(SecondWind.OnError))]
internal static class BonusStageSecondWindErrorPatch
{
    [HarmonyPostfix]
    private static void Postfix(SecondWind __instance)
    {
        try
        {
            BonusStageRetryBridge.MarkContinueError(__instance);
        }
        catch (System.Exception exception)
        {
            BonusStageRetryBridge.ReportHarmonyCallbackFailure(
                "SecondWind.OnError.Postfix",
                exception);
        }
    }
}

[HarmonyPatch(typeof(SecondWind), nameof(SecondWind.OnClose))]
internal static class BonusStageSecondWindClosePatch
{
    [HarmonyPostfix]
    private static void Postfix(SecondWind __instance)
    {
        try
        {
            BonusStageRetryBridge.MarkContinueClosed(__instance);
        }
        catch (System.Exception exception)
        {
            BonusStageRetryBridge.ReportHarmonyCallbackFailure(
                "SecondWind.OnClose.Postfix",
                exception);
        }
    }
}
