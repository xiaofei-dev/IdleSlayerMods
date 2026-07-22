using System;
using AutoClimber.Diagnostics;
using Il2Cpp;

namespace AutoClimber;

internal static class AscendingHeightsSliderSkipBridge
{
    private const float ExpectedWindowSeconds = 15f;
    private static float expectedUntil = -1f;

    internal static void MarkExpected(float now)
    {
        expectedUntil = now + ExpectedWindowSeconds;
    }

    internal static bool IsExpected(float now) =>
        expectedUntil >= 0f && now <= expectedUntil;

    internal static void Clear()
    {
        expectedUntil = -1f;
    }
}

internal sealed class AscendingHeightsSliderSkip
{
    private const float CheckIntervalSeconds = 0.10f;
    private const float ConfirmationDelaySeconds = 1.00f;

    private float nextCheckTime;
    private float visibleSince = -1f;
    private bool handledCurrentSlider;
    private bool observedCurrentSlider;

    internal void Tick(float now)
    {
        if (AutoClimberPlugin.Config?.SkipStartSlider?.Value != true)
        {
            Reset();
            AscendingHeightsSliderSkipBridge.Clear();
            return;
        }

        if (now < nextCheckTime)
            return;

        nextCheckTime = now + CheckIntervalSeconds;

        try
        {
            if (!AscendingHeightsSliderSkipBridge.IsExpected(now))
            {
                ResetSliderState();
                return;
            }

            PopupSlider popup = PopupSlider.instance;
            if (popup == null || !popup.IsVisible())
            {
                // If another mod confirms the slider during our one-second
                // grace period, its disappearance ends this authorization.
                if (observedCurrentSlider)
                    AscendingHeightsSliderSkipBridge.Clear();
                ResetSliderState();
                return;
            }

            if (!observedCurrentSlider)
            {
                observedCurrentSlider = true;
                visibleSince = now;
                ClimberLog.Debug(
                    "Ascending Heights start slider detected; waiting 1.00s before confirmation.",
                    "Slider");
            }

            if (handledCurrentSlider ||
                now - visibleSince < ConfirmationDelaySeconds)
            {
                return;
            }

            BonusStartSlider slider = popup.slider;
            if (slider == null ||
                !slider.sliderReady ||
                slider.confirmAction == null)
            {
                return;
            }

            handledCurrentSlider = true;
            AscendingHeightsSliderSkipBridge.Clear();
            slider.confirmAction.Invoke();
            ClimberLog.User("Ascending Heights start slider skipped.");
        }
        catch (Exception exception)
        {
            ClimberLog.Error(
                $"Ascending Heights start slider skip failed safely: {exception.Message}");
            ResetSliderState();
            AscendingHeightsSliderSkipBridge.Clear();
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
