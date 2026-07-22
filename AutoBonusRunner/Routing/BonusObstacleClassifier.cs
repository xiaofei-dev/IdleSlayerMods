namespace AutoBonusRunner.Routing;

internal enum BonusObstacleKind
{
    Unknown,
    ContinuousRoad,
    LowerLanding,
    OrdinaryGap,
    RaisedLanding,
    NarrowPillarTrench,
    WideWallTrench,
    AdjacentWall,
    WallAcrossGap
}

internal readonly record struct BonusObstacleAssessment(
    BonusObstacleKind Kind,
    string Evidence);

internal static class BonusObstacleClassifier
{
    internal static BonusObstacleAssessment Classify(
        BonusBoardScanResult scan)
    {
        if (!scan.IsValid || !scan.HasNext)
        {
            return new(
                BonusObstacleKind.Unknown,
                $"ScanValid={scan.IsValid},HasNext={scan.HasNext},Reason={scan.Reason}");
        }

        float sourceWidth = scan.Current.Width;
        float targetWidth = scan.Next.Width;
        if (scan.Gap <= 0.10f &&
            System.MathF.Abs(scan.HeightDelta) <= 0.35f)
        {
            return new(
                BonusObstacleKind.ContinuousRoad,
                $"Gap={scan.Gap:F2},DeltaY={scan.HeightDelta:F2}");
        }

        if (scan.HeightDelta < -0.35f)
        {
            return new(
                BonusObstacleKind.LowerLanding,
                $"Gap={scan.Gap:F2},Drop={-scan.HeightDelta:F2}," +
                $"TargetWidth={targetWidth:F2}");
        }

        if (scan.HeightDelta > 0.35f && scan.Gap <= 0.35f)
        {
            return new(
                BonusObstacleKind.AdjacentWall,
                $"Gap={scan.Gap:F2},Rise={scan.HeightDelta:F2}. " +
                "The upper top is not a first-action landing target; " +
                "the executable route begins at its lower wall face.");
        }

        // The first pillar in a rising wall chain can follow a wide road, but
        // a narrow target alone does not prove that a wall extends into the
        // trench. Section 1 has floating, same-height narrow platforms that
        // must be jumped onto directly. Require either an already narrow
        // source or a nearby higher narrow continuation to prove a wall chain.
        bool hasRisingNarrowContinuation =
            scan.Alternatives != null &&
            System.Array.Exists(
                scan.Alternatives,
                candidate =>
                    candidate.Width <= 2.35f &&
                    candidate.Left >= scan.Next.Right + 0.10f &&
                    candidate.Left - scan.Next.Right <= 5.25f &&
                    candidate.Top >= scan.Next.Top + 0.30f);
        bool narrowPillarChain =
            scan.HeightDelta > 0.35f &&
            scan.Gap <= 2.25f &&
            targetWidth <= 2.35f &&
            (sourceWidth <= 2.35f || hasRisingNarrowContinuation);
        if (narrowPillarChain)
        {
            return new(
                BonusObstacleKind.NarrowPillarTrench,
                $"Entry={(sourceWidth <= 2.35f ? "PillarChain" : "WideRoadToRisingPillar")}," +
                $"RisingContinuation={hasRisingNarrowContinuation}," +
                $"SourceWidth={sourceWidth:F2},TargetWidth={targetWidth:F2}," +
                $"Gap={scan.Gap:F2},Rise={scan.HeightDelta:F2}. " +
                "Enter the trench and jump only after wall contact.");
        }

        // Bonus Stage 3 uses a static, repeating trench-wall module: the
        // lower road ends, there is an approximately four-unit opening, and
        // the next top is six units higher. Jumping from the road reaches the
        // wall near its lip and produces a long uncontrolled flight over the
        // useful platforms. The intended executable route is to coast off the
        // edge, fall inside the trench, then climb only after body contact.
        bool staticWideWallTrench =
            scan.HeightDelta >= 5.35f &&
            scan.Gap >= 2.50f &&
            scan.Gap <= 5.25f;
        if (staticWideWallTrench)
        {
            return new(
                BonusObstacleKind.WideWallTrench,
                $"Gap={scan.Gap:F2},Rise={scan.HeightDelta:F2}," +
                $"SourceWidth={sourceWidth:F2},TargetWidth={targetWidth:F2}. " +
                "Static trench-wall topology: solve a descending entry " +
                "trajectory against the physical lower wall face without " +
                "passing underneath it, then begin the separated climb only " +
                "after physical wall contact.");
        }

        if (scan.HeightDelta > 5.35f)
        {
            return new(
                BonusObstacleKind.WallAcrossGap,
                $"Gap={scan.Gap:F2},Rise={scan.HeightDelta:F2}. " +
                "A controlled approach jump is required before wall contact.");
        }

        if (scan.HeightDelta > 0.35f)
        {
            return new(
                BonusObstacleKind.RaisedLanding,
                $"Gap={scan.Gap:F2},Rise={scan.HeightDelta:F2}," +
                $"TargetWidth={targetWidth:F2}. " +
                "Only a verified direct landing is legal.");
        }

        return new(
            BonusObstacleKind.OrdinaryGap,
            $"Gap={scan.Gap:F2},DeltaY={scan.HeightDelta:F2}," +
            $"TargetWidth={targetWidth:F2}");
    }
}
