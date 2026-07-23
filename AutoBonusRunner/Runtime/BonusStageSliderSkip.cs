using System;
using AutoBonusRunner.Diagnostics;
using HarmonyLib;
using Il2Cpp;

namespace AutoBonusRunner.Runtime;

internal static class BonusStageSliderSkipBridge
{
    private const float ExpectedWindowSeconds = 15f;
    private static float expectedUntil = -1f;

    internal static void MarkExpected(float now) =>
        expectedUntil = now + ExpectedWindowSeconds;

    internal static bool IsExpected(float now) =>
        expectedUntil >= 0f && now <= expectedUntil;

    internal static void Clear() => expectedUntil = -1f;
}

[HarmonyPatch(
    typeof(BonusMapController),
    nameof(BonusMapController.ShowPreModal),
    typeof(BonusMap))]
internal static class BonusStageSliderSkipPreModalPatch
{
    [HarmonyPrefix]
    private static void Prefix() =>
        BonusStageSliderSkipBridge.MarkExpected(
            UnityEngine.Time.unscaledTime);
}

internal sealed class BonusStageSliderSkip
{
    private const float CheckIntervalSeconds = 0.10f;
    private const float ConfirmationDelaySeconds = 1.00f;
    private float nextCheckTime;
    private float visibleSince = -1f;
    private bool handledCurrentSlider;
    private bool observedCurrentSlider;

    internal void Tick(float now)
    {
        if (Plugin.Config?.SkipStartSlider?.Value != true)
        {
            Reset();
            BonusStageSliderSkipBridge.Clear();
            return;
        }
        if (now < nextCheckTime) return;
        nextCheckTime = now + CheckIntervalSeconds;

        try
        {
            if (!BonusStageSliderSkipBridge.IsExpected(now))
            {
                ResetSliderState();
                return;
            }

            PopupSlider popup = PopupSlider.instance;
            if (popup == null || !popup.IsVisible())
            {
                if (observedCurrentSlider)
                    BonusStageSliderSkipBridge.Clear();
                ResetSliderState();
                return;
            }

            if (!observedCurrentSlider)
            {
                observedCurrentSlider = true;
                visibleSince = now;
                BonusRunnerLog.Debug(
                    "Bonus Stage start slider detected; waiting 1.00s before confirmation.",
                    "Slider");
            }

            if (handledCurrentSlider ||
                now - visibleSince < ConfirmationDelaySeconds)
                return;

            BonusStartSlider slider = popup.slider;
            if (slider == null || !slider.sliderReady ||
                slider.confirmAction == null)
                return;

            handledCurrentSlider = true;
            BonusStageSliderSkipBridge.Clear();
            slider.confirmAction.Invoke();
            BonusRunnerLog.User("Bonus Stage start slider skipped.");
        }
        catch (Exception exception)
        {
            BonusRunnerLog.Exception(
                "Bonus Stage start slider skip",
                exception);
            ResetSliderState();
            BonusStageSliderSkipBridge.Clear();
        }
    }

    internal void Reset()
    {
        nextCheckTime = 0f;
        ResetSliderState();
    }

    private void ResetSliderState()
    {
        visibleSince = -1f;
        handledCurrentSlider = false;
        observedCurrentSlider = false;
    }
}
