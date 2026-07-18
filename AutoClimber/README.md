# AutoClimber

AutoClimber fully automates the Ascending Heights minigame in Idle Slayer. It
plans routes from the live platform layout, handles difficult landings and
recovery, supports full or quick completion, and can pursue compatible quest
enemies without sacrificing run safety.

AutoClimber is part of **Tashi's Full Automation Suite**. It focuses on
Ascending Heights; AutoAdventurer handles active gameplay and quest travel,
while AutoProgression handles long-term account growth.

## Documentation

- [User Guide](USER_GUIDE.md) for installation, first setup, and common use.
- [Complete Manual](MANUAL.md) for modes, route behavior, configuration,
  logging, and troubleshooting.

## Features

- Detects and evaluates reachable Ascending Heights platforms.
- Prioritizes the finish and safe golden/strong jump pads, using only a small
  one-step route tie-break for ordinary platforms.
- Handles difficult landings, route recovery, and stalled movement.
- Supports background movement control while the game window is unfocused.
- Can automatically continue the challenge after a failed run.
- Can safely target nearby enemies without replacing completion as the primary
  objective.
- Provides Normal, Skip, and quest-aware Auto modes.
- Remains dormant outside Ascending Heights.

## Controls

- `Y`: Enable or disable AutoClimber by default.

The toggle key can be changed in the configuration file.

## Configuration

The configuration file is generated at:

```text
ModLoader/UserData/AutoClimber.cfg
```

Available settings include:

- `Debug Mode`: show detailed `[Debug]` route and lifecycle diagnostics
  (disabled by default). User actions, warnings, errors, and run summaries are
  always logged.
- `Enabled On Startup`: start with automation enabled.
- `Toggle Key`: keyboard key used to toggle automation.
- `Auto Retry Enabled`: continue after a failed run when enabled; automatically
  choose No and exit the challenge when disabled.
- `Mode`: `Auto` plays the full route only for an incomplete Ascending Heights
  enemy quest, `Normal` always plays the full route, and `Skip` always uses
  the independent quick-skip path. Quick-skip runs do not affect route
  statistics.

## Building

Requirements:

- .NET 6 SDK
- Idle Slayer Mod Manager with MelonLoader initialized

From this directory:

```powershell
dotnet build
```

The build creates the DLL and packaged ZIP without deploying by default.

## Full Automation Suite

- **AutoAdventurer** handles active gameplay, quest objectives, dimension
  travel, Rage, movement abilities, Bonus assistance, and boss fights.
- **AutoProgression** handles purchases, normal Ascension, craftables,
  materials, quests, eggs, and repeatable account maintenance.
- **AutoClimber** automates Ascending Heights route planning, recovery,
  rewards, and compatible quest enemies.

Each mod can be used independently. Together, they cover complementary parts
of a fully automated Idle Slayer setup.

## Support Development

If these mods save you time, you can support continued development through
[PayPal](https://www.paypal.com/donate/?business=HK85PL8AREEXY&no_recurring=0&currency_code=USD).

## Versioning

- Public release version: `1.0.1`
- Route-planner development revisions are tracked separately in the source and startup diagnostics.

## Disclaimer

This is an unofficial community mod. Idle Slayer and its assets belong to their respective owners.
