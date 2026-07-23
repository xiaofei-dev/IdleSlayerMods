using UnityEngine;

namespace AutoBonusRunner.Detection;

internal readonly record struct BonusStageState(
    bool IsBonusStage,
    string GameStateName,
    string MapName,
    int SectionIndex,
    int PlayerInstanceId,
    Vector3 PlayerPosition,
    Vector2 PlayerVelocity,
    bool IsGrounded,
    bool HasPlayer,
    int CollectedSpheres,
    double RequiredSpheres,
    float CurrentTime,
    float MaximumTime,
    bool CharacterFellOff,
    bool SpiritBoostEnabled,
    bool IsTimerVisible,
    bool RewardFlagsAvailable,
    bool WaitingForRewardZone,
    bool RewardZoneEntered,
    bool GivingRewards,
    bool IsSupportedBonusMap)
{
    internal static BonusStageState Outside(string gameStateName) =>
        new(
            false,
            gameStateName,
            string.Empty,
            -1,
            0,
            default,
            default,
            false,
            false,
            -1,
            double.NaN,
            float.NaN,
            float.NaN,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false);

    internal bool IsActiveGameplay =>
        IsBonusStage &&
        IsSupportedBonusMap &&
        IsTimerVisible &&
        string.Equals(
            GameStateName,
            "BonusMode",
            System.StringComparison.Ordinal);

    internal bool HasSphereProgress =>
        CollectedSpheres >= 0 &&
        double.IsFinite(RequiredSpheres) &&
        RequiredSpheres >= 0d;

    internal int RemainingRequiredSpheres => HasSphereProgress
        ? Math.Max(0, (int)Math.Ceiling(RequiredSpheres) - CollectedSpheres)
        : -1;

    /// <summary>
    /// Bonus Stage 3 is the only map for which this mod currently owns an
    /// extracted authored-piece registry. Other bonus maps deliberately use
    /// live collider geometry so their routing cannot activate Stage-3-only
    /// Ground 3/5/6/7 contracts by accident.
    /// </summary>
    internal bool UsesStage3AuthoredRouting =>
        MapName.Contains(
            "bonus_stage_3",
            System.StringComparison.OrdinalIgnoreCase);

    internal bool UsesStage2LiveRouting =>
        MapName.Contains(
            "bonus_stage_2",
            System.StringComparison.OrdinalIgnoreCase);
}
