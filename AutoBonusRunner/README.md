# AutoBonusRunner

AutoBonusRunner automatically plays supported Idle Slayer Bonus Stages. It
plans routes from live terrain, controls jump timing, handles wall climbs and
recovery, and continues into the reward phase.

AutoBonusRunner is the Bonus Stage module in **Tashi's Full Automation Suite**.
It remains dormant outside supported Bonus Stages and can be used independently
or together with the other suite modules.

## Documentation

- [User Guide](USER_GUIDE.md) for installation, first setup, modes, and common use.
- [Complete Manual](MANUAL.md) for routing, recovery, configuration, logging,
  and troubleshooting.

## Features

- Supports Bonus Stages 1, 2, and 3.
- Plans from live platforms, gaps, hazards, walls, and Bonus Spheres.
- Adjusts jump timing for current speed and Spirit Boost.
- Handles trenches, wall climbs, difficult landings, and bounded recovery.
- Supports Auto, Manual, and Skip sphere-requirement modes.
- Confirms the start slider and handles the native one-use retry choice.
- Performs reward jumps, bow attacks, and an available grounded Wind Dash.
- Supports background control and detailed per-run diagnostics.

## Compatibility

Do not run AutoBonusRunner with Auto Jump or Bonus Stage Completer. They can
compete for Bonus Stage input and completion state. AutoAdventurer 2.0 includes
automatic attacking, while AutoBonusRunner's `Skip` mode provides the same
quick-completion behavior as Bonus Stage Completer.

## Controls and Configuration

- `U`: Disable or re-enable automatic Bonus Stage control by default.

The configuration file is generated at:

```text
ModLoader/UserData/AutoBonusRunner.cfg
```

Start with `Mode = "Auto"`. Use `Manual` to preserve the native sphere
requirement in every section or `Skip` for fast completion.

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

- **AutoAdventurer** handles active gameplay, quests, dimension travel, Rage,
  movement abilities, events, and bosses.
- **AutoProgression** handles purchases, Ascension, craftables, materials,
  eggs, Minions, and account maintenance.
- **AutoBonusRunner** handles supported Bonus Stages and their reward phase.
- **AutoClimber** handles Ascending Heights routes, enemies, and rewards.

Each mod can be used independently. Together, they automate complementary
parts of Idle Slayer.

## Versioning

- Public release version: `1.0.1`
- Internal development revisions are tracked separately in
  `AutoBonusRunnerInfo.cs` and startup diagnostics.

## Disclaimer

This is an unofficial community mod. Idle Slayer and its assets belong to their
respective owners.
