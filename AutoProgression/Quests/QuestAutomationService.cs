using System;
using System.Collections.Generic;
using AutoProgression.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Quests;

internal sealed class QuestAutomationService
{
    private const float CheckIntervalSeconds = 2f;
    private const float ClaimSettleSeconds = 5f;
    private const float RegenerationSettleSeconds = 5f;

    private QuestsList questsList;
    private DailyQuestsManager dailyManager;
    private WeeklyQuestsManager weeklyManager;
    private DailyQuestReroll dailyReroll;
    private WeeklyQuestReroll weeklyReroll;
    private PortalButton portalButton;
    private float nextCheckTime;
    private bool missingLogged;
    private bool refreshRequired = true;
    private int pendingClaimInstanceId;

    internal void Tick(float now)
    {
        // Rerolling is a manual UI action. Restore both flags every frame so
        // the player never has to wait for the slower quest maintenance pass.
        try
        {
            dailyReroll ??= DailyQuestReroll.instance;
            weeklyReroll ??= WeeklyQuestReroll.instance;
            ApplyUnlimitedRerolls();
        }
        catch (Exception exception)
        {
            Plugin.Logger.Error($"Quest reroll automation failed safely: {exception}");
        }

        if (now < nextCheckTime) return;
        nextCheckTime = now + CheckIntervalSeconds;

        try
        {
            ResolveObjects();
            ApplyPortalCooldownReset();
        }
        catch (Exception exception)
        {
            Plugin.Logger.Error($"Quest automation setup failed safely: {exception}");
            return;
        }

        if (questsList == null)
        {
            LogMissing();
            return;
        }

        if (refreshRequired)
        {
            try
            {
                questsList.RefreshList();
                refreshRequired = false;
            }
            catch (Exception exception)
            {
                Plugin.Logger.Error($"Failed to refresh the quest list safely: {exception}");
                return;
            }
        }

        var quests = questsList.lastScrollListData;
        if (quests == null)
        {
            LogMissing();
            return;
        }

        missingLogged = false;
        List<Quest> snapshot = new(quests.Count);
        for (int index = 0; index < quests.Count; index++)
        {
            Quest quest = quests[index];
            if (quest != null) snapshot.Add(quest);
        }

        if (Plugin.Config.AutoClaimCompletedQuests.Value)
        {
            List<Quest> claimCandidates = BuildClaimCandidates(snapshot);
            ClaimResult claimResult = TryClaimOneCompleted(claimCandidates);
            if (claimResult != ClaimResult.None)
            {
                if (claimResult == ClaimResult.Claimed)
                {
                    ProgressionLog.Debug("Automatically claimed 1 completed quest.");
                }

                // Quest.Claim() can rebuild the native quest collection, and it may
                // throw after partially completing that rebuild. Never inspect any
                // other object from this snapshot after a claim attempt.
                refreshRequired = true;
                nextCheckTime = now +
                    (claimResult == ClaimResult.Pending
                        ? ClaimSettleSeconds
                        : RegenerationSettleSeconds);
                return;
            }
        }

        RegenerateMissingQuestTypes(snapshot, now);
    }

    private static List<Quest> BuildClaimCandidates(List<Quest> uiSnapshot)
    {
        List<Quest> result = new();
        HashSet<int> seen = new();

        // The UI cache can retain inactive Daily and Weekly entries after
        // regeneration or rerolling. Only ordinary quests are trusted here.
        foreach (Quest quest in uiSnapshot)
        {
            if (quest == null || quest.isClaimed ||
                quest is DailyQuest || quest is WeeklyQuest)
                continue;

            if (seen.Add(quest.GetInstanceID()))
                result.Add(quest);
        }

        // Daily and Weekly candidates come from their authoritative live
        // ScriptableObjects instead of QuestsList.lastScrollListData.
        foreach (DailyQuest daily in Resources.FindObjectsOfTypeAll<DailyQuest>())
        {
            if (daily == null || !daily.active || daily.isClaimed)
                continue;

            try
            {
                if (daily.CheckIfIsValid() && seen.Add(daily.GetInstanceID()))
                    result.Add(daily);
            }
            catch
            {
                // An object being rebuilt is not a safe claim candidate yet.
            }
        }

        foreach (WeeklyQuest weekly in Resources.FindObjectsOfTypeAll<WeeklyQuest>())
        {
            if (weekly == null || !weekly.active || weekly.isClaimed)
                continue;

            if (seen.Add(weekly.GetInstanceID()))
                result.Add(weekly);
        }

        return result;
    }

    private ClaimResult TryClaimOneCompleted(List<Quest> quests)
    {
        foreach (Quest quest in quests)
        {
            try
            {
                if (!quest.CanBeClaimed()) continue;

                int instanceId = quest.GetInstanceID();
                if (pendingClaimInstanceId != instanceId)
                {
                    pendingClaimInstanceId = instanceId;
                    return ClaimResult.Pending;
                }

                quest.Claim();
                pendingClaimInstanceId = 0;
                return ClaimResult.Claimed;
            }
            catch (Exception exception)
            {
                pendingClaimInstanceId = 0;

                try
                {
                    // Weekly Quest claiming can finish its data update before
                    // an unavailable UI element throws. Treat the operation as
                    // successful when the authoritative claimed flag changed.
                    if (quest != null && quest.isClaimed)
                    {
                        ProgressionLog.Debug(
                            $"Quest claim completed before a non-fatal native UI exception: " +
                            $"{DescribeQuest(quest)}.");
                        return ClaimResult.Claimed;
                    }
                }
                catch
                {
                    // Fall through to the original error when the quest state
                    // itself can no longer be read safely.
                }

                Plugin.Logger.Error(
                    $"Failed to claim a quest safely: {DescribeQuest(quest)}; {exception}");
                return ClaimResult.Failed;
            }
        }

        pendingClaimInstanceId = 0;
        return ClaimResult.None;
    }

    private static string DescribeQuest(Quest quest)
    {
        if (quest == null) return "Quest=null";

        try
        {
            return $"Type={quest.GetIl2CppType()?.FullName ?? "Unknown"}, " +
                   $"Id={quest.GetInstanceID()}, Name={quest.name ?? "Unknown"}, " +
                   $"LocalizedName={quest.localizedName ?? "Unknown"}, " +
                   $"IsClaimed={quest.isClaimed}";
        }
        catch
        {
            return "Quest metadata unavailable";
        }
    }

    private enum ClaimResult
    {
        None,
        Pending,
        Claimed,
        Failed
    }

    private void RegenerateMissingQuestTypes(List<Quest> quests, float now)
    {
        int dailyCount = 0;
        int weeklyCount = 0;
        foreach (Quest quest in quests)
        {
            string typeName = quest.GetIl2CppType()?.FullName ?? string.Empty;
            if (typeName.EndsWith("DailyQuest", StringComparison.Ordinal)) dailyCount++;
            else if (typeName.EndsWith("WeeklyQuest", StringComparison.Ordinal)) weeklyCount++;
        }

        bool regenerated = false;
        CraftableItems craftables = CraftableItems.list;

        if (dailyCount == 0 &&
            Plugin.Config.RegenerateDailyQuests.Value &&
            dailyManager != null &&
            craftables?.DailyQuests?.IsActive() == true)
        {
            try
            {
                dailyManager.RegenerateDailys();
                ProgressionLog.Info("Generated a new set of Daily Quests.");
                regenerated = true;
            }
            catch (Exception exception)
            {
                Plugin.Logger.Error($"Failed to regenerate Daily Quests safely: {exception}");
            }
        }

        if (weeklyCount == 0 &&
            Plugin.Config.RegenerateWeeklyQuests.Value &&
            weeklyManager != null &&
            craftables?.WeeklyQuests?.IsActive() == true)
        {
            try
            {
                weeklyManager.RegenerateWeeklies();
                ProgressionLog.Info("Generated a new set of Weekly Quests.");
                regenerated = true;
            }
            catch (Exception exception)
            {
                Plugin.Logger.Error($"Failed to regenerate Weekly Quests safely: {exception}");
            }
        }

        if (regenerated)
        {
            refreshRequired = true;
            nextCheckTime = now + RegenerationSettleSeconds;
        }
    }

    private void ApplyUnlimitedRerolls()
    {
        if (!Plugin.Config.UnlimitedQuestRerolls.Value) return;
        if (dailyReroll != null) dailyReroll.rerollEnabled = true;
        if (weeklyReroll != null) weeklyReroll.rerollEnabled = true;
    }

    private void ApplyPortalCooldownReset()
    {
        if (!Plugin.Config.ResetPortalCooldown.Value || portalButton == null) return;
        if (portalButton.currentCd > 0d) portalButton.currentCd = 0d;
    }

    private void ResolveObjects()
    {
        questsList ??= FindLoadedSceneComponent<QuestsList>();
        dailyManager ??= DailyQuestsManager.instance;
        weeklyManager ??= WeeklyQuestsManager.instance;
        dailyReroll ??= DailyQuestReroll.instance;
        weeklyReroll ??= WeeklyQuestReroll.instance;
        portalButton ??= PortalButton.instance;
    }

    private void LogMissing()
    {
        if (missingLogged) return;

        ProgressionLog.Debug(
            $"Quest automation objects unavailable. QuestList={questsList != null}, " +
            $"DailyManager={dailyManager != null}, WeeklyManager={weeklyManager != null}.");
        missingLogged = true;
    }

    private static T FindLoadedSceneComponent<T>() where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component == null || component.gameObject == null) continue;
            var scene = component.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded) return component;
        }

        return null;
    }

    internal void Reset()
    {
        questsList = null;
        dailyManager = null;
        weeklyManager = null;
        dailyReroll = null;
        weeklyReroll = null;
        portalButton = null;
        nextCheckTime = 0f;
        missingLogged = false;
        refreshRequired = true;
        pendingClaimInstanceId = 0;
    }
}
