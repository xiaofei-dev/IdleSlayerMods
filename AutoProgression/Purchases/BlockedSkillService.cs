using AutoProgression.Diagnostics;
using Il2Cpp;

namespace AutoProgression.Purchases;

internal sealed class BlockedSkillService
{
    private bool logged;

    internal void Tick()
    {
        if (!Plugin.Config.DisableVerticalMagnetSkills.Value) return;

        Upgrades upgrades = Upgrades.list;
        Upgrade normal = upgrades?.RandomBoxVerticalMagnet;
        Upgrade special = upgrades?.SpecialRandomBoxVerticalMagnet;
        if (normal == null || special == null) return;

        normal.disabled = true;
        special.disabled = true;

        if (logged) return;
        ProgressionLog.Debug("Vertical Random Box magnet skills disabled for automatic and manual purchase.");
        logged = true;
    }

    internal void Reset() => logged = false;
}
