using Il2Cpp;
using UnityEngine;

namespace AutoBonusRunner.Routing;

internal readonly record struct BonusBoardSegment(
    float Left,
    float Right,
    float Top,
    float SafeLeft,
    float SafeRight,
    int ColliderInstanceId,
    string ColliderName,
    string MapPieceName = "Unknown",
    float MapPieceOriginX = float.NaN,
    int MapPieceInstanceId = 0,
    int RegistryGeneration = 0,
    int StaticSurfaceIndex = -1)
{
    internal float Width => Right - Left;
    internal float LocalLeft => float.IsNaN(MapPieceOriginX) ? float.NaN : Left - MapPieceOriginX;
    internal float LocalRight => float.IsNaN(MapPieceOriginX) ? float.NaN : Right - MapPieceOriginX;
}

internal readonly record struct BonusBoardScanResult(
    bool IsValid,
    BonusBoardSegment Current,
    bool HasNext,
    BonusBoardSegment Next,
    float DistanceToCurrentEdge,
    float Gap,
    float HeightDelta,
    string Reason,
    bool HasIntermediate = false,
    BonusBoardSegment Intermediate = default,
    BonusBoardSegment[] Alternatives = null);

internal sealed class BonusPlatformScanner
{
    private const float SampleStep = 0.15f;
    private const float MinimumLookAhead = 28f;
    private const float MaximumLookAhead = 80f;
    private const float MaximumLookBehind = 8f;
    private const float SameSurfaceTolerance = 0.30f;
    // The authored map contains a nine-unit Ground 5 drop and a fourteen-unit
    // Ground 6 drop (top 12 to floor -2). These are real route edges, not
    // death pits. A smaller cutoff skipped the nearby floor and selected a
    // platform thirteen units away, leaving the runner waiting until timeout.
    private const float MaximumStepDown = 15.25f;
    private const float MaximumStepUp = 5.35f;
    // A bonus-stage route can intentionally end the first jump against the
    // vertical face of a tall pillar and use one more jump to climb onto it.
    // Those tops are not directly landable, but they must remain visible to
    // the route planner instead of being skipped in favour of distant ground.
    private const float MaximumWallClimbStepUp = 12.0f;
    private const float EdgeSafetyMargin = 0.15f;
    private const float MinimumSafeWidth = 0.10f;
    private readonly BonusMapPieceRegistry mapRegistry = new();

    internal BonusMapPieceRegistry MapRegistry => mapRegistry;

    internal bool RefreshStaticMap(int sectionIndex) =>
        mapRegistry.Refresh(sectionIndex);

    internal void ResetStaticMap(string reason) =>
        mapRegistry.Reset(reason);

    internal bool TryFindChainedWallStep(
        BonusBoardSegment currentWall,
        float playerHalfWidth,
        out BonusBoardSegment nextWall)
    {
        nextWall = default;
        if (mapRegistry.State != BonusMapPieceRegistryState.Ready ||
            currentWall.Width <= 0.05f)
        {
            return false;
        }

        BonusStaticWorldSurface[] surfaces = mapRegistry.GetWorldSurfaces(
            currentWall.Right + 0.05f,
            currentWall.Right + 12.0f);
        BonusStaticWorldSurface? candidate = surfaces
            .Where(surface =>
                surface.Left >= currentWall.Right + 0.10f &&
                surface.Left - currentWall.Right <= 5.25f &&
                surface.Top >= currentWall.Top + 0.30f &&
                surface.Top <= currentWall.Top + 7.50f &&
                surface.Right - surface.Left >= 1.25f)
            .OrderBy(surface => surface.Left - currentWall.Right)
            .ThenBy(surface => surface.Top - currentWall.Top)
            .ThenByDescending(surface => surface.Right - surface.Left)
            .Cast<BonusStaticWorldSurface?>()
            .FirstOrDefault();
        if (!candidate.HasValue)
            return false;

        nextWall = BuildStaticSegment(
            MergeConnectedStaticSurface(candidate.Value, surfaces),
            playerHalfWidth);
        return nextWall.Width > 0.05f;
    }

    internal bool TryFindWallRouteContinuation(
        BonusBoardSegment currentWall,
        float playerHalfWidth,
        out BonusBoardSegment continuation)
    {
        continuation = default;
        if (mapRegistry.State != BonusMapPieceRegistryState.Ready ||
            currentWall.Width <= 0.05f)
        {
            return false;
        }

        BonusStaticWorldSurface[] surfaces = mapRegistry.GetWorldSurfaces(
            currentWall.Right + 0.05f,
            currentWall.Right + 12.0f);
        BonusStaticWorldSurface? candidate = surfaces
            .Where(surface =>
                surface.Left >= currentWall.Right + 0.10f &&
                surface.Left - currentWall.Right <= 6.25f &&
                surface.Top >= currentWall.Top - MaximumStepDown &&
                surface.Top <= currentWall.Top + 7.50f &&
                surface.Right - surface.Left >= 1.25f)
            // The closest authored face/support is the route continuation.
            // At equal height this recovers Ground 3's mandatory second face;
            // below a completed chain it recovers Ground 5's -9 exit road.
            .OrderBy(surface => surface.Left - currentWall.Right)
            .ThenBy(surface =>
                surface.Top >= currentWall.Top - 0.35f ? 0 : 1)
            .ThenBy(surface => Mathf.Abs(surface.Top - currentWall.Top))
            .ThenByDescending(surface => surface.Right - surface.Left)
            .Cast<BonusStaticWorldSurface?>()
            .FirstOrDefault();
        if (!candidate.HasValue)
            return false;

        continuation = BuildStaticSegment(
            MergeConnectedStaticSurface(candidate.Value, surfaces),
            playerHalfWidth);
        return continuation.Width > 0.05f;
    }

    internal bool TryFindWallSurfaceAtFace(
        float faceX,
        float contactFeetY,
        float playerHalfWidth,
        out BonusBoardSegment wallSurface)
    {
        wallSurface = default;
        if (mapRegistry.State != BonusMapPieceRegistryState.Ready ||
            float.IsNaN(faceX))
        {
            return false;
        }

        BonusStaticWorldSurface? candidate = mapRegistry.GetWorldSurfaces(
                faceX - 0.45f,
                faceX + 3.0f)
            .Where(surface =>
                Mathf.Abs(surface.Left - faceX) <= 0.35f &&
                surface.Top >= contactFeetY + 0.35f &&
                surface.Top <= contactFeetY + MaximumWallClimbStepUp &&
                surface.Right - surface.Left >= 0.75f)
            .OrderBy(surface => Mathf.Abs(surface.Left - faceX))
            .ThenBy(surface => surface.Top)
            .Cast<BonusStaticWorldSurface?>()
            .FirstOrDefault();
        if (!candidate.HasValue)
            return false;

        wallSurface = BuildStaticSegment(
            candidate.Value,
            playerHalfWidth);
        return wallSurface.Width > 0.05f;
    }

    internal BonusBoardSegment[] GetWallExitLandingCandidates(
        BonusBoardSegment currentWall,
        float playerHalfWidth,
        float horizontalSpeed)
    {
        if (mapRegistry.State != BonusMapPieceRegistryState.Ready ||
            currentWall.Width <= 0.05f)
        {
            return Array.Empty<BonusBoardSegment>();
        }

        // A high-speed wall exit may be physically unable to settle on the
        // nearest short road.  Section 3 deliberately places a broad authored
        // platform behind that road, so expose the ordered static candidates
        // and let the wall-flight solver select by current post-lip velocity.
        float lookAhead = Mathf.Clamp(
            18f + Mathf.Abs(horizontalSpeed) * 0.75f,
            30f,
            70f);
        BonusStaticWorldSurface[] surfaces = mapRegistry.GetWorldSurfaces(
            currentWall.Right + 0.05f,
            currentWall.Right + lookAhead);
        return surfaces
            .Where(surface =>
                surface.Left >= currentWall.Right + 0.10f &&
                surface.Top >= currentWall.Top - MaximumStepDown &&
                surface.Top <= currentWall.Top + 7.50f)
            .Select(surface => BuildStaticSegment(
                MergeConnectedStaticSurface(surface, surfaces),
                playerHalfWidth))
            .Where(segment => segment.Width >= 0.75f)
            .DistinctBy(segment =>
                $"{segment.Left:F2}:{segment.Right:F2}:{segment.Top:F2}")
            .OrderBy(segment => segment.Left)
            .ThenBy(segment => Mathf.Abs(segment.Top - currentWall.Top))
            .ThenByDescending(segment => segment.Width)
            .Take(12)
            .ToArray();
    }

    internal BonusBoardScanResult Scan(
        Vector3 playerPosition,
        PlayerMovement player,
        float horizontalSpeed = 9.5f)
    {
        if (player == null)
            return Invalid("PlayerUnavailable");

        // Bonus-stage safety-road talents can create standable colliders on a
        // layer outside PlayerMovement.jumpableLayer. Scan all physics layers
        // and filter by actual landing-surface properties instead.
        int layerMask = ~0;
        float lookAhead = CalculateLookAhead(horizontalSpeed);

        float feetY = player.playerCollider != null
            ? player.playerCollider.bounds.min.y
            : playerPosition.y - 0.9f;
        float playerHalfWidth = player.playerCollider != null
            ? player.playerCollider.bounds.extents.x
            : 0.35f;

        if (!TryFindSupportSurface(
                playerPosition.x,
                feetY,
                layerMask,
                out SurfaceSample support))
        {
            if (TryBuildStaticScan(
                    playerPosition,
                    feetY,
                    playerHalfWidth,
                    lookAhead,
                    out BonusBoardScanResult staticFallback))
            {
                return staticFallback;
            }

            return Invalid(
                mapRegistry.State == BonusMapPieceRegistryState.Ready
                    ? "SupportSurfaceNotFound:StaticMapNoMatch"
                    : $"SupportSurfaceNotFound:StaticMap{mapRegistry.State}[{mapRegistry.StatusReason}]");
        }

        float currentLeft = FindBoundary(
            playerPosition.x,
            -1f,
            MaximumLookBehind,
            support.Top,
            feetY,
            layerMask);
        float currentRight = FindBoundary(
            playerPosition.x,
            1f,
            lookAhead,
            support.Top,
            feetY,
            layerMask);

        BonusBoardSegment current = EnrichFromStaticMap(BuildSegment(
            currentLeft,
            currentRight,
            support,
            playerHalfWidth), playerPosition.x, support.Top, playerHalfWidth);

        float searchX = currentRight + SampleStep;
        float searchEnd = playerPosition.x + lookAhead;
        SurfaceSample nextSample = default;
        bool hasNext = false;
        float nextLeft = 0f;
        float nextInteriorX = 0f;

        while (searchX <= searchEnd)
        {
            if (TryFindReachableSurface(
                    searchX,
                    support.Top,
                    feetY,
                    layerMask,
                    out nextSample))
            {
                hasNext = true;
                nextInteriorX = searchX;
                nextLeft = RefineNextBoundary(
                    searchX - SampleStep,
                    searchX,
                    nextSample.Top,
                    support.Top,
                    feetY,
                    layerMask);
                break;
            }

            searchX += SampleStep;
        }

        if (!hasNext)
        {
            BonusBoardScanResult noLiveNext = new(true, current, false, default,
                current.SafeRight - playerPosition.x,
                float.PositiveInfinity, 0f,
                current.MapPieceName != "Unknown"
                    ? "StaticMapResolved:NextSurfaceNotFound"
                    : "NextSurfaceNotFound");
            noLiveNext = AddStaticAlternatives(
                noLiveNext,
                playerPosition.x,
                lookAhead,
                playerHalfWidth);
            return PromoteStaticContinuationWhenLiveNextMissing(noLiveNext);
        }

        float nextRight = FindBoundary(
            nextInteriorX,
            1f,
            lookAhead,
            nextSample.Top,
            feetY,
            layerMask);
        BonusBoardSegment next = EnrichFromStaticMap(BuildSegment(
            nextLeft,
            nextRight,
            nextSample,
            playerHalfWidth), nextInteriorX, nextSample.Top, playerHalfWidth);
        BonusBoardSegment[] alternatives = FindAlternativeSurfaces(
            nextInteriorX,
            feetY,
            layerMask,
            lookAhead,
            playerHalfWidth,
            next);

        BonusBoardScanResult directResult = new(
            true,
            current,
            true,
            next,
            current.SafeRight - playerPosition.x,
            Mathf.Max(0f, next.Left - current.Right),
            next.Top - current.Top,
            next.Top > current.Top + MaximumStepUp
                ? "ReadyWallClimbCandidate"
                : "Ready",
            Alternatives: alternatives);
        directResult = AddStaticAlternatives(
            directResult,
            playerPosition.x,
            lookAhead,
            playerHalfWidth);
        return PromoteNarrowStepLookahead(
            directResult,
            support.Top,
            feetY,
            layerMask,
            lookAhead,
            playerHalfWidth,
            playerPosition.x);
    }

    internal BonusBoardScanResult ScanProjectedSupport(
        BonusBoardSegment expectedSupport,
        float expectedLandingX,
        PlayerMovement player,
        float horizontalSpeed = 9.5f)
    {
        if (player == null || expectedSupport.Width <= 0.05f)
            return Invalid("ProjectedSupportUnavailable");

        int layerMask = ~0;
        float lookAhead = CalculateLookAhead(horizontalSpeed);
        float feetY = expectedSupport.Top;
        float playerHalfWidth = player.playerCollider != null
            ? player.playerCollider.bounds.extents.x
            : 0.35f;
        float interiorX = Mathf.Clamp(
            expectedLandingX,
            expectedSupport.Left + 0.02f,
            expectedSupport.Right - 0.02f);

        SurfaceSample support = new(
            expectedSupport.Top,
            expectedSupport.ColliderInstanceId,
            expectedSupport.ColliderName,
            expectedSupport.MapPieceName,
            expectedSupport.MapPieceOriginX);
        if (TryFindSurfaceAtHeight(
                interiorX, expectedSupport.Top, feetY, layerMask,
                out SurfaceSample liveSupport))
        {
            support = liveSupport;
        }

        float currentLeft = FindBoundary(
            interiorX, -1f, MaximumLookBehind,
            support.Top, feetY, layerMask);
        float currentRight = FindBoundary(
            interiorX, 1f, lookAhead,
            support.Top, feetY, layerMask);
        BonusBoardSegment current = EnrichFromStaticMap(
            BuildSegment(
                currentLeft,
                currentRight,
                support,
                playerHalfWidth),
            interiorX,
            support.Top,
            playerHalfWidth);

        float searchX = currentRight + SampleStep;
        float searchEnd = interiorX + lookAhead;
        while (searchX <= searchEnd)
        {
            if (TryFindReachableSurface(
                    searchX, support.Top, feetY, layerMask,
                    out SurfaceSample candidate))
            {
                return BuildProjectedResult(
                    current,
                    searchX,
                    candidate,
                    support.Top,
                    feetY,
                    layerMask,
                    lookAhead,
                    playerHalfWidth,
                    interiorX);
            }

            searchX += SampleStep;
        }

        BonusBoardScanResult noProjectedLiveNext = new(
            true, current, false, default,
            current.SafeRight - interiorX,
            float.PositiveInfinity, 0f,
            "ProjectedNextSurfaceNotFound");
        noProjectedLiveNext = AddStaticAlternatives(
            noProjectedLiveNext,
            interiorX,
            lookAhead,
            playerHalfWidth);
        return PromoteStaticContinuationWhenLiveNextMissing(
            noProjectedLiveNext);
    }

    private BonusBoardScanResult BuildProjectedResult(
        BonusBoardSegment current,
        float sampleX,
        SurfaceSample nextSample,
        float supportTop,
        float feetY,
        int layerMask,
        float lookAhead,
        float playerHalfWidth,
        float interiorX)
    {
        float nextLeft = RefineNextBoundary(
            sampleX - SampleStep, sampleX,
            nextSample.Top, supportTop, feetY, layerMask);
        float nextRight = FindBoundary(
            sampleX, 1f, lookAhead,
            nextSample.Top, feetY, layerMask);
        BonusBoardSegment next = EnrichFromStaticMap(
            BuildSegment(
                nextLeft,
                nextRight,
                nextSample,
                playerHalfWidth),
            sampleX,
            nextSample.Top,
            playerHalfWidth);
        BonusBoardSegment[] alternatives = FindAlternativeSurfaces(
            sampleX,
            feetY,
            layerMask,
            lookAhead,
            playerHalfWidth,
            next);
        BonusBoardScanResult directResult = new(
            true, current, true, next,
            current.SafeRight - interiorX,
            Mathf.Max(0f, next.Left - current.Right),
            next.Top - current.Top,
            next.Top > current.Top + MaximumStepUp
                ? "ProjectedWallClimbCandidate"
                : "ProjectedReady",
            Alternatives: alternatives);
        directResult = AddStaticAlternatives(
            directResult,
            interiorX,
            lookAhead,
            playerHalfWidth);
        return PromoteNarrowStepLookahead(
            directResult,
            supportTop,
            feetY,
            layerMask,
            lookAhead,
            playerHalfWidth,
            interiorX,
            projected: true);
    }

    private BonusBoardScanResult PromoteNarrowStepLookahead(
        BonusBoardScanResult direct,
        float supportTop,
        float feetY,
        int layerMask,
        float lookAhead,
        float playerHalfWidth,
        float originX,
        bool projected = false)
    {
        // Bonus stages 3/4 repeatedly use this exact scoring route:
        // a two-unit-wide raised marker, a short adjacent down-step, then a
        // real gap and a broad landing. Looking only at the adjacent down-step
        // says "do not jump" and misses both the gap and the sphere arc.
        // Promote the surface beyond that short bridge to the route target.
        BonusBoardSegment current = direct.Current;
        BonusBoardSegment bridge = direct.Next;
        bool narrowRaisedMarker = current.Width <= 2.35f;
        bool adjacentDownStep =
            direct.Gap <= 0.16f &&
            bridge.Top < current.Top - 0.35f &&
            bridge.Width <= 5.35f;
        if (!narrowRaisedMarker || !adjacentDownStep)
            return direct;

        float searchX = bridge.Right + SampleStep;
        float searchEnd = originX + lookAhead;
        while (searchX <= searchEnd)
        {
            if (!TryFindReachableSurface(
                    searchX,
                    supportTop,
                    feetY,
                    layerMask,
                    out SurfaceSample sample))
            {
                searchX += SampleStep;
                continue;
            }

            float targetLeft = RefineNextBoundary(
                searchX - SampleStep,
                searchX,
                sample.Top,
                supportTop,
                feetY,
                layerMask);
            float gapAfterBridge = targetLeft - bridge.Right;
            if (gapAfterBridge <= 0.35f)
                return direct;

            float targetRight = FindBoundary(
                searchX,
                1f,
                lookAhead,
                sample.Top,
                feetY,
                layerMask);
            BonusBoardSegment target = EnrichFromStaticMap(
                BuildSegment(
                    targetLeft,
                    targetRight,
                    sample,
                    playerHalfWidth),
                searchX,
                sample.Top,
                playerHalfWidth);
            return new BonusBoardScanResult(
                true,
                current,
                true,
                target,
                current.SafeRight - originX,
                Mathf.Max(0f, target.Left - current.Right),
                target.Top - current.Top,
                projected
                    ? "ProjectedNarrowStepLookahead"
                    : "NarrowStepLookahead",
                true,
                bridge,
                direct.Alternatives);
        }

        return direct;
    }

    internal bool TryRefreshExpectedSurface(
        BonusBoardSegment expected,
        float expectedLandingX,
        PlayerMovement player,
        out BonusBoardSegment refreshed,
        out string reason)
    {
        refreshed = expected;
        if (player == null || expected.Width <= 0.05f)
        {
            reason = "ExpectedSurfaceUnavailable";
            return false;
        }

        int layerMask = ~0;
        float feetY = expected.Top;
        float playerHalfWidth = player.playerCollider != null
            ? player.playerCollider.bounds.extents.x
            : 0.35f;
        float interiorX = Mathf.Clamp(
            expectedLandingX,
            expected.Left + 0.02f,
            expected.Right - 0.02f);
        if (!TryFindSurfaceAtHeight(
                interiorX,
                expected.Top,
                feetY,
                layerMask,
                out SurfaceSample liveSample))
        {
            reason = $"TargetProbeMiss@{interiorX:F3}";
            return false;
        }

        float leftDistance = Mathf.Max(
            1f,
            interiorX - expected.Left + 1f);
        float rightDistance = Mathf.Max(
            1f,
            expected.Right - interiorX + 1f);
        float left = FindBoundary(
            interiorX, -1f, leftDistance,
            liveSample.Top, feetY, layerMask);
        float right = FindBoundary(
            interiorX, 1f, rightDistance,
            liveSample.Top, feetY, layerMask);
        refreshed = EnrichFromStaticMap(
            BuildSegment(left, right, liveSample, playerHalfWidth),
            interiorX,
            liveSample.Top,
            playerHalfWidth);
        // This probe is made at the exact committed landing X and expected
        // height. A pooled prefab seam can legitimately change the static
        // surface annotation while the same CompositeCollider2D remains
        // continuous. Only contradicting live collider identities invalidate
        // the target; missing/static provenance is not a physical mismatch.
        bool identityMatch =
            expected.ColliderInstanceId == 0 ||
            liveSample.ColliderInstanceId == 0 ||
            liveSample.ColliderInstanceId == expected.ColliderInstanceId;
        bool heightMatch = Mathf.Abs(liveSample.Top - expected.Top) <= 0.35f;
        reason =
            $"Live=[{refreshed.Left:F3},{refreshed.Right:F3}]" +
            $"@{refreshed.Top:F3},IdentityMatch={identityMatch}," +
            $"MapPiece={refreshed.MapPieceName}#{refreshed.MapPieceInstanceId}," +
            $"Generation={refreshed.RegistryGeneration}," +
            $"HeightMatch={heightMatch}";
        return identityMatch && heightMatch;
    }

    private bool TryBuildStaticScan(
        Vector3 playerPosition,
        float feetY,
        float playerHalfWidth,
        float lookAhead,
        out BonusBoardScanResult result)
    {
        result = default;
        if (mapRegistry.State != BonusMapPieceRegistryState.Ready)
            return false;

        BonusStaticWorldSurface[] surfaces = mapRegistry.GetWorldSurfaces(
            playerPosition.x - MaximumLookBehind - 1f,
            playerPosition.x + lookAhead);
        float bodyLeft = playerPosition.x - playerHalfWidth;
        float bodyRight = playerPosition.x + playerHalfWidth;
        BonusStaticWorldSurface? support = surfaces
            .Where(surface =>
                surface.Right >= bodyLeft - 0.08f &&
                surface.Left <= bodyRight + 0.08f &&
                // Static data may repair a one-frame composite-ray miss, but
                // it must not manufacture a support a full body-height away.
                Mathf.Abs(surface.Top - feetY) <= 0.20f)
            .OrderBy(surface => Mathf.Abs(surface.Top - feetY))
            .ThenByDescending(surface =>
                Mathf.Min(bodyRight, surface.Right) -
                Mathf.Max(bodyLeft, surface.Left))
            .Cast<BonusStaticWorldSurface?>()
            .FirstOrDefault();
        if (!support.HasValue)
            return false;

        BonusBoardSegment current = BuildStaticSegment(
            MergeConnectedStaticSurface(support.Value, surfaces),
            playerHalfWidth);
        BonusStaticWorldSurface[] candidates = surfaces
            .Where(surface =>
                !IsSameStaticSurface(surface, support.Value) &&
                surface.Right >= current.Right + 0.05f &&
                surface.Left <= playerPosition.x + lookAhead &&
                surface.Top >= current.Top - MaximumStepDown &&
                surface.Top <= current.Top + MaximumWallClimbStepUp)
            .OrderBy(surface => Mathf.Max(0f, surface.Left - current.Right))
            .ThenBy(surface =>
                surface.Top <= current.Top + 0.35f ? 0 :
                surface.Top <= current.Top + MaximumStepUp ? 1 : 2)
            .ThenBy(surface => Mathf.Abs(surface.Top - current.Top))
            .ThenByDescending(surface => surface.Right - surface.Left)
            .ToArray();

        if (candidates.Length == 0)
        {
            result = new BonusBoardScanResult(
                true,
                current,
                false,
                default,
                current.SafeRight - playerPosition.x,
                float.PositiveInfinity,
                0f,
                "StaticMapFallback:NextSurfaceNotFound");
            return true;
        }

        float firstEntry = Mathf.Max(0f, candidates[0].Left - current.Right);
        BonusStaticWorldSurface chosenSurface = candidates
            .Where(surface =>
                Mathf.Abs(
                    Mathf.Max(0f, surface.Left - current.Right) -
                    firstEntry) <= 0.20f)
            .OrderBy(surface =>
                surface.Top <= current.Top + 0.35f ? 0 :
                surface.Top <= current.Top + MaximumStepUp ? 1 : 2)
            .ThenBy(surface => Mathf.Abs(surface.Top - current.Top))
            .ThenByDescending(surface => surface.Right)
            .First();
        BonusBoardSegment next = BuildStaticSegment(
            MergeConnectedStaticSurface(chosenSurface, surfaces),
            playerHalfWidth);
        BonusBoardSegment[] alternatives = candidates
            .Where(surface => !IsSameStaticSurface(surface, chosenSurface))
            .Select(surface => BuildStaticSegment(
                MergeConnectedStaticSurface(surface, surfaces),
                playerHalfWidth))
            .DistinctBy(segment =>
                $"{segment.Left:F2}:{segment.Right:F2}:{segment.Top:F2}")
            .Take(20)
            .ToArray();
        result = new BonusBoardScanResult(
            true,
            current,
            true,
            next,
            current.SafeRight - playerPosition.x,
            Mathf.Max(0f, next.Left - current.Right),
            next.Top - current.Top,
            "StaticMapFallback:LiveSupportMissing",
            Alternatives: alternatives);
        return true;
    }

    private BonusBoardScanResult AddStaticAlternatives(
        BonusBoardScanResult scan,
        float playerX,
        float lookAhead,
        float playerHalfWidth)
    {
        if (!scan.IsValid ||
            mapRegistry.State != BonusMapPieceRegistryState.Ready)
        {
            return scan;
        }

        List<BonusBoardSegment> alternatives = scan.Alternatives?
            .ToList() ?? new List<BonusBoardSegment>();
        BonusStaticWorldSurface[] staticSurfaces =
            mapRegistry.GetWorldSurfaces(
                scan.Current.Left - 0.5f,
                playerX + lookAhead);
        foreach (BonusStaticWorldSurface surface in staticSurfaces)
        {
            if (surface.Right <= scan.Current.Right + 0.05f ||
                surface.Top < scan.Current.Top - MaximumStepDown ||
                surface.Top > scan.Current.Top + MaximumWallClimbStepUp)
            {
                continue;
            }

            BonusBoardSegment candidate = BuildStaticSegment(
                MergeConnectedStaticSurface(surface, staticSurfaces),
                playerHalfWidth);
            bool duplicate = alternatives.Any(existing =>
                Mathf.Abs(existing.Left - candidate.Left) <= 0.12f &&
                Mathf.Abs(existing.Right - candidate.Right) <= 0.12f &&
                Mathf.Abs(existing.Top - candidate.Top) <= 0.12f);
            bool selected = scan.HasNext &&
                Mathf.Abs(scan.Next.Left - candidate.Left) <= 0.12f &&
                Mathf.Abs(scan.Next.Right - candidate.Right) <= 0.12f &&
                Mathf.Abs(scan.Next.Top - candidate.Top) <= 0.12f;
            if (!duplicate && !selected)
                alternatives.Add(candidate);
        }

        return scan with
        {
            Alternatives = alternatives
                .OrderBy(segment => segment.Left)
                .ThenBy(segment => segment.Top)
                .Take(24)
                .ToArray()
        };
    }

    private static BonusBoardScanResult
        PromoteStaticContinuationWhenLiveNextMissing(
            BonusBoardScanResult scan)
    {
        if (!scan.IsValid || scan.HasNext ||
            scan.Alternatives == null || scan.Alternatives.Length == 0)
        {
            return scan;
        }

        BonusBoardSegment selected = scan.Alternatives
            .Where(candidate =>
                candidate.RegistryGeneration > 0 &&
                candidate.StaticSurfaceIndex >= 0 &&
                candidate.Right >= scan.Current.Right + 0.05f)
            .OrderBy(candidate =>
                Mathf.Max(0f, candidate.Left - scan.Current.Right))
            .ThenBy(candidate =>
                candidate.Top <= scan.Current.Top + 0.35f ? 0 :
                candidate.Top <= scan.Current.Top + MaximumStepUp ? 1 : 2)
            .ThenBy(candidate =>
                Mathf.Abs(candidate.Top - scan.Current.Top))
            .ThenByDescending(candidate => candidate.Width)
            .FirstOrDefault();
        if (selected.Width < 0.20f)
            return scan;

        return scan with
        {
            HasNext = true,
            Next = selected,
            Gap = Mathf.Max(0f, selected.Left - scan.Current.Right),
            HeightDelta = selected.Top - scan.Current.Top,
            Reason = "StaticContinuation:LiveNextMissing",
            Alternatives = scan.Alternatives
                .Where(candidate =>
                    Mathf.Abs(candidate.Left - selected.Left) > 0.12f ||
                    Mathf.Abs(candidate.Right - selected.Right) > 0.12f ||
                    Mathf.Abs(candidate.Top - selected.Top) > 0.12f)
                .ToArray()
        };
    }

    private BonusBoardSegment EnrichFromStaticMap(
        BonusBoardSegment segment,
        float probeX,
        float probeTop,
        float playerHalfWidth)
    {
        if (mapRegistry.State != BonusMapPieceRegistryState.Ready)
            return segment;

        if (!mapRegistry.TryResolveTopSurface(
                new Vector2(probeX, probeTop),
                probeX,
                out BonusMapPieceResolution resolution) ||
            !resolution.SurfaceVerified ||
            resolution.IsAmbiguous)
        {
            return segment;
        }

        BonusStaticWorldSurface[] surfaces = mapRegistry.GetWorldSurfaces(
            segment.Left - 0.35f,
            segment.Right + 0.35f);
        BonusStaticWorldSurface? match = surfaces
            .Where(surface =>
                surface.MapPieceInstanceId ==
                    resolution.MapPieceInstanceId &&
                surface.RegistryGeneration ==
                    resolution.RegistryGeneration &&
                surface.SurfaceIndex == resolution.SurfaceIndex &&
                Mathf.Abs(surface.Top - probeTop) <= 0.12f)
            .Cast<BonusStaticWorldSurface?>()
            .FirstOrDefault();
        if (!match.HasValue)
            return segment;

        // Live raycasts own both the collider and the physically continuous
        // bounds. A continuous surface can cross pooled prefab seams; replacing
        // it with one prefab's authored interval would invent a false gap.
        // Static data therefore annotates a live segment, never shrinks it.
        return segment with
        {
            MapPieceName = match.Value.MapPieceName,
            MapPieceOriginX = match.Value.OriginX,
            MapPieceInstanceId = match.Value.MapPieceInstanceId,
            RegistryGeneration = match.Value.RegistryGeneration,
            StaticSurfaceIndex = match.Value.SurfaceIndex
        };
    }

    private static BonusStaticWorldSurface MergeConnectedStaticSurface(
        BonusStaticWorldSurface seed,
        IReadOnlyList<BonusStaticWorldSurface> surfaces)
    {
        float left = seed.Left;
        float right = seed.Right;
        bool expanded;
        do
        {
            expanded = false;
            foreach (BonusStaticWorldSurface candidate in surfaces)
            {
                // A CompositeCollider2D joins equal-height authored surfaces
                // across pooled prefab seams. Geometry must follow that
                // physical continuity even though provenance remains anchored
                // to the seed surface; otherwise Ground 5's [679,684] floor
                // is artificially shortened before the adjacent Ground 3
                // continuation and a safe natural drop is rejected.
                if (candidate.RegistryGeneration != seed.RegistryGeneration ||
                    Mathf.Abs(candidate.Top - seed.Top) > 0.10f ||
                    candidate.Right < left - 0.10f ||
                    candidate.Left > right + 0.10f)
                {
                    continue;
                }

                float expandedLeft = Mathf.Min(left, candidate.Left);
                float expandedRight = Mathf.Max(right, candidate.Right);
                if (expandedLeft < left - 0.001f ||
                    expandedRight > right + 0.001f)
                {
                    left = expandedLeft;
                    right = expandedRight;
                    expanded = true;
                }
            }
        } while (expanded);

        return seed with { Left = left, Right = right };
    }

    private static BonusBoardSegment BuildStaticSegment(
        BonusStaticWorldSurface surface,
        float playerHalfWidth)
    {
        SurfaceSample sample = new(
            surface.Top,
            0,
            $"StaticMap/{surface.MapPieceName}",
            surface.MapPieceName,
            surface.OriginX);
        BonusBoardSegment segment = BuildSegment(
            surface.Left,
            surface.Right,
            sample,
            playerHalfWidth);
        return segment with
        {
            MapPieceInstanceId = surface.MapPieceInstanceId,
            RegistryGeneration = surface.RegistryGeneration,
            StaticSurfaceIndex = surface.SurfaceIndex
        };
    }

    private static bool IsSameStaticSurface(
        BonusStaticWorldSurface left,
        BonusStaticWorldSurface right) =>
        left.MapPieceInstanceId == right.MapPieceInstanceId &&
        left.SurfaceIndex == right.SurfaceIndex &&
        left.RegistryGeneration == right.RegistryGeneration;

    private static float DistanceOutside(float value, float left, float right)
    {
        if (value < left)
            return left - value;
        if (value > right)
            return value - right;
        return 0f;
    }

    private static BonusBoardScanResult Invalid(string reason) =>
        new(false, default, false, default, 0f, 0f, 0f, reason);

    private static float CalculateLookAhead(float horizontalSpeed)
    {
        // Normal running only needs the original 28-unit window. Spirit Boost
        // can exceed 49 units/s, so grow the scan far enough to include a full
        // boosted jump instead of planning against a truncated route.
        float speed = Mathf.Abs(horizontalSpeed);
        return Mathf.Clamp(
            MinimumLookAhead + Mathf.Max(0f, speed - 18f) * 1.4f,
            MinimumLookAhead,
            MaximumLookAhead);
    }

    private static BonusBoardSegment BuildSegment(
        float left,
        float right,
        SurfaceSample sample,
        float playerHalfWidth)
    {
        float width = Mathf.Max(0f, right - left);
        float desiredMargin = Mathf.Max(0f, playerHalfWidth) +
                              EdgeSafetyMargin;
        float largestMargin = Mathf.Max(
            0f,
            (width - MinimumSafeWidth) * 0.5f);
        float margin = Mathf.Min(desiredMargin, largestMargin);
        float safeLeft = left + margin;
        float safeRight = right - margin;
        if (safeRight - safeLeft < MinimumSafeWidth)
        {
            float center = (left + right) * 0.5f;
            float halfSafeWidth = Mathf.Min(
                MinimumSafeWidth * 0.5f,
                width * 0.5f);
            safeLeft = center - halfSafeWidth;
            safeRight = center + halfSafeWidth;
        }

        return new(left, right, sample.Top, safeLeft, safeRight,
            sample.ColliderInstanceId, sample.ColliderName,
            sample.MapPieceName, sample.MapPieceOriginX);
    }

    private static BonusBoardSegment[] FindAlternativeSurfaces(
        float sampleX,
        float feetY,
        int layerMask,
        float lookAhead,
        float playerHalfWidth,
        BonusBoardSegment selected)
    {
        List<SurfaceSample> samples = new();
        foreach (RaycastHit2D hit in GetVerticalHits(sampleX, feetY, layerMask))
        {
            if (!IsLandingSurface(hit) ||
                hit.point.y < feetY - MaximumStepDown ||
                hit.point.y > feetY + MaximumWallClimbStepUp ||
                Mathf.Abs(hit.point.y - selected.Top) <= SameSurfaceTolerance)
            {
                continue;
            }

            SurfaceSample candidate = FromHit(hit);
            if (samples.Any(existing =>
                    existing.ColliderInstanceId == candidate.ColliderInstanceId &&
                    Mathf.Abs(existing.Top - candidate.Top) <=
                        SameSurfaceTolerance))
            {
                continue;
            }
            samples.Add(candidate);
        }

        return samples
            .Select(sample =>
            {
                float left = FindBoundary(
                    sampleX,
                    -1f,
                    lookAhead,
                    sample.Top,
                    feetY,
                    layerMask);
                float right = FindBoundary(
                    sampleX,
                    1f,
                    lookAhead,
                    sample.Top,
                    feetY,
                    layerMask);
                return BuildSegment(
                    left,
                    right,
                    sample,
                    playerHalfWidth);
            })
            .Where(segment => segment.Width >= 0.20f)
            .OrderBy(segment => segment.Top)
            .ThenBy(segment => segment.Left)
            .Take(12)
            .ToArray();
    }

    private static float FindBoundary(
        float startX,
        float direction,
        float maximumDistance,
        float surfaceTop,
        float feetY,
        int layerMask)
    {
        float lastMatchingX = startX;
        float firstNonMatchingX = startX;
        bool foundNonMatch = false;

        for (float distance = SampleStep;
             distance <= maximumDistance;
             distance += SampleStep)
        {
            float x = startX + direction * distance;
            if (TryFindSurfaceAtHeight(
                    x, surfaceTop, feetY, layerMask, out _))
            {
                lastMatchingX = x;
                continue;
            }

            firstNonMatchingX = x;
            foundNonMatch = true;
            break;
        }

        if (!foundNonMatch)
            return lastMatchingX;

        float matching = lastMatchingX;
        float nonMatching = firstNonMatchingX;
        for (int index = 0; index < 7; index++)
        {
            float midpoint = (matching + nonMatching) * 0.5f;
            if (TryFindSurfaceAtHeight(
                    midpoint, surfaceTop, feetY, layerMask, out _))
                matching = midpoint;
            else
                nonMatching = midpoint;
        }

        return matching;
    }

    private static float RefineNextBoundary(
        float noSurfaceX,
        float surfaceX,
        float nextTop,
        float supportTop,
        float feetY,
        int layerMask)
    {
        float missing = noSurfaceX;
        float present = surfaceX;
        for (int index = 0; index < 7; index++)
        {
            float midpoint = (missing + present) * 0.5f;
            if (TryFindReachableSurface(
                    midpoint, supportTop, feetY, layerMask,
                    out SurfaceSample sample) &&
                Mathf.Abs(sample.Top - nextTop) <= SameSurfaceTolerance)
                present = midpoint;
            else
                missing = midpoint;
        }

        return present;
    }

    private static bool TryFindSupportSurface(
        float x,
        float feetY,
        int layerMask,
        out SurfaceSample result)
    {
        result = default;
        float bestDistance = float.PositiveInfinity;
        foreach (RaycastHit2D hit in GetVerticalHits(x, feetY, layerMask))
        {
            if (!IsLandingSurface(hit)) continue;
            float distance = Mathf.Abs(hit.point.y - feetY);
            if (distance > 1.25f || distance >= bestDistance) continue;
            bestDistance = distance;
            result = FromHit(hit);
        }
        return bestDistance < float.PositiveInfinity;
    }

    private static bool TryFindSurfaceAtHeight(
        float x,
        float surfaceTop,
        float feetY,
        int layerMask,
        out SurfaceSample result)
    {
        result = default;
        float bestDifference = float.PositiveInfinity;
        foreach (RaycastHit2D hit in GetVerticalHits(x, feetY, layerMask))
        {
            if (!IsLandingSurface(hit)) continue;
            float difference = Mathf.Abs(hit.point.y - surfaceTop);
            if (difference > SameSurfaceTolerance || difference >= bestDifference)
                continue;
            bestDifference = difference;
            result = FromHit(hit);
        }
        return bestDifference < float.PositiveInfinity;
    }

    private static bool TryFindReachableSurface(
        float x,
        float supportTop,
        float feetY,
        int layerMask,
        out SurfaceSample result)
    {
        result = default;
        float highestDirectTop = float.NegativeInfinity;
        float lowestWallClimbTop = float.PositiveInfinity;
        SurfaceSample directResult = default;
        SurfaceSample wallClimbResult = default;
        foreach (RaycastHit2D hit in GetVerticalHits(x, feetY, layerMask))
        {
            if (!IsLandingSurface(hit) ||
                hit.point.y < supportTop - MaximumStepDown ||
                hit.point.y > supportTop + MaximumWallClimbStepUp)
                continue;

            if (hit.point.y <= supportTop + MaximumStepUp)
            {
                if (hit.point.y <= highestDirectTop)
                    continue;
                highestDirectTop = hit.point.y;
                directResult = FromHit(hit);
                continue;
            }

            // When more than one elevated surface overlaps this X coordinate,
            // climb the lowest one first. It is the physically reachable step
            // in a multi-level route.
            if (hit.point.y >= lowestWallClimbTop)
                continue;
            lowestWallClimbTop = hit.point.y;
            wallClimbResult = FromHit(hit);
        }

        if (highestDirectTop > float.NegativeInfinity)
        {
            result = directResult;
            return true;
        }

        if (lowestWallClimbTop < float.PositiveInfinity)
        {
            result = wallClimbResult;
            return true;
        }

        return false;
    }

    private static RaycastHit2D[] GetVerticalHits(
        float x,
        float feetY,
        int layerMask) =>
        Physics2D.RaycastAll(
            new Vector2(x, feetY + MaximumWallClimbStepUp + 1f),
            Vector2.down,
            MaximumWallClimbStepUp + MaximumStepDown + 2f,
            layerMask);

    private static bool IsLandingSurface(RaycastHit2D hit)
    {
        Collider2D collider = hit.collider;
        return collider != null &&
               collider.enabled &&
               !collider.isTrigger &&
               collider.GetComponentInParent<PlayerMovement>() == null &&
               collider.GetComponentInParent<BonusStageSpike>() == null &&
               hit.normal.y >= 0.50f;
    }

    private static SurfaceSample FromHit(RaycastHit2D hit)
    {
        Transform cursor = hit.collider.transform;
        Transform mapPiece = null;
        int depth = 0;
        while (cursor != null && depth++ < 12)
        {
            if (TryNormalizeGroundName(cursor.name, out _))
                mapPiece = cursor;
            cursor = cursor.parent;
        }

        string mapPieceName = mapPiece != null &&
            TryNormalizeGroundName(mapPiece.name, out string normalized)
                ? normalized
                : "Unknown";
        float originX = mapPiece != null
            ? mapPiece.position.x
            : float.NaN;
        return new(
            hit.point.y,
            hit.collider.GetInstanceID(),
            hit.collider.name,
            mapPieceName,
            originX);
    }

    private static bool TryNormalizeGroundName(string name, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(name) ||
            !name.StartsWith("Ground ", StringComparison.OrdinalIgnoreCase))
            return false;

        int index = "Ground ".Length;
        int start = index;
        while (index < name.Length && char.IsDigit(name[index]))
            index++;
        if (index == start)
            return false;
        normalized = $"Ground {name.Substring(start, index - start)}";
        return true;
    }

    private readonly record struct SurfaceSample(
        float Top,
        int ColliderInstanceId,
        string ColliderName,
        string MapPieceName,
        float MapPieceOriginX);
}
