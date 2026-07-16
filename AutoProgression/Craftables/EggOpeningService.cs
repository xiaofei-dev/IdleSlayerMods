using System;
using AutoProgression.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Craftables;

internal sealed class EggOpeningService
{
    private const float CheckIntervalSeconds = 1f;

    private TemporaryCraftableItem dragonEggOpener;
    private TemporaryCraftableItem simurghEggOpener;
    private float nextCheckTime;
    private bool missingLogged;

    internal void Tick(float now)
    {
        if (now < nextCheckTime) return;
        nextCheckTime = now + CheckIntervalSeconds;

        LootBoxManager lootBoxes = LootBoxManager.instance;
        if (lootBoxes != null && lootBoxes.IsBeingOpened()) return;

        Drop dragonEgg = Drops.list?.DragonEgg;
        Drop dragonScale = Drops.list?.DragonScale;
        Drop simurghEgg = Drops.list?.SimurghEgg;
        if (dragonEgg == null || dragonScale == null || simurghEgg == null)
        {
            LogMissing(dragonEgg, dragonScale, simurghEgg);
            return;
        }

        dragonEggOpener ??= FindSingleEggOpener(dragonEgg);
        simurghEggOpener ??= FindSingleEggOpener(simurghEgg);
        if (dragonEggOpener == null || simurghEggOpener == null)
        {
            LogMissing(dragonEgg, dragonScale, simurghEgg);
            return;
        }

        missingLogged = false;

        if (TryOpen(
                simurghEgg,
                simurghEggOpener,
                Math.Max(0, Plugin.Config.SimurghEggReserveAmount.Value),
                "Simurgh Egg"))
            return;

        if (dragonScale.amount < dragonScale.GetMaxAmount())
        {
            TryOpen(
                dragonEgg,
                dragonEggOpener,
                Math.Max(0, Plugin.Config.DragonEggReserveAmount.Value),
                "Dragon Egg");
        }
    }

    private static bool TryOpen(
        Drop egg,
        TemporaryCraftableItem opener,
        int reserveAmount,
        string displayName)
    {
        if (egg.amount <= reserveAmount ||
            !opener.TabVisible() ||
            !opener.ExtraCondition() ||
            opener.HowManyCanCraft() <= 0)
            return false;

        LootBox lootBox = opener.lootBoxOpen;
        if (lootBox == null) return false;

        double before = egg.amount;
        LootBoxDropWithChance reward;
        try
        {
            // Preserve the game's normal crafting cost and statistics while
            // bypassing LootBoxManager, which owns the slow modal animation.
            opener.lootBoxOpen = null;
            opener.Craft();
            if (egg.amount >= before) return false;

            reward = lootBox.Open();
        }
        finally
        {
            opener.lootBoxOpen = lootBox;
        }

        ProgressionLog.Debug(
            $"Opened one {displayName} in the background; remaining={egg.amount:0.##}, " +
            $"reserve={reserveAmount}, reward={reward?.rewardString ?? "unknown"}.");
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
        missingLogged = false;
    }
}
