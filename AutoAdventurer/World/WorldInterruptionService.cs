using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.World;

internal sealed class WorldInterruptionService
{
    private const string ChestHuntKeyObjectName = "Chest Hunt Key(Clone)";
    private static readonly string[] PortalCloneNameFragments =
    {
        "Portal",
        "Door",
        "Gate"
    };
    private static readonly string[] MinigameCloneNameFragments =
    {
        "Minigame",
        "Mini Game",
        "RandomPortal",
        "Random Portal",
        "Ascending",
        "Grapple"
    };

    internal bool TryGetBlocker(out string blocker)
    {
        GameObject key = GameObject.Find(ChestHuntKeyObjectName);
        if (IsActiveLoadedObject(key))
        {
            blocker = "Chest Hunt Key";
            return true;
        }

        if (TryFindActiveLoadedComponent<SpecialRandomBox>(out SpecialRandomBox specialBox))
        {
            blocker = $"Special Random Box ({specialBox.gameObject.name})";
            return true;
        }

        if (TryFindActiveNamedClone(
                MinigameCloneNameFragments, out GameObject minigameObject))
        {
            blocker = $"Minigame trigger ({minigameObject.name})";
            return true;
        }

        if (TryFindActiveLoadedComponent<PortalInteractive>(out PortalInteractive portal))
        {
            blocker = $"Portal ({portal.gameObject.name})";
            return true;
        }

        if (TryFindActiveNamedClone(
                PortalCloneNameFragments, out GameObject portalObject))
        {
            blocker = $"Portal or door ({portalObject.name})";
            return true;
        }

        blocker = string.Empty;
        return false;
    }

    private static bool TryFindActiveLoadedComponent<T>(out T found)
        where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component == null || !IsActiveLoadedObject(component.gameObject))
                continue;

            found = component;
            return true;
        }

        found = null;
        return false;
    }

    private static bool IsActiveLoadedObject(GameObject gameObject)
    {
        if (gameObject == null || !gameObject.activeInHierarchy) return false;

        var scene = gameObject.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    private static bool TryFindActiveNamedClone(
        string[] nameFragments,
        out GameObject found)
    {
        foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!IsActiveLoadedObject(gameObject)) continue;

            string objectName = gameObject.name ?? string.Empty;
            if (!objectName.EndsWith("(Clone)",
                    System.StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (string fragment in nameFragments)
            {
                if (objectName.IndexOf(fragment,
                        System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                found = gameObject;
                return true;
            }
        }

        found = null;
        return false;
    }
}
