namespace AutoBonusRunner.Diagnostics;

internal static class BonusRunnerLog
{
    private static readonly object TraceLock = new();
    private static StreamWriter traceWriter;
    private static int traceLinesSinceFlush;

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
                    FileShare.ReadWrite),
                new System.Text.UTF8Encoding(false));
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
                traceLinesSinceFlush = 0;
            }
        }
    }

    internal static void User(string message)
    {
        Plugin.Logger.Msg(message);
        Trace("INFO", message);
    }

    internal static void Debug(string message, string category = null)
    {
        if (!IsDebugMode) return;
        string prefix = string.IsNullOrWhiteSpace(category)
            ? "[Debug]"
            : $"[Debug][{category}]";
        Plugin.Logger.Msg($"{prefix} {message}");
        Trace("DEBUG", $"{prefix} {message}");
    }

    internal static void Warning(string message)
    {
        Plugin.Logger.Warning(message);
        Trace("WARNING", message, true);
    }

    internal static void Error(string message)
    {
        Plugin.Logger.Error(message);
        Trace("ERROR", message, true);
    }

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
                traceLinesSinceFlush++;
                if (flush || traceLinesSinceFlush >= 8)
                {
                    traceWriter.Flush();
                    traceLinesSinceFlush = 0;
                }
            }
            catch
            {
                // A logging failure must not alter the control loop.
            }
        }
    }
}
