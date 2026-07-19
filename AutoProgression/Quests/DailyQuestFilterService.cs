using System;
using System.Collections.Generic;
using AutoProgression.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Quests;

internal sealed class DailyQuestFilterService
{
    private const int MaximumRerollsPerGeneration = 500;
    private const int MaximumConsecutiveFailuresPerQuest = 5;
    private const float PostGenerationSettleSeconds = 5f;
    private const float PostRerollSettleSeconds = 0.2f;
    private const float FailedRerollRetrySeconds = 1f;
    private const float ObjectRetrySeconds = 0.5f;
    private const float UnavailableLogIntervalSeconds = 10f;

    private readonly HashSet<int> protectedQuestIds = new();
    private DailyQuest target;
    private bool processing;
    private bool completionVerificationPending;
    private int attempts;
    private int failedTargetId;
    private int consecutiveTargetFailures;
    private int skippedTargets;
    private float rerollReadyAt;
    private float nextUnavailableLogAt;

    internal bool IsProcessing => processing;

    internal bool Tick()
    {
        if (!processing && !TryBeginNextGeneration())
            return false;
        if (Time.unscaledTime < rerollReadyAt)
            return false;

        if (target == null && !PrepareNextTarget())
        {
            if (completionVerificationPending)
            {
                completionVerificationPending = false;
                rerollReadyAt = Time.unscaledTime + PostRerollSettleSeconds;
                return false;
            }

            return FinishSuccessfully();
        }

        completionVerificationPending = false;

        DailyQuestReroll reroll = ResolveReroll();
        if (reroll == null || target == null)
        {
            if (Time.unscaledTime >= nextUnavailableLogAt)
            {
                ProgressionLog.Debug(
                    "Generated Daily Quest reroll object is unavailable; waiting without discarding the generated set.");
                nextUnavailableLogAt =
                    Time.unscaledTime + UnavailableLogIntervalSeconds;
            }

            rerollReadyAt = Time.unscaledTime + ObjectRetrySeconds;
            return false;
        }

        if (IsReadyToClaim(target))
        {
            int completedId = target.GetInstanceID();
            protectedQuestIds.Add(completedId);
            ProgressionLog.Debug(
                $"Generated Daily Quest filtering skipped a completed quest: " +
                $"Id={completedId}, Type={target.questType}.");
            target = null;
            completionVerificationPending = true;
            rerollReadyAt = Time.unscaledTime + PostRerollSettleSeconds;
            return false;
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

        if (target.active)
        {
            if (invocationException != null)
            {
                ProgressionLog.Error(
                    $"Automatic Daily Quest reroll failed safely: {invocationException}");
                return Finish();
            }

            if (attempts >= MaximumRerollsPerGeneration)
            {
                ProgressionLog.Warning(
                    $"Generated Daily Quest filtering stopped after " +
                    $"{MaximumRerollsPerGeneration} rerolls.");
                return Finish();
            }

            int activeTargetId = target.GetInstanceID();
            if (failedTargetId == activeTargetId)
                consecutiveTargetFailures++;
            else
            {
                failedTargetId = activeTargetId;
                consecutiveTargetFailures = 1;
            }

            if (consecutiveTargetFailures >= MaximumConsecutiveFailuresPerQuest)
            {
                protectedQuestIds.Add(activeTargetId);
                skippedTargets++;
                ProgressionLog.Warning(
                    $"Generated Daily Quest filtering skipped an unresponsive slot " +
                    $"after {MaximumConsecutiveFailuresPerQuest} attempts: " +
                    $"Id={activeTargetId}, Type={target.questType}.");
                failedTargetId = 0;
                consecutiveTargetFailures = 0;
            }
            else
            {
                ProgressionLog.Debug(
                    $"Native Daily Quest reroll left its target active; " +
                    $"retrying with fresh objects in {FailedRerollRetrySeconds:0.#}s " +
                    $"({consecutiveTargetFailures}/{MaximumConsecutiveFailuresPerQuest}).");
            }

            target = null;
            completionVerificationPending = true;
            rerollReadyAt = Time.unscaledTime + FailedRerollRetrySeconds;
            return false;
        }

        failedTargetId = 0;
        consecutiveTargetFailures = 0;

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

        // Native quest replacement updates are not guaranteed to become fully
        // visible in the same frame as RewardForShowing(). Reacquire the whole
        // active set after a short delay instead of trusting a transient list.
        target = null;
        completionVerificationPending = true;
        rerollReadyAt = Time.unscaledTime + PostRerollSettleSeconds;

        return false;
    }

    private bool TryBeginNextGeneration()
    {
        while (DailyQuestGenerationBridge.TryDequeue(out _))
        {
            if (!Plugin.Config.FilterGeneratedDailyQuests.Value)
                continue;

            protectedQuestIds.Clear();
            target = null;
            completionVerificationPending = false;
            attempts = 0;
            failedTargetId = 0;
            consecutiveTargetFailures = 0;
            skippedTargets = 0;
            rerollReadyAt = Time.unscaledTime + PostGenerationSettleSeconds;
            nextUnavailableLogAt = 0f;
            processing = true;
            return true;
        }

        return false;
    }

    private bool PrepareNextTarget()
    {
        List<DailyQuest> active = FindActiveDailyQuests();
        if (active.Count == 0) return false;

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
            QuestType.GetMaterials or
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

    private static bool IsReadyToClaim(DailyQuest quest)
    {
        if (quest == null || quest.isClaimed)
            return true;

        try
        {
            return quest.CanBeClaimed();
        }
        catch
        {
            // A rebuilding native object is not safe to reroll this frame.
            return true;
        }
    }

    private static DailyQuestReroll ResolveReroll()
    {
        DailyQuestReroll instance = DailyQuestReroll.instance;
        if (instance != null) return instance;

        foreach (DailyQuestReroll candidate in
                 Resources.FindObjectsOfTypeAll<DailyQuestReroll>())
        {
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    private bool FinishSuccessfully()
    {
        if (skippedTargets > 0)
        {
            ProgressionLog.Warning(
                $"Generated Daily Quest filtering finished with {skippedTargets} " +
                $"unresponsive slot(s) left unchanged after {attempts} total attempt(s).");
            return Finish();
        }

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
        completionVerificationPending = false;
        protectedQuestIds.Clear();
        attempts = 0;
        failedTargetId = 0;
        consecutiveTargetFailures = 0;
        skippedTargets = 0;
        rerollReadyAt = 0f;
        nextUnavailableLogAt = 0f;
        return true;
    }

    private static List<DailyQuest> FindActiveDailyQuests()
    {
        List<DailyQuest> result = new();
        foreach (DailyQuest quest in
                 Resources.FindObjectsOfTypeAll<DailyQuest>())
        {
            if (quest != null && quest.active && !IsReadyToClaim(quest))
                result.Add(quest);
        }

        return result;
    }

    internal void Reset()
    {
        processing = false;
        target = null;
        completionVerificationPending = false;
        protectedQuestIds.Clear();
        attempts = 0;
        failedTargetId = 0;
        consecutiveTargetFailures = 0;
        skippedTargets = 0;
        rerollReadyAt = 0f;
        nextUnavailableLogAt = 0f;
        DailyQuestGenerationBridge.DiscardPending();
    }
}
