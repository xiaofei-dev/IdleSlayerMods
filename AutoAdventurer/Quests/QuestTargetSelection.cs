using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AutoAdventurer.Quests;

internal sealed class QuestTargetSelection
{
    private static readonly HashSet<string> PriorityUnlockBenefits =
        new(StringComparer.Ordinal)
        {
            "IncreaseQuestsLevel",
            "UnlockMinion",
            "UnlockGiant",
            "UnlockCharacter",
            "SupplyBox",
            "UnlockStoneOfTime",
            "UnlockInJewelOfSoulTab",
            "UnlockEquipment",
            "UnlockNPCInVillage",
            "UnlockNewAscensionUpgrades",
            "BossFightFromSpecialRandomBox",
            "ElementalLock",
            "UnlockNewColorVariant",
            "UnlockLoadout"
        };

    internal Quest Quest { get; init; }
    internal Enemy Enemy { get; init; }
    internal BaseMap Map { get; init; }
    internal double RequiredKills { get; init; }
    internal double CurrentKills { get; init; }

    internal string QuestId => Quest?.name ?? "UnknownQuest";
    internal string QuestDisplayName =>
        string.IsNullOrWhiteSpace(Quest?.localizedName)
            ? QuestId
            : Quest.localizedName;
    internal string EnemyId => Enemy?.name ?? "AnyEnemy";
    internal string MapId => Map?.name ?? "UnknownMap";
    internal string QuestTypeId => Quest?.questType.ToString() ?? "UnknownQuestType";
    internal string ObjectiveId => Quest?.enemyToKill?.name ??
        Quest?.enemyType?.name ?? QuestTypeId;
    internal string RuntimeType =>
        Quest?.GetIl2CppType()?.Name ?? "UnknownRuntimeType";
    internal bool SuppressesAutomaticRage =>
        Quest?.questType == QuestType.KillEnemiesWithArrows;
    internal CharacterSkin RequiredCharacter => Quest?.characterRequired;
    internal string RequiredCharacterId =>
        RequiredCharacter?.name ?? string.Empty;
    internal Upgrade UnlockReward => Quest?.unlocksUpgrade;
    internal bool HasUnlockReward => UnlockReward != null;
    internal string UnlockRewardId => UnlockReward?.name ?? string.Empty;
    internal string UnlockRewardDisplayName =>
        string.IsNullOrWhiteSpace(UnlockReward?.localizedName)
            ? UnlockRewardId
            : UnlockReward.localizedName;
    internal string UnlockRewardBenefitId =>
        UnlockReward?.newBenefit?.benefit?.name ?? string.Empty;
    internal int RewardPriority =>
        string.Equals(UnlockRewardBenefitId, "IncreaseQuestsLevel",
            StringComparison.Ordinal)
            ? 2
            : PriorityUnlockBenefits.Contains(UnlockRewardBenefitId)
                ? 1
                : 0;
    internal string RewardPriorityLabel => RewardPriority switch
    {
        2 => "Top",
        1 => "High",
        _ => "Normal"
    };
    internal bool HasPriorityUnlockReward =>
        RewardPriority > 0;
    internal double RemainingKills =>
        System.Math.Max(0d, RequiredKills - CurrentKills);
    internal string LockKey => BuildLockKey(Quest);

    internal static string BuildLockKey(Quest quest)
    {
        if (quest == null) return string.Empty;
        string runtimeType = quest.GetIl2CppType()?.Name ?? "UnknownRuntimeType";
        string enemy = quest.enemyToKill?.name ?? string.Empty;
        string enemyType = quest.enemyType?.name ?? string.Empty;
        string character = quest.characterRequired?.name ?? string.Empty;
        string goal = quest.questGoal.ToString(CultureInfo.InvariantCulture);
        return $"{runtimeType}|{quest.name}|{quest.questType}|{enemy}|{enemyType}|{character}|{goal}";
    }
}
