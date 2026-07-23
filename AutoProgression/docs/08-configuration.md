# Configuration Reference

The configuration schema is version 31. Entries are grouped by purpose.
Routine scheduling details are managed internally so the file focuses on
choices that affect progression, resources, or feature behavior.

## General and Ascension

| Setting | Default | Meaning |
|---|---:|---|
| Debug Mode | `false` | Enables detailed diagnostic logs. |
| Automatic Ascension Enabled | `true` | Allows normal Ascension. |
| Automatic Ultra Ascension Enabled | `false` | Major reset: Ultra Ascends after native requirements and at least 24 Astral Keys. |
| Soul Bonus Threshold Percent | `50` | Required pending-to-lifetime SP percentage. |
| Buy Skills After Automatic Ascension | `true` | Spends remaining SP after an automatic Ascension only; manual Ascension is unaffected. |

Ascension is checked immediately when automation is enabled and then every five
minutes.

## Premium Currency and Eggs

| Setting | Default | Meaning |
|---|---:|---|
| Use Paid 500x Bonuses | `false` | Buys Souls and CpS 500x effects with Jewels of Soul. |
| Egg Opening Enabled | `false` | Master switch for background Dragon and Simurgh Egg opening. |
| Dragon Egg Reserve Amount | `300` | Eggs kept unopened while egg opening is enabled. |
| Simurgh Egg Reserve Amount | `10` | Eggs kept unopened while egg opening is enabled. |

## Minions

| Setting | Default | Meaning |
|---|---:|---|
| Auto Claim and Send | `true` | Claims completed unlocked Minion missions and sends affordable missions again. |
| Automatic Maximum-Level Prestige | `false` | Automatic prestige processes Minions with `maxLevel >= 70`; manual prestige uses maximum level for every Minion and is independent from `T`. |

The prestige option deliberately changes the Minion level used for prestige;
disable it if normal Minion level progression should be preserved.

## Armory Boxes

| Setting | Default | Meaning |
|---|---:|---|
| Boxes Per Press | `10` | Maximum selected Armory boxes or eggs opened per trigger. |
| Select Box Key | `B` | Records the highlighted Armory box, Dragon Egg, or Simurgh Egg. |
| Open Boxes Key | `N` | Opens the selected box or egg in the background; independent from `T`. |

The selection and opening keys must be different. Opening stops when materials
or free Armory slots run out. Egg opening stops when the selected egg runs out.

## Casino Crawler Eyes

| Setting | Default | Meaning |
|---|---:|---|
| Enabled | `false` | Enables the manual bulk-purchase key; independent from `T`. This spends Jewels of Soul. |
| Purchase Key | `M` | Starts one sequential purchase while the Crawler Eye cashier screen is open. |
| Eyes Per Press | `1000` | Requested amount; rounded down to a multiple of 10. |

The service uses the game's confirmed purchase action one 10-eye transaction
at a time. It waits for each inventory increase before sending the next request
and stops on insufficient Jewels, a closed screen, or a safety timeout.

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
| Prefer 180000 Rage Weekly | `false` | Rerolls a newly generated Weekly slot to the minimum Rage-kill target. |
| Filter Generated Daily Quests | `false` | Rerolls selected inconvenient objectives in a new Daily set. |
| Reset Portal Cooldown | `true` | Removes the normal Portal cooldown. |

## Craftables

| Setting | Default | Meaning |
|---|---:|---|
| Enabled | `false` | Master switch for all craftables and their automatic material purchases. |
| Use Rage Pill | `true` | Refreshes Rage cooldown. |
| Use Whetstone | `true` | Maintains Whetstone duration. |
| Use Alternate Dimension Staff | `true` | Maintains its duration. |
| Use Bidimensional Staff | `true` | Maintains its duration. |
| Use Deathwave Scepter | `true` | Maintains it while preserving feathers. |
| Deathwave Scepter Feather Reserve | `300` | Minimum Simurgh Feathers retained. |
| Use Shards Necklace | `true` | Consumes excess Scrap. |
| Shards Necklace Scrap Threshold Percent | `97` | Crafts until Scrap falls below this trigger; no duration cap applies. |
| Use Dragon Scale Overflow Items | `true` | Consumes excess scales with four effects. |
| Dragon Scale Threshold Percent | `95` | Dragon Scale percentage trigger. |
| Ascendant Badge Boost Enabled | `false` | Crafts the one-use Armory boost when Dragon Scales are above the fixed 50% requirement. |
| Timed Item Target Minutes | `6` | Shared target and overflow-effect ceiling. Refilling starts at half this value. |
| Use Quest Assist Craftables | `true` | Enables Specialization and Key Manifest. Specialization will not craft if its cost would leave Scrap below the fixed 50% reserve. |
| Quest Assist Feather Threshold Amount | `1000` | Shared Specialization and Key Manifest Feather threshold. `0` disables both items. |
| Buy Missing With Jewels | `false` | Buys eligible ordinary materials with Jewels only while the Craftables master switch is enabled. |
| Material Purchase Percent | `100` | Purchase option: 25, 50, or 100 percent. |

## Purchases

| Setting | Default | Meaning |
|---|---:|---|
| Buy Skills | `true` | Enables shop-skill purchasing. |
| Buy Equipment | `true` | Enables unlocked normal equipment purchasing. |
| Block Vertical Magnet Skills | `true` | Always blocks two vertical Random Box magnet upgrades, independently from `T`. |

Rage Pill attempts are internally limited to once every 10 seconds. Each
quest-triggered assist item has an independent five-minute cooldown. The
equipment buyer sleeps for ten minutes after one minute without an eligible
purchase.

`Quest Assist Feather Threshold Amount` applies to both quest-assist items.
When it is above zero, the current Simurgh Feather amount must be strictly
greater than the threshold and crafting must leave at least that amount.
Key Manifest also uses it as the independent Feather-overflow trigger.
Specialization still requires its normal quest or Scrap/Dragon Scale trigger.
Setting the value to zero disables both Specialization and Key Manifest.

> `Use Paid 500x Bonuses`, `Casino Crawler Eyes`, and `Buy Missing With
> Jewels` can spend premium currency. All direct Jewel-spending switches are
> disabled by default and should only be enabled after their behavior is
> understood.

[Back to the Complete Manual](../MANUAL.md)
