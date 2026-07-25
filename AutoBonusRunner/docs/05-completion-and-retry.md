# Completion, Rewards, and Retry

## Sphere Completion Is Not the Reward

Meeting the sphere requirement is diagnostic evidence, not permission to
abandon terrain routing. The same movement planner continues until the
runtime confirms a real reward target.

## Typed Reward Target

Reward control requires the same active reward box, coin, or gem to qualify on
two consecutive observations. A latched target remains authoritative until an
explicit stage reset.

Native reward flags and missing terrain are not enough by themselves to
authorize reward actions.

## Reward Actions

After the reward target is latched, the controller can:

- issue minimum jump pulses while grounded and stable;
- fire the bow at a controlled interval;
- activate the selected Wind Dash when its icon is visible, the ability is
  unlocked and ready, and the player is grounded or stably at ground height.

Wind Dash support is independent of AutoAdventurer.

## Completion Traversal

The game can temporarily report that active collection has ended before the
next section or reward is physically reached. AutoBonusRunner preserves route
continuity during this transition and records completion-traversal state
separately from native reward state.

## Native Second Wind

AutoBonusRunner handles only the real one-use retry choice offered by the
game:

- `Auto Retry Enabled = true`: choose Continue.
- `Auto Retry Enabled = false`: choose No and exit.
- automatic control disabled: leave the prompt for manual input.

Successful Continue consumption is never reset or recreated. Only a failed UI
dispatch may be retried, with a bounded attempt count.

[Back to the Complete Manual](../MANUAL.md)

