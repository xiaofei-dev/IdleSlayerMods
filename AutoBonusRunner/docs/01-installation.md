# Installation and Upgrading

## Idle Slayer Mod Manager

1. Install and initialize Idle Slayer Mod Manager.
2. Install Idle Slayer Mods Core.
3. Import `AutoBonusRunner.zip` without extracting it.
4. Enable AutoBonusRunner and launch Idle Slayer.

The ZIP contains the DLL and Mod Manager metadata.

## Manual Installation

Place `AutoBonusRunner.dll` in:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\Mods\
```

Do not place it directly in the Idle Slayer game folder. If the Mod Manager
uses a custom location, use that installation's `ModLoader/Mods` directory.

## Required Mod

AutoBonusRunner requires Idle Slayer Mods Core. MelonLoader and the required
game interop files are normally initialized by Idle Slayer Mod Manager.

The release was tested with MelonLoader 0.7.3 Open-Beta and Idle Slayer Mods
Core 1.3.2.

## Input Compatibility

Do not enable another mod that controls jump press, hold, and release during a
Bonus Stage. AutoJumpMod and similar mods can conflict even when both work
correctly on their own.

AutoBonusRunner does not require AutoAdventurer for routing, bow fire, or
completion Wind Dash.

## Configuration Location

The first launch creates:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoBonusRunner.cfg
```

Existing preferences are migrated automatically. `Configuration Version` is
an internal migration value and should not be edited manually.

## Upgrading

1. Close Idle Slayer.
2. Replace or re-import the old version.
3. Keep the existing configuration unless release notes say otherwise.
4. Launch the game and confirm the initialization line contains the expected
   public and internal versions.

Remove duplicate outdated AutoBonusRunner DLLs outside the locations managed
by Idle Slayer Mod Manager.

[Back to the Complete Manual](../MANUAL.md)

