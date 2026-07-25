# Nexus Mod Page Standard

Use this standard for every mod page in the automation suite. The goal is a page that a new user can understand in under a minute.

## Writing Rules

- Lead with what the mod automates, not its development story.
- Keep the opening description to one sentence.
- Use six to eight core feature bullets. Group related features instead of listing every setting.
- Put the most common and recognizable features first.
- Describe optional or dangerous behavior separately from normal behavior.
- State clearly when a feature may spend premium currency, alter saves, or perform another irreversible action.
- Do not claim that the game is completely automated when uncovered modes or manual steps remain. Prefer “hands-off progression across most of the game.”
- Do not repeat `by Tashi` in the page text when the author name and banner already show it.
- Display the current version of the featured mod, every listed automation-suite mod, recommended companion mods, and version-sensitive requirements.
- Use full semantic versions such as `2.0.0` in lists and requirement entries. A shortened major version such as `2.0` may be used in the visual header when it matches the banner.
- Treat every displayed version as release data: verify and update all of them before publishing a new page or release.
- Avoid claims such as “replaces” unless compatibility and feature parity are guaranteed. Describe each mod by its role instead.
- Use the same names and capitalization as the game, configuration file, and mod manifest.
- Use short sentences, plain English, and consistent punctuation.
- Keep detailed option explanations in the configuration comments, User Guide, or Complete Manual.

## Standard Section Order

1. Header
2. One-sentence purpose
3. Core Features
4. Optional Spending or Safety Warning, when applicable
5. Compatibility, when known conflicts exist
6. Automation Suite or Companion Mods
7. Requirements
8. Installation
9. Default Keys, when applicable
10. Help and Source

Omit any section that does not apply. Do not add an empty section.

## BBCode Template

Copy this template into the mod's ignored `NexusPage.txt` and replace every value in braces.

```text
[center][size=6][b]{SHORT COVER TITLE}[/b][/size]
[size=4][b]{FEATURE 1} • {FEATURE 2} • {FEATURE 3}[/b][/size]
[size=3]{MOD NAME} {DISPLAY VERSION, FOR EXAMPLE 2.0}[/size][/center]

[center]{ONE-SENTENCE DESCRIPTION OF WHAT THE MOD AUTOMATES AND WHY IT IS USEFUL}[/center]

[size=5][b]Core Features[/b][/size]

[list]
[*]{PRIMARY FEATURE}
[*]{SECOND FEATURE}
[*]{THIRD FEATURE}
[*]{FOURTH FEATURE}
[*]{FIFTH FEATURE}
[*]{SIXTH FEATURE}
[/list]

{ONE SHORT SENTENCE EXPLAINING THE MAIN TOGGLE OR NORMAL WAY TO START THE MOD.}

[size=5][b]Optional Spending Features[/b][/size]

[color=#ff5555][b]{CLEAR RISK IN ONE SENTENCE}[/b][/color] {STATE WHICH OPTIONS CAUSE IT AND THAT THEY ARE DISABLED BY DEFAULT.}

[size=5][b]Compatibility[/b][/size]

[color=#ff5555][b]Do not use {MOD NAME} together with {INCOMPATIBLE MODS}.[/b][/color]

{EXPLAIN THE CONFLICT, WHICH MOD REPLACES THE OLD FUNCTION, AND WHETHER THE USER SHOULD DISABLE OR REMOVE THE INCOMPATIBLE MODS.}

[size=5][b]Complete the Automation Suite[/b][/size]

Combine these companion mods for hands-off progression across most of the game:

[list]
[*][b]AutoProgression {CURRENT VERSION}[/b] — Ascension, upgrades, Minions, quests, crafting, and account maintenance
[*][url={AUTOADVENTURER URL}][b]AutoAdventurer {CURRENT VERSION}[/b][/url] — running, jumping, combat, Rage, quest travel, events, and bosses
[*][url={AUTOCLIMBER URL}][b]AutoClimber {CURRENT VERSION}[/b][/url] — plays Ascending Heights
[*][url={AUTOBONUSRUNNER URL}][b]AutoBonusRunner {CURRENT VERSION}[/b][/url] — plays Bonus Stages instead of skipping them
[/list]

[size=5][b]Optional Companion Mods[/b][/size]

[list]
[*][url={COMPANION MOD URL}][b]{COMPANION MOD NAME} {CURRENT VERSION}[/b][/url] — {PURPOSE}
[/list]

[size=5][b]Requirements[/b][/size]

[list]
[*]Idle Slayer Mod Manager
[*]MelonLoader — tested with [b]{TESTED VERSION}[/b]
[*][url={CORE URL}][b]Idle Slayer Mods Core {MINIMUM VERSION}[/b][/url] or a compatible later version
[/list]

[size=5][b]Installation[/b][/size]

[list=1]
[*]Install and initialize Idle Slayer Mod Manager.
[*]Install Idle Slayer Mods Core.
[*]Import [b]{ZIP NAME}.zip[/b] without extracting it.
[*]Enable the mod and start the game once.
[*]Close the game and review [b]{CONFIG NAME}.cfg[/b] in the ModLoader UserData folder.
[*]Restart the game and {FINAL ACTIVATION STEP}.
[/list]

[size=5][b]Default Keys[/b][/size]

[list]
[*][b]{KEY}[/b] — {ACTION}
[/list]

[size=5][b]Help and Source[/b][/size]

The download includes {AVAILABLE DOCUMENTATION}. Configuration options are also explained directly in [b]{CONFIG NAME}.cfg[/b].

[url=https://github.com/xiaofei-dev/IdleSlayerMods][b]Source code, updates, and issue reporting on GitHub[/b][/url]
```

## Final Review Checklist

- The opening sentence identifies the mod without requiring the reader to know the suite.
- The first screen contains the purpose and most important features.
- No feature is promised more strongly than the implementation supports.
- Premium-currency and destructive options have a visible warning.
- Known incompatible or superseded mods are named clearly, with the required user action.
- Names, keys, file names, links, and requirements match the current release.
- The featured mod, suite mods, recommended mods, and version-sensitive requirements all show their current versions.
- Every displayed version has been checked against the files being published.
- The text does not duplicate the banner or author metadata.
- The page remains readable without opening the full manual.
- `NexusPage.txt` remains ignored by Git and is never committed.
