using System;
using AutoProgression.Diagnostics;
using AutoProgression.Materials;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Craftables;

internal sealed class DragonScaleOverflowService
{
    private const float CheckIntervalSeconds = 1f;
    private const float DiagnosticIntervalSeconds = 60f;

    private readonly MaterialPurchaseService materials = new();
    private readonly Entry[] entries =
    {
        new("craftable_item_random_box_staff", "Random Box Staff", "randomboxstaff"),
        new("craftable_item_necklace_of_collectables", "Necklace of Collectables", "necklaceofcollectables"),
        new("craftable_item_cps_compass", "CpS Compass", "cpscompass"),
        new("craftable_item_souls_compass", "Souls Compass", "soulscompass")
    };

    private float nextCheckTime;
    private float nextDiagnosticTime;
    private bool overflowCycleActive;
    private bool dragonScaleMissingLogged;

    internal bool Tick(float now)
    {
        if (!Plugin.Config.EnableDragonScaleOverflowCraftables.Value ||
            now < nextCheckTime)
            return false;

        nextCheckTime = now + CheckIntervalSeconds;
        Drop dragonScale = Drops.list?.DragonScale;
        if (dragonScale == null)
        {
            if (!dragonScaleMissingLogged)
            {
                ProgressionLog.Debug("Dragon Scale overflow material is unavailable.");
                dragonScaleMissingLogged = true;
            }
            return false;
        }

        dragonScaleMissingLogged = false;
        double maximum = dragonScale.GetMaxAmount();
        if (maximum <= 0d) return false;

        double threshold = Math.Clamp(
            Plugin.Config.DragonScaleOverflowThresholdPercent.Value,
            0f,
            100f) / 100d;
        double ratio = dragonScale.amount / maximum;
        double targetSeconds = Math.Max(
            0f,
            Plugin.Config.TimedCraftablesTargetMinutes.Value) * 60d;

        if (now >= nextDiagnosticTime)
        {
            nextDiagnosticTime = now + DiagnosticIntervalSeconds;
            ProgressionLog.Debug(
                $"Dragon Scale overflow status: Amount={dragonScale.amount:0.##}, " +
                $"Max={maximum:0.##}, Ratio={ratio:P2}, Threshold={threshold:P2}, " +
                $"Target={targetSeconds:0.0}s, CycleActive={overflowCycleActive}.");
        }

        // A cycle starts only above the threshold and resets after storage
        // returns to or below it. Each entry can therefore craft at most once
        // per overflow event regardless of its remaining duration.
        if (ratio <= threshold)
        {
            ResetCycle();
            return false;
        }

        overflowCycleActive = true;
        if (AllAvailableEntriesCrafted(targetSeconds))
            ResetCycle();

        overflowCycleActive = true;
        foreach (Entry entry in entries)
        {
            if (entry.CraftedThisCycle) continue;

            entry.Item ??= FindItem(entry);
            if (entry.Item == null)
            {
                if (!entry.MissingLogged)
                {
                    ProgressionLog.Debug(
                        $"Dragon Scale overflow craftable unavailable: {entry.InternalName}.");
                    entry.MissingLogged = true;
                }
                continue;
            }

            entry.MissingLogged = false;
            if (!entry.Item.TabVisible() || !entry.Item.ExtraCondition())
                continue;
            if (entry.Item.currentTime >= targetSeconds)
            {
                entry.CraftedThisCycle = true;
                continue;
            }

            if (entry.Item.HowManyCanCraft() <= 0 &&
                Plugin.Config.BuyMissingMaterialsWithJewels.Value)
                BuyMissingRequirements(entry.Item, dragonScale);

            if (entry.Item.HowManyCanCraft() <= 0)
                continue;

            double before = dragonScale.amount;
            entry.Item.Craft();
            if (dragonScale.amount >= before)
                continue;

            entry.CraftedThisCycle = true;
            ProgressionLog.User(
                $"Dragon Scale overflow: crafted {entry.DisplayName}; " +
                $"Dragon Scale={dragonScale.amount:0.##}/{maximum:0.##} " +
                $"({dragonScale.amount / maximum:P1}).");
            return true;
        }

        return false;
    }

    private bool AllAvailableEntriesCrafted(double targetSeconds)
    {
        bool foundAvailable = false;
        foreach (Entry entry in entries)
        {
            entry.Item ??= FindItem(entry);
            if (entry.Item == null ||
                !entry.Item.TabVisible() ||
                !entry.Item.ExtraCondition())
                continue;

            foundAvailable = true;
            if (!entry.CraftedThisCycle &&
                entry.Item.currentTime < targetSeconds)
                return false;
        }

        return foundAvailable;
    }

    private void BuyMissingRequirements(
        TemporaryCraftableItem item,
        Drop dragonScale)
    {
        var requirements = item.GetRequirements();
        if (requirements == null) return;

        int percent = Plugin.Config.MaterialPurchasePercent.Value;
        foreach (MaterialRequirement requirement in requirements)
        {
            if (requirement == null || requirement.material == null) continue;
            if (ReferenceEquals(requirement.material, dragonScale) ||
                requirement.material.name == dragonScale.name)
                continue;
            if (requirement.material.amount >= requirement.amount) continue;
            materials.Buy(requirement.material, percent);
        }
    }

    private static TemporaryCraftableItem FindItem(Entry entry)
    {
        foreach (TemporaryCraftableItem item in
                 Resources.FindObjectsOfTypeAll<TemporaryCraftableItem>())
        {
            if (item == null) continue;
            if (item.name == entry.InternalName ||
                Normalize(item.name).Contains(entry.NormalizedName))
                return item;
        }

        return null;
    }

    private static string Normalize(string value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();

    private void ResetCycle()
    {
        overflowCycleActive = false;
        foreach (Entry entry in entries)
            entry.CraftedThisCycle = false;
    }

    internal void Reset()
    {
        nextCheckTime = 0f;
        nextDiagnosticTime = 0f;
        dragonScaleMissingLogged = false;
        foreach (Entry entry in entries)
        {
            entry.Item = null;
            entry.MissingLogged = false;
        }
    }

    private sealed class Entry
    {
        internal readonly string InternalName;
        internal readonly string DisplayName;
        internal readonly string NormalizedName;
        internal TemporaryCraftableItem Item;
        internal bool MissingLogged;
        internal bool CraftedThisCycle;

        internal Entry(
            string internalName,
            string displayName,
            string normalizedName)
        {
            InternalName = internalName;
            DisplayName = displayName;
            NormalizedName = normalizedName;
        }
    }
}
