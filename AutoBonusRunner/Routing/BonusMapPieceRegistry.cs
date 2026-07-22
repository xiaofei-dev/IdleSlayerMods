using UnityEngine;

namespace AutoBonusRunner.Routing;

internal enum BonusMapPieceRegistryState
{
    Pending,
    Ready
}

internal readonly record struct BonusStaticWorldSurface(
    string MapPieceName,
    int MapPieceInstanceId,
    int RegistryGeneration,
    int SurfaceIndex,
    float Left,
    float Right,
    float Top,
    float OriginX,
    float OriginY,
    float LocalLeft,
    float LocalRight,
    float LocalTop);

internal readonly record struct BonusMapPieceResolution(
    bool IsValid,
    string MapPieceName,
    int MapPieceInstanceId,
    int RegistryGeneration,
    Vector2 Origin,
    Vector2 LocalPoint,
    int SurfaceIndex,
    float SurfaceError,
    bool SurfaceVerified,
    bool IsAmbiguous);

/// <summary>
/// Resolves hits from the bonus map's shared CompositeCollider2D back to the
/// live ground prefab that contributed the geometry. The collider itself is
/// owned by "All Pools", so collider ancestry cannot provide this identity.
/// </summary>
internal sealed class BonusMapPieceRegistry
{
    private const float ExpectedPieceStride = 24f;
    private const float PieceStrideTolerance = 0.30f;
    private const float TransformTolerance = 0.03f;
    private const float SurfaceMatchTolerance = 0.12f;
    private const float PieceBoundsTolerance = 0.20f;

    private static readonly Dictionary<string, GroundTemplate> Templates =
        BuildTemplates();

    // BonusMap grounds form a cycle. Object pooling moves the oldest clone
    // forward, so the live left-to-right order can be any cyclic rotation.
    private static readonly Dictionary<int, string[]> ExpectedSectionCycles =
        new()
        {
            [0] = new[] { "Ground 1", "Ground 1", "Ground 2" },
            [1] = new[] { "Ground 3", "Ground 4", "Ground 3", "Ground 5" },
            [2] = new[] { "Ground 6", "Ground 7", "Ground 6" },
            [3] = new[] { "Ground 7", "Ground 8", "Ground 7" }
        };

    private readonly List<LivePiece> pieces = new();
    private int topologyHash;
    private bool hasTopologyHash;

    internal BonusMapPieceRegistryState State { get; private set; } =
        BonusMapPieceRegistryState.Pending;

    internal int SectionIndex { get; private set; } = -1;

    /// <summary>
    /// Changes whenever the section, clone set, or a pooled clone's live
    /// transform changes. Plans may retain this value to detect stale routes.
    /// </summary>
    internal int Generation { get; private set; }

    internal string StatusReason { get; private set; } = "NotRefreshed";

    internal int PieceCount => pieces.Count;

    internal bool Refresh(int sectionIndex)
    {
        if (sectionIndex != SectionIndex)
        {
            SectionIndex = sectionIndex;
            SetPending("SectionChanged", clearPieces: true);
        }

        if (!ExpectedSectionCycles.TryGetValue(
                sectionIndex,
                out string[] expectedCycle))
        {
            SetPending($"UnsupportedSection:{sectionIndex}", clearPieces: true);
            return false;
        }

        if (!TryFindLevelRoot(sectionIndex, out Transform levelRoot))
        {
            // currentSectionIndex changes before the next pooled level is
            // populated. Treat that interval as pending instead of resolving
            // against the still-active previous level.
            SetPending($"LevelRootUnavailable:{sectionIndex}", clearPieces: true);
            return false;
        }

        List<LivePiece> discovered = new(expectedCycle.Length + 1);
        for (int index = 0; index < levelRoot.childCount; index++)
        {
            Transform child = levelRoot.GetChild(index);
            if (child == null ||
                child.gameObject == null ||
                !child.gameObject.activeInHierarchy ||
                !TryNormalizeGroundName(child.name, out string normalized) ||
                !Templates.TryGetValue(normalized, out GroundTemplate template))
            {
                continue;
            }

            discovered.Add(new LivePiece(child, template));
        }

        if (discovered.Count == 0)
        {
            SetPending(
                $"PieceCountMismatch:Expected={expectedCycle.Length}," +
                $"Found={discovered.Count}",
                clearPieces: true);
            return false;
        }

        discovered.Sort(static (left, right) =>
            left.Root.position.x.CompareTo(right.Root.position.x));

        // Pooling does not keep the full authored cycle active. One clone is
        // commonly removed behind the player before its replacement appears
        // ahead, and during the hand-off an extra clone can briefly coexist.
        // Every discovered clone is still useful local truth as long as the
        // active left-to-right sequence is a contiguous run of the authored
        // cycle and its transforms retain the 24-unit stride. Only surfaces
        // from these currently active clones are exposed; no stale transform
        // is cached as a ghost platform.
        if (!IsContiguousCyclicRun(discovered, expectedCycle))
        {
            SetPending("PieceCycleMismatch", clearPieces: true);
            return false;
        }

        if (!HasStableTransforms(discovered, out string transformReason))
        {
            SetPending(transformReason, clearPieces: true);
            return false;
        }

        pieces.Clear();
        pieces.AddRange(discovered);
        State = BonusMapPieceRegistryState.Ready;
        StatusReason = discovered.Count == expectedCycle.Length
            ? "Ready"
            : $"ReadyLocalCoverage:{discovered.Count}/{expectedCycle.Length}";
        ObserveTopology(forceGenerationChange: !hasTopologyHash);
        return true;
    }

    internal void Reset(string reason = "Reset")
    {
        SectionIndex = -1;
        SetPending(reason, clearPieces: true);
    }

    /// <summary>
    /// Resolves an arbitrary point to a live ground clone. Exposed top matches
    /// are surface-verified; a point on a wall can still receive a spatial
    /// piece match, but callers must not treat it as a verified landing top.
    /// </summary>
    internal bool TryResolve(
        Vector2 worldPoint,
        float playerX,
        out BonusMapPieceResolution resolution)
    {
        resolution = default;
        if (!EnsureLiveTopology())
            return false;

        if (TryResolveVerifiedSurface(
                worldPoint,
                playerX,
                out resolution))
        {
            return true;
        }

        float bestScore = float.PositiveInfinity;
        float secondScore = float.PositiveInfinity;
        LivePiece bestPiece = null;
        Vector2 bestLocalPoint = default;

        foreach (LivePiece piece in pieces)
        {
            Vector3 local3 = piece.Root.InverseTransformPoint(worldPoint);
            Vector2 local = new(local3.x, local3.y);
            if (local.x < piece.Template.LocalLeft - PieceBoundsTolerance ||
                local.x > piece.Template.LocalRight + PieceBoundsTolerance ||
                local.y < piece.Template.LocalBottom - PieceBoundsTolerance ||
                local.y > piece.Template.LocalTop + PieceBoundsTolerance)
            {
                continue;
            }

            float score = Mathf.Abs(local.x) * 0.01f +
                          DirectionTieBreak(
                              piece.Root.position.x,
                              worldPoint.x,
                              playerX);
            if (score < bestScore)
            {
                secondScore = bestScore;
                bestScore = score;
                bestPiece = piece;
                bestLocalPoint = local;
            }
            else if (score < secondScore)
            {
                secondScore = score;
            }
        }

        if (bestPiece == null)
            return false;

        Vector3 origin = bestPiece.Root.position;
        resolution = new BonusMapPieceResolution(
            true,
            bestPiece.Template.Name,
            bestPiece.InstanceId,
            Generation,
            new Vector2(origin.x, origin.y),
            bestLocalPoint,
            -1,
            float.NaN,
            false,
            secondScore - bestScore <= 0.02f);
        return true;
    }

    internal bool TryResolveTopSurface(
        Vector2 worldPoint,
        float playerX,
        out BonusMapPieceResolution resolution)
    {
        resolution = default;
        return EnsureLiveTopology() &&
               TryResolveVerifiedSurface(
                   worldPoint,
                   playerX,
                   out resolution);
    }

    /// <summary>
    /// Returns the current world-space exposed tops. Origins are read from the
    /// live Transform on every call so pooled clone recycling is reflected.
    /// </summary>
    internal BonusStaticWorldSurface[] GetWorldSurfaces(
        float worldLeft,
        float worldRight)
    {
        if (!EnsureLiveTopology())
            return Array.Empty<BonusStaticWorldSurface>();

        float minimum = Mathf.Min(worldLeft, worldRight);
        float maximum = Mathf.Max(worldLeft, worldRight);
        List<BonusStaticWorldSurface> result = new();

        foreach (LivePiece piece in pieces)
        {
            Vector3 origin = piece.Root.position;
            for (int index = 0;
                 index < piece.Template.Surfaces.Length;
                 index++)
            {
                LocalSurface local = piece.Template.Surfaces[index];
                Vector3 worldStart = piece.Root.TransformPoint(
                    new Vector3(local.Left, local.Top, 0f));
                Vector3 worldEnd = piece.Root.TransformPoint(
                    new Vector3(local.Right, local.Top, 0f));
                float left = Mathf.Min(worldStart.x, worldEnd.x);
                float right = Mathf.Max(worldStart.x, worldEnd.x);
                if (right < minimum || left > maximum)
                    continue;

                result.Add(new BonusStaticWorldSurface(
                    piece.Template.Name,
                    piece.InstanceId,
                    Generation,
                    index,
                    left,
                    right,
                    (worldStart.y + worldEnd.y) * 0.5f,
                    origin.x,
                    origin.y,
                    local.Left,
                    local.Right,
                    local.Top));
            }
        }

        return result
            .OrderBy(surface => surface.Left)
            .ThenBy(surface => surface.Top)
            .ToArray();
    }

    private bool TryResolveVerifiedSurface(
        Vector2 worldPoint,
        float playerX,
        out BonusMapPieceResolution resolution)
    {
        resolution = default;
        float bestScore = float.PositiveInfinity;
        float secondScore = float.PositiveInfinity;
        LivePiece bestPiece = null;
        Vector2 bestLocalPoint = default;
        int bestSurfaceIndex = -1;
        float bestSurfaceError = float.PositiveInfinity;

        foreach (LivePiece piece in pieces)
        {
            Vector3 local3 = piece.Root.InverseTransformPoint(worldPoint);
            Vector2 localPoint = new(local3.x, local3.y);
            for (int index = 0;
                 index < piece.Template.Surfaces.Length;
                 index++)
            {
                LocalSurface surface = piece.Template.Surfaces[index];
                float horizontalError = DistanceOutside(
                    localPoint.x,
                    surface.Left,
                    surface.Right);
                float verticalError = Mathf.Abs(localPoint.y - surface.Top);
                if (horizontalError > SurfaceMatchTolerance ||
                    verticalError > SurfaceMatchTolerance)
                {
                    continue;
                }

                float score = verticalError * 10f +
                              horizontalError * 4f +
                              Mathf.Abs(
                                  localPoint.x -
                                  (surface.Left + surface.Right) * 0.5f) *
                              0.001f +
                              DirectionTieBreak(
                                  piece.Root.position.x,
                                  worldPoint.x,
                                  playerX);
                if (score < bestScore)
                {
                    secondScore = bestScore;
                    bestScore = score;
                    bestPiece = piece;
                    bestLocalPoint = localPoint;
                    bestSurfaceIndex = index;
                    bestSurfaceError = Mathf.Max(
                        horizontalError,
                        verticalError);
                }
                else if (score < secondScore)
                {
                    secondScore = score;
                }
            }
        }

        if (bestPiece == null)
            return false;

        Vector3 origin = bestPiece.Root.position;
        resolution = new BonusMapPieceResolution(
            true,
            bestPiece.Template.Name,
            bestPiece.InstanceId,
            Generation,
            new Vector2(origin.x, origin.y),
            bestLocalPoint,
            bestSurfaceIndex,
            bestSurfaceError,
            true,
            secondScore - bestScore <= 0.02f);
        return true;
    }

    private bool EnsureLiveTopology()
    {
        if (State != BonusMapPieceRegistryState.Ready || pieces.Count == 0)
            return false;

        foreach (LivePiece piece in pieces)
        {
            if (piece.Root == null ||
                piece.Root.gameObject == null ||
                !piece.Root.gameObject.activeInHierarchy)
            {
                SetPending("LivePieceUnavailable", clearPieces: false);
                return false;
            }
        }

        ObserveTopology(forceGenerationChange: false);
        return true;
    }

    private void ObserveTopology(bool forceGenerationChange)
    {
        int currentHash = CalculateTopologyHash(pieces);
        if (forceGenerationChange ||
            !hasTopologyHash ||
            currentHash != topologyHash)
        {
            Generation++;
            topologyHash = currentHash;
            hasTopologyHash = true;
        }
    }

    private void SetPending(string reason, bool clearPieces)
    {
        bool changed = State != BonusMapPieceRegistryState.Pending ||
                       !string.Equals(
                           StatusReason,
                           reason,
                           StringComparison.Ordinal);
        State = BonusMapPieceRegistryState.Pending;
        StatusReason = reason;
        if (clearPieces)
            pieces.Clear();
        if (changed)
            Generation++;
    }

    private static bool TryFindLevelRoot(
        int sectionIndex,
        out Transform levelRoot)
    {
        levelRoot = null;
        GameObject allPools = GameObject.Find("All Pools");
        if (allPools == null)
            return false;

        string expectedName = $"Bonus Map Level {sectionIndex}";
        Transform poolsTransform = allPools.transform;
        for (int index = 0; index < poolsTransform.childCount; index++)
        {
            Transform child = poolsTransform.GetChild(index);
            if (child != null &&
                string.Equals(
                    child.name,
                    expectedName,
                    StringComparison.OrdinalIgnoreCase))
            {
                levelRoot = child;
                return true;
            }
        }

        return false;
    }

    private static bool IsContiguousCyclicRun(
        IReadOnlyList<LivePiece> discovered,
        IReadOnlyList<string> expected)
    {
        if (discovered.Count == 0 || expected.Count == 0)
            return false;

        for (int offset = 0; offset < expected.Count; offset++)
        {
            bool match = true;
            for (int index = 0; index < discovered.Count; index++)
            {
                if (string.Equals(
                        discovered[index].Template.Name,
                        expected[(offset + index) % expected.Count],
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                match = false;
                break;
            }

            if (match)
                return true;
        }

        return false;
    }

    private static bool HasStableTransforms(
        IReadOnlyList<LivePiece> discovered,
        out string reason)
    {
        reason = string.Empty;
        for (int index = 0; index < discovered.Count; index++)
        {
            Transform root = discovered[index].Root;
            Vector3 scale = root.lossyScale;
            if (Mathf.Abs(scale.x - 1f) > TransformTolerance ||
                Mathf.Abs(scale.y - 1f) > TransformTolerance ||
                Mathf.Abs(Mathf.DeltaAngle(root.eulerAngles.z, 0f)) > 0.10f)
            {
                reason = $"UnsupportedTransform:{root.name}";
                return false;
            }

            if (index == 0)
                continue;

            Vector3 previous = discovered[index - 1].Root.position;
            Vector3 current = root.position;
            if (Mathf.Abs(
                    current.x - previous.x - ExpectedPieceStride) >
                PieceStrideTolerance)
            {
                reason = "PieceStrideMismatch";
                return false;
            }

            if (Mathf.Abs(current.y - previous.y) > TransformTolerance)
            {
                reason = "PieceHeightOriginMismatch";
                return false;
            }
        }

        return true;
    }

    private static int CalculateTopologyHash(IEnumerable<LivePiece> livePieces)
    {
        HashCode hash = new();
        foreach (LivePiece piece in livePieces.OrderBy(piece => piece.Root.position.x))
        {
            Vector3 position = piece.Root.position;
            hash.Add(piece.InstanceId);
            hash.Add(piece.Template.Name, StringComparer.OrdinalIgnoreCase);
            hash.Add(Mathf.RoundToInt(position.x * 20f));
            hash.Add(Mathf.RoundToInt(position.y * 20f));
        }
        return hash.ToHashCode();
    }

    private static float DistanceOutside(float value, float left, float right)
    {
        if (value < left)
            return left - value;
        if (value > right)
            return value - right;
        return 0f;
    }

    private static float DirectionTieBreak(
        float pieceOriginX,
        float hitX,
        float playerX)
    {
        if (float.IsNaN(playerX) || Mathf.Abs(hitX - playerX) < 0.01f)
            return 0f;

        bool pointIsAhead = hitX > playerX;
        bool pieceCenterIsAheadOfPoint = pieceOriginX >= hitX;
        return pointIsAhead == pieceCenterIsAheadOfPoint ? 0f : 0.0001f;
    }

    private static bool TryNormalizeGroundName(
        string name,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(name) ||
            !name.StartsWith("Ground ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int index = "Ground ".Length;
        int start = index;
        while (index < name.Length && char.IsDigit(name[index]))
            index++;
        if (index == start)
            return false;

        normalized = $"Ground {name.Substring(start, index - start)}";
        return true;
    }

    private static Dictionary<string, GroundTemplate> BuildTemplates()
    {
        GroundTemplate[] templates =
        {
            Template("Ground 1",
                S(-12, -10, -2), S(10, 12, -2),
                S(-8, -6, 2), S(-1, 1, 2), S(6, 8, 2)),
            Template("Ground 2",
                S(-12, -11, -2), S(9, 12, -2),
                S(-6, -1, 0), S(4, 7, 0)),
            Template("Ground 3",
                S(-12, -10, -2), S(10, 12, -2),
                S(-6, -2, 4), S(1, 6, 4)),
            Template("Ground 4",
                S(-12, -7, -2), S(-5, -3, -2),
                S(-1, 1, -2), S(3, 5, -2), S(7, 12, -2)),
            Template("Ground 5",
                S(-12, -7, -2), S(7, 12, -2),
                S(-5, -3, 0), S(-1, 1, 4), S(3, 5, 7)),
            Template("Ground 6",
                S(-12, -9, -2), S(-7, 3, -2), S(10, 12, -2),
                S(-9, -7, 0), S(0, 3, 4), S(-10, 0, 6),
                S(5, 10, 6), S(4, 10, 12)),
            Template("Ground 7",
                S(-12, -1, -2), S(11, 12, -2), S(-1, 1, 0)),
            Template("Ground 8",
                S(-12, -6, -2), S(9, 12, -2), S(-6, 9, 2))
        };

        Dictionary<string, GroundTemplate> result =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (GroundTemplate template in templates)
            result.Add(template.Name, template);
        return result;
    }

    private static GroundTemplate Template(
        string name,
        params LocalSurface[] surfaces) =>
        new(name, surfaces);

    private static LocalSurface S(float left, float right, float top) =>
        new(left, right, top);

    private readonly record struct LocalSurface(
        float Left,
        float Right,
        float Top);

    private sealed class GroundTemplate
    {
        internal GroundTemplate(string name, LocalSurface[] surfaces)
        {
            Name = name;
            Surfaces = surfaces;
            LocalLeft = surfaces.Min(surface => surface.Left);
            LocalRight = surfaces.Max(surface => surface.Right);
            LocalBottom = -10f;
            LocalTop = surfaces.Max(surface => surface.Top);
        }

        internal string Name { get; }
        internal LocalSurface[] Surfaces { get; }
        internal float LocalLeft { get; }
        internal float LocalRight { get; }
        internal float LocalBottom { get; }
        internal float LocalTop { get; }
    }

    private sealed class LivePiece
    {
        internal LivePiece(Transform root, GroundTemplate template)
        {
            Root = root;
            Template = template;
        }

        internal Transform Root { get; }
        internal GroundTemplate Template { get; }
        internal int InstanceId => Root.gameObject.GetInstanceID();
    }
}
