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

    public float LastObservedX;
    public float LastObservedTime;

    // -1 = left, 0 = unknown/stationary, 1 = right
    public float DirectionX;

    public bool HasPreviousObservation;
}

internal sealed class PlatformCandidate
{
    public GameObject GameObject;
    public Collider2D Collider;
    public int InstanceId;

    public PlatformType Type;
    public string SpriteName;

    public Vector2 CurrentPosition;
    public Vector2 ColliderSize;

    public bool IsMoving;
    public float MovementDirectionX;
    public float PlatformVelocityX;

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
    public float DescendingSpeed;
    public float EffectiveSafeHalfWidth;
    public float RequiredHorizontalDistance;
    public float MaximumHorizontalReach;
    public float ReachRatio;
    public float LandingMargin;

    public int SuccessorCount;
    public int ThirdStepOptionCount;
    public float RouteScore;
    public float Score;

    public string RejectionReason;
}
