using System;
using System.Reflection;
using AutoProgression.Diagnostics;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Minions;

internal sealed class MinionAutomationService
{
    private const float CheckIntervalSeconds = 5f;
    private const int MinimumEligibleMaxLevel = 70;

    private static readonly MethodInfo PrestigeMinionMethod = AccessTools.Method(
        typeof(DivinitiesManager),
        "PrestigeMinion",
        new[] { typeof(Minion) });

    private float nextCheckAt;

    internal void Tick(float now)
    {
        bool claimAndSend = Plugin.Config.EnableMinionClaimAndSend.Value;
        bool autoPrestige = Plugin.Config.EnableAutomaticMinionPrestige.Value;
        if ((!claimAndSend && !autoPrestige) || now < nextCheckAt)
            return;

        nextCheckAt = now + CheckIntervalSeconds;

        PlayerInventory inventory = PlayerInventory.instance;
        if (inventory?.minions == null)
            return;

        bool prestigeAvailable = autoPrestige && IsPrestigeAvailable();
        foreach (Minion minion in inventory.minions)
        {
            if (minion == null || !minion.IsUnlocked())
                continue;

            try
            {
                if (claimAndSend && minion.CanBeClaimed())
                {
                    minion.ClaimQuest(false);
                    ProgressionLog.Debug(
                        $"Minion reward claimed: {GetDisplayName(minion)}; level={minion.level}.");
                }

                if (prestigeAvailable && IsEligibleForPrestige(minion))
                    TryPrestige(minion);

                if (claimAndSend && minion.IsStandingBy())
                {
                    double missionCost = minion.GetCost();
                    if (missionCost < SlayerPoints.pre)
                    {
                        minion.GiveQuest();
                        ProgressionLog.Debug(
                            $"Minion sent: {GetDisplayName(minion)}; cost={missionCost:0.##} SP.");
                    }
                }
            }
            catch (Exception exception)
            {
                ProgressionLog.Exception(
                    $"Minion automation for {GetDisplayName(minion)}",
                    exception);
            }
        }
    }

    internal void Reset() => nextCheckAt = 0f;

    private static bool IsEligibleForPrestige(Minion minion) =>
        minion.IsStandingBy() &&
        minion.level > 1 &&
        minion.maxLevel >= MinimumEligibleMaxLevel;

    private static bool IsPrestigeAvailable()
    {
        DivinitiesManager manager = DivinitiesManager.instance;
        if (manager == null || PrestigeMinionMethod == null)
            return false;

        try
        {
            return manager.ButtonVisible();
        }
        catch
        {
            return false;
        }
    }

    private static void TryPrestige(Minion minion)
    {
        DivinitiesManager manager = DivinitiesManager.instance;
        if (manager == null || PrestigeMinionMethod == null)
            return;

        int originalLevel = minion.level;
        double divinityPointsBefore = PlayerInventory.instance?.divinityPoints ?? 0d;
        try
        {
            minion.level = minion.maxLevel;
            PrestigeMinionMethod.Invoke(manager, new object[] { minion });

            double gained = Math.Max(
                0d,
                (PlayerInventory.instance?.divinityPoints ?? divinityPointsBefore) -
                divinityPointsBefore);
            ProgressionLog.User(
                $"Minion prestiged at maximum level: {GetDisplayName(minion)}; " +
                $"maxLevel={minion.maxLevel}; divinityPointsGained={gained:0.##}.");
        }
        catch
        {
            // Avoid leaving a minion at an artificial maximum level if the
            // native prestige call did not complete.
            if (minion.level == minion.maxLevel)
                minion.level = originalLevel;
            throw;
        }
    }

    private static string GetDisplayName(Minion minion)
    {
        if (!string.IsNullOrWhiteSpace(minion.localizedName))
            return minion.localizedName;
        if (!string.IsNullOrWhiteSpace(minion.name))
            return minion.name;
        return "Unknown Minion";
    }
}
