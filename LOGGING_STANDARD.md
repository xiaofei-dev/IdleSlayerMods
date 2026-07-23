# Idle Slayer Mods Logging Standard

This document defines the shared logging standard for all mods in this
repository. New mods should follow it, and existing mods should use it when
their logging is revised.

The reference implementations are:

- `AutoProgression/Diagnostics/ProgressionLog.cs`
- `AutoAdventurer/Diagnostics/AdventurerLog.cs`

## 1. Log Levels

### User

User logs describe meaningful actions or state changes that a player may want
to see during normal play.

Use User logs for:

- enabling or disabling automation
- starting or completing a major action
- spending premium or limited resources
- changing dimensions, characters, or important game state
- completing a summarized batch of repeated actions

Do not use User logs for polling, readiness checks, object discovery, internal
timers, or expected retry conditions.

```text
AutoProgression enabled: hotkey=T; gameState=Game.
Quest portal opened: quest=Some Quest; targetMap=map_jungle.
Material purchased with Jewels of Soul: Titanium (50%).
```

### Warning

Warnings describe recoverable conditions that may need player attention.

Use Warning logs for:

- missing or invalid configuration
- unavailable required game objects after the normal startup period
- an action that could not complete and may be retried
- configuration conflicts or unsafe combinations

Warnings should not include full exception stack traces.

### Error

Errors describe safely caught failures in a mod operation.

Every caught exception that matters to normal operation must produce:

1. one concise, single-line Error in every mode
2. the complete exception and stack trace under `[Debug][Exception]`

Example:

```text
Quest Automation failed safely (NullReferenceException: manager was null).
[Debug][Exception] System.NullReferenceException: ...
```

Do not interpolate an exception directly into a normal Error message because
that exposes multiline stack traces in User mode.

### Debug

Debug logs explain internal decisions and diagnostics. They must only be
written when the mod's main `Debug Mode` preference is enabled.

Every Debug message must have a subsystem category:

```text
[Debug][Runtime] Waiting for the main gameplay scene.
[Debug][Quest] Quest selected: questId=12; targetMap=map_factory.
[Debug][Movement] Ability blocked: reason=cooldown; remainingSeconds=1.25.
```

Recommended common categories:

| Category | Intended content |
|---|---|
| `Config` | preference loading, parsing, and migration |
| `Runtime` | lifecycle, scene readiness, and global state |
| `Gameplay` | general gameplay automation |
| `Quest` / `Quests` | quest discovery, selection, and execution |
| `Movement` | movement, jump, Boost, and Wind Dash decisions |
| `Rage` | Rage Mode readiness and execution |
| `Purchases` | equipment, skill, and upgrade purchases |
| `Craftables` | temporary and permanent craftables |
| `Materials` | material availability and purchases |
| `Ascension` | normal and Ultra Ascension |
| `Exception` | complete exception details and stack traces |

Prefer a small stable set of categories. Do not create categories from dynamic
values.

## 2. Message Format

Write concise English sentences. A normal message should:

- start with the feature or action
- state the result before diagnostic details
- end with a period
- use semicolons between structured fields
- use invariant numeric formatting where locale could change the output

```text
Automatic Ultra Ascension started: astralKeys=12.
Quest travel waiting: reason=rageActive; targetMap=map_jungle.
Minion prestiged: minion=Skeleton; maxLevel=10; divinityPointsGained=4.
```

Avoid:

```text
Activated!!!
Something happened
Minion: Skeleton, Max Level=10, Divinity Points gained=4
```

## 3. Structured Fields

Use `camelCase=value` for diagnostic fields:

```text
questId=12
targetMap=map_jungle
gameState=Game
remainingSeconds=1.25
divinityPointsGained=4
```

Rules:

- use stable English field names
- do not put spaces inside field names
- use `true` and `false` for Boolean values
- use consistent units in names when needed, such as `remainingSeconds`
- use the same field name for the same concept across all mods
- separate fields with semicolons

Localized display names may be included for readability, but automation and
diagnosis should also log stable internal IDs when available.

## 4. Toggle Messages

Use `enabled` and `disabled` consistently.

```text
AutoProgression enabled: hotkey=T; gameState=Game.
Quest Automation disabled.
Automatic Rage enabled.
```

Avoid mixing `Activated`, `Deactivated`, `ON`, `OFF`, and excessive
punctuation.

## 5. Duplicate Suppression

Every mod logging wrapper must suppress exact repeated messages:

- Debug: 10-second window
- Warning and Error: 30-second window
- User: normally not globally suppressed
- maximum tracked message keys: 512

When an identical message is allowed again, append:

```text
(suppressed 8 identical repeat(s))
```

Suppression keys must include the log level and Debug category so unrelated
messages cannot suppress each other.

Messages containing rapidly changing numeric values are not exact duplicates.
High-frequency systems must additionally provide their own aggregation or
rate limiting instead of relying only on global duplicate suppression.

## 6. High-Frequency Activity

Do not write one line per frame, polling attempt, purchase, craft, claim, or
retry.

Use one of these patterns:

- log only when state changes
- log the first failure and then wait for recovery
- emit periodic totals
- rate-limit a diagnostic at the service level

```text
[Debug][Purchases] Skill purchase summary: purchased=14; skipped=3.
```

## 7. Configuration and Startup

Use the shared startup format:

```text
Plugin {guid} v{version} (internal {internalVersion}) loaded; configuration schema v{configurationVersion}.
```

A missing configuration file is a Warning:

```text
Configuration file was not found at '...'. Default settings will be used and a new file will be created when the game saves preferences. Verify that your Mod Manager edits this exact file.
```

Configuration migration details belong in `[Debug][Config]`. Only failures or
settings requiring player attention should appear as Warning logs.

## 8. Exception Helper

Each mod should expose a common helper with this behavior:

```csharp
internal static void Exception(string operation, Exception exception)
{
    string type = exception?.GetType().Name ?? "UnknownException";
    string detail = exception?.Message;

    Error(
        string.IsNullOrWhiteSpace(detail)
            ? $"{operation} failed safely ({type})."
            : $"{operation} failed safely ({type}: {SingleLine(detail)}).");

    Debug(exception?.ToString() ?? type, "Exception");
}

private static string SingleLine(string value) =>
    value.Replace("\r", " ").Replace("\n", " ").Trim();
```

Call it from catch blocks:

```csharp
catch (Exception exception)
{
    ModLog.Exception("Quest Automation", exception);
    ResetTransientState();
}
```

The operation name should be a short noun phrase. Do not include
`failed safely` in the operation argument because the helper adds it.

## 9. Logging Wrapper Requirements

Every mod should use one central logging wrapper. Feature code should not call
the MelonLogger instance directly.

The wrapper must provide:

```csharp
User(string message)
Warning(string message)
Error(string message)
Exception(string operation, Exception exception)
Debug(string message, string category)
```

It must also provide:

- Debug Mode gating
- Debug category prefixes
- single-line Error summaries
- full exception details in Debug only
- thread-safe duplicate suppression
- bounded duplicate-tracking storage

Subsystem helper methods such as `QuestDebug`, `RuntimeDebug`, and
`MovementDebug` are encouraged when they keep categories consistent.

## 10. Review Checklist

Before releasing a mod, verify:

- normal mode contains only useful player-facing activity
- Debug messages all have stable categories
- no caught exception is interpolated directly into User/Warning/Error output
- full stack traces only appear under `[Debug][Exception]`
- exact Debug duplicates are suppressed for 10 seconds
- exact Warning/Error duplicates are suppressed for 30 seconds
- high-frequency actions are summarized or rate-limited
- structured field names use camelCase
- fields are separated with semicolons
- toggle messages use `enabled` and `disabled`
- configuration-file absence uses Warning
- startup text follows the shared format
- documentation describes the categories and suppression behavior
- the project builds with zero errors and preferably zero warnings

