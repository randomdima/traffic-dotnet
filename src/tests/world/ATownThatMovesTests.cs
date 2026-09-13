using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>The town moves.</b> A town that lays a perfect road fabric nothing drives over has not been shown to
/// have laid one — every dynamic reading this build takes is vacuous without traffic, and each of them
/// reports an absence the same way it reports a fault.
/// </summary>
/// <remarks>
/// <para>
/// <b>It guards a chain rather than a construction</b>, so it states the end of the chain and not any of the
/// links. Nothing else stands a car up (the roster is the plan's spawns); a car is stood on a lane by
/// <c>SpawnStage</c> because there is no bay to stand one in; and it is driven by the rule that drives a map
/// with nowhere to be on it, because there is no bay to be sent to either. Break any one of those and the
/// town has a road network and no traffic — which looks right in a picture and passes every static test.
/// </para>
/// <para>
/// <b>Asked of the city and not of the fixture</b>, because what is being checked is that a whole town's
/// worth of roads carries a whole town's worth of cars.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class ATownThatMovesTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>A minute of town time, which is long enough for a car to be somewhere other than where it was put.</summary>
    static int AMinute => (int)MathF.Ceiling(60f / Config.TickSeconds);

    [Fact]
    public void AGeneratedCityStandsItsCarsOnLanesAndTheyAreDrivingAMinuteLater()
    {
        var plan = Towns.Of(Towns.City);
        Assert.True(plan.Spawns.Count > 0, "the generator stood nothing up at all");

        using var world = new TownWorld(plan, Config);
        Assert.True(world.Cars.Count > 0, "the town stood no car up");

        var stoodAtM = new Vector2[world.Cars.Count];
        for (var car = 0; car < world.Cars.Count; car++) stoodAtM[car] = world.Cars.PositionM[car];

        new SimLoop<TownWorld>(world, Config).Advance(AMinute);

        var moved = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            // A car length, so a body settling into its own pose is not a car that went somewhere.
            if (Vector2.Distance(world.Cars.PositionM[car], stoodAtM[car]) > Config.Car.LengthM) moved++;
        }

        Assert.True(moved > 0, $"not one of {world.Cars.Count} cars moved a length in a minute of town time");
    }

    /// <summary>
    /// <b>And what stands over nothing is inert rather than broken.</b> Parking, the crossings, the signals
    /// and the buildings are all coming back, so their slices are kept standing over a town with none of
    /// their subject on it — which is a state nothing exercises unless it is exercised on purpose.
    /// </summary>
    /// <remarks>
    /// <b>One case for the lot of them</b>, because what is being checked is that an empty subject is a
    /// supported state and not that any one type works: a registry with no bay, a roster with nobody in it
    /// and a crossing table with nothing on it are stood up, ticked and read, and nothing raises.
    /// </remarks>
    [Fact]
    public void StandingTheEmptySlicesOverAGeneratedTownRaisesNothing()
    {
        var plan = Towns.Of(Towns.City);

        Assert.Equal(0, plan.ParkingLots.SpaceCount);
        Assert.Equal(0, plan.Buildings.Count);
        Assert.Equal(0, plan.Crosswalks.Count);

        using var world = new TownWorld(plan, Config);
        new SimLoop<TownWorld>(world, Config).Advance(AMinute);

        // Every one of them answered, over a town that has none of what it is about.
        Assert.Equal(0, world.Parking.BayCount);
        Assert.Equal(0, world.People.Count);
        Assert.Equal(0, world.Signals.CrossingCount);
        Assert.Equal(0, world.Walking.TurnCount);
    }
}
