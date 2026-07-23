using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace AutoProgression.Diagnostics;

internal static class ProgressionLog
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

    internal static void Debug(
        string message,
        string category = null,
        [CallerFilePath] string callerFilePath = null)
    {
        if (Plugin.Config?.DebugMode.Value == true)
        {
            category ??= InferCategory(callerFilePath);
            string prefix = string.IsNullOrWhiteSpace(category)
                ? "[Debug]"
                : $"[Debug][{category}]";
            string key = $"Debug|{prefix}|{message}";
            if (!TryAcquire(key, DebugDuplicateWindowMilliseconds,
                    out int suppressed)) return;
            Plugin.Logger.Msg(
                $"{prefix} {AppendSuppressed(message, suppressed)}");
        }
    }

    internal static void AscensionDebug(string message) =>
        Debug(message, "Ascension");
    internal static void ArmoryDebug(string message) =>
        Debug(message, "Armory");
    internal static void CasinoDebug(string message) =>
        Debug(message, "Casino");
    internal static void CraftablesDebug(string message) =>
        Debug(message, "Craftables");
    internal static void MaterialsDebug(string message) =>
        Debug(message, "Materials");
    internal static void MinionsDebug(string message) =>
        Debug(message, "Minions");
    internal static void PaidBonusesDebug(string message) =>
        Debug(message, "PaidBonuses");
    internal static void PurchasesDebug(string message) =>
        Debug(message, "Purchases");
    internal static void QuestsDebug(string message) =>
        Debug(message, "Quests");
    internal static void RuntimeDebug(string message) =>
        Debug(message, "Runtime");
    internal static void SilverBoxesDebug(string message) =>
        Debug(message, "SilverBoxes");

    private static string InferCategory(string callerFilePath)
    {
        if (string.IsNullOrWhiteSpace(callerFilePath))
            return "General";

        string directory = Path.GetFileName(
            Path.GetDirectoryName(callerFilePath));
        return string.IsNullOrWhiteSpace(directory) ||
               string.Equals(directory, "AutoProgression",
                   StringComparison.OrdinalIgnoreCase)
            ? "General"
            : directory;
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
