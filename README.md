# IdleSlayerMods

**Tashi's Full Automation Suite** is a collection of companion automation mods
for Idle Slayer. The projects divide active gameplay, long-term account
progression, and specialized minigames into independent modules that can be
used separately or together.

The goal is reliable unattended progression with configurable behavior,
scene-aware safety, recoverable failures, and useful diagnostics instead of a
single monolithic automation loop.

## Full Automation Suite

### AutoAdventurer

Automates active gameplay and quest objectives:

- Selects supported kill quests and travels to valid unlocked dimensions.
- Handles exact enemies, evolution stages, categories, elements, arrows, and
  compatible character requirements.
- Coordinates Automatic Rage, Boost/Wind Dash, portals, random boxes, and map
  events.
- Supports Bonus Stage assistance, session quest statistics, and optional
  automatic boss fights.

### AutoProgression

Automates long-term account progression and repeatable maintenance:

- Purchases skills and equipment and performs configurable normal Ascensions.
- Maintains supported temporary craftables and material reserves.
- Opens eggs, purchases configured bonuses, and manages quest maintenance.
- Keeps risky currency-spending features independently configurable.

### AutoClimber

Fully automates Ascending Heights:

- Plans safe routes from the live platform layout.
- Handles super jumps, difficult landings, recovery, and automatic retries.
- Supports 1,000-point and 2,000-point challenges, practice mode, and
  background play.
- Can collect rewards and safely pursue compatible quest enemies.

## Using the Mods Together

- AutoAdventurer decides what active quest objective to pursue and where to
  travel.
- AutoProgression maintains the account systems that support continued growth.
- AutoClimber takes control only inside Ascending Heights and returns control
  when the minigame ends.

Each project has its own configuration, toggle keys, debug logging, and safety
boundaries. Installing one does not require enabling the others.

## Installation

Idle Slayer Mod Manager is the recommended installation method. Import each
downloaded mod ZIP without extracting it, ensure Idle Slayer Mods Core is
installed, then enable the desired mods.

For manual installation, place each DLL in:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\Mods\
```

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

## Support Development

If these mods save you time, you can support continued development through
[PayPal](https://www.paypal.com/donate/?business=HK85PL8AREEXY&no_recurring=0&currency_code=USD).

## Disclaimer

Idle Slayer and all related game assets and trademarks belong to their respective owners. This repository contains unofficial community-made mods and is not affiliated with or endorsed by the game developers.

## License

The source code is available for personal, non-commercial use and modification.
Redistribution, re-uploading, mirroring, bundling, and publication of original or
modified versions are prohibited without prior written permission. See `LICENSE`.
