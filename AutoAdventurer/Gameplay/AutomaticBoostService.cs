using System;
using AutoAdventurer.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Gameplay;

internal sealed class AutomaticBoostService
{
    private const float CheckIntervalSeconds = 0.1f;
    private const float PostAscensionDelaySeconds = 5f;
    // MainScreenGuard already owns the post-transition stabilization delay.
    // Keeping a second delay here made Auto Boost wait again after the screen
    // had already been declared safe.
    private const float RunnerStableSeconds = 0f;
    private const float RepeatedActionLogIntervalSeconds = 30f;
    private const int RequiredStableAbilityChecks = 3;

    private float nextCheckTime;
    private float suspendedUntil;
    private float runnerValidSince = -1f;
    private float cooldownReadySince = -1f;
    private float nextTriggerLogTime;
    private bool hasFirstSkillState;
    private bool previousFirstSkillBought;
    private string stableAbilityType = string.Empty;
    private int stableAbilityChecks;
    private bool managerMissingLogged;
    private bool immediateActivationRequested;

    internal void RequestImmediateActivation(float now)
    {
        immediateActivationRequested = true;
        runnerValidSince = now - RunnerStableSeconds;
    }

    internal void Tick(float now, bool enabled)
    {
        if (!enabled)
        {
            immediateActivationRequested = false;
            runnerValidSince = -1f;
            ResetAbilityStability();
            return;
        }

        if (!IsGameplayStateStable(now)) return;
        ObserveAscensionReset(now);
        if (now < nextCheckTime) return;
        nextCheckTime = now + CheckIntervalSeconds;

        try
        {
            // Resolve the authoritative singleton every pass. Ascension can
            // rebuild ability objects without forcing GameState out of Runner.
            AbilitiesManager manager = AbilitiesManager.instance;
            if (manager == null)
            {
                ResetAbilityStability();
                if (!managerMissingLogged)
                {
                    managerMissingLogged = true;
                    AdventurerLog.Debug("AbilitiesManager is not available yet.");
                }
                return;
            }

            managerMissingLogged = false;
            Ability selected = manager.selectedAbility;
            bool immediate = immediateActivationRequested;
            if (immediate)
            {
                if (selected is not Boost && selected is not WindDash)
                {
                    immediateActivationRequested = false;
                    ResetAbilityStability();
                    return;
                }
            }
            else if (!TrackStableSupportedAbility(selected)) return;

            if (now < suspendedUntil) return;
            if (!selected.Unlocked())
            {
                immediateActivationRequested = false;
                ResetAbilityStability();
                return;
            }
            if (selected.GetCurrentCooldown() > 0d)
            {
                immediateActivationRequested = false;
                cooldownReadySince = -1f;
                return;
            }

            if (!immediate && cooldownReadySince < 0f)
            {
                cooldownReadySince = now;
                return;
            }

            float activationDelay = Math.Max(0f,
                Plugin.Config.AutoBoostActivationDelaySeconds.Value);
            if (!immediate && now - cooldownReadySince < activationDelay) return;

            // Wind Dash can pass above portals and elite enemies when it is
            // activated during a jump. Ground contact is map-independent and
            // is therefore safer than comparing an absolute world Y value.
            // Once the ability is ready, poll every frame until the player is
            // back at the safe height; no additional activation delay is used.
            if (selected is WindDash &&
                Plugin.Config.WindDashRequireGrounded.Value &&
                !IsWindDashHeightSafe())
            {
                nextCheckTime = now;
                return;
            }

            // Ability.Activate() does not reliably publish its cooldown before
            // the next managed poll. Mirror the proven game-mod path and write
            // the authoritative cooldown immediately.
            selected.Activate();
            selected.currentCd = selected.GetCooldown();
            immediateActivationRequested = false;
            cooldownReadySince = -1f;
            if (now >= nextTriggerLogTime)
            {
                nextTriggerLogTime = now + RepeatedActionLogIntervalSeconds;
                AdventurerLog.Debug(
                    $"Auto Boost triggered {GetAbilityName(selected)}; cooldown={selected.currentCd:0.###}.");
            }
        }
        catch (Exception exception)
        {
            AdventurerLog.Error($"Auto Boost failed safely: {exception}");
            SuspendAndClear(now, PostAscensionDelaySeconds);
        }
    }

    private bool IsGameplayStateStable(float now)
    {
        GameStates state = GameState.current;
        if (state != GameStates.RunnerMode && state != GameStates.RageMode)
        {
            runnerValidSince = -1f;
            ResetAbilityStability();
            return false;
        }

        if (runnerValidSince < 0f)
        {
            runnerValidSince = now;
            ResetAbilityStability();
            return false;
        }

        return now - runnerValidSince >= RunnerStableSeconds;
    }

    private void ObserveAscensionReset(float now)
    {
        Upgrade firstSkill = Upgrades.list?.RandomBox;
        if (firstSkill == null)
        {
            hasFirstSkillState = false;
            return;
        }

        bool bought = firstSkill.bought;
        if (!hasFirstSkillState)
        {
            hasFirstSkillState = true;
            previousFirstSkillBought = bought;
            return;
        }

        if (previousFirstSkillBought && !bought)
        {
            SuspendAndClear(now, PostAscensionDelaySeconds);
            AdventurerLog.Debug(
                $"Ascension detected; Auto Boost suspended for {PostAscensionDelaySeconds:0.#} seconds.");
        }

        previousFirstSkillBought = bought;
    }

    private bool TrackStableSupportedAbility(Ability selected)
    {
        if (selected is not Boost && selected is not WindDash)
        {
            ResetAbilityStability();
            return false;
        }

        string typeName = GetAbilityName(selected);
        if (!string.Equals(typeName, stableAbilityType, StringComparison.Ordinal))
        {
            stableAbilityType = typeName;
            stableAbilityChecks = 1;
            AdventurerLog.Debug($"Auto Boost observed selected ability {typeName}.");
            return false;
        }

        if (stableAbilityChecks < RequiredStableAbilityChecks)
            stableAbilityChecks++;

        return stableAbilityChecks >= RequiredStableAbilityChecks;
    }

    private void SuspendAndClear(float now, float seconds)
    {
        suspendedUntil = Math.Max(suspendedUntil, now + Math.Max(0f, seconds));
        ResetAbilityStability();
        managerMissingLogged = false;
    }

    private void ResetAbilityStability()
    {
        stableAbilityType = string.Empty;
        stableAbilityChecks = 0;
        cooldownReadySince = -1f;
    }

    private static string GetAbilityName(Ability ability) =>
        ability?.GetIl2CppType()?.Name ?? "Unknown";

    private static bool IsWindDashHeightSafe()
    {
        PlayerMovement player = PlayerMovement.instance;
        return player != null && player.IsGrounded();
    }

    internal void Reset()
    {
        nextCheckTime = 0f;
        suspendedUntil = 0f;
        nextTriggerLogTime = 0f;
        runnerValidSince = -1f;
        hasFirstSkillState = false;
        previousFirstSkillBought = false;
        managerMissingLogged = false;
        immediateActivationRequested = false;
        ResetAbilityStability();
    }
}
