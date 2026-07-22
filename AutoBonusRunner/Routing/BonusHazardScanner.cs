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

    internal BonusHazard FindNearest(Vector3 playerPosition)
    {
        BonusHazard nearest = default;
        float nearestLeft = float.PositiveInfinity;

        foreach (BonusStageSpike spike in
                 UnityEngine.Object.FindObjectsOfType<BonusStageSpike>())
        {
            if (spike == null || !spike.enabled ||
                spike.gameObject == null || !spike.gameObject.activeInHierarchy)
                continue;

            Collider2D[] colliders = spike.GetComponentsInChildren<Collider2D>(true);
            if (colliders == null || colliders.Length == 0)
                colliders = spike.GetComponentsInParent<Collider2D>(true);

            foreach (Collider2D collider in colliders)
            {
                if (collider == null || !collider.enabled)
                    continue;

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
                    collider.GetInstanceID(),
                    collider.name,
                    BuildPath(spike.transform));
            }
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
