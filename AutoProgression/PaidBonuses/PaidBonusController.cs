using Il2Cpp;
using UnityEngine;
using AutoProgression.Diagnostics;

namespace AutoProgression.PaidBonuses;

internal sealed class PaidBonusController
{
    private enum CheckPhase { Ready, ConfirmPurchase }

    private readonly string displayName;
    private readonly float refreshThreshold;
    private readonly System.Action purchase;
    private readonly System.Func<float> actualTimeLeft;
    private CheckPhase phase;
    private float nextCheckTime;

    internal PaidBonusController(
        string displayName,
        float refreshThreshold,
        System.Action purchase,
        System.Func<float> actualTimeLeft)
    {
        this.displayName = displayName;
        this.refreshThreshold = Mathf.Max(0f, refreshThreshold);
        this.purchase = purchase;
        this.actualTimeLeft = actualTimeLeft;
    }

    internal void Tick(float now, float retrySeconds, float confirmationSeconds)
    {
        if (now < nextCheckTime) return;

        float actualRemaining = Mathf.Max(0f, actualTimeLeft());

        if (phase == CheckPhase.ConfirmPurchase)
        {
            phase = CheckPhase.Ready;
            if (actualRemaining <= refreshThreshold)
            {
                nextCheckTime = now + Mathf.Max(1f, retrySeconds);
                ProgressionLog.Debug($"{displayName} purchase was not confirmed; retry scheduled.");
                return;
            }

            ProgressionLog.Spending(
                $"{displayName} purchased with Jewels of Soul; remaining duration={actualRemaining:0.##}s.");
            ScheduleFromActual(now, actualRemaining, retrySeconds);
            return;
        }

        if (actualRemaining > refreshThreshold)
        {
            ScheduleFromActual(now, actualRemaining, retrySeconds);
            return;
        }

        purchase();
        phase = CheckPhase.ConfirmPurchase;
        nextCheckTime = now + Mathf.Max(0.1f, confirmationSeconds);
        ProgressionLog.Debug($"{displayName} purchase requested.");
    }

    private void ScheduleFromActual(float now, float actualRemaining, float retrySeconds)
    {
        float untilThreshold = actualRemaining - refreshThreshold;
        nextCheckTime = now + Mathf.Max(Mathf.Max(0.1f, retrySeconds), untilThreshold);
    }

    internal void Reset()
    {
        phase = CheckPhase.Ready;
        nextCheckTime = 0f;
    }
}
