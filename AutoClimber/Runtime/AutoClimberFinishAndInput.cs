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
    [HideFromIl2Cpp]
    private float GetExpectedFinishPlatformWorldY(
        AscendingHeightsMap map)
    {
        if (map == null ||
            float.IsNaN(map.finishAtDistance) ||
            float.IsInfinity(map.finishAtDistance))
        {
            return float.PositiveInfinity;
        }

        // Ascending Heights reports distance at half of Unity world-space Y.
        // The finish-map ground is a fixed 47 world units above its spawn
        // boundary (the current 1000-distance map lands at Y=2047). The
        // spawned detector remains the authoritative completion-region check.
        return map.finishAtDistance *
                   FinishDistanceWorldScale +
               FinishPlatformWorldOffset;
    }

    [HideFromIl2Cpp]
    private bool IsFinishPlatformSpawned(
        AscendingHeightsController controller)
    {
        return controller != null &&
               controller.currentAscendingHeightsMap != null &&
               controller.currentAscendingHeightsMap.hasFinishLine &&
               controller.finishMapSpawned;
    }

    [HideFromIl2Cpp]
    private bool TryGetFinishPlatformY(
        AscendingHeightsController controller,
        out float finishPlatformY)
    {
        finishPlatformY = float.PositiveInfinity;

        if (!IsFinishPlatformSpawned(controller))
        {
            return false;
        }

        // Use the map's actual finish-ground transform first. Practice maps
        // do not share the same world-space offset as the normal challenge,
        // so deriving Y from finishAtDistance can leave the player idle at
        // the top even though the real finish platform has been reached.
        try
        {
            if (controller.currentAscendingHeightsMap.finishGround != null &&
                controller.currentAscendingHeightsMap.finishGround.activeInHierarchy &&
                controller.currentAscendingHeightsMap.finishGround.transform != null)
            {
                finishPlatformY =
                    controller.currentAscendingHeightsMap
                        .finishGround.transform.position.y;

                if (!float.IsNaN(finishPlatformY) &&
                    !float.IsInfinity(finishPlatformY))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Some map variants release their finish object during teardown.
            // Continue with detector discovery and the calculated fallback.
        }

        if (finishLineDetectorTransform != null &&
            finishLineDetectorTransform.gameObject != null &&
            finishLineDetectorTransform.gameObject.activeInHierarchy)
        {
            finishPlatformY =
                finishLineDetectorTransform.position.y;

            return !float.IsNaN(finishPlatformY) &&
                   !float.IsInfinity(finishPlatformY);
        }

        if (Time.time < nextFinishDetectorSearchTime)
        {
            return false;
        }

        nextFinishDetectorSearchTime =
            Time.time +
            FinishDetectorSearchIntervalSeconds;

        try
        {
            AscendingHeightsFinishLineDetector[] detectors =
                Resources.FindObjectsOfTypeAll<
                    AscendingHeightsFinishLineDetector>();

            if (detectors != null)
            {
                foreach (AscendingHeightsFinishLineDetector detector in detectors)
                {
                    if (detector == null ||
                        detector.gameObject == null ||
                        !detector.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    finishLineDetectorTransform = detector.transform;
                    finishPlatformY = detector.transform.position.y;
                    finishDetectorSearchWarningLogged = false;

                    LogVerbose(
                        "Spawned typed finish-line detector located at world Y=" +
                        $"{finishPlatformY:F2}."
                    );

                    return !float.IsNaN(finishPlatformY) &&
                           !float.IsInfinity(finishPlatformY);
                }
            }

            MonoBehaviour[] behaviours =
                Resources
                    .FindObjectsOfTypeAll<MonoBehaviour>();

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null ||
                    behaviour.gameObject == null ||
                    !behaviour.gameObject.activeInHierarchy ||
                    !behaviour.GetType().Name.Equals(
                        "AscendingHeightsFinishLineDetector",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                finishLineDetectorTransform =
                    behaviour.transform;

                finishPlatformY =
                    finishLineDetectorTransform.position.y;

                finishDetectorSearchWarningLogged = false;

                LogVerbose(
                    "Spawned finish-line detector located at world Y=" +
                    $"{finishPlatformY:F2}."
                );

                return true;
            }
        }
        catch (Exception exception)
        {
            if (!finishDetectorSearchWarningLogged)
            {
                finishDetectorSearchWarningLogged = true;

                ClimberLog.Exception(
                    "Spawned finish-line detector lookup",
                    exception);
            }
        }

        return false;
    }

    private void HandleFinishedState(
        PlayerMovement playerMovement,
        Rigidbody2D playerRigidbody)
    {
        desiredHorizontalDirection = 1f;

        ApplyHorizontalControl(
            playerMovement,
            playerRigidbody,
            1f
        );
    }

    private void InitializeMovementMembers()
    {
        if (movementMembersInitialized)
        {
            return;
        }

        movementMembersInitialized = true;

        Type playerMovementType =
            typeof(PlayerMovement);

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        currentSpeedField =
            playerMovementType.GetField(
                "currentSpeed",
                flags
            );

        isMovingField =
            playerMovementType.GetField(
                "isMoving",
                flags
            );

        forcedIdleField =
            playerMovementType.GetField(
                "forcedIdle",
                flags
            );

        currentSpeedProperty =
            playerMovementType.GetProperty(
                "currentSpeed",
                flags
            );

        isMovingProperty =
            playerMovementType.GetProperty(
                "isMoving",
                flags
            );

        forcedIdleProperty =
            playerMovementType.GetProperty(
                "forcedIdle",
                flags
            );
    }

    private void LogMovementMembersOnce()
    {
        if (movementMembersLogged)
        {
            return;
        }

        movementMembersLogged = true;

        LogVerbose(
            $"Movement members: " +
            $"currentSpeedField={currentSpeedField != null}, " +
            $"currentSpeedProperty={currentSpeedProperty != null}, " +
            $"isMovingField={isMovingField != null}, " +
            $"isMovingProperty={isMovingProperty != null}, " +
            $"forcedIdleField={forcedIdleField != null}, " +
            $"forcedIdleProperty={forcedIdleProperty != null}"
        );
    }
    private void ApplyHorizontalControl(
        PlayerMovement playerMovement,
        Rigidbody2D playerRigidbody,
        float direction)
    {
        // Movement is now performed through native Windows A/D input.
        // The game receives the same input as a real held keyboard key,
        // so its own movement, collision, and animation logic remain active.
        SetHorizontalDirection(direction);
    }

    private void SetHorizontalDirection(
        float direction)
    {
        if (!Application.isFocused)
        {
            if (!focusPauseLogged)
            {
                focusPauseLogged = true;

                LogVerbose(
                    "AutoClimber paused because the game window is not focused."
                );
            }

            ReleaseAllMovementKeys();
            return;
        }

        focusPauseLogged = false;

        int requestedDirection;

        if (direction > 0.01f)
        {
            requestedDirection = 1;
        }
        else if (direction < -0.01f)
        {
            requestedDirection = -1;
        }
        else
        {
            requestedDirection = 0;
        }

        if (requestedDirection ==
            appliedMovementDirection)
        {
            return;
        }

        if (Time.time <
            nextAllowedMovementStateChangeTime)
        {
            return;
        }

        if (requestedDirection > 0)
        {
            ReleaseAKey();
            PressDKey();
        }
        else if (requestedDirection < 0)
        {
            ReleaseDKey();
            PressAKey();
        }
        else
        {
            ReleaseAllMovementKeys();
        }

        appliedMovementDirection =
            requestedDirection;

        nextAllowedMovementStateChangeTime =
            Time.time +
            MinimumMovementStateSeconds;

        LogVerbose(
            requestedDirection > 0
                ? "Movement input changed: Right (D held)."
                : requestedDirection < 0
                    ? "Movement input changed: Left (A held)."
                    : "Movement input changed: Stop (A/D released)."
        );
    }

    private void PressAKey()
    {
        if (aKeyHeld)
        {
            return;
        }

        keybd_event(
            VirtualKeyA,
            0,
            0,
            UIntPtr.Zero
        );

        aKeyHeld = true;
    }

    private void ReleaseAKey()
    {
        if (!aKeyHeld)
        {
            return;
        }

        keybd_event(
            VirtualKeyA,
            0,
            KeyEventKeyUp,
            UIntPtr.Zero
        );

        aKeyHeld = false;
    }

    private void PressDKey()
    {
        if (dKeyHeld)
        {
            return;
        }

        keybd_event(
            VirtualKeyD,
            0,
            0,
            UIntPtr.Zero
        );

        dKeyHeld = true;
    }

    private void ReleaseDKey()
    {
        if (!dKeyHeld)
        {
            return;
        }

        keybd_event(
            VirtualKeyD,
            0,
            KeyEventKeyUp,
            UIntPtr.Zero
        );

        dKeyHeld = false;
    }

    private void ReleaseAllMovementKeys()
    {
        ReleaseAKey();
        ReleaseDKey();
        appliedMovementDirection = 0;
    }

    private void StopMovementControlSession()
    {
        if (!movementControlSessionActive)
        {
            return;
        }

        movementControlSessionActive = false;
        ReleaseAllMovementKeys();
    }

    [HideFromIl2Cpp]
    private void TrySetMovementMember(
        PlayerMovement playerMovement,
        FieldInfo field,
        PropertyInfo property,
        object value)
    {
        try
        {
            if (field != null)
            {
                field.SetValue(
                    playerMovement,
                    value
                );

                return;
            }

            if (property != null &&
                property.CanWrite)
            {
                property.SetValue(
                    playerMovement,
                    value
                );
            }
        }
        catch (Exception exception)
        {
            ClimberLog.Exception("Movement member update", exception);
        }
    }

    [HideFromIl2Cpp]
    private object TryGetMovementMember(
        PlayerMovement playerMovement,
        FieldInfo field,
        PropertyInfo property)
    {
        try
        {
            if (field != null)
            {
                return field.GetValue(
                    playerMovement
                );
            }

            if (property != null &&
                property.CanRead)
            {
                return property.GetValue(
                    playerMovement
                );
            }
        }
        catch
        {
            // Ignore debug read failures.
        }

        return null;
    }

    private void LogMovementInputState(
        PlayerMovement playerMovement,
        Rigidbody2D playerRigidbody)
    {
        if (!ClimberLog.IsDebugMode)
        {
            return;
        }

        int inputDirection =
            playerRigidbody.velocity.x > 0.10f
                ? 1
                : playerRigidbody.velocity.x < -0.10f
                    ? -1
                    : 0;

        bool grounded =
            playerMovement.IsGrounded();

        bool inputStateChanged =
            !inputDebugStateInitialized ||
            inputDirection !=
                lastLoggedInputDirection ||
            grounded !=
                lastLoggedInputGrounded;

        bool heartbeatDue =
            Time.time >=
            nextInputDebugTime;

        bool stateChangeDebouncePassed =
            Time.time -
                lastInputDebugTime >=
            InputStateChangeLogDebounceSeconds;

        if (!heartbeatDue &&
            (!inputStateChanged ||
             !stateChangeDebouncePassed))
        {
            return;
        }

        nextInputDebugTime =
            Time.time +
            InputDebugIntervalSeconds;

        lastInputDebugTime = Time.time;
        lastLoggedInputDirection =
            inputDirection;
        lastLoggedInputGrounded =
            grounded;
        inputDebugStateInitialized = true;

        InitializeMovementMembers();
        LogMovementMembersOnce();

        object currentSpeed =
            TryGetMovementMember(
                playerMovement,
                currentSpeedField,
                currentSpeedProperty
            );

        object isMoving =
            TryGetMovementMember(
                playerMovement,
                isMovingField,
                isMovingProperty
            );

        object forcedIdle =
            TryGetMovementMember(
                playerMovement,
                forcedIdleField,
                forcedIdleProperty
            );

        LogVerbose(
            $"Input debug: " +
            $"A={Input.GetKey(KeyCode.A)}, " +
            $"D={Input.GetKey(KeyCode.D)}, " +
            $"NativeAHeld={aKeyHeld}, " +
            $"NativeDHeld={dKeyHeld}, " +
            $"VelocityX={playerRigidbody.velocity.x:F2}, " +
            $"VelocityY={playerRigidbody.velocity.y:F2}, " +
            $"currentSpeed={FormatDebugValue(currentSpeed)}, " +
            $"isMoving={FormatDebugValue(isMoving)}, " +
            $"forcedIdle={FormatDebugValue(forcedIdle)}, " +
            $"Grounded={grounded}"
        );
    }

    [HideFromIl2Cpp]
    private string FormatDebugValue(
        object value)
    {
        return value != null
            ? value.ToString()
            : "Unavailable";
    }

    private void LogRuntimeWhenNeeded(
        Vector3 playerPosition,
        Vector2 playerVelocity,
        bool finished)
    {
        if (!ClimberLog.IsDebugMode)
        {
            return;
        }

        if (Time.time <
            nextRuntimeLogTime)
        {
            return;
        }

        nextRuntimeLogTime =
            Time.time +
            RuntimeLogIntervalSeconds;

        bool grounded =
            PlayerMovement.instance != null &&
            PlayerMovement.instance.IsGrounded();

        bool targetVisibleInLatestScan =
            currentTargetId != 0 &&
            planner.FindCandidate(
                currentTargetId,
                currentTargetGeneration
            ) != null;

        float targetlessAirborneSeconds =
            runTargetlessAirborneSeconds +
            (targetlessAirborneActive
                ? Mathf.Max(
                    0f,
                    Time.time -
                        targetlessAirborneStartedAt
                  )
                : 0f);

        string targetText =
            currentTargetId != 0
                ? $"TargetId={currentTargetId}, " +
                  $"TargetGeneration={currentTargetGeneration}, " +
                  $"TargetType={currentTargetType}, " +
                  $"TargetPredictedX=" +
                  $"{currentTargetPredictedX:F2}, " +
                  $"TargetY={currentTargetY:F2}, " +
                  $"TargetVisible=" +
                  $"{targetVisibleInLatestScan}, " +
                  $"TargetSprite=" +
                  $"{currentTargetSpriteName}"
                : "Target=None";

        string jumpModeText =
            currentJumpMode ==
                JumpModeStrong
                    ? "Strong"
                    : currentJumpMode ==
                        JumpModeNormal
                            ? "Normal"
                            : "Initial";

        string targetAgeText =
            currentTargetId != 0
                ? Mathf.Max(
                    0f,
                    Time.time - targetLockedTime
                  ).ToString("F2")
                : "N/A";

        string finishPlatformYText =
            finishPlatformLocated
                ? finishPlatformWorldY.ToString("F2")
                : "N/A";

        LogVerbose(
            $"Runtime: " +
            $"X={playerPosition.x:F2}, " +
            $"Y={playerPosition.y:F2}, " +
            $"VelocityX={playerVelocity.x:F2}, " +
            $"VelocityY={playerVelocity.y:F2}, " +
            $"Grounded={grounded}, " +
            $"ScoreThresholdReached={finished}, " +
            $"FinishMapSpawned={finishMapSpawned}, " +
            $"FinishPlatformLocated={finishPlatformLocated}, " +
            $"FinishPlatformY={finishPlatformYText}, " +
            $"ExpectedFinishPlatformY={finishHeight:F2}, " +
            $"FlagExitActive={finishExitStarted}, " +
            $"JumpMode={jumpModeText}, " +
            $"RejectedFake={rejectedTargetKeys.Count}, " +
            $"Recovery={recoverySelectionRequested}, " +
            $"TargetlessAirTime=" +
            $"{targetlessAirborneSeconds:F2}, " +
            $"ControlPhase={targetControlPhase}, " +
            $"TargetAge={targetAgeText}, " +
            $"SafeHalfWidth={currentTargetSafeHalfWidth:F2}, " +
            $"{targetText}"
        );
    }

    private string GetDirectionText(
        float directionX)
    {
        if (directionX > 0.5f)
        {
            return "Right";
        }

        if (directionX < -0.5f)
        {
            return "Left";
        }

        return "StationaryOrUnknown";
    }

    private void ClearCurrentTarget()
    {
        desiredHorizontalDirection = 0f;
        currentTargetId = 0;
        currentTargetGeneration = 0;

        currentTargetType =
            PlatformType.Unknown;

        currentTargetSpriteName = "";
        currentTargetPredictedX = 0f;
        currentTargetLandingOffsetX = 0f;
        currentTargetY = 0f;
        currentTargetLastSeenTime = 0f;

        currentTargetCandidate = null;
        currentTargetColliderOffset =
            Vector2.zero;
        currentTargetObservedX = 0f;
        currentTargetObservedTime = 0f;
        currentTargetExpectedLandingAt = 0f;
        currentTargetRouteScore = 0f;
        currentTargetReachRatio =
            float.MaxValue;
        currentTargetPersistedOffScanLogged =
            false;
        currentTargetIsLifecycleFallback =
            false;
        emergencyPlatformRescanPending = false;

        ClearTemporaryTargetObservationState();
        nearLandingCommitmentLogged = false;

        currentTargetAnchorY = 0f;
        currentTargetAnchorType =
            PlatformType.Unknown;

        currentTargetIsRescue = false;
        currentTargetSafeHalfWidth = 0f;
        targetLockedTime = 0f;
        targetMinimumHoldUntil = 0f;
        targetInitialDirection = 0;
        targetControlPhase = 0;
    }
}
