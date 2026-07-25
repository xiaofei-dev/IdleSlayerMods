# Routing, Jumping, and Wall Recovery

## Terrain Model

AutoBonusRunner scans live colliders and combines them with authored Bonus
Stage topology where available. A surface is represented as a horizontal
landing interval with:

- raw and safe left and right boundaries;
- top height;
- map-piece and section identity;
- nearby alternatives, walls, and hazards.

Live physical contact remains authoritative when a cached or authored
prediction disagrees with the game.

## Jump Planning

Jump height and flight time depend on how long jump input is held. Candidate
plans evaluate:

- current horizontal and vertical velocity;
- input-to-takeoff delay;
- observed jump impulse and gravity;
- hold duration;
- predicted landing interval;
- edge margin and landing safety;
- sphere and speed-boost intersections;
- Spirit Boost speed transitions.

The controller can wait for a launch window, issue a timed jump, or retain a
safe passive route when no jump is required.

## Sphere Collection

Visible Bonus Spheres are objectives, not guaranteed pickups. The planner
balances collection with landing safety and the remaining section
requirement.

Some stable authored layouts use a typed objective signature to prevent a
generic planner from replacing a proven collection route. These signatures
identify terrain and sphere shape, not a single world coordinate.

## Small Gaps and Height Changes

A gap that the player footprint and current movement can safely traverse may
be treated as continuous terrain. Larger gaps require a verified landing or a
wall route.

Downward terrain can be walked, jumped, or entered intentionally depending on
the next safe support and collectible route.

## Physical Wall Ownership

When the player touches a real forward wall, physical contact overrides a
stale predicted landing. The shared wall executor can:

1. issue an attached bounce;
2. verify upward physics steps;
3. release after the planned hold;
4. continue an attached climb;
5. transfer to the wall top or next wall;
6. return control after a stable support is confirmed.

Retries are bounded. A rejected impulse is not counted as a successful climb.

## Recovery

Recovery can activate when:

- a jump is not accepted by the game;
- actual velocity differs from the prediction;
- the player lands on an intermediate support;
- a new physical wall is contacted;
- a target becomes stale or disappears;
- pit descent is confirmed;
- a respawn begins with temporary acceleration.

Recovery warnings identify the failure domain. Many warnings describe a
successful correction rather than a death.

## Background Input

The mod uses its own jump press, hold, and release state while route control is
active. It does not depend on a foreground mouse click, allowing supported
Bonus Stage control while the game is unfocused.

[Back to the Complete Manual](../MANUAL.md)

