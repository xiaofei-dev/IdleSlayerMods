# Automatic Rage

Press `K` to toggle Automatic Rage. Press `J` to immediately end the current
Rage execution.

## Activation Cycle

- Readiness is checked every 12 seconds by default.
- Rage can be refreshed while an execution is already active.
- Refreshes do not restart the current cycle's maximum-duration timer.
- At the configured limit, refreshes stop and the execution ends naturally.
- The default maximum is 120 seconds; `0` disables the limit.

## Post-Rage Protection

After Rage ends, the map is protected for 5 seconds by default. During this
window the mod:

- Does not restart Rage.
- Prioritizes a fresh quest scan.
- Checks keys, boxes, events, minigame items, and portals.
- Revalidates a planned destination before opening a Portal.

This shared delay prevents a new Rage or Portal from starting before delayed
world objects have appeared.

## Natural and Forced End Behavior

- Pressing `K` off stops future activations but does not end the current Rage.
- Reaching the duration limit stops refreshes and waits naturally.
- Detecting a Chest Hunt Key stops refreshes and waits naturally.
- Quest travel stops refreshes and waits naturally.
- Only the manual `J` key force-ends the current execution.

## Blockers

- Chest Hunt Key
- Existing Portal, door, or interactive entrance
- Special Random Box
- Ascending Heights or Grapple Run trigger items
- Unsupported scenes and map transitions
- Quest travel, character correction, or arrow-task suppression
- Post-Rage protection

## Scene Stabilization

After returning from a village, minigame, story scene, or Portal transition,
the central Runner/Rage scene must remain stable for 2 seconds before
Automatic Rage resumes.

[Back to the Complete Manual](../MANUAL.md)

