using AutoAdventurer.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Gameplay;

internal sealed class AutoBossService
{
    private const float DialogueIntervalSeconds = 0.2f;
    private const float AttackIntervalSeconds = 0.5f;
    private const float ResultCloseIntervalSeconds = 0.5f;

    private BossMapController activeController;
    private float nextDialogueTime;
    private float nextAttackTime;
    private float nextResultCloseTime;
    private bool healthReductionLogged;

    internal void Tick(float now)
    {
        if (!Plugin.Config.AutoBoss.Value)
        {
            Reset();
            return;
        }

        if (TryCloseBossResult(now))
            return;

        BossMapController controller = BossMapController.instance;
        if (!IsActiveBossController(controller))
        {
            Reset();
            return;
        }

        if (activeController == null ||
            activeController.Pointer != controller.Pointer)
        {
            activeController = controller;
            nextDialogueTime = now;
            nextAttackTime = now;
            healthReductionLogged = false;
        }

        if (controller.currentBossHealth > 1f)
        {
            controller.currentBossHealth = 1f;
            if (!healthReductionLogged)
            {
                healthReductionLogged = true;
                AdventurerLog.BossDebug("Active boss reduced to 1 HP.");
            }
        }

        DialogManager dialog = DialogManager.instance;
        if (dialog != null && dialog.isVisible)
        {
            if (now >= nextDialogueTime && !dialog.waitingForOptionSelection &&
                !dialog.cantShowNextDialog &&
                dialog.currentFirstOpenSkipCooldown <= 0f)
            {
                dialog.ShowNextDialog();
                nextDialogueTime = now + DialogueIntervalSeconds;
            }
            return;
        }

        if (controller.currentBossHealth <= 0f ||
            controller.currentBossObject == null || now < nextAttackTime)
            return;

        PlayerMovement player = PlayerMovement.instance;
        if (player == null || player.bowDisabled) return;

        player.ShootArrow();
        nextAttackTime = now + AttackIntervalSeconds;
    }

    private bool TryCloseBossResult(float now)
    {
        if (now < nextResultCloseTime) return false;

        BossResultScreen result =
            UnityEngine.Object.FindObjectOfType<BossResultScreen>();
        if (result == null || result.gameObject == null ||
            !result.gameObject.activeInHierarchy || result.close ||
            result.goingToDestroy || result.closeButton == null ||
            !result.closeButton.gameObject.activeInHierarchy ||
            !result.closeButton.interactable)
            return false;

        result.Close();
        nextResultCloseTime = now + ResultCloseIntervalSeconds;
        AdventurerLog.BossDebug("Boss result screen closed.");
        return true;
    }

    private static bool IsActiveBossController(BossMapController controller) =>
        controller != null && controller.gameObject != null &&
        controller.gameObject.activeInHierarchy &&
        controller.currentBossMap != null;

    internal void Reset()
    {
        activeController = null;
        nextDialogueTime = 0f;
        nextAttackTime = 0f;
        nextResultCloseTime = 0f;
        healthReductionLogged = false;
    }
}
