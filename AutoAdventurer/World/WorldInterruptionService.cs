using UnityEngine;
using Il2Cpp;

namespace AutoAdventurer.World;

internal sealed class WorldInterruptionService
{
    private const string ChestHuntKeyName = "Chest Hunt Key(Clone)";
    private const string SpecialRandomBoxName = "Special Random Box(Clone)";
    private const string ClimbingSoulBootsName = "Climbing Soul Boots(Clone)";
    private const string SoulGrappleCapeName = "Soul Grapple Cape(Clone)";

    internal bool HasChestHuntKey =>
        GameObject.Find(ChestHuntKeyName) != null;

    internal bool HasActivePortal =>
        TryFindActivePortal(out _) ||
        TryFindActiveInteractivePortal(out _);

    internal bool TryGetBlocker(out string blocker)
    {
        // These exact active-object names match the game's pooled spawn names.
        // GameObject.Find is intentionally used here: unlike a Resources-wide
        // scan, it neither walks inactive pooled objects nor retains IL2CPP refs.
        if (GameObject.Find(ChestHuntKeyName) != null)
            blocker = "Chest Hunt Key";
        else if (TryFindActivePortal(out string portalDescription))
            blocker = portalDescription;
        else if (TryFindActiveInteractivePortal(out string interactivePortal))
            blocker = interactivePortal;
        else if (TryGetSpecialRandomBoxBlocker(out string specialBoxDescription))
            blocker = specialBoxDescription;
        else if (GameObject.Find(ClimbingSoulBootsName) != null)
            blocker = "Climbing Soul Boots minigame item";
        else if (GameObject.Find(SoulGrappleCapeName) != null)
            blocker = "Soul Grapple Cape minigame item";
        else
        {
            blocker = string.Empty;
            return false;
        }

        return true;
    }

    internal bool TryGetActiveRandomEvent(out string eventName)
    {
        // Only map-bound events that generate temporary run content should
        // prevent dimension travel. Long-lived numeric bonuses (CpS, Souls,
        // Equipment and Coin Value) remain useful after changing dimensions
        // and must not hold Quest Automation indefinitely.
        foreach (RandomEvent randomEvent in
                 UnityEngine.Object.FindObjectsOfType<RandomEvent>())
        {
            if (randomEvent == null ||
                randomEvent.gameObject == null ||
                !randomEvent.gameObject.activeInHierarchy ||
                !IsMapBoundRandomEvent(randomEvent))
                continue;

            bool nativeActive = false;
            try
            {
                // Use the game's own activation rule. Some events can be
                // logically active without satisfying a raw GameObject/time
                // check because ExtraActiveCondition participates in it.
                // Conversely, a few events visibly run with a positive timer
                // while IsActive briefly reports false, so either signal is
                // sufficient for the travel guard.
                nativeActive = randomEvent.IsActive();
            }
            catch
            {
                // The timer remains a safe fallback for an unreadable native
                // activation state.
            }
            bool isActive = nativeActive || randomEvent.timeLeft > 0d;
            if (!isActive) continue;

            string eventType = randomEvent.GetIl2CppType()?.Name ??
                               "RandomEvent";
            string internalName = string.IsNullOrWhiteSpace(randomEvent.name)
                ? eventType
                : randomEvent.name;
            eventName =
                $"{eventType} (name={internalName}, timeLeft={randomEvent.timeLeft:0.##}s)";
            return true;
        }

        eventName = string.Empty;
        return false;
    }

    private static bool IsMapBoundRandomEvent(RandomEvent randomEvent) =>
        randomEvent is Horde or ExtremeCoins or LuckyCoins or
        GemstoneRush or Frenzy or DualRandomness;

    internal bool TryGetActiveRandomBox(out string boxDescription)
    {
        // RandomBox.Type covers normal, Silver and Golden variants. Block as
        // soon as the pooled object becomes active, including the short
        // interval between impact and RandomEvent activation.
        foreach (RandomBox randomBox in
                 UnityEngine.Object.FindObjectsOfType<RandomBox>())
        {
            if (randomBox == null || randomBox.gameObject == null ||
                !randomBox.gameObject.activeInHierarchy)
                continue;

            boxDescription =
                $"Random Box ({randomBox.type}; hit={randomBox.isHitted})";
            return true;
        }

        // SpecialRandomBox is the separate activity/minigame box component.
        // Treat it identically so every on-screen box freezes quest travel.
        foreach (SpecialRandomBox specialBox in
                 UnityEngine.Object.FindObjectsOfType<SpecialRandomBox>())
        {
            if (specialBox == null || specialBox.gameObject == null ||
                !specialBox.gameObject.activeInHierarchy)
                continue;

            boxDescription =
                $"Special Random Box (hit={specialBox.isHitted})";
            return true;
        }

        boxDescription = string.Empty;
        return false;
    }

    private static bool TryGetSpecialRandomBoxBlocker(out string description)
    {
        GameObject specialBoxObject = GameObject.Find(SpecialRandomBoxName);
        if (specialBoxObject == null)
        {
            description = string.Empty;
            return false;
        }

        SpecialRandomBox specialBox =
            specialBoxObject.GetComponent<SpecialRandomBox>();
        description = specialBox != null && specialBox.isHitted
            ? "Special transition door (Boss or minigame)"
            : "Special Random Box";
        return true;
    }

    private static bool TryFindActivePortal(out string description)
    {
        // Portal object names differ between pools (standard, Boss and return
        // gates). Query only the currently active Portal components; unlike
        // Resources.FindObjectsOfTypeAll this does not traverse inactive pool
        // contents or every GameObject in the game.
        foreach (Il2Cpp.Portal portal in
                 UnityEngine.Object.FindObjectsOfType<Il2Cpp.Portal>())
        {
            if (portal == null || portal.gameObject == null ||
                !portal.gameObject.activeInHierarchy)
                continue;

            description =
                $"Portal ({portal.portalType}; {portal.gameObject.name})";
            return true;
        }

        description = string.Empty;
        return false;
    }

    private static bool TryFindActiveInteractivePortal(out string description)
    {
        foreach (PortalInteractive portal in
                 UnityEngine.Object.FindObjectsOfType<PortalInteractive>())
        {
            if (portal == null || portal.gameObject == null ||
                !portal.gameObject.activeInHierarchy)
                continue;

            description = $"Interactive portal ({portal.gameObject.name})";
            return true;
        }

        description = string.Empty;
        return false;
    }

    internal void Reset()
    {
        // No Unity object references are retained.
    }
}
