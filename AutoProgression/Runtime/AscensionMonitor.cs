using Il2Cpp;

namespace AutoProgression.Runtime;

internal sealed class AscensionMonitor
{
    private bool hasPreviousState;
    private bool previousFirstSkillBought;

    internal bool DetectFirstSkillReset()
    {
        Upgrade firstSkill = Upgrades.list?.RandomBox;
        if (firstSkill == null) return false;

        bool currentlyBought = firstSkill.bought;
        if (!hasPreviousState)
        {
            hasPreviousState = true;
            previousFirstSkillBought = currentlyBought;
            return false;
        }

        bool resetDetected = previousFirstSkillBought && !currentlyBought;
        previousFirstSkillBought = currentlyBought;
        return resetDetected;
    }

    internal void Reset()
    {
        hasPreviousState = false;
        previousFirstSkillBought = false;
    }
}
