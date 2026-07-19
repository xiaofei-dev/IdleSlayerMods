# Configuration Reference

The configuration schema is version 17. Entries are grouped by purpose.

## General and Ascension

| Setting | Default | Meaning |
|---|---:|---|
| Debug Mode | `false` | Enables detailed diagnostic logs. |
| Automatic Ascension Enabled | `true` | Allows normal Ascension. |
| Soul Bonus Threshold Percent | `50` | Required pending-to-lifetime SP percentage. |
| Check Interval Minutes | `1` | Time between checks; enabling also checks once immediately. |
| Buy Skills After Automatic Ascension | `true` | Spends remaining SP after an automatic Ascension only; manual Ascension is unaffected. |

## Premium Currency and Eggs

| Setting | Default | Meaning |
|---|---:|---|
| Use Paid 500x Bonuses | `true` | Buys Souls and CpS 500x effects with Jewels of Soul. |
| Dragon Egg Reserve Amount | `300` | Eggs kept unopened; opening is automatic while the runtime is active. |
| Simurgh Egg Reserve Amount | `10` | Eggs kept unopened; opening is automatic while the runtime is active. |

## Minions

| Setting | Default | Meaning |
|---|---:|---|
| Auto Claim and Send | `true` | Claims completed unlocked Minion missions and sends affordable missions again. |
| Automatic Maximum-Level Prestige | `true` | When Prestige is unlocked, raises eligible standing Minions with `maxLevel >= 70` to maximum level for automatic prestige. |

The prestige option deliberately changes the Minion level used for prestige;
disable it if normal Minion level progression should be preserved.

## Armory Boxes

| Setting | Default | Meaning |
|---|---:|---|
| Boxes Per Press | `10` | Maximum boxes opened by one trigger. |
| Select Box Key | `I` | Records the highlighted one of the five Armory boxes. |
| Open Boxes Key | `O` | Opens the selected box in the background; independent from `T`. |

The selection and opening keys must be different. Opening stops when materials
or free Armory slots run out.

## Silver Boxes

| Setting | Default | Meaning |
|---|---:|---|
| Auto Claim Reward | `true` | Claims an available Silver Box reward automatically. |

This setting takes effect after entering the game and is independent from the
`T` automation toggle. The game's native Silver Box maximum is unchanged.

## Quests

| Setting | Default | Meaning |
|---|---:|---|
| Enabled | `true` | Master switch for every quest option below. |
| Auto Claim Completed Quests | `true` | Claims completed Daily and Weekly Quests. |
| Regenerate Daily Quests | `true` | Generates a new Daily set when available. |
| Regenerate Weekly Quests | `true` | Generates a new Weekly set when available. |
| Unlimited Quest Rerolls | `true` | Keeps Daily/Weekly rerolls available while `T` automation is active. |
| Prefer 180000 Rage Weekly | `true` | Rerolls a newly generated Weekly slot to the minimum Rage-kill target. |
| Filter Generated Daily Quests | `true` | Rerolls selected inconvenient objectives in a new Daily set. |
| Reset Portal Cooldown | `true` | Removes the normal Portal cooldown. |

## Craftables

| Setting | Default | Meaning |
|---|---:|---|
| Enabled | `true` | Master switch for all craftables and their automatic material purchases. |
| Use Rage Pill | `true` | Refreshes Rage cooldown. |
| Rage Pill Minimum Interval Seconds | `10` | Minimum time between attempts. |
| Use Whetstone | `true` | Maintains Whetstone duration. |
| Use Alternate Dimension Staff | `true` | Maintains its duration. |
| Use Bidimensional Staff | `true` | Maintains its duration. |
| Use Deathwave Scepter | `true` | Maintains it while preserving feathers. |
| Deathwave Scepter Feather Reserve | `300` | Minimum Simurgh Feathers retained. |
| Use Shards Necklace | `true` | Consumes excess Scrap. |
| Shards Necklace Scrap Threshold Percent | `95` | Scrap percentage trigger. |
| Use Dragon Scale Overflow Items | `true` | Consumes excess scales with four effects. |
| Dragon Scale Threshold Percent | `95` | Dragon Scale percentage trigger. |
| Timed Item Refill Below Minutes | `3` | Lower refill trigger. |
| Timed Item Target Maximum Minutes | `15` | Shared target and overflow-effect ceiling. |
| Use Quest Assist Craftables | `true` | Enables Specialization and Key Manifest. Specialization will not craft if its cost would leave Scrap below the fixed 50% reserve. |
| Quest Assist Cooldown Minutes | `5` | Independent cooldown for each item. |
| Buy Missing With Jewels | `true` | Buys eligible ordinary materials with Jewels. |
| Material Purchase Percent | `100` | Purchase option: 25, 50, or 100 percent. |

## Purchases

| Setting | Default | Meaning |
|---|---:|---|
| Buy Skills | `true` | Enables shop-skill purchasing. |
| Buy Equipment | `true` | Enables unlocked normal equipment purchasing. |
| Block Vertical Magnet Skills | `true` | Always blocks two vertical Random Box magnet upgrades, independently from `T`. |
| Equipment Idle Minutes Before Sleep | `1` | No-valid-purchase time before sleeping. |
| Equipment Sleep Minutes | `10` | Equipment-only sleep duration. |

> The two Jewel settings can spend premium currency. Their defaults favor the
> development environment and should be reviewed before normal play.

[Back to the Complete Manual](../MANUAL.md)
