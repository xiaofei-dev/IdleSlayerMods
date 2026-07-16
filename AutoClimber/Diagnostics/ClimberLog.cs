namespace AutoClimber.Diagnostics;

internal static class ClimberLog
{
    internal static bool IsDeveloperMode =>
        AutoClimberPlugin.Config?.DebugMode?.Value == true;

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
