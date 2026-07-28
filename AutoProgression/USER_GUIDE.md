# AutoProgression 2.0 User Guide

![AutoProgression 2.0 - Auto Upgrade](Assets/AutoProgression-Final-Cover.png)

AutoProgression is the account-growth core of Tashi's Full Automation Suite
for Idle Slayer. It automates Ascension, shop upgrades, normal equipment,
Minions, craftables, materials, quest maintenance, eggs, Silver Box rewards,
and selected premium-currency actions.

This mod is deliberately modular. It does not control the character during
normal running, choose combat targets, travel for quests, or play every
minigame. Those responsibilities belong to companion modules listed in
[Full Automation Suite](#full-automation-suite).

## Read This Before Enabling the Mod

AutoProgression contains both ordinary quality-of-life automation and options
that can strongly alter game balance.

The following features can spend Jewels of Soul:

- `Use Paid 500x Bonuses`
- `Craftables > Buy Missing With Jewels`
- `Manual Casino Crawler Eyes > Enabled`

The first two automatic Jewel-spending paths are disabled by default.
The manual Casino tool is also disabled by default. Do not enable any of them
until you understand the cost and have reviewed the generated configuration.

The following features can also have major progression consequences:

- automatic Ultra Ascension
- unlimited Daily and Weekly Quest rerolls
- repeated Daily and Weekly Quest generation
- Portal cooldown removal
- maximum-level Minion prestige
- automatic egg and material consumption

Automatic Ultra Ascension, automatic Minion prestige, automatic egg opening,
paid 500x bonuses, and the Craftables master switch are disabled by default.

## Requirements and Tested Versions

| Component | Tested version | Purpose |
|---|---:|---|
| Idle Slayer | Current mod-compatible PC release | Base game |
| Idle Slayer Mod Manager | Current release | Installation and launch |
| MelonLoader | `0.7.3 Open-Beta` | Mod runtime |
| Idle Slayer Mods Core | `1.3.2` | Required shared library |
| AutoProgression | `2.0.0` | This mod |

Later compatible releases may also work, but the versions above describe the
author's tested environment.

## Installation

### Mod Manager installation

1. Install and initialize Idle Slayer Mod Manager.
2. Install Idle Slayer Mods Core `1.3.2` or a compatible later version.
3. Import `AutoProgression.zip` without extracting it.
4. Enable AutoProgression in the manager.
5. Start the game once to generate the configuration file.
6. Close the game and review the configuration before enabling automation.

### Manual installation

Copy `AutoProgression.dll` to:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\Mods\
```

The configuration file is generated at:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoProgression.cfg
```

Edit the configuration while the game is closed, then restart the game.

## Global Control

| Key | Default action |
|---|---|
| `T` (configurable) | Enable or disable periodic AutoProgression automation |
| `B` | Select the highlighted Armory box, Dragon Egg, or Simurgh Egg |
| `N` | Open the selected box or egg in the background |
| `M` | Start one configured Village Casino Crawler Eye bulk purchase |

The runtime starts disabled. Enter a normal Runner or Rage dimension and press
the configured toggle key (`T` by default). Change it with
`General > Toggle Key`. The central gameplay screen must remain stable for approximately three
seconds before periodic actions begin.

Turning the main toggle off pauses periodic automation without changing saved
settings. The following intentionally remain independent from it:

- blocking the two unwanted vertical Random Box magnet upgrades
- manual Armory box and egg opening
- manual Crawler Eye bulk purchasing
- automatic Silver Box reward claiming
- the manual maximum-level Minion prestige enhancement, when enabled

Each independent feature is labeled in the configuration.

## Recommended First Setup

For a conservative first run:

1. Leave `Use Paid 500x Bonuses = false`.
2. Leave `Craftables > Enabled = false`.
3. Leave `Buy Missing With Jewels = false`.
4. Leave automatic Ultra Ascension disabled.
5. Leave automatic maximum-level Minion prestige disabled.
6. Leave automatic egg opening disabled.
7. Review the normal Ascension threshold.
8. Decide whether quest regeneration, unlimited rerolls, Daily filtering, and
   Portal cooldown removal match the way you want to play.
9. Enter a normal dimension and press `T`.

Enable resource-consuming groups one at a time after confirming their
descriptions and observing the log.

## Ascension

### Automatic normal Ascension

Normal Ascension compares pending Slayer Points with lifetime Slayer Points.
The default threshold is `50%`. Enabling the runtime performs one immediate
check; later checks occur every five minutes.

When `Buy Skills After Automatic Ascension` is enabled, the mod repeatedly uses
the Ascension Skill Tree Buy All action until no more Slayer Points can be
spent. This follow-up is triggered only by an automatic normal Ascension;
manual Ascension is unaffected.

### Automatic Ultra Ascension

Automatic Ultra Ascension is disabled by default. When explicitly enabled, it
takes priority over normal Ascension only when:

- the game's native Ultra Ascension requirements are satisfied;
- the native Ultra Ascension action is available; and
- the current Ultra Ascension would grant at least 24 Astral Keys.

The 24-key threshold is fixed. Ultra Ascension is a major reset, so enable this
option only after understanding its consequences.

All other automation pauses behind a two-second Ascension transaction lock
while game objects are rebuilt.

## Shop Skills and Normal Equipment

`Purchases > Skills Enabled` buys all currently affordable and eligible shop
skills every five seconds.

`Purchases > Equipment Enabled` buys only unlocked normal shop equipment. It
starts with the newest unlocked item, purchases that item until it no longer
meets the current bulk threshold, and then moves toward older equipment.
Skills continue to be checked while the equipment buyer is sleeping.

`Disable Vertical Magnet Upgrades` permanently blocks the two Random Box
vertical magnet upgrades from automatic and manual purchase. This protection
does not depend on `T`.

## Minions

`Auto Claim and Send` claims completed unlocked Minion missions and sends
standing Minions again when their Slayer Point cost is affordable.

`Automatic Maximum-Level Prestige` is disabled by default:

- automatic prestige requires `T`;
- the Prestige system must be unlocked;
- a standing Minion must have a maximum level of at least 70;
- eligible automatic prestige uses that Minion's maximum level;
- when both Minion options are enabled, the order is claim, prestige, send.

When this setting is enabled, a manually initiated prestige also uses the
selected Minion's maximum level, including Minions below the automatic
70-level threshold. This manual enhancement remains active while `T` is off.

## Craftables and Materials

`Craftables > Enabled` is the master switch for every automatic craftable
service. When it is false, no automatic craftable is used and no material is
purchased for a craftable.

### Jewel material purchasing

`Buy Missing With Jewels` allows enabled craftable services to purchase
eligible missing materials at the selected `Material Purchase Percent`.
Supported purchase sizes are 25%, 50%, and 100%.

This option never purchases:

- Scrap
- Simurgh Feathers
- Dragon Scales

Every Jewel purchase is shown in the normal user log.

### Timed craftables

The shared `Timed Items Target Minutes` default is 6 minutes. Refilling begins
automatically at half the target, so the default behavior refills from
3 minutes toward 6 minutes.

Supported timed items include:

- Whetstone
- Alternate Dimension Staff
- Bidimensional Staff
- Deathwave Scepter

Deathwave Scepter also preserves its configured Simurgh Feather reserve.
Rage Pill checks separately and may refresh Rage even while Rage Mode is
already active.

### Scrap and Dragon Scale overflow

Shards Necklace consumes excess Scrap when storage reaches its configured
percentage. Its default threshold is 97%. It intentionally ignores the shared
duration target and continues until Scrap falls below the threshold.

The Dragon Scale overflow group contains:

- Random Box Staff
- Necklace of Collectables
- CpS Compass
- Souls Compass

The group begins a cycle above its configured Dragon Scale percentage and
respects the shared duration target.

### Ascendant Badge Boost

`Ascendant Badge Boost Enabled` is an independent switch and is disabled by
default. When enabled, the mod arms the one-use Armory boost only when:

- Craftables and `T` automation are active;
- Dragon Scale storage is strictly above the fixed 50% requirement;
- the item is unlocked and its native one-use state is available; and
- all required materials are available or eligible ordinary materials can be
  purchased under `Buy Missing With Jewels`.

Dragon Scales are never purchased. After the boost is consumed, it may be
crafted again when the native state becomes available.

### Specialization and Key Manifest

`Quest Assist Craftables Enabled` controls both items:

- Specialization supports active normal Goblin and Bonus Stage quests.
- Key Manifest supports active normal Chest Hunt quests.
- Daily and Weekly Quests do not trigger either item.

Both items share `Quest Assist Feather Threshold Amount`:

- `0` disables both items completely;
- above `0`, current Simurgh Feathers must be strictly greater than the value;
- crafting must leave at least the configured amount;
- Simurgh Feathers are never purchased.

Specialization may also run without a quest when Scrap is above 80% and Dragon
Scales are above 50%. It preserves at least 50% Scrap and pauses this overflow
path while an active quest requires normal, Silver, or Golden Random Boxes.
Special Random Boxes do not block it because they start Bonus Stages.

Key Manifest also uses the shared Feather value as an independent overflow
trigger. When Feathers are above the threshold and the native one-use state is
available, it may be crafted without a matching quest.

Quest-triggered Specialization and Key Manifest each have their own internal
five-minute cooldown. Resource-overflow triggers rely on native one-use
availability instead.

## Quest Maintenance

`Quests > Enabled` is the master switch for claiming, regeneration, rerolls,
generated-set filtering, and Portal cooldown maintenance.

AutoProgression can:

- claim completed Daily and Weekly Quests;
- generate another Daily or Weekly set after that type is exhausted;
- keep Daily and Weekly rerolls available;
- reset the normal Portal cooldown;
- reroll a newly generated Weekly slot until the 180,000 Rage Mode kill quest
  appears;
- filter selected inconvenient objectives from newly generated Daily sets.

Weekly preference and Daily filtering run only after a newly generated set.
They do not continuously rewrite existing quests and do not react to later
manual rerolls.

The optional Daily filter removes:

- Goblin kills
- material collection
- temporary-craftable crafting
- Chest Hunt chests
- normal and Silver Random Boxes
- normal Boost uses
- Rage Mode uses
- Bonus Stage entry, full-completion, and section objectives
- Ascending Heights completion
- Grapple Run completion

Rage Mode kill quests and Wind Dash kill quests are retained. Completed quests
waiting to be claimed are never rerolled.

AutoProgression maintains quest data only. AutoAdventurer performs supported
quest objectives and dimension travel.

## Eggs

Automatic egg opening is disabled by default and does not play the slow native
opening animation.

- Dragon Eggs open only while above their reserve and while Dragon Scale
  storage is not full.
- Simurgh Eggs open only while above their reserve.
- Craftable actions have priority over egg opening.
- Background item actions are limited to avoid a large synchronous burst.

Default reserves are 300 Dragon Eggs and 10 Simurgh Eggs.

## Paid 500x Bonuses

`Use Paid 500x Bonuses` is disabled by default and directly spends Jewels of
Soul. When enabled, it maintains both Souls and CpS 500x bonuses.

The service uses a timer for normal operation, then reads the real remaining
effect duration when the timer expires. This avoids constant polling and
remains safe when game-speed effects alter cooldown timing.

## Silver Boxes

`Auto Claim Reward` automatically claims an available Silver Box reward after
entering the game. It is independent from `T`. The native Silver Box storage
limit and consumption rules are not changed.

## Manual Armory Box and Egg Opening

Open the Armory craftables screen and highlight one of the five Armory boxes,
a Dragon Egg, or a Simurgh Egg.

1. Press `Select Box Key` (`B` by default).
2. Press `Open Boxes Key` (`N` by default).
3. Up to `Boxes Per Press` items are opened in the background.

The default amount is 10. Normal material costs and reward rolls remain in
effect. Opening stops when materials, eggs, or free Armory slots run out.
This manual tool is independent from `T`.

## Manual Casino Crawler Eye Purchasing

This tool is disabled by default and directly spends Jewels of Soul.

1. Enable `Manual Casino Crawler Eyes > Enabled`.
2. Open the Village Casino Crawler Eye purchase screen.
3. Press `Purchase Key` (`M` by default).

The default request is 1,000 Eyes. Purchases are performed as sequential native
10-eye transactions and stop safely if the screen closes, Jewels become
insufficient, or a safety timeout occurs.

## Runtime Safety

AutoProgression operates only after the central Runner or Rage scene is stable.
It pauses through unsupported scenes, menus that replace central gameplay,
Portals, minigames, and Ascension reconstruction.

Safety behavior includes:

- a three-second central-screen stabilization gate;
- a two-second Ascension transaction lock;
- cached IL2CPP object resets across relevant boundaries;
- at most one ordinary item action per second;
- safe waiting when objects, recipes, or native actions are unavailable;
- delayed generated-quest processing so native quest data can stabilize;
- concise user errors and detailed stack traces only in Debug Mode.

## Logging

Normal user logs show:

- mod load and `T` state
- normal and Ultra Ascension starts
- Jewel purchases
- Minion prestige
- Silver Box claims
- manual bulk actions
- completed Daily and Weekly filtering results
- warnings and errors

`Debug Mode` adds subsystem diagnostics, object resolution, timers, state
transitions, and aggregated activity summaries. It is disabled by default.

Logs are stored under:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\MelonLoader\Logs\
```

## Full Automation Suite

The current first-party suite is:

| Mod | Version | Responsibility |
|---|---:|---|
| AutoProgression | `2.0.0` | Account progression, Ascension, purchases, quests, Minions, craftables, materials, eggs, and menu maintenance |
| AutoAdventurer | `2.0.0` | Normal running, automatic jumping, combat abilities, Rage, quest selection, dimension travel, event safety, and bosses |
| AutoClimber | `1.2.0` | Ascending Heights route planning, recovery, quest enemies, rewards, retry, and background play |
| AutoBonusRunner | `1.0.0` | Bonus Stage route execution, jumping, sphere requirements, Spirit Boost support, start-slider handling, and optional native retry |

AutoAdventurer 2.0 replaces the old Auto Jump component for normal gameplay.
AutoBonusRunner replaces Bonus Stage Completer. Do not run the replaced mods
alongside their new replacements unless you are deliberately testing
conflicting input automation.

Each first-party module can be used independently. Together, they divide
account maintenance, normal gameplay, Ascending Heights, and Bonus Stages into
separate safety domains.

## Optional Third-Party Companion Mods

The author's currently tested supplemental stack is:

| Mod | Tested version | Responsibility |
|---|---:|---|
| GrappleRunAutocompleter | `1.1.0` | Grapple Run completion |
| Perfect Chest Hunter | `2.0.0` | Chest Hunt automation |
| Armory Manager | `1.2.2` | Automatic Armory cleanup and dismantling |
| Idle Slayer Mods Core | `1.3.2` | Shared dependency used by the suite and several community mods |

These are not all hard dependencies of AutoProgression. Install only the
components you want and follow each author's requirements and permissions.

## Troubleshooting

### Pressing `T` does not immediately act

Enter a normal Runner or Rage dimension and wait for the central screen to
stabilize. Unsupported scenes and transitions intentionally pause automation.

### A craftable is not being made

Check, in order:

1. `T` is active.
2. `Craftables > Enabled` is true.
3. The individual item switch is enabled.
4. The item is unlocked and its native state allows use.
5. Its duration or resource threshold is satisfied.
6. Protected materials are available.
7. `Buy Missing With Jewels` is enabled if ordinary materials must be bought.

For Ascendant Badge Boost, Dragon Scales must be strictly above 50%. With a
capacity of 249, 124 is only 49.8%; at least 125 is required.

### Automatic Ascension does not happen

Check pending versus lifetime Slayer Points, the configured percentage, the
five-minute internal interval, and whether central gameplay is stable.
Enabling `T` performs one immediate check.

### A Daily or Weekly Quest was not rerolled

Generated-set filters run only for a set generated while AutoProgression is
active. Existing sets and manual rerolls are not reprocessed. Completed quests
are intentionally skipped.

### Eggs do not open

The count must be above its reserve. Dragon Eggs also stop while Dragon Scale
storage is full. Eggs yield to higher-priority craftable actions.

### Reporting a problem

Include:

- the latest MelonLoader log;
- the scene and open panel;
- the action immediately before the problem;
- your relevant configuration entries;
- the versions of all installed automation mods.

Confirm that the stack trace names AutoProgression before attributing a loader
or another mod's error to this project.

## Support and Source

- Source and issue tracking:
  [Tashi's IdleSlayerMods](https://github.com/xiaofei-dev/IdleSlayerMods)
- Optional development support:
  [PayPal](https://www.paypal.com/donate/?business=HK85PL8AREEXY&no_recurring=0&currency_code=USD)

This is an unofficial community mod. Idle Slayer and its assets belong to
their respective owners.
