using System;
using System.Collections.Generic;
using AutoProgression.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Quests;

internal sealed class QuestAutomationService
{
    private const float CheckIntervalSeconds = 2f;
    private const float RegenerationSettleSeconds = 5f;

    private QuestsList questsList;
    private DailyQuestsManager dailyManager;
    private WeeklyQuestsManager weeklyManager;
    private DailyQuestReroll dailyReroll;
    private WeeklyQuestReroll weeklyReroll;
    private PortalButton portalButton;
    private float nextCheckTime;
    private bool missingLogged;

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

        try
        {
            questsList.RefreshList();
        }
        catch (Exception exception)
        {
            Plugin.Logger.Error($"Failed to refresh the quest list safely: {exception}");
            return;
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
            int claimed = ClaimCompleted(snapshot);
            if (claimed > 0)
            {
                ProgressionLog.Info($"Automatically claimed {claimed} completed quest(s).");
                return;
            }
        }

        RegenerateMissingQuestTypes(snapshot, now);
    }

    private static int ClaimCompleted(List<Quest> quests)
    {
        int claimed = 0;
        foreach (Quest quest in quests)
        {
            try
            {
                if (!quest.CanBeClaimed()) continue;
                quest.Claim();
                claimed++;
            }
            catch (Exception exception)
            {
                Plugin.Logger.Error($"Failed to claim a quest safely: {exception}");
            }
        }

        return claimed;
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
            nextCheckTime = now + RegenerationSettleSeconds;
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
    }
}
