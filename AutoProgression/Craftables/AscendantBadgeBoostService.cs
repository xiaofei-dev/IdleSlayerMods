using System;
using AutoProgression.Diagnostics;
using AutoProgression.Materials;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Craftables;

internal sealed class AscendantBadgeBoostService
{
    private const float CheckIntervalSeconds = 1f;
    private const double DragonScaleThreshold = 0.5d;

    private readonly MaterialPurchaseService materials = new();
    private TemporaryCraftableItem item;
    private float nextCheckTime;
    private bool missingLogged;

    internal bool Tick(float now)
    {
        if (!Plugin.Config.EnableAscendantBadgeBoost.Value)
            return false;

        if (now < nextCheckTime) return false;
        nextCheckTime = now + CheckIntervalSeconds;

        Drop dragonScale = Drops.list?.DragonScale;
        item ??= FindItem();
        if (dragonScale == null || item == null)
        {
            if (!missingLogged)
            {
                ProgressionLog.Debug(
                    $"Ascendant Badge Boost objects unavailable. " +
                    $"DragonScale={dragonScale != null}, Item={item != null}.");
                missingLogged = true;
            }
            return false;
        }

        missingLogged = false;
        double maximum = dragonScale.GetMaxAmount();
        if (maximum <= 0d || dragonScale.amount / maximum <= DragonScaleThreshold)
            return false;

        try
        {
            // ExtraCondition is the game's authoritative one-use state. Once
            // the boost is armed, it stays false until the effect is spent.
            if (!item.TabVisible() || !item.ExtraCondition()) return false;

            if (item.HowManyCanCraft() <= 0d &&
                Plugin.Config.BuyMissingMaterialsWithJewels.Value)
                BuyMissingRequirements(dragonScale);

            if (item.HowManyCanCraft() <= 0d) return false;

            double before = dragonScale.amount;
            item.Craft();
            if (dragonScale.amount >= before) return false;

            ProgressionLog.Debug(
                $"Ascendant Badge Boost crafted; Dragon Scale=" +
                $"{dragonScale.amount:0.##}/{maximum:0.##} " +
                $"({dragonScale.amount / maximum:P1}).",
                "Craftables");
            return true;
        }
        catch (Exception exception)
        {
            ProgressionLog.Exception(
                "Ascendant Badge Boost crafting",
                exception);
            item = null;
            return false;
        }
    }

    private void BuyMissingRequirements(Drop dragonScale)
    {
        var requirements = item.GetRequirements();
        if (requirements == null) return;

        int percent = Plugin.Config.MaterialPurchasePercent.Value;
        foreach (MaterialRequirement requirement in requirements)
        {
            if (requirement?.material == null) continue;
            if (ReferenceEquals(requirement.material, dragonScale) ||
                string.Equals(requirement.material.name, dragonScale.name,
                    StringComparison.OrdinalIgnoreCase))
                continue;
            if (requirement.material.amount >= requirement.amount) continue;
            materials.Buy(requirement.material, percent);
        }
    }

    private static TemporaryCraftableItem FindItem()
    {
        foreach (TemporaryCraftableItem candidate in
                 Resources.FindObjectsOfTypeAll<TemporaryCraftableItem>())
        {
            if (candidate == null) continue;
            if (Normalize(candidate.name).Contains("ascendantbadgeboost"))
                return candidate;
        }

        return null;
    }

    private static string Normalize(string value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();

    internal void Reset()
    {
        item = null;
        nextCheckTime = 0f;
        missingLogged = false;
    }
}
