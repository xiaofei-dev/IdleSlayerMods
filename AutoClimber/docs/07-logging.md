# Logging and Statistics

## User Logs

Important lifecycle messages remain visible with Debug Mode disabled:

- initialization and configured mode;
- enable or disable actions;
- warnings and errors;
- challenge results and aggregate run statistics;
- retry or exit actions.

## Run Summary

A full-route result prints a compact line similar to:

```text
Run count: Total=9, Success=8, Failure=1, PassRate=88.9%, LastResult=Success, EndReason=AscendingStateEnded, EnemiesDetected=21, EnemyHits=7
```

Depending on the current build and run history, the summary can also include
challenge completions, revived completions, completion rate, maximum failure
height, or confirmed enemy defeat totals.

`Success` and `Failure` describe individual lives. A challenge completed after
a revive can therefore have a failed life and still count as a completed
challenge.

## Skip Isolation

Skip runs are excluded from normal route success statistics. Their shortened
layout and independent finish override would otherwise make the full-route
pass rate misleading. Skip also suppresses detailed debug logs.

## Debug Mode

Enable Debug Mode when investigating a reproducible routing problem. It adds:

- candidate and target decisions;
- predicted movement and reachability;
- landing tolerances and target lifecycle changes;
- finish detection state;
- input state and recovery decisions;
- compact failure traces.

The MelonLoader log directory is normally:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\MelonLoader\Logs\
```

For a useful report, include the complete log containing the failed run rather
than only the final error line.

[Back to the Complete Manual](../MANUAL.md)
