using HarmonyLib;
using AutoBonusRunner.Physics;
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
