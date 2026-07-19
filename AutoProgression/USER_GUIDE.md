# AutoProgression User Guide

AutoProgression automates repeatable account progression in Idle Slayer:
normal Ascension, skills, normal equipment, temporary craftables, materials,
eggs, quest maintenance, and the two paid 500x bonuses.

This guide is for players who want to install the mod and use it safely. For
every setting and behavior, see the [Complete Manual](MANUAL.md).

## What AutoProgression Does

- Performs normal Ascension at a configurable pending-SP percentage.
- Can spend remaining Slayer Points through the Ascension-tree Buy All action.
- Buys available shop skills and levels unlocked normal equipment.
- Maintains supported timed craftables and refreshes Rage with Rage Pills.
- Uses Scrap and Dragon Scale overflow without exceeding the configured
  maximum effect duration.
- Opens Dragon and Simurgh Eggs in the background while preserving reserves.
- Claims completed Daily and Weekly Quests and regenerates exhausted sets.
- Filters selected newly generated Daily Quests and selects the 180,000-kill
  Rage Weekly Quest.
- Can reset the normal Portal cooldown and keep quest rerolls available.
- Can purchase the two 500x Jewels of Soul bonuses.
- Can claim and resend Minion missions and automatically prestige eligible
  Minions at their maximum level.

AutoProgression never performs Ultra Ascension, travels between dimensions,
controls the character, or fights quest targets.

## Installation

### Idle Slayer Mod Manager (recommended)

1. Install and initialize Idle Slayer Mod Manager.
2. Install Idle Slayer Mods Core.
3. Import the AutoProgression ZIP without extracting it.
4. Enable AutoProgression and start the game.

### Manual installation

Place `AutoProgression.dll` in:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\Mods\
```

## Starting and Stopping

Press `T` in a normal running dimension to enable AutoProgression. Press `T`
again to disable it.

The runtime starts disabled each time it is created. The `T` toggle does not
change saved settings. It enables or pauses the configured automation groups
together.

Periodic AutoProgression behavior requires the global `T` toggle, including
unlimited Daily and Weekly reroll availability. Blocking the two vertical
Random Box magnet upgrades is a persistent protection and remains active while
`T` automation is off.

## Recommended First Setup

Before pressing `T`, review these settings:

1. Disable `Use Paid 500x Bonuses` unless you want automatic Jewel spending.
2. Disable `Buy Missing With Jewels` unless craftables may buy materials.
3. Set the Dragon and Simurgh Egg reserves you want to keep.
4. Review the normal Ascension threshold and post-Ascension Buy All setting.
5. Confirm the Scrap and Dragon Scale overflow thresholds.

Then enter a normal Runner or Rage dimension and press `T`.

## Important Currency Warning

The following settings can spend Jewels of Soul:

- `Use Paid 500x Bonuses`
- `Buy Missing With Jewels`

Material purchases use the configured `Purchase Percent`. Specialization and
Key Manifest never buy Scrap, Simurgh Feathers, or Dragon Scales. Other missing
materials follow the global Jewel setting.

## Normal Ascension

The default threshold is 50%: pending Slayer Points must reach the configured
percentage of lifetime Slayer Points. Checks occur every 1 minute by default
and once immediately after enabling AutoProgression.

Only normal Ascension is used. Afterward, the mod can repeatedly invoke the
Ascension-tree Buy All action until two stable rounds spend no more points.

## Craftables

The `Craftables > Enabled` setting is the master switch for every craftable
option and any material purchase requested by those craftables. The detailed
settings below it are evaluated only while this switch is enabled.

Timed items begin refilling at 3 minutes and stop at 15 minutes by default.
Rage Pills use their own minimum interval.

Scrap and Dragon Scale overflow items are triggered by inventory percentage,
but they also stop when their individual remaining duration reaches the shared
maximum-duration setting.

Quest-assist craftables use separate 5-minute cooldowns by default:

- Specialization: normal Goblin and Bonus Stage quests
- Key Manifest: normal Chest Hunt quests

Daily and Weekly Quests do not trigger these two items.
Specialization also preserves a fixed Scrap reserve: it is not crafted when
its cost would leave Scrap below 50% of the player's current unlocked storage
capacity. This safety threshold is intentionally not configurable. Key
Manifest uses Simurgh Feathers and is unaffected by the Scrap reserve.

## Manual Armory Box Opening

In the craftables screen, highlight one of the five Armory boxes and press the
configured `Select Box Key` (`I` by default) to select it. Press the configured
`Open Boxes Key` (`O` by default) to open up to `Boxes Per Press` boxes (`10`
by default) without the slow modal animation. The two keys must differ. Normal
crafting costs and reward rolls are retained. The operation stops when
materials or Armory slots run out and is independent from `T`.

## Silver Boxes

Silver Box enhancement is active independently from `T`. `Auto Claim Reward`
checks once per second and claims an available reward through the game's normal
reward method. The game's native storage limit and Silver Box consumption
behavior are unchanged.

## Quest Maintenance

The `Quests > Enabled` setting is the master switch for this entire group. When
it is disabled, claiming, generation, filtering, rerolls, and Portal cooldown
automation are all inactive regardless of their individual settings.

AutoProgression can claim completed Daily and Weekly Quests, generate another
set when one type is exhausted, and keep rerolls available.

After a new Weekly set is generated, the mod can reroll one generated slot
until the 180,000 Rage Mode kill objective appears. Existing extra Weekly
slots are preserved.

After a new Daily set is generated, the mod can reroll selected inconvenient
objectives. Existing Daily Quests and later manual rerolls do not trigger the
filter.

## Eggs

Eggs open in the background without the normal animation:

- Simurgh Eggs open while above their reserve.
- Dragon Eggs open while above their reserve and Dragon Scale storage is not
  full.

One item action is allowed per second, preventing a large synchronous burst.

## Minions

`Auto Claim and Send` claims completed missions and sends each unlocked Minion
again when its Slayer Point cost is lower than the available balance.

`Automatic Maximum-Level Prestige` acts only when the game reports that Minion
Prestige is unlocked. A standing Minion must have a level above 1 and a maximum
level of at least 70. The service raises that Minion to its maximum level for
the prestige action.

With both settings enabled, the order is claim, prestige when eligible, then
send. With only automatic prestige enabled, manually claiming a mission leaves
the Minion standing by for the next automatic prestige check.

## Why the Mod Sometimes Waits

Automation runs only after the normal central gameplay scene is stable. It
pauses through unsupported scenes, Ascension reconstruction, and unavailable
game objects. A two-second Ascension lock clears cached IL2CPP references
before other services resume.

## Configuration File

The first launch creates:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoProgression.cfg
```

Edit it while the game is closed, then restart the game. See the
[Configuration Reference](docs/08-configuration.md) for every option.

## Quick Troubleshooting

### Pressing `T` does not immediately act

Enter a normal Runner or Rage dimension and wait for the three-second stable
screen guard. Unsupported scenes and transitions intentionally pause actions.

### A craftable is not being made

Check its feature switch, unlock state, material requirements, lower refill or
inventory threshold, and maximum duration. If Jewel purchases are disabled,
missing ordinary materials must be collected manually.

### Automatic Ascension does not happen

Check the threshold calculation, 15-minute default interval, current pending
Slayer Points, and whether the main gameplay scene is stable.

### A Daily or Weekly was not rerolled

Generated-set filters run only for a new set created while AutoProgression is
active. Manual rerolls and tasks that already existed when `T` was enabled are
not reprocessed.

For more help, see [Troubleshooting](docs/10-troubleshooting.md) and
[Logging](docs/09-logging.md).

## Full Automation Suite

- **AutoProgression** handles account growth and repeatable maintenance.
- **AutoAdventurer** handles active gameplay, quest objectives, travel, Rage,
  and movement abilities.
- **AutoClimber** handles Ascending Heights routes, rewards, and compatible
  quest enemies.

Each mod works independently and complements the others.

## Support Development

If these mods save you time, you can support continued development through
[PayPal](https://www.paypal.com/donate/?business=HK85PL8AREEXY&no_recurring=0&currency_code=USD).
