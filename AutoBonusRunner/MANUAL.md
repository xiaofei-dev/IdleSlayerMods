# AutoBonusRunner Complete Manual

AutoBonusRunner is the Bonus Stage module in Tashi's Full Automation Suite. It
controls sphere requirements, terrain routing, jump timing, wall-climb
recovery, reward actions, and the native retry choice.

This manual covers public version 1.0.1, internal version V1.25, and
configuration version 44. New users should begin with the
[User Guide](USER_GUIDE.md).

## Reference Chapters

1. [Installation and Upgrading](docs/01-installation.md)
2. [Quick Start and Controls](docs/02-quick-start.md)
3. [Modes and Sphere Requirements](docs/03-modes-and-requirements.md)
4. [Routing, Jumping, and Wall Recovery](docs/04-routing-and-recovery.md)
5. [Completion, Rewards, and Retry](docs/05-completion-and-retry.md)
6. [Configuration Reference](docs/06-configuration.md)
7. [Logging and Run Statistics](docs/07-logging.md)
8. [Troubleshooting](docs/08-troubleshooting.md)

## Module Responsibilities

- **AutoBonusRunner** controls supported Bonus Stage routes, collection,
  rewards, and the native Second Wind choice.
- **AutoAdventurer** handles active gameplay, quests, dimension travel, Rage,
  movement abilities, events, and bosses.
- **AutoProgression** handles purchases, Ascension, craftables, materials,
  eggs, and account maintenance.
- **AutoClimber** handles Ascending Heights routes, enemies, and rewards.

The mods can be used independently or together.

## Default Control and Mode

| Setting | Default |
|---|---|
| Toggle key | `U` |
| Enabled on startup | Yes |
| Mode | `Auto` |
| Auto retry | No |
| Skip start slider | Yes |
| Debug logging | No |

## Design Priorities

1. Keep the player on a reachable landing route.
2. Collect enough Bonus Spheres to complete the active section.
3. Adapt jump distance to observed speed and physical feedback.
4. Recover from unexpected walls, landings, and missed predictions.
5. Continue normal routing until a real reward target is confirmed.
6. Keep diagnostic detail available without requiring it during normal use.
