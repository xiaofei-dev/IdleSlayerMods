# AutoClimber Complete Manual

AutoClimber is the Ascending Heights module in Tashi's Full Automation Suite.
It controls route selection, platform landings, recovery, finish handling,
optional enemy targeting, and retry prompts.

This manual covers public version 1.2.0 and configuration version 10. New users
should begin with the [User Guide](USER_GUIDE.md).

## Reference Chapters

1. [Installation and Upgrading](docs/01-installation.md)
2. [Quick Start and Controls](docs/02-quick-start.md)
3. [Routes, Platforms, and Completion](docs/03-routes-and-platforms.md)
4. [Normal, Auto, and Skip Modes](docs/04-modes-and-quests.md)
5. [Enemy Targeting and Rewards](docs/05-enemies-and-rewards.md)
6. [Configuration Reference](docs/06-configuration.md)
7. [Logging and Statistics](docs/07-logging.md)
8. [Troubleshooting](docs/08-troubleshooting.md)

## Module Responsibilities

- **AutoClimber** controls Ascending Heights routes, enemies, rewards, and the
  challenge retry prompt.
- **AutoAdventurer** handles active gameplay, quest selection, dimension
  travel, Rage, movement abilities, Bonus assistance, and boss fights.
- **AutoProgression** handles purchases, Ascension, craftables, materials,
  eggs, and completed-quest claiming.

The mods can be used independently or together.

## Default Control and Mode

| Setting | Default |
|---|---|
| Toggle key | `Y` |
| Enabled on startup | Yes |
| Mode | `Normal` |
| Auto retry | No |
| Target enemies | Yes |
| Debug logging | No |

## Design Priorities

1. Confirm the real finish and complete the run.
2. Prefer safe super-jump platforms and stable landings.
3. Recover when a planned platform disappears or becomes invalid.
4. Pursue enemies only when route safety remains acceptable.
5. Keep Skip behavior and statistics separate from the full-route planner.
