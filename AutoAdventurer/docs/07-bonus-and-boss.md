# Bonus, Slider, and Boss Features

## Bonus Start Slider

`Skip Bonus Start Slider` defaults to enabled.

- It runs independently from the Runner/Rage action guard.
- It waits until `PopupSlider` is visible and fully initialized.
- It invokes confirmation exactly once for each visible slider.
- A later slider can be handled only after the previous one disappears.

## Wind Dash in Bonus Content

Minigames and reward sections use the real main-ability icon as authority:

- Visible icon: Wind Dash may activate.
- Hidden icon: no minigame Wind Dash activation.
- Normal Boost is not supported through this path.
- Grounded and activation-delay settings still apply.

## Auto Boss

`Auto Boss` defaults to enabled. In a supported boss scene it:

- Resolves the active boss controller.
- Limits boss health to 1 when it is higher.
- Advances required boss dialogue.
- Uses the game's direct arrow action for the finishing attack.
- Avoids a generic contextual action that can become a jump in boss mode.
- Handles the supported result close action.

Disabling the setting leaves boss health, dialogue, and combat untouched.

## Quest Automation Interaction

Quest scanning and Portal travel freeze in boss maps, Bonus stages, Chest Hunt,
Ascending Heights, and other special scenes. After returning to a stable
Runner/Rage scene, the locked task is resolved again from current objects.

[Back to the Complete Manual](../MANUAL.md)

