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
/// That both lamps follow the pedal and the line ahead is <c>CarLampTests</c>', from a command and a line,
/// in microseconds.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
public class CarLampTrafficTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// Before the first tick no car has a pedal down or a turn to announce, so <b>what is lit is the headlamps,
    /// exactly on the cars something already drives</b> (CAR-14.8) — the fixture puts its own cars on its lanes as
    /// it is stood up — and nothing on a car parked (CAR-14.5).
    /// </summary>
    [Fact]
    public void ATownNobodyHasDrivenYetShowsOnlyTheHeadlampsOfWhatIsDriven()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            Assert.Equal(
                world.Cars.Driven[car] ? CarLampSet.Head : CarLampSet.None,
                CarLamps.Showing(world.Cars, car, Config, handAtTheWheel: false));
        }
    }


    static CarLampSet Showing(TownWorld world, int car) =>
        CarLamps.Showing(world.Cars, car, Config, Selection.Holds(world.HandDriven, SelectionKind.Car, car));

}
