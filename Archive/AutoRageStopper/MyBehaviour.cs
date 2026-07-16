using IdleSlayerMods.Common.Extensions;
using MelonLoader;
using UnityEngine;
using Il2Cpp;

namespace AutoRageStopper
{
    public class MyBehaviour : MonoBehaviour
    {
        private const float ItemCheckIntervalSeconds = 0.5f;
        private const float StateCheckIntervalSeconds = 0.10f;
        private const float ManagerResolveIntervalSeconds = 1.00f;
        private const float ItemEndDelaySeconds = 10f;
        private const float MaximumRageDurationSeconds = 120f;

        private RageModeManager rageModeManager;

        private RageModeManager.RageModeStates lastRageState;

        private float rageStartTime;
        private float nextItemCheckTime;
        private float nextStateCheckTime;
        private float nextManagerResolveTime;

        private bool rageStateInitialized;
        private bool autoEndRequested;
        private bool managerMissingLogged;

        private bool pendingItemEnd;
        private float pendingItemEndTime;
        private string pendingItemReason = "";

        public void Start()
        {
            TryResolveRageModeManager();
            Plugin.Logger.Msg("AutoRageStopper started.");
        }

        public void Update()
        {
            if (!IsSupportedGameState())
            {
                ResetRuntimeState();
                return;
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                CancelPendingItemEnd();
                EndRageImmediately("Manual J key");
                return;
            }

            if (rageModeManager == null)
            {
                TryResolveRageModeManager();

                if (rageModeManager == null)
                {
                    return;
                }
            }

            if (Time.unscaledTime < nextStateCheckTime)
            {
                return;
            }

            nextStateCheckTime =
                Time.unscaledTime + StateCheckIntervalSeconds;

            UpdateRageState();

            if (!IsRageExecuting())
            {
                return;
            }

            if (pendingItemEnd)
            {
                if (Time.unscaledTime >= pendingItemEndTime)
                {
                    string reason = pendingItemReason;

                    CancelPendingItemEnd();
                    EndRageImmediately(reason);
                }

                return;
            }

            CheckMaximumRageDuration();

            if (autoEndRequested)
            {
                return;
            }

            if (Time.unscaledTime < nextItemCheckTime)
            {
                return;
            }

            nextItemCheckTime =
                Time.unscaledTime + ItemCheckIntervalSeconds;

            CheckForKeyOrSpecialBox();
        }

        private static bool IsSupportedGameState()
        {
            GameStates state = GameState.current;

            return state == GameStates.RunnerMode ||
                state == GameStates.RageMode;
        }

        private void TryResolveRageModeManager()
        {
            if (Time.unscaledTime < nextManagerResolveTime)
            {
                return;
            }

            nextManagerResolveTime =
                Time.unscaledTime + ManagerResolveIntervalSeconds;

            // RageModeManager is a game singleton. A scene-wide object scan
            // can bind a stale or transitional component during Game scene
            // startup, so only use the authoritative instance.
            rageModeManager = RageModeManager.instance;

            if (rageModeManager == null)
            {
                if (!managerMissingLogged)
                {
                    managerMissingLogged = true;
                    Plugin.Logger.Warning(
                        "RageModeManager is not available yet."
                    );
                }

                return;
            }

            managerMissingLogged = false;
            rageStateInitialized = false;
            nextStateCheckTime = 0f;

            Plugin.Logger.Msg(
                "RageModeManager found."
            );
        }

        private void UpdateRageState()
        {
            RageModeManager.RageModeStates currentState =
                rageModeManager.currentState;

            if (!rageStateInitialized)
            {
                lastRageState = currentState;
                rageStateInitialized = true;

                if (currentState == RageModeManager.RageModeStates.Execution)
                {
                    StartRageTracking();
                }
                else
                {
                    ResetRageTracking();
                }

                return;
            }

            if (currentState == lastRageState)
            {
                return;
            }

            lastRageState = currentState;

            if (currentState == RageModeManager.RageModeStates.Execution)
            {
                StartRageTracking();
            }
            else
            {
                ResetRageTracking();
            }
        }

        private void StartRageTracking()
        {
            rageStartTime = Time.unscaledTime;
            nextItemCheckTime = Time.unscaledTime;

            autoEndRequested = false;
            CancelPendingItemEnd();

            // Plugin.Logger.Msg(
            //     "Rage timer started."
            // );
        }

        private void ResetRageTracking()
        {
            rageStartTime = 0f;
            nextItemCheckTime = 0f;

            autoEndRequested = false;
            CancelPendingItemEnd();
        }

        private bool IsRageExecuting()
        {
            return rageModeManager != null &&
                rageModeManager.currentState ==
                    RageModeManager.RageModeStates.Execution;
        }

        private void CheckMaximumRageDuration()
        {
            if (autoEndRequested)
            {
                return;
            }

            float elapsedTime =
                Time.unscaledTime - rageStartTime;

            if (elapsedTime < MaximumRageDurationSeconds)
            {
                return;
            }

            autoEndRequested = true;

            EndRageImmediately(
                "Maximum Rage duration reached: 120 seconds."
            );
        }

        private void CheckForKeyOrSpecialBox()
        {
            if (autoEndRequested || pendingItemEnd)
            {
                return;
            }

            GameObject chestHuntKey =
                GameObject.Find("Chest Hunt Key(Clone)");

            if (chestHuntKey != null)
            {
                ScheduleItemEnd(
                    "Chest Hunt Key detected."
                );

                return;
            }

            GameObject specialRandomBox =
                GameObject.Find("Special Random Box(Clone)");

            if (specialRandomBox != null)
            {
                ScheduleItemEnd(
                    "Special Random Box detected."
                );
            }
        }

        private void ScheduleItemEnd(string reason)
        {
            pendingItemEnd = true;
            pendingItemReason = reason;

            pendingItemEndTime =
                Time.unscaledTime + ItemEndDelaySeconds;

            Plugin.Logger.Msg(
                reason +
                " Rage will end in 10 seconds."
            );
        }

        private void CancelPendingItemEnd()
        {
            pendingItemEnd = false;
            pendingItemEndTime = 0f;
            pendingItemReason = "";
        }

        private void EndRageImmediately(string reason)
        {
            if (!IsSupportedGameState())
            {
                ResetRuntimeState();
                return;
            }

            if (rageModeManager == null)
            {
                TryResolveRageModeManager();

                if (rageModeManager == null)
                {
                    return;
                }
            }

            RageModeManager.RageModeStates currentState =
                rageModeManager.currentState;

            if (currentState != RageModeManager.RageModeStates.Execution)
            {
                return;
            }

            if (rageModeManager.isEnding)
            {
                return;
            }

            autoEndRequested = true;

            Plugin.Logger.Msg(
                "Rage Mode end requested. Reason: " +
                reason
            );

            rageModeManager.StartCoroutine(
                rageModeManager.EndRageMode(false)
            );
        }

        private void ResetRuntimeState()
        {
            ResetRageTracking();
            rageStateInitialized = false;
            nextStateCheckTime = 0f;
        }

        public void OnDisable()
        {
            ResetRuntimeState();
            rageModeManager = null;
            nextManagerResolveTime = 0f;
            managerMissingLogged = false;
        }
    }
}
