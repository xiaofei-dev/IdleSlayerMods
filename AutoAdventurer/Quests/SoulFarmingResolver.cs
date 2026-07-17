using System;
using AutoAdventurer.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Quests;

internal sealed class SoulFarmingResolver
{
    private const double ScoreEqualityTolerance = 0.000001d;
    private PopupPortals resolvedPopup;
    private bool unavailableLogged;

    internal SoulFarmingSelection SelectBestAvailable()
    {
        MapController controller = MapController.instance;
        BaseMap currentMap = controller?.selectedMap;
        PopupPortals popup = ResolvePopup();
        if (controller == null || currentMap == null || popup == null)
            return null;

        var maps = controller.GetAvailableMaps(true);
        if (maps == null || maps.Count == 0) return null;

        Map bestMap = null;
        double bestScore = double.MinValue;
        double currentScore = double.NaN;
        string currentMapId = currentMap.name ?? string.Empty;

        for (int index = 0; index < maps.Count; index++)
        {
            Map map = maps[index];
            if (map == null || !map.IsAvailable()) continue;

            double souls;
            double coins;
            try
            {
                popup.GetMapScores(map, out souls, out coins);
            }
            catch (Exception exception)
            {
                AdventurerLog.QuestDebug(
                    $"Soul fallback: score read failed; map={map.name}; exception={exception.GetType().Name}.");
                continue;
            }

            if (double.IsNaN(souls) || double.IsInfinity(souls)) continue;
            bool isCurrent = string.Equals(map.name, currentMapId,
                StringComparison.Ordinal);
            if (isCurrent) currentScore = souls;

            bool isBetter = bestMap == null || souls > bestScore;
            bool tiedAndCurrent = bestMap != null && isCurrent &&
                Math.Abs(souls - bestScore) <= ScoreEqualityTolerance;
            if (!isBetter && !tiedAndCurrent) continue;

            bestMap = map;
            bestScore = souls;
        }

        if (bestMap == null) return null;
        unavailableLogged = false;
        return new SoulFarmingSelection
        {
            Map = bestMap,
            SoulScore = bestScore,
            CurrentMapId = currentMapId,
            CurrentSoulScore = currentScore
        };
    }

    private PopupPortals ResolvePopup()
    {
        PopupPortals popup = PopupPortals.instance ??
                             PortalButton.instance?.popupPortals ??
                             resolvedPopup;
        if (popup != null)
        {
            resolvedPopup = popup;
            unavailableLogged = false;
            return popup;
        }

        foreach (PopupPortals candidate in
                 Resources.FindObjectsOfTypeAll<PopupPortals>())
        {
            if (candidate == null || candidate.gameObject == null) continue;
            var scene = candidate.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) continue;
            resolvedPopup = candidate;
            unavailableLogged = false;
            return candidate;
        }

        if (!unavailableLogged)
        {
            unavailableLogged = true;
            AdventurerLog.QuestDebug(
                "Soul fallback: PopupPortals score provider is unavailable.");
        }
        return null;
    }

    internal void InvalidateSceneObjects()
    {
        resolvedPopup = null;
        unavailableLogged = false;
    }

    internal void Reset() => InvalidateSceneObjects();
}
