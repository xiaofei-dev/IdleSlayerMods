# Normal, Auto, and Skip Modes

## Normal

Normal always uses the complete route planner. Choose it for ordinary play,
rewards, Frozen Shards, enemy kills, and full-route statistics.

## Auto

Auto makes one decision immediately before each Ascending Heights run:

- a compatible incomplete Ascending Heights enemy quest is found: use the
  complete route;
- no such quest is found: use Skip.

The decision is latched for the run so a later quest scan cannot switch route
models halfway through a jump. If quest information is temporarily uncertain,
the mode does not infer Skip from missing data during an active decision.

Quest detection uses internal game objects and enemy identities rather than
localized quest text, so the selected game language does not control the
decision.

## Skip

Skip uses an independent short-completion path. It temporarily adjusts the
current minigame's finish distance and keeps the displayed progress target and
real finish behavior coordinated.

Skip is faster, but it may bypass:

- Ascending Heights enemies;
- Frozen Shards and other climb rewards;
- ordinary full-route platform gameplay.

Skip runs do not write route debug output and are excluded from full-route run
statistics.

## Changing Modes

Edit `Mode` in `AutoClimber.cfg`, save the file, and restart the game. Valid
values are `Normal`, `Auto`, and `Skip`; matching is case-insensitive.

[Back to the Complete Manual](../MANUAL.md)
