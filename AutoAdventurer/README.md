# AutoAdventurer

AutoAdventurer automates selected character actions in Idle Slayer. The first
development feature is Automatic Rage.

## Automatic Rage

- Press `K` to enable or disable Automatic Rage by default.
- While enabled, the mod checks every 12 seconds and activates Rage Mode when
  the player is in Runner or Rage Mode and Rage has no remaining cooldown.
- The periodic activation continues while Rage is already executing; repeated
  activations do not reset the current cycle's maximum-duration timer.
- An automatically started Rage ends when a Chest Hunt Key appears or when the
  configured maximum duration is reached.
- Press `J` to end the current Rage Mode immediately by default. If automation
  remains enabled, this ends only the current cycle.
- Disabling Automatic Rage stops future automatic activations but does not end
  a Rage Mode that is already executing; the current Rage ends naturally.
- After Rage ends, the mod waits six seconds by default, then checks for a
  Chest Hunt Key, Special Random Box/minigame trigger, or active portal. It
  starts the next Rage cycle only after no blocker remains.
- The same world-blocker check runs immediately before every Rage activation,
  including the first activation after enabling automation. Debug logs identify
  the special item, portal, door, or gate that blocked activation.
- Random minigame icons for Ascending Heights and Grapple Run are also treated
  as blockers when their active spawned objects can be identified.
- In-game notifications are shown only when `K` enables or disables Automatic
  Rage. Automatic activation, manual stopping, key detection, and time-limit
  stopping are logged without additional screen notifications.

The mod does not currently restart running after Rage ends.

Automatic Rage and Auto Boost remain active while ordinary menus opened over
the central Runner or Rage scene are visible. They pause only when the game
leaves those gameplay states, the map is changing or unavailable, or a
bonus-start slider is visible. After returning to a supported gameplay scene,
Auto Boost waits half a second while Automatic Rage waits two seconds to avoid
activating during an unsafe transition. Slider Skip remains active in supported
minigame and boss pre-start screens.

## Bonus Slider Skip

When `Skip Bonus Start Slider` is enabled, AutoAdventurer automatically
confirms the timing slider shown before supported bonus minigames. This feature
is enabled by default and can be disabled independently from Automatic Rage.

## Smart Auto Boost

- Press `L` to enable or disable Auto Boost by default.
- The currently selected player ability is read from `AbilitiesManager` before
  every activation.
- When the selected ability is unlocked and its cooldown reaches zero, the
  game ability manager triggers it.
- Switching between Boost and Wind Dash is followed automatically; the mod
  does not attempt to activate both abilities independently.
- When `Wind Dash Require Grounded` is enabled, Wind Dash waits at zero
  cooldown until the player returns to ground level, then activates
  immediately. This prevents airborne dashes from passing over portals or
  elite enemies. Regular Boost activation is unchanged.
- Auto Boost supports both Runner Mode and Rage Mode whenever the selected
  movement ability is available. After returning from another scene, it uses
  the shared half-second central-screen stabilization delay without adding a
  second Boost-specific delay.
- Ascension clears the observed ability state and pauses Auto Boost for five
  seconds. A supported selected ability must then remain stable across several
  checks before automatic activation resumes.

## Quest-Guided Dimension Travel

- Press `P` to enable or disable Quest Automation by default.
- Quest diagnostics have their own `Debug Mode` setting and do not depend on
  the main AutoAdventurer debug setting.
- Quest decision logs include both the localized in-game quest name and its
  stable internal ID.
- All active incomplete kill quests are evaluated together without Daily
  priority. Among targets that can currently be resolved to an unlocked,
  Portal-selectable dimension, the quest with the smallest total kill
  requirement is selected; ties preserve the game-list order.
  Ranking reads the serialized `questGoal` value used by the quest UI;
  `Quest.GetGoal()` is not used because it returns zero for some normal quests.
- Once selected, a quest is locked by internal ID and re-resolved from fresh
  snapshots until it completes. Temporary map or Portal unavailability does
  not cause another task to replace it.
- When `Maximum Quest Time Minutes` expires, another executable quest is
  selected with the current task excluded. If no alternative exists, the
  current target, map, and requirements are validated again; a valid task
  continues with fresh timers, while an invalid task is released for a full
  rescan.
- A task that fails target validation or makes no progress for the fixed
  five-minute health check is placed on a P-session abnormal list. Every
  subsequent scan excludes it. The list and reasons are written to Quest Debug
  when P is disabled, then reset for the next P session.
- The selected target map is also retained by string ID while it continues to
  contain a valid current-stage target. Category quests such as `KillGiants`
  and elemental kills therefore do not bounce between multiple valid maps;
  the map lock changes only after the previous map stops satisfying the task.
- Random Daily quests reuse the same internal ID, so their lock identity also
  includes runtime type, quest type, exact monster/type target, and goal. A new
  Daily cannot be mistaken for the completed locked Daily. Changes in other
  Daily slots do not release the current lock; only disappearance or
  replacement of the locked Daily triggers a rescan.
- If the game has not initialized its quest-list data yet, it is refreshed
  only while the quest panel is closed; the active quest UI is never re-entered.
- Completed or claimed entries trigger the same closed-panel refresh so newly
  unlocked follow-up quests can be selected without reopening the quest menu.
- Exact enemy targets are resolved through the game's enemy objects. Required
  evolution stages and destination-map availability must already be unlocked.
- Target selection supports exact `enemyToKill` objectives, native enemy-type
  objectives such as Fire, Electric, and Dark, plus flying-enemy and giant
  categories. Matching uses each map's currently unlocked evolution stage.
- Exact enemies are matched by their internal game name. Bestiary indices are
  not treated as unique because some runtime definitions share them.
- Generic `AnyEnemy`, box, collection, and other unresolvable objectives are
  ignored and cannot hide a later resolvable target.
- Generic arrow-kill objectives are treated as executable kill quests on the
  current dimension. While one is locked, automatic Rage activation remains
  suppressed until the quest completes or leaves the active quest list, so
  normal bow kills can continue. An already-running Rage ends naturally.
- Character-specific kill quests are executable when their required character
  is unlocked. If the current base character does not match, automatic Rage
  refresh is suppressed and an active Rage is allowed to end naturally before
  `PlayerSkinManager.ApplySkin` is called. The switch uses the same stable
  Runner-scene and world-blocker requirements as Portal preparation, but does
  not depend on Portal cooldown. Automatic Rage may resume after a 0.5-second
  post-switch stability window.
- Any outfit belonging to the required base character is accepted. The mod
  does not restore the previous character after the quest completes. Required
  character identity is stored in the quest lock, while IL2CPP skin objects are
  re-read from the current quest snapshot rather than cached across scenes.
- When a successful scan finds no executable kill quest, Quest Automation uses
  the game's own `PopupPortals.GetMapScores` calculation to compare the current
  soul score of every map in `GetAvailableMaps(true)`. It travels to the
  highest-scoring Portal-selectable map, then keeps scanning there. This uses
  the same live enemy stages and event multipliers behind the Portal star UI;
  ties prefer the current map to avoid unnecessary travel.
- Soul-fallback travel obeys the native Portal cooldown, world blockers, scene
  guards, and the configured minimum dimension-stay time. A newly executable
  kill quest cancels a fallback trip that has not spawned its Portal yet.
- Exact targets may open a normal Portal when its native cooldown is zero.
- Destination resolution accepts only the game's standard `Map` dimensions.
  Bonus stages, boss maps, villages, story areas, and minigames are excluded
  even when their internal enemy lists contain the requested monster.
- Other dimensions are considered only when returned by the game's current
  `GetAvailableMaps(false)` Portal-destination list. An unlocked map that the
  current Portal cannot select is not a valid automation destination.
- If travel is required during Rage Mode, automatic Rage refresh is paused and
  the current execution is allowed to end naturally. The quest and destination
  are revalidated before opening the Portal.
- The travel-intent lock remains active from the decision to travel until the
  Portal transition begins, so transient target resolution cannot restart Rage.
  Only the manual `J` path force-ends an active Rage execution.
- A quest Portal is spawned only while the native Portal button is visible and
  interactable, its cooldown is zero, and no active Portal already exists.
  World events that can create another transition, including special random
  boxes and minigame items, also block quest-Portal preparation.
- No IL2CPP quest, enemy, map, or Portal object is retained across travel. The
  existing scene guards resume Boost after 0.5 seconds and Rage after 2 seconds.
- If the player is moved to another dimension, the locked target is retained
  and normal Portal travel can return to its dimension when available.
- Quest scanning pauses in villages, minigames, boss scenes, and every other
  unsupported GameState. It resumes after the central Runner/Rage scene has
  remained stable for two seconds. Rage Mode and ordinary overlay menus do not
  pause task discovery; Portal spawning still requires the native Portal button
  to be active and interactable.
- Pause diagnostics include the exact GameState, PopupSlider, or map-stability
  condition that caused task automation to freeze.
- Quest scene-state diagnostics are silent while Quest Automation is disabled.
  Repeated `BattleMode` pause/resume pairs are limited to one pair per minute
  while it is enabled; automation still pauses on every actual transition.
- If a Bonus Stage or another scene interrupts travel preparation before the
  quest Portal is spawned, only the travel-intent lock is cleared. The task
  lock remains and the destination is recalculated after returning.
- Required-character correction is checked every 0.25 seconds while Runner is
  stable, independently of the five-second task scan. Rage still ends
  naturally before switching. Applying a character also refreshes the live
  player skin immediately instead of only persisting it for the next launch.
  Random boxes remain silent travel blockers in
  Quest Debug; only a confirmed map event logs its event type and lifecycle.
- Map events are confirmed across all loaded event objects when either the
  game's `RandomEvent.IsActive()` rule is true or its timer remains positive.
  Logs include event type, internal name, and remaining time, avoiding missed
  Horde, coin, Frenzy, Gemstone Rush, Lucky Coins, and Dual Randomness events.
- Map-bound event `OnEventStart` and `OnEventEnd` methods are also observed
  directly through Harmony, following AutoRageMode's proven Horde detection
  approach. Only string descriptions are retained; no IL2CPP event object is
  cached across scenes. Polling remains a fallback for events already active
  when the runtime starts.
- Automatic travel waits at least one minute by default before changing away
  from a dimension reached by an earlier automatic trip.
- Quest Automation keeps completion statistics for each `P` session. The
  counters reset when `P` is enabled and a final summary is written when it is
  disabled. Any normal or Daily quest positively observed as completed or
  actually claimed while Quest Automation is enabled is counted, including a
  claim initiated by another mod. Weekly quests and mere disappearance from
  the active list are excluded, and duplicate claim callbacks are deduplicated.
- Daily and non-Daily completions are counted separately. Every counted
  completion writes its localized name and internal quest ID to the normal
  user log, writes the same message to Quest Debug
  when enabled, and shows an in-game notification with the session total,
  Daily count, and Normal count. `Show Completion Notifications` can disable
  only the in-game notification; both log outputs remain unchanged.

## Configuration

The configuration file is generated at:

```text
ModLoader/UserData/AutoAdventurer.cfg
```

Settings include the automation toggle key, manual stop key, activation check
interval, maximum Rage duration, and debug logging. The default maximum Rage
duration is 120 seconds. Set it to `0` to disable the duration limit. The
post-Rage world observation delay is separately configurable.

Quest Automation has an independent toggle key and a configurable minimum
dimension-stay time. It respects the game's native Portal cooldown and never
resets or bypasses it. Its completion-statistics notification can be enabled
or disabled independently and is enabled by default.

## Building

From this directory:

```powershell
dotnet build --no-restore
```

The build creates the DLL and packaged ZIP without deploying by default. Local
deployment is opt-in with `/p:EnableLocalDeploy=true`.

## Versioning

- Public release version: `1.0.0`
- Internal development version: `V0.73`

## Disclaimer

This is an unofficial community mod. Idle Slayer and its assets belong to their
respective owners.
`Gameplay / Auto Boss` enables automatic boss dialogue advancement, reduces
the active boss to 1 HP, and performs the finishing attack with the game's
direct arrow action. It avoids the contextual Attack action, which can resolve
as a jump in Boss mode. Disable it to leave boss health and combat untouched.

AutoAdventurer does not claim completed quests. Claiming is left to the game or
another mod such as AutoProgression. AutoAdventurer only observes successful
claim events while Quest Automation is enabled so it can update session
completion statistics without modifying quest state. Weekly Quest claim and
reroll methods are not patched or observed by AutoAdventurer.

`Quest Automation / Maximum Quest Time Minutes` limits the total time spent on
one selected quest before a full rescan. Independently of configuration, a
second watchdog checks progress every five minutes and rescans if the locked
quest made no progress. Character requirements are still revalidated on every
normal scan.

Quest Automation pauses dimension travel during map-bound Random Events such
as Hordes, coin waves, Gemstone Rush and Frenzy. Long-lived numeric CpS, Souls,
Equipment and Coin Value bonuses do not block travel. Its quest lock is
retained, while travel-related Rage suppression is released until the event
ends and all conditions are evaluated again.

Normal, Silver, Golden and Special activity Random Boxes pause dimension
travel from the moment their active object appears, covering the delay before
their Random Event or special-stage result becomes active.
