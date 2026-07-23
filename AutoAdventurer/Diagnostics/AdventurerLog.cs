using System;
using System.Collections.Generic;

namespace AutoAdventurer.Diagnostics;

internal static class AdventurerLog
{
    private const long DebugDuplicateWindowMilliseconds = 10_000;
    private const long ImportantDuplicateWindowMilliseconds = 30_000;
    private const int MaximumTrackedMessages = 512;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, RepeatedMessageState> Repeated =
        new(StringComparer.Ordinal);

    internal static void User(string message) => Plugin.Logger.Msg(message);
    internal static void Warning(string message) =>
        WriteImportant("Warning", message, Plugin.Logger.Warning);
    internal static void Error(string message) =>
        WriteImportant("Error", message, Plugin.Logger.Error);

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

    internal static void Debug(string message, string category = null)
    {
        if (Plugin.Config?.DebugMode.Value == true)
            WriteDebug(message, category);
    }

    internal static void QuestDebug(string message)
        => Debug(message, "Quest");
    internal static void ElementDebug(string message)
        => Debug(message, "Element");
    internal static void SilverBoxDebug(string message)
        => Debug(message, "SilverBox");
    internal static void MovementDebug(string message)
        => Debug(message, "Movement");
    internal static void RageDebug(string message)
        => Debug(message, "Rage");
    internal static void BossDebug(string message)
        => Debug(message, "Boss");
    internal static void GameplayDebug(string message)
        => Debug(message, "Gameplay");
    internal static void RuntimeDebug(string message)
        => Debug(message, "Runtime");
    internal static void ConfigDebug(string message)
        => Debug(message, "Config");

    private static void WriteDebug(string message, string category)
    {
        string prefix = string.IsNullOrWhiteSpace(category)
            ? "[Debug]"
            : $"[Debug][{category}]";
        string key = $"Debug|{prefix}|{message}";
        if (!TryAcquire(key, DebugDuplicateWindowMilliseconds,
                out int suppressed)) return;
        Plugin.Logger.Msg($"{prefix} {AppendSuppressed(message, suppressed)}");
    }

    private static void WriteImportant(
        string level, string message, Action<string> writer)
    {
        string key = $"{level}|{message}";
        if (!TryAcquire(key, ImportantDuplicateWindowMilliseconds,
                out int suppressed)) return;
        writer(AppendSuppressed(message, suppressed));
    }

    private static bool TryAcquire(
        string key, long windowMilliseconds, out int suppressed)
    {
        long now = Environment.TickCount64;
        lock (Sync)
        {
            if (Repeated.TryGetValue(key, out RepeatedMessageState state))
            {
                if (now - state.LastWrittenAt < windowMilliseconds)
                {
                    state.Suppressed++;
                    Repeated[key] = state;
                    suppressed = 0;
                    return false;
                }

                suppressed = state.Suppressed;
                Repeated[key] = new RepeatedMessageState(now, 0);
                return true;
            }

            if (Repeated.Count >= MaximumTrackedMessages)
                Repeated.Clear();
            Repeated[key] = new RepeatedMessageState(now, 0);
            suppressed = 0;
            return true;
        }
    }

    private static string AppendSuppressed(string message, int suppressed) =>
        suppressed > 0
            ? $"{message} (suppressed {suppressed} identical repeat(s))"
            : message;

    private static string SingleLine(string value) =>
        value.Replace("\r", " ").Replace("\n", " ").Trim();

    private struct RepeatedMessageState(long lastWrittenAt, int suppressed)
    {
        internal long LastWrittenAt = lastWrittenAt;
        internal int Suppressed = suppressed;
    }
}
