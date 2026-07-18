namespace AutoClimber.Diagnostics;

internal static class ClimberLog
{
    internal static bool IsQuickSkipModeEnabled =>
        AutoClimberQuestMode.IsQuickSkipActive;

    internal static bool IsDebugMode =>
        !IsQuickSkipModeEnabled &&
        AutoClimberPlugin.Config?.DebugMode?.Value == true;

    internal static bool IsEnemyTargetingEnabled =>
        !IsQuickSkipModeEnabled &&
        AutoClimberPlugin.Config?.TargetEnemies?.Value == true;

    internal static void User(string message)
    {
        AutoClimberPlugin.Logger.Msg(message);
    }

    internal static void Debug(string message, string category = null)
    {
        if (IsDebugMode)
        {
            string prefix = string.IsNullOrWhiteSpace(category)
                ? "[Debug]"
                : $"[Debug][{category}]";
            AutoClimberPlugin.Logger.Msg($"{prefix} {message}");
        }
    }

    internal static void Warning(string message)
    {
        AutoClimberPlugin.Logger.Warning(message);
    }

    internal static void Error(string message)
    {
        AutoClimberPlugin.Logger.Error(message);
    }
}
