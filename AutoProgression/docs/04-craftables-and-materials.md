# Craftables and Materials

## Timed Craftables

Whetstone, Alternate Dimension Staff, Bidimensional Staff, and Deathwave
Scepter use a shared refill policy. By default, refilling starts below 10
minutes and continues toward 60 minutes, at most one item action per second.
Deathwave Scepter also preserves the configured Simurgh Feather reserve.

Rage Pill is checked without the normal item startup delay and uses its own
minimum interval. It may refresh Rage while Rage Mode is already active.

## Inventory-Overflow Craftables

- Shards Necklace consumes excess Scrap.
- Random Box Staff, Necklace of Collectables, CpS Compass, and Souls Compass
  consume excess Dragon Scales as one group.

Their percentage thresholds use the player's current unlocked storage
capacity. Although inventory percentage triggers them, each effect also stops
at the shared maximum-duration target.

## Quest-Assist Craftables

- Specialization reacts to active normal Goblin and Bonus Stage objectives.
- Key Manifest reacts to active normal Chest Hunt objectives.

Daily and Weekly Quests are ignored. The items have independent cooldowns,
10 minutes by default.

## Missing Materials

When enabled, ordinary missing materials can be purchased with Jewels of Soul
at 25%, 50%, or 100% capacity. Scrap, Simurgh Feathers, and Dragon Scales must
already be available where required and are never substituted by this path.

> `Buy Missing With Jewels` spends premium currency automatically. Review it
> before enabling the runtime on an unmodified save.

[Back to the Complete Manual](../MANUAL.md)
