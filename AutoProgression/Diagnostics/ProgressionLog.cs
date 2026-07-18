namespace AutoProgression.Diagnostics;

internal static class ProgressionLog
{
    internal static void User(string message) => Plugin.Logger.Msg(message);
    internal static void Warning(string message) => Plugin.Logger.Warning(message);
    internal static void Error(string message) => Plugin.Logger.Error(message);

    internal static void Debug(string message, string category = null)
    {
        if (Plugin.Config?.DebugMode.Value == true)
        {
            string prefix = string.IsNullOrWhiteSpace(category)
                ? "[Debug]"
                : $"[Debug][{category}]";
            Plugin.Logger.Msg($"{prefix} {message}");
        }
    }
}
