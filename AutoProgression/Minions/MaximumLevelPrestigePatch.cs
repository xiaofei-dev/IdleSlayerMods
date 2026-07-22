using AutoProgression.Diagnostics;
using HarmonyLib;
using Il2Cpp;

namespace AutoProgression.Minions;

[HarmonyPatch(typeof(DivinitiesManager), "PrestigeMinion", typeof(Minion))]
internal static class MaximumLevelPrestigePatch
{
    private static void Prefix(Minion minion)
    {
        if (Plugin.Config?.EnableAutomaticMinionPrestige?.Value != true ||
            minion == null ||
            minion.level >= minion.maxLevel)
        {
            return;
        }

        int originalLevel = minion.level;
        minion.level = minion.maxLevel;
        ProgressionLog.Debug(
            $"Maximum-level prestige applied to native prestige action: " +
            $"{GetDisplayName(minion)}; level={originalLevel}->{minion.maxLevel}.");
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
