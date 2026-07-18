# Troubleshooting

## `T` toggles the mod but nothing happens

Use a normal Runner or Rage dimension and wait three seconds. Unsupported UI,
scene transitions, and post-Ascension reconstruction pause the runtime.

## A craftable is skipped

Confirm that it is unlocked, enabled, below its applicable duration or above
its inventory trigger, and has the required non-purchasable resources. Also
check the shared maximum duration and the global Jewel-material option.

## Automatic Ascension is delayed

Verify pending versus lifetime Slayer Points, the configured percentage, and
the check interval. Enabling performs one immediate check; later checks use the
configured schedule.

## Quest claiming or rerolling is delayed

New-set filtering intentionally waits five seconds for native quest data to
stabilize. It does not revisit old sets or react to manual rerolls. A claim
reacquires the list after every successful quest to avoid stale objects.

## Eggs do not open

The count must be above its reserve. Dragon Eggs additionally stop when Dragon
Scale storage is full. Eggs also yield to higher-priority item actions.

## Skills require opening a shop once

Some game versions initialize shop data lazily. AutoProgression attempts to
resolve the required objects safely, but opening the relevant shop once may be
necessary after a game update or unusual reset state.

## The game crashes or freezes

1. Disable AutoProgression with `T` if possible.
2. Reproduce with other automation mods disabled.
3. Save the latest MelonLoader log.
4. Record the scene, open panel, and action immediately before the issue.
5. Check whether the stack trace names AutoProgression or another mod.

[Back to the Complete Manual](../MANUAL.md)
