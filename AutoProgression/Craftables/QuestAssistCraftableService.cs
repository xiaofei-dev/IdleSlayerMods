using System;
using System.Collections.Generic;
using AutoProgression.Diagnostics;
using AutoProgression.Materials;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Craftables;

internal sealed class QuestAssistCraftableService
{
    private const float CheckIntervalSeconds = 1f;
    private readonly MaterialPurchaseService materials = new();

    private readonly Entry goblinSupport = new(
        "craftable_item_specialization",
        "Specialization",
        "specialization",
        NeedsSpecialRandomBox);

    private readonly Entry chestHuntSupport = new(
        "craftable_item_key_manifest",
        "Key Manifest",
        "keymanifest",
        HasChestHuntObjective);

    private float nextCheckTime;
    private QuestsList questsList;

    internal bool Tick(float now)
    {
        if (!Plugin.Config.EnableQuestAssistCraftables.Value ||
            now < nextCheckTime)
            return false;

        nextCheckTime = now + CheckIntervalSeconds;
        List<Quest> quests = SnapshotNormalQuests();
        if (quests.Count == 0) return false;

        return TryUse(goblinSupport, quests, now) ||
               TryUse(chestHuntSupport, quests, now);
    }

    private bool TryUse(Entry entry, List<Quest> quests, float now)
    {
        if (now < entry.CooldownUntil || !ContainsMatchingQuest(quests, entry))
            return false;

        entry.Item ??= FindItem(entry);
        if (entry.Item == null)
        {
            if (!entry.MissingLogged)
            {
                ProgressionLog.Debug(
                    $"Quest assist craftable unavailable: {entry.InternalName}.");
                entry.MissingLogged = true;
            }
            return false;
        }

        entry.MissingLogged = false;
        if (!entry.Item.TabVisible() || !entry.Item.ExtraCondition())
            return false;

        if (entry.Item.HowManyCanCraft() <= 0)
        {
            if (!HasRequiredProtectedMaterials(entry.Item))
                return false;

            if (Plugin.Config.BuyMissingMaterialsWithJewels.Value)
                BuyMissingPurchasableMaterials(entry.Item);

            if (entry.Item.HowManyCanCraft() <= 0)
                return false;
        }

        entry.Item.Craft();
        float cooldownSeconds = Mathf.Max(0f,
            Plugin.Config.QuestAssistCraftableCooldownMinutes.Value) * 60f;
        entry.CooldownUntil = now + cooldownSeconds;
        ProgressionLog.User(
            $"Quest assist: crafted {entry.DisplayName}; " +
            $"its next use is available in {cooldownSeconds / 60f:0.##} minute(s).");
        return true;
    }

    private static bool HasRequiredProtectedMaterials(
        TemporaryCraftableItem item)
    {
        var requirements = item.GetRequirements();
        if (requirements == null) return false;

        foreach (MaterialRequirement requirement in requirements)
        {
            if (requirement?.material == null ||
                !IsProtectedMaterial(requirement.material))
                continue;
            if (requirement.material.amount < requirement.amount)
                return false;
        }

        return true;
    }

    private void BuyMissingPurchasableMaterials(TemporaryCraftableItem item)
    {
        var requirements = item.GetRequirements();
        if (requirements == null) return;

        int percent = Plugin.Config.MaterialPurchasePercent.Value;
        foreach (MaterialRequirement requirement in requirements)
        {
            Drop material = requirement?.material;
            if (material == null || IsProtectedMaterial(material) ||
                material.amount >= requirement.amount)
                continue;
            materials.Buy(material, percent);
        }
    }

    private static bool IsProtectedMaterial(Drop material)
    {
        if (material == null) return false;
        Drops drops = Drops.list;
        if (ReferenceEquals(material, drops?.Scrap) ||
            ReferenceEquals(material, drops?.SimurghFeather) ||
            ReferenceEquals(material, drops?.DragonScale))
            return true;

        string name = Normalize(material.name);
        return name.Contains("scrap") ||
               name.Contains("simurghfeather") ||
               name.Contains("dragonscale");
    }

    private List<Quest> SnapshotNormalQuests()
    {
        questsList ??= QuestsList.instance ?? FindLoadedQuestList();
        var source = questsList?.lastScrollListData;
        List<Quest> result = new();
        if (source == null) return result;

        for (int index = 0; index < source.Count; index++)
        {
            Quest quest = source[index];
            if (quest == null || quest is DailyQuest || quest is WeeklyQuest)
                continue;

            try
            {
                if (!quest.isClaimed && !quest.IsCompleted() &&
                    quest.CanBeCompleted())
                    result.Add(quest);
            }
            catch
            {
                // A quest can be replaced while this read-only snapshot is
                // being built. Skip that stale IL2CPP object safely.
            }
        }

        return result;
    }

    private static bool ContainsMatchingQuest(List<Quest> quests, Entry entry)
    {
        foreach (Quest quest in quests)
        {
            try
            {
                if (entry.Matches(quest)) return true;
            }
            catch
            {
                // Treat a concurrently replaced quest as unavailable.
            }
        }

        return false;
    }

    private static bool HasGoblinObjective(Quest quest)
    {
        if (ContainsGoblin(quest.enemyToKill) ||
            ContainsText(quest.enemyType?.name, "goblin"))
            return true;

        return ContainsText(quest.name, "goblin");
    }

    private static bool NeedsSpecialRandomBox(Quest quest) =>
        HasGoblinObjective(quest) ||
        quest.questType is QuestType.BonusStage or
            QuestType.BonusStageSections;

    private static bool ContainsGoblin(Enemy enemy)
    {
        HashSet<string> visited = new(StringComparer.Ordinal);
        Enemy current = enemy;
        for (int depth = 0; current != null && depth < 32; depth++)
        {
            string name = current.name ?? string.Empty;
            if (ContainsText(name, "goblin")) return true;
            if (!visited.Add(name)) break;
            current = current.evolutionBackward;
        }

        return false;
    }

    private static bool HasChestHuntObjective(Quest quest)
    {
        string type = quest.questType.ToString();
        return ContainsText(type, "chesthunt") ||
               ContainsText(quest.name, "chest_hunt") ||
               ContainsText(quest.name, "chesthunt");
    }

    private static bool ContainsText(string value, string fragment) =>
        !string.IsNullOrEmpty(value) &&
        value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;

    private static TemporaryCraftableItem FindItem(Entry entry)
    {
        foreach (TemporaryCraftableItem item in
                 Resources.FindObjectsOfTypeAll<TemporaryCraftableItem>())
        {
            if (item == null) continue;
            if (string.Equals(item.name, entry.InternalName,
                    StringComparison.Ordinal) ||
                Normalize(item.name).Contains(entry.NormalizedName))
                return item;
        }

        return null;
    }

    private static QuestsList FindLoadedQuestList()
    {
        foreach (QuestsList candidate in Resources.FindObjectsOfTypeAll<QuestsList>())
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid() ||
                !candidate.gameObject.scene.isLoaded)
                continue;
            return candidate;
        }

        return null;
    }

    private static string Normalize(string value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();

    internal void Reset()
    {
        nextCheckTime = 0f;
        questsList = null;
        goblinSupport.Item = null;
        goblinSupport.MissingLogged = false;
        chestHuntSupport.Item = null;
        chestHuntSupport.MissingLogged = false;
        // Successful-use cooldowns intentionally survive scene transitions
        // and ascension cache resets within the current game session.
    }

    private sealed class Entry
    {
        internal readonly string InternalName;
        internal readonly string DisplayName;
        internal readonly string NormalizedName;
        internal readonly Func<Quest, bool> Matches;
        internal TemporaryCraftableItem Item;
        internal float CooldownUntil;
        internal bool MissingLogged;

        internal Entry(
            string internalName,
            string displayName,
            string normalizedName,
            Func<Quest, bool> matches)
        {
            InternalName = internalName;
            DisplayName = displayName;
            NormalizedName = normalizedName;
            Matches = matches;
        }
    }
}
