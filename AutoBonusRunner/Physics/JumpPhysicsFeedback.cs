using AutoBonusRunner.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoBonusRunner.Physics;

internal enum JumpHeightBand
{
    DeepDown,
    StepDown3,
    StepDown2,
    StepDown1,
    Level,
    StepUp1,
    StepUp2,
    StepUp3,
    HighUp
}

internal enum JumpHoldBucket
{
    Step1,
    Step2,
    Step3,
    Step4,
    Step5,
    Step6,
    Step7,
    Step8,
    Step9
}

internal static class JumpCalibrationBuckets
{
    // Bonus-stage surface tops are authored on integer-height layers. Use the
    // half-unit boundaries between those layers so floating values such as
    // +2.995 can never alias a +2 landing. Downward routes are separated for
    // the same reason: a one-unit step and a deep drop do not share a flight
    // duration merely because both have a negative height delta.
    internal const int HeightBandCount = 9;
    // Jump input is consumed by PlayerMovement.FixedUpdate. A requested 50ms
    // press is physically three 20ms steps, while 75ms is four; grouping those
    // requests previously mixed materially different trajectories. Each cell
    // now represents one reproducible native physics-step count.
    internal const int HoldBucketCount = 9;
    internal const int CellCount = HeightBandCount * HoldBucketCount;

    internal static JumpHeightBand GetHeightBand(float heightDelta)
    {
        if (heightDelta >= 3.5f)
            return JumpHeightBand.HighUp;
        if (heightDelta >= 2.5f)
            return JumpHeightBand.StepUp3;
        if (heightDelta >= 1.5f)
            return JumpHeightBand.StepUp2;
        if (heightDelta > 0.5f)
            return JumpHeightBand.StepUp1;
        if (heightDelta < -3.5f)
            return JumpHeightBand.DeepDown;
        if (heightDelta < -2.5f)
            return JumpHeightBand.StepDown3;
        if (heightDelta < -1.5f)
            return JumpHeightBand.StepDown2;
        if (heightDelta < -0.5f)
            return JumpHeightBand.StepDown1;
        return JumpHeightBand.Level;
    }

    internal static JumpHoldBucket GetHoldBucket(float holdSeconds)
    {
        // The controller releases on the first FixedUpdate at or beyond the
        // requested duration. Subtract a small floating-point tolerance before
        // the ceiling so an observed 0.060000004s remains the three-step cell.
        int fixedSteps = Mathf.Clamp(
            Mathf.CeilToInt((Mathf.Max(0.001f, holdSeconds) - 0.0005f) / 0.02f),
            1,
            HoldBucketCount);
        return (JumpHoldBucket)(fixedSteps - 1);
    }

    internal static int GetCellIndex(
        JumpHeightBand heightBand,
        JumpHoldBucket holdBucket) =>
        (int)heightBand * HoldBucketCount + (int)holdBucket;

    internal static string GetHeightBandLabel(JumpHeightBand heightBand) =>
        heightBand switch
        {
            JumpHeightBand.DeepDown => "DeepDown",
            JumpHeightBand.StepDown3 => "StepDown3",
            JumpHeightBand.StepDown2 => "StepDown2",
            JumpHeightBand.StepDown1 => "StepDown1",
            JumpHeightBand.Level => "Level",
            JumpHeightBand.StepUp1 => "StepUp1",
            JumpHeightBand.StepUp2 => "StepUp2",
            JumpHeightBand.StepUp3 => "StepUp3",
            JumpHeightBand.HighUp => "HighUp",
            _ => "UnknownHeight"
        };

    internal static string GetHoldBucketLabel(JumpHoldBucket holdBucket) =>
        holdBucket switch
        {
            JumpHoldBucket.Step1 => "S1-020",
            JumpHoldBucket.Step2 => "S2-040",
            JumpHoldBucket.Step3 => "S3-060",
            JumpHoldBucket.Step4 => "S4-080",
            JumpHoldBucket.Step5 => "S5-100",
            JumpHoldBucket.Step6 => "S6-120",
            JumpHoldBucket.Step7 => "S7-140",
            JumpHoldBucket.Step8 => "S8-160",
            JumpHoldBucket.Step9 => "S9-180",
            _ => "UnknownHold"
        };
}

internal readonly record struct JumpCalibrationProfile(
    float[] Values,
    int[] Counts)
{
    internal void GetCell(
        float heightDelta,
        float holdSeconds,
        out float value,
        out int count)
    {
        JumpHeightBand heightBand =
            JumpCalibrationBuckets.GetHeightBand(heightDelta);
        JumpHoldBucket holdBucket =
            JumpCalibrationBuckets.GetHoldBucket(holdSeconds);
        int index = JumpCalibrationBuckets.GetCellIndex(
            heightBand,
            holdBucket);
        bool available =
            Values != null && Counts != null &&
            index >= 0 && index < Values.Length && index < Counts.Length;
        value = available ? Values[index] : 0f;
        count = available ? Counts[index] : 0;
    }

    internal string Summary(string label)
    {
        System.Text.StringBuilder summary =
            new System.Text.StringBuilder(label).Append('[');
        bool anyCell = false;
        for (int heightIndex = 0;
             heightIndex < JumpCalibrationBuckets.HeightBandCount;
             heightIndex++)
        {
            JumpHeightBand heightBand = (JumpHeightBand)heightIndex;
            bool anyHeightCell = false;
            for (int holdIndex = 0;
                 holdIndex < JumpCalibrationBuckets.HoldBucketCount;
                 holdIndex++)
            {
                JumpHoldBucket holdBucket = (JumpHoldBucket)holdIndex;
                int index = JumpCalibrationBuckets.GetCellIndex(
                    heightBand,
                    holdBucket);
                int count =
                    Counts != null && index < Counts.Length
                        ? Counts[index]
                        : 0;
                if (count <= 0)
                    continue;

                if (!anyHeightCell)
                {
                    if (anyCell)
                        summary.Append(';');
                    summary.Append(
                        JumpCalibrationBuckets.GetHeightBandLabel(heightBand));
                    summary.Append(':');
                    anyHeightCell = true;
                    anyCell = true;
                }
                else
                {
                    summary.Append(',');
                }

                float value =
                    Values != null && index < Values.Length
                        ? Values[index]
                        : 0f;
                summary.Append(
                    $"{JumpCalibrationBuckets.GetHoldBucketLabel(holdBucket)}=" +
                    $"{value:F3}/N{count}");
            }
        }

        if (!anyCell)
            summary.Append("Empty");
        return summary.Append(']').ToString();
    }
}

internal sealed class JumpCalibrationGrid
{
    private readonly List<float>[] samples =
        new List<float>[JumpCalibrationBuckets.CellCount];
    private readonly int maximumSamples;

    internal JumpCalibrationGrid(int maximumSamples)
    {
        this.maximumSamples = maximumSamples;
        for (int index = 0; index < samples.Length; index++)
            samples[index] = new List<float>();
    }

    internal void Add(float heightDelta, float holdSeconds, float value)
    {
        List<float> cell = GetCell(heightDelta, holdSeconds);
        if (cell.Count >= maximumSamples)
            cell.RemoveAt(0);
        cell.Add(value);
    }

    internal int GetCount(float heightDelta, float holdSeconds) =>
        GetCell(heightDelta, holdSeconds).Count;

    internal void Clear()
    {
        foreach (List<float> cell in samples)
            cell.Clear();
    }

    internal JumpCalibrationProfile CaptureSnapshot()
    {
        float[] values = new float[samples.Length];
        int[] counts = new int[samples.Length];
        for (int index = 0; index < samples.Length; index++)
        {
            values[index] = MedianOrZero(samples[index]);
            counts[index] = samples[index].Count;
        }
        return new JumpCalibrationProfile(values, counts);
    }

    private List<float> GetCell(float heightDelta, float holdSeconds)
    {
        JumpHeightBand heightBand =
            JumpCalibrationBuckets.GetHeightBand(heightDelta);
        JumpHoldBucket holdBucket =
            JumpCalibrationBuckets.GetHoldBucket(holdSeconds);
        return samples[JumpCalibrationBuckets.GetCellIndex(
            heightBand,
            holdBucket)];
    }

    private static float MedianOrZero(List<float> values)
    {
        if (values.Count == 0)
            return 0f;

        List<float> ordered = new(values);
        ordered.Sort();
        int middle = ordered.Count / 2;
        return ordered.Count % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) * 0.5f
            : ordered[middle];
    }
}

internal readonly record struct JumpTravelProfile(
    JumpCalibrationProfile Calibration)
{
    internal bool TryGetDuration(
        float requestedHold,
        float targetHeightDelta,
        out float duration,
        out int count)
    {
        Calibration.GetCell(
            targetHeightDelta,
            requestedHold,
            out duration,
            out count);

        return count > 0 && duration > 0.05f;
    }

    internal string Summary =>
        Calibration.Summary("TravelTimeByHeightHold");
}

internal readonly record struct LandingErrorProfile(
    JumpCalibrationProfile Calibration)
{
    internal float GetBias(
        float heightDelta,
        float requestedHold,
        out int count)
    {
        Calibration.GetCell(
            heightDelta,
            requestedHold,
            out float bias,
            out count);
        return bias;
    }

    internal float GetAppliedBias(
        float heightDelta,
        float requestedHold,
        out float observedBias,
        out int count)
    {
        observedBias = GetBias(
            heightDelta,
            requestedHold,
            out count);
        if (count <= 0)
            return 0f;

        float weight = count >= 3
            ? 1f
            : count == 2
                ? 0.75f
                : 0.50f;
        return Mathf.Clamp(observedBias * weight, -1.25f, 1.25f);
    }

    internal string Summary =>
        Calibration.Summary("LandingBiasByHeightHold");
}

internal readonly record struct JumpPhysicsSnapshot(
    float JumpVelocity,
    float GravityMagnitude,
    float EffectiveHoldCapSeconds,
    float InputDelaySeconds,
    float HorizontalTravelScale,
    float FlightTimeScale,
    float BaseHorizontalSpeed,
    float BoostHorizontalDeceleration,
    float RawJumpForce,
    float RawJumpTime,
    float RawJumpCounter,
    float BodyGravityScale,
    float WorldGravityY,
    float FixedDeltaTime,
    bool IsJumping,
    int VelocitySampleCount,
    int GravitySampleCount,
    int DelaySampleCount,
    int HoldSampleCount,
    int LandingSampleCount,
    int ModelRevision,
    string VelocitySource,
    string GravitySource,
    string HoldSource,
    string DelaySource,
    LandingErrorProfile LandingErrorProfile,
    JumpTravelProfile TravelProfile)
{
    internal string Summary =>
        $"V={JumpVelocity:F3}({VelocitySource},N={VelocitySampleCount}), " +
        $"G={GravityMagnitude:F3}({GravitySource},N={GravitySampleCount}), " +
        $"HoldCap={EffectiveHoldCapSeconds:F3}({HoldSource},N={HoldSampleCount}), " +
        $"Delay={InputDelaySeconds:F3}({DelaySource},N={DelaySampleCount}), " +
        $"TravelScale={HorizontalTravelScale:F3}(N={LandingSampleCount}), " +
        $"FlightScale={FlightTimeScale:F3}, BaseVX={BaseHorizontalSpeed:F3}, " +
        $"BoostDecel={BoostHorizontalDeceleration:F3}, " +
        $"{LandingErrorProfile.Summary}, {TravelProfile.Summary}, Rev={ModelRevision}";
}

internal sealed class JumpPhysicsFeedback
{
    // A single landing timestamp can move by one or two physics ticks because
    // stable-support confirmation and render/fixed-step ordering are not
    // identical on every landing. Keep collecting those observations, but do
    // not let one transient level sample replace the conservative analytical
    // fallback used by the active section.
    private const int StableTimingSampleCount = 3;
    // Repeated mature Stage-1 traces converge near 0.970. The former 0.950
    // cold-start value shortened the route enough to jump onto the first
    // narrow support before collecting the grounded Spirit Boost.
    private const float StableFlightTimeFallback = 0.970f;

    private const float FallbackJumpVelocity = 18.627f;
    private const float FallbackGravity = 68.67f;
    private const float FallbackHoldCap = 0.15f;
    private const float FallbackInputDelay = 0.02f;
    private const int MaximumSamples = 7;

    private static JumpPhysicsFeedback activeInstance;

    // Render frames are not a reliable release barrier: several LateUpdate
    // calls can observe the same physics state. Wall actions use this sequence
    // to require an actual PlayerMovement.FixedUpdate after pointer UP.
    internal static long FixedStepSequence { get; private set; }
    private static double lastCapturedFixedTime = double.NaN;
    private static int lastCapturedPlayerInstanceId;
    private static long duplicateFixedStepCallbacks;
    private static float nextDuplicateFixedStepLogTime;

    internal static bool IsPrimaryPlayerInstance(PlayerMovement player)
    {
        if (player == null)
            return false;

        PlayerMovement primary = PlayerMovement.instance;
        if (primary == null)
            return false;

        try
        {
            return player.GetInstanceID() == primary.GetInstanceID();
        }
        catch
        {
            return false;
        }
    }

    private readonly List<float> takeoffVelocitySamples = new();
    private readonly List<float> observedGravitySamples = new();
    private readonly List<float> inputDelaySamples = new();
    private readonly List<float> effectiveHoldSamples = new();
    private readonly List<float> horizontalScaleSamples = new();
    private readonly List<float> flightTimeScaleSamples = new();
    private readonly List<float> baseHorizontalSpeedSamples = new();
    private readonly List<float> boostHorizontalDecelerationSamples = new();
    private readonly JumpCalibrationGrid travelDurationSamples =
        new(MaximumSamples);
    private readonly JumpCalibrationGrid landingErrorSamples =
        new(MaximumSamples);

    private bool profileInitialized;
    private float rawJumpForce = 20f;
    private float rawJumpTime = FallbackHoldCap;
    private float rawBodyGravityScale = 7f;
    private float rawWorldGravityY = -9.81f;
    private float rawFixedDeltaTime = 0.02f;
    private int modelRevision;
    private int cachedCalibrationRevision = int.MinValue;
    private JumpCalibrationProfile cachedTravelDurationProfile;
    private JumpCalibrationProfile cachedLandingErrorProfile;
    private float nextModelLogTime;
    private float nextErrorLogTime;
    private string lastModelSignature = string.Empty;

    private bool sampleActive;
    private bool sampleMayLearnGroundKinematics;
    private bool completedSampleContextAvailable;
    private bool completedSampleMayLearnGroundKinematics;
    private string sampleSource = string.Empty;
    private float sampleStartedAt;
    private int sampleFixedSteps;
    private int sampleIsJumpingSteps;
    private int sampleCounterChangedSteps;
    private int sampleCounterIncreasingSteps;
    private int sampleCounterDecreasingSteps;
    private int samplePositiveJumpResidualSteps;
    private float sampleCounterStart;
    private float sampleCounterMinimum;
    private float sampleCounterMaximum;
    private float sampleCounterEnd;
    private float samplePreviousCounter;
    private bool samplePreviousFixedValid;
    private bool samplePreviousGrounded;
    private bool samplePreviousIsJumping;
    private float samplePreviousVelocityY;
    private float samplePreviousVelocityX;
    private float sampleHeldAccelerationSum;
    private int sampleHeldAccelerationCount;
    private float sampleReleasedAccelerationSum;
    private int sampleReleasedAccelerationCount;
    private readonly List<float> sampleReleasedGravityCandidates = new();
    private float sampleHeldResidualSum;
    private int sampleHeldResidualCount;
    private float sampleLaunchVelocityY;
    private bool sampleFixedTakeoffCaptured;
    private float sampleFixedTakeoffVelocityY;
    private float sampleFixedTakeoffTime;
    private bool sampleCeilingHit;
    private float nextFrameLogTime;

    internal void Attach()
    {
        activeInstance = this;
        lastCapturedFixedTime = double.NaN;
        lastCapturedPlayerInstanceId = 0;
        duplicateFixedStepCallbacks = 0;
        nextDuplicateFixedStepLogTime = 0f;
    }

    internal void Detach()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

    internal static void CaptureFixedStep(PlayerMovement player)
    {
        // PlayerMovement.FixedUpdate can run for secondary/ghost movement
        // objects. Their frames must never advance the release barrier or
        // contaminate the primary player's sampled trajectory.
        if (!IsPrimaryPlayerInstance(player))
            return;

        int playerInstanceId;
        try
        {
            playerInstanceId = player.GetInstanceID();
        }
        catch
        {
            return;
        }

        double fixedTime = Time.fixedTimeAsDouble;
        if (playerInstanceId == lastCapturedPlayerInstanceId &&
            !double.IsNaN(lastCapturedFixedTime) &&
            fixedTime == lastCapturedFixedTime)
        {
            duplicateFixedStepCallbacks++;
            if (duplicateFixedStepCallbacks == 1 ||
                Time.unscaledTime >= nextDuplicateFixedStepLogTime)
            {
                nextDuplicateFixedStepLogTime =
                    Time.unscaledTime + 5f;
                string message =
                    $"FixedStepCallbackDeduplicated Player=" +
                    $"{playerInstanceId}, FixedTime={fixedTime:F4}, " +
                    $"SuppressedTotal={duplicateFixedStepCallbacks}. " +
                    "FixedStepSequence and physics learning advance once " +
                    "per real Unity physics tick.";
                if (duplicateFixedStepCallbacks == 1)
                    BonusRunnerLog.Warning(message);
                else
                    BonusRunnerLog.Debug(message, "Physics");
            }
            return;
        }

        lastCapturedPlayerInstanceId = playerInstanceId;
        lastCapturedFixedTime = fixedTime;

        FixedStepSequence++;
        JumpPhysicsFeedback instance = activeInstance;
        if (instance == null)
            return;

        try
        {
            instance.ObserveFixedStep(player);
        }
        catch (System.Exception exception)
        {
            if (Time.unscaledTime < instance.nextErrorLogTime)
                return;

            instance.nextErrorLogTime = Time.unscaledTime + 5f;
            BonusRunnerLog.Exception(
                "Jump physics feedback",
                exception);
        }
    }

    internal JumpPhysicsSnapshot CaptureSnapshot(
        PlayerMovement player,
        bool forceLog = false)
    {
        Rigidbody2D body = player?.GetComponent<Rigidbody2D>();
        if (player != null && body != null)
            RefreshProfile(player, body, player.IsGrounded());

        float directGravity = GetDirectGravity();
        bool useObservedGravity = observedGravitySamples.Count >= 2;
        float gravity = useObservedGravity
            ? Median(observedGravitySamples)
            : directGravity;
        string gravitySource = useObservedGravity
            ? "ReleasedFlightMedian"
            : profileInitialized
                ? "Physics2D.gravity*Rigidbody2D.gravityScale"
                : "Fallback";
        if (!IsFiniteInRange(gravity, 5f, 200f))
        {
            gravity = FallbackGravity;
            gravitySource = "Fallback";
        }

        float apiStepVelocity = rawJumpForce - gravity * rawFixedDeltaTime;
        bool useObservedVelocity = takeoffVelocitySamples.Count >= 2;
        float jumpVelocity = useObservedVelocity
            ? Median(takeoffVelocitySamples)
            : apiStepVelocity;
        string velocitySource = useObservedVelocity
            ? "ObservedTakeoffMedian"
            : profileInitialized
                ? "jumpForce-G*fixedDelta"
                : "Fallback";
        if (!IsFiniteInRange(jumpVelocity, 5f, 50f))
        {
            jumpVelocity = FallbackJumpVelocity;
            velocitySource = "Fallback";
        }

        // The apex-derived value measures the remaining powered ascent after
        // the first airborne frame. It is useful diagnostics, but it is not
        // the input-relative jumpTime cap used by the planner. Treating the
        // observed ~0.15s as the live ~0.18s input cap made every later high
        // pillar jump predict about 0.6 units too little horizontal travel.
        float holdCap = rawJumpTime;
        string holdSource = profileInitialized
            ? "PlayerMovement.jumpTime"
            : "Fallback";
        if (!IsFiniteInRange(holdCap, 0.03f, 1f))
        {
            holdCap = FallbackHoldCap;
            holdSource = "Fallback";
        }

        bool useObservedDelay = inputDelaySamples.Count >= 2;
        float inputDelay = useObservedDelay
            ? Median(inputDelaySamples)
            : rawFixedDeltaTime;
        string delaySource = useObservedDelay
            ? "InputToTakeoffMedian"
            : profileInitialized
                ? "fixedDeltaTime"
                : "Fallback";
        if (!IsFiniteInRange(inputDelay, 0f, 0.25f))
        {
            inputDelay = FallbackInputDelay;
            delaySource = "Fallback";
        }

        float travelScale = horizontalScaleSamples.Count >= 2
            ? Median(horizontalScaleSamples)
            : 1f;
        travelScale = Mathf.Clamp(travelScale, 0.85f, 1.15f);
        float flightTimeScale =
            flightTimeScaleSamples.Count >= StableTimingSampleCount
            ? Median(flightTimeScaleSamples)
            : StableFlightTimeFallback;
        flightTimeScale = Mathf.Clamp(flightTimeScale, 0.85f, 1.10f);
        float baseHorizontalSpeed = baseHorizontalSpeedSamples.Count > 0
            ? Median(baseHorizontalSpeedSamples)
            : 9.4f;
        baseHorizontalSpeed = Mathf.Clamp(baseHorizontalSpeed, 4f, 15f);
        float boostHorizontalDeceleration =
            boostHorizontalDecelerationSamples.Count > 0
                ? Median(boostHorizontalDecelerationSamples)
                : 5f;
        boostHorizontalDeceleration = Mathf.Clamp(
            boostHorizontalDeceleration, 1f, 15f);

        RefreshCalibrationProfileCache();

        JumpPhysicsSnapshot snapshot = new(
            jumpVelocity,
            gravity,
            holdCap,
            inputDelay,
            travelScale,
            flightTimeScale,
            baseHorizontalSpeed,
            boostHorizontalDeceleration,
            rawJumpForce,
            rawJumpTime,
            player?.jumpTimeCounter ?? 0f,
            rawBodyGravityScale,
            rawWorldGravityY,
            rawFixedDeltaTime,
            player?.isJumping ?? false,
            takeoffVelocitySamples.Count,
            observedGravitySamples.Count,
            inputDelaySamples.Count,
            effectiveHoldSamples.Count,
            horizontalScaleSamples.Count,
            modelRevision,
            velocitySource,
            gravitySource,
            holdSource,
            delaySource,
            new LandingErrorProfile(
                cachedLandingErrorProfile),
            new JumpTravelProfile(
                cachedTravelDurationProfile));

        LogSnapshot(snapshot, player, body, forceLog);
        return snapshot;
    }

    private void RefreshCalibrationProfileCache()
    {
        if (cachedCalibrationRevision == modelRevision)
            return;

        cachedTravelDurationProfile =
            travelDurationSamples.CaptureSnapshot();
        cachedLandingErrorProfile =
            landingErrorSamples.CaptureSnapshot();
        cachedCalibrationRevision = modelRevision;
    }

    internal void BeginInput(
        string source,
        PlayerMovement player,
        bool learnGroundKinematics)
    {
        ClearSample();
        sampleActive = true;
        sampleMayLearnGroundKinematics = learnGroundKinematics;
        sampleSource = source;
        sampleStartedAt = Time.unscaledTime;
        float counter = player?.jumpTimeCounter ?? 0f;
        sampleCounterStart = counter;
        sampleCounterMinimum = counter;
        sampleCounterMaximum = counter;
        sampleCounterEnd = counter;
        samplePreviousCounter = counter;
        nextFrameLogTime = 0f;

        JumpPhysicsSnapshot snapshot = CaptureSnapshot(player, true);
        BonusRunnerLog.Debug(
            $"JumpFeedbackStart Source={source}, Counter={counter:F3}/{snapshot.RawJumpTime:F3}, " +
            $"IsJumping={snapshot.IsJumping}, " +
            $"LearnGroundKinematics={sampleMayLearnGroundKinematics}; Model: {snapshot.Summary}",
            "Physics");
    }

    internal void ObserveTakeoff(
        float inputDownTime,
        Vector2 takeoffVelocity)
    {
        JumpPhysicsSnapshot snapshot = CaptureSnapshot(PlayerMovement.instance);
        float observedVelocityY = sampleFixedTakeoffCaptured
            ? sampleFixedTakeoffVelocityY
            : takeoffVelocity.y;
        float observedTakeoffTime = sampleFixedTakeoffCaptured
            ? sampleFixedTakeoffTime
            : Time.unscaledTime;
        float delay = Mathf.Max(0f, observedTakeoffTime - inputDownTime);
        float interfaceVelocity =
            snapshot.RawJumpForce -
            snapshot.GravityMagnitude * snapshot.FixedDeltaTime;
        float velocityTolerance = sampleFixedTakeoffCaptured
            ? Mathf.Max(3f, Mathf.Abs(interfaceVelocity) * 0.25f)
            : Mathf.Max(1.5f, Mathf.Abs(interfaceVelocity) * 0.12f);
        bool velocityCandidate =
            IsFiniteInRange(observedVelocityY, 5f, 50f) &&
            Mathf.Abs(observedVelocityY - interfaceVelocity) <= velocityTolerance;
        bool delayCandidate = velocityCandidate &&
            IsFiniteInRange(delay, 0f, 0.25f) &&
            Time.timeScale > 0.80f && Time.timeScale < 1.20f;
        bool velocityAccepted =
            sampleMayLearnGroundKinematics && velocityCandidate;
        bool delayAccepted =
            sampleMayLearnGroundKinematics && delayCandidate;

        if (velocityAccepted)
        {
            AddSample(takeoffVelocitySamples, observedVelocityY);
            sampleLaunchVelocityY = observedVelocityY;
        }
        if (delayAccepted)
            AddSample(inputDelaySamples, delay);
        if (velocityAccepted || delayAccepted)
            modelRevision++;

        BonusRunnerLog.Debug(
            $"JumpFeedbackTakeoff Source={sampleSource}, DownToAir={delay:F3}s " +
            $"(Accepted={delayAccepted}), UpdateV=({takeoffVelocity.x:F3},{takeoffVelocity.y:F3}), " +
            $"FixedEdgeVY={(sampleFixedTakeoffCaptured ? sampleFixedTakeoffVelocityY.ToString("F3") : "Unavailable")}, " +
            $"InterfaceVY={interfaceVelocity:F3}, UsedVY={observedVelocityY:F3}, " +
            $"Tolerance={velocityTolerance:F3}, Accepted={velocityAccepted}, " +
            $"PhysicalCandidate={velocityCandidate}, " +
            $"LearnGroundKinematics={sampleMayLearnGroundKinematics}, " +
            $"VelocitySamples={takeoffVelocitySamples.Count}, " +
            $"DelaySamples={inputDelaySamples.Count}, Rev={modelRevision}",
            "Physics");
    }

    internal string CompleteSample(
        float physicalHoldSeconds,
        bool tookOff,
        bool firstApexCaptured,
        Vector3 takeoffPosition,
        Vector3 firstApexPosition)
    {
        if (!sampleActive)
            return "Feedback=Unavailable";

        float heldAcceleration = sampleHeldAccelerationCount > 0
            ? sampleHeldAccelerationSum / sampleHeldAccelerationCount
            : 0f;
        float releasedAcceleration = sampleReleasedAccelerationCount > 0
            ? sampleReleasedAccelerationSum / sampleReleasedAccelerationCount
            : 0f;
        float heldResidual = sampleHeldResidualCount > 0
            ? sampleHeldResidualSum / sampleHeldResidualCount
            : 0f;
        bool holdApplied =
            sampleIsJumpingSteps >= 2 &&
            (sampleCounterChangedSteps > 0 ||
             samplePositiveJumpResidualSteps >= 2);
        string classification = sampleCeilingHit
            ? "CeilingHit"
            : holdApplied
                ? "HoldApplied"
                : "HoldNotApplied";
        string counterTrend = sampleCounterIncreasingSteps > 0 &&
                              sampleCounterDecreasingSteps > 0
            ? "Mixed"
            : sampleCounterDecreasingSteps > 0
                ? "Decreasing"
                : sampleCounterIncreasingSteps > 0
                    ? "Increasing"
                    : "Static";

        float observedGravity = sampleReleasedGravityCandidates.Count > 0
            ? Median(sampleReleasedGravityCandidates)
            : 0f;
        bool gravityCandidate = sampleReleasedGravityCandidates.Count >= 3;
        bool gravityAccepted =
            sampleMayLearnGroundKinematics && gravityCandidate;
        if (gravityAccepted)
        {
            AddSample(observedGravitySamples, observedGravity);
            modelRevision++;
        }

        float derivedHold = 0f;
        bool derivedHoldAccepted = false;
        if (sampleMayLearnGroundKinematics &&
            tookOff && firstApexCaptured && holdApplied && !sampleCeilingHit)
        {
            JumpPhysicsSnapshot snapshot = CaptureSnapshot(
                PlayerMovement.instance);
            float launchVelocity = IsFiniteInRange(sampleLaunchVelocityY, 5f, 50f)
                ? sampleLaunchVelocityY
                : snapshot.JumpVelocity;
            float apexRise = firstApexPosition.y - takeoffPosition.y;
            float ballisticRise =
                launchVelocity * launchVelocity /
                (2f * snapshot.GravityMagnitude);
            derivedHold = (apexRise - ballisticRise) / launchVelocity;
            float saturationThreshold = Mathf.Max(
                0.03f,
                snapshot.RawJumpTime - snapshot.FixedDeltaTime * 1.5f);
            float rawHoldTolerance = Mathf.Max(
                0.05f,
                snapshot.RawJumpTime * 0.35f);
            derivedHoldAccepted =
                physicalHoldSeconds >= saturationThreshold &&
                IsFiniteInRange(derivedHold, 0.03f, 0.50f) &&
                derivedHold <= physicalHoldSeconds + snapshot.FixedDeltaTime * 2f &&
                Mathf.Abs(derivedHold - snapshot.RawJumpTime) <= rawHoldTolerance;
            if (derivedHoldAccepted)
            {
                AddSample(effectiveHoldSamples, derivedHold);
                modelRevision++;
            }
        }

        string summary =
            $"Classification={classification}, HoldApplied={holdApplied}, CeilingHit={sampleCeilingHit}, " +
            $"FixedSteps={sampleFixedSteps}, IsJumpingSteps={sampleIsJumpingSteps}, " +
            $"Counter={sampleCounterStart:F3}->{sampleCounterEnd:F3} " +
            $"Range=[{sampleCounterMinimum:F3},{sampleCounterMaximum:F3}] " +
            $"Trend={counterTrend} ChangedSteps={sampleCounterChangedSteps}, " +
            $"HeldAy={heldAcceleration:F3}, ReleasedAy={releasedAcceleration:F3}, " +
            $"ObservedG={observedGravity:F3} (Accepted={gravityAccepted}," +
            $"Candidate={gravityCandidate},Frames={sampleReleasedGravityCandidates.Count}), " +
            $"HeldJumpResidual={heldResidual:F3}, DerivedHold={derivedHold:F3} " +
            $"(Accepted={derivedHoldAccepted}), " +
            $"LearnGroundKinematics={sampleMayLearnGroundKinematics}, Rev={modelRevision}";
        BonusRunnerLog.Debug(
            $"JumpFeedbackResult Source={sampleSource}, PhysicalHold={physicalHoldSeconds:F3}s, {summary}",
            "Physics");
        bool completedMayLearnGroundKinematics =
            sampleMayLearnGroundKinematics;
        ClearSample();
        completedSampleContextAvailable = true;
        completedSampleMayLearnGroundKinematics =
            completedMayLearnGroundKinematics;
        return summary;
    }

    internal void ObserveSuccessfulLanding(
        float plannedTravel,
        float actualTravel,
        float plannedScale,
        float plannedHold,
        float triggerSpeed,
        float targetHeightDelta)
    {
        float baseTravel = plannedTravel / Mathf.Max(0.01f, plannedScale);
        float observedScale = actualTravel / Mathf.Max(0.01f, baseTravel);
        bool physicalCandidate =
            IsFiniteInRange(actualTravel, 0.25f, 20f) &&
            IsFiniteInRange(observedScale, 0.75f, 1.25f) &&
            Mathf.Abs(targetHeightDelta) <= 0.35f;
        bool learningEligible =
            completedSampleContextAvailable &&
            completedSampleMayLearnGroundKinematics;
        bool accepted = learningEligible && physicalCandidate;
        if (accepted)
        {
            AddSample(horizontalScaleSamples, observedScale);
            modelRevision++;
        }

        BonusRunnerLog.Debug(
            $"LandingFeedback PlannedTravel={plannedTravel:F3}, ActualTriggerTravel={actualTravel:F3}, " +
            $"PlannedScale={plannedScale:F3}, ObservedScale={observedScale:F3}, " +
            $"HoldTier={plannedHold:F3}s, TriggerSpeed={triggerSpeed:F3}, " +
            $"TargetDeltaY={targetHeightDelta:F3}, " +
            $"NaiveTravelOverStartSpeed={actualTravel / Mathf.Max(1f, Mathf.Abs(triggerSpeed)):F3}s, " +
            "PlannerModel=DynamicSpeedIntegral, " +
            $"HoldBucket={GetHoldBucketLabel(plannedHold)}, " +
            $"PhysicalCandidate={physicalCandidate}, LearningEligible={learningEligible}, " +
            $"Accepted={accepted}, Samples={horizontalScaleSamples.Count}, Rev={modelRevision}",
            "Physics");
    }

    internal void ObserveLandingError(
        float predictedTravel,
        float actualTravel,
        float appliedBiasAtPlan,
        float targetHeightDelta,
        float plannedHold,
        string planReason)
    {
        float innovation = actualTravel - predictedTravel;
        // The profile stores an absolute residual for a height/hold cell.
        // predictedTravel already contains the bias that was active when the
        // command was planned, so storing only the latest innovation makes a
        // correct next sample drive the median back toward zero and creates a
        // self-erasing oscillation. Recover the absolute residual implied by
        // this observation instead.
        float impliedAbsoluteBias =
            appliedBiasAtPlan + innovation;
        // Landing bias is a small residual correction, not a substitute for a
        // wrong trajectory model. V0.62 accepted +1.76/+2.02-unit misses and
        // fed them back into subsequent routes, amplifying a timing-bucket
        // error. Keep only bounded residuals; larger errors remain diagnostic
        // evidence for the flight model.
        float maximumResidualError = Mathf.Clamp(
            Mathf.Abs(predictedTravel) * 0.08f,
            0.50f,
            0.75f);
        bool physicalCandidate =
            IsFiniteInRange(actualTravel, 0.10f, 25f) &&
            Mathf.Abs(innovation) <= maximumResidualError &&
            Mathf.Abs(impliedAbsoluteBias) <= 1.25f &&
            !planReason.StartsWith(
                "WallRecovery",
                System.StringComparison.Ordinal) &&
            !planReason.Contains(
                "WallApproach",
                System.StringComparison.Ordinal);
        bool learningEligible =
            completedSampleContextAvailable &&
            completedSampleMayLearnGroundKinematics;
        bool accepted = learningEligible && physicalCandidate;
        if (accepted)
        {
            landingErrorSamples.Add(
                targetHeightDelta,
                plannedHold,
                impliedAbsoluteBias);
            modelRevision++;
        }

        BonusRunnerLog.Debug(
            $"LandingErrorFeedback Plan={planReason}, " +
            $"DeltaY={targetHeightDelta:F3}, HoldTier={plannedHold:F3}s, " +
            $"PredictedTravel={predictedTravel:F3}, " +
            $"ActualTravel={actualTravel:F3}, Innovation={innovation:F3}, " +
            $"AppliedBiasAtPlan={appliedBiasAtPlan:F3}, " +
            $"ImpliedAbsoluteBias={impliedAbsoluteBias:F3}, " +
            $"MaximumResidualError={maximumResidualError:F3}, " +
            $"HeightBand={GetHeightBandLabel(targetHeightDelta)}, " +
            $"HoldBucket={GetHoldBucketLabel(plannedHold)}, " +
            $"PhysicalCandidate={physicalCandidate}, LearningEligible={learningEligible}, " +
            $"Accepted={accepted}, CellSamples=" +
            $"{landingErrorSamples.GetCount(targetHeightDelta, plannedHold)}, " +
            $"Rev={modelRevision}",
            "Physics");
    }

    internal string ObserveFlightTiming(
        float predictedInputToLandingSeconds,
        float actualInputToLandingSeconds,
        string source,
        float holdSeconds,
        float heightDelta,
        bool useExclusiveLiveChannel = false)
    {
        float scale = actualInputToLandingSeconds /
            Mathf.Max(0.05f, predictedInputToLandingSeconds);
        bool cleanDuration =
            IsFiniteInRange(actualInputToLandingSeconds, 0.25f, 1.50f) &&
            IsFiniteInRange(scale, 0.65f, 1.45f);
        bool levelScaleCandidate =
            cleanDuration &&
            IsFiniteInRange(scale, 0.80f, 1.15f) &&
            Mathf.Abs(heightDelta) <= 0.35f;
        bool durationCandidate = cleanDuration;
        bool learningEligible =
            completedSampleContextAvailable &&
            completedSampleMayLearnGroundKinematics;
        bool levelTrajectory = Mathf.Abs(heightDelta) <= 0.35f;
        bool levelTimingStable =
            flightTimeScaleSamples.Count >= StableTimingSampleCount;
        bool durationTimingStable =
            travelDurationSamples.GetCount(heightDelta, holdSeconds) >=
                StableTimingSampleCount;
        // Every map uses exactly one correction layer for each sample.
        // Level landings calibrate the analytical flight scale; non-level
        // landings calibrate the exact height/hold duration cell. Applying a
        // duration and then also applying a residual/scale derived from the
        // pre-update prediction double-counts the same timing error. Once a
        // timing channel has three clean samples, subsequent independent
        // landings may calibrate the horizontal endpoint instead.
        bool levelScaleAccepted =
            learningEligible && levelScaleCandidate &&
            !levelTimingStable;
        bool durationAccepted =
            learningEligible && durationCandidate &&
            (!useExclusiveLiveChannel || !levelTrajectory) &&
            !durationTimingStable;
        if (levelScaleAccepted)
        {
            AddSample(flightTimeScaleSamples, scale);
        }
        if (durationAccepted)
        {
            travelDurationSamples.Add(
                heightDelta,
                holdSeconds,
                actualInputToLandingSeconds);
        }
        if (levelScaleAccepted || durationAccepted)
            modelRevision++;

        string selectedChannel =
            levelScaleAccepted && durationAccepted
                ? "LevelFlightScale+HeightHoldDuration"
                : levelScaleAccepted
                    ? "LevelFlightScale"
                    : durationAccepted
                        ? "HeightHoldDuration"
                        : learningEligible && levelScaleCandidate &&
                          levelTrajectory && levelTimingStable
                            ? "StableLevelFlightScale"
                        : learningEligible && durationCandidate &&
                          !levelTrajectory && durationTimingStable
                            ? "StableHeightHoldDuration"
                        : "Rejected";

        BonusRunnerLog.Debug(
            $"FlightTimingFeedback Source={source}, Hold={holdSeconds:F3}s, " +
            $"DeltaY={heightDelta:F3}, PredictedInputToLanding=" +
            $"{predictedInputToLandingSeconds:F3}s, Actual=" +
            $"{actualInputToLandingSeconds:F3}s, Scale={scale:F3}, " +
            $"LevelScaleAccepted={levelScaleAccepted}, " +
            $"DurationAccepted={durationAccepted}, " +
            $"ExclusiveLiveChannel={useExclusiveLiveChannel}, " +
            $"SelectedChannel={selectedChannel}, " +
            $"TimingStable[Level={levelTimingStable}," +
            $"HeightHold={durationTimingStable}], " +
            $"PhysicalCandidates[LevelScale={levelScaleCandidate},Duration={durationCandidate}], " +
            $"LearningEligible={learningEligible}, " +
            $"LevelSamples={flightTimeScaleSamples.Count}, " +
            $"HeightBand={GetHeightBandLabel(heightDelta)}, " +
            $"HoldBucket={GetHoldBucketLabel(holdSeconds)}, " +
            $"HeightHoldDurationSamples=" +
            $"{travelDurationSamples.GetCount(heightDelta, holdSeconds)}, " +
            $"Rev={modelRevision}",
            "Physics");
        return selectedChannel;
    }

    private static string GetHeightBandLabel(float heightDelta) =>
        JumpCalibrationBuckets.GetHeightBandLabel(
            JumpCalibrationBuckets.GetHeightBand(heightDelta));

    private static string GetHoldBucketLabel(float holdSeconds) =>
        JumpCalibrationBuckets.GetHoldBucketLabel(
            JumpCalibrationBuckets.GetHoldBucket(holdSeconds));

    private static float MedianOrZero(List<float> values) =>
        values.Count == 0 ? 0f : Median(values);

    internal void ResetTransient(string reason)
    {
        if (sampleActive)
        {
            BonusRunnerLog.Debug(
                $"Jump feedback transient sample discarded: Source={sampleSource}, Reason={reason}, " +
                $"FixedSteps={sampleFixedSteps}.",
                "Physics");
        }
        ClearSample();
    }

    internal void ResetRouteCalibration(string reason)
    {
        horizontalScaleSamples.Clear();
        flightTimeScaleSamples.Clear();
        travelDurationSamples.Clear();
        landingErrorSamples.Clear();
        modelRevision++;
        BonusRunnerLog.Debug(
            $"Route calibration reset: Reason={reason}, Rev={modelRevision}. " +
            "Native jump/gravity/input-delay samples are retained; " +
            "hold-duration, landing-bias, and flight-scale samples are now " +
            "scoped to the new bonus-stage section.",
            "Physics");
    }

    private void ObserveFixedStep(PlayerMovement player)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body == null)
            return;
        if (!sampleActive)
            return;

        bool grounded = player.IsGrounded();
        RefreshProfile(player, body, grounded);

        float counter = player.jumpTimeCounter;
        float velocityY = body.velocity.y;
        float velocityX = Mathf.Abs(body.velocity.x);
        if (!sampleFixedTakeoffCaptured && !grounded && velocityY > 5f)
        {
            sampleFixedTakeoffCaptured = true;
            sampleFixedTakeoffVelocityY = velocityY;
            sampleFixedTakeoffTime = Time.unscaledTime;
        }
        float deltaCounter = counter - samplePreviousCounter;
        if (Mathf.Abs(deltaCounter) > 0.0005f)
        {
            sampleCounterChangedSteps++;
            if (deltaCounter > 0f)
                sampleCounterIncreasingSteps++;
            else
                sampleCounterDecreasingSteps++;
        }

        sampleFixedSteps++;
        if (player.isJumping)
            sampleIsJumpingSteps++;
        sampleCounterMinimum = Mathf.Min(sampleCounterMinimum, counter);
        sampleCounterMaximum = Mathf.Max(sampleCounterMaximum, counter);
        sampleCounterEnd = counter;

        float acceleration = 0f;
        float jumpResidual = 0f;
        bool continuousAirFrame =
            samplePreviousFixedValid &&
            !samplePreviousGrounded &&
            !grounded;
        if (continuousAirFrame)
        {
            float dt = Mathf.Max(0.001f, Time.fixedDeltaTime);
            float velocityDelta = velocityY - samplePreviousVelocityY;
            float previousSpeedX = Mathf.Abs(samplePreviousVelocityX);
            float horizontalAcceleration =
                (velocityX - previousSpeedX) / dt;
            if (sampleMayLearnGroundKinematics &&
                previousSpeedX > 10.25f &&
                velocityX >= 9f &&
                horizontalAcceleration <= -0.50f &&
                horizontalAcceleration >= -15f)
            {
                AddSample(
                    boostHorizontalDecelerationSamples,
                    -horizontalAcceleration);
            }
            else if (sampleMayLearnGroundKinematics &&
                     velocityX >= 5f && velocityX <= 12f &&
                     Mathf.Abs(horizontalAcceleration) <= 0.25f)
            {
                AddSample(baseHorizontalSpeedSamples, velocityX);
            }
            float expectedGravityDelta =
                Physics2D.gravity.y * body.gravityScale * dt;
            acceleration = velocityDelta / dt;
            jumpResidual = velocityDelta - expectedGravityDelta;
            bool heldBranch = player.isJumping || samplePreviousIsJumping;
            if (heldBranch)
            {
                sampleHeldAccelerationSum += acceleration;
                sampleHeldAccelerationCount++;
                sampleHeldResidualSum += jumpResidual;
                sampleHeldResidualCount++;
                if (jumpResidual > Mathf.Abs(expectedGravityDelta) * 0.35f)
                    samplePositiveJumpResidualSteps++;
            }
            else
            {
                sampleReleasedAccelerationSum += acceleration;
                sampleReleasedAccelerationCount++;
                float measuredGravity = -acceleration;
                float directGravity = Mathf.Abs(
                    Physics2D.gravity.y * body.gravityScale);
                float gravityTolerance = Mathf.Max(3f, directGravity * 0.20f);
                if (IsFiniteInRange(measuredGravity, 5f, 200f) &&
                    Mathf.Abs(measuredGravity - directGravity) <= gravityTolerance)
                {
                    AddSample(
                        sampleReleasedGravityCandidates,
                        measuredGravity);
                }
            }

            float expectedVelocity =
                samplePreviousVelocityY + expectedGravityDelta;
            if ((player.isJumping || JumpPanel.jumpPressed) &&
                samplePreviousVelocityY > 4f &&
                velocityY < expectedVelocity - 2.5f)
            {
                sampleCeilingHit = true;
            }
        }

        bool stateChanged =
            !samplePreviousFixedValid ||
            grounded != samplePreviousGrounded ||
            player.isJumping != samplePreviousIsJumping;
        if (BonusRunnerLog.IsDebugMode &&
            (stateChanged || Time.unscaledTime >= nextFrameLogTime))
        {
            nextFrameLogTime = Time.unscaledTime + 0.10f;
            Vector2 preStep = player.preStepVelocity;
            BonusRunnerLog.Debug(
                $"JumpPhysicsFrame Source={sampleSource}, T={Time.unscaledTime - sampleStartedAt:F3}s, " +
                $"Pos=({body.position.x:F3},{body.position.y:F3}), " +
                $"BodyV=({body.velocity.x:F3},{body.velocity.y:F3}), " +
                $"PreStepV=({preStep.x:F3},{preStep.y:F3}), Grounded={grounded}, " +
                $"IsJumping={player.isJumping}, Counter={counter:F3}/{player.jumpTime:F3}, " +
                $"Input[Down={JumpPanel.jumpDown},Pressed={JumpPanel.jumpPressed},Up={JumpPanel.jumpUp}], " +
                $"Ay={acceleration:F3}, JumpResidualDV={jumpResidual:F3}, " +
                $"Force={player.jumpForce:F3}, RBGravityScale={body.gravityScale:F3}, " +
                $"WorldG={Physics2D.gravity.y:F3}, " +
                $"FixedDt={Time.fixedDeltaTime:F4}, Simulated={body.simulated}, BodyType={body.bodyType}",
                "Physics");
        }

        samplePreviousFixedValid = true;
        samplePreviousGrounded = grounded;
        samplePreviousIsJumping = player.isJumping;
        samplePreviousVelocityY = velocityY;
        samplePreviousVelocityX = body.velocity.x;
        samplePreviousCounter = counter;
    }

    private void RefreshProfile(
        PlayerMovement player,
        Rigidbody2D body,
        bool allowChange)
    {
        float jumpForce = player.jumpForce;
        float jumpTime = player.jumpTime;
        float bodyGravityScale = body.gravityScale;
        float worldGravityY = Physics2D.gravity.y;
        float fixedDeltaTime = Time.fixedDeltaTime;

        if (!profileInitialized)
        {
            ApplyProfile(
                jumpForce,
                jumpTime,
                bodyGravityScale,
                worldGravityY,
                fixedDeltaTime);
            profileInitialized = true;
            modelRevision = 1;
            BonusRunnerLog.Debug("Jump physics interface profile initialized from live game fields.", "Physics");
            return;
        }

        bool changed =
            Mathf.Abs(jumpForce - rawJumpForce) > 0.05f ||
            Mathf.Abs(jumpTime - rawJumpTime) > 0.005f ||
            Mathf.Abs(bodyGravityScale - rawBodyGravityScale) > 0.05f ||
            Mathf.Abs(worldGravityY - rawWorldGravityY) > 0.01f ||
            Mathf.Abs(fixedDeltaTime - rawFixedDeltaTime) > 0.0005f;
        if (!changed || !allowChange)
            return;

        string previous =
            $"Force={rawJumpForce:F3},Time={rawJumpTime:F3}," +
            $"RBG={rawBodyGravityScale:F3},WorldG={rawWorldGravityY:F3},Dt={rawFixedDeltaTime:F4}";
        ApplyProfile(
            jumpForce,
            jumpTime,
            bodyGravityScale,
            worldGravityY,
            fixedDeltaTime);
        takeoffVelocitySamples.Clear();
        observedGravitySamples.Clear();
        inputDelaySamples.Clear();
        effectiveHoldSamples.Clear();
        horizontalScaleSamples.Clear();
        flightTimeScaleSamples.Clear();
        baseHorizontalSpeedSamples.Clear();
        boostHorizontalDecelerationSamples.Clear();
        travelDurationSamples.Clear();
        landingErrorSamples.Clear();
        modelRevision++;
        BonusRunnerLog.Debug(
            $"Jump physics profile changed; calibration samples cleared. Previous[{previous}] " +
            $"Current[Force={rawJumpForce:F3},Time={rawJumpTime:F3}," +
            $"RBG={rawBodyGravityScale:F3},WorldG={rawWorldGravityY:F3},Dt={rawFixedDeltaTime:F4}], " +
            $"Rev={modelRevision}",
            "Physics");
    }

    private void ApplyProfile(
        float jumpForce,
        float jumpTime,
        float bodyGravityScale,
        float worldGravityY,
        float fixedDeltaTime)
    {
        rawJumpForce = jumpForce;
        rawJumpTime = jumpTime;
        rawBodyGravityScale = bodyGravityScale;
        rawWorldGravityY = worldGravityY;
        rawFixedDeltaTime = fixedDeltaTime;
    }

    private float GetDirectGravity()
    {
        float bodyGravity = Mathf.Abs(rawWorldGravityY * rawBodyGravityScale);
        if (IsFiniteInRange(bodyGravity, 5f, 200f))
            return bodyGravity;

        return FallbackGravity;
    }

    private void LogSnapshot(
        JumpPhysicsSnapshot snapshot,
        PlayerMovement player,
        Rigidbody2D body,
        bool force)
    {
        if (!BonusRunnerLog.IsDebugMode)
            return;

        string signature =
            $"{snapshot.ModelRevision}:{snapshot.RawJumpForce:F2}:{snapshot.RawJumpTime:F3}:" +
            $"{snapshot.BodyGravityScale:F2}:" +
            $"{snapshot.JumpVelocity:F2}:{snapshot.GravityMagnitude:F2}:" +
            $"{snapshot.EffectiveHoldCapSeconds:F3}:{snapshot.InputDelaySeconds:F3}";
        bool changed = signature != lastModelSignature;
        if (!force && !changed && Time.unscaledTime < nextModelLogTime)
            return;

        lastModelSignature = signature;
        nextModelLogTime = Time.unscaledTime + 1f;
        Vector2 bodyVelocity = body?.velocity ?? Vector2.zero;
        Vector2 preStep = player?.preStepVelocity ?? Vector2.zero;
        BonusRunnerLog.Debug(
            $"JumpPhysicsModel {snapshot.Summary}; " +
            $"Raw[Force={snapshot.RawJumpForce:F3}, JumpTime={snapshot.RawJumpTime:F3}, " +
            $"Counter={snapshot.RawJumpCounter:F3}, IsJumping={snapshot.IsJumping}, " +
            $"PreStepV=({preStep.x:F3},{preStep.y:F3}), " +
            $"BodyV=({bodyVelocity.x:F3},{bodyVelocity.y:F3}), " +
            $"RBGravityScale={snapshot.BodyGravityScale:F3}, " +
            $"WorldG={snapshot.WorldGravityY:F3}, FixedDt={snapshot.FixedDeltaTime:F4}]",
            "Physics");
    }

    private void ClearSample()
    {
        sampleActive = false;
        sampleMayLearnGroundKinematics = false;
        completedSampleContextAvailable = false;
        completedSampleMayLearnGroundKinematics = false;
        sampleSource = string.Empty;
        sampleStartedAt = 0f;
        sampleFixedSteps = 0;
        sampleIsJumpingSteps = 0;
        sampleCounterChangedSteps = 0;
        sampleCounterIncreasingSteps = 0;
        sampleCounterDecreasingSteps = 0;
        samplePositiveJumpResidualSteps = 0;
        sampleCounterStart = 0f;
        sampleCounterMinimum = 0f;
        sampleCounterMaximum = 0f;
        sampleCounterEnd = 0f;
        samplePreviousCounter = 0f;
        samplePreviousFixedValid = false;
        samplePreviousGrounded = false;
        samplePreviousIsJumping = false;
        samplePreviousVelocityY = 0f;
        samplePreviousVelocityX = 0f;
        sampleHeldAccelerationSum = 0f;
        sampleHeldAccelerationCount = 0;
        sampleReleasedAccelerationSum = 0f;
        sampleReleasedAccelerationCount = 0;
        sampleReleasedGravityCandidates.Clear();
        sampleHeldResidualSum = 0f;
        sampleHeldResidualCount = 0;
        sampleLaunchVelocityY = 0f;
        sampleFixedTakeoffCaptured = false;
        sampleFixedTakeoffVelocityY = 0f;
        sampleFixedTakeoffTime = 0f;
        sampleCeilingHit = false;
        nextFrameLogTime = 0f;
    }

    private static void AddSample(List<float> samples, float value)
    {
        if (samples.Count >= MaximumSamples)
            samples.RemoveAt(0);
        samples.Add(value);
    }

    private static float Median(List<float> samples)
    {
        if (samples.Count == 0)
            return 0f;

        List<float> ordered = new(samples);
        ordered.Sort();
        int middle = ordered.Count / 2;
        return ordered.Count % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) * 0.5f
            : ordered[middle];
    }

    private static bool IsFiniteInRange(
        float value,
        float minimum,
        float maximum) =>
        !float.IsNaN(value) &&
        !float.IsInfinity(value) &&
        value >= minimum &&
        value <= maximum;
}
