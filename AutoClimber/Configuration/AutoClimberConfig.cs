using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoClimber.Configuration;

internal sealed class AutoClimberConfig(string configName) : BaseConfig(configName)
{
    private const int CurrentConfigurationVersion = 9;
    private const string MainSection = "AutoClimber";
    private const string AutomationSection = "Automation";

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<bool> EnabledOnStartup;
    internal MelonPreferences_Entry<string> ToggleKey;
    internal MelonPreferences_Entry<bool> EnableAutoRetry;
    internal MelonPreferences_Entry<bool> TargetEnemies;
    internal MelonPreferences_Entry<string> Mode;

    protected override void SetBindings()
    {
        ConfigurationVersion = Bind(MainSection, "Configuration Version",
            CurrentConfigurationVersion,
            $"Internal preference migration version. Current version: {CurrentConfigurationVersion}. Do not edit manually; modified values are restored automatically.");
        DebugMode = Bind(MainSection, "Debug Mode", false,
            "Show detailed diagnostic logs. User actions, warnings, errors, and run summaries are always logged; debug output is disabled automatically in Skip mode.");

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
                : false;
        string migratedMode = "Normal";

        if (migrateLegacyValues &&
            MelonPreferences.HasEntry(
                AutomationSection,
                "Automatic Quest Mode") &&
            !MelonPreferences.GetEntryValue<bool>(
                AutomationSection,
                "Automatic Quest Mode"))
        {
            migratedMode =
                MelonPreferences.HasEntry(
                    AutomationSection,
                    "Skip Minigame") &&
                MelonPreferences.GetEntryValue<bool>(
                    AutomationSection,
                    "Skip Minigame")
                    ? "Skip"
                    : "Normal";
        }

        Mode = Bind(
            AutomationSection,
            "Mode",
            migratedMode,
            "Auto: full route only for Ascending Heights enemy quests. Normal: always play the full route. Skip: always quick-skip the minigame."
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
        EnableAutoRetry = Bind(
            AutomationSection,
            "Auto Retry Enabled",
            autoRetryEnabled,
            "Continue after a failed run when enabled; automatically choose No and exit when disabled."
        );
        TargetEnemies = Bind(
            AutomationSection,
            "Target Enemies",
            true,
            "Prefer touching enemies only when the detour remains safe; completion and boost platforms keep priority."
        );
        if (migrateLegacyValues)
            RemoveLegacyEntries();

        if (ConfigurationVersion.Value != CurrentConfigurationVersion)
        {
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

        MelonPreferences_Category automation =
            MelonPreferences.GetCategory(AutomationSection);

        automation?.DeleteEntry("Skip Minigame");
        automation?.DeleteEntry("Automatic Quest Mode");
    }
}
