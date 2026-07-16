# IdleSlayerMods

A collection of modular quality-of-life and automation mods for Idle Slayer.

## Active mods

- **AutoProgression** - Automates selected temporary craftables, paid bonuses, skill purchases, and normal equipment progression.
- **AutoClimber** - Automates Ascending Heights traversal and retry handling.

## Archived mods

The `Archive` directory contains older standalone experiments that are no longer actively maintained:

- AutoBuy500x
- AutoRageStopper

Their functionality has been reorganized into newer independent mods where appropriate.

## Building

Requirements:

- .NET 6 SDK
- Idle Slayer Mod Manager with MelonLoader initialized at its default location

From a mod project directory:

```powershell
dotnet build --no-restore
```

The projects resolve game assemblies from:

```text
%LocalAppData%\IdleSlayerModManager\ModLoader
```

For a custom location:

```powershell
dotnet build /p:IdleSlayerModLoaderDir="D:\path\to\ModLoader"
```

Builds generate a DLL and packaged ZIP without deploying by default. Local deployment is opt-in:

```powershell
dotnet build /p:EnableLocalDeploy=true
```

## Disclaimer

Idle Slayer and all related game assets and trademarks belong to their respective owners. This repository contains unofficial community-made mods and is not affiliated with or endorsed by the game developers.

## License

Source code in this repository is available under the MIT License. See `LICENSE`.
