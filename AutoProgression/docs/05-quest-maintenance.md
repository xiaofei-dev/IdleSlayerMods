# Quest Maintenance

AutoProgression maintains quest data; it does not travel or perform objectives.

## Claiming and Regeneration

Completed Daily and Weekly Quests can be claimed automatically. When a quest
type is exhausted, the mod can generate its next set. Claims reacquire current
game data after each successful action to avoid reusing invalid IL2CPP quest
objects.

## Weekly Selection

Five seconds after a newly generated Weekly set becomes stable, the generated
slot can be rerolled until the 180,000 Rage Mode kill objective appears.
Existing additional Weekly slots are preserved. Existing sets and later manual
rerolls do not trigger this process.

## Daily Filtering

Five seconds after a newly generated Daily set becomes stable, these objectives
can be rerolled:

- Goblin kills
- Chest Hunt chests
- normal and Silver Random Boxes
- normal Boost uses (Wind Dash kill objectives remain)
- Rage Mode uses (Rage Mode kill objectives remain)
- Bonus Stage entries, sections, or completions
- Ascending Heights completions
- Grapple Run completions

Filtering is generation-triggered. Existing Daily Quests and manual rerolls
are not continuously rewritten.

## Other Options

The mod can keep Daily and Weekly rerolls available and reset the normal Portal
cooldown. Unlimited rerolls remain active independently from the `T` toggle.

[Back to the Complete Manual](../MANUAL.md)
