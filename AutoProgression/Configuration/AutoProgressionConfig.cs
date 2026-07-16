using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoProgression.Configuration;

internal sealed class AutoProgressionConfig(string configName) : BaseConfig(configName)
{
    internal const int CurrentConfigurationVersion = 1;

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<bool> EnablePaidBonuses;
    internal MelonPreferences_Entry<bool> EnableRagePill;
    internal MelonPreferences_Entry<bool> EnableWhetstone;
    internal MelonPreferences_Entry<bool> EnableAlternateDimensionStaff;
    internal MelonPreferences_Entry<bool> EnableBidimensionalStaff;
    internal MelonPreferences_Entry<bool> EnableShardsNecklaceScrapOverflow;
    internal MelonPreferences_Entry<float> ShardsNecklaceScrapThresholdPercent;
    internal MelonPreferences_Entry<float> TimedCraftablesRefillAtMinutes;
    internal MelonPreferences_Entry<float> TimedCraftablesTargetMinutes;
    internal MelonPreferences_Entry<float> RagePillMinimumIntervalSeconds;
    internal MelonPreferences_Entry<bool> BuyMissingMaterialsWithJewels;
    internal MelonPreferences_Entry<int> MaterialPurchasePercent;
    internal MelonPreferences_Entry<bool> EnableSkillPurchases;
    internal MelonPreferences_Entry<bool> DisableVerticalMagnetSkills;
    internal MelonPreferences_Entry<bool> EnableEquipmentPurchases;
    internal MelonPreferences_Entry<float> EquipmentIdleBeforeSleepMinutes;
    internal MelonPreferences_Entry<float> EquipmentSleepMinutes;
    internal MelonPreferences_Entry<string> PurchasePriority;
    internal MelonPreferences_Entry<bool> EnableAutomaticAscension;
    internal MelonPreferences_Entry<float> AutomaticAscensionSoulBonusPercent;

    protected override void SetBindings()
    {
        ConfigurationVersion = Bind("Configuration Version", 0,
            "Internal preference migration version. Do not edit manually.");
        DebugMode = Bind("Debug Mode", true,
            "Show detailed state, timer, object lookup, and action logs. Formal mode only logs important hidden spending.");
        EnablePaidBonuses = Bind("Use Paid 500x Bonuses", true,
            "Use both 500x bonuses while AutoProgression is active.");
        EnableRagePill = Bind("Craftables - Rage Pill Enabled", true,
            "Use Rage Pills to refresh Rage only while Rage has an active cooldown.");
        EnableWhetstone = Bind("Craftables - Whetstone Enabled", true,
            "Keep the Whetstone temporary effect active.");
        EnableAlternateDimensionStaff = Bind("Craftables - Alternate Dimension Staff Enabled", true,
            "Keep the Alternate Dimension Staff temporary effect active.");
        EnableBidimensionalStaff = Bind("Craftables - Bidimensional Staff Enabled", true,
            "Keep the Bidimensional Staff temporary effect active.");
        EnableShardsNecklaceScrapOverflow = Bind("Craftables - Shards Necklace Scrap Overflow Enabled", true,
            "Craft Shards Necklaces to spend Scrap when Scrap reaches the configured capacity percentage.");
        ShardsNecklaceScrapThresholdPercent = Bind("Craftables - Shards Necklace Scrap Threshold Percent", 95f,
            "Start crafting Shards Necklaces at or above this Scrap capacity percentage and stop below it. Remaining effect time is ignored.");
        TimedCraftablesRefillAtMinutes = Bind("Craftables - Timed Items Refill At Minutes", 10f,
            "Start refilling Whetstone and both supported staffs when their remaining duration reaches this value.");
        TimedCraftablesTargetMinutes = Bind("Craftables - Timed Items Target Minutes", 60f,
            "After reaching the refill threshold, repeatedly craft each timed item until it exceeds this duration.");
        RagePillMinimumIntervalSeconds = Bind("Craftables - Rage Pill Minimum Interval Seconds", 10f,
            "Minimum time between Rage Pill use attempts.");
        BuyMissingMaterialsWithJewels = Bind("Materials - Buy Missing With Jewels", true,
            "Global switch allowing all craftable modules to buy missing materials with Jewels of Soul.");
        MaterialPurchasePercent = Bind("Materials - Purchase Percent", 100,
            "Global material refill option. Supported values are 25, 50, and 100; invalid values use 100.");
        EnableSkillPurchases = Bind("Purchases - Skills Enabled", true,
            "Safely buy all currently eligible shop skills every 10 seconds.");
        DisableVerticalMagnetSkills = Bind("Skills - Disable Vertical Magnet Upgrades", true,
            "Disable both normal and special Random Box vertical magnet upgrades, including manual purchase.");
        EnableEquipmentPurchases = Bind("Purchases - Equipment Enabled", true,
            "Automatically buy levels for dynamically unlocked normal equipment.");
        EquipmentIdleBeforeSleepMinutes = Bind("Equipment - No Purchase Before Sleep Minutes", 2f,
            "Sleep the equipment buyer after this many minutes without any equipment allowing a purchase of at least 10 levels.");
        EquipmentSleepMinutes = Bind("Equipment - Sleep Minutes", 15f,
            "How long only the equipment buyer sleeps. Skill purchases and all other modules continue running.");
        PurchasePriority = Bind("Purchases - Priority", "Skills",
            "Future purchase priority. Supported values are Skills and Equipment; Skills is used for invalid values.");
        EnableAutomaticAscension = Bind("Ascension - Automatic Ascension Enabled", true,
            "Automatically perform a normal ascension when the pending Slayer Points reach the configured percentage of lifetime Slayer Points. Ultra Ascension is never used.");
        AutomaticAscensionSoulBonusPercent = Bind("Ascension - Soul Bonus Threshold Percent", 100f,
            "Normal ascension threshold expressed as pending Slayer Points divided by lifetime Slayer Points. After ascending, all affordable Ascension Skills are purchased.");

        if (ConfigurationVersion.Value < CurrentConfigurationVersion)
        {
            // Future schema migrations belong here. Version 1 only starts
            // tracking and deliberately preserves all existing user choices.
            ConfigurationVersion.Value = CurrentConfigurationVersion;
            MelonPreferences.Save();
        }
    }
}
