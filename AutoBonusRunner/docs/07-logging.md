# Logging and Run Statistics

## User Logs

Important lifecycle messages remain available with Debug Mode disabled:

- initialization and selected mode;
- enable or disable actions;
- retry decisions;
- section transitions;
- run results and aggregate statistics.

Errors remain visible because they can identify a real loading or runtime
failure. Route, physics, landing, and wall-recovery warnings are diagnostic
evidence and are emitted only when Debug Mode is enabled.

## Independent Session Logs

AutoBonusRunner also writes a dedicated session trace:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoBonusRunner\Logs\
```

The filename contains the date, start time, and internal version. A new game
session creates a new file, which makes it easier to avoid mixing tests from
different DLLs.

## Run Summary

A completed run prints fields such as:

```text
Run count: Total=12, Success=12, Failure=0, PassRate=100.0%, Deathless=2, SessionDeaths=18, Deaths=1, Attempts=2, SectionsCleared=4/4, SpiritBoost=True.
```

`Success` describes complete Bonus Stage runs. `Deaths` and `Attempts`
describe the current run, so a successful run can still include a death and a
native retry.

## Debug Mode

Enable Debug Mode for a reproducible route problem. It adds:

- diagnostic warnings;
- terrain and map-piece identity;
- current and alternative landing intervals;
- route candidates and rejection evidence;
- predicted speed, flight time, and landing;
- jump press, hold, release, and physics-frame evidence;
- wall contact and climb phases;
- sphere and boost progress;
- predicted-versus-actual landing results;
- completion target and reward actions.

## Useful Problem Report

Include:

1. the latest complete independent log;
2. Bonus Stage 1, 2, or 3;
3. ordinary or Spirit Boost mode;
4. the section where the problem occurred;
5. whether any manual input was used;
6. a screenshot or video when the visual route is important.

Do not use a newly created startup-only log in place of the previous complete
session.

[Back to the Complete Manual](../MANUAL.md)
