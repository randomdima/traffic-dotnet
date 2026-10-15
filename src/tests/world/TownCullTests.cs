using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The cull a frame asks of the fleet's own places, which is kept for the frames between two ticks: a body put down
/// between them is where the next frame looks for it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
public class TownCullTests
{
    [Fact]
    public void ACarPutDownBetweenTwoTicksIsCulledWhereItNowStands()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(Towns.Fixture), config);
        var into = new int[world.Cars.Capacity];
        new SimLoop<TownWorld>(world, config).Advance(1);

        // The whole town is more cells of the world's grids than it has cars, so it is answered off the fleet's places.
        var townM = world.Plan.WorldSizeM;
        world.StandTheCarAt(0, townM * 0.5f, 0f);
        Assert.Contains(0, world.CarsIn(Vector2.Zero, townM, into).ToArray());

        world.StandTheCarAt(0, -townM, 0f);

        Assert.DoesNotContain(0, world.CarsIn(Vector2.Zero, townM, into).ToArray());
    }
}
