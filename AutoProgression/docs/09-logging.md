# Logging Reference

AutoProgression separates player-facing information from diagnostics.

## Always Visible

- runtime enabled and disabled messages
- Jewels of Soul purchases and remaining paid-bonus duration
- normal and Ultra Ascension starts
- manual Armory/egg opening and Crawler Eye purchase results
- Minion prestige, Silver Box claims, and Shards Necklace overflow crafting
- completed generated-quest filtering and selected Weekly results
- warnings and safely caught errors

## Debug Mode

When `Debug Mode` is enabled, logs include object availability, timers,
purchase summaries, quest-reroll state, and screen-guard transitions.
Each diagnostic is labeled with its subsystem, for example
`[Debug][Ascension]`, `[Debug][Craftables]`, `[Debug][Purchases]`,
`[Debug][Quests]`, or `[Debug][Runtime]`.
High-frequency activity is summarized: quest claims, timed craftables, eggs,
and skill purchases are emitted as periodic totals instead of one line per
action. Expected native lifecycle misses are logged once or rate-limited.

Safely caught exceptions produce one concise error line in every mode. The
full managed/native exception detail is emitted only when `Debug Mode` is
enabled.

Exact repeated Debug messages are emitted at most once every 10 seconds.
Warnings and errors use a 30-second duplicate window. When logging resumes,
the message includes the number of identical repeats that were suppressed.

MelonLoader RemoteAPI HTTP failures and messages explicitly attributed to
another mod are not AutoProgression failures. Native IL2CPP exceptions should
be evaluated using the service and stack trace named in the same entry.

The main log directory is:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\MelonLoader\Logs\
```

[Back to the Complete Manual](../MANUAL.md)
