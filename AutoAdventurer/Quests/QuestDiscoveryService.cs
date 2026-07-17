using System;
using System.Collections.Generic;
using AutoAdventurer.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Quests;

internal sealed class QuestDiscoveryService
{
    private QuestsList resolvedQuestList;
    private bool unavailableLogged;
    private float nextListResolveTime;
    private float nextSafeRefreshTime;
    private float nextProgressRefreshTime;
    private string lastRefreshLog = string.Empty;
    private string lastActiveDailySignature = string.Empty;
    private string lastSupplementLogSignature = string.Empty;
    private bool dailySignatureInitialized;

    internal bool LastSnapshotAvailable { get; private set; }
    internal bool WatchedQuestCompleted { get; private set; }
    internal bool ActiveDailySetChanged { get; private set; }

    internal List<Quest> SnapshotActiveIncomplete(
        string watchedQuestKey = "")
    {
        WatchedQuestCompleted = false;
        ActiveDailySetChanged = false;
        QuestsList questsList = QuestsList.instance ?? resolvedQuestList;
        if (questsList == null && Time.unscaledTime >= nextListResolveTime)
        {
            nextListResolveTime = Time.unscaledTime + 30f;
            questsList = FindLoadedQuestList();
            if (questsList != null)
            {
                resolvedQuestList = questsList;
                AdventurerLog.QuestDebug(
                    "Quest list: resolved the inactive component in the loaded game scene.");
            }
        }

        var source = questsList?.lastScrollListData;
        if (source == null && Time.unscaledTime >= nextSafeRefreshTime)
        {
            nextSafeRefreshTime = Time.unscaledTime + 30f;
            if (TryRefreshInactiveList(questsList,
                    "Quest list: initialized while the quest panel was closed."))
                source = questsList.lastScrollListData;
        }
        else if (source != null)
        {
            ObserveWatchedCompletion(source, watchedQuestKey);
            if (Time.unscaledTime >= nextProgressRefreshTime &&
                ContainsSettledQuest(source))
            {
                nextProgressRefreshTime = Time.unscaledTime + 5f;
                if (TryRefreshInactiveList(questsList,
                        "Quest list: refreshed after quest progress changed."))
                    source = questsList.lastScrollListData;
            }
        }

        if (source == null)
        {
            LastSnapshotAvailable = false;
            if (!unavailableLogged)
            {
                unavailableLogged = true;
                AdventurerLog.QuestDebug(
                    $"Quest list: waiting for game data; componentResolved={questsList != null}.");
            }
            return new List<Quest>();
        }

        LastSnapshotAvailable = true;
        unavailableLogged = false;
        ObserveWatchedCompletion(source, watchedQuestKey);
        var result = new List<Quest>(source.Count);
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 0; index < source.Count; index++)
        {
            Quest quest = source[index];
            // Daily quests are rebuilt below from their authoritative live
            // active flags. The UI cache can retain entries after a reroll.
            if (quest == null || quest is DailyQuest ||
                quest is WeeklyQuest || quest.isClaimed)
                continue;

            try
            {
                if (!quest.IsCompleted() && seen.Add(
                        QuestTargetSelection.BuildLockKey(quest)))
                    result.Add(quest);
            }
            catch (Exception exception)
            {
                AdventurerLog.QuestDebug(
                    $"Quest list: skipped unreadable entry index={index}; exception={exception.GetType().Name}.");
            }
        }

        int dailyAdded = AppendActiveDailyQuests(result, seen);
        if (dailyAdded > 0 && !string.Equals(lastSupplementLogSignature,
                lastActiveDailySignature, StringComparison.Ordinal))
        {
            lastSupplementLogSignature = lastActiveDailySignature;
            AdventurerLog.QuestDebug(
                $"Quest list: supplemented {dailyAdded} active Daily quest(s) missing from the UI cache.");
        }

        return result;
    }

    private int AppendActiveDailyQuests(
        List<Quest> result, HashSet<string> seen)
    {
        int added = 0;
        SortedSet<string> activeKeys = new(StringComparer.Ordinal);
        foreach (DailyQuest daily in
                 Resources.FindObjectsOfTypeAll<DailyQuest>())
        {
            if (daily == null || !daily.active || daily.isClaimed) continue;
            string lockKey = QuestTargetSelection.BuildLockKey(daily);
            activeKeys.Add(lockKey);
            try
            {
                if (!daily.CheckIfIsValid() || daily.IsCompleted()) continue;
                if (!seen.Add(lockKey)) continue;
                result.Add(daily);
                added++;
            }
            catch (Exception exception)
            {
                AdventurerLog.QuestDebug(
                    $"Quest list: skipped unreadable active Daily quest; quest={daily.name ?? "UnknownDaily"}; exception={exception.GetType().Name}.");
            }
        }

        string signature = string.Join("|", activeKeys);
        if (dailySignatureInitialized && !string.Equals(signature,
                lastActiveDailySignature, StringComparison.Ordinal))
            ActiveDailySetChanged = true;
        lastActiveDailySignature = signature;
        dailySignatureInitialized = true;

        return added;
    }

    private void ObserveWatchedCompletion(
        Il2CppSystem.Collections.Generic.List<Quest> quests,
        string watchedQuestKey)
    {
        if (WatchedQuestCompleted || quests == null ||
            string.IsNullOrEmpty(watchedQuestKey)) return;

        for (int index = 0; index < quests.Count; index++)
        {
            Quest quest = quests[index];
            if (quest == null || !string.Equals(
                    QuestTargetSelection.BuildLockKey(quest), watchedQuestKey,
                    StringComparison.Ordinal)) continue;

            try
            {
                WatchedQuestCompleted = quest.isClaimed || quest.IsCompleted();
            }
            catch
            {
                WatchedQuestCompleted = false;
            }
            return;
        }
    }

    private static bool ContainsSettledQuest(
        Il2CppSystem.Collections.Generic.List<Quest> quests)
    {
        for (int index = 0; index < quests.Count; index++)
        {
            Quest quest = quests[index];
            if (quest == null) continue;
            try
            {
                if (quest.isClaimed || quest.IsCompleted()) return true;
            }
            catch
            {
                // The snapshot pass reports an unreadable entry safely.
            }
        }

        return false;
    }

    private bool TryRefreshInactiveList(QuestsList questsList, string successMessage)
    {
        if (questsList == null) return false;
        GameStates state = GameState.current;
        if (state != GameStates.RunnerMode && state != GameStates.RageMode)
            return false;

        MapController maps = MapController.instance;
        if (maps == null || !maps.initialized || maps.changingMap)
            return false;

        // RefreshList owns UI data. It is safe only while its actual scroll
        // view is inactive; calling it while the quest panel is drawing can
        // re-enter the game's list rebuild and freeze the client.
        bool listVisible = questsList.questsScrollRect != null
            ? questsList.questsScrollRect.gameObject.activeInHierarchy
            : questsList.gameObject.activeInHierarchy;
        if (listVisible) return false;

        try
        {
            questsList.RefreshList();
            if (!string.Equals(successMessage, lastRefreshLog,
                    StringComparison.Ordinal))
            {
                lastRefreshLog = successMessage;
                AdventurerLog.QuestDebug(successMessage);
            }
            return questsList.lastScrollListData != null;
        }
        catch (Exception exception)
        {
            AdventurerLog.QuestDebug(
                $"Quest list: safe refresh deferred; exception={exception.GetType().Name}.");
            return false;
        }
    }

    private static QuestsList FindLoadedQuestList()
    {
        foreach (QuestsList candidate in
                 Resources.FindObjectsOfTypeAll<QuestsList>())
        {
            if (candidate == null || candidate.gameObject == null) continue;
            var scene = candidate.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded) return candidate;
        }

        return null;
    }

    internal void Reset()
    {
        resolvedQuestList = null;
        unavailableLogged = false;
        nextListResolveTime = 0f;
        nextSafeRefreshTime = 0f;
        nextProgressRefreshTime = 0f;
        LastSnapshotAvailable = false;
        WatchedQuestCompleted = false;
        lastRefreshLog = string.Empty;
        lastActiveDailySignature = string.Empty;
        lastSupplementLogSignature = string.Empty;
        dailySignatureInitialized = false;
        ActiveDailySetChanged = false;
    }

    internal void InvalidateSceneObjects()
    {
        resolvedQuestList = null;
        nextListResolveTime = 0f;
        nextSafeRefreshTime = 0f;
        nextProgressRefreshTime = 0f;
        LastSnapshotAvailable = false;
        WatchedQuestCompleted = false;
        ActiveDailySetChanged = false;
    }
}
