# IdleSlayerMods

A collection of modular quality-of-life and automation mods for Idle Slayer.

## Active mods

- **AutoProgression** - Automates selected temporary craftables, paid bonuses, skill purchases, and normal equipment progression.
- **AutoClimber** - Automates Ascending Heights traversal and retry handling.
- **AutoAdventurer** - Automates selected character actions, beginning with configurable Rage Mode activation and stopping.

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

The source code is available for personal, non-commercial use and modification.
Redistribution, re-uploading, mirroring, bundling, and publication of original or
modified versions are prohibited without prior written permission. See `LICENSE`.
