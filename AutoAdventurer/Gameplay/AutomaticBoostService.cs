using System;
using AutoAdventurer.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Gameplay;

internal sealed class AutomaticBoostService
{
    private const float CheckIntervalSeconds = 0.1f;
    private const float PostAscensionDelaySeconds = 5f;
    private const float RunnerStableSeconds = 2f;
    private const float MinimumTriggerDebounceSeconds = 0.5f;
    private const float RepeatedActionLogIntervalSeconds = 30f;
    private const int RequiredStableAbilityChecks = 3;

    private float nextCheckTime;
    private float suspendedUntil;
    private float nextAllowedTriggerTime;
    private float runnerValidSince = -1f;
    private float nextTriggerLogTime;
    private bool hasFirstSkillState;
    private bool previousFirstSkillBought;
    private string stableAbilityType = string.Empty;
    private int stableAbilityChecks;
    private bool managerMissingLogged;

    internal void Tick(float now, bool enabled)
    {
        if (!enabled)
        {
            runnerValidSince = -1f;
            ResetAbilityStability();
            return;
        }

        if (!IsRunnerStable(now)) return;
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
            if (!TrackStableSupportedAbility(selected)) return;
            if (now < suspendedUntil || now < nextAllowedTriggerTime) return;
            if (!selected.Unlocked())
            {
                ResetAbilityStability();
                return;
            }
            if (selected.GetCurrentCooldown() > 0d) return;

            // Ability.Activate() does not reliably publish its cooldown before
            // the next managed poll. Mirror the proven game-mod path and write
            // the authoritative cooldown immediately, plus a local debounce.
            selected.Activate();
            selected.currentCd = selected.GetCooldown();
            nextAllowedTriggerTime = now + MinimumTriggerDebounceSeconds;
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

    private bool IsRunnerStable(float now)
    {
        if (GameState.current != GameStates.RunnerMode)
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
        nextAllowedTriggerTime = suspendedUntil;
        ResetAbilityStability();
        managerMissingLogged = false;
    }

    private void ResetAbilityStability()
    {
        stableAbilityType = string.Empty;
        stableAbilityChecks = 0;
    }

    private static string GetAbilityName(Ability ability) =>
        ability?.GetIl2CppType()?.Name ?? "Unknown";

    internal void Reset()
    {
        nextCheckTime = 0f;
        suspendedUntil = 0f;
        nextAllowedTriggerTime = 0f;
        nextTriggerLogTime = 0f;
        runnerValidSince = -1f;
        hasFirstSkillState = false;
        previousFirstSkillBought = false;
        managerMissingLogged = false;
        ResetAbilityStability();
    }
}
