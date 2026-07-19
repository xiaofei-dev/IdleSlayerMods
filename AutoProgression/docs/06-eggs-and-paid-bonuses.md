# Minions, Eggs, and Paid Bonuses

## Minion Automation

Two independent settings control Minions, and both require the global `T`
runtime to be active.

- `Auto Claim and Send` claims completed unlocked Minion missions and sends a
  standing Minion when its mission cost is lower than available Slayer Points.
- `Automatic Maximum-Level Prestige` checks standing Minions with a level
  above 1 and a maximum level of at least 70. When Minion Prestige is unlocked,
  the eligible Minion is raised to maximum level for that prestige action.

When both are enabled, the order is claim, prestige, then send. When only
automatic prestige is enabled, a mission must be claimed manually before its
standing Minion can be processed. Locked Minions and an unavailable Prestige
system are skipped safely.

## Background Egg Opening

Eggs are opened directly in the background without playing the normal opening
animation.

- Dragon Eggs open only above their reserve and only while Dragon Scale
  storage is not full.
- Simurgh Eggs open only above their reserve. Simurgh Feathers have no storage
  cap, so no equivalent full-storage check is needed.

Default reserves are 300 Dragon Eggs and 10 Simurgh Eggs.

## Paid 500x Bonuses

The Souls and CpS 500x bonuses can be purchased automatically. A timer avoids
constant checks, but when it expires the service reads the real remaining
effect duration and updates the next check conservatively. This also tolerates
time-changing effects.

> `Use Paid 500x Bonuses` directly spends Jewels of Soul. It is intended only
> for players who explicitly want that behavior.

[Back to the Complete Manual](../MANUAL.md)
