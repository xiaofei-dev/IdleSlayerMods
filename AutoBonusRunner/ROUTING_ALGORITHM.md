# AutoBonusRunner Routing Algorithm

## Objective

AutoBonusRunner must choose and execute jump actions from live game state. It must not replay a fixed timeline. Each decision must account for current X/Y position, velocity, section, Spirit Boost, live platform geometry, hazards, learned jump physics, wall contact, and the result of the previous action.

Normal navigation controls jump press/release; press duration changes jump height. Direct bow shots and a selected grounded Wind Dash are permitted only after the runtime confirms an active, interactable reward box, coin, or gem on two consecutive render frames. Horizontal motion is supplied by the game and can continue while the game is in the background.

## Current revision status

The current source target is public `1.0.0`, internal `V0.45`, configuration schema `7`. V0.31 introduced phase-aware speed and wall-exit planning, V0.32 added isolated retry handling, V0.33 added run statistics, V0.34 added confirmed-contact escape planning plus the authored Ground 5 pickup sink, V0.35 added native reward diagnostics plus the Section-3 sphere sweep, and V0.36 introduced the typed reward-object latch and transition-road lifecycle. V0.37 closes four trace-proven execution gaps: immediate narrow-support chaining, an extra-light mandatory face setup pulse, static-wall recovery during inactive completion traversal, and a smaller normal Section-3 face-intercept transfer. V0.38 widens only the boosted high-pillar sink entry and makes an active Section-3 airborne wall collision a continuous climb handoff instead of a ground re-plan. V0.39 lets exact completion walls override a stale boosted landing target and makes an unexpected stable wall-exit landing replan from its real support. V0.40 adds a bounded reactive correction supervisor, removes false undershoot-as-landing success, shortens observed low-lip recovery, and applies completion acceleration evidence to wall-exit target selection. V0.41 separates a committed face/top outcome from landing success, preserves its complete flight ownership, and immediately hands narrow watched landings back to grounded planning. V0.42 introduced exact popup matching and native acknowledgement, but incorrectly rearmed the one-use native Second Wind. V0.43 added exact current-wall fallback and a later physics-step wall-impulse decision barrier. V0.44 restores native one-use retry semantics, admits a strictly verified live-position chain from a narrow support when safe launch intervals narrowly do not intersect, and rebases a newly touched forward face during wall `ExitFlight` without waiting for ground. V0.45 adds only scoped delayed start-slider confirmation and does not change routing.

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
8. Explicit face pulses use an exact fixed-step release count shared by solver and actuator. Ordinary ground/wall plans retain their calibrated wall-clock release during normal rendering and also carry a derived fixed-step safety ceiling, so background batching cannot add unlimited powered ticks. A missing fixed callback has a logged wall-clock fail-safe. Keep wall-clock and delivered-step measurements separate in learning.
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
7. Confirmed pit/death, player replacement or unavailability, loss of supported-map eligibility, and global position discontinuity revoke the terrain-continuation epoch and stale reward ownership. Rearm requires the restart delay plus two distinct stable-ground physics steps with `|VY| <= 2.50`, a supported map, live player, remembered terrain section, and forward-gameplay evidence: `IsActiveGameplay=true` or `|VX| >= 1.0`. The VX alternative safely resumes an inactive post-quota road; stationary ground alone does not. Active gameplay adopts the current section, while inactive recovery retains the last active terrain section.
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

V0.27 therefore uses one `4 x 6` duration grid. Every height band (`Level`, `ModerateUp`, `HighUp`, and `Down`) is crossed with the same six hold buckets: `H02` (`<= 0.030 s`), `H04` (`<= 0.050 s`), `H06-075` (`<= 0.0825 s`), `H09-105` (`<= 0.1125 s`), `H12-135` (`<= 0.1425 s`), and `H15-180` (longer holds through the supported cap). At minimum, calibration also distinguishes section and ordinary ground jump versus wall pulse. Learned observations correct the analytical curve; a coarse median may not replace it across materially different holds. Section 0 route/travel observations do not calibrate Section 1-or-later routing.

Ordinary ground takeoff velocity near `18.627` and wall reset velocity near `20` are separate populations. Only clean grounded-to-ground samples update the ground takeoff model. Wall-contact pulses may update a wall-specific model but must not enter the ground sample median. Large errors must be logged as evidence even when excluded from a stable estimator; silently rejecting every failed jump prevents correction.

Duration-grid ownership is stricter than general feedback eligibility: only an observed input-DOWN-to-confirmed-stable-landing interval writes a duration cell. Neither horizontal travel nor launch/start-speed is a proxy for elapsed flight time, so those observations update only their dedicated travel or speed estimators. This prevents one physical sample from contaminating the duration curve through multiple incompatible units.

The Harmony input/feedback bridge runs only for the primary `PlayerMovement.instance`. V0.26 allowed secondary player instances to contaminate capture. A separate V0.35 audit confirmed a second doubling mechanism: MelonLoader automatically installed the attributed patches, and AutoBonusRunner manually called `PatchAll()` again. The explicit call is removed; startup inventory must be `UpdatePrefixes/FixedPrefixes/FixedPostfixes = 1/1/1`. Exact per-player `Time.fixedTimeAsDouble` deduplication remains in the actuator and feedback bridge so one real integration advances `FixedStepSequence`, held-step limits, and learning exactly once.

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

Harmony patch ownership is automatic under MelonLoader. AutoBonusRunner never calls `PatchAll()` itself. Do not use `Time.frameCount` as the physics identity: background catch-up can execute several legitimate fixed ticks in one render frame. Exact double fixed time suppresses only repeated callbacks for the same tick and preserves every distinct catch-up integration.

AutoBonusRunner does not inspect, patch, configure, or suppress AutoJumpMod. The user owns input exclusivity and must turn AutoJumpMod off before testing this planner.

## Successful section continuation

`PreBonusMode` is not by itself a death or reward state. In current V0.36,
sphere quota and native reward flags are diagnostics only. The normal terrain
planner continues with `lastActiveTerrainSection` while the continuation epoch
is valid and no typed reward target has latched. It never sends a fallback
reward pulse merely because terrain is missing. Confirmed death, player
replacement/unavailability, or supported-map eligibility loss revokes this
epoch. Rearm requires two stable-ground fixed steps plus either active gameplay
or reliable forward motion (`|VX| >= 1`), allowing a moving inactive post-quota
road to resume without authorizing a stationary respawn. Reward jump/arrow/dash
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
    require two stable fixed steps and (active gameplay or |VX| >= 1) to rearm
    return

if the same qualified reward target has been observed on two distinct frames:
    release terrain/wall ownership atomically
    run contextual reward jump, arrow, and optional stable-ground Wind Dash
    return

refresh live map registry
scan support, next surfaces, hazards, spheres, and wall contact
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

- V0.43 is deployed and gameplay-tested. Its latest trace proves the fixed-step impulse barrier and the exact popup/acknowledgement chain, while also proving that repeatable native rearm, narrow-support route rejection, and unowned new-face `ExitFlight` contact require V0.44. V0.44 is now disk-deployed at length `563712`, SHA-256 `5BD44DC06248C6BA2886462D168770BA9740A84B1489B19A41B6DFD4FCF96ECC`, but still needs a fresh gameplay trace.
- Ground 6 S1 -> S6 must remain input-free until physical contact; both an early DOWN and an `InputNotAcceptedByGame` passive-route reset are failures.
- Section 3 Ground 7 S2 must validate the two-phase solver at normal `16.9` speed and during Spirit Boost. The trace must compare selected `LipVX`, predicted landing, actual support, and residual.
- Ground 5 S2/S3/S4 must not report `ObservedPlannedFaceContact`; require `Ground5NarrowPillarBodyGate=True` followed by `TriggerMode=BodyContact`. Other retained one-frame wall sites may still use `ObservedWatchedExitFaceContact` or late physical-face salvage when their mapped contract permits it.
- Code Section 3 must compare every `SphereSweepToLowerLanding` expected hit count with the actual sphere delta and verify the same lower support. A pickup increase that changes the landing target is failure.
- A boost acquired after input DOWN is not knowable at planning time. Log it as a discontinuity/outcome residual and do not convert it into a permanent section cruise floor.
- Farther wall-exit candidates are statically ordered but do not yet carry a general intervening-solid occlusion proof. The known Section 3 corridor is the bounded use case.
- The typed reward state machine needs a trace showing terrain continuation while no eligible target qualifies, one `RewardTargetObservation` candidate, a two-frame `RewardObjectPhaseLatched`, atomic `RewardObjectOwnershipHandoff`, and jump/arrow/dash actions. Its next-epoch tests must distinguish identity from emptiness: the `StableInventory=X/2` gate quarantines one complete inventory after a successful snapshot or two after an incomplete snapshot; only then may a distinct non-retired ID log `RewardTargetFreshEpochPending/Latched` without a global-empty gap. An old ID remains `OnlyRetiredEpochTargets` until two consecutive complete inventories omit it, and two consecutive complete physical-empty scans may establish the alternative baseline. Any partial scan must clear pending positive proof, must not prove absence, and should emit `RewardTargetScanIncomplete`. Confirmed death, player replacement/loss, or supported-map eligibility loss must block continuation/reward ownership until two stable fixed steps plus active gameplay or `|VX| >= 1`; release must return without a same-frame route and let the next `Update` refresh map identity. Native flags may disagree without changing control.
- Fixed-time validation must show `HarmonyAutoPatchInventory` at `1/1/1`, approximately one `FixedStepSequence` increment per real `fixedDeltaTime`, and no normal `FixedStepCallbackDeduplicated` records. Ground 5 sink elapsed steps and deadlines must follow `Time.fixedTimeAsDouble` even when multiple physics ticks occur between render observations.
- Later sections remain less thoroughly demonstrated, and the large runtime still needs eventual subsystem separation.

## Deployment validity gate

A V0.44 gameplay trace is valid only when its startup block explicitly loads `AutoBonusRunner.dll` and reports public `1.0.0`, internal `V0.44`, schema `6`, followed by successful registration of `AutoBonusRunnerRuntime` and exact inventory `UpdatePrefixes=1, FixedPrefixes=1, FixedPostfixes=1, RetryPromptPostfixes=1, RetryPopupPrefixes=1, RetryPopupPostfixes=1, RetryRewardPostfixes=1, RetryErrorPostfixes=1, RetryClosePostfixes=1`. Historical enabled logs load `./Mods/AutoBonusRunner.dll`, so the root loader copy is authoritative. Matching hashes prove deployment only; V0.44 gameplay claims require a new trace after this deployment. The build artifact and all three installed DLLs currently match at length `563712`, SHA-256 `5BD44DC06248C6BA2886462D168770BA9740A84B1489B19A41B6DFD4FCF96ECC`.

See `MAP_REFERENCE.md` for full static geometry and concrete logged examples.
