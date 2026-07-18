using System;
using Il2Cpp;
using UnityEngine;
using AutoProgression.Diagnostics;

namespace AutoProgression.Materials;

internal sealed class MaterialPurchaseService
{
    internal bool Buy(string internalName, int purchasePercent)
    {
        Drop material = FindDrop(internalName);
        return material != null && Buy(material, purchasePercent);
    }

    internal bool Buy(Drop material, int purchasePercent)
    {
        try
        {
            PopupBuyMaterials popup = PopupBuyMaterials.instance;
            if (material == null || popup == null)
            {
                ProgressionLog.Debug(
                    $"Material purchase objects unavailable. Material={material != null}, Popup={popup != null}.");
                return false;
            }

            popup.selectedMat = material;
            popup.SelectIndex(ToPopupIndex(purchasePercent));
            popup.Confirm();
            ProgressionLog.User($"Material purchased with Jewels of Soul: {material.name} ({NormalizePercent(purchasePercent)}%).");
            return true;
        }
        catch (Exception exception)
        {
            ProgressionLog.Error($"Failed to purchase material '{material?.name}': {exception}");
            return false;
        }
    }

    private static Drop FindDrop(string internalName)
    {
        foreach (Drop drop in Resources.FindObjectsOfTypeAll<Drop>())
        {
            if (drop != null && string.Equals(drop.name, internalName, StringComparison.OrdinalIgnoreCase))
                return drop;
        }

        ProgressionLog.Warning($"Material was not found: {internalName}.");
        return null;
    }

    private static int ToPopupIndex(int percent) => NormalizePercent(percent) switch
    {
        25 => 1,
        50 => 2,
        _ => 3
    };

    private static int NormalizePercent(int percent) => percent is 25 or 50 or 100 ? percent : 100;
}
