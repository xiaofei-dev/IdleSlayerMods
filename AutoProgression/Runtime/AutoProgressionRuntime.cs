using AutoProgression.PaidBonuses;
using AutoProgression.Craftables;
using AutoProgression.Diagnostics;
using AutoProgression.Purchases;
using AutoProgression.Ascension;
using AutoProgression.Quests;
using AutoProgression.Minions;
using AutoProgression.Armory;
using AutoProgression.SilverBoxes;
using UnityEngine;

namespace AutoProgression.Runtime;

public sealed class AutoProgressionRuntime : MonoBehaviour
{
    private const float MainScreenStableSeconds = 3f;
    private const float PostAscensionLockSeconds = 2f;
    private const float ItemStartupDelaySeconds = 2f;
    private const float ItemActionIntervalSeconds = 1f;

    private readonly MainScreenGuard mainScreen = new();
    private readonly AscensionMonitor ascension = new();
    private readonly AutomaticAscensionService automaticAscension = new();
    private readonly PaidBonusService paidBonuses = new();
    private readonly MinionAutomationService minions = new();
    private readonly RagePillService ragePill = new();
    private readonly TimedCraftableService timedCraftables = new();
    private readonly ShardsNecklaceService shardsNecklace = new();
    private readonly DragonScaleOverflowService dragonScaleOverflow = new();
    private readonly QuestAssistCraftableService questAssistCraftables = new();
    private readonly EggOpeningService eggOpening = new();
    private readonly QuestAutomationService quests = new();
    private readonly WeeklyRageQuestService weeklyRageQuests = new();
    private readonly DailyQuestFilterService dailyQuestFilter = new();
    private readonly SkillPurchaseService skillPurchases = new();
    private readonly BlockedSkillService blockedSkills = new();
    private readonly EquipmentPurchaseService equipmentPurchases = new();
    private readonly ArmoryBoxOpeningService armoryBoxes = new();
    private readonly SilverBoxClaimService silverBoxes = new();
    private bool autoProgressionEnabled;
    private bool wasReady;
    private bool readyLogged;
    private bool pendingAscensionReset;
    private float ascensionLockUntil;
    private float itemActionsReadyAt;
    private float nextItemActionTime;
    private bool questAutomationWasEnabled;

    public void Update()
    {
        // Manual Armory-box controls are intentionally independent from T.
        armoryBoxes.Tick();
        silverBoxes.Tick(Time.unscaledTime);

        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleAutoProgression();
        }

        // This configured safety rule must also block manual purchases while
        // periodic automation is paused with T.
        blockedSkills.Tick();

        if (!autoProgressionEnabled)
        {
            WeeklyQuestGenerationBridge.DiscardPending();
            DailyQuestGenerationBridge.DiscardPending();
            return;
        }

        if (Plugin.Config.EnableQuestAutomation.Value)
        {
            quests.TickPersistent();
            questAutomationWasEnabled = true;
        }
        else if (questAutomationWasEnabled)
        {
            quests.StopPersistentAutomation();
            questAutomationWasEnabled = false;
        }

        float now = Time.unscaledTime;
        if (now < ascensionLockUntil)
        {
            if (!mainScreen.IsReady(MainScreenStableSeconds))
                automaticAscension.NotifyMainScreenUnavailable();
            return;
        }

        bool ready = mainScreen.IsReady(MainScreenStableSeconds);
        if (!ready)
        {
            automaticAscension.NotifyMainScreenUnavailable();
            if (wasReady)
            {
                ascension.Reset();
                ResetOperationalServices(discardWeeklyQuestWork: false);
            }
            wasReady = false;
            readyLogged = false;
            return;
        }

        wasReady = true;
        if (!readyLogged)
        {
            ProgressionLog.Debug($"AutoProgression runtime ready. GameState={Il2Cpp.GameState.current}.");
            readyLogged = true;
        }
        bool ascensionResetDetected = ascension.DetectFirstSkillReset();
        if (ascensionResetDetected)
        {
            pendingAscensionReset = true;
            BeginAscensionLock(now, "Ascension detected");
            return;
        }

        bool resetForAutomaticAscension = pendingAscensionReset;
        pendingAscensionReset = false;
        if (automaticAscension.Tick(now, resetForAutomaticAscension))
        {
            if (automaticAscension.StartedAscensionThisTick)
            {
                pendingAscensionReset = true;
                BeginAscensionLock(now, "Automatic ascension started");
            }
            return;
        }

        paidBonuses.Tick(now);
        minions.Tick(now);
        TickItemActions(now);

        if (Plugin.Config.EnableQuestAutomation.Value)
        {
            // Do not let normal quest maintenance refresh the same native list
            // while a generated Weekly Quest is being rerolled.
            if (!weeklyRageQuests.IsProcessing && !dailyQuestFilter.IsProcessing)
                quests.Tick(now);

            if (weeklyRageQuests.Tick())
                quests.Reset();
            if (!weeklyRageQuests.IsProcessing && dailyQuestFilter.Tick())
                quests.Reset();
        }
        else
        {
            WeeklyQuestGenerationBridge.DiscardPending();
            DailyQuestGenerationBridge.DiscardPending();
            weeklyRageQuests.Reset();
            dailyQuestFilter.Reset();
            quests.Reset();
        }
        TickPurchases(now);
    }

    private void TickItemActions(float now)
    {
        // Rage Pill is time-sensitive and uses its own configured check
        // interval, so it is not held behind the startup delay. A successful
        // Rage Pill action still occupies the shared one-second action slot.
        bool craftablesEnabled = Plugin.Config.EnableCraftableAutomation.Value;
        if (craftablesEnabled && ragePill.Tick(now))
        {
            nextItemActionTime = now + ItemActionIntervalSeconds;
            return;
        }

        if (itemActionsReadyAt <= 0f)
        {
            itemActionsReadyAt = now + ItemStartupDelaySeconds;
            return;
        }

        if (now < itemActionsReadyAt || now < nextItemActionTime)
            return;

        bool acted = craftablesEnabled &&
                     (questAssistCraftables.Tick(now) ||
                      timedCraftables.Tick(now) ||
                      shardsNecklace.Tick(now) ||
                      dragonScaleOverflow.Tick(now));

        if (!acted && Plugin.Config.EnableEggOpening.Value)
            acted = eggOpening.Tick(now);

        if (acted)
            nextItemActionTime = now + ItemActionIntervalSeconds;
    }

    private void BeginAscensionLock(float now, string reason)
    {
        ascensionLockUntil = now + PostAscensionLockSeconds;
        ascension.Reset();
        // Normal Ascension does not invalidate active Daily or Weekly Quests.
        // Preserve generated-set work captured immediately before Ascension so
        // it can resume after the global transaction lock.
        ResetOperationalServices(discardWeeklyQuestWork: false);
        ProgressionLog.User(
            $"{reason}; other automation paused for {PostAscensionLockSeconds:0.#} seconds and cached objects were cleared.");
    }

    private void ResetOperationalServices(bool discardWeeklyQuestWork)
    {
        paidBonuses.Reset();
        minions.Reset();
        ragePill.Reset();
        timedCraftables.Reset();
        shardsNecklace.Reset();
        dragonScaleOverflow.Reset();
        questAssistCraftables.Reset();
        eggOpening.Reset();
        quests.Reset();
        if (discardWeeklyQuestWork)
        {
            weeklyRageQuests.Reset();
            dailyQuestFilter.Reset();
        }
        skillPurchases.Reset();
        equipmentPurchases.Reset();
        itemActionsReadyAt = 0f;
        nextItemActionTime = 0f;
    }

    private void TickPurchases(float now)
    {
        skillPurchases.Tick(now);
        if (equipmentPurchases.Tick(now))
            skillPurchases.Tick(now);
    }

    private void ToggleAutoProgression()
    {
        autoProgressionEnabled = !autoProgressionEnabled;
        ResetRuntimeState();
        if (!autoProgressionEnabled)
        {
            quests.StopPersistentAutomation();
            questAutomationWasEnabled = false;
        }

        string message = autoProgressionEnabled
            ? "AutoProgression Activated!"
            : "AutoProgression Deactivated!";

        ProgressionLog.User($"T toggle: {message}; GameState={Il2Cpp.GameState.current}.");
        Plugin.ModHelperInstance?.ShowNotification(message, autoProgressionEnabled);
    }

    private void ResetRuntimeState()
    {
        mainScreen.Reset();
        ascension.Reset();
        automaticAscension.Reset();
        ResetOperationalServices(discardWeeklyQuestWork: true);
        wasReady = false;
        readyLogged = false;
        pendingAscensionReset = false;
        ascensionLockUntil = 0f;
        questAutomationWasEnabled = false;
    }

    public void OnDisable()
    {
        armoryBoxes.Reset();
        silverBoxes.Reset();
        ResetRuntimeState();
    }
}
