using System;
using AutoProgression.Diagnostics;
using AutoProgression.Runtime;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Casino;

internal sealed class CrawlerEyePurchaseService
{
    private const string ReptileEyeDropName = "drop_reptile_eye";
    private const float PurchaseTimeoutSeconds = 10f;
    private const float NextPurchaseDelaySeconds = 0.05f;

    private CasinoRewardButton activeButton;
    private double amountBeforePurchase;
    private int targetAmount;
    private int purchasedAmount;
    private float requestDeadline;
    private float nextPurchaseAt;
    private bool purchasePending;
    private bool invalidKeyLogged;

    internal void Tick(float now)
    {
        if (!Plugin.Config.EnableCrawlerEyeBulkPurchase.Value)
        {
            Cancel(false);
            return;
        }

        KeyCode key = ResolveKey();
        if (Input.GetKeyDown(key))
        {
            if (targetAmount > 0)
                Notify("A Crawler Eye bulk purchase is already running.", false);
            else
                Begin(now);
        }

        if (targetAmount <= 0)
            return;

        if (purchasePending)
        {
            double currentAmount = GetEyeAmount(activeButton);
            if (currentAmount > amountBeforePurchase)
            {
                purchasedAmount += Math.Max(1, (int)Math.Round(currentAmount - amountBeforePurchase));
                purchasePending = false;
                nextPurchaseAt = now + NextPurchaseDelaySeconds;
            }
            else if (now >= requestDeadline)
            {
                Finish("the native purchase did not complete before the safety timeout");
            }
            return;
        }

        if (purchasedAmount >= targetAmount)
        {
            Finish(null);
            return;
        }

        if (now >= nextPurchaseAt)
            RequestNextPurchase(now);
    }

    private void Begin(float now)
    {
        CasinoRewardButton button = FindVisibleCrawlerEyeButton();
        if (button == null)
        {
            Notify("Open the Village Casino Crawler Eye purchase screen first.", false);
            return;
        }

        int configured = Math.Clamp(Plugin.Config.CrawlerEyesPerPress.Value, 10, 100000);
        targetAmount = configured - configured % 10;
        if (targetAmount <= 0)
            targetAmount = 10;

        activeButton = button;
        purchasedAmount = 0;
        nextPurchaseAt = now;
        ProgressionLog.User(
            $"Crawler Eye bulk purchase started: target={targetAmount}, key={ResolveKey()}.");
    }

    private void RequestNextPurchase(float now)
    {
        CasinoRewardButton button = FindVisibleCrawlerEyeButton();
        if (button == null)
        {
            Finish("the Crawler Eye purchase screen was closed or became unavailable");
            return;
        }

        double jewelCost = button.reward?.josNeeded ?? 0d;
        if (jewelCost <= 0d ||
            (PlayerInventory.instance?.jewelsOfSoul ?? 0d) < jewelCost)
        {
            Finish("there were not enough Jewels of Soul");
            return;
        }

        double before = GetEyeAmount(button);
        try
        {
            activeButton = button;
            amountBeforePurchase = before;
            purchasePending = true;
            requestDeadline = now + PurchaseTimeoutSeconds;
            // The configured key is the user's explicit confirmation for this
            // manual bulk action. Invoke the button's native confirmed action
            // directly so a modal is not opened for every 10-eye purchase.
            button._BuyWithJewelsOfSoul_b__12_0();
        }
        catch (Exception exception)
        {
            ProgressionLog.Exception(
                $"Crawler Eye bulk purchase after {purchasedAmount} eye(s)",
                exception);
            Cancel(false);
        }
    }

    private static CasinoRewardButton FindVisibleCrawlerEyeButton()
    {
        foreach (CasinoRewardButton button in Resources.FindObjectsOfTypeAll<CasinoRewardButton>())
        {
            if (button == null || !button.gameObject.scene.IsValid() ||
                !button.gameObject.scene.isLoaded ||
                !button.gameObject.activeInHierarchy ||
                button.reward?.grantDrop == null ||
                !string.Equals(button.reward.grantDrop.name, ReptileEyeDropName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return button;
        }

        return null;
    }

    private static double GetEyeAmount(CasinoRewardButton button) =>
        button?.reward?.grantDrop?.amount ?? 0d;

    private KeyCode ResolveKey()
    {
        string configured = Plugin.Config.CrawlerEyePurchaseKey.Value;
        if (ConfiguredKey.TryResolve(configured, out KeyCode key))
        {
            invalidKeyLogged = false;
            return key;
        }

        if (!invalidKeyLogged)
        {
            ProgressionLog.Warning(
                $"Invalid Crawler Eye purchase key '{configured}'. Falling back to M.");
            invalidKeyLogged = true;
        }
        return KeyCode.M;
    }

    private void Finish(string reason)
    {
        string suffix = string.IsNullOrEmpty(reason) ? string.Empty : $"; stopped because {reason}";
        ProgressionLog.User(
            $"Crawler Eye bulk purchase completed: purchased={purchasedAmount}/{targetAmount}{suffix}.");
        Notify($"Purchased {purchasedAmount} Crawler Eyes", purchasedAmount > 0);
        Cancel(false);
    }

    private void Cancel(bool clearKeyWarning)
    {
        activeButton = null;
        amountBeforePurchase = 0d;
        targetAmount = 0;
        purchasedAmount = 0;
        requestDeadline = 0f;
        nextPurchaseAt = 0f;
        purchasePending = false;
        if (clearKeyWarning)
            invalidKeyLogged = false;
    }

    private static void Notify(string message, bool positive)
    {
        ProgressionLog.Debug(message);
        Plugin.ModHelperInstance?.ShowNotification(message, positive);
    }

    internal void Reset() => Cancel(true);
}
