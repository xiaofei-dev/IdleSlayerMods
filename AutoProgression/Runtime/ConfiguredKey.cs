using System;
using UnityEngine;

namespace AutoProgression.Runtime;

internal static class ConfiguredKey
{
    internal static bool TryResolve(string configuredValue, out KeyCode key)
    {
        string configured = configuredValue?.Trim();
        key = configured switch
        {
            "[" => KeyCode.LeftBracket,
            "]" => KeyCode.RightBracket,
            "\\" or "|" => KeyCode.Backslash,
            _ => KeyCode.None
        };

        return key != KeyCode.None ||
               (!string.IsNullOrEmpty(configured) &&
                Enum.TryParse(configured, true, out key) &&
                key != KeyCode.None);
    }
}
