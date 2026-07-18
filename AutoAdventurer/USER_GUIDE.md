# AutoAdventurer User Guide

AutoAdventurer automates active gameplay in Idle Slayer: quest selection,
dimension travel, Rage Mode, Boost/Wind Dash, Bonus helpers, and boss fights.

This guide is written for players who want to install the mod and start using
it without reading implementation details. For every setting and diagnostic
rule, see the [Complete Manual](MANUAL.md).

## What AutoAdventurer Does

- Finds kill quests that your current save can actually complete.
- Prioritizes quests that unlock more quests or other new content.
- Travels to a valid unlocked dimension when the target is elsewhere.
- Automatically uses Rage Mode, Boost, or Wind Dash when enabled.
- Waits safely around keys, boxes, map events, portals, and scene transitions.
- Supports compatible character-specific, arrow, elemental, flyer, giant, and
  evolved-enemy quests.
- Can skip supported Bonus start sliders and automatically finish boss fights.
- Tracks quests completed during each Quest Automation session.

AutoAdventurer does **not** claim completed quests. Use the game or
AutoProgression to claim them.

## Installation

### Idle Slayer Mod Manager (recommended)

1. Install and initialize Idle Slayer Mod Manager.
2. Install Idle Slayer Mods Core.
3. Import the AutoAdventurer ZIP without extracting it.
4. Enable AutoAdventurer and start the game.

### Manual installation

Place `AutoAdventurer.dll` in:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\Mods\
```

## Default Keys

| Key | Action |
|---|---|
| `P` | Enable or disable Quest Automation |
| `K` | Enable or disable Automatic Rage |
| `J` | Immediately end the current Rage Mode |
| `L` | Enable or disable Auto Boost |

The keys can be changed in `AutoAdventurer.cfg`.

## Recommended First Setup

1. Enter a normal running dimension.
2. Press `K` to enable Automatic Rage.
3. Press `L` to enable Auto Boost.
4. Press `P` to enable Quest Automation.
5. Keep completed-quest claiming enabled in AutoProgression if you use both
   mods.

The three key-controlled systems start disabled each time the mod runtime is
created. Auto Boss and Bonus Slider Skip are configuration-controlled and are
enabled by default.

## How Quest Automation Chooses a Task

AutoAdventurer first removes tasks that cannot currently be completed. A task
must have a supported kill objective, an unlocked enemy or evolution, an
available required character, and a valid normal Portal destination.

Only then does it compare reward priority:

1. **Top** — unlocks additional quest levels.
2. **High** — unlocks equipment, characters, minions, giants, NPCs, loadouts,
   or another new system.
3. **Normal** — gives a numeric upgrade or no unlock reward.

Within the same priority, the quest with the smallest total kill requirement
is selected. Once selected, the task remains locked until it completes,
becomes invalid, is replaced, or reaches its configured time limit.

## Why the Mod Sometimes Waits

Waiting is usually an intentional safety rule. AutoAdventurer may be waiting
for:

- Rage Mode to end naturally.
- The post-Rage protection period.
- The normal Portal cooldown.
- A map event or Random Box outcome.
- A Chest Hunt Key, minigame item, door, or existing Portal.
- A scene transition to finish.
- Wind Dash to reach the ground.
- The minimum stay time after automatic dimension travel.

Except for the manual `J` key, the mod never force-ends Rage Mode.

## Automatic Rage

- Press `K` once to enable it and again to disable it.
- Disabling it stops future activations but lets the current Rage end naturally.
- Press `J` only when you intentionally want to end the current Rage
  immediately.
- Keys, travel preparation, and configured duration limits stop Rage refreshes
  and wait for the execution to finish naturally.

## Auto Boost and Wind Dash

- Press `L` to enable or disable Auto Boost.
- The mod follows whichever supported movement ability the player selected.
- Wind Dash can wait for ground contact before activating.
- In minigames and reward sections, Wind Dash is allowed only while the game's
  main ability icon is actually visible.
- Normal Boost is not automatically used through the minigame-specific path.

## Bonus and Boss Features

- **Skip Bonus Start Slider** confirms supported timing sliders once they are
  ready.
- **Auto Boss** advances supported boss dialogue, reduces the boss to 1 HP,
  and performs the finishing arrow attack.
- Both features can be disabled independently in the configuration file.

## Configuration File

Launch the game once to create:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoAdventurer.cfg
```

Useful defaults:

| Setting | Default |
|---|---:|
| Rage activation check | 12 seconds |
| Maximum automatic Rage cycle | 120 seconds |
| Post-Rage protection | 5 seconds |
| Boost activation delay | 0.3 seconds |
| Wind Dash requires ground | Enabled |
| Minimum automatic dimension stay | 1 minute |
| Maximum time on one quest | 5 minutes |

See the [Configuration Reference](docs/08-configuration.md) for every option.

## Quick Troubleshooting

### Rage is not activating

Check the log for the last `Automatic Rage enabled/disabled` message. A quest
line containing `automaticRageSuppressed=False` only means that the quest is
not blocking Rage; it does not mean the `K` toggle is enabled.

### Boost is not activating

Confirm that Auto Boost is enabled, the selected ability is unlocked, its
cooldown is ready, and Wind Dash has reached the ground if required.

### Quest Automation is not travelling

It may be waiting for the Portal cooldown, Rage, an event, a box, a scene
transition, the minimum stay timer, or an interactable Portal button.

### A Portal request timed out

The mod did not detect arrival within 60 seconds. It releases the request
safely and can retry the locked task. An occasional timeout is recoverable.

For detailed diagnosis, see [Troubleshooting](docs/10-troubleshooting.md) and
[Logging](docs/09-logging.md).

## Full Automation Suite

- **AutoAdventurer** handles active gameplay and quest objectives.
- **AutoProgression** handles purchases, Ascension, craftables, materials,
  eggs, and quest claiming.
- **AutoClimber** handles Ascending Heights routes, rewards, and compatible
  quest enemies.

Each mod works independently. Together, they cover complementary parts of a
fully automated Idle Slayer setup.

## Support Development

If these mods save you time, you can support continued development through
[PayPal](https://www.paypal.com/donate/?business=HK85PL8AREEXY&no_recurring=0&currency_code=USD).

