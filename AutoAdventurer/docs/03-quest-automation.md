# Quest Automation

Press `P` to enable or disable Quest Automation. Only kill quests that are
proven executable are considered.

## Eligibility Validation

A quest must pass every relevant check before ranking:

1. It is active, incomplete, valid, and unclaimed.
2. Its quest type is supported.
3. Its target enemy or category can be resolved from internal game objects.
4. A required evolution is unlocked; a later descendant in the same evolution
   chain can satisfy an earlier-stage requirement.
5. A required character is unlocked.
6. A valid target map currently contains a matching enemy.
7. If travel is required, the destination is a normal `Map` currently offered
   by the native Portal.

An impossible task cannot win because of its reward.

## Supported Objectives

- Exact enemy kills
- Exact enemy evolution stages
- Elemental or native enemy-type kills
- Flyers
- Giants
- Arrow kills
- Resolvable Rage kills
- Resolvable Wind Dash kills
- Character-specific kills for unlocked characters

## Ignored Objectives

- Unresolvable generic targets such as `AnyEnemy`
- Boxes, collections, and other non-kill objectives
- Tasks requiring an unavailable character
- Locked exact evolution stages that the current stage cannot satisfy
- Targets limited to Bonus, boss, village, story, or other non-Portal maps
- Destinations absent from the current native Portal list
- Weekly Quests, for both execution and completion statistics

## Reward Priority

Priority is applied only after full eligibility validation:

| Priority | Reward |
|---|---|
| `Top` | Unlocks additional quest levels (`IncreaseQuestsLevel`) |
| `High` | Unlocks equipment, characters, minions, giants, NPCs, Stones of Time, Ascension upgrades, loadouts, or another explicit content system |
| `Normal` | Gives a numeric improvement or no unlock reward |

Within the same priority, the quest with the smallest **total kill
requirement** is selected. Exact ties keep game-list order.

CpS, Coins, Souls, Rage, Boost, Wind Dash, drop-rate, damage, and offline
numeric improvements remain Normal priority.

## Quest Locking

- The lock combines internal ID, runtime type, target, character, and goal.
- A selected quest remains locked instead of changing every scan.
- Daily identity includes additional runtime data because multiple Daily slots
  can share the same base ID.
- Manually rerolled Daily quests are detected as removed or replaced.
- Scene interruptions retain a valid task lock.

## Time Limits and Abnormal Tasks

- `Maximum Quest Time Minutes` defaults to 10 minutes.
- When it expires, the current task is excluded while alternatives are tested.
- If no alternative exists, the target is validated again; a valid task may
  continue with refreshed timers.
- A separate watchdog checks progress every 5 minutes.
- Invalid or non-progressing tasks enter the current `P` session's abnormal
  list and are excluded from later scans.
- Disabling `P` prints and resets the abnormal list.

## Character Tasks

- Only unlocked required characters are accepted.
- Automatic Rage refresh is suppressed while a character correction is needed.
- An active Rage execution ends naturally before switching.
- Any outfit belonging to the required base character is accepted.
- The previous character is not restored after completion.

## Arrow Tasks

Generic arrow-kill tasks suppress new automatic Rage activations so ordinary
bow kills can continue. A Rage execution already running still ends naturally.

## Completion Statistics

- Counters reset each time `P` is enabled.
- Daily and normal completions are counted separately.
- Weekly completions are excluded.
- A task must be positively observed as completed or successfully claimed
  while the session is active.
- Claims initiated by AutoProgression or another path can be observed.
- Disabling `P` prints the final total.
- In-game notifications can be disabled without disabling logs.

## No Executable Quest

The mod reads the same live Souls score used by the Portal star display and
selects the highest-scoring currently available dimension. A tie prefers the
current map to avoid unnecessary travel. Quest scanning continues there.

[Back to the Complete Manual](../MANUAL.md)
