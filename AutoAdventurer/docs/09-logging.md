# Logging Reference

## Log Categories

### User Log

Important actions appear without a special debug marker:

```text
[AutoAdventurer] Automatic Rage enabled.
[AutoAdventurer] Quest travel arrived: map=map_factory.
[AutoAdventurer] Quest completed: ... Session total: ...
```

### Main Debug

Enable `[AutoAdventurer] Debug Mode`:

```text
[AutoAdventurer] [Debug][Movement] Ability triggered: selected=WindDash; cooldown=...
[AutoAdventurer] [Debug][Rage] Automatic Rage blocker detected: Portal ...
```

Detailed messages are grouped by subsystem:

| Category | Purpose |
|---|---|
| `Config` | Parsed numeric settings and migration results |
| `Runtime` | Runtime lifecycle and scene readiness |
| `Movement` | Jump, arrow, Boost, Wind Dash, and box-targeting decisions |
| `Rage` | Rage readiness, execution, refresh, and blockers |
| `Quest` | Quest discovery, selection, locks, travel, and progress |
| `Element` | Elemental quest and Dark Divinity alignment |
| `SilverBox` | Silver Bank and Silver Random Box task control |
| `Boss` | Boss health and result-screen automation |
| `Gameplay` | Other global gameplay helpers |

Quest and all other diagnostics use the single main `Debug Mode` setting:

```text
[AutoAdventurer] [Debug][Quest] Quest selected: ...
```

Exact repeated Debug messages are emitted at most once every 10 seconds.
Warnings and errors use a 30-second duplicate window. When logging resumes, the
message includes the number of identical repeats that were suppressed.

Safely caught exceptions produce one concise error line in every mode. The
full managed/native exception detail is emitted only when `Debug Mode` is
enabled under `[Debug][Exception]`.

## Quest Selection Fields

| Field | Meaning |
|---|---|
| `quest` | Localized name and internal ID |
| `questType` | Internal quest type |
| `objective` | Original target ID |
| `matchedEnemy` | Current enemy stage selected for the objective |
| `currentMap` | Current dimension |
| `targetMap` | Planned dimension |
| `mapState` | On target or travel required |
| `progress` | Current and required amount |
| `remainingKills` | Remaining count |
| `unlockReward` | Reward Upgrade name and ID |
| `rewardBenefit` | Internal reward effect ID |
| `rewardPriority` | `Top`, `High`, or `Normal` |
| `requiredCharacter` | Required character ID |
| `automaticRageSuppressed` | Whether quest state currently blocks new Rage |

## Common Messages

### `Travel lock acquired`

A destination was selected. New Rage is suppressed while safe travel is
prepared.

### `waiting for Rage Mode to end naturally`

Rage is still running and will not be force-ended.

### `Post-Rage protection scheduled`

The mod is waiting for delayed boxes, events, entrances, or a fresh task scan.

### `Map event detected`

Dimension travel is paused and Rage may serve the event.

### `Quest travel ... timed out`

Arrival was not detected within 60 seconds. The request was released safely
and can be retried.

### `automaticRageSuppressed=False`

The quest is not blocking Rage. This does **not** mean the `K` toggle is
enabled. Use the latest `Automatic Rage enabled/disabled` message.

## Garbled Localized Names

Console encoding can display localized quest names incorrectly. Automation
does not depend on those strings; internal quest, target, map, and reward IDs
remain usable for diagnosis.

[Back to the Complete Manual](../MANUAL.md)
