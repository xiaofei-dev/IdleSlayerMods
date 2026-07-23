using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace AutoClimber.Diagnostics;

internal static class ClimberLog
{
    private const long DebugDuplicateWindowMilliseconds = 10_000;
    private const long ImportantDuplicateWindowMilliseconds = 30_000;
    private const int MaximumTrackedMessages = 512;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, RepeatedMessageState> Repeated =
        new(StringComparer.Ordinal);

    internal static bool IsQuickSkipModeEnabled =>
        AutoClimberQuestMode.IsQuickSkipActive;

    internal static bool IsDebugMode =>
        !IsQuickSkipModeEnabled &&
        AutoClimberPlugin.Config?.DebugMode?.Value == true;

    internal static bool IsEnemyTargetingEnabled =>
        !IsQuickSkipModeEnabled &&
        AutoClimberPlugin.Config?.TargetEnemies?.Value == true;

    internal static void User(string message) =>
        AutoClimberPlugin.Logger.Msg(message);

    internal static void Warning(string message) =>
        WriteImportant("Warning", message, AutoClimberPlugin.Logger.Warning);

    internal static void Error(string message) =>
        WriteImportant("Error", message, AutoClimberPlugin.Logger.Error);

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

    internal static void Debug(
        string message,
        string category = null,
        [CallerFilePath] string callerFilePath = null)
    {
        if (!IsDebugMode) return;

        category ??= InferCategory(callerFilePath);
        string prefix = $"[Debug][{category}]";
        string key = $"Debug|{prefix}|{message}";
        if (!TryAcquire(
                key,
                DebugDuplicateWindowMilliseconds,
                out int suppressed))
            return;

        AutoClimberPlugin.Logger.Msg(
            $"{prefix} {AppendSuppressed(message, suppressed)}");
    }

    private static string InferCategory(string callerFilePath)
    {
        if (string.IsNullOrWhiteSpace(callerFilePath))
            return "General";

        string directory = Path.GetFileName(
            Path.GetDirectoryName(callerFilePath));
        return string.IsNullOrWhiteSpace(directory) ||
               string.Equals(
                   directory,
                   "AutoClimber",
                   StringComparison.OrdinalIgnoreCase)
            ? "General"
            : directory;
    }

    private static void WriteImportant(
        string level,
        string message,
        Action<string> writer)
    {
        string key = $"{level}|{message}";
        if (!TryAcquire(
                key,
                ImportantDuplicateWindowMilliseconds,
                out int suppressed))
            return;

        writer(AppendSuppressed(message, suppressed));
    }

    private static bool TryAcquire(
        string key,
        long windowMilliseconds,
        out int suppressed)
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
