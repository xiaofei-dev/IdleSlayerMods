# AutoAdventurer Complete Manual

AutoAdventurer is the active-gameplay module in Tashi's Full Automation Suite.
It coordinates quest objectives, dimension travel, character requirements,
Rage Mode, movement abilities, Bonus helpers, and boss fights.

This manual covers configuration version 35. New users should begin with the
[User Guide](USER_GUIDE.md).

## Reference Chapters

1. [Installation and Upgrading](docs/01-installation.md)
2. [Quick Start and Controls](docs/02-quick-start.md)
3. [Quest Automation](docs/03-quest-automation.md)
4. [Automatic Rage](docs/04-automatic-rage.md)
5. [Auto Movement & Combat](docs/05-auto-boost.md)
6. [Travel, Events, and Safety](docs/06-travel-and-events.md)
7. [Bonus, Slider, and Boss Features](docs/07-bonus-and-boss.md)
8. [Configuration Reference](docs/08-configuration.md)
9. [Logging Reference](docs/09-logging.md)
10. [Troubleshooting](docs/10-troubleshooting.md)

## Module Responsibilities

- **AutoAdventurer** selects and performs kill quests, travels between
  dimensions, switches compatible characters, and controls Rage/Boost helpers.
- **AutoProgression** handles purchases, normal Ascension, craftables,
  materials, eggs, and completed-quest claiming.
- **AutoClimber** controls Ascending Heights routes, enemies, and rewards.

The mods can be used independently or together.

## Default Controls

| Key | Action |
|---|---|
| `P` | Toggle Quest Automation |
| `K` | Toggle Automatic Rage |
| `J` | Immediately end the current Rage Mode |
| `L` | Toggle Auto Movement & Combat |

Automatic Rage and Quest Automation start disabled. Auto Movement & Combat
starts enabled by default and can still be toggled with `L`. Slider Skip and
Auto Boss are controlled directly by configuration and default to enabled.

## Safety Principles

- Quest decisions use internal IDs and game objects instead of localized text.
- Reward priority is applied only after a quest is proven executable.
- Native Portal cooldowns and destination restrictions are never bypassed.
- Only the manual `J` action force-ends Rage Mode.
- IL2CPP quest, enemy, map, character, and Portal objects are not retained
  across scene changes.
- Events, boxes, keys, portals, and special scenes take priority over travel.

## Support Development

If these mods save you time, you can support continued development through
[PayPal](https://www.paypal.com/donate/?business=HK85PL8AREEXY&no_recurring=0&currency_code=USD).
