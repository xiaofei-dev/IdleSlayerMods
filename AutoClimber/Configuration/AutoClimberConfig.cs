using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoClimber.Configuration;

internal sealed class AutoClimberConfig(string configName) : BaseConfig(configName)
{
    private const int CurrentConfigurationVersion = 2;

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<bool> EnableAutoRetry;
    internal MelonPreferences_Entry<bool> LogEnemyDefeats;

    protected override void SetBindings()
    {
        ConfigurationVersion = Bind("Configuration Version", 0,
            "Internal preference migration version. Do not edit manually.");
        DebugMode = Bind("Debug Mode", true,
            "Show route decisions, failure traces, object discovery, and input diagnostics.");
        EnableAutoRetry = Bind("Auto Retry Enabled", true,
            "Automatically confirm Continue Challenge after a failed Ascending Heights run.");
        LogEnemyDefeats = Bind("Log Enemy Defeats", false,
            "Show individual enemy defeat details in developer logs. Run totals are always included in the user summary.");

        // V5.2 needs detailed target-lifecycle traces for the 95% reliability
        // validation. Enable it once for existing installations while still
        // respecting a later user choice to turn Debug Mode off.
        if (ConfigurationVersion.Value <
            CurrentConfigurationVersion)
        {
            DebugMode.Value = true;
            ConfigurationVersion.Value =
                CurrentConfigurationVersion;
            MelonPreferences.Save();
        }
    }
}
