using AutoProgression.Materials;
using Il2Cpp;
using UnityEngine;
using AutoProgression.Diagnostics;

namespace AutoProgression.Craftables;

internal sealed class RagePillService
{
    private const string RagePillName = "craftable_item_rage_pill";

    private readonly MaterialPurchaseService materials = new();
    private TemporaryCraftableItem ragePill;
    private RageModeManager rageManager;
    private float nextCheckTime;
    private bool missingObjectsLogged;

    internal bool Tick(float now)
    {
        var config = Plugin.Config;
        if (!config.EnableRagePill.Value) return false;

        if (!ResolveObjects())
        {
            if (!missingObjectsLogged)
            {
                ProgressionLog.Debug(
                    $"Rage Pill objects unavailable. RageManager={rageManager != null}, RagePill={ragePill != null}.");
                missingObjectsLogged = true;
            }
            return false;
        }
        missingObjectsLogged = false;

        if (now < nextCheckTime) return false;

        float interval = Configuration.AutoProgressionConfig.RagePillMinimumIntervalSeconds;
        nextCheckTime = now + interval;

        if (rageManager.currentCd <= 0d ||
            !ragePill.ExtraCondition())
        {
            return false;
        }

        if (ragePill.HowManyCanCraft() <= 0)
        {
            if (!config.BuyMissingMaterialsWithJewels.Value) return false;

            BuyMissingRequirements();

            // Material purchases are synchronous in the current game API, but always
            // re-check the recipe instead of assuming any purchase succeeded.
            if (ragePill.HowManyCanCraft() <= 0) return false;
        }

        ragePill.Craft();
        ProgressionLog.Debug("Rage Pill crafted to refresh Rage cooldown.");
        return true;
    }

    private void BuyMissingRequirements()
    {
        var requirements = ragePill.GetRequirements();
        if (requirements == null)
        {
            ProgressionLog.Debug(
                "Rage Pill requirements are temporarily unavailable.");
            return;
        }

        int percent = Plugin.Config.MaterialPurchasePercent.Value;
        foreach (MaterialRequirement requirement in requirements)
        {
            Drop material = requirement?.material;
            if (material == null || material.amount >= requirement.amount)
                continue;

            materials.Buy(material, percent);
        }
    }

    private bool ResolveObjects()
    {
        rageManager ??= RageModeManager.instance;
        if (ragePill == null)
        {
            foreach (TemporaryCraftableItem item in Resources.FindObjectsOfTypeAll<TemporaryCraftableItem>())
            {
                if (item != null && item.name == RagePillName)
                {
                    ragePill = item;
                    break;
                }
            }
        }

        return rageManager != null && ragePill != null;
    }

    internal void Reset()
    {
        ragePill = null;
        rageManager = null;
        nextCheckTime = 0f;
        missingObjectsLogged = false;
    }
}
