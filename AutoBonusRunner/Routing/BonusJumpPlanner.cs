using AutoBonusRunner.Physics;
using System.Text;
using UnityEngine;

namespace AutoBonusRunner.Routing;

internal enum BonusManeuverKind
{
    None,
    GroundJumpToLanding,
    CoastToLowerLanding,
    EnterTrenchThenWallJump,
    ApproachJumpThenWallJump,
    WallJumpClimb,
    SphereCollectionJump,
    HazardClearanceJump
}

internal readonly record struct BonusJumpPlan(
    bool IsValid,
    bool ShouldJumpNow,
    float HoldSeconds,
    float PredictedFlightSeconds,
    float HorizontalTravel,
    float PlannedLaunchX,
    float PredictedLandingX,
    float LaunchWindowLeft,
    float LaunchWindowRight,
    string Reason,
    string CandidateSummary,
    BonusManeuverKind Maneuver = BonusManeuverKind.None,
    int ExpectedSphereHits = 0);

internal sealed class BonusJumpPlanner
{
    private const float MinimumHoldSeconds = 0.02f;
    private const float MaximumHoldSeconds = 0.18f;
    private const float UpwardClearance = 0.45f;
    private const float TriggerTolerance = 0.16f;
    // The scanner's safe range already includes a 0.15-unit extra safety
    // inset beyond the player's physical half width. On a just-landed narrow
    // platform one frame can move the player slightly beyond that preferred
    // range while the collider still fits on both source and target.
    private const float LandingRecoveryTolerance = 0.14f;
    private const float PreferredEdgeInset = 0.30f;
    // Two independent completed wall exits (A30 and A42) landed 0.135-0.138
    // world units farther than the FlightTimeScale-calibrated integral. This
    // is the small body-to-wall release offset that is absent when contactX is
    // used as the horizontal origin.
    private const float WallExitObservedTravelBias = 0.14f;
    private const float WallFaceTopClearance = 0.10f;
    private const float WallFaceMaximumContactVelocityY = 2.0f;
    // The mandatory face pulse is actively released on the first render-frame
    // horizontal-resume observation. V0.27 measured controller/render error at
    // roughly +0.001..+0.007s, so use an 8ms high-side envelope here. The full
    // extra FixedUpdate bucket remains in requested-hold/setup selection.
    // JumpController cannot release before its requested deadline, but the
    // render Update that sends UP can arrive one FixedUpdate later. Wall-face
    // plans therefore evaluate the requested fixed-step bucket through one
    // additional scheduling bucket instead of treating a duration as a
    // continuous height.
    // The first stage must meet the wall below its top edge. Contact timing
    // and hold are solved together from the live speed and wall height.
    private const float WallApproachEarlyContactSeconds = 0.52f;
    private const float WallApproachPreferredContactSeconds = 0.44f;
    // A four-unit trench cannot be crossed by passive falling: V0.24
    // repeatedly reached the pit guard roughly 0.77 units before the wall.
    // V0.25 then aimed below the physical bottom of the raised wall and flew
    // underneath it. The viable low-face band is deeper than V0.23's upper
    // contact, but still overlaps the approximately four-unit-tall wall body.
    private const float TrenchEntryMaximumContactSeconds = 0.72f;
    private const float TrenchEntryMinimumFaceClearance = 3.90f;
    private const float TrenchEntryMaximumFaceClearance = 4.50f;
    private const float TrenchEntryPreferredFaceClearance = 4.20f;
    private const float MaximumPassiveWallDrop = 1.25f;
    // Short holds are necessary for +2/+3 faces and Spirit-Boost speed;
    // taller faces can use the demonstrated 0.105-0.120s approach range.
    private static readonly float[] WallApproachHoldCandidates =
    {
        0.02f, 0.04f, 0.06f, 0.075f, 0.09f, 0.105f, 0.12f
    };

    private static readonly float[] HoldCandidates =
    {
        // The live game reports a 0.18s native jump-time cap. Longer presses
        // do not add height; they only keep the controller unavailable and
        // previously produced misleading 0.35-0.65s "large jump" plans.
        0.02f, 0.03f, 0.04f, 0.05f, 0.06f,
        0.075f, 0.09f, 0.105f, 0.12f, 0.135f,
        0.15f, 0.165f, 0.18f
    };

    internal BonusJumpPlan Plan(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard = default,
        IReadOnlyList<Vector2> sphereObjectives = null)
    {
        scan = SelectLowerRouteWhenItContinues(scan);
        if (!scan.IsValid)
            return Invalid(scan.Reason, scan.Reason);

        float speed = Mathf.Abs(playerVelocity.x);
        // This route contract owns the source before optional sphere jumps are
        // considered.  A same-surface pickup must not reintroduce the exact
        // airborne underpass collision that the mapped route forbids.
        if (speed >= 1f && IsMappedGround6UnderpassWallRoute(scan))
        {
            BonusJumpPlan underpass = PlanWallDropApproach(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                "MappedRoute=Ground6:S1->S6:UnderpassContact");
            return underpass.IsValid
                ? underpass with
                {
                    Reason = "MappedGround6UnderpassWallDrop",
                    CandidateSummary = underpass.CandidateSummary +
                        " | RouteContract[No airborne input is legal before " +
                        "the S6 face because the overlapping S4/S5 ceiling " +
                        "physically intercepts it.]"
                }
                : Invalid(
                    "MappedGround6UnderpassWallDropUnavailable",
                    underpass.CandidateSummary);
        }

        if (TryPlanSameSurfaceSphereCollection(
                scan,
                playerPosition,
                playerVelocity,
                physics,
                hazard,
                sphereObjectives,
                out BonusJumpPlan sphereCollection))
        {
            return sphereCollection;
        }

        if (!scan.HasNext)
            return Invalid("NextBoardUnavailable", scan.Reason);

        if (speed < 1f)
            return Invalid("HorizontalSpeedTooLow", $"Speed={speed:F2}");

        BonusObstacleAssessment obstacle =
            BonusObstacleClassifier.Classify(scan);

        bool deepTrenchEntry =
            obstacle.Kind == BonusObstacleKind.LowerLanding &&
            scan.HeightDelta <= -5.50f;
        if (obstacle.Kind == BonusObstacleKind.LowerLanding &&
            TryPlanNaturalDrop(
                scan,
                playerPosition,
                speed,
                physics,
                out BonusJumpPlan naturalDrop))
        {
            return naturalDrop;
        }

        // Adjacent and narrow walls have a short enough unsupported span to
        // reach the face before the pit guard, so they remain true passive
        // entries. Wider authored trenches need a separately solved low
        // descending entry hop (handled below).
        if (obstacle.Kind == BonusObstacleKind.AdjacentWall ||
            obstacle.Kind == BonusObstacleKind.NarrowPillarTrench)
        {
            BonusJumpPlan wallDrop = PlanWallDropApproach(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                $"Obstacle={obstacle.Kind}[{obstacle.Evidence}]");
            if (wallDrop.IsValid)
                return wallDrop;
            return Invalid(
                $"{obstacle.Kind}HasNoExecutableRoute",
                wallDrop.CandidateSummary);
        }

        // These topologies encode a wall-contact route. A direct ballistic
        // landing on the visible top may be mathematically possible at Spirit
        // Boost speed, but it skips the authored trench and the wall spheres.
        // The previous fallback ordering did exactly that: it tried every
        // ground landing first and used the wall route only after they failed.
        if (obstacle.Kind == BonusObstacleKind.WideWallTrench)
        {
            BonusJumpPlan trenchEntry = PlanWallApproach(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                $"Obstacle={obstacle.Kind}[{obstacle.Evidence}]",
                preferLowerTrenchContact: true,
                requireBelowSourceContact: true);
            return trenchEntry.IsValid
                ? trenchEntry
                : Invalid(
                    "WideWallTrenchHasNoSafeEntryTrajectory",
                    trenchEntry.CandidateSummary);
        }

        if (obstacle.Kind == BonusObstacleKind.WallAcrossGap)
        {
            // A tall WallAcrossGap is not an upper-lip landing problem. The
            // playable route is to enter the lower face and use separated
            // wall presses. Restricting contact to 0.12-1.65 below an
            // eight-unit lip rejected every real trajectory and returned
            // Wait. Use the lower-face solver for all tall authored walls.
            bool requiresLowerFaceContact = scan.HeightDelta >= 4.00f;
            BonusJumpPlan wallApproach = PlanWallApproach(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                $"Obstacle={obstacle.Kind}[{obstacle.Evidence}]",
                requiresLowerFaceContact,
                requireBelowSourceContact: false);
            return wallApproach.IsValid
                ? wallApproach
                : Invalid(
                    $"{obstacle.Kind}HasNoExecutableWallRoute",
                    wallApproach.CandidateSummary);
        }

        bool transitionRequired =
            scan.Gap > 0.10f ||
            Mathf.Abs(scan.HeightDelta) > 0.35f;
        if (!transitionRequired)
            return Invalid("ContinuousSurface", "Gap/height transition below threshold");

        float targetSafeWidth = Mathf.Max(
            0f,
            scan.Next.SafeRight - scan.Next.SafeLeft);
        float preferredLandingX = scan.Next.SafeLeft +
            Mathf.Min(1.0f, targetSafeWidth * 0.50f);
        float preferredLaunchX = Mathf.Clamp(
            scan.Current.SafeRight - PreferredEdgeInset,
            scan.Current.SafeLeft,
            scan.Current.SafeRight);
        float preferredHold = Mathf.Clamp(
            0.06f + scan.Gap * 0.035f +
            Mathf.Max(0f, scan.HeightDelta) * 0.03f,
            MinimumHoldSeconds,
            MaximumHoldSeconds);
        BonusJumpPlan best = default;
        float bestScore = float.NegativeInfinity;
        StringBuilder evaluations = new();
        evaluations.Append(
            $"Obstacle={obstacle.Kind}[{obstacle.Evidence}] | ");
        if (deepTrenchEntry)
        {
            evaluations.Append(
                "StaticDeepTrenchEntry[Natural drop did not fit the " +
                "observed landing corridor; evaluate a verified short " +
                "jump before the next wall action.] | ");
        }
        if (scan.HasIntermediate)
        {
            evaluations.Append(
                $"Lookahead[{scan.Reason},Via=" +
                $"[{scan.Intermediate.Left:F2},{scan.Intermediate.Right:F2}]" +
                $"@{scan.Intermediate.Top:F2},Target=" +
                $"[{scan.Next.Left:F2},{scan.Next.Right:F2}]@{scan.Next.Top:F2}] | ");
        }

        foreach (float hold in HoldCandidates)
        {
            // Height feasibility is decided by the live ballistic model
            // below. Do not impose a fixed hold tier for upward platforms:
            // during Spirit Boost a 0.12s jump can be the only trajectory
            // that both clears a +4 platform and does not overshoot it.
            if (!TryPredictFlightTime(
                    hold,
                    scan.HeightDelta,
                    physics,
                    out float flightSeconds,
                    out float effectiveHold,
                    out float maximumRise))
            {
                AppendEvaluation(evaluations,
                    $"H={hold:F2}:EH={effectiveHold:F2},MaxRise={maximumRise:F2},UnreachableHeight");
                continue;
            }

            flightSeconds *= physics.FlightTimeScale;

            flightSeconds = CalibrateBaseSpeedFlightDuration(
                speed,
                hold,
                scan.HeightDelta,
                flightSeconds,
                physics,
                out string flightSource);

            float travel = PredictHorizontalTravel(
                speed, flightSeconds, hold, scan.HeightDelta, physics,
                out string travelSource);
            travel = ApplyLandingBias(
                travel,
                scan.HeightDelta,
                hold,
                physics,
                ref travelSource);
            travelSource = $"{flightSource};{travelSource}";
            float launchLeft = scan.Next.SafeLeft - travel;
            float launchRight = scan.Next.SafeRight - travel;
            float usableLeft = Mathf.Max(launchLeft, scan.Current.SafeLeft);
            float usableRight = Mathf.Min(launchRight, scan.Current.SafeRight);

            if (usableRight - usableLeft < 0.02f)
            {
                AppendEvaluation(evaluations,
                    $"H={hold:F2}:EH={effectiveHold:F2},T={flightSeconds:F3},D={travel:F2},NoIntersection");
                continue;
            }

            float plannedLaunchX = Mathf.Clamp(
                preferredLandingX - travel,
                usableLeft,
                usableRight);
            float plannedLandingX = plannedLaunchX + travel;

            if (!TrajectoryClearsHazard(
                    hazard,
                    plannedLaunchX,
                    plannedLandingX,
                    scan.Current.Top,
                    hold,
                    flightSeconds,
                    physics,
                    out string hazardCheck))
            {
                AppendEvaluation(evaluations,
                    $"H={hold:F2}:RejectedByTrajectory,{hazardCheck}");
                continue;
            }
            bool beyondIdealWindow =
                playerPosition.x > usableRight + TriggerTolerance;
            // A late jump can be physically valid after the conservative
            // safe-to-safe launch intersection has ended.  The retained
            // trace at X=539.922 saw the real [544,546] target but rejected
            // every hold because the preferred window ended near X=539.62;
            // doing nothing was guaranteed to miss the platform.  Use the
            // raw collider bounds as a last-resort landing corridor while
            // the player is still on the verified source.  This is not a
            // blind pit jump: target bounds and the full hazard trajectory
            // are still required.
            bool stillOnVerifiedSource =
                playerPosition.x >= scan.Current.Left - 0.12f &&
                playerPosition.x <= scan.Current.Right + 0.12f;
            bool lateLandingInsideRawTarget =
                playerPosition.x + travel >= scan.Next.Left &&
                playerPosition.x + travel <= scan.Next.Right;
            bool lateTrajectorySafe = stillOnVerifiedSource &&
                lateLandingInsideRawTarget &&
                TrajectoryClearsHazard(
                    hazard,
                    playerPosition.x,
                    playerPosition.x + travel,
                    scan.Current.Top,
                    hold,
                    flightSeconds,
                    physics,
                    out _);
            bool emergencyLateRawLanding =
                beyondIdealWindow && lateTrajectorySafe;
            bool missed = beyondIdealWindow && !emergencyLateRawLanding;
            int predictedSphereHits = CountTrajectorySphereHits(
                sphereObjectives,
                plannedLaunchX,
                plannedLandingX,
                scan.Current.Top,
                speed,
                hold,
                flightSeconds,
                physics);
            string status = emergencyLateRawLanding
                ? "EmergencyLateRawLanding"
                : missed
                ? "Missed"
                : playerPosition.x >= plannedLaunchX - TriggerTolerance
                    ? "Jump"
                    : "Wait";

            AppendEvaluation(evaluations,
                $"H={hold:F2}:EH={effectiveHold:F2},T={flightSeconds:F3},D={travel:F2}," +
                $"TravelSource={travelSource}," +
                $"W=[{usableLeft:F2},{usableRight:F2}],L={plannedLaunchX:F2}," +
                $"P={plannedLandingX:F2},SphereHits={predictedSphereHits}," +
                $"{status},{hazardCheck}");

            if (missed)
                continue;

            // Prefer the hold that would land safely when launching close to
            // the current board's right edge. This makes larger gaps select a
            // genuinely longer hold instead of always choosing the first tier.
            float edgeLandingX = preferredLaunchX + travel;
            float edgeLandingError = Mathf.Abs(
                edgeLandingX - preferredLandingX);
            float centerError = Mathf.Abs(
                plannedLandingX - preferredLandingX);
            float windowWidth = usableRight - usableLeft;
            float holdError = Mathf.Abs(hold - preferredHold);
            bool hasTierCalibration = physics.TravelProfile.TryGetDuration(
                hold,
                scan.HeightDelta,
                out _,
                out int tierSampleCount);
            // A previously unseen long level jump is much less predictable
            // than a calibrated medium hold. Keep it available when it is the
            // only geometric solution, but do not prefer it merely because
            // its analytical edge landing is a few tenths closer to center.
            float uncertaintyPenalty =
                Mathf.Abs(scan.HeightDelta) <= 0.35f && hold > 0.14f
                    ? !hasTierCalibration
                        ? 0.75f
                        : tierSampleCount == 1
                            ? 0.25f
                            : 0f
                    : 0f;
            float score =
                windowWidth * 0.35f -
                edgeLandingError * 2.5f -
                centerError * 1.5f -
                holdError * 2.0f -
                uncertaintyPenalty;

            bool predictedInsideSafeTarget =
                playerPosition.x + travel >=
                    scan.Next.SafeLeft - LandingRecoveryTolerance &&
                playerPosition.x + travel <=
                    scan.Next.SafeRight + LandingRecoveryTolerance;
            bool lateLandingRecovery =
                playerPosition.x > usableRight &&
                playerPosition.x <= usableRight + TriggerTolerance;
            bool actualTrajectorySafe = TrajectoryClearsHazard(
                hazard,
                playerPosition.x,
                playerPosition.x + travel,
                scan.Current.Top,
                hold,
                flightSeconds,
                physics,
                out _);
            bool shouldJump =
                emergencyLateRawLanding ||
                (playerPosition.x >= plannedLaunchX - TriggerTolerance &&
                 playerPosition.x <= usableRight + TriggerTolerance &&
                 predictedInsideSafeTarget &&
                 actualTrajectorySafe);
            float predictedLandingX = shouldJump
                ? playerPosition.x + travel
                : plannedLandingX;
            BonusJumpPlan candidate = new(
                true,
                shouldJump,
                hold,
                flightSeconds,
                travel,
                plannedLaunchX,
                predictedLandingX,
                usableLeft,
                usableRight,
                shouldJump
                    ? emergencyLateRawLanding
                        ? "EmergencyLateRawLanding"
                        : lateLandingRecovery
                        ? "LateLandingRecovery"
                        : "InsideLaunchWindow"
                    : "ApproachingLaunchWindow",
                string.Empty,
                BonusManeuverKind.GroundJumpToLanding,
                predictedSphereHits);

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        string evaluationSummary = evaluations.ToString();
        if (best.IsValid)
            return best with { CandidateSummary = evaluationSummary };

        return Invalid(
            obstacle.Kind == BonusObstacleKind.RaisedLanding
                ? "RaisedLandingHasNoSafeDirectJump"
                : "NoVerifiedLaunchWindow",
            evaluationSummary);
    }

    internal static BonusBoardScanResult SelectLowerRouteWhenItContinues(
        BonusBoardScanResult scan)
    {
        if (!scan.IsValid ||
            !scan.HasNext ||
            scan.Alternatives == null ||
            scan.Alternatives.Length == 0 ||
            scan.Reason.StartsWith(
                "StaticLowerContinuation:",
                System.StringComparison.Ordinal))
        {
            return scan;
        }

        float scannedNextGap = Mathf.Max(
            0f,
            scan.Next.Left - scan.Current.Right);
        BonusBoardSegment lowerRoute = scan.Alternatives
            .Where(candidate =>
                candidate.RegistryGeneration > 0 &&
                candidate.StaticSurfaceIndex >= 0 &&
                candidate.Top <= scan.Current.Top + 0.35f &&
                candidate.Top >= scan.Current.Top - 15.25f &&
                candidate.Right >= scan.Current.Right + 0.25f &&
                candidate.Width >= 1.0f &&
                Mathf.Max(0f, candidate.Left - scan.Current.Right) <= 3.25f &&
                (candidate.Left <= scan.Current.Right + 0.20f ||
                 Mathf.Max(0f, candidate.Left - scan.Current.Right) + 0.75f <
                    scannedNextGap))
            // Prefer the continuous lower road encoded by the static map,
            // even when a taller tile overlaps the same forward probe. The
            // old height-first rule selected the upper top and invented a
            // large jump where the authored route simply walks down.
            .OrderBy(candidate =>
                Mathf.Max(0f, candidate.Left - scan.Current.Right))
            .ThenBy(candidate =>
                Mathf.Abs(candidate.Top - scan.Current.Top))
            .ThenByDescending(candidate => candidate.Right)
            .FirstOrDefault();
        if (lowerRoute.Width < 0.20f)
            return scan;

        return scan with
        {
            Next = lowerRoute,
            Gap = Mathf.Max(0f, lowerRoute.Left - scan.Current.Right),
            HeightDelta = lowerRoute.Top - scan.Current.Top,
            Reason = $"StaticLowerContinuation:{lowerRoute.MapPieceName}",
            HasIntermediate = true,
            Intermediate = scan.Next
        };
    }

    internal BonusBoardScanResult SelectBoostReachableRoute(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        out string selection)
    {
        selection = "ImmediateRouteRetained";
        if (!scan.IsValid || !scan.HasNext ||
            scan.Alternatives == null || scan.Alternatives.Length == 0)
        {
            return scan;
        }

        BonusObstacleAssessment immediateObstacle =
            BonusObstacleClassifier.Classify(scan);
        if (immediateObstacle.Kind == BonusObstacleKind.AdjacentWall ||
            immediateObstacle.Kind == BonusObstacleKind.NarrowPillarTrench ||
            immediateObstacle.Kind == BonusObstacleKind.WideWallTrench ||
            immediateObstacle.Kind == BonusObstacleKind.WallAcrossGap ||
            immediateObstacle.Kind == BonusObstacleKind.LowerLanding ||
            immediateObstacle.Kind == BonusObstacleKind.ContinuousRoad)
        {
            selection = $"TopologyOwned:{immediateObstacle.Kind}";
            return scan;
        }

        List<BonusBoardSegment> targets = new() { scan.Next };
        targets.AddRange(scan.Alternatives.Where(candidate =>
            candidate.Right > scan.Current.Right + 0.10f &&
            candidate.SafeRight > candidate.SafeLeft + 0.10f));

        BonusBoardScanResult bestScan = scan;
        float bestScore = float.NegativeInfinity;
        BonusJumpPlan bestPlan = default;
        foreach (BonusBoardSegment target in targets
                     .GroupBy(candidate =>
                         $"{candidate.Left:F2}:{candidate.Right:F2}:{candidate.Top:F2}")
                     .Select(group => group.First())
                     .OrderBy(candidate => candidate.Left))
        {
            BonusBoardScanResult candidateScan = scan with
            {
                HasNext = true,
                Next = target,
                Gap = Mathf.Max(0f, target.Left - scan.Current.Right),
                HeightDelta = target.Top - scan.Current.Top,
                HasIntermediate = !SameSurfaceIdentity(target, scan.Next),
                Intermediate = scan.Next,
                Alternatives = Array.Empty<BonusBoardSegment>(),
                Reason = SameSurfaceIdentity(target, scan.Next)
                    ? scan.Reason
                    : $"BoostReachableAlternative:{target.MapPieceName}"
            };
            BonusObstacleAssessment candidateObstacle =
                BonusObstacleClassifier.Classify(candidateScan);
            bool candidateRequiresAuthoredIntermediateAction =
                candidateObstacle.Kind == BonusObstacleKind.AdjacentWall ||
                candidateObstacle.Kind == BonusObstacleKind.NarrowPillarTrench ||
                candidateObstacle.Kind == BonusObstacleKind.WideWallTrench ||
                candidateObstacle.Kind == BonusObstacleKind.WallAcrossGap ||
                candidateObstacle.Kind == BonusObstacleKind.LowerLanding ||
                candidateObstacle.Kind == BonusObstacleKind.ContinuousRoad;
            if (!SameSurfaceIdentity(target, scan.Next) &&
                candidateRequiresAuthoredIntermediateAction)
            {
                // A high-speed alternative may skip a short ordinary landing,
                // but it must never reinterpret a farther wall or lower road as
                // a direct ballistic target. Those surfaces own a multi-action
                // route which must be discovered when they become immediate.
                continue;
            }
            BonusJumpPlan candidatePlan = Plan(
                candidateScan,
                playerPosition,
                playerVelocity,
                physics,
                hazard,
                Array.Empty<Vector2>());
            if (!candidatePlan.IsValid)
                continue;

            float safeWidth = target.SafeRight - target.SafeLeft;
            float windowWidth = Mathf.Max(
                0f,
                candidatePlan.LaunchWindowRight -
                candidatePlan.LaunchWindowLeft);
            float forwardDistance = Mathf.Max(
                0f,
                target.Left - scan.Current.Right);
            float score =
                safeWidth * 1.50f +
                windowWidth * 0.65f -
                forwardDistance * 0.035f +
                (candidatePlan.ShouldJumpNow ? 0.20f : 0f);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestScan = candidateScan;
            bestPlan = candidatePlan;
        }

        if (!SameSurfaceIdentity(bestScan.Next, scan.Next))
        {
            selection =
                $"BoostAlternativeSelected Immediate=" +
                $"[{scan.Next.Left:F2},{scan.Next.Right:F2}]@{scan.Next.Top:F2}," +
                $"Selected=[{bestScan.Next.Left:F2},{bestScan.Next.Right:F2}]" +
                $"@{bestScan.Next.Top:F2},Score={bestScore:F3}," +
                $"Hold={bestPlan.HoldSeconds:F3}," +
                $"Window=[{bestPlan.LaunchWindowLeft:F2}," +
                $"{bestPlan.LaunchWindowRight:F2}]";
        }
        return bestScan;
    }

    private static bool SameSurfaceIdentity(
        BonusBoardSegment left,
        BonusBoardSegment right)
    {
        if (left.RegistryGeneration > 0 && right.RegistryGeneration > 0)
        {
            return left.MapPieceInstanceId == right.MapPieceInstanceId &&
                left.StaticSurfaceIndex == right.StaticSurfaceIndex &&
                Mathf.Abs(left.Left - right.Left) <= 0.20f &&
                Mathf.Abs(left.Right - right.Right) <= 0.20f;
        }
        return Mathf.Abs(left.Left - right.Left) <= 0.20f &&
            Mathf.Abs(left.Right - right.Right) <= 0.20f &&
               Mathf.Abs(left.Top - right.Top) <= 0.25f;
    }

    private static bool IsMappedGround6UnderpassWallRoute(
        BonusBoardScanResult scan)
    {
        if (!scan.IsValid || !scan.HasNext)
            return false;

        BonusBoardSegment source = scan.Current;
        BonusBoardSegment target = scan.Next;
        return source.RegistryGeneration > 0 &&
            target.RegistryGeneration == source.RegistryGeneration &&
            source.MapPieceInstanceId != 0 &&
            target.MapPieceInstanceId == source.MapPieceInstanceId &&
            string.Equals(
                source.MapPieceName,
                "Ground 6",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                target.MapPieceName,
                "Ground 6",
                StringComparison.OrdinalIgnoreCase) &&
            source.StaticSurfaceIndex == 1 &&
            target.StaticSurfaceIndex == 6 &&
            scan.Gap >= 1.25f && scan.Gap <= 2.75f &&
            scan.HeightDelta >= 7.25f && scan.HeightDelta <= 8.75f;
    }

    private static BonusJumpPlan PlanWallDropApproach(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float speed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        string priorEvaluation)
    {
        float inferredHalfWidth = Mathf.Max(
            0.15f,
            scan.Next.SafeLeft - scan.Next.Left - 0.15f);
        float wallContactX = scan.Next.Left - inferredHalfWidth;
        if (wallContactX <= playerPosition.x + 0.05f ||
            wallContactX <= scan.Current.SafeLeft + 0.10f)
        {
            return Invalid(
                "WallDropGeometryInvalid",
                priorEvaluation +
                $" | WallDropRejected[WallX={wallContactX:F2}," +
                $"PlayerX={playerPosition.x:F2}," +
                $"CurrentSafeLeft={scan.Current.SafeLeft:F2}]");
        }

        bool hazardBlocksLowerRoute =
            hazard.IsValid &&
            hazard.Left <= wallContactX + inferredHalfWidth &&
            hazard.Right >= scan.Current.Right - 0.10f;
        if (hazardBlocksLowerRoute)
        {
            return Invalid(
                "WallDropBlockedByHazard",
                priorEvaluation +
                $" | WallDropRejected[Hazard=[{hazard.Left:F2}," +
                $"{hazard.Right:F2}]@{hazard.Top:F2},WallX={wallContactX:F2}]");
        }

        float armX = Mathf.Clamp(
            Mathf.Min(scan.Current.SafeRight - 0.08f, wallContactX - 0.18f),
            scan.Current.SafeLeft,
            scan.Current.SafeRight);
        float travelToWall = Mathf.Max(0f, wallContactX - armX);
        float contactTime = SolveHorizontalTravelTime(
            speed,
            travelToWall,
            0.60f,
            physics);
        float unsupportedCenterX = scan.Current.Right + inferredHalfWidth;
        float unsupportedTravel = Mathf.Max(
            0f,
            wallContactX - unsupportedCenterX);
        float unsupportedTime = unsupportedTravel > 0.01f
            ? SolveHorizontalTravelTime(
                speed,
                unsupportedTravel,
                contactTime,
                physics)
            : 0f;
        float predictedDrop = 0.5f * Mathf.Clamp(
            physics.GravityMagnitude,
            5f,
            200f) * unsupportedTime * unsupportedTime;
        float predictedContactFeetY = scan.Current.Top - predictedDrop;
        if (predictedDrop > MaximumPassiveWallDrop)
        {
            return Invalid(
                "PassiveWallDropBelowSafeContactBand",
                priorEvaluation +
                $" | WallDropRejected[WallX={wallContactX:F2}," +
                $"UnsupportedT={unsupportedTime:F3},Drop={predictedDrop:F2}," +
                $"ContactFeetY={predictedContactFeetY:F2}," +
                $"MaximumDrop={MaximumPassiveWallDrop:F2}]. " +
                "The face is not reachable before the observed pit boundary; " +
                "a controlled trench-entry trajectory is required.");
        }
        bool armNow = playerPosition.x >= armX - TriggerTolerance;
        string summary = priorEvaluation +
            $" | WallDropApproach[ArmX={armX:F2},Now={playerPosition.x:F2}," +
            $"WallX={wallContactX:F2},Gap={scan.Gap:F2}," +
            $"Rise={scan.HeightDelta:F2},ContactT={contactTime:F3}," +
            $"UnsupportedT={unsupportedTime:F3},Drop={predictedDrop:F2}," +
            $"ContactFeetY={predictedContactFeetY:F2}," +
            $"State={(armNow ? "ArmNow" : "Approach")}]. " +
            "No jump is sent before the wall foot; the next input is a " +
            "contact-confirmed wall jump.";

        return new BonusJumpPlan(
            true,
            false,
            0f,
            contactTime,
            travelToWall,
            armX,
            wallContactX,
            armX - TriggerTolerance,
            armX + TriggerTolerance,
            "WallDropApproach",
            summary,
            BonusManeuverKind.EnterTrenchThenWallJump);
    }

    private static bool TryPlanNaturalDrop(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float speed,
        JumpPhysicsSnapshot physics,
        out BonusJumpPlan plan)
    {
        plan = default;
        if (scan.HeightDelta >= -0.35f)
            return false;

        float dropHeight = Mathf.Max(0f, -scan.HeightDelta);
        float gravity = Mathf.Clamp(
            physics.GravityMagnitude,
            5f,
            200f);
        float fallSeconds = Mathf.Sqrt(2f * dropHeight / gravity);
        float inferredHalfWidth = Mathf.Max(
            0.15f,
            scan.Current.Right - scan.Current.SafeRight - 0.15f);

        // Falling starts only after the character's centre has moved one half
        // width beyond the source edge. Measuring from Current.Right alone was
        // systematically predicting short landings on two-unit step-downs.
        float unsupportedCenterX =
            scan.Current.Right + inferredHalfWidth;
        float fallTravel = PredictHorizontalTravelAtTime(
            speed,
            fallSeconds,
            physics);
        float predictedLandingX = unsupportedCenterX + fallTravel;
        float stableLeft = scan.Next.Left + inferredHalfWidth;
        float stableRight = scan.Next.Right - inferredHalfWidth;
        const float bodyFitTolerance = 0.10f;
        bool bodyFits =
            stableRight >= stableLeft &&
            predictedLandingX >= stableLeft - bodyFitTolerance &&
            predictedLandingX <= stableRight + bodyFitTolerance;
        if (!bodyFits)
            return false;

        string summary =
            $"NaturalDrop SourceEdge={scan.Current.Right:F3}," +
            $"HalfWidth={inferredHalfWidth:F3}," +
            $"UnsupportedCenter={unsupportedCenterX:F3}," +
            $"Drop={dropHeight:F3},Fall={fallSeconds:F3}s," +
            $"FallTravel={fallTravel:F3},Landing={predictedLandingX:F3}," +
            $"StableTarget=[{stableLeft:F3},{stableRight:F3}]," +
            $"Gap={scan.Gap:F3},DeltaY={scan.HeightDelta:F3}";
        plan = new BonusJumpPlan(
            false,
            false,
            0f,
            fallSeconds,
            predictedLandingX - playerPosition.x,
            unsupportedCenterX,
            predictedLandingX,
            scan.Current.SafeLeft,
            scan.Current.SafeRight,
            "IntentionalDrop",
            summary,
            BonusManeuverKind.CoastToLowerLanding);
        return true;
    }

    private BonusJumpPlan PlanWallApproach(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float speed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        string directEvaluation,
        bool preferLowerTrenchContact = false,
        bool requireBelowSourceContact = false)
    {
        // SafeLeft is inset by player half-width plus the scanner's 0.15-unit
        // edge margin. Recover the half-width so the planned centre stops at
        // the wall face rather than pretending it can land on the top.
        float inferredHalfWidth = Mathf.Max(
            0.15f,
            scan.Next.SafeLeft - scan.Next.Left - 0.15f);
        float wallContactX = scan.Next.Left - inferredHalfWidth;
        // An adjacent tall step has its wall face exactly at Current.Right;
        // the player's centre naturally contacts it one half-width before
        // that edge. That is valid wall-climb geometry, not an overlap error.
        // Reject only when there is no room anywhere on the current support
        // to launch toward the face.
        if (wallContactX <= scan.Current.SafeLeft + 0.10f)
        {
            return Invalid(
                "WallApproachGeometryInvalid",
                directEvaluation +
                $" | WallApproachRejected[ContactX={wallContactX:F2}," +
                $"CurrentSafeLeft={scan.Current.SafeLeft:F2}," +
                $"CurrentRight={scan.Current.Right:F2}]");
        }

        float distanceFromLatestLaunch = Mathf.Max(
            0f,
            wallContactX - scan.Current.SafeRight);
        float distanceFromEarliestLaunch = Mathf.Max(
            distanceFromLatestLaunch,
            wallContactX - scan.Current.SafeLeft);
        float maximumContactSeconds = requireBelowSourceContact
            ? TrenchEntryMaximumContactSeconds
            : WallApproachEarlyContactSeconds;
        float preferredContactSeconds = requireBelowSourceContact
            ? 0.52f
            : WallApproachPreferredContactSeconds;
        float geometryContactStart = Mathf.Max(
            0.04f,
            SolveWallContactTravelTime(
                speed,
                distanceFromLatestLaunch));
        float geometryContactEnd = Mathf.Min(
            maximumContactSeconds,
            SolveWallContactTravelTime(
                speed,
                distanceFromEarliestLaunch));
        if (geometryContactStart > geometryContactEnd + 0.005f)
        {
            return Invalid(
                "NoWallApproachContactTime",
                directEvaluation +
                $" | WallApproachRejected[WallX={wallContactX:F2}," +
                $"ContactT=[{geometryContactStart:F3},{geometryContactEnd:F3}]," +
                $"Travel=[{distanceFromLatestLaunch:F2},{distanceFromEarliestLaunch:F2}]]");
        }

        // Jointly solve the first-press duration and wall-contact time. The
        // planned feet must still be on the vertical face. This is essential
        // for short +2/+3 pillars: the former fixed 0.120s approach could
        // already be above the lip before contact, turning the intended wall
        // route into an uncontrolled overshoot.
        float chosenApproachHold = 0f;
        float preferredContactTime = 0f;
        float predictedContactFeetY = float.NegativeInfinity;
        float contactChoiceScore = float.PositiveInfinity;
        const int contactSamples = 28;
        float minimumFaceClearance = requireBelowSourceContact
            ? TrenchEntryMinimumFaceClearance
            : preferLowerTrenchContact
                ? 2.25f
                : 0.12f;
        float maximumFaceClearance = requireBelowSourceContact
            ? Mathf.Min(
                TrenchEntryMaximumFaceClearance,
                scan.HeightDelta - 0.10f)
            : preferLowerTrenchContact
                ? Mathf.Min(4.75f, scan.HeightDelta - 0.55f)
                : 1.65f;
        float preferredFaceClearance = requireBelowSourceContact
            ? Mathf.Clamp(
                TrenchEntryPreferredFaceClearance,
                minimumFaceClearance,
                maximumFaceClearance)
            : preferLowerTrenchContact
                ? Mathf.Clamp(scan.HeightDelta * 0.58f, 3.10f, 3.85f)
                : 0.45f;
        foreach (float hold in WallApproachHoldCandidates)
        {
            if (hold > physics.EffectiveHoldCapSeconds + 0.001f)
                continue;

            for (int sample = 0; sample <= contactSamples; sample++)
            {
                float sampledContactTime = Mathf.Lerp(
                    geometryContactStart,
                    geometryContactEnd,
                    sample / (float)contactSamples);
                float contactFeetY = PredictVerticalYAtTime(
                    scan.Current.Top,
                    hold,
                    sampledContactTime,
                    physics);
                float faceClearance = scan.Next.Top - contactFeetY;
                if (faceClearance < minimumFaceClearance ||
                    faceClearance > maximumFaceClearance)
                {
                    continue;
                }

                float score =
                    Mathf.Abs(faceClearance - preferredFaceClearance) +
                    Mathf.Abs(
                        sampledContactTime - preferredContactSeconds) * 0.04f +
                    hold * 0.015f;
                if (score >= contactChoiceScore)
                    continue;

                contactChoiceScore = score;
                chosenApproachHold = hold;
                preferredContactTime = sampledContactTime;
                predictedContactFeetY = contactFeetY;
            }
        }

        if (chosenApproachHold <= 0f)
        {
            return Invalid(
                "NoWallFaceTrajectory",
                directEvaluation +
                $" | WallApproachRejected[WallX={wallContactX:F2}," +
                $"Rise={scan.HeightDelta:F2}," +
                $"ContactT=[{geometryContactStart:F3},{geometryContactEnd:F3}]," +
                $"RequiredClearance=[{minimumFaceClearance:F2},{maximumFaceClearance:F2}]]");
        }

        float acceptedContactStart = float.PositiveInfinity;
        float acceptedContactEnd = float.NegativeInfinity;
        for (int sample = 0; sample <= contactSamples * 2; sample++)
        {
            float sampledContactTime = Mathf.Lerp(
                geometryContactStart,
                geometryContactEnd,
                sample / (float)(contactSamples * 2));
            float contactFeetY = PredictVerticalYAtTime(
                scan.Current.Top,
                chosenApproachHold,
                sampledContactTime,
                physics);
            float faceClearance = scan.Next.Top - contactFeetY;
            if (faceClearance < minimumFaceClearance ||
                faceClearance > maximumFaceClearance)
            {
                continue;
            }

            acceptedContactStart = Mathf.Min(
                acceptedContactStart,
                sampledContactTime);
            acceptedContactEnd = Mathf.Max(
                acceptedContactEnd,
                sampledContactTime);
        }

        // Wall approaches are shorter than one second and the game keeps the
        // observed bonus-stage VX constant throughout them (11.9 in the V0.25
        // trace). The general landing integrator incorrectly decelerated that
        // value toward BaseVX=9.4, placing the planned wall contact about 0.8
        // units behind the real body. Use the live constant velocity for this
        // contact problem; ordinary landing prediction remains unchanged.
        float earlyTravel = PredictWallContactTravelAtTime(
            speed, acceptedContactEnd);
        float lateTravel = PredictWallContactTravelAtTime(
            speed, acceptedContactStart);
        float preferredTravel = PredictWallContactTravelAtTime(
            speed, preferredContactTime);
        float usableLeft = Mathf.Max(
            wallContactX - earlyTravel,
            scan.Current.SafeLeft);
        float usableRight = Mathf.Min(
            wallContactX - lateTravel,
            scan.Current.SafeRight);
        if (usableRight - usableLeft < 0.02f)
        {
            return Invalid(
                "NoWallApproachLaunchWindow",
                directEvaluation +
                $" | WallApproachRejected[WallX={wallContactX:F2}," +
                $"T=[{acceptedContactStart:F3},{acceptedContactEnd:F3}]," +
                $"D=[{lateTravel:F2},{earlyTravel:F2}]," +
                $"Window=[{usableLeft:F2},{usableRight:F2}]]");
        }

        float plannedLaunchX = Mathf.Clamp(
            wallContactX - preferredTravel,
            usableLeft,
            usableRight);
        float plannedDistance = wallContactX - plannedLaunchX;
        float contactTime = SolveWallContactTravelTime(
            speed,
            plannedDistance);
        predictedContactFeetY = PredictVerticalYAtTime(
            scan.Current.Top,
            chosenApproachHold,
            contactTime,
            physics);
        bool missed = playerPosition.x > usableRight;

        // The preferred launch window is only valid for the hold selected at
        // the nominal contact time. Once that window is behind the player, a
        // different hold can still meet the same physical face from the live
        // position. Re-solve that control input here instead of returning a
        // valid Wait forever while the wall (and pit guard) approaches.
        if (missed)
        {
            float liveDistanceToFace = wallContactX - playerPosition.x;
            float liveContactTimeFromNow = liveDistanceToFace > 0f
                ? SolveWallContactTravelTime(speed, liveDistanceToFace)
                : 0f;
            float salvageHold = 0f;
            float salvageContactFeetY = float.NegativeInfinity;
            float salvageScore = float.PositiveInfinity;
            string salvageHazardCheck = "NotEvaluated";
            StringBuilder salvageEvaluations = new();

            foreach (float hold in WallApproachHoldCandidates)
            {
                if (hold > physics.EffectiveHoldCapSeconds + 0.001f)
                {
                    AppendEvaluation(
                        salvageEvaluations,
                        $"H={hold:F3}:RejectedAboveCap=" +
                        $"{physics.EffectiveHoldCapSeconds:F3}");
                    continue;
                }

                if (liveDistanceToFace <= 0f)
                {
                    AppendEvaluation(
                        salvageEvaluations,
                        $"H={hold:F3}:RejectedFaceAlreadyPassed");
                    continue;
                }

                float candidateFeetY = PredictVerticalYAtTime(
                    scan.Current.Top,
                    hold,
                    liveContactTimeFromNow,
                    physics);
                float candidateClearance = scan.Next.Top - candidateFeetY;
                bool hitsFaceBand =
                    candidateClearance >= minimumFaceClearance &&
                    candidateClearance <= maximumFaceClearance;
                if (!hitsFaceBand)
                {
                    AppendEvaluation(
                        salvageEvaluations,
                        $"H={hold:F3}:FeetY={candidateFeetY:F2}," +
                        $"Clearance={candidateClearance:F2},FaceHit=False");
                    continue;
                }

                bool candidateSafe = TrajectoryClearsHazard(
                    hazard,
                    playerPosition.x,
                    wallContactX,
                    scan.Current.Top,
                    hold,
                    liveContactTimeFromNow,
                    physics,
                    out string candidateHazardCheck);
                AppendEvaluation(
                    salvageEvaluations,
                    $"H={hold:F3}:FeetY={candidateFeetY:F2}," +
                    $"Clearance={candidateClearance:F2},FaceHit=True," +
                    $"Safe={candidateSafe},{candidateHazardCheck}");
                if (!candidateSafe)
                    continue;

                float score =
                    Mathf.Abs(candidateClearance - preferredFaceClearance) +
                    Mathf.Abs(
                        liveContactTimeFromNow - preferredContactSeconds) * 0.04f +
                    hold * 0.015f;
                if (score >= salvageScore)
                    continue;

                salvageScore = score;
                salvageHold = hold;
                salvageContactFeetY = candidateFeetY;
                salvageHazardCheck = candidateHazardCheck;
            }

            string missedSummary = directEvaluation +
                $" | WallApproachMissed[TargetTop={scan.Next.Top:F2}," +
                $"Rise={scan.HeightDelta:F2},WallX={wallContactX:F2}," +
                $"ContactBandFeet=[{scan.Next.Top - maximumFaceClearance:F2}," +
                $"{scan.Next.Top - minimumFaceClearance:F2}]," +
                $"NominalH={chosenApproachHold:F3}," +
                $"NominalWindow=[{usableLeft:F2},{usableRight:F2}]," +
                $"NominalLaunch={plannedLaunchX:F2},Now={playerPosition.x:F2}," +
                $"LiveDistance={liveDistanceToFace:F2}," +
                $"LiveContactT={liveContactTimeFromNow:F3}]" +
                $" | LiveWallSalvage[{salvageEvaluations}]";

            if (salvageHold <= 0f)
            {
                return Invalid(
                    "MissedWallApproachLaunchWindow",
                    missedSummary +
                    " | LiveWallSalvageResult=NoSafeFaceTrajectory");
            }

            TryPredictFlightTime(
                salvageHold,
                0f,
                physics,
                out _,
                out float salvageEffectiveHold,
                out float salvageMaximumRise);
            string salvageProfile = requireBelowSourceContact
                ? "DeepTrenchEntry"
                : preferLowerTrenchContact
                    ? "LowerTrenchEntry"
                    : "UpperFaceContact";
            string salvageSummary = missedSummary +
                $" | LiveWallSalvageSelected[Profile={salvageProfile}," +
                $"H={salvageHold:F3},EH={salvageEffectiveHold:F3}," +
                $"MaxRise={salvageMaximumRise:F2}," +
                $"ContactFeetY={salvageContactFeetY:F2}," +
                $"Action=ImmediateJump,{salvageHazardCheck}]";
            string salvageReason = requireBelowSourceContact
                ? "LateDeepTrenchEntrySalvage"
                : preferLowerTrenchContact
                    ? "LateTrenchEntrySalvage"
                    : "LateWallApproachSalvage";

            return new BonusJumpPlan(
                true,
                true,
                salvageHold,
                liveContactTimeFromNow,
                liveDistanceToFace,
                playerPosition.x,
                wallContactX,
                playerPosition.x,
                playerPosition.x,
                salvageReason,
                salvageSummary,
                BonusManeuverKind.ApproachJumpThenWallJump);
        }

        bool shouldJump =
            playerPosition.x >= plannedLaunchX - TriggerTolerance &&
            playerPosition.x <= usableRight + TriggerTolerance;

        float liveDistance = Mathf.Max(0f, wallContactX - playerPosition.x);
        float liveContactTime = shouldJump
            ? SolveWallContactTravelTime(
                speed,
                liveDistance)
            : contactTime;
        float liveContactFeetY = PredictVerticalYAtTime(
            scan.Current.Top,
            chosenApproachHold,
            liveContactTime,
            physics);
        bool liveHitsWallFace =
            liveContactFeetY <= scan.Next.Top - minimumFaceClearance &&
            liveContactFeetY >= scan.Next.Top - maximumFaceClearance;
        shouldJump &= liveHitsWallFace;
        bool trajectorySafe = TrajectoryClearsHazard(
            hazard,
            shouldJump ? playerPosition.x : plannedLaunchX,
            wallContactX,
            scan.Current.Top,
            chosenApproachHold,
            liveContactTime,
            physics,
            out string hazardCheck);
        shouldJump &= trajectorySafe;

        TryPredictFlightTime(
            chosenApproachHold,
            0f,
            physics,
            out _,
            out float effectiveHold,
            out float maximumRise);

        string summary = directEvaluation +
            $" | WallApproach[TargetTop={scan.Next.Top:F2}," +
            $"Rise={scan.HeightDelta:F2},WallX={wallContactX:F2}," +
            $"Profile={(requireBelowSourceContact ? "DeepTrenchEntry" : preferLowerTrenchContact ? "LowerTrenchEntry" : "UpperFaceContact")}," +
            $"ContactBandFeet=[{scan.Next.Top - maximumFaceClearance:F2}," +
            $"{scan.Next.Top - minimumFaceClearance:F2}]," +
            $"HalfWidth={inferredHalfWidth:F2},H={chosenApproachHold:F3}," +
            $"EH={effectiveHold:F3},MaxRise={maximumRise:F2}," +
            $"ContactT={contactTime:F3},ContactWindow=" +
            $"[{acceptedContactStart:F3},{acceptedContactEnd:F3}]," +
            $"ContactFeetY={predictedContactFeetY:F2}," +
            $"LiveContactFeetY={liveContactFeetY:F2},FaceHit={liveHitsWallFace}," +
            $"Window=[{usableLeft:F2},{usableRight:F2}]," +
            $"Launch={plannedLaunchX:F2},Now={playerPosition.x:F2}," +
            $"Action={(shouldJump ? "Jump" : missed ? "Missed" : "Wait")}," +
            $"{hazardCheck}]";

        return new BonusJumpPlan(
            true,
            shouldJump,
            chosenApproachHold,
            shouldJump ? liveContactTime : contactTime,
            shouldJump ? liveDistance : plannedDistance,
            plannedLaunchX,
            wallContactX,
            usableLeft,
            usableRight,
            shouldJump
                ? requireBelowSourceContact
                    ? "DeepTrenchEntryContact"
                    : preferLowerTrenchContact
                    ? "TrenchEntryContact"
                    : "WallApproachContact"
                : requireBelowSourceContact
                    ? "ApproachingDeepTrenchEntry"
                    : preferLowerTrenchContact
                    ? "ApproachingTrenchEntry"
                    : "ApproachingWallContact",
            summary,
            BonusManeuverKind.ApproachJumpThenWallJump);
    }

    private bool TryPlanSameSurfaceSphereCollection(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        IReadOnlyList<Vector2> sphereObjectives,
        out BonusJumpPlan plan)
    {
        plan = default;
        if (sphereObjectives == null || sphereObjectives.Count == 0)
            return false;

        float speed = Mathf.Abs(playerVelocity.x);
        if (speed < 1f)
            return false;

        Vector2[] elevatedAhead = sphereObjectives
            .Where(sphere =>
                sphere.x >= playerPosition.x - TriggerTolerance &&
                sphere.x <= scan.Current.SafeRight + 0.40f &&
                sphere.y >= scan.Current.Top + 0.70f)
            .OrderBy(sphere => sphere.x)
            .ThenBy(sphere => sphere.y)
            .Take(24)
            .ToArray();
        if (elevatedAhead.Length == 0)
            return false;

        // A collection jump must leave enough runway to observe and execute
        // the actual terrain transition. Spirit Boost requires more margin
        // because one control frame covers substantially more distance.
        float postLandingRunway = scan.HasNext
            ? Mathf.Clamp(speed * 0.18f, 1.50f, 5.00f)
            : 0f;
        float landingRightLimit =
            scan.Current.SafeRight - postLandingRunway;
        if (landingRightLimit <= scan.Current.SafeLeft + 0.10f)
            return false;

        BonusJumpPlan best = default;
        float bestScore = float.NegativeInfinity;
        StringBuilder evaluations = new();
        evaluations.Append(
            $"SameSurfaceSphereObjective[Count={elevatedAhead.Length}," +
            $"Current=[{scan.Current.SafeLeft:F2},{scan.Current.SafeRight:F2}]" +
            $"@{scan.Current.Top:F2}] | ");

        foreach (float hold in HoldCandidates)
        {
            if (!TryPredictFlightTime(
                    hold,
                    0f,
                    physics,
                    out float flightSeconds,
                    out float effectiveHold,
                    out _))
            {
                continue;
            }

            flightSeconds *= physics.FlightTimeScale;
            flightSeconds = CalibrateBaseSpeedFlightDuration(
                speed,
                hold,
                0f,
                flightSeconds,
                physics,
                out string flightSource);
            float travel = PredictHorizontalTravel(
                speed,
                flightSeconds,
                hold,
                0f,
                physics,
                out string travelSource);
            travel = ApplyLandingBias(
                travel,
                0f,
                hold,
                physics,
                ref travelSource);
            float usableLeft = Mathf.Max(
                scan.Current.SafeLeft,
                playerPosition.x - TriggerTolerance);
            float usableRight = landingRightLimit - travel;
            if (usableRight < usableLeft)
            {
                AppendEvaluation(
                    evaluations,
                    $"H={hold:F2}:D={travel:F2},NoSameSurfaceLanding");
                continue;
            }

            float range = usableRight - usableLeft;
            int samples = Mathf.Clamp(
                Mathf.CeilToInt(range / 0.12f),
                1,
                120);
            for (int sampleIndex = 0;
                 sampleIndex <= samples;
                 sampleIndex++)
            {
                float launchX = Mathf.Lerp(
                    usableLeft,
                    usableRight,
                    samples <= 0 ? 0f : sampleIndex / (float)samples);
                float landingX = launchX + travel;
                int hits = CountTrajectorySphereHits(
                    elevatedAhead,
                    launchX,
                    landingX,
                    scan.Current.Top,
                    speed,
                    hold,
                    flightSeconds,
                    physics);
                if (hits <= 0)
                    continue;

                if (!TrajectoryClearsHazard(
                        hazard,
                        launchX,
                        landingX,
                        scan.Current.Top,
                        hold,
                        flightSeconds,
                        physics,
                        out string hazardCheck))
                {
                    continue;
                }

                float distanceToLaunch = Mathf.Max(
                    0f,
                    launchX - playerPosition.x);
                float score =
                    hits * 10f -
                    distanceToLaunch * 0.20f -
                    hold * 0.50f -
                    Mathf.Abs(
                        landingX -
                        (scan.Current.SafeLeft + scan.Current.SafeRight) * 0.5f) *
                    0.02f;
                if (score <= bestScore)
                    continue;

                bool shouldJump =
                    playerPosition.x >= launchX - TriggerTolerance &&
                    playerPosition.x <= launchX + TriggerTolerance;
                bestScore = score;
                best = new BonusJumpPlan(
                    true,
                    shouldJump,
                    hold,
                    flightSeconds,
                    travel,
                    launchX,
                    shouldJump ? playerPosition.x + travel : landingX,
                    launchX - TriggerTolerance,
                    launchX + TriggerTolerance,
                    shouldJump
                        ? "SameSurfaceSphereCollection"
                        : "ApproachingSameSurfaceSphereCollection",
                    string.Empty,
                    BonusManeuverKind.SphereCollectionJump,
                    hits);
                evaluations.Append(
                    $"Best[H={hold:F2},EH={effectiveHold:F2}," +
                    $"L={launchX:F2},P={landingX:F2},D={travel:F2}," +
                    $"T={flightSeconds:F3},Hits={hits}," +
                    $"ReservedRunway={postLandingRunway:F2}," +
                    $"Flight={flightSource},Travel={travelSource}," +
                    $"{hazardCheck}] | ");
            }
        }

        if (!best.IsValid)
            return false;

        plan = best with { CandidateSummary = evaluations.ToString() };
        return true;
    }

    internal BonusJumpPlan PlanSameSurfaceHazard(
        BonusBoardScanResult scan,
        BonusHazard hazard,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        JumpPhysicsSnapshot physics)
    {
        if (!scan.IsValid || !hazard.IsValid ||
            hazard.Left < scan.Current.Left ||
            hazard.Right > scan.Current.Right ||
            hazard.Right <= playerPosition.x)
            return Invalid("HazardScanInvalid", "Hazard or support unavailable");

        float speed = Mathf.Abs(playerVelocity.x);
        if (speed < 1f)
            return Invalid("HorizontalSpeedTooLow", $"Hazard={hazard.ComponentPath}");

        BonusJumpPlan best = default;
        float bestScore = float.NegativeInfinity;
        StringBuilder evaluations = new();
        foreach (float hold in HoldCandidates)
        {
            if (!TryPredictFlightTime(hold, 0f, physics,
                    out float flightSeconds, out float effectiveHold,
                    out _))
                continue;

            flightSeconds *= physics.FlightTimeScale;

            flightSeconds = CalibrateBaseSpeedFlightDuration(
                speed,
                hold,
                0f,
                flightSeconds,
                physics,
                out string flightSource);

            float travel = PredictHorizontalTravel(
                speed, flightSeconds, hold, 0f, physics, out string source);
            travel = ApplyLandingBias(
                travel,
                0f,
                hold,
                physics,
                ref source);
            source = $"{flightSource};{source}";
            float targetLeft = Mathf.Max(scan.Current.SafeLeft, hazard.Right + 0.55f);
            float targetRight = scan.Current.SafeRight;
            if (targetRight <= targetLeft)
                continue;

            float desiredLanding = (targetLeft + targetRight) * 0.5f;
            float plannedLaunch = desiredLanding - travel;
            float latestLaunch = hazard.Left - 0.55f;
            if (plannedLaunch < scan.Current.SafeLeft || plannedLaunch > latestLaunch)
                continue;
            float plannedLanding = plannedLaunch + travel;
            if (!TrajectoryClearsHazard(hazard, plannedLaunch, plannedLanding,
                    scan.Current.Top, hold, flightSeconds, physics,
                    out string hazardCheck))
            {
                AppendEvaluation(evaluations,
                    $"H={hold:F2}:Rejected,{hazardCheck}");
                continue;
            }

            bool shouldJump =
                playerPosition.x >= plannedLaunch - TriggerTolerance &&
                playerPosition.x <= latestLaunch;
            float score = -Mathf.Abs(plannedLanding - desiredLanding) - hold * 0.10f;
            AppendEvaluation(evaluations,
                $"H={hold:F2}:EH={effectiveHold:F2},T={flightSeconds:F3}," +
                $"D={travel:F2},Source={source},L={plannedLaunch:F2}," +
                $"P={plannedLanding:F2},{hazardCheck}");
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = new BonusJumpPlan(
                true, shouldJump, hold, flightSeconds, travel,
                plannedLaunch,
                shouldJump ? playerPosition.x + travel : plannedLanding,
                plannedLaunch - TriggerTolerance, latestLaunch,
                shouldJump ? "SameSurfaceHazard" : "ApproachingSameSurfaceHazard",
                string.Empty);
        }

        return best.IsValid
            ? best with { CandidateSummary = evaluations.ToString() }
            : Invalid("NoVerifiedHazardTrajectory", evaluations.ToString());
    }

    internal float ChooseWallRecoveryHold(
        float remainingRise,
        JumpPhysicsSnapshot physics,
        out float predictedMaximumRise,
        float minimumHoldSeconds = MinimumHoldSeconds,
        float maximumHoldSeconds = MaximumHoldSeconds)
    {
        float maximumUsefulHold = Mathf.Clamp(
            Mathf.Min(physics.EffectiveHoldCapSeconds, maximumHoldSeconds),
            MinimumHoldSeconds,
            MaximumHoldSeconds);
        float minimumUsefulHold = Mathf.Clamp(
            minimumHoldSeconds,
            MinimumHoldSeconds,
            maximumUsefulHold);
        foreach (float hold in HoldCandidates)
        {
            if (hold + 0.001f < minimumUsefulHold)
                continue;
            if (hold > maximumUsefulHold + 0.001f)
                continue;
            TryPredictFlightTime(hold, 0f, physics,
                out _, out _, out float maximumRise);
            if (maximumRise >= remainingRise + 0.25f)
            {
                predictedMaximumRise = maximumRise;
                return hold;
            }
        }

        // Holding beyond the native cap adds no height and only delays the
        // next control decision. If one wall jump cannot finish the climb,
        // use the strongest useful press and let the runtime perform another
        // confirmed wall phase if necessary.
        TryPredictFlightTime(maximumUsefulHold, 0f, physics,
            out _, out _, out predictedMaximumRise);
        return maximumUsefulHold;
    }

    /// <summary>
    /// Chooses a released wall press by its downstream landing, not merely by
    /// whether it can rise above the wall lip. Horizontal motion is blocked
    /// while attached to the face and resumes only after the predicted lip
    /// crossing, so integrating the whole flight as horizontal travel would
    /// overestimate the transfer just as badly as the old fixed 1.34-unit
    /// placeholder underestimated it.
    /// </summary>
    internal bool TryChooseWallExitTransferHold(
        float contactX,
        float contactFeetY,
        float wallReleaseY,
        BonusBoardSegment downstreamTarget,
        float horizontalSpeed,
        JumpPhysicsSnapshot physics,
        float releaseTravelBias,
        float minimumHoldSeconds,
        float maximumHoldSeconds,
        out float selectedHold,
        out float predictedFlightSeconds,
        out float predictedHorizontalTravel,
        out float predictedLandingX,
        out string summary)
    {
        selectedHold = 0f;
        predictedFlightSeconds = 0f;
        predictedHorizontalTravel = 0f;
        predictedLandingX = contactX;
        StringBuilder evaluations = new();
        float bestScore = float.PositiveInfinity;
        float maximumUsefulHold = Mathf.Clamp(
            Mathf.Min(physics.EffectiveHoldCapSeconds, maximumHoldSeconds),
            MinimumHoldSeconds,
            MaximumHoldSeconds);
        float minimumUsefulHold = Mathf.Clamp(
            minimumHoldSeconds,
            MinimumHoldSeconds,
            maximumUsefulHold);
        float requiredLipRise = Mathf.Max(0f, wallReleaseY - contactFeetY);
        float targetHeightDelta = downstreamTarget.Top - contactFeetY;
        float targetCenter =
            (downstreamTarget.SafeLeft + downstreamTarget.SafeRight) * 0.5f;
        float boundedReleaseTravelBias = Mathf.Clamp(
            releaseTravelBias,
            WallExitObservedTravelBias,
            1.0f);

        foreach (float hold in HoldCandidates)
        {
            if (hold + 0.001f < minimumUsefulHold ||
                hold > maximumUsefulHold + 0.001f)
            {
                continue;
            }

            if (!TryPredictFlightTime(
                    hold,
                    targetHeightDelta,
                    physics,
                    out float rawFlightSeconds,
                    out float effectiveHold,
                    out float maximumRise) ||
                maximumRise < requiredLipRise + 0.05f)
            {
                evaluations.Append($"H={hold:F3}:VerticalReject | ");
                continue;
            }

            float ascentEnd =
                physics.InputDelaySeconds + effectiveHold +
                physics.JumpVelocity / Mathf.Max(5f, physics.GravityMagnitude);
            float low = 0f;
            float high = Mathf.Min(ascentEnd, rawFlightSeconds);
            for (int index = 0; index < 18; index++)
            {
                float midpoint = (low + high) * 0.5f;
                if (PredictVerticalDisplacementAtTime(
                        hold,
                        midpoint,
                        physics) < requiredLipRise)
                {
                    low = midpoint;
                }
                else
                {
                    high = midpoint;
                }
            }

            float lipCrossingSeconds = high;
            // Ground-jump samples have already measured that the analytical
            // vertical model overstates real input-to-landing time (about
            // 0.565s raw versus 0.493s observed on the repeated section-two
            // wall exits). Normal jump planning applies this calibration; wall
            // exit planning must do the same or it changes demonstrated 0.120s
            // transfers to unsafe 0.105s short jumps.
            float flightSeconds = Mathf.Max(
                lipCrossingSeconds + 0.02f,
                rawFlightSeconds * Mathf.Clamp(
                    physics.FlightTimeScale,
                    0.75f,
                    1.15f));
            float horizontalSeconds = Mathf.Max(
                0f,
                flightSeconds - lipCrossingSeconds);
            // Spirit Boost continues to decay while the body is vertically
            // attached and VX is reported as zero. Horizontal movement starts
            // at the lip, so first age the latched run speed through that
            // zero-travel interval; then integrate only the post-lip phase.
            float baseHorizontalSpeed = Mathf.Min(
                Mathf.Abs(horizontalSpeed),
                Mathf.Max(1f, physics.BaseHorizontalSpeed));
            float speedAtLip = Mathf.Max(
                baseHorizontalSpeed,
                Mathf.Abs(horizontalSpeed) -
                    Mathf.Max(0.10f, physics.BoostHorizontalDeceleration) *
                    lipCrossingSeconds);
            float travel = PredictHorizontalTravelAtTime(
                    speedAtLip,
                    horizontalSeconds,
                    physics) *
                Mathf.Clamp(physics.HorizontalTravelScale, 0.75f, 1.25f) +
                boundedReleaseTravelBias;
            float landingX = contactX + travel;
            float leftMargin = landingX - downstreamTarget.SafeLeft;
            float rightMargin = downstreamTarget.SafeRight - landingX;
            // SafeLeft/SafeRight already add a 0.15-unit margin beyond the
            // physical body width.  A 0.20 tolerance therefore still requires
            // a body-fit landing while covering render/fixed-step quantization
            // at the wall lip.  The demonstrated Ground-7 transfer lands only
            // 0.067 units outside the preferred inset and is physically stable.
            bool inside = leftMargin >= -0.20f && rightMargin >= -0.20f;
            evaluations.Append(
                $"H={hold:F3}:RawT={rawFlightSeconds:F3}," +
                $"Scale={physics.FlightTimeScale:F3},T={flightSeconds:F3}," +
                $"LipT={lipCrossingSeconds:F3}," +
                $"LipVX={speedAtLip:F3},MoveT={horizontalSeconds:F3}," +
                $"D={travel:F3},X={landingX:F3}," +
                $"ReleaseTravelBias={boundedReleaseTravelBias:F3}," +
                $"Margins=[{leftMargin:F3},{rightMargin:F3}]," +
                $"{(inside ? "Safe" : "Reject")} | ");
            if (!inside)
                continue;

            float edgePenalty = Mathf.Max(0f, 0.35f - Mathf.Min(
                leftMargin,
                rightMargin));
            float score = Mathf.Abs(landingX - targetCenter) +
                          edgePenalty * 3f +
                          hold * 0.05f;
            if (score >= bestScore)
                continue;

            bestScore = score;
            selectedHold = hold;
            predictedFlightSeconds = flightSeconds;
            predictedHorizontalTravel = travel;
            predictedLandingX = landingX;
        }

        summary = evaluations.ToString();
        return bestScore < float.PositiveInfinity;
    }

    /// <summary>
    /// Chooses a bounded setup pulse whose complete fixed-step/released apex
    /// remains below the current wall's release height.  This deliberately
    /// creates room to observe a real apex and solve a second pulse from live
    /// state; it is not a weaker approximation of a one-pulse wall exit.
    /// </summary>
    internal bool TryChooseWallFaceSetupHold(
        float contactFeetY,
        float wallReleaseY,
        JumpPhysicsSnapshot physics,
        float minimumHoldSeconds,
        float maximumHoldSeconds,
        float releaseSafetyMargin,
        out float selectedHold,
        out int minimumHeldSteps,
        out int maximumHeldSteps,
        out float minimumApexFeetY,
        out float maximumApexFeetY,
        out string summary)
    {
        selectedHold = 0f;
        minimumHeldSteps = 0;
        maximumHeldSteps = 0;
        minimumApexFeetY = contactFeetY;
        maximumApexFeetY = contactFeetY;
        StringBuilder evaluations = new();
        float maximumUsefulHold = Mathf.Clamp(
            Mathf.Min(physics.EffectiveHoldCapSeconds, maximumHoldSeconds),
            MinimumHoldSeconds,
            MaximumHoldSeconds);
        float minimumUsefulHold = Mathf.Clamp(
            minimumHoldSeconds,
            MinimumHoldSeconds,
            maximumUsefulHold);
        GetWallFixedStepPhysics(
            physics,
            out float gravity,
            out float heldVelocity,
            out float fixedStep);
        float discreteReleaseTail = PredictReleasedApexRiseFixedStep(
            heldVelocity,
            gravity,
            fixedStep);
        // The continuous tail is deliberately used only as the high side of
        // the safety envelope. It covers the sub-step phase between the last
        // observed held tick and the render Update that delivers UP.
        float conservativeReleaseTail =
            heldVelocity * heldVelocity / (2f * gravity);
        float highestLegalApex = wallReleaseY - Mathf.Max(
            0.08f,
            releaseSafetyMargin);

        foreach (float hold in HoldCandidates)
        {
            if (hold + 0.001f < minimumUsefulHold ||
                hold > maximumUsefulHold + 0.001f)
            {
                continue;
            }

            GetWallHeldStepEnvelope(
                hold,
                physics,
                fixedStep,
                out int candidateMinimumSteps,
                out int candidateMaximumSteps);
            float lowApexFeetY = contactFeetY +
                candidateMinimumSteps * heldVelocity * fixedStep +
                discreteReleaseTail;
            float highApexFeetY = contactFeetY +
                candidateMaximumSteps * heldVelocity * fixedStep +
                conservativeReleaseTail;
            bool belowReleaseEnvelope =
                highApexFeetY <= highestLegalApex + 0.001f;
            evaluations.Append(
                $"H={hold:F3}:HeldSteps=[{candidateMinimumSteps}," +
                $"{candidateMaximumSteps}],ApexFeet=[{lowApexFeetY:F3}," +
                $"{highApexFeetY:F3}],Limit={highestLegalApex:F3}," +
                $"HeldV={heldVelocity:F3},Tail=[{discreteReleaseTail:F3}," +
                $"{conservativeReleaseTail:F3}]," +
                $"{(belowReleaseEnvelope ? "SafeSetup" : "WouldCrossLip")} | ");
            if (!belowReleaseEnvelope)
                continue;

            // The strongest pulse that still preserves the wall is the most
            // useful reposition: it shortens the released descent without
            // allowing the setup action itself to become an exit action.
            selectedHold = hold;
            minimumHeldSteps = candidateMinimumSteps;
            maximumHeldSteps = candidateMaximumSteps;
            minimumApexFeetY = lowApexFeetY;
            maximumApexFeetY = highApexFeetY;
        }

        summary = evaluations.ToString();
        return selectedHold > 0f;
    }

    /// <summary>
    /// Solves a wall press against a downstream vertical face in two
    /// dimensions. A legal candidate must remain above the current platform
    /// while clearing its right edge, then meet the next face inside the
    /// supplied feet-Y window. This differs from wall-top landing prediction:
    /// the desired result is descending physical contact, not safe support.
    /// </summary>
    internal bool TryChooseWallFaceInterceptHold(
        float contactX,
        float contactFeetY,
        BonusBoardSegment currentWall,
        float wallReleaseY,
        BonusBoardSegment downstreamFace,
        float playerHalfWidth,
        float horizontalSpeed,
        float minimumContactFeetY,
        float maximumContactFeetY,
        float preferredContactFeetY,
        JumpPhysicsSnapshot physics,
        float minimumHoldSeconds,
        float maximumHoldSeconds,
        out float selectedHold,
        out float lipCrossingSeconds,
        out float topClearSeconds,
        out float targetContactSeconds,
        out float predictedTopClearFeetY,
        out float predictedContactFeetY,
        out float predictedContactVelocityY,
        out string summary)
    {
        selectedHold = 0f;
        lipCrossingSeconds = 0f;
        topClearSeconds = 0f;
        targetContactSeconds = 0f;
        predictedTopClearFeetY = contactFeetY;
        predictedContactFeetY = contactFeetY;
        predictedContactVelocityY = 0f;
        StringBuilder evaluations = new();
        float bestScore = float.PositiveInfinity;
        float speed = Mathf.Max(1f, Mathf.Abs(horizontalSpeed));
        float halfWidth = Mathf.Clamp(playerHalfWidth, 0.15f, 1.25f);
        float topClearCenterX = currentWall.Right + halfWidth + 0.03f;
        float targetContactCenterX = downstreamFace.Left - halfWidth;
        float topClearTravel = topClearCenterX - contactX;
        float targetTravel = targetContactCenterX - contactX;
        if (topClearTravel <= 0.05f ||
            targetTravel <= topClearTravel + 0.05f ||
            maximumContactFeetY < minimumContactFeetY + 0.05f)
        {
            summary =
                $"GeometryReject[ContactX={contactX:F3}," +
                $"TopClearX={topClearCenterX:F3},TargetX={targetContactCenterX:F3}," +
                $"Window=[{minimumContactFeetY:F3},{maximumContactFeetY:F3}]]";
            return false;
        }

        float maximumUsefulHold = Mathf.Clamp(
            Mathf.Min(physics.EffectiveHoldCapSeconds, maximumHoldSeconds),
            MinimumHoldSeconds,
            MaximumHoldSeconds);
        float minimumUsefulHold = Mathf.Clamp(
            minimumHoldSeconds,
            MinimumHoldSeconds,
            maximumUsefulHold);
        float desiredContactFeetY = Mathf.Clamp(
            preferredContactFeetY,
            minimumContactFeetY,
            maximumContactFeetY);
        GetWallFixedStepPhysics(
            physics,
            out float gravity,
            out float heldVelocity,
            out float fixedStep);

        foreach (float hold in HoldCandidates)
        {
            if (hold + 0.001f < minimumUsefulHold ||
                hold > maximumUsefulHold + 0.001f)
            {
                continue;
            }

            GetWallHeldStepEnvelope(
                hold,
                physics,
                fixedStep,
                out int candidateMinimumSteps,
                out int candidateMaximumSteps);
            bool legal = true;
            float worstTopMargin = float.PositiveInfinity;
            float worstFaceMargin = float.PositiveInfinity;
            float nominalFaceY = contactFeetY;
            float nominalFaceVelocityY = 0f;
            float nominalLipSeconds = 0f;
            float nominalTopSeconds = 0f;
            float nominalTargetSeconds = 0f;
            float nominalTopY = contactFeetY;
            int nominalSteps = Mathf.Clamp(
                Mathf.CeilToInt(hold / fixedStep - 0.0001f),
                candidateMinimumSteps,
                candidateMaximumSteps);
            StringBuilder stepEvaluations = new();

            for (int heldSteps = candidateMinimumSteps;
                 heldSteps <= candidateMaximumSteps;
                 heldSteps++)
            {
                if (!TryPredictWallFaceInterceptForHeldSteps(
                        heldSteps,
                        contactFeetY,
                        wallReleaseY,
                        currentWall.Top,
                        topClearTravel,
                        targetTravel,
                        speed,
                        physics.BaseHorizontalSpeed,
                        physics.BoostHorizontalDeceleration,
                        minimumContactFeetY,
                        maximumContactFeetY,
                        gravity,
                        heldVelocity,
                        fixedStep,
                        out float stepLipSeconds,
                        out float stepTopSeconds,
                        out float stepTargetSeconds,
                        out float stepTopY,
                        out float stepFaceY,
                        out float stepFaceVelocityY,
                        out float stepTopMargin,
                        out float stepFaceMargin,
                        out string stepReason))
                {
                    legal = false;
                }

                stepEvaluations.Append(
                    $"N{heldSteps}[LipT={stepLipSeconds:F3}," +
                    $"ClearY={stepTopY:F3},FaceY={stepFaceY:F3}," +
                    $"FaceVY={stepFaceVelocityY:F3}," +
                    $"Margins=[{stepTopMargin:F3},{stepFaceMargin:F3}]," +
                    $"{stepReason}] ");
                worstTopMargin = Mathf.Min(worstTopMargin, stepTopMargin);
                worstFaceMargin = Mathf.Min(worstFaceMargin, stepFaceMargin);
                if (heldSteps == nominalSteps)
                {
                    nominalLipSeconds = stepLipSeconds;
                    nominalTopSeconds = stepTopSeconds;
                    nominalTargetSeconds = stepTargetSeconds;
                    nominalTopY = stepTopY;
                    nominalFaceY = stepFaceY;
                    nominalFaceVelocityY = stepFaceVelocityY;
                }
            }

            evaluations.Append(
                $"H={hold:F3}:HeldSteps=[{candidateMinimumSteps}," +
                $"{candidateMaximumSteps}],WorstMargins=" +
                $"[{worstTopMargin:F3},{worstFaceMargin:F3}]," +
                $"{(legal ? "SafeIntercept" : "Reject")}," +
                $"Steps{{{stepEvaluations}}} | ");
            if (!legal)
                continue;

            float edgePenalty = Mathf.Max(0f, 0.25f - worstFaceMargin);
            float topPenalty = Mathf.Max(0f, 0.35f - worstTopMargin);
            float score =
                Mathf.Abs(nominalFaceY - desiredContactFeetY) +
                edgePenalty * 2f + topPenalty * 1.5f + hold * 0.02f;
            if (score >= bestScore)
                continue;

            bestScore = score;
            selectedHold = hold;
            lipCrossingSeconds = nominalLipSeconds + physics.InputDelaySeconds;
            topClearSeconds = nominalTopSeconds + physics.InputDelaySeconds;
            targetContactSeconds = nominalTargetSeconds + physics.InputDelaySeconds;
            predictedTopClearFeetY = nominalTopY;
            predictedContactFeetY = nominalFaceY;
            predictedContactVelocityY = nominalFaceVelocityY;
        }

        summary = evaluations.ToString();
        return bestScore < float.PositiveInfinity;
    }

    private static bool TryPredictWallFaceInterceptForHeldSteps(
        int heldSteps,
        float contactFeetY,
        float wallReleaseY,
        float currentWallTop,
        float topClearTravel,
        float targetTravel,
        float horizontalSpeed,
        float baseHorizontalSpeed,
        float boostHorizontalDeceleration,
        float minimumContactFeetY,
        float maximumContactFeetY,
        float gravity,
        float heldVelocity,
        float fixedStep,
        out float lipCrossingSeconds,
        out float topClearSeconds,
        out float targetContactSeconds,
        out float predictedTopClearFeetY,
        out float predictedContactFeetY,
        out float predictedContactVelocityY,
        out float topMargin,
        out float faceMargin,
        out string reason)
    {
        lipCrossingSeconds = 0f;
        topClearSeconds = 0f;
        targetContactSeconds = 0f;
        predictedTopClearFeetY = contactFeetY;
        predictedContactFeetY = contactFeetY;
        predictedContactVelocityY = 0f;
        topMargin = float.NegativeInfinity;
        faceMargin = float.NegativeInfinity;
        reason = "Unsolved";

        // Wall motion is semi-implicit and fixed-step quantised. The V0.27
        // trace showed that a continuous parabola overestimates a roughly
        // 0.55-second face flight by enough to move the result across the
        // finite-face boundary. Simulate the same order as the game instead:
        // powered steps retain the observed post-gravity held velocity;
        // released steps subtract gravity before integrating position.
        float feetY = contactFeetY;
        float velocityY = heldVelocity;
        int lipStep = -1;
        float lipFeetY = contactFeetY;
        float lipVelocityY = heldVelocity;
        bool lipReachedWhileHeld = contactFeetY >= wallReleaseY - 0.0001f;
        if (lipReachedWhileHeld)
            lipStep = 0;

        for (int step = 1; step <= heldSteps; step++)
        {
            velocityY = heldVelocity;
            feetY += velocityY * fixedStep;
            if (lipStep < 0 && feetY >= wallReleaseY - 0.0001f)
            {
                lipStep = step;
                lipFeetY = feetY;
                lipVelocityY = velocityY;
                lipReachedWhileHeld = true;
            }
        }

        if (lipStep < 0)
        {
            for (int releasedStep = 1; releasedStep <= 128; releasedStep++)
            {
                velocityY -= gravity * fixedStep;
                feetY += velocityY * fixedStep;
                if (feetY >= wallReleaseY - 0.0001f)
                {
                    lipStep = heldSteps + releasedStep;
                    lipFeetY = feetY;
                    lipVelocityY = velocityY;
                    break;
                }

                if (velocityY <= 0f)
                    break;
            }
        }

        if (lipStep < 0)
        {
            float achievedRise = feetY - contactFeetY;
            reason = $"LipUnreachable(DiscreteRise={achievedRise:F3}," +
                     $"Required={wallReleaseY - contactFeetY:F3})";
            return false;
        }

        int baseTopSteps = SolveFixedStepHorizontalTravelSteps(
            horizontalSpeed,
            baseHorizontalSpeed,
            boostHorizontalDeceleration,
            topClearTravel,
            fixedStep);
        int baseTargetSteps = Mathf.Max(
            baseTopSteps + 1,
            SolveFixedStepHorizontalTravelSteps(
                horizontalSpeed,
                baseHorizontalSpeed,
                boostHorizontalDeceleration,
                targetTravel,
                fixedStep));
        int earliestReleaseHeldStep = lipReachedWhileHeld
            ? lipStep
            : heldSteps;
        int latestReleaseHeldStep = heldSteps;
        float minimumTopY = float.PositiveInfinity;
        float maximumTopY = float.NegativeInfinity;
        float minimumFaceY = float.PositiveInfinity;
        float maximumFaceY = float.NegativeInfinity;
        float maximumFaceVelocityY = float.NegativeInfinity;
        float minimumFaceVelocityY = float.PositiveInfinity;

        for (int releaseHeldStep = earliestReleaseHeldStep;
             releaseHeldStep <= latestReleaseHeldStep;
             releaseHeldStep++)
        {
            int poweredStepsAfterLip = lipReachedWhileHeld
                ? Mathf.Max(0, releaseHeldStep - lipStep)
                : 0;
            // One horizontal fixed-step of timing tolerance covers collision
            // resolution at the old lip and a small live-speed quantisation
            // error without reverting to a render-time hold envelope.
            for (int timingOffset = -1; timingOffset <= 1; timingOffset++)
            {
                int topSteps = Mathf.Max(1, baseTopSteps + timingOffset);
                int targetSteps = Mathf.Max(
                    topSteps + 1,
                    baseTargetSteps + timingOffset);
                PredictFixedStepWallFlight(
                    lipFeetY,
                    lipVelocityY,
                    poweredStepsAfterLip,
                    topSteps,
                    heldVelocity,
                    gravity,
                    fixedStep,
                    out float candidateTopY,
                    out _);
                PredictFixedStepWallFlight(
                    lipFeetY,
                    lipVelocityY,
                    poweredStepsAfterLip,
                    targetSteps,
                    heldVelocity,
                    gravity,
                    fixedStep,
                    out float candidateFaceY,
                    out float candidateFaceVelocityY);
                minimumTopY = Mathf.Min(minimumTopY, candidateTopY);
                maximumTopY = Mathf.Max(maximumTopY, candidateTopY);
                minimumFaceY = Mathf.Min(minimumFaceY, candidateFaceY);
                maximumFaceY = Mathf.Max(maximumFaceY, candidateFaceY);
                minimumFaceVelocityY = Mathf.Min(
                    minimumFaceVelocityY,
                    candidateFaceVelocityY);
                maximumFaceVelocityY = Mathf.Max(
                    maximumFaceVelocityY,
                    candidateFaceVelocityY);

                if (releaseHeldStep == earliestReleaseHeldStep &&
                    timingOffset == 0)
                {
                    predictedTopClearFeetY = candidateTopY;
                    predictedContactFeetY = candidateFaceY;
                    predictedContactVelocityY = candidateFaceVelocityY;
                }
            }
        }

        lipCrossingSeconds = lipStep * fixedStep;
        topClearSeconds = (lipStep + baseTopSteps) * fixedStep;
        targetContactSeconds = (lipStep + baseTargetSteps) * fixedStep;
        topMargin = minimumTopY - currentWallTop;
        float lowerMargin = minimumFaceY - minimumContactFeetY;
        float upperMargin = maximumContactFeetY - maximumFaceY;
        faceMargin = Mathf.Min(lowerMargin, upperMargin);
        bool clearsTop = topMargin >= WallFaceTopClearance;
        bool insideFace = faceMargin >= 0f;
        bool descending = maximumFaceVelocityY <=
            WallFaceMaximumContactVelocityY;
        reason = $"DiscreteLip[Step={lipStep},Y={lipFeetY:F3}," +
                 $"VY={lipVelocityY:F3},Held={lipReachedWhileHeld}]," +
                 $"ReleaseHeldSteps=[{earliestReleaseHeldStep}," +
                 $"{latestReleaseHeldStep}],XSteps[Clear={baseTopSteps}," +
                 $"Face={baseTargetSteps},Tolerance=+/-1,V0=" +
                 $"{horizontalSpeed:F3},Base={baseHorizontalSpeed:F3}," +
                 $"Decel={boostHorizontalDeceleration:F3}]," +
                 $"ClearY=[{minimumTopY:F3},{maximumTopY:F3}]," +
                 $"FaceY=[{minimumFaceY:F3},{maximumFaceY:F3}]," +
                 $"FaceVY=[{minimumFaceVelocityY:F3}," +
                 $"{maximumFaceVelocityY:F3}]," +
                 $"{(clearsTop && insideFace && descending ? "Safe" : "Reject")}";
        return clearsTop && insideFace && descending;
    }

    private static void PredictFixedStepWallFlight(
        float startFeetY,
        float startVelocityY,
        int poweredSteps,
        int elapsedSteps,
        float heldVelocity,
        float gravity,
        float fixedStep,
        out float feetY,
        out float velocityY)
    {
        feetY = startFeetY;
        velocityY = startVelocityY;
        for (int step = 1; step <= elapsedSteps; step++)
        {
            if (step <= poweredSteps)
            {
                velocityY = heldVelocity;
            }
            else
            {
                velocityY -= gravity * fixedStep;
            }
            feetY += velocityY * fixedStep;
        }
    }

    private static int SolveFixedStepHorizontalTravelSteps(
        float initialSpeed,
        float baseHorizontalSpeed,
        float boostHorizontalDeceleration,
        float requiredTravel,
        float fixedStep)
    {
        float speed = Mathf.Max(1f, initialSpeed);
        float baseSpeed = Mathf.Min(
            speed,
            Mathf.Max(1f, baseHorizontalSpeed));
        float deceleration = Mathf.Max(0.10f, boostHorizontalDeceleration);
        float travel = 0f;
        for (int step = 1; step <= 256; step++)
        {
            // Horizontal speed is sampled before the section's fixed-step
            // deceleration, matching the existing dynamic integral while
            // retaining the collision/contact tick boundary.
            travel += speed * fixedStep;
            if (travel >= requiredTravel - 0.0001f)
                return step;
            speed = Mathf.Max(baseSpeed, speed - deceleration * fixedStep);
        }

        return 256;
    }

    private static void GetWallFixedStepPhysics(
        JumpPhysicsSnapshot physics,
        out float gravity,
        out float heldVelocity,
        out float fixedStep)
    {
        gravity = Mathf.Clamp(physics.GravityMagnitude, 5f, 200f);
        heldVelocity = Mathf.Clamp(physics.JumpVelocity, 5f, 50f);
        fixedStep = Mathf.Clamp(physics.FixedDeltaTime, 0.005f, 0.05f);
    }

    private static void GetWallHeldStepEnvelope(
        float requestedHold,
        JumpPhysicsSnapshot physics,
        float fixedStep,
        out int minimumSteps,
        out int maximumSteps)
    {
        float cappedHold = Mathf.Min(
            requestedHold,
            physics.EffectiveHoldCapSeconds);
        int nativeCapSteps = Mathf.Max(
            1,
            Mathf.CeilToInt(
                physics.EffectiveHoldCapSeconds / fixedStep - 0.0001f));
        int nominalSteps = Mathf.Max(
            1,
            Mathf.CeilToInt(cappedHold / fixedStep - 0.0001f));
        // Mandatory pulses are delivered by JumpController with this exact
        // fixed-step ceiling. Unlike render-time pointer scheduling, there is
        // no N-1/N+1 hold ambiguity to model here.
        minimumSteps = Mathf.Min(nativeCapSteps, nominalSteps);
        maximumSteps = minimumSteps;
    }

    private static float PredictReleasedApexRiseFixedStep(
        float releasedVelocity,
        float gravity,
        float fixedStep)
    {
        float rise = 0f;
        float velocity = releasedVelocity;
        for (int step = 0; step < 128; step++)
        {
            velocity -= gravity * fixedStep;
            if (velocity <= 0f)
                break;
            rise += velocity * fixedStep;
        }
        return rise;
    }

    internal bool TryPredictReleasedWallFaceContact(
        float currentX,
        float currentFeetY,
        float currentVelocityY,
        float targetContactX,
        float horizontalSpeed,
        JumpPhysicsSnapshot physics,
        out int fixedSteps,
        out float contactSeconds,
        out float predictedFeetY,
        out float predictedVelocityY)
    {
        GetWallFixedStepPhysics(
            physics,
            out float gravity,
            out _,
            out float fixedStep);
        float travel = targetContactX - currentX;
        if (travel <= 0f || horizontalSpeed <= 0.1f)
        {
            fixedSteps = 0;
            contactSeconds = 0f;
            predictedFeetY = currentFeetY;
            predictedVelocityY = currentVelocityY;
            return false;
        }

        fixedSteps = SolveFixedStepHorizontalTravelSteps(
            horizontalSpeed,
            physics.BaseHorizontalSpeed,
            physics.BoostHorizontalDeceleration,
            travel,
            fixedStep);
        contactSeconds = fixedSteps * fixedStep;
        predictedFeetY = currentFeetY +
            fixedSteps * fixedStep * currentVelocityY -
            gravity * fixedStep * fixedStep *
                fixedSteps * (fixedSteps + 1) * 0.5f;
        predictedVelocityY = currentVelocityY -
            fixedSteps * gravity * fixedStep;
        return true;
    }

    internal bool IsTrajectorySafe(
        BonusHazard hazard,
        float launchX,
        float landingX,
        float launchFeetY,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        out string summary) =>
        TrajectoryClearsHazard(
            hazard, launchX, landingX, launchFeetY,
            requestedHold, flightSeconds, physics, out summary);

    internal float PredictVerticalYAtTime(
        float launchFeetY,
        float requestedHold,
        float elapsedSinceInput,
        JumpPhysicsSnapshot physics)
    {
        float motionTime = Mathf.Max(
            0f,
            elapsedSinceInput - physics.InputDelaySeconds);
        float effectiveHold = Mathf.Min(
            requestedHold,
            physics.EffectiveHoldCapSeconds);
        float height;
        if (motionTime <= effectiveHold)
        {
            height = physics.JumpVelocity * motionTime;
        }
        else
        {
            float releasedTime = motionTime - effectiveHold;
            height = physics.JumpVelocity * effectiveHold +
                physics.JumpVelocity * releasedTime -
                0.5f * physics.GravityMagnitude *
                releasedTime * releasedTime;
        }

        return launchFeetY + height;
    }

    private static int CountTrajectorySphereHits(
        IReadOnlyList<Vector2> spheres,
        float launchX,
        float landingX,
        float launchFeetY,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics)
    {
        if (spheres == null || spheres.Count == 0 || landingX <= launchX)
            return 0;

        int hits = 0;
        for (int index = 0; index < spheres.Count; index++)
        {
            Vector2 sphere = spheres[index];
            if (sphere.x < launchX - 0.35f ||
                sphere.x > landingX + 0.35f)
            {
                continue;
            }

            float travel = Mathf.Clamp(
                sphere.x - launchX,
                0f,
                Mathf.Max(0f, landingX - launchX));
            float time = SolveHorizontalTravelTime(
                speed,
                travel,
                flightSeconds,
                physics);
            if (time > flightSeconds + 0.03f)
                continue;

            float predictedFeetY = launchFeetY +
                PredictVerticalDisplacementAtTime(
                    requestedHold,
                    time,
                    physics);
            // The sphere trigger overlaps the character body, so its centre
            // does not need to equal the feet coordinate exactly. This score
            // only ranks already-safe landing trajectories; it never makes an
            // unsafe landing legal.
            if (Mathf.Abs(predictedFeetY - sphere.y) <= 1.15f)
                hits++;
        }

        return hits;
    }

    private static float PredictVerticalDisplacementAtTime(
        float requestedHold,
        float elapsedSinceInput,
        JumpPhysicsSnapshot physics)
    {
        float motionTime = Mathf.Max(
            0f,
            elapsedSinceInput - physics.InputDelaySeconds);
        float effectiveHold = Mathf.Min(
            requestedHold,
            physics.EffectiveHoldCapSeconds);
        if (motionTime <= effectiveHold)
            return physics.JumpVelocity * motionTime;

        float releasedTime = motionTime - effectiveHold;
        return physics.JumpVelocity * effectiveHold +
            physics.JumpVelocity * releasedTime -
            0.5f * physics.GravityMagnitude *
            releasedTime * releasedTime;
    }

    internal float PredictRawInputToLandingSeconds(
        float holdSeconds,
        float targetHeightDelta,
        JumpPhysicsSnapshot physics) =>
        TryPredictFlightTime(
            holdSeconds,
            targetHeightDelta,
            physics,
            out float flightSeconds,
            out _,
            out _)
            ? flightSeconds
            : 0f;

    private static bool TrajectoryClearsHazard(
        BonusHazard hazard,
        float launchX,
        float landingX,
        float launchFeetY,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        out string summary)
    {
        bool intersectsX = hazard.IsValid &&
            hazard.Right >= launchX && hazard.Left <= landingX;
        if (!intersectsX)
        {
            summary = hazard.IsValid ? "HazardOutsideFlight" : "NoHazard";
            return true;
        }

        float span = Mathf.Max(0.01f, landingX - launchX);
        float hazardX = Mathf.Clamp(hazard.CenterX, launchX, landingX);
        float time = flightSeconds * (hazardX - launchX) / span;
        float motionTime = Mathf.Max(0f, time - physics.InputDelaySeconds);
        float effectiveHold = Mathf.Min(requestedHold, physics.EffectiveHoldCapSeconds);
        float jumpVelocity = physics.JumpVelocity;
        float height;
        if (motionTime <= effectiveHold)
        {
            height = jumpVelocity * motionTime;
        }
        else
        {
            float releasedTime = motionTime - effectiveHold;
            height = jumpVelocity * effectiveHold +
                jumpVelocity * releasedTime -
                0.5f * physics.GravityMagnitude * releasedTime * releasedTime;
        }

        float predictedFeetY = launchFeetY + height;
        float requiredFeetY = hazard.Top + 0.30f;
        bool clears = predictedFeetY >= requiredFeetY;
        summary =
            $"HazardTrajectory[X={hazardX:F2},T={time:F3}," +
            $"FeetY={predictedFeetY:F2},Need={requiredFeetY:F2},Clear={clears}]";
        return clears;
    }

    private static float PredictHorizontalTravel(
        float speed,
        float predictedFlightSeconds,
        float requestedHold,
        float targetHeightDelta,
        JumpPhysicsSnapshot physics,
        out string source)
    {
        float travel = PredictHorizontalTravelAtTime(
            speed,
            predictedFlightSeconds,
            physics);
        float baseSpeed = Mathf.Min(
            speed,
            Mathf.Max(1f, physics.BaseHorizontalSpeed));
        float deceleration = Mathf.Max(
            0.10f,
            physics.BoostHorizontalDeceleration);
        source =
            $"DynamicIntegral[V0={speed:F2},Base={baseSpeed:F2}," +
            $"Decel={deceleration:F2},T={predictedFlightSeconds:F3}]";
        return travel;
    }

    private static float ApplyLandingBias(
        float predictedTravel,
        float heightDelta,
        float requestedHold,
        JumpPhysicsSnapshot physics,
        ref string source)
    {
        float observedBias = physics.LandingErrorProfile.GetBias(
            heightDelta,
            requestedHold,
            out int sampleCount);
        if (sampleCount <= 0)
        {
            source += ";LandingBias[None]";
            return predictedTravel;
        }

        float weight = sampleCount >= 3
            ? 1f
            : sampleCount == 2
                ? 0.75f
                : 0.50f;
        float appliedBias = Mathf.Clamp(
            observedBias * weight,
            -1.25f,
            1.25f);
        source +=
            $";LandingBias[Observed={observedBias:F3},N={sampleCount}," +
            $"Weight={weight:F2},Applied={appliedBias:F3}]";
        return Mathf.Max(0.10f, predictedTravel + appliedBias);
    }

    private static float PredictHorizontalTravelAtTime(
        float speed,
        float elapsedSeconds,
        JumpPhysicsSnapshot physics)
    {
        float duration = Mathf.Max(0f, elapsedSeconds);
        float baseSpeed = Mathf.Min(
            speed,
            Mathf.Max(1f, physics.BaseHorizontalSpeed));
        float deceleration = Mathf.Max(
            0.10f,
            physics.BoostHorizontalDeceleration);
        float boostedDuration = speed > baseSpeed
            ? Mathf.Min(duration, (speed - baseSpeed) / deceleration)
            : 0f;
        float boostedTravel =
            speed * boostedDuration -
            0.5f * deceleration *
            boostedDuration * boostedDuration;
        float baseTravel = baseSpeed * Mathf.Max(
            0f,
            duration - boostedDuration);
        return boostedTravel + baseTravel;
    }

    private static float PredictWallContactTravelAtTime(
        float speed,
        float elapsedSeconds) =>
        Mathf.Max(0f, speed) * Mathf.Max(0f, elapsedSeconds);

    private static float SolveWallContactTravelTime(
        float speed,
        float requiredTravel) =>
        Mathf.Max(0f, requiredTravel) / Mathf.Max(1f, speed);

    private static float SolveHorizontalTravelTime(
        float speed,
        float requiredTravel,
        float maximumTime,
        JumpPhysicsSnapshot physics)
    {
        float low = 0f;
        float high = Mathf.Max(0.05f, maximumTime);
        while (PredictHorizontalTravelAtTime(speed, high, physics) < requiredTravel &&
               high < 1.5f)
        {
            high += 0.10f;
        }

        for (int iteration = 0; iteration < 18; iteration++)
        {
            float midpoint = (low + high) * 0.5f;
            if (PredictHorizontalTravelAtTime(speed, midpoint, physics) < requiredTravel)
                low = midpoint;
            else
                high = midpoint;
        }

        return high;
    }

    private static float CalibrateBaseSpeedFlightDuration(
        float speed,
        float requestedHold,
        float targetHeightDelta,
        float analyticalFlightSeconds,
        JumpPhysicsSnapshot physics,
        out string source)
    {
        float baseSpeed = Mathf.Max(1f, physics.BaseHorizontalSpeed);
        bool isBaseSpeed = Mathf.Abs(speed - baseSpeed) <= 0.50f;
        bool heightSpecificTiming = Mathf.Abs(targetHeightDelta) > 0.35f;
        if ((isBaseSpeed || heightSpecificTiming) &&
            physics.TravelProfile.TryGetDuration(
                requestedHold,
                targetHeightDelta,
                out float observedDuration,
                out int sampleCount))
        {
            float weight = sampleCount >= 3
                ? 1f
                : sampleCount == 2
                    ? 0.70f
                    : 0.35f;
            float calibrated = Mathf.Lerp(
                analyticalFlightSeconds,
                observedDuration,
                weight);
            source =
                $"{(heightSpecificTiming ? "HeightFlightFeedback" : "BaseFlightFeedback")}" +
                $"[Analytic={analyticalFlightSeconds:F3}," +
                $"Observed={observedDuration:F3},N={sampleCount}," +
                $"Weight={weight:F2},Used={calibrated:F3}]";
            return calibrated;
        }

        source = isBaseSpeed
            ? $"AnalyticalFlight[NoTierSample,T={analyticalFlightSeconds:F3}]"
            : heightSpecificTiming
                ? $"AnalyticalHeightFlight[NoHeightSample,V0={speed:F2}," +
                  $"Base={baseSpeed:F2},T={analyticalFlightSeconds:F3}]"
            : $"BoostFlightIntegralOnly[V0={speed:F2},Base={baseSpeed:F2}," +
              $"T={analyticalFlightSeconds:F3}]";
        return analyticalFlightSeconds;
    }

    private static bool TryPredictFlightTime(
        float holdSeconds,
        float targetHeightDelta,
        JumpPhysicsSnapshot physics,
        out float flightSeconds,
        out float effectiveHoldSeconds,
        out float maximumRise)
    {
        float jumpVelocity = Mathf.Clamp(
            physics.JumpVelocity,
            5f,
            50f);
        float gravityMagnitude = Mathf.Clamp(
            physics.GravityMagnitude,
            5f,
            200f);
        float effectiveHoldCapSeconds = Mathf.Clamp(
            physics.EffectiveHoldCapSeconds,
            0.03f,
            MaximumHoldSeconds);
        float inputDelaySeconds = Mathf.Clamp(
            physics.InputDelaySeconds,
            0f,
            0.25f);
        effectiveHoldSeconds = Mathf.Min(
            Mathf.Clamp(
                holdSeconds,
                MinimumHoldSeconds,
                MaximumHoldSeconds),
            effectiveHoldCapSeconds);
        float heightAtRelease =
            jumpVelocity * effectiveHoldSeconds;
        maximumRise =
            heightAtRelease +
            jumpVelocity * jumpVelocity /
            (2f * gravityMagnitude);
        float requiredRise = targetHeightDelta +
            (targetHeightDelta > 0.35f ? UpwardClearance : 0f);
        if (maximumRise < requiredRise)
        {
            flightSeconds = 0f;
            return false;
        }

        float remainingHeight =
            targetHeightDelta - heightAtRelease;
        float discriminant =
            jumpVelocity * jumpVelocity -
            2f * gravityMagnitude * remainingHeight;

        if (discriminant < 0f)
        {
            flightSeconds = 0f;
            return false;
        }

        float descendingTime =
            (jumpVelocity + Mathf.Sqrt(discriminant)) /
            gravityMagnitude;
        flightSeconds =
            inputDelaySeconds +
            effectiveHoldSeconds +
            descendingTime;
        return descendingTime >= 0f &&
               flightSeconds > 0.05f &&
               flightSeconds < 2f;
    }

    private static void AppendEvaluation(
        StringBuilder builder,
        string evaluation)
    {
        if (builder.Length > 0)
            builder.Append(" | ");
        builder.Append(evaluation);
    }

    private static BonusJumpPlan Invalid(
        string reason,
        string candidateSummary) =>
        new(false, false, 0f, 0f, 0f, 0f, 0f,
            0f, 0f, reason, candidateSummary);
}
