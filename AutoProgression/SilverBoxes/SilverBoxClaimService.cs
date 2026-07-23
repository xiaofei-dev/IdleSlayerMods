using AutoProgression.Diagnostics;
using Il2Cpp;

namespace AutoProgression.SilverBoxes;

internal sealed class SilverBoxClaimService
{
    private const float CheckIntervalSeconds = 1f;
    private float nextCheckTime;

    internal void Tick(float now)
    {
        if (!Plugin.Config.EnableAutomaticSilverBoxClaim.Value ||
            now < nextCheckTime)
            return;

        nextCheckTime = now + CheckIntervalSeconds;
        SilverRandomBoxManager manager = SilverRandomBoxManager.instance;
        if (manager == null) return;

        try
        {
            if (!manager.CanBeRedeemed()) return;
            double before = manager.silverBoxesLeft;
            manager.RewardForShowing();
            double gained = Math.Max(0d, manager.silverBoxesLeft - before);
            if (gained <= 0d) return;

            ProgressionLog.User(
                $"Claimed Silver Box reward: +{gained:0}, stored=" +
                $"{manager.silverBoxesLeft:0}.");
            Plugin.ModHelperInstance?.ShowNotification(
                $"Claimed {gained:0} Silver Boxes", true);
        }
        catch (Exception exception)
        {
            ProgressionLog.Exception(
                "Silver Box reward claim",
                exception);
        }
    }

    internal void Reset() => nextCheckTime = 0f;
}
