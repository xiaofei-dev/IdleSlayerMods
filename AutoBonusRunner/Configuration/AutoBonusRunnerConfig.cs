using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoBonusRunner.Configuration;

internal sealed class AutoBonusRunnerConfig(string configName) : BaseConfig(configName)
{
    internal const int CurrentConfigurationVersion = 4;
    private const string MainSection = "AutoBonusRunner";
    // MelonPreferences category names are global across loaded mods. Keep this
    // category unique so AutoClimber's "Automation" entries cannot override us.
    private const string AutomationSection = "AutoBonusRunner Automation";

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<bool> EnabledOnStartup;
    internal MelonPreferences_Entry<string> ToggleKey;
    internal MelonPreferences_Entry<bool> AutomaticJumping;
    internal MelonPreferences_Entry<bool> CompletionRewardActions;
    internal MelonPreferences_Entry<bool> CompletionWindDash;

    protected override void SetBindings()
    {
        int previousVersion = MelonPreferences.HasEntry(MainSection, "Configuration Version")
            ? MelonPreferences.GetEntryValue<int>(MainSection, "Configuration Version")
            : 0;
        ConfigurationVersion = Bind(MainSection, "Configuration Version",
            CurrentConfigurationVersion,
            $"Internal preference migration version. Current version: {CurrentConfigurationVersion}. Do not edit manually; modified values are restored automatically.");
        DebugMode = Bind(MainSection, "Debug Mode", true,
            "Show detailed Bonus Stage route, input, wall-contact, physics-frame, landing, and completion diagnostics.");
        EnabledOnStartup = Bind(AutomationSection, "Enabled On Startup", true,
            "Enable automatic jump control when the game starts. Detection, debug logging, and manual-jump learning remain active when control is disabled.");
        ToggleKey = Bind(AutomationSection, "Toggle Key", "U",
            "Keyboard key reserved for enabling or disabling automatic jump control only.");
        AutomaticJumping = Bind(AutomationSection, "Automatic Jumping", true,
            "Allow AutoBonusRunner to control jump press, hold, and release while a supported Bonus Stage route is active.");
        CompletionRewardActions = Bind(AutomationSection,
            "Completion Reward Actions", true,
            "After the current section's sphere quota is complete, keep normal route planning active and use a minimum jump/context pulse plus direct bow fire only when no downstream traversal route remains.");
        CompletionWindDash = Bind(AutomationSection,
            "Completion Wind Dash", true,
            "During successful post-quota traversal, activate the selected Wind Dash only when its icon is visible, the ability is unlocked and ready, and the player is grounded or stably at ground height. No AutoAdventurer dependency is required.");

        if (previousVersion < 3)
        {
            DebugMode.Value = true;
            EnabledOnStartup.Value = true;
        }

        if (ConfigurationVersion.Value != CurrentConfigurationVersion)
        {
            ConfigurationVersion.Value = CurrentConfigurationVersion;
            MelonPreferences.Save();
        }
    }
}
