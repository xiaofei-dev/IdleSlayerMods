# Configuration Reference

Configuration file:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoAdventurer.cfg
```

## AutoAdventurer

| Setting | Default | Description |
|---|---:|---|
| `Configuration Version` | `20` | Internal migration version; do not edit |
| `Debug Mode` | `false` | All detailed diagnostics, including Quest Automation; user actions, warnings, and errors always log |

## Automatic Rage

| Setting | Default | Description |
|---|---:|---|
| `Toggle Key` | `K` | Toggle Automatic Rage |
| `Stop Key` | `J` | Force-end the current Rage execution |
| `Activation Check Interval Seconds` | `12` | Rage activation and refresh interval |
| `Maximum Rage Duration Seconds` | `20` | Maximum automatic cycle; `0` disables the limit |
| `Post Rage Protection Seconds` | `8` | Shared delay for quests, boxes, events, entrances, travel, and the next Rage |

## Gameplay

| Setting | Default | Description |
|---|---:|---|
| `Skip Bonus Start Slider` | `true` | Confirm supported minigame start sliders |
| `Auto Boss` | `true` | Set boss to 1 HP, advance dialogue, and finish the fight |

## Auto Boost

| Setting | Default | Description |
|---|---:|---|
| `Toggle Key` | `L` | Toggle Auto Boost |
| `Activation Delay Seconds` | `0.1` | Delay after cooldown reaches zero |
| `Wind Dash Require Grounded` | `true` | Require ground contact for automatic Wind Dash |

## Quest Automation

| Setting | Default | Description |
|---|---:|---|
| `Toggle Key` | `P` | Toggle quest selection and guided travel |
| `Show Completion Notifications` | `true` | Show in-game session totals; logs remain enabled |
| `Minimum Dimension Stay Minutes` | `0` | Minimum stay after automatic arrival |
| `Maximum Quest Time Minutes` | `10` | Maximum time on one locked task; `0` disables the limit |

## Example

```ini
[AutoAdventurer]
"Configuration Version" = 20
"Debug Mode" = false

["Automatic Rage"]
"Toggle Key" = "K"
"Stop Key" = "J"
"Activation Check Interval Seconds" = 12.0
"Maximum Rage Duration Seconds" = 20.0
"Post Rage Protection Seconds" = 8.0

[Gameplay]
"Skip Bonus Start Slider" = true
"Auto Boss" = true

["Auto Boost"]
"Toggle Key" = "L"
"Activation Delay Seconds" = 0.1
"Wind Dash Require Grounded" = true

["Quest Automation"]
"Toggle Key" = "P"
"Show Completion Notifications" = true
"Minimum Dimension Stay Minutes" = 0.0
"Maximum Quest Time Minutes" = 10.0
```

The activation delay is stored as a double to avoid long single-precision
representations such as `0.30000001192092896` after migration.

[Back to the Complete Manual](../MANUAL.md)
