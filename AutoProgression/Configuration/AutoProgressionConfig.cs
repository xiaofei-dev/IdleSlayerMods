using System.Collections.Generic;
using IdleSlayerMods.Common.Config;
using MelonLoader;

namespace AutoProgression.Configuration;

internal sealed class AutoProgressionConfig(string configName) : BaseConfig(configName)
{
    internal const int CurrentConfigurationVersion = 17;
    private const string LegacySection = "AutoProgression";

    internal MelonPreferences_Entry<int> ConfigurationVersion;
    internal MelonPreferences_Entry<bool> DebugMode;
    internal MelonPreferences_Entry<bool> EnableAutomaticAscension;
    internal MelonPreferences_Entry<float> AutomaticAscensionSoulBonusPercent;
    internal MelonPreferences_Entry<float> AutomaticAscensionCheckIntervalMinutes;
    internal MelonPreferences_Entry<bool> BuyAscensionSkillsAfterAutomaticAscension;
    internal MelonPreferences_Entry<bool> EnablePaidBonuses;
    internal MelonPreferences_Entry<bool> EnableMinionClaimAndSend;
    internal MelonPreferences_Entry<bool> EnableAutomaticMinionPrestige;
    internal MelonPreferences_Entry<int> DragonEggReserveAmount;
    internal MelonPreferences_Entry<int> SimurghEggReserveAmount;
    internal MelonPreferences_Entry<int> ArmoryBoxesPerPress;
    internal MelonPreferences_Entry<string> ArmoryBoxSelectKey;
    internal MelonPreferences_Entry<string> ArmoryBoxOpenKey;
    internal MelonPreferences_Entry<bool> EnableAutomaticSilverBoxClaim;
    internal MelonPreferences_Entry<bool> EnableQuestAutomation;
    internal MelonPreferences_Entry<bool> AutoClaimCompletedQuests;
    internal MelonPreferences_Entry<bool> RegenerateDailyQuests;
    internal MelonPreferences_Entry<bool> RegenerateWeeklyQuests;
    internal MelonPreferences_Entry<bool> UnlimitedQuestRerolls;
    internal MelonPreferences_Entry<bool> PreferMinimumRageWeeklyQuest;
    internal MelonPreferences_Entry<bool> FilterGeneratedDailyQuests;
    internal MelonPreferences_Entry<bool> ResetPortalCooldown;
    internal MelonPreferences_Entry<bool> EnableCraftableAutomation;
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

    protected override void SetBindings()
    {
        ConfigurationVersion = Bind("Configuration Version",
            CurrentConfigurationVersion,
            $"Internal preference migration version. Current version: {CurrentConfigurationVersion}. Do not edit manually; modified values are restored automatically.");
        DebugMode = Bind("Debug Mode", false,
            "Show detailed state, timer, object lookup, and diagnostic logs. User actions, warnings, and errors are always logged.");

        bool migrateLegacyValues = ConfigurationVersion.Value < CurrentConfigurationVersion;

        EnableAutomaticAscension = BindMigrated(
            "Ascension", "Automatic Ascension Enabled", "Ascension - Automatic Ascension Enabled", true,
            "Automatically perform normal Ascension at the configured soul bonus threshold. Ultra Ascension is never used.", migrateLegacyValues);
        AutomaticAscensionSoulBonusPercent = BindMigrated(
            "Ascension", "Soul Bonus Threshold Percent", "Ascension - Soul Bonus Threshold Percent", 50f,
            "Ascend when pending Slayer Points reach this percentage of lifetime Slayer Points.", migrateLegacyValues);
        AutomaticAscensionCheckIntervalMinutes = BindMigrated(
            "Ascension", "Check Interval Minutes", "Ascension - Check Interval Minutes", 1f,
            "How often to check the normal Ascension threshold. Enabling AutoProgression always performs an initial check.", migrateLegacyValues);
        BuyAscensionSkillsAfterAutomaticAscension = BindMoved(
            "Ascension", "Buy Skills After Automatic Ascension", "Ascension", "Buy Skills After Ascension",
            "Ascension - Buy Skills After Ascension", true,
            "After automatic Ascension, use the Ascension Skill Tree Buy All action until no more Slayer Points can be spent.", migrateLegacyValues);

        EnablePaidBonuses = BindMigrated(
            "Paid Bonuses", "Use Paid 500x Bonuses", "Use Paid 500x Bonuses", true,
            "WARNING: This option spends Jewels of Soul. Use both paid 500x bonuses while AutoProgression is active.", migrateLegacyValues);

        EnableMinionClaimAndSend = BindMigrated(
            "Minions", "Auto Claim and Send", "Minions - Auto Claim and Send", true,
            "While AutoProgression is active, claim completed unlocked Minion missions and send standing Minions again when their Slayer Point cost is affordable.", migrateLegacyValues);
        EnableAutomaticMinionPrestige = BindMigrated(
            "Minions", "Automatic Maximum-Level Prestige", "Minions - Automatic Maximum-Level Prestige", true,
            "While AutoProgression is active and Minion Prestige is unlocked, automatically prestige standing Minions whose level is above 1 and whose maximum level is at least 70. The eligible Minion is raised to its maximum level for that prestige.", migrateLegacyValues);

        DragonEggReserveAmount = BindMigrated(
            "Egg Opening", "Dragon Egg Reserve Amount", "Egg Opening - Dragon Egg Reserve Amount", 300,
            "Open normal Dragon Eggs one at a time while the inventory amount is greater than this value.", migrateLegacyValues);
        SimurghEggReserveAmount = BindMigrated(
            "Egg Opening", "Simurgh Egg Reserve Amount", "Egg Opening - Simurgh Egg Reserve Amount", 10,
            "Open normal Simurgh Eggs one at a time while the inventory amount is greater than this value.", migrateLegacyValues);

        ArmoryBoxesPerPress = BindMigrated(
            "Armory Boxes", "Boxes Per Press", "Armory Boxes - Boxes Per Press", 10,
            "Maximum number of the selected Armory box opened in the background per trigger.", migrateLegacyValues);
        ArmoryBoxSelectKey = BindMigrated(
            "Armory Boxes", "Select Box Key", "Armory Boxes - Select Box Key", "I",
            "Key used to record the currently highlighted one of the five Armory boxes. This must differ from Open Boxes Key.", migrateLegacyValues);
        ArmoryBoxOpenKey = BindMigrated(
            "Armory Boxes", "Open Boxes Key", "Armory Boxes - Open Boxes Key", "O",
            "Key used to open the selected Armory box. This must differ from Select Box Key. This manual feature is independent from the T automation toggle.", migrateLegacyValues);

        EnableAutomaticSilverBoxClaim = BindMigrated(
            "Silver Boxes", "Auto Claim Reward", "Silver Boxes - Auto Claim Reward", true,
            "Automatically claim an available Silver Box reward after entering the game. This feature is independent from the T automation toggle.", migrateLegacyValues);
        EnableQuestAutomation = BindMigrated(
            "Quests", "Enabled", "Quests - Enabled", true,
            "Master switch for all quest automation. When disabled, every other option in this section is inactive.", migrateLegacyValues);
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
            "Keep Daily and Weekly Quest rerolls enabled while AutoProgression is active.", migrateLegacyValues);
        PreferMinimumRageWeeklyQuest = BindMigrated(
            "Quests", "Prefer 180k Rage Weekly Quest", "Quests - Prefer 180k Rage Weekly Quest", true,
            "After a new Weekly Quest is generated, reroll one generated slot until the 180,000 Rage Mode kill quest appears. Existing additional Weekly Quests are preserved, and manual rerolls do not trigger this feature.", migrateLegacyValues);
        FilterGeneratedDailyQuests = BindMigrated(
            "Quests", "Filter Generated Daily Quests", "Quests - Filter Generated Daily Quests", true,
            "After a new Daily Quest set is generated, reroll Goblin kills, material collection, Chest Hunt chests, normal or Silver Random Boxes, normal Boost uses, Rage Mode uses, Bonus Stage entry/full completion/sections, Ascending Heights, and Grapple Run objectives. Rage Mode kill and Wind Dash kill quests are retained. Manual rerolls do not trigger this feature.", migrateLegacyValues);
        ResetPortalCooldown = BindMigrated(
            "Quests", "Reset Portal Cooldown", "Quests - Reset Portal Cooldown", true,
            "Keep the normal Portal cooldown at zero while AutoProgression is active.", migrateLegacyValues);

        EnableCraftableAutomation = BindMigrated(
            "Craftables", "Enabled", "Craftables - Enabled", true,
            "Master switch for all craftable automation. When disabled, every other option in this section and its automatic material purchases are inactive.", migrateLegacyValues);
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
            "Use Specialization for active normal Goblin or Bonus Stage quests and Key Manifest for active normal Chest Hunt quests. Daily and Weekly Quests are ignored. Specialization is not crafted if its Scrap cost would leave storage below 50%. Scrap, Simurgh Feathers, and Dragon Scales must already be available; other missing materials follow the global Jewel purchase settings.", migrateLegacyValues);
        QuestAssistCraftableCooldownMinutes = BindMigrated(
            "Craftables", "Quest Assist Craftable Cooldown Minutes", "Craftables - Quest Assist Craftable Cooldown Minutes", 5f,
            "Minimum interval after each successful use. Specialization and Key Manifest track this cooldown independently.", migrateLegacyValues);
        TimedCraftablesRefillAtMinutes = BindMigrated(
            "Craftables", "Timed Items Refill At Minutes", "Craftables - Timed Items Refill At Minutes", 3f,
            "Start refilling timed craftables when their remaining duration reaches this value.", migrateLegacyValues);
        TimedCraftablesTargetMinutes = BindMigrated(
            "Craftables", "Timed Items Target Minutes", "Craftables - Timed Items Target Minutes", 15f,
            "Stop crafting timed and material-overflow duration items when their remaining duration reaches this value.", migrateLegacyValues);

        BuyMissingMaterialsWithJewels = BindMoved(
            "Craftables", "Buy Missing With Jewels", "Materials", "Buy Missing With Jewels",
            "Materials - Buy Missing With Jewels", true,
            "WARNING: This option spends Jewels of Soul. Allow enabled craftable modules to buy missing materials automatically.", migrateLegacyValues);
        MaterialPurchasePercent = BindMoved(
            "Craftables", "Material Purchase Percent", "Materials", "Purchase Percent",
            "Materials - Purchase Percent", 100,
            "Material refill option for Jewel purchases. Supported values are 25, 50, and 100. WARNING: Higher values may spend more Jewels of Soul per purchase.", migrateLegacyValues);

        EnableSkillPurchases = BindMigrated(
            "Purchases", "Skills Enabled", "Purchases - Skills Enabled", true,
            "Buy all currently eligible shop skills every 10 seconds.", migrateLegacyValues);
        EnableEquipmentPurchases = BindMigrated(
            "Purchases", "Equipment Enabled", "Purchases - Equipment Enabled", true,
            "Automatically buy levels for unlocked normal equipment.", migrateLegacyValues);

        DisableVerticalMagnetSkills = BindMoved(
            "Purchases", "Disable Vertical Magnet Upgrades", "Skills", "Disable Vertical Magnet Upgrades",
            "Skills - Disable Vertical Magnet Upgrades", true,
            "Always disable both Random Box vertical magnet upgrades, including manual purchase. This protection is independent from the T automation toggle.", migrateLegacyValues);

        EquipmentIdleBeforeSleepMinutes = BindMoved(
            "Purchases", "Equipment No Purchase Before Sleep Minutes", "Equipment", "No Purchase Before Sleep Minutes",
            "Equipment - No Purchase Before Sleep Minutes", 1f,
            "Sleep the equipment buyer after this many minutes without an eligible purchase: 10 levels for the latest unlocked equipment or 50 levels for any older equipment.", migrateLegacyValues);
        EquipmentSleepMinutes = BindMoved(
            "Purchases", "Equipment Sleep Minutes", "Equipment", "Sleep Minutes",
            "Equipment - Sleep Minutes", 10f,
            "How long only the equipment buyer sleeps.", migrateLegacyValues);

        if (migrateLegacyValues)
        {
            MigrateChangedDefaults();
            RemoveLegacyEntries();
            RemoveRetiredEntries();
        }

        if (ConfigurationVersion.Value != CurrentConfigurationVersion)
        {
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

    private MelonPreferences_Entry<T> BindMoved<T>(
        string section,
        string key,
        string oldSection,
        string oldKey,
        string legacyKey,
        T defaultValue,
        string description,
        bool migrateValue)
    {
        bool hasMovedValue = migrateValue &&
                             MelonPreferences.HasEntry(oldSection, oldKey);
        T movedValue = hasMovedValue
            ? MelonPreferences.GetEntryValue<T>(oldSection, oldKey)
            : defaultValue;

        MelonPreferences_Entry<T> entry = BindMigrated(
            section, key, legacyKey, defaultValue, description, migrateValue);
        if (hasMovedValue)
            entry.Value = movedValue;

        return entry;
    }

    private static void RemoveLegacyEntries()
    {
        MelonPreferences_Category category = MelonPreferences.GetCategory(LegacySection);
        if (category == null) return;

        foreach (string key in LegacyKeys)
            category.DeleteEntry(key);
    }

    private static void RemoveRetiredEntries()
    {
        MelonPreferences.GetCategory("Purchases")?.DeleteEntry("Priority");
        MelonPreferences.GetCategory("Ascension")?.DeleteEntry("Buy Skills After Ascension");
        MelonPreferences.GetCategory("Materials")?.DeleteEntry("Buy Missing With Jewels");
        MelonPreferences.GetCategory("Materials")?.DeleteEntry("Purchase Percent");
        MelonPreferences.GetCategory("Skills")?.DeleteEntry("Disable Vertical Magnet Upgrades");
        MelonPreferences.GetCategory("Equipment")?.DeleteEntry("No Purchase Before Sleep Minutes");
        MelonPreferences.GetCategory("Equipment")?.DeleteEntry("Sleep Minutes");
    }

    private void MigrateChangedDefaults()
    {
        if (AutomaticAscensionSoulBonusPercent.Value == 100f)
            AutomaticAscensionSoulBonusPercent.Value = 50f;
        if (AutomaticAscensionCheckIntervalMinutes.Value == 15f)
            AutomaticAscensionCheckIntervalMinutes.Value = 1f;
        if (QuestAssistCraftableCooldownMinutes.Value == 10f)
            QuestAssistCraftableCooldownMinutes.Value = 5f;
        if (TimedCraftablesRefillAtMinutes.Value == 10f)
            TimedCraftablesRefillAtMinutes.Value = 3f;
        if (TimedCraftablesTargetMinutes.Value == 60f)
            TimedCraftablesTargetMinutes.Value = 15f;
        if (EquipmentIdleBeforeSleepMinutes.Value == 2f)
            EquipmentIdleBeforeSleepMinutes.Value = 1f;
    }

    private static readonly IReadOnlyList<string> LegacyKeys = new[]
    {
        "Ascension - Automatic Ascension Enabled",
        "Ascension - Soul Bonus Threshold Percent",
        "Ascension - Check Interval Minutes",
        "Ascension - Buy Skills After Ascension",
        "Use Paid 500x Bonuses",
        "Minions - Auto Claim and Send",
        "Minions - Automatic Maximum-Level Prestige",
        "Egg Opening - Dragon Egg Reserve Amount",
        "Egg Opening - Simurgh Egg Reserve Amount",
        "Quests - Enabled",
        "Quests - Auto Claim Completed Quests",
        "Quests - Regenerate Daily Quests",
        "Quests - Regenerate Weekly Quests",
        "Quests - Unlimited Quest Rerolls",
        "Quests - Prefer 180k Rage Weekly Quest",
        "Quests - Filter Generated Daily Quests",
        "Quests - Reset Portal Cooldown",
        "Craftables - Enabled",
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
