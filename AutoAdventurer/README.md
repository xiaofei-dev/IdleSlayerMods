# AutoAdventurer

AutoAdventurer automates selected character actions in Idle Slayer. The first
development feature is Automatic Rage.

## Automatic Rage

- Press `K` to enable or disable Automatic Rage by default.
- While enabled, the mod checks every 10 seconds and activates Rage Mode when
  the player is in Runner or Rage Mode and Rage has no remaining cooldown.
- The periodic activation continues while Rage is already executing; repeated
  activations do not reset the current cycle's maximum-duration timer.
- An automatically started Rage ends when a Chest Hunt Key appears or when the
  configured maximum duration is reached.
- Press `J` to end the current Rage Mode immediately by default. If automation
  remains enabled, this ends only the current cycle.
- Disabling Automatic Rage stops future automatic activations but does not end
  a Rage Mode that is already executing; the current Rage ends naturally.
- After Rage ends, the mod waits five seconds by default, then checks for a
  Chest Hunt Key, Special Random Box/minigame trigger, or active portal. It
  starts the next Rage cycle only after no blocker remains.
- The same world-blocker check runs immediately before every Rage activation,
  including the first activation after enabling automation. Debug logs identify
  the special item, portal, door, or gate that blocked activation.
- Random minigame icons for Ascending Heights and Grapple Run are also treated
  as blockers when their active spawned objects can be identified.
- In-game notifications are shown only when `K` enables or disables Automatic
  Rage. Automatic activation, manual stopping, key detection, and time-limit
  stopping are logged without additional screen notifications.

The mod does not currently restart running after Rage ends.

Character automation pauses while another game menu is open. After returning
to the central gameplay screen, Rage and Auto Boost wait for two seconds of
stable gameplay before resuming. Slider Skip remains active in supported
minigame and boss pre-start screens.

## Bonus Slider Skip

When `Skip Bonus Start Slider` is enabled, AutoAdventurer automatically
confirms the timing slider shown before supported bonus minigames. This feature
is enabled by default and can be disabled independently from Automatic Rage.

## Smart Auto Boost

- Press `L` to enable or disable Auto Boost by default.
- The currently selected player ability is read from `AbilitiesManager` before
  every activation.
- When the selected ability is unlocked and its cooldown reaches zero, the
  game ability manager triggers it.
- Switching between Boost and Wind Dash is followed automatically; the mod
  does not attempt to activate both abilities independently.
- Auto Boost never reads or activates abilities during Rage Mode, where the
  selected movement ability is temporarily unavailable. After Rage ends, it
  waits for Runner Mode to remain stable for two seconds before resuming.
- Ascension clears the observed ability state and pauses Auto Boost for five
  seconds. A supported selected ability must then remain stable across several
  checks before automatic activation resumes.

## Configuration

The configuration file is generated at:

```text
ModLoader/UserData/AutoAdventurer.cfg
```

Settings include the automation toggle key, manual stop key, activation check
interval, maximum Rage duration, and debug logging. The default maximum Rage
duration is 120 seconds. Set it to `0` to disable the duration limit. The
post-Rage world observation delay is separately configurable.

## Building

From this directory:

```powershell
dotnet build --no-restore
```

The build creates the DLL and packaged ZIP without deploying by default. Local
deployment is opt-in with `/p:EnableLocalDeploy=true`.

## Versioning

- Public release version: `1.0.0`
- Internal development version: `V0.24`

## Disclaimer

This is an unofficial community mod. Idle Slayer and its assets belong to their
respective owners.
