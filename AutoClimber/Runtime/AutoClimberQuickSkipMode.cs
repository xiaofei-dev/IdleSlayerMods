using AutoClimber.Diagnostics;
using Il2Cpp;

namespace AutoClimber;

internal static class QuickSkipFinishDistanceOverride
{
    internal const float FinishDistance = 100f;
    private const float HigherAltitudesInternalFinishDistance = -900f;
    private const float HigherAltitudesStartingBoostThreshold = 500f;

    private static AscendingHeightsMap adjustedMap;
    private static float originalFinishDistance;
    private static bool higherAltitudesWasEnabled;
    private static bool isAdjusted;

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
            higherAltitudesWasEnabled =
                Divinities.list?.HigherAltitudes?.unlocked == true;
            isAdjusted = true;
        }

        // Confirm the pre-run talent scan from the initialized controller.
        // Some game paths expose the purchased talent before its active
        // starting boost is populated. Once +1000 is observed, this run must
        // keep the compensated path and may never downgrade again.
        AscendingHeightsController controller =
            AscendingHeightsController.instance;

        if (!higherAltitudesWasEnabled &&
            controller != null &&
            controller.startingBoost >=
                HigherAltitudesStartingBoostThreshold)
        {
            higherAltitudesWasEnabled = true;
        }

        map.finishAtDistance =
            GetInternalFinishDistance();
    }

    private static float GetInternalFinishDistance()
    {
        // These are the internal map values required by vanilla:
        //   talent off:  100
        //   talent on:  -900 + vanilla 1000 starting boost = 100
        //
        // Do not derive this from the map's original target. That target can
        // already reflect the Higher Altitudes presentation and would create
        // a real finish around 1100 while the UI misleadingly displays 100.
        return higherAltitudesWasEnabled
            ? HigherAltitudesInternalFinishDistance
            : FinishDistance;
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
        higherAltitudesWasEnabled = false;
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
