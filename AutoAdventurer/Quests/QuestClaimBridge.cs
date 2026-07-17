using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Quests;

internal readonly record struct ClaimedQuestEvent(
    string LockKey, string QuestId, string QuestDisplayName, int InstanceId,
    bool IsDaily, bool IsWeekly);

internal static class QuestClaimBridge
{
    private static readonly Queue<ClaimedQuestEvent> Pending = new();
    private static readonly Dictionary<int, int> LastRecordedFrame = new();

    internal static void Record(Quest quest)
    {
        if (quest == null) return;
        try
        {
            // Weekly Quest lifecycle and rerolling are owned by the game or
            // AutoProgression. AutoAdventurer deliberately does not observe
            // or patch that flow, even if a WeeklyQuest reaches the base
            // Quest.Claim postfix.
            string runtimeType = quest.GetIl2CppType()?.Name ?? string.Empty;
            if (quest is WeeklyQuest || string.Equals(runtimeType,
                    nameof(WeeklyQuest), StringComparison.Ordinal))
                return;

            int instanceId = quest.GetInstanceID();
            int frame = Time.frameCount;
            if (LastRecordedFrame.TryGetValue(instanceId, out int lastFrame) &&
                lastFrame == frame)
                return;
            LastRecordedFrame[instanceId] = frame;

            bool daily = quest is DailyQuest || string.Equals(runtimeType,
                nameof(DailyQuest), StringComparison.Ordinal);
            Pending.Enqueue(new ClaimedQuestEvent(
                QuestTargetSelection.BuildLockKey(quest),
                quest.name ?? "UnknownQuest",
                string.IsNullOrWhiteSpace(quest.localizedName)
                    ? quest.name ?? "UnknownQuest"
                    : quest.localizedName,
                instanceId, daily, false));
        }
        catch
        {
            // Claiming must never fail because diagnostic capture failed.
        }
    }

    internal static bool TryDequeue(out ClaimedQuestEvent claimed)
    {
        if (Pending.Count > 0)
        {
            claimed = Pending.Dequeue();
            return true;
        }

        claimed = default;
        return false;
    }

    internal static void Clear()
    {
        Pending.Clear();
        LastRecordedFrame.Clear();
    }
}

[HarmonyPatch(typeof(Quest), nameof(Quest.Claim))]
internal static class QuestClaimPatch
{
    private static void Postfix(Quest __instance) =>
        QuestClaimBridge.Record(__instance);
}

[HarmonyPatch(typeof(DailyQuest), nameof(DailyQuest.Claim))]
internal static class DailyQuestClaimPatch
{
    private static void Postfix(DailyQuest __instance) =>
        QuestClaimBridge.Record(__instance);
}
