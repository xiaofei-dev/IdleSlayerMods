using IdleSlayerMods.Common;
using MelonLoader;
using AutoAdventurer.Configuration;
using AutoAdventurer.Runtime;

[assembly: MelonInfo(typeof(AutoAdventurer.Plugin), AutoAdventurer.AutoAdventurerInfo.PluginName, AutoAdventurer.AutoAdventurerInfo.PluginVersion, AutoAdventurer.AutoAdventurerInfo.PluginAuthor)]
[assembly: MelonAdditionalDependencies("IdleSlayerMods.Common")]

namespace AutoAdventurer;

public class Plugin : MelonMod
{
    internal static AutoAdventurerConfig Config;
    internal static ModHelper ModHelperInstance;
    internal static readonly MelonLogger.Instance Logger = Melon<Plugin>.Logger;

    public override void OnInitializeMelon()
    {
        ModHelper.ModHelperMounted += instance => ModHelperInstance = instance;
        HarmonyInstance.PatchAll();
        Config = new(AutoAdventurerInfo.PluginGuid);
        Logger.Msg(
            $"Plugin {AutoAdventurerInfo.PluginGuid} v{AutoAdventurerInfo.PluginVersion} " +
            $"(internal {AutoAdventurerInfo.InternalVersion}) loaded; " +
            $"configuration schema v{AutoAdventurerConfig.CurrentConfigurationVersion}.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Game") return;
        ModUtils.RegisterComponent<AutoAdventurerRuntime>();
    }
}
