namespace AutoProgression.Diagnostics;

internal static class ProgressionLog
{
    internal static void Info(string message) => Plugin.Logger.Msg(message);

    internal static void Debug(string message)
    {
        if (Plugin.Config?.DebugMode.Value == true)
            Plugin.Logger.Msg($"[Debug] {message}");
    }

    internal static void Spending(string message) => Plugin.Logger.Msg(message);
}
