using AutoProgression.Materials;
using Il2Cpp;
using UnityEngine;
using AutoProgression.Diagnostics;

namespace AutoProgression.Craftables;

internal sealed class RagePillService
{
    private const string RagePillName = "craftable_item_rage_pill";
    private const string SlimeName = "drop_slime";
    private const string RootName = "drop_root";

    private readonly MaterialPurchaseService materials = new();
    private TemporaryCraftableItem ragePill;
    private RageModeManager rageManager;
    private float nextCheckTime;
    private bool missingObjectsLogged;

    internal void Tick(float now)
    {
        var config = Plugin.Config;
        if (!config.EnableRagePill.Value) return;

        if (!ResolveObjects())
        {
            if (!missingObjectsLogged)
            {
                ProgressionLog.Debug(
                    $"Rage Pill objects unavailable. RageManager={rageManager != null}, RagePill={ragePill != null}.");
                missingObjectsLogged = true;
            }
            return;
        }
        missingObjectsLogged = false;

        if (now < nextCheckTime) return;

        float interval = Mathf.Max(0.5f, config.RagePillMinimumIntervalSeconds.Value);
        nextCheckTime = now + interval;

        if (rageManager.currentCd <= 0d ||
            !ragePill.ExtraCondition())
        {
            return;
        }

        if (ragePill.HowManyCanCraft() <= 0)
        {
            if (!config.BuyMissingMaterialsWithJewels.Value) return;

            int percent = config.MaterialPurchasePercent.Value;
            materials.Buy(SlimeName, percent);
            materials.Buy(RootName, percent);

            // Material purchases are synchronous in the current game API, but always
            // re-check the recipe instead of assuming either purchase succeeded.
            if (ragePill.HowManyCanCraft() <= 0) return;
        }

        ragePill.Craft();
        ProgressionLog.Debug("Rage Pill crafted to refresh Rage cooldown.");
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
        nextCheckTime = 0f;
        missingObjectsLogged = false;
    }
}
