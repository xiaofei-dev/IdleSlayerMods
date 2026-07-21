using Il2Cpp;
using UnityEngine;

namespace AutoBonusRunner.Detection;

internal sealed class BonusStageDetector
{
    private int stableRequiredSection = -1;
    private double stableRequiredSpheres = double.NaN;

    internal BonusStageState Capture()
    {
        string gameStateName = GameState.current.ToString();
        if (!GameState.IsBonus())
        {
            stableRequiredSection = -1;
            stableRequiredSpheres = double.NaN;
            return BonusStageState.Outside(gameStateName);
        }

        BonusMapController controller = BonusMapController.instance;
        PlayerMovement player = PlayerMovement.instance;
        BaseMap currentMap = null;
        try
        {
            currentMap = MapController.instance?.CurrentBonusMap() ??
                controller?.currentBonusMap;
        }
        catch
        {
            currentMap = controller?.currentBonusMap;
        }
        string mapName = currentMap?.name ?? "Unknown";
        bool isSupportedBonusMap = false;
        try
        {
            BaseMap supportedMap = Maps.list?.BonusStage3;
            isSupportedBonusMap =
                currentMap != null && supportedMap != null &&
                currentMap.id == supportedMap.id;
        }
        catch
        {
            // Preserve a narrow name fallback during singleton initialization.
        }
        if (!isSupportedBonusMap)
        {
            isSupportedBonusMap = mapName.Contains(
                "bonus_stage_3",
                System.StringComparison.OrdinalIgnoreCase);
        }
        int sectionIndex = controller?.currentSectionIndex ?? -1;
        int collectedSpheres = controller?.bonusSpheresPickedUp ?? -1;
        double requiredSpheres = double.NaN;
        try
        {
            if (controller?.currentSection != null)
                requiredSpheres = controller.currentSection.GetRequiredSpheres();
        }
        catch
        {
            // A section can be replaced by the pool between the index change
            // and the next BonusMode frame. Treat progress as unavailable for
            // that short transition instead of retaining stale requirements.
        }
        if (double.IsFinite(requiredSpheres) && requiredSpheres > 0d)
        {
            stableRequiredSection = sectionIndex;
            stableRequiredSpheres = requiredSpheres;
        }
        else if (stableRequiredSection == sectionIndex &&
                 double.IsFinite(stableRequiredSpheres))
        {
            requiredSpheres = stableRequiredSpheres;
        }
        else
        {
            requiredSpheres = double.NaN;
        }
        float currentTime = controller?.currentTime ?? float.NaN;
        float maximumTime = controller?.maxTime ?? float.NaN;
        bool characterFellOff = controller?.characterFellOff ?? false;
        bool spiritBoostEnabled = controller?.spiritBoostEnabled ?? false;
        bool isTimerVisible = controller?.showCurrentTime ?? false;

        if (player == null)
            return new(
                true,
                gameStateName,
                mapName,
                sectionIndex,
                0,
                default,
                default,
                false,
                false,
                collectedSpheres,
                requiredSpheres,
                currentTime,
                maximumTime,
                characterFellOff,
                spiritBoostEnabled,
                isTimerVisible,
                isSupportedBonusMap);

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        return new(
            true,
            gameStateName,
            mapName,
            sectionIndex,
            player.GetInstanceID(),
            player.transform.position,
            body?.velocity ?? Vector2.zero,
            player.IsGrounded(),
            true,
            collectedSpheres,
            requiredSpheres,
            currentTime,
            maximumTime,
            characterFellOff,
            spiritBoostEnabled,
            isTimerVisible,
            isSupportedBonusMap);
    }
}
