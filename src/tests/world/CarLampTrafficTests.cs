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
        // Before the first tick, which is what "nobody has driven yet" is: the town stands its cars on its
        // own lanes and they are away inside a second of town time.
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            Assert.Equal(CarLampSet.None, CarLamps.Showing(world.Cars, car, Config, handAtTheWheel: false));
        }
    }


    static CarLampSet Showing(TownWorld world, int car) =>
        CarLamps.Showing(world.Cars, car, Config, Selection.Holds(world.HandDriven, SelectionKind.Car, car));

}
