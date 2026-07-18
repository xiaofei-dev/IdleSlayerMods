using System.Collections.Generic;
using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoProgression.Configuration;

internal sealed class AutoProgressionConfig(string configName) : BaseConfig(configName)
{
    internal const int CurrentConfigurationVersion = 10;
    private const string LegacySection = "AutoProgression";

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<bool> EnableAutomaticAscension;
    internal MelonPreferences_Entry<float> AutomaticAscensionSoulBonusPercent;
    internal MelonPreferences_Entry<float> AutomaticAscensionCheckIntervalMinutes;
    internal MelonPreferences_Entry<bool> BuyAscensionSkillsAfterAutomaticAscension;
    internal MelonPreferences_Entry<bool> EnablePaidBonuses;
    internal MelonPreferences_Entry<int> DragonEggReserveAmount;
    internal MelonPreferences_Entry<int> SimurghEggReserveAmount;
    internal MelonPreferences_Entry<bool> AutoClaimCompletedQuests;
    internal MelonPreferences_Entry<bool> RegenerateDailyQuests;
    internal MelonPreferences_Entry<bool> RegenerateWeeklyQuests;
    internal MelonPreferences_Entry<bool> UnlimitedQuestRerolls;
    internal MelonPreferences_Entry<bool> PreferMinimumRageWeeklyQuest;
    internal MelonPreferences_Entry<bool> FilterGeneratedDailyQuests;
    internal MelonPreferences_Entry<bool> ResetPortalCooldown;
    internal MelonPreferences_Entry<bool> EnableRagePill;
    internal MelonPreferences_Entry<bool> EnableWhetstone;
    internal MelonPreferences_Entry<bool> EnableAlternateDimensionStaff;
    internal MelonPreferences_Entry<bool> EnableBidimensionalStaff;
    internal MelonPreferences_Entry<bool> EnableDeathwaveScepter;
    internal MelonPreferences_Entry<int> DeathwaveScepterFeatherReserveAmount;
    internal MelonPreferences_Entry<bool> EnableShardsNecklaceScrapOverflow;
    internal MelonPreferences_Entry<float> ShardsNecklaceScrapThresholdPercent;
    internal MelonPreferences_Entry<bool> EnableDragonScaleOverflowCraftables;
    internal MelonPreferences_Entry<float> DragonScaleOverflowThresholdPercent;
    internal MelonPreferences_Entry<bool> EnableQuestAssistCraftables;
    internal MelonPreferences_Entry<float> QuestAssistCraftableCooldownMinutes;
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

    protected override void SetBindings()
    {
        ConfigurationVersion = Bind("Configuration Version", 0,
            "Internal preference migration version. Do not edit manually.");
        DebugMode = Bind("Debug Mode", false,
            "Show detailed state, timer, object lookup, and diagnostic logs. User actions, warnings, and errors are always logged.");

        bool migrateLegacyValues = ConfigurationVersion.Value < CurrentConfigurationVersion;

        EnableAutomaticAscension = BindMigrated(
            "Ascension", "Automatic Ascension Enabled", "Ascension - Automatic Ascension Enabled", true,
            "Automatically perform normal Ascension at the configured soul bonus threshold. Ultra Ascension is never used.", migrateLegacyValues);
        AutomaticAscensionSoulBonusPercent = BindMigrated(
            "Ascension", "Soul Bonus Threshold Percent", "Ascension - Soul Bonus Threshold Percent", 100f,
            "Ascend when pending Slayer Points reach this percentage of lifetime Slayer Points.", migrateLegacyValues);
        AutomaticAscensionCheckIntervalMinutes = BindMigrated(
            "Ascension", "Check Interval Minutes", "Ascension - Check Interval Minutes", 15f,
            "How often to check the normal Ascension threshold. Enabling AutoProgression always performs an initial check.", migrateLegacyValues);
        BuyAscensionSkillsAfterAutomaticAscension = BindMigrated(
            "Ascension", "Buy Skills After Ascension", "Ascension - Buy Skills After Ascension", true,
            "After automatic Ascension, use the Ascension Skill Tree Buy All action until no more Slayer Points can be spent.", migrateLegacyValues);

        EnablePaidBonuses = BindMigrated(
            "Paid Bonuses", "Use Paid 500x Bonuses", "Use Paid 500x Bonuses", true,
            "WARNING: This option spends Jewels of Soul. Use both paid 500x bonuses while AutoProgression is active.", migrateLegacyValues);

        DragonEggReserveAmount = BindMigrated(
            "Egg Opening", "Dragon Egg Reserve Amount", "Egg Opening - Dragon Egg Reserve Amount", 300,
            "Open normal Dragon Eggs one at a time while the inventory amount is greater than this value.", migrateLegacyValues);
        SimurghEggReserveAmount = BindMigrated(
            "Egg Opening", "Simurgh Egg Reserve Amount", "Egg Opening - Simurgh Egg Reserve Amount", 10,
            "Open normal Simurgh Eggs one at a time while the inventory amount is greater than this value.", migrateLegacyValues);

        AutoClaimCompletedQuests = BindMigrated(
            "Quests", "Auto Claim Completed Quests", "Quests - Auto Claim Completed Quests", true,
            "Automatically claim completed Daily and Weekly Quests.", migrateLegacyValues);
        RegenerateDailyQuests = BindMigrated(
            "Quests", "Regenerate Daily Quests", "Quests - Regenerate Daily Quests", true,
            "Generate another set of Daily Quests when no active Daily Quests remain.", migrateLegacyValues);
        RegenerateWeeklyQuests = BindMigrated(
            "Quests", "Regenerate Weekly Quests", "Quests - Regenerate Weekly Quests", true,
            "Generate another set of Weekly Quests when no active Weekly Quests remain.", migrateLegacyValues);
        UnlimitedQuestRerolls = BindMigrated(
            "Quests", "Unlimited Quest Rerolls", "Quests - Unlimited Quest Rerolls", true,
            "Keep Daily and Weekly Quest rerolls enabled at all times, independently from the AutoProgression toggle.", migrateLegacyValues);
        PreferMinimumRageWeeklyQuest = BindMigrated(
            "Quests", "Prefer 180k Rage Weekly Quest", "Quests - Prefer 180k Rage Weekly Quest", true,
            "After a new Weekly Quest is generated, reroll one generated slot until the 180,000 Rage Mode kill quest appears. Existing additional Weekly Quests are preserved, and manual rerolls do not trigger this feature.", migrateLegacyValues);
        FilterGeneratedDailyQuests = BindMigrated(
            "Quests", "Filter Generated Daily Quests", "Quests - Filter Generated Daily Quests", true,
            "After a new Daily Quest set is generated, reroll Goblin kills, Chest Hunt chests, normal or Silver Random Boxes, normal Boost uses, Rage Mode uses, Bonus Stage entry/completion, Ascending Heights, and Grapple Run objectives. Rage Mode kill and Wind Dash kill quests are retained. Manual rerolls do not trigger this feature.", migrateLegacyValues);
        ResetPortalCooldown = BindMigrated(
            "Quests", "Reset Portal Cooldown", "Quests - Reset Portal Cooldown", true,
            "Keep the normal Portal cooldown at zero while AutoProgression is active.", migrateLegacyValues);

        EnableRagePill = BindMigrated(
            "Craftables", "Rage Pill Enabled", "Craftables - Rage Pill Enabled", true,
            "Use Rage Pills to refresh Rage while Rage has an active cooldown. WARNING: This can spend Jewels of Soul on missing materials when the global material-purchase option is enabled.", migrateLegacyValues);
        RagePillMinimumIntervalSeconds = BindMigrated(
            "Craftables", "Rage Pill Minimum Interval Seconds", "Craftables - Rage Pill Minimum Interval Seconds", 10f,
            "Minimum time between Rage Pill use attempts.", migrateLegacyValues);
        EnableWhetstone = BindMigrated(
            "Craftables", "Whetstone Enabled", "Craftables - Whetstone Enabled", true,
            "Keep the Whetstone temporary effect active. WARNING: This can spend Jewels of Soul on missing materials when the global material-purchase option is enabled.", migrateLegacyValues);
        EnableAlternateDimensionStaff = BindMigrated(
            "Craftables", "Alternate Dimension Staff Enabled", "Craftables - Alternate Dimension Staff Enabled", true,
            "Keep the Alternate Dimension Staff temporary effect active. WARNING: This can spend Jewels of Soul on missing materials when the global material-purchase option is enabled.", migrateLegacyValues);
        EnableBidimensionalStaff = BindMigrated(
            "Craftables", "Bidimensional Staff Enabled", "Craftables - Bidimensional Staff Enabled", true,
            "Keep the Bidimensional Staff temporary effect active. WARNING: This can spend Jewels of Soul on missing materials when the global material-purchase option is enabled.", migrateLegacyValues);
        EnableDeathwaveScepter = BindMigrated(
            "Craftables", "Deathwave Scepter Enabled", "Craftables - Deathwave Scepter Enabled", true,
            "Keep the Deathwave Scepter temporary effect active while the Simurgh Feather reserve condition is met. WARNING: This can spend Jewels of Soul on other missing materials when the global material-purchase option is enabled.", migrateLegacyValues);
        DeathwaveScepterFeatherReserveAmount = BindMigrated(
            "Craftables", "Deathwave Scepter Feather Reserve Amount", "Craftables - Deathwave Scepter Feather Reserve Amount", 300,
            "Craft Deathwave Scepters only while the Simurgh Feather amount is greater than this reserve value.", migrateLegacyValues);
        EnableShardsNecklaceScrapOverflow = BindMigrated(
            "Craftables", "Shards Necklace Scrap Overflow Enabled", "Craftables - Shards Necklace Scrap Overflow Enabled", true,
            "Craft Shards Necklaces when Scrap reaches the configured capacity percentage. WARNING: This can spend Jewels of Soul on missing materials when the global material-purchase option is enabled.", migrateLegacyValues);
        ShardsNecklaceScrapThresholdPercent = BindMigrated(
            "Craftables", "Shards Necklace Scrap Threshold Percent", "Craftables - Shards Necklace Scrap Threshold Percent", 95f,
            "Craft Shards Necklaces at or above this Scrap percentage and stop below it.", migrateLegacyValues);
        EnableDragonScaleOverflowCraftables = BindMigrated(
            "Craftables", "Dragon Scale Overflow Craftables Enabled", "Craftables - Dragon Scale Overflow Craftables Enabled", true,
            "Craft one of each supported Dragon Scale overflow item per overflow cycle. WARNING: This can spend Jewels of Soul on other missing materials when the global material-purchase option is enabled.", migrateLegacyValues);
        DragonScaleOverflowThresholdPercent = BindMigrated(
            "Craftables", "Dragon Scale Overflow Threshold Percent", "Craftables - Dragon Scale Overflow Threshold Percent", 95f,
            "Start one Dragon Scale overflow crafting cycle when Dragon Scale storage rises above this percentage. Each effect also respects the Timed Items Target Minutes limit.", migrateLegacyValues);
        EnableQuestAssistCraftables = BindMigrated(
            "Craftables", "Quest Assist Craftables Enabled", "Craftables - Quest Assist Craftables Enabled", true,
            "Use Specialization for active normal Goblin or Bonus Stage quests and Key Manifest for active normal Chest Hunt quests. Daily and Weekly Quests are ignored. Scrap, Simurgh Feathers, and Dragon Scales must already be available; other missing materials follow the global Jewel purchase settings.", migrateLegacyValues);
        QuestAssistCraftableCooldownMinutes = BindMigrated(
            "Craftables", "Quest Assist Craftable Cooldown Minutes", "Craftables - Quest Assist Craftable Cooldown Minutes", 10f,
            "Minimum interval after each successful use. Specialization and Key Manifest track this cooldown independently.", migrateLegacyValues);
        TimedCraftablesRefillAtMinutes = BindMigrated(
            "Craftables", "Timed Items Refill At Minutes", "Craftables - Timed Items Refill At Minutes", 10f,
            "Start refilling timed craftables when their remaining duration reaches this value.", migrateLegacyValues);
        TimedCraftablesTargetMinutes = BindMigrated(
            "Craftables", "Timed Items Target Minutes", "Craftables - Timed Items Target Minutes", 60f,
            "Stop crafting timed and material-overflow duration items when their remaining duration reaches this value.", migrateLegacyValues);

        BuyMissingMaterialsWithJewels = BindMigrated(
            "Materials", "Buy Missing With Jewels", "Materials - Buy Missing With Jewels", true,
            "WARNING: This option spends Jewels of Soul. Allow enabled craftable modules to buy missing materials automatically.", migrateLegacyValues);
        MaterialPurchasePercent = BindMigrated(
            "Materials", "Purchase Percent", "Materials - Purchase Percent", 100,
            "Material refill option for Jewel purchases. Supported values are 25, 50, and 100. WARNING: Higher values may spend more Jewels of Soul per purchase.", migrateLegacyValues);

        PurchasePriority = BindMigrated(
            "Purchases", "Priority", "Purchases - Priority", "Skills",
            "Purchase priority. Supported values are Skills and Equipment.", migrateLegacyValues);
        EnableSkillPurchases = BindMigrated(
            "Purchases", "Skills Enabled", "Purchases - Skills Enabled", true,
            "Buy all currently eligible shop skills every 10 seconds.", migrateLegacyValues);
        EnableEquipmentPurchases = BindMigrated(
            "Purchases", "Equipment Enabled", "Purchases - Equipment Enabled", true,
            "Automatically buy levels for unlocked normal equipment.", migrateLegacyValues);

        DisableVerticalMagnetSkills = BindMigrated(
            "Skills", "Disable Vertical Magnet Upgrades", "Skills - Disable Vertical Magnet Upgrades", true,
            "Disable both Random Box vertical magnet upgrades at all times, including manual purchase, independently from the AutoProgression toggle.", migrateLegacyValues);

        EquipmentIdleBeforeSleepMinutes = BindMigrated(
            "Equipment", "No Purchase Before Sleep Minutes", "Equipment - No Purchase Before Sleep Minutes", 2f,
            "Sleep the equipment buyer after this many minutes without an eligible purchase: 10 levels for the latest unlocked equipment or 50 levels for any older equipment.", migrateLegacyValues);
        EquipmentSleepMinutes = BindMigrated(
            "Equipment", "Sleep Minutes", "Equipment - Sleep Minutes", 10f,
            "How long only the equipment buyer sleeps.", migrateLegacyValues);

        if (migrateLegacyValues)
        {
            RemoveLegacyEntries();
            ConfigurationVersion.Value = CurrentConfigurationVersion;
            MelonPreferences.Save();
        }
    }

    private MelonPreferences_Entry<T> BindMigrated<T>(
        string section,
        string key,
        string legacyKey,
        T defaultValue,
        string description,
        bool migrateLegacyValue)
    {
        bool hasLegacyValue = migrateLegacyValue &&
                              MelonPreferences.HasEntry(LegacySection, legacyKey);
        T legacyValue = hasLegacyValue
            ? MelonPreferences.GetEntryValue<T>(LegacySection, legacyKey)
            : defaultValue;

        MelonPreferences_Entry<T> entry = Bind(section, key, defaultValue, description);
        if (hasLegacyValue)
            entry.Value = legacyValue;

        return entry;
    }

    private static void RemoveLegacyEntries()
    {
        MelonPreferences_Category category = MelonPreferences.GetCategory(LegacySection);
        if (category == null) return;

        foreach (string key in LegacyKeys)
            category.DeleteEntry(key);
    }

    private static readonly IReadOnlyList<string> LegacyKeys = new[]
    {
        "Ascension - Automatic Ascension Enabled",
        "Ascension - Soul Bonus Threshold Percent",
        "Ascension - Check Interval Minutes",
        "Ascension - Buy Skills After Ascension",
        "Use Paid 500x Bonuses",
        "Egg Opening - Dragon Egg Reserve Amount",
        "Egg Opening - Simurgh Egg Reserve Amount",
        "Quests - Auto Claim Completed Quests",
        "Quests - Regenerate Daily Quests",
        "Quests - Regenerate Weekly Quests",
        "Quests - Unlimited Quest Rerolls",
        "Quests - Prefer 180k Rage Weekly Quest",
        "Quests - Filter Generated Daily Quests",
        "Quests - Reset Portal Cooldown",
        "Craftables - Rage Pill Enabled",
        "Craftables - Rage Pill Minimum Interval Seconds",
        "Craftables - Whetstone Enabled",
        "Craftables - Alternate Dimension Staff Enabled",
        "Craftables - Bidimensional Staff Enabled",
        "Craftables - Deathwave Scepter Enabled",
        "Craftables - Deathwave Scepter Feather Reserve Amount",
        "Craftables - Shards Necklace Scrap Overflow Enabled",
        "Craftables - Shards Necklace Scrap Threshold Percent",
        "Craftables - Dragon Scale Overflow Craftables Enabled",
        "Craftables - Dragon Scale Overflow Threshold Percent",
        "Craftables - Quest Assist Craftables Enabled",
        "Craftables - Quest Assist Craftable Cooldown Minutes",
        "Craftables - Timed Items Refill At Minutes",
        "Craftables - Timed Items Target Minutes",
        "Materials - Buy Missing With Jewels",
        "Materials - Purchase Percent",
        "Purchases - Priority",
        "Purchases - Skills Enabled",
        "Purchases - Equipment Enabled",
        "Skills - Disable Vertical Magnet Upgrades",
        "Equipment - No Purchase Before Sleep Minutes",
        "Equipment - Sleep Minutes"
    };
}
