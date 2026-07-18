# Auto Boost and Wind Dash

Press `L` to enable or disable Auto Boost.

## Selected Ability

- The mod reads `AbilitiesManager.selectedAbility` before activation.
- Switching between Boost and Wind Dash is followed automatically.
- The two abilities are not treated as independent cooldowns.
- Ability objects are re-resolved after Ascension and scene reconstruction.

## Cooldown and Delay

- Activation waits until the selected ability cooldown reaches zero.
- `Activation Delay Seconds` defaults to 0.1 seconds.
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

- Runner and Rage scenes use normal ability rules.
- In other minigames or reward sections, Wind Dash is allowed only while the
  game's main ability icon is active and visible.
- The minigame path stops as soon as the icon disappears.
- Normal Boost is not triggered through this path.
- Ground and activation-delay settings still apply.

The rule follows actual game UI availability instead of a hard-coded list of
minigame names.

## Scene Stabilization

Auto Boost waits 0.5 seconds after returning to the central gameplay scene.

[Back to the Complete Manual](../MANUAL.md)
