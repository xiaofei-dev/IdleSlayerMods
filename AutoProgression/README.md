# AutoProgression

AutoProgression is an automation and quality-of-life mod for Idle Slayer. It combines repeatable progression tasks behind one global runtime toggle while keeping individual feature groups configurable.

## Features

- Purchases the two 500x Jewels of Soul bonuses and refreshes them from their real cooldown state.
- Crafts and maintains supported temporary craftables, including Rage Pills, Whetstones, and both temporary staffs.
- Buys missing craftable materials with Jewels of Soul when allowed.
- Crafts Shards Necklaces when Scrap reaches a configurable percentage of its current capacity.
- Opens Dragon Eggs and Simurgh Eggs in the background while preserving configurable reserves.
- Purchases available skills and unlocked normal equipment.
- Performs normal Ascension at a configurable soul-bonus threshold and can buy skills afterward.
- Claims completed Daily and Weekly Quests, regenerates exhausted quest sets, keeps rerolls available, and resets the normal Portal cooldown.

Ultra Ascension is never performed by this mod.

## Controls

- `T`: Enable or disable all AutoProgression runtime automation.

Individual features remain controlled by the configuration file. The global toggle does not change saved preferences.

## Configuration

The configuration file is generated at:

```text
ModLoader/UserData/AutoProgression.cfg
```

Settings are grouped into these sections:

- `AutoProgression`: configuration version and debug logging.
- `Ascension`: normal Ascension threshold, check interval, and post-Ascension skill purchasing.
- `Paid Bonuses`: paid 500x bonus automation.
- `Egg Opening`: Dragon Egg and Simurgh Egg reserve amounts.
- `Quests`: claiming, regeneration, rerolls, and Portal cooldown.
- `Craftables`: supported craftables, durations, Rage Pill interval, and Scrap threshold.
- Deathwave Scepter uses the shared timed-item refill and target durations, but
  only crafts while Simurgh Feathers remain above its configured reserve.
- `Materials`: Jewel material purchases and refill percentage.
- `Purchases`: purchase priority and skill/equipment switches.
- `Skills`: blocked skill options.
- `Equipment`: idle and sleep timing for equipment purchasing.

> **Currency warning:** `Use Paid 500x Bonuses` directly spends Jewels of Soul. `Buy Missing With Jewels` allows enabled craftable features to spend Jewels of Soul automatically when recipe materials are insufficient. Review these settings before enabling AutoProgression on an unmodified save.

Existing configuration files are migrated when the configuration schema changes.

## Safety and Scope

Automation runs only after the normal gameplay screen has remained stable long enough to be safely operated. Missing or temporarily unavailable game objects are handled without intentionally interrupting the game loop.

Quest automation claims and refreshes quests; it does not travel to dimensions or perform quest objectives.

Character control, quest target selection, Rage termination, and quest-guided Portal travel belong to a separate companion mod. Its implementation plan is documented in [`../CHARACTER_MOD_HANDOFF.md`](../CHARACTER_MOD_HANDOFF.md).

## Building

Requirements:

- .NET 6 SDK
- Idle Slayer Mod Manager with MelonLoader initialized

From this directory:

```powershell
dotnet build
```

The build creates the DLL and `Publish/Debug/AutoProgression.zip`. It does not deploy automatically unless local deployment is explicitly enabled through the project build property.

## Versioning

- Public release version: `1.0.0`
- Internal development revisions are tracked separately in `AutoProgressionInfo.cs`.

## Disclaimer

This is an unofficial community mod. Idle Slayer and its assets belong to their respective owners.
