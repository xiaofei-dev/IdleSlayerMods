# Installation and Upgrading

## Idle Slayer Mod Manager

1. Install and initialize Idle Slayer Mod Manager.
2. Install Idle Slayer Mods Core.
3. Import the AutoClimber ZIP without extracting it.
4. Enable AutoClimber and launch Idle Slayer.

The ZIP contains the DLL and Mod Manager metadata.

## Manual Installation

Place `AutoClimber.dll` in:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\Mods\
```

Do not place it directly in the Idle Slayer game folder. If the Mod Manager
uses a custom location, use that installation's `ModLoader/Mods` directory.

## Required Mod

AutoClimber requires Idle Slayer Mods Core. MelonLoader and the required game
interop files are normally initialized by Idle Slayer Mod Manager.

## Configuration Location

The first launch creates:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoClimber.cfg
```

Existing preferences are migrated automatically. `Configuration Version` is
an internal migration value and should not be edited manually.

## Upgrading

1. Close Idle Slayer.
2. Replace or re-import the old version.
3. Keep the existing configuration unless release notes say otherwise.
4. Launch the game and confirm the AutoClimber initialization line.

Do not keep duplicate AutoClimber DLLs in multiple loader folders outside the
locations managed by Idle Slayer Mod Manager.

[Back to the Complete Manual](../MANUAL.md)
