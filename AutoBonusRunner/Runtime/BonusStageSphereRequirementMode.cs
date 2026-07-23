using System;
using AutoBonusRunner.Diagnostics;
using HarmonyLib;
using Il2Cpp;

namespace AutoBonusRunner.Runtime;

internal static class BonusStageSphereRequirementMode
{
    internal static string ConfiguredMode
    {
        get
        {
            string configured = Plugin.Config?.Mode?.Value;

            if (string.Equals(
                    configured,
                    "Manual",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    configured,
                    "Normal",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Manual";
            }

            if (string.Equals(
                    configured,
                    "Skip",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Skip";
            }

            return "Auto";
        }
    }

    internal static bool ShouldUseSingleSphere(
        BonusMapController controller)
    {
        string mode = ConfiguredMode;
        if (mode == "Manual")
            return false;

        if (mode == "Skip")
            return true;

        // Auto keeps the native objective only for the game's typed Spirit
        // Boost run. No map name, speed inference, or route state participates
        // in this decision.
        return controller != null &&
               !controller.spiritBoostEnabled;
    }
}

[HarmonyPatch(
    typeof(BonusSection),
    nameof(BonusSection.GetRequiredSpheres))]
internal static class BonusStageSphereRequirementPatch
{
    private static int lastControllerId;
    private static nint lastSectionPointer;
    private static int lastSectionIndex = -1;
    private static string lastMode = string.Empty;
    private static bool lastSpiritBoost;
    private static double lastNativeRequirement = double.NaN;
    private static double lastEffectiveRequirement = double.NaN;

    [HarmonyPostfix]
    private static void Postfix(
        BonusSection __instance,
        ref double __result)
    {
        BonusMapController controller =
            BonusMapController.instance;

        // Only the controller's active section is eligible. Calls made while
        // maps or pooled sections are being prepared retain their native
        // ScriptableObject value.
        if (controller == null ||
            __instance == null ||
            controller.currentSection == null ||
            controller.currentSection.Pointer !=
                __instance.Pointer)
        {
            return;
        }

        double nativeRequirement = __result;
        string mode =
            BonusStageSphereRequirementMode.ConfiguredMode;
        bool spiritBoost = controller.spiritBoostEnabled;
        bool singleSphere =
            BonusStageSphereRequirementMode
                .ShouldUseSingleSphere(controller);

        if (singleSphere)
            __result = 1d;

        int controllerId = controller.GetInstanceID();
        nint sectionPointer = __instance.Pointer;
        int sectionIndex = controller.currentSectionIndex;
        bool changed =
            controllerId != lastControllerId ||
            sectionPointer != lastSectionPointer ||
            sectionIndex != lastSectionIndex ||
            !string.Equals(
                mode,
                lastMode,
                StringComparison.Ordinal) ||
            spiritBoost != lastSpiritBoost ||
            !NearlyEqual(
                nativeRequirement,
                lastNativeRequirement) ||
            !NearlyEqual(
                __result,
                lastEffectiveRequirement);

        if (!changed)
            return;

        lastControllerId = controllerId;
        lastSectionPointer = sectionPointer;
        lastSectionIndex = sectionIndex;
        lastMode = mode;
        lastSpiritBoost = spiritBoost;
        lastNativeRequirement = nativeRequirement;
        lastEffectiveRequirement = __result;

        BonusRunnerLog.Debug(
            $"Sphere requirement selected: mode={mode}; " +
            $"section={sectionIndex}; spiritBoost=" +
            $"{spiritBoost.ToString().ToLowerInvariant()}; " +
            $"nativeRequirement={nativeRequirement:F3}; " +
            $"effectiveRequirement={__result:F3}; " +
            $"overrideApplied=" +
            $"{singleSphere.ToString().ToLowerInvariant()}.",
            "Gameplay");
    }

    private static bool NearlyEqual(
        double left,
        double right)
    {
        if (double.IsNaN(left) && double.IsNaN(right))
            return true;

        return Math.Abs(left - right) <= 0.0001d;
    }
}
