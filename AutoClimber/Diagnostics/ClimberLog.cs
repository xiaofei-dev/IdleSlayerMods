namespace AutoClimber.Diagnostics;

internal static class ClimberLog
{
    internal static bool IsQuickSkipModeEnabled =>
        AutoClimberPlugin.Config?.SkipMinigame?.Value == true;

    internal static bool IsDeveloperMode =>
        !IsQuickSkipModeEnabled &&
        AutoClimberPlugin.Config?.DebugMode?.Value == true;

    internal static bool IsEnemyTargetingEnabled =>
        !IsQuickSkipModeEnabled &&
        AutoClimberPlugin.Config?.TargetEnemies?.Value == true;

    internal static void User(string message)
    {
        AutoClimberPlugin.Logger.Msg(message);
    }

    internal static void Developer(string message)
    {
        if (IsDeveloperMode)
        {
            AutoClimberPlugin.Logger.Msg("[Debug] " + message);
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
