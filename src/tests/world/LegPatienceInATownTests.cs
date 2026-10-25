using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.World.Parking;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A leg that covers no ground ends</b> (CAR-15a), staged on the suite's own city: a car on its street with a wreck
/// put down in its lane in front of it, and nothing in this town to get it past one.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class LegPatienceInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// CAR-15a: <b>past its last reroute a leg gives its place up for another once, and past that it is over</b>. Given
    /// up for the next free bay at every patience, a car stuck in a jam would be aimed at bay after bay for the rest of the run.
    /// </summary>
    [Fact]
    public void ALegThatCoversNoGroundIsGivenUp()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var cars = world.Cars;
        var (car, _) = BayManoeuvreTests.SendACarOut(world, noseIn: null);
        for (var tick = 0; tick < LeavingTicks && cars.Line[car].LaneCount == 0; tick++) loop.Advance(1);
        Assert.True(cars.Line[car].LaneCount > 0, $"car {car} never got out of its bay onto the street");

        var wreck = car == 0 ? 1 : 0;
        var ahead = Spline.SampleAt(cars.LineOf(car), cars.ProgressM[car] + (2f * cars.BuildOf(car).LengthM));
        BayManoeuvreTests.PutDown(world, wreck, ahead.PositionM, ahead.HeadingRad);

        // Every reroute, the one place given up and the patience that ends the leg — and one more for coming to rest. The
        // town's lights are no excuse: nothing queues between the wreck and the next one.
        var patienceS = Config.CarPatienceS * cars.FuseJitter[car];
        var boundTicks = (int)((Config.Patience.ReroutesPerLeg + 3) * patienceS / Config.TickSeconds);
        var givenUp = world.LegsGivenUp;
        for (var tick = 0; tick < boundTicks && world.LegsGivenUp == givenUp; tick++) loop.Advance(1);

        Assert.True(
            world.LegsGivenUp > givenUp,
            $"car {car} behind wreck {wreck} still on its leg after {boundTicks} ticks: reroutes {cars.Reroutes[car]}, " +
            $"aimed at bay {world.Parking.ClaimedBayOf(car)}, {world.PlacesGivenUp} places given up in the town");
    }

    /// <summary>Long enough for every car the city stands to be in its bay and at rest.</summary>
    const int WarmUpTicks = 120;

    /// <summary>Half a minute: long enough to wait out the street and drive the whole of a way out.</summary>
    const int LeavingTicks = 1800;
}
