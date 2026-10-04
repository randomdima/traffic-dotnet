using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen.Traced;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A car stood on a bridge is on the bridge from its first step</b> (PHY-1a): the plan stands it on the bridge's
/// lane, so it is on the level above before it has taken that lane — and drawn over the deck rather than under it —
/// asked of one street carried over another (<see cref="TracedPlanTests.OverAStreet"/>), whose cars stand on both.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class LevelsInATownTests
{
    [Fact]
    public void ACarStoodOnABridgeIsOnTheLevelAboveAndOneStoodUnderItIsNot()
    {
        var plan = TracedPlanTests.OverAStreet();
        var crossingM = TracedPlanTests.OverAStreetCrossingM;
        using var world = new TownWorld(plan, SimConfig.Shipped());

        var onTheBridge = Enumerable.Range(0, world.Cars.Count)
            .ToLookup(car => MathF.Abs(world.Cars.PositionM[car].X - crossingM.X) < 1e-3f, car => world.Cars.Level[car]);
        Assert.Equal([CityPlan.RoadArrays.Over, CityPlan.RoadArrays.Over], onTheBridge[true]);
        Assert.Equal([CityPlan.RoadArrays.Ground, CityPlan.RoadArrays.Ground], onTheBridge[false]);
    }
}
