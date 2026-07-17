using System;
using AutoAdventurer.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Gameplay;

internal sealed class SliderSkipService
{
    private const float CheckIntervalSeconds = 0.1f;

    private float nextCheckTime;
    private bool handledCurrentStage;
    private bool visibleLogged;

    internal void Tick(float now)
    {
        if (!Plugin.Config.SkipBonusStartSlider.Value)
        {
            handledCurrentStage = false;
            visibleLogged = false;
            return;
        }

        if (now < nextCheckTime) return;
        nextCheckTime = now + CheckIntervalSeconds;

        try
        {
            PopupSlider popup = PopupSlider.instance;
            if (popup == null || !popup.IsVisible())
            {
                handledCurrentStage = false;
                visibleLogged = false;
                return;
            }

            if (!visibleLogged)
            {
                visibleLogged = true;
                AdventurerLog.Debug("Bonus start slider detected.");
            }

            // The popup can appear while GameState still reports RunnerMode.
            // Wait until its own slider has completed initialization, then
            // invoke exactly once for this visible popup.
            if (handledCurrentStage) return;

            BonusStartSlider slider = popup.slider;
            if (slider == null || !slider.sliderReady || slider.confirmAction == null)
                return;

            handledCurrentStage = true;
            slider.confirmAction.Invoke();
            AdventurerLog.User("Bonus start slider skipped.");
        }
        catch (Exception exception)
        {
            AdventurerLog.Error(
                $"Bonus start slider skip failed safely: {exception}");
            handledCurrentStage = false;
        }
    }

    internal void Reset()
    {
        nextCheckTime = 0f;
        handledCurrentStage = false;
        visibleLogged = false;
    }
}
