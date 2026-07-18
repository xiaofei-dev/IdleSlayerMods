# Travel, Events, and Safety

## Travel Sequence

1. Select and lock an executable quest.
2. Stay if the current map can complete it.
3. Validate a destination from the native Portal list if travel is required.
4. Acquire a travel lock and suppress new automatic Rage activations.
5. Let any active Rage execution end naturally.
6. Wait through post-Rage protection.
7. Revalidate the quest, map, Portal button, cooldown, and world blockers.
8. Spawn the Portal and wait for entry.
9. Re-scan after arrival and scene stabilization.

## Portal Requirements

- Native Portal cooldown is zero.
- The native Portal button is visible, enabled, and interactable.
- No active Portal already exists.
- The target appears in `GetAvailableMaps(false)`.
- The target is a normal `Map`, not a Bonus, boss, village, or story map.
- AutoAdventurer never resets or bypasses Portal cooldown.

## Minimum Dimension Stay

After automatic arrival, the mod remains in that dimension for at least 1
minute by default before another automatic departure. This prevents repeated
movement among several valid maps.

## Map Events

Travel pauses for supported map-bound events including:

- Horde
- Extreme Coins
- Lucky Coins
- Gemstone Rush
- Frenzy
- Dual Randomness

The task lock is retained, while travel-related Rage suppression is released
so Rage can serve the event. Task and Portal conditions are re-evaluated after
the event.

Detection combines the game's `IsActive()` state, remaining time, and event
lifecycle callbacks. A briefly missing Unity object cannot immediately end an
event: the last timer and consecutive-miss confirmation protect travel.

## Random Boxes

Normal, Silver, Golden, and Special Random Boxes pause travel from the moment
their live object appears. After the box clears, the mod checks whether it
created an event, minigame entrance, or another world transition. Boxes remain
quiet in repeated debug logs; confirmed events log their type.

## Keys and Minigame Items

- A Chest Hunt Key stops new Rage refreshes.
- Ascending Heights and Grapple Run trigger items block conflicting Rage/travel
  preparation.
- Quest scanning freezes inside special scenes and resumes from fresh objects
  after returning.

## Travel Timeout

If arrival is not detected within 60 seconds, the Portal request is released
safely and the task can be retried. Possible causes include a missed Portal,
an interrupted scene, or a transition that did not complete. A valid task lock
is not discarded merely because the travel request timed out.

[Back to the Complete Manual](../MANUAL.md)

