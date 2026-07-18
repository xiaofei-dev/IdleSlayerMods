using HarmonyLib;
using Il2Cpp;
using System.Collections.Generic;
using UnityEngine;
using AutoClimber.Diagnostics;

namespace AutoClimber;

[HarmonyPatch(typeof(AscendingHeightsController), nameof(AscendingHeightsController.ShowPreModal))]
internal static class AscendingHeightsQuickSkipPreModalPatch
{
    [HarmonyPrefix]
    private static void Prefix(AscendingHeightsMap __0)
    {
        // Apply before the game calculates its target label, starting boost,
        // finish spawn point and completion condition. Applying this from the
        // runtime Update loop is too late for those cached values.
        AutoClimberQuestMode.BeginRunDecision();
        QuickSkipFinishDistanceOverride.Apply(__0);
    }
}

[HarmonyPatch(typeof(AscendingHeightsController), nameof(AscendingHeightsController.StartBonus))]
internal static class AscendingHeightsQuickSkipStartingBoostPatch
{
    [HarmonyPrefix]
    private static void Prefix(
        AscendingHeightsController __instance,
        out bool __state)
    {
        __state = false;

        // Practice starts can enter StartBonus without going through the
        // regular pre-modal path. Make the mode decision and apply the finish
        // distance here as well, before StartBonus caches its target and
        // finish-spawn values. The operation is idempotent for normal starts.
        AutoClimberQuestMode.BeginRunDecision();
        QuickSkipFinishDistanceOverride.Apply(
            __instance?.currentAscendingHeightsMap
        );

        if (!ClimberLog.IsQuickSkipModeEnabled)
        {
            return;
        }

        Divinity higherAltitudes =
            Divinities.list?.HigherAltitudes;

        if (higherAltitudes == null ||
            !higherAltitudes.unlocked)
        {
            return;
        }

        // The vanilla Higher Altitudes divinity injects +1000 starting
        // distance. Cross-TM compensates by using -975, which works but also
        // exposes a negative target. Suppress the injection only while the
        // run is initialized, then restore the real unlock immediately.
        higherAltitudes.unlocked = false;
        __state = true;
    }

    [HarmonyPostfix]
    private static void Postfix(bool __state)
    {
        if (!__state)
        {
            return;
        }

        Divinity higherAltitudes =
            Divinities.list?.HigherAltitudes;

        if (higherAltitudes != null)
        {
            higherAltitudes.unlocked = true;
        }
    }
}

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

        if (ClimberLog.IsDeveloperMode)
        {
            ClimberLog.Developer(
                $"Enemy defeated: Id={instanceId}, " +
                $"Name={enemyName}, " +
                $"X={position.x:F2}, Y={position.y:F2}, " +
                $"RunEnemyDefeats={RunConfirmedDeaths}"
            );
        }
    }

    internal static bool WasHit(int enemyInstanceId)
    {
        return enemyInstanceId != 0 &&
               ConfirmedDeathIds.Contains(enemyInstanceId);
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
