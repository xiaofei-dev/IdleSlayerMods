using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace AutoAdventurer.Quests;

internal readonly record struct ClaimedQuestEvent(
    string LockKey, string QuestId, bool IsDaily);

internal static class QuestClaimBridge
{
    private static readonly Queue<ClaimedQuestEvent> Pending = new();

    internal static void Record(Quest quest)
    {
        if (quest == null) return;
        try
        {
            string runtimeType = quest.GetIl2CppType()?.Name ?? string.Empty;
            bool daily = quest is DailyQuest || string.Equals(runtimeType,
                nameof(DailyQuest), StringComparison.Ordinal);
            Pending.Enqueue(new ClaimedQuestEvent(
                QuestTargetSelection.BuildLockKey(quest),
                quest.name ?? "UnknownQuest", daily));
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

    internal static void Clear() => Pending.Clear();
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

[HarmonyPatch(typeof(WeeklyQuest), nameof(WeeklyQuest.Claim))]
internal static class WeeklyQuestClaimPatch
{
    private static void Postfix(WeeklyQuest __instance) =>
        QuestClaimBridge.Record(__instance);
}
