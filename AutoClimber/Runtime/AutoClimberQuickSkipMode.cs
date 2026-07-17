using AutoClimber.Diagnostics;
using Il2Cpp;

namespace AutoClimber;

internal static class QuickSkipFinishDistanceOverride
{
    internal const float FinishDistance = 100f;

    private static AscendingHeightsMap adjustedMap;
    private static float originalFinishDistance;
    private static bool isAdjusted;

    internal static void Apply(AscendingHeightsMap map)
    {
        if (!ClimberLog.IsQuickSkipModeEnabled || map == null)
        {
            return;
        }

        if (isAdjusted && adjustedMap != map)
        {
            Restore();
        }

        if (!isAdjusted)
        {
            adjustedMap = map;
            originalFinishDistance = map.finishAtDistance;
            isAdjusted = true;
        }

        map.finishAtDistance = FinishDistance;
    }

    internal static void Restore()
    {
        if (!isAdjusted)
        {
            return;
        }

        if (adjustedMap != null)
        {
            try
            {
                adjustedMap.finishAtDistance = originalFinishDistance;
            }
            catch
            {
                // The ScriptableObject may already have been released during
                // scene teardown. There is nothing left to restore then.
            }
        }

        adjustedMap = null;
        originalFinishDistance = 0f;
        isAdjusted = false;
    }
}

public sealed partial class AutoClimberRuntime
{
    private void UpdateQuickSkipFinishDistance(
        bool ascendingHeightsActive)
    {
        if (!ClimberLog.IsQuickSkipModeEnabled)
        {
            RestoreQuickSkipFinishDistance();
            return;
        }

        if (!ascendingHeightsActive)
        {
            return;
        }

        AscendingHeightsController controller =
            AscendingHeightsController.instance;

        QuickSkipFinishDistanceOverride.Apply(
            controller?.currentAscendingHeightsMap
        );

        if (controller?.progress != null)
        {
            controller.progress.maxValue =
                QuickSkipFinishDistanceOverride.FinishDistance;
        }

        if (controller?.targetDistanceText != null)
        {
            controller.targetDistanceText.text =
                QuickSkipFinishDistanceOverride.FinishDistance
                    .ToString("N0") +
                controller.metersSymbol;
        }
    }

    private void RestoreQuickSkipFinishDistance()
    {
        QuickSkipFinishDistanceOverride.Restore();
    }
}
