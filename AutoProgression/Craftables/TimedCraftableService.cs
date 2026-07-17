using AutoProgression.Diagnostics;
using AutoProgression.Materials;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace AutoProgression.Craftables;

internal sealed class TimedCraftableService
{
    private const float CheckIntervalSeconds = 1f;

    private readonly MaterialPurchaseService materials = new();
    private readonly Entry[] entries;
    private float nextCheckTime;

    internal TimedCraftableService()
    {
        entries = new[]
        {
            new Entry("craftable_item_whetstone", "Whetstone", () => Plugin.Config.EnableWhetstone),
            new Entry("craftable_item_alternate_dimension_staff", "Alternate Dimension Staff", () => Plugin.Config.EnableAlternateDimensionStaff),
            new Entry("craftable_item_bidimensional_staff", "Bidimensional Staff", () => Plugin.Config.EnableBidimensionalStaff)
        };
    }

    internal bool Tick(float now)
    {
        if (now < nextCheckTime) return false;
        nextCheckTime = now + CheckIntervalSeconds;
        float refillAtMinutes = Mathf.Max(0f, Plugin.Config.TimedCraftablesRefillAtMinutes.Value);
        float targetMinutes = Mathf.Max(
            refillAtMinutes,
            Mathf.Max(0f, Plugin.Config.TimedCraftablesTargetMinutes.Value));
        double refillAtSeconds = refillAtMinutes * 60d;
        double targetSeconds = targetMinutes * 60d;

        foreach (Entry entry in entries)
        {
            if (!entry.Enabled().Value)
            {
                entry.Refilling = false;
                continue;
            }
            entry.Item ??= FindItem(entry.InternalName);
            if (entry.Item == null)
            {
                if (!entry.MissingLogged)
                {
                    ProgressionLog.Debug($"Craftable object unavailable: {entry.InternalName}.");
                    entry.MissingLogged = true;
                }
                continue;
            }
            entry.MissingLogged = false;

            if (!entry.Item.TabVisible() || !entry.Item.ExtraCondition()) continue;

            // Enter refill mode at the lower threshold, then keep stacking even
            // after crossing it until the upper target has been reached.
            if (!entry.Refilling && entry.Item.currentTime <= refillAtSeconds)
                entry.Refilling = true;

            if (!entry.Refilling) continue;
            if (entry.Item.currentTime >= targetSeconds)
            {
                entry.Refilling = false;
                continue;
            }

            if (entry.Item.HowManyCanCraft() <= 0 && Plugin.Config.BuyMissingMaterialsWithJewels.Value)
                BuyMissingRequirements(entry.Item);

            if (entry.Item.HowManyCanCraft() <= 0) continue;

            double previousRemaining = entry.Item.currentTime;
            entry.Item.Craft();
            ProgressionLog.Debug($"{entry.DisplayName} crafted at {previousRemaining:0.0}s remaining.");
            return true;
        }

        return false;
    }

    private void BuyMissingRequirements(TemporaryCraftableItem item)
    {
        int percent = Plugin.Config.MaterialPurchasePercent.Value;
        var requirements = item.GetRequirements();
        if (requirements == null)
        {
            ProgressionLog.Debug($"Craftable requirements unavailable: {item.name}.");
            return;
        }

        foreach (MaterialRequirement requirement in requirements)
        {
            if (requirement == null || requirement.material == null) continue;
            if (requirement.material.amount >= requirement.amount) continue;
            materials.Buy(requirement.material, percent);
        }
    }

    private static TemporaryCraftableItem FindItem(string internalName)
    {
        foreach (TemporaryCraftableItem item in Resources.FindObjectsOfTypeAll<TemporaryCraftableItem>())
        {
            if (item != null && item.name == internalName) return item;
        }

        return null;
    }

    internal void Reset()
    {
        nextCheckTime = 0f;
        foreach (Entry entry in entries)
        {
            entry.Item = null;
            entry.MissingLogged = false;
            entry.Refilling = false;
        }
    }

    private sealed class Entry
    {
        internal readonly string InternalName;
        internal readonly string DisplayName;
        internal readonly System.Func<MelonPreferences_Entry<bool>> Enabled;
        internal TemporaryCraftableItem Item;
        internal bool MissingLogged;
        internal bool Refilling;

        internal Entry(string internalName, string displayName, System.Func<MelonPreferences_Entry<bool>> enabled)
        {
            InternalName = internalName;
            DisplayName = displayName;
            Enabled = enabled;
        }
    }
}
