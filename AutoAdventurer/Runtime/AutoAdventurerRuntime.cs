using System;
using AutoAdventurer.Diagnostics;
using AutoAdventurer.Rage;
using AutoAdventurer.Gameplay;
using AutoAdventurer.Quests;
using UnityEngine;

namespace AutoAdventurer.Runtime;

public sealed class AutoAdventurerRuntime : MonoBehaviour
{
    private const float RageScreenStableSeconds = 2f;
    private const float BoostScreenStableSeconds = 0.5f;
    private const float QuestScreenStableSeconds = 2f;
    private const float RepeatedBattleModeLogIntervalSeconds = 60f;

    private readonly MainScreenGuard rageScreen = new(blockMainScreenMenus: false);
    private readonly MainScreenGuard boostScreen = new(blockMainScreenMenus: false);
    private readonly MainScreenGuard questScreen = new(blockMainScreenMenus: false);
    private readonly RageControlService rage = new();
    private readonly SliderSkipService sliderSkip = new();
    private readonly AutoBossService autoBoss = new();
    private readonly AutomaticBoostService autoBoost = new();
    private readonly QuestTravelService questTravel = new();
    private bool automaticRageEnabled;
    private bool automaticBoostEnabled;
    private bool questAutomationEnabled;
    private bool wasRageReady;
    private bool wasBoostReady;
    private bool wasQuestReady;
    private bool logNextQuestResume = true;
    private float nextBattleModeLogTime;
    private KeyCode toggleKey = KeyCode.K;
    private KeyCode stopKey = KeyCode.J;
    private KeyCode autoBoostToggleKey = KeyCode.L;
    private KeyCode questAutomationToggleKey = KeyCode.P;

    public void Start()
    {
        toggleKey = ParseKey(Plugin.Config.ToggleKey.Value, KeyCode.K, "Toggle Key");
        stopKey = ParseKey(Plugin.Config.StopKey.Value, KeyCode.J, "Stop Key");
        autoBoostToggleKey = ParseKey(
            Plugin.Config.AutoBoostToggleKey.Value, KeyCode.L, "Auto Boost Toggle Key");
        questAutomationToggleKey = ParseKey(
            Plugin.Config.QuestAutomationToggleKey.Value, KeyCode.P,
            "Quest Automation Toggle Key");
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
            autoBoss.Tick(Time.unscaledTime);
            while (QuestClaimBridge.TryDequeue(out ClaimedQuestEvent claimed))
                questTravel.ObserveClaimedQuest(
                    claimed, questAutomationEnabled);

            bool questReady = questScreen.IsReady(QuestScreenStableSeconds);
            if (questReady)
            {
                if (questAutomationEnabled &&
                    rage.IsPostRageProtectionReady(Time.unscaledTime))
                    questTravel.PrioritizePostRageScan();
                if (questAutomationEnabled && !wasQuestReady &&
                    logNextQuestResume)
                    AdventurerLog.QuestDebug(
                        "Runtime state: central gameplay scene stabilized; Quest Automation resumed.");
                if (!wasQuestReady)
                    logNextQuestResume = false;
                questTravel.Tick(Time.unscaledTime, questAutomationEnabled);
            }
            else if (wasQuestReady)
            {
                if (questAutomationEnabled)
                {
                    questTravel.PauseForSceneTransition();
                    bool battleMode = questScreen.LastBlockReason.StartsWith(
                        "unsupported GameState=BattleMode",
                        StringComparison.Ordinal);
                    bool shouldLog = !battleMode ||
                                     Time.unscaledTime >= nextBattleModeLogTime;
                    if (shouldLog)
                    {
                        if (battleMode)
                            nextBattleModeLogTime = Time.unscaledTime +
                                RepeatedBattleModeLogIntervalSeconds;
                        AdventurerLog.QuestDebug(
                            $"Runtime state: Quest Automation paused; reason={questScreen.LastBlockReason}.");
                    }
                    logNextQuestResume = shouldLog;
                }
                else
                    logNextQuestResume = false;
            }
            wasQuestReady = questReady;

            bool boostReady = boostScreen.IsReady(BoostScreenStableSeconds);
            if (boostReady)
            {
                if (!wasBoostReady)
                    AdventurerLog.Debug(
                        "Auto Boost resumed after the central gameplay scene stabilized.");
                autoBoost.Tick(Time.unscaledTime, automaticBoostEnabled);
            }
            else if (wasBoostReady)
            {
                autoBoost.Reset();
                AdventurerLog.Debug(
                    "Auto Boost paused outside the central gameplay scene.");
            }
            wasBoostReady = boostReady;

            bool rageReady = rageScreen.IsReady(RageScreenStableSeconds);
            if (rageReady)
            {
                if (!wasRageReady)
                    AdventurerLog.Debug(
                        "Automatic Rage resumed after the central gameplay screen stabilized.");
                rage.Tick(Time.unscaledTime,
                    automaticRageEnabled && !questTravel.SuppressAutomaticRage);
            }
            else if (wasRageReady)
            {
                rage.Reset();
                AdventurerLog.Debug(
                    "Automatic Rage paused outside the central gameplay screen.");
            }
            wasRageReady = rageReady;
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
            if (automaticBoostEnabled)
                autoBoost.RequestImmediateActivation(Time.unscaledTime);
            string state = automaticBoostEnabled ? "enabled" : "disabled";
            AdventurerLog.User($"Auto Boost {state}.");
            Plugin.ModHelperInstance?.ShowNotification(
                $"Auto Boost {state}!", automaticBoostEnabled);
        }

        if (Input.GetKeyDown(questAutomationToggleKey))
        {
            questAutomationEnabled = !questAutomationEnabled;
            if (!questAutomationEnabled)
            {
                questTravel.EndSession();
                questTravel.Reset();
            }
            else
            {
                questTravel.BeginSession();
                logNextQuestResume = true;
            }
            string state = questAutomationEnabled ? "enabled" : "disabled";
            AdventurerLog.User($"Quest Automation {state}.");
            Plugin.ModHelperInstance?.ShowNotification(
                $"Quest Automation {state}!", questAutomationEnabled);
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
        rageScreen.Reset();
        boostScreen.Reset();
        questScreen.Reset();
        rage.Reset();
        sliderSkip.Reset();
        autoBoss.Reset();
        autoBoost.Reset();
        questTravel.Reset();
        wasRageReady = false;
        wasBoostReady = false;
        wasQuestReady = false;
        logNextQuestResume = true;
        nextBattleModeLogTime = 0f;
    }

    public void OnDisable() => ResetRuntimeState();
}
