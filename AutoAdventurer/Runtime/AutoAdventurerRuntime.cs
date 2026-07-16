using System;
using AutoAdventurer.Diagnostics;
using AutoAdventurer.Rage;
using AutoAdventurer.Gameplay;
using UnityEngine;

namespace AutoAdventurer.Runtime;

public sealed class AutoAdventurerRuntime : MonoBehaviour
{
    private const float MainScreenStableSeconds = 2f;

    private readonly MainScreenGuard mainScreen = new();
    private readonly RageControlService rage = new();
    private readonly SliderSkipService sliderSkip = new();
    private readonly AutomaticBoostService autoBoost = new();
    private bool automaticRageEnabled;
    private bool automaticBoostEnabled;
    private bool wasReady;
    private KeyCode toggleKey = KeyCode.K;
    private KeyCode stopKey = KeyCode.J;
    private KeyCode autoBoostToggleKey = KeyCode.L;

    public void Start()
    {
        toggleKey = ParseKey(Plugin.Config.ToggleKey.Value, KeyCode.K, "Toggle Key");
        stopKey = ParseKey(Plugin.Config.StopKey.Value, KeyCode.J, "Stop Key");
        autoBoostToggleKey = ParseKey(
            Plugin.Config.AutoBoostToggleKey.Value, KeyCode.L, "Auto Boost Toggle Key");
        AdventurerLog.User(
            $"Automatic Rage is ready. Press {toggleKey} to toggle automation and {stopKey} to end the current Rage Mode.");
    }

    public void Update()
    {
        try
        {
            HandleInput();

            // The bonus-start slider uses its own modal GameState, so it must
            // run independently from the Runner/Rage character-action guard.
            sliderSkip.Tick(Time.unscaledTime);

            bool ready = mainScreen.IsReady(MainScreenStableSeconds);
            if (!ready)
            {
                if (wasReady)
                {
                    rage.Reset();
                    autoBoost.Reset();
                    AdventurerLog.Debug(
                        "Character automation paused outside the central gameplay screen.");
                }
                wasReady = false;
                return;
            }

            if (!wasReady)
                AdventurerLog.Debug(
                    "Character automation resumed after the central gameplay screen stabilized.");
            wasReady = true;
            autoBoost.Tick(Time.unscaledTime, automaticBoostEnabled);
            rage.Tick(Time.unscaledTime, automaticRageEnabled);
        }
        catch (Exception exception)
        {
            AdventurerLog.Error($"AutoAdventurer runtime failed safely: {exception}");
            ResetRuntimeState();
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            automaticRageEnabled = !automaticRageEnabled;
            string state = automaticRageEnabled ? "enabled" : "disabled";
            AdventurerLog.User($"Automatic Rage {state}.");
            Plugin.ModHelperInstance?.ShowNotification(
                $"Automatic Rage {state}!", automaticRageEnabled);
        }

        if (Input.GetKeyDown(stopKey))
            rage.EndImmediately($"Manual {stopKey} key.");

        if (Input.GetKeyDown(autoBoostToggleKey))
        {
            automaticBoostEnabled = !automaticBoostEnabled;
            string state = automaticBoostEnabled ? "enabled" : "disabled";
            AdventurerLog.User($"Auto Boost {state}.");
            Plugin.ModHelperInstance?.ShowNotification(
                $"Auto Boost {state}!", automaticBoostEnabled);
        }
    }

    private static KeyCode ParseKey(string value, KeyCode fallback, string setting)
    {
        if (Enum.TryParse(value, true, out KeyCode parsed) && parsed != KeyCode.None)
            return parsed;

        AdventurerLog.Warning(
            $"Invalid {setting} value '{value}'. Falling back to {fallback}.");
        return fallback;
    }

    private void ResetRuntimeState()
    {
        mainScreen.Reset();
        rage.Reset();
        sliderSkip.Reset();
        autoBoost.Reset();
        wasReady = false;
    }

    public void OnDisable() => ResetRuntimeState();
}
