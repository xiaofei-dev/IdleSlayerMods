# Quick Start and Controls

## Start Playing

1. Install AutoBonusRunner and launch the game once.
2. Keep `Mode = "Auto"` for normal unattended use.
3. Enter Bonus Stage 1, 2, or 3.
4. AutoBonusRunner takes control when supported Bonus gameplay becomes active.

No focus is required; route input can continue while the game window is in the
background.

## Toggle Key

Press `U` to disable or re-enable automatic control. The game displays a
notification confirming the new state.

The key is read from `Toggle Key`. An invalid Unity key name falls back to `U`
and writes a warning.

## Startup State

- `Enabled On Startup = true`: automatic control begins enabled.
- `Enabled On Startup = false`: press the toggle key before use.

Disabling control releases mod-owned jump input immediately. Detection,
logging, and manual-jump observation remain active.

## Start Slider

With `Skip Start Slider = true`, the mod waits approximately one second after
the Bonus Stage start slider appears. It confirms the slider only if the same
slider is still visible.

## Recommended Profiles

### Normal unattended play

```ini
Mode = "Auto"
"Auto Retry Enabled" = false
"Skip Start Slider" = true
```

### Full-route testing

```ini
Mode = "Manual"
"Debug Mode" = true
```

### Fast completion in every Bonus Stage

```ini
Mode = "Skip"
```

[Back to the Complete Manual](../MANUAL.md)

