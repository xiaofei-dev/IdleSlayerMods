using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AutoBonusRunner.Diagnostics;

internal static class BonusRunnerLog
{
    private const long DebugDuplicateWindowMilliseconds = 10_000;
    private const long ImportantDuplicateWindowMilliseconds = 30_000;
    private const int MaximumTrackedMessages = 512;
    private static readonly object TraceLock = new();
    private static readonly object SuppressionLock = new();
    private static readonly Dictionary<string, RepeatedMessageState> Repeated =
        new(StringComparer.Ordinal);
    private const int TraceBufferBytes = 64 * 1024;
    private static StreamWriter traceWriter;

    internal static bool IsDebugMode =>
        Plugin.Config?.DebugMode?.Value == true;

    internal static string SessionTracePath { get; private set; } = string.Empty;

    internal static void InitializeSessionTrace()
    {
        try
        {
            string directory = Path.Combine(
                MelonLoader.Utils.MelonEnvironment.UserDataDirectory,
                "AutoBonusRunner",
                "Logs");
            Directory.CreateDirectory(directory);
            SessionTracePath = Path.Combine(
                directory,
                $"AutoBonusRunner-{DateTime.Now:yyyyMMdd-HHmmss-fff}-" +
                $"{AutoBonusRunnerInfo.InternalVersion}.log");
            traceWriter = new StreamWriter(
                new FileStream(
                    SessionTracePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    TraceBufferBytes,
                    FileOptions.SequentialScan),
                new System.Text.UTF8Encoding(false),
                TraceBufferBytes);
            // Make the independent trace observable while the game is still
            // running. StreamWriter.AutoFlush drains only to the operating
            // system's file cache; it does not force a physical disk flush for
            // every line, so complete diagnostics remain available without
            // introducing synchronous disk-write stalls in FixedUpdate.
            traceWriter.AutoFlush = true;
            Trace(
                "SESSION",
                $"Public={AutoBonusRunnerInfo.PluginVersion}, " +
                $"Internal={AutoBonusRunnerInfo.InternalVersion}, " +
                $"Schema={Configuration.AutoBonusRunnerConfig.CurrentConfigurationVersion}, " +
                $"Started={DateTime.Now:O}",
                true);
        }
        catch
        {
            traceWriter = null;
            SessionTracePath = string.Empty;
        }
    }

    internal static void ShutdownSessionTrace()
    {
        lock (TraceLock)
        {
            try
            {
                traceWriter?.Flush();
                traceWriter?.Dispose();
            }
            catch
            {
                // Diagnostics must never break gameplay shutdown.
            }
            finally
            {
                traceWriter = null;
            }
        }
    }

    internal static void User(string message)
    {
        // The independent trace is authoritative. Write it before invoking
        // MelonLoader so a logger exception or re-entrant callback cannot
        // suppress the dedicated session record.
        Trace("INFO", message, true);
        Plugin.Logger.Msg(message);
    }

    internal static void Debug(
        string message,
        string category = null,
        [CallerFilePath] string callerFilePath = null)
    {
        if (!IsDebugMode)
            return;

        category = string.IsNullOrWhiteSpace(category)
            ? InferCategory(callerFilePath)
            : category;
        string prefix = $"[Debug][{category}]";
        string key = $"Debug|{category}|{message}";
        if (!TryAcquire(
                key,
                DebugDuplicateWindowMilliseconds,
                out int suppressed))
        {
            return;
        }

        string formatted =
            $"{prefix} {AppendSuppressed(message, suppressed)}";
        // Keep the dedicated trace and MelonLoader's Latest.log equivalent.
        // The independent trace goes first and AutoFlush makes it readable
        // during a live game. V0.75 called MelonLoader first; the observed
        // session consequently remained at its single startup line even while
        // MelonLoader received the debug stream.
        Trace("DEBUG", formatted);
        Plugin.Logger.Msg(formatted);
    }

    internal static void Warning(string message)
    {
        WriteImportant(
            "Warning",
            "WARNING",
            message,
            Plugin.Logger.Warning);
    }

    internal static void Error(string message)
    {
        WriteImportant(
            "Error",
            "ERROR",
            message,
            Plugin.Logger.Error);
    }

    internal static void Exception(
        string operation,
        Exception exception)
    {
        string type =
            exception?.GetType().Name ?? "UnknownException";
        string detail = exception?.Message;
        Error(
            string.IsNullOrWhiteSpace(detail)
                ? $"{operation} failed safely ({type})."
                : $"{operation} failed safely ({type}: " +
                  $"{SingleLine(detail)}).");
        Debug(
            exception?.ToString() ?? type,
            "Exception");
    }

    private static void WriteImportant(
        string keyLevel,
        string traceLevel,
        string message,
        Action<string> writer)
    {
        string key = $"{keyLevel}|{message}";
        if (!TryAcquire(
                key,
                ImportantDuplicateWindowMilliseconds,
                out int suppressed))
        {
            return;
        }

        string formatted =
            AppendSuppressed(message, suppressed);
        Trace(traceLevel, formatted, true);
        writer(formatted);
    }

    private static bool TryAcquire(
        string key,
        long windowMilliseconds,
        out int suppressed)
    {
        long now = Environment.TickCount64;
        lock (SuppressionLock)
        {
            if (Repeated.TryGetValue(
                    key,
                    out RepeatedMessageState state))
            {
                if (now - state.LastWrittenAt <
                    windowMilliseconds)
                {
                    state.Suppressed++;
                    Repeated[key] = state;
                    suppressed = 0;
                    return false;
                }

                suppressed = state.Suppressed;
                Repeated[key] =
                    new RepeatedMessageState(now, 0);
                return true;
            }

            if (Repeated.Count >= MaximumTrackedMessages)
                Repeated.Clear();

            Repeated[key] =
                new RepeatedMessageState(now, 0);
            suppressed = 0;
            return true;
        }
    }

    private static string InferCategory(
        string callerFilePath)
    {
        if (string.IsNullOrWhiteSpace(callerFilePath))
            return "General";

        string directory = Path.GetFileName(
            Path.GetDirectoryName(callerFilePath));
        return string.IsNullOrWhiteSpace(directory) ||
               string.Equals(
                   directory,
                   "AutoBonusRunner",
                   StringComparison.OrdinalIgnoreCase)
            ? "General"
            : directory;
    }

    private static string AppendSuppressed(
        string message,
        int suppressed) =>
        suppressed > 0
            ? $"{message} (suppressed {suppressed} identical repeat(s))"
            : message;

    private static string SingleLine(string value) =>
        value.Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

    private static void Trace(string level, string message, bool flush = false)
    {
        lock (TraceLock)
        {
            if (traceWriter == null)
                return;
            try
            {
                traceWriter.WriteLine(
                    $"[{DateTime.Now:O}] [{level}] {message}");
                if (flush)
                    traceWriter.Flush();
            }
            catch
            {
                // A logging failure must not alter the control loop.
            }
        }
    }

    private struct RepeatedMessageState(
        long lastWrittenAt,
        int suppressed)
    {
        internal long LastWrittenAt = lastWrittenAt;
        internal int Suppressed = suppressed;
    }
}
