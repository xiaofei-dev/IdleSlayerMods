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
    private const float PostGenerationSettleSeconds = 5f;

    private readonly HashSet<int> protectedQuestIds = new();
    private WeeklyQuest target;
    private int[] generatedQuestIds = Array.Empty<int>();
    private bool processing;
    private int attempts;
    private float rerollReadyAt;

    internal bool IsProcessing => processing;

    // This is event-driven. While a generated Weekly Quest is being corrected,
    // only one native reroll is issued per frame, with no timer-based delay.
    internal bool Tick()
    {
        if (!processing && !TryBeginNextGeneration())
            return false;

        if (Time.unscaledTime < rerollReadyAt)
            return false;

        if (HasPreferredQuest(FindActiveWeeklyQuests()))
            return FinishSuccessfully();

        if (target == null && !PrepareTargetAfterSettle())
            return Finish();

        WeeklyQuestReroll reroll = WeeklyQuestReroll.instance;
        if (reroll == null || target == null)
        {
            ProgressionLog.Debug("Weekly Rage quest replacement objects are unavailable.");
            return Finish();
        }

        Exception invocationException = null;
        try
        {
            // The game consumes this flag after every native reroll. Normal
            // quest maintenance is paused while this service is processing,
            // so each automatic attempt must restore it independently.
            reroll.rerollEnabled = true;
            if (!reroll.rerollEnabled)
            {
                ProgressionLog.Warning(
                    "Automatic Weekly Quest reroll could not restore the native reroll permission.");
                return Finish();
            }

            int targetId = target.GetInstanceID();
            int boundBefore = reroll.weeklyQuestToReroll?.GetInstanceID() ?? 0;
            ProgressionLog.Debug(
                $"Preparing native Weekly Quest reroll: TargetId={targetId}, " +
                $"BoundBefore={boundBefore}, RerollEnabled={reroll.rerollEnabled}.");

            reroll.PrepareReroll(target);
            int boundAfter = reroll.weeklyQuestToReroll?.GetInstanceID() ?? 0;
            if (boundAfter != targetId)
            {
                ProgressionLog.Warning(
                    $"Automatic Weekly Quest reroll did not bind its selected target. " +
                    $"TargetId={targetId}, BoundAfter={boundAfter}.");
                return Finish();
            }

            attempts++;
            reroll.RewardForShowing();
        }
        catch (Exception exception)
        {
            // Some game versions finish the native replacement before their UI
            // icon update throws. Validate the quest data below before deciding
            // whether this attempt failed.
            invocationException = exception;
        }

        List<WeeklyQuest> active = FindActiveWeeklyQuests();
        ProgressionLog.Debug(
            $"Native Weekly Quest reroll returned: TargetId={target.GetInstanceID()}, " +
            $"TargetActive={target.active}, ActiveCount={active.Count}, " +
            $"BoundAfter={reroll.weeklyQuestToReroll?.GetInstanceID() ?? 0}.");
        if (target.active)
        {
            if (invocationException != null)
                ProgressionLog.Exception(
                    "Automatic Weekly Quest reroll",
                    invocationException);
            else
                ProgressionLog.Warning("Automatic Weekly Quest reroll did not replace its selected quest.");

            return Finish();
        }

        if (invocationException != null)
        {
            ProgressionLog.Debug(
                "The native Weekly Quest reroll completed before a non-fatal UI update exception.");
        }

        if (HasPreferredQuest(active))
            return FinishSuccessfully();

        if (attempts >= MaximumRerollsPerGeneration)
        {
            ProgressionLog.Warning(
                $"Automatic Weekly Quest reroll stopped after {MaximumRerollsPerGeneration} attempts without finding the 180,000 Rage Mode kill quest.");
            return Finish();
        }

        target = FindUnprotectedQuest(active, protectedQuestIds);
        if (target == null)
        {
            ProgressionLog.Warning(
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

            target = null;
            generatedQuestIds = newlyActiveIds ?? Array.Empty<int>();
            protectedQuestIds.Clear();
            attempts = 0;
            rerollReadyAt = Time.unscaledTime + PostGenerationSettleSeconds;
            processing = true;
            return true;
        }

        return false;
    }

    private bool PrepareTargetAfterSettle()
    {
        List<WeeklyQuest> active = FindActiveWeeklyQuests();
        if (active.Count == 0)
            return false;

        target = FindRageQuest(active) ??
                 FindByInstanceId(active, generatedQuestIds) ??
                 active[^1];

        protectedQuestIds.Clear();
        foreach (WeeklyQuest quest in active)
        {
            if (quest != target)
                protectedQuestIds.Add(quest.GetInstanceID());
        }

        return true;
    }

    private bool FinishSuccessfully()
    {
        ProgressionLog.User(
            $"Weekly Quest selected: 180,000 Rage Mode kills after {attempts} reroll(s).");
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
        target = null;
        generatedQuestIds = Array.Empty<int>();
        processing = false;
        attempts = 0;
        rerollReadyAt = 0f;
        protectedQuestIds.Clear();
        WeeklyQuestGenerationBridge.DiscardPending();
    }
}
