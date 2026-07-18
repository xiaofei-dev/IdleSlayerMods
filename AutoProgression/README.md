# AutoProgression

AutoProgression automates long-term progression and repeatable account
maintenance in Idle Slayer. It combines purchases, normal Ascension,
craftables, materials, quests, eggs, and paid bonuses behind one global runtime
toggle while keeping individual feature groups configurable.

AutoProgression is part of **Tashi's Full Automation Suite**. It focuses on
account growth and menu-driven maintenance; AutoAdventurer handles active
gameplay and quest objectives, while AutoClimber handles Ascending Heights.

## Features

- Purchases the two 500x Jewels of Soul bonuses and refreshes them from their real cooldown state.
- Crafts and maintains supported temporary craftables, including Rage Pills, Whetstones, and both temporary staffs.
- Buys missing craftable materials with Jewels of Soul when allowed.
- Crafts Shards Necklaces when Scrap reaches a configurable percentage of its current capacity.
- Opens Dragon Eggs and Simurgh Eggs in the background while preserving configurable reserves.
- Purchases available skills and unlocked normal equipment.
- Performs normal Ascension at a configurable soul-bonus threshold and can buy skills afterward.
- Claims completed Daily and Weekly Quests, regenerates exhausted quest sets, keeps rerolls available, and resets the normal Portal cooldown. Unlimited Daily/Weekly rerolls and blocked vertical-magnet skills remain enforced whenever their settings are enabled, independently from the `T` automation toggle.
- Successful background Weekly Quest rerolls suppress only the trailing native UI exception; genuine reroll failures continue to surface normally.

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
- `Equipment`: idle and sleep timing for equipment purchasing. The latest unlocked equipment is purchased in 10-level increments; older equipment must afford at least 50 levels and is purchased in 50-level increments. Only a purchase meeting those thresholds resets the no-purchase timer.

> **Currency warning:** `Use Paid 500x Bonuses` directly spends Jewels of Soul. `Buy Missing With Jewels` allows enabled craftable features to spend Jewels of Soul automatically when recipe materials are insufficient. Review these settings before enabling AutoProgression on an unmodified save.

Existing configuration files are migrated when the configuration schema changes.

## Safety and Scope

Automation runs only after the normal gameplay screen has remained stable long enough to be safely operated. Missing or temporarily unavailable game objects are handled without intentionally interrupting the game loop.

Quest automation claims and refreshes quests; it does not travel to dimensions or perform quest objectives.

Character control, quest target selection, Rage coordination, and quest-guided
Portal travel belong to the companion AutoAdventurer mod. Ascending Heights
movement, rewards, and compatible quest enemies belong to AutoClimber.

## Building

Requirements:

- .NET 6 SDK
- Idle Slayer Mod Manager with MelonLoader initialized

From this directory:

```powershell
dotnet build
```

The build creates the DLL and `Publish/Debug/AutoProgression.zip`. It does not deploy automatically unless local deployment is explicitly enabled through the project build property.

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

- Public release version: `1.0.0`
- Internal development revisions are tracked separately in `AutoProgressionInfo.cs`.

## Disclaimer

This is an unofficial community mod. Idle Slayer and its assets belong to their respective owners.
