# AutoBonusRunner User Guide

AutoBonusRunner automatically plays supported Idle Slayer Bonus Stages. It
detects the active stage, plans jumps from the live terrain, handles wall
climbs and recovery, collects Bonus Spheres, and performs reward actions.

This guide covers installation, everyday controls, modes, and common
troubleshooting. For route behavior, every setting, and detailed diagnostics,
see the [Complete Manual](MANUAL.md).

## What AutoBonusRunner Does

- Supports Bonus Stages 1, 2, and 3.
- Adjusts jump timing for current movement speed and Spirit Boost.
- Handles gaps, height changes, trenches, wall contacts, and chained climbs.
- Continues normal terrain routing until a real reward object is confirmed.
- Uses small reward jumps, bow fire, and an available grounded Wind Dash.
- Can confirm the Bonus Stage start slider automatically.
- Handles the game's native one-use retry choice.
- Supports background control while the game window is unfocused.
- Records run results and detailed diagnostics.

AutoBonusRunner remains dormant outside supported Bonus Stages.

## Installation

### Idle Slayer Mod Manager (recommended)

1. Install and initialize Idle Slayer Mod Manager.
2. Install Idle Slayer Mods Core.
3. Import `AutoBonusRunner.zip` without extracting it.
4. Enable AutoBonusRunner and start the game.

### Manual installation

Place `AutoBonusRunner.dll` in:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\Mods\
```

Do not place the DLL directly in the Idle Slayer game directory.

Avoid running another mod that controls jump press and release at the same
time. Disable AutoJumpMod or similar automatic-jump mods while using
AutoBonusRunner.

## Default Control

| Key | Action |
|---|---|
| `U` | Disable or re-enable automatic Bonus Stage control |

An in-game notification confirms the new state. The key can be changed in
`AutoBonusRunner.cfg`.

Disabling automatic control releases mod-owned jump input. Detection,
diagnostics, and manual-jump observation remain available.

## Recommended First Setup

1. Launch the game once so the configuration file is created.
2. Keep `Mode = "Auto"` for normal unattended play.
3. Keep `Skip Start Slider = true`.
4. Choose whether the game's native one-use retry should be accepted.
5. Enter a supported Bonus Stage; no additional key press is required.

## Choosing a Mode

| Mode | Ordinary Bonus Stage | Spirit Boost Bonus Stage |
|---|---|---|
| `Auto` | Requires 1 Bonus Sphere, then continues toward the reward | Preserves the game's native sphere requirement |
| `Manual` | Preserves the game's native sphere requirement | Preserves the game's native sphere requirement |
| `Skip` | Requires 1 Bonus Sphere | Requires 1 Bonus Sphere |

`Auto` is the default. It preserves full Spirit Boost gameplay while allowing
ordinary Bonus Stages to complete quickly. `Manual` is recommended when
testing full routes or collecting the normal sphere requirement in every run.

The selected requirement is fixed for each section. If the requirement patch
is unavailable, AutoBonusRunner fails safely to the game's native value.

## Routing and Collection

AutoBonusRunner reads the active terrain and treats platform tops as landing
intervals rather than single points. It evaluates current speed, jump hold
time, predicted flight, landing safety, nearby spheres, and physical wall
contacts.

The planner prioritizes survival and section completion. Sphere collection is
best-effort beyond the amount required to finish. A run may use a normal jump,
enter a trench intentionally, climb a contacted wall, or re-plan after an
unexpected landing.

Spirit Boost changes horizontal speed during a section. The mod observes the
real velocity and updates later predictions instead of relying only on the
initial speed.

## Completion and Rewards

Reaching the sphere requirement does not immediately disable routing.
AutoBonusRunner continues across the terrain until it confirms a real reward
box, coin, or gem on consecutive frames.

After that confirmation it can:

- issue small jump pulses;
- fire the bow directly;
- use a selected grounded Wind Dash when its icon is visible, unlocked, and
  ready.

These reward actions do not require AutoAdventurer.

## Failure and Retry

- With `Auto Retry Enabled = true`, the mod chooses the real Continue option
  when the native one-use Second Wind choice is offered.
- With it set to `false`, the mod chooses No and exits.
- The mod does not create additional retry opportunities or repeatedly reopen
  the prompt.
- If automatic control is disabled with `U`, the retry prompt remains manual.

The run summary distinguishes successful completion, deaths, attempts, and
completion after a retry.

## Configuration File

Launch the game once to create:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoBonusRunner.cfg
```

Default user settings:

```ini
[AutoBonusRunner]
"Debug Mode" = false

[AutoBonusRunner Automation]
Mode = "Auto"
"Enabled On Startup" = true
"Toggle Key" = "U"
"Auto Retry Enabled" = false
"Skip Start Slider" = true
```

`Configuration Version` is managed internally and should not be edited.
Restart the game after changing settings so every value is loaded
consistently.

## Logs and Run Summaries

Important initialization messages, errors, user actions, and run summaries
remain visible when Debug Mode is disabled. Route, physics, landing, and
wall-recovery warnings are shown only when Debug Mode is enabled.

Independent session logs are stored in:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\UserData\AutoBonusRunner\Logs\
```

Enable Debug Mode only when investigating a reproducible problem. Detailed
logs include terrain scans, route candidates, jump input, physics feedback,
wall phases, predicted and actual landings, sphere progress, and reward
actions.

## Quick Troubleshooting

### Nothing happens in a Bonus Stage

Press the configured toggle key and look for the enabled notification. Confirm
that the stage is Bonus Stage 1, 2, or 3 and that AutoBonusRunner and Idle
Slayer Mods Core are enabled.

### The character does not jump correctly

Disable other automatic-jump mods. Two mods controlling the same jump input
can cancel holds or releases.

### The wrong sphere requirement is used

Check `Mode`, restart the game after editing, and confirm the startup log shows
exactly one sphere-requirement patch.

### A run dies but later completes

The controller includes recovery and native retry handling, but it does not
guarantee a deathless run. Preserve the complete independent session log when
the same location fails repeatedly.

### Background control does not work

Confirm AutoBonusRunner is enabled and no overlay or other input mod is
blocking the game process.

For more help, see [Troubleshooting](docs/08-troubleshooting.md) and
[Logging and Run Statistics](docs/07-logging.md).

## Full Automation Suite

- **AutoAdventurer** handles active gameplay, quest selection and travel,
  Rage, movement abilities, events, and bosses.
- **AutoProgression** handles purchases, Ascension, craftables, materials,
  eggs, and quest maintenance.
- **AutoClimber** handles Ascending Heights routes, enemies, and rewards.
- **AutoBonusRunner** handles supported Bonus Stages and their reward phase.

Each mod works independently. Together, they automate complementary parts of
Idle Slayer.

## Support Development

If these mods save you time, you can support continued development through
[PayPal](https://www.paypal.com/donate/?business=HK85PL8AREEXY&no_recurring=0&currency_code=USD).
