# Craftables and Materials

`Craftables > Enabled` is the master switch for this chapter. When disabled,
no craftable is used and no material is purchased on behalf of a craftable.
Egg opening remains controlled separately.

## Timed Craftables

Whetstone, Alternate Dimension Staff, Bidimensional Staff, and Deathwave
Scepter use a shared refill policy. By default, refilling starts below 3
minutes and continues toward 6 minutes, at most one item action per second.
Deathwave Scepter also preserves the configured Simurgh Feather reserve.

Rage Pill is checked without the normal item startup delay and uses its own
minimum interval. It may refresh Rage while Rage Mode is already active.

## Inventory-Overflow Craftables

Ascendant Badge Boost is a one-use effect rather than a timed stack. It is
controlled by its own switch, which is disabled by default. When enabled, it
is crafted whenever its native condition permits use and Dragon Scales are
strictly above the fixed 50% capacity threshold. Dragon Scales are never
purchased; other eligible missing materials follow the global Jewel purchase
setting.

- Shards Necklace consumes excess Scrap.
- Random Box Staff, Necklace of Collectables, CpS Compass, and Souls Compass
  consume excess Dragon Scales as one group.

Their percentage thresholds use the player's current unlocked storage
capacity. Shards Necklace ignores the shared duration target and continues
until Scrap falls below its threshold. Dragon Scale duration effects still
stop at the shared maximum-duration target.

## Quest-Assist Craftables

- Specialization reacts to active normal Goblin and Bonus Stage objectives.
- Key Manifest reacts to active normal Chest Hunt objectives.

Every Specialization use requires Dragon Scales to be strictly above 50% of
current unlocked capacity, including quest-triggered uses.

Specialization can also be used without a matching quest when Scrap is above
80%. This resource path neither reads nor
starts the quest-assist cooldown. Native availability and protected-material
checks still apply. It pauses while any active quest requires normal, Silver,
or Golden Random Boxes.

Specialization and Key Manifest share `Quest Assist Feather Threshold Amount`
(1,000 by default). Setting it to `0` disables both items completely. Above
zero, Feathers must be strictly greater than the value and crafting must leave
at least that amount. Key Manifest also uses the shared value as its
independent Feather-overflow trigger. Both items require the quest-assist
switch, the Craftables master switch, and global `T`.

Daily and Weekly Quests are ignored. Quest-triggered uses have independent
fixed five-minute cooldowns.

Specialization has a fixed safety reserve: it is not crafted if the resulting
Scrap amount would be below 50% of the player's current unlocked storage
capacity. This threshold is deliberately hidden rather than configurable, so
an unsafe setting cannot permanently starve the Scrap-overflow craftable. Key
Manifest is unaffected because it does not consume Scrap.

## Missing Materials

When enabled, ordinary missing materials can be purchased with Jewels of Soul
at 25%, 50%, or 100% capacity. Scrap, Simurgh Feathers, and Dragon Scales must
already be available where required and are never substituted by this path.

> `Buy Missing With Jewels` spends premium currency automatically. Review it
> before enabling the runtime on an unmodified save.

[Back to the Complete Manual](../MANUAL.md)
