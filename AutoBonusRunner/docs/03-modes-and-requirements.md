# Modes and Sphere Requirements

## Auto

Auto is the default mode.

- Ordinary Bonus Stages use an effective requirement of 1 Bonus Sphere.
- Spirit Boost Bonus Stages preserve the game's native requirement.

After the requirement is met, terrain routing continues until a real reward
object is confirmed. Reducing the requirement does not intentionally stop the
character at the current platform.

## Manual

Manual preserves the game's native sphere requirement in ordinary and Spirit
Boost runs. Choose it for full-route testing, maximum collection attempts, or
normal Bonus Stage rules in every run.

The name describes requirement behavior; automatic movement remains enabled
unless it is toggled off with `U`.

## Skip

Skip uses an effective requirement of 1 Bonus Sphere in both ordinary and
Spirit Boost runs.

Skip changes only the section requirement. AutoBonusRunner still uses the
normal route and reward controllers rather than teleporting or forcing the
stage to end.

## Requirement Safety

The effective value is selected by a postfix on the game's native requirement
method. The runtime verifies that exactly one owned patch is present.

If that verification fails, Auto and Skip fail safely to the native
requirement. Missing data does not invent a requirement.

## Section Boundaries

Requirement choice is made for each active Bonus Stage section. Section,
respawn, and retry transitions reset route calibration without discarding
stable native physics observations that remain valid.

[Back to the Complete Manual](../MANUAL.md)

