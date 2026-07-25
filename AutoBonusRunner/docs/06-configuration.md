# Configuration Reference

The configuration file is created after the first game launch:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoBonusRunner.cfg
```

## AutoBonusRunner Section

| Setting | Default | Description |
|---|---|---|
| `Configuration Version` | Managed | Internal preference migration version. Do not edit. |
| `Debug Mode` | `false` | Enables detailed route, input, physics, wall, landing, and completion diagnostics. |

User actions, warnings, errors, and run summaries remain logged when Debug
Mode is disabled.

## AutoBonusRunner Automation Section

| Setting | Default | Description |
|---|---|---|
| `Mode` | `Auto` | Selects `Auto`, `Manual`, or `Skip` requirement behavior. |
| `Enabled On Startup` | `true` | Starts automatic Bonus Stage control enabled. |
| `Toggle Key` | `U` | Keyboard key used to disable or re-enable automatic control. |
| `Auto Retry Enabled` | `false` | Chooses native Continue when true or No when false. |
| `Skip Start Slider` | `true` | Waits one second, then confirms the same visible start slider. |

Automatic jumping, reward actions, and completion Wind Dash are built-in
behaviors and are not separate configuration switches.

## Default File

```ini
[AutoBonusRunner]
"Configuration Version" = 44
"Debug Mode" = false

[AutoBonusRunner Automation]
Mode = "Auto"
"Enabled On Startup" = true
"Toggle Key" = "U"
"Auto Retry Enabled" = false
"Skip Start Slider" = true
```

## Editing Rules

- Restart the game after editing so every value is loaded consistently.
- Valid modes are `Auto`, `Manual`, and `Skip`; matching is
  case-insensitive.
- Use Unity key names such as `U`, `F8`, or `Keypad1` for `Toggle Key`.
- Invalid keys fall back to `U` and produce a warning.
- Retired automatic-jump, reward-action, and Wind Dash entries are deleted
  during configuration migration.

[Back to the Complete Manual](../MANUAL.md)

