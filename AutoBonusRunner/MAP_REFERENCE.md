# AutoBonusRunner Map Reference

This document records the static Bonus Stage geometry currently known to the mod and representative world-coordinate observations from runtime logs. It is a debugging reference, not a hard-coded route script.

Current source target is public `1.0.0`, internal `V0.31`, schema `4`. V0.31 does not change authored geometry; it corrects the executable interpretation of Ground 6 S1 -> S6 and speed-dependent Ground 7/8 wall exits. Completion traversal remains keyed directly to the live completed quota during `PreBonusMode`.

Historical V0.30 deployment identity is length `380928`, SHA-256 `1A7607B4A4D639DA053AC52E69549A19FF40D9660F1C02A66C81E631B63021C2`. V0.31 is synchronized in all four DLL locations at length `394240`, SHA-256 `A1D575CB397EB60B75C9A019C4FBEA0AF27E620BC1B43FA3F2F3AD8270C72EE1`.

## Coordinate conventions

- `X` increases in the running direction.
- `Top` is the world or local Y coordinate of a standable horizontal surface.
- A surface is written as `[Left, Right] @ Top`.
- Template coordinates are local to a live `Ground N` clone.
- World coordinates are calculated as `world = piece origin + local`.
- Authored pieces normally have origins separated by `24.0` world units.
- Pooling moves and reuses clones. Therefore, a world X value seen in one run is not a permanent piece ID or a safe hard-coded trigger.
- `RegistryGeneration` changes when the section, active clone set, or clone transforms change. A change triggers revalidation but does not by itself invalidate an unchanged route, because recycling a remote pooled clone can advance the global generation. Validate the target by nonzero piece instance, piece origin, authored surface role, and current geometry; generation equality is diagnostic.

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
| Exit road | `[P+7, P+14] @ -2` | Verified downstream landing/drop target |
| Vertical lanes | around local X `-2` and `+2`, with authored spikes below | Traverse by bounded face-contact decisions, not direct long jumps |

Intended Ground 5 route graph:

`incoming road -> passive gap entry -> pillar-1 contact/pulse -> pillar-2 contact/pulse -> pillar-3 contact or top -> explicit S4-to-downstream-exit transition -> verified exit road`

These pillars are vertically deep enough for passive entry and sequential contact. Do not reuse Ground 3's finite floating-body approach or attached objective-descent rule here. Surface 4 is not terminal: its nearest authored forward continuation is the broad road `[P+7,P+14] @ -2`, nine units lower. That road must remain a named exit target through a surface-4 face-contact fallback.

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
- The V0.20 Section 3 trace repeatedly classified an eight-unit wall correctly but rejected its action: player X `956.250 -> face 964.41`, `1004.255 -> 1012.41`, `1028.265 -> 1036.41`, `1081.150 -> 1084.41`, and `1100.265 -> 1108.41` all ended as `WallAcrossGapHasNoExecutableWallRoute`. The rejected clearance was `[0.12,1.65]` below an eight-unit lip. These are lower-face/multi-pulse routes, not direct upper-lip approaches.
- The V0.20 manual Section 4 evidence proves deep trenches are playable: at `(1786.401,-3.653)` a `0.108 s` wall press rose `2.267`; at `(1882.401,-5.015)` a `0.123 s` press rose `2.235` and remained attached to face X `1883`; at `(1954.326,-3.437)` a `0.102 s` press rose `2.235`. Detected faces at X `1787`, `1883`, `1979`, and `2027` invalidate any global `Y < -3.5` death rule.
- Section 2 entry `[538,545] @ -2 -> [547,549] @ 0` has source width `7`, target width `2`, gap `2`, and rise `2`. V0.20 labeled it `RaisedLanding`, found no safe direct jump, and fell to `(546.401,-3.506)` beside face X `547`. The correct route is wide-road-to-narrow-pillar trench entry followed by a contact-confirmed wall press.
- Section 1 counterexample: `[77,86] @ -2 -> [88,90] @ 2`, followed by `[95,97] @ 2` and `[102,104] @ 2`, is a same-height floating-platform chain. V0.22 incorrectly armed a passive wall drop and fell without detecting a wall. The target being narrow is insufficient; unlike the Section 2 sequence, there is no nearby higher narrow continuation.

## Known map-related risks

- Spirit Boost and section-dependent speed change horizontal travel substantially. The same hold duration cannot be bound to one fixed distance.
- Safety-road talents may add standable colliders outside `PlayerMovement.jumpableLayer`; the scanner therefore inspects all physics layers and filters for landing geometry.
- The active piece set changes during pooling. Never retain a surface solely by world coordinates or global generation. Preserve its mapped piece instance, origin, authored surface role, and live bounds; use registry generation as diagnostic context.
- A visible top is not necessarily an executable first-action target. Tall tops may require wall-face contact first.
- The current static registry records horizontal top intervals but not solid face bottoms, objective-lane semantics, or route roles. Ground 3 and Ground 5 metadata in this document must therefore remain explicit policy evidence until those attributes have a first-class source representation.
- A downward route can be correct without jumping. The planner must distinguish continuous road, natural drop, trench entry, and lethal fall.
- The screenshot supplied by the user confirms deep wall/trench geometry where upward progress must be generated by wall-contact jump phases.

## Source of truth

- Static templates and section cycles: `Routing/BonusMapPieceRegistry.cs`
- Live support and next-surface construction: `Routing/BonusPlatformScanner.cs`
- Obstacle semantics: `Routing/BonusObstacleClassifier.cs`
- Wall contact probes: `Routing/BonusWallDetector.cs`
- Action planning: `Routing/BonusJumpPlanner.cs`
- Execution and wall phases: `Runtime/AutoBonusRunnerRuntime.cs`

## V0.9 evidence capture

For wall regressions, retain the complete sequence from the first
`ActionPhysicsFrame` with `WallPhase=ApproachJumpInFlight` or
`AwaitingWallContact` through the final `JumpAttemptResult`. The fixed-step
records include the unchanged wall target identity, contact/touching state,
both DOWN/UP boundaries, native jump counters, lip-crossing boundary, live
support scan, and actual landing. For post-completion failures, retain the
preceding `ControlGate` plus all `ActionPhysicsFrame` and
`CompletionRewardPulse` records. These records distinguish a blocked control
gate, missing route, rejected input, lost wall contact, overshoot, and missing
reward attack without requiring video.

## V0.10 observations from the V0.9 trace

- At the `0 -> 1` section transition, Section 1 temporarily exposed `38/38` while still in `PreBonusMode`. V0.10 treated that as stale and required a later `BonusMode`; V0.30 superseded this interpretation after the real reward transition proved the live completed quota is authoritative. The next `0/N` quota ends completion traversal naturally.
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
