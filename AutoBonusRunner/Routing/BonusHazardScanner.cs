using AutoBonusRunner.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoBonusRunner.Routing;

internal readonly record struct BonusHazard(
    bool IsValid,
    float Left,
    float Right,
    float Top,
    int InstanceId,
    string Name,
    string ComponentPath)
{
    internal float CenterX => (Left + Right) * 0.5f;
}

internal sealed class BonusHazardScanner
{
    private const float LookAhead = 14f;
    private const float PassedMargin = 0.25f;
    private readonly record struct HazardColliderBinding(
        BonusStageSpike Spike,
        Collider2D Collider,
        int InstanceId,
        string Name,
        string ComponentPath);

    private HazardColliderBinding[] cachedHazardColliders =
        Array.Empty<HazardColliderBinding>();
    private int cachedSection = int.MinValue;
    private bool cacheReady;

    internal void ResetCache(string reason)
    {
        cacheReady = false;
        cachedSection = int.MinValue;
        cachedHazardColliders = Array.Empty<HazardColliderBinding>();
    }

    internal BonusHazard FindNearest(Vector3 playerPosition)
    {
        BonusHazard nearest = default;
        float nearestLeft = float.PositiveInfinity;
        int section =
            BonusMapController.instance?.currentSectionIndex ?? -1;
        if (!cacheReady || cachedSection != section)
        {
            BonusStageSpike[] spikes =
                UnityEngine.Object.FindObjectsOfType<BonusStageSpike>() ??
                Array.Empty<BonusStageSpike>();
            List<HazardColliderBinding> bindings = new();
            HashSet<int> seenColliders = new();
            foreach (BonusStageSpike spike in spikes)
            {
                if (spike == null)
                    continue;

                Collider2D[] colliders =
                    spike.GetComponentsInChildren<Collider2D>(true);
                if (colliders == null || colliders.Length == 0)
                {
                    colliders =
                        spike.GetComponentsInParent<Collider2D>(true);
                }

                string componentPath = BuildPath(spike.transform);
                foreach (Collider2D collider in colliders)
                {
                    if (collider == null)
                        continue;

                    int instanceId = collider.GetInstanceID();
                    if (!seenColliders.Add(instanceId))
                        continue;

                    bindings.Add(new HazardColliderBinding(
                        spike,
                        collider,
                        instanceId,
                        collider.name,
                        componentPath));
                }
            }

            cachedHazardColliders = bindings.ToArray();
            cachedSection = section;
            cacheReady = true;
            BonusRunnerLog.Debug(
                $"HazardColliderCacheBuilt Section={section}, " +
                $"Spikes={spikes.Length}, Colliders=" +
                $"{cachedHazardColliders.Length}.",
                "Performance");
        }

        foreach (HazardColliderBinding binding in cachedHazardColliders)
        {
            BonusStageSpike spike = binding.Spike;
            Collider2D collider = binding.Collider;
            if (spike == null || !spike.enabled ||
                spike.gameObject == null ||
                !spike.gameObject.activeInHierarchy ||
                collider == null ||
                !collider.enabled ||
                collider.gameObject == null ||
                !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            if (bounds.max.x < playerPosition.x + PassedMargin ||
                bounds.min.x > playerPosition.x + LookAhead ||
                bounds.min.x >= nearestLeft)
                continue;

            nearestLeft = bounds.min.x;
            nearest = new BonusHazard(
                true,
                bounds.min.x,
                bounds.max.x,
                bounds.max.y,
                binding.InstanceId,
                binding.Name,
                binding.ComponentPath);
        }

        return nearest;
    }

    private static string BuildPath(Transform transform)
    {
        if (transform == null)
            return "BonusStageSpike";

        string path = transform.name;
        Transform current = transform.parent;
        int depth = 0;
        while (current != null && depth++ < 4)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }
}
