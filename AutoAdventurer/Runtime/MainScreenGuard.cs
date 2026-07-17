using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Runtime;

internal sealed class MainScreenGuard
{
    private readonly bool blockMainScreenMenus;

    private static readonly string[] MenuHolderPaths =
    {
        "UIManager/Menu Holder",
        "UIManager/MenuHolder",
        "Menu Holder",
        "MenuHolder"
    };

    private float validSince = -1f;
    private float nextMenuCheckTime;
    private bool menuOpenCached;

    internal string LastBlockReason { get; private set; } = "Not evaluated";

    internal MainScreenGuard(bool blockMainScreenMenus = true)
    {
        this.blockMainScreenMenus = blockMainScreenMenus;
    }

    internal bool IsReady(float stableSeconds)
    {
        GameStates state = GameState.current;
        bool supported = state == GameStates.RunnerMode ||
                         state == GameStates.RageMode;

        if (blockMainScreenMenus && Time.unscaledTime >= nextMenuCheckTime)
        {
            nextMenuCheckTime = Time.unscaledTime + 0.1f;
            menuOpenCached = IsMenuHolderActive();
        }

        if (!supported)
        {
            validSince = -1f;
            LastBlockReason = $"unsupported GameState={state}";
            return false;
        }

        if (blockMainScreenMenus && (menuOpenCached || IsGameMenuVisible()))
        {
            validSince = -1f;
            LastBlockReason = $"main-screen menu visible; GameState={state}";
            return false;
        }

        if (IsPopupSliderVisible())
        {
            validSince = -1f;
            LastBlockReason = $"bonus-start slider visible; GameState={state}";
            return false;
        }

        if (!IsRunnerMapStable())
        {
            validSince = -1f;
            MapController maps = MapController.instance;
            LastBlockReason = maps == null
                ? $"MapController unavailable; GameState={state}"
                : $"map unstable; initialized={maps.initialized}; changingMap={maps.changingMap}; GameState={state}";
            return false;
        }

        if (validSince < 0f)
        {
            validSince = Time.unscaledTime;
            LastBlockReason = $"waiting for scene stability; GameState={state}";
            return false;
        }

        bool ready = Time.unscaledTime - validSince >= Mathf.Max(0f, stableSeconds);
        LastBlockReason = ready
            ? string.Empty
            : $"waiting for scene stability; GameState={state}";
        return ready;
    }

    internal void Reset()
    {
        validSince = -1f;
        nextMenuCheckTime = 0f;
        menuOpenCached = false;
        LastBlockReason = "Reset";
    }

    private static bool IsMenuHolderActive()
    {
        foreach (string path in MenuHolderPaths)
        {
            GameObject holder = GameObject.Find(path);
            if (holder != null && holder.activeInHierarchy)
                return true;
        }

        return false;
    }

    private static bool IsPopupSliderVisible()
    {
        PopupSlider popup = PopupSlider.instance;
        return popup != null && popup.IsVisible();
    }

    private static bool IsGameMenuVisible()
    {
        UIManager ui = UIManager.instance;
        if (ui == null) return true;

        return ui.ShopVisible() || ui.AscensionVisible() || ui.BagVisible();
    }

    private static bool IsRunnerMapStable()
    {
        MapController maps = MapController.instance;
        return maps != null && maps.initialized && !maps.changingMap;
    }
}
