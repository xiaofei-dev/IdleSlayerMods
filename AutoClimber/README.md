# AutoClimber

AutoClimber automatically controls movement during the Ascending Heights minigame in Idle Slayer.

## Features

- Detects and evaluates reachable Ascending Heights platforms.
- Plans multi-step climbing routes and preserves committed targets between scans.
- Handles difficult landings, route recovery, and stalled movement.
- Supports background movement control while the game window is unfocused.
- Can automatically continue the challenge after a failed run.
- Remains dormant outside Ascending Heights.

Enemy-target routing is currently disabled.

## Controls

- `Y`: Enable or disable AutoClimber by default.

The toggle key can be changed in the configuration file.

## Configuration

The configuration file is generated at:

```text
ModLoader/UserData/AutoClimber.cfg
```

Available settings include:

- `Debug Mode`: show detailed route and lifecycle diagnostics.
- `Enabled On Startup`: start with automation enabled.
- `Toggle Key`: keyboard key used to toggle automation.
- `Auto Retry Enabled`: automatically continue after a failed run.

## Building

Requirements:

- .NET 6 SDK
- Idle Slayer Mod Manager with MelonLoader initialized

From this directory:

```powershell
dotnet build
```

The build creates the DLL and packaged ZIP without deploying by default.

## Versioning

- Public release version: `1.0.0`
- Route-planner development revisions are tracked separately in the source and startup diagnostics.

## Disclaimer

This is an unofficial community mod. Idle Slayer and its assets belong to their respective owners.
