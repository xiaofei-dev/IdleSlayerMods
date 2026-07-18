using System;
using System.Collections.Generic;
using AutoProgression.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Quests;

internal sealed class DailyQuestFilterService
{
    private const int MaximumRerollsPerGeneration = 500;
    private const float PostGenerationSettleSeconds = 5f;

    private readonly HashSet<int> protectedQuestIds = new();
    private DailyQuest target;
    private int[] generatedQuestIds = Array.Empty<int>();
    private bool processing;
    private int attempts;
    private float rerollReadyAt;

    internal bool IsProcessing => processing;

    internal bool Tick()
    {
        if (!processing && !TryBeginNextGeneration())
            return false;
        if (Time.unscaledTime < rerollReadyAt)
            return false;

        if (target == null && !PrepareNextTarget())
            return FinishSuccessfully();

        DailyQuestReroll reroll = DailyQuestReroll.instance;
        if (reroll == null || target == null)
        {
            ProgressionLog.Debug(
                "Generated Daily Quest filter objects are unavailable.");
            return Finish();
        }

        Exception invocationException = null;
        try
        {
            reroll.rerollEnabled = true;
            if (!reroll.rerollEnabled)
            {
                ProgressionLog.Warning(
                    "Automatic Daily Quest reroll could not restore the native reroll permission.");
                return Finish();
            }

            int targetId = target.GetInstanceID();
            reroll.PrepareReroll(target);
            int boundId = reroll.dailyQuestToReroll?.GetInstanceID() ?? 0;
            if (boundId != targetId)
            {
                ProgressionLog.Warning(
                    $"Automatic Daily Quest reroll did not bind its selected target. " +
                    $"TargetId={targetId}, BoundAfter={boundId}.");
                return Finish();
            }

            ProgressionLog.Debug(
                $"Rerolling generated Daily Quest: Id={targetId}, " +
                $"Type={target.questType}, Name={target.name}.");
            attempts++;
            reroll.RewardForShowing();
        }
        catch (Exception exception)
        {
            invocationException = exception;
        }

        List<DailyQuest> active = FindActiveDailyQuests();
        if (target.active)
        {
            if (invocationException != null)
                ProgressionLog.Error(
                    $"Automatic Daily Quest reroll failed safely: {invocationException}");
            else
                ProgressionLog.Warning(
                    "Automatic Daily Quest reroll did not replace its selected quest.");
            return Finish();
        }

        if (invocationException != null)
        {
            ProgressionLog.Debug(
                "The native Daily Quest reroll completed before a non-fatal UI update exception.");
        }

        if (attempts >= MaximumRerollsPerGeneration)
        {
            ProgressionLog.Warning(
                $"Generated Daily Quest filtering stopped after " +
                $"{MaximumRerollsPerGeneration} rerolls.");
            return Finish();
        }

        DailyQuest replacement = FindUnprotected(active);
        if (replacement == null)
        {
            ProgressionLog.Warning(
                "Generated Daily Quest filtering stopped because the replacement could not be identified.");
            return Finish();
        }

        if (ShouldReroll(replacement))
            target = replacement;
        else
        {
            protectedQuestIds.Add(replacement.GetInstanceID());
            target = null;
        }

        return false;
    }

    private bool TryBeginNextGeneration()
    {
        while (DailyQuestGenerationBridge.TryDequeue(out int[] ids))
        {
            if (!Plugin.Config.FilterGeneratedDailyQuests.Value)
                continue;

            generatedQuestIds = ids ?? Array.Empty<int>();
            protectedQuestIds.Clear();
            target = null;
            attempts = 0;
            rerollReadyAt = Time.unscaledTime + PostGenerationSettleSeconds;
            processing = true;
            return true;
        }

        return false;
    }

    private bool PrepareNextTarget()
    {
        List<DailyQuest> active = FindActiveDailyQuests();
        if (active.Count == 0) return false;

        if (protectedQuestIds.Count == 0)
        {
            HashSet<int> generated = new(generatedQuestIds);
            foreach (DailyQuest quest in active)
            {
                if (!generated.Contains(quest.GetInstanceID()))
                    protectedQuestIds.Add(quest.GetInstanceID());
            }
        }

        foreach (DailyQuest quest in active)
        {
            if (protectedQuestIds.Contains(quest.GetInstanceID()))
                continue;
            if (ShouldReroll(quest))
            {
                target = quest;
                return true;
            }

            protectedQuestIds.Add(quest.GetInstanceID());
        }

        return false;
    }

    private DailyQuest FindUnprotected(IEnumerable<DailyQuest> quests)
    {
        foreach (DailyQuest quest in quests)
        {
            if (!protectedQuestIds.Contains(quest.GetInstanceID()))
                return quest;
        }

        return null;
    }

    private static bool ShouldReroll(DailyQuest quest)
    {
        if (quest == null) return false;
        if (HasGoblinObjective(quest)) return true;

        return quest.questType is QuestType.ChestHuntChests or
            QuestType.HitRandomBoxes or
            QuestType.HitRandomSilverBoxes or
            QuestType.Boost or
            QuestType.UseRageMode or
            QuestType.BonusStage or
            QuestType.BonusStageSections or
            QuestType.CompleteAscendingHeights or
            QuestType.CompleteGrappleRuns;
    }

    private static bool HasGoblinObjective(Quest quest)
    {
        if (ContainsText(quest.enemyType?.name, "goblin") ||
            ContainsText(quest.name, "goblin"))
            return true;

        HashSet<string> visited = new(StringComparer.Ordinal);
        Enemy enemy = quest.enemyToKill;
        for (int depth = 0; enemy != null && depth < 32; depth++)
        {
            string name = enemy.name ?? string.Empty;
            if (ContainsText(name, "goblin")) return true;
            if (!visited.Add(name)) break;
            enemy = enemy.evolutionBackward;
        }

        return false;
    }

    private static bool ContainsText(string value, string fragment) =>
        !string.IsNullOrEmpty(value) && value.IndexOf(
            fragment, StringComparison.OrdinalIgnoreCase) >= 0;

    private bool FinishSuccessfully()
    {
        if (attempts > 0)
        {
            ProgressionLog.User(
                $"Generated Daily Quests filtered after {attempts} reroll(s).");
        }
        else
        {
            ProgressionLog.Debug(
                "Generated Daily Quests passed the automatic filter without rerolls.");
        }
        return Finish();
    }

    private bool Finish()
    {
        processing = false;
        target = null;
        generatedQuestIds = Array.Empty<int>();
        protectedQuestIds.Clear();
        attempts = 0;
        rerollReadyAt = 0f;
        return true;
    }

    private static List<DailyQuest> FindActiveDailyQuests()
    {
        List<DailyQuest> result = new();
        foreach (DailyQuest quest in
                 Resources.FindObjectsOfTypeAll<DailyQuest>())
        {
            if (quest != null && quest.active && !quest.isClaimed)
                result.Add(quest);
        }

        return result;
    }

    internal void Reset()
    {
        processing = false;
        target = null;
        generatedQuestIds = Array.Empty<int>();
        protectedQuestIds.Clear();
        attempts = 0;
        rerollReadyAt = 0f;
        DailyQuestGenerationBridge.DiscardPending();
    }
}
