using HarmonyLib;
using AutoBonusRunner.Physics;
using AutoBonusRunner.Runtime;
using Il2Cpp;

namespace AutoBonusRunner.Control;

[HarmonyPatch(typeof(PlayerMovement), "Update")]
internal static class PlayerMovementUpdateJumpHoldPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance)
    {
        if (!JumpPhysicsFeedback.IsPrimaryPlayerInstance(__instance))
            return;

        JumpController.MaintainHeldJump(__instance, "Update");
    }
}

[HarmonyPatch(typeof(PlayerMovement), "FixedUpdate")]
internal static class PlayerMovementFixedUpdateJumpHoldPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance)
    {
        if (!JumpPhysicsFeedback.IsPrimaryPlayerInstance(__instance))
            return;

        // Observe automatic flight, support, face contact, objective descent,
        // and separated wall pulses on the physics cadence. In background or
        // low-render mode several FixedUpdates can occur between Update calls;
        // waiting for the next render frame can miss the only actionable step.
        AutoBonusRunnerRuntime.ObserveCommittedFaceFixedStep(__instance);
        JumpController.MaintainHeldJump(__instance, "FixedUpdate");
    }

    [HarmonyPostfix]
    private static void Postfix(PlayerMovement __instance)
    {
        if (!JumpPhysicsFeedback.IsPrimaryPlayerInstance(__instance))
            return;

        JumpPhysicsFeedback.CaptureFixedStep(__instance);
    }
}
