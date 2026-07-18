# Installation and Upgrading

## Requirements

- Idle Slayer Mod Manager with MelonLoader initialized
- Idle Slayer Mods Core
- A compatible AutoProgression ZIP or DLL

## Mod Manager Installation

1. Import `AutoProgression.zip` without extracting it.
2. Enable AutoProgression in the manager.
3. Start Idle Slayer through the configured loader.

## Manual Installation

Copy `AutoProgression.dll` to:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\Mods\
```

The generated configuration is stored at:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoProgression.cfg
```

## Upgrading

Replace the old DLL or import the new ZIP, then restart the game. Configuration
schema migrations preserve supported existing values and add new entries.
Keeping a backup of the configuration is still recommended.

[Back to the Complete Manual](../MANUAL.md)
