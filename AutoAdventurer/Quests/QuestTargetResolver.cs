using System;
using System.Collections.Generic;
using AutoAdventurer.Diagnostics;
using Il2Cpp;

namespace AutoAdventurer.Quests;

internal sealed class QuestTargetResolver
{
    private readonly HashSet<string> loggedUnavailableStages = new();
    private readonly HashSet<string> loggedUnavailableCharacterQuests = new();

    internal QuestTargetSelection Select(List<Quest> quests)
    {
        QuestTargetSelection best = null;
        foreach (Quest quest in quests)
        {
            if (!IsExecutableKillQuest(quest)) continue;

            QuestTargetSelection candidate = ResolveMap(quest);
            if (candidate == null) continue;

            if (best == null || candidate.RequiredKills < best.RequiredKills)
                best = candidate;
        }

        return best;
    }

    internal QuestTargetSelection ResolveLocked(
        Quest quest, string preferredMapId)
    {
        if (quest == null || !IsExecutableKillQuest(quest)) return null;
        return ResolveMap(quest, preferredMapId);
    }

    private bool IsExecutableKillQuest(Quest quest)
    {
        try
        {
            if (quest is DailyQuest daily &&
                (!daily.active || !daily.CheckIfIsValid())) return false;
            if (!quest.CanBeCompleted()) return false;
            bool characterQuest =
                quest.questType == QuestType.KillEnemiesWithCharacter ||
                quest.characterRequired != null;
            if (characterQuest && (quest.characterRequired == null ||
                                   !quest.characterRequired.unlocked))
            {
                string key = QuestTargetSelection.BuildLockKey(quest);
                if (loggedUnavailableCharacterQuests.Add(key))
                    AdventurerLog.QuestDebug(
                        $"Quest skipped: quest={GetQuestLabel(quest)}; reason=required character is unavailable; character={quest.characterRequired?.name ?? "UnknownCharacter"}.");
                return false;
            }

            bool killType = quest.questType is QuestType.KillEnemies or
                QuestType.KillEnemiesWithArrows or
                QuestType.KillFlyers or
                QuestType.KillGiants or
                QuestType.KillEnemiesWithRageMode or
                QuestType.KillEnemiesWithCharacter or
                QuestType.KillEnemiesOfType or
                QuestType.KillShinyEnemies or
                QuestType.WindDashKills;
            if (!killType) return false;

            // Exact targets, native types, provable map categories, and
            // generic arrow kills are executable without parsing text.
            return quest.enemyToKill != null ||
                   (quest.questType == QuestType.KillEnemiesOfType &&
                    quest.enemyType != null) ||
                   quest.questType == QuestType.KillFlyers ||
                   quest.questType == QuestType.KillGiants ||
                   quest.questType == QuestType.KillEnemiesWithArrows ||
                   characterQuest;
        }
        catch
        {
            return false;
        }
    }

    private QuestTargetSelection ResolveMap(
        Quest quest, string preferredMapId = "")
    {
        MapController controller = MapController.instance;
        BaseMap currentMap = controller?.selectedMap;
        if (controller == null || currentMap == null) return null;

        if (quest.enemyToKill != null)
            return ResolveExactEnemy(controller, currentMap, quest,
                preferredMapId);

        Func<Enemy, bool> predicate;
        if (quest.questType == QuestType.KillEnemiesOfType &&
            quest.enemyType != null)
        {
            EnemyType requiredType = quest.enemyType;
            predicate = enemy => SameEnemyType(enemy?.type, requiredType);
        }
        else if (quest.questType == QuestType.KillFlyers)
            predicate = enemy => enemy?.isFlying == true;
        else if (quest.questType == QuestType.KillGiants)
            predicate = enemy => enemy?.isGiant == true;
        else if (quest.questType == QuestType.KillEnemiesWithArrows)
            predicate = enemy => enemy != null;
        else if (quest.questType == QuestType.KillEnemiesWithCharacter ||
                 quest.characterRequired != null)
            predicate = enemy => enemy != null;
        else
            return null;

        return ResolveMatchingEnemy(controller, currentMap, quest,
            predicate, preferredMapId);
    }

    private QuestTargetSelection ResolveExactEnemy(
        MapController controller, BaseMap currentMap, Quest quest,
        string preferredMapId)
    {
        Enemy target = quest.enemyToKill;
        Enemy firstStage = GetEnemyFirstStage(target);
        if (firstStage == null) return null;

        // A target evolution must itself be unlocked. Killing that evolution
        // or any later descendant in the same branch satisfies the quest;
        // lower stages and sibling branches do not.
        if (target.evolutionBackward != null &&
            !IsEnemyEvolutionUnlocked(target))
        {
            string key = $"{quest.name}|{target.name}";
            if (loggedUnavailableStages.Add(key))
            {
                Upgrade unlock = target.upgradeToUnlockIt;
                AdventurerLog.QuestDebug(
                    $"Quest skipped: quest={GetQuestLabel(quest)}; reason=required enemy evolution is unavailable; enemy={target.name}; unlockUpgrade={unlock?.name ?? "None"}; bought={unlock?.bought.ToString() ?? "N/A"}; specialUnlocked={unlock?.specialUnlocked.ToString() ?? "N/A"}.");
            }
            return null;
        }

        Func<Enemy, bool> predicate = enemy =>
            IsSameOrDescendant(enemy, target);
        return ResolveMatchingEnemy(controller, currentMap, quest, predicate,
            preferredMapId);
    }

    private static QuestTargetSelection ResolveMatchingEnemy(
        MapController controller, BaseMap currentMap, Quest quest,
        Func<Enemy, bool> predicate, string preferredMapId)
    {
        Enemy currentEnemy = FindMatchingCurrentEnemy(currentMap, predicate);
        if (currentEnemy == null &&
            quest.questType == QuestType.KillGiants &&
            !string.IsNullOrEmpty(preferredMapId) &&
            string.Equals(currentMap.name, preferredMapId,
                StringComparison.Ordinal))
            currentEnemy = FindUnlockedGiantInEvolutionChains(currentMap);

        // A dimension the player is already running in is usable even when
        // the Portal availability list temporarily omits or rejects it.
        if (IsStandardPortalDimension(currentMap) && currentEnemy != null &&
            (string.IsNullOrEmpty(preferredMapId) || string.Equals(
                currentMap.name, preferredMapId, StringComparison.Ordinal)))
        {
            return CreateSelection(quest, currentEnemy, currentMap);
        }

        // This is the authoritative list used by the normal Portal selector.
        // includeCurrent=false because the current dimension was checked above.
        var maps = controller.GetAvailableMaps(false);
        if (maps == null) return null;

        // Keep a locked quest on its chosen dimension while that dimension
        // remains Portal-selectable and still contains a matching enemy.
        if (!string.IsNullOrEmpty(preferredMapId))
        {
            for (int index = 0; index < maps.Count; index++)
            {
                Map preferred = maps[index];
                if (preferred == null || !string.Equals(preferred.name,
                        preferredMapId, StringComparison.Ordinal) ||
                    !preferred.IsAvailable()) continue;
                Enemy matched = FindMatchingCurrentEnemy(preferred, predicate);
                if (matched == null &&
                    quest.questType == QuestType.KillGiants)
                    matched = FindUnlockedGiantInEvolutionChains(preferred);
                if (matched != null)
                    return CreateSelection(quest, matched, preferred);
            }
        }

        // The preferred map is no longer valid. The caller may update the
        // string lock to a newly valid current or available dimension.
        if (IsStandardPortalDimension(currentMap) && currentEnemy != null)
            return CreateSelection(quest, currentEnemy, currentMap);

        for (int index = 0; index < maps.Count; index++)
        {
            Map map = maps[index];
            if (!IsStandardPortalDimension(map) || !map.IsAvailable()) continue;
            Enemy matched = FindMatchingCurrentEnemy(map, predicate);
            if (matched == null) continue;

            return CreateSelection(quest, matched, map);
        }

        return null;
    }

    private static Enemy FindUnlockedGiantInEvolutionChains(BaseMap map)
    {
        var enemies = map?.enemies;
        if (enemies == null) return null;

        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy stage = GetEnemyFirstStage(enemies[index]);
            HashSet<string> visited = new(StringComparer.Ordinal);
            for (int depth = 0; stage != null && depth < 32; depth++)
            {
                string identity = stage.name ?? $"stage_{index}_{depth}";
                if (!visited.Add(identity)) break;
                if (stage.isGiant && IsEnemyEvolutionUnlocked(stage))
                    return stage;
                stage = stage.evolutionForward;
            }
        }

        return null;
    }

    private static bool IsStandardPortalDimension(BaseMap map) => map is Map;

    private static QuestTargetSelection CreateSelection(
        Quest quest, Enemy enemy, BaseMap map) =>
        new()
        {
            Quest = quest,
            Enemy = enemy,
            Map = map,
            // GetGoal() returns zero for a number of normal quest assets.
            // questGoal is the serialized kill requirement shown by the UI.
            RequiredKills = Math.Max(0d, quest.questGoal),
            CurrentKills = Math.Max(0d, quest.questCurrentGoal)
        };

    private static Enemy FindMatchingCurrentEnemy(
        BaseMap map, Func<Enemy, bool> predicate)
    {
        var enemies = map.enemies;
        if (enemies == null) return null;

        for (int index = 0; index < enemies.Count; index++)
        {
            Enemy candidate = enemies[index];
            if (candidate == null) continue;
            Enemy firstStage = GetEnemyFirstStage(candidate);
            Enemy currentStage = firstStage?.GetCurrentStage();
            if (currentStage != null && predicate(currentStage))
                return currentStage;
        }

        return null;
    }

    private static Enemy GetEnemyFirstStage(Enemy enemy)
    {
        // EnemyUtility.GetEnemyFirstStage can resolve the wrong asset for
        // enemies whose runtime definitions share lookup metadata. Following
        // the serialized chain on the enemy itself preserves its identity.
        Enemy current = enemy;
        HashSet<string> visited = new(StringComparer.Ordinal);
        for (int depth = 0; current != null && depth < 32; depth++)
        {
            string identity = current.name ?? $"stage_{depth}";
            if (!visited.Add(identity) || current.evolutionBackward == null)
                return current;
            current = current.evolutionBackward;
        }

        return current;
    }

    private static bool IsEnemyEvolutionUnlocked(Enemy enemy)
    {
        Upgrade unlock = enemy?.upgradeToUnlockIt;
        if (unlock == null) return true;
        return unlock.bought || (unlock.isSpecialUnlock && unlock.specialUnlocked);
    }

    private static bool IsSameOrDescendant(Enemy candidate, Enemy target)
    {
        HashSet<string> visited = new(StringComparer.Ordinal);
        Enemy current = candidate;
        for (int depth = 0; current != null && depth < 32; depth++)
        {
            if (SameEnemy(current, target)) return true;
            string identity = current.name ?? $"stage_{depth}";
            if (!visited.Add(identity)) return false;
            current = current.evolutionBackward;
        }

        return false;
    }

    private static bool SameEnemyType(EnemyType left, EnemyType right)
    {
        if (left == null || right == null) return false;
        return string.Equals(left.name, right.name, StringComparison.Ordinal);
    }

    private static bool SameEnemy(Enemy left, Enemy right)
    {
        if (left == null || right == null) return false;
        // bestiaryIndex is not unique for every runtime enemy definition.
        // Internal names are the stable identity used by quest targets.
        return string.Equals(left.name, right.name, StringComparison.Ordinal);
    }

    private static string GetQuestLabel(Quest quest)
    {
        string id = quest?.name ?? "UnknownQuest";
        string displayName = quest?.localizedName;
        return string.IsNullOrWhiteSpace(displayName)
            ? id
            : $"{displayName} ({id})";
    }
}
