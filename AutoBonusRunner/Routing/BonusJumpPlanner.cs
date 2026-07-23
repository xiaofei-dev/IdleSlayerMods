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
    GroundedContactEscape,
    SphereCollectionJump,
    SphereSweepToLowerLanding,
    HazardClearanceJump
}

internal enum Stage2Section1RoutePhase
{
    Inactive,
    EntryChain,
    NarrowHandoff,
    LowCorridorWallCatch,
    SteppedWallTraverse,
    RisingStair,
    HighRoadDescent,
    FreeLanding
}

internal readonly record struct Stage2Section1RouteDomain(
    bool IsActive,
    Stage2Section1RoutePhase Phase,
    string Evidence)
{
    internal string Summary =>
        IsActive
            ? $"Stage2Section1Domain[Phase={Phase},{Evidence}]"
            : "Stage2Section1Domain[Inactive]";
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
    int ExpectedSphereHits = 0,
    float MinimumHorizontalTravel = 0f,
    float MaximumHorizontalTravel = 0f,
    bool FutureSpeedTransitionExpected = false,
    int ExpectedSpeedBoostHits = 0);

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
    private const float RouteLandingSafetyTier = 0.08f;
    private const float RouteDistanceTier = 0.10f;
    // Landing safety is already reduced by fixed-step and model uncertainty.
    // Once two holds both retain this additional reserve on the same target,
    // soul coverage may choose between them. A marginal-but-positive hold may
    // never trade away the comfortable landing of another candidate.
    private const float ComfortableSoulLandingMargin = 0.20f;
    // Sections 0-2 contain forward soul arcs whose leftmost pickups become
    // permanently unreachable when a centre-biased route waits until the
    // source lip. Derive one earliest-comfortable launch analytically inside
    // the already-proved window; do not multiply the trajectory search.
    // A raised target is not cleared merely because the feet are above its
    // raw left edge at first body overlap. Unity may resolve a descending
    // corner contact before the player centre reaches the scanner's safe
    // interval. The complete V0.62 trace did exactly that on a two-unit +3
    // pillar: the final ballistic endpoint was centred, but physical contact
    // occurred on the left corner and immediately changed the next action.
    // Require a small vertical corridor through SafeLeft as well. This is a
    // body/contact rule shared by every live map and speed mode, not a map
    // coordinate exception.
    private const float RaisedTargetFaceClearance = 0.08f;
    private const float RaisedTargetSafeEntryClearance = 0.35f;
    // Player position/trajectory Y is the feet coordinate. The pickup body is
    // almost entirely above it, so a symmetric +/- envelope invents hits on
    // souls well below a high arc. Include the measured body height and sphere
    // trigger radius above the feet while keeping only a small sole tolerance.
    private const float SpherePickupBelowFeet = 0.45f;
    private const float SpherePickupAboveFeet = 2.15f;
    // V0.64 runtime bounds measured the player's horizontal half width at
    // about 0.594. The old +/-0.35 centre test excluded souls touched by the
    // body at a stable landing, especially the first sphere just beyond a
    // narrow pillar. Keep a small rounded value that does not assume the
    // sphere's own trigger radius.
    private const float SpherePickupHorizontalReach = 0.60f;
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
        // Every command must be reproducible by the fixed-step actuator.
        // Fractional render-time tiers such as 0.075/0.105 were delivered as
        // different native durations depending on foreground/background
        // cadence, so they cannot be members of a deterministic route model.
        0.02f, 0.04f, 0.06f, 0.08f, 0.10f, 0.12f
    };

    private static readonly float[] HoldCandidates =
    {
        // The live game reports a 0.18s native cap and consumes input on a
        // 20 ms physics cadence. Keep the general list quantized too: a plan
        // and its delivered input must describe the same action on every map.
        0.02f, 0.04f, 0.06f, 0.08f, 0.10f,
        0.12f, 0.14f, 0.16f, 0.18f
    };

    // Live-geometry sections 3/4 need a plan the fixed-step actuator can
    // reproduce exactly. The retained V0.52 trace shows that wall-clock
    // requests such as 0.050/0.075s physically become 0.040/0.080s, which is
    // enough to invalidate a two-unit raised landing. Earlier sections keep
    // their mature candidate set; late live sections use only native 20ms
    // buckets so planned height and delivered height are the same quantity.
    private static readonly float[] FixedStepAlignedHoldCandidates =
    {
        0.02f, 0.04f, 0.06f, 0.08f, 0.10f,
        0.12f, 0.14f, 0.16f, 0.18f
    };

    internal BonusJumpPlan Plan(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard = default,
        IReadOnlyList<Vector2> sphereObjectives = null,
        int sectionIndex = -1,
        bool preferSphereCoverage = false,
        bool allowRecoverableLowerFaceCatch = false,
        bool useFixedStepAlignedHolds = false,
        SpiritBoostRouteContext spiritBoost = default,
        bool routeTargetIsAuthoritative = false,
        bool useStage2LiveTopologyProfile = false,
        int minimumSphereHits = 0)
    {
        if (!scan.IsValid)
            return Invalid(scan.Reason, scan.Reason);
        if (!routeTargetIsAuthoritative)
            scan = SelectLowerRouteWhenItContinues(scan, hazard);

        float speed = Mathf.Abs(playerVelocity.x);
        bool stage2Section1Domain =
            useStage2LiveTopologyProfile &&
            sectionIndex == 1;
        bool preferEarlySpiritPickupCoverage =
            preferSphereCoverage &&
            spiritBoost.Enabled &&
            sectionIndex >= 0 &&
            sectionIndex < 2;
        // Stage 1 Section 1 repeats a seven-sphere arc immediately above a
        // typed Spirit Boost. The retained trace proved a dual-outcome-safe
        // 5-sphere + boost command with 0.080 post-uncertainty landing margin,
        // while the generic 0.200 comfortable tier deliberately selected a
        // later 5-sphere command that could no longer reach the boost. Keep
        // this narrower objective trade local to that section; every other
        // section retains the full comfortable-margin ranking.
        bool allowStage1Section1VerifiedBoostMargin =
            preferSphereCoverage &&
            spiritBoost.Enabled &&
            sectionIndex == 1;
        // PredictHorizontalTravel already starts at input time and its
        // vertical model includes InputDelaySeconds. Adding another native
        // step for every Spirit command therefore counts the same horizontal
        // interval twice. V0.89 exposed that error twice: a narrow boost top
        // was reached on its raw leading edge instead of its safe corridor,
        // and a later raised-platform command changed hold tiers until its
        // launch window was missed. Stage 2 Section 1 has a separate retained
        // fixed-step execution contract, so keep compensation only there.
        // A real pickup/reset between input and takeoff is represented by the
        // slow/reset Spirit envelope rather than by translating both traces.
        bool compensateFixedStepInput =
            stage2Section1Domain;
        float fixedStepCommitTravel = compensateFixedStepInput
            ? PredictHorizontalTravelAtTime(
                speed,
                Mathf.Clamp(physics.FixedDeltaTime, 0.005f, 0.05f),
                physics)
            : 0f;
        float actionTriggerTolerance = compensateFixedStepInput
            ? Mathf.Max(
                TriggerTolerance,
                fixedStepCommitTravel + 0.03f)
            : TriggerTolerance;
        if (spiritBoost.Enabled && !spiritBoost.KinematicsAvailable)
        {
            return Invalid(
                "SpiritBoostKinematicsUnavailable",
                $"Typed Spirit Boost state is required before a " +
                $"speed-sensitive command can be proved. " +
                $"Context[{spiritBoost.Summary}]");
        }
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
                useFixedStepAlignedHolds
                    ? FixedStepAlignedHoldCandidates
                    : HoldCandidates,
                preferSphereCoverage &&
                    spiritBoost.Enabled &&
                    sectionIndex >= 0 &&
                    sectionIndex < 2,
                spiritBoost,
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

        // A level seam narrower than the player's body is continuous physical
        // road on every map and in every gameplay phase. The Stage-1 trace
        // exposed a 1.001-unit example for a measured 1.188-unit body, but the
        // decision is derived from each live scan rather than that coordinate,
        // map, section, speed, or Spirit state. A nearby spike still owns the
        // decision. Sphere scoring may override the continuous-road action
        // only when at least one objective is too high to be collected by the
        // grounded body; ordinary row spheres are collected while running.
        float inferredBodyWidth = 0f;
        float walkableGapLimit = 0.10f;
        bool walkableMicroGap = TryDescribeWalkableMicroGap(
                scan,
                hazard,
                out inferredBodyWidth,
                out walkableGapLimit);
        int walkablePassiveSphereObjectives = 0;
        int walkableElevatedSphereObjectives = 0;
        if (walkableMicroGap && sphereObjectives != null)
        {
            float passiveFeetY =
                Mathf.Max(scan.Current.Top, scan.Next.Top);
            float objectiveLeft = playerPosition.x - TriggerTolerance;
            float objectiveRight = scan.Next.SafeRight + 0.40f;
            foreach (Vector2 sphere in sphereObjectives)
            {
                if (sphere.x < objectiveLeft || sphere.x > objectiveRight)
                    continue;

                if (TrajectoryBodyOverlapsSphere(
                        passiveFeetY,
                        sphere.y))
                    walkablePassiveSphereObjectives++;
                else
                    walkableElevatedSphereObjectives++;
            }
        }
        bool walkableGapRequiresAirborneCollection =
            walkableElevatedSphereObjectives > 0;

        bool deepTrenchEntry =
            obstacle.Kind == BonusObstacleKind.LowerLanding &&
            scan.HeightDelta <= -5.50f;
        if (obstacle.Kind == BonusObstacleKind.LowerLanding &&
            TryPlanNaturalDrop(
                scan,
                playerPosition,
                speed,
                physics,
                spiritBoost,
                allowRecoverableLowerFaceCatch,
                out BonusJumpPlan naturalDrop))
        {
            // A verified natural drop is the safety baseline.  It may be
            // replaced only by a live-speed jump that lands on the same
            // verified lower support and intersects additional active sphere
            // triggers.  This recovers the dense high-platform rows without
            // weakening the terrain contract.
            if (sectionIndex == 3 || preferSphereCoverage)
            {
                if (TryPlanSphereSweepToLowerLanding(
                        scan,
                        playerPosition,
                        speed,
                        physics,
                        hazard,
                        sphereObjectives,
                        naturalDrop,
                        useFixedStepAlignedHolds
                            ? FixedStepAlignedHoldCandidates
                            : HoldCandidates,
                        spiritBoost,
                        out BonusJumpPlan sphereSweep,
                        out string sphereSweepEvaluation))
                {
                    return sphereSweep;
                }

                naturalDrop = naturalDrop with
                {
                    CandidateSummary = naturalDrop.CandidateSummary +
                        $" | SphereSweepFallbackToNaturalDrop[" +
                        $"{sphereSweepEvaluation}]"
                };
            }
            return naturalDrop;
        }

        // Adjacent and narrow walls have a short enough unsupported span to
        // reach the face before the pit guard, so they remain true passive
        // entries. Wider authored trenches need a separately solved low
        // descending entry hop (handled below).
        bool proactiveAdjacentWallApproach =
            useStage2LiveTopologyProfile &&
            spiritBoost.Enabled &&
            obstacle.Kind == BonusObstacleKind.AdjacentWall &&
            scan.Gap <= 0.12f &&
            scan.HeightDelta >= 1.50f;
        string proactiveAdjacentWallFallback = string.Empty;
        if (proactiveAdjacentWallApproach)
        {
            BonusJumpPlan proactiveWallApproach = PlanWallApproach(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                $"Stage2SpiritRisingStair[{obstacle.Evidence}]",
                spiritBoost,
                triggerTolerance: actionTriggerTolerance);
            if (proactiveWallApproach.IsValid)
            {
                return proactiveWallApproach with
                {
                    Reason = proactiveWallApproach.ShouldJumpNow
                        ? "Stage2SpiritRisingStairContact"
                        : "ApproachingStage2SpiritRisingStair",
                    CandidateSummary =
                        proactiveWallApproach.CandidateSummary +
                        " | RouteProfile=Stage2SpiritProactiveAdjacentWall. " +
                        "The nearly gapless rising step is approached with " +
                        "a speed-proved jump so wall contact is established " +
                        "before the passive drop route can miss the face."
                };
            }

            proactiveAdjacentWallFallback =
                $" | ProactiveApproachRejected[Reason=" +
                $"{proactiveWallApproach.Reason},Evidence=" +
                $"{proactiveWallApproach.CandidateSummary}]";
        }

        if (obstacle.Kind == BonusObstacleKind.AdjacentWall ||
            obstacle.Kind == BonusObstacleKind.NarrowPillarTrench)
        {
            BonusJumpPlan wallDrop = PlanWallDropApproach(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                proactiveAdjacentWallApproach
                    ? $"Stage2SpiritProactiveFallback[" +
                      $"{obstacle.Evidence}]" +
                      proactiveAdjacentWallFallback
                    : $"Obstacle={obstacle.Kind}[{obstacle.Evidence}]");
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
                spiritBoost,
                preferLowerTrenchContact: true,
                requireBelowSourceContact: true,
                triggerTolerance: actionTriggerTolerance);
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
                spiritBoost,
                requiresLowerFaceContact,
                requireBelowSourceContact: false,
                triggerTolerance: actionTriggerTolerance);
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
        // The old fixed one-unit inset deliberately hugged the leading edge
        // of every wide platform. That leaves little tolerance for timing or
        // speed error and little residence time for the next decision. A
        // receding-horizon controller should aim at the centre of the current
        // verified safe corridor, then replan from the physical landing.
        float preferredLandingX =
            (scan.Next.SafeLeft + scan.Next.SafeRight) * 0.50f;
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
        float bestLandingSafety = float.NegativeInfinity;
        BonusJumpPlan narrowSourceRecovery = default;
        float narrowSourceRecoveryScore = float.NegativeInfinity;
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

        // Route planning and input delivery share one native action lattice.
        // Keeping a render-time candidate list here made an otherwise valid
        // trajectory change length when the game was backgrounded.
        IReadOnlyList<float> groundHoldCandidates =
            FixedStepAlignedHoldCandidates;
        foreach (float hold in groundHoldCandidates)
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
            float noPickupTravel = travel;
            float minimumTravel = travel;
            float maximumTravel = travel;
            bool futureSpeedTransitionExpected = false;
            string speedEnvelopeCheck =
                "SpiritEnvelope=NotRequired";
            float launchLeft = scan.Next.SafeLeft - travel;
            float launchRight = scan.Next.SafeRight - travel;
            float usableLeft = Mathf.Max(launchLeft, scan.Current.SafeLeft);
            float usableRight = Mathf.Min(launchRight, scan.Current.SafeRight);

            if (usableRight - usableLeft < 0.02f)
            {
                // A two-unit landing can be physically real while its player
                // centre is just outside the scanner's extra-safe inset. The
                // retained V0.43 trace repeatedly landed on those narrow
                // supports, then rejected every next jump because the
                // safe-source/safe-target windows missed by only a few
                // hundredths. Evaluate the live grounded centre as a bounded
                // recovery launch before declaring that no intersection
                // exists. The target still has to be a safe-centre landing
                // and the complete trajectory still has to clear hazards.
                // The source, not the destination, creates the one-physics-step
                // deadline. Bonus Stage 2 Section 1 repeatedly uses a one-unit
                // transition support followed by a three-unit ordinary
                // platform. Requiring both surfaces to be narrow rejected the
                // already-prepared 0.02s continuation on the exact landing
                // FixedUpdate: the runner touched the one-unit top, advanced
                // beyond its safe-centre corridor, and fell before ordinary
                // two-step landing confirmation could replan.
                //
                // Keep every existing physical proof below. This broadens only
                // recovery from a narrow current support; the destination must
                // still have a verified safe-centre landing (with the existing
                // bounded recovery tolerance), clear its leading face, and
                // clear all hazards. A wider verified destination is safer and
                // must not disqualify the urgent handoff.
                bool narrowChainGeometry =
                    scan.Current.Width <= 2.25f &&
                    scan.Gap >= 0.50f;
                bool liveCentreStillOnRawSource =
                    playerPosition.x >= scan.Current.Left - 0.12f &&
                    playerPosition.x <= scan.Current.Right + 0.12f;
                float liveRecoveryLandingX = playerPosition.x + travel;
                bool liveRecoveryLandingSafe =
                    liveRecoveryLandingX >=
                        scan.Next.SafeLeft - LandingRecoveryTolerance &&
                    liveRecoveryLandingX <=
                        scan.Next.SafeRight + LandingRecoveryTolerance;
                bool liveRecoveryClearsTargetFace =
                    TrajectoryClearsRaisedTargetFace(
                        scan.Current,
                        scan.Next,
                        playerPosition.x,
                        speed,
                        travel,
                        hold,
                        flightSeconds,
                        physics,
                        out string recoveryFaceCheck);
                string recoveryHazardCheck = "RecoveryGeometryRejected";
                bool liveRecoveryTrajectorySafe =
                    narrowChainGeometry &&
                    liveCentreStillOnRawSource &&
                    liveRecoveryLandingSafe &&
                    liveRecoveryClearsTargetFace &&
                    TrajectoryClearsHazard(
                        hazard,
                        playerPosition.x,
                        liveRecoveryLandingX,
                        scan.Current.Top,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        out recoveryHazardCheck);
                SpeedEnvelopeEvaluation recoveryEnvelope = default;
                if (liveRecoveryTrajectorySafe &&
                    spiritBoost.RequiresSpeedEnvelope)
                {
                    recoveryEnvelope =
                        EvaluateSpiritBoostTrajectoryEnvelope(
                            scan.Current,
                            scan.Next,
                            GetIntermediateClearanceSurfaces(scan),
                            hazard,
                            spiritBoost,
                            playerPosition.x,
                            speed,
                            hold,
                            flightSeconds,
                            physics,
                            travel,
                            useRawTargetBounds: false);
                    liveRecoveryTrajectorySafe =
                        recoveryEnvelope.IsSafe;
                    recoveryHazardCheck +=
                        $";{recoveryEnvelope.Summary}";
                }
                if (liveRecoveryTrajectorySafe)
                {
                    float recoveryTravel =
                        spiritBoost.RequiresSpeedEnvelope
                            ? recoveryEnvelope.ExpectedTravel
                            : travel;
                    float recoveryMinimumTravel =
                        spiritBoost.RequiresSpeedEnvelope
                            ? recoveryEnvelope.MinimumTravel
                            : travel;
                    float recoveryMaximumTravel =
                        spiritBoost.RequiresSpeedEnvelope
                            ? recoveryEnvelope.MaximumTravel
                            : travel;
                    bool recoveryFutureSpeedTransition =
                        spiritBoost.RequiresConservativeImmediateBoost ||
                        recoveryEnvelope.TriggerHits > 0;
                    liveRecoveryLandingX =
                        playerPosition.x + recoveryTravel;
                    int recoverySphereHits =
                        CountTrajectorySphereHitsAcrossSpeedScenarios(
                        sphereObjectives,
                        playerPosition.x,
                        scan.Current.Top,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        spiritBoost,
                        travel);
                    float recoveryCenter =
                        (scan.Next.SafeLeft + scan.Next.SafeRight) * 0.5f;
                    float recoveryScore =
                        -Mathf.Abs(liveRecoveryLandingX - recoveryCenter) * 2f -
                        Mathf.Abs(hold - preferredHold);
                    AppendEvaluation(
                        evaluations,
                        $"H={hold:F2}:EH={effectiveHold:F2},T={flightSeconds:F3}," +
                        $"D={travel:F2},ImmediateNarrowSourceRecovery," +
                        $"SourceWidth={scan.Current.Width:F2}," +
                        $"TargetWidth={scan.Next.Width:F2}," +
                        $"Policy=NarrowSourceToVerifiedTarget," +
                        $"LiveLaunch={playerPosition.x:F2}," +
                        $"LiveLanding={liveRecoveryLandingX:F2}," +
                        $"SphereHits={recoverySphereHits}," +
                        $"{recoveryFaceCheck},{recoveryHazardCheck}");
                    if (recoveryScore > narrowSourceRecoveryScore)
                    {
                        narrowSourceRecoveryScore = recoveryScore;
                        narrowSourceRecovery = new BonusJumpPlan(
                            true,
                            true,
                            hold,
                            flightSeconds,
                            recoveryTravel,
                            playerPosition.x,
                            liveRecoveryLandingX,
                            playerPosition.x,
                            playerPosition.x,
                            "NarrowSupportImmediateChain",
                            string.Empty,
                            BonusManeuverKind.GroundJumpToLanding,
                            recoverySphereHits,
                            recoveryMinimumTravel,
                            recoveryMaximumTravel,
                            recoveryFutureSpeedTransition);
                    }
                    continue;
                }

                AppendEvaluation(evaluations,
                    $"H={hold:F2}:EH={effectiveHold:F2},T={flightSeconds:F3},D={travel:F2},NoIntersection");
                continue;
            }

            float plannedLaunchX = Mathf.Clamp(
                preferredLandingX - travel,
                usableLeft,
                usableRight);
            float plannedLandingX = plannedLaunchX + travel;

            bool clearsRaisedTargetFace =
                TrajectoryClearsRaisedTargetFace(
                    scan.Current,
                    scan.Next,
                    plannedLaunchX,
                    speed,
                    travel,
                    hold,
                    flightSeconds,
                    physics,
                    out string targetFaceCheck);
            if (!clearsRaisedTargetFace &&
                plannedLaunchX > usableLeft + 0.01f)
            {
                // An earlier point in the same safe landing window reaches the
                // vertical face later in the arc. Prefer that physically valid
                // launch over merely changing the final landing score.
                float earlierLaunchX = usableLeft;
                if (TrajectoryClearsRaisedTargetFace(
                        scan.Current,
                        scan.Next,
                        earlierLaunchX,
                        speed,
                        travel,
                        hold,
                        flightSeconds,
                        physics,
                        out string earlierFaceCheck))
                {
                    plannedLaunchX = earlierLaunchX;
                    plannedLandingX = plannedLaunchX + travel;
                    clearsRaisedTargetFace = true;
                    targetFaceCheck =
                        $"PreferredRejected;Earlier[{earlierFaceCheck}]";
                }
            }
            if (!clearsRaisedTargetFace)
            {
                AppendEvaluation(
                    evaluations,
                    $"H={hold:F2}:RejectedByTargetFace," +
                    targetFaceCheck);
                continue;
            }

            if (!TrajectoryClearsHazard(
                    hazard,
                    plannedLaunchX,
                    plannedLandingX,
                    scan.Current.Top,
                    speed,
                    hold,
                    flightSeconds,
                    physics,
                    out string hazardCheck))
            {
                AppendEvaluation(evaluations,
                    $"H={hold:F2}:RejectedByTrajectory,{hazardCheck}");
                continue;
            }

            BonusBoardSegment[] intermediateSurfaces =
                GetIntermediateClearanceSurfaces(scan);
            if (!TrajectoryClearsIntermediateSurfaces(
                    scan.Current,
                    scan.Next,
                    intermediateSurfaces,
                    plannedLaunchX,
                    plannedLandingX,
                    speed,
                    hold,
                    flightSeconds,
                    physics,
                    out string intermediateCheck))
            {
                AppendEvaluation(
                    evaluations,
                    $"H={hold:F2}:RejectedByIntermediateSurface," +
                    intermediateCheck);
                continue;
            }

            bool hasTierCalibration = physics.TravelProfile.TryGetDuration(
                hold,
                scan.HeightDelta,
                out _,
                out int tierSampleCount);
            string soulLaunchSelection = "SoulLaunchSearch=Inactive";
            bool allowSoulLaunchSearch =
                preferSphereCoverage &&
                !spiritBoost.Enabled &&
                sectionIndex >= 0 && sectionIndex <= 2 &&
                sphereObjectives != null && sphereObjectives.Count > 0 &&
                usableRight - usableLeft >= 0.04f;
            if (allowSoulLaunchSearch)
            {
                HashSet<int> bestLaunchHits =
                    GetTrajectorySphereHitIndicesAcrossSpeedScenarios(
                        sphereObjectives,
                        plannedLaunchX,
                        scan.Current.Top,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        spiritBoost,
                        noPickupTravel);
                BonusJumpPlan bestLaunchProbe = new(
                    true,
                    false,
                    hold,
                    flightSeconds,
                    travel,
                    plannedLaunchX,
                    plannedLaunchX + travel,
                    usableLeft,
                    usableRight,
                    "SoulLaunchProbe",
                    string.Empty,
                    BonusManeuverKind.GroundJumpToLanding,
                    bestLaunchHits.Count,
                    minimumTravel,
                    maximumTravel,
                    false);
                float bestLaunchSafety = GetLandingFirstSafetyMargin(
                    bestLaunchProbe,
                    scan.Next,
                    plannedLaunchX,
                    speed,
                    physics,
                    hasTierCalibration,
                    tierSampleCount);
                float originalLaunchX = plannedLaunchX;
                float baselineEnvelopeLeft = plannedLaunchX +
                    Mathf.Min(minimumTravel, maximumTravel);
                float baselineEnvelopeRight = plannedLaunchX +
                    Mathf.Max(minimumTravel, maximumTravel);
                float baselineRawMargin = Mathf.Min(
                    baselineEnvelopeLeft - scan.Next.SafeLeft,
                    scan.Next.SafeRight - baselineEnvelopeRight);
                float modeledUncertainty = Mathf.Max(
                    0f,
                    baselineRawMargin - bestLaunchSafety);
                float earliestComfortableLaunch =
                    scan.Next.SafeLeft +
                    modeledUncertainty +
                    ComfortableSoulLandingMargin -
                    Mathf.Min(minimumTravel, maximumTravel);
                float earliestSoulProbeLaunchX = Mathf.Clamp(
                    Mathf.Max(
                        usableLeft,
                        earliestComfortableLaunch,
                        playerPosition.x - TriggerTolerance),
                    usableLeft,
                    Mathf.Min(usableRight, plannedLaunchX));
                float midpointSoulProbeLaunchX =
                    (earliestSoulProbeLaunchX + originalLaunchX) * 0.50f;
                int evaluatedCandidateCount = 0;
                int acceptedCandidateCount = 0;
                StringBuilder analyticCandidateEvidence = new();
                // Baseline plus these two analytically placed alternatives is
                // a strict maximum of three trajectories per hold. Evaluate
                // the midpoint first, then the earliest comfortable point, so
                // equal positive coverage naturally converges leftward.
                for (int analyticIndex = 0; analyticIndex < 2; analyticIndex++)
                {
                    float soulProbeLaunchX = analyticIndex == 0
                        ? midpointSoulProbeLaunchX
                        : earliestSoulProbeLaunchX;
                    bool duplicateCandidate =
                        analyticIndex == 1 &&
                        Mathf.Abs(
                            earliestSoulProbeLaunchX -
                            midpointSoulProbeLaunchX) <= 0.01f;
                    bool candidateEvaluated =
                        !duplicateCandidate &&
                        soulProbeLaunchX < originalLaunchX - 0.01f;
                    bool candidateTrajectorySafe = false;
                    bool candidateAccepted = false;
                    int candidateHitCount =
                        bestLaunchProbe.ExpectedSphereHits;
                    float candidateSafety = bestLaunchSafety;
                    if (!candidateEvaluated)
                    {
                        analyticCandidateEvidence.Append(
                            $"P{analyticIndex}[X={soulProbeLaunchX:F3}," +
                            $"Evaluated=False];");
                        continue;
                    }

                    evaluatedCandidateCount++;
                    float candidateLandingX = soulProbeLaunchX + travel;
                    string candidateFaceCheck = "NotEvaluated";
                    string candidateHazardCheck = "NotEvaluated";
                    string candidateIntermediateCheck = "NotEvaluated";
                    candidateTrajectorySafe =
                        TrajectoryClearsRaisedTargetFace(
                            scan.Current,
                            scan.Next,
                            soulProbeLaunchX,
                            speed,
                            travel,
                            hold,
                            flightSeconds,
                            physics,
                            out candidateFaceCheck) &&
                        TrajectoryClearsHazard(
                            hazard,
                            soulProbeLaunchX,
                            candidateLandingX,
                            scan.Current.Top,
                            speed,
                            hold,
                            flightSeconds,
                            physics,
                            out candidateHazardCheck) &&
                        TrajectoryClearsIntermediateSurfaces(
                            scan.Current,
                            scan.Next,
                            intermediateSurfaces,
                            soulProbeLaunchX,
                            candidateLandingX,
                            speed,
                            hold,
                            flightSeconds,
                            physics,
                            out candidateIntermediateCheck);
                    if (candidateTrajectorySafe)
                    {
                        HashSet<int> candidateHits =
                            GetTrajectorySphereHitIndicesAcrossSpeedScenarios(
                                sphereObjectives,
                                soulProbeLaunchX,
                                scan.Current.Top,
                                speed,
                                hold,
                                flightSeconds,
                                physics,
                                spiritBoost,
                                noPickupTravel);
                        candidateHitCount = candidateHits.Count;
                        BonusJumpPlan candidateProbe = bestLaunchProbe with
                        {
                            PlannedLaunchX = soulProbeLaunchX,
                            PredictedLandingX = candidateLandingX,
                            ExpectedSphereHits = candidateHits.Count
                        };
                        candidateSafety = GetLandingFirstSafetyMargin(
                            candidateProbe,
                            scan.Next,
                            soulProbeLaunchX,
                            speed,
                            physics,
                            hasTierCalibration,
                            tierSampleCount);
                        bool candidateComfortable =
                            candidateSafety >=
                                ComfortableSoulLandingMargin - 0.001f;
                        bool incumbentComfortable =
                            bestLaunchSafety >= ComfortableSoulLandingMargin;
                        candidateAccepted =
                            candidateComfortable &&
                            incumbentComfortable &&
                            (candidateHits.Count > bestLaunchHits.Count ||
                             candidateHits.Count == bestLaunchHits.Count &&
                             candidateHits.Count > 0 &&
                             soulProbeLaunchX < plannedLaunchX - 0.01f);
                        if (candidateAccepted)
                        {
                            acceptedCandidateCount++;
                            plannedLaunchX = soulProbeLaunchX;
                            plannedLandingX = candidateLandingX;
                            bestLaunchHits = candidateHits;
                            bestLaunchSafety = candidateSafety;
                            targetFaceCheck = candidateFaceCheck;
                            hazardCheck = candidateHazardCheck;
                            intermediateCheck = candidateIntermediateCheck;
                        }
                    }

                    analyticCandidateEvidence.Append(
                        $"P{analyticIndex}[X={soulProbeLaunchX:F3}," +
                        $"Safe={candidateTrajectorySafe},Hits=" +
                        $"{candidateHitCount},Margin={candidateSafety:F3}," +
                        $"Accepted={candidateAccepted}];");
                }

                soulLaunchSelection =
                    $"SoulLaunchAnalytic[Original={originalLaunchX:F3}," +
                    $"Earliest={earliestSoulProbeLaunchX:F3},Midpoint=" +
                    $"{midpointSoulProbeLaunchX:F3},Selected=" +
                    $"{plannedLaunchX:F3},BaseHits=" +
                    $"{bestLaunchProbe.ExpectedSphereHits}," +
                    $"Evaluated={evaluatedCandidateCount}/2," +
                    $"Accepted={acceptedCandidateCount},Probes=" +
                    $"{analyticCandidateEvidence}]";
            }

            SpeedEnvelopeEvaluation plannedSpeedEnvelope = default;
            if (spiritBoost.RequiresSpeedEnvelope)
            {
                if (!TryFindSpiritBoostRobustLaunch(
                        scan,
                        hazard,
                        spiritBoost,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        travel,
                        usableLeft,
                        usableRight,
                        plannedLaunchX,
                        sphereObjectives,
                        preferSphereCoverage,
                        preferEarlySpiritPickupCoverage,
                        allowStage1Section1VerifiedBoostMargin,
                        hasTierCalibration,
                        tierSampleCount,
                        out float robustLaunchX,
                        out float robustWindowLeft,
                        out float robustWindowRight,
                        out plannedSpeedEnvelope))
                {
                    AppendEvaluation(
                        evaluations,
                        $"H={hold:F2}:RejectedBySpeedEnvelope," +
                        plannedSpeedEnvelope.Summary);
                    continue;
                }

                plannedLaunchX = robustLaunchX;
                usableLeft = robustWindowLeft;
                usableRight = robustWindowRight;
                travel = plannedSpeedEnvelope.ExpectedTravel;
                minimumTravel = plannedSpeedEnvelope.MinimumTravel;
                maximumTravel = plannedSpeedEnvelope.MaximumTravel;
                plannedLandingX = plannedLaunchX + travel;
                futureSpeedTransitionExpected =
                    spiritBoost.RequiresConservativeImmediateBoost ||
                    plannedSpeedEnvelope.TriggerHits > 0;
                speedEnvelopeCheck = plannedSpeedEnvelope.Summary;
            }
            else if (spiritBoost.Enabled)
            {
                speedEnvelopeCheck =
                    $"SpiritEnvelope=Unavailable[{spiritBoost.Summary}]";
            }
            bool beyondIdealWindow =
                playerPosition.x >
                    usableRight + actionTriggerTolerance;
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
            bool speedEnvelopeActive =
                spiritBoost.RequiresSpeedEnvelope;
            float fixedStepInputTravel = fixedStepCommitTravel;
            SpeedEnvelopeEvaluation liveSafeEnvelope = default;
            float liveTravel = travel + fixedStepInputTravel;
            float liveMinimumTravel =
                minimumTravel + fixedStepInputTravel;
            float liveMaximumTravel =
                maximumTravel + fixedStepInputTravel;
            bool liveLaunchClearsTargetFace;
            bool liveTrajectoryInsideSafeTarget;
            bool lateTrajectorySafe;
            string liveTargetFaceCheck;
            if (speedEnvelopeActive)
            {
                // A trigger is fixed in world space; the plan proved one
                // launch X, not a translation-invariant distance. Re-run the
                // complete slow/fast envelope at the actual body position on
                // the frame that may send DOWN.
                liveSafeEnvelope =
                    EvaluateSpiritBoostTrajectoryEnvelope(
                        scan.Current,
                        scan.Next,
                        intermediateSurfaces,
                        hazard,
                        spiritBoost,
                        playerPosition.x,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        noPickupTravel + fixedStepInputTravel,
                        useRawTargetBounds: false,
                        sphereObjectives);
                liveTravel = liveSafeEnvelope.ExpectedTravel;
                liveMinimumTravel = liveSafeEnvelope.MinimumTravel;
                liveMaximumTravel = liveSafeEnvelope.MaximumTravel;
                liveTrajectoryInsideSafeTarget =
                    liveSafeEnvelope.IsSafe;
                liveLaunchClearsTargetFace =
                    liveSafeEnvelope.IsSafe;
                lateTrajectorySafe = false;
                liveTargetFaceCheck = liveSafeEnvelope.Summary;
            }
            else
            {
                // A late action is useful only while its complete body-centre
                // envelope still lands inside the verified safe corridor. The
                // old raw-collider escape hatch could authorize a prediction
                // beyond SafeRight; the retained traces then landed still
                // farther right and fell before the next replanning tick.
                float lateLandingX =
                    playerPosition.x + liveTravel;
                float lateLandingRawMargin = Mathf.Min(
                    lateLandingX - scan.Next.SafeLeft,
                    scan.Next.SafeRight - lateLandingX);
                float lateExecutionUncertainty =
                    speed * Mathf.Clamp(
                        physics.FixedDeltaTime,
                        0.005f,
                        0.05f) * 0.35f +
                    speed * Mathf.Max(0f, flightSeconds) * 0.04f;
                bool lateLandingInsideSafeTarget =
                    lateLandingRawMargin >= lateExecutionUncertainty;
                liveLaunchClearsTargetFace =
                    TrajectoryClearsRaisedTargetFace(
                        scan.Current,
                        scan.Next,
                        playerPosition.x,
                        speed,
                        liveTravel,
                        hold,
                        flightSeconds,
                        physics,
                        out liveTargetFaceCheck);
                liveTargetFaceCheck +=
                    $";LateLandingMargin={lateLandingRawMargin:F3}/" +
                    $"{lateExecutionUncertainty:F3}";
                lateTrajectorySafe = stillOnVerifiedSource &&
                    lateLandingInsideSafeTarget &&
                    liveLaunchClearsTargetFace &&
                    TrajectoryClearsHazard(
                        hazard,
                        playerPosition.x,
                        playerPosition.x + liveTravel,
                        scan.Current.Top,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        out _);
                liveTrajectoryInsideSafeTarget =
                    playerPosition.x + liveTravel >=
                        scan.Next.SafeLeft - LandingRecoveryTolerance &&
                    playerPosition.x + liveTravel <=
                        scan.Next.SafeRight + LandingRecoveryTolerance;
            }
            // A speed-trigger route may cross the singleton robust launch
            // point between two planning ticks. Do not fall back to a wall
            // face merely because that cached point is now behind us. Permit
            // at most one physics-step of late recovery, and only after the
            // complete exact-live no-pickup/pickup envelope proves that both
            // outcomes still land inside the same verified safe corridor.
            float speedEnvelopeLateOvershoot =
                Mathf.Max(0f, playerPosition.x - usableRight);
            float speedEnvelopeLateAllowance = Mathf.Max(
                actionTriggerTolerance,
                speed * Mathf.Clamp(
                    physics.FixedDeltaTime,
                    0.005f,
                    0.05f) + 0.05f);
            bool speedEnvelopeLateSafeLanding =
                speedEnvelopeActive &&
                beyondIdealWindow &&
                stillOnVerifiedSource &&
                speedEnvelopeLateOvershoot <=
                    speedEnvelopeLateAllowance &&
                liveSafeEnvelope.IsSafe;
            if (speedEnvelopeActive)
            {
                liveTargetFaceCheck +=
                    $";LateSpeedEnvelope={speedEnvelopeLateOvershoot:F3}/" +
                    $"{speedEnvelopeLateAllowance:F3}," +
                    $"Authorized={speedEnvelopeLateSafeLanding}";
            }

            // A late single-speed action still uses its independent
            // uncertainty-qualified proof. It must never stand in for the
            // dual-outcome check above when future acceleration is possible.
            bool emergencyLateSafeLanding =
                !speedEnvelopeActive &&
                beyondIdealWindow && lateTrajectorySafe;
            // A one-unit support can be consumed by one native physics step.
            // Its live centre may therefore be a few hundredths beyond the
            // ordinary trigger tolerance while the body is still physically
            // supported. The strict uncertainty-qualified emergency above is
            // appropriate for ordinary platforms, but waiting on this topology
            // is guaranteed to fall. Reuse the same bounded raw-support and
            // safe-target recovery contract as NarrowSupportImmediateChain.
            bool narrowSourceLateRecovery =
                !speedEnvelopeActive &&
                beyondIdealWindow &&
                scan.Current.Width <= 2.25f &&
                stillOnVerifiedSource &&
                liveTrajectoryInsideSafeTarget &&
                liveLaunchClearsTargetFace &&
                TrajectoryClearsHazard(
                    hazard,
                    playerPosition.x,
                    playerPosition.x + liveTravel,
                    scan.Current.Top,
                    speed,
                    hold,
                    flightSeconds,
                    physics,
                    out _);
            bool missed =
                beyondIdealWindow &&
                !speedEnvelopeLateSafeLanding &&
                !emergencyLateSafeLanding &&
                !narrowSourceLateRecovery;
            int predictedSphereHits = speedEnvelopeActive
                ? plannedSpeedEnvelope.GuaranteedSphereHits
                : CountTrajectorySphereHitsAcrossSpeedScenarios(
                    sphereObjectives,
                    plannedLaunchX,
                    scan.Current.Top,
                    speed,
                    hold,
                    flightSeconds,
                    physics,
                    spiritBoost,
                    noPickupTravel);
            int predictedSpeedBoostHits = speedEnvelopeActive
                ? plannedSpeedEnvelope.TriggerHits
                : 0;
            string status = speedEnvelopeLateSafeLanding
                ? "SpeedEnvelopeLateSafeLanding"
                : emergencyLateSafeLanding
                ? "EmergencyLateSafeLanding"
                : narrowSourceLateRecovery
                ? "NarrowSourceLateRecovery"
                : missed
                ? "Missed"
                : playerPosition.x >=
                    plannedLaunchX - actionTriggerTolerance
                    ? "Jump"
                    : "Wait";

            AppendEvaluation(evaluations,
                $"H={hold:F2}:EH={effectiveHold:F2},T={flightSeconds:F3},D={travel:F2}," +
                $"TravelSource={travelSource}," +
                $"W=[{usableLeft:F2},{usableRight:F2}],L={plannedLaunchX:F2}," +
                $"P={plannedLandingX:F2},SphereHits={predictedSphereHits}," +
                $"BoostHits={predictedSpeedBoostHits}," +
                $"{status},{targetFaceCheck},{hazardCheck}," +
                $"{intermediateCheck},{speedEnvelopeCheck}," +
                $"{soulLaunchSelection}");

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
                uncertaintyPenalty +
                // Stage 1/2 do not yet have an authored route. Among
                // trajectories that already satisfy the same landing and
                // hazard contracts, prefer the one that intersects the live
                // soul row. Stage 3 passes false and therefore keeps its
                // mature candidate ordering unchanged.
                (preferSphereCoverage ? predictedSphereHits * 25f : 0f);

            bool predictedInsideSafeTarget =
                liveTrajectoryInsideSafeTarget;
            bool lateLandingRecovery =
                playerPosition.x > usableRight &&
                playerPosition.x <= usableRight + TriggerTolerance;
            bool actualTrajectorySafe = speedEnvelopeActive
                ? liveSafeEnvelope.IsSafe
                : TrajectoryClearsHazard(
                    hazard,
                    playerPosition.x,
                    playerPosition.x + liveTravel,
                    scan.Current.Top,
                    speed,
                    hold,
                    flightSeconds,
                    physics,
                    out _);
            bool shouldJump =
                speedEnvelopeLateSafeLanding ||
                emergencyLateSafeLanding ||
                narrowSourceLateRecovery ||
                (playerPosition.x >=
                     plannedLaunchX - actionTriggerTolerance &&
                 playerPosition.x <=
                     usableRight + actionTriggerTolerance &&
                 predictedInsideSafeTarget &&
                 liveLaunchClearsTargetFace &&
                 actualTrajectorySafe);
            float predictedLandingX = shouldJump
                ? playerPosition.x + liveTravel
                : plannedLandingX;
            int committedSphereHits = shouldJump && speedEnvelopeActive
                ? liveSafeEnvelope.GuaranteedSphereHits
                : shouldJump
                    ? CountTrajectorySphereHitsAcrossSpeedScenarios(
                    sphereObjectives,
                    playerPosition.x,
                    scan.Current.Top,
                    speed,
                    hold,
                    flightSeconds,
                    physics,
                    spiritBoost,
                    noPickupTravel + fixedStepInputTravel)
                    : predictedSphereHits;
            int committedSpeedBoostHits = shouldJump && speedEnvelopeActive
                ? liveSafeEnvelope.TriggerHits
                : predictedSpeedBoostHits;
            BonusJumpPlan candidate = new(
                true,
                shouldJump,
                hold,
                flightSeconds,
                shouldJump ? liveTravel : travel,
                plannedLaunchX,
                predictedLandingX,
                usableLeft,
                usableRight,
                shouldJump
                    ? speedEnvelopeLateSafeLanding
                        ? "SpeedEnvelopeLateSafeLanding"
                        : emergencyLateSafeLanding
                        ? "EmergencyLateSafeLanding"
                        : narrowSourceLateRecovery
                        ? "NarrowSourceLateRecovery"
                        : lateLandingRecovery
                        ? "LateLandingRecovery"
                        : "InsideLaunchWindow"
                    : "ApproachingLaunchWindow",
                $"CommittedTargetFace={liveTargetFaceCheck};" +
                $"FixedStepInputCommitTravel=" +
                $"{fixedStepInputTravel:F3};TriggerTolerance=" +
                $"{actionTriggerTolerance:F3}",
                BonusManeuverKind.GroundJumpToLanding,
                committedSphereHits,
                shouldJump ? liveMinimumTravel : minimumTravel,
                shouldJump ? liveMaximumTravel : maximumTravel,
                futureSpeedTransitionExpected,
                committedSpeedBoostHits);

            float candidateLaunchX = shouldJump
                ? playerPosition.x
                : plannedLaunchX;
            float candidateLandingSafety =
                GetLandingFirstSafetyMargin(
                    candidate,
                    scan.Next,
                    candidateLaunchX,
                    speed,
                    physics,
                    hasTierCalibration,
                    tierSampleCount);
            AppendEvaluation(
                evaluations,
                $"LandingFirst[H={hold:F2},Margin=" +
                $"{candidateLandingSafety:F3},RawEnvelope=" +
                $"{DescribeLandingEnvelope(candidate, scan.Next, candidateLaunchX)}]");

            // A small number of authored collection fixtures are explicit
            // route obligations rather than optional score. Keep that policy
            // outside the generic planner, but let the caller require a
            // minimum from the same live trajectory proof. Unsafe candidates
            // are never rescued by this constraint; a candidate that misses
            // the requested live objectives simply cannot win this plan.
            if (minimumSphereHits > 0 &&
                committedSphereHits < minimumSphereHits)
            {
                AppendEvaluation(
                    evaluations,
                    $"RequiredSphereMinimumRejected[Required=" +
                    $"{minimumSphereHits},Predicted=" +
                    $"{committedSphereHits},H={hold:F2}]");
                continue;
            }

            // Safety is a constraint before pickup utility. For the same
            // support, first reject unsafe or marginal alternatives; after
            // both holds retain a comfortable post-uncertainty reserve, use
            // actual predicted soul intersections before spending surplus
            // margin that has no survival value. This changes jump timing,
            // never the selected support topology.
            bool replaceBest;
            if (!best.IsValid)
            {
                replaceBest = true;
            }
            else
            {
                bool candidateSafe = candidateLandingSafety >= 0f;
                bool bestSafe = bestLandingSafety >= 0f;
                bool candidateComfortable =
                    candidateLandingSafety >= ComfortableSoulLandingMargin;
                bool bestComfortable =
                    bestLandingSafety >= ComfortableSoulLandingMargin;
                bool candidateVerifiedBoostObjective =
                    allowStage1Section1VerifiedBoostMargin &&
                    committedSpeedBoostHits > 0 &&
                    committedSphereHits >= best.ExpectedSphereHits &&
                    candidateLandingSafety + 0.001f >=
                        RouteLandingSafetyTier;
                bool bestVerifiedBoostObjective =
                    allowStage1Section1VerifiedBoostMargin &&
                    best.ExpectedSpeedBoostHits > 0 &&
                    best.ExpectedSphereHits >= committedSphereHits &&
                    bestLandingSafety + 0.001f >=
                        RouteLandingSafetyTier;
                if (candidateSafe != bestSafe)
                {
                    replaceBest = candidateSafe;
                }
                else if (!candidateSafe)
                {
                    replaceBest =
                        candidateLandingSafety > bestLandingSafety + 0.001f;
                }
                else if (candidateVerifiedBoostObjective !=
                         bestVerifiedBoostObjective)
                {
                    // Both the no-reset and reset trajectories already pass
                    // the exact envelope proof. In the one repeated Stage-1
                    // Section-1 arc, retain equal sphere coverage and collect
                    // the typed boost once the post-uncertainty reserve still
                    // reaches the normal route safety tier.
                    replaceBest = candidateVerifiedBoostObjective;
                }
                else if (candidateComfortable != bestComfortable)
                {
                    replaceBest = candidateComfortable;
                }
                else if (!candidateComfortable)
                {
                    replaceBest =
                        candidateLandingSafety > bestLandingSafety + 0.001f ||
                        Mathf.Abs(
                            candidateLandingSafety - bestLandingSafety) <=
                            0.001f && score > bestScore;
                }
                else if (preferEarlySpiritPickupCoverage &&
                         committedSpeedBoostHits !=
                            best.ExpectedSpeedBoostHits)
                {
                    // Spirit Boost is a route objective, not merely a source
                    // of travel uncertainty. Once both commands preserve the
                    // complete comfortable landing reserve, prefer the one
                    // that actually intersects more verified native boost
                    // triggers. The already-computed speed envelope proves
                    // that both the reset and no-reset landings remain safe.
                    replaceBest =
                        committedSpeedBoostHits >
                        best.ExpectedSpeedBoostHits;
                }
                else if (preferSphereCoverage &&
                         committedSphereHits != best.ExpectedSphereHits)
                {
                    replaceBest =
                        committedSphereHits > best.ExpectedSphereHits;
                }
                else
                {
                    bool materiallySafer =
                        candidateLandingSafety >
                        bestLandingSafety + RouteLandingSafetyTier;
                    bool sameSafetyTier =
                        Mathf.Abs(
                            candidateLandingSafety - bestLandingSafety) <=
                        RouteLandingSafetyTier;
                    replaceBest =
                        materiallySafer || sameSafetyTier && score > bestScore;
                }
            }
            AppendEvaluation(
                evaluations,
                $"SoulHoldSelection[H={hold:F2},Safe=" +
                $"{candidateLandingSafety >= 0f},Comfortable=" +
                $"{candidateLandingSafety >= ComfortableSoulLandingMargin}," +
                $"SoulHits={committedSphereHits},BoostHits=" +
                $"{committedSpeedBoostHits},Replace={replaceBest}]");
            if (replaceBest)
            {
                bestLandingSafety = candidateLandingSafety;
                bestScore = score;
                best = candidate;
            }
        }

        string evaluationSummary = evaluations.ToString();
        if (best.IsValid)
        {
            if (walkableMicroGap &&
                !walkableGapRequiresAirborneCollection)
            {
                return Invalid(
                    "WalkableMicroGap",
                    $"Gap={scan.Gap:F3},Limit={walkableGapLimit:F3}," +
                    $"InferredBodyWidth={inferredBodyWidth:F3}," +
                    $"DeltaY={scan.HeightDelta:F3},HazardClear=True," +
                    $"PredictedJumpHits={best.ExpectedSphereHits}," +
                    $"PassiveWalkObjectives=" +
                    $"{walkablePassiveSphereObjectives}," +
                    $"ElevatedObjectives=" +
                    $"{walkableElevatedSphereObjectives}. " +
                    "The body bridges the seam and every nearby objective " +
                    "is inside the grounded pickup envelope; jump scoring " +
                    "cannot override the continuous road. | " +
                    evaluationSummary);
            }
            return best with { CandidateSummary = evaluationSummary };
        }
        if (narrowSourceRecovery.IsValid)
        {
            return narrowSourceRecovery with
            {
                CandidateSummary = evaluationSummary
            };
        }

        if (useStage2LiveTopologyProfile &&
            TryPlanStage2LowCorridorWallCatch(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                obstacle,
                actionTriggerTolerance,
                evaluationSummary,
                out BonusJumpPlan lowCorridorWallCatch))
        {
            return lowCorridorWallCatch;
        }

        if (useStage2LiveTopologyProfile &&
            TryPlanStage2NarrowSourceWallDrop(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                obstacle,
                evaluationSummary,
                out BonusJumpPlan narrowSourceWallDrop))
        {
            return narrowSourceWallDrop;
        }

        if (stage2Section1Domain &&
            TryPlanStage2UnmappedWallIntercept(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                obstacle,
                actionTriggerTolerance,
                evaluationSummary,
                out BonusJumpPlan unmappedWallIntercept))
        {
            return unmappedWallIntercept;
        }

        if (walkableMicroGap)
        {
            return Invalid(
                "WalkableMicroGap",
                $"Gap={scan.Gap:F3},Limit={walkableGapLimit:F3}," +
                $"InferredBodyWidth={inferredBodyWidth:F3}," +
                $"DeltaY={scan.HeightDelta:F3},HazardClear=True," +
                $"PassiveWalkObjectives=" +
                $"{walkablePassiveSphereObjectives}," +
                $"ElevatedObjectives=" +
                $"{walkableElevatedSphereObjectives}. " +
                "The body physically bridges this level seam.");
        }

        // A short raised top can be impossible to land on at late-section
        // speed even though its vertical face is safely reachable. Waiting is
        // then guaranteed to walk off the source. Live geometry has no
        // authored route contract to classify this topology for us, so turn
        // the verified face trajectory into the correction route already
        // understood by the wall controller. Stage 3 authored routing and
        // the mature first two live sections retain their existing behavior.
        if (allowRecoverableLowerFaceCatch &&
            obstacle.Kind == BonusObstacleKind.RaisedLanding)
        {
            BonusJumpPlan faceFallback = PlanWallApproach(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                evaluationSummary +
                    " | LiveRaisedLandingFaceFallback",
                spiritBoost,
                preferLowerTrenchContact: false,
                requireBelowSourceContact: false,
                triggerTolerance: actionTriggerTolerance,
                compensateInputCommit: stage2Section1Domain);
            if (faceFallback.IsValid)
            {
                return faceFallback with
                {
                    Reason = "LiveRaisedLandingFaceFallback"
                };
            }
            evaluationSummary +=
                $" | LiveRaisedLandingFaceRejected[" +
                $"{faceFallback.Reason}:{faceFallback.CandidateSummary}]";
        }

        return Invalid(
            obstacle.Kind == BonusObstacleKind.RaisedLanding
                ? "RaisedLandingHasNoSafeDirectJump"
                : "NoVerifiedLaunchWindow",
            evaluationSummary);
    }

    private static bool TryPlanStage2LowCorridorWallCatch(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float speed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        BonusObstacleAssessment obstacle,
        float actionTriggerTolerance,
        string directEvaluation,
        out BonusJumpPlan plan)
    {
        plan = default;
        BonusBoardSegment raised = scan.Intermediate;
        bool verifiedLowCorridor =
            scan.IsValid &&
            scan.HasNext &&
            !scan.HasIntermediate &&
            scan.Reason.StartsWith(
                "Stage2VerifiedLowCorridor:",
                System.StringComparison.Ordinal) &&
            raised.Width >= 0.75f &&
            raised.Top - scan.Current.Top >= 5.35f &&
            raised.Left >= scan.Current.Right + 0.75f &&
            raised.Left - scan.Current.Right <= 4.50f &&
            scan.Next.Left >= raised.Right - 0.20f &&
            scan.Next.Left <= raised.Right + 0.35f &&
            Mathf.Abs(scan.Next.Top - scan.Current.Top) <= 0.35f &&
            // The repeated module exposes either the three/four-unit
            // intermediate support or the five-unit module entry road. The
            // wider source is needed when a completion-speed reset makes the
            // one-unit landing unsafe before the boost is physically applied.
            scan.Current.Width <= 6.25f &&
            scan.Next.Width >= 3.00f &&
            obstacle.Kind == BonusObstacleKind.OrdinaryGap;
        if (!verifiedLowCorridor)
            return false;

        // The raised composite is deliberately excluded from ordinary
        // landing clearance because its five live side probes did not expose
        // a stable mapped face. It is still authoritative collision evidence:
        // the repeated Stage-2 module physically stops the runner between the
        // narrow launch support and the verified low continuation. If every
        // direct low landing has already failed, enter that measured corridor
        // with a maximum native jump and let exact wall contact own the climb.
        // This is not a blind wall jump: the action requires the complete
        // raised/low/short-source topology and is armed against the retained
        // raised bounds.
        float inferredHalfWidth = Mathf.Clamp(
            scan.Current.SafeLeft - scan.Current.Left - 0.15f,
            0.15f,
            1.25f);
        float latestPhysicalCenter =
            scan.Current.Right - inferredHalfWidth + 0.08f;
        if (playerPosition.x > latestPhysicalCenter + TriggerTolerance)
            return false;

        float fixedDelta = Mathf.Clamp(
            physics.FixedDeltaTime,
            0.005f,
            0.05f);
        float maximumHold = Mathf.Min(
            MaximumHoldSeconds,
            physics.EffectiveHoldCapSeconds);
        int holdSteps = Mathf.FloorToInt(
            (maximumHold + 0.0001f) / fixedDelta);
        if (holdSteps < 1)
            return false;

        float hold = Mathf.Min(
            MaximumHoldSeconds,
            holdSteps * fixedDelta);
        float launchWindowRight = scan.Current.SafeRight;
        float launchWindowLeft = Mathf.Max(
            scan.Current.SafeLeft,
            launchWindowRight -
                Mathf.Max(0.45f, speed * fixedDelta * 2.25f));
        float plannedLaunchX = Mathf.Clamp(
            launchWindowRight - 0.04f,
            launchWindowLeft,
            launchWindowRight);
        float predictedContactCenterX =
            raised.Left - inferredHalfWidth;
        float contactTravel = Mathf.Max(
            0.05f,
            predictedContactCenterX - plannedLaunchX);
        float contactSeconds =
            contactTravel / Mathf.Max(1f, speed);
        if (contactSeconds > 0.45f)
            return false;

        if (!TrajectoryClearsHazard(
                hazard,
                plannedLaunchX,
                predictedContactCenterX,
                scan.Current.Top,
                speed,
                hold,
                Mathf.Max(contactSeconds, fixedDelta),
                physics,
                out string hazardCheck))
        {
            return false;
        }

        bool shouldJumpNow =
            playerPosition.x >=
                launchWindowLeft - actionTriggerTolerance;
        string reason = shouldJumpNow
            ? "Stage2LowCorridorWallCatch"
            : "ApproachingStage2LowCorridorWallCatch";
        plan = new BonusJumpPlan(
            true,
            shouldJumpNow,
            hold,
            Mathf.Clamp(contactSeconds + 0.20f, 0.30f, 0.65f),
            contactTravel,
            plannedLaunchX,
            predictedContactCenterX,
            launchWindowLeft,
            launchWindowRight,
            reason,
            $"{directEvaluation} | Stage2LowCorridorWallCatch[" +
            $"Source=[{scan.Current.Left:F3},{scan.Current.Right:F3}]" +
            $"@{scan.Current.Top:F3},Raised=" +
            $"[{raised.Left:F3},{raised.Right:F3}]@{raised.Top:F3}," +
            $"Low=[{scan.Next.Left:F3},{scan.Next.Right:F3}]" +
            $"@{scan.Next.Top:F3},HalfWidth={inferredHalfWidth:F3}," +
            $"ContactCenterX={predictedContactCenterX:F3}," +
            $"ContactTravel={contactTravel:F3},ContactT=" +
            $"{contactSeconds:F3},MaxNativeHold={hold:F3}," +
            $"FixedStepTriggerTolerance=" +
            $"{actionTriggerTolerance:F3}," +
            $"Hazard={hazardCheck}]. The low landing is unreachable from " +
            "the remaining narrow support; a measured raised-collider " +
            "contact transfers ownership to the existing wall executor.",
            BonusManeuverKind.ApproachJumpThenWallJump,
            0,
            contactTravel,
            contactTravel,
            false);
        return true;
    }

    private static bool TryPlanStage2NarrowSourceWallDrop(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float speed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        BonusObstacleAssessment obstacle,
        string directEvaluation,
        out BonusJumpPlan plan)
    {
        plan = default;
        if (!scan.IsValid ||
            !scan.HasNext ||
            scan.HasIntermediate ||
            obstacle.Kind != BonusObstacleKind.OrdinaryGap ||
            scan.Current.Width > 2.25f ||
            scan.Next.Width < 2.50f ||
            scan.Gap < 1.50f ||
            scan.Gap > 6.50f ||
            Mathf.Abs(scan.HeightDelta) > 0.35f)
        {
            return false;
        }

        // At high completion/Spirit speed, even the minimum native jump can
        // overshoot a nearby same-height platform. Waiting without route
        // ownership is also fatal: the body drops into that platform's left
        // face. Reuse the measured passive wall-entry solver only after all
        // direct landing holds failed. The next input remains gated by exact
        // physical contact, so this cannot manufacture a jump from a visual
        // gap alone.
        BonusJumpPlan wallDrop = PlanWallDropApproach(
            scan,
            playerPosition,
            speed,
            physics,
            hazard,
            $"{directEvaluation} | Stage2NarrowSourceWallDropFallback");
        if (!wallDrop.IsValid)
            return false;

        plan = wallDrop with
        {
            Reason = "Stage2NarrowSourceWallDrop",
            CandidateSummary =
                wallDrop.CandidateSummary +
                " | Direct minimum jump overshoots the nearby verified " +
                "support; passive descent retains its measured left face " +
                "for contact-confirmed climb recovery."
        };
        return true;
    }

    private static bool TryPlanStage2UnmappedWallIntercept(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float speed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        BonusObstacleAssessment obstacle,
        float actionTriggerTolerance,
        string directEvaluation,
        out BonusJumpPlan plan)
    {
        plan = default;
        float remainingSourceRunway =
            scan.Current.Right - playerPosition.x;
        bool boundedWallEntryRunway =
            scan.Current.Width <= 6.25f ||
            remainingSourceRunway >= -0.20f &&
            remainingSourceRunway <= 6.25f;
        if (!scan.IsValid ||
            !scan.HasNext ||
            obstacle.Kind != BonusObstacleKind.OrdinaryGap ||
            Mathf.Abs(scan.HeightDelta) > 0.35f ||
            !boundedWallEntryRunway ||
            scan.Next.Width < 3.0f ||
            scan.HasIntermediate)
        {
            return false;
        }

        // Stage 2 can expose the landing beyond a stepped wall while the
        // live/static support graph has no climbable top for the intervening
        // composite collider. A direct landing proof must still fail first.
        // Only then may a maximum native jump enter the gap and hand control
        // to physical zero-VX wall contacts. The later climb presses are
        // authorized and executed by the runtime, not predicted from a
        // fabricated wall coordinate.
        float fixedDelta = Mathf.Clamp(
            physics.FixedDeltaTime,
            0.005f,
            0.05f);
        float maximumHold = Mathf.Min(
            MaximumHoldSeconds,
            physics.EffectiveHoldCapSeconds);
        int holdSteps = Mathf.FloorToInt(
            (maximumHold + 0.0001f) / fixedDelta);
        if (holdSteps < 1)
            return false;

        float hold = Mathf.Min(
            MaximumHoldSeconds,
            holdSteps * fixedDelta);
        if (!TryPredictFlightTime(
                hold,
                0f,
                physics,
                out float flightSeconds,
                out float effectiveHold,
                out _))
        {
            return false;
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

        bool landingIsBeyondBallisticReach =
            scan.Gap >= travel + 2.0f &&
            scan.Gap <= 30.0f;
        if (!landingIsBeyondBallisticReach)
            return false;

        float launchWindowRight = scan.Current.SafeRight;
        float launchWindowLeft = Mathf.Max(
            scan.Current.SafeLeft,
            launchWindowRight -
                Mathf.Max(0.45f, speed * fixedDelta * 2.25f));
        float plannedLaunchX = Mathf.Clamp(
            launchWindowRight - 0.05f,
            launchWindowLeft,
            launchWindowRight);
        float predictedEntryX = plannedLaunchX + travel;
        if (!TrajectoryClearsHazard(
                hazard,
                plannedLaunchX,
                predictedEntryX,
                scan.Current.Top,
                speed,
                hold,
                flightSeconds,
                physics,
                out string hazardCheck))
        {
            return false;
        }

        bool shouldJumpNow =
            playerPosition.x >=
                launchWindowLeft - actionTriggerTolerance;
        string reason = shouldJumpNow
            ? "Stage2UnmappedWallIntercept"
            : "ApproachingStage2UnmappedWallIntercept";
        plan = new BonusJumpPlan(
            true,
            shouldJumpNow,
            hold,
            flightSeconds,
            travel,
            plannedLaunchX,
            predictedEntryX,
            launchWindowLeft,
            launchWindowRight,
            reason,
            $"{directEvaluation} | Stage2UnmappedWallIntercept[" +
            $"Source=[{scan.Current.Left:F3},{scan.Current.Right:F3}]" +
            $"@{scan.Current.Top:F3},Downstream=" +
            $"[{scan.Next.Left:F3},{scan.Next.Right:F3}]" +
            $"@{scan.Next.Top:F3},Gap={scan.Gap:F3}," +
            $"SourceWidth={scan.Current.Width:F3},RemainingRunway=" +
            $"{remainingSourceRunway:F3}," +
            $"MaxNativeHold={hold:F3},EffectiveHold={effectiveHold:F3}," +
            $"PredictedEntryX={predictedEntryX:F3}," +
            $"LandingShortfall={scan.Next.SafeLeft - predictedEntryX:F3}," +
            $"FixedStepTriggerTolerance=" +
            $"{actionTriggerTolerance:F3}," +
            $"Flight={flightSource},Travel={travelSource}," +
            $"Hazard={hazardCheck}]. Direct landing is unreachable; " +
            "the maximum jump enters the gap while physical grounded " +
            "zero-VX contacts own subsequent climb presses.",
            BonusManeuverKind.GroundJumpToLanding,
            0,
            travel,
            travel,
            false);
        return true;
    }

    private static BonusBoardScanResult SelectStage2VerifiedLowCorridor(
        BonusBoardScanResult scan,
        out string decision)
    {
        decision = "Stage2ObservedImmediate";
        if (!scan.IsValid ||
            !scan.HasNext ||
            scan.Alternatives == null ||
            scan.Alternatives.Length == 0 ||
            scan.HeightDelta < 5.35f ||
            float.IsFinite(scan.Next.WallFaceX))
        {
            return scan;
        }

        // Bonus Stage 2 contains short pits underneath raised pieces. The
        // vertical scanner can see the raised top first even though five
        // body-height probes find no side face in the runner's corridor.
        // Treating Next.Left as a wall in that geometry produces an
        // impossible wall-contact problem, returns Wait, and lets the player
        // walk into the pit. A same-height support beginning immediately
        // behind the face-less raised piece is the verified low route.
        BonusBoardSegment lowCorridor = scan.Alternatives
            .Where(candidate =>
                candidate.Width >= 0.75f &&
                candidate.Right > candidate.Left + 0.05f &&
                Mathf.Abs(candidate.Top - scan.Current.Top) <= 0.35f &&
                candidate.Left >= scan.Next.Right - 0.20f &&
                candidate.Left <= scan.Next.Right + 0.35f &&
                candidate.Left - scan.Current.Right <= 7.00f)
            .OrderBy(candidate => candidate.Left)
            .ThenByDescending(candidate => candidate.Width)
            .FirstOrDefault();
        if (lowCorridor.Width < 0.75f)
        {
            decision =
                $"Stage2RaisedSurfaceRetained[NoLowContinuation," +
                $"Raised=[{scan.Next.Left:F2},{scan.Next.Right:F2}]" +
                $"@{scan.Next.Top:F2},WallFace=Unverified]";
            return scan;
        }

        BonusBoardSegment raisedSurface = scan.Next;
        decision =
            $"Stage2VerifiedLowCorridorSelected[Raised=" +
            $"[{raisedSurface.Left:F2},{raisedSurface.Right:F2}]" +
            $"@{raisedSurface.Top:F2},WallFace=Unverified,Low=" +
            $"[{lowCorridor.Left:F2},{lowCorridor.Right:F2}]" +
            $"@{lowCorridor.Top:F2}]";
        return scan with
        {
            Next = lowCorridor,
            Gap = Mathf.Max(
                0f,
                lowCorridor.Left - scan.Current.Right),
            HeightDelta = lowCorridor.Top - scan.Current.Top,
            Reason =
                $"Stage2VerifiedLowCorridor:" +
                $"Raised[{raisedSurface.Left:F2}," +
                $"{raisedSurface.Right:F2}]@{raisedSurface.Top:F2}",
            // The missing low wall face is positive free-corridor evidence.
            // Re-inserting the raised top as an intermediate would make the
            // generic clearance test demand a jump over it and recreate the
            // same false rejection. Retain its measured bounds as inactive
            // evidence so the late narrow-support wall-catch route can target
            // the real collider after a direct low landing has failed.
            HasIntermediate = false,
            Intermediate = raisedSurface,
            Alternatives = scan.Alternatives
                .Where(candidate =>
                    !SameSurfaceGeometry(candidate, lowCorridor))
                .ToArray()
        };
    }

    private static Stage2Section1RouteDomain
        ClassifyStage2Section1RouteDomain(
            BonusBoardScanResult scan,
            int sectionIndex,
            bool useStage2LiveTopologyProfile)
    {
        if (!useStage2LiveTopologyProfile ||
            sectionIndex != 1 ||
            !scan.IsValid ||
            !scan.HasNext)
        {
            return new Stage2Section1RouteDomain(
                false,
                Stage2Section1RoutePhase.Inactive,
                "SectionOrTopologyProfileInactive");
        }

        float currentWidth = scan.Current.Width;
        float nextWidth = scan.Next.Width;
        bool verifiedLowCorridor =
            scan.Reason.StartsWith(
                "Stage2VerifiedLowCorridor:",
                System.StringComparison.Ordinal) &&
            !scan.HasIntermediate &&
            scan.Intermediate.Width >= 0.75f;
        Stage2Section1RoutePhase phase;
        if (verifiedLowCorridor &&
            currentWidth > 4.25f &&
            nextWidth <= 1.35f)
        {
            phase = Stage2Section1RoutePhase.EntryChain;
        }
        else if (currentWidth <= 2.25f &&
                 nextWidth >= 2.50f &&
                 Mathf.Abs(scan.HeightDelta) <= 0.35f)
        {
            phase = Stage2Section1RoutePhase.NarrowHandoff;
        }
        else if (verifiedLowCorridor &&
                 scan.Intermediate.Top - scan.Current.Top >= 5.35f)
        {
            phase = Stage2Section1RoutePhase.LowCorridorWallCatch;
        }
        else if (currentWidth <= 6.25f &&
                 nextWidth >= 8.0f &&
                 scan.Gap >= 12.0f)
        {
            phase = Stage2Section1RoutePhase.SteppedWallTraverse;
        }
        else if (scan.Gap <= 0.16f &&
                 scan.HeightDelta >= 1.50f)
        {
            phase = Stage2Section1RoutePhase.RisingStair;
        }
        else if (scan.HeightDelta <= -5.0f)
        {
            phase = Stage2Section1RoutePhase.HighRoadDescent;
        }
        else
        {
            phase = Stage2Section1RoutePhase.FreeLanding;
        }

        return new Stage2Section1RouteDomain(
            true,
            phase,
            $"CurrentWidth={currentWidth:F3},NextWidth={nextWidth:F3}," +
            $"Gap={scan.Gap:F3},DeltaY={scan.HeightDelta:F3}," +
            $"LowCorridor={verifiedLowCorridor}");
    }

    internal static BonusBoardScanResult SelectLowerRouteWhenItContinues(
        BonusBoardScanResult scan,
        BonusHazard hazard = default)
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

        // Topology normalization owns a body-width level seam before any
        // alternative target can replace it. Otherwise the downstream lower
        // route erases the original walkable seam and the planner later
        // invents a jump for geometry the player can simply run across.
        // A spike spanning the seam still owns the action.
        if (TryDescribeWalkableMicroGap(
                scan,
                hazard,
                out _,
                out _))
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
        SpiritBoostRouteContext spiritBoost,
        int sectionIndex,
        bool allowRecoverableLowerFaceCatch,
        bool useFixedStepAlignedHolds,
        out string selection,
        out BonusJumpPlan selectedPlan,
        out bool selectedPlanAvailable)
    {
        selection = "ImmediateRouteRetained";
        selectedPlan = default;
        selectedPlanAvailable = false;
        if (!scan.IsValid || !scan.HasNext ||
            scan.Alternatives == null || scan.Alternatives.Length == 0)
        {
            return scan;
        }

        if (TryDescribeWalkableMicroGap(
                scan,
                hazard,
                out float inferredBodyWidth,
                out float walkableGapLimit))
        {
            selection =
                $"TopologyOwned:WalkableMicroGap[Gap={scan.Gap:F3}," +
                $"Limit={walkableGapLimit:F3}," +
                $"InferredBodyWidth={inferredBodyWidth:F3}]";
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
                Array.Empty<Vector2>(),
                sectionIndex: sectionIndex,
                allowRecoverableLowerFaceCatch:
                    allowRecoverableLowerFaceCatch,
                useFixedStepAlignedHolds: useFixedStepAlignedHolds,
                spiritBoost: spiritBoost,
                routeTargetIsAuthoritative: true);
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

        if (bestPlan.IsValid)
        {
            selectedPlan = bestPlan;
            selectedPlanAvailable = true;
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

    private static bool TryDescribeWalkableMicroGap(
        BonusBoardScanResult scan,
        BonusHazard hazard,
        out float inferredBodyWidth,
        out float walkableGapLimit)
    {
        inferredBodyWidth = 0f;
        walkableGapLimit = 0.10f;
        if (!scan.IsValid || !scan.HasNext)
            return false;

        float inferredHalfWidth = Mathf.Max(
            0.15f,
            Mathf.Min(
                Mathf.Min(
                    scan.Current.SafeLeft - scan.Current.Left,
                    scan.Current.Right - scan.Current.SafeRight),
                Mathf.Min(
                    scan.Next.SafeLeft - scan.Next.Left,
                    scan.Next.Right - scan.Next.SafeRight)) - 0.15f);
        inferredBodyWidth = inferredHalfWidth * 2f;
        walkableGapLimit = Mathf.Max(
            0.10f,
            inferredBodyWidth - 0.10f);
        bool hazardOwnsMicroGap =
            hazard.IsValid &&
            hazard.Right >= scan.Current.Right - 0.15f &&
            hazard.Left <= scan.Next.Left + 0.15f;
        return scan.Gap > 0.10f &&
               scan.Gap <= walkableGapLimit &&
               Mathf.Abs(scan.HeightDelta) <= 0.10f &&
               !hazardOwnsMicroGap;
    }

    /// <summary>
    /// Replaces an impossible immediate landing with a verified downstream
    /// landing. The scanner supplies the ordered live/static support graph;
    /// this selector changes the target only after the immediate route has a
    /// direct-landing failure and every intermediate surface is cleared by the
    /// candidate trajectory.
    /// </summary>
    internal BonusBoardScanResult SelectReachableRoute(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        IReadOnlyList<Vector2> sphereObjectives,
        int sectionIndex,
        bool preferSphereCoverage,
        bool allowRecoverableLowerFaceCatch,
        bool useFixedStepAlignedHolds,
        SpiritBoostRouteContext spiritBoost,
        string selectionContext,
        out string selection,
        out BonusJumpPlan selectedPlan,
        out bool selectedPlanAvailable,
        int continuationDepth = 1,
        bool useStage2LiveTopologyProfile = false)
    {
        selection = "ImmediateRouteRetained";
        selectedPlan = default;
        selectedPlanAvailable = false;

        // Keep the scanner's nearest observed target authoritative while its
        // objectives are still active. A lower static continuation is a
        // useful topology candidate, but replacing Next before soul
        // arbitration loses the identity of pickups attached to the observed
        // platform and can make an otherwise safe run deliberately skip them.
        // With no such objective, normalize the lower continuation once here;
        // candidate Plan calls below are then explicitly target-authoritative
        // so the selected surface and the evaluated action cannot diverge.
        string stage2TopologyDecision = "Stage2ProfileInactive";
        if (useStage2LiveTopologyProfile)
        {
            scan = SelectStage2VerifiedLowCorridor(
                scan,
                out stage2TopologyDecision);
        }
        Stage2Section1RouteDomain stage2Section1RouteDomain =
            ClassifyStage2Section1RouteDomain(
                scan,
                sectionIndex,
                useStage2LiveTopologyProfile);
        if (stage2Section1RouteDomain.IsActive)
        {
            stage2TopologyDecision +=
                $";{stage2Section1RouteDomain.Summary}";
        }

        BonusBoardScanResult observedScan = scan;
        BonusBoardScanResult lowerContinuation =
            SelectLowerRouteWhenItContinues(scan, hazard);
        bool lowerContinuationChanged =
            observedScan.HasNext &&
            lowerContinuation.HasNext &&
            !SameSurfaceGeometry(
                observedScan.Next,
                lowerContinuation.Next);
        Vector2[] observedImmediateObjectives =
            preferSphereCoverage
                ? GetObjectivesAttachedToTarget(
                    sphereObjectives,
                    observedScan.Current,
                    observedScan.Next)
                : Array.Empty<Vector2>();
        string topologyDecision = useStage2LiveTopologyProfile
            ? stage2TopologyDecision
            : "ObservedImmediate";
        if (lowerContinuationChanged &&
            observedImmediateObjectives.Length == 0)
        {
            scan = lowerContinuation;
            topologyDecision =
                $"LowerContinuationSelected:" +
                $"[{scan.Next.Left:F2},{scan.Next.Right:F2}]@" +
                $"{scan.Next.Top:F2}";
        }
        else if (lowerContinuationChanged)
        {
            topologyDecision =
                $"ObservedImmediateRetainedForObjectives=" +
                $"{observedImmediateObjectives.Length}";
        }

        if (!scan.IsValid || !scan.HasNext ||
            scan.Alternatives == null || scan.Alternatives.Length == 0)
        {
            selection = topologyDecision;
            return scan;
        }

        // One live support graph feeds both the immediate optimizer and its
        // bounded continuation check. Keeping this set intact is what turns a
        // sequence of independent jumps into a receding-horizon route: a safe
        // landing that visibly leaves no executable next action is ranked
        // below a safe landing with a verified continuation.
        BonusBoardSegment[] allForwardSurfaces =
            new[]
                {
                    observedScan.Next,
                    lowerContinuation.Next,
                    scan.Next
                }
                .Concat(observedScan.Alternatives ??
                    Array.Empty<BonusBoardSegment>())
                .Concat(scan.Alternatives ??
                    Array.Empty<BonusBoardSegment>())
                .Where(candidate =>
                    candidate.Width > 0.05f &&
                    candidate.Right > scan.Current.Right + 0.10f &&
                    candidate.SafeRight > candidate.SafeLeft + 0.10f)
                .GroupBy(candidate =>
                    $"{candidate.Left:F2}:{candidate.Right:F2}:" +
                    $"{candidate.Top:F2}")
                .Select(group => group.First())
                .OrderBy(candidate => candidate.Left)
                .ThenBy(candidate => candidate.Top)
                .ToArray();

        // A Spirit reset can turn a narrow support into a one-physics-step
        // touch. When this selector is evaluating that projected support (or
        // the live fixed step immediately after pickup), prove its downstream
        // wall jump now. The original jump into the boost support remains
        // unchanged; the urgent fixed-step controller executes this prepared
        // continuation without losing a frame to a fresh topology search.
        if (TrySelectSpiritBoostWallContinuation(
                scan,
                allForwardSurfaces,
                playerPosition,
                playerVelocity,
                physics,
                hazard,
                sectionIndex,
                spiritBoost,
                out BonusBoardScanResult spiritWallScan,
                out BonusJumpPlan spiritWallPlan,
                out string spiritWallSelection))
        {
            selection = spiritWallSelection;
            selectedPlan = spiritWallPlan;
            selectedPlanAvailable = true;
            return spiritWallScan;
        }

        BonusJumpPlan immediatePlan = Plan(
            scan,
            playerPosition,
            playerVelocity,
            physics,
            hazard,
            preferSphereCoverage
                ? sphereObjectives
                : Array.Empty<Vector2>(),
            sectionIndex: sectionIndex,
            preferSphereCoverage: preferSphereCoverage,
            allowRecoverableLowerFaceCatch:
                allowRecoverableLowerFaceCatch,
            useFixedStepAlignedHolds: useFixedStepAlignedHolds,
            spiritBoost: spiritBoost,
            routeTargetIsAuthoritative: true,
            useStage2LiveTopologyProfile:
                useStage2LiveTopologyProfile);
        selectedPlan = immediatePlan;
        selectedPlanAvailable = true;
        bool immediateStableLandingCandidate =
            immediatePlan.IsValid &&
            (immediatePlan.Maneuver ==
                 BonusManeuverKind.GroundJumpToLanding ||
              immediatePlan.Maneuver ==
                  BonusManeuverKind.SphereSweepToLowerLanding);
        bool immediateLandingOutcome =
            immediateStableLandingCandidate ||
            immediatePlan.Reason == "IntentionalDrop";
        bool preferEarlySpiritSpeedBoostCoverage =
            spiritBoost.Enabled &&
            sectionIndex >= 0 &&
            sectionIndex < 2;
        bool immediateContinuationViable = true;
        float immediateContinuationSafety = 0f;
        string immediateContinuation = "NotRequired";
        if (continuationDepth > 0 && immediateLandingOutcome)
        {
            immediateContinuationViable = HasVerifiedLandingContinuation(
                scan.Next,
                immediatePlan,
                allForwardSurfaces,
                playerPosition,
                playerVelocity,
                physics,
                hazard,
                sectionIndex,
                allowRecoverableLowerFaceCatch,
                useFixedStepAlignedHolds,
                spiritBoost,
                continuationDepth,
                out immediateContinuationSafety,
                out immediateContinuation);
        }
        bool immediateTopologyOwned =
            immediatePlan.Reason == "IntentionalDrop" &&
                immediateContinuationViable ||
            immediatePlan.Reason == "ContinuousSurface" ||
            immediatePlan.IsValid && !immediateStableLandingCandidate ||
            stage2Section1RouteDomain.IsActive &&
            immediatePlan.IsValid &&
            immediatePlan.Reason.StartsWith(
                "Stage2UnmappedWallIntercept",
                System.StringComparison.Ordinal);
        if (immediateTopologyOwned)
        {
            selection =
                $"ImmediateRouteRetained:{immediatePlan.Reason};" +
                $"Continuation={immediateContinuation};" +
                topologyDecision;
            return scan;
        }

        // A boosted runner can be several units per second faster when the
        // current support first becomes visible, yet that extra tier decays
        // continuously before the body actually leaves the support edge.  Do
        // not replace a lower immediate support with a downstream jump merely
        // because a natural drop is invalid at the *current* speed. Project
        // the speed at the unsupported edge and retain the immediate route
        // when that physical boundary makes the drop valid. This is the
        // fourth-section three-soul lane: selecting the farther support early
        // both skips the objectives and integrates a speed tier that no longer
        // exists by takeoff.
        bool lowerImmediate = scan.HeightDelta < -0.35f;
        float liveSpeed = Mathf.Abs(playerVelocity.x);
        float decayFloor = Mathf.Clamp(
            physics.BaseHorizontalSpeed,
            1f,
            Mathf.Max(1f, liveSpeed));
        bool decayingSpeedTier =
            allowRecoverableLowerFaceCatch &&
            lowerImmediate &&
            liveSpeed > decayFloor + 0.25f;
        if (decayingSpeedTier)
        {
            float inferredHalfWidth = Mathf.Max(
                0.15f,
                scan.Current.Right - scan.Current.SafeRight - 0.15f);
            float unsupportedCenterX =
                scan.Current.Right + inferredHalfWidth;
            float travelToEdge = Mathf.Max(
                0f,
                unsupportedCenterX - playerPosition.x);
            float timeToEdge = SolveHorizontalTravelTime(
                liveSpeed,
                travelToEdge,
                0.60f,
                physics);
            float projectedEdgeSpeed = Mathf.Max(
                decayFloor,
                liveSpeed -
                    Mathf.Max(0.10f, physics.BoostHorizontalDeceleration) *
                    timeToEdge);
            BonusJumpPlan edgePlan = Plan(
                scan,
                playerPosition,
                new Vector2(projectedEdgeSpeed, playerVelocity.y),
                physics,
                hazard,
                preferSphereCoverage
                    ? sphereObjectives
                    : Array.Empty<Vector2>(),
                sectionIndex: sectionIndex,
                preferSphereCoverage: preferSphereCoverage,
                allowRecoverableLowerFaceCatch:
                    allowRecoverableLowerFaceCatch,
                useFixedStepAlignedHolds: useFixedStepAlignedHolds,
                spiritBoost: spiritBoost,
                routeTargetIsAuthoritative: true,
                useStage2LiveTopologyProfile:
                    useStage2LiveTopologyProfile);
            if (edgePlan.Reason == "IntentionalDrop")
            {
                string edgeContinuation = "DepthLimit";
                float edgeContinuationSafety = 0f;
                bool edgeContinuationViable = continuationDepth <= 0 ||
                    HasVerifiedLandingContinuation(
                        scan.Next,
                        edgePlan,
                        allForwardSurfaces,
                        playerPosition,
                        new Vector2(projectedEdgeSpeed, playerVelocity.y),
                        physics,
                        hazard,
                        sectionIndex,
                        allowRecoverableLowerFaceCatch,
                        useFixedStepAlignedHolds,
                        spiritBoost,
                        continuationDepth,
                        out edgeContinuationSafety,
                        out edgeContinuation);
                if (edgeContinuationViable)
                {
                    selection =
                        $"ImmediateRouteRetained:ProjectedEdgeIntentionalDrop" +
                        $"[LiveVX={liveSpeed:F3},EdgeVX=" +
                        $"{projectedEdgeSpeed:F3},BaseVX={decayFloor:F3}," +
                        $"TravelToEdge={travelToEdge:F3}," +
                        $"TimeToEdge={timeToEdge:F3}," +
                        $"Landing={edgePlan.PredictedLandingX:F3}," +
                        $"ContinuationSafety=" +
                        $"{edgeContinuationSafety:F3}," +
                        $"Continuation={edgeContinuation}]";
                    selectedPlan = edgePlan;
                    return scan;
                }

                // Keep the drop as a safe fallback if every visible route is
                // terminal, but allow alternatives to compete above it.
                immediatePlan = edgePlan;
                selectedPlan = edgePlan;
                immediateContinuationViable = false;
                immediateContinuationSafety = edgeContinuationSafety;
                immediateContinuation = edgeContinuation;
            }
        }

        bool directLandingFailure =
            immediatePlan.Reason == "NoVerifiedLaunchWindow" ||
            immediatePlan.Reason == "RaisedLandingHasNoSafeDirectJump" ||
            immediatePlan.Reason == "IntentionalDrop" &&
                !immediateContinuationViable;
        if (!immediateStableLandingCandidate && !directLandingFailure)
        {
            selection =
                $"TopologyFailureRetained:{immediatePlan.Reason}";
            return scan;
        }

        float speed = Mathf.Abs(playerVelocity.x);
        if (speed < 1f)
        {
            selection = "ImmediateFailureRetained:HorizontalSpeedTooLow";
            return scan;
        }

        BonusBoardScanResult bestScan = scan;
        BonusJumpPlan bestPlan = immediateStableLandingCandidate
            ? immediatePlan
            : default;
        bool bestContinuationViable =
            immediateStableLandingCandidate &&
            immediateContinuationViable;
        float bestContinuationSafety = bestContinuationViable
            ? immediateContinuationSafety
            : float.NegativeInfinity;
        float bestScore = float.NegativeInfinity;
        float bestLandingSafety = float.NegativeInfinity;
        float bestForwardDistance = immediateStableLandingCandidate
            ? Mathf.Max(0f, scan.Next.Left - scan.Current.Right)
            : float.PositiveInfinity;
        HashSet<int> immediateRouteObjectiveIds =
            GetObjectiveIndicesAttachedToTarget(
                sphereObjectives,
                scan.Current,
                scan.Next);
        HashSet<int> bestGuaranteedObjectiveIds =
            immediateStableLandingCandidate && preferSphereCoverage
                ? GetGuaranteedRouteObjectiveHitIndices(
                    sphereObjectives,
                    scan.Current,
                    playerPosition.x,
                    speed,
                    immediatePlan,
                    physics,
                    spiritBoost)
                : new HashSet<int>();
        int bestExpectedSpeedBoostHits =
            immediateStableLandingCandidate
                ? immediatePlan.ExpectedSpeedBoostHits
                : 0;
        StringBuilder evaluation = new();
        evaluation.Append(
            $"{stage2Section1RouteDomain.Summary};" +
            $"Immediate={immediatePlan.Reason},VX={speed:F3}," +
            $"ImmediateObjectives={immediateRouteObjectiveIds.Count}," +
            $"ImmediateSpeedBoostHits={bestExpectedSpeedBoostHits}," +
            $"ImmediateGuaranteedObjectives=" +
            $"{DescribeObjectiveIds(bestGuaranteedObjectiveIds)}," +
            $"ImmediateContinuation=" +
            $"{immediateContinuationViable}/" +
            $"Safety={immediateContinuationSafety:F3}" +
            $"[{immediateContinuation}]");
        if (immediateStableLandingCandidate)
        {
            bool immediateCalibrated =
                physics.TravelProfile.TryGetDuration(
                    immediatePlan.HoldSeconds,
                    scan.Next.Top - scan.Current.Top,
                    out _,
                    out int immediateSamples);
            float immediateLaunchX = immediatePlan.ShouldJumpNow
                ? playerPosition.x
                : immediatePlan.PlannedLaunchX;
            bestLandingSafety = GetLandingFirstSafetyMargin(
                immediatePlan,
                scan.Next,
                immediateLaunchX,
                speed,
                physics,
                immediateCalibrated,
                immediateSamples);
            float immediateRunway = GetLandingRunway(
                immediatePlan,
                scan.Next,
                immediateLaunchX);
            float immediateSafeWidth = Mathf.Max(
                0f,
                scan.Next.SafeRight - scan.Next.SafeLeft);
            float immediateWindow = Mathf.Max(
                0f,
                immediatePlan.LaunchWindowRight -
                immediatePlan.LaunchWindowLeft);
            bestScore = GetRouteGeometryTieBreakScore(
                immediateSafeWidth,
                immediateWindow,
                immediateRunway,
                preferSphereCoverage
                    ? bestGuaranteedObjectiveIds.Count
                    : 0);
            evaluation.Append(
                $",ImmediateLandingSafety={bestLandingSafety:F3}," +
                $"ImmediateRunway={immediateRunway:F3}," +
                $"ImmediateEnvelope=" +
                $"{DescribeLandingEnvelope(immediatePlan, scan.Next, immediateLaunchX)}");
        }

        foreach (BonusBoardSegment target in allForwardSurfaces)
        {
            if (SameSurfaceGeometry(target, scan.Next))
                continue;

            // A future boost may narrow a route's safe launch window, but the
            // no-pickup trajectory must be able to reach it independently.
            // Reject obviously unreachable alternatives before invoking the
            // nine-hold, three-probe slow/fast solver. This is especially
            // important in Section 4, where two distant surfaces remained in
            // the horizon and cost 20-26 ms every final-proof FixedUpdate even
            // though the base-speed endpoint ended several units short.
            if (spiritBoost.Enabled)
            {
                float optimisticHold = Mathf.Min(
                    MaximumHoldSeconds,
                    physics.EffectiveHoldCapSeconds);
                bool hasOptimisticFlight = TryPredictFlightTime(
                    optimisticHold,
                    target.Top - scan.Current.Top,
                    physics,
                    out float optimisticFlightSeconds,
                    out _,
                    out _);
                float optimisticNoPickupTravel = hasOptimisticFlight
                    ? PredictHorizontalTravel(
                        speed,
                        optimisticFlightSeconds,
                        optimisticHold,
                        target.Top - scan.Current.Top,
                        physics,
                        out _)
                    : 0f;
                float fixedStepReserve = PredictHorizontalTravelAtTime(
                    speed,
                    Mathf.Clamp(
                        physics.FixedDeltaTime,
                        0.005f,
                        0.05f),
                    physics);
                float optimisticNoPickupRight =
                    scan.Current.SafeRight +
                    optimisticNoPickupTravel +
                    fixedStepReserve +
                    0.75f;
                if (!hasOptimisticFlight ||
                    optimisticNoPickupRight <
                        target.SafeLeft - LandingRecoveryTolerance)
                {
                    evaluation.Append(
                        $" | Target=[{target.Left:F2},{target.Right:F2}]" +
                        $"@{target.Top:F2}:BroadPhaseRejected[" +
                        $"NoPickupReach=False,MaxRight=" +
                        $"{optimisticNoPickupRight:F2},Need=" +
                        $"{target.SafeLeft:F2},MaxHold=" +
                        $"{optimisticHold:F3}]");
                    continue;
                }
            }

            BonusBoardScanResult candidateScan = scan with
            {
                HasNext = true,
                Next = target,
                Gap = Mathf.Max(0f, target.Left - scan.Current.Right),
                HeightDelta = target.Top - scan.Current.Top,
                HasIntermediate = true,
                Intermediate = scan.Next,
                // Preserve the full set so the exact hold selected by Plan
                // proves every crossed support. The selector must not verify
                // one terrain command and let a later sphere-scored command
                // inherit that proof.
                Alternatives = allForwardSurfaces
                    .Where(surface =>
                        !SameSurfaceGeometry(surface, target))
                    .ToArray(),
                Reason =
                    $"{selectionContext}DynamicAlternative:" +
                    $"{target.MapPieceName}"
            };
            BonusJumpPlan candidatePlan = Plan(
                candidateScan,
                playerPosition,
                playerVelocity,
                physics,
                hazard,
                preferSphereCoverage
                    ? sphereObjectives
                    : Array.Empty<Vector2>(),
                sectionIndex: sectionIndex,
                preferSphereCoverage: preferSphereCoverage,
                allowRecoverableLowerFaceCatch:
                    allowRecoverableLowerFaceCatch,
                useFixedStepAlignedHolds: useFixedStepAlignedHolds,
                spiritBoost: spiritBoost,
                routeTargetIsAuthoritative: true,
                useStage2LiveTopologyProfile:
                    useStage2LiveTopologyProfile);
            if (!candidatePlan.IsValid ||
                candidatePlan.Maneuver !=
                    BonusManeuverKind.GroundJumpToLanding)
            {
                evaluation.Append(
                    $" | Target=[{target.Left:F2},{target.Right:F2}]" +
                    $"@{target.Top:F2}:PlanRejected=" +
                    $"{candidatePlan.Reason}/{candidatePlan.Maneuver}");
                continue;
            }

            float plannedLaunchX = candidatePlan.ShouldJumpNow
                ? playerPosition.x
                : candidatePlan.PlannedLaunchX;
            float plannedLandingX = candidatePlan.ShouldJumpNow
                ? candidatePlan.PredictedLandingX
                : candidatePlan.PlannedLaunchX +
                    candidatePlan.HorizontalTravel;
            string clearanceSummary =
                "ProvedInsideSharedSlow/FastPlanEnvelope";
            bool clearsIntermediate =
                candidatePlan.FutureSpeedTransitionExpected;
            if (!clearsIntermediate)
            {
                clearsIntermediate = TrajectoryClearsIntermediateSurfaces(
                    scan.Current,
                    target,
                    allForwardSurfaces,
                    plannedLaunchX,
                    plannedLandingX,
                    speed,
                    candidatePlan.HoldSeconds,
                    candidatePlan.PredictedFlightSeconds,
                    physics,
                    out clearanceSummary);
            }
            HashSet<int> candidateGuaranteedObjectiveIds =
                preferSphereCoverage
                    ? GetGuaranteedRouteObjectiveHitIndices(
                        sphereObjectives,
                        scan.Current,
                        playerPosition.x,
                        speed,
                        candidatePlan,
                        physics,
                        spiritBoost)
                    : new HashSet<int>();
            int predictedSphereHits =
                candidateGuaranteedObjectiveIds.Count;
            int predictedSpeedBoostHits =
                candidatePlan.ExpectedSpeedBoostHits;
            int immediateObjectiveHits =
                candidateGuaranteedObjectiveIds.Count(
                    immediateRouteObjectiveIds.Contains);
            int lostBestObjectives = bestGuaranteedObjectiveIds.Count(
                objectiveId =>
                    !candidateGuaranteedObjectiveIds.Contains(objectiveId));
            int gainedBestObjectives =
                candidateGuaranteedObjectiveIds.Count(
                    objectiveId =>
                        !bestGuaranteedObjectiveIds.Contains(objectiveId));
            bool preservesBestGuaranteedObjectives =
                lostBestObjectives == 0;
            evaluation.Append(
                $" | Target=[{target.Left:F2},{target.Right:F2}]" +
                $"@{target.Top:F2}:Plan={candidatePlan.Reason}," +
                $"Hold={candidatePlan.HoldSeconds:F3}," +
                $"Launch={plannedLaunchX:F2},Landing=" +
                $"{plannedLandingX:F2},Window=" +
                $"[{candidatePlan.LaunchWindowLeft:F2}," +
                $"{candidatePlan.LaunchWindowRight:F2}]," +
                $"SphereHits={predictedSphereHits},ImmediateObjectiveHits=" +
                $"{immediateObjectiveHits}/" +
                $"{immediateRouteObjectiveIds.Count}," +
                $"SpeedBoostHits={predictedSpeedBoostHits}," +
                $"GuaranteedObjectives=" +
                $"{DescribeObjectiveIds(candidateGuaranteedObjectiveIds)}," +
                $"ObjectiveDelta=+{gainedBestObjectives}/" +
                $"-{lostBestObjectives},PreservesBest=" +
                $"{preservesBestGuaranteedObjectives}," +
                $"Clear={clearsIntermediate}[{clearanceSummary}]");
            if (!clearsIntermediate)
                continue;

            float safeWidth = Mathf.Max(
                0f,
                target.SafeRight - target.SafeLeft);
            float windowWidth = Mathf.Max(
                0f,
                candidatePlan.LaunchWindowRight -
                candidatePlan.LaunchWindowLeft);
            float forwardDistance = Mathf.Max(
                0f,
                target.Left - scan.Current.Right);
            // Readiness is an execution detail, not route utility. Every route
            // is compared with the same score and distance is handled as a
            // separate lexicographic tier below, so a farther surface cannot
            // buy its way past the nearest route with width/runway points.
            float score = GetRouteGeometryTieBreakScore(
                safeWidth,
                windowWidth,
                0f,
                preferSphereCoverage ? predictedSphereHits : 0);
            bool candidateCalibrated =
                physics.TravelProfile.TryGetDuration(
                    candidatePlan.HoldSeconds,
                    target.Top - scan.Current.Top,
                    out _,
                    out int candidateSamples);
            float candidateLandingSafety =
                GetLandingFirstSafetyMargin(
                    candidatePlan,
                    target,
                    plannedLaunchX,
                    speed,
                    physics,
                    candidateCalibrated,
                    candidateSamples);
            float candidateRunway = GetLandingRunway(
                candidatePlan,
                target,
                plannedLaunchX);
            score = GetRouteGeometryTieBreakScore(
                safeWidth,
                windowWidth,
                candidateRunway,
                preferSphereCoverage ? predictedSphereHits : 0);
            string candidateContinuation = "DepthLimit";
            float candidateContinuationSafety = 0f;
            bool candidateContinuationViable = continuationDepth <= 0 ||
                HasVerifiedLandingContinuation(
                    target,
                    candidatePlan,
                    allForwardSurfaces,
                    playerPosition,
                    playerVelocity,
                    physics,
                    hazard,
                    sectionIndex,
                    allowRecoverableLowerFaceCatch,
                    useFixedStepAlignedHolds,
                    spiritBoost,
                    continuationDepth,
                    out candidateContinuationSafety,
                    out candidateContinuation);
            evaluation.Append(
                $",LandingSafety={candidateLandingSafety:F3}," +
                $"Runway={candidateRunway:F3}," +
                $"Continuation={candidateContinuationViable}/" +
                $"Safety={candidateContinuationSafety:F3}" +
                $"[{candidateContinuation}]," +
                $"Envelope=" +
                $"{DescribeLandingEnvelope(candidatePlan, target, plannedLaunchX)}");
            bool replaceBest = ShouldReplaceRouteCandidate(
                candidateLandingSafety,
                candidateContinuationViable,
                candidateContinuationSafety,
                candidateGuaranteedObjectiveIds,
                predictedSpeedBoostHits,
                preferEarlySpiritSpeedBoostCoverage,
                forwardDistance,
                score,
                bestLandingSafety,
                bestContinuationViable,
                bestContinuationSafety,
                bestGuaranteedObjectiveIds,
                bestExpectedSpeedBoostHits,
                bestForwardDistance,
                bestScore,
                out string rankingDecision);
            evaluation.Append($",Ranking={rankingDecision}");
            if (!replaceBest)
                continue;

            bestForwardDistance = forwardDistance;
            bestLandingSafety = candidateLandingSafety;
            bestContinuationViable = candidateContinuationViable;
            bestContinuationSafety = candidateContinuationSafety;
            bestGuaranteedObjectiveIds =
                candidateGuaranteedObjectiveIds;
            bestExpectedSpeedBoostHits =
                predictedSpeedBoostHits;
            bestScore = score;
            bestScan = candidateScan;
            bestPlan = candidatePlan;
        }

        if (SameSurfaceGeometry(bestScan.Next, scan.Next))
        {
            selection =
                $"NoVerified{selectionContext}Alternative[{evaluation}]";
            return scan;
        }

        selection =
            $"{selectionContext}LandingFirstAlternativeSelected Immediate=" +
            $"[{scan.Next.Left:F2},{scan.Next.Right:F2}]@" +
            $"{scan.Next.Top:F2}/{immediatePlan.Reason},Selected=" +
            $"[{bestScan.Next.Left:F2},{bestScan.Next.Right:F2}]@" +
            $"{bestScan.Next.Top:F2},Hold={bestPlan.HoldSeconds:F3}," +
            $"Landing={bestPlan.PredictedLandingX:F2},Score=" +
            $"{bestScore:F3},WorstCaseSafety=" +
            $"{bestLandingSafety:F3},ContinuationSafety=" +
            $"{bestContinuationSafety:F3},GuaranteedObjectives=" +
            $"{DescribeObjectiveIds(bestGuaranteedObjectiveIds)}," +
            $"ExpectedSpeedBoostHits={bestExpectedSpeedBoostHits}; " +
            $"{evaluation}";
        selectedPlan = bestPlan;
        selectedPlanAvailable = true;
        return bestScan;
    }

    private bool TrySelectSpiritBoostWallContinuation(
        BonusBoardScanResult scan,
        IReadOnlyList<BonusBoardSegment> allForwardSurfaces,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        int sectionIndex,
        SpiritBoostRouteContext spiritBoost,
        out BonusBoardScanResult selectedScan,
        out BonusJumpPlan selectedPlan,
        out string selection)
    {
        selectedScan = scan;
        selectedPlan = default;
        selection = "SpiritWallContinuationInactive";
        if (sectionIndex != 3 ||
            !spiritBoost.Enabled ||
            !spiritBoost.KinematicsAvailable ||
            !scan.IsValid ||
            !scan.HasNext)
        {
            return false;
        }

        float fastSpeed = Mathf.Max(
            Mathf.Abs(playerVelocity.x),
            spiritBoost.BaseHorizontalSpeed +
            spiritBoost.MaximumBoostComponent);
        float safeWidth = Mathf.Max(
            0f,
            scan.Current.SafeRight - scan.Current.SafeLeft);
        float stableObservationTravel =
            fastSpeed *
            Mathf.Clamp(physics.FixedDeltaTime, 0.005f, 0.05f) *
            3f;
        bool transientTouchTarget =
            safeWidth <= stableObservationTravel + 0.15f;
        bool pendingPickupOnTransientSource =
            spiritBoost.ActiveTriggers != null &&
            spiritBoost.ActiveTriggers.Any(trigger =>
                trigger.IsValid &&
                trigger.Left <= scan.Current.Right + 0.75f &&
                trigger.Right >= scan.Current.Left - 0.75f);
        bool activeBoostOnTransientSource =
            spiritBoost.CurrentBoostComponent > 0.50f &&
            Mathf.Abs(playerVelocity.x) >
                spiritBoost.BaseHorizontalSpeed + 0.50f;
        if (!transientTouchTarget ||
            (!pendingPickupOnTransientSource &&
             !activeBoostOnTransientSource))
            return false;

        BonusBoardSegment wall = allForwardSurfaces
            .Where(candidate =>
                candidate.Left >= scan.Current.Right + 2.0f &&
                candidate.Left - scan.Current.Right <= 28.0f &&
                candidate.Top >= scan.Current.Top + 5.35f)
            .OrderBy(candidate => candidate.Left)
            .ThenBy(candidate => candidate.Top)
            .FirstOrDefault();
        if (wall.Width <= 0.05f)
            return false;

        BonusBoardSegment terminalWallApron =
            allForwardSurfaces
                .Where(candidate =>
                    !SameSurfaceGeometry(candidate, wall) &&
                    candidate.Right >= wall.Left - 0.15f &&
                    candidate.Left < wall.Left &&
                    candidate.Top <= scan.Current.Top + 0.35f &&
                    candidate.Top >= scan.Current.Top - 3.50f)
                .OrderByDescending(candidate => candidate.Right)
                .FirstOrDefault();
        BonusBoardSegment[] intermediateSurfaces =
            allForwardSurfaces
                .Where(candidate =>
                    !SameSurfaceGeometry(candidate, wall) &&
                    (terminalWallApron.Width <= 0.05f ||
                     !SameSurfaceGeometry(
                         candidate,
                         terminalWallApron)) &&
                    candidate.Left < wall.Left)
                .ToArray();
        BonusBoardScanResult wallScan = scan with
        {
            Next = wall,
            Gap = Mathf.Max(0f, wall.Left - scan.Current.Right),
            HeightDelta = wall.Top - scan.Current.Top,
            HasIntermediate = terminalWallApron.Width <= 0.05f,
            Intermediate = terminalWallApron.Width <= 0.05f
                ? scan.Next
                : default,
            Alternatives = intermediateSurfaces,
            Reason = pendingPickupOnTransientSource
                ? "SpiritBoostTransientLandingToWallContinuationPendingPickup"
                : "SpiritBoostTransientLandingToWallContinuationActiveBoost"
        };
        BonusJumpPlan wallPlan = Plan(
            wallScan,
            playerPosition,
            playerVelocity,
            physics,
            hazard,
            Array.Empty<Vector2>(),
            sectionIndex: sectionIndex,
            preferSphereCoverage: false,
            allowRecoverableLowerFaceCatch: true,
            useFixedStepAlignedHolds: true,
            spiritBoost: spiritBoost,
            routeTargetIsAuthoritative: true);
        if (!wallPlan.IsValid ||
            wallPlan.Maneuver !=
                BonusManeuverKind.ApproachJumpThenWallJump)
        {
            selection =
                $"SpiritTransientLandingWallProofRejected[" +
                $"TransientSource=[{scan.Current.Left:F2}," +
                $"{scan.Current.Right:F2}]@{scan.Current.Top:F2}," +
                $"SafeWidth={safeWidth:F3}," +
                $"ObservationTravel={stableObservationTravel:F3}," +
                $"SpeedState=" +
                $"{(pendingPickupOnTransientSource
                    ? "PendingPickup"
                    : "ActiveBoost")}," +
                $"Wall=[{wall.Left:F2},{wall.Right:F2}]@{wall.Top:F2}," +
                $"WallFaceX=" +
                $"{(float.IsFinite(wall.WallFaceX)
                    ? wall.WallFaceX.ToString("F3")
                    : "Unresolved")}," +
                $"Reason={wallPlan.Reason}]";
            return false;
        }

        selectedScan = wallScan;
        selectedPlan = wallPlan with
        {
            CandidateSummary = wallPlan.CandidateSummary +
                $" | SpiritTransientLandingContinuation[Source=" +
                $"[{scan.Current.Left:F3},{scan.Current.Right:F3}]" +
                $"@{scan.Current.Top:F3},SafeWidth={safeWidth:F3}," +
                $"FastVX={fastSpeed:F3},ObservationTravel=" +
                $"{stableObservationTravel:F3},PrearmedWall=" +
                $"[{wall.Left:F3},{wall.Right:F3}]@{wall.Top:F3}," +
                $"WallFaceX=" +
                $"{(float.IsFinite(wall.WallFaceX)
                    ? wall.WallFaceX.ToString("F3")
                    : "Unresolved")}," +
                $"TerminalApron=" +
                $"{(terminalWallApron.Width > 0.05f
                    ? $"[{terminalWallApron.Left:F3}," +
                      $"{terminalWallApron.Right:F3}]" +
                      $"@{terminalWallApron.Top:F3}"
                    : "None")}]"
        };
        selection =
            $"SpiritTransientLandingWallContinuationSelected " +
            $"TransientSource=[{scan.Current.Left:F2}," +
            $"{scan.Current.Right:F2}]@{scan.Current.Top:F2},Wall=" +
            $"[{wall.Left:F2},{wall.Right:F2}]@{wall.Top:F2}," +
            $"WallFaceX=" +
            $"{(float.IsFinite(wall.WallFaceX)
                ? wall.WallFaceX.ToString("F3")
                : "Unresolved")}," +
            $"Hold={wallPlan.HoldSeconds:F3},Launch=" +
            $"{wallPlan.PlannedLaunchX:F3},SpeedState=" +
            $"{(pendingPickupOnTransientSource
                ? "PendingPickup"
                : "ActiveBoost")},Reason={wallPlan.Reason}";
        return true;
    }

    private static float GetRouteGeometryTieBreakScore(
        float safeWidth,
        float launchWindowWidth,
        float landingRunway,
        int guaranteedObjectiveHits) =>
        Mathf.Max(0f, safeWidth) * 0.60f +
        Mathf.Max(0f, launchWindowWidth) * 0.85f +
        landingRunway * 0.75f +
        Mathf.Max(0, guaranteedObjectiveHits) * 0.25f;

    /// <summary>
    /// Route comparison is deliberately lexicographic. First-contact landing
    /// safety owns the decision before topology, objectives, distance or
    /// geometric comfort. Continuation may decide only between comparable safe
    /// first landings. If every option is outside the conservative safety
    /// margin, the least-negative margin is retained instead of returning no
    /// command and walking off the source.
    /// </summary>
    private static bool ShouldReplaceRouteCandidate(
        float candidateLandingSafety,
        bool candidateContinuationViable,
        float candidateContinuationSafety,
        IReadOnlySet<int> candidateObjectiveIds,
        int candidateSpeedBoostHits,
        bool preferSpeedBoostCoverage,
        float candidateForwardDistance,
        float candidateScore,
        float bestLandingSafety,
        bool bestContinuationViable,
        float bestContinuationSafety,
        IReadOnlySet<int> bestObjectiveIds,
        int bestSpeedBoostHits,
        float bestForwardDistance,
        float bestScore,
        out string decision)
    {
        float candidateSafety = float.IsFinite(candidateLandingSafety)
            ? candidateLandingSafety
            : float.NegativeInfinity;
        float incumbentSafety = float.IsFinite(bestLandingSafety)
            ? bestLandingSafety
            : float.NegativeInfinity;
        bool candidateSafe = candidateSafety >= 0f;
        bool incumbentSafe = incumbentSafety >= 0f;
        if (candidateSafe != incumbentSafe)
        {
            decision = candidateSafe
                ? "Replace:OnlyCandidatePassesFirstLandingSafetyGate"
                : "Keep:IncumbentPassesFirstLandingSafetyGate";
            return candidateSafe;
        }

        if (!candidateSafe)
        {
            bool replaceUnsafe =
                candidateSafety > incumbentSafety + 0.001f;
            decision = replaceUnsafe
                ? $"Replace:LeastUnsafeMargin[{candidateSafety:F3}>" +
                  $"{incumbentSafety:F3}]"
                : $"Keep:LeastUnsafeMargin[{candidateSafety:F3}<=" +
                  $"{incumbentSafety:F3}]";
            return replaceUnsafe;
        }

        if (candidateSafety > incumbentSafety + RouteLandingSafetyTier)
        {
            decision =
                $"Replace:MateriallySaferFirstLanding[" +
                $"{candidateSafety:F3}>{incumbentSafety:F3}]";
            return true;
        }
        if (candidateSafety < incumbentSafety - RouteLandingSafetyTier)
        {
            decision =
                $"Keep:MateriallySaferFirstLanding[" +
                $"{incumbentSafety:F3}>{candidateSafety:F3}]";
            return false;
        }

        if (candidateContinuationViable != bestContinuationViable)
        {
            decision = candidateContinuationViable
                ? "Replace:ContinuationWithinSafeLandingTier"
                : "Keep:ContinuationWithinSafeLandingTier";
            return candidateContinuationViable;
        }
        if (candidateContinuationViable && bestContinuationViable)
        {
            float candidateContinuation =
                float.IsFinite(candidateContinuationSafety)
                    ? candidateContinuationSafety
                    : float.NegativeInfinity;
            float incumbentContinuation =
                float.IsFinite(bestContinuationSafety)
                    ? bestContinuationSafety
                    : float.NegativeInfinity;
            if (candidateContinuation >
                incumbentContinuation + RouteLandingSafetyTier)
            {
                decision =
                    $"Replace:SaferContinuation[" +
                    $"{candidateContinuation:F3}>" +
                    $"{incumbentContinuation:F3}]";
                return true;
            }
            if (candidateContinuation <
                incumbentContinuation - RouteLandingSafetyTier)
            {
                decision =
                    $"Keep:SaferContinuation[" +
                    $"{incumbentContinuation:F3}>" +
                    $"{candidateContinuation:F3}]";
                return false;
            }
        }

        int lostObjectives = bestObjectiveIds.Count(
            objectiveId => !candidateObjectiveIds.Contains(objectiveId));
        int gainedObjectives = candidateObjectiveIds.Count(
            objectiveId => !bestObjectiveIds.Contains(objectiveId));
        if (lostObjectives == 0 && gainedObjectives > 0)
        {
            decision =
                $"Replace:GuaranteedObjectiveSuperset[+" +
                $"{gainedObjectives}]";
            return true;
        }
        if (gainedObjectives == 0 && lostObjectives > 0)
        {
            decision =
                $"Keep:GuaranteedObjectiveSuperset[-" +
                $"{lostObjectives}]";
            return false;
        }
        if (candidateObjectiveIds.Count != bestObjectiveIds.Count)
        {
            bool replaceCoverage =
                candidateObjectiveIds.Count > bestObjectiveIds.Count;
            decision = replaceCoverage
                ? $"Replace:MoreGuaranteedObjectives[" +
                  $"{candidateObjectiveIds.Count}>" +
                  $"{bestObjectiveIds.Count},DifferentIds]"
                : $"Keep:MoreGuaranteedObjectives[" +
                  $"{bestObjectiveIds.Count}>" +
                  $"{candidateObjectiveIds.Count},DifferentIds]";
            return replaceCoverage;
        }

        // Speed boosts are route objectives only in the first two Spirit
        // sections. They may choose between routes that already preserve the
        // same guaranteed souls and the same landing/continuation safety tier,
        // but can never buy away a soul or a safer first landing.
        if (preferSpeedBoostCoverage &&
            candidateSpeedBoostHits != bestSpeedBoostHits)
        {
            bool replaceBoostCoverage =
                candidateSpeedBoostHits > bestSpeedBoostHits;
            decision = replaceBoostCoverage
                ? $"Replace:MoreSpeedBoostHits[" +
                  $"{candidateSpeedBoostHits}>{bestSpeedBoostHits}]"
                : $"Keep:MoreSpeedBoostHits[" +
                  $"{bestSpeedBoostHits}>{candidateSpeedBoostHits}]";
            return replaceBoostCoverage;
        }

        if (candidateForwardDistance <
            bestForwardDistance - RouteDistanceTier)
        {
            decision =
                $"Replace:CloserRoute[" +
                $"{candidateForwardDistance:F3}<" +
                $"{bestForwardDistance:F3}]";
            return true;
        }
        if (candidateForwardDistance >
            bestForwardDistance + RouteDistanceTier)
        {
            decision =
                $"Keep:CloserRoute[{bestForwardDistance:F3}<" +
                $"{candidateForwardDistance:F3}]";
            return false;
        }

        bool replaceTie = candidateScore > bestScore + 0.001f;
        decision = replaceTie
            ? $"Replace:GeometryTieBreak[{candidateScore:F3}>" +
              $"{bestScore:F3}]"
            : $"Keep:GeometryTieBreak[{candidateScore:F3}<=" +
              $"{bestScore:F3}]";
        return replaceTie;
    }

    private static HashSet<int> GetObjectiveIndicesAttachedToTarget(
        IReadOnlyList<Vector2> sphereObjectives,
        BonusBoardSegment source,
        BonusBoardSegment target)
    {
        HashSet<int> result = new();
        if (sphereObjectives == null || sphereObjectives.Count == 0 ||
            target.Width <= 0.05f)
        {
            return result;
        }

        for (int index = 0; index < sphereObjectives.Count; index++)
        {
            Vector2 sphere = sphereObjectives[index];
            if (sphere.x >= target.Left - 0.50f &&
                sphere.x <= target.Right + 0.75f &&
                sphere.y >= Mathf.Min(source.Top, target.Top) - 0.45f &&
                sphere.y <= Mathf.Max(source.Top, target.Top) + 4.25f)
            {
                result.Add(index);
            }
        }
        return result;
    }

    private static HashSet<int> GetGuaranteedRouteObjectiveHitIndices(
        IReadOnlyList<Vector2> sphereObjectives,
        BonusBoardSegment source,
        float currentX,
        float speed,
        BonusJumpPlan plan,
        JumpPhysicsSnapshot physics,
        SpiritBoostRouteContext spiritBoost)
    {
        HashSet<int> result = new();
        if (sphereObjectives == null || sphereObjectives.Count == 0)
            return result;

        float launchX = float.IsFinite(plan.PredictedLandingX) &&
                        float.IsFinite(plan.HorizontalTravel)
            ? plan.PredictedLandingX - plan.HorizontalTravel
            : plan.ShouldJumpNow
                ? currentX
                : plan.PlannedLaunchX;
        if (!float.IsFinite(launchX))
            launchX = currentX;

        for (int index = 0; index < sphereObjectives.Count; index++)
        {
            Vector2 sphere = sphereObjectives[index];
            if (sphere.x < currentX - SpherePickupHorizontalReach ||
                sphere.x >= launchX - SpherePickupHorizontalReach)
            {
                continue;
            }
            if (TrajectoryBodyOverlapsSphere(source.Top, sphere.y))
                result.Add(index);
        }

        if (plan.PredictedFlightSeconds <= 0.01f ||
            plan.HorizontalTravel <= 0.01f)
        {
            return result;
        }

        float noPickupTravel = plan.MinimumHorizontalTravel > 0f
            ? plan.MinimumHorizontalTravel
            : plan.HorizontalTravel;
        result.UnionWith(
            GetTrajectorySphereHitIndicesAcrossSpeedScenarios(
                sphereObjectives,
                launchX,
                source.Top,
                speed,
                plan.HoldSeconds,
                plan.PredictedFlightSeconds,
                physics,
                spiritBoost,
                noPickupTravel));
        return result;
    }

    private static string DescribeObjectiveIds(
        IReadOnlyCollection<int> objectiveIds)
    {
        if (objectiveIds == null || objectiveIds.Count == 0)
            return "None";
        string summary = string.Join(",", objectiveIds.OrderBy(id => id).Take(12));
        return objectiveIds.Count > 12
            ? summary + $",+{objectiveIds.Count - 12}"
            : summary;
    }

    /// <summary>
    /// Bounded topology look-ahead for a prospective landing. This does not
    /// cache an action: it asks the same live selector whether the predicted
    /// support has at least one executable continuation, then the runtime will
    /// scan and solve again from the real landing. No visible successor is
    /// deliberately "unknown/accepted" so a finite scan horizon cannot freeze
    /// the runner. A visibly terminal support is ranked below a continuable
    /// one, but remains a fallback when every visible choice is terminal.
    /// </summary>
    private bool HasVerifiedLandingContinuation(
        BonusBoardSegment landedTarget,
        BonusJumpPlan incomingPlan,
        IReadOnlyList<BonusBoardSegment> forwardSurfaces,
        Vector3 currentPosition,
        Vector2 currentVelocity,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        int sectionIndex,
        bool allowRecoverableLowerFaceCatch,
        bool useFixedStepAlignedHolds,
        SpiritBoostRouteContext spiritBoost,
        int continuationDepth,
        out float worstLandingSafety,
        out string summary)
    {
        worstLandingSafety = float.NegativeInfinity;
        float travelOriginX =
            incomingPlan.PredictedLandingX -
            incomingPlan.HorizontalTravel;
        float minimumTravel = incomingPlan.MinimumHorizontalTravel > 0f
            ? incomingPlan.MinimumHorizontalTravel
            : incomingPlan.HorizontalTravel;
        float maximumTravel = incomingPlan.MaximumHorizontalTravel > 0f
            ? incomingPlan.MaximumHorizontalTravel
            : incomingPlan.HorizontalTravel;
        float slowLandingX = travelOriginX +
            Mathf.Min(minimumTravel, maximumTravel);
        float fastLandingX = travelOriginX +
            Mathf.Max(minimumTravel, maximumTravel);
        if (!float.IsFinite(travelOriginX) ||
            !float.IsFinite(slowLandingX) ||
            !float.IsFinite(fastLandingX))
        {
            summary = "Rejected:NonFiniteLanding";
            return false;
        }

        float currentSpeed = Mathf.Max(1f, Mathf.Abs(currentVelocity.x));
        float waitDistance = Mathf.Max(
            0f,
            incomingPlan.PlannedLaunchX - currentPosition.x);
        float waitSeconds = SolveHorizontalTravelTime(
            currentSpeed,
            waitDistance,
            4.0f,
            physics);
        float elapsedToLanding = Mathf.Max(
            0f,
            waitSeconds + incomingPlan.PredictedFlightSeconds);
        float projectedSlowSpeed = PredictHorizontalSpeedAtTime(
            currentSpeed,
            elapsedToLanding,
            physics);
        bool slowViable = EvaluateLandingContinuationEndpoint(
            landedTarget,
            forwardSurfaces,
            slowLandingX,
            projectedSlowSpeed,
            physics,
            hazard,
            sectionIndex,
            allowRecoverableLowerFaceCatch,
            useFixedStepAlignedHolds,
            spiritBoost,
            continuationDepth,
            "Slow",
            out float slowLandingSafety,
            out string slowSummary);

        bool requiresFastCheck =
            spiritBoost.Enabled &&
            spiritBoost.KinematicsAvailable &&
            (incomingPlan.FutureSpeedTransitionExpected ||
             spiritBoost.RequiresSpeedEnvelope);
        float projectedFastSpeed = projectedSlowSpeed;
        bool fastViable = true;
        float fastLandingSafety = slowLandingSafety;
        string fastSummary = "NotRequired";
        if (requiresFastCheck)
        {
            projectedFastSpeed = Mathf.Max(
                projectedSlowSpeed,
                spiritBoost.BaseHorizontalSpeed +
                    spiritBoost.MaximumBoostComponent);
            fastViable = EvaluateLandingContinuationEndpoint(
                landedTarget,
                forwardSurfaces,
                fastLandingX,
                projectedFastSpeed,
                physics,
                hazard,
                sectionIndex,
                allowRecoverableLowerFaceCatch,
                useFixedStepAlignedHolds,
                spiritBoost,
                continuationDepth,
                "Fast",
                out fastLandingSafety,
                out fastSummary);
        }

        bool viable = slowViable && fastViable;
        worstLandingSafety = viable
            ? Mathf.Min(slowLandingSafety, fastLandingSafety)
            : float.NegativeInfinity;
        summary =
            $"{(viable ? "Verified" : "Rejected")}:" +
            $"TravelOrigin={travelOriginX:F3},Elapsed={elapsedToLanding:F3}," +
            $"SlowEndpoint=({slowLandingX:F3},VX=" +
            $"{projectedSlowSpeed:F3},Safety={slowLandingSafety:F3})" +
            $"[{slowSummary}],FastEndpoint=({fastLandingX:F3},VX=" +
            $"{projectedFastSpeed:F3},Safety={fastLandingSafety:F3})" +
            $"[{fastSummary}],WorstSafety={worstLandingSafety:F3}";
        return viable;
    }

    private bool EvaluateLandingContinuationEndpoint(
        BonusBoardSegment landedTarget,
        IReadOnlyList<BonusBoardSegment> forwardSurfaces,
        float landingX,
        float projectedSpeed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        int sectionIndex,
        bool allowRecoverableLowerFaceCatch,
        bool useFixedStepAlignedHolds,
        SpiritBoostRouteContext spiritBoost,
        int continuationDepth,
        string speedLabel,
        out float landingSafety,
        out string summary)
    {
        landingSafety = 0f;
        // Limit the branch factor. Overlapping terrain that extends beyond the
        // landed support is a real continuation even when its Left begins
        // behind the predicted endpoint; filtering on Left discarded exactly
        // those lower-road continuations.
        BonusBoardSegment[] downstream = (forwardSurfaces ??
                Array.Empty<BonusBoardSegment>())
            .Where(candidate =>
                !SameSurfaceGeometry(candidate, landedTarget) &&
                candidate.Right >
                    Mathf.Max(landedTarget.Right + 0.10f, landingX + 0.25f) &&
                candidate.SafeRight > candidate.SafeLeft + 0.10f)
            .OrderBy(candidate =>
                Mathf.Max(0f, candidate.Left - landedTarget.Right))
            .ThenBy(candidate => candidate.Left)
            .ThenBy(candidate => candidate.Top)
            .Take(7)
            .ToArray();
        if (downstream.Length == 0)
        {
            summary = $"{speedLabel}:NoVisibleSuccessor";
            return true;
        }

        BonusBoardSegment next = downstream[0];
        BonusBoardScanResult continuationScan = new(
            true,
            landedTarget,
            true,
            next,
            Mathf.Max(0f, landedTarget.SafeRight - landingX),
            Mathf.Max(0f, next.Left - landedTarget.Right),
            next.Top - landedTarget.Top,
            "TopologyContinuation",
            false,
            default,
            downstream.Skip(1).ToArray());
        BonusHazard continuationHazard =
            hazard.IsValid && hazard.Right >= landingX - 0.50f
                ? hazard
                : default;
        SpiritBoostRouteContext continuationSpirit = spiritBoost;
        if (spiritBoost.Enabled)
        {
            BonusSpeedBoostTrigger[] remainingTriggers =
                (spiritBoost.ActiveTriggers ??
                    Array.Empty<BonusSpeedBoostTrigger>())
                .Where(trigger =>
                    trigger.IsValid &&
                    trigger.Right >=
                        landingX + spiritBoost.PlayerLeftOffset - 0.10f)
                .ToArray();
            continuationSpirit = spiritBoost with
            {
                ActiveTriggers = remainingTriggers,
                Evidence = spiritBoost.Evidence +
                    $";ContinuationFromX={landingX:F3}"
            };
        }

        bool viable = EvaluateContinuationAtSpeed(
            continuationScan,
            landingX,
            projectedSpeed,
            physics,
            continuationHazard,
            sectionIndex,
            allowRecoverableLowerFaceCatch,
            useFixedStepAlignedHolds,
            continuationSpirit,
            continuationDepth,
            speedLabel,
            out landingSafety,
            out string planSummary);
        summary =
            $"Next=[{next.Left:F3},{next.Right:F3}]@{next.Top:F3}," +
            planSummary;
        return viable;
    }

    private bool EvaluateContinuationAtSpeed(
        BonusBoardScanResult continuationScan,
        float landingX,
        float projectedSpeed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        int sectionIndex,
        bool allowRecoverableLowerFaceCatch,
        bool useFixedStepAlignedHolds,
        SpiritBoostRouteContext spiritBoost,
        int continuationDepth,
        string speedLabel,
        out float landingSafety,
        out string summary)
    {
        landingSafety = float.NegativeInfinity;
        float speed = Mathf.Max(1f, projectedSpeed);
        SpiritBoostRouteContext speedContext = spiritBoost.Enabled
            ? spiritBoost with
            {
                CurrentBoostComponent = Mathf.Max(
                    0f,
                    speed - spiritBoost.BaseHorizontalSpeed),
                NativeCurrentSpeed = speed,
                NativePreStepSpeed = speed
            }
            : spiritBoost;
        Vector3 projectedPosition = new(
            landingX,
            continuationScan.Current.Top,
            0f);
        Vector2 projectedVelocity = new(speed, 0f);
        BonusBoardScanResult selectedScan = SelectReachableRoute(
            continuationScan,
            projectedPosition,
            projectedVelocity,
            physics,
            hazard,
            Array.Empty<Vector2>(),
            sectionIndex,
            preferSphereCoverage: false,
            allowRecoverableLowerFaceCatch:
                allowRecoverableLowerFaceCatch,
            useFixedStepAlignedHolds: useFixedStepAlignedHolds,
            spiritBoost: speedContext,
            selectionContext: "TopologyContinuation",
            selection: out string routeSelection,
            selectedPlan: out BonusJumpPlan continuationPlan,
            selectedPlanAvailable: out bool planAvailable,
            continuationDepth: Mathf.Max(0, continuationDepth - 1));
        if (!planAvailable)
        {
            continuationPlan = Plan(
                selectedScan,
                projectedPosition,
                projectedVelocity,
                physics,
                hazard,
                Array.Empty<Vector2>(),
                sectionIndex: sectionIndex,
                preferSphereCoverage: false,
                allowRecoverableLowerFaceCatch:
                    allowRecoverableLowerFaceCatch,
                useFixedStepAlignedHolds: useFixedStepAlignedHolds,
                spiritBoost: speedContext,
                routeTargetIsAuthoritative: true);
        }

        bool viable = continuationPlan.IsValid ||
            continuationPlan.Reason == "IntentionalDrop" ||
            continuationPlan.Reason == "ContinuousSurface" ||
            continuationPlan.Reason == "WalkableMicroGap";
        if (viable)
        {
            bool stableLanding =
                continuationPlan.Maneuver ==
                    BonusManeuverKind.GroundJumpToLanding ||
                continuationPlan.Maneuver ==
                    BonusManeuverKind.SphereSweepToLowerLanding ||
                continuationPlan.Reason == "IntentionalDrop";
            if (stableLanding && selectedScan.HasNext)
            {
                float continuationLaunchX =
                    continuationPlan.PredictedLandingX -
                    continuationPlan.HorizontalTravel;
                bool calibrated = physics.TravelProfile.TryGetDuration(
                    continuationPlan.HoldSeconds,
                    selectedScan.Next.Top - selectedScan.Current.Top,
                    out _,
                    out int calibrationSamples);
                landingSafety = GetLandingFirstSafetyMargin(
                    continuationPlan,
                    selectedScan.Next,
                    continuationLaunchX,
                    speed,
                    physics,
                    calibrated,
                    calibrationSamples);
            }
            else
            {
                // Continuous road and wall-contact continuations are
                // executable but do not expose a stable landing corridor.
                // Keep them neutral rather than inventing infinite safety.
                landingSafety = 0f;
            }
        }
        string compactSelection = CompactDiagnostic(
            routeSelection,
            480);
        summary =
            $"{speedLabel}:{(viable ? "Executable" : "Terminal")}," +
            $"Plan={continuationPlan.Reason}/" +
            $"{continuationPlan.Maneuver},LandingSafety=" +
            $"{landingSafety:F3},Selection={compactSelection}";
        return viable;
    }

    private static Vector2[] GetObjectivesAttachedToTarget(
        IReadOnlyList<Vector2> sphereObjectives,
        BonusBoardSegment source,
        BonusBoardSegment target)
    {
        if (sphereObjectives == null || sphereObjectives.Count == 0 ||
            target.Width <= 0.05f)
        {
            return Array.Empty<Vector2>();
        }

        return sphereObjectives
            .Where(sphere =>
                sphere.x >= target.Left - 0.50f &&
                sphere.x <= target.Right + 0.75f &&
                sphere.y >= Mathf.Min(source.Top, target.Top) - 0.45f &&
                sphere.y <= Mathf.Max(source.Top, target.Top) + 4.25f)
            .ToArray();
    }

    private bool TrajectoryClearsIntermediateSurfaces(
        BonusBoardSegment source,
        BonusBoardSegment target,
        IReadOnlyList<BonusBoardSegment> forwardSurfaces,
        float launchX,
        float landingX,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        out string summary)
    {
        float trajectoryTravelScale =
            GetCalibratedHorizontalTravelScale(
                speed,
                flightSeconds,
                landingX - launchX,
                physics);
        float inferredHalfWidth = Mathf.Max(
            0.15f,
            source.SafeLeft - source.Left - 0.15f);
        StringBuilder checks = new();
        bool sourceLipSafe = TrajectoryClearsSourceLipOnDescent(
            source,
            target,
            launchX,
            speed,
            requestedHold,
            flightSeconds,
            physics,
            trajectoryTravelScale,
            inferredHalfWidth,
            out string sourceLipCheck);
        checks.Append(sourceLipCheck).Append(';');
        if (!sourceLipSafe)
        {
            summary = checks.ToString();
            return false;
        }
        foreach (BonusBoardSegment surface in forwardSurfaces
                     .Where(surface =>
                         !SameSurfaceGeometry(surface, source) &&
                         !SameSurfaceGeometry(surface, target) &&
                         surface.Left - inferredHalfWidth <
                            landingX - 0.02f &&
                         surface.Right + inferredHalfWidth >
                            launchX + 0.02f)
                     .OrderBy(surface => surface.Left))
        {
            float entryX = Mathf.Max(
                launchX + 0.02f,
                surface.Left - inferredHalfWidth);
            float exitX = Mathf.Min(
                landingX - 0.02f,
                surface.Right + inferredHalfWidth);
            if (exitX <= entryX)
                continue;

            float entryTime = SolveHorizontalTravelTime(
                speed,
                entryX - launchX,
                flightSeconds,
                physics,
                trajectoryTravelScale);
            float exitTime = SolveHorizontalTravelTime(
                speed,
                exitX - launchX,
                flightSeconds,
                physics,
                trajectoryTravelScale);
            if (entryTime > flightSeconds + 0.02f)
                continue;

            float entryFeetY = PredictVerticalYAtTime(
                source.Top,
                requestedHold,
                entryTime,
                physics);
            float exitFeetY = PredictVerticalYAtTime(
                source.Top,
                requestedHold,
                Mathf.Min(exitTime, flightSeconds),
                physics);
            float requiredFeetY = surface.Top + 0.10f;
            bool faceIntercept = entryFeetY < requiredFeetY;
            bool earlyTopLanding =
                !faceIntercept && exitFeetY < requiredFeetY;
            checks.Append(
                $"[{surface.Left:F2},{surface.Right:F2}]@" +
                $"{surface.Top:F2}:Tin={entryTime:F3}/" +
                $"Yin={entryFeetY:F2},Tout={exitTime:F3}/" +
                $"Yout={exitFeetY:F2},Need={requiredFeetY:F2}," +
                $"{(faceIntercept ? "FaceIntercept" :
                    earlyTopLanding ? "EarlyTopLanding" : "Clear")};");
            if (faceIntercept || earlyTopLanding)
            {
                summary = checks.ToString();
                return false;
            }
        }

        summary = checks.ToString();
        return true;
    }

    private bool TrajectoryClearsSourceLipOnDescent(
        BonusBoardSegment source,
        BonusBoardSegment target,
        float launchX,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        float trajectoryTravelScale,
        float inferredHalfWidth,
        out string summary)
    {
        if (target.Top >= source.Top - 0.35f)
        {
            summary = "SourceLip=NotLowerTarget";
            return true;
        }

        float crossingTime = FindDescendingSourceTopCrossingTime(
            requestedHold,
            flightSeconds,
            physics);
        if (crossingTime > flightSeconds + 0.001f)
        {
            summary = "SourceLip=AboveSourceUntilLanding";
            return true;
        }

        float crossingX = launchX +
            PredictHorizontalTravelAtTime(
                speed,
                crossingTime,
                physics) * trajectoryTravelScale;
        float requiredClearX =
            source.Right + inferredHalfWidth + 0.02f;
        bool clears = crossingX >= requiredClearX;
        summary =
            $"SourceLip[T={crossingTime:F3},X={crossingX:F3}," +
            $"NeedX={requiredClearX:F3},HalfWidth=" +
            $"{inferredHalfWidth:F3},Clear={clears}]";
        return clears;
    }

    private float FindDescendingSourceTopCrossingTime(
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics)
    {
        float apexTime =
            physics.InputDelaySeconds +
            Mathf.Min(
                requestedHold,
                physics.EffectiveHoldCapSeconds) +
            physics.JumpVelocity /
                Mathf.Max(5f, physics.GravityMagnitude);
        float high = Mathf.Max(apexTime, flightSeconds);
        if (PredictVerticalDisplacementAtTime(
                requestedHold,
                flightSeconds,
                physics) > 0.001f)
        {
            return flightSeconds + 0.01f;
        }

        float low = Mathf.Min(apexTime, flightSeconds);
        high = flightSeconds;
        for (int iteration = 0; iteration < 20; iteration++)
        {
            float midpoint = (low + high) * 0.5f;
            if (PredictVerticalDisplacementAtTime(
                    requestedHold,
                    midpoint,
                    physics) > 0f)
            {
                low = midpoint;
            }
            else
            {
                high = midpoint;
            }
        }
        return high;
    }

    private static bool SameSurfaceGeometry(
        BonusBoardSegment left,
        BonusBoardSegment right) =>
        Mathf.Abs(left.Left - right.Left) <= 0.12f &&
        Mathf.Abs(left.Right - right.Right) <= 0.12f &&
        Mathf.Abs(left.Top - right.Top) <= 0.12f;

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

    internal bool TryPlanAuthoredGround6EntryWallRoute(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float speed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        out BonusJumpPlan plan)
    {
        plan = default;
        if (!scan.IsValid ||
            !scan.HasNext ||
            !string.Equals(
                scan.Current.MapPieceName,
                "Ground 6",
                StringComparison.OrdinalIgnoreCase) ||
            scan.Current.StaticSurfaceIndex != 0 ||
            !string.Equals(
                scan.Next.MapPieceName,
                "Ground 6",
                StringComparison.OrdinalIgnoreCase) ||
            scan.Next.StaticSurfaceIndex != 3 ||
            scan.Current.MapPieceInstanceId == 0 ||
            scan.Current.MapPieceInstanceId !=
                scan.Next.MapPieceInstanceId ||
            scan.Next.Top < scan.Current.Top + 1.50f ||
            scan.Next.Top > scan.Current.Top + 2.50f ||
            scan.Next.Width > 2.50f)
        {
            return false;
        }

        BonusJumpPlan wallRoute = PlanWallDropApproach(
            scan,
            playerPosition,
            speed,
            physics,
            hazard,
            "AuthoredRoute=Ground6:S0->S3:NarrowPillarWallEntry");
        if (!wallRoute.IsValid)
            return false;

        plan = wallRoute with
        {
            Reason = "AuthoredGround6EntryWallRoute",
            CandidateSummary =
                wallRoute.CandidateSummary +
                " | StaticRoleContract=Ground6:S0->S3. The narrow raised " +
                "pillar is entered as a physical wall route at every speed; " +
                "sphere or landing ranking cannot replace it with a nominal " +
                "top landing."
        };
        return true;
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
        float wallContactX =
            scan.Next.Left - inferredHalfWidth;
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
        SpiritBoostRouteContext spiritBoost,
        bool allowRecoverableLowerFaceCatch,
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
        float horizontalTravelScale = Mathf.Clamp(
            physics.HorizontalTravelScale,
            0.75f,
            1.25f);
        float fallTravel = PredictHorizontalTravelAtTime(
                speed,
                fallSeconds,
                physics) *
            horizontalTravelScale;
        float predictedLandingX = unsupportedCenterX + fallTravel;
        bool integrateSpiritCoast =
            spiritBoost.Enabled &&
            spiritBoost.KinematicsAvailable &&
            spiritBoost.BaseHorizontalSpeed > 0.01f &&
            spiritBoost.BoostDecreasePerSecond > 0.01f;
        NaturalDropSpeedTrace noPickupTrace = default;
        if (integrateSpiritCoast)
        {
            // A Spirit speed tier decays while the player is still coasting
            // across the source. Starting the fall integral at the current
            // speed silently ignores that whole interval. Integrate from the
            // live X through the support edge so both the edge time and the
            // speed at the edge belong to the same X(t) model.
            noPickupTrace = BuildNaturalDropSpeedTrace(
                spiritBoost,
                playerPosition.x,
                scan.Current.Top,
                unsupportedCenterX,
                speed,
                fallSeconds,
                gravity,
                physics,
                allowTriggerPickups: false,
                worldTravelScale: horizontalTravelScale);
            predictedLandingX =
                playerPosition.x + noPickupTrace.TotalTravel;
        }
        float stableLeft = scan.Next.Left + inferredHalfWidth;
        float stableRight = scan.Next.Right - inferredHalfWidth;
        const float bodyFitTolerance = 0.10f;
        // At Spirit speed a left-face graze is not a stable landing from
        // which the prepared next wall action can execute. The V0.68 trace
        // accepted two such drops, rejected the prepared support at the real
        // contact, and died at the translated downstream wall. Ordinary mode
        // retains its mature recoverable-face behavior.
        bool allowDropFaceCatch =
            allowRecoverableLowerFaceCatch && !spiritBoost.Enabled;
        bool baselineSafe = IsNaturalDropLandingSafe(
            predictedLandingX,
            scan.Next,
            inferredHalfWidth,
            stableLeft,
            stableRight,
            bodyFitTolerance,
            allowDropFaceCatch,
            out bool stableBodyFits,
            out bool recoverableLeftFaceCatch,
            out float rawBodyOverlap);
        if (!baselineSafe)
            return false;

        float expectedLandingX = predictedLandingX;
        float minimumLandingX = predictedLandingX;
        float maximumLandingX = predictedLandingX;
        float minimumHorizontalTravel =
            predictedLandingX - playerPosition.x;
        float maximumHorizontalTravel = minimumHorizontalTravel;
        bool futureSpeedTransitionExpected = false;
        string speedEnvelope = spiritBoost.Enabled
            ? $"SpiritDropEnvelope=Inactive[{spiritBoost.Summary}]"
            : "SpiritDropEnvelope=Disabled";
        if (spiritBoost.RequiresSpeedEnvelope)
        {
            NaturalDropSpeedTrace pickupTrace =
                BuildNaturalDropSpeedTrace(
                    spiritBoost,
                    playerPosition.x,
                    scan.Current.Top,
                    unsupportedCenterX,
                    speed,
                    fallSeconds,
                    gravity,
                    physics,
                    allowTriggerPickups: true,
                    worldTravelScale: horizontalTravelScale);
            float pickupLandingX =
                playerPosition.x + pickupTrace.TotalTravel;
            bool pickupSafe = IsNaturalDropLandingSafe(
                pickupLandingX,
                scan.Next,
                inferredHalfWidth,
                stableLeft,
                stableRight,
                bodyFitTolerance,
                allowDropFaceCatch,
                out bool pickupStableBodyFits,
                out bool pickupRecoverableLeftFaceCatch,
                out float pickupRawBodyOverlap);
            if (!pickupSafe)
                return false;

            minimumLandingX = Mathf.Min(
                predictedLandingX,
                pickupLandingX);
            maximumLandingX = Mathf.Max(
                predictedLandingX,
                pickupLandingX);
            minimumHorizontalTravel =
                minimumLandingX - playerPosition.x;
            maximumHorizontalTravel =
                maximumLandingX - playerPosition.x;
            futureSpeedTransitionExpected =
                pickupTrace.TriggerHits > 0 ||
                spiritBoost.RequiresConservativeImmediateBoost;
            expectedLandingX = futureSpeedTransitionExpected
                ? pickupLandingX
                : predictedLandingX;
            speedEnvelope =
                $"SpiritDropEnvelope[Safe=True,NoPickupD=" +
                $"{noPickupTrace.TotalTravel:F3},PickupD=" +
                $"{pickupTrace.TotalTravel:F3},Delta=" +
                $"{pickupTrace.TotalTravel - noPickupTrace.TotalTravel:F3}," +
                $"Landings=[{minimumLandingX:F3}," +
                $"{maximumLandingX:F3}],Target=[{stableLeft:F3}," +
                $"{stableRight:F3}],PickupOutcome=" +
                $"{(pickupStableBodyFits ? "StableTop" :
                    pickupRecoverableLeftFaceCatch
                        ? "RecoverableLeftFaceCatch"
                        : "Rejected")},PickupOverlap=" +
                $"{pickupRawBodyOverlap:F3},Hits=" +
                $"{pickupTrace.TriggerHits}:" +
                $"{pickupTrace.TriggerSignature},NoPickupEdgeT=" +
                $"{noPickupTrace.EdgeSeconds:F3},PickupEdgeT=" +
                $"{pickupTrace.EdgeSeconds:F3}]";
        }
        else if (integrateSpiritCoast)
        {
            speedEnvelope =
                $"SpiritDropIntegrated[NoPendingTrigger,Travel=" +
                $"{noPickupTrace.TotalTravel:F3},EdgeT=" +
                $"{noPickupTrace.EdgeSeconds:F3},Landing=" +
                $"{predictedLandingX:F3}]";
        }

        string summary =
            $"NaturalDrop SourceEdge={scan.Current.Right:F3}," +
            $"HalfWidth={inferredHalfWidth:F3}," +
            $"UnsupportedCenter={unsupportedCenterX:F3}," +
            $"Drop={dropHeight:F3},Fall={fallSeconds:F3}s," +
            $"FallTravel={fallTravel:F3},TravelScale=" +
            $"{horizontalTravelScale:F3},Landing={predictedLandingX:F3}," +
            $"StableTarget=[{stableLeft:F3},{stableRight:F3}]," +
            $"RawBodyOverlap={rawBodyOverlap:F3},Outcome=" +
            $"{(stableBodyFits ? "StableTop" : "RecoverableLeftFaceCatch")}," +
            $"Gap={scan.Gap:F3},DeltaY={scan.HeightDelta:F3}," +
            $"{speedEnvelope}";
        plan = new BonusJumpPlan(
            false,
            false,
            0f,
            fallSeconds,
            expectedLandingX - playerPosition.x,
            unsupportedCenterX,
            expectedLandingX,
            scan.Current.SafeLeft,
            scan.Current.SafeRight,
            "IntentionalDrop",
            summary,
            BonusManeuverKind.CoastToLowerLanding,
            0,
            minimumHorizontalTravel,
            maximumHorizontalTravel,
            futureSpeedTransitionExpected);
        return true;
    }

    private static bool IsNaturalDropLandingSafe(
        float landingX,
        BonusBoardSegment target,
        float inferredHalfWidth,
        float stableLeft,
        float stableRight,
        float bodyFitTolerance,
        bool allowRecoverableLowerFaceCatch,
        out bool stableBodyFits,
        out bool recoverableLeftFaceCatch,
        out float rawBodyOverlap)
    {
        stableBodyFits =
            stableRight >= stableLeft &&
            landingX >= stableLeft - bodyFitTolerance &&
            landingX <= stableRight + bodyFitTolerance;
        rawBodyOverlap = Mathf.Max(
            0f,
            Mathf.Min(
                landingX + inferredHalfWidth,
                target.Right) -
            Mathf.Max(
                landingX - inferredHalfWidth,
                target.Left));
        recoverableLeftFaceCatch =
            allowRecoverableLowerFaceCatch &&
            !stableBodyFits &&
            landingX < stableLeft - bodyFitTolerance &&
            landingX >= target.Left - inferredHalfWidth - 0.10f &&
            rawBodyOverlap >= 0.18f;
        return stableBodyFits || recoverableLeftFaceCatch;
    }

    private bool TryPlanSphereSweepToLowerLanding(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float speed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        IReadOnlyList<Vector2> sphereObjectives,
        BonusJumpPlan naturalDrop,
        IReadOnlyList<float> holdCandidates,
        SpiritBoostRouteContext spiritBoost,
        out BonusJumpPlan plan,
        out string rejectionSummary)
    {
        plan = default;
        rejectionSummary = "EligibilityNotMet";
        if (!scan.HasNext ||
            scan.HeightDelta >= -0.35f ||
            speed < 1f ||
            sphereObjectives == null ||
            sphereObjectives.Count == 0 ||
            playerPosition.x < scan.Current.Left - 0.12f ||
            playerPosition.x > scan.Current.SafeRight +
                LandingRecoveryTolerance)
        {
            return false;
        }

        // Use the same physical pickup envelope everywhere. A separate 1.15
        // threshold disagreed with the micro-gap and trajectory scorers and
        // could turn an ordinary grounded pickup into a risky airborne plan.
        Vector2[] elevatedAhead = sphereObjectives
            .Where(sphere =>
                sphere.x >= playerPosition.x - TriggerTolerance &&
                sphere.x <= scan.Current.Right + 0.40f &&
                !TrajectoryBodyOverlapsSphere(
                    scan.Current.Top,
                    sphere.y))
            .OrderBy(sphere => sphere.x)
            .ThenBy(sphere => sphere.y)
            .Take(48)
            .ToArray();
        if (elevatedAhead.Length == 0)
        {
            rejectionSummary = "NoElevatedSphereObjective";
            return false;
        }

        BonusJumpPlan best = default;
        int bestHits = -1;
        float bestHold = float.PositiveInfinity;
        float bestFlight = float.PositiveInfinity;
        float bestLandingMargin = float.NegativeInfinity;
        float naturalMinimumTravel =
            naturalDrop.MinimumHorizontalTravel > 0f
                ? naturalDrop.MinimumHorizontalTravel
                : naturalDrop.HorizontalTravel;
        float naturalMaximumTravel =
            naturalDrop.MaximumHorizontalTravel > 0f
                ? naturalDrop.MaximumHorizontalTravel
                : naturalDrop.HorizontalTravel;
        float naturalEnvelopeLeft = playerPosition.x + Mathf.Min(
            naturalMinimumTravel,
            naturalMaximumTravel);
        float naturalEnvelopeRight = playerPosition.x + Mathf.Max(
            naturalMinimumTravel,
            naturalMaximumTravel);
        float naturalLandingMargin = Mathf.Min(
            naturalEnvelopeLeft - scan.Next.SafeLeft,
            scan.Next.SafeRight - naturalEnvelopeRight);
        // A soul sweep is optional. It may only replace the no-input baseline
        // when it retains essentially the same landing reserve; otherwise the
        // safe drop wins even if the jump intersects more objectives.
        float requiredSweepLandingMargin = Mathf.Max(
            0.20f,
            naturalLandingMargin - 0.08f);
        StringBuilder evaluations = new();
        evaluations.Append(
            $"SphereSweepToLowerLanding[Objectives=" +
            $"{elevatedAhead.Length},LaunchX={playerPosition.x:F3}," +
            $"Source=[{scan.Current.Left:F3},{scan.Current.Right:F3}]" +
            $"@{scan.Current.Top:F3},Target=[{scan.Next.SafeLeft:F3}," +
            $"{scan.Next.SafeRight:F3}]@{scan.Next.Top:F3}," +
            $"NaturalBaseline={naturalDrop.CandidateSummary}] | ");

        foreach (float hold in holdCandidates)
        {
            if (hold > physics.EffectiveHoldCapSeconds + 0.001f ||
                !TryPredictFlightTime(
                    hold,
                    scan.HeightDelta,
                    physics,
                    out float flightSeconds,
                    out float effectiveHold,
                    out float maximumRise))
            {
                AppendEvaluation(
                    evaluations,
                    $"H={hold:F3}:VerticalReject");
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
                speed,
                flightSeconds,
                hold,
                scan.HeightDelta,
                physics,
                out string travelSource);
            travel = ApplyLandingBias(
                travel,
                scan.HeightDelta,
                hold,
                physics,
                ref travelSource);
            float landingX = playerPosition.x + travel;
            bool insideVerifiedTarget =
                landingX >= scan.Next.SafeLeft - 0.05f &&
                landingX <= scan.Next.SafeRight + 0.05f;
            if (!insideVerifiedTarget)
            {
                AppendEvaluation(
                    evaluations,
                    $"H={hold:F3}:EH={effectiveHold:F3}," +
                    $"MaxRise={maximumRise:F3},T={flightSeconds:F3}," +
                    $"D={travel:F3},X={landingX:F3}," +
                    "RejectedOutsideVerifiedTarget");
                continue;
            }

            bool trajectorySafe = TrajectoryClearsHazard(
                hazard,
                playerPosition.x,
                landingX,
                scan.Current.Top,
                speed,
                hold,
                flightSeconds,
                physics,
                out string hazardCheck);
            bool targetFaceSafe = TrajectoryClearsRaisedTargetFace(
                scan.Current,
                scan.Next,
                playerPosition.x,
                speed,
                travel,
                hold,
                flightSeconds,
                physics,
                out string targetFaceCheck);
            BonusBoardSegment[] intermediateSurfaces =
                GetIntermediateClearanceSurfaces(scan);
            bool intermediateSafe =
                TrajectoryClearsIntermediateSurfaces(
                    scan.Current,
                    scan.Next,
                    intermediateSurfaces,
                    playerPosition.x,
                    landingX,
                    speed,
                    hold,
                    flightSeconds,
                    physics,
                    out string intermediateCheck);
            if (!trajectorySafe || !targetFaceSafe || !intermediateSafe)
            {
                AppendEvaluation(
                    evaluations,
                    $"H={hold:F3}:X={landingX:F3}," +
                    $"RejectedTrajectory[{hazardCheck};" +
                    $"{targetFaceCheck};{intermediateCheck}]");
                continue;
            }

            float noPickupTravel = travel;
            float minimumTravel = travel;
            float maximumTravel = travel;
            bool futureSpeedTransitionExpected = false;
            string speedEnvelopeCheck =
                "SpiritEnvelope=NotRequired";
            if (spiritBoost.RequiresSpeedEnvelope)
            {
                SpeedEnvelopeEvaluation envelope =
                    EvaluateSpiritBoostTrajectoryEnvelope(
                        scan.Current,
                        scan.Next,
                        intermediateSurfaces,
                        hazard,
                        spiritBoost,
                        playerPosition.x,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        travel,
                        useRawTargetBounds: false);
                if (!envelope.IsSafe)
                {
                    AppendEvaluation(
                        evaluations,
                        $"H={hold:F3}:RejectedBySpeedEnvelope[" +
                        $"{envelope.Summary}]");
                    continue;
                }

                travel = envelope.ExpectedTravel;
                minimumTravel = envelope.MinimumTravel;
                maximumTravel = envelope.MaximumTravel;
                landingX = playerPosition.x + travel;
                futureSpeedTransitionExpected =
                    spiritBoost.RequiresConservativeImmediateBoost ||
                    envelope.TriggerHits > 0;
                speedEnvelopeCheck = envelope.Summary;
            }

            float envelopeLandingLeft =
                playerPosition.x + Mathf.Min(minimumTravel, maximumTravel);
            float envelopeLandingRight =
                playerPosition.x + Mathf.Max(minimumTravel, maximumTravel);
            float landingMargin = Mathf.Min(
                envelopeLandingLeft - scan.Next.SafeLeft,
                scan.Next.SafeRight - envelopeLandingRight);
            if (landingMargin < requiredSweepLandingMargin)
            {
                AppendEvaluation(
                    evaluations,
                    $"H={hold:F3}:RejectedByLandingPriority," +
                    $"Envelope=[{envelopeLandingLeft:F3}," +
                    $"{envelopeLandingRight:F3}],Margin=" +
                    $"{landingMargin:F3},NaturalMargin=" +
                    $"{naturalLandingMargin:F3},Required=" +
                    $"{requiredSweepLandingMargin:F3}");
                continue;
            }

            int hits = CountTrajectorySphereHitsAcrossSpeedScenarios(
                elevatedAhead,
                playerPosition.x,
                scan.Current.Top,
                speed,
                hold,
                flightSeconds,
                physics,
                spiritBoost,
                noPickupTravel);
            AppendEvaluation(
                evaluations,
                $"H={hold:F3}:EH={effectiveHold:F3}," +
                $"MaxRise={maximumRise:F3},T={flightSeconds:F3}," +
                $"D={travel:F3},X={landingX:F3},Hits={hits}," +
                $"Flight={flightSource},Travel={travelSource}," +
                $"{hazardCheck};{targetFaceCheck};" +
                $"{intermediateCheck};{speedEnvelopeCheck}");
            if (hits <= 0)
                continue;

            // Landing reserve is primary. Pickup count and action size only
            // break a near tie between trajectories already comparable to the
            // passive baseline.
            bool better =
                landingMargin > bestLandingMargin + 0.08f ||
                Mathf.Abs(landingMargin - bestLandingMargin) <= 0.08f &&
                (hits > bestHits ||
                 hits == bestHits &&
                 (hold < bestHold - 0.0005f ||
                  Mathf.Abs(hold - bestHold) <= 0.0005f &&
                  flightSeconds < bestFlight - 0.0005f));
            if (!better)
                continue;

            bestHits = hits;
            bestHold = hold;
            bestFlight = flightSeconds;
            bestLandingMargin = landingMargin;
            best = new BonusJumpPlan(
                true,
                true,
                hold,
                flightSeconds,
                travel,
                playerPosition.x,
                landingX,
                playerPosition.x - TriggerTolerance,
                playerPosition.x + TriggerTolerance,
                "SphereSweepToLowerLanding",
                string.Empty,
                BonusManeuverKind.SphereSweepToLowerLanding,
                hits,
                minimumTravel,
                maximumTravel,
                futureSpeedTransitionExpected);
        }

        if (!best.IsValid)
        {
            rejectionSummary = evaluations.ToString();
            return false;
        }

        plan = best with
        {
            CandidateSummary = evaluations +
                $" | Selected[H={best.HoldSeconds:F3}," +
                $"Landing={best.PredictedLandingX:F3}," +
                $"ExpectedHits={best.ExpectedSphereHits}," +
                $"LandingMargin={bestLandingMargin:F3}," +
                $"NaturalMargin={naturalLandingMargin:F3}]"
        };
        rejectionSummary = "SelectedSafeSweep";
        return true;
    }

    private BonusJumpPlan PlanWallApproach(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float speed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        string directEvaluation,
        SpiritBoostRouteContext spiritBoost,
        bool preferLowerTrenchContact = false,
        bool requireBelowSourceContact = false,
        float triggerTolerance = TriggerTolerance,
        bool compensateInputCommit = false)
    {
        bool spiritBoostPassThroughWall =
            spiritBoost.Enabled &&
            scan.Reason.StartsWith(
                "SpiritBoostTransientLandingToWallContinuation",
                StringComparison.Ordinal);
        bool requiresVerifiedPickupForReach =
            string.Equals(
                scan.Reason,
                "SpiritBoostTransientLandingToWallContinuationPendingPickup",
                StringComparison.Ordinal);
        // SafeLeft is inset by player half-width plus the scanner's 0.15-unit
        // edge margin. Recover the half-width so the planned centre stops at
        // the wall face rather than pretending it can land on the top.
        float inferredHalfWidth = Mathf.Max(
            0.15f,
            scan.Next.SafeLeft - scan.Next.Left - 0.15f);
        float physicalWallFaceX =
            spiritBoostPassThroughWall &&
            float.IsFinite(scan.Next.WallFaceX)
                ? scan.Next.WallFaceX
                : scan.Next.Left;
        float wallContactX =
            physicalWallFaceX - inferredHalfWidth;
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
        float maximumContactSeconds = spiritBoostPassThroughWall
            ? 1.20f
            : requireBelowSourceContact
            ? TrenchEntryMaximumContactSeconds
            : WallApproachEarlyContactSeconds;
        float preferredContactSeconds = spiritBoostPassThroughWall
            ? 0.78f
            : requireBelowSourceContact
            ? 0.52f
            : WallApproachPreferredContactSeconds;
        float geometryContactStart = spiritBoostPassThroughWall
            ? 0.04f
            : Mathf.Max(
                0.04f,
                SolveWallContactTravelTime(
                    speed,
                    distanceFromLatestLaunch));
        float geometryContactEnd = spiritBoostPassThroughWall
            ? maximumContactSeconds
            : Mathf.Min(
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
        float minimumFaceClearance = spiritBoostPassThroughWall
            ? Mathf.Max(1.65f, scan.HeightDelta - 5.75f)
            : requireBelowSourceContact
            ? TrenchEntryMinimumFaceClearance
            : preferLowerTrenchContact
                ? 2.25f
                : 0.12f;
        float maximumFaceClearance = spiritBoostPassThroughWall
            ? scan.HeightDelta + 0.45f
            : requireBelowSourceContact
            ? Mathf.Min(
                TrenchEntryMaximumFaceClearance,
                scan.HeightDelta - 0.10f)
            : preferLowerTrenchContact
                ? Mathf.Min(4.75f, scan.HeightDelta - 0.55f)
                : 1.65f;
        float preferredFaceClearance = spiritBoostPassThroughWall
            ? Mathf.Clamp(
                3.85f,
                minimumFaceClearance,
                maximumFaceClearance)
            : requireBelowSourceContact
            ? Mathf.Clamp(
                TrenchEntryPreferredFaceClearance,
                minimumFaceClearance,
                maximumFaceClearance)
            : preferLowerTrenchContact
                ? Mathf.Clamp(scan.HeightDelta * 0.58f, 3.10f, 3.85f)
                : 0.45f;
        if (spiritBoost.Enabled)
        {
            return PlanSpiritBoostWallApproach(
                scan,
                playerPosition,
                speed,
                physics,
                hazard,
                directEvaluation,
                spiritBoost,
                wallContactX,
                inferredHalfWidth,
                minimumFaceClearance,
                maximumFaceClearance,
                preferredFaceClearance,
                preferredContactSeconds,
                maximumContactSeconds,
                preferLowerTrenchContact,
                requireBelowSourceContact,
                requiresVerifiedPickupForReach,
                compensateInputCommit);
        }
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
            // Keep the authored deep-entry band for an on-time launch: it
            // deliberately sends the body low enough to collect the trench
            // lane.  If a prior landing puts the runner beyond that launch
            // window, however, requiring the same narrow band turns a still
            // reachable physical wall face into an invalid Wait and causes a
            // guaranteed fall.  Relax only this missed-window salvage to any
            // safe below-lip body contact on the verified face.
            float salvageMinimumFaceClearance =
                requireBelowSourceContact
                    ? 0.75f
                    : minimumFaceClearance;
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
                    candidateClearance >= salvageMinimumFaceClearance &&
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
                    speed,
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
                $"{scan.Next.Top - salvageMinimumFaceClearance:F2}]," +
                $"NominalContactBandFeet=" +
                $"[{scan.Next.Top - maximumFaceClearance:F2}," +
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
                ? "LateBelowLipPhysicalFaceRecovery"
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
                ? "LateDeepTrenchPhysicalFaceSalvage"
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
            playerPosition.x >= plannedLaunchX - triggerTolerance &&
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
            speed,
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

    /// <summary>
    /// Solves a wall approach against the same piecewise Spirit-Boost X(t)
    /// model used by landing, hazard and pickup checks. The command is valid
    /// only when both the no-pickup trace and the pickup/reset trace first
    /// reach the physical face inside the same vertical contact band.
    /// </summary>
    private BonusJumpPlan PlanSpiritBoostWallApproach(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float speed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        string directEvaluation,
        SpiritBoostRouteContext spiritBoost,
        float wallContactX,
        float inferredHalfWidth,
        float minimumFaceClearance,
        float maximumFaceClearance,
        float preferredFaceClearance,
        float preferredContactSeconds,
        float maximumContactSeconds,
        bool preferLowerTrenchContact,
        bool requireBelowSourceContact,
        bool requiresVerifiedPickupForReach,
        bool compensateInputCommit)
    {
        if (!spiritBoost.KinematicsAvailable)
        {
            return Invalid(
                "SpiritWallKinematicsUnavailable",
                directEvaluation +
                $" | SpiritWallEnvelopeRejected[{spiritBoost.Summary}]");
        }

        float futureLeft = Mathf.Max(
            playerPosition.x,
            scan.Current.SafeLeft);
        float futureRight = scan.Current.SafeRight;
        bool liveCentreOnSource =
            playerPosition.x >= scan.Current.Left - 0.12f &&
            playerPosition.x <= scan.Current.Right + 0.12f;
        bool missedPreferredSourceWindow =
            futureLeft > futureRight + 0.001f;
        float acceptedMinimumClearance =
            requireBelowSourceContact && missedPreferredSourceWindow
                ? 0.75f
                : minimumFaceClearance;
        if (missedPreferredSourceWindow)
        {
            if (!liveCentreOnSource ||
                playerPosition.x >= wallContactX - 0.02f)
            {
                return Invalid(
                    "MissedSpiritWallApproachLaunchWindow",
                    directEvaluation +
                    $" | SpiritWallEnvelopeRejected[Now=" +
                    $"{playerPosition.x:F3},Source=" +
                    $"[{scan.Current.Left:F3},{scan.Current.Right:F3}]," +
                    $"WallX={wallContactX:F3}]");
            }

            futureLeft = playerPosition.x;
            futureRight = playerPosition.x;
        }

        float worldTravelScale = Mathf.Clamp(
            physics.HorizontalTravelScale,
            0.75f,
            1.25f);
        float inputCommitTravel = compensateInputCommit
            ? PredictHorizontalTravelAtTime(
                speed,
                Mathf.Clamp(physics.FixedDeltaTime, 0.005f, 0.05f),
                physics)
            : 0f;
        float candidateWidth = Mathf.Max(0f, futureRight - futureLeft);
        int launchSamples = Mathf.Clamp(
            Mathf.CeilToInt(candidateWidth / 0.08f),
            1,
            40);
        float bestScore = float.PositiveInfinity;
        float bestLaunchX = 0f;
        float bestHold = 0f;
        SpiritWallContactOutcome bestNoPickup = default;
        SpiritWallContactOutcome bestPickup = default;
        int evaluatedCandidates = 0;
        int rejectedReach = 0;
        int rejectedFace = 0;
        int rejectedIntermediate = 0;
        int rejectedHazard = 0;
        BonusBoardSegment[] intermediateSurfaces =
            GetIntermediateClearanceSurfaces(scan);
        IReadOnlyList<float> wallHoldCandidates =
            requiresVerifiedPickupForReach
                ? FixedStepAlignedHoldCandidates
                : WallApproachHoldCandidates;

        // Index -1 is the exact live transform. Future samples are only used
        // to describe where to wait; every physics callback re-solves the
        // exact live X before an irreversible DOWN is allowed.
        for (int launchIndex = -1;
             launchIndex <= launchSamples;
             launchIndex++)
        {
            float launchX = launchIndex < 0
                ? playerPosition.x
                : Mathf.Lerp(
                    futureLeft,
                    futureRight,
                    launchIndex / (float)launchSamples);
            bool exactLiveCandidate = launchIndex < 0;
            if (exactLiveCandidate &&
                (!liveCentreOnSource ||
                 launchX < futureLeft - 0.001f ||
                 launchX > futureRight + 0.001f))
            {
                continue;
            }
            if (!exactLiveCandidate &&
                Mathf.Abs(launchX - playerPosition.x) <= 0.002f)
            {
                continue;
            }
            if (launchX >= wallContactX - 0.02f)
                continue;
            float effectiveTakeoffX = launchX + inputCommitTravel;
            if (effectiveTakeoffX >= wallContactX - 0.02f)
                continue;

            foreach (float hold in wallHoldCandidates)
            {
                if (hold > physics.EffectiveHoldCapSeconds + 0.001f)
                    continue;

                evaluatedCandidates++;
                SpiritWallContactOutcome noPickup =
                    SimulateSpiritWallContact(
                        spiritBoost,
                        effectiveTakeoffX,
                        scan.Current.Top,
                        speed,
                        hold,
                        wallContactX,
                        maximumContactSeconds,
                        physics,
                        hazard,
                        allowTriggerPickups: false,
                        worldTravelScale);
                SpiritWallContactOutcome pickup =
                    spiritBoost.RequiresSpeedEnvelope
                        ? SimulateSpiritWallContact(
                            spiritBoost,
                            effectiveTakeoffX,
                            scan.Current.Top,
                            speed,
                            hold,
                            wallContactX,
                            maximumContactSeconds,
                            physics,
                            hazard,
                            allowTriggerPickups: true,
                            worldTravelScale)
                        : noPickup;
                bool verifiedPickupReachesFace =
                    requiresVerifiedPickupForReach &&
                    pickup.ReachedFace &&
                    pickup.TriggerHits > 0;
                if (!pickup.ReachedFace ||
                    !verifiedPickupReachesFace &&
                    !noPickup.ReachedFace)
                {
                    rejectedReach++;
                    continue;
                }

                float noPickupClearance =
                    scan.Next.Top - noPickup.ContactFeetY;
                float pickupClearance =
                    scan.Next.Top - pickup.ContactFeetY;
                bool bothHitFaceBand =
                    (requiresVerifiedPickupForReach ||
                     noPickupClearance >= acceptedMinimumClearance &&
                     noPickupClearance <= maximumFaceClearance) &&
                    pickupClearance >= acceptedMinimumClearance &&
                    pickupClearance <= maximumFaceClearance;
                if (!bothHitFaceBand)
                {
                    rejectedFace++;
                    continue;
                }
                SpiritBoostTrajectoryTrace pickupTrace =
                    BuildSpiritBoostTrajectoryTrace(
                        spiritBoost,
                        effectiveTakeoffX,
                        scan.Current.Top,
                        speed,
                        hold,
                        pickup.ContactSeconds,
                        physics,
                        allowTriggerPickups: true,
                        worldTravelScale);
                bool pickupClearsIntermediate =
                    TrajectoryClearsIntermediateSurfacesWithSpiritBoost(
                        scan.Current,
                        scan.Next,
                        intermediateSurfaces,
                        effectiveTakeoffX,
                        wallContactX,
                        speed,
                        hold,
                        pickup.ContactSeconds,
                        physics,
                        pickupTrace,
                        out string pickupIntermediateCheck);
                bool noPickupClearsIntermediate = true;
                string noPickupIntermediateCheck =
                    "VerifiedPickupRouteDoesNotRequireCounterfactualWallReach";
                if (!requiresVerifiedPickupForReach)
                {
                    SpiritBoostTrajectoryTrace noPickupTrace =
                        BuildSpiritBoostTrajectoryTrace(
                            spiritBoost,
                            effectiveTakeoffX,
                            scan.Current.Top,
                            speed,
                            hold,
                            noPickup.ContactSeconds,
                            physics,
                            allowTriggerPickups: false,
                            worldTravelScale);
                    noPickupClearsIntermediate =
                        TrajectoryClearsIntermediateSurfacesWithSpiritBoost(
                            scan.Current,
                            scan.Next,
                            intermediateSurfaces,
                            effectiveTakeoffX,
                            wallContactX,
                            speed,
                            hold,
                            noPickup.ContactSeconds,
                            physics,
                            noPickupTrace,
                            out noPickupIntermediateCheck);
                }
                if (!noPickupClearsIntermediate ||
                    !pickupClearsIntermediate)
                {
                    rejectedIntermediate++;
                    continue;
                }
                if ((!requiresVerifiedPickupForReach &&
                     !noPickup.HazardSafe) ||
                    !pickup.HazardSafe)
                {
                    rejectedHazard++;
                    continue;
                }

                float worstClearanceError = Mathf.Max(
                    requiresVerifiedPickupForReach
                        ? 0f
                        : Mathf.Abs(
                            noPickupClearance - preferredFaceClearance),
                    Mathf.Abs(
                        pickupClearance - preferredFaceClearance));
                float worstTimingError = Mathf.Max(
                    requiresVerifiedPickupForReach
                        ? 0f
                        : Mathf.Abs(
                            noPickup.ContactSeconds -
                            preferredContactSeconds),
                    Mathf.Abs(
                        pickup.ContactSeconds -
                        preferredContactSeconds));
                float waitDistance = Mathf.Max(
                    0f,
                    launchX - playerPosition.x);
                float score =
                    (exactLiveCandidate ? 0f : 2f) +
                    worstClearanceError +
                    worstTimingError * 0.08f +
                    waitDistance * 0.02f +
                    hold * 0.015f;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestLaunchX = launchX;
                bestHold = hold;
                bestNoPickup = noPickup;
                bestPickup = pickup;
            }

            // Future launch samples carry a fixed +2 score penalty and all
            // accepted face-band errors are smaller than that tier. Once the
            // exact live DOWN position has a proved command it is therefore
            // already the lexicographic winner; scanning up to forty future
            // launch positions can only repeat expensive slow/fast traces.
            if (exactLiveCandidate && bestHold > 0f)
                break;
        }

        if (bestHold <= 0f)
        {
            return Invalid(
                missedPreferredSourceWindow
                    ? "MissedSpiritWallApproachLaunchWindow"
                    : "NoSpiritWallFaceEnvelope",
                directEvaluation +
                $" | SpiritWallEnvelopeRejected[WallX=" +
                $"{wallContactX:F3},LaunchRange=" +
                $"[{futureLeft:F3},{futureRight:F3}],ContactBand=" +
                $"[{acceptedMinimumClearance:F3}," +
                $"{maximumFaceClearance:F3}],Candidates=" +
                $"{evaluatedCandidates},ReachReject={rejectedReach}," +
                $"FaceReject={rejectedFace},HazardReject=" +
                $"{rejectedHazard},IntermediateReject=" +
                $"{rejectedIntermediate},Scale={worldTravelScale:F3}," +
                $"Context={spiritBoost.Summary}]");
        }

        bool shouldJump =
            Mathf.Abs(bestLaunchX - playerPosition.x) <= 0.002f;
        bool pickupTransitionExpected =
            requiresVerifiedPickupForReach ||
            bestPickup.TriggerHits > 0 ||
            spiritBoost.RequiresConservativeImmediateBoost;
        SpiritWallContactOutcome expected = pickupTransitionExpected
            ? bestPickup
            : bestNoPickup;
        float travel = Mathf.Max(0f, wallContactX - bestLaunchX);
        TryPredictFlightTime(
            bestHold,
            0f,
            physics,
            out _,
            out float effectiveHold,
            out float maximumRise);
        string profile = requireBelowSourceContact
            ? missedPreferredSourceWindow
                ? "LateBelowLipPhysicalFaceRecovery"
                : "DeepTrenchEntry"
            : preferLowerTrenchContact
                ? "LowerTrenchEntry"
                : "UpperFaceContact";
        string action = shouldJump ? "Jump" : "Wait";
        string summary = directEvaluation +
            $" | SpiritWallEnvelope[Safe=True,Profile={profile}," +
            $"WallX={wallContactX:F3},HalfWidth=" +
            $"{inferredHalfWidth:F3},Launch={bestLaunchX:F3}," +
            $"InputCommitTravel={inputCommitTravel:F3}," +
            $"Now={playerPosition.x:F3},Action={action}," +
            $"H={bestHold:F3},EH={effectiveHold:F3}," +
            $"MaxRise={maximumRise:F3},ContactBand=" +
            $"[{acceptedMinimumClearance:F3}," +
            $"{maximumFaceClearance:F3}],VerifiedPickupRequired=" +
            $"{requiresVerifiedPickupForReach},NoPickup=" +
            $"{bestNoPickup.Summary},Pickup={bestPickup.Summary}," +
            $"Candidates={evaluatedCandidates},Scale=" +
            $"{worldTravelScale:F3},Context={spiritBoost.Summary}]";
        string reason = shouldJump
            ? requireBelowSourceContact
                ? missedPreferredSourceWindow
                    ? "LateDeepTrenchPhysicalFaceSalvage"
                    : "DeepTrenchEntryContact"
                : preferLowerTrenchContact
                    ? "TrenchEntryContact"
                    : "WallApproachContact"
            : requireBelowSourceContact
                ? "ApproachingDeepTrenchEntry"
                : preferLowerTrenchContact
                    ? "ApproachingTrenchEntry"
                    : "ApproachingWallContact";

        return new BonusJumpPlan(
            true,
            shouldJump,
            bestHold,
            expected.ContactSeconds,
            travel,
            bestLaunchX,
            wallContactX,
            bestLaunchX,
            bestLaunchX,
            reason,
            summary,
            BonusManeuverKind.ApproachJumpThenWallJump,
            0,
            travel,
            travel,
            pickupTransitionExpected);
    }

    private bool TryPlanSameSurfaceSphereCollection(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        Vector2 playerVelocity,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        IReadOnlyList<Vector2> sphereObjectives,
        IReadOnlyList<float> holdCandidates,
        bool preferComfortableSphereCoverage,
        SpiritBoostRouteContext spiritBoost,
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
                !TrajectoryBodyOverlapsSphere(
                    scan.Current.Top,
                    sphere.y))
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
        BonusBoardSegment collectionTarget = scan.Current with
        {
            SafeLeft = scan.Current.SafeLeft,
            SafeRight = landingRightLimit
        };

        BonusJumpPlan best = default;
        float bestScore = float.NegativeInfinity;
        float bestLandingSafety = float.NegativeInfinity;
        int bestRankingSphereHits = -1;
        int bestRankingSpeedBoostHits = -1;
        StringBuilder evaluations = new();
        evaluations.Append(
            $"SameSurfaceSphereObjective[Count={elevatedAhead.Length}," +
            $"Current=[{scan.Current.SafeLeft:F2},{scan.Current.SafeRight:F2}]" +
            $"@{scan.Current.Top:F2},ComfortableCoveragePriority=" +
            $"{preferComfortableSphereCoverage}] | ");

        foreach (float hold in holdCandidates)
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
                float noPickupTravel = travel;
                float candidateTravel = travel;
                float minimumTravel = travel;
                float maximumTravel = travel;
                bool futureSpeedTransitionExpected = false;
                string speedEnvelopeCheck =
                    "SpiritEnvelope=NotRequired";
                SpeedEnvelopeEvaluation candidateEnvelope = default;
                // Guaranteed Spirit pickup value is the intersection of the
                // no-reset and reset trajectories. If the calibrated
                // no-reset curve cannot even enter a deliberately expanded
                // sphere body envelope, the expensive 10 ms slow/fast trace
                // pair cannot possibly produce a guaranteed hit. Perform
                // this allocation-free broad phase before building either
                // trace; the final exact test below remains authoritative.
                if (!MayTrajectoryHitAnySphereBroadPhase(
                        elevatedAhead,
                        launchX,
                        scan.Current.Top,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        travel))
                {
                    continue;
                }
                if (spiritBoost.RequiresSpeedEnvelope)
                {
                    candidateEnvelope =
                        EvaluateSpiritBoostTrajectoryEnvelope(
                            scan.Current,
                            collectionTarget,
                            Array.Empty<BonusBoardSegment>(),
                            hazard,
                            spiritBoost,
                            launchX,
                            speed,
                            hold,
                            flightSeconds,
                            physics,
                            travel,
                            useRawTargetBounds: false);
                    if (!candidateEnvelope.IsSafe)
                        continue;

                    candidateTravel =
                        candidateEnvelope.ExpectedTravel;
                    minimumTravel =
                        candidateEnvelope.MinimumTravel;
                    maximumTravel =
                        candidateEnvelope.MaximumTravel;
                    futureSpeedTransitionExpected =
                        spiritBoost.RequiresConservativeImmediateBoost ||
                        candidateEnvelope.TriggerHits > 0;
                    speedEnvelopeCheck = candidateEnvelope.Summary;
                }

                float landingX = launchX + candidateTravel;
                int hits = CountTrajectorySphereHitsAcrossSpeedScenarios(
                    elevatedAhead,
                    launchX,
                    scan.Current.Top,
                    speed,
                    hold,
                    flightSeconds,
                    physics,
                    spiritBoost,
                    noPickupTravel);
                if (hits <= 0)
                    continue;
                int speedBoostHits = spiritBoost.RequiresSpeedEnvelope
                    ? candidateEnvelope.TriggerHits
                    : 0;

                string hazardCheck = "SpeedEnvelopeOwnsHazardProof";
                if (!spiritBoost.RequiresSpeedEnvelope &&
                    !TrajectoryClearsHazard(
                        hazard,
                        launchX,
                        landingX,
                        scan.Current.Top,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        out hazardCheck))
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
                float envelopeLeft = launchX + Mathf.Min(
                    minimumTravel,
                    maximumTravel);
                float envelopeRight = launchX + Mathf.Max(
                    minimumTravel,
                    maximumTravel);
                float landingSafety = Mathf.Min(
                    envelopeLeft - collectionTarget.SafeLeft,
                    collectionTarget.SafeRight - envelopeRight);
                bool materiallySafer =
                    landingSafety > bestLandingSafety + 0.08f;
                bool sameSafetyTier =
                    Mathf.Abs(landingSafety - bestLandingSafety) <= 0.08f;
                bool replaceBest;
                if (!best.IsValid)
                {
                    replaceBest = true;
                }
                else if (!preferComfortableSphereCoverage)
                {
                    // Preserve the mature ordinary-mode and code Section-2/3
                    // ordering exactly: raw landing reserve is compared in
                    // 0.08-unit tiers before collection utility.
                    replaceBest =
                        materiallySafer ||
                        sameSafetyTier && score > bestScore;
                }
                else
                {
                    bool candidateSafe = landingSafety >= 0f;
                    bool bestSafe = bestLandingSafety >= 0f;
                    bool candidateComfortable =
                        landingSafety >= ComfortableSoulLandingMargin;
                    bool bestComfortable =
                        bestLandingSafety >= ComfortableSoulLandingMargin;
                    if (candidateSafe != bestSafe)
                    {
                        replaceBest = candidateSafe;
                    }
                    else if (!candidateSafe)
                    {
                        replaceBest =
                            landingSafety > bestLandingSafety + 0.001f;
                    }
                    else if (candidateComfortable != bestComfortable)
                    {
                        replaceBest = candidateComfortable;
                    }
                    else if (!candidateComfortable)
                    {
                        // Below the comfortable reserve, survival still owns
                        // the choice and pickup count cannot spend margin.
                        replaceBest =
                            landingSafety > bestLandingSafety + 0.001f ||
                            Mathf.Abs(
                                landingSafety - bestLandingSafety) <= 0.001f &&
                            score > bestScore;
                    }
                    else if (spiritBoost.Enabled &&
                             speedBoostHits !=
                                bestRankingSpeedBoostHits)
                    {
                        replaceBest =
                            speedBoostHits >
                            bestRankingSpeedBoostHits;
                    }
                    else if (hits != bestRankingSphereHits)
                    {
                        // Both trajectories retain the complete post-model
                        // safety reserve. Prefer the one that guarantees more
                        // live sphere intersections instead of accumulating
                        // unused centre margin.
                        replaceBest = hits > bestRankingSphereHits;
                    }
                    else
                    {
                        replaceBest =
                            materiallySafer ||
                            sameSafetyTier && score > bestScore;
                    }
                }
                if (!replaceBest)
                    continue;

                bool shouldJump =
                    playerPosition.x >= launchX - TriggerTolerance &&
                    playerPosition.x <= launchX + TriggerTolerance;
                float committedTravel = candidateTravel;
                float committedMinimumTravel = minimumTravel;
                float committedMaximumTravel = maximumTravel;
                bool committedFutureSpeedTransition =
                    futureSpeedTransitionExpected;
                int committedSpeedBoostHits = speedBoostHits;
                string committedEnvelopeCheck = speedEnvelopeCheck;
                if (shouldJump && spiritBoost.RequiresSpeedEnvelope)
                {
                    SpeedEnvelopeEvaluation liveEnvelope =
                        EvaluateSpiritBoostTrajectoryEnvelope(
                            scan.Current,
                            collectionTarget,
                            Array.Empty<BonusBoardSegment>(),
                            hazard,
                            spiritBoost,
                            playerPosition.x,
                            speed,
                            hold,
                            flightSeconds,
                            physics,
                            travel,
                            useRawTargetBounds: false);
                    if (!liveEnvelope.IsSafe)
                        continue;

                    committedTravel = liveEnvelope.ExpectedTravel;
                    committedMinimumTravel = liveEnvelope.MinimumTravel;
                    committedMaximumTravel = liveEnvelope.MaximumTravel;
                    committedFutureSpeedTransition =
                        spiritBoost.RequiresConservativeImmediateBoost ||
                        liveEnvelope.TriggerHits > 0;
                    committedSpeedBoostHits =
                        liveEnvelope.TriggerHits;
                    committedEnvelopeCheck = liveEnvelope.Summary;
                }
                float committedLandingX = shouldJump
                    ? playerPosition.x + committedTravel
                    : landingX;
                int committedHits = shouldJump
                    ? CountTrajectorySphereHitsAcrossSpeedScenarios(
                        elevatedAhead,
                        playerPosition.x,
                        scan.Current.Top,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        spiritBoost,
                        noPickupTravel)
                    : hits;
                bestLandingSafety = landingSafety;
                bestScore = score;
                bestRankingSphereHits = hits;
                bestRankingSpeedBoostHits = speedBoostHits;
                best = new BonusJumpPlan(
                    true,
                    shouldJump,
                    hold,
                    flightSeconds,
                    shouldJump ? committedTravel : candidateTravel,
                    launchX,
                    committedLandingX,
                    launchX - TriggerTolerance,
                    launchX + TriggerTolerance,
                    shouldJump
                        ? "SameSurfaceSphereCollection"
                        : "ApproachingSameSurfaceSphereCollection",
                    string.Empty,
                    BonusManeuverKind.SphereCollectionJump,
                    committedHits,
                    shouldJump
                        ? committedMinimumTravel
                        : minimumTravel,
                    shouldJump
                        ? committedMaximumTravel
                        : maximumTravel,
                    shouldJump
                        ? committedFutureSpeedTransition
                        : futureSpeedTransitionExpected,
                    committedSpeedBoostHits);
                evaluations.Append(
                    $"Best[H={hold:F2},EH={effectiveHold:F2}," +
                    $"L={launchX:F2},P={landingX:F2}," +
                    $"D={candidateTravel:F2}," +
                    $"T={flightSeconds:F3},SoulHits={hits},BoostHits=" +
                    $"{speedBoostHits}," +
                    $"LandingSafety={landingSafety:F3}," +
                    $"ReservedRunway={postLandingRunway:F2}," +
                    $"Flight={flightSource},Travel={travelSource}," +
                    $"{hazardCheck},{committedEnvelopeCheck}] | ");
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
        JumpPhysicsSnapshot physics,
        SpiritBoostRouteContext spiritBoost)
    {
        if (!scan.IsValid || !hazard.IsValid ||
            hazard.Left < scan.Current.Left ||
            hazard.Right > scan.Current.Right ||
            hazard.Right <= playerPosition.x)
            return Invalid("HazardScanInvalid", "Hazard or support unavailable");
        if (spiritBoost.Enabled && !spiritBoost.KinematicsAvailable)
        {
            return Invalid(
                "SpiritBoostKinematicsUnavailable",
                $"Same-surface hazard fallback cannot replace a rejected " +
                $"route without the same typed speed proof. Context[" +
                $"{spiritBoost.Summary}]");
        }

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
                    scan.Current.Top, speed, hold, flightSeconds, physics,
                    out string hazardCheck))
            {
                AppendEvaluation(evaluations,
                    $"H={hold:F2}:Rejected,{hazardCheck}");
                continue;
            }

            BonusBoardSegment hazardLanding = scan.Current with
            {
                SafeLeft = targetLeft,
                SafeRight = targetRight
            };
            float candidateTravel = travel;
            float minimumTravel = travel;
            float maximumTravel = travel;
            bool futureSpeedTransitionExpected = false;
            string speedEnvelopeCheck =
                "SpiritEnvelope=NotRequired";
            if (spiritBoost.RequiresSpeedEnvelope)
            {
                SpeedEnvelopeEvaluation envelope =
                    EvaluateSpiritBoostTrajectoryEnvelope(
                        scan.Current,
                        hazardLanding,
                        Array.Empty<BonusBoardSegment>(),
                        hazard,
                        spiritBoost,
                        plannedLaunch,
                        speed,
                        hold,
                        flightSeconds,
                        physics,
                        travel,
                        useRawTargetBounds: false);
                if (!envelope.IsSafe)
                {
                    AppendEvaluation(
                        evaluations,
                        $"H={hold:F2}:RejectedBySpeedEnvelope[" +
                        $"{envelope.Summary}]");
                    continue;
                }

                candidateTravel = envelope.ExpectedTravel;
                minimumTravel = envelope.MinimumTravel;
                maximumTravel = envelope.MaximumTravel;
                plannedLanding = plannedLaunch + candidateTravel;
                futureSpeedTransitionExpected =
                    spiritBoost.RequiresConservativeImmediateBoost ||
                    envelope.TriggerHits > 0;
                speedEnvelopeCheck = envelope.Summary;
            }

            bool shouldJump =
                playerPosition.x >= plannedLaunch - TriggerTolerance &&
                playerPosition.x <= latestLaunch;
            float committedTravel = candidateTravel;
            float committedMinimumTravel = minimumTravel;
            float committedMaximumTravel = maximumTravel;
            bool committedFutureSpeedTransition =
                futureSpeedTransitionExpected;
            if (shouldJump)
            {
                if (spiritBoost.RequiresSpeedEnvelope)
                {
                    SpeedEnvelopeEvaluation liveEnvelope =
                        EvaluateSpiritBoostTrajectoryEnvelope(
                            scan.Current,
                            hazardLanding,
                            Array.Empty<BonusBoardSegment>(),
                            hazard,
                            spiritBoost,
                            playerPosition.x,
                            speed,
                            hold,
                            flightSeconds,
                            physics,
                            travel,
                            useRawTargetBounds: false);
                    if (!liveEnvelope.IsSafe)
                        continue;

                    committedTravel = liveEnvelope.ExpectedTravel;
                    committedMinimumTravel = liveEnvelope.MinimumTravel;
                    committedMaximumTravel = liveEnvelope.MaximumTravel;
                    committedFutureSpeedTransition =
                        spiritBoost.RequiresConservativeImmediateBoost ||
                        liveEnvelope.TriggerHits > 0;
                    speedEnvelopeCheck = liveEnvelope.Summary;
                }
                else
                {
                    float liveLanding = playerPosition.x + travel;
                    if (liveLanding < targetLeft ||
                        liveLanding > targetRight ||
                        !TrajectoryClearsHazard(
                            hazard,
                            playerPosition.x,
                            liveLanding,
                            scan.Current.Top,
                            speed,
                            hold,
                            flightSeconds,
                            physics,
                            out _))
                    {
                        continue;
                    }
                }
            }
            float committedLanding = shouldJump
                ? playerPosition.x + committedTravel
                : plannedLanding;
            float score =
                -Mathf.Abs(plannedLanding - desiredLanding) - hold * 0.10f;
            AppendEvaluation(evaluations,
                $"H={hold:F2}:EH={effectiveHold:F2},T={flightSeconds:F3}," +
                $"D={candidateTravel:F2},Source={source}," +
                $"L={plannedLaunch:F2},P={plannedLanding:F2}," +
                $"{hazardCheck},{speedEnvelopeCheck}");
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = new BonusJumpPlan(
                true,
                shouldJump,
                hold,
                flightSeconds,
                shouldJump ? committedTravel : candidateTravel,
                plannedLaunch,
                committedLanding,
                plannedLaunch - TriggerTolerance, latestLaunch,
                shouldJump ? "SameSurfaceHazard" : "ApproachingSameSurfaceHazard",
                string.Empty,
                BonusManeuverKind.HazardClearanceJump,
                0,
                shouldJump
                    ? committedMinimumTravel
                    : minimumTravel,
                shouldJump
                    ? committedMaximumTravel
                    : maximumTravel,
                shouldJump
                    ? committedFutureSpeedTransition
                    : futureSpeedTransitionExpected);
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
    /// Plans an escape from a flush raised obstacle after physics has reduced
    /// live VX to zero.  The horizontal model deliberately uses the last
    /// accepted pre-contact speed: the game restores that motion as soon as
    /// the jump clears the face.  This is not a generic wall/trench fallback;
    /// callers must prove grounded contact with an AdjacentWall whose gap is
    /// effectively zero.
    /// </summary>
    internal bool TryPlanGroundedContactEscape(
        BonusBoardScanResult scan,
        Vector3 playerPosition,
        float preContactSpeed,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        bool requireStrictSafeLanding,
        out BonusJumpPlan selectedPlan,
        out BonusBoardSegment selectedTarget,
        out string rejectionSummary)
    {
        selectedPlan = default;
        selectedTarget = default;
        rejectionSummary = string.Empty;
        BonusObstacleAssessment obstacle =
            BonusObstacleClassifier.Classify(scan);
        if (!scan.IsValid || !scan.HasNext ||
            obstacle.Kind != BonusObstacleKind.AdjacentWall ||
            scan.Gap > 0.10f ||
            scan.HeightDelta <= 0.35f ||
            preContactSpeed <= 1f || preContactSpeed >= 80f)
        {
            rejectionSummary =
                $"EligibilityRejected[ScanValid={scan.IsValid}," +
                $"HasNext={scan.HasNext},Obstacle={obstacle.Kind}," +
                $"Gap={scan.Gap:F3},Rise={scan.HeightDelta:F3}," +
                $"PreContactVX={preContactSpeed:F3}]";
            return false;
        }

        float maximumHold = Mathf.Clamp(
            physics.EffectiveHoldCapSeconds,
            MinimumHoldSeconds,
            MaximumHoldSeconds);
        TryPredictFlightTime(
            maximumHold,
            0f,
            physics,
            out _,
            out _,
            out float maximumRise);
        if (scan.HeightDelta > maximumRise - 0.05f)
        {
            rejectionSummary =
                $"RiseUnreachable[Rise={scan.HeightDelta:F3}," +
                $"MaximumRise={maximumRise:F3}," +
                $"MaximumHold={maximumHold:F3}]";
            return false;
        }

        List<BonusBoardSegment> targets = new();
        if (scan.Alternatives != null)
        {
            targets.AddRange(scan.Alternatives.Where(candidate =>
                candidate.Width >= 0.75f &&
                candidate.Right > scan.Next.Right + 0.10f &&
                candidate.Left >= scan.Next.Right - 0.15f &&
                candidate.Top >= scan.Current.Top - 15.25f &&
                candidate.Top <= scan.Current.Top + maximumRise - 0.05f));
        }
        targets.Add(scan.Next);
        targets = targets
            .GroupBy(candidate =>
                $"{candidate.Left:F2}:{candidate.Right:F2}:" +
                $"{candidate.Top:F2}")
            .Select(group => group.First())
            .OrderBy(candidate => candidate.Left)
            .ToList();

        StringBuilder evaluations = new();
        float bestScore = float.NegativeInfinity;
        float bestFallbackMiss = float.PositiveInfinity;
        BonusJumpPlan fallbackPlan = default;
        BonusBoardSegment fallbackTarget = default;
        foreach (BonusBoardSegment target in targets)
        {
            float targetCenter =
                (target.SafeLeft + target.SafeRight) * 0.5f;
            float inferredHalfWidth = Mathf.Max(
                0.15f,
                target.SafeLeft - target.Left - 0.15f);
            float physicalLeft = target.Left - inferredHalfWidth + 0.05f;
            float physicalRight = target.Right + inferredHalfWidth - 0.05f;
            bool downstreamTarget =
                target.Right > scan.Next.Right + 0.10f;

            foreach (float hold in HoldCandidates)
            {
                if (hold > maximumHold + 0.001f ||
                    !TryPredictFlightTime(
                        hold,
                        target.Top - scan.Current.Top,
                        physics,
                        out float flightSeconds,
                        out float effectiveHold,
                        out float candidateMaximumRise) ||
                    candidateMaximumRise < scan.HeightDelta + 0.05f)
                {
                    continue;
                }

                flightSeconds *= physics.FlightTimeScale;
                flightSeconds = CalibrateBaseSpeedFlightDuration(
                    preContactSpeed,
                    hold,
                    target.Top - scan.Current.Top,
                    flightSeconds,
                    physics,
                    out string flightSource);
                float travel = PredictHorizontalTravel(
                    preContactSpeed,
                    flightSeconds,
                    hold,
                    target.Top - scan.Current.Top,
                    physics,
                    out string travelSource);
                travel = ApplyLandingBias(
                    travel,
                    target.Top - scan.Current.Top,
                    hold,
                    physics,
                    ref travelSource);
                float landingX = playerPosition.x + travel;
                if (!TrajectoryClearsHazard(
                        hazard,
                        playerPosition.x,
                        landingX,
                        scan.Current.Top,
                        preContactSpeed,
                        hold,
                        flightSeconds,
                        physics,
                        out string hazardCheck))
                {
                    evaluations.Append(
                        $"T=[{target.Left:F2},{target.Right:F2}]" +
                        $"H={hold:F3}:HazardReject[{hazardCheck}] | ");
                    continue;
                }

                float safeTolerance = requireStrictSafeLanding
                    ? 0.02f
                    : LandingRecoveryTolerance;
                bool insideSafe =
                    landingX >= target.SafeLeft - safeTolerance &&
                    landingX <= target.SafeRight + safeTolerance;
                float footprintLeft = landingX - inferredHalfWidth;
                float footprintRight = landingX + inferredHalfWidth;
                float overlap = Mathf.Max(
                    0f,
                    Mathf.Min(footprintRight, target.Right) -
                    Mathf.Max(footprintLeft, target.Left));
                bool rawBodyFit =
                    !requireStrictSafeLanding && overlap >= 0.15f;
                bool accepted = insideSafe || rawBodyFit;
                float rawMiss = landingX < physicalLeft
                    ? physicalLeft - landingX
                    : landingX > physicalRight
                        ? landingX - physicalRight
                        : 0f;
                evaluations.Append(
                    $"T=[{target.Left:F2},{target.Right:F2}]@" +
                    $"{target.Top:F2},H={hold:F3},EH={effectiveHold:F3}," +
                    $"TFlight={flightSeconds:F3},D={travel:F3}," +
                    $"X={landingX:F3},Safe=[{target.SafeLeft:F3}," +
                    $"{target.SafeRight:F3}],Footprint=[{footprintLeft:F3}," +
                    $"{footprintRight:F3}],Overlap={overlap:F3}," +
                    $"Source={flightSource};{travelSource}," +
                    $"Result={(insideSafe ? "Safe" : rawBodyFit ? "RawBodyFit" : "Reject")} | ");

                BonusJumpPlan candidate = new(
                    true,
                    true,
                    hold,
                    flightSeconds,
                    travel,
                    playerPosition.x,
                    landingX,
                    playerPosition.x,
                    playerPosition.x,
                    insideSafe
                        ? "GroundedContactEscapeSafeLanding"
                        : rawBodyFit
                            ? "GroundedContactEscapeRawBodyFit"
                            : "GroundedContactEscapeClosestSupport",
                    string.Empty,
                    BonusManeuverKind.GroundedContactEscape);

                if (!accepted)
                {
                    if (rawMiss < bestFallbackMiss)
                    {
                        bestFallbackMiss = rawMiss;
                        fallbackPlan = candidate;
                        fallbackTarget = target;
                    }
                    continue;
                }

                float centerError = Mathf.Abs(landingX - targetCenter);
                float score =
                    (insideSafe ? 200f : 100f) +
                    (downstreamTarget ? 40f : 0f) +
                    Mathf.Min(15f, target.Width) * 1.5f -
                    centerError * 2f +
                    // At an ordinary-speed confirmed collision, retained
                    // traces and user testing show undershoot/insufficient
                    // clearance, while a longer press always breaks contact.
                    // Prefer the longest hold that still has verified support
                    // beneath its predicted footprint. Boosted routes keep the
                    // centre/short-hold bias because overshoot is their known
                    // failure mode.
                    (requireStrictSafeLanding ? -hold : hold * 125f);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                selectedPlan = candidate;
                selectedTarget = target;
            }
        }

        if (selectedPlan.IsValid)
        {
            selectedPlan = selectedPlan with
            {
                CandidateSummary = evaluations.ToString()
            };
            rejectionSummary = "None";
            return true;
        }

        // A confirmed face contact must remain live even at boost speed.  The
        // game can always leave this state with a jump, while waiting here
        // leaves VX pinned to zero forever.  Prefer a strict safe interval
        // above; if model uncertainty leaves no such interval, use the
        // hazard-cleared candidate whose physical footprint misses mapped
        // support by the least amount.  The runtime still has a confirmed-wall
        // executor as a final fallback when no ballistic candidate survives.
        if (requireStrictSafeLanding)
        {
            if (fallbackPlan.IsValid)
            {
                selectedPlan = fallbackPlan with
                {
                    Reason = "GroundedContactEscapeStrictClosestSupport",
                    CandidateSummary = evaluations +
                        $"StrictLivenessFallback[Miss={bestFallbackMiss:F3}]"
                };
                selectedTarget = fallbackTarget;
                rejectionSummary = "None:StrictLivenessFallback";
                return true;
            }

            rejectionSummary =
                $"StrictSafeLandingAndHazardClearedFallbackUnavailable Candidates[" +
                $"{evaluations}]";
            return false;
        }

        // Remaining stopped is never a useful action in Idle Slayer.  If no
        // conservative corridor survives model uncertainty, choose the hold
        // whose predicted footprint is closest to verified support.  At
        // ordinary speed bias the native-cap press, because retained traces
        // show only undershoot.
        if (!requireStrictSafeLanding)
        {
            BonusBoardSegment fallbackPreferredTarget = targets
                .Where(target => target.Right > scan.Next.Right + 0.10f)
                .OrderByDescending(target => target.Width)
                .ThenBy(target => target.Left)
                .FirstOrDefault();
            if (fallbackPreferredTarget.Width > 0.05f &&
                TryPredictFlightTime(
                    maximumHold,
                    fallbackPreferredTarget.Top - scan.Current.Top,
                    physics,
                    out float fallbackFlight,
                    out _,
                    out _))
            {
                fallbackFlight *= physics.FlightTimeScale;
                fallbackFlight = CalibrateBaseSpeedFlightDuration(
                    preContactSpeed,
                    maximumHold,
                    fallbackPreferredTarget.Top - scan.Current.Top,
                    fallbackFlight,
                    physics,
                    out string fallbackFlightSource);
                float fallbackTravel = PredictHorizontalTravel(
                    preContactSpeed,
                    fallbackFlight,
                    maximumHold,
                    fallbackPreferredTarget.Top - scan.Current.Top,
                    physics,
                    out string fallbackTravelSource);
                fallbackTravel = ApplyLandingBias(
                    fallbackTravel,
                    fallbackPreferredTarget.Top - scan.Current.Top,
                    maximumHold,
                    physics,
                    ref fallbackTravelSource);
                float fallbackLandingX =
                    playerPosition.x + fallbackTravel;
                if (TrajectoryClearsHazard(
                        hazard,
                        playerPosition.x,
                        fallbackLandingX,
                        scan.Current.Top,
                        preContactSpeed,
                        maximumHold,
                        fallbackFlight,
                        physics,
                        out string fallbackHazardCheck))
                {
                    float inferredHalfWidth = Mathf.Max(
                        0.15f,
                        fallbackPreferredTarget.SafeLeft -
                        fallbackPreferredTarget.Left - 0.15f);
                    float physicalLeft =
                        fallbackPreferredTarget.Left -
                        inferredHalfWidth + 0.05f;
                    float physicalRight =
                        fallbackPreferredTarget.Right +
                        inferredHalfWidth - 0.05f;
                    bestFallbackMiss = fallbackLandingX < physicalLeft
                        ? physicalLeft - fallbackLandingX
                        : fallbackLandingX > physicalRight
                            ? fallbackLandingX - physicalRight
                            : 0f;
                    fallbackPlan = new(
                        true,
                        true,
                        maximumHold,
                        fallbackFlight,
                        fallbackTravel,
                        playerPosition.x,
                        fallbackLandingX,
                        playerPosition.x,
                        playerPosition.x,
                        "GroundedContactEscapeNativeCap",
                        string.Empty,
                        BonusManeuverKind.GroundedContactEscape);
                    fallbackTarget = fallbackPreferredTarget;
                    evaluations.Append(
                        $"NativeCap[T=[{fallbackPreferredTarget.Left:F2}," +
                        $"{fallbackPreferredTarget.Right:F2}]," +
                        $"X={fallbackLandingX:F3}," +
                        $"Source={fallbackFlightSource};" +
                        $"{fallbackTravelSource},Hazard=" +
                        $"{fallbackHazardCheck}] | ");
                }
                else
                {
                    evaluations.Append(
                        $"NativeCapHazardReject[" +
                        $"{fallbackHazardCheck}] | ");
                }
            }
        }

        if (!fallbackPlan.IsValid)
        {
            rejectionSummary =
                $"NoHazardSafeFallback Candidates[{evaluations}]";
            return false;
        }

        selectedPlan = fallbackPlan with
        {
            CandidateSummary = evaluations +
                $"Fallback[Miss={bestFallbackMiss:F3}," +
                $"Strict={requireStrictSafeLanding}]"
        };
        selectedTarget = fallbackTarget;
        rejectionSummary = "None";
        return true;
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
        float physicalWallLipY,
        float requiredApexY,
        BonusBoardSegment downstreamTarget,
        float horizontalSpeed,
        JumpPhysicsSnapshot physics,
        float releaseTravelBias,
        float minimumHoldSeconds,
        float maximumHoldSeconds,
        float safeEdgeTolerance,
        bool allowRawBodyFitLanding,
        out float selectedHold,
        out float predictedFlightSeconds,
        out float predictedHorizontalTravel,
        out float predictedLandingX,
        out bool selectedRawBodyFit,
        out string summary)
    {
        selectedHold = 0f;
        predictedFlightSeconds = 0f;
        predictedHorizontalTravel = 0f;
        predictedLandingX = contactX;
        selectedRawBodyFit = false;
        StringBuilder evaluations = new();
        float bestScore = float.PositiveInfinity;
        int bestSafetyTier = int.MaxValue;
        float bestLandingMargin = float.NegativeInfinity;
        bool bestObjectiveApexComplete = false;
        float maximumUsefulHold = Mathf.Clamp(
            Mathf.Min(physics.EffectiveHoldCapSeconds, maximumHoldSeconds),
            MinimumHoldSeconds,
            MaximumHoldSeconds);
        float minimumUsefulHold = Mathf.Clamp(
            minimumHoldSeconds,
            MinimumHoldSeconds,
            maximumUsefulHold);
        float physicalLipRise = Mathf.Max(
            0f,
            physicalWallLipY - contactFeetY);
        float objectiveApexRise = Mathf.Max(
            0f,
            requiredApexY - contactFeetY);
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
                    out float maximumRise))
            {
                evaluations.Append($"H={hold:F3}:FlightReject | ");
                continue;
            }

            // The physical lip is a hard feasibility boundary. The higher
            // pickup/objective apex is not: if it conflicts with every safe
            // landing, retain a lip-clearing landing instead of returning no
            // route and falling back to a blind wall pulse.
            if (maximumRise < physicalLipRise + 0.05f)
            {
                evaluations.Append(
                    $"H={hold:F3}:PhysicalLipReject[MaxRise=" +
                    $"{maximumRise:F3},NeedLipRise=" +
                    $"{physicalLipRise + 0.05f:F3}] | ");
                continue;
            }
            bool objectiveApexComplete =
                maximumRise >= objectiveApexRise + 0.05f;

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
                        physics) < physicalLipRise)
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
            float inferredHalfWidth = Mathf.Max(
                0.15f,
                downstreamTarget.SafeLeft - downstreamTarget.Left - 0.15f);
            float footprintLeft = landingX - inferredHalfWidth;
            float footprintRight = landingX + inferredHalfWidth;
            float rawOverlap = Mathf.Max(
                0f,
                Mathf.Min(footprintRight, downstreamTarget.Right) -
                Mathf.Max(footprintLeft, downstreamTarget.Left));
            float requiredRawOverlap = Mathf.Max(
                0.15f,
                Mathf.Abs(horizontalSpeed) * Mathf.Clamp(
                    physics.FixedDeltaTime,
                    0.005f,
                    0.05f));
            bool insideSafe =
                leftMargin >= -safeEdgeTolerance &&
                rightMargin >= -safeEdgeTolerance;
            bool rawBodyFit =
                allowRawBodyFitLanding &&
                rawOverlap >= requiredRawOverlap;
            bool inside = insideSafe || rawBodyFit;
            float landingMargin = Mathf.Min(leftMargin, rightMargin);
            evaluations.Append(
                $"H={hold:F3}:RawT={rawFlightSeconds:F3}," +
                $"Scale={physics.FlightTimeScale:F3},T={flightSeconds:F3}," +
                $"PhysicalLipY={physicalWallLipY:F3}," +
                $"RequiredApexY={requiredApexY:F3}," +
                $"MaxRise={maximumRise:F3}," +
                $"ObjectiveApexComplete={objectiveApexComplete}," +
                $"LipT={lipCrossingSeconds:F3}," +
                $"LipVX={speedAtLip:F3},MoveT={horizontalSeconds:F3}," +
                $"D={travel:F3},X={landingX:F3}," +
                $"ReleaseTravelBias={boundedReleaseTravelBias:F3}," +
                $"Margins=[{leftMargin:F3},{rightMargin:F3}]," +
                $"SafeTolerance={safeEdgeTolerance:F3},Footprint=" +
                $"[{footprintLeft:F3},{footprintRight:F3}]," +
                $"RawOverlap={rawOverlap:F3}/" +
                $"{requiredRawOverlap:F3}," +
                $"{(insideSafe ? "Safe" : rawBodyFit ? "RawBodyFit" : "Reject")} | ");
            if (!inside)
                continue;

            int safetyTier = insideSafe ? 0 : 1;
            float edgePenalty = Mathf.Max(0f, 0.35f - Mathf.Min(
                leftMargin,
                rightMargin));
            float score = Mathf.Abs(landingX - targetCenter) +
                          edgePenalty * 3f +
                          hold * 0.05f;
            bool better;
            if (safetyTier != bestSafetyTier)
            {
                better = safetyTier < bestSafetyTier;
            }
            else if (landingMargin >
                     bestLandingMargin + RouteLandingSafetyTier)
            {
                better = true;
            }
            else if (landingMargin <
                     bestLandingMargin - RouteLandingSafetyTier)
            {
                better = false;
            }
            else if (objectiveApexComplete != bestObjectiveApexComplete)
            {
                // Pickup height may decide only inside the same physical
                // landing-safety band.
                better = objectiveApexComplete;
            }
            else
            {
                better = score < bestScore;
            }
            if (!better)
                continue;

            bestSafetyTier = safetyTier;
            bestLandingMargin = landingMargin;
            bestObjectiveApexComplete = objectiveApexComplete;
            bestScore = score;
            selectedHold = hold;
            predictedFlightSeconds = flightSeconds;
            predictedHorizontalTravel = travel;
            predictedLandingX = landingX;
            selectedRawBodyFit = !insideSafe && rawBodyFit;
        }

        summary = bestScore < float.PositiveInfinity
            ? evaluations +
              $"Selected[H={selectedHold:F3},X={predictedLandingX:F3}," +
              $"Mode={(selectedRawBodyFit ? "RawBodyFit" : "SafeTolerance")}," +
              $"LandingMargin={bestLandingMargin:F3}," +
              $"ObjectiveApex=" +
              $"{(bestObjectiveApexComplete ? "Complete" : "Fallback")}]." +
              (bestObjectiveApexComplete
                  ? string.Empty
                  : " ObjectiveApexFallback[Physical lip and landing are " +
                    "safe; the optional higher apex is unavailable in the " +
                    "selected safety tier.]")
            : evaluations.ToString();
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
        float physicalWallLipY,
        float requiredApexY,
        BonusBoardSegment downstreamFace,
        float playerHalfWidth,
        float horizontalSpeed,
        float minimumContactFeetY,
        float maximumContactFeetY,
        float preferredContactFeetY,
        JumpPhysicsSnapshot physics,
        float minimumHoldSeconds,
        float maximumHoldSeconds,
        bool preserveHoldThroughLip,
        int horizontalTimingToleranceSteps,
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
        float bestInterceptSafety = float.NegativeInfinity;
        bool bestObjectiveApexComplete = false;
        float bestMinimumTrajectoryApexFeetY = float.NegativeInfinity;
        float bestMaximumTrajectoryApexFeetY = float.NegativeInfinity;
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
        int timingToleranceSteps = Mathf.Clamp(
            horizontalTimingToleranceSteps,
            0,
            1);

        foreach (float hold in HoldCandidates)
        {
            if (hold + 0.001f < minimumUsefulHold ||
                hold > maximumUsefulHold + 0.001f)
            {
                continue;
            }

            // This result is delivered by JumpController's fixed-step ceiling,
            // so the actuator produces exactly ceil(hold/fixedDelta) powered
            // PlayerMovement ticks.  The former +/- one-step timer envelope
            // rejected valid face intercepts that the controller can deliver
            // deterministically.
            int exactHeldSteps = Mathf.Max(
                1,
                Mathf.CeilToInt(hold / fixedStep - 0.0001f));
            int candidateMinimumSteps = exactHeldSteps;
            int candidateMaximumSteps = exactHeldSteps;
            bool legal = true;
            float worstTopMargin = float.PositiveInfinity;
            float worstFaceMargin = float.PositiveInfinity;
            float minimumTrajectoryApexFeetY = float.PositiveInfinity;
            float maximumTrajectoryApexFeetY = float.NegativeInfinity;
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
                        physicalWallLipY,
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
                        preserveHoldThroughLip,
                        timingToleranceSteps,
                        out float stepLipSeconds,
                        out float stepTopSeconds,
                        out float stepTargetSeconds,
                        out float stepTopY,
                        out float stepFaceY,
                        out float stepFaceVelocityY,
                        out float stepTopMargin,
                        out float stepFaceMargin,
                        out float stepMinimumApexFeetY,
                        out float stepMaximumApexFeetY,
                        out string stepReason))
                {
                    legal = false;
                }

                stepEvaluations.Append(
                    $"N{heldSteps}[LipT={stepLipSeconds:F3}," +
                    $"ClearY={stepTopY:F3},FaceY={stepFaceY:F3}," +
                    $"FaceVY={stepFaceVelocityY:F3}," +
                    $"Margins=[{stepTopMargin:F3},{stepFaceMargin:F3}]," +
                    $"TrajectoryApex=[{stepMinimumApexFeetY:F3}," +
                    $"{stepMaximumApexFeetY:F3}]," +
                    $"{stepReason}] ");
                worstTopMargin = Mathf.Min(worstTopMargin, stepTopMargin);
                worstFaceMargin = Mathf.Min(worstFaceMargin, stepFaceMargin);
                minimumTrajectoryApexFeetY = Mathf.Min(
                    minimumTrajectoryApexFeetY,
                    stepMinimumApexFeetY);
                maximumTrajectoryApexFeetY = Mathf.Max(
                    maximumTrajectoryApexFeetY,
                    stepMaximumApexFeetY);
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

            bool objectiveApexComplete =
                minimumTrajectoryApexFeetY >= requiredApexY + 0.05f;
            evaluations.Append(
                $"H={hold:F3}:HeldSteps=[{candidateMinimumSteps}," +
                $"{candidateMaximumSteps}],WorstMargins=" +
                $"[{worstTopMargin:F3},{worstFaceMargin:F3}]," +
                $"ActualReleaseApex=[{minimumTrajectoryApexFeetY:F3}," +
                $"{maximumTrajectoryApexFeetY:F3}],RequiredApexY=" +
                $"{requiredApexY:F3},ObjectiveApexComplete=" +
                $"{objectiveApexComplete}," +
                $"{(legal ? "SafeIntercept" : "Reject")}," +
                $"Steps{{{stepEvaluations}}} | ");
            if (!legal)
                continue;

            float edgePenalty = Mathf.Max(0f, 0.25f - worstFaceMargin);
            float topPenalty = Mathf.Max(0f, 0.35f - worstTopMargin);
            float score =
                Mathf.Abs(nominalFaceY - desiredContactFeetY) +
                edgePenalty * 2f + topPenalty * 1.5f + hold * 0.02f;
            float interceptSafety = Mathf.Min(
                worstTopMargin,
                worstFaceMargin);
            bool better;
            if (bestScore == float.PositiveInfinity ||
                interceptSafety >
                    bestInterceptSafety + RouteLandingSafetyTier)
            {
                better = true;
            }
            else if (interceptSafety <
                     bestInterceptSafety - RouteLandingSafetyTier)
            {
                better = false;
            }
            else if (objectiveApexComplete != bestObjectiveApexComplete)
            {
                // The higher pickup objective cannot exchange a materially
                // safer finite-face intercept for a marginal one.
                better = objectiveApexComplete;
            }
            else
            {
                better = score < bestScore;
            }
            if (!better)
                continue;

            bestInterceptSafety = interceptSafety;
            bestObjectiveApexComplete = objectiveApexComplete;
            bestMinimumTrajectoryApexFeetY = minimumTrajectoryApexFeetY;
            bestMaximumTrajectoryApexFeetY = maximumTrajectoryApexFeetY;
            bestScore = score;
            selectedHold = hold;
            lipCrossingSeconds = nominalLipSeconds + physics.InputDelaySeconds;
            topClearSeconds = nominalTopSeconds + physics.InputDelaySeconds;
            targetContactSeconds = nominalTargetSeconds + physics.InputDelaySeconds;
            predictedTopClearFeetY = nominalTopY;
            predictedContactFeetY = nominalFaceY;
            predictedContactVelocityY = nominalFaceVelocityY;
        }

        summary = bestScore < float.PositiveInfinity
            ? evaluations +
              $"Selected[H={selectedHold:F3},ActualReleaseApex=" +
              $"[{bestMinimumTrajectoryApexFeetY:F3}," +
              $"{bestMaximumTrajectoryApexFeetY:F3}]," +
              $"InterceptSafety={bestInterceptSafety:F3},ObjectiveApex=" +
              $"{(bestObjectiveApexComplete ? "Complete" : "Fallback")}]." +
              (bestObjectiveApexComplete
                  ? string.Empty
                  : " ObjectiveApexFallback[Physical lip, top clearance, " +
                    "and finite-face contact remain safe; the actual " +
                    "release trajectory does not reach the optional higher " +
                    "apex.]")
            : evaluations.ToString();
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
        bool preserveHoldThroughLip,
        int timingToleranceSteps,
        out float lipCrossingSeconds,
        out float topClearSeconds,
        out float targetContactSeconds,
        out float predictedTopClearFeetY,
        out float predictedContactFeetY,
        out float predictedContactVelocityY,
        out float topMargin,
        out float faceMargin,
        out float minimumTrajectoryApexFeetY,
        out float maximumTrajectoryApexFeetY,
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
        minimumTrajectoryApexFeetY = float.NegativeInfinity;
        maximumTrajectoryApexFeetY = float.NegativeInfinity;
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

        float speedAtLip = Mathf.Max(
            Mathf.Max(1f, baseHorizontalSpeed),
            horizontalSpeed -
                Mathf.Max(0.10f, boostHorizontalDeceleration) *
                lipStep * fixedStep);
        int baseTopSteps = SolveFixedStepHorizontalTravelSteps(
            speedAtLip,
            baseHorizontalSpeed,
            boostHorizontalDeceleration,
            topClearTravel,
            fixedStep);
        int baseTargetSteps = Mathf.Max(
            baseTopSteps + 1,
            SolveFixedStepHorizontalTravelSteps(
                speedAtLip,
                baseHorizontalSpeed,
                boostHorizontalDeceleration,
                targetTravel,
                fixedStep));
        int earliestReleaseHeldStep =
            preserveHoldThroughLip
                ? heldSteps
                : lipReachedWhileHeld
                    ? lipStep
                    : heldSteps;
        int latestReleaseHeldStep = heldSteps;
        float minimumTopY = float.PositiveInfinity;
        float maximumTopY = float.NegativeInfinity;
        float minimumFaceY = float.PositiveInfinity;
        float maximumFaceY = float.NegativeInfinity;
        float maximumFaceVelocityY = float.NegativeInfinity;
        float minimumFaceVelocityY = float.PositiveInfinity;
        float minimumActualApex = float.PositiveInfinity;
        float maximumActualApex = float.NegativeInfinity;

        for (int releaseHeldStep = earliestReleaseHeldStep;
             releaseHeldStep <= latestReleaseHeldStep;
             releaseHeldStep++)
        {
            int poweredStepsAfterLip = lipReachedWhileHeld
                ? Mathf.Max(0, releaseHeldStep - lipStep)
                : 0;
            float actualReleaseApex = PredictFixedStepWallApex(
                lipFeetY,
                lipVelocityY,
                poweredStepsAfterLip,
                heldVelocity,
                gravity,
                fixedStep);
            minimumActualApex = Mathf.Min(
                minimumActualApex,
                actualReleaseApex);
            maximumActualApex = Mathf.Max(
                maximumActualApex,
                actualReleaseApex);
            // One horizontal fixed-step of timing tolerance covers collision
            // resolution at the old lip and a small live-speed quantisation
            // error without reverting to a render-time hold envelope. A caller
            // that already owns exact physical contact and exact fixed-step
            // actuation may explicitly request the zero-offset solution as a
            // bounded fallback after the robust envelope has failed.
            for (int timingOffset = -timingToleranceSteps;
                 timingOffset <= timingToleranceSteps;
                 timingOffset++)
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
        minimumTrajectoryApexFeetY = minimumActualApex;
        maximumTrajectoryApexFeetY = maximumActualApex;
        float lowerMargin = minimumFaceY - minimumContactFeetY;
        float upperMargin = maximumContactFeetY - maximumFaceY;
        faceMargin = Mathf.Min(lowerMargin, upperMargin);
        bool clearsTop = topMargin >= WallFaceTopClearance;
        bool insideFace = faceMargin >= 0f;
        bool descending = maximumFaceVelocityY <=
            WallFaceMaximumContactVelocityY;
        reason = $"DiscreteLip[Step={lipStep},Y={lipFeetY:F3}," +
                 $"VY={lipVelocityY:F3},Held={lipReachedWhileHeld}]," +
                 $"ReleaseMode=" +
                 $"{(preserveHoldThroughLip ? "FixedStepCeiling" : "LipToCeilingEnvelope")}," +
                 $"ReleaseHeldSteps=[{earliestReleaseHeldStep}," +
                 $"{latestReleaseHeldStep}],XSteps[Clear={baseTopSteps}," +
                 $"Face={baseTargetSteps},Tolerance=+/-{timingToleranceSteps},V0=" +
                 $"{horizontalSpeed:F3},LipVX={speedAtLip:F3}," +
                 $"Base={baseHorizontalSpeed:F3}," +
                 $"Decel={boostHorizontalDeceleration:F3}]," +
                 $"ClearY=[{minimumTopY:F3},{maximumTopY:F3}]," +
                 $"FaceY=[{minimumFaceY:F3},{maximumFaceY:F3}]," +
                 $"FaceVY=[{minimumFaceVelocityY:F3}," +
                 $"{maximumFaceVelocityY:F3}]," +
                 $"ActualReleaseApex=[{minimumActualApex:F3}," +
                 $"{maximumActualApex:F3}]," +
                 $"{(clearsTop && insideFace && descending ? "Safe" : "Reject")}";
        return clearsTop && insideFace && descending;
    }

    private static float PredictFixedStepWallApex(
        float startFeetY,
        float startVelocityY,
        int poweredSteps,
        float heldVelocity,
        float gravity,
        float fixedStep)
    {
        float feetY = startFeetY;
        float velocityY = startVelocityY;
        float apexFeetY = feetY;
        for (int step = 0; step < poweredSteps; step++)
        {
            velocityY = heldVelocity;
            feetY += velocityY * fixedStep;
            apexFeetY = Mathf.Max(apexFeetY, feetY);
        }

        for (int step = 0; step < 128; step++)
        {
            velocityY -= gravity * fixedStep;
            feetY += velocityY * fixedStep;
            apexFeetY = Mathf.Max(apexFeetY, feetY);
            if (velocityY <= 0f)
                break;
        }

        return apexFeetY;
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
        float horizontalSpeed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        out string summary) =>
        TrajectoryClearsHazard(
            hazard, launchX, landingX, launchFeetY,
            horizontalSpeed, requestedHold, flightSeconds, physics,
            out summary);

    private readonly record struct SpeedEnvelopeEvaluation(
        bool IsSafe,
        float ExpectedTravel,
        float MinimumTravel,
        float MaximumTravel,
        int TriggerHits,
        string TriggerSignature,
        int GuaranteedSphereHits,
        string Summary);

    private readonly record struct SpiritBoostTrajectoryTrace(
        float[] Times,
        float[] Travels,
        int Count,
        int TriggerHits,
        string TriggerSignature)
    {
        internal float FinalTravel =>
            Count > 0 && Travels != null
                ? Travels[Count - 1]
                : 0f;
    }

    private readonly record struct SpiritWallContactOutcome(
        bool ReachedFace,
        float ContactSeconds,
        float ContactFeetY,
        bool HazardSafe,
        int TriggerHits,
        string TriggerSignature,
        string Summary);

    private readonly record struct NaturalDropSpeedTrace(
        float TotalTravel,
        float EdgeSeconds,
        int TriggerHits,
        string TriggerSignature);

    /// <summary>
    /// Integrates the complete coast-to-edge plus free-fall interval. This is
    /// deliberately separate from the jump trace: before the support edge the
    /// feet stay on the source top, and only the time after the centre becomes
    /// unsupported contributes vertical fall. Running the same integrator
    /// once with pickups disabled and once with native SpiritBoost triggers
    /// enabled isolates the future speed-reset delta without changing the
    /// already calibrated no-pickup landing.
    /// </summary>
    private static NaturalDropSpeedTrace BuildNaturalDropSpeedTrace(
        SpiritBoostRouteContext spiritBoost,
        float startX,
        float sourceTop,
        float unsupportedCenterX,
        float initialSpeed,
        float fallSeconds,
        float gravity,
        JumpPhysicsSnapshot physics,
        bool allowTriggerPickups,
        float worldTravelScale)
    {
        float horizontalScale = Mathf.Clamp(
            worldTravelScale,
            0.50f,
            1.50f);
        float step = Mathf.Min(
            0.01f,
            Mathf.Clamp(physics.FixedDeltaTime, 0.005f, 0.05f) * 0.50f);
        float baseSpeed = Mathf.Clamp(
            spiritBoost.BaseHorizontalSpeed,
            1f,
            Mathf.Max(1f, initialSpeed));
        float decrease = Mathf.Clamp(
            spiritBoost.BoostDecreasePerSecond,
            0.01f,
            120f);
        float speed = Mathf.Max(baseSpeed, initialSpeed);
        float x = startX;
        float elapsed = 0f;
        float airborneSeconds = 0f;
        float edgeSeconds = startX >= unsupportedCenterX ? 0f : -1f;
        bool airborne = edgeSeconds >= 0f;
        int triggerHits = 0;
        List<int> hitIds = new();
        BonusSpeedBoostTrigger[] triggers =
            spiritBoost.ActiveTriggers ??
            Array.Empty<BonusSpeedBoostTrigger>();
        bool[] picked = new bool[triggers.Length];

        if (allowTriggerPickups &&
            spiritBoost.RequiresConservativeImmediateBoost)
        {
            triggerHits = 1;
            hitIds.Add(-1);
            speed = Mathf.Max(
                speed,
                baseSpeed + spiritBoost.MaximumBoostComponent);
        }

        float approachUpperBound = Mathf.Max(
            0f,
            unsupportedCenterX - startX) /
            Mathf.Max(1f, baseSpeed * horizontalScale);
        int maximumIterations = Mathf.Clamp(
            Mathf.CeilToInt(
                (approachUpperBound + fallSeconds + 0.50f) / step) + 4,
            4,
            8192);
        for (int iteration = 0;
             iteration < maximumIterations;
             iteration++)
        {
            float feetY = airborne
                ? sourceTop - 0.5f * gravity *
                    airborneSeconds * airborneSeconds
                : sourceTop;
            if (allowTriggerPickups)
            {
                for (int index = 0; index < triggers.Length; index++)
                {
                    if (picked[index])
                        continue;

                    BonusSpeedBoostTrigger trigger = triggers[index];
                    bool overlaps =
                        x + spiritBoost.PlayerRightOffset >= trigger.Left &&
                        x + spiritBoost.PlayerLeftOffset <= trigger.Right &&
                        feetY + spiritBoost.PlayerTopOffset >= trigger.Bottom &&
                        feetY + spiritBoost.PlayerBottomOffset <= trigger.Top;
                    if (!overlaps)
                        continue;

                    picked[index] = true;
                    triggerHits++;
                    hitIds.Add(trigger.InstanceId);
                    speed = Mathf.Max(
                        speed,
                        baseSpeed + spiritBoost.MaximumBoostComponent);
                }
            }

            if (airborne && airborneSeconds >= fallSeconds - 0.0001f)
                break;

            float delta = step;
            if (airborne)
            {
                delta = Mathf.Min(
                    delta,
                    Mathf.Max(0f, fallSeconds - airborneSeconds));
            }
            if (delta <= 0.00001f)
                break;

            float nextSpeed = Mathf.Max(
                baseSpeed,
                speed - decrease * delta);
            float deltaTravel =
                (speed + nextSpeed) * 0.5f * delta * horizontalScale;
            if (!airborne && x + deltaTravel >= unsupportedCenterX)
            {
                float fraction = deltaTravel > 0.0001f
                    ? Mathf.Clamp01(
                        (unsupportedCenterX - x) / deltaTravel)
                    : 1f;
                edgeSeconds = elapsed + delta * fraction;
                airborne = true;
                airborneSeconds = delta * (1f - fraction);
            }
            else if (airborne)
            {
                airborneSeconds += delta;
            }

            x += deltaTravel;
            elapsed += delta;
            speed = nextSpeed;
        }

        string signature = hitIds.Count == 0
            ? "None"
            : string.Join(",", hitIds.Take(12));
        if (hitIds.Count > 12)
            signature += $",+{hitIds.Count - 12}";
        return new NaturalDropSpeedTrace(
            Mathf.Max(0f, x - startX),
            Mathf.Max(0f, edgeSeconds),
            triggerHits,
            signature);
    }

    /// <summary>
    /// Finds one command which is safe both when no pending SpiritBoost is
    /// collected and when the native trigger resets the additive boost during
    /// this flight. Reachability is always established by the no-pickup path;
    /// future acceleration can narrow a launch window but can never create it.
    /// </summary>
    private bool TryFindSpiritBoostRobustLaunch(
        BonusBoardScanResult scan,
        BonusHazard hazard,
        SpiritBoostRouteContext spiritBoost,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        float baselineTravel,
        float usableLeft,
        float usableRight,
        float preferredLaunchX,
        IReadOnlyList<Vector2> sphereObjectives,
        bool preferSphereCoverage,
        bool preferSpeedBoostCoverage,
        bool allowVerifiedBoostMargin,
        bool hasTierCalibration,
        int tierSampleCount,
        out float robustLaunchX,
        out float robustWindowLeft,
        out float robustWindowRight,
        out SpeedEnvelopeEvaluation robustEvaluation)
    {
        robustLaunchX = preferredLaunchX;
        robustWindowLeft = usableLeft;
        robustWindowRight = usableRight;
        robustEvaluation = default;

        if (!spiritBoost.RequiresSpeedEnvelope)
        {
            robustEvaluation = new SpeedEnvelopeEvaluation(
                true,
                baselineTravel,
                baselineTravel,
                baselineTravel,
                0,
                "None",
                0,
                $"SpiritEnvelope=Inactive[{spiritBoost.Summary}]");
            return true;
        }

        BonusBoardSegment[] intermediateSurfaces =
            GetIntermediateClearanceSurfaces(scan);
        float clampedPreferred = Mathf.Clamp(
            preferredLaunchX,
            usableLeft,
            usableRight);
        SpeedEnvelopeEvaluation preferred =
            EvaluateSpiritBoostTrajectoryEnvelope(
                scan.Current,
                scan.Next,
                intermediateSurfaces,
                hazard,
                spiritBoost,
                clampedPreferred,
                speed,
                requestedHold,
                flightSeconds,
                physics,
                baselineTravel,
                useRawTargetBounds: false,
                sphereObjectives);

        // The retained V0.68 Spirit trace proved that the former 0.08-unit
        // sweep could evaluate up to 33 launches for every hold, target and
        // continuation. It created severe frame-time spikes and rebuilt the
        // same two speed traces repeatedly. Use three analytically meaningful
        // commands instead: the original no-pickup preference, the centre of
        // its complete slow/fast envelope, and the earliest comfortable
        // landing. Every command is still independently simulated.
        float targetCenter = (scan.Next.SafeLeft + scan.Next.SafeRight) * 0.5f;
        float preferredTravelCenter =
            (preferred.MinimumTravel + preferred.MaximumTravel) * 0.50f;
        float centeredLaunchX = Mathf.Clamp(
            targetCenter - preferredTravelCenter,
            usableLeft,
            usableRight);
        BonusJumpPlan preferredProbe = new(
            true,
            false,
            requestedHold,
            flightSeconds,
            preferred.ExpectedTravel,
            clampedPreferred,
            clampedPreferred + preferred.ExpectedTravel,
            usableLeft,
            usableRight,
            "SpiritLaunch3PreferredProbe",
            string.Empty,
            BonusManeuverKind.GroundJumpToLanding,
            preferred.GuaranteedSphereHits,
            preferred.MinimumTravel,
            preferred.MaximumTravel,
            spiritBoost.RequiresConservativeImmediateBoost ||
                preferred.TriggerHits > 0,
            preferred.TriggerHits);
        float preferredSafety = GetLandingFirstSafetyMargin(
            preferredProbe,
            scan.Next,
            clampedPreferred,
            speed,
            physics,
            hasTierCalibration,
            tierSampleCount);
        float preferredRawMargin = Mathf.Min(
            clampedPreferred + preferred.MinimumTravel - scan.Next.SafeLeft,
            scan.Next.SafeRight -
                (clampedPreferred + preferred.MaximumTravel));
        float modeledUncertainty = Mathf.Max(
            0f,
            preferredRawMargin - preferredSafety);
        float earliestComfortableLaunchX = Mathf.Clamp(
            scan.Next.SafeLeft + modeledUncertainty +
                ComfortableSoulLandingMargin - preferred.MinimumTravel,
            usableLeft,
            usableRight);

        bool bestFound = false;
        float bestSafety = float.NegativeInfinity;
        float bestCenterError = float.PositiveInfinity;
        int bestSphereHits = -1;
        int bestSpeedBoostHits = -1;
        int bestIndex = -1;
        int evaluatedCount = 0;
        StringBuilder probeEvidence = new();
        for (int index = 0; index < 3; index++)
        {
            float launchX = index == 0
                ? clampedPreferred
                : index == 1
                    ? centeredLaunchX
                    : earliestComfortableLaunchX;
            bool duplicate =
                index == 1 &&
                    Mathf.Abs(launchX - clampedPreferred) <= 0.01f ||
                index == 2 &&
                    (Mathf.Abs(launchX - clampedPreferred) <= 0.01f ||
                     Mathf.Abs(launchX - centeredLaunchX) <= 0.01f);
            if (duplicate)
            {
                probeEvidence.Append(
                    $"P{index}[X={launchX:F3},Duplicate=True];");
                continue;
            }

            evaluatedCount++;
            SpeedEnvelopeEvaluation evaluation = index == 0
                ? preferred
                : EvaluateSpiritBoostTrajectoryEnvelope(
                    scan.Current,
                    scan.Next,
                    intermediateSurfaces,
                    hazard,
                    spiritBoost,
                    launchX,
                    speed,
                    requestedHold,
                    flightSeconds,
                    physics,
                    baselineTravel,
                    useRawTargetBounds: false,
                    sphereObjectives);
            if (!evaluation.IsSafe)
            {
                probeEvidence.Append(
                    $"P{index}[X={launchX:F3},Safe=False," +
                    $"Hits={evaluation.GuaranteedSphereHits}];");
                continue;
            }

            float slowLanding = launchX + evaluation.MinimumTravel;
            float fastLanding = launchX + evaluation.MaximumTravel;
            float centerError = Mathf.Max(
                Mathf.Abs(slowLanding - targetCenter),
                Mathf.Abs(fastLanding - targetCenter));
            BonusJumpPlan candidateProbe = preferredProbe with
            {
                HorizontalTravel = evaluation.ExpectedTravel,
                PlannedLaunchX = launchX,
                PredictedLandingX = launchX + evaluation.ExpectedTravel,
                ExpectedSphereHits = evaluation.GuaranteedSphereHits,
                ExpectedSpeedBoostHits = evaluation.TriggerHits,
                MinimumHorizontalTravel = evaluation.MinimumTravel,
                MaximumHorizontalTravel = evaluation.MaximumTravel,
                FutureSpeedTransitionExpected =
                    spiritBoost.RequiresConservativeImmediateBoost ||
                    evaluation.TriggerHits > 0
            };
            float safety = GetLandingFirstSafetyMargin(
                candidateProbe,
                scan.Next,
                launchX,
                speed,
                physics,
                hasTierCalibration,
                tierSampleCount);
            bool candidateSafe = safety >= 0f;
            bool bestSafe = bestSafety >= 0f;
            bool candidateComfortable =
                safety >= ComfortableSoulLandingMargin;
            bool bestComfortable =
                bestSafety >= ComfortableSoulLandingMargin;
            bool candidateVerifiedBoostObjective =
                allowVerifiedBoostMargin &&
                evaluation.TriggerHits > 0 &&
                evaluation.GuaranteedSphereHits >= bestSphereHits &&
                safety + 0.001f >= RouteLandingSafetyTier;
            bool bestVerifiedBoostObjective =
                allowVerifiedBoostMargin &&
                bestSpeedBoostHits > 0 &&
                bestSphereHits >= evaluation.GuaranteedSphereHits &&
                bestSafety + 0.001f >= RouteLandingSafetyTier;
            bool replace;
            if (!bestFound)
            {
                replace = true;
            }
            else if (candidateSafe != bestSafe)
            {
                replace = candidateSafe;
            }
            else if (candidateVerifiedBoostObjective !=
                     bestVerifiedBoostObjective)
            {
                replace = candidateVerifiedBoostObjective;
            }
            else if (candidateComfortable != bestComfortable)
            {
                replace = candidateComfortable;
            }
            else if (candidateComfortable &&
                     preferSpeedBoostCoverage &&
                     evaluation.TriggerHits != bestSpeedBoostHits)
            {
                replace =
                    evaluation.TriggerHits > bestSpeedBoostHits;
            }
            else if (candidateComfortable &&
                     preferSphereCoverage &&
                     evaluation.GuaranteedSphereHits != bestSphereHits)
            {
                replace =
                    evaluation.GuaranteedSphereHits > bestSphereHits;
            }
            else if (candidateComfortable &&
                     preferSphereCoverage &&
                     evaluation.GuaranteedSphereHits > 0 &&
                     evaluation.GuaranteedSphereHits == bestSphereHits &&
                     launchX < robustLaunchX - 0.01f)
            {
                // Once slow and fast trajectories guarantee the same pickups
                // and both retain the full comfortable landing reserve, take
                // the earlier launch. Souls already passed on the left cannot
                // be recovered, whereas forward souls remain available to a
                // later route. This is the same constrained tie-break used by
                // the mature ordinary selector; it never trades away safety
                // tier or guaranteed coverage.
                replace = true;
            }
            else
            {
                replace =
                    safety > bestSafety + 0.001f ||
                    Mathf.Abs(safety - bestSafety) <= 0.001f &&
                    centerError < bestCenterError;
            }
            probeEvidence.Append(
                $"P{index}[X={launchX:F3},Safe=True," +
                $"Margin={safety:F3},SoulHits=" +
                $"{evaluation.GuaranteedSphereHits},BoostHits=" +
                $"{evaluation.TriggerHits}," +
                $"Replace={replace}];");
            if (!replace)
                continue;

            bestFound = true;
            bestSafety = safety;
            bestCenterError = centerError;
            bestSphereHits = evaluation.GuaranteedSphereHits;
            bestSpeedBoostHits = evaluation.TriggerHits;
            bestIndex = index;
            robustLaunchX = launchX;
            robustEvaluation = evaluation;
        }

        if (!bestFound)
        {
            robustEvaluation = preferred with
            {
                Summary =
                    $"NoThreePointSpeedEnvelopeWindow[Evaluated=" +
                    $"{evaluatedCount}/3,Probes={probeEvidence}];Preferred[" +
                    $"{preferred.Summary}]"
            };
            return false;
        }

        // The actual live X is independently revalidated before DOWN. A
        // single-command window avoids presenting un-sampled positions as a
        // continuous proof and prevents route locking across trigger states.
        robustWindowLeft = robustLaunchX;
        robustWindowRight = robustLaunchX;
        robustEvaluation = robustEvaluation with
        {
            Summary = robustEvaluation.Summary +
                $";SpiritLaunch3[Selected=P{bestIndex},X=" +
                $"{robustLaunchX:F3},Margin={bestSafety:F3},Hits=" +
                $"{bestSphereHits},BoostHits={bestSpeedBoostHits}," +
                $"Evaluated={evaluatedCount}/3," +
                $"Probes={probeEvidence}]"
        };
        return true;
    }

    private SpeedEnvelopeEvaluation EvaluateSpiritBoostTrajectoryEnvelope(
        BonusBoardSegment source,
        BonusBoardSegment target,
        IReadOnlyList<BonusBoardSegment> intermediateSurfaces,
        BonusHazard hazard,
        SpiritBoostRouteContext spiritBoost,
        float launchX,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        float baselineTravel,
        bool useRawTargetBounds,
        IReadOnlyList<Vector2> sphereObjectives = null)
    {
        SpiritBoostTrajectoryTrace noPickupTrace =
            BuildSpiritBoostTrajectoryTrace(
                spiritBoost,
                launchX,
                source.Top,
                speed,
                requestedHold,
                flightSeconds,
                physics,
                allowTriggerPickups: false,
                worldTravelScale: 1f);
        float baselineRawTravel = noPickupTrace.FinalTravel;
        float calibratedTraceScale = baselineRawTravel > 0.05f
            ? Mathf.Clamp(
                baselineTravel / baselineRawTravel,
                0.50f,
                1.50f)
            : 1f;
        float sweptBodyLeft =
            launchX + spiritBoost.PlayerLeftOffset - 0.10f;
        float sweptBodyRight =
            launchX + baselineTravel +
            spiritBoost.PlayerRightOffset + 0.10f;
        bool triggerCanIntersectHorizontalSweep =
            spiritBoost.RequiresConservativeImmediateBoost ||
            (spiritBoost.ActiveTriggers ??
                Array.Empty<BonusSpeedBoostTrigger>())
            .Any(trigger =>
                trigger.IsValid &&
                trigger.Right >= sweptBodyLeft &&
                trigger.Left <= sweptBodyRight);
        SpiritBoostTrajectoryTrace boostTrace =
            triggerCanIntersectHorizontalSweep
                ? BuildSpiritBoostTrajectoryTrace(
                    spiritBoost,
                    launchX,
                    source.Top,
                    speed,
                    requestedHold,
                    flightSeconds,
                    physics,
                    allowTriggerPickups: true,
                    worldTravelScale: calibratedTraceScale)
                : noPickupTrace;
        float boostTraceReadScale =
            triggerCanIntersectHorizontalSweep
                ? 1f
                : calibratedTraceScale;
        float boostAwareTravelFromTrace =
            boostTrace.FinalTravel * boostTraceReadScale;
        int triggerHits = boostTrace.TriggerHits;
        string triggerSignature = boostTrace.TriggerSignature;
        float futureBoostDelta = Mathf.Max(
            0f,
            boostAwareTravelFromTrace - baselineTravel);
        float boostAwareTravel = baselineTravel + futureBoostDelta;
        float minimumTravel = Mathf.Min(
            baselineTravel,
            boostAwareTravel);
        float maximumTravel = Mathf.Max(
            baselineTravel,
            boostAwareTravel);
        float minimumLanding = launchX + minimumTravel;
        float maximumLanding = launchX + maximumTravel;
        float targetLeft = useRawTargetBounds
            ? target.Left
            : target.SafeLeft;
        float targetRight = useRawTargetBounds
            ? target.Right
            : target.SafeRight;
        bool bothLandingsFit =
            minimumLanding >= targetLeft &&
            maximumLanding <= targetRight;

        bool slowFaceSafe =
            TrajectoryClearsRaisedTargetFaceWithSpiritBoost(
                source,
                target,
                launchX,
                speed,
                baselineTravel,
                requestedHold,
                flightSeconds,
                physics,
                noPickupTrace,
                out string slowFaceCheck);
        bool slowHazardSafe =
            TrajectoryClearsHazardWithSpiritBoost(
                hazard,
                launchX,
                launchX + baselineTravel,
                source.Top,
                speed,
                requestedHold,
                flightSeconds,
                physics,
                noPickupTrace,
                out string slowHazardCheck);
        bool slowIntermediateSafe =
            TrajectoryClearsIntermediateSurfacesWithSpiritBoost(
                source,
                target,
                intermediateSurfaces,
                launchX,
                launchX + baselineTravel,
                speed,
                requestedHold,
                flightSeconds,
                physics,
                noPickupTrace,
                out string slowIntermediateCheck);

        string fastFaceCheck = "SameAsNoPickup";
        string fastHazardCheck = "SameAsNoPickup";
        string fastIntermediateCheck = "SameAsNoPickup";
        bool fastFaceSafe = true;
        bool fastHazardSafe = true;
        bool fastIntermediateSafe = true;
        if (triggerHits > 0)
        {
            fastFaceSafe =
                TrajectoryClearsRaisedTargetFaceWithSpiritBoost(
                source,
                target,
                launchX,
                speed,
                boostAwareTravel,
                requestedHold,
                flightSeconds,
                physics,
                boostTrace,
                out fastFaceCheck);
            fastHazardSafe =
                TrajectoryClearsHazardWithSpiritBoost(
                hazard,
                launchX,
                launchX + boostAwareTravel,
                source.Top,
                speed,
                requestedHold,
                flightSeconds,
                physics,
                boostTrace,
                out fastHazardCheck);
            fastIntermediateSafe =
                TrajectoryClearsIntermediateSurfacesWithSpiritBoost(
                source,
                target,
                intermediateSurfaces,
                launchX,
                launchX + boostAwareTravel,
                speed,
                requestedHold,
                flightSeconds,
                physics,
                boostTrace,
                out fastIntermediateCheck);
        }

        bool safe = bothLandingsFit &&
            slowFaceSafe && slowHazardSafe && slowIntermediateSafe &&
            fastFaceSafe && fastHazardSafe && fastIntermediateSafe;
        float expectedTravel = triggerHits > 0
            ? boostAwareTravel
            : baselineTravel;
        int guaranteedSphereHits = 0;
        if (sphereObjectives != null && sphereObjectives.Count > 0)
        {
            HashSet<int> noPickupSphereHits =
                GetTrajectorySphereHitIndicesOnTrace(
                    sphereObjectives,
                    launchX,
                    source.Top,
                    requestedHold,
                    flightSeconds,
                    physics,
                    noPickupTrace,
                    calibratedTraceScale);
            HashSet<int> pickupSphereHits =
                GetTrajectorySphereHitIndicesOnTrace(
                    sphereObjectives,
                    launchX,
                    source.Top,
                    requestedHold,
                    flightSeconds,
                    physics,
                    boostTrace,
                    boostTraceReadScale);
            noPickupSphereHits.IntersectWith(pickupSphereHits);
            guaranteedSphereHits = noPickupSphereHits.Count;
        }
        string summary =
            $"SpiritEnvelope[Safe={safe},Launch={launchX:F3}," +
            $"NoPickupD={baselineTravel:F3},PickupD=" +
            $"{boostAwareTravel:F3},Delta={futureBoostDelta:F3}," +
            $"RawNoPickupD={baselineRawTravel:F3}," +
            $"ScaledPickupD={boostAwareTravelFromTrace:F3},XScale=" +
            $"{calibratedTraceScale:F3}," +
            $"Landings=[{minimumLanding:F3},{maximumLanding:F3}]," +
            $"Target=[{targetLeft:F3},{targetRight:F3}],Hits=" +
            $"{triggerHits}:{triggerSignature},Slow=" +
            $"{slowFaceCheck};{slowHazardCheck};" +
            $"{slowIntermediateCheck},Fast={fastFaceCheck};" +
            $"{fastHazardCheck};{fastIntermediateCheck},SoulHits=" +
            $"{guaranteedSphereHits}]";
        return new SpeedEnvelopeEvaluation(
            safe,
            expectedTravel,
            minimumTravel,
            maximumTravel,
            triggerHits,
            triggerSignature,
            guaranteedSphereHits,
            summary);
    }

    private static BonusBoardSegment[] GetIntermediateClearanceSurfaces(
        BonusBoardScanResult scan)
    {
        List<BonusBoardSegment> surfaces = new();
        if (scan.HasIntermediate && scan.Intermediate.Width > 0.05f)
            surfaces.Add(scan.Intermediate);
        if (scan.Alternatives != null)
        {
            surfaces.AddRange(scan.Alternatives.Where(surface =>
                surface.Width > 0.05f));
        }
        return surfaces
            .GroupBy(surface =>
                $"{surface.Left:F2}:{surface.Right:F2}:" +
                $"{surface.Top:F2}")
            .Select(group => group.First())
            .ToArray();
    }

    private static SpiritBoostTrajectoryTrace BuildSpiritBoostTrajectoryTrace(
        SpiritBoostRouteContext spiritBoost,
        float launchX,
        float launchFeetY,
        float initialSpeed,
        float requestedHold,
        float elapsedSeconds,
        JumpPhysicsSnapshot physics,
        bool allowTriggerPickups,
        float worldTravelScale)
    {
        float duration = Mathf.Max(0f, elapsedSeconds);
        float horizontalScale = Mathf.Clamp(
            worldTravelScale,
            0.50f,
            1.50f);
        BonusSpeedBoostTrigger[] triggers = allowTriggerPickups
            ? spiritBoost.ActiveTriggers ??
                Array.Empty<BonusSpeedBoostTrigger>()
            : Array.Empty<BonusSpeedBoostTrigger>();
        float baseSpeed = Mathf.Clamp(
            spiritBoost.BaseHorizontalSpeed,
            1f,
            Mathf.Max(1f, initialSpeed));
        bool resetCanOccur =
            allowTriggerPickups &&
            (spiritBoost.RequiresConservativeImmediateBoost ||
             triggers.Any(trigger => trigger.IsValid));
        bool stableBaseSpeedWithoutReset =
            !resetCanOccur &&
            initialSpeed <= baseSpeed + 0.01f;
        // The game integrates movement and consumes jump/trigger state on
        // this native fixed cadence. Half-step sampling doubled every V0.64
        // slow/fast proof without adding an observable control state; the
        // trigger plus player envelope is wider than one maximum-speed native
        // travel. Retain every real physics boundary and the exact duration
        // endpoint.
        float step = Mathf.Clamp(
            physics.FixedDeltaTime,
            0.005f,
            0.05f);
        if (!spiritBoost.RequiresSpeedEnvelope ||
            elapsedSeconds <= 0f ||
            stableBaseSpeedWithoutReset)
        {
            float[] twoPointTimes = new float[2];
            float[] twoPointTravels = new float[2];
            twoPointTimes[0] = 0f;
            twoPointTravels[0] = 0f;
            twoPointTimes[1] = duration;
            twoPointTravels[1] = PredictHorizontalTravelAtTime(
                initialSpeed,
                elapsedSeconds,
                physics) * horizontalScale;
            return new SpiritBoostTrajectoryTrace(
                twoPointTimes,
                twoPointTravels,
                2,
                0,
                "None");
        }

        int capacity = Mathf.Max(
            2,
            Mathf.CeilToInt(duration / step) + 2);
        float[] times = new float[capacity];
        float[] travels = new float[capacity];
        float decrease = Mathf.Clamp(
            spiritBoost.BoostDecreasePerSecond,
            0.01f,
            120f);
        float speed = Mathf.Max(baseSpeed, initialSpeed);
        float travel = 0f;
        float elapsed = 0f;
        int sampleCount = 0;
        int triggerHits = 0;
        string triggerSignature = "None";
        ulong pickedMask = 0UL;

        if (allowTriggerPickups &&
            spiritBoost.RequiresConservativeImmediateBoost)
        {
            triggerHits = 1;
            triggerSignature = "UnknownTriggerState:AssumeImmediate";
            speed = Mathf.Max(
                speed,
                baseSpeed + spiritBoost.MaximumBoostComponent);
        }

        while (elapsed <= duration + 0.0001f)
        {
            times[sampleCount] = elapsed;
            travels[sampleCount] = travel;
            sampleCount++;
            float bodyX = launchX + travel;
            float bodyFeetY = launchFeetY +
                PredictVerticalDisplacementAtTime(
                    requestedHold,
                    elapsed,
                    physics);
            int triggerLimit = Mathf.Min(63, triggers.Length);
            for (int index = 0; index < triggerLimit; index++)
            {
                ulong bit = 1UL << index;
                if ((pickedMask & bit) != 0UL)
                    continue;

                BonusSpeedBoostTrigger trigger = triggers[index];
                bool overlaps =
                    bodyX + spiritBoost.PlayerRightOffset >=
                        trigger.Left &&
                    bodyX + spiritBoost.PlayerLeftOffset <=
                        trigger.Right &&
                    bodyFeetY + spiritBoost.PlayerTopOffset >=
                        trigger.Bottom &&
                    bodyFeetY + spiritBoost.PlayerBottomOffset <=
                        trigger.Top;
                if (!overlaps)
                    continue;

                pickedMask |= bit;
                triggerHits++;
                speed = Mathf.Max(
                    speed,
                    baseSpeed + spiritBoost.MaximumBoostComponent);
            }

            if (elapsed >= duration)
                break;

            float delta = Mathf.Min(step, duration - elapsed);
            float nextSpeed = Mathf.Max(
                baseSpeed,
                speed - decrease * delta);
            travel +=
                (speed + nextSpeed) * 0.5f * delta * horizontalScale;
            speed = nextSpeed;
            elapsed += delta;
        }

        if (pickedMask != 0UL)
            triggerSignature = $"Mask=0x{pickedMask:X}";
        return new SpiritBoostTrajectoryTrace(
            times,
            travels,
            sampleCount,
            triggerHits,
            triggerSignature);
    }

    private SpiritWallContactOutcome SimulateSpiritWallContact(
        SpiritBoostRouteContext spiritBoost,
        float launchX,
        float launchFeetY,
        float initialSpeed,
        float requestedHold,
        float wallContactX,
        float maximumContactSeconds,
        JumpPhysicsSnapshot physics,
        BonusHazard hazard,
        bool allowTriggerPickups,
        float worldTravelScale)
    {
        float horizontalScale = Mathf.Clamp(
            worldTravelScale,
            0.75f,
            1.25f);
        float fixedStep = Mathf.Clamp(
            physics.FixedDeltaTime,
            0.005f,
            0.05f);
        float integrationStep = Mathf.Min(0.01f, fixedStep * 0.5f);
        float baseSpeed = Mathf.Max(
            1f,
            spiritBoost.BaseHorizontalSpeed > 0.01f
                ? spiritBoost.BaseHorizontalSpeed
                : physics.BaseHorizontalSpeed);
        float decrease = Mathf.Max(
            0.10f,
            spiritBoost.BoostDecreasePerSecond > 0.01f
                ? spiritBoost.BoostDecreasePerSecond
                : physics.BoostHorizontalDeceleration);
        float horizontalSpeed = Mathf.Max(baseSpeed, initialSpeed);
        float x = launchX;
        float elapsed = 0f;
        bool hazardSafe = true;
        int triggerHits = 0;
        ulong pickedMask = 0UL;
        string triggerSignature = "None";
        BonusSpeedBoostTrigger[] triggers =
            allowTriggerPickups
                ? spiritBoost.ActiveTriggers ??
                    Array.Empty<BonusSpeedBoostTrigger>()
                : Array.Empty<BonusSpeedBoostTrigger>();
        if (allowTriggerPickups &&
            spiritBoost.RequiresConservativeImmediateBoost)
        {
            horizontalSpeed = Mathf.Max(
                horizontalSpeed,
                baseSpeed + spiritBoost.MaximumBoostComponent);
            triggerHits = 1;
            triggerSignature = "UnknownTriggerState:AssumeImmediate";
        }

        while (elapsed <= maximumContactSeconds + 0.0001f)
        {
            float feetY = PredictVerticalYAtTime(
                launchFeetY,
                requestedHold,
                elapsed,
                physics);
            int triggerLimit = Mathf.Min(63, triggers.Length);
            for (int index = 0; index < triggerLimit; index++)
            {
                ulong bit = 1UL << index;
                BonusSpeedBoostTrigger trigger = triggers[index];
                if ((pickedMask & bit) != 0UL || !trigger.IsValid)
                    continue;

                bool overlaps =
                    x + spiritBoost.PlayerRightOffset >= trigger.Left &&
                    x + spiritBoost.PlayerLeftOffset <= trigger.Right &&
                    feetY + spiritBoost.PlayerTopOffset >= trigger.Bottom &&
                    feetY + spiritBoost.PlayerBottomOffset <= trigger.Top;
                if (!overlaps)
                    continue;

                pickedMask |= bit;
                triggerHits++;
                horizontalSpeed = Mathf.Max(
                    horizontalSpeed,
                    baseSpeed + spiritBoost.MaximumBoostComponent);
            }

            if (x >= wallContactX - 0.0001f)
            {
                if (pickedMask != 0UL)
                    triggerSignature = $"Mask=0x{pickedMask:X}";
                float clearance = feetY;
                return new SpiritWallContactOutcome(
                    true,
                    elapsed,
                    feetY,
                    hazardSafe,
                    triggerHits,
                    triggerSignature,
                    $"Reached=True,T={elapsed:F3},FeetY=" +
                    $"{clearance:F3},HazardSafe={hazardSafe},Hits=" +
                    $"{triggerHits}:{triggerSignature}");
            }
            if (elapsed >= maximumContactSeconds)
                break;

            float delta = Mathf.Min(
                integrationStep,
                maximumContactSeconds - elapsed);
            float nextSpeed = Mathf.Max(
                baseSpeed,
                horizontalSpeed - decrease * delta);
            float nextX = x +
                (horizontalSpeed + nextSpeed) * 0.5f *
                delta * horizontalScale;
            float clippedNextX = Mathf.Min(nextX, wallContactX);
            float fraction = nextX > x + 0.0001f
                ? Mathf.Clamp01((clippedNextX - x) / (nextX - x))
                : 1f;
            float nextElapsed = elapsed + delta * fraction;
            float nextFeetY = PredictVerticalYAtTime(
                launchFeetY,
                requestedHold,
                nextElapsed,
                physics);
            if (hazard.IsValid)
            {
                float sweptLeft = Mathf.Min(
                    x + spiritBoost.PlayerLeftOffset,
                    clippedNextX + spiritBoost.PlayerLeftOffset);
                float sweptRight = Mathf.Max(
                    x + spiritBoost.PlayerRightOffset,
                    clippedNextX + spiritBoost.PlayerRightOffset);
                bool overlapsHazardX =
                    sweptRight >= hazard.Left &&
                    sweptLeft <= hazard.Right;
                if (overlapsHazardX &&
                    Mathf.Min(feetY, nextFeetY) < hazard.Top + 0.30f)
                {
                    hazardSafe = false;
                }
            }

            if (nextX >= wallContactX - 0.0001f)
            {
                if (pickedMask != 0UL)
                    triggerSignature = $"Mask=0x{pickedMask:X}";
                return new SpiritWallContactOutcome(
                    true,
                    nextElapsed,
                    nextFeetY,
                    hazardSafe,
                    triggerHits,
                    triggerSignature,
                    $"Reached=True,T={nextElapsed:F3},FeetY=" +
                    $"{nextFeetY:F3},HazardSafe={hazardSafe},Hits=" +
                    $"{triggerHits}:{triggerSignature}");
            }

            x = nextX;
            elapsed += delta;
            horizontalSpeed = nextSpeed;
        }

        if (pickedMask != 0UL)
            triggerSignature = $"Mask=0x{pickedMask:X}";
        float finalFeetY = PredictVerticalYAtTime(
            launchFeetY,
            requestedHold,
            maximumContactSeconds,
            physics);
        return new SpiritWallContactOutcome(
            false,
            maximumContactSeconds,
            finalFeetY,
            hazardSafe,
            triggerHits,
            triggerSignature,
            $"Reached=False,X={x:F3}/{wallContactX:F3},T=" +
            $"{maximumContactSeconds:F3},FeetY={finalFeetY:F3}," +
            $"HazardSafe={hazardSafe},Hits=" +
            $"{triggerHits}:{triggerSignature}");
    }

    private static float SolveSpiritBoostTraceTravelTime(
        SpiritBoostTrajectoryTrace trace,
        float requiredTravel,
        float maximumTime,
        float trajectoryTravelScale = 1f)
    {
        if (requiredTravel <= 0f)
            return 0f;
        if (trace.Count <= 0 || trace.Times == null ||
            trace.Travels == null)
        {
            return Mathf.Max(0.05f, maximumTime) + 0.05f;
        }

        int low = 0;
        int high = trace.Count - 1;
        float scale = Mathf.Clamp(
            trajectoryTravelScale,
            0.50f,
            1.50f);
        if (trace.Travels[high] * scale < requiredTravel)
            return Mathf.Max(maximumTime, trace.Times[high]) + 0.05f;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (trace.Travels[middle] * scale < requiredTravel)
                low = middle;
            else
                high = middle;
        }

        float leftTravel = trace.Travels[low] * scale;
        float rightTravel = trace.Travels[high] * scale;
        float fraction = rightTravel > leftTravel + 0.0001f
            ? Mathf.Clamp01(
                (requiredTravel - leftTravel) /
                (rightTravel - leftTravel))
            : 1f;
        return Mathf.Lerp(
            trace.Times[low],
            trace.Times[high],
            fraction);
    }

    private static float GetSpiritBoostTraceTravelAtTime(
        SpiritBoostTrajectoryTrace trace,
        float elapsedSeconds)
    {
        if (trace.Count <= 0 || trace.Times == null ||
            trace.Travels == null)
        {
            return 0f;
        }
        if (elapsedSeconds <= trace.Times[0])
            return trace.Travels[0];
        int last = trace.Count - 1;
        if (elapsedSeconds >= trace.Times[last])
            return trace.Travels[last];

        int low = 0;
        int high = last;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (trace.Times[middle] < elapsedSeconds)
                low = middle;
            else
                high = middle;
        }

        float fraction = trace.Times[high] > trace.Times[low] + 0.0001f
            ? Mathf.Clamp01(
                (elapsedSeconds - trace.Times[low]) /
                (trace.Times[high] - trace.Times[low]))
            : 1f;
        return Mathf.Lerp(
            trace.Travels[low],
            trace.Travels[high],
            fraction);
    }

    private bool TrajectoryClearsRaisedTargetFaceWithSpiritBoost(
        BonusBoardSegment source,
        BonusBoardSegment target,
        float launchX,
        float speed,
        float calibratedTravel,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        SpiritBoostTrajectoryTrace boostTrace,
        out string summary)
    {
        float physicalGap = Mathf.Max(0f, target.Left - source.Right);
        bool targetFaceCanIntercept =
            physicalGap > 0.10f ||
            target.Top > source.Top + 0.35f;
        if (!targetFaceCanIntercept)
        {
            summary = "BoostTargetFace=NoInterceptingFace";
            return true;
        }

        float inferredHalfWidth = Mathf.Max(
            0.15f,
            Mathf.Max(
                Mathf.Min(
                    source.SafeLeft - source.Left,
                    source.Right - source.SafeRight),
                Mathf.Min(
                    target.SafeLeft - target.Left,
                    target.Right - target.SafeRight)) - 0.15f);
        float faceX = target.Left - inferredHalfWidth;
        float safeEntryX = Mathf.Max(faceX, target.SafeLeft);
        float traceTravelScale = boostTrace.FinalTravel > 0.05f
            ? Mathf.Clamp(
                calibratedTravel / boostTrace.FinalTravel,
                0.50f,
                1.50f)
            : 1f;
        float faceTime = SolveSpiritBoostTraceTravelTime(
            boostTrace,
            faceX - launchX,
            flightSeconds,
            traceTravelScale);
        float safeEntryTime = SolveSpiritBoostTraceTravelTime(
            boostTrace,
            safeEntryX - launchX,
            flightSeconds,
            traceTravelScale);
        float faceFeetY = PredictVerticalYAtTime(
            source.Top,
            requestedHold,
            faceTime,
            physics);
        float safeEntryFeetY = PredictVerticalYAtTime(
            source.Top,
            requestedHold,
            safeEntryTime,
            physics);
        bool clears =
            faceTime <= flightSeconds + 0.02f &&
            safeEntryTime <= flightSeconds + 0.02f &&
            faceFeetY >= target.Top + RaisedTargetFaceClearance &&
            safeEntryFeetY >=
                target.Top + RaisedTargetSafeEntryClearance;
        summary =
            $"BoostTargetFace[X={faceX:F3},T={faceTime:F3}," +
            $"Y={faceFeetY:F3},SafeX={safeEntryX:F3}," +
            $"SafeT={safeEntryTime:F3},SafeY={safeEntryFeetY:F3}," +
            $"RawD={boostTrace.FinalTravel:F3},CalibratedD=" +
            $"{calibratedTravel:F3},XScale={traceTravelScale:F3}," +
            $"Clear={clears}]";
        return clears;
    }

    private static bool TrajectoryClearsHazardWithSpiritBoost(
        BonusHazard hazard,
        float launchX,
        float landingX,
        float launchFeetY,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        SpiritBoostTrajectoryTrace boostTrace,
        out string summary)
    {
        bool intersectsX = hazard.IsValid &&
            hazard.Right >= launchX && hazard.Left <= landingX;
        if (!intersectsX)
        {
            summary = hazard.IsValid
                ? "BoostHazardOutsideFlight"
                : "NoHazard";
            return true;
        }

        float hazardX = Mathf.Clamp(
            hazard.CenterX,
            launchX,
            landingX);
        float traceTravelScale = boostTrace.FinalTravel > 0.05f
            ? Mathf.Clamp(
                (landingX - launchX) / boostTrace.FinalTravel,
                0.50f,
                1.50f)
            : 1f;
        float time = SolveSpiritBoostTraceTravelTime(
            boostTrace,
            hazardX - launchX,
            flightSeconds,
            traceTravelScale);
        float feetY = launchFeetY + PredictVerticalDisplacementAtTime(
            requestedHold,
            time,
            physics);
        float requiredFeetY = hazard.Top + 0.30f;
        bool clears = feetY >= requiredFeetY;
        summary =
            $"BoostHazard[X={hazardX:F3},T={time:F3}," +
            $"Y={feetY:F3},Need={requiredFeetY:F3},Clear={clears}]";
        return clears;
    }

    private bool TrajectoryClearsIntermediateSurfacesWithSpiritBoost(
        BonusBoardSegment source,
        BonusBoardSegment target,
        IReadOnlyList<BonusBoardSegment> forwardSurfaces,
        float launchX,
        float landingX,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        SpiritBoostTrajectoryTrace boostTrace,
        out string summary)
    {
        float traceTravelScale = boostTrace.FinalTravel > 0.05f
            ? Mathf.Clamp(
                (landingX - launchX) / boostTrace.FinalTravel,
                0.50f,
                1.50f)
            : 1f;
        float inferredHalfWidth = Mathf.Max(
            0.15f,
            source.SafeLeft - source.Left - 0.15f);
        StringBuilder checks = new();
        bool sourceLipSafe =
            TrajectoryClearsSourceLipOnDescentWithSpiritBoost(
                source,
                target,
                launchX,
                requestedHold,
                flightSeconds,
                physics,
                boostTrace,
                traceTravelScale,
                inferredHalfWidth,
                out string sourceLipCheck);
        checks.Append(sourceLipCheck).Append(';');
        if (!sourceLipSafe)
        {
            summary = checks.ToString();
            return false;
        }
        foreach (BonusBoardSegment surface in forwardSurfaces
                     .Where(surface =>
                         !SameSurfaceGeometry(surface, source) &&
                         !SameSurfaceGeometry(surface, target) &&
                         surface.Left - inferredHalfWidth <
                            landingX - 0.02f &&
                         surface.Right + inferredHalfWidth >
                            launchX + 0.02f)
                     .OrderBy(surface => surface.Left))
        {
            float entryX = Mathf.Max(
                launchX + 0.02f,
                surface.Left - inferredHalfWidth);
            float exitX = Mathf.Min(
                landingX - 0.02f,
                surface.Right + inferredHalfWidth);
            if (exitX <= entryX)
                continue;

            float entryTime = SolveSpiritBoostTraceTravelTime(
                boostTrace,
                entryX - launchX,
                flightSeconds,
                traceTravelScale);
            float exitTime = SolveSpiritBoostTraceTravelTime(
                boostTrace,
                exitX - launchX,
                flightSeconds,
                traceTravelScale);
            if (entryTime > flightSeconds + 0.02f)
                continue;

            float entryFeetY = PredictVerticalYAtTime(
                source.Top,
                requestedHold,
                entryTime,
                physics);
            float exitFeetY = PredictVerticalYAtTime(
                source.Top,
                requestedHold,
                Mathf.Min(exitTime, flightSeconds),
                physics);
            float requiredFeetY = surface.Top + 0.10f;
            bool faceIntercept = entryFeetY < requiredFeetY;
            bool earlyTopLanding =
                !faceIntercept && exitFeetY < requiredFeetY;
            checks.Append(
                $"[{surface.Left:F2},{surface.Right:F2}]@" +
                $"{surface.Top:F2}:Tin={entryTime:F3}/" +
                $"Yin={entryFeetY:F2},Tout={exitTime:F3}/" +
                $"Yout={exitFeetY:F2},Need={requiredFeetY:F2}," +
                $"{(faceIntercept ? "FaceIntercept" :
                    earlyTopLanding ? "EarlyTopLanding" : "Clear")};");
            if (faceIntercept || earlyTopLanding)
            {
                summary = checks.ToString();
                return false;
            }
        }

        summary = checks.ToString();
        return true;
    }

    private bool TrajectoryClearsSourceLipOnDescentWithSpiritBoost(
        BonusBoardSegment source,
        BonusBoardSegment target,
        float launchX,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        SpiritBoostTrajectoryTrace trace,
        float traceTravelScale,
        float inferredHalfWidth,
        out string summary)
    {
        if (target.Top >= source.Top - 0.35f)
        {
            summary = "BoostSourceLip=NotLowerTarget";
            return true;
        }

        float crossingTime = FindDescendingSourceTopCrossingTime(
            requestedHold,
            flightSeconds,
            physics);
        if (crossingTime > flightSeconds + 0.001f)
        {
            summary = "BoostSourceLip=AboveSourceUntilLanding";
            return true;
        }

        float crossingTravel = GetSpiritBoostTraceTravelAtTime(
            trace,
            crossingTime) * traceTravelScale;
        float crossingX = launchX + crossingTravel;
        float requiredClearX =
            source.Right + inferredHalfWidth + 0.02f;
        bool clears = crossingX >= requiredClearX;
        summary =
            $"BoostSourceLip[T={crossingTime:F3}," +
            $"X={crossingX:F3},NeedX={requiredClearX:F3}," +
            $"HalfWidth={inferredHalfWidth:F3},Clear={clears}]";
        return clears;
    }

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

    private bool TrajectoryClearsRaisedTargetFace(
        BonusBoardSegment source,
        BonusBoardSegment target,
        float launchX,
        float speed,
        float calibratedTravel,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        out string summary)
    {
        float physicalGap = Mathf.Max(0f, target.Left - source.Right);
        bool targetFaceCanIntercept =
            physicalGap > 0.10f ||
            target.Top > source.Top + 0.35f;
        if (!targetFaceCanIntercept)
        {
            summary = "TargetFace=NoInterceptingFace";
            return true;
        }

        float inferredHalfWidth = Mathf.Max(
            0.15f,
            Mathf.Max(
                Mathf.Min(
                    source.SafeLeft - source.Left,
                    source.Right - source.SafeRight),
                Mathf.Min(
                target.SafeLeft - target.Left,
                    target.Right - target.SafeRight)) - 0.15f);
        float faceContactCenterX = target.Left - inferredHalfWidth;
        float travelToFace = faceContactCenterX - launchX;
        if (travelToFace <= 0f)
        {
            summary =
                $"TargetFace=AlreadyReached,ContactX=" +
                $"{faceContactCenterX:F3},LaunchX={launchX:F3}";
            return false;
        }

        float rawTravel = PredictHorizontalTravelAtTime(
            speed,
            flightSeconds,
            physics);
        float trajectoryTravelScale =
            GetCalibratedHorizontalTravelScale(
                speed,
                flightSeconds,
                calibratedTravel,
                physics);
        float contactTime = SolveHorizontalTravelTime(
            speed,
            travelToFace,
            flightSeconds,
            physics,
            trajectoryTravelScale);
        float contactFeetY = PredictVerticalYAtTime(
            source.Top,
            requestedHold,
            contactTime,
            physics);
        float safeEntryCenterX = Mathf.Max(
            faceContactCenterX,
            target.SafeLeft);
        float safeEntryTime = SolveHorizontalTravelTime(
            speed,
            safeEntryCenterX - launchX,
            flightSeconds,
            physics,
            trajectoryTravelScale);
        float safeEntryFeetY = PredictVerticalYAtTime(
            source.Top,
            requestedHold,
            safeEntryTime,
            physics);
        float requiredFaceFeetY =
            target.Top + RaisedTargetFaceClearance;
        float requiredSafeEntryFeetY =
            target.Top + RaisedTargetSafeEntryClearance;
        bool reachedFaceDuringFlight =
            contactTime <= flightSeconds + 0.02f;
        bool reachedSafeEntryDuringFlight =
            safeEntryTime <= flightSeconds + 0.02f;
        bool clears =
            reachedFaceDuringFlight &&
            reachedSafeEntryDuringFlight &&
            contactFeetY >= requiredFaceFeetY &&
            safeEntryFeetY >= requiredSafeEntryFeetY;
        summary =
            $"TargetFace[ContactX={faceContactCenterX:F3}," +
            $"T={contactTime:F3}/{flightSeconds:F3},FeetY=" +
            $"{contactFeetY:F3},Need={requiredFaceFeetY:F3}," +
            $"SafeEntryX={safeEntryCenterX:F3},SafeEntryT=" +
            $"{safeEntryTime:F3},SafeEntryFeetY=" +
            $"{safeEntryFeetY:F3},SafeEntryNeed=" +
            $"{requiredSafeEntryFeetY:F3}," +
            $"RawD={rawTravel:F3},CalibratedD=" +
            $"{calibratedTravel:F3},XScale=" +
            $"{trajectoryTravelScale:F3}," +
            $"Clear={clears}]";
        return clears;
    }

    internal bool IsRaisedTargetFaceClear(
        BonusBoardSegment source,
        BonusBoardSegment target,
        float launchX,
        float speed,
        float calibratedTravel,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        out string summary) =>
        TrajectoryClearsRaisedTargetFace(
            source,
            target,
            launchX,
            speed,
            calibratedTravel,
            requestedHold,
            flightSeconds,
            physics,
            out summary);

    internal static int CountTrajectoryObjectiveHits(
        IReadOnlyList<Vector2> objectives,
        float launchX,
        float landingX,
        float launchFeetY,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics) =>
        CountTrajectorySphereHits(
            objectives,
            launchX,
            landingX,
            launchFeetY,
            speed,
            requestedHold,
            flightSeconds,
            physics);

    private static int CountTrajectorySphereHits(
        IReadOnlyList<Vector2> spheres,
        float launchX,
        float landingX,
        float launchFeetY,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics)
        => GetTrajectorySphereHitIndices(
            spheres,
            launchX,
            landingX,
            launchFeetY,
            speed,
            requestedHold,
            flightSeconds,
            physics).Count;

    private static bool MayTrajectoryHitAnySphereBroadPhase(
        IReadOnlyList<Vector2> spheres,
        float launchX,
        float launchFeetY,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        float calibratedTravel)
    {
        if (spheres == null ||
            spheres.Count == 0 ||
            calibratedTravel <= 0.01f)
        {
            return false;
        }

        const float broadPhaseHorizontalPadding = 0.20f;
        const float broadPhaseVerticalPadding = 0.30f;
        float landingX = launchX + calibratedTravel;
        float rawTravel = PredictHorizontalTravelAtTime(
            speed,
            flightSeconds,
            physics);
        float trajectoryTravelScale = rawTravel > 0.05f
            ? Mathf.Clamp(
                calibratedTravel / rawTravel,
                0.50f,
                1.50f)
            : 1f;
        foreach (Vector2 sphere in spheres)
        {
            if (sphere.x <
                    launchX -
                    SpherePickupHorizontalReach -
                    broadPhaseHorizontalPadding ||
                sphere.x >
                    landingX +
                    SpherePickupHorizontalReach +
                    broadPhaseHorizontalPadding)
            {
                continue;
            }

            float requiredTravel = Mathf.Clamp(
                sphere.x - launchX,
                0f,
                calibratedTravel);
            float crossingTime = SolveHorizontalTravelTime(
                speed,
                requiredTravel,
                flightSeconds,
                physics,
                trajectoryTravelScale);
            if (crossingTime > flightSeconds + 0.05f)
                continue;

            float predictedFeetY = launchFeetY +
                PredictVerticalDisplacementAtTime(
                    requestedHold,
                    crossingTime,
                    physics);
            if (sphere.y >=
                    predictedFeetY -
                    SpherePickupBelowFeet -
                    broadPhaseVerticalPadding &&
                sphere.y <=
                    predictedFeetY +
                    SpherePickupAboveFeet +
                    broadPhaseVerticalPadding)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<int> GetTrajectorySphereHitIndices(
        IReadOnlyList<Vector2> spheres,
        float launchX,
        float landingX,
        float launchFeetY,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics)
    {
        HashSet<int> hits = new();
        if (spheres == null || spheres.Count == 0 || landingX <= launchX)
            return hits;

        float trajectoryTravelScale =
            GetCalibratedHorizontalTravelScale(
                speed,
                flightSeconds,
                landingX - launchX,
                physics);
        for (int index = 0; index < spheres.Count; index++)
        {
            Vector2 sphere = spheres[index];
            if (sphere.x < launchX - SpherePickupHorizontalReach ||
                sphere.x > landingX + SpherePickupHorizontalReach)
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
                physics,
                trajectoryTravelScale);
            if (time > flightSeconds + 0.03f)
                continue;

            float predictedFeetY = launchFeetY +
                PredictVerticalDisplacementAtTime(
                    requestedHold,
                    time,
                    physics);
            // This score only ranks already-safe landing trajectories; it
            // never makes an unsafe landing legal.
            if (TrajectoryBodyOverlapsSphere(predictedFeetY, sphere.y))
                hits.Add(index);
        }

        return hits;
    }

    /// <summary>
    /// Scores pickups with the same piecewise horizontal traces used by the
    /// Spirit safety proof. A route receives credit only for objectives that
    /// are intersected in every currently plausible speed outcome; pickup
    /// utility can rank safe commands but cannot select a command whose soul
    /// estimate came from a different X(t) model.
    /// </summary>
    private static int CountTrajectorySphereHitsAcrossSpeedScenarios(
        IReadOnlyList<Vector2> spheres,
        float launchX,
        float launchFeetY,
        float speed,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        SpiritBoostRouteContext spiritBoost,
        float calibratedNoPickupTravel)
        => GetTrajectorySphereHitIndicesAcrossSpeedScenarios(
            spheres,
            launchX,
            launchFeetY,
            speed,
            requestedHold,
            flightSeconds,
            physics,
            spiritBoost,
            calibratedNoPickupTravel).Count;

    private static HashSet<int>
        GetTrajectorySphereHitIndicesAcrossSpeedScenarios(
            IReadOnlyList<Vector2> spheres,
            float launchX,
            float launchFeetY,
            float speed,
            float requestedHold,
            float flightSeconds,
            JumpPhysicsSnapshot physics,
            SpiritBoostRouteContext spiritBoost,
            float calibratedNoPickupTravel)
    {
        if (!spiritBoost.RequiresSpeedEnvelope)
        {
            return GetTrajectorySphereHitIndices(
                spheres,
                launchX,
                launchX + calibratedNoPickupTravel,
                launchFeetY,
                speed,
                requestedHold,
                flightSeconds,
                physics);
        }

        SpiritBoostTrajectoryTrace noPickupTrace =
            BuildSpiritBoostTrajectoryTrace(
                spiritBoost,
                launchX,
                launchFeetY,
                speed,
                requestedHold,
                flightSeconds,
                physics,
                allowTriggerPickups: false,
                worldTravelScale: 1f);
        float noPickupScale = noPickupTrace.FinalTravel > 0.05f
            ? Mathf.Clamp(
                calibratedNoPickupTravel / noPickupTrace.FinalTravel,
                0.50f,
                1.50f)
            : 1f;
        SpiritBoostTrajectoryTrace pickupTrace =
            BuildSpiritBoostTrajectoryTrace(
                spiritBoost,
                launchX,
                launchFeetY,
                speed,
                requestedHold,
                flightSeconds,
                physics,
                allowTriggerPickups: true,
                worldTravelScale: noPickupScale);
        HashSet<int> noPickupHits = GetTrajectorySphereHitIndicesOnTrace(
            spheres,
            launchX,
            launchFeetY,
            requestedHold,
            flightSeconds,
            physics,
            noPickupTrace,
            noPickupScale);
        HashSet<int> pickupHits = GetTrajectorySphereHitIndicesOnTrace(
            spheres,
            launchX,
            launchFeetY,
            requestedHold,
            flightSeconds,
            physics,
            pickupTrace,
            1f);
        noPickupHits.IntersectWith(pickupHits);
        return noPickupHits;
    }

    private static int CountTrajectorySphereHitsOnTrace(
        IReadOnlyList<Vector2> spheres,
        float launchX,
        float launchFeetY,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        SpiritBoostTrajectoryTrace trace,
        float traceTravelScale)
        => GetTrajectorySphereHitIndicesOnTrace(
            spheres,
            launchX,
            launchFeetY,
            requestedHold,
            flightSeconds,
            physics,
            trace,
            traceTravelScale).Count;

    private static HashSet<int> GetTrajectorySphereHitIndicesOnTrace(
        IReadOnlyList<Vector2> spheres,
        float launchX,
        float launchFeetY,
        float requestedHold,
        float flightSeconds,
        JumpPhysicsSnapshot physics,
        SpiritBoostTrajectoryTrace trace,
        float traceTravelScale)
    {
        HashSet<int> hits = new();
        if (spheres == null || spheres.Count == 0 ||
            trace.Count <= 0 || trace.FinalTravel <= 0.01f)
        {
            return hits;
        }

        float scale = Mathf.Clamp(traceTravelScale, 0.50f, 1.50f);
        float finalTravel = trace.FinalTravel * scale;
        for (int index = 0; index < spheres.Count; index++)
        {
            Vector2 sphere = spheres[index];
            if (sphere.x < launchX - SpherePickupHorizontalReach ||
                sphere.x >
                    launchX + finalTravel + SpherePickupHorizontalReach)
            {
                continue;
            }

            float requiredTravel = Mathf.Clamp(
                sphere.x - launchX,
                0f,
                finalTravel);
            float time = SolveSpiritBoostTraceTravelTime(
                trace,
                requiredTravel,
                flightSeconds,
                scale);
            if (time > flightSeconds + 0.03f)
                continue;

            float predictedFeetY = launchFeetY +
                PredictVerticalDisplacementAtTime(
                    requestedHold,
                    time,
                    physics);
            if (TrajectoryBodyOverlapsSphere(predictedFeetY, sphere.y))
                hits.Add(index);
        }

        return hits;
    }

    private static bool TrajectoryBodyOverlapsSphere(
        float predictedFeetY,
        float sphereY) =>
        sphereY >= predictedFeetY - SpherePickupBelowFeet &&
        sphereY <= predictedFeetY + SpherePickupAboveFeet;

    /// <summary>
    /// Conservative, mode-independent route quality. Positive values are the
    /// amount of target corridor left after the complete slow/fast travel
    /// envelope and a bounded execution/model uncertainty are removed.
    /// Selection is lexicographic on this value before pickups or progress.
    /// </summary>
    private static float GetLandingFirstSafetyMargin(
        BonusJumpPlan plan,
        BonusBoardSegment target,
        float launchX,
        float speed,
        JumpPhysicsSnapshot physics,
        bool hasTierCalibration,
        int tierSampleCount)
    {
        float minimumTravel = plan.MinimumHorizontalTravel > 0f
            ? plan.MinimumHorizontalTravel
            : plan.HorizontalTravel;
        float maximumTravel = plan.MaximumHorizontalTravel > 0f
            ? plan.MaximumHorizontalTravel
            : plan.HorizontalTravel;
        float envelopeLeft = launchX + Mathf.Min(
            minimumTravel,
            maximumTravel);
        float envelopeRight = launchX + Mathf.Max(
            minimumTravel,
            maximumTravel);
        float rawMargin = Mathf.Min(
            envelopeLeft - target.SafeLeft,
            target.SafeRight - envelopeRight);

        float fixedStepUncertainty =
            Mathf.Abs(speed) *
            Mathf.Clamp(physics.FixedDeltaTime, 0.005f, 0.05f) *
            0.35f;
        float modelFraction = !hasTierCalibration
            ? 0.040f
            : tierSampleCount < 3
                ? 0.025f
                : 0.012f;
        float flightModelUncertainty =
            Mathf.Abs(speed) *
            Mathf.Max(0f, plan.PredictedFlightSeconds) *
            modelFraction;
        float transitionUncertainty =
            plan.FutureSpeedTransitionExpected ? 0.12f : 0f;
        return rawMargin -
            fixedStepUncertainty -
            flightModelUncertainty -
            transitionUncertainty;
    }

    private static float GetLandingRunway(
        BonusJumpPlan plan,
        BonusBoardSegment target,
        float launchX)
    {
        float maximumTravel = plan.MaximumHorizontalTravel > 0f
            ? Mathf.Max(
                plan.MinimumHorizontalTravel,
                plan.MaximumHorizontalTravel)
            : plan.HorizontalTravel;
        return target.SafeRight - (launchX + maximumTravel);
    }

    private static string DescribeLandingEnvelope(
        BonusJumpPlan plan,
        BonusBoardSegment target,
        float launchX)
    {
        float minimumTravel = plan.MinimumHorizontalTravel > 0f
            ? plan.MinimumHorizontalTravel
            : plan.HorizontalTravel;
        float maximumTravel = plan.MaximumHorizontalTravel > 0f
            ? plan.MaximumHorizontalTravel
            : plan.HorizontalTravel;
        float left = launchX + Mathf.Min(minimumTravel, maximumTravel);
        float right = launchX + Mathf.Max(minimumTravel, maximumTravel);
        return $"[{left:F3},{right:F3}] in " +
            $"[{target.SafeLeft:F3},{target.SafeRight:F3}]";
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
        float horizontalSpeed,
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

        float hazardX = Mathf.Clamp(hazard.CenterX, launchX, landingX);
        float trajectoryTravelScale =
            GetCalibratedHorizontalTravelScale(
                horizontalSpeed,
                flightSeconds,
                landingX - launchX,
                physics);
        float time = SolveHorizontalTravelTime(
            horizontalSpeed,
            hazardX - launchX,
            flightSeconds,
            physics,
            trajectoryTravelScale);
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
        float horizontalScale = Mathf.Clamp(
            physics.HorizontalTravelScale,
            0.75f,
            1.25f);
        travel *= horizontalScale;
        float baseSpeed = Mathf.Min(
            speed,
            Mathf.Max(1f, physics.BaseHorizontalSpeed));
        float deceleration = Mathf.Max(
            0.10f,
            physics.BoostHorizontalDeceleration);
        source =
            $"DynamicIntegral[V0={speed:F2},Base={baseSpeed:F2}," +
            $"Decel={deceleration:F2},T={predictedFlightSeconds:F3}," +
            $"TravelScale={horizontalScale:F3}]";
        return travel;
    }

    private static float ApplyLandingBias(
        float predictedTravel,
        float heightDelta,
        float requestedHold,
        JumpPhysicsSnapshot physics,
        ref string source)
    {
        float appliedBias = physics.LandingErrorProfile.GetAppliedBias(
            heightDelta,
            requestedHold,
            out float observedBias,
            out int sampleCount);
        if (sampleCount <= 0)
        {
            source += ";LandingBias[None]";
            return predictedTravel;
        }

        float weight = Mathf.Abs(observedBias) > 0.0001f
            ? appliedBias / observedBias
            : sampleCount >= 3
                ? 1f
                : sampleCount == 2
                    ? 0.75f
                    : 0.50f;
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

    private static float PredictHorizontalSpeedAtTime(
        float speed,
        float elapsedSeconds,
        JumpPhysicsSnapshot physics)
    {
        float absoluteSpeed = Mathf.Max(1f, Mathf.Abs(speed));
        float baseSpeed = Mathf.Min(
            absoluteSpeed,
            Mathf.Max(1f, physics.BaseHorizontalSpeed));
        float deceleration = Mathf.Max(
            0.10f,
            physics.BoostHorizontalDeceleration);
        return Mathf.Max(
            baseSpeed,
            absoluteSpeed - deceleration * Mathf.Max(0f, elapsedSeconds));
    }

    private static float GetCalibratedHorizontalTravelScale(
        float speed,
        float flightSeconds,
        float calibratedTravel,
        JumpPhysicsSnapshot physics)
    {
        float rawTravel = PredictHorizontalTravelAtTime(
            speed,
            flightSeconds,
            physics);
        if (rawTravel <= 0.05f || calibratedTravel <= 0.05f)
            return 1f;

        // Landing feedback describes the whole observed horizontal flight,
        // not a teleport at its endpoint. Scale the complete X(t) curve so
        // target-face, hazard, sphere and intermediate-surface crossing times
        // are derived from the same calibrated endpoint used for selection.
        return Mathf.Clamp(calibratedTravel / rawTravel, 0.50f, 1.50f);
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
        JumpPhysicsSnapshot physics,
        float trajectoryTravelScale = 1f)
    {
        float low = 0f;
        float high = Mathf.Max(0.05f, maximumTime);
        float scale = Mathf.Clamp(trajectoryTravelScale, 0.50f, 1.50f);
        while (PredictHorizontalTravelAtTime(speed, high, physics) * scale < requiredTravel &&
               high < 1.5f)
        {
            high += 0.10f;
        }

        for (int iteration = 0; iteration < 18; iteration++)
        {
            float midpoint = (low + high) * 0.5f;
            if (PredictHorizontalTravelAtTime(speed, midpoint, physics) * scale < requiredTravel)
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

    private static string CompactDiagnostic(
        string value,
        int maximumLength)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length <= maximumLength)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, Mathf.Max(16, maximumLength)) +
            $"...(+{value.Length - maximumLength} chars)";
    }

    private static BonusJumpPlan Invalid(
        string reason,
        string candidateSummary) =>
        new(false, false, 0f, 0f, 0f, 0f, 0f,
            0f, 0f, reason, candidateSummary);
}
