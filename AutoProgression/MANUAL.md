# AutoProgression Complete Manual

AutoProgression is the account-progression and maintenance module in Tashi's
Full Automation Suite. It coordinates normal Ascension, purchases,
craftables, materials, eggs, paid bonuses, and quest maintenance.

This manual covers configuration version 28. New users should begin with the
[User Guide](USER_GUIDE.md).

## Reference Chapters

1. [Installation and Upgrading](docs/01-installation.md)
2. [Quick Start and Global Control](docs/02-quick-start.md)
3. [Ascension and Purchases](docs/03-ascension-and-purchases.md)
4. [Craftables and Materials](docs/04-craftables-and-materials.md)
5. [Quest Maintenance](docs/05-quest-maintenance.md)
6. [Minions, Eggs, and Paid Bonuses](docs/06-eggs-and-paid-bonuses.md)
7. [Runtime Scope and Safety](docs/07-runtime-and-safety.md)
8. [Configuration Reference](docs/08-configuration.md)
9. [Logging Reference](docs/09-logging.md)
10. [Troubleshooting](docs/10-troubleshooting.md)

## Module Responsibilities

- **AutoProgression** handles purchases, normal Ascension, craftables,
  materials, eggs, and quest maintenance.
- **AutoAdventurer** selects and performs supported quest objectives, travels
  between dimensions, and controls Rage and movement helpers.
- **AutoClimber** controls Ascending Heights routes, enemies, and rewards.

The mods can be used independently or together.

## Default Control

| Key | Action |
|---|---|
| `T` | Toggle the AutoProgression runtime |

The runtime starts disabled. Feature preferences remain saved when `T` is
turned off.

## Safety Principles

- Ultra Ascension is disabled by default and requires both the game's native
  prerequisite state and at least 24 Astral Keys when explicitly enabled.
- Automation runs only in a stable central Runner or Rage scene.
- Ascension applies a global two-second lock and clears cached game objects.
- Item actions are limited to one per second.
- Missing IL2CPP objects cause a safe retry instead of a forced action.
- Jewel-spending behavior is explicit in configuration descriptions and logs.
- Quest filters react to generated sets rather than continuously rewriting
  existing or manually rerolled tasks.

## Support Development

If these mods save you time, you can support continued development through
[PayPal](https://www.paypal.com/donate/?business=HK85PL8AREEXY&no_recurring=0&currency_code=USD).
