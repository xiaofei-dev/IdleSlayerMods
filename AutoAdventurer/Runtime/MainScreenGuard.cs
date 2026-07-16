using Il2Cpp;
using UnityEngine;

namespace AutoAdventurer.Runtime;

internal sealed class MainScreenGuard
{
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

    internal bool IsReady(float stableSeconds)
    {
        GameStates state = GameState.current;
        bool supported = state == GameStates.RunnerMode ||
                         state == GameStates.RageMode;

        if (Time.unscaledTime >= nextMenuCheckTime)
        {
            nextMenuCheckTime = Time.unscaledTime + 0.1f;
            menuOpenCached = IsMenuHolderActive();
        }

        if (!supported || menuOpenCached)
        {
            validSince = -1f;
            return false;
        }

        if (validSince < 0f)
        {
            validSince = Time.unscaledTime;
            return false;
        }

        return Time.unscaledTime - validSince >= Mathf.Max(0f, stableSeconds);
    }

    internal void Reset()
    {
        validSince = -1f;
        nextMenuCheckTime = 0f;
        menuOpenCached = false;
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
}
