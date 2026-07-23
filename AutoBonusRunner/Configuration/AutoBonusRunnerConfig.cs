using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoBonusRunner.Configuration;

internal sealed class AutoBonusRunnerConfig(string configName) : BaseConfig(configName)
{
    internal const int CurrentConfigurationVersion = 34;
    private const string MainSection = "AutoBonusRunner";
    // MelonPreferences category names are global across loaded mods. Keep this
    // category unique so AutoClimber's "Automation" entries cannot override us.
    private const string AutomationSection = "AutoBonusRunner Automation";

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<bool> EnabledOnStartup;
    internal MelonPreferences_Entry<string> ToggleKey;
    internal MelonPreferences_Entry<string> Mode;
    internal MelonPreferences_Entry<bool> EnableAutoRetry;
    internal MelonPreferences_Entry<bool> SkipStartSlider;

    protected override void SetBindings()
    {
        int previousVersion = MelonPreferences.HasEntry(MainSection, "Configuration Version")
            ? MelonPreferences.GetEntryValue<int>(MainSection, "Configuration Version")
            : 0;
        ConfigurationVersion = Bind(MainSection, "Configuration Version",
            CurrentConfigurationVersion,
            $"Internal preference migration version. Current version: {CurrentConfigurationVersion}. Do not edit manually; modified values are restored automatically.");
        DebugMode = Bind(MainSection, "Debug Mode", false,
            "Show detailed Bonus Stage route, input, wall-contact, physics-frame, landing, and completion diagnostics.");
        Mode = Bind(AutomationSection, "Mode", "Auto",
            "Auto: require only 1 sphere in ordinary Bonus Stages while preserving the native requirement in Spirit Boost runs. Manual: always preserve the native sphere requirement. Skip: always require only 1 sphere.");
        EnabledOnStartup = Bind(AutomationSection, "Enabled On Startup", true,
            "Enable automatic jump control when the game starts. Detection, debug logging, and manual-jump learning remain active when control is disabled.");
        ToggleKey = Bind(AutomationSection, "Toggle Key", "U",
            "Keyboard key reserved for enabling or disabling automatic jump control only.");
        EnableAutoRetry = Bind(AutomationSection,
            "Auto Retry Enabled", false,
            "When the native one-use Second Wind choice is offered and the runner's U-toggle is enabled, choose its real Continue button when enabled or its real No button when disabled. A successful Continue remains consumed exactly as the game intended; only failed UI dispatches may be retried up to three times. When the runner itself is disabled, the prompt remains manual.");
        SkipStartSlider = Bind(AutomationSection,
            "Skip Start Slider", true,
            "Wait one second after the Bonus Stage start slider appears, then confirm it automatically if it is still visible.");

        if (previousVersion < 3)
        {
            DebugMode.Value = false;
            EnabledOnStartup.Value = true;
        }

        bool retiredAutomationEntriesPresent =
            MelonPreferences.HasEntry(
                AutomationSection,
                "Automatic Jumping") ||
            MelonPreferences.HasEntry(
                AutomationSection,
                "Completion Reward Actions") ||
            MelonPreferences.HasEntry(
                AutomationSection,
                "Completion Wind Dash");
        if (retiredAutomationEntriesPresent)
        {
            MelonPreferences_Category automation =
                MelonPreferences.GetCategory(AutomationSection);
            automation?.DeleteEntry("Automatic Jumping");
            automation?.DeleteEntry("Completion Reward Actions");
            automation?.DeleteEntry("Completion Wind Dash");
        }

        if (ConfigurationVersion.Value != CurrentConfigurationVersion ||
            retiredAutomationEntriesPresent)
        {
            ConfigurationVersion.Value = CurrentConfigurationVersion;
            MelonPreferences.Save();
        }
    }
}
