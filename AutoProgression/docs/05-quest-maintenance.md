# Quest Maintenance

`Quests > Enabled` is the master switch for all behavior in this chapter.
Individual quest settings are ignored while it is disabled.

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
- material collection
- Chest Hunt chests
- normal and Silver Random Boxes
- normal Boost uses (Wind Dash kill objectives remain)
- Rage Mode uses (Rage Mode kill objectives remain)
- Bonus Stage entries, full completions, or section completions
- Ascending Heights completions
- Grapple Run completions

Filtering is generation-triggered. Existing Daily Quests and manual rerolls
are not continuously rewritten. Whenever the game generates or adds Daily
Quests, all currently active Daily slots receive one filtering pass; this also
covers an older slot retained while the game fills empty slots. Manual rerolls
still do not trigger another pass. Completed quests waiting to be claimed are
always skipped, including when completion occurs after a filtering target was
selected. After every native reroll, the service waits
briefly for the replacement object and performs a second completion check so a
late-appearing filtered objective is not missed. The generated-set event is
retained while the native reroll UI object is unavailable, and inactive loaded
objects are resolved directly, so opening the Quest menu is not required. A
normal Ascension pauses filtering behind the global lock but preserves the
captured generation event and resumes it afterward. If quest claiming and a
native reroll overlap, an unchanged target triggers another delayed full scan
instead of terminating the whole filtering pass.

If the native game reroll repeatedly leaves the same Daily Quest active, the
service waits one second and reacquires both the quest and reroll objects. After
five consecutive failures, that slot is left unchanged and filtering continues
with the remaining generated slots. The 500-attempt per-generation limit remains
as a final safeguard.

## Other Options

The mod can keep Daily and Weekly rerolls available and reset the normal Portal
cooldown. Unlimited rerolls remain active only while the global `T` toggle is on.

[Back to the Complete Manual](../MANUAL.md)
