namespace AutoAdventurer.Diagnostics;

internal static class AdventurerLog
{
    internal static void User(string message) => Plugin.Logger.Msg(message);
    internal static void Warning(string message) => Plugin.Logger.Warning(message);
    internal static void Error(string message) => Plugin.Logger.Error(message);

    internal static void Debug(string message)
    {
        if (Plugin.Config?.DebugMode.Value == true)
            Plugin.Logger.Msg($"[Debug] {message}");
    }

    internal static void QuestDebug(string message)
    {
        if (Plugin.Config?.QuestAutomationDebugMode.Value == true)
            Plugin.Logger.Msg($"[Quest Debug] {message}");
    }
}
