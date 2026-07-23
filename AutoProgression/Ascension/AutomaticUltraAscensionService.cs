using System;
using System.Reflection;
using AutoProgression.Diagnostics;
using Il2Cpp;

namespace AutoProgression.Ascension;

internal sealed class AutomaticUltraAscensionService
{
    private const double RequiredAstralKeys = 24d;
    private static readonly MethodInfo ConfirmUltraAscensionMethod =
        typeof(AscensionManager).GetMethod(
        "_UltraAscendPopup_b__50_0",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private float nextCheckTime;
    private bool startedThisSession;

    internal bool Tick(float now)
    {
        if (!Plugin.Config.EnableAutomaticUltraAscension.Value ||
            startedThisSession || now < nextCheckTime)
            return false;

        nextCheckTime = now + GetCheckIntervalSeconds();
        AscensionManager manager = AscensionManager.instance;
        AscensionSkill ultraAscension = AscensionSkills.list?.UltraAscension;
        if (manager == null || ultraAscension == null ||
            ConfirmUltraAscensionMethod == null)
        {
            ProgressionLog.Debug(
                $"Automatic Ultra Ascension objects unavailable. " +
                $"Manager={manager != null}, Skill={ultraAscension != null}, " +
                $"ConfirmMethod={ConfirmUltraAscensionMethod != null}.");
            return false;
        }

        try
        {
            // Let the game own unlock and prerequisite rules across all
            // progression paths instead of duplicating them in this mod.
            if (!ultraAscension.IsActive()) return false;

            double astralKeys = manager.GetAstralKeys();
            ProgressionLog.Debug(
                $"Automatic Ultra Ascension status: RequirementsMet=True, " +
                $"AstralKeys={astralKeys:0.##}, Required={RequiredAstralKeys:0}.");
            if (double.IsNaN(astralKeys) || double.IsInfinity(astralKeys) ||
                astralKeys < RequiredAstralKeys)
                return false;

            // This is the native callback bound to the confirmation action in
            // UltraAscendPopup. Ascend(true) is not the Ultra Ascension entry
            // point and only produces a transient cutscene without resetting.
            ConfirmUltraAscensionMethod.Invoke(manager, Array.Empty<object>());
            startedThisSession = true;
            ProgressionLog.User(
                $"Automatic Ultra Ascension started with " +
                $"{astralKeys:0.##} Astral Keys available.");
            return true;
        }
        catch (Exception exception)
        {
            ProgressionLog.Exception("Automatic Ultra Ascension", exception);
            return false;
        }
    }

    private static float GetCheckIntervalSeconds() => Math.Max(
        1f,
        Configuration.AutoProgressionConfig.AutomaticAscensionCheckIntervalMinutes * 60f);
}
