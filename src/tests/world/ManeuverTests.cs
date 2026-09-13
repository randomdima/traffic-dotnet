using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The catalogue in a running town: that every entry this engine has built is actually reached, that
/// nothing stands still with no clock against it, and that a car <b>stops short of a crossing somebody
/// is on</b> (TER-4c.1) — which no entry of the catalogue does, so what is asserted here is the profile
/// these tests can only see through the bodies.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class ManeuverTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// Long enough for the fixture map to turn a whole trip over, which is where the parking entries
    /// come from. <b>Two minutes and not one</b>: the ground took a rolling resistance when the tyres
    /// did, which costs a car about a fifth of its acceleration, and a small lit map is stop-start
    /// enough that a whole leg no longer fits in a minute.
    /// </summary>
    const int MeasuredTicks = 7_200;

    static TownWorld Open(string map) => new(Towns.Of(map), Config);

    /// <summary>
    /// <b>There is no state a car can stand still in that nothing is running for.</b> The watchdog has
    /// every driven car and the light has the ones queueing at one — so this counter is zero or the wiring
    /// has a hole in it.
    /// </summary>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void NoCarStandsStillWithNothingRunningForIt(string map)
    {
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1_200);

        Assert.Equal(0, world.Trace.StoodUnclocked);
    }
}
