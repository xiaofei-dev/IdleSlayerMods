# Logging Reference

AutoProgression separates player-facing information from diagnostics.

## Always Visible

- runtime enabled and disabled messages
- Jewels of Soul purchases and remaining paid-bonus duration
- notable resource actions such as Shards Necklace crafting
- generated quest sets and selected Weekly results
- warnings and safely caught errors

## Debug Mode

When `Debug Mode` is enabled, logs include object availability, timers,
craftable duration, purchase summaries, quest-reroll state, and screen-guard
transitions. Repetitive status messages are rate-limited where practical.

MelonLoader RemoteAPI HTTP failures and messages explicitly attributed to
another mod are not AutoProgression failures. Native IL2CPP exceptions should
be evaluated using the service and stack trace named in the same entry.

The main log directory is:

```text
%LOCALAPPDATA%\IdleSlayerModManager\ModLoader\MelonLoader\Logs\
```

[Back to the Complete Manual](../MANUAL.md)
