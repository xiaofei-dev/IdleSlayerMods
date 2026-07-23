# Auto Movement & Combat

Auto Movement & Combat starts enabled by default. Press `L` to disable or re-enable it.

## Jumping and Arrow Attacks

- Minimum-height jumping and automatic arrow attacks can be enabled
  independently in the configuration.
- Arrow frequency supports Light, Medium, High, Extra High, and Ultra.
- All movement and combat helpers run only in the central Runner/Rage scene.

## Selected Ability

- The mod reads `AbilitiesManager.selectedAbility` before activation.
- Switching between Boost and Wind Dash is followed automatically.
- The two abilities are not treated as independent cooldowns.
- Ability objects are re-resolved after Ascension and scene reconstruction.

## Cooldown and Delay

- Activation waits until the selected ability cooldown reaches zero.
- Ability activation uses a fixed internal 0.2-second delay after cooldown
  reaches zero. This safety timing is intentionally not exposed in the
  configuration file.
- Wind Dash pauses while a Chest Hunt Key is active on the map, preventing it
  from outrunning the key. Normal Boost is unaffected.
- Before horizontal Random Box tracking is unlocked, both Boost and Wind Dash
  pause while the box-catching helper is targeting an active box. Its jump
  lead adapts to the observed approach speed so existing speed bonuses do not
  invalidate the intercept timing.
- Enabling with `L` requests immediate activation, but all scene, unlock, and
  safety conditions still apply.
- The authoritative cooldown is synchronized immediately after activation to
  prevent duplicate calls in one ready window.

## Grounded Wind Dash

`Wind Dash Require Grounded` defaults to enabled:

- When ready, Wind Dash is checked every frame until the player is grounded.
- It activates immediately after ground contact without a second delay.
- This reduces the chance of passing above portals, keys, gems, or elite
  enemies.

Disable the setting to allow automatic airborne Wind Dash.

## Minigames and Reward Sections

Bonus stages, reward sections, and other minigames are excluded. AutoAdventurer
does not inject jumps, arrows, Boost, or Wind Dash into those scenes.

## Scene Stabilization

Auto Movement & Combat waits 0.5 seconds after returning to the central
gameplay scene.

[Back to the Complete Manual](../MANUAL.md)
