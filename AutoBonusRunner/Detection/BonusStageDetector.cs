using Il2Cpp;
using AutoBonusRunner.Diagnostics;
using UnityEngine;

namespace AutoBonusRunner.Detection;

internal sealed class BonusStageDetector
{
    private int stableRequiredSection = -1;
    private double stableRequiredSpheres = double.NaN;
    private bool rewardFlagReadFailureLogged;

    internal BonusStageState Capture()
    {
        string gameStateName = GameState.current.ToString();
        if (!GameState.IsBonus())
        {
            stableRequiredSection = -1;
            stableRequiredSpheres = double.NaN;
            rewardFlagReadFailureLogged = false;
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
        bool waitingForRewardZone = false;
        bool rewardZoneEntered = false;
        bool givingRewards = false;
        bool rewardFlagsAvailable = false;
        try
        {
            if (controller != null)
            {
                waitingForRewardZone = controller.waitForRewardZone;
                rewardZoneEntered = controller.rewardZone;
                givingRewards = controller.givingRewards;
                rewardFlagsAvailable = true;
                if (rewardFlagReadFailureLogged)
                {
                    rewardFlagReadFailureLogged = false;
                    BonusRunnerLog.Debug(
                        "Native reward flags are readable again.",
                        "Detection");
                }
            }
        }
        catch (System.Exception exception)
        {
            // Reward flags are independent diagnostics/control evidence. A
            // transient wrapper read failure must not invalidate terrain and
            // player state captured in the same frame.
            if (!rewardFlagReadFailureLogged)
            {
                rewardFlagReadFailureLogged = true;
                BonusRunnerLog.Warning(
                    $"Native reward flags unavailable: " +
                    $"{exception.GetType().Name}: {exception.Message}. " +
                    "These flags are diagnostic only; terrain control and " +
                    "the independent typed reward-target scan continue.");
            }
        }

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
                rewardFlagsAvailable,
                waitingForRewardZone,
                rewardZoneEntered,
                givingRewards,
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
            rewardFlagsAvailable,
            waitingForRewardZone,
            rewardZoneEntered,
            givingRewards,
            isSupportedBonusMap);
    }
}
