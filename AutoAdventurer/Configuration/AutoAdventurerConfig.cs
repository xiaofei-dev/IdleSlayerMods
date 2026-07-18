using System;
using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoAdventurer.Configuration;

internal sealed class AutoAdventurerConfig(string configName) : BaseConfig(configName)
{
    internal const int CurrentConfigurationVersion = 20;
    private const string MainSection = "AutoAdventurer";
    private const string RageSection = "Automatic Rage";
    private const string GameplaySection = "Gameplay";
    private const string AutoBoostSection = "Auto Boost";
    private const string QuestAutomationSection = "Quest Automation";

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<string> ToggleKey;
    internal MelonPreferences_Entry<string> StopKey;
    internal MelonPreferences_Entry<float> ActivationCheckIntervalSeconds;
    internal MelonPreferences_Entry<float> MaximumRageDurationSeconds;
    internal MelonPreferences_Entry<float> PostRageObservationSeconds;
    internal MelonPreferences_Entry<bool> SkipBonusStartSlider;
    internal MelonPreferences_Entry<bool> AutoBoss;
    internal MelonPreferences_Entry<string> AutoBoostToggleKey;
    internal MelonPreferences_Entry<double> AutoBoostActivationDelaySeconds;
    internal MelonPreferences_Entry<bool> WindDashRequireGrounded;
    internal MelonPreferences_Entry<string> QuestAutomationToggleKey;
    internal MelonPreferences_Entry<bool> QuestCompletionNotifications;
    internal MelonPreferences_Entry<float> MinimumDimensionStayMinutes;
    internal MelonPreferences_Entry<float> MaximumQuestTimeMinutes;

    protected override void SetBindings()
    {
        ConfigurationVersion = Bind(MainSection, "Configuration Version", 0,
            "Internal preference migration version. Do not edit manually.");
        DebugMode = Bind(MainSection, "Debug Mode", false,
            "Show detailed AutoAdventurer diagnostic logs. User actions, warnings, and errors are always logged.");

        ToggleKey = Bind(RageSection, "Toggle Key", "K",
            "Keyboard key used to enable or disable Automatic Rage.");
        StopKey = Bind(RageSection, "Stop Key", "J",
            "Keyboard key used to end the current Rage Mode immediately.");
        ActivationCheckIntervalSeconds = Bind(RageSection,
            "Activation Check Interval Seconds", 12f,
            "How often Automatic Rage checks whether Rage Mode is ready to activate.");
        MaximumRageDurationSeconds = Bind(RageSection,
            "Maximum Rage Duration Seconds", 20f,
            "End an automatically started Rage Mode after this many seconds. Set to 0 to disable the time limit.");
        bool migratePostRageProtection = ConfigurationVersion.Value < 18 &&
            MelonPreferences.HasEntry(RageSection,
                "Post Rage Observation Seconds");
        float postRageProtectionSeconds = migratePostRageProtection
            ? MelonPreferences.GetEntryValue<float>(RageSection,
                "Post Rage Observation Seconds")
            : 8f;
        PostRageObservationSeconds = Bind(RageSection,
            "Post Rage Protection Seconds", postRageProtectionSeconds,
            "After Rage ends, protect the map for this long while checking quests, boxes, events, minigame triggers, and portals before allowing travel or another Rage activation.");

        SkipBonusStartSlider = Bind(GameplaySection,
            "Skip Bonus Start Slider", true,
            "Automatically confirm the timing slider shown before supported bonus minigames.");
        AutoBoss = Bind(GameplaySection, "Auto Boss", true,
            "Automatically reduce bosses to 1 HP, advance boss dialogue, and perform the finishing attack.");

        AutoBoostToggleKey = Bind(AutoBoostSection, "Toggle Key", "L",
            "Keyboard key used to enable or disable smart Auto Boost.");
        bool migrateBoostActivationDelay = ConfigurationVersion.Value < 19 &&
            MelonPreferences.HasEntry(AutoBoostSection,
                "Activation Delay Seconds");
        double boostActivationDelay = migrateBoostActivationDelay
            ? Math.Round(MelonPreferences.GetEntryValue<float>(
                AutoBoostSection, "Activation Delay Seconds"), 3)
            : 0.1d;
        if (migrateBoostActivationDelay)
            MelonPreferences.GetCategory(AutoBoostSection)?
                .DeleteEntry("Activation Delay Seconds");
        AutoBoostActivationDelaySeconds = Bind(AutoBoostSection,
            "Activation Delay Seconds", boostActivationDelay,
            "Wait this long after Boost or WindDash cooldown reaches zero before activating it.");
        WindDashRequireGrounded = Bind(AutoBoostSection,
            "Wind Dash Require Grounded", true,
            "Require the player to be at ground level before automatically activating Wind Dash, preventing it from passing over portals or elite enemies.");

        QuestAutomationToggleKey = Bind(QuestAutomationSection, "Toggle Key", "P",
            "Keyboard key used to enable or disable quest-guided dimension travel.");
        // Quest diagnostics now share the main Debug Mode. Remove the former
        // section-level switch even from configurations already migrated to v20.
        bool removeLegacyQuestDebug =
            MelonPreferences.HasEntry(QuestAutomationSection, "Debug Mode");
        if (removeLegacyQuestDebug)
            MelonPreferences.GetCategory(QuestAutomationSection)?
                .DeleteEntry("Debug Mode");
        QuestCompletionNotifications = Bind(QuestAutomationSection,
            "Show Completion Notifications", true,
            "Show an in-game notification with session quest statistics whenever Quest Automation completes a task.");
        bool removeLegacyAutoClaim = ConfigurationVersion.Value < 20 &&
            MelonPreferences.HasEntry(QuestAutomationSection,
                "Auto Claim Completed Quests");
        MinimumDimensionStayMinutes = Bind(QuestAutomationSection,
            "Minimum Dimension Stay Minutes", 0f,
            "Stay in a dimension for at least this long after automatic travel before changing dimension again.");
        bool migrateQuestTime = ConfigurationVersion.Value < 17 &&
            MelonPreferences.HasEntry(QuestAutomationSection,
                "Quest Lock Rescan Minutes");
        float maximumQuestTime = migrateQuestTime
            ? MelonPreferences.GetEntryValue<float>(QuestAutomationSection,
                "Quest Lock Rescan Minutes")
            : 10f;
        MaximumQuestTimeMinutes = Bind(QuestAutomationSection,
            "Maximum Quest Time Minutes", maximumQuestTime,
            "Allow one selected quest to run for at most this many minutes before releasing it and fully rescanning. Set to 0 to disable the maximum time limit.");
        if (ConfigurationVersion.Value < CurrentConfigurationVersion ||
            removeLegacyQuestDebug)
        {
            if (migratePostRageProtection)
                MelonPreferences.GetCategory(RageSection)?
                    .DeleteEntry("Post Rage Observation Seconds");
            if (migrateQuestTime)
                MelonPreferences.GetCategory(QuestAutomationSection)?
                    .DeleteEntry("Quest Lock Rescan Minutes");
            if (removeLegacyAutoClaim)
                MelonPreferences.GetCategory(QuestAutomationSection)?
                    .DeleteEntry("Auto Claim Completed Quests");
            ConfigurationVersion.Value = CurrentConfigurationVersion;
            MelonPreferences.Save();
        }
    }
}
