using System;
using AutoBonusRunner.Detection;
using AutoBonusRunner.Diagnostics;
using AutoBonusRunner.Physics;
using Il2Cpp;
using UnityEngine;

namespace AutoBonusRunner.Control;

/// <summary>
/// Owns only a reward phase explicitly authorized by AutoBonusRunnerRuntime.
/// Normal terrain navigation remains in the runtime; this controller supplies
/// the short JumpPanel action, direct bow fire, and an optional grounded Wind
/// Dash. Native reward flags are diagnostics here, not authorization. It has
/// no dependency on AutoJumpMod or AutoAdventurer.
/// </summary>
internal sealed class CompletionRewardController
{
    private const string BowSkillName =
        "ascension_upgrade_sacred_book_of_projectiles";
    private const float RewardJumpIntervalSeconds = 0.14f;
    private const float RewardArrowIntervalSeconds = 0.10f;
    private const float WindDashCheckIntervalSeconds = 0.05f;
    private const float StationaryGroundFallbackSeconds = 0.25f;
    private const float StationaryVerticalTolerance = 0.0025f;
    private const float StableGroundVerticalSpeed = 2.5f;
    private const int RequiredStableGroundFixedSteps = 2;
    private const float RepeatedUnavailableLogSeconds = 2.0f;

    private bool traversalActive;
    private int traversalSection = -1;
    private float traversalStartedAt;
    private float nextRewardJumpTime;
    private float nextRewardArrowTime;
    private float nextWindDashCheckTime;
    private float nextUnavailableLogTime;
    private float lastPlayerY;
    private float verticallyStationarySince = -1f;
    private bool hasPlayerY;
    private long stableGroundLastFixedStep = -1;
    private int stableGroundFixedSteps;
    private string lastUnavailableReason = string.Empty;
    private long rewardJumpCount;
    private long rewardArrowCount;
    private long windDashCount;

    internal bool IsRewardPhaseActive => traversalActive;

    internal void ObserveTraversal(
        bool active,
        BonusStageState state)
    {
        if (!active)
        {
            if (traversalActive)
            {
                BonusRunnerLog.Debug(
                    $"NativeRewardPhaseEnded Section={traversalSection}, " +
                    $"Elapsed={Time.unscaledTime - traversalStartedAt:F3}s, " +
                    $"RewardJumps={rewardJumpCount}, " +
                    $"RewardArrows={rewardArrowCount}, " +
                    $"WindDashes={windDashCount}, " +
                    $"NextState={state.GameStateName}.",
                    "Completion");
            }
            ResetState();
            return;
        }

        if (traversalActive && traversalSection == state.SectionIndex)
            return;

        ResetState();
        traversalActive = true;
        traversalSection = state.SectionIndex;
        traversalStartedAt = Time.unscaledTime;
        nextRewardJumpTime = Time.unscaledTime;
        nextRewardArrowTime = Time.unscaledTime;
        nextWindDashCheckTime = Time.unscaledTime;
        BonusRunnerLog.Debug(
            $"NativeRewardPhaseStarted Section={state.SectionIndex}, " +
            $"Position=({state.PlayerPosition.x:F3}," +
            $"{state.PlayerPosition.y:F3}), Velocity=" +
            $"({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
            $"Spheres={state.CollectedSpheres}/" +
            $"{Math.Ceiling(state.RequiredSpheres):F0}. " +
            $"RewardFlags[Available={state.RewardFlagsAvailable},Wait=" +
            $"{state.WaitingForRewardZone},Trigger=" +
            $"{state.RewardZoneEntered},Giving={state.GivingRewards}]. " +
            "The runtime's confirmed typed reward-target latch authorizes " +
            "reward input; native flags remain diagnostic only.",
            "Completion");
    }

    internal bool TickGroundWindDash(
        BonusStageState state,
        PlayerMovement player,
        bool enabled)
    {
        if (!traversalActive || !enabled || player == null)
            return false;

        float now = Time.unscaledTime;
        // Observe/reset contact on every call, including cooldown and missing-
        // ability frames. Otherwise a two-step proof from an earlier landing
        // can survive an airborne cooldown interval and be reused later.
        bool stableGround = IsGroundHeightSafe(now, state, player);
        if (now < nextWindDashCheckTime)
            return false;
        nextWindDashCheckTime = now + WindDashCheckIntervalSeconds;

        try
        {
            MainAbilityButton button =
                UnityEngine.Object.FindObjectOfType<MainAbilityButton>();
            bool abilityIconVisible =
                button != null &&
                button.isActiveAndEnabled &&
                button.gameObject != null &&
                button.gameObject.activeInHierarchy;
            if (!abilityIconVisible)
            {
                LogWindDashUnavailable(
                    now,
                    "MainAbilityIconNotVisible",
                    state,
                    null);
                return false;
            }

            AbilitiesManager manager = ResolveAbilitiesManager();
            Ability selected = manager?.selectedAbility;
            if (!IsWindDash(selected))
            {
                LogWindDashUnavailable(
                    now,
                    $"SelectedAbility={GetAbilityName(selected)}",
                    state,
                    selected);
                return false;
            }

            if (!selected.Unlocked())
            {
                LogWindDashUnavailable(
                    now,
                    "WindDashLocked",
                    state,
                    selected);
                return false;
            }

            double cooldown = selected.GetCurrentCooldown();
            if (cooldown > 0d)
            {
                LogWindDashUnavailable(
                    now,
                    $"Cooldown={cooldown:F3}",
                    state,
                    selected);
                return false;
            }

            if (!stableGround)
            {
                // Poll every render frame after cooldown reaches zero so the
                // first safe ground contact is not lost to the normal check
                // interval.
                nextWindDashCheckTime = now;
                LogWindDashUnavailable(
                    now,
                    "AwaitingGroundContact",
                    state,
                    selected);
                return false;
            }

            selected.Activate();
            selected.currentCd = selected.GetCooldown();
            windDashCount++;
            ResetStableGroundObservation();
            lastUnavailableReason = string.Empty;
            BonusRunnerLog.Debug(
                $"CompletionWindDashActivated Section={state.SectionIndex}, " +
                $"Position=({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Velocity=" +
                $"({state.PlayerVelocity.x:F3},{state.PlayerVelocity.y:F3}), " +
                $"Grounded={state.IsGrounded}, Ability=" +
                $"{GetAbilityName(selected)}, NewCooldown=" +
                $"{selected.currentCd:F3}, Count={windDashCount}. " +
                "The ability was selected, unlocked, visible, ready, and " +
                "activated from ground-safe height.",
                "Completion");
            return true;
        }
        catch (Exception exception)
        {
            nextWindDashCheckTime = now + 0.50f;
            BonusRunnerLog.Exception(
                $"Completion Wind Dash for section {state.SectionIndex}",
                exception);
            return false;
        }
    }

    internal bool TryRewardActions(
        BonusStageState state,
        PlayerMovement player,
        JumpController jumpController,
        bool enabled)
    {
        if (!traversalActive || !enabled || player == null)
            return false;

        float now = Time.unscaledTime;
        bool jumpIssued = false;
        bool arrowIssued = false;

        bool stableGround = IsGroundHeightSafe(now, state, player);
        if (stableGround &&
            !jumpController.IsHoldingJump &&
            now >= nextRewardJumpTime)
        {
            nextRewardJumpTime = now + RewardJumpIntervalSeconds;
            jumpIssued = jumpController.Pulse(
                player,
                "CompletionReward: AutoJump-style minimum contextual pulse");
            if (jumpIssued)
                rewardJumpCount++;
        }

        string arrowDecision;
        if (now < nextRewardArrowTime)
        {
            arrowDecision = "IntervalPending";
        }
        else if (player.bowDisabled)
        {
            nextRewardArrowTime = now + RewardArrowIntervalSeconds;
            arrowDecision = "PlayerBowDisabled";
        }
        else if (!IsBowUnlocked())
        {
            nextRewardArrowTime = now + RewardArrowIntervalSeconds;
            arrowDecision = "BowSkillLockedOrUnavailable";
        }
        else
        {
            nextRewardArrowTime = now + RewardArrowIntervalSeconds;
            try
            {
                player.ShootArrow();
                rewardArrowCount++;
                arrowIssued = true;
                arrowDecision = "ShootArrowIssued";
            }
            catch (Exception exception)
            {
                arrowDecision =
                    $"ShootArrowFailed:{exception.GetType().Name}";
                BonusRunnerLog.Exception(
                    "Completion reward bow fire",
                    exception);
            }
        }

        if (jumpIssued || arrowIssued ||
            !string.Equals(
                arrowDecision,
                "IntervalPending",
                StringComparison.Ordinal))
        {
            BonusRunnerLog.Debug(
                $"CompletionRewardAction Section={state.SectionIndex}, " +
                $"Position=({state.PlayerPosition.x:F3}," +
                $"{state.PlayerPosition.y:F3}), Grounded=" +
                $"{state.IsGrounded}, StableGround={stableGround}, " +
                $"StableFixedSteps={stableGroundFixedSteps}/" +
                $"{RequiredStableGroundFixedSteps}, JumpIssued={jumpIssued}, " +
                "JumpMode=ImmediateDownUp, " +
                $"ArrowDecision={arrowDecision}, Counts[Jump=" +
                $"{rewardJumpCount},Arrow={rewardArrowCount},Dash=" +
                $"{windDashCount}], RewardFlags[Available=" +
                $"{state.RewardFlagsAvailable},Wait=" +
                $"{state.WaitingForRewardZone},Trigger=" +
                $"{state.RewardZoneEntered},Giving=" +
                $"{state.GivingRewards}]. The runtime's confirmed typed " +
                "reward-target latch is the authorization; this action is " +
                "never inferred from flags or missing terrain geometry.",
                "Completion");
        }

        return jumpIssued || arrowIssued;
    }

    internal void Reset(string reason)
    {
        if (traversalActive)
        {
            BonusRunnerLog.Debug(
                $"NativeRewardPhaseReset Reason={reason}, " +
                $"Section={traversalSection}, RewardJumps=" +
                $"{rewardJumpCount}, RewardArrows={rewardArrowCount}, " +
                $"WindDashes={windDashCount}.",
                "Completion");
        }
        ResetState();
    }

    private static AbilitiesManager ResolveAbilitiesManager()
    {
        AbilitiesManager singleton = AbilitiesManager.instance;
        if (singleton != null && IsWindDash(singleton.selectedAbility))
            return singleton;

        AbilitiesManager[] managers =
            Resources.FindObjectsOfTypeAll<AbilitiesManager>();
        foreach (AbilitiesManager candidate in managers)
        {
            if (candidate != null && IsWindDash(candidate.selectedAbility))
                return candidate;
        }

        return singleton;
    }

    private static string GetAbilityName(Ability ability) =>
        ability?.GetIl2CppType()?.Name ?? "Unknown";

    private static bool IsWindDash(Ability ability) =>
        string.Equals(
            GetAbilityName(ability),
            nameof(WindDash),
            StringComparison.Ordinal);

    private bool IsGroundHeightSafe(
        float now,
        BonusStageState state,
        PlayerMovement player)
    {
        float currentY = player.transform.position.y;
        float verticalSpeed = Math.Abs(state.PlayerVelocity.y);
        bool groundedSignal = state.IsGrounded || player.IsGrounded();
        bool stablePhysicsContact =
            groundedSignal && verticalSpeed <= StableGroundVerticalSpeed;
        if (stablePhysicsContact)
        {
            long fixedStep = JumpPhysicsFeedback.FixedStepSequence;
            if (fixedStep != stableGroundLastFixedStep)
            {
                if (stableGroundLastFixedStep >= 0 &&
                    fixedStep - stableGroundLastFixedStep > 2)
                {
                    stableGroundFixedSteps = 0;
                }
                stableGroundLastFixedStep = fixedStep;
                stableGroundFixedSteps++;
            }
            lastPlayerY = currentY;
            hasPlayerY = true;
            verticallyStationarySince = now;
            return stableGroundFixedSteps >= RequiredStableGroundFixedSteps;
        }

        stableGroundLastFixedStep = -1;
        stableGroundFixedSteps = 0;

        if (!hasPlayerY ||
            Math.Abs(currentY - lastPlayerY) >
                StationaryVerticalTolerance ||
            verticalSpeed > StableGroundVerticalSpeed)
        {
            lastPlayerY = currentY;
            hasPlayerY = true;
            verticallyStationarySince = now;
            return false;
        }

        return verticallyStationarySince >= 0f &&
            now - verticallyStationarySince >=
                StationaryGroundFallbackSeconds;
    }

    private static bool IsBowUnlocked()
    {
        PlayerInventory inventory = PlayerInventory.instance;
        if (inventory?.ascensionSkills == null)
            return false;

        foreach (AscensionSkill skill in inventory.ascensionSkills)
        {
            if (skill != null &&
                string.Equals(
                    skill.name,
                    BowSkillName,
                    StringComparison.Ordinal))
            {
                return skill.unlocked;
            }
        }

        return false;
    }

    private void LogWindDashUnavailable(
        float now,
        string reason,
        BonusStageState state,
        Ability selected)
    {
        bool changed = !string.Equals(
            reason,
            lastUnavailableReason,
            StringComparison.Ordinal);
        if (!changed && now < nextUnavailableLogTime)
            return;

        lastUnavailableReason = reason;
        nextUnavailableLogTime = now + RepeatedUnavailableLogSeconds;
        BonusRunnerLog.Debug(
            $"CompletionWindDashUnavailable Section={state.SectionIndex}, " +
            $"Reason={reason}, Selected={GetAbilityName(selected)}, " +
            $"Grounded={state.IsGrounded}, Position=" +
            $"({state.PlayerPosition.x:F3},{state.PlayerPosition.y:F3}), " +
            $"Velocity=({state.PlayerVelocity.x:F3}," +
            $"{state.PlayerVelocity.y:F3}). Other typed-target reward " +
            "actions remain available; terrain navigation no longer owns " +
            "input.",
            "Completion");
    }

    private void ResetState()
    {
        traversalActive = false;
        traversalSection = -1;
        traversalStartedAt = 0f;
        nextRewardJumpTime = 0f;
        nextRewardArrowTime = 0f;
        nextWindDashCheckTime = 0f;
        nextUnavailableLogTime = 0f;
        ResetStableGroundObservation();
        lastUnavailableReason = string.Empty;
        rewardJumpCount = 0;
        rewardArrowCount = 0;
        windDashCount = 0;
    }

    private void ResetStableGroundObservation()
    {
        lastPlayerY = 0f;
        verticallyStationarySince = -1f;
        hasPlayerY = false;
        stableGroundLastFixedStep = -1;
        stableGroundFixedSteps = 0;
    }
}
