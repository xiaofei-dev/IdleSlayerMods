# AutoBonusRunner Routing Algorithm

## V1.00 minimum-objective route constraint

The generic ballistic planner accepts an optional minimum live-sphere
intersection count. This is a filter after the normal landing and hazard
proofs, not a score bonus: a candidate below the minimum cannot win, while an
unsafe candidate can never become legal merely because it intersects a
sphere.

The only V1.00 caller is ordinary Stage 1 code Section 2. It activates after
all of these live facts agree:

1. map identity is `map_bonus_stage_1`, Section `2`, without Spirit Boost;
2. current and destination roads are each approximately seven units wide,
   level, and separated by `12..14` units;
3. a level `1.5..2.5`-unit intermediate stone is present; and
4. five active `BonusSphere` coordinates contain a centre plus neighbours at
   left, right, above, and below by one unit.

The resulting plan must predict at least one sphere hit. If no proved
candidate satisfies it, the existing plan is retained and
`Stage1Section2CrossSphereMinimumUnavailable` records the failure; the
controller does not issue an unproved jump.

## V0.99 authored Stage-3 wall-contact continuity

Stage 3 retains authored topology at both planning and execution:

1. In code Section 2, a verified same-instance `Ground 6/S0 -> S3`
   transition with a narrow raised S3 top is a wall-entry contract.
   `EnterTrenchThenWallJump` overrides an optional top-landing or pickup
   result. No world coordinate participates in this decision.
2. When a non-wall automatic flight on authored Stage 3 closes, it may retain
   one immutable contact credential for at most eight native physics steps
   and `0.30 s`. The credential includes player, map, section, route, attempt,
   closure outcome, source, target, plan, and predicted landing identity.
3. A recovery requires current `IsDetected && IsTouching` native collision
   and a mapped top above the player's feet. The face must match the retained
   target or lie between the retained source and target as a mapped blocking
   surface. Stage-3 Section-2 `Ground 6/S3` is also independently authorized
   by its authored static role.
4. Existing mandatory-face, attached-objective, exit-watch, and active wall
   ownership always win. If an old DOWN is still held, recovery sends UP and
   waits through the existing fixed-step separation barrier before a new
   wall pulse.
5. This physical correction runs before pit confirmation. Failure to satisfy
   its strict identity and collision gates leaves the existing pit guard
   unchanged.
6. Starting any newer automatic jump or passive wall approach invalidates the
   old contact credential.

Completed Section-0 Spirit traversal no longer admits
`SectionOneSpiritResumeRecoverySelected`. That branch was entered only after
the full slow/fast envelope proved there was no common safe transfer, so its
upper-tier landing was explicitly best-effort. Until typed reward target
latch, lack of a full-envelope proof now leaves the normal bounded wall/top
continuation in control instead of authorizing that direct jump.

## V0.98 Stage-2 Section-1 authoritative wall chain

Stage 2 code Section 1 uses exact native wall collision as the final action
authority:

1. A touching face may capture any active automatic flight in this section.
   The prior predicted landing or wall target does not need to classify the
   face correctly.
2. If the approach input is still held, the face is latched and no second
   DOWN is sent until native UP has separated the inputs.
3. The dedicated controller waits for stable physical contact, then emits a
   fixed-step-aligned `0.08 s` first pulse and bounded `0.12 s` continuation
   pulses.
4. During airborne travel, unsettled contact, and intermediate step
   transitions, the dedicated controller retains ownership; generic wall
   recovery cannot consume retries for the same staircase.
5. A stable support under the player but still on the near side of the
   captured face is the wall foot, not route completion. A verified landing
   beyond the face closes the temporary chain and resumes ordinary live
   planning.
6. If the old automatic prediction has already closed, an exact stationary
   or recoverable sliding contact above the pit threshold can bootstrap the
   same bounded chain. No coordinate-only or ray-near-wall evidence can do so.

This policy is limited to `map_bonus_stage_2` code Section 1. Stage 1, the
other Stage-2 sections, reward behavior, route scoring, and pickup policy are
unchanged.

## V0.97 Stage-2 Section-0 rebound confirmation

Wall impulse confirmation normally requires either two upward physics
observations with at least `0.30` rise or an established compressed
fixed-step impulse. Stage 2 code Section 0 adds one narrowly scoped physical
success proof for a wall contact already near its lip:

1. the delivered pulse has fixed-step evidence;
2. at least one upward physics observation exists and total rise is at least
   `0.04`;
3. starting vertical velocity is below `-1`;
4. current vertical velocity is above `+2`; and
5. the pulse increased vertical velocity by at least `5`.

This recognizes a real downward-to-upward native rebound without declaring a
zero-velocity ground impact successful. The native Grounded flag is not part
of this proof because a physical wall contact can assert it during a genuine
upward pulse. A fixed-step pulse that satisfies neither the normal
nor near-lip proof is not rejected until two physics steps have completed
after release. Route selection, wall target ownership, hold calculation, and
the six-attempt bound are unchanged.

## V0.96 Stage-2 Section-1 contact correction

Two equivalent proofs can authorize an unmapped stepped-wall entry in Stage 2
code Section 1:

1. the observed source support is at most `6.25` units wide; or
2. the raw runway remaining from the live player centre to the source edge is
   within `[-0.20, 6.25]`.

The second form handles a retry that respawns on a longer continuous collider
immediately before the same wall. It does not remove the existing requirements
for a same-height ordinary gap, downstream width of at least `3.00`, no
intermediate support, a gap `2.00..30.00` units beyond maximum ballistic
travel, a clear hazard trajectory, and fixed-step-aligned maximum hold.
Zero-VX authorization uses the identical remaining-runway predicate.

A committed `Stage2LowCorridorWallCatch` has one closed-loop correction. If
the retained raised bounds do not produce contact but a farther native face is
exactly touching while the body is stationary, the physical collision becomes
authoritative even when the static registry is pending. The old sample is
closed as `Stage2LowCorridorPhysicalWallHandoff`, an `0.08 s`
fixed-step-aligned climb pulse starts immediately, and the established
six-pulse unmapped-wall chain owns later stationary contacts. This promotion
is legal only in Stage 2 code Section 1, only for the low-corridor maneuver,
and never while a mandatory face or active input hold owns the frame. The
first verified support cancels the temporary chain and returns control to the
normal planner.

## V0.95 constrained Stage-1 Section-1 boost ranking

In Spirit Stage 1 code Section 1 only, a typed boost-hit trajectory may cross
the generic `0.200` comfortable-margin tier when all of these conditions
hold:

1. the exact no-reset and reset trajectory envelope is safe;
2. the post-uncertainty landing margin is at least the normal
   `RouteLandingSafetyTier`;
3. the boost candidate guarantees at least as many spheres as the selected
   no-boost candidate; and
4. the candidate intersects at least one verified typed Spirit Boost trigger.

The rule is applied both while choosing the robust launch position for one
hold and while comparing hold durations. It changes objective ranking only;
support selection, collision checks, and live-position revalidation remain
unchanged.

## V0.94 diagnostic default

Detailed debug diagnostics remain available through `Debug Mode`, but newly
generated preferences default it to `false`. This configuration-only change
does not alter route selection, physics feedback, or action timing.

## V0.93 bounded exact-live recovery for speed envelopes

A boost-aware robust launch window may collapse to one world-space point.
When a planning tick crosses that point, a candidate is not automatically
missed. The planner re-runs the complete slow/no-pickup and
fast/boost-reset trajectories from the current body position. A late command
is authorized only if:

1. the candidate requires the speed envelope;
2. the player is still supported by the verified source;
3. the launch point was crossed by no more than
   `max(trigger tolerance, speed * fixedDeltaTime + 0.05)`; and
4. every envelope outcome clears faces, hazards, and intermediate surfaces
   and lands in the same verified safe target.

This is an execution correction, not an additional predicted travel offset.
The launch and landing calculations remain on the existing single
input-to-landing timeline. Ordinary single-speed late recovery retains its
separate uncertainty proof.

## V0.92 stable cold-start flight centre

Before three accepted level-flight samples exist, the planner uses `0.970` as
the global flight-time scale. This is the repeated stable centre from retained
Stage-1 traces, not a map coordinate or authored-route override. At three
samples the observed median replaces it normally.

The Stage-1 Spirit first-section regression must run through the low Spirit
Boost before committing the narrow intermediate-support jump, then use the
boost-aware farther landing and preserve the full following sphere-row
opportunity. The Section-1 regression remains unchanged: one `1.010` timing
sample cannot activate and must not remove the next narrow `+3` pillar route.

## V0.91 stable timing evidence before model activation

Clean level-flight timing observations are still collected immediately, but
the global `FlightTimeScale` remains at the stable cold-start centre until
three samples exist. A one- or two-fixed-step difference in
stable landing confirmation can therefore remain visible in diagnostics
without changing the active route. Once three samples exist, their median
becomes the section model exactly as before. Height/hold-specific duration
cells retain their existing independent three-sample calibration contract.

This rule is map- and mode-independent. In the retained Stage-1 Spirit
Section-1 regression, one `1.010` level sample must not make the following
`+3` narrow pillar unreachable, and must not remove a later safe Spirit Boost
pickup. The expected pre-stability physics log remains
`FlightScale=0.970`; after the third accepted level sample it may show the
three-sample median.

## V0.90 one input-to-landing timeline and pending-reset edge proof

`PredictHorizontalTravel` covers the complete input-to-landing interval; its
vertical duration already contains `InputDelaySeconds`. Stage 1 therefore
does not add another `speed * fixedDeltaTime` to landing travel and does not
expand the normal action tolerance by that amount. The separate Stage-2
Section-1 execution contract retains its established compensation.

If a future-speed jump physically touches the raw edge of its exact expected
boost support, the urgent fixed-step controller may reconstruct one pending
reset only when all of these live facts hold:

1. the committed prior plan expected at least one speed-boost trigger hit;
2. that plan declared a future speed transition;
3. the player body overlaps the exact expected support and is grounded on its
   top during the landing callback; and
4. typed Spirit kinematics report an active boost component.

The controller derives base speed from the current physics snapshot rather
than the boost-contaminated section cruise estimate. It then runs the existing
trajectory selector with a conservative immediate-reset envelope spanning the
observed contact speed and `base + maximum boost`. The selected action still
needs an exact-live landing, hazard, intermediate-surface, continuation, or
wall-contact proof; the cached preview remains diagnostic only. The expected
evidence is `SpiritEdgeResetEnvelope`, followed by a fresh
`SpiritTransientLandingWallContinuationSelected` or an explicit rejection.

The Stage-1 Section-1 regression fixture must commit before the raised
platform launch window instead of changing hold tiers until
`RaisedLandingHasNoSafeDirectJump`. The Stage-1 Section-3 fixture must return
to safe-centre boost-support landings; if it reaches only the raw edge, the
next action must be proved against the pending reset speed and must not issue
the former slow-only `UrgentNarrowLandingChainFixedStep` toward the lower
platform.

## V0.89 route-level boost utility and execution-state prediction

For Spirit sections 0 and 1, reachable-platform comparison now uses this
lexicographic order:

1. first-contact landing safety;
2. verified continuation;
3. the guaranteed soul-objective set;
4. verified speed-boost trigger hits;
5. route distance and geometry.

Thus a farther platform may be selected to collect a visible boost only when
its slow/reset envelope is safe and it preserves every guaranteed soul. The
boost cannot override survival, continuation, or soul coverage.

The live command proof treats DOWN and takeoff as two different states. It
integrates one native fixed step of horizontal travel before evaluating both
the no-reset and reset trajectories. The same compensation is used by the
late raised-face fallback, where vertical contact is simulated from the
effective takeoff X rather than the input X.

Spirit alternative enumeration first applies a conservative no-pickup reach
bound. A future reset may constrain an already reachable route but cannot make
an otherwise unreachable target legal. This avoids full slow/reset simulation
of distant Section 4 surfaces. Ground WAIT caching now ends one native step
before launch instead of more than two steps before it; DOWN still requires a
fresh live proof.

## V0.88 Spirit pickup utility

Landing safety remains the first constraint. In the first two sections, when
two Spirit-mode commands both retain `ComfortableSoulLandingMargin`, the
planner now prefers the command with more verified native speed-boost trigger
intersections, then more guaranteed soul intersections, then the existing
safety/geometry tie-breaks. Later-section ordering is unchanged.
Boost hits are taken from the already-required no-reset/reset speed envelope;
this adds no new trajectory search. Ordinary-mode candidate ordering and all
support topology are unchanged.

## V0.87 diagnostic transport

Routing decisions are unchanged. `BonusRunnerLog` now applies bounded exact
duplicate suppression before writing either MelonLoader output or the
independent trace: 10 seconds for Debug and 30 seconds for Warning/Error, with
512 level-and-category-aware keys. Dynamic route records retain their existing
state-change signatures and service-level rate limits.

Every Debug record has a stable category. Operational exceptions produce a
single-line Error and a full `[Debug][Exception]` record, so normal logs stay
readable without losing stack evidence needed to diagnose control failures.
The V0.86 requirement transition is now logged as
`[Debug][Gameplay] Sphere requirement selected` with camel-case fields.

## V0.86 native sphere-requirement policy

The configured mode is normalized case-insensitively to `Auto`, `Manual`, or
`Skip`; missing and unrecognized values safely become `Auto`. One Harmony
postfix owns the active section's native requirement:

1. reject calls without a live controller, active section, or exact Il2Cpp
   section-pointer match;
2. retain the native `GetRequiredSpheres()` result in `Manual`;
3. return `1` in `Skip`;
4. in `Auto`, retain the native result when
   `BonusMapController.spiritBoostEnabled` is true and otherwise return `1`;
5. log one `[Debug][Gameplay] Sphere requirement selected` record whenever
   controller, section, mode, Spirit state, native requirement, or effective
   requirement changes.

The postfix changes neither collected-sphere state nor terrain intent.
Downstream route logic reads the same effective value through
`BonusStageDetector`, and continues the normal terrain policy after quota
until typed reward-object confirmation.

## V0.85 fixed capability contract

The terrain controller, typed-latch reward actions, and eligible grounded
completion Wind Dash are always compiled on. They are no longer independent
preferences. The user's `Enabled On Startup` setting and `U` toggle still
control whether AutoBonusRunner owns automatic input.

This is a configuration-surface change, not a routing change. Automatic
jumping still requires a supported Bonus Stage and the existing gameplay,
retry, manual-cooldown, lifecycle, and player-validity gates. Reward actions
still require the same reward target on two frames. Wind Dash still requires
that latch plus the selected icon, unlock/readiness evidence, idle trajectory
ownership, and stable ground. V0.84 terrain selection and every jump/wall
constant remain unchanged.

## V0.84 shared terrain policy before reward confirmation

Quota completion is a lifecycle observation, not a route-selection mode.
While the completed section is inactive and no typed reward target is latched,
`GetRoutingState` retains the completed terrain section and the ordinary
ground controller continues. V0.84 applies these invariants:

1. live-geometry maps use the same `Live` reachable-route selector before and
   after quota;
2. authored Stage 3 uses the same `Authored` selector before and after quota;
3. the post-quota state alone cannot force `SpeedAdaptive` routing;
4. the verified section cruise speed is supplied as a transient-speed base
   only when the native Spirit flag is active, matching pre-quota behavior;
5. an ordinary completion scan with no valid uncollected boost trigger returns
   a disabled speed context and uses observed Rigidbody velocity; and
6. only a verified trigger may produce a future maximum-speed envelope or
   raise projected-support speed.

The reward controller still becomes authoritative atomically after the same
typed object is qualified on two frames. Physical wall contact, retry
lifecycle guards, stable-landing confirmation, and reward actions are not
weakened. The `CompletionTraversalNavigation` record remains diagnostic and
must now describe a plan produced by the shared terrain policy.

Pre-quota behavior is structurally isolated from this correction:
`state.IsActiveGameplay` does not satisfy the completion-normalization branch,
and no hold duration, topology rule, soul score, or map constant changes.

## V0.83 Stage-2 Section-1 unified route and correction model

V0.83 isolates the second displayed Stage-2 section as one topology-relative
route domain. It does not replay the known 72-unit module period. Every scan is
classified from current/next widths, gap, height delta, the verified
face-less-raised/low pair, and downstream support visibility:

- `EntryChain`: broad road to the one-unit/three-unit touch sequence;
- `NarrowHandoff`: one-unit source to a wider verified successor;
- `LowCorridorWallCatch`: raised collider plus attached low continuation;
- `SteppedWallTraverse`: direct landing is beyond maximum ballistic travel;
- `RisingStair`: near-zero-gap upward contact chain;
- `HighRoadDescent`: safe descent from the upper road; or
- `FreeLanding`: ordinary landing-first optimization.

The domain does not prescribe a hold. It constrains route ownership: a proved
`Stage2UnmappedWallIntercept`, face contact, passive wall entry, or wall climb
cannot be replaced by a farther top merely because that top has a larger
geometric score. Direct landings continue to use the shared landing-first,
objective-aware optimizer.

### Pre-movement FixedUpdate commitment

`JumpPanel.OnPointerDown` is issued in a pre-movement physics callback. The
native controller advances the runner once before the first upward frame, so
input X and takeoff X are different physical states. For Stage-2 Section 1:

1. compute one-step travel from live VX, fixed delta, base speed, and native
   boost deceleration;
2. arm when `currentX + oneStepTravel` reaches the proved launch window;
3. add that same travel to the committed min/expected/max landing envelope;
4. rerun safe-target, raised-face, intermediate, and hazard checks from the
   live input state; and
5. record the correction as
   `Stage2Section1FixedStepCommitTravel` and the expanded *early* trigger
   tolerance.

The late edge is not expanded. A body that has already missed the route still
uses the existing bounded late-recovery rules. Passive wall ownership is also
armed one FixedUpdate early because it sends no input before exact contact.

### Native completion-speed envelope

Successful post-quota traversal can reset the native boost component even
when the reward's Spirit flag is false. The runtime therefore captures typed
boost kinematics whenever either Spirit mode or successful completion terrain
traversal is active. During normal completion:

- unreadable native fields fall back to observed-speed routing;
- a visible uncollected trigger uses the ordinary location-aware envelope;
- no visible trigger produces
  `CompletionImplicitBoostResetEnvelope`, comparing current speed against
  base plus maximum native boost; and
- projected-support planning uses the fast endpoint when that implicit reset
  is possible.

If the slow/fast envelope rejects the one-unit entry landing, the verified
raised collider becomes the controlled contact target. The low-corridor
contract accepts either the intermediate support or the five-unit module-entry
road (`source <= 6.25`, raised-face distance `0.75..4.50`) only after all
direct safe landings fail.

### Dynamic wall correction

During completion flight, exact descending wall contact is authoritative over
a stale predicted landing. Static-map wall identity remains preferred. If the
registry is pending, the runtime may use the prepared projected successor only
when its left face matches measured contact within `0.35`, width is at least
`0.75`, and its top lies `0.35..12` above the live feet. It then closes the
old attempt and transfers to the existing separated contact-confirmed wall
executor. No wall pulse is authorized by visual proximity alone.

Pooled sphere refresh now also requires six units of forward progress;
pooled Spirit bindings use a `0.75s`/eight-unit exhaustion refresh. These
spatial gates prevent stationary scene-wide rebuild loops.

## V0.82 pooled objectives and low-corridor contact transfer

### Pooled sphere inventory

The typed sphere cache is section-scoped, but the game activates pooled sphere
rows over time. A cache generation is refreshed only when:

1. the route still requests sphere objectives;
2. no active cached sphere lies in the requested range;
3. no active cached sphere lies anywhere ahead of the range's left edge; and
4. at least `0.60s` has elapsed since the last exhaustion refresh.

An active cached sphere beyond the current right horizon proves that the
generation remains usable and prevents a refresh. The refresh rebuilds the
typed sphere/Spirit inventory once, then logs the before/after forward counts.
Ordinary fixed/render planning consume the same refreshed array. This makes
future pooled rows visible without an unconditional scene query in either
frame loop.

### Stage-2 verified-low-corridor contact route

`SelectStage2VerifiedLowCorridor` continues to exclude the face-less raised top
from ordinary landing clearance, but retains its measured bounds in the scan's
inactive intermediate slot. After the ordinary low-landing search fails, a
`Stage2LowCorridorWallCatch` is legal only when:

1. Stage-2 live routing selected the verified-low-corridor topology;
2. the source is at most `4.25` wide and the low successor is at least `3.00`
   wide;
3. the raised collider begins `0.75..3.25` units beyond the source, rises at
   least `5.35`, and ends flush with the same-height low continuation;
4. the player's body still physically fits on the raw source;
5. a maximum fixed-step-aligned jump to the raised face clears the live
   hazard path; and
6. every direct safe low landing has already failed.

The predicted endpoint is the player-centre contact X at the measured raised
left face, not a fabricated landing on the low continuation. The runtime stores
the raised segment as the automatic target and uses
`ApproachJumpThenWallJump`, so only exact physical contact can authorize the
existing bounded climb presses. This closes the former
`NoVerifiedLaunchWindow -> DetectedWallHasNoPlannedWallRoute` gap without
changing other map profiles or adding fixed world coordinates.

### Stage-2 narrow-source passive face transfer

Completion acceleration can make a nearby same-height successor unreachable
by every native jump: the minimum fixed-step pulse overshoots it, while no
input drops into its left face. After direct jump failure, Stage-2 may retain
that face as `Stage2NarrowSourceWallDrop` only when the source is at most
`2.25` wide, the successor is at least `2.50` wide, the gap is `1.50..6.50`,
their tops differ by no more than `0.35`, there is no represented intermediate
surface, and the passive-drop solver proves contact above its maximum depth
with no blocking hazard.

If the prior flight produces only one raw edge-contact physics step, the urgent
handoff finishes that landing sample and arms passive wall ownership in the
same pre-movement callback. It sends no DOWN. Exact wall contact remains the
sole authorization for the climb press. This turns a speed-discontinuity
overshoot into closed-loop recovery without relaxing direct landing safety.

## V0.81 stationary retry handoff

An acknowledged native retry may respawn Bonus Stage 2 on a narrow support with
Rigidbody VX equal to zero. That zero is a lifecycle spawn state, not sufficient
evidence for either a blind jump or a permanent wait.

The fixed-step controller may substitute the retained free-run speed for one
planning pass only when the current map uses Stage-2 live routing. Execution is
then authorized only if the rebuilt action is the bounded
`Stage2UnmappedWallIntercept`, its source/downstream width contracts still
match, and the observed gap remains at least two units beyond the planned
ballistic entry travel. The committed attempt records actual trigger VX `0`
and the reliable planning speed separately. Once the jump is accepted, the
existing physical wall-contact and stalled-step pulse controller owns the
climb. Every other zero-VX route remains blocked.

## V0.80 narrow-source fixed-step handoff

A landing on a narrow support is a control deadline even when its successor is
wide. V0.79 incorrectly treated urgent ground chaining as a narrow-to-narrow
topology and therefore rejected the common Stage-2 sequence
`one-unit support -> three-unit platform`.

V0.80 applies the following general rule after the normal safe launch-window
intersection is exhausted:

1. the current support width must be at most `2.25`;
2. the player centre/body must still belong to that raw support;
3. the live launch plus one native hold bucket must land inside the next
   verified safe interval with at most the existing `0.14` edge-recovery
   tolerance;
4. the full leading-face and hazard checks must pass;
5. the fixed-step urgent executor issues the continuation before ordinary
   stable-landing confirmation consumes two physics steps.

The successor has no narrow-width requirement. Making a verified destination
wider increases physical safety and cannot invalidate the handoff. This change
does not bypass target scanning, does not create a blind jump, and does not
alter wall-route ownership. If the native landing step has already moved the
centre beyond the ordinary `0.16` trigger tolerance, the same raw-support,
safe-target, face, and hazard proof may emit `NarrowSourceLateRecovery`; this
exception is unavailable on a current support wider than `2.25`.

## Objective

AutoBonusRunner must choose and execute jump actions from live game state. It must not replay a fixed timeline. Each decision must account for current X/Y position, velocity, section, Spirit Boost, live platform geometry, hazards, learned jump physics, wall contact, and the result of the previous action.

Normal navigation controls jump press/release; press duration changes jump height. Direct bow shots and a selected grounded Wind Dash are permitted only after the runtime confirms an active, interactable reward box, coin, or gem on two consecutive render frames. Horizontal motion is supplied by the game and can continue while the game is in the background.

## Current revision status

The current source target is public `1.0.0`, internal `V0.87`, configuration schema `34`. V0.31-V0.49 established phase-aware speed, feedback, wall recovery, reward handling, retry semantics, and completion traversal. V0.50 enabled isolated live-geometry profiles for Bonus Stage 1/2 while retaining Stage 3 authored geometry and hard topology. V0.51 added ordered downstream successors. V0.52-V0.56 constrained Spirit speed and recoverable-face modeling to verified live evidence. V0.57-V0.60 established universal body-bridged seams and passive-soul-safe action arbitration. V0.61 added same-fixed-step continuation from an exact prepared-support edge contact. V0.63 replaced V0.62's failed pickup-accelerated bypass with one shared contact-aware route model. V0.64 makes that model landing-first and receding-horizon for normal and Spirit Stage-3 ordinary landings while retaining authored Ground 3/5/6/7 maneuver ownership. V0.65 corrects same-support soul utility without changing route topology. V0.66 normalizes typed Spirit speed fields into the Rigidbody world-unit system before the shared route proof. V0.67 extended ordinary Sections 0-2 soul utility from hold selection to launch-position selection. V0.68 bounded ordinary spatial search. V0.69 bounded Spirit launch envelopes. V0.70 added an invariant launch-window WAIT cache and removed same-step Spirit duplication. V0.71 bounds live geometry/object-discovery cost and models a boosted transient-top touch as a continuation to a visible wall. V0.72 caches proven grounded WAIT intervals and collider bindings without changing route scoring. V0.73 restores complete slow-proof diagnostic records. V0.74 removes redundant trace construction while preserving the complete V0.64 safety contract. V0.75 restores full MelonLoader debug mirroring. V0.76 makes the independent trace live-readable and aligns early Spirit same-support collection with the existing comfortable-safety utility contract. V0.77 replaces the passive zero-input ownership of Bonus Stage 2 Spirit rising stairs with a proved proactive wall-contact jump. V0.78 recognizes Stage-2 face-less raised pieces as overhead geometry and routes to the verified low continuation instead of returning a fatal zero-input wall-plan failure. V0.79 adds a closed-loop Stage-2 wall-intercept and stepped-climb action when the downstream support is visible but mathematically beyond direct ballistic reach. V0.80 extends the already-proved same-fixed-step narrow-source handoff to wider verified successor platforms. V0.81 lets the exact same bounded wall entry start from a native auto-retry zero-VX spawn using separately retained free-run speed. V0.82 refreshes exhausted pooled-sphere inventories and transfers an otherwise unreachable verified low-corridor route to measured physical wall contact. V0.83 unifies the Stage-2 Section-1 route domain, models pre-takeoff FixedUpdate travel, treats post-quota native boost as a real speed envelope, and allows exact contact to recover through a verified projected wall when static mapping is pending. V0.84 restores the pre-quota terrain selector until typed reward confirmation and requires positive trigger evidence for future completion acceleration. V0.85 removes three always-on capability preferences without changing route behavior. V0.86 adds the native sphere-requirement mode without changing terrain selection. V0.87 aligns diagnostic transport with the repository logging standard. Objective ranking remains subordinate to landing safety.

## V0.79 unmapped stepped-wall recovery

This recovery is evaluated only after the ordinary landing search has returned
no valid hold. Its topology gates are: Stage-2 live routing, an ordinary
same-height gap, a source no wider than `6.25`, a downstream support at least
`3.0` wide, no represented intermediate support, and a landing whose safe
left edge remains at least `2.0` beyond maximum native ballistic travel. The
entry action uses the maximum fixed-step-aligned hold and retains the real
downstream support as its terminal identity.

The runtime then uses feedback rather than a fabricated wall top:

1. The first climb pulse requires `Grounded`, `|VX| <= 0.50`,
   `|VY| <= 2.50`, two stationary physics steps, an invalid support scan, and
   exact native/raycast wall contact.
2. A later stepped pulse may replace wall-ray evidence only after a previous
   accepted pulse and at least `0.35` units of forward progress. It retains
   all other stationary, support, target, and fixed-step gates.
3. The first pulse is `0.08s`; later pulses are `0.12s`. Both are quantized to
   the current native fixed step and limited to six presses.
4. Before every pulse, the prior automatic learning sample is closed. This
   prevents a failed landing prediction from retaining input ownership.
5. A verified downstream landing resolves the chain. Any other verified
   support returns control to the ordinary receding-horizon planner.

No map coordinate, screenshot coordinate, or static wall height is used.

## V0.78 Stage 2 verified low-corridor contract

1. The profile is active only for `map_bonus_stage_2`, in both ordinary and Spirit modes. Other maps retain their prior topology.
2. A raised immediate surface is an overhead candidate only when its top is at least `5.35` units above the current support and the scanner's five runner-height side probes leave `WallFaceX` unverified.
3. A low continuation must begin within `0.20` units before to `0.35` units after the raised surface's right edge, remain within `0.35` vertical units of the source, have at least `0.75` raw width, and be no farther than `7.00` units from the source edge.
4. When all conditions hold, the low continuation replaces the false wall as the immediate target. The raised top is not treated as an intermediate vertical face because the runner-height raycasts already proved that corridor clear.
5. The standard landing-first solver remains authoritative for hold duration, live and Spirit speed envelopes, hazards, target corridor, continuation, and sphere coverage. Failure to prove that lower landing still produces no input; this normalization does not authorize a blind pit jump.
6. Evidence scans include the measured physical face as `WF<world X>` or `WFNone`, and selection evidence records `Stage2VerifiedLowCorridorSelected` with both observed surfaces.

Required validation is startup `Internal=V0.78`, schema `26`. Before the repeated first-section pits, the log must show `Stage2VerifiedLowCorridorSelected`, select `[141,142] @ -2` and `[153,163] @ -2` or their translated equivalents, and issue an automatic nonzero DOWN. The previous `WideWallTrenchHasNoSafeEntryTrajectory` / `WallAcrossGapHasNoExecutableWallRoute` followed by `AttemptId=0` pit descent must not recur.

## V0.77 Stage 2 Spirit rising-stair contract

1. The profile is active only for `map_bonus_stage_2` with typed Spirit mode enabled. Bonus Stage 1, ordinary Stage 2, and Stage 3 do not enter this branch.
2. The immediate live obstacle must still classify as `AdjacentWall`, have a measured gap no larger than `0.12`, and rise by at least `1.50`. No world X coordinate or recorded module index is used.
3. Instead of returning the passive `WallDropApproach` immediately, the planner solves `ApproachJumpThenWallJump` against the physical face using live horizontal speed, jump physics, the Spirit slow/fast envelope, the vertical face band, intermediate surfaces, and hazards.
4. A planned wait is permitted only while a future verified launch window remains ahead. At that window the fixed-step proof must produce a nonzero hold and establish wall contact; the existing wall-contact controller owns subsequent separated climb presses.
5. If the proactive proof is invalid, the prior passive route remains available. The log records `ProactiveApproachRejected` and the complete fallback evidence so failure can be attributed to classification, face reach, launch window, hazard, or speed envelope.

Required validation is startup `Internal=V0.77`, schema `25`; Bonus Stage 2 Spirit should emit `Stage2SpiritRisingStair` or `RouteProfile=Stage2SpiritProactiveAdjacentWall`, followed by an automatic DOWN record before the rising face. User mouse input must be excluded from automatic success evidence.

## V0.76 early Spirit collection contract

1. The change applies only when typed Spirit mode is active in code Section 0 or 1. Ordinary mode and code Sections 2 and 3 keep the previous candidate order.
2. Unsafe candidates remain rejected. A candidate below `ComfortableSoulLandingMargin` cannot beat one at or above that reserve merely by collecting more spheres.
3. When both same-support trajectories retain the complete comfortable reserve, the larger guaranteed intersection set wins before unused additional centre margin. Equal-hit candidates retain the prior `RouteLandingSafetyTier` and score order.
4. Platform topology, target support, slow/fast speed envelopes, hazard clearance, continuation proof, hold candidates, and final live-position verification are unchanged.
5. Every independent trace record is written before its MelonLoader mirror and is immediately visible through the managed writer. Both destinations retain the same complete Debug stream.

Required validation is startup `Internal=V0.76`, schema `24`; Spirit Sections 0 and 1 should show `ComfortableCoveragePriority=True` and select the highest guaranteed hit count among comfortable same-support candidates. Sections 2 and 3 must show the prior route behavior, and the fourth-section transient-top continuation must remain successful.

## V0.74 equivalent-proof cost contract

1. Landing-first safety, slow/fast agreement, one-step continuation, native hold candidates, objective ranking, and final live-X proof are unchanged.
2. Before a same-support candidate constructs Spirit traces, an expanded no-reset broad phase checks horizontal and vertical body/sphere reach without allocation. Only an impossible guaranteed intersection is rejected; every possible candidate proceeds to the unchanged exact slow/fast proof.
3. A typed boost trigger can alter a flight only when its X bounds overlap the complete swept player-body interval. With no such overlap and no unknown-trigger fallback, the pickup and no-pickup traces are identical and share one trace object.
4. A stable base-speed flight with no possible reset uses its exact start/end travel points. A dynamic decay/reset trace retains every native physics boundary plus the exact duration endpoint; half-fixed-step points never corresponded to an input or collision integration boundary.
5. Route objectives use the cached, current-section inventory through a 512-unit horizon. Individual planning routines retain their existing source, target and trajectory filters. The cache signature therefore changes on actual pickup/behind-state changes, not merely because a static sphere crosses a moving 30-unit right boundary.
6. A cached WAIT may survive a temporary no-target scan or a farther substitute while still before its final-proof X. A newly visible target earlier than the cached target invalidates it. The cache still cannot send input.

Required validation is startup `Internal=V0.74`, schema `22`; Section-0/1 `GroundPlanningPhaseCost` should keep `RouteMs` below one native `20 ms` frame in ordinary and Spirit operation, with no repeated `90-110 ms` full proofs as a fixed soul row enters the horizon. V0.71's fourth-section continuation must remain successful.

## V0.72 performance-only WAIT contract

1. The first live fixed-step proof is unchanged. The same platform scan, objectives, speed envelope, holds, launch samples, route ranking, and final action are evaluated.
2. A valid future route may be cached only while it says WAIT. It expires before the existing final-proof distance, so the DOWN decision is still recomputed from live X/VX.
3. A negative `NextBoardUnavailable` or `ContinuousSurface` result may be cached only when the platform scan has proved `HasNext=false`. It expires before the verified current-support right edge and cannot hide an unsampled edge.
4. Map/section, physics revision, VX, sphere progress and objective signature, trigger signature, hazard signature, source support, and target availability/geometry must remain identical. Spirit WAIT additionally requires the typed stable base-speed state. Any mismatch deletes the cache.
5. The cache is shared with ordinary mode because the expensive same-surface sphere search is shared. It returns only the prior WAIT result and never dispatches input.
6. Spike and Spirit-trigger child colliders are bound once per section. Component active/enabled state, collider active/enabled state and bounds, and native pickup state are still read live.
7. `GroundPlanningPhaseCost` is throttled to one record per 0.75 seconds and splits the measured time into platform, hazard, physics, objectives, Spirit context, and route phases. `SpiritGroundPlanningCost` no longer writes every slow physics step.

Required validation is startup `Internal=V0.72`, schema `20`; one initial full proof may be visible, followed by `GroundWaitPlanCacheArmed` and cheap cache hits; long no-next supports must not repeat 60-130 ms route proofs every physics step; ordinary and Spirit route decisions must match V0.71; the verified Section-3 transient-top continuation must remain successful.

## V0.71 geometry-cost and transient-landing contract

1. `FindBoundary` uses `0.60`-unit coarse probes and seven bisection steps after the first mismatch. A gap wider than `0.60` cannot lie entirely between consecutive probes. Smaller missed seams are narrower than the measured player body and remain more conservatively walkable than the existing body-width rule.
2. A no-next live scan may cache only the already sampled current support when its verified safe horizon is at least ten units ahead. The cached result is always `HasNext=false`; it cannot authorize DOWN, create a target, or project geometry beyond the prior raycasts. Feet height, inferred body width, direction, and forward progress must remain compatible.
3. The clearance cache expires before `SafeRight` by `clamp(VX * 0.40, 6, 12)`. The full live scan therefore resumes before an edge enters the action horizon. Any discovered successor, section/profile reset, stage lifecycle reset, support-height change, or body-width change invalidates it.
4. Sphere, Spirit-trigger, and spike inventories use typed component arrays scoped to the current controller section. Active state, current section ownership, and native `PickedUp()` remain live checks on every use. Stage entry/exit and section transitions clear the arrays.
5. A Spirit support is transient when its safe corridor is no wider than three projected boosted fixed-step travels and either a pending typed boost trigger overlaps that support or the live body has the corresponding active boost component. In code Section 3 only, a visible downstream face at least `5.35` above the support may then own its continuation.
6. The original jump onto the transient boost support is not replaced. `PrepareSecondStagePreview` projects the deterministic reset speed and solves `ApproachJumpThenWallJump` before the first jump lands. The physical low face is obtained from horizontal collision probes; a top overhang must not move the planned face to `Top.Left`.
7. On the one grounded contact step, the urgent fixed-step planner rebuilds the wall proof with live boosted X/VX and sends the second DOWN immediately. Pending-pickup preview requires a typed trigger intersection; active-boost execution requires the observed native boost component. Other intervening supports must clear the trace. A lower support terminating at the same wall is a permitted contact apron because landing there preserves pre-armed wall ownership.
8. Ordinary mode, missing pending/active boost evidence, a stable-width source, no visible tall face, an unsafe intermediate collision, or an unproved wall envelope all retain the existing immediate route. No world X coordinate is a trigger.

Required validation is startup `Internal=V0.71`, schema `19`; `ForwardClearanceCacheArmed` followed by cheap `ForwardClearanceCacheHit`; no repeated 100 ms `NextBoardUnavailable` proofs along the cached span; no section-start scene-wide inventory loop; ordinary decisions unchanged; and in Spirit code Section 3, a prepared `SpiritTransientLandingWallContinuationSelected`, one-frame `UrgentNarrowLandingFixedStepChained`, then confirmed wall climb.

## V0.70 Spirit planning-cadence contract

1. `PlayerMovement.FixedUpdate` remains the only callback allowed to authorize a Spirit DOWN. Once it has consumed a grounded Spirit snapshot, the following `LateUpdate` records that fixed-step sequence as already planned and cannot run the same route proof again. Ordinary mode retains its previous scheduling.
2. A cache entry is legal only for `IsValid && !ShouldJumpNow && Reason=ApproachingLaunchWindow`, a stable top-landing maneuver, typed Spirit kinematics, and a current boost component no greater than `0.15` while live VX is within `0.25` of the typed base speed. Accelerating or decaying boosted motion is never cached.
3. Cache identity includes map, code section, model revision, live VX within `0.15`, sphere progress, a centi-unit signature of all routed objectives, typed trigger scan state plus trigger IDs/bounds, hazard ID/bounds, source right edge/top/collider, and continued visibility of the selected target. Any mismatch deletes the entry.
4. The cached object is a WAIT proof, not an input command. It is invalidated when live X reaches `PlannedLaunchX - max(0.45, VX * fixedDelta * 2.25)`. Every final approach and every DOWN therefore recomputes the slow/no-pickup and fast/pickup trajectories at actual X.
5. All existing route/lifecycle ownership resets call `ClearRoutePlanLock`, which now clears the WAIT cache. An airborne action, wall route, retry, death, completion transition, automation toggle, section change, pickup inventory change, or speed-state change cannot inherit it.
6. `SpiritGroundPlanningCost` reports total fixed-step planning time and whether the result was `FullProof` or `CachedWait`. `SpiritWaitPlanCacheArmed` states the fixed step, launch distance, hold, expected souls, physics revision and objective count. The first hit is logged; later cheap hits are suppressed unless unexpectedly slow.
7. Within `SpiritLaunch3`, landing safety and guaranteed pickup count retain priority. If both candidates are comfortable and guarantee the same positive count, the smaller launch X wins. A marginal candidate or a candidate with fewer guaranteed hits cannot invoke this tie-break.

Required validation is startup `Internal=V0.70`, schema `18`; a full Spirit proof followed by `SpiritWaitPlanCacheArmed` and sub-frame `CachedWait` costs; no repeated `FullProof` at unchanged X-to-launch geometry; no same-fixed-step duplicate route evaluation; ordinary behavior unchanged; improved Section-0/1 actual pickups; and eventual Section-3 validation of the V0.69 stable-drop/wall correction.

## V0.69 Spirit three-trajectory and physical-correction contract

1. Spirit fallback launch recovery evaluates at most three unique X positions per hold: preferred no-pickup X, centre of the common slow/fast interval, and earliest X retaining the comfortable landing reserve. There is no width-derived spatial loop. Every candidate must independently pass both speed-endpoint target-face, intermediate-solid, hazard, and post-uncertainty landing proofs.
2. `EvaluateSpiritBoostTrajectoryEnvelope` owns one no-pickup trace and one pickup-reset trace. Guaranteed soul value is the intersection of objective identities hit by those same traces; route ranking must not rebuild them merely to count pickups. Safety tier remains lexicographically above souls. Among comfortable candidates, more guaranteed identities may choose the earlier launch; unsafe or marginal collection never replaces a comfortable landing.
3. A Spirit `IntentionalDrop` succeeds only on stable top support. `RecoverableLeftFaceCatch` remains available to ordinary routing but is not a valid Spirit landing because speed aging can move the real contact outside the predicted face overlap before wall ownership exists.
4. The only post-prediction exception is observed physics in live-geometry code Section 3. While already below the pit threshold and descending, an exact touching face—or a mapped face within `0.35` while horizontal velocity is at most `0.75`—may transfer directly into the bounded wall sequence. The mapped top must be at least `0.20` above the feet. A ray-visible but uncontacted wall, another section, ordinary mode, or authored Stage 3 cannot manufacture this recovery.
5. The correction runs before pit confirmation and clears only the pending pit counter after ownership transfers. If an earlier jump hold is still DOWN, it is released for a complete fixed-step separation before a new wall pulse. Evidence records prior route/attempt/plan, live position and velocity, wall distance/touch state, mapped target, separation step, and whether the first wall pulse was immediate.

Required validation is startup `Internal=V0.69`, schema `17`, no recurring `RecoveredCommandX`, bounded `SpiritLaunch3` evidence with `Evaluated<=3`, improved Spirit low-percentile frame rate, soul attempt expectations that use `SoulHits`, no Spirit `IntentionalDrop` accepted only as `RecoverableLeftFaceCatch`, and either a stable Section-3 top landing or `SpiritPitWallContactRecovered` followed by wall input rather than `PitDescentDetected`. Ordinary trace decisions should remain V0.68-equivalent.

## V0.68 bounded-cost execution

The ordinary Section 0-2 soul adjustment now derives exactly two alternative launches per hold. From the baseline landing envelope it recovers modeled uncertainty, then solves the earliest X whose worst landing endpoint retains the `0.20` comfortable margin; the second alternative is the midpoint from that X to the baseline. Including the baseline, at most three trajectories are evaluated. Both alternatives are clamped to the existing usable launch interval and live player position, and repeat target-face, hazard, intermediate-surface, and landing-first checks. An alternative may replace the incumbent only when both are comfortable and it improves guaranteed pickup coverage, or preserves positive coverage while launching earlier. There is no spatial sampling loop.

LateUpdate may perform grounded route planning at most once per native fixed-step sequence because Rigidbody geometry and velocity do not change between those render frames. This gate does not throttle wall ownership, airborne monitoring, lifecycle handling, or the pre-movement fixed-step controller. `PlayerMovement.FixedUpdate` still scans, optimizes, and commits from the current physics state at native cadence. Required launch evidence is `SoulLaunchAnalytic`; V0.67 `SoulLaunchSearch` records describe only the superseded implementation.

## V0.67 bounded launch-position soul utility

For ordinary code Sections 0-2, each fixed-step hold now has a second bounded optimization dimension: launch position inside the already-derived source/target safe-window intersection. The search is disabled when there are no active objectives, in code Section 3, and whenever Spirit mode is enabled. It samples no more than twelve positions at approximately `0.20` world-unit spacing and ignores positions already behind the live player.

Every launch sample repeats the raised/level target-face check, hazard trajectory, all intermediate-surface clearances, and post-uncertainty first-landing margin. Selection remains lexicographic: safe beats unsafe; comfortable (`margin >= 0.20`) beats marginal; two comfortable launches compare guaranteed pickup count; equal nonzero coverage prefers the earlier launch so unrecoverable left-side objectives are consumed before forward motion passes them. A zero-pickup command receives no early-launch preference. The chosen hold is still compared by the existing V0.65 landing-first soul rule, and platform/continuation selection is unchanged.

Required evidence is `SoulLaunchSearch` embedded in each evaluated hold and the final `JumpAttemptPlan` showing a selected `PlannedLaunch` within its `LaunchWindow`. Section 3 must retain its existing launch decisions. Spirit launch selection remains owned by the V0.66 slow/fast trigger envelope and is not changed by this rule.

## V0.66 typed Spirit speed unit contract

All horizontal trajectory inputs use Rigidbody world units per second. Typed `PlayerMovement` speed fields must therefore be normalized before they enter `SpiritBoostRouteContext`; raw game-native values may never be compared directly with trajectory limits or Rigidbody velocity. Raw current speed above `80`, maximum boost above `80`, or decay above `120` selects the verified `50:1` scale. A finite same-frame ratio `abs(currentSpeed) / abs(Rigidbody2D.velocity.x)` may refine it only within `45..55`; this rejects the asynchronous map-start fixture `currentSpeed=200`, Rigidbody `VX=9.5`. Otherwise the scale is `1` for compatibility with an already-world-scaled build.

After conversion, an inactive negative boost sentinel is clamped to zero. Maximum boost, decay, current speed, and the current component share the selected scale. `preStepSpeed` is divided only when its magnitude is itself above the native-range threshold because the V0.65 fixture contains `preStepSpeed=20.178` beside native `currentSpeed=470`. Kinematics remain fail-closed only after normalized values fail finite/physical checks; the known `-5/750/250/470` fixture must normalize to `0/15/5/9.4` and remain available. Learning and transition detection must call the same normalized component reader as route capture.

This contract changes only input units. The slow/fast envelope, landing-first ranking, continuation proof, trigger scan, collision checks, soul utility, and command execution are unchanged. Required evidence includes both normalized values and raw typed values so future game updates can be diagnosed without guessing the scale.

## V0.65 constrained soul utility

Soul collection is a constrained utility, not permission to weaken a landing. Hold candidates for the same support are classified as unsafe (`margin < 0`), marginal safe (`0 <= margin < 0.20`), or comfortably safe (`margin >= 0.20`) after fixed-step and model uncertainty have already been removed. A safe hold always beats an unsafe hold; a comfortable hold always beats a marginal one. When both are comfortable, guaranteed soul intersections rank before surplus landing margin. With equal soul coverage, the safer hold and existing geometric score remain authoritative. Platform selection, bounded continuation, hard topology, walls, hazards, and the slow/fast Spirit intersection are unchanged.

Pickup X overlap uses `0.60`, derived from the measured approximately `0.594` player half width. Attempt feedback records `RawExpectedSphereHits`, `RemainingAtPlan`, and quota-capped `ExpectedSphereHits`. Consequently, collecting the last required soul does not create a false `BelowPrediction` when native code deactivates surplus spheres. Required evidence is `SoulHoldSelection[Safe=...,Comfortable=...,Hits=...,Replace=...]` plus the three expectation fields in `JumpAttemptPlan` and `JumpAttemptResult`.

## V0.64 landing-first receding-horizon controller

V0.64 treats every supported platform as a temporary driving corridor rather than a terminal jump target. Perception, optimization, and DOWN delivery still occur from one pre-movement `PlayerMovement.FixedUpdate` snapshot; the selected action is never replayed from an older preview.

1. Route ranking is lexicographic and landing-first. A safe complete slow/fast first-landing envelope always beats an unsafe one. If every route is unsafe, the largest worst margin wins. A first-landing safety difference greater than the near-tie tolerance is resolved before continuation, pickup, distance, or runway. Within the same first-landing tier, an executable continuation and its worst slow/fast margin rank first; only then do guaranteed objective identities, progress, runway, and the common geometry tie-break apply. A materially safer route may skip an immediate soul, but a comparable route may not.
2. Every stable jump landing and currently modeled `IntentionalDrop` receives one bounded topology-continuation evaluation. The predicted support becomes the source of a synthetic live scan, and the same selector/plan code proves the next action. The test covers both the minimum- and maximum-travel endpoint and uses the worse continuation result. Boost triggers behind the predicted landing are removed. No visible successor is `unknown/accepted`, not a reason to stop at the end of the scan horizon. Exact natural-fall first-contact topology for a drop is intentionally left as a validation item until it can be simulated without incorrectly applying jump launch velocity.
3. Look-ahead is ranking evidence, not a cached command. If all visible options are terminal, the safest immediate action remains the fallback; the runner does not freeze because the horizon found no perfect route. After real contact the controller discards the projection and replans from the observed support, position, Rigidbody velocity, hazards, spheres, and typed Spirit state.
4. A late command may no longer use raw collider bounds as an emergency authorization. It must place the predicted centre inside `[SafeLeft, SafeRight]`, clear the target face, source lip, intermediate surfaces, and hazard. This removes the repeated `EmergencyLateRawLanding` pattern whose predicted endpoint was already beyond `SafeRight` before the larger observed overshoot.
5. Ground landing selection aims at the verified safe-corridor centre and compares the complete slow/fast travel envelope. Its diagnostic record includes target safe bounds, travel/landing envelope, left/right margins, worst margin, runway, projected continuation outcome, speed endpoints, guaranteed objective instance IDs, and the exact ranking reason. Immediate and alternative routes use the same geometry score; a farther route receives no unconditional bonus. With `preferSphereCoverage=false`, objectives cannot retain the observed topology or enter route comparison.
6. Wall routing separates `PhysicalLipY = sourceWall.Top - 0.20` from `ObjectiveReleaseY`, which may be higher for a wall-soul row. Lip-crossing time, boost aging, horizontal-resume detection, transfer travel, setup pulses, and downstream face interception use one frozen physical wall rectangle; diagnostic surface refreshes cannot mutate its left/right/lip release test. Objective scanning remains active even when the body is already within `0.10` of the top. Safety tier and material landing/face-contact margin are compared before objective reachability. The objective is not a hard survival gate: if no safe exit can reach it, the controller selects the safe physical transfer and records `ObjectiveApexFallback`. When slow and fast Spirit outcomes differ, every native `20 ms` hold is evaluated at both endpoints; only their intersection is eligible, then worst endpoint safety, objective completion, and centring rank the command. A solved wall transfer retains DOWN through its planned deadline instead of being overwritten by the generic wall chain. The former single height delayed horizontal motion in the model long after the game had detached from the wall, under-predicting Spirit wall-exit travel by roughly `19..24` units.
7. Active gameplay and successful completion traversal both preserve the verified section cruise speed as the horizontal floor. A post-quota Spirit jump therefore cannot regress to a stale lower base speed while the real runner remains at the section plateau.

Required V0.64 evidence is `FixedStepRouteCommitted` with `Action=DOWN` or `Action=COAST`, `LandingEnvelope`, `LandingMargins`, `WorstMargin`, `FutureSpeedTransition`, `Ranking=`, guaranteed-objective IDs, and a `Selection` containing `Continuation=Verified`, `Rejected`, or `NoVisibleSuccessor`. Continuation evidence must show both endpoint outcomes and `WorstSafety`. Wall evidence must include both `PhysicalLipY` and `ObjectiveReleaseY`, `JointSpeedEnvelope` or `JointFaceSpeedEnvelope` when a speed range exists, and an unreachable optional pickup must produce `ObjectiveApexFallback` instead of a blind attached climb. Ordinary and Spirit routes must still be validated by unassisted gameplay; V0.64 has not been deployed.

## V0.63 unified route-command proof

V0.63 replaces the V0.62 pickup-assisted bypass with one route-selection and action-proof pipeline. Normal and Spirit modes use the same terrain graph, target ordering, collision checks, and command contract; Spirit contributes a typed speed-state envelope rather than a separate path script.

1. `SelectReachableRoute` selects one terrain edge and the same `Plan` invocation proves the command that will execute it. Farther alternatives retain all intervening surfaces, so a target cannot be selected with one simplified scan and executed with another. The nearest feasible top, face, or lower-support route remains authoritative unless the exact farther command clears every intervening solid and preserves the immediate route's objectives.
2. Normal soul collection is part of that same terrain command. Active `BonusSphere` positions are passed into the ordinary route proof and score only commands that already satisfy target, face, intermediate-surface, and hazard constraints. There is no later sphere-only override that can replace a safe terrain command with an independently proved jump.
3. Live-geometry code Sections `2` and later enumerate only native fixed-step holds: `0.02, 0.04, ... 0.18 s`. Selection, logging, actuation, and calibration therefore describe the same physical `1..9` fixed-step buckets. Earlier/authored profiles retain their established candidate set.
4. Every separated target has a leading-face collision contract, including a same-height target across a real gap. The trajectory must clear the physical target-left face and still have a safe vertical entry at `SafeLeft`; checking only the final centre landing is insufficient because Unity can resolve an earlier descending corner contact. Continuous/physically bridged seams remain governed by the micro-gap rule instead of this face rule.
5. A walkable micro-gap is derived from the measured player body width: `0.10 < gap <= 2 * halfWidth - 0.10`, level tops within `0.10`, and no overlapping hazard. Ground-row objectives within the body's passive pickup envelope do not turn that seam into a jump; only a genuinely elevated objective may justify an airborne command.
6. Native `SpiritBoost` state is independent from `BonusSphere`. The runtime reads the typed `PlayerMovement.currentSpiritBoost`, maximum component, decay, current/pre-step speeds, and active uncollected `SpiritBoost` trigger bounds in the current section. A soul-count change is never treated as proof of a speed reset.
7. When a typed boost trigger can be crossed, one launch/hold command must be safe for both trajectories: the no-pickup/slow path and the pickup-reset/fast path. Both endpoints must fit the same target and clear its leading face, safe-entry line, hazards, and every intermediate surface. A failed trigger inventory is handled conservatively as an immediate maximum reset; unavailable typed kinematics fail closed.
8. Landing calibration is applied to the complete horizontal curve `X(t)`, not added only at its endpoint. Target-face, safe-entry, hazard, intermediate-surface, and sphere crossing times all use the same calibrated travel scale as the selected landing. The Spirit no-pickup trace integrates the current boost decay at sub-fixed-step resolution; the pickup trace uses that same scale while testing typed trigger overlap and maximum-component reset.
9. A future-speed command has a singleton launch position and is re-evaluated against exact live X before DOWN. Spirit gameplay never reuses a ground route lock, because a pooled typed trigger can appear without changing terrain or current velocity. A future-speed jump or intentional drop cannot publish a speculative second-stage preview and is rescanned after real contact or pickup. `MinimumHorizontalTravel`, `MaximumHorizontalTravel`, `FutureSpeedTransitionExpected`, `TravelEnvelope`, and `SpiritBoostRoute` preserve that proof in the action log.
10. Pickup overlap is asymmetric from the feet: only `0.45` below is reachable, while the body/trigger envelope extends `2.15` above. This prevents a high arc from inventing lower-soul hits while preserving real body-height collection.
11. Timing calibration is a `9 x 9` grid: integer surface-height layers (`DeepDown`, three downward steps, `Level`, three upward steps, `HighUp`) crossed with native hold steps `S1..S9`. A floating `+2.995` edge contact no longer shares a timing cell with a stable `+2` landing, and a delivered `60 ms` hold cannot calibrate an `80 ms` command. A sample needs two distinct stable fixed steps on the planned safe support, matching source/target and hold bucket, and a bounded landing residual no greater than `0.75`.
12. Calibration separately records typed boost component and maximum VX from takeoff through landing. A timing/travel sample is rejected when the typed component rises by more than `0.25` or VX rises by at least `max(4, 20% of start VX)`. Ordinary soul pickups neither authorize acceleration nor invalidate a clean sample.
13. Exact target-face contact remains a first-class correction outcome. In the `PlayerMovement.FixedUpdate` prefix, before render-time pit classification, the runtime releases the old pointer and transfers the same target to the bounded wall controller. Wall DOWN/UP ownership advances on unique native fixed steps; separated pulses still require current physical contact and cannot attach to an unrelated death wall.

The V0.63 source passes the local no-deploy build with zero warnings and zero errors plus whitespace/static-reference checks. It still requires one unassisted normal and one unassisted Spirit gameplay trace. It has not been deployed.

## V0.61 edge-contact continuation rule

1. The ordinary initial jump plan remains authoritative. Do not shift its launch window, hold, predicted travel, or target merely because a later edge contact may be recoverable.
2. Edge takeover requires an active automatic learning flight, a prepared second-stage scan and executable plan, observed airborne history, exact projected current/next geometry, and an expected support no wider than `4.25`.
3. Physical support evidence is `Grounded`, at least `0.15` horizontal overlap between the live player collider and the expected raw support, feet within `0.35` of its top, player centre outside the expected safe interval, and live VY no greater than `10`. A support wider than `2.25` is eligible only through this edge evidence; centred landings keep the ordinary confirmation path.
4. This later physical collision overrides an earlier interpolation-only `TrajectoryDeviation`, but it does not override player/life identity, manual-input cooldown, retry/reward ownership, terrain lifecycle blocks, or source/next geometry mismatch.
5. The collision-damped live VX is not a flight-planning sample. Use `max(abs(liveVX), lastReliableHorizontalSpeed)` for the continuation because DOWN is issued before the native FixedUpdate restores forward motion.
6. Recompute route selection, hold, horizontal travel, target fit, and hazard trajectory from the contact position. Early promotion remains bounded by one collider width plus one planned fixed-step travel and is legal only when the translated landing stays inside `[target.SafeLeft-0.20,target.SafeRight+0.20]`.
7. Finish the prior sample with the exact expected support as current fixed-step evidence, release any old pointer, and issue the new DOWN before the same native movement update. The prior attempt may still be classified as an edge/unsafe-centre landing; recovery must not relabel its prediction as accurate.
8. Emit `NarrowEdgeContactTakeover` before planning and `UrgentNarrowLandingFixedStepChained ... EdgeContactTakeover=True` after input acceptance. A failed correction must emit its allowed early distance, fresh landing, target interval, and trajectory check.

## V0.60 walkable-seam action arbitration

1. First prove `WalkableMicroGap` with the universal V0.58 physical gates: body-width fit, level tops, and no seam-overlapping hazard.
2. Inspect active objectives only from the live player X through the immediate target's safe right edge. Use `max(Current.Top, Next.Top) + 1.15` as the grounded pickup envelope.
3. Count objectives inside that envelope as `PassiveWalkObjectives`; a jump trajectory intersecting them does not represent an incremental pickup because ordinary running already collects them.
4. Count objectives above that envelope as `ElevatedObjectives`. Only this count may keep sphere-aware jump arbitration available across an otherwise walkable seam.
5. When `ElevatedObjectives == 0`, return `WalkableMicroGap` even if the ballistic candidate reports `PredictedJumpHits > 0`. The runtime sends no jump input and continuous horizontal motion crosses the seam.
6. A raised-pillar transfer is not a micro-gap. V0.63 retains the immediate top or exact-face route; a farther support cannot replace it while attached objectives remain uncollected.

## V0.59 native-rebound wall ownership rule

1. The normal `EnterTrenchThenWallJump` route is still armed at its computed `PlannedLaunchX`. No earlier input is introduced.
2. One early ownership handoff is allowed only when all of these are true: Spirit Boost is active; code Section is `3`; the live state reports `Grounded`; live `VY >= 2.50`; the already selected maneuver is `EnterTrenchThenWallJump`; its next surface is adjacent (`Gap <= 0.10`) and at least `4.0` units higher; and the predicted wall-face contact centre is no more than `0.65 s` away at current planning speed.
3. This state combination is treated as a native collision rebound from a recoverable edge/corner landing. The runtime stores the exact current source, next-wall identity, route speed, physics snapshot, and wall target before the next FixedUpdate can make the body airborne.
4. The handoff calls the existing passive approach state only. It must release/retain no new DOWN: the ordinary wall detector must still prove `Detected && Touching` on the planned face before the bounded wall pulse executor can act.
5. The ownership contract expires through the existing `1.50 s` timeout/target-overflight logic and cannot attach to an unrelated death wall. A valid trace emits `SpiritSection3NativeReboundWallOwnershipArmed`, then `WallDropRouteArmed`, and finally contact-confirmed recovery.

## V0.58 universal walkable-gap rule

1. Apply body-bridged seam detection to every map, section, speed mode, and active/completion phase; it is no longer a live Stage-1/2 routing option.
2. Infer player half-width from the minimum raw-to-safe inset minus the scanner's `0.15` safety margin. A seam is walkable only when `0.10 < Gap <= 2 * inferredHalfWidth - 0.10` and `abs(DeltaY) <= 0.10`.
3. If the nearest hazard overlaps the seam corridor, retain ordinary jump/hazard planning.
4. Terrain-only, projected-landing, downstream reachable, and Spirit/high-speed selectors all retain a walkable immediate seam rather than promoting a farther platform.
5. Sphere-aware planning may override walking only for an objective above the grounded pickup envelope. Passive row-sphere intersections do not add value over walking.

## V0.57 walkable micro-gap rule

1. Enable the rule only for non-authored live-geometry code Section `2` (the displayed third section).
2. Infer player half-width from the minimum raw-to-safe inset minus the scanner's `0.15` safety margin. A seam is walkable only when `0.10 < Gap <= 2 * inferredHalfWidth - 0.10` and `abs(DeltaY) <= 0.10`.
3. If the nearest hazard overlaps the seam corridor, retain ordinary jump/hazard planning.
4. Terrain-only, projected-landing, reachable-route, and Spirit/high-speed selectors all retain a walkable immediate seam rather than promoting a farther platform.
5. Sphere-aware planning still enumerates safe jumps; it overrides walking only when the selected trajectory predicts at least one soul pickup.

## V0.56 Spirit transient-speed and passive-correction rules

1. Establish `SectionCruiseVX` only after three accepted grounded samples remain within `0.04` of one another. Spirit's approximately `0.10` per fixed-step decay cannot masquerade as a stable floor.
2. Only for Spirit-Boosted live Section 3, preserve live speed as trajectory `V0`; if `V0 > SectionCruiseVX + 0.25`, use verified cruise as `BaseVX` and integrate measured boost deceleration. Normal Section 3 and all earlier sections retain their previous speed floor.
3. In that same Spirit Section-3 scope, before replacing an impossible immediate lower support, project speed at `Current.Right + playerHalfWidth`. If the immediate natural drop is valid at that edge speed, retain it and keep reevaluating rather than locking a farther jump.
4. One-step landing ownership is legal when the exact expected support and next geometry match, the contact is grounded/non-rising, source width is at most `4.25`, remaining residence is at most `max(0.070 s, 3.75 fixed steps)`, and the prepared successor is executable or `IntentionalDrop`. The widened rule applies only to live Section 2 or Spirit live Section 3; the prior `2.25` narrow-chain rule remains unchanged elsewhere.
5. During a Spirit Section-3 `IntentionalDrop`, only exact physical contact with its expected lower support face can create a wall route. The observed face and feet replace prediction; the ordinary bounded wall pulse controller performs the climb. Unrelated pit walls remain ineligible.

Historical V0.30 deployment identity is length `380928`, SHA-256 `1A7607B4A4D639DA053AC52E69549A19FF40D9660F1C02A66C81E631B63021C2`. V0.31 is synchronized at length `394240`, SHA-256 `A1D575CB397EB60B75C9A019C4FBEA0AF27E620BC1B43FA3F2F3AD8270C72EE1`.

Section 0's map semantics and planner-specific routing policies remain unchanged because the V0.27 run completed it. V0.28 changes the shared wall continuation scheduler and two Section 1 route contracts, so the next trace must still regression-test Section 0 rather than assume source locality guarantees runtime equivalence.

## V0.37 bounded correction rules

1. After a prepared narrow landing is accepted on one physics step, recompute the next route from current position and live speed. Early promotion is legal only while current X has not passed the nominal launch. Recompute `liveLandingX = currentX + plannedHorizontalTravel`, require the wait to be less than `max(0.80, 1.75 * VX * fixedDelta)`, require that live landing inside `[SafeLeft-0.20, SafeRight+0.20]`, and rerun the hazard trajectory from the live launch. Otherwise retain the ordinary barrier/wait; urgency alone is not permission to jump.
2. For the mandatory Ground 3 S2-to-S3 lower-objective route, try the direct finite-face intercept first. If it is unavailable, enumerate setup pulses from `0.060..0.135 s` and require the high-side fixed-step apex envelope to remain at least `0.30` below the current release height. Every other attached climb still starts at `0.075 s`.
3. During an inactive but valid completion traversal, an airborne route-less collision can become a wall route only when `VY <= -1`, native/raycast evidence says touching, and static-map lookup finds a surface with `abs(surface.Left-faceX) <= 0.35`, top at least `0.35` above the feet, and rise no more than `12`. Arm that exact surface and run the ordinary bounded wall controller. This is not a generic death-wall retry.
4. A post-death guard may also rearm after two stable grounded physics steps at an exact mapped wall even when collision has reduced VX to zero. The remembered terrain section remains authoritative until the next real active section frame.
5. In normal active code Section 3 on authored Ground 7 S2, while required souls remain, solve a face intercept against the downstream platform using hold range `0.020..0.135 s` and feet band `[targetTop-4.15, targetTop-0.45]`, preferred `targetTop-2.00`. On physical contact the existing wall watch promotes that face and performs the climb. If no intercept is safe, retain the verified top-landing/static-alternate fallback. Boost and above-cruise exits never use this collection shortcut.

## V0.38 bounded correction rules

1. On code Section 1 Ground 5/S4 only, arm the low-pickup descent when feet Y is in `(minimumSphereY + 0.42, minimumSphereY + 1.25]` and VY is below `-2`. Predict up to eight fixed steps from live VY/gravity, stop as soon as pickup disappearance/count/minimum-Y proves collection or the feet band is reached, and retain the dynamic fixed-time deadline for background catch-up.
2. Before generic wall recovery and grounded routing, code Section 3 may convert an owned, already-taken-off ordinary jump into a wall route when native wall detection proves `Detected && Touching`, VY is at most `+2`, and static lookup resolves the touched face to a platform whose top is more than `0.20` above the feet. Finish the old flight sample as `AirborneWallContactHandoff`, make that exact surface the target, and invoke the existing bounded wall solver in the same frame.
3. Do not apply this promotion to Section 0-2, completion traversal, an unowned/manual flight, a wall maneuver, a route with any active wall/contact/descent owner, or a top at/below the current feet. A transient Grounded flag does not veto promotion because it is the collision pulse being handled.

## V0.39 bounded correction rules

1. During successful completion traversal, exact descending physical contact may override either no route or an owned, already-taken-off automatic non-wall flight. A passive approach, wall climb, exit watch, mandatory-face state, or attached descent cannot be overridden. Static face lookup and the existing climbability bounds remain mandatory.
2. When overriding an active completion flight, finish its learning sample as `CompletionAirborneWallContactHandoff` before installing the touched static face. Log the prior route, attempt, plan, maneuver, and target so boosted overshoot is distinguishable from route-less completion recovery.
3. If an armed wall-exit contact watch observes a verified stable landing on a support other than its exit target, the physical support is authoritative. Finish the old attempt as `Landed`, clear wall recovery and plan locks, set automatic ground planning ready immediately, and log `WallExitIntermediateLandingReplan`. Do not keep an obsolete face watch across that landing.

## V0.40 reactive correction rules

The nominal planner remains responsible for selecting the intended route. A separate, tightly gated supervisor owns only observations that prove the nominal contract false:

1. `Unexpected mapped wall contact`: require an owned automatic flight or wall route, exact `Detected && Touching` evidence, and a static-map face whose top is still climbable from the observed feet Y. Finish the stale attempt, retain its prediction in the log, and rebuild the wall route from the observed face/feet/speed. Never apply this rebase to Ground 3's mandatory objective face.
2. `Unexpected stable support`: use the V0.39 two-fixed-step landing replan. A real support clears all obsolete exit watches and predicted targets before grounded planning resumes.
3. `Known undershoot`: a rejected landing candidate remains rejected. Do not turn maximum hold into a landing solely because every shorter hold is worse. If the same geometry intentionally reaches a vertical face, keep that face armed and wait for physical contact.
4. `Low-lip contact`: when the current mapped top is wide, no downstream target exists, the actual contact is at most `1.25` below the release height, and landing prediction has no safe solution, choose the shortest vertical pulse that clears the lip (`0.020..0.075`). This is `ReactiveLipEscape`, not a predicted landing. The next real wall contact or stable support must replan again.
5. `Acceleration discontinuity`: completion Spirit traversal stores recent positive `dVX/dt` and a per-section observed completion speed ceiling. While that evidence is active, wall-exit planning uses a bounded higher speed. A narrow nearest target that fails at that planning speed is replaced only by the first statically enumerated candidate with a valid strict landing.
6. `Observed chained face`: when promotion is caused by current physical contact, require the observed face X to match the mapped successor and the feet to remain below its top. Promotion from the old lip may still arm a future face without pretending contact already occurred.

Every recovery log must include the abandoned route/attempt/target, predicted landing, actual position/velocity/contact feet Y, selected recovery state, and the new mapped target. The recovery layer may prevent a death, but it must never retroactively report the failed nominal prediction as correct.

## V0.41 committed face/top and completion-speed rules

1. Run the strict target-top landing solver and every authorized alternate-support search first. Do not relax their safe interval or raw-body rules.
2. If those searches fail on mapped `Ground 7/S2`, the lower target face is `5.25..15.50` units ahead, and top delta is in `[-3.25,+0.35]`, create a distinct `FaceOrTopPending` contract. Normal code Section 2 uses at most the native `0.180 s` cap and normal code Section 3 uses at most `0.135 s`; a fixed-step-proven hold is preferred. The solver evaluates exactly `ceil(hold/fixedDelta)` powered ticks because that is what the actuator now delivers, rather than rejecting completion routes with the obsolete timer uncertainty envelope. Before reward-object latch, successful post-quota terrain traversal may use a model-proven face intercept at its measured completion speed, but it may not reuse the empirical normal-speed cap. The retained cap is legal only on the two exact normal authored routes and is never labeled predicted landing success.
3. Deliver every explicit face pulse with an exact fixed-step hold ceiling. Give its contact watch a fixed-step deadline derived from predicted flight plus a bounded margin. From DOWN onward, retain the exit target, sample, watch, and pit-recovery ownership. While the player remains above the finite face bottom, before target overflight, and inside that deadline, the committed target itself is recoverable-wall evidence even when the `1.20`-unit ray cannot see it. `WallBounceExitFlight` is a transition state, not a successful terminal result while this contract is pending.
4. A face outcome requires actual `Detected && Touching` contact, left-facing normal, `abs(faceX - target.Left) <= 0.45`, player centre before the face, a live static-surface/collider identity and geometry match, and feet inside the route's planned face window. Check this at render cadence and immediately before the primary `PlayerMovement.FixedUpdate`; the latter may issue the attached DOWN for the same physics step. Atomically promote the mapped exit target and solve the next wall press from observed contact. Do not reuse the generic `gap <= 5.25` chained-wall promotion for this ten-unit flight.
5. A top outcome requires an exact static-support scan whose top and body X overlap the stored target and non-rising `VY <= 2.5`. If the old pointer is still held in the FixedUpdate prefix, exact support closes that edge before native movement consumes the step. One fixed step is sufficient for a pending optional face/top contract, a successful completion-traversal target no wider than `2.25`, or the final top of an owned automatic wall climb; grounded negative VY does not invalidate physical support. Ground 3 mandatory-face top landing remains failure. The Section-3 low-soul `CollectionFace` contract is distinct: an exact face is success, while one real top-support step is logged `CollectionFaceTopLandingMissed` and never scored as collection success.
6. A fixed-step support latch stores attempt/player/route identity, target and actual surface, position, velocity, body half-width, event time, and physics step. Render-time consumption must use that captured surface and translated historical body bounds; it must never combine historical coordinates with the current collider scan. Historical evidence may score the real landing but is excluded from wall-clock flight-time training. Ground planning is rearmed only if the live body is still supported; otherwise the log reports `HistoricalSupportOnly` and waits for current airborne state to resolve.
7. Completion wall-exit speed uses only current-epoch evidence. `planningVX = max(observedVX + 0.25 * acceptedPositiveAcceleration, observedCompletionCeiling)` when the relevant evidence exists, with the gain bounded to `12`; there is no unconditional `+4`. Reset completion ceilings and acceleration on stage/section boundaries and every life/terrain revocation. Active Spirit routes retain their separate deceleration model.
8. Explicit face pulses and live-geometry Sections 2/3 use an exact fixed-step release count shared by solver and actuator. Earlier live sections and authored ground plans retain calibrated wall-clock release plus a fixed-step safety ceiling. A missing fixed callback has a logged wall-clock fail-safe. Keep wall-clock and delivered-step measurements separate in learning.
9. The primary-player FixedUpdate prefix owns committed face contact, mandatory setup/intercept, attached-objective descent, passive wall approach, and every separated pulse of an active generic wall climb. It can release an old edge and issue the next DOWN before the same native FixedUpdate. This prevents background render throttling from losing a contact or leaving a multi-pulse climb waiting for LateUpdate.
10. A prepared narrow-chain action runs before support latching. It releases any prior owned hold, closes the old sample, rescans from live position/speed/physics, validates target geometry and hazards, and issues the next DOWN in the same prefix. If no safe next action exists, the support latch owns the frame and generic wall recovery may not mutate that route.
11. Lifecycle discontinuity envelopes use elapsed fixed steps, current/prior velocity, learned jump velocity, and gravity. A large render-to-render displacement inside that physical envelope is background catch-up, not teleport. While a face contract exists, the generic four-second wall-clock sample timeout is subordinate to its fixed-step watch deadline.

Required diagnostics are `CommittedExitFaceInterceptSelected`, `CommittedExitFaceContactHandoff`, `CommittedExitFaceFixedStepHandoff`, `CommittedExitFaceContactRejected`, `CommittedExitFaceFlightPending`, `UrgentNarrowLandingFixedStepChained`, `UrgentNarrowLandingFixedStepRejected`, `WatchedExitSupportForcedRelease`, `WatchedExitSupportFixedStepLatched`, `WatchedExitSupportFixedStepConsumed`, `AuthoritativeWatchedExitLandingConfirmed`, `AuthoritativeCollectionTopSupportObserved`, `WallTargetLandingFixedStepHandoff`, `WallExitLandingOwnershipHandedOff`, `CollectionFaceTopLandingMissed`, `CompletionWallExitSpeedProjection`, and `CompletionSpeedEpochReset`. Every one-step landing log must state the exact mode, fixed step, captured support/target, captured and current positions, VX/VY, release state, evidence source, and whether the live body is still supported.

## V0.42 retry lifecycle rules

1. `SecondWindSuggest()` proves only that the native coroutine was created. Preserve that intent across Bonus/non-Bonus transition frames; never use a fixed delay as popup readiness and never clear it merely because `GameState.IsBonus()` is temporarily false.
2. Claim only a newly presented `Popup.Show(PopupData,bool)` whose data has both actions and whose sprite is the exact `secondWindIcon` from the pending `SecondWind`. On the following render frame, revalidate the same popup identity, visibility, displayed sprite, and active/interactable requested button.
3. Dispatch Continue through `confirmButton.onClick.Invoke()` and No through `PressCancelButton()`. Compiler-generated `_b__7_x` methods are implementation details and are not called directly. `OnClose()` is not the No action.
4. A Continue click is only a request. Native retry acknowledgement is the matching `SecondWind.RewardForShowing()` postfix. The callback reads but never writes `secondWindUsed`; the game's one-use choice remains consumed. Keep terrain blocked after that acknowledgement until the matched popup is no longer visible and two distinct frames satisfy `IsBonusStage && IsActiveGameplay && HasPlayer`. `CharacterFellOff` is logged but cannot veto resume because the native flag may remain sticky for the rest of the protected run.
5. Release owned input immediately when the prompt is observed, suppress every new `Press`/`Pulse`, and fail closed in both PlayerMovement Update and FixedUpdate hold refresh while retry owns the modal. Clear stale completion-speed, quota-road, and reward-target ownership without changing the authored terrain map or any jump/wall parameter. Keep the existing run-tracking epoch across the transient outside-stage boundary and resume from a fresh route decision only after the two-frame active-gameplay acknowledgement.
6. Retry native errors, thrown UI invocation, or `OnClose` without reward/error acknowledgement at most three Continue attempts, always against the same revalidated popup. The close callback has a `3 s` grace interval for callback ordering. Exhaustion or the `120 s` reward timeout arms one verified native No fallback; if fallback cannot be invoked or acknowledged, modal ownership remains fail-closed. Other timeouts are prompt `30 s`, popup readiness `30 s`, gameplay resume `15 s`, and exit acknowledgement `20 s`. Lack of reward acknowledgement must not change `secondWindUsed`.
7. Startup inventory must show exactly one each of `RetryPromptPostfixes`, `RetryPopupPrefixes`, `RetryPopupPostfixes`, `RetryRewardPostfixes`, `RetryErrorPostfixes`, and `RetryClosePostfixes`, in addition to movement patch inventory `1/1/1`. This exact inventory gates automatic retry dispatch.

Required retry evidence is `RetryPromptObserved -> RetryPopupMatched -> RetryModalOwnershipHandoff -> Auto retry request dispatching -> Continue acknowledged by RewardForShowing` with `NativeUsedAfterAcknowledgement=True, Rearmed=False`, then `RetryResumeEvidence 1/2 -> RetryResumeEvidence 2/2 -> Auto retry outcome -> RetryModalOwnershipReleased`. Later deaths must not produce another prompt merely because of this mod; a new sequence is legal only when the game itself calls `SecondWindSuggest` again. Error exhaustion must instead show `RetryFallbackCancelArmed`, a validated No dispatch, and popup-close plus exit acknowledgement; no terrain action may appear while any retry gate is active.

## V0.43 exact-contact and impulse-ordering rules

1. Ground 3's mandatory downstream-face solver first evaluates the existing robust `+/-1` horizontal fixed-step envelope. Only if it has no valid candidate, while the runtime already owns exact mapped current-wall contact, evaluate the zero-offset horizontal step. The powered-step count remains exact and the complete finite feet-Y/contact-velocity checks still apply. The next face must be physically observed; this fallback cannot report a landing or route success by prediction alone.
2. A fixed-step wall pulse that has just released cannot be rejected on the same `FixedStepSequence`. Record the release observation and wait for a strictly later physics step. This lets the second upward observation arrive after a one-step click while preventing render callbacks from consuming a retry slot between physics ticks.
3. Impulse success and failure are terminal alternatives for one attempt. Once failure has been logged, later residual rise cannot confirm that attempt. Required ordering is `WallClimbImpulseDecisionDeferred -> WallClimbImpulseConfirmed` or `WallClimbImpulseDecisionDeferred -> WallClimbImpulseRejected`, never rejection followed by confirmation.

## V0.44 one-use retry, narrow-support chain, and new-face handoff

1. `RewardForShowing` is acknowledgement only. Read `secondWindUsed` after the callback and never modify it. A successful native Continue remains consumed. The existing limit of three dispatch attempts applies only to failures while invoking the same verified popup; it is not a number of gameplay retries. With Auto Retry enabled, choose Continue when the one native choice is offered; with it disabled, choose No. A later prompt is handled only if the game itself legitimately offers one.
2. When a current and next support are both no wider than `2.25`, the gap is real, the live grounded centre remains within the raw current support tolerance, and a hold predicts a safe-centre landing on the next support with a hazard-cleared full trajectory, `NarrowSupportImmediateChain` may execute from the live centre even if conservative safe-source/safe-target launch intervals have no `0.02` intersection. Any ordinary conservative candidate remains preferred. This is a source-edge recovery, not a global landing-tolerance expansion.
3. While a prior wall pulse is in `ExitFlight`, exact `Detected && Touching` contact with a forward face more than one body-width beyond the prior pulse contact is a new wall. Mandatory objective-face, setup/intercept, and attached-descent ownership cannot be replaced. Otherwise, resolve the new face to a climbable static surface, reset the per-face pulse budget, and invoke the existing bounded wall solver immediately. Required evidence is `ReactiveWallRouteRebased -> ChainedExitFlightWallContact -> WallRecoveryPlan/WallJumpDown` without an intervening grounded `RouteEval`.

## V0.46 completion-only dynamic landing rules

1. This selector is legal only while `IsSuccessfulCompletionTraversal(state)` is true. It receives the scanner's uncollapsed candidate list. The older boost selector remains unchanged for active gameplay and is not run first during completion.
2. Evaluate the immediate route using the ordinary planner. Retain it if valid, `IntentionalDrop`, or `ContinuousSurface`. Retain all wall/trench/topology failures. Farther-target search is legal only for `NoVerifiedLaunchWindow` and `RaisedLandingHasNoSafeDirectJump`.
3. For every distinct farther static support, rebuild a candidate scan and run the ordinary all-hold solver with live VX, current deceleration/base speed, flight scale, landing bias, hazard geometry, and safe-centre bounds. Only `GroundJumpToLanding` may become a dynamic alternative.
4. Let `x_in = intermediate.Left - playerHalfWidth` and `x_out = intermediate.Right + playerHalfWidth`. Solve horizontal time from the same dynamic speed integral used by the landing planner. At `x_in`, predicted feet must be at least `intermediate.Top + 0.10`; otherwise the body hits the leading face. If feet start above that bound but fall below it by `x_out`, the flight would land on the intermediate top before the selected target and is rejected. This check is repeated for every intervening surface.
5. Score only verified candidates: current-frame executability dominates, followed by safe landing width, launch-window width, and shorter forward travel. A farther wide platform cannot win merely because it is farther.
6. Completion wall exits use the existing two-phase wall solver. If the nearest exit target has no legal hold, enumerate ordered static supports, re-run the strict hold/landing solver, and validate the existing route contract. Active pre-quota wall behavior is unchanged.
7. Log one `CompletionDynamicRoute` record whenever the selection changes. It must include immediate failure, live VX, each target, selected hold, planned launch/landing/window, and per-intermediate entry/exit height. Ordinary `JumpAttemptResult` remains the authoritative expected-versus-actual landing result.

## V0.47 completion-only dynamic face correction

1. Run strict landing selection and every authorized downstream top alternative first. A face correction is legal only when all of them fail and `IsSuccessfulCompletionTraversal(state)` remains true.
2. Enumerate the same ordered static surfaces used by wall-exit landing search. Exclude the current immediate exit, require face gap in `(5.25,15.50]`, and require target-top delta in `[-3.25,+0.35]`. These are bounded collision candidates, not assumed platforms.
3. For each candidate, solve exact fixed-step vertical motion together with the current post-lip horizontal speed integral. The old wall must be cleared by at least `0.10`; at target-left contact the feet must lie inside `[targetTop-4.15,targetTop-0.25]`, and vertical velocity must be descending. The actuator uses exactly `ceil(hold/fixedDelta)` powered steps, matching the solver.
4. Promote only a solver-proven candidate. Type it `FaceOrTop`, preserve its target and fixed-step deadline from DOWN, suppress generic pit/free-flight abandonment while the finite face remains reachable, and hand exact physical contact to the existing bounded wall controller. A verified landing on the same target top is also safe; neither outcome is reported as strict landing prediction.
5. If no face candidate is proven, retain the prior fallback unchanged. Never use empirical Ground-7 evidence for this dynamic completion branch, and never expose it to pre-quota collection navigation.
6. Log every geometry rejection and every hold evaluation in `CompletionDynamicFaceSearch`. A selection must additionally emit `CompletionWallExitFacePromoted` with old/new target, gap, observed and planning VX, hold, contact X/Y/time/VY, and finite face window. Subsequent authority is proved by `CommittedExitFaceContactHandoff`, watched target-top support, timeout, overflight, or death.

## V0.48 completion wall-exit speed envelope

1. Keep active/pre-quota routing unchanged. The speed envelope is enabled only when `IsSuccessfulCompletionTraversal(state)` is true.
2. Define the fast endpoint from current accepted speed, current-terrain completion ceiling, measured positive acceleration, and—while Spirit is active—the measured minimum resume tier `22.75`. Define the slow endpoint from latched contact speed minus `0.25 s * live boost deceleration`, clamped to the live base-speed floor.
3. For a top landing, solve at the fast endpoint first because overshoot is the dangerous result. Replay the exact selected hold at the slow endpoint; reject it if that same press undershoots or loses physical body fit. Never combine two different endpoint-optimal holds into one claim.
4. For a finite-face interception, solve at the slow endpoint first because arriving late and below the face is dangerous. Replay the exact selected fixed-step hold at the fast endpoint and reject an early/above-face result. The contact deadline uses the slower endpoint's flight time.
5. If completed code Section 0 with active Spirit has no candidate safe across the full interval, a high-end landing may be selected as `SectionOneSpiritResumeUpperBound`. This is a typed, logged best-effort response to the measured `15.5 -> 22.4` discrete lip-resume event; it is not available in other sections or before quota completion.
6. A promoted completion wall chain remains authoritative when Unity emits `Grounded=true` while `VY > 2.5`, passive wall approach and residual-rise wait are both active, and the phase is `AwaitingWallContact`. Release input and wait for the next wall/apex/stable-support observation. Do not invoke ground planning from that collision pulse.
7. Required diagnostics are `CompletionWallExitSpeedProjection` with `SpiritResumeFloor`, `PlanningVXEnvelope=[slow,fast]`, both endpoint candidate summaries, `CompletionWallGroundPulsePreserved`, and the scoped upper-bound recovery warning when used.

The ordinary control failure at `[699,701] @ 7` is the canonical slow-end regression: the old scalar plan used VX `19.9`, selected a nine-step `0.180 s` transfer and predicted X `715.016`, while the delivered press ran at decaying VX `13.3 -> 11.9` and died at X `711.60`. The envelope must reject that target-specific landing claim. If no target survives both endpoints, existing bounded attached pulses and exact physical-face recovery remain available; uncertainty is not converted into a false landing prediction.

## V0.49 accelerated respawn takeover

1. Keep releasing all input throughout the airborne protector/respawn sequence. An arbitrary scene discontinuity, missing player, replacement player, or unsupported map remains on the conservative two-stable-step plus restart-delay path.
2. Mark immediate takeover eligible only after two distinct fixed steps confirm an unrecoverable pit, or after a discontinuity proves an upward respawn teleport: `deltaY >= 2`, sticky native `CharacterFellOff=true`, and the reconstructed prior Y is below the pit threshold. These are lifecycle proofs, not route guesses.
3. The first observed grounded fixed step must also have `|VY| <= 2.50`, a supported map, a live player, a remembered terrain section, and either `|VX| >= 1.0` or exact mapped wall contact. This is the earliest frame where a ground/wall jump can be physically accepted.
4. On that fixed step, clear the old `0.45 s` gate, refresh the static registry after rearming the remembered terrain epoch, recompute routing state, and continue through the ordinary planner in the same `LateUpdate`. Every plan uses current Rigidbody VX; no old death-route plan, target, hold, or speed barrier is reused.
5. If any immediate-takeover proof is missing, retain the former conservative path and return for a later frame. Required diagnostics are `VerifiedRespawnTeleport`, `RecoveryEvidence`, `ImmediateTakeover`, `RequiredStableFixedSteps`, `SameFrameRouting`, and both controller/routing sections in `PitDescentGuardReleased`.

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

## V0.34 collision escape and wall-exit contract

The V0.33 trace separates three previously conflated values: live collision VX, pre-contact route VX, and post-lip VX. A live value of zero is not a useful jump-distance input when the player is physically flush against the intended face.

1. A direct grounded contact escape is legal only for the observed Ground 6 S0 -> S3 raised lip, when the current plan is `EnterTrenchThenWallJump`, the classifier reports `AdjacentWall`, gap is at most `0.10`, the player reached the planned face, and the wall detector confirms both `IsDetected` and `IsTouching` at the exact next-surface face. Other mapped adjacent walls enter the normal contact-confirmed wall executor instead of either waiting forever or skipping their trench/objective route. A velocity transition or completion-dash fixed-step barrier still has priority.
2. Enumerate supports beyond the immediate raised body plus the body itself. Predict every supported hold with the latched pre-contact VX; do not substitute live collision VX `0`. Check vertical clearance and hazards before scoring the landing.
3. In normal-speed mode, preferred safe bounds remain best, but a predicted player footprint must overlap a verified raw top by at least `max(0.15, speed * fixedDeltaTime)` to be executable. If model uncertainty rejects every ordinary candidate, a native-cap press toward the widest downstream verified support is preferable to permanent zero-input contact. This direct fail-open rule is restricted to confirmed zero-gap Ground 6 S0 -> S3 contact.
4. In Spirit Boost mode, require the conservative safe interval with only `0.02` tolerance. For a confirmed grounded Ground 6 contact, if no strict interval exists, execute the hazard-cleared candidate with the smallest physical-support miss; if no ballistic candidate survives, transfer ownership to the confirmed-wall climbing executor instead of waiting at VX `0`. For an attached Ground 7 exit, if the nearest support is unsafe at current post-lip speed, enumerate ordered static supports and promote the first strictly safe target. Never apply normal raw-body-fit or native-cap undershoot acceptance to that boosted wall-exit transfer.
   Section cruise sampling is disabled whenever `SpiritBoostEnabled=true`; a boost plateau may never become the section's base-speed floor. A latched speed more than `max(1.0, 10%)` above an established cruise is also treated as boosted even if the flag has just changed.
5. When lookahead from an expected wall-top support produces a valid downstream action, retain its target, hold, predicted landing, and planning speed across the wall-contact handoff. At compatible normal speed, the prepared hold becomes the wall-exit solver's lower bound; the wall-specific attached/lip/post-lip calculation still chooses and validates the final hold. Ground 7 uses the observed post-gravity held velocity plus the measured flight-time scale. If every allowed hold is still predicted left of the target, the landing solver returns failure. While Section-3 objectives remain, the target stays armed as a planned physical face; it is not converted to undershoot success.
6. Ground 7 S2 static fallback and speed promotion are selected by authored piece/surface identity, not by section index, because the same piece occurs in Sections 2 and 3.
7. Ground 5 S4 has one narrowly scoped pickup correction. If a lower sphere remains, the player is descending faster than `-2`, and feet Y lies in `(minimumSphereY + 0.42, minimumSphereY + 1.25]`, release through at least one distinct fixed step. Resume when the pickup count/minimum-Y proves collection or feet reach the pickup band; use the live fixed-step/gravity deadline (up to eight predicted steps) and log if background catch-up advanced more than one step between control frames. No other wall reuses this rule.

Required V0.34 evidence includes `GroundedContactEscapeDecision`, `WallExitPreparedContractCaptured`, the wall-exit `Policy[...]` and `RawBodyFit` candidate result, `WallExitTargetSpeedPromoted` for boosted alternate selection, and `Ground5HighestPillarSinkArmed/Complete`.

## V0.36 typed reward, transition-road, and precision contracts

The two retained V0.35 runs both completed, but all seven deaths occurred after quota equality while the actual reward object was still absent. The controller had already advanced its mutable section index and the runner accelerated on a narrow transition road. This separates a lifecycle/reset error from normal active-section routing.

1. Sphere equality, `waitForRewardZone`, `rewardZone`, and `givingRewards` are diagnostics only. They never authorize reward input and never select a terrain section.
2. The last section observed in real active gameplay remains the routing/map/physics section during inactive transition frames only while its terrain-continuation epoch remains valid. A mutable controller index change does not clear a committed jump, wall target, speed latch, route calibration, or second-stage preview. Section-scoped reset occurs only when the next section produces a real active-gameplay frame.
3. Reward mode requires the same qualified native component on two distinct render frames: either an active, enabled, unhit `RandomBox`, or an active, enabled, unpicked `CollectableGameObject` whose exact reward type is Coin, Ruby, Sapphire, Emerald, Diamond, or Zynium. The object must have an enabled active collider and an enabled active SpriteRenderer with a sprite and nonzero alpha, and must lie in the player corridor `X=[-4,+45]`, `|Y|<=18`. `Renderer.isVisible` and object names are forbidden because background rendering and pooled names are unreliable.
4. A typed target latch persists after hit/despawn through its current reward interval. Its rising edge atomically releases terrain/wall input before minimum contextual jump pulses, arrows, and optional grounded Wind Dash begin. A typed target may latch even if the mutable active-gameplay flag changes late; confirmed object identity dominates native phase flags.
5. `BeginEpoch` retires every pending, latched, previously qualified, and currently scannable typed-target instance ID at a real section boundary or lifecycle revocation. A successful boundary snapshot plus one later complete inventory on a distinct render frame establishes the short stabilization gate; an unavailable/partial snapshot requires two later complete inventories on distinct frames. Every typed target visible during those quarantined inventories is retired, including the inventory that completes stabilization. After stabilization, a distinct non-retired ID may start its own two-frame `1/2 -> 2/2` latch without a global-empty gap. A retired ID becomes eligible again only after two consecutive complete inventories observe that ID absent.
6. Two consecutive successful complete scans whose reason is exactly `NoQualifiedActiveRewardTarget` remain an alternative global rearm proof. `OnlyRetiredEpochTargets` is not physically empty. A partial/failed scan cannot authorize positive evidence, prove absence, remove a retired ID, or advance the two-empty count. It clears the pending `1/2` proof and emits a rate-limited warning while terrain/lifecycle ownership remains fail-closed.
7. Confirmed pit/death, player replacement or unavailability, loss of supported-map eligibility, and global position discontinuity revoke the terrain-continuation epoch and stale reward ownership. Unverified lifecycle recovery requires the restart delay plus two distinct stable-ground physics steps with `|VY| <= 2.50`, a supported map, live player, remembered terrain section, and forward-gameplay evidence: `IsActiveGameplay=true` or `|VX| >= 1.0`. V0.49 allows the first such grounded step to resume immediately only for a confirmed pit or verified upward respawn teleport and only with moving VX or exact mapped-wall proof; it refreshes map identity before same-frame planning. Stationary unverified ground alone does not authorize recovery. Active gameplay adopts the current section, while inactive recovery retains the last active terrain section.
8. A first stable fixed-step contact may complete a landing only for an already prepared two-stage route onto a verified authored support no wider than `2.25`. The next plan is rescanned from live position and Rigidbody speed in the same control cycle. If a speed discontinuity barrier would consume the entire narrow support, the freshly recomputed plan may bypass that one-step wait; every target, hazard, and launch-window check remains required.
9. Ground 5 authored pillar surfaces S2/S3/S4 do not use V0.35's current-frame planned-face bypass. Their stable V0.34 behavior requires body-contact/VX-collapse readiness, preventing a wall pulse from firing while excessive upward velocity remains. The S4 lower-pickup sink arm window is measured from its target feet height, and its release deadline is simulated from live downward VY, gravity, and fixed delta (bounded to `2..8` steps). Authoritative elapsed steps and the deadline are derived from exact `Time.fixedTimeAsDouble`; raw `FixedStepSequence` delta is diagnostic only, so background render catch-up cannot shorten the sink.
10. `SphereSweepToLowerLanding` uses strict lexicographic selection: maximum predicted sphere hits, minimum hold, minimum flight time, then maximum landing margin. A blended margin score may not turn an equal-pickup candidate into a heavier jump. Normal active Section 3 Ground 7 collection exits cap the attached transfer at `0.165 s`; Spirit/high-speed transition exits retain the full `0.180 s` safety range.
11. MelonLoader automatically discovers the attributed Harmony patches; AutoBonusRunner must not call `PatchAll()` manually. The V0.35 audit confirmed that doing both installed duplicate callbacks, so one real `PlayerMovement.FixedUpdate` advanced the hold counter and feedback twice. The startup inventory must be exactly one owned `Update` prefix, one owned `FixedUpdate` prefix, and one owned `FixedUpdate` postfix (`1/1/1`). `JumpController` and `JumpPhysicsFeedback` also deduplicate exact `Time.fixedTimeAsDouble` values, without epsilon: each distinct catch-up physics tick counts, while duplicate callbacks at the same fixed time cannot advance a limit, release before the original integration, increment `FixedStepSequence`, or add a learning sample.
12. Fixed-step wall pulses report and learn their delivered physics duration as `held steps * fixedDeltaTime`, not shorter wall-clock time observed during render catch-up. A truncation warning is legal only when both clocks prove the delivered action was short.

Required V0.36 evidence includes `HarmonyAutoPatchInventory` with `1/1/1`, `RewardTargetObservation` with epoch/snapshot/retired-ID reasons, `RewardTargetFreshEpochPending/Latched`, `RewardTargetRearmEmptyScan` and `RewardTargetRearmBaselineEstablished` when the empty alternative is used, `TerrainContinuationEpochRevoked` where emitted, `PitDescentGuardReleased` with `ForwardGameplayResumed` and `TerrainEpochRearmed`, `RewardObjectPhaseLatched`, `RewardObjectOwnershipHandoff`, `RewardObjectGateMismatch` when native flags disagree, `UrgentNarrowLandingConfirmed`, `UrgentNarrowLandingBarrierBypass` when used, `Ground5NarrowPillarBodyGate=True`, the Ground 5 sink's fixed-time/deadline fields, the Section-3 sweep candidate list and selected hold, and wall-exit policy fields `NormalSection3CollectionExit`/`MaximumHold`. `FixedStepCallbackDeduplicated` should be absent with a correct `1/1/1` patch inventory; if present, it proves the defense-in-depth guard suppressed another callback installation defect.

## Historical V0.35 wall, pickup, and native-reward contracts

1. Probe the wall before applying the incoming-horizontal-speed return. A current-frame `Detected && Touching` hit may bypass speed/stall confirmation only when it belongs to the already planned wall or the currently armed downstream exit face. A predicted centre position or wide forward ray is never equivalent to physical contact.
2. Keep the nominal deep-entry clearance band for an on-time trench launch. After that launch window is irreversibly missed, re-solve from live X and allow a hazard-cleared below-lip physical-face contact with a relaxed minimum clearance. Label it `LateDeepTrenchPhysicalFaceSalvage`; it is a survival route and does not claim that the nominal deep pickup lane was preserved.
3. Restrict the Ground 5 raw-body exception to the exact same-piece S4 -> S1 exit. Carry the selected safe tolerance and `RawBodyFit` authorization through wall release into landing outcome scoring, then recompute actual player-footprint overlap with the planner's same minimum-width rule. Coincident raw bounds/top on the composite collider override a pooled prefab annotation mismatch, but do not override geometry checks.
4. `SphereSweepToLowerLanding` is eligible only in code Section 3 and only when a verified natural drop already exists. Enumerate holds using current VX and the current physics snapshot; require the predicted landing to remain within the same lower support and the trajectory to clear the current hazard. Score only spheres more than `1.15` units above source feet (passive body-overlap pickups do not count). Maximize pickup count, then prefer shorter flight/hold and greater landing margin.
5. A post-quota road latch requires current-run evidence that one specific section was first below quota and later reached quota. A native wait transition may corroborate unavailable quota data only when a readable `waitForRewardZone=false` baseline was captured after that incomplete proof and a later readable sample becomes `true`; a sticky level or an edge hidden inside an unreadable interval is insufficient. The completed section is always that proven incomplete section, never `previousSectionIndex`. A new-run section wrap and the first active frame of the next section form hard reset barriers, preventing stale totals from re-arming the latch.
6. While the proven road latch is active and neither `rewardZone` nor `givingRewards` has appeared, normal terrain routing continues using the completed section's map identity. `rewardZone` ends terrain ownership. Readable `givingRewards=true` directly authorizes reward control on the supported map even if quota data is temporarily unavailable.
7. On the `givingRewards` rising edge, release and clear any terrain jump/wall owner atomically. Then minimum contextual jump pulses, direct bow fire, and optional grounded Wind Dash may run. Reward flag read failures are fail-closed and rate-limited in the log. Coin/chest GameObject names are diagnostic corroboration only because pooled object presence is not authoritative.

Required V0.35 evidence includes `IncompleteQuotaObserved`, `RewardWaitFalseBaselineObserved`, `PostQuotaRoadArmed`, `RewardPhaseObservation`, `NativeRewardOwnershipHandoff`, `NativeRewardPhaseStarted`, `CompletionRewardAction`, `RewardObjects`, `ObservedPlannedFaceContact` or `ObservedWatchedExitFaceContact`, `LateDeepTrenchPhysicalFaceSalvage`, `SphereSweepToLowerLanding`, and the attempt result's expected/actual sphere counts and `LandingAcceptance[...]` fields.

## Historical V0.32 retry isolation (superseded by V0.42)

V0.33 leaves this retry isolation and every routing decision unchanged. Its run-level statistics use confirmed lifecycle and completion events only; statistics never feed back into control.

- `Auto Retry Enabled=false` is the default and selects the real popup No/cancel action after a failed Bonus Stage.
- `Auto Retry Enabled=true` historically selected the native Continue callback after a fixed one-second wait. V0.42 replaces that unsafe timing and direct callback with exact popup/UI/acknowledgement handling.
- The retry bridge remains outside route planning. The runtime now deliberately releases any stale jump/wall/reward input while the native modal owns control; it does not alter route parameters.
- Turning AutoBonusRunner off clears the pending prompt and leaves it available for manual interaction.

## Historical V0.29 post-quota completion state machine (superseded by V0.36)

V0.29 used `BonusMode observed -> current quota complete -> PreBonusMode -> normal terrain planner -> inferred terminal route -> reward actions`. V0.36 retains the useful terrain continuation but removes both terminal-route and native-flag authorization; only the confirmed typed reward-object latch moves Wind Dash, small jumps, and arrows into reward control.

- Navigation remains authoritative until the typed reward-object gate latches; missing terrain is never reward evidence.
- The old `HasNext=false`/three-invalid-fixed-step terminal inference is no longer executable behavior.
- The minimum pulse calls `JumpPanel.OnPointerDown` and `OnPointerUp` in the same frame, matching AutoJump's contextual short-jump behavior without referencing that mod.
- Bow fire calls `PlayerMovement.ShootArrow()` at a 0.10 second cadence only when the Sacred Book of Projectiles is unlocked and bow use is not disabled.
- Wind Dash is attempted only after the typed reward latch, when the main ability UI is visible, the selected ability is Wind Dash, it is unlocked and ready, and ground contact is stable. Failure is non-fatal; jump/arrow reward actions remain available.
- Current evidence records are listed in the V0.36 contract above; historical `CompletionTerminalCandidate` is obsolete.

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
13. Missing terrain, quota equality, native reward flags, and untyped pooled-object presence are never permission to send reward input. Only the two-frame qualified typed reward-target latch authorizes it.
14. The last section observed in real active gameplay remains authoritative through the inactive transition road even if the controller publishes the next section index early, but only while the current continuation epoch remains valid. Death, player replacement/loss, supported-map eligibility loss, or global position discontinuity revokes that authority. Rearm requires two stable fixed steps plus forward gameplay (`IsActiveGameplay` or `|VX| >= 1`), so safe moving inactive continuation is possible but a stationary respawn is not sufficient.
15. Epoch separation is instance-based. `BeginEpoch` retires old/current typed IDs; after complete-inventory stabilization, a distinct ID may latch `2/2` without a global-empty gap. A retired ID must first be absent from two consecutive complete inventories. A successful boundary snapshot requires one later quarantined complete inventory, while an incomplete snapshot requires two. Two complete physical-empty scans are the fallback global baseline. Partial scans cannot provide positive or negative proof.
16. One logical fixed step means one unique exact `Time.fixedTimeAsDouble` for the bound primary player, not one Harmony callback or one render frame.
17. Route selection and action proof must use the same scan, target, intermediates, objectives, and physics context. A candidate proved from a reduced world model is not executable.
18. `BonusSphere` is a collection objective; typed `SpiritBoost` is a speed transition. Never infer one from the other.
19. A final landing inside the target is insufficient when the body intersects its leading face earlier. Every physically separated face and safe-entry line must be checked along the trajectory.
20. A command that may change speed in flight must survive both speed histories at the same launch/hold and be revalidated from exact live X; it cannot be cached as a normal route lock or prepared successor.

## Inputs observed each control cycle

- Bonus-stage active state and game-state name.
- Map name and section index.
- Player instance identity.
- Player world position and rigidbody velocity.
- Grounded/contact state and collider bounds.
- Collected and required sphere counts.
- Remaining section timer.
- Fell-off/death state.
- Typed Spirit Boost state: current/maximum additive component, decay, native current/pre-step speed, verified base speed, trigger-scan completeness, and active uncollected `SpiritBoost` trigger bounds. This is separate from `BonusSphere` objectives.
- Native reward flags (`waitForRewardZone`, `rewardZone`, `givingRewards`) and whether all three were read successfully.
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

V0.27 originally used a `4 x 6` duration grid. V0.63 replaces it with the `9 x 9` surface-layer/native-step grid defined above after the complete V0.62 trace proved that `+3/+2` heights and `60/80 ms` holds were still being aliased. Calibration remains section-scoped and ordinary ground motion remains isolated from wall pulses. Learned observations correct the analytical curve; a coarse median may not replace it across materially different holds.

Ordinary ground takeoff velocity near `18.627` and wall reset velocity near `20` are separate populations. Only clean grounded-to-ground samples update the ground takeoff model. Wall-contact pulses may update a wall-specific model but must not enter the ground sample median. Large errors must be logged as evidence even when excluded from a stable estimator; silently rejecting every failed jump prevents correction.

Duration-grid ownership is stricter than general feedback eligibility: only an observed input-DOWN-to-confirmed-stable-landing interval writes a duration cell. Neither horizontal travel nor launch/start-speed is a proxy for elapsed flight time, so those observations update only their dedicated travel or speed estimators. This prevents one physical sample from contaminating the duration curve through multiple incompatible units.

The Harmony input/feedback bridge runs only for the primary `PlayerMovement.instance`. V0.26 allowed secondary player instances to contaminate capture. A separate V0.35 audit confirmed a second doubling mechanism: MelonLoader automatically installed the attributed patches, and AutoBonusRunner manually called `PatchAll()` again. The explicit call is removed; startup inventory must be `UpdatePrefixes/FixedPrefixes/FixedPostfixes = 1/1/1`. Exact per-player `Time.fixedTimeAsDouble` deduplication remains in the actuator and feedback bridge so one real integration advances `FixedStepSequence`, held-step limits, and learning exactly once.

Horizontal distance must never be computed from hold duration alone. It depends on live horizontal velocity, predicted flight duration, game movement behavior, Spirit Boost, section speed, contact offsets, and learned travel scale/bias.

For a wall route, latch horizontal speed from a reliable free-running sample before collision. Do not replace a latched value near `11.9` with a collision slowdown such as `2.36`. A verified upward speed transition associated with Spirit Boost is allowed to replace the latch immediately; ordinary downward collision transients are rejected and logged.

## Direct jump planning

Candidate holds are:

`0.020, 0.030, 0.040, 0.050, 0.060, 0.075, 0.090, 0.105, 0.120, 0.135, 0.150, 0.165, 0.180 seconds`.

Live-geometry code Sections `2` and later use only the exactly deliverable subset `0.020, 0.040, 0.060, 0.080, 0.100, 0.120, 0.140, 0.160, 0.180`. This avoids selecting a wall-clock duration whose physical actuator result belongs to a different native step bucket.

For each candidate:

1. Predict the vertical trajectory using current learned jump physics.
2. Solve when the descending body reaches the target top.
3. Integrate live horizontal travel over that flight time.
4. Apply calibrated travel scale and only maneuver-specific measured bias.
5. Require clearance from every retained intervening surface and hazard.
6. For any real gap, require body clearance at the target-left face and a safe vertical entry at `SafeLeft`, even when source and target tops are level.
7. Require predicted landing inside the target safe interval, with a preference inset of `0.30`.
8. Score the already-safe command by landing margin, predicted error, integrated normal soul value, and unnecessary airtime.
9. If a typed Spirit trigger may be crossed, replay this same launch/hold for both no-pickup and pickup-reset speed histories; both must pass steps 5-7.
10. Derive a launch position/window from the chosen target, flight time, and speed envelope. A future-transition proof is a singleton and must be revalidated at exact live X before DOWN.
11. Wait until the player reaches that launch proof; then press for the selected duration. Do not lock or publish a second-stage preview for a command whose speed can still change in flight.

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
4. For an unverified transition, wait for stable respawn support over two fixed steps plus the restart delay.
5. For a confirmed pit or verified upward respawn teleport, accept the first stable grounded fixed step with forward VX or exact mapped-wall evidence, refresh section/static identity immediately, and run the ordinary planner in that same control cycle.

This prevents the previous failure where the mod kept jumping randomly after death.

## Background input

`Application.runInBackground` is enabled. Jump state is injected through the native player jump path using `JumpController` and Harmony input patches. The mod must not depend on a foreground-only mouse click simulator. Manual left-click input remains available when automation is disabled, and manual jumps should still be observed for calibration.

Harmony patch ownership is automatic under MelonLoader. AutoBonusRunner never calls `PatchAll()` itself. Do not use `Time.frameCount` as the physics identity: background catch-up can execute several legitimate fixed ticks in one render frame. Exact double fixed time suppresses only repeated callbacks for the same tick and preserves every distinct catch-up integration.

AutoBonusRunner does not inspect, patch, configure, or suppress AutoJumpMod. The user owns input exclusivity and must turn AutoJumpMod off before testing this planner.

## Successful section continuation

`PreBonusMode` is not by itself a death or reward state. In current V0.36,
sphere quota and native reward flags are diagnostics only. The normal terrain
planner continues with `lastActiveTerrainSection` while the continuation epoch
is valid and no typed reward target has latched. It never sends a fallback
reward pulse merely because terrain is missing. Confirmed death, player
replacement/unavailability, or supported-map eligibility loss revokes this
epoch. Unverified recovery requires two stable-ground fixed steps plus either
active gameplay or reliable forward motion (`|VX| >= 1`). A confirmed pit or
verified upward respawn teleport may rearm on the first such grounded fixed
step and refresh the remembered map before same-frame routing. This allows a
moving inactive post-quota road to resume without authorizing a stationary
respawn. Reward jump/arrow/dash
ownership begins only after one eligible typed instance is confirmed on two
distinct render frames. `BeginEpoch` retires prior IDs, so a distinct ID may
latch without global emptiness; a retired ID requires a complete observed
absence, and two complete physical-empty scans remain the fallback baseline.

## Per-frame decision outline

```text
observe game and player state
if automation is disabled:
    release synthetic input
    observe manual jumps for diagnostics/learning
    return

if not in a supported Bonus Stage:
    revoke any terrain-continuation/reward epoch
    reset transient route state
    return

observe typed reward targets only when a terrain/reward epoch is authorized
on an epoch boundary, retire known and currently scannable target instance IDs
if the boundary inventory was incomplete:
    retire the first later complete inventory before qualifying its IDs
    quarantine one complete inventory after a successful boundary snapshot,
        or two after an incomplete snapshot; retire every visible ID there
    ignore retired IDs until two consecutive complete inventories observe each ID absent
    allow a distinct non-retired ID to qualify on two distinct frames only after stabilization
alternatively, accept two consecutive complete physical-empty scans as baseline
never use a partial scan to prove absence or advance the empty baseline

if death, pit, player loss/replacement, respawn, or position discontinuity is active:
    revoke continuation authority
    release input and run lifecycle stabilization
    if confirmed pit or verified upward respawn teleport:
        on first stable grounded fixed step with forward VX or mapped wall:
            rearm, refresh remembered terrain identity, and continue this cycle
    otherwise require two stable fixed steps plus restart delay to rearm
    return unless verified same-cycle takeover completed

if the same qualified reward target has been observed on two distinct frames:
    release terrain/wall ownership atomically
    run contextual reward jump, arrow, and optional stable-ground Wind Dash
    return

refresh live map registry
scan support, next surfaces, hazards, spheres, and wall contact
capture typed Spirit component/max/decay and active SpiritBoost triggers
keep SpiritBoost triggers separate from BonusSphere objectives
accept feedback only from the primary PlayerMovement instance
update free-running speed, rejecting collision slowdown; accept verified boost increases

if an action is already executing:
    advance its fixed-step state machine once per unique fixedTimeAsDouble
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
run one objective-aware selector and prove that same command against the full terrain scan
enumerate physics-valid candidates using current speed and calibration
for a possible SpiritBoost pickup, require both slow and pickup-reset trajectories to fit
if a nominal wall window is behind live X:
    re-evaluate all wall-approach holds from current state
if no verified action exists:
    log NoVerifiedRoute with full geometry; do not blindly jump
else if inside the computed launch window:
    revalidate target face, safe entry, intermediates, hazards, and exact live X
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

Essential fields include session, automation-authority boundary, section, fixed-step sequence, authoritative player instance, position, velocity, typed Spirit component/maximum/decay and trigger-scan state, separate `BonusSphere` objectives, registry generation, current/next/all intermediate surfaces with raw and safe bounds, piece names and local coordinates, obstacle kind, authored topology policy, hazards/objectives, selected maneuver, all rejected candidates and reasons, target-face and safe-entry checks, nominal and exact-live-X launch proof, target, planned and actual fixed-step hold bucket, predicted and actual flight/travel/apex/landing, no-pickup/pickup-reset travel envelope, future-transition/no-lock state, free-running and wall-latched speed, accepted/rejected calibration samples with typed boost kinematics, wall phase/contact, residual-rise wait state, mandatory-face-contact state, downstream-exit ownership, actual landing support, sphere delta, prediction error, and lifecycle reset reason.

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

- V0.63 has not yet completed its final local build/regression check and has no gameplay trace. Compilation cannot validate route quality; the next evidence must include one complete unassisted normal and one complete unassisted Spirit Stage-1 run.
- The normal `+3` pillar fixture must prove that a command either clears both the target-left face and safe-entry line or deliberately hands the exact face to wall control. A stable top landing, actual sphere delta, and two-fixed-step timing sample are separate outcomes.
- The Spirit same-level target sequence must prove a common slow/pickup-fast command. The eight repeated V0.62 deaths near target face X `1664` are the primary regression family; an endpoint inside the road is not enough if the body collides with its left face first.
- Typed `SpiritBoost` trigger scans, component/decay reads, live-X command revalidation, no-lock/no-preview behavior, and typed calibration rejection still need real-game evidence. `BonusSphere` count is objective evidence only.
- The shared selector must be checked for both survival and collection. Normal mode must not lose the final `+3` pillar souls, and Spirit mode must not skip an objective-bearing immediate surface to manufacture future speed.
- Ground 6 underpass, Ground 3/Ground 5 authored wall chains, reward ownership, retry choice, and respawn takeover remain regression surfaces even though V0.63 does not intentionally redesign their topology.
- Later maps remain less thoroughly demonstrated, and the large runtime still needs eventual subsystem separation.

## Deployment validity gate

The current source is intentionally not deployed. A V0.63 gameplay trace is valid only after an explicit user-requested/manual deployment and a startup block that loads `AutoBonusRunner.dll`, reports public `1.0.0`, internal `V0.63`, schema `11`, registers `AutoBonusRunnerRuntime`, and reports the expected owned Harmony inventory (`UpdatePrefixes=1`, `FixedPrefixes=1`, `FixedPostfixes=1`, plus the existing retry hooks). A matching DLL hash proves artifact identity only; it does not prove the routing redesign works.

See `MAP_REFERENCE.md` for full static geometry and concrete logged examples.
