using System;
using IdleSlayerMods.Common.Extensions;
using MelonLoader;
using Il2Cpp;
using UnityEngine;


namespace AutoBuy500x;

public class MyBehaviour : MonoBehaviour
{
    // Purchase slightly before the 4-minute and 8-minute marks.
    private const float SoulsIntervalSeconds = 237f;
    private const float CpsIntervalSeconds = 477f;

    // Check Rage Pill every 10 seconds.
    private const float RagePillCheckIntervalSeconds = 10f;

    // Keep Rage Pill checks at least 5 seconds away while a
    // Special Random Box or Chest Hunt Key is present.
    private const float RageBlockDelaySeconds = 5f;

    private const string SpecialRandomBoxObjectName =
        "Special Random Box(Clone)";

    private const string ChestHuntKeyObjectName =
        "Chest Hunt Key(Clone)";

    // PopupBuyMaterials index:
    // 1 = 25%
    // 2 = 50%
    // 3 = 100%
    private const int BuyMaxMaterialIndex = 3;

    // Only refill an individual material when its amount
    // is at or below this threshold.
    private const double MaterialRefillThreshold = 200d;

    // Only inspect material amounts when crafting leaves
    // exactly this many additional Rage Pills available.
    private const int MaterialCheckCraftableCount = 1;

    private const string RootInternalName = "drop_root";
    private const string SlimeInternalName = "drop_slime";

    // L controls automatic 500x purchases and Rage Pill crafting.
    private bool autoBuyEnabled;

    // K controls material refill and Rage activation
    // after successful Rage Pill crafting.
    private bool autoRageAfterPillEnabled;

    // Prevent repeated Special Random Box log messages.
    private bool specialBoxSkipLogged;

    private float nextSoulsPurchaseTime;
    private float nextCpsPurchaseTime;
    private float nextRagePillCheckTime;

    private JewelsOfSoulSoulsBonus soulsBonus;
    private JewelsOfSoulCPSBonus cpsBonus;

    private TemporaryCraftableItem ragePill;
    private RageModeManager rageModeManager;

    public void Awake()
    {
        FindBonusComponents();
        FindRagePill();
        FindRageModeManager();

        Plugin.Logger.Msg(
            soulsBonus != null
                ? "Souls bonus component found."
                : "Souls bonus component not found."
        );

        Plugin.Logger.Msg(
            cpsBonus != null
                ? "CPS bonus component found."
                : "CPS bonus component not found."
        );

        Plugin.Logger.Msg(
            ragePill != null
                ? "Rage Pill component found."
                : "Rage Pill component not found yet."
        );

        Plugin.Logger.Msg(
            rageModeManager != null
                ? "Rage Mode Manager found."
                : "Rage Mode Manager not found yet."
        );
    }

    public void Start()
    {
        Plugin.Logger.Msg(
            "Press L to toggle Auto Buy and Auto Rage Pill."
        );

        Plugin.Logger.Msg(
            "Press K to toggle material refill and Rage activation after pill crafting."
        );
    }

    public void Update()
    {
        // Skip the entire mod outside normal runner gameplay.
        if (!GameState.IsRunner())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            ToggleAutoBuy();
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            ToggleAutoRageAfterPill();
        }

        if (!autoBuyEnabled)
        {
            return;
        }

        float currentTime = Time.time;

        // Detect the blue box and Chest Hunt Key every frame,
        // independently of the Rage Pill check interval.
        DelayRagePillCheckForBlockingObjects(currentTime);

        if (currentTime >= nextSoulsPurchaseTime)
        {
            PurchaseSoulsBonus();

            nextSoulsPurchaseTime =
                currentTime + SoulsIntervalSeconds;
        }

        if (currentTime >= nextCpsPurchaseTime)
        {
            PurchaseCpsBonus();

            nextCpsPurchaseTime =
                currentTime + CpsIntervalSeconds;
        }

        if (currentTime >= nextRagePillCheckTime)
        {
            CheckAndCraftRagePill();

            nextRagePillCheckTime =
                currentTime + RagePillCheckIntervalSeconds;
        }
    }

    private void DelayRagePillCheckForBlockingObjects(
        float currentTime)
    {
        if (!IsRageBlockingObjectPresent())
        {
            specialBoxSkipLogged = false;
            return;
        }

        nextRagePillCheckTime = Mathf.Max(
            nextRagePillCheckTime,
            currentTime + RageBlockDelaySeconds
        );

        if (!specialBoxSkipLogged)
        {
            Plugin.Logger.Msg(
                "Special Random Box or Chest Hunt Key detected. " +
                "Rage Pill check delayed."
            );

            specialBoxSkipLogged = true;
        }
    }

    private bool IsRageBlockingObjectPresent()
    {
        return GameObject.Find(SpecialRandomBoxObjectName) != null
            || GameObject.Find(ChestHuntKeyObjectName) != null;
    }

    private void CheckAndCraftRagePill()
    {
        if (IsRageBlockingObjectPresent())
        {
            return;
        }

        TryCraftRagePill();
    }

    private void ToggleAutoBuy()
    {
        autoBuyEnabled = !autoBuyEnabled;

        if (autoBuyEnabled)
        {
            PurchaseSoulsBonus();
            PurchaseCpsBonus();

            // Use the same Special Random Box protection
            // during initial activation.
            CheckAndCraftRagePill();

            float currentTime = Time.time;

            nextSoulsPurchaseTime =
                currentTime + SoulsIntervalSeconds;

            nextCpsPurchaseTime =
                currentTime + CpsIntervalSeconds;

            nextRagePillCheckTime =
                currentTime + RagePillCheckIntervalSeconds;
        }
        else
        {
            specialBoxSkipLogged = false;
        }

        string message = autoBuyEnabled
            ? "Auto Buy Activated!"
            : "Auto Buy Deactivated!";

        Plugin.Logger.Msg(message);

        if (Plugin.ModHelperInstance != null)
        {
            Plugin.ModHelperInstance.ShowNotification(
                message,
                autoBuyEnabled
            );
        }
    }

    private void ToggleAutoRageAfterPill()
    {
        autoRageAfterPillEnabled =
            !autoRageAfterPillEnabled;

        string message = autoRageAfterPillEnabled
            ? "Auto Rage After Pill Activated!"
            : "Auto Rage After Pill Deactivated!";

        Plugin.Logger.Msg(message);

        if (autoRageAfterPillEnabled)
        {
            if (ragePill == null)
            {
                FindRagePill();
            }

            if (ragePill == null)
            {
                Plugin.Logger.Warning(
                    "Rage Pill component is unavailable during K initialization."
                );
            }
            else if (ragePill.HowManyCanCraft() <= 0)
            {
                Plugin.Logger.Msg(
                    "No Rage Pills can currently be crafted. " +
                    "Checking Root and Slime refill."
                );

                BuyRagePillMaterials();
            }

            // Do not activate Rage while a blue box or key exists.
            if (IsRageBlockingObjectPresent())
            {
                Plugin.Logger.Msg(
                    "Special Random Box or Chest Hunt Key detected. " +
                    "Initial Rage activation skipped."
                );
            }
            else
            {
                ActivateRage();
            }
        }

        if (Plugin.ModHelperInstance != null)
        {
            Plugin.ModHelperInstance.ShowNotification(
                message,
                autoRageAfterPillEnabled
            );
        }
    }

    private void FindBonusComponents()
    {
        if (soulsBonus == null)
        {
            soulsBonus =
                UnityEngine.Object.FindObjectOfType<
                    JewelsOfSoulSoulsBonus
                >();
        }

        if (cpsBonus == null)
        {
            cpsBonus =
                UnityEngine.Object.FindObjectOfType<
                    JewelsOfSoulCPSBonus
                >();
        }
    }

    private void FindRagePill()
    {
        TemporaryCraftableItem[] items =
            Resources.FindObjectsOfTypeAll<
                TemporaryCraftableItem
            >();

        foreach (TemporaryCraftableItem item in items)
        {
            if (item == null)
            {
                continue;
            }

            if (item.name != "craftable_item_rage_pill")
            {
                continue;
            }

            ragePill = item;

            Plugin.Logger.Msg(
                "Rage Pill component found."
            );

            return;
        }

        Plugin.Logger.Warning(
            "Rage Pill component was not found."
        );
    }

    private void FindRageModeManager()
    {
        rageModeManager =
            RageModeManager.instance;

        if (rageModeManager == null)
        {
            Plugin.Logger.Warning(
                "Rage Mode Manager was not found."
            );
        }
    }

    private void PurchaseSoulsBonus()
    {
        if (soulsBonus == null)
        {
            FindBonusComponents();
        }

        if (soulsBonus == null)
        {
            Plugin.Logger.Warning(
                "Souls bonus component is unavailable."
            );

            return;
        }

        soulsBonus._PurchaseHandler_b__10_0();

        Plugin.Logger.Msg(
            "Soul 500x purchase handler called."
        );
    }

    private void PurchaseCpsBonus()
    {
        if (cpsBonus == null)
        {
            FindBonusComponents();
        }

        if (cpsBonus == null)
        {
            Plugin.Logger.Warning(
                "CPS bonus component is unavailable."
            );

            return;
        }

        cpsBonus._PurchaseHandler_b__10_0();

        Plugin.Logger.Msg(
            "CPS 500x purchase handler called."
        );
    }

    private void TryCraftRagePill()
    {
        if (ragePill == null)
        {
            FindRagePill();
        }

        if (ragePill == null)
        {
            Plugin.Logger.Msg(
                "Rage Pill component is unavailable."
            );

            return;
        }

        // Rage does not currently need refreshing.
        if (!ragePill.ExtraCondition())
        {
            return;
        }

        // Not enough materials.
        if (ragePill.HowManyCanCraft() <= 0)
        {
            return;
        }

        ragePill.Craft();

        if (!autoRageAfterPillEnabled)
        {
            return;
        }

        int craftableAfterCraft =
            (int)ragePill.HowManyCanCraft();

        // Do not inspect materials after every craft.
        // Only inspect them when one additional Rage Pill remains.
        if (craftableAfterCraft == MaterialCheckCraftableCount)
        {
            // Plugin.Logger.Msg(
            //     "One Rage Pill remains craftable. " +
            //     "Checking individual material amounts."
            // );

            BuyRagePillMaterials();
        }

        // Check again because a blue box or key could appear
        // while the Rage Pill is being crafted.
        if (IsRageBlockingObjectPresent())
        {
            Plugin.Logger.Msg(
                "Special Random Box or Chest Hunt Key detected " +
                "after pill crafting. Rage activation skipped."
            );

            return;
        }

        ActivateRage();
    }

    private void BuyRagePillMaterials()
    {
        bool slimePurchased = false;
        bool rootPurchased = false;

        double slimeAmount =
            GetMaterialAmount(SlimeInternalName);

        // Plugin.Logger.Msg(
        //     $"Current Slime amount: {slimeAmount}."
        // );

        if (slimeAmount <= MaterialRefillThreshold)
        {
            slimePurchased =
                BuyMaterialToMax(SlimeInternalName);
        }
        // else
        // {
        //     Plugin.Logger.Msg(
        //         "Slime is above the refill threshold. " +
        //         "Slime purchase skipped."
        //     );
        // }

        double rootAmount =
            GetMaterialAmount(RootInternalName);

        // Plugin.Logger.Msg(
        //     $"Current Root amount: {rootAmount}."
        // );

        if (rootAmount <= MaterialRefillThreshold)
        {
            rootPurchased =
                BuyMaterialToMax(RootInternalName);
        }
        // else
        // {
        //     Plugin.Logger.Msg(
        //         "Root is above the refill threshold. " +
        //         "Root purchase skipped."
        //     );
        // }

        if (!slimePurchased && !rootPurchased)
        {
            Plugin.Logger.Msg(
                "No Rage Pill materials required refilling."
            );

            return;
        }

        Plugin.Logger.Msg(
            $"Rage Pill material refill completed. " +
            $"Slime: {slimePurchased}, Root: {rootPurchased}."
        );
    }

    private double GetMaterialAmount(string internalName)
    {
        Drop material =
            FindDrop(internalName);

        if (material == null)
        {
            Plugin.Logger.Warning(
                $"Material was not found: {internalName}."
            );

            // Returning MaxValue prevents an accidental purchase
            // when the material object cannot be found.
            return double.MaxValue;
        }

        return material.amount;
    }

    private bool BuyMaterialToMax(string internalName)
    {
        try
        {
            Drop material =
                FindDrop(internalName);

            if (material == null)
            {
                Plugin.Logger.Error(
                    $"Material was not found: {internalName}."
                );

                return false;
            }

            PopupBuyMaterials popup =
                PopupBuyMaterials.instance;

            if (popup == null)
            {
                Plugin.Logger.Error(
                    "PopupBuyMaterials.instance is unavailable."
                );

                return false;
            }

            popup.selectedMat = material;

            popup.SelectIndex(
                BuyMaxMaterialIndex
            );

            popup.Confirm();

            // Plugin.Logger.Msg(
            //     $"Material purchased to 100%: " +
            //     $"{material} [{internalName}]."
            // );

            return true;
        }
        catch (Exception exception)
        {
            Plugin.Logger.Error(
                $"Failed to purchase material " +
                $"'{internalName}': {exception}"
            );

            return false;
        }
    }

    private Drop FindDrop(string internalName)
    {
        Drop[] drops =
            Resources.FindObjectsOfTypeAll<Drop>();

        foreach (Drop drop in drops)
        {
            if (drop == null)
            {
                continue;
            }

            if (string.Equals(
                    drop.name,
                    internalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return drop;
            }
        }

        Plugin.Logger.Error(
            $"Material data was not found: {internalName}. " +
            $"Loaded Drop count: {drops.Length}."
        );

        return null;
    }

    private void ActivateRage()
    {
        if (!GameState.IsRunner())
        {
            Plugin.Logger.Msg(
                "Rage activation skipped because runner mode is inactive."
            );

            return;
        }

        if (rageModeManager == null)
        {
            FindRageModeManager();
        }

        if (rageModeManager == null)
        {
            Plugin.Logger.Warning(
                "Rage Mode Manager is unavailable."
            );

            return;
        }

        if (rageModeManager.currentCd > 0)
        {
            Plugin.Logger.Warning(
                "Rage cooldown is still active."
            );

            return;
        }

        rageModeManager.Activate();
    }
}
