using System;
using AutoProgression.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Armory;

internal sealed class ArmoryBoxOpeningService
{
    private TemporaryCraftableItem selectedBox;
    private bool invalidSelectKeyLogged;
    private bool invalidOpenKeyLogged;
    private bool duplicateKeysLogged;

    internal void Tick()
    {
        KeyCode selectKey = ResolveKey(
            Plugin.Config.ArmoryBoxSelectKey.Value,
            KeyCode.I,
            "select",
            ref invalidSelectKeyLogged);
        KeyCode openKey = ResolveKey(
            Plugin.Config.ArmoryBoxOpenKey.Value,
            KeyCode.O,
            "open",
            ref invalidOpenKeyLogged);

        if (selectKey == openKey)
        {
            if (!duplicateKeysLogged)
            {
                ProgressionLog.Warning(
                    $"Armory box select and open keys are both '{selectKey}'. Configure different keys before using this feature.");
                duplicateKeysLogged = true;
            }
            return;
        }

        duplicateKeysLogged = false;
        if (Input.GetKeyDown(selectKey)) SelectHighlightedBox();
        if (Input.GetKeyDown(openKey)) OpenSelectedBoxes();
    }

    private void SelectHighlightedBox()
    {
        TemporaryCraftableItem selected = null;
        foreach (TemporaryCraftableItemButton button in
                 Resources.FindObjectsOfTypeAll<TemporaryCraftableItemButton>())
        {
            if (button == null || !button.gameObject.scene.IsValid() ||
                !button.gameObject.scene.isLoaded ||
                !button.gameObject.activeInHierarchy ||
                button.selectedOverlay == null ||
                !button.selectedOverlay.gameObject.activeInHierarchy)
                continue;

            selected = button.item;
            break;
        }

        if (!IsArmoryBox(selected))
        {
            Notify("Select one of the five Armory boxes first.", false);
            return;
        }

        selectedBox = selected;
        string name = GetDisplayName(selected);
        ProgressionLog.User($"Armory box selected: {name}.");
        Notify($"Selected Armory box: {name}", true);
    }

    private void OpenSelectedBoxes()
    {
        if (!IsArmoryBox(selectedBox))
        {
            selectedBox = null;
            Notify("No Armory box selected.", false);
            return;
        }

        LootBoxManager manager = LootBoxManager.instance;
        if (manager != null && manager.IsBeingOpened())
        {
            Notify("Wait for the current loot-box animation to finish.", false);
            return;
        }

        int requested = Math.Clamp(Plugin.Config.ArmoryBoxesPerPress.Value, 1, 100);
        int opened = 0;
        string name = GetDisplayName(selectedBox);

        for (int index = 0; index < requested; index++)
        {
            if (!CanOpen(selectedBox)) break;
            LootBox lootBox = selectedBox.lootBoxOpen;
            if (lootBox == null) break;

            try
            {
                selectedBox.lootBoxOpen = null;
                selectedBox.Craft();
                lootBox.Open();
                opened++;
            }
            catch (Exception exception)
            {
                ProgressionLog.Error(
                    $"Armory box opening stopped safely after {opened} box(es): {exception}");
                break;
            }
            finally
            {
                selectedBox.lootBoxOpen = lootBox;
            }
        }

        if (opened <= 0)
        {
            Notify("No box opened: check materials and free Armory slots.", false);
            return;
        }

        ProgressionLog.User($"Opened {opened} {name} Armory box(es) in the background.");
        Notify($"Opened {opened} {name} box(es)", true);
    }

    private static bool CanOpen(TemporaryCraftableItem item)
    {
        try
        {
            return IsArmoryBox(item) && item.TabVisible() &&
                   item.ExtraCondition() && item.HowManyCanCraft() > 0d &&
                   (WeaponsManager.instance?.hasFreeSlot ?? false);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsArmoryBox(TemporaryCraftableItem item) =>
        item != null && item.requiresFreeArmorySpace && item.lootBoxOpen != null;

    private static KeyCode ResolveKey(
        string configuredValue,
        KeyCode fallback,
        string purpose,
        ref bool invalidLogged)
    {
        string configured = configuredValue?.Trim();
        if (!string.IsNullOrEmpty(configured) &&
            Enum.TryParse(configured, true, out KeyCode parsed) &&
            parsed != KeyCode.None)
        {
            invalidLogged = false;
            return parsed;
        }

        if (!invalidLogged)
        {
            ProgressionLog.Warning(
                $"Invalid Armory box {purpose} key '{configured}'. Falling back to {fallback}.");
            invalidLogged = true;
        }
        return fallback;
    }

    private static string GetDisplayName(TemporaryCraftableItem item) =>
        string.IsNullOrWhiteSpace(item?.localizedName)
            ? item?.name ?? "unknown"
            : item.localizedName;

    private static void Notify(string message, bool positive)
    {
        ProgressionLog.Debug(message);
        Plugin.ModHelperInstance?.ShowNotification(message, positive);
    }

    internal void Reset()
    {
        selectedBox = null;
        invalidSelectKeyLogged = false;
        invalidOpenKeyLogged = false;
        duplicateKeysLogged = false;
    }
}
