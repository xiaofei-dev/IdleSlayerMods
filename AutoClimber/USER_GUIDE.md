# AutoClimber User Guide

AutoClimber automatically completes Idle Slayer's Ascending Heights minigame.
It plans a safe route across the live platform layout, uses super-jump pads,
recovers from difficult landings, and continues through the real finish line.

This guide is for players who want to install the mod and start using it. For
technical behavior, every setting, and detailed diagnostics, see the
[Complete Manual](MANUAL.md).

## What AutoClimber Does

- Supports practice, 1,000-point, and 2,000-point Ascending Heights layouts.
- Detects normal, moving, fragile, strong, golden, and finish platforms.
- Prioritizes reliable completion and valuable super-jump platforms.
- Keeps moving after the displayed score threshold until the real exit is
  reached.
- Can touch nearby Ascending Heights enemies when the detour remains safe.
- Can collect Frozen Shards and other rewards available during the full climb.
- Supports background control while the game window is unfocused.
- Can retry after failure or choose No and leave automatically.

AutoClimber remains dormant outside Ascending Heights.

## Installation

### Idle Slayer Mod Manager (recommended)

1. Install and initialize Idle Slayer Mod Manager.
2. Install Idle Slayer Mods Core.
3. Import the AutoClimber ZIP without extracting it.
4. Enable AutoClimber and start the game.

### Manual installation

Place `AutoClimber.dll` in:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\Mods\
```

Do not place the DLL directly in the Idle Slayer game directory.

## Default Control

| Key | Action |
|---|---|
| `Y` | Enable or disable AutoClimber |

The key can be changed in `AutoClimber.cfg`. An in-game notification confirms
whether automation is enabled or disabled.

## Recommended First Setup

1. Launch the game once so the configuration file is created.
2. Leave `Mode = "Normal"` for full completion, enemies, and rewards.
3. Leave `Target Enemies = true` if you want safe enemy collection.
4. Decide whether failed challenges should retry automatically.
5. Enter Ascending Heights; no additional input is required.

## Choosing a Mode

| Mode | Behavior |
|---|---|
| `Normal` | Always plays the complete route. Recommended for general use, enemies, and rewards. |
| `Auto` | Plays the full route only when an incomplete quest requires an Ascending Heights enemy; otherwise uses Skip. |
| `Skip` | Uses a separate short route for fast completion and may bypass enemies and climb rewards. |

The Auto decision is made before each minigame run and stays fixed for that
run. Missing or uncertain quest data does not force an unsafe Skip decision.

## Platforms and Completion

AutoClimber treats platforms as landing areas rather than points. It predicts
moving-platform positions, gives fragile platforms a lower safety preference,
and uses strong or golden super-jump platforms when they provide a safe route.

The displayed score is not treated as proof of completion. The mod waits for
the spawned finish platform, lands on it, stabilizes, and then keeps moving
right until the real exit is triggered. This applies to practice and full
challenges.

## Enemy Targeting

When `Target Enemies = true`, AutoClimber may adjust an airborne path or choose
another safe platform to touch a detected enemy. Completion and super-jump
platforms retain priority. An enemy is intentionally skipped when pursuing it
would make the route meaningfully less reliable.

Enemy targeting is best-effort, not a promise to defeat every enemy.

## Failure and Retry

- With `Auto Retry Enabled = true`, the mod selects Continue Challenge after a
  failed run.
- With it set to `false`, the mod selects No and exits the retry prompt.
- A revived run is tracked separately so the summary can distinguish first-
  life completion from completion after revival.

## Configuration File

Launch the game once to create:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoClimber.cfg
```

Default user settings:

```ini
[AutoClimber]
"Debug Mode" = false

[Automation]
Mode = "Normal"
"Enabled On Startup" = true
"Toggle Key" = "Y"
"Auto Retry Enabled" = false
"Target Enemies" = true
```

Save changes and restart the game so every setting is loaded consistently.
See the [Configuration Reference](docs/06-configuration.md) for details.

## Run Summary

Full-route runs always produce a compact summary similar to:

```text
Run count: Total=10, Success=9, Failure=1, PassRate=90.0%, ... EnemiesDetected=20, EnemyHits=9
```

Skip runs are excluded from normal route statistics and detailed route debug
output so they do not distort full-route reliability data.

## Quick Troubleshooting

### Nothing happens in Ascending Heights

Press the configured toggle key and confirm the enabled notification. Check
that `Enabled On Startup` is true if you expect automation immediately.

### The mod takes a route that misses an enemy

Confirm `Target Enemies = true`. The enemy may have been rejected because the
detour was unsafe or conflicted with a better platform.

### The score is complete but the character keeps moving

This is intentional until the real finish platform and exit are confirmed.
If the character remains stuck at the top, enable Debug Mode and keep the
relevant MelonLoader log.

### The mod chose Skip unexpectedly

Check `Mode`. Auto uses Skip unless a supported incomplete Ascending Heights
enemy quest is positively detected before the run.

For more help, see [Troubleshooting](docs/08-troubleshooting.md) and
[Logging and Statistics](docs/07-logging.md).

## Full Automation Suite

- **AutoAdventurer** handles active gameplay, quest selection and travel,
  Rage, movement abilities, Bonus helpers, and bosses.
- **AutoProgression** handles purchases, Ascension, craftables, materials,
  eggs, and quest claiming.
- **AutoClimber** handles Ascending Heights routes, enemies, and rewards.

Each mod works independently. Together, they automate complementary parts of
Idle Slayer.

## Support Development

If these mods save you time, you can support continued development through
[PayPal](https://www.paypal.com/donate/?business=HK85PL8AREEXY&no_recurring=0&currency_code=USD).
