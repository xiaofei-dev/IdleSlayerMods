# AutoBonusRunner Routing Algorithm

## Objective

AutoBonusRunner must choose and execute jump actions from live game state. It must not replay a fixed timeline. Each decision must account for current X/Y position, velocity, section, Spirit Boost, live platform geometry, hazards, learned jump physics, wall contact, and the result of the previous action.

Normal navigation controls jump press/release; press duration changes jump height. V0.29 additionally permits direct bow shots and a selected ground Wind Dash only during post-quota completion traversal. Horizontal motion is supplied by the game and can continue while the game is in the background.

## Current revision status

The current source target is public `1.0.0`, internal `V0.31`, configuration schema `4`. V0.30 fixed the completion-entry gate: `PreBonusMode` plus `CollectedSpheres >= ceil(RequiredSpheres)` is sufficient even after a section change resets active-gameplay history. V0.31 changes route topology and phase-aware speed planning from the two-session V0.30 trace.

Historical V0.30 deployment identity is length `380928`, SHA-256 `1A7607B4A4D639DA053AC52E69549A19FF40D9660F1C02A66C81E631B63021C2`. V0.31 is synchronized at length `394240`, SHA-256 `A1D575CB397EB60B75C9A019C4FBEA0AF27E620BC1B43FA3F2F3AD8270C72EE1`.

Section 0's map semantics and planner-specific routing policies remain unchanged because the V0.27 run completed it. V0.28 changes the shared wall continuation scheduler and two Section 1 route contracts, so the next trace must still regression-test Section 0 rather than assume source locality guarantees runtime equivalence.

## V0.31 speed-phase and Section 2 route corrections

1. `planningVX = abs(live Rigidbody VX)` whenever it is in `(1,80)`; the reliable run estimate is only a fallback for scanning and diagnostics. If a grounded player has a downstream route but live VX is `<= 1`, send no DOWN.
2. Accept reliable free-running speed during active gameplay and successful completion traversal. Reject pit, wall-action, out-of-range, and collision-like airborne slowdown samples. A verified Spirit wall-speed increase may replace the wall latch.
3. If an accepted sample changes by at least `max(4.0, 0.20 * priorVX)`, clear/recompute the route and cross one subsequent `FixedUpdate` before issuing a ground-jump DOWN. Wind Dash uses a separate two-fixed-step barrier after activation.
4. Resolve a section cruise floor from three distinct grounded, near-zero-VY physics steps whose VX values form an exact plateau (`<= 0.04` spread). Reset it on every section change. Normal motion uses that floor directly; Spirit Boost and completion-ability speeds remain transient and decay toward `max(model BaseVX, sectionCruiseVX)`.
5. Ground 6 S1 -> S6 is an authored underpass/contact route. The S4/S5 overlap invalidates every pre-face airborne approach, including optional sphere jumps. Arm passive movement and wait for confirmed S6 contact. This explicit no-input intent is exempt from generic jump-acceptance timeout logic.
6. A wall exit has two horizontal phases: zero reported VX while attached, then latched pre-contact motion after the lip. Age transient boosted speed through the predicted zero-travel lip time, integrate horizontal travel only after lip clearance, and log `AttachedVX`, latched `PostLipVX`, predicted `LipVX`, base VX, and speed mode.
7. A known wall-exit target can be selected on the first attached press. The selected press remains owned through lip clearance until its planned deadline. On Section 3 Ground 7 S2, if the nearest target has no safe hold, enumerate static forward candidates and promote the first solver-safe support.
8. Completion Wind Dash requires no committed jump/wall owner plus two distinct fixed steps of near-zero-VY ground contact. The proof is observed even during cooldown, is reset by airborne/contact gaps and after activation, and cannot be reused from an earlier landing. Its replanning barrier never sends an unconditional UP. Terminal reward jump pulses use the same stable-ground proof; bow fire remains independent.

Required V0.31 evidence includes `MappedGround6UnderpassWallDrop`, `WallDropRouteArmed`, `HorizontalSpeedObservation`, `SectionCruiseSpeedEstablished`, `JumpPlanningDeferred`, `CompletionWindDashDeferred` or `CompletionWindDashPlanningBarrier`, `WallExitKinematics`, `LipVX`, `WallExitHoldPreserved`, and, when needed, `WallExitTargetSpeedPromoted`.

## V0.29 post-quota completion state machine

`BonusMode observed -> current quota complete -> PreBonusMode -> normal terrain planner continues -> optional grounded Wind Dash -> terminal route confirmed -> immediate minimum jump pulse plus direct bow fire`

- Navigation remains authoritative while any next surface is available; reward actions cannot replace a valid route or intentional drop.
- A valid scan with `HasNext=false` confirms the terminal route immediately. An invalid scan must remain grounded across three distinct fixed steps before fallback actions are allowed, preventing a one-frame perception failure from causing an early pulse.
- The minimum pulse calls `JumpPanel.OnPointerDown` and `OnPointerUp` in the same frame, matching AutoJump's contextual short-jump behavior without referencing that mod.
- Bow fire calls `PlayerMovement.ShootArrow()` at a 0.10 second cadence only when the Sacred Book of Projectiles is unlocked and bow use is not disabled.
- Wind Dash is attempted only when the main ability UI is visible, the selected ability is Wind Dash, it is unlocked and ready, no trajectory owns input, and ground contact has near-zero VY for two distinct physics steps (with the stable-Y fallback retained). Failure is non-fatal and navigation continues.
- Evidence records include `CompletionTraversalStarted`, `CompletionTraversalNavigation`, `CompletionTerminalCandidate`, `CompletionRewardAction`, `CompletionWindDashActivated`, `CompletionWindDashUnavailable`, `CompletionTraversalEnded`, and `CompletionTraversalReset`.

The deployed V0.29 DLL is length `380928`, SHA-256 `B92D6661A02BC41EBB0432629C0AD78AC9736294417CE8B07952789E69BF63C4`, identical in the build output, Mod Manager source, authoritative root loader, and nested compatibility loader.

## Core invariants

1. Never issue a jump without an explicit target, predicted result, and reason.
2. Separate route correctness from action correctness:
   - Route error: the selected surface or maneuver was wrong.
   - Execution error: the route was valid, but timing or physics prediction was wrong.
3. Refresh and revalidate from live state after meaningful contact, landing, speed change, map-generation change, or prediction error, but do not discard an unchanged target solely because remote pooled-clone recycling advanced the global generation.
4. Do not trust a `grounded` pulse as a landing. Confirm stable support across two distinct `FixedUpdate` steps.
5. Do not continue control after a death or pit transition. Release all input, clear route state, and wait for stable respawn.
6. Do not use permanent world-coordinate scripts. Use static local templates transformed by live pooled clone positions.
7. A wall top can be visible but not directly reachable. Treat wall-face contact as an intermediate action state.
8. Only the authoritative `PlayerMovement.instance` may maintain synthetic input or contribute fixed-step feedback.
9. A candidate marked `Missed` is not a valid `Wait`. Re-solve from live X or return an explicit invalid/missed result.
10. Keep calibration domains separate by section and action context. Ground takeoff, wall reset, and collision-speed samples are not interchangeable.
11. Physical wall contact is not the same as vertical stall. After UP, never reset a still-positive wall impulse merely because the body remains attached below the lip; observe release height or a real apex/descent first.
12. Authored route success may require an intermediate face contact. A safe top landing is not success when it skips a still-active Ground 3 objective lane, and a high Ground 5 pillar is not terminal while its mapped lower exit remains ahead.

## Inputs observed each control cycle

- Bonus-stage active state and game-state name.
- Map name and section index.
- Player instance identity.
- Player world position and rigidbody velocity.
- Grounded/contact state and collider bounds.
- Collected and required sphere counts.
- Remaining section timer.
- Fell-off/death state.
- Spirit Boost state.
- Live static-map registry generation and piece instances.
- Nearby standable surfaces, hazards, spheres, and forward wall contact.
- Current jump-physics snapshot and calibration confidence.
- Active plan, action phase, launch state, and last observed result.

## Geometry model

A `BonusBoardSegment` contains raw bounds, safe bounds, top Y, collider identity, piece name, piece origin, instance ID, registry generation, and static surface index.

Safe bounds inset the raw surface using player half-width plus an additional `0.15` edge margin. A landing on the raw collider can still be physically valid even if it misses this preferred interval. The planner therefore distinguishes:

- preferred safe landing;
- recoverable raw support;
- unsupported airborne state;
- lethal/pit state.

The static map registry validates the live active clone sequence and transforms local surfaces into world surfaces. Dynamic physics scans remain necessary for safety-road talents, transient colliders, and verification.

## Surface scan

1. Refresh the static map for the current section.
2. Find the player support surface using live physics.
3. If live support is unavailable, attempt a static-map match around the player feet.
4. Build ordered candidate surfaces ahead, using `28` to `80` units of speed-dependent lookahead and `8` units behind.
5. Merge adjacent equal-height surfaces across prefab seams.
6. Reject tiny or non-standable surfaces.
7. Retain meaningful lower routes down to `15.25` units below, ordinary rises up to `5.35`, and wall-climb tops up to `12.0` above.
8. Return current, next, optional intermediate, alternatives, gap, height delta, edge distance, and evidence.

## Obstacle classification

Classification is topology-based:

| Kind | Current rule | Intended behavior |
|---|---|---|
| `ContinuousRoad` | gap `<= 0.10`, `abs(deltaY) <= 0.35` | do not jump merely because a prefab seam exists |
| `LowerLanding` | `deltaY < -0.35` | prefer verified natural drop when it remains on safe terrain |
| `AdjacentWall` | rise `> 0.35`, gap `<= 0.35` | target the wall face, not the upper top as the first action |
| `NarrowPillarTrench` | narrow positive-rise target across a small gap, with source/continuation topology proving a rising chain | enter the trench, establish wall contact, then perform separated wall presses |
| `WideWallTrench` | rise `>= 5.35`, gap `2.50..5.25` | dynamically solve a descending hop into the physical lower wall face, then climb after confirmed contact |
| `WallAcrossGap` | rise `> 5.35` outside the prior rule | approach jump followed by wall-contact phases |
| `RaisedLanding` | rise `> 0.35` | only use a verified direct trajectory |
| `OrdinaryGap` | otherwise | enumerate direct landing candidates |

Classification thresholds are current heuristics, not proven game constants. Logs must retain the raw geometry so errors can be reclassified later.

Section 1 has two authored exceptions that must remain distinguishable after generic classification:

- Ground 3 surfaces 2 and 3 are floating bodies spanning local Y `-1..4`. The first face requires an active descending hop because a passive fall passes below the physical body. The equal-height second face contains an objective trench lane; while that lane has active spheres, surface 2 -> surface 3 is a mandatory face-contact edge. A top landing cannot satisfy it. Physical contact must arm attached descent before climb-out pulses.
- Ground 5 surfaces 2, 3, and 4 are solid pillars extending to local Y `-10`, at tops `0`, `4`, and `7`. Its intended maneuver is passive gap entry followed by sequential, physical-contact-confirmed wall pulses. Ground 3's attached objective descent must not be applied to this chain. After surface 4, the nearest authored continuation is the broad lower exit `[P+7,P+14] @ -2`, a nine-unit drop that must remain an explicit exit target.

## Physics model

The fallback model uses approximately:

- launch vertical velocity: `18.627`;
- gravity magnitude: `68.67`;
- input delay: `0.02 s`;
- hold cap fallback: `0.15 s`;
- observed native hold cap in recent logs: approximately `0.18 s`.

The V0.26 trace proves that coarse correction can be worse than the analytical fallback. Every request from `0.020` through `0.075 s` could use one aggregate tier median, so different requested durations became the same predicted jump. A `0.075 s` ordinary jump was predicted as `0.529 s` and `6.251` units but actually produced `0.683 s` and `8.329` units, a `+2.078` landing error.

V0.27 therefore uses one `4 x 6` duration grid. Every height band (`Level`, `ModerateUp`, `HighUp`, and `Down`) is crossed with the same six hold buckets: `H02` (`<= 0.030 s`), `H04` (`<= 0.050 s`), `H06-075` (`<= 0.0825 s`), `H09-105` (`<= 0.1125 s`), `H12-135` (`<= 0.1425 s`), and `H15-180` (longer holds through the supported cap). At minimum, calibration also distinguishes section and ordinary ground jump versus wall pulse. Learned observations correct the analytical curve; a coarse median may not replace it across materially different holds. Section 0 route/travel observations do not calibrate Section 1-or-later routing.

Ordinary ground takeoff velocity near `18.627` and wall reset velocity near `20` are separate populations. Only clean grounded-to-ground samples update the ground takeoff model. Wall-contact pulses may update a wall-specific model but must not enter the ground sample median. Large errors must be logged as evidence even when excluded from a stable estimator; silently rejecting every failed jump prevents correction.

Duration-grid ownership is stricter than general feedback eligibility: only an observed input-DOWN-to-confirmed-stable-landing interval writes a duration cell. Neither horizontal travel nor launch/start-speed is a proxy for elapsed flight time, so those observations update only their dedicated travel or speed estimators. This prevents one physical sample from contaminating the duration curve through multiple incompatible units.

The Harmony input/feedback bridge runs only for the primary `PlayerMovement.instance`. V0.26 allowed duplicate instances to advance fixed-step capture roughly twice per real physics step, which made observed released gravity appear near half the real value and contaminated timing barriers.

Horizontal distance must never be computed from hold duration alone. It depends on live horizontal velocity, predicted flight duration, game movement behavior, Spirit Boost, section speed, contact offsets, and learned travel scale/bias.

For a wall route, latch horizontal speed from a reliable free-running sample before collision. Do not replace a latched value near `11.9` with a collision slowdown such as `2.36`. A verified upward speed transition associated with Spirit Boost is allowed to replace the latch immediately; ordinary downward collision transients are rejected and logged.

## Direct jump planning

Candidate holds are:

`0.020, 0.030, 0.040, 0.050, 0.060, 0.075, 0.090, 0.105, 0.120, 0.135, 0.150, 0.165, 0.180 seconds`.

For each candidate:

1. Predict the vertical trajectory using current learned jump physics.
2. Solve when the descending body reaches the target top.
3. Integrate live horizontal travel over that flight time.
4. Apply calibrated travel scale and only maneuver-specific measured bias.
5. Require upward and horizontal clearance from intervening geometry and hazards.
6. Require predicted landing inside the target safe interval, with a preference inset of `0.30`.
7. Score by safety margin, predicted error, sphere value, and unnecessary airtime.
8. Derive a launch window from the chosen target, flight time, and current speed.
9. Wait until the player reaches that launch window; then press for the selected duration.

The action log must record planned hold, actual hold, planned launch X, actual launch X, predicted landing, actual landing, target bounds, error, and support identity.

## Natural drops and lower routes

A lower surface must not automatically cause a jump. The planner first evaluates whether simply leaving the edge produces a safe descending intersection with a lower surface. It predicts fall time and horizontal displacement from live speed and gravity, checks hazards and intervening faces, and uses the lower landing if the predicted body footprint is safe.

Large authored drops, including approximately `9` and `14` units, can be valid route edges. Falling below `Y = -3.5` is handled separately by the pit/lifecycle guard and is not inferred solely from height delta.

## Emergency raw-edge recovery

Recent logs show valid jumps landing on the raw collider near its far edge but just outside the conservative safe interval. Waiting for a normal plan then allowed the runner to walk off and die.

`EmergencyLateRawLanding` is legal only when all of the following are true:

1. The player is still physically supported by the raw collider.
2. The position is outside or nearly outside the preferred safe interval but no farther than approximately `Right + 0.12`.
3. An immediate trajectory from the current position reaches the next verified safe target.
4. The trajectory clears known hazards and walls.
5. The mapped support identity and current geometry still match; registry generation remains diagnostic rather than a standalone rejection condition.

This is a recovery action, not permission to weaken all landing margins.

## Wall approach

Wall approach holds currently considered are:

`0.020, 0.040, 0.060, 0.075, 0.090, 0.105, 0.120 seconds`.

The preferred first contact is roughly `0.44 s` into flight and must occur before roughly `0.52 s`, below the lip rather than above it. Short holds are important for low walls and high horizontal speeds.

The forward wall detector samples three vertical offsets (`-0.42`, `0`, `+0.42` of collider extent) and probes an additional `0.42` units. It accepts a suitably vertical face and reports body gap, face X, normal, collider, and whether contact is physical or only imminent.

Nominal planning and live salvage are separate operations. The nominal source window may become obsolete after an earlier jump lands farther right than predicted. If live X is beyond the nominal usable-right boundary:

1. mark the nominal candidate `Missed`;
2. compute current time-to-face from live X and the reliable/latched horizontal speed;
3. re-evaluate every legal wall-approach hold against the physical face's vertical span and desired contact band;
4. execute an immediate salvage hold when one is valid;
5. otherwise return an explicit invalid/missed route and its rejected candidates.

The outer plan may never convert this state to `Valid=True, Action=Wait`. That V0.26 contract bug directly caused deaths beside reachable walls.

### Ground 3 attached objective descent

Ground 3 surface 3 is not an ordinary same-height wall transfer. On physical contact with its leading face while the active objective lane below still contains spheres:

1. when surface 2 captures surface 3 as its successor and lower-lane spheres exist, set `MandatoryFaceContact`; suppress ordinary top-landing and direct-exit success for that edge;
2. first try a direct two-dimensional face intercept from live contact; if none is safe, select the strongest setup pulse whose complete FixedUpdate/apex envelope stays at least `0.30` below the current release height;
3. keep UP released through the observed setup apex. Recompute every frame until a final pulse simultaneously clears the current platform's trailing top and intersects the mapped surface-3 face while descending inside the safe feet window `[1.5,3.45]`; each candidate uses the exact actuator step count, not a render-time duration envelope;
4. release the final powered phase on the first height-gated old-wall horizontal-resume observation, or immediately before fixed physics step `N+1`, not the later whole-body lip boundary;
5. lock out all further DOWN input until finite mapped surface-3 side contact is first observed and then survives a later FixedUpdate;
6. require physical contact with surface 3's leading face before clearing the route contract; a stable top landing emits `MandatoryWallFaceContactMissed`, is classified as route execution failure, and triggers a live replan rather than false success;
7. on valid face contact, enter `AttachedObjectiveDescent` and keep synthetic jump released; the confirmation frame has an UP-only handoff barrier and cannot recurse into generic wall input;
8. require continued contact with the same mapped face or strict slow-body persistence in its near-face corridor;
9. descend only to a bounded objective/safety threshold derived from the lowest active lane sphere and the authored spikes;
10. tolerate a transient ray miss only while the body remains slow and inside the mapped near-face corridor; if neither ray contact nor that kinematic contact test succeeds, clear `AttachedObjectiveDescent` immediately and relinquish its exclusive frame ownership so ordinary wall-contact, airborne, and lifecycle handling can resume;
11. after reaching the threshold, enter the normal contact-confirmed bounded pulse train;
12. target the broad downstream lower road after climb-out, not the narrow top as an assumed terminal landing.

This policy exists to collect the three local lane spheres at X `0`, Y `0.5/1.5/2.5`. Immediately sending a large pulse from high contact is a route failure even if the player survives.

## Wall-climb state machine

The current phases are:

`None -> EnteringTrench or ApproachJumpInFlight -> AwaitingWallContact -> WallJumpPhaseOne -> (AwaitingNextWallPress -> WallJumpPhaseTwo)* -> ExitFlight -> Completed or Failed`.

Ground 3 `S2 -> S3` adds an explicit managed substate inside those public phases: `SetupPulse -> WaitForObservedApex/Descent -> FaceInterceptPulse -> TargetContactConfirmation`. These are logged states and plan commitments, not blind pulse-count aliases.

`WallJumpPhaseTwo` denotes every continuation pulse after the first; it is not a two-pulse limit.

Required behavior:

1. Choose trench entry or an approach trajectory from the classified topology.
2. Do not start the climb before the intended wall face is detected and validated against target bounds.
3. Press the first wall jump only while the detector confirms physical touching
   of the intended face. Predicted contact coordinates are not sufficient.
4. Release it after the chosen hold.
5. Wait at least one distinct fixed-physics step.
6. Confirm continued wall attachment or a valid wall-relative trajectory.
7. If VY remains above `+0.25`, keep UP released and observe the residual rise. Complete the wait when release height is reached or VY reaches apex/descent; expire it after `0.45 s` so stale telemetry cannot hang control.
8. If the body is still below the lip after apex/descent, revalidate physical/kinematic contact. Only then may another separated DOWN occur.
9. Recompute a genuinely required pulse from current remaining rise and learned physics. Ordinary attached pulses retain the evidence-supported `0.075..0.135 s` range. Ground 3's mandatory face pulse searches the complete native `0.020..0.180 s` candidate set. Each request maps to one exact actuator ceiling `N`; setup evaluates exact `N`, while final interception evaluates possible height-gated early releases from lip crossing through `N` and `±1` horizontal collision-timing step. Every modeled branch must clear the old top and intersect the finite face safely.
10. Track the body along the wall and validate lip crossing.
11. If the body leaves a completed low face and a nearby forward/higher static
     face is known, promote that face and return to `AwaitingWallContact`.
12. If an exit transfer was predicted to land on top but the body physically contacts the target face, actual contact overrides the optimistic prediction: reopen wall authority, retain the route pulse budget, and re-run the residual-rise gate before solving another bounded pulse.
13. Capture the nearest authored forward continuation even when it is lower. Ground 5 surface 4 -> exit road is an explicit `MappedDownstreamExit`, not absence of a target.
14. End only on route-valid confirmed top/exit support or a clearly failed/dead state.

V0.7 originally added two execution invariants: a wall-top landing selected by
one pulse remains the target for its continuation, and each pulse calculates
`holdUntilLip` from remaining rise, input
delay, and observed jump velocity. The input is released at the observed lip
crossing so the runner does not convert a wall climb into a long ordinary jump
that skips the next crack or narrow pillar.

Current runtime constants include a `0.115 s` fallback wall hold, generic attached-recovery holds from approximately `0.115` to `0.165`, target landing holds from `0.020` to `0.180`, adaptive staged pulses from `0.075` to `0.135`, a residual-rise threshold of `+0.25` VY with a `0.45 s` wait, a `0.30` mandatory-setup detach margin, a `1.20 s` setup budget, a `1.25 s` face-contact-watch budget, a `0.90 s` attached-descent budget, and a Ground 3 S3 route-safe contact window of `[1.5,3.45]`. The final pulse's horizontal-resume release requires at least `0.03` X movement and feet no lower than `releaseY - 0.15`; mapped-face ray/body tolerances are `0.35/0.16`. Setup, intercept, contact watch, and descent budgets are converted to fixed-physics step counts. The runtime caps an ordinary wall route at six separated pulses; the mandatory S2 -> S3 route forbids generic fallback after a committed face-intercept failure.

The V0.27 wall trace calibrates the mandatory solver in fixed steps rather than nominal hold time:

```text
dt = 0.020
G = 68.67
held-step VY = RawJumpForce - G*dt = 18.6266
held-step rise = 0.372532
released semi-implicit apex tail = 2.343328
actual no-lip rise after N held ticks = 2.343328 + 0.372532*N
```

Two requested holds near `0.092 s` produced rises separated by exactly one `0.020 s` tick. Mandatory requests therefore map to `N = ceil(hold / fixedDeltaTime)`, and `JumpController` preserves held input for exactly those `N` authoritative `PlayerMovement.FixedUpdate` calls before releasing ahead of `N+1`. The solver uses the same semi-implicit Y update and integrates X per fixed step from the live route speed toward the observed base speed using boost deceleration. A height-gated horizontal-resume observation may release a final intercept earlier, and the solver conservatively validates those possible lip-to-`N` release steps. Continuous hold multiplication is diagnostic only and cannot authorize input.

`StagedAttachedBounce` is intended for a narrow wall that has no safe direct top/exit solution. Every pulse is separated by a fixed step and recomputed from current height, but separation alone is not authorization: V0.28 first observes any residual upward impulse. The action retains the wall top so an intermediate pulse cannot replace it with a distant exit surface. When a distinct nearby higher face follows the completed wall, it is promoted instead of silently retargeting the current pulse; when a completed Ground 5 high pillar is followed by its lower road, that road is retained as the explicit exit.

## Landing confirmation

A landing is accepted only when:

- the same support is observed on two distinct `FixedUpdate` sequence values;
- a current support scan is valid;
- absolute vertical velocity is no more than approximately `2.5`;
- jump input is released;
- the player is not still rising.

A single grounded pulse, transient collider overlap, wall scrape, or raw contact during a held jump is not enough.

After confirmation, calculate and log:

- target surface versus actual support;
- target safe interval versus actual X;
- predicted versus actual flight time;
- predicted versus actual landing X;
- planned versus actual hold;
- speed and Spirit Boost changes during flight;
- route success, recoverable miss, wrong surface, overshoot, undershoot, wall attachment, or death.

## Death and transition guards

When `fellOff` is true, the player falls below approximately `Y = -3.5`, the player instance changes, or a large position discontinuity occurs:

1. Release all synthetic jump input immediately.
2. Cancel hold timers, plans, target locks, wall phases, and pending feedback.
3. Stop route planning during the death/transition interval.
4. Wait for a stable respawn support over two fixed steps plus the configured restart delay.
5. Refresh the section and static registry before resuming.

This prevents the previous failure where the mod kept jumping randomly after death.

## Background input

`Application.runInBackground` is enabled. Jump state is injected through the native player jump path using `JumpController` and Harmony input patches. The mod must not depend on a foreground-only mouse click simulator. Manual left-click input remains available when automation is disabled, and manual jumps should still be observed for calibration.

AutoBonusRunner does not inspect, patch, configure, or suppress AutoJumpMod. The user owns input exclusivity and must turn AutoJumpMod off before testing this planner.

## Successful section continuation

`PreBonusMode` is not always a death state. When sphere quota is complete,
V0.8 keeps using the normal route planner so post-finish walls and reward
approaches remain executable. It deliberately does not gate this state on
`characterFellOff`, because that field can remain true after an earlier revive
even though the current section has completed successfully. If no route action
is currently required, it sends a short shared jump/attack pulse to fire the
bow or claim the reward. An incomplete/failed `PreBonusMode` remains
input-blocked because its remaining sphere quota is still positive.

## Per-frame decision outline

```text
observe game and player state
if automation is disabled:
    release synthetic input
    observe manual jumps for diagnostics/learning
    return

if not in a supported Bonus Stage:
    reset transient route state
    return

if death, pit, respawn, instance change, or position discontinuity is active:
    release input and run lifecycle stabilization
    return

refresh live map registry
scan support, next surfaces, hazards, spheres, and wall contact
accept feedback only from the primary PlayerMovement instance
update free-running speed, rejecting collision slowdown; accept verified boost increases

if an action is already executing:
    advance its fixed-step state machine
    after wall UP, keep input released while observed VY remains positive
    if release height is reached, continue exit/contact handling without another DOWN
    else at apex/descent, revalidate contact before solving a continuation pulse
    verify contact/landing/failure
    update feedback when the result becomes unambiguous
    return

if stable support is near the raw far edge:
    try EmergencyLateRawLanding

classify current topology
apply authored Ground 3/Ground 5 policy for Section 1
require Ground 3 S2 -> S3 face contact while lower-lane objectives remain
retain Ground 5 S4 -> lower-road continuation as MappedDownstreamExit
choose natural drop, direct landing, trench entry, approach, or wall climb
enumerate physics-valid candidates using current speed and calibration
if a nominal wall window is behind live X:
    re-evaluate all wall-approach holds from current state
if no verified action exists:
    log NoVerifiedRoute with full geometry; do not blindly jump
else if inside the computed launch window:
    execute the chosen hold
else:
    keep observing and revalidate the plan every frame
```

## Diagnostic contract

Every plan should make it possible to answer four questions from the log alone:

1. What did the mod see?
2. Why did it select this route and action?
3. What did it predict?
4. What actually happened?

Essential fields include session, automation-authority boundary, section, fixed-step sequence, authoritative player instance, position, velocity, Spirit Boost, registry generation, current/next/intermediate surfaces with raw and safe bounds, piece names and local coordinates, obstacle kind, authored topology policy, hazards/objectives, selected maneuver, all rejected candidates and reasons, nominal and live-salvage launch windows, target, planned and actual hold, predicted and actual flight/travel/apex/landing, free-running and wall-latched speed, accepted/rejected calibration samples with context, wall phase/contact, residual-rise wait state, mandatory-face-contact state, downstream-exit ownership, actual landing support, prediction error, and lifecycle reset reason.

Every terminal or corrective record has one primary failure domain:

| Failure domain | Meaning |
|---|---|
| `Perception` | Surface, wall, hazard, sphere, player, or map generation was read incorrectly |
| `Topology` | Static geometry was correct but assigned the wrong authored semantics |
| `RouteSelection` | The semantic route was known but the wrong target/maneuver was chosen |
| `PhysicsModel` | Route and delivered input were correct, but predicted trajectory was wrong |
| `InputDelivery` | Actual press/release duration or native state did not match the request |
| `ActionState` | Contact/phase ownership, pulse continuation, or fallback was lost |
| `LandingRecognition` | Actual support was reached but not accepted correctly |
| `Lifecycle` | Death, respawn, section transition, or player-instance handling was wrong |

The expected, delivered, and actual values must appear together. A generic warning without the candidate, state, and failure domain is diagnostic noise and should be removed or demoted.

V0.9 adds two diagnostic records without changing route selection or input:

- `ControlGate` is emitted whenever eligibility changes. It records automation/config state, game state, player availability, completion quota, sticky fall state, manual-input cooldown, active learning/plan state, and wall phase. This explains why control did or did not run.
- `ActionPhysicsFrame` records every fixed-physics step during an active wall action. Ordinary jumps and successful completion traversal are sampled on event changes and at `0.25 s`. Each record contains route/attempt IDs, position/velocity/collider bounds, native player jump fields, synthetic `JumpPanel` flags, down/up times, selected plan and target identity, every wall state flag and counter, live wall raycast/contact data, current/next/intermediate/alternative surfaces, and the active physics model.

The high-frequency trace is intentionally limited to short wall actions so diagnostic I/O does not alter ordinary whole-stage timing.

## V0.10 evidence-backed corrections

- V0.10 cleared `bonusGameplayStarted` and required the new section to enter `BonusMode` before completion input. V0.30 superseded that gate after a real reward transition proved the completed `38/38` quota remains authoritative in `PreBonusMode`; the next `0/N` quota ends completion traversal naturally.
- `CompletionRewardPulse` is now an actual end-of-route fallback. It is not sent when a next surface exists, so it cannot override `IntentionalDrop` and cannot interrupt a valid plan that is waiting for its launch window.
- The generic `holdUntilWallLip` minimum is applied only to blind attached climbing. A target-aware wall-top/exit prediction and a deliberate staged attached bounce keep their computed hold. In the V0.9 trace, a safe `0.090 s` wall-top candidate was incorrectly changed to `0.180 s`, crossed the lip with excessive velocity, and landed beyond the target.
- Ordinary grounded jump scoring, platform selection, and physics calibration are unchanged in V0.10.

## V0.11 evidence-backed corrections

- V0.11 historically required `bonusGameplayStarted`; V0.30 superseded that requirement with the live completed-quota predicate described above.
- No optional-mod compatibility or discovery code participates in the control path.
- The public plugin version remains `1.0.0`; only the internal development identity advances.

## V0.12 height-specific feedback correction

The physics snapshot already contained duration buckets for moderate rises, high rises, and downward landings, but earlier code rejected non-level samples both when recording and when reading them. As a result, logs could show many completed jumps while these buckets remained `N0`.

V0.12 records actual input-to-stable-landing duration for clean non-level jumps. Later plans blend the matching height-class duration with the analytical vertical solution. This correction is allowed during Spirit Boost because horizontal speed changes travel but does not change the vertical time required for the same hold and target height. Current horizontal velocity and boost deceleration are still integrated separately after the corrected flight duration is selected.

## V0.13 route-order and Spirit-Boost corrections

- `WideWallTrench` and `WallAcrossGap` are topology-owned actions. The planner must return a wall approach before enumerating any direct landing on the visible top. This makes classification operational instead of merely diagnostic.
- `AdjacentWall` and `NarrowPillarTrench` are passive trench-entry actions: send no input while entering the short gap, then authorize the first climb press only from physical wall contact. Passive entry is rejected when its predicted drop exceeds `1.25` units.
- `WideWallTrench` uses a speed-aware deep-entry hop. The planner jointly solves launch X and hold duration for a face clearance of `3.90..4.50` below the target top (preferred `4.20`). For the observed `targetTop=4`, this targets feet Y near `-0.20`, low enough to descend inside the gap but high enough for the player's body/raycast to overlap the wall that begins near Y `0`. Wall contact travel uses current constant VX because traces show bonus-stage VX does not decay during the sub-second approach; the ordinary landing integrator is not used for this contact problem. Wall pulses remain contact-confirmed.
- During Spirit Boost or any speed materially above the captured base speed, evaluate the immediate landing and scanner alternatives with current velocity. Prefer a wider verified launch/landing corridor when the nearest platform is incompatible with minimum jump travel.
- Speed adaptation may skip only ordinary ballistic landings. It may not skip an immediate lower route, trench, adjacent wall, or wall-across-gap action, and a farther candidate that introduces one of those topologies is rejected.
- Log target substitution as `SpeedAdaptiveRoute`; retain the chosen surface in the normal route/attempt diagnostics so predicted and actual support remain directly comparable.

## V0.14 native-domain and wall-contact evidence

Reference-source inspection establishes two useful boundaries:

- BonusStageCompleter gates its mutation on a selected map whose name contains `bonus` and `BonusMapController.showCurrentTime`.
- AutoJumpMod uses `GameState.IsBonus()`, exact `Maps.list.BonusStage*` map IDs, `showCurrentTime`, and `currentSectionIndex`. It performs no map or landing analysis; its common bonus action sends immediate `JumpPanel.OnPointerDown` and `OnPointerUp` pulses repeatedly.

AutoJumpMod's wall success therefore does not prove a hidden route algorithm. It proves that the game-native `PlayerMovement.IsWalled()` decision and native `JumpPanel` edge path are sufficient to authorize wall impulses. V0.14 uses that native result as physical-contact evidence. The forward ray still supplies face position/collider identity and predicts upcoming contact, but a ray miss while `IsWalled()` is true no longer blocks the wall action. The fallback contact is tagged `NativeIsWalled=True;RaycastUnavailable` in diagnostics.

Active planned control is gated by all of: `GameState.IsBonus()`, exact Bonus Stage 3 map identity (name fallback only during initialization), `showCurrentTime`, and `GameStateName == BonusMode`. `showCurrentTime` alone is not authoritative: the V0.14 trace retained it in failed `PreBonusMode` with a zero timer. Completed-section traversal remains a separately validated state and requires a completed sphere quota.

## V0.15 historical warning diagnosis

The earlier optional-mod discovery path used `AccessTools.TypeByName`, which could scan IL2CPP Unity proxy assemblies and emit large `ReflectionTypeLoadException` warnings. V0.16 removes that entire path. AutoBonusRunner no longer discovers, inspects, patches, configures, or suppresses AutoJumpMod; input exclusivity is user-managed.

## V0.16 lifecycle and wall-continuation correction

- All AutoJumpMod compatibility/discovery code is removed.
- `showCurrentTime` no longer authorizes control in failed or expired `PreBonusMode`; ordinary planning requires `BonusMode`.
- V0.16 experimented with computing passive ballistic rise after wall UP. That implementation was too willing to infer success from a free-flight formula and did not use the bounded observed-state contract now used by V0.28.

## V0.17 late-landing and attached-wall correction

- V0.17 removed `CoastOnExistingMomentum`. Its valid insight remains that a ballistic formula cannot replace real wall-contact evidence. Its stronger conclusion—continued contact below the lip should immediately authorize another press after one fixed step—is superseded by the V0.27 trace, which shows that behavior resetting VY `+17.253` near the lip and causing repeatable overshoot.
- A safe-to-safe launch intersection is preferred, but missing that conservative window is no longer permission to walk into a pit. `EmergencyLateRawLanding` fires only while the player is still on the verified source, the live current-position trajectory ends inside the target's raw collider interval, and the entire trajectory clears known hazards.
- `GetStableRoutePlan` is hidden from IL2CPP method injection because its managed `IReadOnlyList<Vector2>` parameter is not an IL2CPP-compatible public surface.
- AutoBonusRunner mirrors its own messages into a non-rotating session file under `UserData/AutoBonusRunner/Logs`; this is independent of MelonLoader's ten-file log rotation.

## V0.18 dynamic wall pulse train

- The fixed two-press invariant is removed. The retained V0.17 session contains a clean manual three-pulse climb with holds `0.132, 0.132, 0.090 s`, so continuation count and weight are observed-state decisions.
- A pulse is legal only with intended-wall contact (or the existing one-frame kinematic overlap fallback), after a real UP/fixed-step boundary, below the current target lip, and below the six-pulse route cap.
- V0.18 solved narrow-wall pulse holds from current remaining rise over `0.075..0.135 s`. V0.28 retains that interval only after a new continuation has been proven necessary; it no longer interprets a small remaining rise plus contact as automatic permission to send the `0.075 s` minimum while VY is still positive.
- A static-map candidate may become a chained wall only if it is forward, `0.10..5.25` units beyond the old right edge, `0.30..7.50` units higher, and at least `1.25` units wide. On old-face detachment it becomes the active target and the runtime waits for physical contact before sending the next DOWN.
- `ManualWallPulseDown` and `ManualWallPulseUp` are independent event records. They do not require a full grounded manual-learning sample, so manual assistance after an automatic override remains measurable.

## V0.19 passive manual-demonstration recorder

When automation is disabled with U, observation remains active. The first physical left-mouse DOWN in a Bonus Stage begins a demonstration session. The recorder captures exact mouse edges and holds plus periodic trajectory frames, platform scan, static-map identity, wall contact, hazards, player bounds, native jump state, Spirit Boost, timer, sphere progress, apex, landing, pit descent, and lifecycle termination.

Each recorded frame may call the route planner in shadow mode. This is a pure comparison: it records `ShouldJumpNow`, maneuver, reason, hold, launch window, predicted flight, predicted landing, and candidate details at forced event frames. It must never call `JumpController.Press`, mutate the active route, or block physical input. Valid completed manual jumps continue to calibrate physics. Route and wall-action demonstrations are retained in logs for causal analysis before any policy is changed.

## V0.20 downstream wall-contact ownership

The next mapped surface can be both a landing target and a wall face. A safe top landing should remain legal, but touching its vertical face must restore wall-jump authority.

- If the next surface is at least `0.30` higher and within the chained gap limit, promote it immediately when the old lip is crossed.
- If the next surface is level within `0.35`, keep an `ExitTargetContactWatch` during flight. Do not press merely because the surface exists.
- On physical `IsTouching` contact whose face X belongs to the watched target, atomically promote it, retain the route-wide pulse count, return to `AwaitingWallContact`, and solve the next hold from the new height.
- If the runner lands safely, normal two-fixed-step landing confirmation wins. If the watch times out or passes the target, it expires without blind input.
- Required evidence is `WallExitContactWatchArmed`, `WallExitContactIntercepted`, `WallChainTargetPromoted`, followed by `WallRecoveryDecision` against the new target.

## V0.23 geometry-aware pit confirmation and tall-wall entry

World-space height is not a lifecycle state. Section 4 contains valid wall contacts below `Y=-5`, so `Y < -3.5` must only open a pit candidate. Automatic control confirms a pit after two distinct fixed-physics observations only when all of the following hold:

1. the player is airborne;
2. feet Y is below `-3.5`;
3. vertical velocity is at most `-8`;
4. no wall within `1.20` units matches the current `ApproachJumpThenWallJump`, `EnterTrenchThenWallJump`, or `WallJumpClimb` target.

During the confirmation interval, no new route is started, but the existing wall route is retained. A matching wall suppresses the pit transition and returns authority to the normal contact executor; V0.28's residual-rise gate still decides whether input is legal on that frame. Manual recording has no authoritative target, so any detected forward wall within `1.20` units keeps the demonstration alive. A held physical mouse input or upward velocity also prevents manual pit confirmation.

For `WallAcrossGap`, contact height depends on wall rise. A wall with rise below four units retains the upper-face solver (`0.12..1.65` units below the lip). A wall with rise of at least four units uses lower-face entry (`2.25..min(4.75, rise-0.55)` below the lip), then the action executor performs separated contact-confirmed wall presses. Returning `Wait` merely because the first jump cannot reach the upper lip is invalid for this topology.

`NarrowPillarTrench` requires topology evidence. A positive-rise target no wider than `2.35` across a gap no wider than `2.25` is a wall-contact maneuver when the source is already narrow. From a wide source, a nearby alternative must also be narrow, begin within `5.25` units after the target, and be at least `0.30` higher. This retains the first edge of a rising wall chain without treating Section 1's same-height floating platforms as climbable walls.

Implementation constraint: `HasRecoverableWallAhead` has managed `out BonusWallContact` and `out string` parameters. Because `AutoBonusRunnerRuntime` is registered into the IL2CPP domain, this helper must remain `[HideFromIl2Cpp]`. V0.21 omitted that attribute and failed inside `ClassInjector.ConvertMethodInfo`; V0.22 adds it without changing the routing policy.

## V0.27 causal corrections

The V0.26 baseline showed accurate input delivery but incorrect prediction and authority:

- holds `0.020`, `0.075`, and `0.090 s` were delivered as approximately `0.022`, `0.078`, and `0.091 s`;
- a `0.075 s` ordinary jump predicted `0.529 s / 6.251` but produced `0.683 s / 8.329`, landing `+2.078` farther than predicted;
- the next window was behind live X, but the planner returned a valid wait instead of live salvage;
- collision VX near `2.36` contaminated free-running speed near `11.9`;
- duplicate player callbacks distorted fixed-step/gravity observations;
- ground launch samples near `18.627` mixed with wall resets near `20`.

V0.27 addresses those causes as one coherent contract: primary-player patch ownership, ground/wall learning isolation, the same six hold buckets across all four height bands, duration cells written only by measured input-to-stable-landing timing, section-scoped route calibration, wall-entry speed latching with Spirit Boost increase handling, live missed-window all-hold salvage, Ground 3 surface-3 attached objective descent with immediate ownership release when both contact tests fail, Ground 5 chain preservation, exit-face contact fallback, and expected/delivered/actual failure-domain logging. Section 0-specific map and route policies are unchanged; shared correctness fixes still require regression testing there.

## V0.28 residual-rise and route-contract correction

The V0.27 automatic interval validates perception but rejects the immediate-repress scheduler:

- Ground 3 at origin `O=480` succeeded with a first pulse from feet Y `-1.226` to `1.727` and a second `0.076 s` pulse. Horizontal travel resumed near feet Y `3.909`; the mapped S3 face was physically intercepted at X `480.401`, feet Y `2.177`, VY `-22.214`. Attached descent armed at line 2033 and completed at feet Y `0.131` on line 2040.
- The later `O=528/576/624` repetitions issued an additional DOWN while the previous pulse still reported VY `+17.253`. Their reconstructed S3 face intersections were approximately Y `4.849`, `5.391`, and `4.846`, all above the finite face top Y `4`. This explains both top landings and the following trench overshoots without changing map identity.
- A release-state semi-implicit prediction from the successful flight (`x=473.814, feetY=4.234, VY=16.241`) gives S3 contact feet Y `2.181` and VY `-22.214`; the log observed `2.177/-22.214`. Perception and target X are therefore not the primary error domain.
- Ground 5 surface 4 was physically contacted at `(650.471,6.681)`, leaving `0.313` rise (line 3851). The fallback cleared its exit target, sent another `0.075 s` pulse (lines 3856/3862), landed at X `662.101` beyond `[655,662] @ -2` (line 3966), and died (line 3974).

V0.28 changes six contracts:

1. `WallResidualRiseWait` starts whenever a contact-qualified continuation is considered while VY remains above `+0.25` and release height has not yet been reached. It keeps UP released, logs observed rise/apex state, completes at release height or apex/descent, and expires after `0.45 s`. It uses the ballistic apex calculation only as diagnostic evidence; the actual observed velocity boundary owns the decision.
2. Ground 3 surface 2 -> surface 3 becomes `MandatoryFaceContact` while lower-lane spheres remain. The runtime blocks ordinary landing/exit solvers and runs `SetupPulse -> observed apex/descent -> FaceInterceptPulse`. Setup height uses exact powered steps plus a conservative released-apex bound; the final pulse checks old-top clearance plus finite S3 face intersection, releases on height-gated horizontal resume or before `N+1`, and locks DOWN until target contact survives a later fixed step. A top landing remains `MandatoryWallFaceContactMissed`.
3. Static wall continuation searches may select the nearest lower authored support within the existing `MaximumStepDown` envelope. Ground 5 surface 4 therefore retains `[P+7,P+14] @ -2` as `Role=MappedDownstreamExit`; a face-contact fallback no longer leaves the high narrow pillar as a target with no exit.
4. Mandatory route identity requires Section 2, Ground 3 `S2 -> S3`, the same nonzero mapped instance, compatible origin, and the expected gap/top geometry. Pool registry generation is logged but is not an identity requirement. A transient live `Unknown#0/S-1` probe inherits the locked verified metadata; contradictory mapped identity atomically fails the route.
5. Preview candidates are validated before publication. An invalid Ground 3 S2 preview cannot set `wallExitTargetActive`, suppress the same-frame static-map capture, and fall through to a generic top-landing jump.
6. Setup, pre-watch intercept, contact watch, and attached descent use fixed-step deadlines. A late first contact cannot revive an expired plan. Confirmed-face handoff preserves objective bounds only when the live sphere scan actually fails, distinguishes a successful empty lane, and keeps the confirmation frame UP-only.

## Current unresolved problems

- V0.31 is not gameplay-tested. Its first validation must prove Section 0/1 did not regress before interpreting the new Section 2/3 behavior.
- Ground 6 S1 -> S6 must remain input-free until physical contact; both an early DOWN and an `InputNotAcceptedByGame` passive-route reset are failures.
- Section 3 Ground 7 S2 must validate the two-phase solver at normal `16.9` speed and during Spirit Boost. The trace must compare selected `LipVX`, predicted landing, actual support, and residual.
- A boost acquired after input DOWN is not knowable at planning time. Log it as a discontinuity/outcome residual and do not convert it into a permanent section cruise floor.
- Farther wall-exit candidates are statically ordered but do not yet carry a general intervening-solid occlusion proof. The known Section 3 corridor is the bounded use case.
- Completion Wind Dash stable-ground/ownership gating needs a post-quota runtime trace.
- Later sections remain less thoroughly demonstrated, and the large runtime still needs eventual subsystem separation.

## Deployment validity gate

A V0.31 gameplay trace is valid only when its startup block explicitly loads `AutoBonusRunner.dll` and reports public `1.0.0`, internal `V0.31`, schema `4`, followed by successful registration of `AutoBonusRunnerRuntime`. The build artifact, Mod Manager source DLL, authoritative root loader DLL `ModLoader/Mods/AutoBonusRunner.dll`, and nested compatibility mirror are synchronized at length `394240`, SHA-256 `A1D575CB397EB60B75C9A019C4FBEA0AF27E620BC1B43FA3F2F3AD8270C72EE1`. `app_config.cfg` does not disable AutoBonusRunner. Historical enabled logs load `./Mods/AutoBonusRunner.dll`, so the root loader copy is authoritative. Matching hashes prove deployment only; gameplay claims require a new V0.31 trace. `AutoBonusRunner-20260720-155045-894-V0.30.log` remains the causal regression baseline.

See `MAP_REFERENCE.md` for full static geometry and concrete logged examples.
