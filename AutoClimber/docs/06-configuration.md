# Configuration Reference

The configuration file is created after the first game launch:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoClimber.cfg
```

## AutoClimber Section

| Setting | Default | Description |
|---|---:|---|
| `Configuration Version` | Managed | Internal preference migration version. Do not edit. |
| `Debug Mode` | `false` | Enables detailed route, input, object, and failure diagnostics. |

User actions, warnings, errors, and run summaries remain visible when Debug
Mode is disabled. Debug output is automatically suppressed in Skip mode.

## Automation Section

| Setting | Default | Description |
|---|---:|---|
| `Mode` | `Normal` | Selects `Normal`, `Auto`, or `Skip` behavior. |
| `Enabled On Startup` | `true` | Starts automation enabled. |
| `Toggle Key` | `Y` | Keyboard key used to enable or disable AutoClimber. |
| `Auto Retry Enabled` | `false` | Continues after failure when true; chooses No and exits when false. |
| `Skip Start Slider` | `true` | Waits one second, then confirms an Ascending Heights start slider only if it remains visible. |
| `Target Enemies` | `true` | Allows enemy detours only when route safety remains acceptable. |

## Default File

```ini
[AutoClimber]
"Configuration Version" = 10
"Debug Mode" = false

[Automation]
Mode = "Normal"
"Enabled On Startup" = true
"Toggle Key" = "Y"
"Auto Retry Enabled" = false
"Skip Start Slider" = true
"Target Enemies" = true
```

## Editing Rules

- Close or restart the game after editing so every value is loaded cleanly.
- Use Unity key names such as `Y`, `F8`, or `Keypad1` for `Toggle Key`.
- Invalid toggle keys fall back to `Y` and produce a warning.
- Mode values are normalized to the supported modes.
- Old Boolean Skip and Automatic Quest settings are migrated automatically.

[Back to the Complete Manual](../MANUAL.md)
