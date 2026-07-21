using System;
using System.Globalization;
using System.IO;
using IdleSlayerMods.Common.Config;
using MelonLoader;
using MelonLoader.Utils;

namespace AutoAdventurer.Configuration;

internal sealed class AutoAdventurerConfig(string configName) : BaseConfig(configName)
{
    internal const int CurrentConfigurationVersion = 30;
    private const string MainSection = "AutoAdventurer";
    private const string RageSection = "Automatic Rage";
    private const string GameplaySection = "Gameplay";
    private const string SilverBoxAutomationSection = "Silver Box Automation";
    private const string AutoBoostSection = "Auto Boost";
    private const string QuestAutomationSection = "Quest Automation";

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<string> ToggleKey;
    internal MelonPreferences_Entry<string> StopKey;
    internal MelonPreferences_Entry<double> ActivationCheckIntervalSeconds;
    internal MelonPreferences_Entry<double> MaximumRageDurationSeconds;
    internal MelonPreferences_Entry<double> PostRageObservationSeconds;
    internal MelonPreferences_Entry<bool> SkipBonusStartSlider;
    internal MelonPreferences_Entry<bool> AutoBoss;
    internal MelonPreferences_Entry<string> AutoBoostToggleKey;
    internal MelonPreferences_Entry<double> AutoBoostActivationDelaySeconds;
    internal MelonPreferences_Entry<bool> WindDashRequireGrounded;
    internal MelonPreferences_Entry<string> QuestAutomationToggleKey;
    internal MelonPreferences_Entry<bool> QuestCompletionNotifications;
    internal MelonPreferences_Entry<bool> AutoAlignElementalDivinities;
    internal MelonPreferences_Entry<bool> EnableSilverBoxControl;
    internal MelonPreferences_Entry<bool> AutoReleaseSilverBoxLock;
    internal MelonPreferences_Entry<double> PermanentSilverBoxReleaseAboveDivinityPoints;
    internal MelonPreferences_Entry<double> MinimumDimensionStayMinutes;
    internal MelonPreferences_Entry<double> MaximumQuestTimeMinutes;

    internal double MinimumDimensionStayMinutesValue =>
        Math.Max(0d, MinimumDimensionStayMinutes.Value);

    internal double MaximumQuestTimeMinutesValue =>
        Math.Max(0d, MaximumQuestTimeMinutes.Value);

    internal float ActivationCheckIntervalSecondsValue =>
        (float)Math.Max(0d, ActivationCheckIntervalSeconds.Value);

    internal float MaximumRageDurationSecondsValue =>
        (float)Math.Max(0d, MaximumRageDurationSeconds.Value);

    internal float PostRageObservationSecondsValue =>
        (float)Math.Max(0d, PostRageObservationSeconds.Value);

    internal double AutoBoostActivationDelaySecondsValue =>
        Math.Max(0d, AutoBoostActivationDelaySeconds.Value);

    internal double PermanentSilverBoxReleaseAboveDivinityPointsValue =>
        Math.Max(0d, PermanentSilverBoxReleaseAboveDivinityPoints.Value);

    protected override void SetBindings()
    {
        ConfigurationVersion = Bind(MainSection, "Configuration Version",
            CurrentConfigurationVersion,
            $"Internal preference migration version. Current version: {CurrentConfigurationVersion}. Do not edit manually; modified values are restored automatically.");
        DebugMode = Bind(MainSection, "Debug Mode", false,
            "Show detailed AutoAdventurer diagnostic logs. User actions, warnings, and errors are always logged.");

        // Global helpers are bound first so they appear above the hotkey-
        // controlled automation sections in newly generated configurations.
        SkipBonusStartSlider = Bind(GameplaySection,
            "Skip Bonus Start Slider", true,
            "Global setting with no hotkey or manual trigger. Automatically confirm the timing slider shown before supported bonus minigames whenever it appears.");
        AutoBoss = Bind(GameplaySection, "Auto Boss", true,
            "Global setting with no hotkey or manual trigger. Automatically reduce bosses to 1 HP, advance boss dialogue, perform the finishing attack, and close supported result screens whenever a boss encounter appears.");

        bool migrateSilverRelease = ConfigurationVersion.Value < 24 &&
            MelonPreferences.HasEntry(QuestAutomationSection,
                "Auto Release Silver Box Lock");
        bool autoReleaseSilverBoxLock = migrateSilverRelease
            ? MelonPreferences.GetEntryValue<bool>(QuestAutomationSection,
                "Auto Release Silver Box Lock")
            : true;
        double permanentReleaseThreshold = migrateSilverRelease &&
            MelonPreferences.HasEntry(QuestAutomationSection,
                "Permanent Release Above Divinity Points")
            ? MelonPreferences.GetEntryValue<double>(QuestAutomationSection,
                "Permanent Release Above Divinity Points")
            : ReadLegacyNumber(SilverBoxAutomationSection,
                "Permanent Release Above Divinity Points", 0d);
        bool migrateSilverMasterSwitch = ConfigurationVersion.Value < 26 &&
            MelonPreferences.HasEntry(SilverBoxAutomationSection,
                "Enable Silver Box Control");
        bool enableSilverBoxControl = migrateSilverMasterSwitch
            ? MelonPreferences.GetEntryValue<bool>(SilverBoxAutomationSection,
                "Enable Silver Box Control")
            : true;
        EnableSilverBoxControl = Bind(SilverBoxAutomationSection,
            "Enabled", enableSilverBoxControl,
            "Master switch for all Silver Box automation. When disabled, every other option in this section is inactive and Silver Bank remains under manual control.");
        AutoReleaseSilverBoxLock = Bind(SilverBoxAutomationSection,
            "Auto Release Silver Box Lock", autoReleaseSilverBoxLock,
            "Global setting independent from Quest Automation and its P hotkey. When an active normal quest requires Silver Random Boxes, disable Silver Bank so those boxes can spawn.");
        if (ConfigurationVersion.Value < 30)
            MelonPreferences.GetCategory(SilverBoxAutomationSection)?
                .DeleteEntry("Permanent Release Above Divinity Points");
        PermanentSilverBoxReleaseAboveDivinityPoints = Bind(
            SilverBoxAutomationSection,
            "Permanent Release Above Divinity Points",
            permanentReleaseThreshold,
            "Global five-second Silver Bank threshold. Above this normalized available Divinity Point value Silver Bank is disabled; at or below it Silver Bank is enabled when affordable. Task-triggered release overrides this threshold. Set to 0 to disable threshold control completely and leave the normal Silver Bank state unchanged.");

        // Quest Automation is the mod's primary feature, so keep it directly
        // below the global helpers in newly generated configurations.
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
        AutoAlignElementalDivinities = Bind(QuestAutomationSection,
            "Auto Align Elemental Divinities", true,
            "While Quest Automation is enabled with P, check every five seconds and align elemental Dark Divinities with the first active elemental kill quest in the live quest list. The mod corrects later manual changes, switches an active element, or attempts to activate the required unlocked element when none is active and enough Divinity Points are available. This does not change quest priority or replace the current quest lock.");
        bool removeLegacyAutoClaim = ConfigurationVersion.Value < 20 &&
            MelonPreferences.HasEntry(QuestAutomationSection,
                "Auto Claim Completed Quests");
        bool migrateTimingValues = ConfigurationVersion.Value < 30;
        double minimumDimensionStayMinutes = ReadLegacyNumber(
            QuestAutomationSection, "Minimum Dimension Stay Minutes", 10d);
        bool migrateQuestTime = ConfigurationVersion.Value < 17 &&
            MelonPreferences.HasEntry(QuestAutomationSection,
                "Quest Lock Rescan Minutes");
        double maximumQuestTime = migrateQuestTime
            ? MelonPreferences.GetEntryValue<float>(QuestAutomationSection,
                "Quest Lock Rescan Minutes")
            : ReadLegacyNumber(QuestAutomationSection,
                "Maximum Quest Time Minutes", 5d);
        if (migrateTimingValues)
        {
            MelonPreferences.GetCategory(QuestAutomationSection)?
                .DeleteEntry("Minimum Dimension Stay Minutes");
            MelonPreferences.GetCategory(QuestAutomationSection)?
                .DeleteEntry("Maximum Quest Time Minutes");
        }
        MinimumDimensionStayMinutes = Bind(QuestAutomationSection,
            "Minimum Dimension Stay Minutes", minimumDimensionStayMinutes,
            "Stay in a dimension for at least this many minutes after automatic travel before changing dimension again. Actual travel timing is still limited by the game's Portal cooldown and Portal availability.");
        MaximumQuestTimeMinutes = Bind(QuestAutomationSection,
            "Maximum Quest Time Minutes", maximumQuestTime,
            "Allow one selected quest to run for at most this many minutes before releasing it and fully rescanning. Set to 0 to disable the maximum time limit.");

        ToggleKey = Bind(RageSection, "Toggle Key", "K",
            "Keyboard key used to enable or disable Automatic Rage.");
        StopKey = Bind(RageSection, "Stop Key", "J",
            "Keyboard key used to end the current Rage Mode immediately.");
        bool migrateAllNumericValues = ConfigurationVersion.Value < 30;
        double activationCheckIntervalSeconds = ReadLegacyNumber(RageSection,
            "Activation Check Interval Seconds", 12d);
        double maximumRageDurationSeconds = ReadLegacyNumber(RageSection,
            "Maximum Rage Duration Seconds", 20d);
        bool migratePostRageProtection = ConfigurationVersion.Value < 18 &&
            MelonPreferences.HasEntry(RageSection,
                "Post Rage Observation Seconds");
        double postRageProtectionSeconds = migratePostRageProtection
            ? MelonPreferences.GetEntryValue<float>(RageSection,
                "Post Rage Observation Seconds")
            : ReadLegacyNumber(RageSection,
                "Post Rage Protection Seconds", 8d);
        if (migrateAllNumericValues)
        {
            MelonPreferences.GetCategory(RageSection)?
                .DeleteEntry("Activation Check Interval Seconds");
            MelonPreferences.GetCategory(RageSection)?
                .DeleteEntry("Maximum Rage Duration Seconds");
            MelonPreferences.GetCategory(RageSection)?
                .DeleteEntry("Post Rage Protection Seconds");
        }
        ActivationCheckIntervalSeconds = Bind(RageSection,
            "Activation Check Interval Seconds", activationCheckIntervalSeconds,
            "How often Automatic Rage checks whether Rage Mode is ready to activate.");
        MaximumRageDurationSeconds = Bind(RageSection,
            "Maximum Rage Duration Seconds", maximumRageDurationSeconds,
            "End an automatically started Rage Mode after this many seconds. Set to 0 to disable the time limit.");
        PostRageObservationSeconds = Bind(RageSection,
            "Post Rage Protection Seconds", postRageProtectionSeconds,
            "After Rage ends, protect the map for this long while checking quests, boxes, events, minigame triggers, and portals before allowing travel or another Rage activation.");

        AutoBoostToggleKey = Bind(AutoBoostSection, "Toggle Key", "L",
            "Keyboard key used to enable or disable smart Auto Boost. While enabled, the mod detects whether the player currently selected Boost or Wind Dash, activates that ability automatically, and follows later ability changes without another toggle.");
        bool migrateBoostActivationDelay = ConfigurationVersion.Value < 19 &&
            MelonPreferences.HasEntry(AutoBoostSection,
                "Activation Delay Seconds");
        double boostActivationDelay = migrateBoostActivationDelay
            ? Math.Round(MelonPreferences.GetEntryValue<float>(
                AutoBoostSection, "Activation Delay Seconds"), 3)
            : ReadLegacyNumber(AutoBoostSection,
                "Activation Delay Seconds", 0.1d);
        if (migrateBoostActivationDelay)
            MelonPreferences.GetCategory(AutoBoostSection)?
                .DeleteEntry("Activation Delay Seconds");
        else if (migrateAllNumericValues)
            MelonPreferences.GetCategory(AutoBoostSection)?
                .DeleteEntry("Activation Delay Seconds");
        AutoBoostActivationDelaySeconds = Bind(AutoBoostSection,
            "Activation Delay Seconds", boostActivationDelay,
            "Wait this long after the currently selected Boost or Wind Dash ability reaches zero cooldown before activating that selected ability.");
        WindDashRequireGrounded = Bind(AutoBoostSection,
            "Wind Dash Require Grounded", true,
            "Require the player to be at ground level before automatically activating Wind Dash, preventing it from passing over portals or elite enemies.");

        if (ConfigurationVersion.Value != CurrentConfigurationVersion ||
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
            if (migrateSilverRelease)
            {
                MelonPreferences.GetCategory(QuestAutomationSection)?
                    .DeleteEntry("Auto Release Silver Box Lock");
                MelonPreferences.GetCategory(QuestAutomationSection)?
                    .DeleteEntry("Permanent Release Above Divinity Points");
            }
            if (migrateSilverMasterSwitch)
                MelonPreferences.GetCategory(SilverBoxAutomationSection)?
                    .DeleteEntry("Enable Silver Box Control");
            ConfigurationVersion.Value = CurrentConfigurationVersion;
            MelonPreferences.Save();
        }
    }

    private static double ParseNonNegativeNumber(string rawValue, double fallback)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return fallback;

        string value = rawValue.Trim();
        if (double.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double parsed) ||
            double.TryParse(value, NumberStyles.Float,
                CultureInfo.CurrentCulture, out parsed) ||
            double.TryParse(value.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out parsed))
            return Math.Max(0d, parsed);

        return fallback;
    }

    private static double ReadLegacyNumber(string section, string key,
        double fallback) => ParseNonNegativeNumber(
            ReadLegacyValue(section, key,
                fallback.ToString("R", CultureInfo.InvariantCulture)), fallback);

    private static string ReadLegacyValue(string section, string key,
        string fallback)
    {
        try
        {
            string path = Path.Combine(MelonEnvironment.UserDataDirectory,
                "AutoAdventurer.cfg");
            if (!File.Exists(path)) return fallback;

            bool inQuestAutomationSection = false;
            foreach (string sourceLine in File.ReadLines(path))
            {
                string line = sourceLine.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    inQuestAutomationSection =
                        string.Equals(line, $"[\"{section}\"]",
                            StringComparison.Ordinal) ||
                        string.Equals(line, $"[{section}]",
                            StringComparison.Ordinal);
                    continue;
                }

                if (!inQuestAutomationSection) continue;
                string prefix = $"\"{key}\"";
                if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                int equalsIndex = line.IndexOf('=');
                if (equalsIndex < 0) continue;

                string raw = line[(equalsIndex + 1)..].Trim();
                int commentIndex = raw.IndexOf('#');
                if (commentIndex >= 0) raw = raw[..commentIndex].Trim();
                if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
                    raw = raw[1..^1];
                return string.IsNullOrWhiteSpace(raw) ? fallback : raw;
            }
        }
        catch (Exception exception)
        {
            Plugin.Logger.Warning(
                $"Could not migrate legacy timing setting '{key}'; using {fallback}. {exception.GetType().Name}: {exception.Message}");
        }

        return fallback;
    }
}
