# Runtime Scope and Safety

AutoProgression operates only in the stable central gameplay screen, including
normal Runner and Rage states. Shops, Ascension panels, Portals, minigames, and
other unsupported scenes pause runtime actions.

## Stability Guards

- The central screen must remain valid for three seconds before actions begin.
- Normal Ascension applies a two-second global transaction lock.
- Cached IL2CPP objects are cleared across scene and Ascension boundaries.
- Missing or invalid objects cause a retry instead of a forced call.
- Item automation has a short startup delay and permits at most one item action
  per second. Rage Pill is intentionally exempt from the startup delay.
- Egg opening is lower priority than craftable maintenance.

Quest-set rerolls wait five seconds after generation and verify that quest data
is in a normal state before modifying it. While a generated-set filter is
running, conflicting quest maintenance is paused. Daily replacements receive a
short post-reroll settle delay and a final second scan before filtering ends.
Missing Daily reroll objects are retried without discarding the generation.
Normal Ascension also preserves pending Daily and Weekly generation events;
explicit runtime shutdown still clears them. A native Daily reroll that leaves
its target active is treated as a transient claim/list race and retried within
the existing per-generation safety limit.

[Back to the Complete Manual](../MANUAL.md)
