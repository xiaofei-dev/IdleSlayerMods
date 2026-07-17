using System;
using System.Collections.Generic;
using AutoAdventurer.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Quests;

internal sealed class CompletedQuestClaimService
{
    private const float CheckIntervalSeconds = 2f;
    private float nextCheckTime;

    internal void Tick(float now)
    {
        if (!Plugin.Config.AutoClaimCompletedQuests.Value ||
            now < nextCheckTime) return;
        nextCheckTime = now + CheckIntervalSeconds;

        int claimed = 0;
        HashSet<int> seen = new();
        foreach (Quest quest in Resources.FindObjectsOfTypeAll<Quest>())
        {
            if (quest == null || !seen.Add(quest.GetInstanceID())) continue;
            try
            {
                if (!quest.CanBeClaimed()) continue;
                quest.Claim();
                claimed++;
            }
            catch (Exception exception)
            {
                AdventurerLog.QuestDebug(
                    $"Auto Claim: failed safely; quest={quest.name ?? "UnknownQuest"}; exception={exception.GetType().Name}.");
            }
        }

        if (claimed > 0)
            AdventurerLog.QuestDebug(
                $"Auto Claim: claimed {claimed} completed quest(s).");
    }

    internal void Reset() => nextCheckTime = 0f;
}
