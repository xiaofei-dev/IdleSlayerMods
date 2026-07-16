using IdleSlayerMods.Common;
using MelonLoader;
using AutoProgression.Configuration;
using AutoProgression.Runtime;
using Plugin = AutoProgression.Plugin;

[assembly: MelonInfo(typeof(Plugin), AutoProgression.AutoProgressionInfo.PluginName, AutoProgression.AutoProgressionInfo.PluginVersion, AutoProgression.AutoProgressionInfo.PluginAuthor)]
[assembly: MelonAdditionalDependencies("IdleSlayerMods.Common")]

namespace AutoProgression;

public class Plugin : MelonMod
{
    internal static AutoProgressionConfig Config;
    internal static ModHelper ModHelperInstance;
    internal static readonly MelonLogger.Instance Logger = Melon<Plugin>.Logger;
    
    public override void OnInitializeMelon()
    {
        ModHelper.ModHelperMounted += SetModHelperInstance;
        Config = new(AutoProgressionInfo.PluginGuid);
        Logger.Msg(
            $"Plugin {AutoProgressionInfo.PluginGuid} v{AutoProgressionInfo.PluginVersion} " +
            $"(internal {AutoProgressionInfo.InternalVersion}) is loaded; " +
            $"configuration schema v{AutoProgressionConfig.CurrentConfigurationVersion}.");
    }

    private static void SetModHelperInstance(ModHelper instance)
    {
        ModHelperInstance = instance;
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Game") return;
        ModUtils.RegisterComponent<AutoProgressionRuntime>();
    }
}
