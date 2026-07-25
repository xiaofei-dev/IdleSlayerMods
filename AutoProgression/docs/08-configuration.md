# Configuration Reference

AutoProgression 2.0 uses configuration schema 31. The generated file is:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoProgression.cfg
```

Routine polling and sleep intervals are managed internally so the public
configuration focuses on progression, resources, and feature behavior.

## General

| Setting | Default | Meaning |
|---|---:|---|
| Configuration Version | `31` | Internal migration value. Do not edit. |
| Debug Mode | `false` | Adds detailed subsystem diagnostics and aggregated activity summaries. |

## Ascension

| Setting | Default | Meaning |
|---|---:|---|
| Automatic Ascension Enabled | `true` | Performs normal Ascension at the configured threshold while `T` is active. |
| Automatic Ultra Ascension Enabled | `false` | Major reset. Requires native eligibility and at least 24 Astral Keys. |
| Soul Bonus Threshold Percent | `50` | Required pending-to-lifetime Slayer Point percentage for normal Ascension. |
| Buy Skills After Automatic Ascension | `true` | Uses Ascension-tree Buy All after an automatic normal Ascension only. |

The threshold is checked immediately when `T` is enabled and every five
minutes afterward.

## Purchases

| Setting | Default | Meaning |
|---|---:|---|
| Skills Enabled | `true` | Buys eligible and affordable shop skills every five seconds while `T` is active. |
| Equipment Enabled | `true` | Buys levels for unlocked normal equipment, newest first. |
| Disable Vertical Magnet Upgrades | `true` | Blocks two unwanted Random Box vertical magnet upgrades from automatic and manual purchase, independently from `T`. |

The equipment buyer sleeps for ten minutes after one minute without an
eligible bulk purchase. Skill purchasing continues during that sleep.

## Paid Bonuses

| Setting | Default | Meaning |
|---|---:|---|
| Use Paid 500x Bonuses | `false` | Spends Jewels of Soul to maintain both Souls and CpS 500x effects. |

## Minions

| Setting | Default | Meaning |
|---|---:|---|
| Auto Claim and Send | `true` | Claims completed unlocked missions and sends affordable missions again while `T` is active. |
| Automatic Maximum-Level Prestige | `false` | Automatically prestiges standing Minions with maximum level at least 70; also makes every manual prestige use the selected Minion's maximum level independently from `T`. |

## Egg Opening

| Setting | Default | Meaning |
|---|---:|---|
| Enabled | `false` | Master switch for background Dragon and Simurgh Egg opening while `T` is active. |
| Dragon Egg Reserve Amount | `300` | Dragon Eggs kept unopened. Opening also stops when Dragon Scale storage is full. |
| Simurgh Egg Reserve Amount | `10` | Simurgh Eggs kept unopened. |

## Silver Boxes

| Setting | Default | Meaning |
|---|---:|---|
| Auto Claim Reward | `true` | Claims an available Silver Box reward independently from `T`. |

The native Silver Box storage maximum and consumption behavior are unchanged.

## Quests

| Setting | Default | Meaning |
|---|---:|---|
| Enabled | `true` | Master switch for every quest option below. |
| Auto Claim Completed Quests | `true` | Claims completed Daily and Weekly Quests. |
| Regenerate Daily Quests | `true` | Generates another Daily set when no active Daily Quests remain. |
| Regenerate Weekly Quests | `true` | Generates another Weekly set when no active Weekly Quests remain. |
| Unlimited Quest Rerolls | `true` | Keeps Daily and Weekly rerolls available while `T` is active. |
| Prefer 180k Rage Weekly Quest | `false` | Rerolls one newly generated Weekly slot until the 180,000 Rage Mode kill objective appears. |
| Filter Generated Daily Quests | `false` | Rerolls documented inconvenient objectives in newly generated Daily slots. |
| Reset Portal Cooldown | `true` | Keeps the normal Portal cooldown at zero while `T` is active. |

Generated-set filtering does not rewrite old quests or react to later manual
rerolls. Completed quests are skipped.

## Craftables

| Setting | Default | Meaning |
|---|---:|---|
| Enabled | `false` | Master switch for all automatic craftables and material purchases in this section. |
| Buy Missing With Jewels | `false` | Spends Jewels of Soul on eligible ordinary missing recipe materials. Never buys Scrap, Simurgh Feathers, or Dragon Scales. |
| Material Purchase Percent | `100` | Jewel refill size. Supported values: 25, 50, or 100 percent. |
| Timed Items Target Minutes | `6` | Shared target; timed refilling begins automatically at half this value. |
| Rage Pill Enabled | `true` | Refreshes an active Rage cooldown. |
| Whetstone Enabled | `true` | Maintains Whetstone duration. |
| Alternate Dimension Staff Enabled | `true` | Maintains Alternate Dimension Staff duration. |
| Bidimensional Staff Enabled | `true` | Maintains Bidimensional Staff duration. |
| Deathwave Scepter Enabled | `true` | Maintains Deathwave Scepter duration while preserving its Feather reserve. |
| Deathwave Scepter Feather Reserve Amount | `300` | Minimum Simurgh Feather amount preserved for Deathwave Scepter. |
| Shards Necklace Scrap Overflow Enabled | `true` | Consumes excess Scrap without using the timed duration cap. |
| Shards Necklace Scrap Threshold Percent | `97` | Starts at or above this Scrap percentage and stops below it. |
| Dragon Scale Overflow Craftables Enabled | `true` | Enables Random Box Staff, Necklace of Collectables, CpS Compass, and Souls Compass as one overflow group. |
| Dragon Scale Overflow Threshold Percent | `95` | Starts one overflow cycle above this Dragon Scale percentage. |
| Ascendant Badge Boost Enabled | `false` | Arms the one-use Armory boost when Dragon Scales are strictly above the fixed 50% requirement. |
| Quest Assist Craftables Enabled | `true` | Enables Specialization and Key Manifest under their task and resource rules. |
| Quest Assist Feather Threshold Amount | `1000` | Shared Feather threshold. `0` disables both items; crafting must preserve this amount. |

Rage Pill attempts are limited internally to once every ten seconds.
Quest-triggered Specialization and Key Manifest have separate fixed
five-minute cooldowns.

`Quest Assist Feather Threshold Amount` controls both quest-assist items:

- `0` disables Specialization and Key Manifest completely.
- Above zero, current Feathers must be strictly greater than the value.
- Crafting must leave at least the configured amount.
- Key Manifest also uses it as an independent Feather-overflow trigger.
- Specialization still requires a normal quest or its Scrap/Dragon Scale
  overflow conditions.

## Manual Armory Boxes

| Setting | Default | Meaning |
|---|---:|---|
| Boxes Per Press | `10` | Maximum selected Armory boxes or eggs opened per trigger. |
| Select Box Key | `B` | Records the highlighted Armory box, Dragon Egg, or Simurgh Egg. |
| Open Boxes Key | `N` | Opens the selected item in the background independently from `T`. |

The two keys must differ. Opening stops when materials, eggs, or free Armory
slots run out.

## Manual Casino Crawler Eyes

| Setting | Default | Meaning |
|---|---:|---|
| Enabled | `false` | Enables the manual Jewel-spending bulk-purchase tool independently from `T`. |
| Purchase Key | `M` | Starts one operation on the correct Village Casino purchase screen. |
| Eyes Per Press | `1000` | Requested amount, rounded down to a multiple of ten. |

The service uses sequential native 10-eye transactions and stops on
insufficient Jewels, a closed screen, or a safety timeout.

## Premium-Currency Warning

These settings can spend Jewels of Soul:

- `Use Paid 500x Bonuses`
- `Buy Missing With Jewels`
- `Manual Casino Crawler Eyes > Enabled`

All three are disabled by default. Enable them only after understanding their
cost.

[Back to the Complete Manual](../MANUAL.md)
