# Character and Quest Automation Mod Handoff

This document defines the starting scope for the next independent Idle Slayer MelonLoader mod. Read it before implementation and use the existing `AutoProgression` and `AutoClimber` projects as architecture and API references only.

## Project Boundary

Create a new project, namespace, DLL, manifest, configuration file, runtime toggle, and README. Do not add these features to AutoProgression.

The intended ownership split is:

- **AutoProgression:** economy and progression maintenance, including quest claiming/regeneration, purchases, craftables, Ascension, and egg opening.
- **New character mod:** character actions, quest target selection, dimension selection, Portal travel, Rage termination, skill use, and later movement/combat behavior.
- **AutoClimber:** Ascending Heights traversal only.

The new mod must not share runtime state with another mod or modify another mod's source files.

## First Major Feature: Quest-Guided Dimension Travel

The first implementation should select a kill quest that the current account can actually progress, determine its dimension, and travel there when appropriate.

### Required Configuration

Start with a compact configuration surface:

```ini
[Quest Automation]
Enabled = true
Quest Priority = Daily
Portal Maximum Remaining Cooldown Minutes = 0
End Rage Before Portal Travel = true
```

`Quest Priority` initially supports:

- `Daily` (default)
- `NonDaily`

The priority is a preference, not an exclusive filter. If the preferred group has no executable monster quest, scan the other group.

`Portal Maximum Remaining Cooldown Minutes = 0` means travel only when the Portal is fully ready. The first test version should use zero. Confirm the actual Portal API and game-side readiness condition before allowing nonzero values.

### Quest Selection Rules

1. Read all active, incomplete quests.
2. Partition them into Daily and non-Daily groups.
3. Scan the configured priority group first.
4. Consider only monster-kill objectives in the first version.
5. If no executable monster quest exists in the preferred group, scan the other group.
6. Ignore completed, claimable, non-combat, unsupported, or unresolvable objectives.
7. Prefer a target that can be progressed in the current dimension before requesting Portal travel.
8. If one monster can advance multiple quests, prefer it. Add a scoring model only after the basic mapping is verified.

## Unlock and Evolution Validation

Never assume that every quest target, monster evolution, or dimension is available.

Before choosing a target, verify all of the following through game-owned state:

- The target dimension is unlocked and currently reachable.
- The base monster is unlocked in that dimension.
- The exact evolved form required by the quest is unlocked and can currently spawn.
- Required upgrades, evolutions, or account progression conditions are satisfied.
- The Portal can actually select and enter the target dimension.

Skip the candidate if any required object is missing or any condition cannot be proven. Do not travel based only on localized quest text. Prefer stable game object references, internal identifiers, and native unlock predicates.

Build the monster-to-dimension mapping dynamically from game data when possible. If a static compatibility table becomes necessary, isolate it in its own file and keep identifiers/configuration separate from decision logic.

## Portal Travel Rules

- Stay in the current dimension when it already contains an executable target.
- Travel only when another reachable dimension has a selected target.
- Check the native Portal readiness state immediately before travel.
- Respect `Portal Maximum Remaining Cooldown Minutes`.
- Do not repeatedly request the same Portal action during a transition.
- Add a transition lock and wait until normal gameplay in the destination is stable before resuming decisions.
- Reset cached game objects, target decisions, and timers after a scene/dimension transition.

AutoProgression currently has a configurable `Reset Portal Cooldown` feature. Decide which mod owns Portal cooldown behavior before both mods are used together. The character mod should not silently fight AutoProgression over the same field.

## Rage Handling

If Portal travel is required while Rage is executing:

1. Confirm the authoritative `RageModeManager` instance and execution state.
2. If `End Rage Before Portal Travel` is enabled, terminate Rage through a verified native game path.
3. Wait for Rage Mode to finish and normal runner operation to become stable.
4. Revalidate the quest, target dimension, Portal readiness, and unlock state.
5. Travel only after revalidation.

Rage termination belongs to the new character mod because it changes character behavior. Do not reuse the archived AutoRageStopper implementation without validating the current API.

## Runtime and Safety Requirements

- Use a project-specific global toggle key that does not conflict with AutoProgression `T` or AutoClimber's configurable default `Y`.
- Keep the runtime dormant outside explicitly supported game states.
- Use a stable-main-screen guard before character or Portal actions.
- Separate quest discovery, target resolution, scoring, Portal travel, Rage control, and diagnostics into independent services/files.
- Null-check all IL2CPP singletons, dynamic lists, quest targets, monsters, dimensions, and UI objects.
- Treat cached IL2CPP references as invalid after transitions.
- Catch failures at service boundaries so an automation error does not escape the managed `Update` trampoline.
- Log decisions at a useful rate; never log every frame.
- User logs should report meaningful actions such as the selected quest target, skipped unreachable targets, Rage termination, and dimension travel.
- Debug logs may include identifiers and validation details but must be throttled.

## Investigation Before Implementation

Inspect the current game assemblies and existing local mods to identify and verify:

- Quest base types and monster-kill quest subclasses.
- Quest target fields and progress requirements.
- Daily versus non-Daily classification.
- Monster, evolution, and spawn-unlock objects.
- Dimension definitions and unlock predicates.
- Portal destination selection, cooldown, readiness, and travel methods.
- The authoritative Rage termination method and state transition.
- Scene/dimension transition signals suitable for cache reset.

Do not begin with UI clicks if a stable game API exists. Use UI interaction only when no safe native method is available.

## Development Conventions

- All uploaded source, comments, manifests, and README files must be English.
- Use the project name as the namespace; do not use names such as `MyBehaviour`.
- Keep configuration parameters intended for real user choices, not temporary debugging details.
- Public version remains `1.0.0` before release.
- Track an independent internal development version and increment it for every functional change or fix.
- Track configuration schema separately and increment it only for configuration structure/migration changes.
- Build and verify after changes. Do not deploy or push automatically unless explicitly requested.

## Suggested First Conversation Prompt

> This is a new independent Idle Slayer MelonLoader character automation mod. Read `CHARACTER_MOD_HANDOFF.md` completely, then inspect the sibling AutoProgression and AutoClimber projects as architecture/API references. Do not modify the existing mods. First confirm the project name, toggle key, supported GameStates, quest/monster/dimension APIs, and the exact boundary of the first quest-guided travel feature before implementation.
