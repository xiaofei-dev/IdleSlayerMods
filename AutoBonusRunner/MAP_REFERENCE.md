# AutoBonusRunner Map Reference

## V1.00 Stage-1 ordinary third-section cross

The retained detailed fixture
`AutoBonusRunner-20260723-132559-761-V0.93.log` records the route from source
`[1095.001,1102.000] @ -2` across narrow level stones near
`[1106,1108]` and `[1111,1113]` to target
`[1115.001,1121.998] @ -2`. Its live route inventory is the five-sphere
cross:

`(1106,2); (1107,1); (1107,2); (1107,3); (1108,2)`.

Attempt 40 predicted and collected four with a `0.120 s` Spirit command.
Those coordinates identify the fixture only. V1.00 recognizes the ordinary
route from relative widths, level topology, and the live unit-cross shape,
then requires the ordinary solver to intersect at least one sphere.

## V0.99 Stage-3 Spirit Ground-6 entry ownership

The V0.98 Stage-3 Spirit run entered code Section 2 at `15:32:15.960` and
fell four seconds later at player centre `(994.401,-4.731)` with
`VX=0`, `VY=-43.176`, and `DetectedWallHasNoPlannedWallRoute`. The pooled
translation is `+448` from the retained Section-2 entry
`[538,545] @ -2 -> [547,549] @ 0`, placing the real blocking face at
`x=995` and the stopped player centre exactly one collider half-width before
it. This is the authored `Ground 6/S3` narrow-pillar wall entry. V0.99 uses
that static role and a short-lived exact-contact credential; the absolute
coordinates remain diagnostic only.

## V0.89 retained Stage 1 Spirit fixtures

Current source target is public `1.0.0`, internal `V0.89`, schema `34`.
V0.89 adds no coordinate-specific route. The final V0.88 run supplies these
diagnostic fixtures:

- Section 0 boost-reset landings exceeded the trigger-time endpoint by
  `+0.592`, `+0.596`, `+0.622`, and `+1.244` world units. The common cause is
  the native FixedUpdate between DOWN and takeoff, now modeled in the live
  envelope.
- In Section 1, the visible boost at approximately
  `[496.77,498.27] @ [3.80,5.30]` was passed from below because platform
  selection had no boost-utility tier. V0.89 adds that tier without encoding
  the coordinate.
- Section 3's only death used the short raised target
  `[1845.00,1848.00] @ 1.00`. The contact began near X `1844.40` while the
  native controller reported `IsWalled=False`; this is retained as an
  execution-state fixture, not a map exception.

## V0.88 Spirit pickup ranking

The V0.88 source target was public `1.0.0`, internal `V0.88`, schema `34`.
V0.88 changes no authored geometry or coordinate rule. In the first two
sections, among commands that already retain the full landing-safety reserve,
Spirit mode ranks verified speed-boost trigger intersections before guaranteed
soul coverage. Later-section ordering is unchanged. Values come from the
existing slow/reset envelope and are logged as `ExpectedSpeedBoostHits`.

## V0.87 logging-only revision

V0.87 changes log transport, duplicate suppression, categories, and exception
reporting only. It adds no map coordinate, route exception, geometry rule,
landing envelope, jump duration, or wall transition.

## V0.86 requirement-mode revision

V0.86 adds no map coordinate or route exception. Its `Auto`, `Manual`, and
`Skip` modes change only the active section's effective native sphere
requirement. Spirit Boost recognition uses the controller's typed flag rather
than map geometry or movement speed.

## V0.85 configuration-only revision

V0.85 removes three always-on capability preferences and migrates their old
entries out of the configuration file. It changes no authored or observed map
geometry, route fixture, landing envelope, jump duration, or wall transition.

## V0.84 post-quota failure fixtures

The V0.83 trace contains two ordinary completed-section road fixtures. They
are diagnostic coordinates, never route triggers.

On Stage 3 Section 0, quota reached `38/38` near X `246.857`. No reward target
existed. The completion preview raised projected VX to `24.500` using
`CompletionImplicitBoostReset=True`; subsequent plans returned
`NoVerifiedLaunchWindow` and `RaisedLandingHasNoSafeDirectJump`, and the
runner died at X `256.29`. The Emerald later latched at player X `301.167`,
target X `327.79`.

On Stage 1, post-quota deaths occurred once after Sections 0 and 1, four times
after Section 2, and once after Section 3. At every death the reward target
was unqualified and unlatched. Representative terminal reasons were
`NoVerifiedLaunchWindow`, `RaisedLandingHasNoSafeDirectJump`, and
`SupportSurfaceNotFound:StaticMapPending`. The latter appeared after the
earlier no-jump decision had already allowed the body to leave its support.

V0.84 removes the implicit maximum-speed endpoint and the authored
completion-only selector. Until actual reward-object confirmation, these
positions are evaluated by the same terrain route profile as active gameplay.

## V0.83 Stage-2 Section-1 module and actuation fixtures

The retained V0.80 log contains four ordinary landing-error classes in the
second displayed section:

| Relative action | Hold | Planned X | Actual X | Error |
|---|---:|---:|---:|---:|
| broad entry -> one-unit touch | `0.060` | `501.520` | `501.669` | `+0.149` |
| one-unit -> three-unit | `0.020` | `508.286` | `508.334` | `+0.047` |
| three-unit -> low continuation | `0.020` | about `514.957` | `515.473` | about `+0.516` |
| Spirit broad entry -> three-unit | `0.080` | `507.666` | `508.327` | `+0.661` |

The errors prove that no single world-X or landing-bias patch is valid.
However, every input has a separately measured pre-takeoff step: about
`0.238` at VX `11.9` and `0.432` at VX `21.7`. V0.83 models that control
boundary before applying the existing hold/height travel model. For replay,
moving the Spirit input one fixed step earlier estimates X `507.893`, inside
safe interval `[506.746,508.253]`. The corresponding ordinary estimates keep
the wider targets safe; the one-unit touch moves to approximately `501.431`,
which has stronger raw body overlap and is immediately chained rather than
treated as a stable stopping platform.

The post-quota fixture begins on `[778.001,782.998] @ -2`, with a face-less
raised collider `[787,789] @ 8`, a one-unit low support `[789,790] @ -2`, and
the next three-unit support `[794,797] @ -2`. Attempt 82 planned constant VX
`12`, but native boost reset during flight and actual contact reached the wall
at X `794`. V0.83 treats the source/raised/low geometry as the same relative
low-corridor contact problem when the completion slow/fast envelope rejects
the one-unit landing. If the prediction still reaches X `794`, the prepared
successor `[794,797]` may own that exact face contact while the static registry
is pending. These values are replay fixtures only.

## V0.82 Stage-2 repeated Spirit low-corridor wall fixture

The retained V0.80 Spirit run repeated one translated module three times:

- current `[506.002,508.997] @ -2`, raised `[511.000,512.997] @ 8`,
  low continuation `[513.000,516.999] @ -2`, death at `512.401`;
- current/raised/low translated by `72`, death at `584.401`;
- the same topology translated by another `72`, death at `656.401`.

At the first copy, the prior flight landed at X `508.327` with VX `18.2`.
Every direct low-corridor jump predicted at least `9.35` travel and therefore
returned `NoVerifiedLaunchWindow`. The body still physically fit on the raw
source, and the measured raised face contact centre was approximately
`510.406`. V0.82 derives `Stage2LowCorridorWallCatch` from those bounds and
hands the measured raised collider to contact-confirmed wall recovery. The
coordinates are diagnostic fixtures only.

The same ordinary run reached `43/43` near the one-unit support
`[789.001,789.999] @ -2`. Completion acceleration changed VX from `11.9` to
about `20.1`; the next same-height support was `[794.000,796.998] @ -2`.
Minimum jump travel overshot it, while zero input reached its left face.
`Stage2NarrowSourceWallDrop` is derived from the narrow-source/nearby-face
geometry and never from these positions.

## V0.81 Stage-2 native-retry spawn fixture

After the single native Continue in the V0.79 trace, gameplay resumed at
`x=804.201` on `[801.004,804.998] @ -2` with actual VX `0` and retained free-run
speed `11.900`. The live downstream support was
`[827.000,854.652] @ -2`, gap `22.002`; the planner repeatedly produced
`Stage2UnmappedWallIntercept` with a `0.180 s` entry and about `9.982` travel,
but the generic stationary guard suppressed it.

These coordinates are a translated diagnostic fixture, not a route trigger.
V0.81 authorizes the action from the topology and plan class, then relies on
the unchanged physical-contact wall traversal.

## V0.80 Stage-2 Section-1 narrow transition fixtures

The V0.79 trace exposed two translated copies of the same live topology:

- source ending near `566.997`, one-unit transition `[573.001,573.996]`,
  then three-unit support `[578.000,581.000]`;
- source ending near `638.999`, one-unit transition `[645.000,645.999]`,
  then three-unit support `[650.000,653.000]`.

These coordinates are diagnostic fixtures, not route triggers. In both cases
the first jump reached the one-unit transition, but the old narrow-to-narrow
qualification prevented the prepared immediate continuation to the wider
support. V0.80 classifies both as the same geometry-derived narrow-source
handoff. The Stage-2 unmapped stepped wall added by V0.79 remains unchanged.

This document records the static Bonus Stage geometry currently known to the mod and representative world-coordinate observations from runtime logs. It is a debugging reference, not a hard-coded route script.

Current source target is public `1.0.0`, internal `V0.87`, schema `34`. Authored geometry is unchanged. V0.58 recognizes a level seam only when the measured player body can bridge it; representative Stage-1 seams are `1.001` units while inferred body width is about `1.188`. V0.60 keeps passively collectable ground-row souls from converting such a seam back into a jump. V0.63 introduced one objective-aware terrain command and a typed Spirit speed envelope. V0.64 first compares whether the complete slow/fast landing envelope is physically safe, then its worst margin, and only then continuation, guaranteed objective identities, distance, runway, and geometry. V0.65 corrects same-support soul utility without changing platform topology. V0.66 corrects only the unit system of the typed Spirit speed envelope. V0.67 searched earlier still-safe launch positions for ordinary Sections 0-2 only. V0.68 bounded ordinary alternatives. V0.69 bounded Spirit launch recovery and required stable top support. V0.70 changed planning cadence and equal-pickup launch ordering. V0.71 bounds live geometry cost and turns one verified boosted transient landing into a pre-planned wall continuation. V0.72 changes only repeated planning/collider cost and adds no map coordinate or route exception. V0.73 restores full slow-proof diagnostic records. V0.74 reduces equivalent-proof cost and adds no map coordinate, jump constant, or route exception. V0.75 restores MelonLoader debug delivery. V0.76 changes no map geometry: only Spirit Sections 0 and 1 may prefer more guaranteed same-support pickups after every compared landing retains the complete comfortable safety reserve. V0.77 adds the Stage-2 Spirit adjacent-wall approach. V0.78 adds geometry-derived low-corridor ownership beneath face-less raised Stage-2 pieces. V0.79 adds a bounded physical-contact traversal for Stage-2 stepped walls whose intermediate tops are absent from the live support graph. V0.80 permits the existing narrow-source urgent handoff to land on a wider verified successor. V0.81 permits that same bounded wall entry from a native auto-retry zero-VX spawn. V0.82 adds geometry-derived low-corridor contact and narrow-source passive-face recovery; pooled sphere refresh changes perception only. V0.83 adds a unified runtime-Section-1 domain, one-FixedUpdate actuation compensation, native completion-speed envelopes, and projected-wall physical correction. V0.84 restores pre-reward terrain-policy parity and requires positive typed trigger evidence for future completion acceleration. V0.85 changes configuration only. V0.86 adds no map behavior. V0.87 changes logging only. All world coordinates below are diagnostic fixtures for replay and log comparison. They are never route triggers or map patches.

The V0.87 source is built locally and is not deployed. V0.71's Section-3 transient-top wall continuation, the V0.64 landing-first safety gate, and V0.74's performance changes are unchanged. V0.76 Spirit Section-0/1 collection, V0.77's Stage-2 proactive wall contact, V0.78's Stage-2 low corridor, V0.79's unmapped stepped-wall traversal, V0.80's narrow-source handoff, V0.81's stationary retry entry, V0.82's pooled-object/contact transfer, V0.83's unified second-section control model, V0.84's pre-reward parity, and V0.86's requirement mode require gameplay validation.

## V0.79 Bonus Stage 2 stepped-wall fixture

The retained ordinary Section-2 trace lands automatically on
`[849.001,852.999] @ -2`, then sees only the downstream support
`[875.000,884.999] @ -2`. The 22-unit raw gap is beyond the roughly 9.9-unit
maximum direct ballistic travel. Manual control demonstrates the missing
topology without defining a coordinate rule: maximum entry from `X=852.201`,
exact physical wall contact at `X=863.201`, a `0.076s` pulse, a second
zero-speed step at `X=868.488`, a `0.111s` pulse, and landing at `X=879.260`.
V0.79 recognizes the general narrow-source/unreachable-level-successor shape,
then requires physical wall/stall evidence for every corrective input.

## V0.78 Bonus Stage 2 first-section low-corridor fixture

`AutoBonusRunner-20260722-220905-047-V0.77.log` is an ordinary Stage-2 trace. Four automatic deaths occurred before the final timer expiry: `(137.022,-3.808)`, `(151.082,-3.817)`, `(209.022,-3.808)`, and `(223.082,-3.817)`. Every death reported `NoForwardWallHit`.

The first repeated geometry is current `[130.003,134.996] @ -2`, raised `[139.001,140.996] @ 8`, and low continuation `[141,142] @ -2`; it repeats translated by 72 units as `[202.004,206.996] -> [211.001,212.996] -> [213,214]`. The second geometry is current `[146.002,148.998] @ -2`, raised `[151.000,152.996] @ 8`, and low continuation `[153,163] @ -2`; it repeats as `[218.003,220.998] -> [223.000,224.996] -> [225,235]`.

V0.77 classified the first pair as `WideWallTrench` and rejected clearance `[3.90,4.50]`; it classified the second pair as `WallAcrossGap` and rejected `[2.25,4.75]`. Both failures became authoritative `Wait` actions. V0.78 uses the absence of a measured runner-height `WallFaceX` and the immediately following level support to select the low route. These coordinates are validation fixtures only.

## V0.77 Bonus Stage 2 Spirit rising-stair fixture

`AutoBonusRunner-20260722-214756-500-V0.75.log` is the first complete Stage-2 Spirit capture. Progress after the automatic stalls was produced by the user's manual mouse input; it is demonstration data, not automatic success.

The first repeated module exposed a road ending near `[?,90.997] @ -2`, followed by narrow rising supports `[91.001,92.999] @ 0`, `[93.001,94.998] @ 2`, and a later lower continuation. A later module exposed `[108.374,116.999] @ -2`, then `[117.001,117.997] @ 2`, `[118.000,118.995] @ 5`, and the wider top `[119.000,128.999] @ 6`. The automatic planner repeatedly classified the near-zero first gap as `AdjacentWall`, returned `WallDropApproach` with hold `0`, failed to confirm contact, and logged `WallDropRouteAborted` near X `119.224`. Equivalent translated failures appeared near X `191`, `263`, `431`, `503`, `575`, `647`, `935`, `1007`, `1079`, and `1151`.

These positions identify a repeated topology only. V0.77 triggers from `map_bonus_stage_2`, typed Spirit state, live gap/rise measurements, and a verified face trajectory. Expected evidence is a nonzero `Stage2SpiritRisingStairContact` command followed by the existing wall-contact climb. Repeated `WallDropApproach Hold=0` without a preceding `ProactiveApproachRejected` record indicates the new build or map profile is not active.

## V0.71 Spirit fourth-section transient-top fixture

`AutoBonusRunner-20260722-195907-600-V0.70.log`, attempt `60`, observed current support `[1610.410,1625.999] @ -2`, immediate raised target `[1629.001,1632.000] @ 1`, lower continuation `[1640.000,1645.000] @ -2`, and tall face/top beginning near `[1645.000,1646.949] @ 10`. The immediate target safe corridor was only `[1629.745,1631.256]`. A `0.180 s` jump launched at X `1618.360`; the Spirit trigger reset velocity from `16.9` to about `26.7`, one grounded fixed step was observed at X `1631.122`, and the body left at X `1632.712`. It contacted the tall face near X `1646.401` at feet Y about `-2.135`, too late for an unplanned recovery.

This is a translated live-topology fixture, not a coordinate route. V0.71 keeps the successful first jump to the transient boost top. Its projected-support pass recognizes the trigger on that narrow source, resolves the recessed lower wall face behind the overhanging top, and prepares the boosted second jump. The actual one-step landing must produce `UrgentNarrowLandingFixedStepChained` and immediate `ApproachJumpThenWallJump`; waiting until the following frame reproduces the V0.70 fall.

## V0.70 repeated Section-0 planning fixture

In `AutoBonusRunner-20260722-192709-873-V0.69.log`, the Spirit runner moved from X `74.450` to `97.250` before the first soul-row launch. Across that interval the plan remained hold `0.180`, launch X approximately `97.256`, landing X approximately `105.101`, base VX `9.5`, and physics revision `253`. Eight logged plan records independently repeated the same nine holds and `SpiritLaunch3` probes; measured single proofs reached roughly `120 ms`, while the authoritative fixed-step proof was also duplicated by render planning. This is the V0.70 WAIT-cache fixture, not a static route.

The first attempt predicted four souls and collected five. Code Section 0 eventually met quota, but code Section 1 ended three short after many zero-pickup crossings and eight below-prediction attempts. When two comfortable Spirit launch positions guarantee the same positive pickup count, V0.70 selects the earlier X to retain left-side collection opportunity. The positions remain dynamically derived from the live target corridor and never become coordinate rules.

## V0.69 repeated Spirit Section-3 wall fixture

`AutoBonusRunner-20260722-185404-919-V0.68.log` contains two deaths at the same Bonus Stage 1 fourth-section feature: X `1718.401` and X `2174.401`, separated by exactly `456` world units. The preceding boosted landings were X `1702.986` and `2158.976`. Both next commands were `IntentionalDrop` predictions (`1712.178` and `2168.177`) lying left of their stable safe corridors (`[1712.594,1716.406]` and `[2168.595,2172.406]`). Both were accepted only through `RecoverableLeftFaceCatch`; observed contact arrived at X `1711.616` / `2167.609`, second-stage support was rejected, and death followed after `DetectedWallHasNoPlannedWallRoute`.

These translated positions diagnose one repeated shape but are not hard-coded. V0.69 changes the geometry contract: the Spirit drop must reach stable top support. If real motion still reaches the climbable face while below the pit threshold, actual touch/stall plus live mapped surface identity may rebase it into wall control. Expected evidence is either the stable support landing or `SpiritPitWallContactRecovered`; the old `RecoverableLeftFaceCatch -> SecondStageRejected -> PitDescentDetected` chain must disappear.

V0.68 introduces no new geometry fixture. It reuses the V0.67 ordinary early-soul fixture below, but authoritative evidence is now `SoulLaunchAnalytic` rather than a multi-sample `SoulLaunchSearch`.

## V0.67 ordinary early-soul launch fixture

`AutoBonusRunner-20260722-174627-106-V0.66.log` provides the representative fixture. In code Section 0, the seven-soul arc is `(97,3);(98,4);(99,4);(100,4);(101,4);(102,4);(103,3)`. The selected `0.180 s` route launched at X `97.220` even though its safe launch interval was `[94.757,97.256]`; the attempt collected five souls. The same late-lip geometry recurred at the translated rows beginning near X `169` and `241`. These positions prove the repeated shape and timing regression but are not coordinate triggers.

V0.67 may shift a Section 0-2 launch only inside the current live safe interval and only after repeating face, hazard, intermediate-surface, and landing-margin proofs. `SoulLaunchSearch[Original=...,Selected=...,Hits=...,Safety=...,SafeSamples=...]` is the authoritative evidence. Code Section 3 is excluded because its ordinary route was reported as perfect. Spirit routes are excluded until the normalized slow/fast envelope has its own V0.66 gameplay evidence.

## V0.66 Spirit Boost unit fixture

`AutoBonusRunner-20260722-172441-670-V0.65.log` is the authoritative failed Spirit fixture. It reports same-frame typed values `CurrentBoost=-5`, `MaximumBoost=750`, `Decrease=250`, and `NativeCurrentSpeed=470` beside observed Rigidbody `VX=9.4`. These values prove a `50:1` native-to-world conversion: the routed values must be `CurrentBoost=0` after inactive-sentinel clamping, `MaximumBoost=15`, `Decrease=5`, and `NativeCurrentSpeed=9.4`. This is a unit fixture, not a map coordinate or route exception.

Every route in that run was retained as `TopologyFailureRetained:SpiritBoostKinematicsUnavailable`, followed by `Reason=SpiritBoostKinematicsUnavailable` and no DOWN action. A valid V0.66 replay must instead expose `Kinematics=True` and `Evidence=TypedPlayerMovementFieldsNormalized[Scale=50.000,...]`. Any failure with these same raw values is a kinematics-read regression, not a landing, support-selection, or map-topology failure.

## V0.65 ordinary Bonus Stage 1 soul fixtures

`AutoBonusRunner-20260722-170102-952-V0.64.log` is a complete ordinary-only run. Section quotas were `13`, `28`, `33`, and `53`; all were reached. The first Section-2 life reached `31/33` before an unrelated route death and the retry completed the quota. Two apparent pickup shortfalls are expected quota caps: route `9` planned four while only three remained, and route `31` planned two while only one remained. They must log capped expectations in V0.65.

Repeated narrow-pillar fixtures expose the real scoring blind spot. Route `21` landed at predicted X `572.144` while the first active sphere was X `572.50`; route `26` had `643.948 -> 644.50`; route `30` had `715.958 -> 716.50`. The old `0.35` horizontal centre allowance reported zero hits even though the measured player half width is about `0.594`. V0.65 uses `0.60` and may use soul count to choose a hold only after the same target has at least `0.20` post-uncertainty landing reserve. These are collision-model fixtures, not coordinate branches.

## V0.64 appended-log routing fixtures

`AutoBonusRunner-20260722-110407-364-V0.62.log` was still being appended after its earlier inspections. The bounded V0.64 analysis snapshot ends at the latest complete run 29 (`16:37:28`), not at an arbitrary file tail. Ordinary runs 18 through 20 and run 28 were deathless; runs 21 through 23 completed with one death each. Spirit runs 24 through 27 completed with three, two, five, and four deaths respectively; run 29 completed with one death in code Section `1`. All run-25/26/27 deaths were in code Section `0`. Every one of runs 24 through 29 completed all four sections, but the Spirit fourth sections retained severe wall target/prediction errors even when they did not record a death there. Preserve the strong ordinary baseline while changing shared selection, and do not count the stale `FellOff=True` field after respawn as another death.

| failure class | representative evidence | V0.64 invariant |
|---|---|---|
| late unsafe endpoint | Spirit run 17 target safe `[160.744,161.256]`, predicted `161.572`, actual `162.470`, death near `166.63` | a late DOWN must already fit the safe centre interval; raw top bounds cannot authorize it |
| isolated safe drop, terminal next edge | Spirit run 15 preview reported `RaisedLandingHasNoSafeDirectJump`; drop landed near `300.063`, then died near `306.75` | a visible executable successor outranks a one-step landing with a known terminal continuation |
| wall lip/objective conflation | Spirit wall-top predictions around `1698.744/1914.744`; actual lower-support landings around `1717.600/1939.168` | physical lip starts horizontal motion; high-soul height is a soft objective and cannot veto a safe transfer |
| latest complete-run wall mismatch | run 26 route `1556` predicted upper-target X `1746.744`; actual stable landing was lower-support X `1765.600` (`+18.856`) | compare the physical slow/fast landing or face-contact margin before optional apex completion |
| repeated latest wall-exit drift | Spirit run 24 action `2172` planned from the wall target near X `1890`, then first stabilized near X `1915.468`, about `24.7` units farther right | wall transfer and face-intercept solvers must use physical lip timing while independently proving objective apex |
| completion speed floor | older paired log predicted with `BaseVX=11.9` while live motion remained near `16.9`, creating about `+1.35` landing error at the final support | successful completion traversal retains the verified section cruise floor |

Run-24 action `2172` is the decisive wall fixture. Contact feet were about `-1.057`; the physical lip required roughly `2.857` rise, while the highest soul implied `6.707`, above the modeled `6.212` maximum by only `0.495`. The old hard objective rejected every safe transfer before checking its landing and fell back to a `0.165 s` attached climb that predicted X `1890.744` but actually stabilized around X `1915.468`. V0.64 must therefore solve the physical landing first and report the missed high objective as `ObjectiveApexFallback`.

These coordinates are regression measurements only. The implementation uses live support geometry, current velocity, typed boost state, and a bounded continuation graph; none of the numbers above are route branches. Exact natural-fall first-contact topology remains a gameplay validation item for intentional drops.

## V0.63 complete-log regression fixtures

The paired normal/Spirit evidence file first used for V0.63 is `AutoBonusRunner-20260722-100018-956-V0.62.log`. It contains a complete normal run and then a complete Spirit run. `AutoBonusRunner-20260722-110407-364-V0.62.log` was still being appended during its first inspection; its later runs include Spirit evidence and are summarized in the V0.64 fixtures above. Do not infer file-wide mode coverage from an earlier prefix.

### Normal `+3` narrow-pillar contact

| field | complete-log evidence |
|---|---|
| route/action | route `54` |
| source | `[1159,1169] @ -2` |
| immediate target | `[1171,1173] @ 1` |
| planned command | hold `0.090 s`; predicted centre landing X `1171.935` |
| first physical result | left-corner contact at X `1170.866`, feet Y `1.184`, VY `-8.841` |
| run result | the normal section ended `26/28`; the narrow-pillar objective chain did not collect its final souls |

The first collision happened about `1.069` units before the predicted centre endpoint. This is why target fit must be evaluated at the target-left face and again at `SafeLeft`, not only at the final landing X. The floating height delta around `+2.995/+2.996` is also a calibration fixture: a one-step corner latch near `0.396 s` is not a stable `+2` landing sample near `0.556..0.563 s`.

### Spirit same-level target-face failures

| field | first translated failure |
|---|---|
| action | `A119` |
| live launch | X `1648.262`, VX `16.9` |
| retained intermediate pillar | `[1653,1656] @ 1` |
| selected same-level road | `[1664,1668.999] @ -2` |
| planned command | hold `0.180 s`, travel `18.329`, predicted landing X `1666.590` |
| actual result | zero predicted pickups occurred; VX stayed `16.9`; death at approximately `(1663.401,-4.169)` |

The centre X `1663.401` is approximately the target-left face X `1664.000` minus the player half-width. This is a deterministic leading-face collision, even though source and selected target have the same top. The same geometry/failure repeats at `+72` translations in actions `A124`, `A129`, `A134`, `A137`, `A140`, `A145`, and `A150`. V0.63 therefore applies face and safe-entry validation to every separated target rather than only to positive-rise targets.

### Spirit boost identity

The active speed pickup is a typed `SpiritBoost`, not a `BonusSphere`. One diagnostic object path in the failing section ends in `Spirit Boost@(1654.50,1.50)`; another observed translated instance was near `(575.68,3.79)`. `PlayerMovement.currentSpiritBoost`, its maximum component, and its decay provide speed state. `BonusSphere` positions and count provide collection objectives only. V0.63 evaluates the same launch/hold under both no-pickup/slow and typed-trigger-reset/fast trajectories, records the resulting travel interval, and revalidates the exact live launch X. These object/world positions identify the captured fixture only and must not appear as hard-coded routing conditions.

The older successful direct-pillar evidence remains useful: from roughly `[1637,1650] @ -2` to `[1653,1656] @ 1`, `0.100 s` at VX `16.9` landed near X `1654.4`. It proves the immediate pillar route can be physically valid; it does not authorize skipping the pillar or assuming a future speed reset.

## V0.61 first-section edge-contact fixtures

The deployed V0.59 Spirit trace contains two translated instances of the same first-section failure. These are regression fixtures for action ownership, not coordinate triggers:

| attempt | expected support | first grounded contact | raw body overlap | prior trajectory state | result in V0.59 |
|---|---|---|---|---|---|
| `98` | `[192.001,195.000] @ -3` | X `191.681`, feet `-3.088`, VY `+1.728`, collision VX `2.590` | `0.273` | invalidated at error `(-0.770,-0.364)` | native rebound, no next DOWN, death X `197.663` |
| `101` | `[264.001,267.000] @ -3` | X `263.729`, feet `-3.060`, VY `+2.543`, collision VX `4.451` | `0.323` | prepared next geometry retained | native rebound, no next DOWN, death X `269.406` |

Both expected supports are raw width `2.999` and safe width about `1.511`. The live centre is still left of `SafeLeft`, so a centre-only scan is expected to fail even though the collider is physically on the target. The next routes are already present in the projected support graph: `[192,195] -> [199,213]` and `[264,267] -> [271,285]`. V0.61 may use those projected pairs only after the live body/top contact proves the expected current support, then must recompute the continuation at retained pre-impact speed and revalidate its landing and hazard corridor.

## V0.60 clarified Stage-1 live geometry

The user's screenshot identifies the leftmost vertical opening in the Spirit fourth section as the first raised-pillar transfer. The deployed V0.59 trace repeats the same translated geometry successfully; these four representative attempts are from its latest Spirit run:

| Attempt | source road | target pillar | command | observed result |
|---|---|---|---|---|
| `253` | `[1637.022,1649.999] @ -2` | `[1653.000,1655.999] @ 1` | `0.100 s`, `VX=16.9` | safe landing X `1654.496` |
| `257` | translated +72 | translated +72 | `0.100 s`, `VX=16.9` | safe landing X `1726.851` |
| `263` | translated +144 | translated +144 | `0.100 s`, `VX=16.9` | safe landing X `1798.518` |
| `281` | translated +648 | translated +648 | `0.100 s`, `VX=16.9` | safe landing X `2302.825` |

This is `Gap≈3.002`, `Rise=3.000`. Three active spheres sit above the target pillar; the opening below contains no lower-route objective. The direct top landing is locally accurate and remains authoritative in V0.63. A later road or wall is replanned only after the real pillar outcome and any actual pickup speed change have been observed.

The displayed third section contains repeated level seams such as `[1110,1118] @ -2` to `[1119,1126] @ -2`. The physical gap is `1.001`; the inferred body width is `1.188` and the derived walkable limit is `1.088`. The active row at `(1121.5,-1)`, `(1122.5,-1)`, `(1123.5,-1)` is only one unit above the road and is passively collectible. The separate pink reward diamond in the screenshot is not returned by `GetActiveSpherePositions`, which requires an active uncollected `BonusSphere` component in the current section root. These observations are the V0.60/V0.61 regression fixture: expected terrain action is `WalkableMicroGap`, `PassiveWalkObjectives=3`, `ElevatedObjectives=0`, and no jump input.

## V0.59 Spirit Section-4 fixed-death observation

The V0.57 trace records this live sequence during Attempt 67:

| Role | Observed world surface/state |
|---|---|
| prior lower launch road | `[1710.001,1722.000] @ -2.000` |
| narrow upper landing | `[1725.001,1728.000] @ 1.000` |
| intended natural-drop support | `[1736.000,1741.000] @ -2.000` |
| immediate tall wall/top | `[1741.001,1744.999] @ 10.000` |
| first short contact | `X=1735.533`, projected second stage rejected |
| authoritative live re-scan | `X=1736.207`, `VX=24.800`, `VY=+9.740`, `Grounded=True` |
| planned wall route | arm X `1740.176`, face-contact centre X `1740.407`, gap `0.001`, rise `12.000` |
| native no-input rebound | takeoff X `1736.701`, apex X `1740.103`, `InputHeld=False` |
| failure | death at `(1742.401,-4.062)`, `DetectedWallHasNoPlannedWallRoute` |

These coordinates are diagnostic evidence only. The correction matches the live source/target geometry, the rising Grounded collision pulse, current speed, and bounded time-to-face; it does not recognize a world X or force the historical upper route.

Historical V0.30 deployment identity is length `380928`, SHA-256 `1A7607B4A4D639DA053AC52E69549A19FF40D9660F1C02A66C81E631B63021C2`. V0.31 is synchronized in all four DLL locations at length `394240`, SHA-256 `A1D575CB397EB60B75C9A019C4FBEA0AF27E620BC1B43FA3F2F3AD8270C72EE1`.

Historical V0.35 deployment identity is length `435200`, SHA-256 `BD6F55DFC34D3B40563EAC1015072055A5F8BA686A58FBB272A9C581AA159495` for its checked build/deployment artifacts. `AutoBonusRunner-20260722-020550-519-V0.43.log` is a valid deployed V0.43 trace and is the evidence source for V0.44. V0.44 is synchronized across the build artifact and all three installed compatibility locations at length `563712`, SHA-256 `5BD44DC06248C6BA2886462D168770BA9740A84B1489B19A41B6DFD4FCF96ECC`; it still requires a fresh gameplay trace.

V0.45 is synchronized across the build artifact and all three installed
compatibility locations at length `565760`, SHA-256
`2F490E7E7F461C93F6D586CC497E687172292EFD148D7BD6A66EB9A2DDC5B8C1`.
Its slider helper changes no authored geometry or route contract.

## V0.46 post-quota observations

The V0.45 trace `AutoBonusRunner-20260722-025127-993-V0.45.log`
repeats this Section-0 completion geometry four times:

| Role | Observed world surface |
|---|---|
| narrow source | `[270.001,271.999] @ 2.000`, Ground 1 S4 |
| unreachable immediate lower support | `[274.000,276.999] @ -2.000`, Ground 1 S1 |
| first dynamic alternative | `[282.000,287.000] @ 0.000`, Ground 2 S2 |
| later alternatives | `[292.000,295.000] @ 0.000`, Ground 2 S3; `[297.000,300.000] @ -2.000`, Ground 2 S1 |

At player X `270.934`, live VX `20.500`, the immediate lower support had
predicted travel `14.03..17.99` for holds `0.020..0.180`, so no safe launch
intersection existed. Replaying the same physics snapshot against Ground 2 S2
produces safe intersections for short holds and a predicted landing near
`283.7`; the intervening lower top is crossed while airborne. These world
coordinates are evidence only. Pool origins and registry identity remain the
runtime authority.

In Section 1, three V0.45 completion deaths ended near X `713.5` after the X
`699` wall was climbed successfully. The selected nearest exit road could not
contain the measured post-lip VX around `20.4`. V0.46 therefore authorizes the
existing ordered static wall-exit candidate solver during completion rather
than changing wall pulse timing or encoding an X-specific exit.

The V0.46 trace `AutoBonusRunner-20260722-035413-938-V0.46.log` refines that
geometry. After quota `33/33`, the last Ground 5 surfaces and the following
platform were:

| Role | Observed world surface |
|---|---|
| attached final narrow wall | `[699.000,701.000] @ 7.000`, Ground 5 S4 |
| immediate lower road | `[703.001,709.999] @ -2.000`, Ground 5 S1 |
| recoverable downstream face/top | `[714.000,718.000] @ 4.000`, Ground 3 S2 |
| later surfaces | `[721.000,726.000] @ -2.000`; `[730.000,732.000] @ -2.000` |

At feet Y `4.071` and post-lip VX `20.600`, no hold landed on the immediate
road or the downstream four-unit platform. The platform's left face was still
reachable: exact fixed-step replay accepts holds `0.150..0.180 s` and selects
`0.165 s`, with target-centre contact X about `713.406` and feet Y envelope
about `1.15..2.52`. These coordinates are diagnostic evidence only. V0.47
discovers the candidate from the current static registry and validates gap,
height delta, speed, player width, and finite face window at runtime.

## V0.48 completion failure observations

The V0.47 trace `AutoBonusRunner-20260722-041707-374-V0.47.log` adds three
runtime observations without changing authored geometry:

| Mode | Wall/source | Intended result | Actual evidence |
|---|---|---|---|
| Spirit, Section 0 | `[263,265] @ 2` | land on `[270,272] @ 2` at predicted X `271.100` | contact VX `15.5`, planned VX `17.2`, actual lip VX `22.4`; overflew the narrow top and died at X `278.69` |
| Spirit, Section 1 | `[699,701] @ 7` | meet `[714,718] @ 4` left face at predicted feet Y `2.494` | planned VX `18.936`, actual post-lip VX decayed from `17.2` to `12.2`; crossed target X below feet Y `-3` and died at X `713.78` |
| Ordinary, Section 1 | rising contact at `[699,701] @ 7` | continue attached climb | transient `(698.548,6.869)`, VX `5.463`, VY `12.976`, `Grounded=true` was misread as ground ownership; no executable DOWN followed and death occurred at X `714.15` |
| Ordinary control failure, Section 1 | `[699,701] @ 7` | `0.180 s` top transfer to `[714,718] @ 4`, predicted X `715.016` at planning VX `19.9` | latched VX `13.3` decayed to `11.9`; exact nine-step press ended in the pit at X `711.60`, proving slow-end infeasibility rather than actuator truncation |
| Ordinary control success, Section 1 | physical face `[714,718] @ 4` | recover any imperfect transfer by authoritative collision | face was detected at X `713.401`, feet Y `-0.766`, VY `-35.260`; the existing bounded wall controller climbed it and the run remained deathless |

These positions remain replay/debug anchors only. V0.48 derives every target
from the current registry and uses the live speed/deceleration envelope.

## V0.49 respawn timing observations

The same V0.47 trace proves that the old lifecycle guard can miss a complete
launch window after the game performs its own protector recovery. A Spirit
death was confirmed at `(278.689,-4.326)` and did not release control until
`(291.864,0.005)`, `0.901 s` later, after fourteen stable fixed steps. An
ordinary death at `(711.600,-5.100)` released at `(733.854,-2.001)`, `1.616 s`
later. The player instance did not change; native recovery moved the body and
applied forward speeds around `23..26` while the mod retained no input.

A separate manual sample observed an upward discontinuity of `+9.573` from a
reconstructed prior Y of approximately `-6.273` while native `FellOff=true`.
This is sufficient to identify a respawn teleport even when the pit detector
did not own the preceding manual fall. V0.49 does not encode these X positions.
It uses only confirmed pit physics or the upward-teleport signature, then
requires the first real grounded fixed step plus live forward VX (or exact
mapped-wall contact), refreshes the remembered terrain section, and runs the
normal planner in the same control cycle.

## Coordinate conventions

- `X` increases in the running direction.
- `Top` is the world or local Y coordinate of a standable horizontal surface.
- A surface is written as `[Left, Right] @ Top`.
- Template coordinates are local to a live `Ground N` clone.
- World coordinates are calculated as `world = piece origin + local`.
- Authored pieces normally have origins separated by `24.0` world units.
- Pooling moves and reuses clones. Therefore, a world X value seen in one run is not a permanent piece ID or a safe hard-coded trigger.
- `RegistryGeneration` changes when the section, active clone set, or clone transforms change. A change triggers revalidation but does not by itself invalidate an unchanged route, because recycling a remote pooled clone can advance the global generation. Validate the target by nonzero piece instance, piece origin, authored surface role, and current geometry; generation equality is diagnostic.

## V0.36 runtime identity boundaries

- A mutable controller section index is not a map-identity boundary during an inactive transition. The last section seen in real active gameplay remains authoritative only while its continuation epoch is valid.
- Confirmed pit/death, player replacement or unavailability, loss of supported-map eligibility, and global position discontinuity revoke that epoch. Unverified recovery requires two stable grounded physics steps after the delay, `|VY| <= 2.50`, a supported map/live player/remembered section, and forward-gameplay evidence. A confirmed pit or verified upward respawn teleport may instead rearm on the first such grounded step when live `|VX| >= 1.0` or exact mapped-wall contact proves gameplay can accept control; it refreshes the remembered terrain map before same-frame planning. Player replacement/loss and unsupported-map transitions retain the conservative path.
- A typed reward target can take ownership only after the same eligible instance is observed on two distinct render frames. `BeginEpoch` retires pending, latched, previously qualified, and currently scannable IDs. A successful boundary snapshot plus one later complete inventory on a distinct render frame establishes the stabilization gate; if the snapshot is incomplete, two later complete inventories on distinct frames are required. Objects visible during those quarantined inventories are retired, including the scan that completes stabilization. After stabilization, a distinct non-retired ID may latch `2/2` without a global-empty gap. A retired ID becomes eligible only after two consecutive complete inventories omit it. Two consecutive complete scans with no physical candidate are the alternative baseline. Any partial scan fails closed for both positive and negative evidence; `OnlyRetiredEpochTargets` cannot prove emptiness.
- Map/action evidence uses real physics ticks. MelonLoader automatically installs the attributed Harmony patches, so AutoBonusRunner does not call `PatchAll()`. `HarmonyAutoPatchInventory` must report one owned `Update` prefix, one `FixedUpdate` prefix, and one `FixedUpdate` postfix (`1/1/1`). Exact `Time.fixedTimeAsDouble` deduplication prevents a repeated callback from advancing a hold, `FixedStepSequence`, or learning, while preserving distinct background catch-up ticks.

## Authored section cycles

The active clones must form a contiguous cyclic run of the relevant authored sequence. During pooling, fewer or one more than the nominal number of clones may temporarily be active.

| Section index | Authored cyclic sequence |
|---:|---|
| 0 | `Ground 1 -> Ground 1 -> Ground 2` |
| 1 | `Ground 3 -> Ground 4 -> Ground 3 -> Ground 5` |
| 2 | `Ground 6 -> Ground 7 -> Ground 6` |
| 3 | `Ground 7 -> Ground 8 -> Ground 7` |

The level root is expected at `All Pools/Bonus Map Level {sectionIndex}`. The visible terrain is contributed to the shared `All Pools` `CompositeCollider2D`, so collider ancestry cannot identify the originating piece. `BonusMapPieceRegistry` resolves that identity from live clone transforms and templates.

## Static local landing surfaces

### Ground 1

| Surface | Local interval | Local top |
|---:|---:|---:|
| 0 | `[-12, -10]` | `-2` |
| 1 | `[10, 12]` | `-2` |
| 2 | `[-8, -6]` | `2` |
| 3 | `[-1, 1]` | `2` |
| 4 | `[6, 8]` | `2` |

### Ground 2

| Surface | Local interval | Local top |
|---:|---:|---:|
| 0 | `[-12, -11]` | `-2` |
| 1 | `[9, 12]` | `-2` |
| 2 | `[-6, -1]` | `0` |
| 3 | `[4, 7]` | `0` |

### Ground 3

| Surface | Local interval | Local top |
|---:|---:|---:|
| 0 | `[-12, -10]` | `-2` |
| 1 | `[10, 12]` | `-2` |
| 2 | `[-6, -2]` | `4` |
| 3 | `[1, 6]` | `4` |

Ground 3 is not described completely by its four standable tops. For a clone at origin `O` in Section 1, the route-relevant authored geometry is:

| Element | Local/world-relative geometry | Meaning |
|---|---|---|
| Incoming merged lower road | `[O-17, O-10] @ -2` | Stable source before the first floating body |
| First floating body | top `[O-6, O-2] @ 4`, solid Y `[-1,4]` | Its leading face can be contacted only while the body still vertically overlaps this finite wall |
| First-face spheres | `(O-6.5,1.5)`, `(O-6.5,2.5)` | Collect during the first wall approach/climb |
| Second floating body | top `[O+1, O+6] @ 4`, solid Y `[-1,4]` | Same-height successor whose leading face bounds the authored objective trench |
| Objective trench spheres | local `(0,0.5)`, `(0,1.5)`, `(0,2.5)` | High contact must descend through this lane before climbing out |
| Trench spikes | local `(-0.5,-1.25)`, `(0.5,-1.25)` | Lower bound for the attached descent; never descend blindly |
| Exit spheres | local `(7.5,2.5)`, `(8.5,2.5)` | Collected on the exit side of the second body |
| Downstream merged lower road | `[O+10, O+17] @ -2` | Broad terminal target after the trench/climb sequence |

Intended Ground 3 route graph:

`incoming road -> active descending hop -> first-face contact -> bounded pulses/first two spheres -> mandatory second-face contact -> attached released descent through three objective spheres -> bounded contact pulses -> downstream lower road`

A passive drop from the incoming road is invalid because the first body ends at local Y `-1`; the player can fall below it before horizontal contact. A top landing on surface 3 is also invalid while the lower lane remains active, because it bypasses the required face contact and attached descent. Conversely, immediately pulsing upward on high contact with the second face is invalid because it skips the authored objective lane.

### Ground 4

| Surface | Local interval | Local top |
|---:|---:|---:|
| 0 | `[-12, -7]` | `-2` |
| 1 | `[-5, -3]` | `-2` |
| 2 | `[-1, 1]` | `-2` |
| 3 | `[3, 5]` | `-2` |
| 4 | `[7, 12]` | `-2` |

### Ground 5

| Surface | Local interval | Local top |
|---:|---:|---:|
| 0 | `[-12, -7]` | `-2` |
| 1 | `[7, 12]` | `-2` |
| 2 | `[-5, -3]` | `0` |
| 3 | `[-1, 1]` | `4` |
| 4 | `[3, 5]` | `7` |

For a Ground 5 clone at origin `P`, the standable tops belong to pillars whose solids extend down to local Y `-10`:

| Element | Local/world-relative geometry | Intended behavior |
|---|---|---|
| Incoming merged lower road | `[P-14, P-7] @ -2` | Walk/passively enter the first gap |
| Pillar 1 | `[P-5, P-3] @ 0`, solid to Y `-10` | Establish physical leading-face contact and pulse |
| Pillar 2 | `[P-1, P+1] @ 4`, solid to Y `-10` | Preserve chain ownership; contact and pulse again |
| Pillar 3 | `[P+3, P+5] @ 7`, solid to Y `-10` | Final high pillar/contact or verified top landing |
| Lowest S4 pickup | sphere centre near `(P+2, 3)`, trigger approximately `1 x 1` | While descending on the S4 face, feet must reach about `P.y+3.42`; release for at least one real physics step, then stop on pickup/band confirmation or a dynamically predicted `2..8`-step double-fixed-time deadline |
| Exit road | `[P+7, P+14] @ -2` | Verified downstream landing/drop target |
| Vertical lanes | around local X `-2` and `+2`, with authored spikes below | Traverse by bounded face-contact decisions, not direct long jumps |

Intended Ground 5 route graph:

`incoming road -> passive gap entry -> pillar-1 contact/pulse -> pillar-2 contact/pulse -> pillar-3 contact or top -> explicit S4-to-downstream-exit transition -> verified exit road`

These pillars are vertically deep enough for passive entry and sequential contact. Do not reuse Ground 3's finite floating-body approach or attached objective-descent rule here. Surface 4 is not terminal: its nearest authored forward continuation is the broad road `[P+7,P+14] @ -2`, nine units lower. That road must remain a named exit target through a surface-4 face-contact fallback.

The S4 pickup sink is timed from `Time.fixedTimeAsDouble`, not render elapsed time and not the raw callback count. At arm time the runtime integrates live downward VY and gravity using the current clamped fixed delta, selects a `2..8`-step safety budget, and stores an absolute double fixed-time deadline. Later observations reconstruct real elapsed physics steps from the double-time delta. `RawSequenceDelta` is diagnostic only; `BackgroundCatchUpObserved=True` records that more than one real physics step elapsed between render observations without shortening the sink.

### Ground 6

| Surface | Local interval | Local top |
|---:|---:|---:|
| 0 | `[-12, -9]` | `-2` |
| 1 | `[-7, 3]` | `-2` |
| 2 | `[10, 12]` | `-2` |
| 3 | `[-9, -7]` | `0` |
| 4 | `[0, 3]` | `4` |
| 5 | `[-10, 0]` | `6` |
| 6 | `[5, 10]` | `6` |
| 7 | `[4, 10]` | `12` |

Ground 6 S1 `[-7,3] @ -2` to S6 `[5,10] @ 6` is not a direct eight-unit wall approach. S4 `[0,3] @ 4` and S5 `[-10,0] @ 6` overlap the airborne corridor and intercept the character body. The executable route is underpass/contact: stay unpowered below the overlap, enter the two-unit gap, and jump only after physical contact with the S6 face. Optional same-surface sphere collection cannot override this topology.

### Ground 7

| Surface | Local interval | Local top |
|---:|---:|---:|
| 0 | `[-12, -1]` | `-2` |
| 1 | `[11, 12]` | `-2` |
| 2 | `[-1, 1]` | `0` |

### Ground 8

| Surface | Local interval | Local top |
|---:|---:|---:|
| 0 | `[-12, -6]` | `-2` |
| 1 | `[9, 12]` | `-2` |
| 2 | `[-6, 9]` | `2` |

For a Ground 7 clone at origin `O`, the narrow wall is S2 `[O-1,O+1] @ 0`. Its immediate same-height/lower exit can merge across a piece seam; the following Ground 8 S2 is a broad `[P-6,P+9] @ 2` support. Exit choice is speed-dependent. Normal wall-contact demonstrations with post-lip VX near `16.9` traveled about `9.7-15.1` units with a native-cap jump. Spirit exits can travel about `19.2` units from a `0.075 s` press. Therefore the nearest static road is not automatically the executable landing: retain ordered candidates and solve them using attached/lip/post-lip speed phases.

## Registry tolerances

| Meaning | Value |
|---|---:|
| Expected piece stride | `24.00` |
| Piece stride tolerance | `0.30` |
| Transform change tolerance | `0.03` |
| Static surface match tolerance | `0.12` |
| Piece bounds tolerance | `0.20` |

The platform scanner samples at `0.15`, uses a same-surface tolerance of `0.30`, looks `8` units behind and dynamically looks `28` to `80` units ahead. It retains drops up to `15.25`, directly reachable rises up to `5.35`, and wall-climb candidates up to `12.0`. Static surfaces with equal tops that meet at prefab seams are merged.

## Representative observed world geometry

These values came from runtime logs. They demonstrate repeating topology and provide regression examples, but must not be treated as permanent route coordinates.

| Context | Observed surfaces or action |
|---|---|
| V0.30 Section 2 Ground 6 underpass failure | world route `[1073,1083] @ -2 -> [1085,1090] @ 6`; the bad `0.090 s` jump began near X `1077.480`, collided with the S4/S5 overlap (`VX=-1.281`), and relanded at X `1082.097`. This proves route topology was wrong even though requested and physical hold agreed. |
| V0.30 Section 3 normal automatic Ground 7 exit | wall `[1823,1825] @ 0`, downstream exit `[1835,1842] @ -2`; a staged `0.075/0.076 s` press traveled `11.448` and fell at X `1833.649`, just before support. Manual native-cap examples from VX `0` after contact traveled about `9.713-15.137`. |
| V0.30 Section 3 Spirit Ground 7 exit | one staged `0.075 s` wall press traveled `19.201` although the old model predicted `1.544`; the pre-contact boost speed decayed after lip clearance, so neither attached VX `0` nor a constant boosted speed is a valid whole-flight model. |
| V0.33 Section 2 Ground 6 contact stall | S0 `[1020,1023] @ -2` to adjacent S3 `[1023,1025] @ 0`; the player stopped at X `1022.201`, live VX became `0`, and the old gate deferred repeatedly for about `23.88 s`. A manual `0.104 s` press using the retained `14.4` run speed moved `2.598` units and landed at X `1024.799`. |
| V0.33 Section 2 Ground 7 short exit | wall `[1055.001,1057.000] @ 0`, downstream `[1067.000,1069.999] @ -2`; lookahead prepared `0.150 s` and X `1068.500`, but the contact handoff discarded it. The wall solver's `0.075 s` staged pulse predicted X `1064.141` and died before safe-left `1067.744`; a native-cap candidate predicted X `1066.767`, whose player footprint still overlaps the raw support. |
| V0.33 Section 1 Ground 5 highest-pillar pickup | S4 world `[603,605] @ 7`, lowest remaining sphere Y `3.000`; contact feet Y `3.573` with VY `-11.657` was reset upward immediately, leaving the run at `5/6` route pickups. The authored correction target is feet Y about `3.420` before the next pulse. |
| V0.34 human-level-2 one-frame contact losses | Automatic deaths near X `522.59` and `666.48` each exposed the intended face for one frame with `Detected=True`, `Touching=True`, and native wall contact while incoming VX was still above the old attachment gate. The next physics frame had VX `0` but no retained ray. This is an execution-order race, not missing map geometry. |
| V0.34 human-level-2 late Ground 5 exits | Automatic deaths near X `640.68` and `616.68` followed a missed highest-pillar launch. Candidate face clearances around `3.10..3.38` were physically below the verified lip but rejected by the nominal `3.90..4.50` deep-pickup band. The late salvage keeps the same verified face/hazard contract but is not credited as the nominal objective route. |
| V0.34 Ground 5 S4 -> S1 raw-fit evidence | Spirit exit target `[631,638] @ -2` had preferred safe-right `637.256`; the body landed stably near X `637.348`, only `0.092` beyond the extra safe inset. Raw-body authorization is therefore limited to the exact same-piece S4 -> S1 continuation and must be preserved in result scoring. |
| V0.34 normal code Section 3 sphere shortage | The run ended `151/158`. Eleven ordinary arcs collected about `6` each, while five dense high-platform arcs collected `17/30` and left `13` each. Near X `1586.24`, active objectives around X `1587..1590` remained above the high support immediately before a verified lower landing; this is the regression case for `SphereSweepToLowerLanding`. |
| V0.39 completion acceleration after mapped wall handoff | At the Section-0 completion face X `263`, the route latched `15.600` and selected `0.060 s` toward `[270,272] @ 2`, predicting X `270.914`. Actual speed rose through `19.100` and `22.800`; the player passed the target and died at X `279.463`. V0.40 treats positive completion acceleration plus the observed per-section speed ceiling as wall-exit target-selection evidence. |
| V0.39 normal code Section 3 Ground 7 correction chain | From wall `[1703,1705] @ 0`, the maximum collection hold predicted X `1714.031`, `1.712` short of `[1715.744,1721.256]`, but the old `NativeCapUndershootEscape` called it a landing. Actual face contact occurred at X `1714.370`, feet about `-2.497`; the second generic `0.120 s` pulse landed at X `1721.401`. V0.41 preserves the first result as a distinct `CollectionFace` outcome and keeps the target owned until physical contact. A target-top landing is recoverable but explicitly recorded as a missed low-soul route. |
| V0.40 normal code Section 2 Ground 7 regression | Wall `[1031,1033] @ 0` targeted lower support `[1043,1045.999] @ -2` at VX `14.4`. Strict landing rejected all holds and the staged fallback selected `0.075 s`; actual release near X `1030.778` died at X `1040.857` before the target. The retained V0.39 native-cap action reached the X `1043` face. V0.41 therefore commits `0.180 s` as a face/top outcome on this exact route, never as landing success. |
| V0.40 normal code Section 3 Ground 7 regression | Wall near `[1535,1537] @ 0` targeted the lower face beginning X `1547` at VX `16.9`. A `0.075 s` staged pulse died near X `1545.694`; later repetitions physically touched the next wall but had already discarded the planned wall route. V0.41 caps this exact normal route at `0.135 s`, preserves ownership through flight, and validates the target-left face before climbing. |
| V0.40 Section 0 completion narrow landing | Quota was `38/38`. The exit target was `[270,271.999] @ 2`; at X `271.585`, VX `24.4`, VY `1.484`, the static support was valid but confirmation remained `1/2`. The next fixed step left the top, reset support, and the watch expired. V0.41 captures that exact physics-step surface/position/body width. It may issue a verified prepared next action immediately; otherwise it preserves the landing as historical evidence and rearms grounded planning only while the live body is still supported. |
| V0.40 committed-face lifecycle hazard | Ground 7 exit faces can start `10..12` units beyond the old wall, outside the runtime's `1.20`-unit wall ray. V0.40 could therefore accumulate two low-descent steps and declare a pit before the face became detectable. V0.41 treats the bounded committed target as recoverable evidence until its fixed-step deadline, finite-face bottom, or target overflight, validates the exact mapped face on fixed-physics cadence, and prevents render-time displacement/timeout rules from revoking it first. |
| V0.26 Ground 3 at origin approximately `O=504` | incoming road `[487,494] @ -2`, floating tops `[498,502] @ 4` and `[505,510] @ 4`, downstream lower road `[514,521] @ -2`; physical leading faces were observed near X `498` and `505` |
| V0.26 Ground 5 at origin approximately `P=576` | incoming road `[562,569] @ -2`, pillars `[571,573] @ 0`, `[575,577] @ 4`, `[579,581] @ 7`, exit road `[583,590] @ -2`; the contact chain reached the third top at X `579.910` |
| V0.27 Ground 3 at `O=480` | `[474,478] @ 4 -> [481,486] @ 4 -> [490,497] @ -2`; face contact at player X `480.401` armed objective descent (line 2033), feet descended `2.183 -> 0.131` (line 2040), then an extra near-lip pulse landed at X `490.888` (line 2213) |
| V0.27 repeated Ground 3 at `O=528,576,624` | surface-2/surface-3 pairs `[522,526] -> [529,534]`, `[570,574] -> [577,582]`, and `[618,622] -> [625,630]`, all at Y `4`; near-lip remaining rises `0.566,0.338,0.566` caused top landings at X `529.243,577.730,625.242` instead of mandatory second-face contact |
| V0.27 Ground 5 at `P=552` | pillars `[547,549] @ 0 -> [551,553] @ 4 -> [555,557] @ 7`; the direct transfer landed at X `555.318` on the raw surface-4 top (line 2876), showing the chain can reach S4 but not proving its exit |
| V0.27 Ground 5 at `P=648` | pillars `[643,645] @ 0 -> [647,649] @ 4 -> [651,653] @ 7`, exit `[655,662] @ -2`; surface-4 face contact occurred at `(650.471,6.681)` (line 3851), but the following pulse landed at X `662.101` beyond the exit and died (lines 3966/3974) |
| Section 1 repeating raised platforms | `[498, 502] @ 4 -> [505, 510] @ 4` |
| Same topology one cycle later | `[546, 550] @ 4 -> [553, 558] @ 4` |
| Same topology another cycle later | `[594, 598] @ 4 -> [601, 606] @ 4` |
| Section 1 wall transition | `[631, 638] @ -2 -> [642, 646] @ 4 -> [649, 654] @ 4 -> [658, 665] @ -2` |
| Section 2 high-to-low route example | current support near `[675, 677] @ 7`, followed by lower terrain near `[679, 686] @ -2`; the approximately 9-unit drop is real terrain, not automatically a death pit |
| Section 2 chained-column failure (V0.17 attempt 31) | `[547,549] @ 0 -> [551,553] @ 4`; V0.17 climbed the first face, discarded wall state on detachment, then hit the second face near player X `550.401` without pressing |
| Section 2 manual multi-pulse face | player remained at X `650.401`; clean holds `0.132, 0.132, 0.090 s` advanced Y `-4.260 -> -1.653`, `0.691 -> 3.298`, and `2.577 -> 4.440` before the exit/landing |
| V0.19 level successor proof | after automatic exit from `[618,622] @ 4`, the runner physically stopped at the next face X `625`; a manual `0.111 s` click at player X `624.401` rose from Y `1.098` to `2.961`, proving the face and input were valid while automatic authority was missing |
| Section 3 narrow wall module | `[995, 999] @ -2 -> [999, 1001] @ 0 -> [1001, 1011] @ -2` |
| Section 3 repeated narrow wall module | lower road ending near `1055`, wall top `[1055, 1057] @ 0`, exit road approximately `[1067, 1071] @ -2` |
| Section 3 tall wall module | `[1571, 1583] @ -2 -> [1583, 1585] @ 0 -> [1595, 1602] @ -2 -> [1602, 1617] @ 2` |

## Logged landing errors that matter

The following cases explain why route intent and action execution must be logged separately.

| Attempt | Planned action | Prediction | Actual result |
|---:|---|---|---|
| V0.27 Ground 3 route 18, pulse 5 | finish climbing surface 3 after successful objective descent | from `(480.401,3.423)`, `0.075 s`, predicted landing X `481.744` | prior pulse had ended at VY `+17.253`; with only `0.571` rise remaining, the extra DOWN produced apex X `484.224` and landing X `490.888`, error `+9.143` (lines 2095-2213) |
| V0.27 Ground 3 repeated surface-2 exits | reach the same-height surface-3 face and enter its objective trench | each `0.075 s` staged pulse predicted near the active surface-2 top | with residual rises `0.566,0.338,0.566`, actual top landings were X `529.243,577.730,625.242`; physical second-face contact/descent was skipped (lines 2521-2619, 3067-3163, 3518-3616) |
| V0.27 Ground 5 route 27, surface-4 fallback | recover physical contact with `[651,653] @ 7`, then exit onto `[655,662] @ -2` | at `(650.471,6.681)`, remaining rise `0.313`; planner lost the exit and predicted X `651.744` from another `0.075 s` pulse | actual landing X `662.101`, travel `11.630`, error `+10.357`; it missed the exit road by about `0.101` and death followed at X `664.719` (lines 3851-3974) |
| V0.26 ordinary `0.075 s` | leave Ground 3-derived support and preserve the next wall-entry window | flight `0.529 s`, travel `6.251`, landing X `536.613` | actual hold `0.078 s`, flight `0.683 s`, travel `8.329`, landing X `538.691`, error `+2.078`; next window `[537.470,537.785]` was behind, but planner returned valid `Wait` and died beside face X about `545.407` |
| V0.26 Ground 5 exit transfer | `0.090 s` direct transfer toward `[675,677] @ 7` | landing X `676.058` | actual hold `0.091 s`; body contacted/crossed the target face without stable top support and eventually landed on lower terrain near X `682.523`; committed exit state did not restore wall authority |
| 19 | `0.090 s` jump to `[498, 502] @ 4` | landing X `500.852` | hold `0.097 s`, landing X `501.713`, error `+0.861`; physically on the raw top but outside conservative safe-right `501.255`; planner then reported no route and the player fell |
| 23 | `0.120 s` jump to `[546, 550] @ 4` | landing X `548.713` | landing X `549.867`, error `+1.154`; again raw support but outside safe-right `549.256` |
| 28 | `0.105 s` jump to `[594, 598] @ 4` | landing X `596.829` | landing X `597.564`, error `+0.735`; raw support but outside safe-right `597.256` |
| 31/32 | wall transfer toward `[642, 646] @ 4` | short approach then wall recovery | landed X `645.777`; raw top contact, outside safe-right `645.255` |
| 37 | low wall transfer through `[1055, 1057] @ 0` toward exit road | hold `0.040 s`, predicted X `1068.370` | landed X `1070.201`, error `+1.831`; actual road existed but predicted target bounds were too conservative/misaligned |
| retained late-window case | source `[538,542] @ -2` to target `[544,546] @ 2` | all conservative windows ended near X `539.62` | at X `539.922` the planner returned `NoVerifiedLaunchWindow` and sent no input although a current-position trajectory remained inside the target's raw bounds; V0.17 uses `EmergencyLateRawLanding` for this verified-source, hazard-cleared case |

For attempts 19, 23, and 28, a short immediate follow-up jump from the raw supported edge could have reached the next platform. V0.17 generalizes this as `EmergencyLateRawLanding`, also covering the retained X `539.922` case where the conservative launch window ended before the player reached the physical source edge.

## Wall-climb evidence

- The intended maneuver is not to walk onto a wall top.
- For trench routes, allow the runner to enter the trench or approach the face, then press jump while the body is against the wall.
- Release every press. Apply the next press only after a distinct fixed-physics step while the body is still attached to the intended wall.
- The player can remain against the wall through three or more pulses. This is a bounded, height-aware pulse train, not continuous clicking.
- A wall climb has a maximum useful timing window. Unbounded retry spam is invalid.
- The first wall-climb DOWN must be backed by actual `IsTouching` contact. A
  predicted body-contact X is not sufficient because an early DOWN can be
  consumed before wall jumping becomes legal.
- Releasing while still far below the lip can drop the character from the wall.
  V0.18 therefore selected staged pulse holds over `0.075..0.135 s`; the clean
  manual evidence includes `0.090` and `0.132 s`. V0.27 proves that this minimum
  must not be applied merely because a small residual rise remains.
- The V0.17/V0.18 rule to re-press promptly after the fixed-step barrier is
  superseded. Lines 2095-2109 show VY `+17.253` with only `0.571` rise remaining,
  followed by a redundant `0.075 s` reset and major overshoot. V0.28 keeps UP
  released while VY remains above `+0.25`, waits for release height or a real
  apex/descent, and caps that wait at `0.45 s`. A subsequent DOWN still requires
  valid wall contact and a lip that remains unreached.
- Ground 3 S2 -> S3 is a finite-face intercept, not a generic wall-top jump.
  The verified target face is `Y=[-1,4]`. Route-safe target feet are
  `[1.5,3.45]` with a preferred interval around `[2.0,2.8]` while descending.
- The successful automatic clone used feet Y `-1.226 -> 1.727` before its final
  `0.076 s` pulse, resumed horizontal travel near feet Y `3.909`, and contacted
  S3 at X `480.401`, feet Y `2.177`, VY `-22.214`. Later redundant rising-state
  pulses moved the reconstructed S3 intersection to Y `4.849/5.391/4.846`,
  above the face top. This is action scheduling, not a different static map.
- Fixed-step calibration for this route is `Vheld=18.6266`,
  `rise/tick=0.372532`, and released apex tail `2.343328`. A setup action is
  selected from an exact actuator tick count and must stay at least `0.30`
  below the old release height; it is not a fixed `0.165 s` press.
- The final pulse is released on the first height-gated old-wall horizontal
  resume, or immediately before fixed physics step `N+1`.
  A semi-implicit prediction from the successful release state yields target
  feet Y `2.181` versus actual `2.177`. After release, all DOWN input is locked
  until mapped S3 contact persists on a later FixedUpdate.
- V0.6 logs showed phase one selecting the wall top, then phase two replacing
  it with a distant exit target (`[529,534] @ 4` in the repeated wall case).
  This skipped the next authored crack. V0.7 locks the wall top across both
  phases, caps the climb at two presses, and releases at observed lip crossing.
- V0.7 accidentally omitted `StagedAttachedBounce` from that target lock and
  lip-crossing release. V0.8 applies both invariants to the staged low/narrow
  wall action as well; ordinary platform-jump routing is unchanged.
- The first phase hold is no longer copied from ordinary jump distance. Its
  lower bound is derived from remaining wall rise and live vertical velocity;
  the second phase repeats that calculation from the new contact height.
- A demonstrated successful tall-wall sequence used approximately `0.165 s` for the first press, reached an attached position near `(1457.485, 1.328)`, then used `0.135 s` for the second press and landed on `[1458, 1473] @ 2` near X `1470.339`.
- The same pattern later landed on `[1602, 1617] @ 2` near X `1614.339`.
- Low two-unit walls behave differently: a `0.120 s` press can cross the lip and overshoot the useful narrow surfaces. They require a deliberately staged short first bounce or a verified direct transfer.
- V0.17 attempt 31 proves that leaving one low face is not always a final exit. The runtime had already captured `[551,553] @ 4` while climbing `[547,549] @ 0`; this target must be promoted and awaited as the next physical wall contact.
- V0.19 gives stronger evidence: at player X `550.401`, the wall probe repeatedly reported face X `551.000`, `Touching=True`, and native `IsWalled=True`, but the phase was `ExitFlight` and the stale target was `[547,549] @ 0`. This is an action-state ownership failure, not perception failure.
- V0.27 proves the complementary failure: having wall authority does not imply a new press is immediately correct. Ground 3 repeats ended at VY `+17.253` with only `0.338-0.566` rise remaining and still sent the minimum pulse. The correct scheduler must distinguish horizontal contact from vertical stall.
- Ground 3 surface 2 -> surface 3 is now a route contract, not an optional watch. While lower-lane spheres remain, physical surface-3 face contact is mandatory; landing on its top is logged as `MandatoryWallFaceContactMissed` rather than success.
- Ground 5 surface 4 -> exit road is the inverse height case. The static continuation search must retain the broad lower road even though it is nine units below the pillar top; otherwise contact fallback has no downstream target and repeats a blind pulse at the narrow high face.
- For the exact same-piece Ground 5 S4 -> S1 exit, preferred safe bounds remain the first choice, but a solver-proven physical body overlap may be accepted and must carry its landing mode into outcome validation. No other five-unit drop inherits this exception.
- Code Section 3 may replace a verified natural drop with a sphere-scored jump only when the predicted landing remains on that same lower support. This is an objective optimization over known-safe geometry, not a new hard-coded route coordinate.
- The V0.20 Section 3 trace repeatedly classified an eight-unit wall correctly but rejected its action: player X `956.250 -> face 964.41`, `1004.255 -> 1012.41`, `1028.265 -> 1036.41`, `1081.150 -> 1084.41`, and `1100.265 -> 1108.41` all ended as `WallAcrossGapHasNoExecutableWallRoute`. The rejected clearance was `[0.12,1.65]` below an eight-unit lip. These are lower-face/multi-pulse routes, not direct upper-lip approaches.
- The V0.20 manual Section 4 evidence proves deep trenches are playable: at `(1786.401,-3.653)` a `0.108 s` wall press rose `2.267`; at `(1882.401,-5.015)` a `0.123 s` press rose `2.235` and remained attached to face X `1883`; at `(1954.326,-3.437)` a `0.102 s` press rose `2.235`. Detected faces at X `1787`, `1883`, `1979`, and `2027` invalidate any global `Y < -3.5` death rule.
- Section 2 entry `[538,545] @ -2 -> [547,549] @ 0` has source width `7`, target width `2`, gap `2`, and rise `2`. V0.20 labeled it `RaisedLanding`, found no safe direct jump, and fell to `(546.401,-3.506)` beside face X `547`. The correct route is wide-road-to-narrow-pillar trench entry followed by a contact-confirmed wall press.
- Section 1 counterexample: `[77,86] @ -2 -> [88,90] @ 2`, followed by `[95,97] @ 2` and `[102,104] @ 2`, is a same-height floating-platform chain. V0.22 incorrectly armed a passive wall drop and fell without detecting a wall. The target being narrow is insufficient; unlike the Section 2 sequence, there is no nearby higher narrow continuation.

## Known map-related risks

- Spirit Boost and section-dependent speed change horizontal travel substantially. The same hold duration cannot be bound to one fixed distance. `SpiritBoost` trigger/state and `BonusSphere` objectives are different typed systems and must never be inferred from one another.
- Safety-road talents may add standable colliders outside `PlayerMovement.jumpableLayer`; the scanner therefore inspects all physics layers and filters for landing geometry.
- The active piece set changes during pooling. Never retain a surface solely by world coordinates or global generation. Preserve its mapped piece instance, origin, authored surface role, and live bounds; use registry generation as diagnostic context.
- A visible top is not necessarily an executable first-action target. Tall tops may require wall-face contact first.
- The current static registry records horizontal top intervals but not solid face bottoms, objective-lane semantics, or route roles. Ground 3 and Ground 5 metadata in this document must therefore remain explicit policy evidence until those attributes have a first-class source representation.
- A downward route can be correct without jumping. The planner must distinguish continuous road, natural drop, trench entry, and lethal fall.
- The screenshot supplied by the user confirms deep wall/trench geometry where upward progress must be generated by wall-contact jump phases.
- Do not interpret a stationary grounded respawn as permission to reuse an inactive section map. Death, player loss/replacement, supported-map eligibility loss, or global position discontinuity invalidates the continuation epoch. Unverified recovery requires two stable fixed steps plus active gameplay or reliable forward motion (`|VX| >= 1`). A confirmed pit or verified upward respawn teleport can use the first stable grounded step only with forward VX or exact mapped-wall evidence, and must refresh the remembered map before same-frame routing.
- Do not interpret a reused reward object as a new epoch target. Retire old/current instance IDs at the boundary. Complete-inventory stabilization quarantines one scan after a successful snapshot, or two scans after an incomplete snapshot. A distinct ID may then latch normally, but a retired ID needs two consecutive complete observed absences before reuse. Two complete physical-empty scans are an alternate baseline; partial scans and `OnlyRetiredEpochTargets` do not count.

## Source of truth

- Static templates and section cycles: `Routing/BonusMapPieceRegistry.cs`
- Live support and next-surface construction: `Routing/BonusPlatformScanner.cs`
- Obstacle semantics: `Routing/BonusObstacleClassifier.cs`
- Wall contact probes: `Routing/BonusWallDetector.cs`
- Typed Spirit state/trigger capture: `Diagnostics/BonusStageInspector.cs`, `Routing/SpiritBoostRouteContext.cs`
- Action planning: `Routing/BonusJumpPlanner.cs`
- Execution and wall phases: `Runtime/AutoBonusRunnerRuntime.cs`
- Typed reward scan completeness: `Detection/BonusRewardTargetDetector.cs`
- Harmony ownership and inventory: `Plugin.cs`, `Control/JumpHoldInputPatches.cs`
- Logical fixed-step identity: `Control/JumpController.cs`, `Physics/JumpPhysicsFeedback.cs`

## V0.9 evidence capture

For wall regressions, retain the complete sequence from the first
`ActionPhysicsFrame` with `WallPhase=ApproachJumpInFlight` or
`AwaitingWallContact` through the final `JumpAttemptResult`. The fixed-step
records include the unchanged wall target identity, contact/touching state,
both DOWN/UP boundaries, native jump counters, lip-crossing boundary, live
support scan, and actual landing. For transition/reward failures, retain the
preceding `ControlGate` plus all `ActionPhysicsFrame` and
`RewardTargetObservation`, `RewardObjectPhaseLatched`,
`RewardTargetFreshEpochPending/Latched`,
`RewardTargetRearmEmptyScan`, `RewardTargetRearmBaselineEstablished`,
`TerrainContinuationEpochRevoked` and `PitDescentGuardReleased`,
`RewardObjectOwnershipHandoff`, and
`CompletionRewardAction` records. These records distinguish a blocked control
gate, missing route, rejected input, lost wall contact, overshoot, and missing
reward attack without requiring video. Preserve startup
`HarmonyAutoPatchInventory` and any `FixedStepCallbackDeduplicated` record as
well; the valid normal inventory is `1/1/1`, and a deduplication event indicates
that an unexpected duplicate callback was suppressed.

## V0.10 observations from the V0.9 trace

- At the `0 -> 1` section transition, Section 1 temporarily exposed `38/38` while still in `PreBonusMode`. V0.10 treated that as stale, V0.30 treated live equality as authoritative, and V0.35 required an incomplete-to-complete proof. V0.36 removes quota from control entirely: the last section observed in real active gameplay remains the routing identity until an eligible typed reward target latches or a different section becomes genuinely active, unless lifecycle loss first revokes the continuation epoch. A revoked epoch may resume on an inactive road only after two stable fixed steps with reliable forward VX.
- The wall target `[474,478] @ 4` produced a safe target-aware candidate of `0.090 s`, but the generic lip rule raised the executed hold to `0.180 s`. The runner crossed the lip near `(474.591,5.190)` with velocity about `(11.9,11.759)` and overshot into the pit near X `481.115`. Target-aware and staged wall actions now retain their own hold.
- Repeated landings beyond `[522,526]`, `[551,553]`, `[570,574]`, `[618,622]`, `[647,649]`, and `[666,670]` are consistent with the same excessive post-lip velocity. The V0.10 change addresses the shared execution override instead of adding map-specific exceptions.

The single successful two-pillar climb in the same general topology is evidence against a static-template error. In the failed repetitions the target surface remained plausible, while hold policy and post-lip velocity differed. Treat this family primarily as an execution-state regression unless a future trace shows a different piece/surface identity or registry generation at the first bad decision.

## V0.28 map-contract validation targets

- Ground 3 `S2 -> S3`: the log must first emit `WallExitRouteContract ... MandatoryFaceContact`, then `MandatoryFaceSetup` (unless a direct intercept is already safe), residual-rise observation without a DOWN while VY is positive, a dynamic `MandatoryFaceIntercept` plan, `MandatoryFaceInterceptEarlyRelease` or an already-released lip crossing, `MandatoryFaceContactConfirmationStarted`, `MandatoryFaceInterceptObserved`, and `AttachedObjectiveDescentArmed`. A direct S3 top landing is failure even if no life is lost.
- Each mandatory DOWN must log `FixedStepLimit=N`; normal actuator release must report `Trigger=FixedStepLimit, HeldFixedSteps=N`. An earlier UP is legal only with `MandatoryFaceInterceptEarlyRelease Trigger=HorizontalResume`. Contact confirmation must span two distinct fixed-step sequence values and identify finite target-side evidence.
- Ground 3 clone repetition: verify the same contract at origins equivalent to `O=480,528,576,624`; this detects accidental dependence on one world X while still using live transformed map identities.
- Ground 3 identity: require the same nonzero piece instance, `S2 -> S3`, compatible origin, gap `2.50..3.50`, and top delta within `0.15`. Registry generation is diagnostic only because pooled remote-piece refreshes changed `G27 -> G28` without changing the mapped clone. A transient `Unknown#0/S-1` live hit preserves the locked identity; a contradictory mapped hit fails the contract.
- Contact semantics: accept only the finite S3 leading side face with feet in the physical band `[top-4.90, top-0.15]`, left-facing normal/body evidence, and confirmation on a later fixed step. Top/corner contact is not success. The confirmation frame remains UP-only; captured sphere bounds are used only if the live scan fails, not when it succeeds with zero remaining spheres.
- Ground 5 `S4 -> exit`: after `[P+3,P+5] @ 7`, require `WallChainTargetCaptured ... Role=MappedDownstreamExit` for `[P+7,P+14] @ -2`. At the V0.27 regression origin `P=648`, that means `[651,653] @ 7 -> [655,662] @ -2`.
- Authority boundary: identify automatic, post-death-enabled, and U-disabled/manual intervals before using any coordinate as behavior evidence. The V0.27 reference boundaries are lines 3974, 4285, 4355, and 4391.
- Deployment validity: accept a new test only if startup reports internal `V0.28`, schema `3`, runtime registration succeeds, and the build, Mod Manager source, root loader, and nested compatibility DLLs match. The deployed V0.28 SHA-256 is `1CD08E0F83441CB2539671F9DBB7FF59C4A56A4BBA77FDEC7CBEC81DDCDF2CAF`, length `367616`; `app_config.cfg` currently disables AutoBonusRunner, so re-enable it in Mod Manager and restart before treating any trace as valid.

## V0.36 runtime/map validation targets

- Patch cadence: startup must report `HarmonyAutoPatchInventory UpdatePrefixes=1, FixedPrefixes=1, FixedPostfixes=1`. MelonLoader owns automatic patch discovery; no explicit AutoBonusRunner `PatchAll()` is legal. A normal run should contain no `FixedStepCallbackDeduplicated`; if one appears, retain it as evidence of another duplicate installation path.
- Logical physics time: fixed-limited DOWN records must advance once per unique exact `Time.fixedTimeAsDouble`, and feedback must increment `FixedStepSequence` once per real primary-player integration. Multiple distinct catch-up ticks within one render frame remain distinct.
- Ground 5 S4 pickup: `Ground5HighestPillarSinkArmed` must record `FixedTime`, `DeadlineFixedTime`, and a simulated `Steps=2..8`. Completion must report double-time-derived `ElapsedPhysicsSteps`; `RawSequenceDelta` is diagnostic and may not own the deadline.
- Reward epochs: retain the `EpochReset:...Retired=N:SnapshotComplete=...:StableInventory=X/2` observation. A successful snapshot requires one later quarantined complete inventory; `SnapshotComplete=False` requires two. Only after stabilization may a distinct non-retired ID log `RewardTargetFreshEpochPending` then `RewardTargetFreshEpochLatched` without empty scans. A retired ID must remain filtered as `OnlyRetiredEpochTargets` until two consecutive complete inventories omit it. The alternative baseline is `RewardTargetRearmEmptyScan ... 1/2` followed by `RewardTargetRearmBaselineEstablished ... 2/2`; incomplete scans and retired-only inventories cannot advance it. A persistent partial wrapper scan must emit rate-limited `RewardTargetScanIncomplete` and cannot authorize a target.
- Continuation epoch: confirmed pit/death, player replacement/unavailability, supported-map eligibility loss, or global position discontinuity must block both map and reward ownership. Release requires two distinct stable-ground fixed steps after the delay with `|VY| <= 2.50` and forward gameplay (`IsActiveGameplay` or `|VX| >= 1`). Validate `PitDescentGuardReleased ... ForwardGameplayResumed=True,TerrainEpochRearmed=True`; when active gameplay is false, the remembered map section must remain authoritative rather than adopting a mutable controller index.
