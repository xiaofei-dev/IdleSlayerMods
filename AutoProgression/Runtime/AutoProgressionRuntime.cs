using AutoProgression.PaidBonuses;
using AutoProgression.Craftables;
using AutoProgression.Diagnostics;
using AutoProgression.Purchases;
using AutoProgression.Ascension;
using AutoProgression.Quests;
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
    private readonly RagePillService ragePill = new();
    private readonly TimedCraftableService timedCraftables = new();
    private readonly ShardsNecklaceService shardsNecklace = new();
    private readonly EggOpeningService eggOpening = new();
    private readonly QuestAutomationService quests = new();
    private readonly WeeklyRageQuestService weeklyRageQuests = new();
    private readonly SkillPurchaseService skillPurchases = new();
    private readonly BlockedSkillService blockedSkills = new();
    private readonly EquipmentPurchaseService equipmentPurchases = new();
    private bool autoProgressionEnabled;
    private bool wasReady;
    private bool readyLogged;
    private bool pendingAscensionReset;
    private float ascensionLockUntil;
    private float itemActionsReadyAt;
    private float nextItemActionTime;

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleAutoProgression();
        }

        // These configuration-backed protections are intentionally
        // independent from the T automation toggle.
        blockedSkills.Tick();
        quests.TickPersistent();

        if (!autoProgressionEnabled)
        {
            return;
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
        TickItemActions(now);

        // Do not let normal quest maintenance refresh the same native list
        // while a generated Weekly Quest is being rerolled.
        if (!weeklyRageQuests.IsProcessing)
            quests.Tick(now);

        if (weeklyRageQuests.Tick())
            quests.Reset();
        TickPurchases(now);
    }

    private void TickItemActions(float now)
    {
        // Rage Pill is time-sensitive and uses its own configured check
        // interval, so it is not held behind the startup delay. A successful
        // Rage Pill action still occupies the shared one-second action slot.
        if (ragePill.Tick(now))
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

        bool acted = timedCraftables.Tick(now) ||
                     shardsNecklace.Tick(now) ||
                     eggOpening.Tick(now);

        if (acted)
            nextItemActionTime = now + ItemActionIntervalSeconds;
    }

    private void BeginAscensionLock(float now, string reason)
    {
        ascensionLockUntil = now + PostAscensionLockSeconds;
        ascension.Reset();
        ResetOperationalServices(discardWeeklyQuestWork: true);
        ProgressionLog.Info(
            $"{reason}; other automation paused for {PostAscensionLockSeconds:0.#} seconds and cached objects were cleared.");
    }

    private void ResetOperationalServices(bool discardWeeklyQuestWork)
    {
        paidBonuses.Reset();
        ragePill.Reset();
        timedCraftables.Reset();
        shardsNecklace.Reset();
        eggOpening.Reset();
        quests.Reset();
        if (discardWeeklyQuestWork)
            weeklyRageQuests.Reset();
        skillPurchases.Reset();
        equipmentPurchases.Reset();
        itemActionsReadyAt = 0f;
        nextItemActionTime = 0f;
    }

    private void TickPurchases(float now)
    {
        bool equipmentFirst = string.Equals(
            Plugin.Config.PurchasePriority.Value,
            "Equipment",
            System.StringComparison.OrdinalIgnoreCase);

        if (equipmentFirst)
        {
            equipmentPurchases.Tick(now);
            skillPurchases.Tick(now);
            return;
        }

        skillPurchases.Tick(now);
        if (equipmentPurchases.Tick(now))
            skillPurchases.Tick(now);
    }

    private void ToggleAutoProgression()
    {
        autoProgressionEnabled = !autoProgressionEnabled;
        ResetRuntimeState();

        string message = autoProgressionEnabled
            ? "AutoProgression Activated!"
            : "AutoProgression Deactivated!";

        ProgressionLog.Info($"T toggle: {message}; GameState={Il2Cpp.GameState.current}.");
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
    }

    public void OnDisable()
    {
        ResetRuntimeState();
    }
}
