using Il2Cpp;
using UnityEngine;

namespace AutoBonusRunner.Routing;

internal readonly record struct BonusWallContact(
    bool IsDetected,
    bool IsTouching,
    float Distance,
    float BodyGap,
    float FaceX,
    Vector2 Point,
    Vector2 Normal,
    int ColliderInstanceId,
    string ColliderName,
    string Reason);

internal sealed class BonusWallDetector
{
    // Probe one render frame before physical contact. Some bonus sections
    // switch out of BonusMode on the same frame that a failed approach begins
    // sliding down the wall, which is too late for a stall-only detector.
    // Runtime target-bounds validation prevents this wider probe from turning
    // unrelated scenery into a wall jump.
    private const float ExtraProbeDistance = 0.42f;
    private static readonly float[] VerticalOffsets =
    {
        -0.42f, 0f, 0.42f
    };

    internal BonusWallContact Detect(
        PlayerMovement player,
        float expectedHorizontalSpeed)
    {
        if (player == null)
            return Missing("PlayerUnavailable");

        Collider2D playerCollider = player.playerCollider;
        if (playerCollider == null)
            return Missing("PlayerColliderUnavailable");

        int layerMask = player.jumpableLayer.value;
        if (layerMask == 0)
            return Missing("JumpableLayerEmpty");

        Bounds bounds = playerCollider.bounds;
        float directionSign = expectedHorizontalSpeed < 0f ? -1f : 1f;
        Vector2 direction = directionSign > 0f
            ? Vector2.right
            : Vector2.left;
        float probeDistance =
            Mathf.Max(0.15f, bounds.extents.x) + ExtraProbeDistance;

        // AutoJumpMod succeeds on wall contacts without maintaining its own
        // geometry detector: it repeatedly feeds JumpPanel and lets the native
        // PlayerMovement.IsWalled check authorize the wall jump. The native
        // result is therefore authoritative contact evidence. Our ray remains
        // useful for target identity and upcoming-face distance, but may miss
        // while the player collider overlaps the composite wall.
        bool nativeIsWalled = false;
        try
        {
            nativeIsWalled = player.IsWalled();
        }
        catch
        {
            // Fall back to geometry-only evidence if the IL2CPP call is
            // transiently unavailable during a pooled player replacement.
        }

        RaycastHit2D bestHit = default;
        bool found = false;
        foreach (float offset in VerticalOffsets)
        {
            Vector2 origin = new(
                bounds.center.x,
                bounds.center.y + bounds.extents.y * offset);
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(
                         origin,
                         direction,
                         probeDistance,
                         layerMask))
            {
                Collider2D collider = hit.collider;
                if (collider == null || collider == playerCollider)
                    continue;

                float facing = Vector2.Dot(hit.normal, direction);
                bool verticalFace = facing <= -0.35f;
                bool overlappingFace =
                    hit.distance <= 0.01f &&
                    Mathf.Abs(hit.normal.x) >= 0.35f;
                if (!verticalFace && !overlappingFace)
                    continue;
                if (found && hit.distance >= bestHit.distance)
                    continue;

                bestHit = hit;
                found = true;
            }
        }

        if (!found)
        {
            if (!nativeIsWalled)
                return Missing("NoForwardWallHit;NativeIsWalled=False");

            float nativeFaceX = directionSign > 0f
                ? bounds.max.x
                : bounds.min.x;
            return new(
                true,
                true,
                Mathf.Max(0.05f, bounds.extents.x),
                0f,
                nativeFaceX,
                new Vector2(nativeFaceX, bounds.center.y),
                directionSign > 0f ? Vector2.left : Vector2.right,
                0,
                "NativePlayerMovementWall",
                "NativeIsWalled=True;RaycastUnavailable");
        }

        float bodyGap = Mathf.Max(
            0f,
            bestHit.distance - Mathf.Max(0.05f, bounds.extents.x));
        bool touching =
            nativeIsWalled || bodyGap <= 0.055f || bestHit.distance <= 0.01f;
        return new(
            true,
            touching,
            bestHit.distance,
            bodyGap,
            bestHit.point.x,
            bestHit.point,
            bestHit.normal,
            bestHit.collider.GetInstanceID(),
            bestHit.collider.name ?? "Unnamed",
            nativeIsWalled
                ? "ForwardWallDetected;NativeIsWalled=True"
                : "ForwardWallDetected;NativeIsWalled=False");
    }

    private static BonusWallContact Missing(string reason) =>
        new(
            false,
            false,
            0f,
            float.PositiveInfinity,
            float.NaN,
            default,
            default,
            0,
            string.Empty,
            reason);
}
