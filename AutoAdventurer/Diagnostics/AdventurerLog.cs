namespace AutoAdventurer.Diagnostics;

internal static class AdventurerLog
{
    internal static void User(string message) => Plugin.Logger.Msg(message);
    internal static void Warning(string message) => Plugin.Logger.Warning(message);
    internal static void Error(string message) => Plugin.Logger.Error(message);

    internal static void Debug(string message, string category = null)
    {
        if (Plugin.Config?.DebugMode.Value == true)
            WriteDebug(message, category);
    }

    internal static void QuestDebug(string message)
    {
        if (Plugin.Config?.DebugMode.Value == true)
            WriteDebug(message, "Quest");
    }

    private static void WriteDebug(string message, string category)
    {
        string prefix = string.IsNullOrWhiteSpace(category)
            ? "[Debug]"
            : $"[Debug][{category}]";
        Plugin.Logger.Msg($"{prefix} {message}");
    }
}
