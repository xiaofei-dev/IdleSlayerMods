# Troubleshooting

## Automatic Rage Does Not Activate

Check in order:

1. The latest toggle message says `Automatic Rage enabled`.
2. No key, Portal, minigame item, or Special Random Box is blocking it.
3. The 2-second post-scene stabilization has completed.
4. No travel, character correction, or arrow task suppresses it.
5. Rage cooldown is ready.
6. The current maximum-duration cycle is not waiting to end naturally.

`automaticRageSuppressed=False` does not prove that the `K` toggle is enabled.

## Auto Movement & Combat Does Not Activate

1. Confirm `Auto Movement & Combat enabled` is the latest toggle state.
2. Confirm the selected Boost or Wind Dash is unlocked.
3. Wait for ground contact if grounded Wind Dash is enabled.
4. Confirm the game is in the central Runner/Rage scene; minigames are excluded.
5. After Ascension, wait for the 5-second safety suspension and ability
   stability checks.

## Quest Automation Does Not Travel

Quest Debug may show that the mod is waiting for:

- Native Portal cooldown
- An interactable Portal button
- Rage to end naturally
- Post-Rage protection
- A box, event, key, or entrance
- Minimum dimension-stay time
- A stable supported scene
- A destination in the native Portal list

## Quest Selection Looks Wrong

Inspect:

```text
rewardPriority=
remainingKills=
matchedEnemy=
targetMap=
```

Ranking is `Top > High > Normal`, then smallest total kill requirement. A task
already locked is not replaced merely because a new task appears.

## Completion Does Not Advance to the Next Quest

- AutoAdventurer does not claim completed quests.
- Confirm the game or AutoProgression claimed the task.
- The quest list is refreshed only through the safe closed-panel path.
- A manually rerolled Daily is recognized on a later scan.

## Portal Timeout

`Quest travel ... timed out` means arrival was not detected within 60 seconds.
The old request is released and can retry. One occasional timeout is normally
recoverable; repeated timeouts require the full surrounding log.

## Travel Occurred During an Event

Provide log lines containing:

- `Map event detected/ended`
- `Travel lock acquired`
- `opened a portal`
- Event remaining time

Event end requires the last observed timer and consecutive missing checks; one
failed Unity lookup must not end the guard.

## Freeze or Crash

1. Preserve the full MelonLoader log.
2. Note the GameState, map, Rage state, and whether a Portal, menu, boss, or
   Ascension had just occurred.
3. Use `P`, `K`, and `L` separately to isolate Quest, Rage, and Boost behavior.
4. Never replace the DLL while the game is running.
5. Confirm compatible MelonLoader and Idle Slayer Mods Core versions.

## What to Include in a Bug Report

- Time of the problem
- Full log file, not only the last error
- Current map and special scene
- Whether `P`, `K`, and `L` were enabled
- Relevant configuration section
- Reproduction steps

Default log directory:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\MelonLoader\Logs\
```

[Back to the Complete Manual](../MANUAL.md)
