namespace AutoBonusRunner.Routing;

/// <summary>
/// One uncollected native SpiritBoost trigger in the active bonus section.
/// Bounds are world-space collider bounds. They are deliberately independent
/// from BonusSphere objectives: collecting a soul does not itself change run
/// speed.
/// </summary>
internal readonly record struct BonusSpeedBoostTrigger(
    float Left,
    float Right,
    float Bottom,
    float Top,
    int InstanceId,
    string Name)
{
    internal bool IsValid =>
        InstanceId != 0 &&
        Right > Left &&
        Top > Bottom &&
        float.IsFinite(Left) &&
        float.IsFinite(Right) &&
        float.IsFinite(Bottom) &&
        float.IsFinite(Top);
}

/// <summary>
/// Native Spirit Boost state captured in the same frame as route geometry.
/// Current/maximum boost are additive horizontal-speed components normalized
/// from PlayerMovement native speed units into Rigidbody world units. The
/// planner may use this context only when every numeric field and the typed
/// trigger scan are valid; otherwise it keeps the observed Rigidbody velocity
/// and makes no speculative future acceleration.
/// </summary>
internal readonly record struct SpiritBoostRouteContext(
    bool Enabled,
    bool KinematicsAvailable,
    bool TriggerScanSucceeded,
    float CurrentBoostComponent,
    float MaximumBoostComponent,
    float BoostDecreasePerSecond,
    float NativeCurrentSpeed,
    float NativePreStepSpeed,
    float BaseHorizontalSpeed,
    float PlayerLeftOffset,
    float PlayerRightOffset,
    float PlayerBottomOffset,
    float PlayerTopOffset,
    BonusSpeedBoostTrigger[] ActiveTriggers,
    string Evidence)
{
    internal bool HasVerifiedNoPendingTrigger =>
        Enabled &&
        KinematicsAvailable &&
        TriggerScanSucceeded &&
        (ActiveTriggers == null || ActiveTriggers.Length == 0);

    internal bool RequiresConservativeImmediateBoost =>
        Enabled && KinematicsAvailable && !TriggerScanSucceeded;

    internal bool RequiresSpeedEnvelope =>
        Enabled &&
        KinematicsAvailable &&
        MaximumBoostComponent > 0.01f &&
        BoostDecreasePerSecond > 0.01f &&
        BaseHorizontalSpeed > 0.01f &&
        (RequiresConservativeImmediateBoost ||
         ActiveTriggers != null &&
         ActiveTriggers.Any(trigger => trigger.IsValid));

    internal string Summary =>
        $"Enabled={Enabled},Kinematics={KinematicsAvailable}," +
        $"TriggerScan={TriggerScanSucceeded},CurrentBoost=" +
        $"{CurrentBoostComponent:F3},MaximumBoost=" +
        $"{MaximumBoostComponent:F3},Decrease=" +
        $"{BoostDecreasePerSecond:F3},NativeCurrentSpeed=" +
        $"{NativeCurrentSpeed:F3},NativePreStepSpeed=" +
        $"{NativePreStepSpeed:F3},Base={BaseHorizontalSpeed:F3}," +
        $"PlayerOffsets=[{PlayerLeftOffset:F3},{PlayerRightOffset:F3}]" +
        $"@[{PlayerBottomOffset:F3},{PlayerTopOffset:F3}]," +
        $"Triggers={ActiveTriggers?.Length ?? 0}:" +
        $"{DescribeTriggers()},Evidence={Evidence}";

    private string DescribeTriggers()
    {
        if (ActiveTriggers == null || ActiveTriggers.Length == 0)
            return "None";

        string details = string.Join(
            ";",
            ActiveTriggers
                .Take(8)
                .Select(trigger =>
                    $"{trigger.InstanceId}:{trigger.Name}" +
                    $"[{trigger.Left:F3},{trigger.Right:F3}]" +
                    $"@[{trigger.Bottom:F3},{trigger.Top:F3}]"));
        if (ActiveTriggers.Length > 8)
            details += $";+{ActiveTriggers.Length - 8}";
        return details;
    }

    internal static SpiritBoostRouteContext Disabled(string evidence) =>
        new(
            false,
            false,
            true,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            -0.35f,
            0.35f,
            0f,
            1.80f,
            Array.Empty<BonusSpeedBoostTrigger>(),
            evidence);
}
