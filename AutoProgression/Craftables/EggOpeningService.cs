using System;
using AutoProgression.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Craftables;

internal sealed class EggOpeningService
{
    private const float CheckIntervalSeconds = 1f;
    private const float SummaryIntervalSeconds = 30f;

    private TemporaryCraftableItem dragonEggOpener;
    private TemporaryCraftableItem simurghEggOpener;
    private float nextCheckTime;
    private float nextSummaryTime;
    private int dragonEggsOpened;
    private int simurghEggsOpened;
    private bool missingLogged;

    internal bool Tick(float now)
    {
        FlushSummary(now);
        if (now < nextCheckTime) return false;
        nextCheckTime = now + CheckIntervalSeconds;

        LootBoxManager lootBoxes = LootBoxManager.instance;
        if (lootBoxes != null && lootBoxes.IsBeingOpened()) return false;

        Drop dragonEgg = Drops.list?.DragonEgg;
        Drop dragonScale = Drops.list?.DragonScale;
        Drop simurghEgg = Drops.list?.SimurghEgg;
        if (dragonEgg == null || dragonScale == null || simurghEgg == null)
        {
            LogMissing(dragonEgg, dragonScale, simurghEgg);
            return false;
        }

        dragonEggOpener ??= FindSingleEggOpener(dragonEgg);
        simurghEggOpener ??= FindSingleEggOpener(simurghEgg);
        if (dragonEggOpener == null || simurghEggOpener == null)
        {
            LogMissing(dragonEgg, dragonScale, simurghEgg);
            return false;
        }

        missingLogged = false;

        if (TryOpen(
                simurghEgg,
                simurghEggOpener,
                Math.Max(0, Plugin.Config.SimurghEggReserveAmount.Value)))
        {
            RecordOpen(now, false);
            return true;
        }

        if (dragonScale.amount < dragonScale.GetMaxAmount())
        {
            bool opened = TryOpen(
                dragonEgg,
                dragonEggOpener,
                Math.Max(0, Plugin.Config.DragonEggReserveAmount.Value));
            if (opened) RecordOpen(now, true);
            return opened;
        }

        return false;
    }

    private static bool TryOpen(
        Drop egg,
        TemporaryCraftableItem opener,
        int reserveAmount)
    {
        if (egg.amount <= reserveAmount ||
            !opener.TabVisible() ||
            !opener.ExtraCondition() ||
            opener.HowManyCanCraft() <= 0)
            return false;

        LootBox lootBox = opener.lootBoxOpen;
        if (lootBox == null) return false;

        double before = egg.amount;
        try
        {
            // Preserve the game's normal crafting cost and statistics while
            // bypassing LootBoxManager, which owns the slow modal animation.
            opener.lootBoxOpen = null;
            opener.Craft();
            if (egg.amount >= before) return false;

            lootBox.Open();
        }
        finally
        {
            opener.lootBoxOpen = lootBox;
        }

        return true;
    }

    private static TemporaryCraftableItem FindSingleEggOpener(Drop egg)
    {
        foreach (TemporaryCraftableItem candidate in
                 Resources.FindObjectsOfTypeAll<TemporaryCraftableItem>())
        {
            if (candidate == null) continue;

            var requirements = candidate.GetRequirements();
            if (requirements == null) continue;

            MaterialRequirement matched = null;
            int requirementCount = 0;
            foreach (MaterialRequirement requirement in requirements)
            {
                if (requirement == null || requirement.material == null) continue;
                requirementCount++;
                matched = requirement;
            }

            if (requirementCount != 1 || matched == null) continue;
            if (matched.amount != 1d) continue;
            if (matched.material == egg ||
                string.Equals(matched.material.name, egg.name, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private void LogMissing(Drop dragonEgg, Drop dragonScale, Drop simurghEgg)
    {
        if (missingLogged) return;

        ProgressionLog.Debug(
            $"Egg opening objects unavailable. DragonEgg={dragonEgg != null}, DragonScale={dragonScale != null}, " +
            $"DragonOpener={dragonEggOpener != null}, SimurghEgg={simurghEgg != null}, " +
            $"SimurghOpener={simurghEggOpener != null}.");
        missingLogged = true;
    }

    internal void Reset()
    {
        dragonEggOpener = null;
        simurghEggOpener = null;
        nextCheckTime = 0f;
        nextSummaryTime = 0f;
        dragonEggsOpened = 0;
        simurghEggsOpened = 0;
        missingLogged = false;
    }

    private void RecordOpen(float now, bool dragon)
    {
        if (dragon) dragonEggsOpened++;
        else simurghEggsOpened++;
        if (nextSummaryTime <= 0f)
            nextSummaryTime = now + SummaryIntervalSeconds;
    }

    private void FlushSummary(float now)
    {
        if (nextSummaryTime <= 0f || now < nextSummaryTime)
            return;

        ProgressionLog.Debug(
            $"Eggs opened in the last {SummaryIntervalSeconds:0} seconds: " +
            $"Dragon={dragonEggsOpened}, Simurgh={simurghEggsOpened}.",
            "Eggs");
        dragonEggsOpened = 0;
        simurghEggsOpened = 0;
        nextSummaryTime = 0f;
    }
}
