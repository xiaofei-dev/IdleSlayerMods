# Configuration Reference

Configuration file:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoAdventurer.cfg
```

## AutoAdventurer

| Setting | Default | Description |
|---|---:|---|
| `Configuration Version` | `26` | Internal migration version; do not edit |
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
| `Auto Align Elemental Divinities` | `true` | While `P` is enabled, every five seconds align to the first active elemental task in the live list, correcting manual changes and activating an available affordable element when none is active; this never changes task priority or replaces the current lock |
| `Minimum Dimension Stay Minutes` | `0` | Minimum stay after automatic arrival |
| `Maximum Quest Time Minutes` | `5` | Maximum time on one locked task; `0` disables the limit |

## Silver Box Automation

These global settings run every five seconds after the game loads and do not
depend on `P` or any other hotkey.

| Setting | Default | Description |
|---|---:|---|
| `Enabled` | `true` | Master switch for this section; when disabled, both rules are inactive and Silver Bank remains under manual control |
| `Auto Release Silver Box Lock` | `true` | Force Silver Bank off whenever an active normal task requires Silver Random Boxes |
| `Permanent Release Above Divinity Points` | `0` | Disable Silver Bank above this normalized available-point threshold and enable it again at or below the threshold when affordable; `0` disables threshold control without forcing either state |

## Example

```ini
[AutoAdventurer]
"Configuration Version" = 26
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

["Silver Box Automation"]
"Enabled" = true
"Auto Release Silver Box Lock" = true
"Permanent Release Above Divinity Points" = 0.0

["Auto Boost"]
"Toggle Key" = "L"
"Activation Delay Seconds" = 0.1
"Wind Dash Require Grounded" = true

["Quest Automation"]
"Toggle Key" = "P"
"Show Completion Notifications" = true
"Auto Align Elemental Divinities" = true
"Minimum Dimension Stay Minutes" = 0.0
"Maximum Quest Time Minutes" = 5.0
```

The activation delay is stored as a double to avoid long single-precision
representations such as `0.30000001192092896` after migration.

[Back to the Complete Manual](../MANUAL.md)
