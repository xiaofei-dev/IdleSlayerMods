using System;
using AutoAdventurer.Diagnostics;
using AutoAdventurer.World;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Rage;

internal sealed class RageControlService
{
    private const float ManagerResolveIntervalSeconds = 1f;
    private const float StatePollIntervalSeconds = 0.1f;
    private const float KeyPollIntervalSeconds = 0.25f;
    private const float ActivationConfirmationSeconds = 3f;
    private const float BlockerPollIntervalSeconds = 0.5f;
    private const float RepeatedActionLogIntervalSeconds = 30f;

    private RageModeManager manager;
    private readonly WorldInterruptionService interruptions = new();
    private float nextManagerResolveTime;
    private float nextStatePollTime;
    private float nextKeyPollTime;
    private float nextActivationCheckTime;
    private float automaticRageStartedAt;
    private float observationReadyAt;
    private float nextBlockerPollTime;
    private float nextWorldScanTime;
    private float nextRepeatActivationLogTime;
    private float nextCooldownLogTime;
    private bool automaticRageActive;
    private bool endRequested;
    private bool refreshSuppressed;
    private bool managerMissingLogged;
    private bool observationPending;
    private string lastBlocker = string.Empty;

    internal void Tick(float now, bool automationEnabled)
    {
        if (!automationEnabled) return;

        try
        {
            ResolveManager(now);
            if (manager == null) return;

            ObserveWorldBlockers(now);

            if (now >= nextStatePollTime)
            {
                nextStatePollTime = now + StatePollIntervalSeconds;
                ObserveRageState(now);
            }

            if (observationPending)
            {
                if (!CanResumeAfterObservation(now)) return;
                observationPending = false;
                lastBlocker = string.Empty;
                nextActivationCheckTime = now;
            }

            if (IsExecuting())
            {
                CheckAutomaticEndConditions(now);
                if (!endRequested && !refreshSuppressed &&
                    now >= nextActivationCheckTime)
                {
                    nextActivationCheckTime = now + GetActivationInterval();
                    TryActivate(now);
                }
                return;
            }

            if (endRequested || manager.isEnding) return;
            if (now < nextActivationCheckTime) return;

            nextActivationCheckTime = now + GetActivationInterval();
            TryActivate(now);
        }
        catch (Exception exception)
        {
            AdventurerLog.Error($"Automatic Rage failed safely: {exception}");
            ResetTransientState();
        }
    }

    internal void EndImmediately(string reason)
    {
        // This method is intentionally called only by the configured manual
        // stop key. Quest travel suppresses refresh and waits for natural end.
        try
        {
            ResolveManager(Time.unscaledTime, true);
            if (manager == null || !IsExecuting() || manager.isEnding || endRequested)
                return;

            endRequested = true;
            AdventurerLog.User($"Rage Mode end requested. Reason: {reason}");
            manager.StartCoroutine(manager.EndRageMode(false));
        }
        catch (Exception exception)
        {
            endRequested = false;
            AdventurerLog.Error($"Failed to end Rage Mode safely: {exception}");
        }
    }

    private void TryActivate(float now)
    {
        GameStates state = GameState.current;
        if (state != GameStates.RunnerMode && state != GameStates.RageMode)
            return;

        if (interruptions.TryGetBlocker(out string blocker))
        {
            LogWorldBlocker(blocker);
            return;
        }

        if (!string.IsNullOrEmpty(lastBlocker))
        {
            AdventurerLog.Debug($"World blocker cleared: {lastBlocker}.");
            lastBlocker = string.Empty;
        }

        if (manager.currentCd > 0d)
        {
            if (now >= nextCooldownLogTime)
            {
                nextCooldownLogTime = now + RepeatedActionLogIntervalSeconds;
                AdventurerLog.Debug($"Rage activation is cooling down: {manager.currentCd:0.0}s remaining.");
            }
            return;
        }

        bool alreadyAutomaticRage = automaticRageActive && IsExecuting();
        manager.Activate();
        if (!alreadyAutomaticRage)
        {
            automaticRageActive = true;
            automaticRageStartedAt = now;
        }
        endRequested = false;
        refreshSuppressed = false;

        // User-facing activation state is logged only by the K-key toggle.
        // Repeated skill execution remains diagnostic and is rate limited.
        if (alreadyAutomaticRage && now >= nextRepeatActivationLogTime)
        {
            nextRepeatActivationLogTime = now + RepeatedActionLogIntervalSeconds;
            AdventurerLog.Debug("Automatic Rage activated again during execution.");
        }
    }

    private void ObserveWorldBlockers(float now)
    {
        if (now < nextWorldScanTime) return;
        nextWorldScanTime = now + BlockerPollIntervalSeconds;

        if (interruptions.TryGetBlocker(out string blocker))
        {
            LogWorldBlocker(blocker);
            return;
        }

        if (!string.IsNullOrEmpty(lastBlocker))
        {
            AdventurerLog.Debug($"World blocker cleared: {lastBlocker}.");
            lastBlocker = string.Empty;
        }
    }

    private void CheckAutomaticEndConditions(float now)
    {
        if (!automaticRageActive || endRequested || refreshSuppressed) return;

        if (now >= nextKeyPollTime)
        {
            nextKeyPollTime = now + KeyPollIntervalSeconds;
            if (interruptions.HasChestHuntKey)
            {
                refreshSuppressed = true;
                AdventurerLog.Debug(
                    "Chest Hunt Key detected; Rage refresh stopped until the current execution ends naturally.");
                return;
            }
        }

        float maximumDuration = Math.Max(0f,
            Plugin.Config.MaximumRageDurationSeconds.Value);
        if (maximumDuration > 0f &&
            now - automaticRageStartedAt >= maximumDuration)
        {
            refreshSuppressed = true;
            AdventurerLog.Debug(
                $"Maximum automatic Rage duration reached ({maximumDuration:0.#} seconds); " +
                "Rage refresh stopped until the current execution ends naturally.");
        }
    }

    private void ObserveRageState(float now)
    {
        if (IsExecuting()) return;

        // Activate() can return before the game enters Execution. Preserve
        // ownership briefly so the resulting Rage still receives automatic
        // key and duration handling after the transition completes.
        if (automaticRageActive &&
            now - automaticRageStartedAt < ActivationConfirmationSeconds)
            return;

        bool automaticRageJustEnded =
            automaticRageActive || endRequested || refreshSuppressed;
        if (automaticRageJustEnded)
            AdventurerLog.Debug("Rage Mode execution ended.");

        automaticRageActive = false;
        automaticRageStartedAt = 0f;
        endRequested = false;
        refreshSuppressed = false;

        if (automaticRageJustEnded)
        {
            observationPending = true;
            observationReadyAt = now + Math.Max(0f,
                Plugin.Config.PostRageObservationSeconds.Value);
            nextBlockerPollTime = observationReadyAt;
            AdventurerLog.Debug(
                $"Post-Rage observation scheduled for {Plugin.Config.PostRageObservationSeconds.Value:0.#} seconds.");
        }
    }

    private bool CanResumeAfterObservation(float now)
    {
        if (now < observationReadyAt || now < nextBlockerPollTime)
            return false;

        nextBlockerPollTime = now + BlockerPollIntervalSeconds;
        if (!interruptions.TryGetBlocker(out string blocker))
        {
            if (!string.IsNullOrEmpty(lastBlocker))
                AdventurerLog.Debug($"World blocker cleared: {lastBlocker}.");
            return true;
        }

        if (!string.Equals(blocker, lastBlocker, StringComparison.Ordinal))
            LogWorldBlocker(blocker);

        return false;
    }

    private void LogWorldBlocker(string blocker)
    {
        if (string.Equals(blocker, lastBlocker, StringComparison.Ordinal))
            return;

        lastBlocker = blocker;
        AdventurerLog.Debug(
            $"Automatic Rage blocker detected: {blocker}.");
    }

    private bool IsExecuting() =>
        manager != null &&
        manager.currentState == RageModeManager.RageModeStates.Execution;

    private static float GetActivationInterval() =>
        Math.Max(0.1f, Plugin.Config.ActivationCheckIntervalSeconds.Value);

    private void ResolveManager(float now, bool force = false)
    {
        if (manager != null) return;
        if (!force && now < nextManagerResolveTime) return;

        nextManagerResolveTime = now + ManagerResolveIntervalSeconds;
        manager = RageModeManager.instance;
        if (manager == null)
        {
            if (!managerMissingLogged)
            {
                managerMissingLogged = true;
                AdventurerLog.Warning("RageModeManager is not available yet.");
            }
            return;
        }

        managerMissingLogged = false;
        AdventurerLog.Debug("RageModeManager resolved.");
    }

    private void ResetTransientState()
    {
        automaticRageActive = false;
        automaticRageStartedAt = 0f;
        endRequested = false;
        refreshSuppressed = false;
        observationPending = false;
        observationReadyAt = 0f;
        nextBlockerPollTime = 0f;
        nextWorldScanTime = 0f;
        nextRepeatActivationLogTime = 0f;
        nextCooldownLogTime = 0f;
        lastBlocker = string.Empty;
        interruptions.Reset();
        nextStatePollTime = 0f;
        nextKeyPollTime = 0f;
    }

    internal void Reset()
    {
        manager = null;
        nextManagerResolveTime = 0f;
        nextActivationCheckTime = 0f;
        managerMissingLogged = false;
        ResetTransientState();
    }
}
