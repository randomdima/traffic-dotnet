using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// CAR-14 and CTL-5c against a town rather than against arithmetic: the states a lamp is read from that
/// only a standing world has — a car nobody has driven yet, and one a player has taken the wheel of.
/// </summary>
/// <remarks>
/// <b>A minute of a city counting brake lamps used to be here and is not</b> (VER-12): what it asserted
/// was <c>count &gt; 0</c> over driven traffic, which goes red the day a fixture is given room and says
/// nothing about a rule. That both lamps follow the pedal and the line ahead is <c>CarLampTests</c>', from
/// a command and a line, in microseconds.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
public class CarLampTrafficTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Every car starts standing in a bay with nobody in it, and a parked car says nothing (CAR-14.5).</summary>
    [Fact]
    public void ATownNobodyHasDrivenYetShowsNoLampAtAll()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        new SimLoop<TownWorld>(world, Config).Advance(60);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            Assert.Equal(CarLampSet.None, CarLamps.Showing(world.Cars, car, Config, handAtTheWheel: false));
        }
    }


    /// <summary>
    /// CTL-5c against a town: a police car is taken over from its apron, which is the state the
    /// arithmetic cannot see — a car standing by is driving nothing, so a beacon wired only to
    /// <see cref="CarFleet.Driven"/> stays dark for the whole of the case the player is in.
    /// </summary>
    [Fact]
    public void APoliceCarTakenOverFromItsApronRunsItsBeacon()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var police = FirstPoliceCarIn(world);

        world.Select(new Selection(SelectionKind.Car, police));
        Assert.Equal(CarLampSet.None, Showing(world, police));

        world.Hands(new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero));
        new SimLoop<TownWorld>(world, Config).Advance(30);

        Assert.Equal(CarLampSet.Beacon, Showing(world, police) & CarLampSet.Beacon);

        // And it buys nothing: the beacon is the picture, and the road is still not told.
        Assert.False(world.Cars.BlueLight[police], "a hand at the wheel granted itself the road");

        // The reset gives the wheel up, and the bar goes out with it.
        world.ReleaseHands();
        Assert.Equal(CarLampSet.None, Showing(world, police) & CarLampSet.Beacon);
    }

    static CarLampSet Showing(TownWorld world, int car) =>
        CarLamps.Showing(world.Cars, car, Config, Selection.Holds(world.HandDriven, SelectionKind.Car, car));

    static int FirstPoliceCarIn(TownWorld world)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Variant[car] == CarCatalog.Shared.Police) return car;
        }

        Assert.Fail("the town stood no police car to take the wheel of");
        return -1;
    }
}
