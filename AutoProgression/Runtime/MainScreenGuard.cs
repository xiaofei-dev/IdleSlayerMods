using Il2Cpp;
using UnityEngine;

namespace AutoProgression.Runtime;

internal sealed class MainScreenGuard
{
    private float validSince = -1f;

    internal bool IsReady(float stableSeconds)
    {
        GameStates state = GameState.current;
        bool isSupportedMainScreen =
            state == GameStates.RunnerMode ||
            state == GameStates.RageMode;

        if (!isSupportedMainScreen)
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

    internal void Reset() => validSince = -1f;
}
