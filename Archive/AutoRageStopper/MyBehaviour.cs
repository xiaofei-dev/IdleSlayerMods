using IdleSlayerMods.Common.Extensions;
using MelonLoader;
using UnityEngine;
using Il2Cpp;

namespace AutoRageStopper
{
    public class MyBehaviour : MonoBehaviour
    {
        private const float ItemCheckIntervalSeconds = 0.5f;
        private const float ItemEndDelaySeconds = 10f;
        private const float MaximumRageDurationSeconds = 120f;

        private RageModeManager rageModeManager;

        private string lastRageState = "";

        private float rageStartTime;
        private float nextItemCheckTime;

        private bool rageStateInitialized;
        private bool autoEndRequested;

        private bool pendingItemEnd;
        private float pendingItemEndTime;
        private string pendingItemReason = "";

        public void Start()
        {
            FindRageModeManager();

            Plugin.Logger.Msg("AutoClimber started.");
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.J))
            {
                CancelPendingItemEnd();
                EndRageImmediately("Manual J key");
                return;
            }

            if (rageModeManager == null)
            {
                FindRageModeManager();

                if (rageModeManager == null)
                {
                    return;
                }
            }

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

        private void FindRageModeManager()
        {
            rageModeManager =
                UnityEngine.Object.FindObjectOfType<RageModeManager>();

            if (rageModeManager == null)
            {
                Plugin.Logger.Warning(
                    "RageModeManager was not found."
                );

                return;
            }

            rageStateInitialized = false;
            lastRageState = "";

            Plugin.Logger.Msg(
                "RageModeManager found."
            );
        }

        private void UpdateRageState()
        {
            string currentState =
                rageModeManager.currentState.ToString();

            if (!rageStateInitialized)
            {
                lastRageState = currentState;
                rageStateInitialized = true;

                if (currentState == "Execution")
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

            if (currentState == "Execution")
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
                   rageModeManager.currentState.ToString() ==
                       "Execution";
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
            if (rageModeManager == null)
            {
                FindRageModeManager();

                if (rageModeManager == null)
                {
                    return;
                }
            }

            string currentState =
                rageModeManager.currentState.ToString();

            if (currentState != "Execution")
            {
                return;
            }

            if (rageModeManager.isEnding)
            {
                return;
            }

            autoEndRequested = true;

            // Plugin.Logger.Msg(
            //     "Rage Mode end requested. Reason: " +
            //     reason
            // );

            rageModeManager.StartCoroutine(
                rageModeManager.EndRageMode(false)
            );
        }
    }
}
