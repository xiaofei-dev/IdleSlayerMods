using System;
using System.Collections.Generic;
using AutoProgression.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Quests;

internal sealed class WeeklyRageQuestService
{
    private const int PreferredGoal = 180000;
    private const int MaximumRerollsPerGeneration = 200;

    private readonly HashSet<int> protectedQuestIds = new();
    private WeeklyQuestReroll reroll;
    private WeeklyQuest target;
    private bool processing;
    private int attempts;

    internal bool IsProcessing => processing;

    // This is event-driven. While a generated Weekly Quest is being corrected,
    // only one native reroll is issued per frame, with no timer-based delay.
    internal bool Tick()
    {
        if (!processing && !TryBeginNextGeneration())
            return false;

        if (HasPreferredQuest(FindActiveWeeklyQuests()))
            return FinishSuccessfully();

        reroll ??= WeeklyQuestReroll.instance;
        if (reroll == null || target == null)
        {
            ProgressionLog.Debug("Weekly Rage quest reroll objects are unavailable.");
            return Finish();
        }

        try
        {
            // RewardForShowing is the game's native reroll completion path.
            // Setting the target directly avoids opening an ad or UI popup.
            reroll.weeklyQuestToReroll = target;
            reroll.RewardForShowing();
            reroll.rerollEnabled = true;
            attempts++;
        }
        catch (Exception exception)
        {
            Plugin.Logger.Error($"Automatic Weekly Quest reroll failed safely: {exception}");
            return Finish();
        }

        List<WeeklyQuest> active = FindActiveWeeklyQuests();
        if (HasPreferredQuest(active))
            return FinishSuccessfully();

        if (attempts >= MaximumRerollsPerGeneration)
        {
            Plugin.Logger.Warning(
                $"Automatic Weekly Quest reroll stopped after {MaximumRerollsPerGeneration} attempts without finding the 180,000 Rage Mode kill quest.");
            return Finish();
        }

        target = FindUnprotectedQuest(active, protectedQuestIds);
        if (target == null)
        {
            Plugin.Logger.Warning(
                "Automatic Weekly Quest reroll stopped because the generated replacement could not be identified.");
            return Finish();
        }

        return false;
    }

    private bool TryBeginNextGeneration()
    {
        while (WeeklyQuestGenerationBridge.TryDequeue(out int[] newlyActiveIds))
        {
            if (!Plugin.Config.PreferMinimumRageWeeklyQuest.Value)
                continue;

            List<WeeklyQuest> active = FindActiveWeeklyQuests();
            if (active.Count == 0 || HasPreferredQuest(active))
                continue;

            target = FindRageQuest(active) ??
                     FindByInstanceId(active, newlyActiveIds ?? Array.Empty<int>()) ??
                     active[^1];

            protectedQuestIds.Clear();
            foreach (WeeklyQuest quest in active)
            {
                if (quest != target)
                    protectedQuestIds.Add(quest.GetInstanceID());
            }

            attempts = 0;
            processing = true;
            return true;
        }

        return false;
    }

    private bool FinishSuccessfully()
    {
        ProgressionLog.Info(
            $"Weekly Quest selected: 180,000 Rage Mode kills after {attempts} reroll(s).");
        return Finish();
    }

    private bool Finish()
    {
        processing = false;
        target = null;
        protectedQuestIds.Clear();
        attempts = 0;
        return true;
    }

    private static bool HasPreferredQuest(IEnumerable<WeeklyQuest> quests)
    {
        foreach (WeeklyQuest quest in quests)
        {
            if (quest.questType == QuestType.KillEnemiesWithRageMode &&
                quest.questGoal == PreferredGoal)
                return true;
        }

        return false;
    }

    private static WeeklyQuest FindRageQuest(IEnumerable<WeeklyQuest> quests)
    {
        foreach (WeeklyQuest quest in quests)
        {
            if (quest.questType == QuestType.KillEnemiesWithRageMode)
                return quest;
        }

        return null;
    }

    private static WeeklyQuest FindByInstanceId(
        IEnumerable<WeeklyQuest> quests,
        IReadOnlyCollection<int> candidateIds)
    {
        foreach (WeeklyQuest quest in quests)
        {
            if (candidateIds.Contains(quest.GetInstanceID()))
                return quest;
        }

        return null;
    }

    private static WeeklyQuest FindUnprotectedQuest(
        IEnumerable<WeeklyQuest> quests,
        IReadOnlySet<int> protectedIds)
    {
        foreach (WeeklyQuest quest in quests)
        {
            if (!protectedIds.Contains(quest.GetInstanceID()))
                return quest;
        }

        return null;
    }

    private static List<WeeklyQuest> FindActiveWeeklyQuests()
    {
        List<WeeklyQuest> result = new();
        foreach (WeeklyQuest quest in Resources.FindObjectsOfTypeAll<WeeklyQuest>())
        {
            if (quest != null && quest.active && !quest.isClaimed)
                result.Add(quest);
        }

        return result;
    }

    internal void Reset()
    {
        reroll = null;
        target = null;
        processing = false;
        attempts = 0;
        protectedQuestIds.Clear();
        WeeklyQuestGenerationBridge.DiscardPending();
    }
}
