using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoAdventurer.Configuration;

internal sealed class AutoAdventurerConfig(string configName) : BaseConfig(configName)
{
    internal const int CurrentConfigurationVersion = 4;
    private const string MainSection = "AutoAdventurer";
    private const string RageSection = "Automatic Rage";
    private const string GameplaySection = "Gameplay";
    private const string AutoBoostSection = "Auto Boost";

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<string> ToggleKey;
    internal MelonPreferences_Entry<string> StopKey;
    internal MelonPreferences_Entry<float> ActivationCheckIntervalSeconds;
    internal MelonPreferences_Entry<float> MaximumRageDurationSeconds;
    internal MelonPreferences_Entry<float> PostRageObservationSeconds;
    internal MelonPreferences_Entry<bool> SkipBonusStartSlider;
    internal MelonPreferences_Entry<string> AutoBoostToggleKey;

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
            "Activation Check Interval Seconds", 10f,
            "How often Automatic Rage checks whether Rage Mode is ready to activate.");
        MaximumRageDurationSeconds = Bind(RageSection,
            "Maximum Rage Duration Seconds", 120f,
            "End an automatically started Rage Mode after this many seconds. Set to 0 to disable the time limit.");
        PostRageObservationSeconds = Bind(RageSection,
            "Post Rage Observation Seconds", 5f,
            "Wait this long after Rage ends before checking for keys, special boxes, minigame triggers, or portals.");

        SkipBonusStartSlider = Bind(GameplaySection,
            "Skip Bonus Start Slider", true,
            "Automatically confirm the timing slider shown before supported bonus minigames.");

        AutoBoostToggleKey = Bind(AutoBoostSection, "Toggle Key", "L",
            "Keyboard key used to enable or disable smart Auto Boost.");

        if (ConfigurationVersion.Value < CurrentConfigurationVersion)
        {
            ConfigurationVersion.Value = CurrentConfigurationVersion;
            MelonPreferences.Save();
        }
    }
}
