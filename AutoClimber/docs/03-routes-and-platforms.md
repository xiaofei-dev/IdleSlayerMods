# Routes, Platforms, and Completion

## Live Route Planning

AutoClimber scans the active platform layout and evaluates real landing
intervals, horizontal travel time, vertical motion, edge exposure, platform
movement, and the current jump type. It locks a target long enough to avoid
rapid route changes but can release or replace it when the physical route is
no longer valid.

## Platform Priorities

- **Finish platform:** highest priority once it is available and reachable.
- **Golden and strong platforms:** preferred when their super jump provides a
  safe and useful route.
- **Normal platforms:** standard route progression.
- **Fragile platforms:** lower preference and used when they remain the best
  safe option.
- **Moving platforms:** evaluated using reflected movement prediction rather
  than impossible positions beyond the play-area boundary.

Platform type never overrides basic physical reachability.

## Landing Control

The controller steers toward a safe area near the platform center. Strong and
golden platforms use tighter landing tolerances because their higher bounce
speed makes edge contacts less forgiving. Near landing, route commitment is
preserved unless a clearly safer survival option exists.

## Recovery

Recovery can activate when:

- the locked platform disappears or is recycled;
- the target is passed while descending;
- no normal forward candidate remains;
- movement stalls;
- a lower physical platform is the only safe landing.

The planner retains lower rescue platforms in its scan so temporary downward
progress is possible when it prevents a death.

## High-Altitude Behavior

Later sections of long challenges are treated more conservatively. Edge
exposure, marginal reach, fragile platforms, and unique-route constraints
receive stricter handling while safe super-jump platforms remain valuable.

## Real Finish Handling

The score threshold is informational. It can vary by challenge, practice
layout, buffs, and game state. AutoClimber therefore waits for the actual
spawned finish platform, confirms a stable landing, and holds right until the
real exit is triggered.

Practice, 1,000-point, and 2,000-point routes use the same finish principle.

[Back to the Complete Manual](../MANUAL.md)
