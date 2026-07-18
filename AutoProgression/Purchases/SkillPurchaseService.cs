using System;
using System.Collections.Generic;
using AutoProgression.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Purchases;

internal sealed class SkillPurchaseService
{
    private const float PurchaseIntervalSeconds = 5f;
    private const float SummaryIntervalSeconds = 30f;
    private ShopManager shopManager;
    private UpgradesList upgradesList;
    private float nextPurchaseTime;
    private float nextSummaryTime;
    private int purchasedSinceLastSummary;

    internal void Tick(float now)
    {
        if (!Plugin.Config.EnableSkillPurchases.Value) return;

        FlushSummary(now);
        if (now < nextPurchaseTime) return;
        nextPurchaseTime = now + PurchaseIntervalSeconds;

        shopManager ??= ShopManager.instance;
        upgradesList ??= UpgradesList.instance ?? FindUpgradesList();
        if (shopManager == null || upgradesList == null)
            return;

        try
        {
            // The game normally refreshes this cache only when the shop is
            // opened. Refresh it explicitly so ascension needs no UI action.
            upgradesList.RefreshList();
        }
        catch (Exception exception)
        {
            ProgressionLog.Error($"Failed to refresh the skill list safely: {exception}");
        }

        var upgrades = upgradesList.scrollListData;
        if (upgrades == null)
            return;
        List<Upgrade> snapshot = new(upgrades.Count);
        for (int index = 0; index < upgrades.Count; index++)
        {
            Upgrade upgrade = upgrades[index];
            if (upgrade != null) snapshot.Add(upgrade);
        }

        // The native Buy All crashes when disabled upgrades become null slots.
        // Buy every eligible entry in this snapshot to preserve Buy All behavior.
        int purchasedCount = 0;
        foreach (Upgrade upgrade in snapshot)
        {
            if (upgrade == null || upgrade.disabled || upgrade.bought) continue;
            try
            {
                shopManager.BuyUpgrade(upgrade, false, false, true);
                if (upgrade.bought) purchasedCount++;
            }
            catch (Exception exception)
            {
                ProgressionLog.Error($"Failed to purchase skill '{upgrade.name}' safely: {exception}");
            }
        }

        if (purchasedCount > 0)
        {
            purchasedSinceLastSummary += purchasedCount;
            if (nextSummaryTime <= 0f)
                nextSummaryTime = now + SummaryIntervalSeconds;
        }
    }

    private void FlushSummary(float now)
    {
        if (purchasedSinceLastSummary <= 0 || now < nextSummaryTime) return;

        ProgressionLog.Debug(
            $"Skill purchases in the last 30 seconds: {purchasedSinceLastSummary} skill(s).");
        purchasedSinceLastSummary = 0;
        nextSummaryTime = 0f;
    }

    private static UpgradesList FindUpgradesList()
    {
        foreach (UpgradesList candidate in Resources.FindObjectsOfTypeAll<UpgradesList>())
        {
            if (candidate == null) continue;
            var scene = candidate.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded) return candidate;
        }

        return null;
    }

    internal void Reset()
    {
        nextPurchaseTime = 0f;
        nextSummaryTime = 0f;
        purchasedSinceLastSummary = 0;
        shopManager = null;
        upgradesList = null;
    }
}
