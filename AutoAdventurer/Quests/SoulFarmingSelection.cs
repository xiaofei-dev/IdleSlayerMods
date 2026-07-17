using Il2Cpp;

namespace AutoAdventurer.Quests;

internal sealed class SoulFarmingSelection
{
    internal Map Map { get; init; }
    internal double SoulScore { get; init; }
    internal string CurrentMapId { get; init; } = string.Empty;
    internal double CurrentSoulScore { get; init; }

    internal string MapId => Map?.name ?? "UnknownMap";
}
