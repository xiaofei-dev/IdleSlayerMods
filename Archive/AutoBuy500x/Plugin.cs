using IdleSlayerMods.Common;
using MelonLoader;
using MyPluginInfo = AutoBuy500x.MyPluginInfo;
using Plugin = AutoBuy500x.Plugin;

[assembly: MelonInfo(typeof(Plugin), MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION, MyPluginInfo.PLUGIN_AUTHOR)]
[assembly: MelonAdditionalDependencies("IdleSlayerMods.Common")]

namespace AutoBuy500x;

public class Plugin : MelonMod
{
    internal static ConfigFile Config;
    internal static ModHelper ModHelperInstance;
    internal static readonly MelonLogger.Instance Logger = Melon<Plugin>.Logger;

    public override void OnInitializeMelon()
    {
        ModHelper.ModHelperMounted += SetModHelperInstance;

        Config = new(MyPluginInfo.PLUGIN_GUID);
        Logger.Msg($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private static void SetModHelperInstance(ModHelper instance)
    {
        ModHelperInstance = instance;
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Game")
        {
            return;
        }

        ModUtils.RegisterComponent<MyBehaviour>();
    }
}
