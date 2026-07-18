# Enemy Targeting and Rewards

## Enemy Detection

During a full-route run, AutoClimber scans live Ascending Heights enemy
objects and associates nearby enemies with route candidates. Detection is
independent of the game's display language.

## Safe Targeting

With `Target Enemies = true`, the planner can:

- adjust an airborne path without replacing the landing platform;
- select another platform when the alternate route is confirmed safe;
- retain the normal route when an enemy intercept would add meaningful risk.

The priority order remains completion, safe super-jump platforms, then enemy
collection. Some detected enemies are intentionally skipped.

## Statistics

The run summary includes:

- `EnemiesDetected`: enemies observed during the run;
- `EnemyHits`: confirmed enemy contacts or defeats credited to targeting;
- additional aggregate enemy fields when available.

These values measure best-effort collection, not a guaranteed kill rate.

## Rewards and Frozen Shards

Normal and Auto full-route runs preserve opportunities to collect climb
rewards, including Frozen Shards. Skip mode may bypass them. Frozen Shards are
associated with special gameplay content and are not described as exclusive
to Ascending Heights.

## Quest Use

Auto mode uses a compatible incomplete Ascending Heights enemy quest to decide
whether the full route is needed. AutoClimber performs the minigame and enemy
contacts; quest claiming remains the game's responsibility or can be handled
by AutoProgression.

[Back to the Complete Manual](../MANUAL.md)
