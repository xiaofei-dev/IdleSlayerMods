# Logging Reference

## Log Categories

### User Log

Important actions appear without a special debug marker:

```text
[AutoAdventurer] Automatic Rage enabled.
[AutoAdventurer] Quest travel arrived in map_factory.
[AutoAdventurer] Quest completed: ... Session total: ...
```

### Main Debug

Enable `[AutoAdventurer] Debug Mode`:

```text
[AutoAdventurer] [Debug] Auto Boost triggered WindDash; cooldown=...
[AutoAdventurer] [Debug] Automatic Rage blocker detected: Portal ...
```

### Quest Debug

Quest diagnostics use the main `[AutoAdventurer] Debug Mode` setting:

```text
[AutoAdventurer] [Debug][Quest] Quest selected: ...
```

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
