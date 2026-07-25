# Troubleshooting

## AutoBonusRunner Does Nothing

- Press the configured toggle key and confirm the enabled notification.
- Confirm `Enabled On Startup = true` if no manual toggle is expected.
- Verify that AutoBonusRunner and Idle Slayer Mods Core are enabled.
- Confirm the active map is Bonus Stage 1, 2, or 3.
- Check the startup log for the expected internal version.

## The Character Will Not Jump

- Disable AutoJumpMod and any other mod controlling jump input.
- Check that `U` has not disabled automatic control.
- Preserve warnings about input not being accepted by the game.
- Restart the game after replacing the DLL.

## Jump Distance Is Wrong

Jump distance depends on current horizontal speed, Spirit Boost, press time,
input delay, and collisions. A useful log must include the full approach,
takeoff, and landing rather than only the death line.

Look for predicted and actual speed, flight time, landing position, and
trajectory compatibility in Debug Mode.

## The Character Stops at a Wall

Wall climbing uses bounded phases and requires a real physical contact. A
rejected impulse can be retried, but repeated rejection at the same face may
indicate stale wall ownership or an input barrier problem.

Preserve `WallClimbImpulseRejected`, `ReactiveWallRouteRebased`, and the
surrounding physics-frame evidence.

## The Sphere Requirement Looks Wrong

- `Auto` uses 1 sphere only in ordinary runs.
- `Manual` always preserves the native requirement.
- `Skip` always uses 1 sphere.
- Restart after changing `Mode`.
- Check startup patch inventory; missing verification falls back to the native
  value.

## The Character Dies After Meeting the Requirement

The mod should continue normal routing until a real reward target is
confirmed. Repeated deaths after `Remaining=0` are completion-traversal
problems, not successful reward detection. Include the entire section log.

## Retry Is Repeated or Does Nothing

AutoBonusRunner uses only the native one-use Second Wind choice. Set `Auto
Retry Enabled` to true for Continue or false for No. If control is disabled,
the prompt remains manual.

Include retry state, prompt callbacks, and UI-dispatch warnings when reporting
a problem.

## Background Control Does Not Work

AutoBonusRunner uses a background-compatible jump path only during supported
route control. Check that another input mod, overlay, or game process setting
is not blocking input.

## Performance Drops

Static map and object data are cached. Heavy route planning should be bounded
and event-driven. Enable Debug Mode for one reproducible run and look for
performance, budget, or cache-reset evidence rather than leaving detailed
logging enabled indefinitely.

## IL2CPP Registration Errors

Use the newest DLL and remove duplicate outdated copies. Include the startup
section of the MelonLoader log if registration still fails.

[Back to the Complete Manual](../MANUAL.md)

