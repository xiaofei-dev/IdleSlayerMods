using System;
using Il2Cpp;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AutoAdventurer.Gameplay;

/// <summary>
/// Lightweight Runner/Rage companion for the L automation toggle. It sends
/// the shortest native jump-panel pulse possible and continuously requests an
/// arrow attack; the game remains authoritative for jump eligibility, bow
/// availability, and attack cooldown.
/// </summary>
internal sealed class AutomaticJumpAttackService
{
    private const float RetryJumpAfterRejectedInputSeconds = 0.08f;
    private const float BoxScanIntervalSeconds = 0.05f;
    private const float BoxJumpHoldSeconds = 0.14f;
    private const float BoxApproachViewportDistance = 0.38f;
    private const float BoxPassedViewportDistance = -0.04f;
    private const float LightAttackIntervalSeconds = 1f;
    private const float MediumAttackIntervalSeconds = 1f / 3f;
    private const float HighAttackIntervalSeconds = 0.125f;
    private const float ExtraHighAttackIntervalSeconds = 1f / 15f;

    private float nextJumpAttemptTime;
    private float nextAttackTime;
    private float nextBoxScanTime;
    private float releaseBoxJumpAt = -1f;
    private bool boxJumpHeld;
    private bool untrackedBoxApproaching;

    internal void Tick(
        float now,
        bool enabled,
        bool autoJump,
        bool autoShootArrows,
        string arrowAttackFrequency)
    {
        if (!enabled || !IsCentralGameplayState())
        {
            Reset();
            return;
        }

        PlayerMovement player = PlayerMovement.instance;
        if (player == null)
        {
            Reset();
            return;
        }

        ReleaseHeldBoxJumpWhenDue(now);

        if (autoShootArrows && now >= nextAttackTime)
        {
            TryAttack(player);
            nextAttackTime = now + GetAttackInterval(arrowAttackFrequency);
        }

        if (!autoJump)
        {
            ReleaseHeldBoxJump();
            return;
        }

        // Before the horizontal box-magnet upgrade is bought, early-game
        // Random Boxes can pass over a character that is only making minimum
        // jump pulses. When an unhit box reaches the player, briefly hold the
        // native jump input so the character rises into its path. Once the
        // magnet exists, the game already handles horizontal alignment and
        // this helper remains completely dormant.
        if (now >= nextBoxScanTime)
        {
            nextBoxScanTime = now + BoxScanIntervalSeconds;
            untrackedBoxApproaching =
                TryGetApproachingUntrackedRandomBox(player, out _);
        }

        if (untrackedBoxApproaching)
        {
            if (!boxJumpHeld && now >= nextJumpAttemptTime && BeginBoxJump())
            {
                boxJumpHeld = true;
                releaseBoxJumpAt = now + BoxJumpHoldSeconds;
                nextJumpAttemptTime = releaseBoxJumpAt +
                                      RetryJumpAfterRejectedInputSeconds;
            }
            return;
        }

        if (now < nextJumpAttemptTime)
            return;

        // DOWN+UP in one managed pass produces the minimum native jump. Pulse
        // continuously and let PlayerMovement accept it only when jumping is
        // legal. This deliberately avoids IsGrounded(), which can remain
        // false while the character is visibly standing on the floor.
        if (PulseJump())
            nextJumpAttemptTime = now + RetryJumpAfterRejectedInputSeconds;
    }

    private static float GetAttackInterval(string frequency)
    {
        if (string.Equals(frequency, "Ultra", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(frequency, "Extreme", StringComparison.OrdinalIgnoreCase))
            return 0f;
        if (string.Equals(frequency, "Extra High", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(frequency, "ExtraHigh", StringComparison.OrdinalIgnoreCase))
            return ExtraHighAttackIntervalSeconds;
        if (string.Equals(frequency, "High", StringComparison.OrdinalIgnoreCase))
            return HighAttackIntervalSeconds;
        if (string.Equals(frequency, "Light", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(frequency, "Low", StringComparison.OrdinalIgnoreCase))
            return LightAttackIntervalSeconds;
        return MediumAttackIntervalSeconds;
    }

    private static void TryAttack(PlayerMovement player)
    {
        if (player.bowDisabled)
            return;

        try
        {
            // Called every rendered frame so no ready attack window is missed.
            // ShootArrow performs the game's own unlock/cooldown validation.
            player.ShootArrow();
        }
        catch
        {
            // Missing/rebuilt player and bow objects are normal around
            // Ascension and scene changes; the outer runtime will retry.
        }
    }

    private static bool PulseJump()
    {
        JumpPanel panel = JumpPanel.instance;
        EventSystem eventSystem = EventSystem.current;
        if (panel == null || eventSystem == null)
            return false;

        PointerEventData data = new(eventSystem)
        {
            position = new Vector2(Screen.width * 0.8f, Screen.height)
        };

        bool downSent = false;
        try
        {
            panel.OnPointerDown(data);
            downSent = true;
            panel.OnPointerUp(data);
            return true;
        }
        catch
        {
            if (downSent)
            {
                try
                {
                    panel.OnPointerUp(data);
                }
                catch
                {
                    // Input cleanup must never break the main runtime.
                }
            }
            return false;
        }
    }

    private static bool BeginBoxJump()
    {
        JumpPanel panel = JumpPanel.instance;
        EventSystem eventSystem = EventSystem.current;
        if (panel == null || eventSystem == null)
            return false;

        try
        {
            panel.OnPointerDown(CreateJumpPointer(eventSystem));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ReleaseHeldBoxJumpWhenDue(float now)
    {
        if (boxJumpHeld && now >= releaseBoxJumpAt)
            ReleaseHeldBoxJump();
    }

    private void ReleaseHeldBoxJump()
    {
        if (!boxJumpHeld)
            return;

        boxJumpHeld = false;
        releaseBoxJumpAt = -1f;
        JumpPanel panel = JumpPanel.instance;
        EventSystem eventSystem = EventSystem.current;
        if (panel == null || eventSystem == null)
            return;

        try
        {
            panel.OnPointerUp(CreateJumpPointer(eventSystem));
        }
        catch
        {
            // Scene changes can destroy the input target while held.
        }
    }

    private static PointerEventData CreateJumpPointer(EventSystem eventSystem) =>
        new(eventSystem)
        {
            position = new Vector2(Screen.width * 0.8f, Screen.height)
        };

    private static bool TryGetApproachingUntrackedRandomBox(
        PlayerMovement player,
        out Transform boxTransform)
    {
        boxTransform = null;
        float closestDistance = float.MaxValue;
        AscensionSkills skills = AscensionSkills.list;
        bool normalBoxTracked = skills?.RandomBoxMagnet?.unlocked == true;
        bool specialBoxTracked = skills?.SpecialRandomBoxMagnet?.unlocked == true;

        if (!normalBoxTracked)
        {
            foreach (RandomBox box in
                     UnityEngine.Object.FindObjectsOfType<RandomBox>())
            {
                if (box == null || box.isHitted || box.gameObject == null ||
                    !box.gameObject.activeInHierarchy)
                    continue;
                ConsiderBox(player.transform, box.transform,
                    ref boxTransform, ref closestDistance);
            }
        }

        if (!specialBoxTracked)
        {
            foreach (SpecialRandomBox box in
                     UnityEngine.Object.FindObjectsOfType<SpecialRandomBox>())
            {
                if (box == null || box.isHitted || box.gameObject == null ||
                    !box.gameObject.activeInHierarchy)
                    continue;
                ConsiderBox(player.transform, box.transform,
                    ref boxTransform, ref closestDistance);
            }
        }

        return boxTransform != null;
    }

    private static void ConsiderBox(
        Transform player,
        Transform candidate,
        ref Transform selected,
        ref float closestDistance)
    {
        if (player == null || candidate == null)
            return;

        float horizontalDistance;
        Camera camera = Camera.main;
        if (camera != null)
        {
            float playerX = camera.WorldToViewportPoint(player.position).x;
            float boxX = camera.WorldToViewportPoint(candidate.position).x;
            horizontalDistance = boxX - playerX;
        }
        else
        {
            // This fallback is only used for the brief frames in which the
            // gameplay camera is being rebuilt.
            horizontalDistance = candidate.position.x - player.position.x;
        }

        if (horizontalDistance < BoxPassedViewportDistance ||
            horizontalDistance > BoxApproachViewportDistance ||
            horizontalDistance >= closestDistance)
            return;

        closestDistance = horizontalDistance;
        selected = candidate;
    }

    private static bool IsCentralGameplayState()
    {
        GameStates state = GameState.current;
        return state == GameStates.RunnerMode ||
               state == GameStates.RageMode;
    }

    internal void Reset()
    {
        ReleaseHeldBoxJump();
        nextJumpAttemptTime = 0f;
        nextAttackTime = 0f;
        nextBoxScanTime = 0f;
        untrackedBoxApproaching = false;
    }
}
