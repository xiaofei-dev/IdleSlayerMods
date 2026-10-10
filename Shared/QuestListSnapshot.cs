using System.Collections.Generic;
using Il2Cpp;

namespace IdleSlayerMods.Compatibility;

/// <summary>
/// Reads the categorized quest collections introduced with Idle Slayer's
/// Unity 6 update and exposes the same flat snapshot the mods previously read
/// from QuestsList.lastScrollListData.
/// </summary>
internal static class QuestListSnapshot
{
    internal static bool TryCapture(
        QuestsList questList,
        out List<Quest> quests)
    {
        quests = new List<Quest>();
        if (questList == null) return false;

        var seen = new HashSet<int>();
        bool available = false;
        available |= Append(questList.eventQuests, quests, seen);
        available |= AppendCollaborationQuests(questList, quests, seen);
        available |= Append(questList.weeklyQuests, quests, seen);
        available |= Append(questList.dailyQuests, quests, seen);
        available |= Append(questList.mainQuests, quests, seen);
        return available;
    }

    private static bool AppendCollaborationQuests(
        QuestsList questList,
        List<Quest> quests,
        HashSet<int> seen)
    {
        var collections = questList.collabQuests;
        if (collections == null) return false;

        foreach (var pair in collections)
            Append(pair.Value, quests, seen);
        return true;
    }

    private static bool Append(
        Il2CppSystem.Collections.Generic.List<Quest> source,
        List<Quest> quests,
        HashSet<int> seen)
    {
        if (source == null) return false;

        for (int index = 0; index < source.Count; index++)
        {
            Quest quest = source[index];
            if (quest == null) continue;

            int instanceId;
            try
            {
                instanceId = quest.GetInstanceID();
            }
            catch
            {
                // A quest can be replaced while RefreshList is rebuilding.
                // The next snapshot will pick up its fresh wrapper.
                continue;
            }

            if (seen.Add(instanceId)) quests.Add(quest);
        }

        return true;
    }
}
