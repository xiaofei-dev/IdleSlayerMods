using HarmonyLib;
using Il2Cpp;
using System.Collections.Generic;
using UnityEngine;
using AutoClimber.Diagnostics;

namespace AutoClimber;

// Bridges the runtime controller's direction requests into the game's own
// movement state so background play does not depend on native key presses.
internal static class BackgroundMovementBridge
{
    // The proven planner was calibrated against Ascending Heights movement:
    // currentSpeed +/-500 produces Rigidbody velocity X +/-10.
    internal const float AscendingHeightsCurrentSpeed = 500f;

    internal static bool ControlEnabled { get; private set; }
    internal static int Direction { get; private set; }

    internal static void SetDirection(float direction)
    {
        ControlEnabled = true;
        Direction = direction > 0.01f
            ? 1
            : direction < -0.01f
                ? -1
                : 0;
    }

    internal static void Release()
    {
        ControlEnabled = false;
        Direction = 0;
    }
}

internal static class EnemyDiagnosticsBridge
{
    private static readonly HashSet<int> ConfirmedDeathIds =
        new HashSet<int>();

    internal static int RunConfirmedDeaths { get; private set; }

    internal static void BeginRun()
    {
        ConfirmedDeathIds.Clear();
        RunConfirmedDeaths = 0;
    }

    internal static void RecordDeath(
        EnemyGameObject enemy)
    {
        if (enemy == null ||
            !GameState.IsAscendingHeights())
        {
            return;
        }

        int instanceId = enemy.GetInstanceID();

        if (!ConfirmedDeathIds.Add(instanceId))
        {
            return;
        }

        RunConfirmedDeaths++;

        string enemyName = "unknown";
        Vector3 position = Vector3.zero;

        try
        {
            if (enemy.gameObject != null)
            {
                enemyName = enemy.gameObject.name;
                position = enemy.transform.position;
            }
        }
        catch
        {
            // Death confirmation is still valid even if the object is already
            // being returned to the pool and its transform cannot be read.
        }

        if (AutoClimberPlugin.Config?.LogEnemyDefeats?.Value == true)
        {
            ClimberLog.Developer(
                $"Enemy defeated: Id={instanceId}, " +
                $"Name={enemyName}, " +
                $"X={position.x:F2}, Y={position.y:F2}, " +
                $"RunEnemyDefeats={RunConfirmedDeaths}"
            );
        }
    }
}

internal static class AscendingHeightsRetryBridge
{
    private const float PromptSettleSeconds = 1.00f;

    private static SecondWindAscendingHeights pendingPrompt;
    private static float promptReadyAtRealtime;
    private static bool promptShownSignal;

    internal static void MarkPromptShown(
        SecondWindAscendingHeights prompt)
    {
        if (prompt == null)
        {
            return;
        }

        pendingPrompt = prompt;
        promptShownSignal = true;
        promptReadyAtRealtime =
            Time.realtimeSinceStartup +
            PromptSettleSeconds;
    }

    internal static bool TryConsumePromptShownSignal()
    {
        if (!promptShownSignal)
        {
            return false;
        }

        promptShownSignal = false;
        return true;
    }

    internal static bool TryTakeReadyPrompt(
        out SecondWindAscendingHeights prompt)
    {
        prompt = null;

        if (pendingPrompt == null ||
            Time.realtimeSinceStartup <
                promptReadyAtRealtime)
        {
            return false;
        }

        prompt = pendingPrompt;
        pendingPrompt = null;
        return true;
    }

    internal static void Reset()
    {
        pendingPrompt = null;
        promptReadyAtRealtime = 0f;
        promptShownSignal = false;
    }
}

[HarmonyPatch(typeof(AutoClimberRuntime), "SetHorizontalDirection")]
internal static class AutoClimberDirectionOutputPatch
{
    [HarmonyPrefix]
    private static bool Prefix(float direction)
    {
        BackgroundMovementBridge.SetDirection(direction);

        // Skip keybd_event and the Application.isFocused guard. The route
        // planner still decides the direction exactly as before.
        return false;
    }
}

[HarmonyPatch(typeof(AutoClimberRuntime), "ReleaseAllMovementKeys")]
internal static class AutoClimberDirectionReleasePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        BackgroundMovementBridge.Release();
    }
}

[HarmonyPatch(typeof(PlayerMovement), "CalculateCurrentSpeed")]
internal static class PlayerMovementCalculateCurrentSpeedPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerMovement __instance)
    {
        if (!BackgroundMovementBridge.ControlEnabled)
        {
            return true;
        }

        int direction = BackgroundMovementBridge.Direction;

        __instance.forceIdle = false;
        __instance.isMoving = direction != 0;
        __instance.currentSpeed =
            BackgroundMovementBridge.AscendingHeightsCurrentSpeed *
            direction;

        // Only the focus-dependent input calculation is replaced. The
        // original FixedUpdate, MoveForward, physics and animation continue.
        return false;
    }
}

[HarmonyPatch(typeof(EnemyGameObject), "Die")]
internal static class EnemyGameObjectDiePatch
{
    [HarmonyPrefix]
    private static void Prefix(
        EnemyGameObject __instance)
    {
        EnemyDiagnosticsBridge.RecordDeath(
            __instance
        );
    }
}

[HarmonyPatch(
    typeof(SecondWindAscendingHeights),
    "SecondWindSuggest"
)]
internal static class AscendingHeightsSecondWindPromptPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        SecondWindAscendingHeights __instance)
    {
        AscendingHeightsRetryBridge.MarkPromptShown(
            __instance
        );
    }
}
