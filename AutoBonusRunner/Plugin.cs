using IdleSlayerMods.Common;
using HarmonyLib;
using MelonLoader;
using AutoBonusRunner.Configuration;
using AutoBonusRunner.Diagnostics;
using AutoBonusRunner.Runtime;
using UnityEngine;

[assembly: MelonInfo(typeof(AutoBonusRunner.Plugin), AutoBonusRunner.AutoBonusRunnerInfo.PluginName, AutoBonusRunner.AutoBonusRunnerInfo.PluginVersion, AutoBonusRunner.AutoBonusRunnerInfo.PluginAuthor)]
[assembly: MelonAdditionalDependencies("IdleSlayerMods.Common")]

namespace AutoBonusRunner;

public sealed class Plugin : MelonMod
{
    internal static AutoBonusRunnerConfig Config;
    internal static ModHelper ModHelperInstance;
    private static bool harmonyPatchesApplied;
    internal static readonly MelonLogger.Instance Logger = Melon<Plugin>.Logger;
    
    public override void OnInitializeMelon()
    {
        Application.runInBackground = true;
        ModHelper.ModHelperMounted += instance => ModHelperInstance = instance;
        WarnIfConfigurationIsMissing();
        Config = new(AutoBonusRunnerInfo.PluginGuid);
        BonusRunnerLog.InitializeSessionTrace();
        BonusRunnerLog.User(
            $"Plugin {AutoBonusRunnerInfo.PluginGuid} v{AutoBonusRunnerInfo.PluginVersion} " +
            $"(internal {AutoBonusRunnerInfo.InternalVersion}) loaded; " +
            $"configuration schema v{AutoBonusRunnerConfig.CurrentConfigurationVersion}. " +
            "Debug observation remains active when automatic control is disabled.");
        BonusRunnerLog.User(
            string.IsNullOrWhiteSpace(BonusRunnerLog.SessionTracePath)
                ? "Independent session trace could not be opened; MelonLoader log remains active."
                : $"Independent session trace: {BonusRunnerLog.SessionTracePath}");
    }

    private static void WarnIfConfigurationIsMissing()
    {
        string path = System.IO.Path.Combine(
            MelonLoader.Utils.MelonEnvironment.UserDataDirectory,
            $"{AutoBonusRunnerInfo.PluginGuid}.cfg");
        if (!System.IO.File.Exists(path))
            BonusRunnerLog.User(
                $"Configuration file was not found at '{path}'. Default settings will be used and a new file will be created when the game saves preferences. Verify that your Mod Manager edits this exact file.");
    }

    public override void OnDeinitializeMelon()
    {
        BonusRunnerLog.ShutdownSessionTrace();
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Game") return;
        Application.runInBackground = true;
        ModUtils.RegisterComponent<AutoBonusRunnerRuntime>();
        if (!harmonyPatchesApplied)
        {
            HarmonyInstance.PatchAll();
            harmonyPatchesApplied = true;
            BonusRunnerLog.Debug(
                "Variable-jump hold patches applied to PlayerMovement.",
                "Control");
        }
    }
}
