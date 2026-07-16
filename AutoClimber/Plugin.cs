using IdleSlayerMods.Common;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using AutoClimber.Configuration;

[assembly: MelonInfo(typeof(AutoClimber.AutoClimberPlugin), AutoClimber.AutoClimberInfo.PluginName, AutoClimber.AutoClimberInfo.PluginVersion, AutoClimber.AutoClimberInfo.PluginAuthor)]
[assembly: MelonAdditionalDependencies("IdleSlayerMods.Common")]

namespace AutoClimber;

public sealed class AutoClimberPlugin : MelonMod
{
    internal static AutoClimberConfig Config;
    internal static readonly MelonLogger.Instance Logger =
        Melon<AutoClimberPlugin>.Logger;
    
    public override void OnInitializeMelon()
    {
        Application.runInBackground = true;
        Config = new(AutoClimberInfo.PluginGuid);
        HarmonyInstance.PatchAll();
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Game") return;
        Application.runInBackground = true;
        ModUtils.RegisterComponent<AutoClimberRuntime>();
    }
}
