# Configuration Reference

The configuration schema is version 10. Entries are grouped by purpose.

## General and Ascension

| Setting | Default | Meaning |
|---|---:|---|
| Debug Mode | `false` | Enables detailed diagnostic logs. |
| Automatic Ascension Enabled | `true` | Allows normal Ascension. |
| Soul Bonus Threshold Percent | `100` | Required pending-to-lifetime SP percentage. |
| Check Interval Minutes | `15` | Time between checks; enabling also checks once immediately. |
| Buy Skills After Ascension | `true` | Spends remaining SP through Ascension-tree Buy All. |

## Premium Currency and Eggs

| Setting | Default | Meaning |
|---|---:|---|
| Use Paid 500x Bonuses | `true` | Buys Souls and CpS 500x effects with Jewels of Soul. |
| Dragon Egg Reserve Amount | `300` | Eggs kept unopened; opening is automatic while the runtime is active. |
| Simurgh Egg Reserve Amount | `10` | Eggs kept unopened; opening is automatic while the runtime is active. |

## Quests

| Setting | Default | Meaning |
|---|---:|---|
| Auto Claim Completed Quests | `true` | Claims completed Daily and Weekly Quests. |
| Regenerate Daily Quests | `true` | Generates a new Daily set when available. |
| Regenerate Weekly Quests | `true` | Generates a new Weekly set when available. |
| Unlimited Quest Rerolls | `true` | Keeps Daily/Weekly rerolls available, independent from `T`. |
| Prefer 180000 Rage Weekly | `true` | Rerolls a newly generated Weekly slot to the minimum Rage-kill target. |
| Filter Generated Daily Quests | `true` | Rerolls selected inconvenient objectives in a new Daily set. |
| Reset Portal Cooldown | `true` | Removes the normal Portal cooldown. |

## Craftables and Materials

| Setting | Default | Meaning |
|---|---:|---|
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
| Timed Item Refill Below Minutes | `10` | Lower refill trigger. |
| Timed Item Target Maximum Minutes | `60` | Shared target and overflow-effect ceiling. |
| Use Quest Assist Craftables | `true` | Enables Specialization and Key Manifest. |
| Quest Assist Cooldown Minutes | `10` | Independent cooldown for each item. |
| Buy Missing With Jewels | `true` | Buys eligible ordinary materials with Jewels. |
| Material Purchase Percent | `100` | Purchase option: 25, 50, or 100 percent. |

## Purchases, Skills, and Equipment

| Setting | Default | Meaning |
|---|---:|---|
| Purchase Priority | `Skills` | Chooses Skills or Equipment first. |
| Buy Skills | `true` | Enables shop-skill purchasing. |
| Buy Equipment | `true` | Enables unlocked normal equipment purchasing. |
| Block Vertical Magnet Skills | `true` | Blocks two vertical Random Box magnet upgrades, independent from `T`. |
| Equipment Idle Minutes Before Sleep | `2` | No-valid-purchase time before sleeping. |
| Equipment Sleep Minutes | `10` | Equipment-only sleep duration. |

> The two Jewel settings can spend premium currency. Their defaults favor the
> development environment and should be reviewed before normal play.

[Back to the Complete Manual](../MANUAL.md)
