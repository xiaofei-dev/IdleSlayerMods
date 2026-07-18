using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Quests;

internal static class DailyQuestGenerationBridge
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
            if (activeBeforeGeneration == null ||
                !activeBeforeGeneration.Contains(id))
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
        foreach (DailyQuest quest in
                 Resources.FindObjectsOfTypeAll<DailyQuest>())
        {
            if (quest != null && quest.active && !quest.isClaimed)
                result.Add(quest.GetInstanceID());
        }

        return result;
    }
}

[HarmonyPatch(typeof(DailyQuestsManager), nameof(DailyQuestsManager.RegenerateDailys))]
internal static class DailyQuestRegenerationPatch
{
    private static void Prefix() => DailyQuestGenerationBridge.BeginGeneration();
    private static void Postfix() => DailyQuestGenerationBridge.EndGeneration();
}

[HarmonyPatch(typeof(DailyQuestsManager), nameof(DailyQuestsManager.AddQuests))]
internal static class DailyQuestAdditionPatch
{
    private static void Prefix() => DailyQuestGenerationBridge.BeginGeneration();
    private static void Postfix() => DailyQuestGenerationBridge.EndGeneration();
}

[HarmonyPatch(typeof(DailyQuestReroll), nameof(DailyQuestReroll.RewardForShowing))]
internal static class DailyQuestManualRerollPatch
{
    private static void Prefix() => DailyQuestGenerationBridge.BeginManualReroll();

    private static Exception Finalizer(
        DailyQuestReroll __instance,
        Exception __exception)
    {
        DailyQuestGenerationBridge.EndManualReroll();
        if (__exception == null) return null;

        try
        {
            DailyQuest target = __instance?.dailyQuestToReroll;
            if (target != null && !target.active)
                return null;
        }
        catch
        {
            // Retain the original exception when completion cannot be proven.
        }

        return __exception;
    }
}
