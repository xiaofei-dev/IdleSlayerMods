# Quick Start and Controls

## Start Playing

1. Install AutoClimber and launch the game once.
2. Keep `Mode = "Normal"` for the full Ascending Heights route.
3. Enter the minigame normally or through practice mode.
4. AutoClimber takes control only after Ascending Heights becomes active.

No focus is required; route input can continue while the game is in the
background.

## Toggle Key

Press `Y` to enable or disable AutoClimber. The configured key is read from
`Toggle Key`, and the game displays a notification confirming the new state.

An invalid key name falls back to `Y` and writes a warning to the log.

## Startup State

- `Enabled On Startup = true`: automation begins enabled.
- `Enabled On Startup = false`: press the toggle key before use.

Disabling AutoClimber releases its movement input. It does not change the
game's native Ascending Heights state.

## Recommended Profiles

### Full rewards and enemies

```ini
Mode = "Normal"
"Target Enemies" = true
"Skip Start Slider" = true
```

### Fast completion

```ini
Mode = "Skip"
```

### Quest-aware automation

```ini
Mode = "Auto"
"Target Enemies" = true
"Skip Start Slider" = true
```

Auto plays the full route only when a compatible unfinished Ascending Heights
enemy quest is detected before the run.

[Back to the Complete Manual](../MANUAL.md)
