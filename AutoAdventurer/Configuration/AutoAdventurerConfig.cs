using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoAdventurer.Configuration;

internal sealed class AutoAdventurerConfig(string configName) : BaseConfig(configName)
{
    internal const int CurrentConfigurationVersion = 15;
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
    internal MelonPreferences_Entry<float> AutoBoostActivationDelaySeconds;
    internal MelonPreferences_Entry<bool> WindDashRequireGrounded;
    internal MelonPreferences_Entry<string> QuestAutomationToggleKey;
    internal MelonPreferences_Entry<bool> QuestAutomationDebugMode;
    internal MelonPreferences_Entry<bool> QuestCompletionNotifications;
    internal MelonPreferences_Entry<bool> AutoClaimCompletedQuests;
    internal MelonPreferences_Entry<float> MinimumDimensionStayMinutes;

    protected override void SetBindings()
    {
        ConfigurationVersion = Bind(MainSection, "Configuration Version", 0,
            "Internal preference migration version. Do not edit manually.");
        DebugMode = Bind(MainSection, "Debug Mode", false,
            "Show detailed AutoAdventurer diagnostic logs.");

        ToggleKey = Bind(RageSection, "Toggle Key", "K",
            "Keyboard key used to enable or disable Automatic Rage.");
        StopKey = Bind(RageSection, "Stop Key", "J",
            "Keyboard key used to end the current Rage Mode immediately.");
        ActivationCheckIntervalSeconds = Bind(RageSection,
            "Activation Check Interval Seconds", 12f,
            "How often Automatic Rage checks whether Rage Mode is ready to activate.");
        MaximumRageDurationSeconds = Bind(RageSection,
            "Maximum Rage Duration Seconds", 120f,
            "End an automatically started Rage Mode after this many seconds. Set to 0 to disable the time limit.");
        PostRageObservationSeconds = Bind(RageSection,
            "Post Rage Observation Seconds", 6f,
            "Wait this long after Rage ends before checking for keys, special boxes, minigame triggers, or portals.");

        SkipBonusStartSlider = Bind(GameplaySection,
            "Skip Bonus Start Slider", true,
            "Automatically confirm the timing slider shown before supported bonus minigames.");
        AutoBoss = Bind(GameplaySection, "Auto Boss", true,
            "Automatically reduce bosses to 1 HP, advance boss dialogue, and perform the finishing attack.");

        AutoBoostToggleKey = Bind(AutoBoostSection, "Toggle Key", "L",
            "Keyboard key used to enable or disable smart Auto Boost.");
        AutoBoostActivationDelaySeconds = Bind(AutoBoostSection,
            "Activation Delay Seconds", 0.3f,
            "Wait this long after Boost or WindDash cooldown reaches zero before activating it.");
        WindDashRequireGrounded = Bind(AutoBoostSection,
            "Wind Dash Require Grounded", true,
            "Require the player to be at ground level before automatically activating Wind Dash, preventing it from passing over portals or elite enemies.");

        QuestAutomationToggleKey = Bind(QuestAutomationSection, "Toggle Key", "P",
            "Keyboard key used to enable or disable quest-guided dimension travel.");
        QuestAutomationDebugMode = Bind(QuestAutomationSection, "Debug Mode", false,
            "Show detailed Quest Automation diagnostic logs independently from other AutoAdventurer features.");
        QuestCompletionNotifications = Bind(QuestAutomationSection,
            "Show Completion Notifications", true,
            "Show an in-game notification with session quest statistics whenever Quest Automation completes a task.");
        AutoClaimCompletedQuests = Bind(QuestAutomationSection,
            "Auto Claim Completed Quests", true,
            "Automatically claim any completed normal, Daily, or Weekly quest independently from Quest Automation.");
        MinimumDimensionStayMinutes = Bind(QuestAutomationSection,
            "Minimum Dimension Stay Minutes", 1f,
            "Stay in a dimension for at least this long after automatic travel before changing dimension again.");
        if (ConfigurationVersion.Value < CurrentConfigurationVersion)
        {
            ConfigurationVersion.Value = CurrentConfigurationVersion;
            MelonPreferences.Save();
        }
    }
}
