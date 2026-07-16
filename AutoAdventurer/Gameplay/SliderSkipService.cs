using System;
using AutoAdventurer.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Gameplay;

internal sealed class SliderSkipService
{
    private const string OverlayPath = "UIManager/Popup Slider/Overlay";
    private const string SliderPath =
        "UIManager/Popup Slider/Overlay/Panel/Confirm Button/Slider";
    private const float CheckIntervalSeconds = 0.1f;

    private float nextCheckTime;
    private bool handledCurrentStage;

    internal void Tick(float now)
    {
        if (!Plugin.Config.SkipBonusStartSlider.Value)
        {
            handledCurrentStage = false;
            return;
        }

        // The slider exists only while a special stage is waiting to start.
        // Avoid touching its transient IL2CPP UI hierarchy during normal play.
        GameStates state = GameState.current;
        if (state != GameStates.PreBonusMode && state != GameStates.PreBossMode)
        {
            handledCurrentStage = false;
            return;
        }

        // A special stage presents this slider only once. After a successful
        // confirmation, remain completely idle until the stage state changes.
        if (handledCurrentStage) return;

        if (now < nextCheckTime) return;
        nextCheckTime = now + CheckIntervalSeconds;

        try
        {
            // GameObject.Find only returns active hierarchy objects. Resolve
            // transient UI references on demand so no IL2CPP wrapper survives
            // a popup rebuild or minigame transition.
            GameObject overlay = GameObject.Find(OverlayPath);
            bool active = overlay != null && overlay.activeInHierarchy;
            if (active)
            {
                GameObject sliderObject = GameObject.Find(SliderPath);
                BonusStartSlider slider =
                    sliderObject?.GetComponent<BonusStartSlider>();
                if (slider?.confirmAction != null)
                {
                    slider.confirmAction.Invoke();
                    handledCurrentStage = true;
                    AdventurerLog.User("Bonus start slider skipped.");
                }
                else
                {
                    AdventurerLog.Debug(
                        "Bonus start slider became active but its action was unavailable.");
                }
            }

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
    }
}
