namespace TrafficSimulation.World.Town;

/// <summary><b>The claims handed to the instruments.</b> Nothing that decides anything reads them.</summary>
internal sealed partial class TownWorld
{
    /// <summary>For the instruments: the hold this car's claim laid this rebuild, or none.</summary>
    public int PlanHoldOf(int car) => _ground.PlanHold[car];
}
