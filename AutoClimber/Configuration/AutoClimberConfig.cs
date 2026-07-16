using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoClimber.Configuration;

internal sealed class AutoClimberConfig(string configName) : BaseConfig(configName)
{
    private const int CurrentConfigurationVersion = 4;
    private const string MainSection = "AutoClimber";
    private const string AutomationSection = "Automation";

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<bool> EnabledOnStartup;
    internal MelonPreferences_Entry<string> ToggleKey;
    internal MelonPreferences_Entry<bool> EnableAutoRetry;

    protected override void SetBindings()
    {
        ConfigurationVersion = Bind(MainSection, "Configuration Version", 0,
            "Internal preference migration version. Do not edit manually.");
        DebugMode = Bind(MainSection, "Debug Mode", true,
            "Show detailed diagnostic logs. Run count summaries are always shown.");

        bool migrateLegacyValues =
            ConfigurationVersion.Value <
                CurrentConfigurationVersion;

        bool autoRetryEnabled =
            migrateLegacyValues &&
            MelonPreferences.HasEntry(
                MainSection,
                "Auto Retry Enabled"
            )
                ? MelonPreferences.GetEntryValue<bool>(
                    MainSection,
                    "Auto Retry Enabled"
                )
                : true;

        EnableAutoRetry = Bind(
            AutomationSection,
            "Auto Retry Enabled",
            autoRetryEnabled,
            "Automatically continue the challenge after a failed run."
        );
        EnabledOnStartup = Bind(
            AutomationSection,
            "Enabled On Startup",
            true,
            "Enable AutoClimber when the game starts."
        );
        ToggleKey = Bind(
            AutomationSection,
            "Toggle Key",
            "Y",
            "Keyboard key used to enable or disable AutoClimber."
        );

        // V5.2 needs detailed target-lifecycle traces for the 95% reliability
        // validation. Enable it once for existing installations while still
        // respecting a later user choice to turn Debug Mode off.
        if (migrateLegacyValues)
        {
            RemoveLegacyEntries();
            ConfigurationVersion.Value =
                CurrentConfigurationVersion;
            MelonPreferences.Save();
        }
    }

    private static void RemoveLegacyEntries()
    {
        MelonPreferences_Category category =
            MelonPreferences.GetCategory(MainSection);

        if (category == null)
        {
            return;
        }

        category.DeleteEntry("My Setting");
        category.DeleteEntry("Auto Retry Enabled");
        category.DeleteEntry("Log Enemy Defeats");
    }
}
