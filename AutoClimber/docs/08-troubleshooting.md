# Troubleshooting

## AutoClimber Does Nothing

- Press the configured toggle key and look for the enabled notification.
- Confirm `Enabled On Startup = true` if no manual toggle is expected.
- Verify that AutoClimber and Idle Slayer Mods Core are enabled.
- AutoClimber intentionally remains dormant outside Ascending Heights.

## The Wrong Mode Runs

- Check `Mode` in `AutoClimber.cfg` and restart the game after editing.
- `Normal` always climbs, `Skip` always shortens the run, and `Auto` depends on
  a compatible unfinished Ascending Heights enemy quest.
- Auto makes its decision before the run and does not change it mid-run.

## The Character Stops Near the Top

Reaching the visible score is not completion. The mod must locate and land on
the real finish platform, then move right through the exit. Enable Debug Mode
and preserve the log if it remains stationary instead of completing.

Relevant debug fields include `FinishMapSpawned`, `FinishPlatformLocated`,
`FinishPlatformY`, and `FlagExitActive`.

## Practice Mode Does Not Finish

Current versions use the live finish-ground transform when available instead
of assuming a fixed score. Confirm version 1.2.0 or newer, enable Debug Mode,
and include the full run log if practice still stops at the top.

## Enemy Targeting Appears Inactive

- Confirm `Target Enemies = true`.
- Confirm the run is not using Skip.
- The enemy may be detected but rejected because the intercept is unsafe.
- Look at `EnemiesDetected` and `EnemyHits` in the run summary.

## A Run Fails on a Platform Edge

Occasional procedural layouts can still produce marginal landings. Debug logs
record the target type, safe half-width, player position, prediction, and miss
reason. The complete trace is required to distinguish route selection,
movement prediction, disappearing platforms, and landing tolerance.

## Auto Retry Is Too Fast or Does Not Exit

- Set `Auto Retry Enabled = true` to choose Continue Challenge.
- Set it to `false` to choose No and leave.
- Restart after changing the configuration.

## Background Control Does Not Work

AutoClimber uses a background-compatible input path only while Ascending
Heights route control is active. Check that another mod or overlay is not
blocking the game process and inspect the log for focus or input warnings.

## IL2CPP Registration Errors

Use the newest DLL and remove duplicate outdated copies. Current runtime
methods with managed-only route types are hidden from IL2CPP registration. If
the error persists, include the startup section of the latest MelonLoader log.

[Back to the Complete Manual](../MANUAL.md)
