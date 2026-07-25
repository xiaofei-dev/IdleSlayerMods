using AutoClimber.Diagnostics;
using Il2Cpp;

namespace AutoClimber;

internal static class QuickSkipFinishDistanceOverride
{
    internal const float FinishDistance = 100f;
    private const float HigherAltitudesDistance = 1000f;

    private static AscendingHeightsMap adjustedMap;
    private static float originalFinishDistance;
    private static bool isAdjusted;
    private static AscendingHeightsMap compensatedMap;
    private static bool startingBoostCompensationActive;

    internal static void Apply(AscendingHeightsMap map)
    {
        if (!ClimberLog.IsQuickSkipModeEnabled)
        {
            // Auto mode may follow one or more quick-skip runs with a full
            // route. Restore the shared map before StartBonus caches its
            // target distance, finish spawn point and progress-bar maximum.
            Restore();
            return;
        }

        if (map == null)
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

    internal static void BeginStartingBoostCompensation(
        AscendingHeightsMap map)
    {
        EndStartingBoostCompensation();

        if (!ClimberLog.IsQuickSkipModeEnabled ||
            map == null)
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

        compensatedMap = map;
        startingBoostCompensationActive = true;
        map.finishAtDistance =
            FinishDistance - HigherAltitudesDistance;
    }

    internal static void EndStartingBoostCompensation()
    {
        if (!startingBoostCompensationActive)
        {
            return;
        }

        if (compensatedMap != null)
        {
            try
            {
                compensatedMap.finishAtDistance =
                    FinishDistance;
            }
            catch
            {
                // The active map may have been released during a failed
                // StartBonus initialization.
            }
        }

        compensatedMap = null;
        startingBoostCompensationActive = false;
    }

    internal static void Restore()
    {
        EndStartingBoostCompensation();

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
