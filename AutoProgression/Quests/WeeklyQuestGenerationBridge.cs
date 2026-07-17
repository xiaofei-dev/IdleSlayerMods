using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Quests;

internal static class WeeklyQuestGenerationBridge
{
    private static readonly Queue<int[]> PendingGenerations = new();
    private static HashSet<int> activeBeforeGeneration;
    private static int generationDepth;
    private static int manualRerollDepth;

    internal static void BeginGeneration()
    {
        if (manualRerollDepth > 0) return;

        if (generationDepth++ == 0)
            activeBeforeGeneration = SnapshotActiveIds();
    }

    internal static void EndGeneration()
    {
        if (manualRerollDepth > 0 || generationDepth <= 0) return;
        if (--generationDepth > 0) return;

        HashSet<int> activeAfter = SnapshotActiveIds();
        List<int> newlyActive = new();
        foreach (int id in activeAfter)
        {
            if (activeBeforeGeneration == null || !activeBeforeGeneration.Contains(id))
                newlyActive.Add(id);
        }

        PendingGenerations.Enqueue(newlyActive.ToArray());
        activeBeforeGeneration = null;
    }

    internal static void BeginManualReroll() => manualRerollDepth++;

    internal static void EndManualReroll()
    {
        if (manualRerollDepth > 0) manualRerollDepth--;
    }

    internal static bool TryDequeue(out int[] newlyActiveIds)
    {
        if (PendingGenerations.Count > 0)
        {
            newlyActiveIds = PendingGenerations.Dequeue();
            return true;
        }

        newlyActiveIds = null;
        return false;
    }

    internal static void DiscardPending()
    {
        PendingGenerations.Clear();
        activeBeforeGeneration = null;
        generationDepth = 0;
        manualRerollDepth = 0;
    }

    private static HashSet<int> SnapshotActiveIds()
    {
        HashSet<int> result = new();
        foreach (WeeklyQuest quest in Resources.FindObjectsOfTypeAll<WeeklyQuest>())
        {
            if (quest != null && quest.active && !quest.isClaimed)
                result.Add(quest.GetInstanceID());
        }

        return result;
    }
}

[HarmonyPatch(typeof(WeeklyQuestsManager), nameof(WeeklyQuestsManager.RegenerateWeeklies))]
internal static class WeeklyQuestRegenerationPatch
{
    private static void Prefix() => WeeklyQuestGenerationBridge.BeginGeneration();
    private static void Postfix() => WeeklyQuestGenerationBridge.EndGeneration();
}

[HarmonyPatch(typeof(WeeklyQuestsManager), nameof(WeeklyQuestsManager.AddQuests))]
internal static class WeeklyQuestAdditionPatch
{
    private static void Prefix() => WeeklyQuestGenerationBridge.BeginGeneration();
    private static void Postfix() => WeeklyQuestGenerationBridge.EndGeneration();
}

[HarmonyPatch(typeof(WeeklyQuestReroll), nameof(WeeklyQuestReroll.RewardForShowing))]
internal static class WeeklyQuestManualRerollPatch
{
    private static void Prefix() => WeeklyQuestGenerationBridge.BeginManualReroll();

    private static Exception Finalizer(Exception __exception)
    {
        WeeklyQuestGenerationBridge.EndManualReroll();
        return __exception;
    }
}
