using AutoProgression.PaidBonuses;
using AutoProgression.Craftables;
using AutoProgression.Diagnostics;
using AutoProgression.Purchases;
using AutoProgression.Ascension;
using UnityEngine;

namespace AutoProgression.Runtime;

public sealed class AutoProgressionRuntime : MonoBehaviour
{
    private const float MainScreenStableSeconds = 3f;

    private readonly MainScreenGuard mainScreen = new();
    private readonly AscensionMonitor ascension = new();
    private readonly AutomaticAscensionService automaticAscension = new();
    private readonly PaidBonusService paidBonuses = new();
    private readonly RagePillService ragePill = new();
    private readonly TimedCraftableService timedCraftables = new();
    private readonly ShardsNecklaceService shardsNecklace = new();
    private readonly SkillPurchaseService skillPurchases = new();
    private readonly BlockedSkillService blockedSkills = new();
    private readonly EquipmentPurchaseService equipmentPurchases = new();
    private bool autoProgressionEnabled;
    private bool wasReady;
    private bool readyLogged;

    public void Update()
    {
        blockedSkills.Tick();

        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleAutoProgression();
        }

        if (!autoProgressionEnabled)
        {
            return;
        }

        bool ready = mainScreen.IsReady(MainScreenStableSeconds);
        if (!ready)
        {
            automaticAscension.NotifyMainScreenUnavailable();
            if (wasReady)
            {
                ascension.Reset();
                paidBonuses.Reset();
                ragePill.Reset();
                timedCraftables.Reset();
                shardsNecklace.Reset();
                skillPurchases.Reset();
                equipmentPurchases.Reset();
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
            skillPurchases.Reset();
            equipmentPurchases.Reset();
            ProgressionLog.Info("Ascension detected; purchase timers and equipment sleep were reset.");
        }

        if (automaticAscension.Tick(Time.unscaledTime, ascensionResetDetected))
            return;

        paidBonuses.Tick(Time.unscaledTime);
        ragePill.Tick(Time.unscaledTime);
        timedCraftables.Tick(Time.unscaledTime);
        shardsNecklace.Tick(Time.unscaledTime);
        TickPurchases(Time.unscaledTime);
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
        {
            skillPurchases.Reset();
            skillPurchases.Tick(now);
        }
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
        paidBonuses.Reset();
        ragePill.Reset();
        timedCraftables.Reset();
        shardsNecklace.Reset();
        skillPurchases.Reset();
        equipmentPurchases.Reset();
        blockedSkills.Reset();
        wasReady = false;
        readyLogged = false;
    }

    public void OnDisable()
    {
        ResetRuntimeState();
    }
}
