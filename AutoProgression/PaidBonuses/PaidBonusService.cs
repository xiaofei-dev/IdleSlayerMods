using Il2Cpp;
using UnityEngine;
using AutoProgression.Diagnostics;

namespace AutoProgression.PaidBonuses;

internal sealed class PaidBonusService
{
    private const float RefreshThresholdSeconds = 3f;
    private const float RetrySeconds = 5f;
    private const float PurchaseConfirmationSeconds = 1f;

    private JewelsOfSoulSoulsBonus soulsPurchase;
    private JewelsOfSoulCPSBonus cpsPurchase;
    private PaidSoulsBonus soulsEffect;
    private PaidCpSBonus cpsEffect;
    private PaidBonusController soulsController;
    private PaidBonusController cpsController;
    private bool missingObjectsLogged;
    private bool disabledLogged;

    internal void Tick(float now)
    {
        var config = Plugin.Config;
        if (!config.EnablePaidBonuses.Value)
        {
            if (!disabledLogged)
            {
                ProgressionLog.Debug("Paid 500x purchases are disabled by configuration.");
                disabledLogged = true;
            }
            return;
        }
        disabledLogged = false;
        if (!ResolveObjects()) return;

        soulsController ??= new PaidBonusController(
            "Souls 500x",
            RefreshThresholdSeconds,
            () => soulsPurchase._PurchaseHandler_b__10_0(),
            GetSoulsTimeLeft);

        cpsController ??= new PaidBonusController(
            "CPS 500x",
            RefreshThresholdSeconds,
            () => cpsPurchase._PurchaseHandler_b__10_0(),
            GetCpsTimeLeft);

        soulsController.Tick(now, RetrySeconds, PurchaseConfirmationSeconds);
        cpsController.Tick(now, RetrySeconds, PurchaseConfirmationSeconds);
    }

    private bool ResolveObjects()
    {
        // These handlers live under UI objects that can be inactive while the
        // panel is closed. Include inactive objects, but only accept components
        // belonging to the currently loaded scene (never prefab/resource assets).
        soulsPurchase ??= FindLoadedSceneComponent<JewelsOfSoulSoulsBonus>();
        cpsPurchase ??= FindLoadedSceneComponent<JewelsOfSoulCPSBonus>();
        soulsEffect ??= UnityEngine.Object.FindObjectOfType<PaidSoulsBonus>();
        cpsEffect ??= UnityEngine.Object.FindObjectOfType<PaidCpSBonus>();

        bool ready = soulsPurchase != null && cpsPurchase != null;
        if (!ready && !missingObjectsLogged)
        {
            ProgressionLog.Debug(
                $"500x objects unavailable. SoulsPurchase={soulsPurchase != null}, " +
                $"CpsPurchase={cpsPurchase != null}, SoulsEffect={soulsEffect != null}, " +
                $"CpsEffect={cpsEffect != null}.");
            missingObjectsLogged = true;
        }
        else if (ready)
        {
            missingObjectsLogged = false;
        }

        return ready;
    }

    private static T FindLoadedSceneComponent<T>() where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component == null || component.gameObject == null) continue;

            UnityEngine.SceneManagement.Scene scene = component.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded) return component;
        }

        return null;
    }

    private float GetSoulsTimeLeft()
    {
        soulsEffect ??= UnityEngine.Object.FindObjectOfType<PaidSoulsBonus>();
        return soulsEffect != null && soulsEffect.IsActive() ? (float)soulsEffect.timeLeft : 0f;
    }

    private float GetCpsTimeLeft()
    {
        cpsEffect ??= UnityEngine.Object.FindObjectOfType<PaidCpSBonus>();
        return cpsEffect != null && cpsEffect.IsActive() ? (float)cpsEffect.timeLeft : 0f;
    }

    internal void Reset()
    {
        soulsController?.Reset();
        cpsController?.Reset();
    }
}
