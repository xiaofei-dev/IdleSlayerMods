using System;
using AutoProgression.Diagnostics;
using AutoProgression.Materials;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Craftables;

internal sealed class KeyManifestOverflowService
{
    private const float CheckIntervalSeconds = 1f;
    private readonly MaterialPurchaseService materials = new();
    private TemporaryCraftableItem item;
    private float nextCheckTime;
    private bool missingLogged;

    internal bool Tick(float now)
    {
        if (!Plugin.Config.EnableQuestAssistCraftables.Value ||
            Plugin.Config.QuestAssistFeatherThresholdAmount.Value <= 0)
            return false;

        if (now < nextCheckTime) return false;
        nextCheckTime = now + CheckIntervalSeconds;

        Drop feather = Drops.list?.SimurghFeather;
        item ??= FindItem();
        if (feather == null || item == null)
        {
            if (!missingLogged)
            {
                ProgressionLog.Debug(
                    $"Key Manifest overflow objects unavailable. " +
                    $"SimurghFeather={feather != null}, Item={item != null}.");
                missingLogged = true;
            }
            return false;
        }

        missingLogged = false;
        int threshold =
            Plugin.Config.QuestAssistFeatherThresholdAmount.Value;
        if (feather.amount <= threshold) return false;

        try
        {
            // Native availability is the only repetition gate for this path.
            if (!item.TabVisible() || !item.ExtraCondition()) return false;

            if (item.HowManyCanCraft() <= 0d &&
                Plugin.Config.BuyMissingMaterialsWithJewels.Value)
                BuyMissingRequirements(feather);

            if (item.HowManyCanCraft() <= 0d ||
                !PreservesFeatherThreshold(feather, threshold))
                return false;

            double before = feather.amount;
            item.Craft();
            if (feather.amount >= before) return false;

            ProgressionLog.Debug(
                $"Simurgh Feather overflow: crafted Key Manifest; " +
                $"remaining={feather.amount:0.##}, threshold={threshold}.",
                "Craftables");
            return true;
        }
        catch (Exception exception)
        {
            ProgressionLog.Exception(
                "Key Manifest overflow crafting",
                exception);
            item = null;
            return false;
        }
    }

    private bool PreservesFeatherThreshold(Drop feather, int threshold)
    {
        var requirements = item.GetRequirements();
        if (requirements == null) return false;

        double featherCost = 0d;
        foreach (MaterialRequirement requirement in requirements)
        {
            Drop material = requirement?.material;
            if (material != null &&
                (ReferenceEquals(material, feather) ||
                 string.Equals(material.name, feather.name,
                     StringComparison.OrdinalIgnoreCase)))
                featherCost += requirement.amount;
        }

        return feather.amount > threshold &&
               feather.amount - featherCost >= threshold;
    }

    private void BuyMissingRequirements(Drop feather)
    {
        var requirements = item.GetRequirements();
        if (requirements == null) return;

        int percent = Plugin.Config.MaterialPurchasePercent.Value;
        foreach (MaterialRequirement requirement in requirements)
        {
            Drop material = requirement?.material;
            if (material == null) continue;
            if (ReferenceEquals(material, feather) ||
                string.Equals(material.name, feather.name,
                    StringComparison.OrdinalIgnoreCase))
                continue;
            if (material.amount >= requirement.amount) continue;
            materials.Buy(material, percent);
        }
    }

    private static TemporaryCraftableItem FindItem()
    {
        foreach (TemporaryCraftableItem candidate in
                 Resources.FindObjectsOfTypeAll<TemporaryCraftableItem>())
        {
            if (candidate == null) continue;
            if (Normalize(candidate.name).Contains("keymanifest"))
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
