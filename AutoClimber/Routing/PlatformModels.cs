using UnityEngine;

namespace AutoClimber;

internal enum PlatformType
{
    Unknown,
    Normal,
    Strong,
    Golden,
    Breakable,
    Fake
}

internal sealed class PlatformTrack
{
    public int InstanceId;

    // Ascending Heights reuses platform GameObjects aggressively. InstanceId
    // alone therefore does not identify a route node for the lifetime of a
    // run. Generation is incremented whenever the pooled object changes
    // height/type; a long observation gap separately resets motion history.
    public int Generation;

    public float LastObservedX;
    public float LastObservedY;
    public float LastObservedTime;
    public float GenerationObservedSince;
    public float GenerationChangedAt;

    public PlatformType LastObservedType;

    public int ConsecutiveObservations;
    public int StationaryObservations;

    // -1 = left, 0 = unknown/stationary, 1 = right
    public float DirectionX;
    public float VelocityX;

    public bool HasPreviousObservation;
    public bool WasRecycled;
}

internal sealed class PlatformCandidate
{
    public GameObject GameObject;
    public Collider2D Collider;
    public int InstanceId;
    public int Generation;

    public PlatformType Type;
    public string SpriteName;

    public Vector2 CurrentPosition;
    public Vector2 ColliderSize;

    public bool IsMoving;
    public float MovementDirectionX;
    public float PlatformVelocityX;

    public float ObservationAge;
    public int ConsecutiveObservations;
    public bool GenerationStable;
    public bool RecentlyRecycled;

    // Applied by the runtime for the current jump. This is a soft risk score
    // for a logical platform signature that has just vanished from the pool.
    public float LifecycleHazardPenalty;

    public float EstimatedLandingTime;
    public float PredictedLandingX;

    public float RequiredHorizontalDistance;
    public float MaximumHorizontalReach;

    public float DeltaY;

    public bool VerticallyReachable;
    public bool HorizontallyReachable;
    public bool Reachable;

    public float PriorityScore;
    public string RejectionReason;
}

internal sealed class V5RouteDecision
{
    public PlatformCandidate Candidate;

    public bool Feasible;
    public bool IsEmergency;

    public float LandingTime;
    public float LandingX;
    public float CenterwardLandingX;
    public float ControlHalfWidth;
    public float EdgeReserve;
    public float DescendingSpeed;
    public float EffectiveSafeHalfWidth;
    public float RequiredHorizontalDistance;
    public float MaximumHorizontalReach;
    public float ReachRatio;
    public float LandingMargin;

    public int SuccessorCount;
    public int CenterReturnSuccessorCount;

    public float PredictedApexY;
    public float ApexOvershoot;
    public float LifecycleRisk;
    public float EdgeExposure;

    public bool GenerationStable;
    public bool RetentionSafe;
    public bool EdgeSafe;
    public bool IsEdgeLastResort;
    public bool InwardLandingPlanned;
    public bool IsFinishApproach;

    public float RouteScore;
    public float Score;

    public string RejectionReason;
}
