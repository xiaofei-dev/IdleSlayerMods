using UnityEngine;

namespace AutoAdventurer.World;

internal sealed class WorldInterruptionService
{
    private const string ChestHuntKeyName = "Chest Hunt Key(Clone)";
    private const string SpecialRandomBoxName = "Special Random Box(Clone)";
    private const string ClimbingSoulBootsName = "Climbing Soul Boots(Clone)";
    private const string PortalName = "Portal(Clone)";
    private const string SoulGrappleCapeName = "Soul Grapple Cape(Clone)";

    internal bool HasChestHuntKey =>
        GameObject.Find(ChestHuntKeyName) != null;

    internal bool TryGetBlocker(out string blocker)
    {
        // These exact active-object names match the game's pooled spawn names.
        // GameObject.Find is intentionally used here: unlike a Resources-wide
        // scan, it neither walks inactive pooled objects nor retains IL2CPP refs.
        if (GameObject.Find(ChestHuntKeyName) != null)
            blocker = "Chest Hunt Key";
        else if (GameObject.Find(SpecialRandomBoxName) != null)
            blocker = "Special Random Box";
        else if (GameObject.Find(ClimbingSoulBootsName) != null)
            blocker = "Climbing Soul Boots minigame item";
        else if (GameObject.Find(SoulGrappleCapeName) != null)
            blocker = "Soul Grapple Cape minigame item";
        else if (GameObject.Find(PortalName) != null)
            blocker = "Portal";
        else
        {
            blocker = string.Empty;
            return false;
        }

        return true;
    }

    internal void Reset()
    {
        // No Unity object references are retained.
    }
}
