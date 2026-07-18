using System;
using AutoProgression.Diagnostics;
using AutoProgression.Materials;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Craftables;

internal sealed class ShardsNecklaceService
{
    private const float CheckIntervalSeconds = 1f;
    private const float DiagnosticIntervalSeconds = 60f;
    // Crafting refreshes inventory UI state. Keep it to one action per tick so
    // opening the inventory cannot trigger a large synchronous crafting burst.
    private const int MaxCraftsPerTick = 1;

    private readonly MaterialPurchaseService materials = new();
    private TemporaryCraftableItem item;
    private float nextCheckTime;
    private float nextDiagnosticTime;
    private bool missingLogged;

    internal bool Tick(float now)
    {
        if (!Plugin.Config.EnableShardsNecklaceScrapOverflow.Value || now < nextCheckTime) return false;
        nextCheckTime = now + CheckIntervalSeconds;

        Drop scrap = Drops.list?.Scrap;
        item ??= FindItem();
        if (scrap == null || item == null)
        {
            if (!missingLogged)
            {
                ProgressionLog.Debug($"Shards Necklace objects unavailable. Scrap={scrap != null}, Item={item != null}.");
                missingLogged = true;
            }
            return false;
        }

        missingLogged = false;
        bool tabVisible = item.TabVisible();
        bool extraCondition = item.ExtraCondition();

        double maximum = scrap.GetMaxAmount();
        if (maximum <= 0d) return false;

        double threshold = Math.Clamp(
            Plugin.Config.ShardsNecklaceScrapThresholdPercent.Value,
            0f,
            100f) / 100d;
        double targetSeconds = Math.Max(
            0f,
            Plugin.Config.TimedCraftablesTargetMinutes.Value) * 60d;

        if (now >= nextDiagnosticTime)
        {
            nextDiagnosticTime = now + DiagnosticIntervalSeconds;
            ProgressionLog.Debug(
                $"Shards Necklace status: Scrap={scrap.amount:0.##}, Max={maximum:0.##}, " +
                $"Ratio={scrap.amount / maximum:P2}, Threshold={threshold:P2}, " +
                $"TabVisible={tabVisible}, ExtraCondition={extraCondition}, " +
                $"Remaining={item.currentTime:0.0}s, Target={targetSeconds:0.0}s, " +
                $"CanCraft={item.HowManyCanCraft()}.");
        }

        if (!tabVisible || !extraCondition) return false;
        if (scrap.amount / maximum < threshold) return false;
        if (item.currentTime >= targetSeconds) return false;

        int crafted = 0;
        while (crafted < MaxCraftsPerTick &&
               scrap.amount / maximum >= threshold &&
               item.currentTime < targetSeconds)
        {
            if (item.HowManyCanCraft() <= 0 && Plugin.Config.BuyMissingMaterialsWithJewels.Value)
                BuyMissingRequirements();

            if (item.HowManyCanCraft() <= 0) break;

            double previousScrap = scrap.amount;
            item.Craft();
            if (scrap.amount >= previousScrap) break;
            crafted++;
        }

        if (crafted > 0)
        {
            ProgressionLog.User(
                $"Shards Necklace Scrap overflow: crafted {crafted}, Scrap={scrap.amount:0.##}/{maximum:0.##} ({scrap.amount / maximum:P1}).");
            return true;
        }

        return false;
    }

    private void BuyMissingRequirements()
    {
        var requirements = item.GetRequirements();
        if (requirements == null) return;

        int percent = Plugin.Config.MaterialPurchasePercent.Value;
        foreach (MaterialRequirement requirement in requirements)
        {
            if (requirement == null || requirement.material == null) continue;
            if (requirement.material.amount >= requirement.amount) continue;
            materials.Buy(requirement.material, percent);
        }
    }

    private static TemporaryCraftableItem FindItem()
    {
        foreach (TemporaryCraftableItem candidate in Resources.FindObjectsOfTypeAll<TemporaryCraftableItem>())
        {
            if (candidate == null) continue;
            string normalized = Normalize(candidate.name);
            if (normalized.Contains("shardsnecklace")) return candidate;
        }

        return null;
    }

    private static string Normalize(string value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();

    internal void Reset()
    {
        item = null;
        nextCheckTime = 0f;
        nextDiagnosticTime = 0f;
        missingLogged = false;
    }
}
