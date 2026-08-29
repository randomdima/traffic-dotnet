using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// CAR-14 against a town being driven rather than against arithmetic: that the lamps are wired to the
/// driving and not merely to a struct. Every lamp is a read of state something else sets, so the way
/// this breaks is silently — the arithmetic keeps passing while nothing on screen ever lights up.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
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
    /// A minute of a city is a minute of cars braking for one another and turning at junctions, so both
    /// lamps have to happen — and be seen to, since what lights them is read off the command and the
    /// line rather than set anywhere.
    /// </summary>
    [Fact]
    public void ADrivenTownBrakesAndIndicates()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var braked = 0;
        var indicated = 0;

        // The minute is the bound on how long a city can take to show both, and not a window worth
        // watching to the end of: the second that has seen each of them once is the whole answer.
        for (var second = 0; second < 60 && (braked == 0 || indicated == 0); second++)
        {
            loop.Advance((int)MathF.Round(1f / Config.TickSeconds));
            for (var car = 0; car < world.Cars.Count; car++)
            {
                var showing = CarLamps.Showing(world.Cars, car, Config, Selection.Holds(world.HandDriven, SelectionKind.Car, car));
                if ((showing & CarLampSet.Brake) != 0) braked++;
                if ((showing & (CarLampSet.TurnLeft | CarLampSet.TurnRight)) != 0) indicated++;
            }
        }

        Assert.True(braked > 0, "a minute of a city went by with nobody's brake lamps on");
        Assert.True(indicated > 0, "a minute of a city went by with nobody indicating a turn");
    }

    /// <summary>
    /// CTL-5c against a town: a police car is taken over from its apron, which is the state the
    /// arithmetic cannot see — a car standing by is driving nothing, so a beacon wired only to
    /// <see cref="CarFleet.Driven"/> stays dark for the whole of the case the player is in.
    /// </summary>
    [Fact]
    public void APoliceCarTakenOverFromItsApronRunsItsBeacon()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
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
