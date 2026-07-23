# Quick Start and Controls

## Recommended Start

1. Enter a normal running dimension.
2. Press `K` to enable Automatic Rage.
3. Auto Movement & Combat starts enabled by default; press `L` whenever you want to disable or re-enable it.
4. Press `P` to enable Quest Automation.
5. If AutoProgression is installed, keep completed-quest claiming enabled so
   follow-up quests can unlock automatically.

## Key Behavior

### `P` — Quest Automation

- Starts a new completion-statistics session when enabled.
- Prints session totals and abnormal tasks when disabled.
- Clears the session task lock and abnormal exclusion list when disabled.

### `K` — Automatic Rage

- Enables or disables future automatic Rage activations.
- Disabling it does not end a Rage execution already in progress.

### `J` — Immediate Rage Stop

- This is the only AutoAdventurer path that force-ends Rage Mode.
- Keys, time limits, and travel preparation wait for Rage to end naturally.

### `L` — Auto Movement & Combat

- Enables or disables minimum-height jumping, automatic arrows, and the
  selected movement ability according to their configuration switches.
- Requests an immediate supported Boost/Wind Dash activation when enabled and
  safe.
- Follows the player's selected Boost or Wind Dash ability.
- Runs only in the central Runner/Rage scene.

## Independent Features

The following settings default to enabled and do not depend on `P`, `K`, or
`L`:

- `Skip Bonus Start Slider`
- `Auto Boss`

## Menus and Scenes

Ordinary menus over the central Runner/Rage scene do not pause character or
quest automation. Villages, minigames, boss maps, story scenes, and true
GameState changes use their own pause or specialized behavior.

[Back to the Complete Manual](../MANUAL.md)
