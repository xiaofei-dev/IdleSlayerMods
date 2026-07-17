using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoClimber.Configuration;

internal sealed class AutoClimberConfig(string configName) : BaseConfig(configName)
{
    private const int CurrentConfigurationVersion = 6;
    private const string MainSection = "AutoClimber";
    private const string AutomationSection = "Automation";

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<bool> EnabledOnStartup;
    internal MelonPreferences_Entry<string> ToggleKey;
    internal MelonPreferences_Entry<bool> EnableAutoRetry;
    internal MelonPreferences_Entry<bool> SkipMinigame;
    internal MelonPreferences_Entry<bool> TargetEnemies;

    protected override void SetBindings()
    {
        ConfigurationVersion = Bind(MainSection, "Configuration Version", 0,
            "Internal preference migration version. Do not edit manually.");
        DebugMode = Bind(MainSection, "Debug Mode", true,
            "Show detailed diagnostic logs. Disabled automatically while Skip Minigame is enabled.");

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
            "Continue after a failed run when enabled; automatically choose No and exit when disabled."
        );
        SkipMinigame = Bind(
            AutomationSection,
            "Skip Minigame",
            false,
            "Use the independent quick-skip mode. It temporarily sets the Ascending Heights finish distance to 100 and does not record route diagnostics or run statistics."
        );
        TargetEnemies = Bind(
            AutomationSection,
            "Target Enemies",
            true,
            "Prefer touching enemies only when the detour remains safe; completion and boost platforms keep priority."
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

        // V5.3 needs detailed retention/generation traces for reliability
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
