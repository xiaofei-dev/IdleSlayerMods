using AutoBonusRunner.Diagnostics;
using Il2Cpp;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AutoBonusRunner.Control;

internal sealed class JumpController
{
    private static JumpController activeController;
    private static float nextHoldPatchErrorLogTime;
    internal bool IsHoldingJump { get; private set; }
    internal float LastPressStartedAt { get; private set; } = -1f;
    internal float LastReleaseAt { get; private set; } = -1f;
    internal int LastReleaseHeldFixedSteps { get; private set; }
    internal int LastReleaseDuplicateFixedCallbacks { get; private set; }
    private bool releasePulsePending;
    private float releaseAt;
    private int heldFixedSteps;
    private int maximumHeldFixedSteps;
    private bool exactFixedStepRelease;
    private double lastCountedFixedTime = double.NaN;
    private float lastFixedRefreshAt = -1f;
    private int duplicateFixedCallbacks;
    private float nextHoldStateLogTime;
    private PointerEventData pointerEventData;
    private JumpPanel pressedPanel;
    private int pressedPlayerInstanceId;

    internal void Press(
        PlayerMovement player,
        float holdSeconds,
        string reason,
        int fixedStepHoldLimit = 0)
    {
        if (BonusStageRetryBridge.BlocksTerrainControl)
        {
            Release();
            return;
        }
        if (player == null || IsHoldingJump) return;
        JumpPanel panel = JumpPanel.instance;
        EventSystem eventSystem = EventSystem.current;
        if (panel == null || eventSystem == null)
        {
            BonusRunnerLog.Warning(
                $"Automatic jump input unavailable: JumpPanel={panel != null}, EventSystem={eventSystem != null}.");
            IsHoldingJump = false;
            releasePulsePending = false;
            heldFixedSteps = 0;
            maximumHeldFixedSteps = 0;
            exactFixedStepRelease = false;
            lastCountedFixedTime = double.NaN;
            lastFixedRefreshAt = -1f;
            duplicateFixedCallbacks = 0;
            pointerEventData = null;
            pressedPanel = null;
            pressedPlayerInstanceId = 0;
            if (activeController == this)
                activeController = null;
            return;
        }

        IsHoldingJump = true;
        releasePulsePending = false;
        float clampedHold = Mathf.Clamp(holdSeconds, 0.02f, 0.18f);
        releaseAt = Time.unscaledTime + clampedHold;
        heldFixedSteps = 0;
        float fixedDelta = Mathf.Clamp(Time.fixedDeltaTime, 0.005f, 0.05f);
        int derivedFixedStepLimit = Mathf.Max(
            1,
            Mathf.CeilToInt(clampedHold / fixedDelta - 0.0001f));
        maximumHeldFixedSteps = Mathf.Clamp(
            fixedStepHoldLimit > 0
                ? fixedStepHoldLimit
                : derivedFixedStepLimit,
            1,
            64);
        exactFixedStepRelease = fixedStepHoldLimit > 0;
        lastCountedFixedTime = double.NaN;
        lastFixedRefreshAt = -1f;
        duplicateFixedCallbacks = 0;
        pressedPanel = panel;
        pressedPlayerInstanceId = player.GetInstanceID();
        pointerEventData = new PointerEventData(eventSystem)
        {
            position = new Vector2(Screen.width * 0.8f, Screen.height)
        };
        LastPressStartedAt = Time.unscaledTime;
        LastReleaseAt = -1f;
        LastReleaseHeldFixedSteps = 0;
        LastReleaseDuplicateFixedCallbacks = 0;
        nextHoldStateLogTime = Time.unscaledTime;
        activeController = this;
        try
        {
            panel.OnPointerDown(pointerEventData);
        }
        catch
        {
            IsHoldingJump = false;
            releasePulsePending = false;
            heldFixedSteps = 0;
            maximumHeldFixedSteps = 0;
            exactFixedStepRelease = false;
            lastCountedFixedTime = double.NaN;
            lastFixedRefreshAt = -1f;
            duplicateFixedCallbacks = 0;
            pointerEventData = null;
            pressedPanel = null;
            pressedPlayerInstanceId = 0;
            if (activeController == this)
                activeController = null;
            throw;
        }
        BonusRunnerLog.Debug(
            $"Automatic jump DOWN: Hold={holdSeconds:F3}s, " +
            $"FixedStepLimit=" +
            $"{(maximumHeldFixedSteps > 0 ? maximumHeldFixedSteps.ToString() : "TimeScheduled")}, " +
            $"ReleasePolicy=" +
            $"{(exactFixedStepRelease ? "ExactFixedStep" : "TimeWithFixedStepCeiling")}, " +
            $"Reason={reason}",
            "Control");
        BonusRunnerLog.Debug(
            $"JumpPanel after DOWN: Down={JumpPanel.jumpDown}, Pressed={JumpPanel.jumpPressed}, " +
            $"Up={JumpPanel.jumpUp}, HeldPointers={panel.heldPointerCount}; " +
            $"PlayerJump: IsJumping={player.isJumping}, JumpTime={player.jumpTime:F3}, " +
            $"Counter={player.jumpTimeCounter:F3}, Force={player.jumpForce:F3}",
            "Control");
    }

    internal bool Pulse(
        PlayerMovement player,
        string reason)
    {
        if (BonusStageRetryBridge.BlocksTerrainControl)
            return false;
        if (player == null || IsHoldingJump)
            return false;

        JumpPanel panel = JumpPanel.instance;
        EventSystem eventSystem = EventSystem.current;
        if (panel == null || eventSystem == null)
        {
            BonusRunnerLog.Warning(
                $"Automatic contextual pulse unavailable: JumpPanel=" +
                $"{panel != null}, EventSystem={eventSystem != null}, " +
                $"Reason={reason}.");
            return false;
        }

        PointerEventData data = new(eventSystem)
        {
            position = new Vector2(Screen.width * 0.8f, Screen.height)
        };
        bool pointerDownSent = false;
        float now = Time.unscaledTime;
        LastPressStartedAt = now;
        LastReleaseAt = now;
        LastReleaseHeldFixedSteps = 0;
        try
        {
            panel.OnPointerDown(data);
            pointerDownSent = true;
            panel.OnPointerUp(data);
            BonusRunnerLog.Debug(
                $"Automatic contextual pulse DOWN+UP: Reason={reason}, " +
                $"Pressed={JumpPanel.jumpPressed}, Down=" +
                $"{JumpPanel.jumpDown}, Up={JumpPanel.jumpUp}, " +
                $"HeldPointers={panel.heldPointerCount}.",
                "Control");
            return true;
        }
        catch (System.Exception exception)
        {
            if (pointerDownSent)
            {
                try
                {
                    panel.OnPointerUp(data);
                }
                catch
                {
                    // Preserve the original failure; the global runtime gate
                    // will not acquire ownership from this one-shot pulse.
                }
            }
            BonusRunnerLog.Warning(
                $"Automatic contextual pulse failed safely: Reason=" +
                $"{reason}, Exception={exception.GetType().Name}: " +
                $"{exception.Message}.");
            return false;
        }
    }

    internal void Update(PlayerMovement player)
    {
        if (player == null)
        {
            Release();
            return;
        }

        if (IsHoldingJump &&
            pressedPlayerInstanceId != 0 &&
            player.GetInstanceID() != pressedPlayerInstanceId)
        {
            AbortHeldInput(
                "PlayerInstanceChangedBeforeRuntimeReset",
                player);
            return;
        }

        if (IsHoldingJump)
        {
            // Ordinary ground plans retain their calibrated wall-clock release
            // while the fixed-step count is only a background safety ceiling.
            // Face solvers pass an explicit count and therefore use exact
            // fixed-step delivery. If that Harmony callback is unavailable,
            // a render-time fail-safe still prevents an indefinite pointer.
            if (maximumHeldFixedSteps > 0 && exactFixedStepRelease)
            {
                float callbackGrace = Mathf.Max(
                    0.10f,
                    Mathf.Clamp(Time.fixedDeltaTime, 0.005f, 0.05f) * 3f);
                float lastFixedActivity = lastFixedRefreshAt >= 0f
                    ? lastFixedRefreshAt
                    : LastPressStartedAt;
                if (Time.unscaledTime >= releaseAt + callbackGrace &&
                    Time.unscaledTime - lastFixedActivity >= callbackGrace)
                {
                    ReleaseAtWallClock(
                        player,
                        "FixedCallbackUnavailableFallback");
                }
                return;
            }
            if (Time.unscaledTime < releaseAt)
            {
                return;
            }

            ReleaseAtWallClock(player, "TimeSchedule");
            return;
        }

        if (releasePulsePending)
        {
            releasePulsePending = false;
        }
    }

    internal void Release()
    {
        bool ownedInputActive = IsHoldingJump || pointerEventData != null;
        if (IsHoldingJump)
            RecordReleaseTime();
        if (ownedInputActive)
        {
            LastReleaseHeldFixedSteps = heldFixedSteps;
            LastReleaseDuplicateFixedCallbacks =
                duplicateFixedCallbacks;
        }
        IsHoldingJump = false;
        if (activeController == this)
            activeController = null;
        releasePulsePending = false;
        maximumHeldFixedSteps = 0;
        exactFixedStepRelease = false;
        try
        {
            ReleasePointer();
        }
        finally
        {
            heldFixedSteps = 0;
            lastCountedFixedTime = double.NaN;
            lastFixedRefreshAt = -1f;
            duplicateFixedCallbacks = 0;
            pressedPlayerInstanceId = 0;
        }
    }

    private void ReleasePointer()
    {
        JumpPanel panel = pressedPanel;
        PointerEventData data = pointerEventData;
        try
        {
            if (panel != null && data != null)
            {
                panel.OnPointerUp(data);
                BonusRunnerLog.Debug(
                    $"JumpPanel after UP: Down={JumpPanel.jumpDown}, Pressed={JumpPanel.jumpPressed}, " +
                    $"Up={JumpPanel.jumpUp}, HeldPointers={panel.heldPointerCount}", "Control");
            }
        }
        finally
        {
            pointerEventData = null;
            pressedPanel = null;
            pressedPlayerInstanceId = 0;
        }
    }

    private void RecordReleaseTime()
    {
        if (LastPressStartedAt >= 0f && LastReleaseAt < LastPressStartedAt)
            LastReleaseAt = Time.unscaledTime;
    }

    internal static void MaintainHeldJump(
        PlayerMovement player,
        string phase)
    {
        try
        {
            MaintainHeldJumpCore(player, phase);
        }
        catch (System.Exception exception)
        {
            JumpController controller = activeController;
            activeController = null;
            if (controller != null)
            {
                controller.RecordReleaseTime();
                controller.IsHoldingJump = false;
                controller.releasePulsePending = false;
                controller.LastReleaseHeldFixedSteps =
                    controller.heldFixedSteps;
                controller.LastReleaseDuplicateFixedCallbacks =
                    controller.duplicateFixedCallbacks;
                controller.maximumHeldFixedSteps = 0;
                controller.exactFixedStepRelease = false;
                try
                {
                    controller.ReleasePointer();
                }
                catch
                {
                    controller.pointerEventData = null;
                    controller.pressedPanel = null;
                }
                controller.heldFixedSteps = 0;
                controller.lastCountedFixedTime = double.NaN;
                controller.lastFixedRefreshAt = -1f;
                controller.duplicateFixedCallbacks = 0;
                controller.pressedPlayerInstanceId = 0;
            }

            try
            {
                if (Time.unscaledTime >= nextHoldPatchErrorLogTime)
                {
                    nextHoldPatchErrorLogTime = Time.unscaledTime + 5f;
                    BonusRunnerLog.Warning(
                        $"Held-jump patch failed safely in PlayerMovement.{phase}: " +
                        $"{exception.GetType().Name}: {exception.Message}");
                }
            }
            catch
            {
                // A Harmony prefix must never prevent the original game method.
            }
        }
    }

    internal static void ReleaseOwnedInputForRetryModal()
    {
        try
        {
            activeController?.Release();
        }
        catch (System.Exception exception)
        {
            BonusRunnerLog.Warning(
                $"Retry modal failed to release owned jump input safely: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void MaintainHeldJumpCore(
        PlayerMovement player,
        string phase)
    {
        JumpController controller = activeController;
        if (BonusStageRetryBridge.BlocksTerrainControl)
        {
            controller?.Release();
            return;
        }
        if (controller == null ||
            !controller.IsHoldingJump ||
            player == null)
        {
            return;
        }

        if (controller.pressedPlayerInstanceId != 0 &&
            player.GetInstanceID() != controller.pressedPlayerInstanceId)
        {
            controller.AbortHeldInput(
                "PlayerInstanceChangedInPatchedCallback",
                player);
            return;
        }

        bool fixedUpdatePhase = string.Equals(
            phase,
            "FixedUpdate",
            System.StringComparison.Ordinal);
        bool newLogicalFixedStep = false;
        bool duplicateFixedCallback = false;
        if (fixedUpdatePhase &&
            controller.maximumHeldFixedSteps > 0)
        {
            double fixedTime = Time.fixedTimeAsDouble;
            duplicateFixedCallback =
                !double.IsNaN(controller.lastCountedFixedTime) &&
                fixedTime == controller.lastCountedFixedTime;
            if (duplicateFixedCallback)
            {
                controller.duplicateFixedCallbacks++;
            }
            else
            {
                controller.lastCountedFixedTime = fixedTime;
                controller.lastFixedRefreshAt = Time.unscaledTime;
                newLogicalFixedStep = true;
            }
        }

        if (controller.maximumHeldFixedSteps > 0 &&
            controller.heldFixedSteps >=
                controller.maximumHeldFixedSteps &&
            fixedUpdatePhase &&
            newLogicalFixedStep)
        {
            controller.ReleaseAtFixedStepLimit(player);
            return;
        }

        // JumpPanel.LateUpdate can clear the synthetic pressed flag because
        // there is no physical mouse button behind it. Reassert the held flag
        // immediately before PlayerMovement consumes jump input.
        JumpPanel.jumpPressed = true;
        if (fixedUpdatePhase &&
            controller.maximumHeldFixedSteps > 0 &&
            newLogicalFixedStep)
        {
            controller.heldFixedSteps++;
        }

        if (Time.unscaledTime < controller.nextHoldStateLogTime)
            return;

        controller.nextHoldStateLogTime = Time.unscaledTime + 0.10f;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        BonusRunnerLog.Debug(
            $"Held jump refreshed in PlayerMovement.{phase}: " +
            $"Elapsed={Time.unscaledTime - controller.LastPressStartedAt:F3}s, " +
            $"HeldFixedSteps={controller.heldFixedSteps}/" +
            $"{(controller.maximumHeldFixedSteps > 0 ? controller.maximumHeldFixedSteps.ToString() : "TimeScheduled")}, " +
            $"DuplicateFixedCallback={duplicateFixedCallback}, " +
            $"SuppressedFixedCallbacks=" +
            $"{controller.duplicateFixedCallbacks}, " +
            $"Pressed={JumpPanel.jumpPressed}, Down={JumpPanel.jumpDown}, Up={JumpPanel.jumpUp}, " +
            $"IsJumping={player.isJumping}, Counter={player.jumpTimeCounter:F3}/{player.jumpTime:F3}, " +
            $"VY={(body != null ? body.velocity.y : 0f):F3}",
            "Control");
    }

    private void ReleaseAtFixedStepLimit(PlayerMovement player)
    {
        bool usedExactFixedStepRelease = exactFixedStepRelease;
        RecordReleaseTime();
        LastReleaseHeldFixedSteps = heldFixedSteps;
        LastReleaseDuplicateFixedCallbacks = duplicateFixedCallbacks;
        IsHoldingJump = false;
        if (activeController == this)
            activeController = null;
        releasePulsePending = true;
        maximumHeldFixedSteps = 0;
        exactFixedStepRelease = false;
        try
        {
            ReleasePointer();
        }
        finally
        {
            heldFixedSteps = 0;
            lastCountedFixedTime = double.NaN;
            lastFixedRefreshAt = -1f;
            duplicateFixedCallbacks = 0;
            pressedPlayerInstanceId = 0;
        }
        Rigidbody2D body = player != null
            ? player.GetComponent<Rigidbody2D>()
            : null;
        BonusRunnerLog.Debug(
            $"Automatic jump UP: Trigger=FixedStepLimit, " +
            $"ReleasePolicy=" +
            $"{(usedExactFixedStepRelease ? "ExactFixedStep" : "BackgroundSafetyCeiling")}, " +
            $"HeldFixedSteps={LastReleaseHeldFixedSteps}, " +
            $"SuppressedFixedCallbacks=" +
            $"{LastReleaseDuplicateFixedCallbacks}, " +
            $"Elapsed={Time.unscaledTime - LastPressStartedAt:F3}s, " +
            $"VY={(body != null ? body.velocity.y : 0f):F3}. " +
            (usedExactFixedStepRelease
                ? "The actuator and face solver used the same physics tick count."
                : "The background safety ceiling prevented a time-scheduled " +
                  "hold from gaining extra powered physics ticks."),
            "Control");
    }

    private void ReleaseAtWallClock(
        PlayerMovement player,
        string trigger)
    {
        RecordReleaseTime();
        LastReleaseHeldFixedSteps = heldFixedSteps;
        LastReleaseDuplicateFixedCallbacks = duplicateFixedCallbacks;
        IsHoldingJump = false;
        if (activeController == this)
            activeController = null;
        releasePulsePending = true;
        maximumHeldFixedSteps = 0;
        exactFixedStepRelease = false;
        try
        {
            ReleasePointer();
        }
        finally
        {
            heldFixedSteps = 0;
            lastCountedFixedTime = double.NaN;
            lastFixedRefreshAt = -1f;
            duplicateFixedCallbacks = 0;
            pressedPlayerInstanceId = 0;
        }
        Rigidbody2D body = player != null
            ? player.GetComponent<Rigidbody2D>()
            : null;
        BonusRunnerLog.Debug(
            $"Automatic jump UP: Trigger={trigger}, " +
            $"HeldFixedSteps={LastReleaseHeldFixedSteps}, " +
            $"SuppressedFixedCallbacks=" +
            $"{LastReleaseDuplicateFixedCallbacks}, " +
            $"Elapsed={Time.unscaledTime - LastPressStartedAt:F3}s, " +
            $"VY={(body != null ? body.velocity.y : 0f):F3}.",
            "Control");
    }

    private void AbortHeldInput(
        string trigger,
        PlayerMovement observedPlayer)
    {
        RecordReleaseTime();
        LastReleaseHeldFixedSteps = heldFixedSteps;
        LastReleaseDuplicateFixedCallbacks = duplicateFixedCallbacks;
        int expectedPlayer = pressedPlayerInstanceId;
        int observedPlayerId = observedPlayer != null
            ? observedPlayer.GetInstanceID()
            : 0;
        IsHoldingJump = false;
        if (activeController == this)
            activeController = null;
        releasePulsePending = false;
        maximumHeldFixedSteps = 0;
        exactFixedStepRelease = false;
        try
        {
            ReleasePointer();
        }
        finally
        {
            heldFixedSteps = 0;
            lastCountedFixedTime = double.NaN;
            lastFixedRefreshAt = -1f;
            duplicateFixedCallbacks = 0;
            pressedPlayerInstanceId = 0;
        }
        BonusRunnerLog.Warning(
            $"Automatic jump input aborted: Trigger={trigger}, " +
            $"ExpectedPlayer={expectedPlayer}, ObservedPlayer=" +
            $"{observedPlayerId}, HeldFixedSteps=" +
            $"{LastReleaseHeldFixedSteps}, SuppressedFixedCallbacks=" +
            $"{LastReleaseDuplicateFixedCallbacks}. The owned pointer was released " +
            "before input could reach a replacement player instance.");
    }
}
