# Installation and Upgrading

## Recommended Installation

1. Install and initialize Idle Slayer Mod Manager.
2. Install Idle Slayer Mods Core.
3. Import the AutoAdventurer ZIP without extracting it.
4. Enable AutoAdventurer and start the game.

## Manual Installation

Place `AutoAdventurer.dll` in:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\Mods\
```

Do not place the DLL directly in the Steam game directory. A custom Mod Manager
installation must use its own `ModLoader\Mods` directory.

## Configuration File

The first launch creates:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoAdventurer.cfg
```

Edit the file while the game is closed, save it, and restart the game.

## Upgrading

- Replace the old DLL or import the new ZIP.
- Keep `AutoAdventurer.cfg` to preserve personal settings.
- The mod migrates renamed or removed settings when its schema changes.
- Do not manually edit `Configuration Version`.

## Requirements

- Idle Slayer
- MelonLoader 0.7.1 or newer
- Idle Slayer Mods Core
- Idle Slayer Mod Manager (recommended on Windows)

[Back to the Complete Manual](../MANUAL.md)

