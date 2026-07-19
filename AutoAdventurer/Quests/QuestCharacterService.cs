using System;
using AutoAdventurer.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Quests;

internal sealed class QuestCharacterService
{
    internal bool RequiresSwitch(QuestTargetSelection selection)
    {
        return RequiresSwitch(selection?.Quest);
    }

    internal bool RequiresSwitch(Quest quest)
    {
        CharacterSkin required = quest?.characterRequired;
        if (required == null) return false;

        PlayerSkinManager manager = PlayerSkinManager.instance;
        return manager == null || !SameBaseCharacter(manager.skin, required);
    }

    internal bool TryApply(QuestTargetSelection selection)
    {
        CharacterSkin required = selection?.RequiredCharacter;
        if (required == null) return true;

        PlayerSkinManager manager = PlayerSkinManager.instance;
        if (manager == null || !required.unlocked) return false;
        if (SameBaseCharacter(manager.skin, required)) return true;

        try
        {
            manager.ApplySkin(required);
            manager.RefreshSkin();
            if (!SameBaseCharacter(manager.skin, required)) return false;

            AdventurerLog.User(
                $"Quest Automation switched character to {GetSkinLabel(required)} for {selection.QuestId}.");
            return true;
        }
        catch (Exception exception)
        {
            AdventurerLog.Error(
                $"Quest character switch failed safely: {exception}");
            return false;
        }
    }

    internal bool TryApply(string requiredCharacterId, string questId)
    {
        if (string.IsNullOrEmpty(requiredCharacterId)) return true;

        foreach (CharacterSkin candidate in
                 Resources.FindObjectsOfTypeAll<CharacterSkin>())
        {
            if (candidate == null || !string.Equals(candidate.name,
                    requiredCharacterId, StringComparison.Ordinal)) continue;

            PlayerSkinManager manager = PlayerSkinManager.instance;
            if (manager == null || !candidate.unlocked) return false;
            if (SameBaseCharacter(manager.skin, candidate)) return true;

            try
            {
                manager.ApplySkin(candidate);
                manager.RefreshSkin();
                if (!SameBaseCharacter(manager.skin, candidate)) return false;
                AdventurerLog.User(
                    $"Quest Automation switched character to {GetSkinLabel(candidate)} for {questId}.");
                return true;
            }
            catch (Exception exception)
            {
                AdventurerLog.Error(
                    $"Quest character switch failed safely: {exception}");
                return false;
            }
        }

        return false;
    }

    private static bool SameBaseCharacter(
        CharacterSkin left, CharacterSkin right)
    {
        string leftId = GetBaseCharacterId(left);
        string rightId = GetBaseCharacterId(right);
        return !string.IsNullOrEmpty(leftId) && string.Equals(
            leftId, rightId, StringComparison.Ordinal);
    }

    private static string GetBaseCharacterId(CharacterSkin skin)
    {
        if (skin == null) return string.Empty;
        CharacterSkin baseSkin = skin.outfitOf ?? skin;
        return baseSkin.name ?? string.Empty;
    }

    private static string GetSkinLabel(CharacterSkin skin)
    {
        string id = skin?.name ?? "UnknownCharacter";
        string displayName = skin?.localizedName;
        return string.IsNullOrWhiteSpace(displayName)
            ? id
            : $"{LogText.Normalize(displayName)} ({id})";
    }
}
