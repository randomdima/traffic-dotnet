using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// What the traffic writes on the ground, in a running town rather than in arithmetic: that a town
/// standing still writes nothing, and that one being driven writes something.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class GroundMarkTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A parked car with its handbrake on does not scrub the road.</b> Every car in the town starts
    /// standing in a bay, so a town nobody has driven yet is a town with nothing written on it — and a
    /// mark model that gets this wrong paints the whole roster's outline into the car park.
    /// </summary>
    [Fact]
    public void ATownNobodyHasDrivenYetHasNothingWrittenOnIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        new SimLoop<TownWorld>(world, Config).Advance(60);

        Assert.Equal(0, world.Marks.Count);
    }

    /// <summary>
    /// <b>A car stopped on the whole of its pedal writes on the ground</b>: the pedal stands well clear of the
    /// tyres, so the wheels lock and drag rubber over the road. What is held is that the marks reach the ground
    /// at all, which nothing else in the suite would notice going missing.
    /// </summary>
    /// <remarks>
    /// <b>Staged and not waited for</b>: a driver of the town's own brakes inside what its tyres hold (CAR-47), so
    /// an ordinary stop writes nothing and a town may drive for minutes before one of its own does. A hand is held
    /// to nothing of the kind.
    /// </remarks>
    [Fact]
    public void ACarStoppedOnTheWholePedalWritesOnTheGround()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        var car = -1;
        for (var tick = 0; tick < LongestS * 60 && car < 0; tick++)
        {
            loop.Advance(1);
            for (var at = 0; at < world.Cars.Count && car < 0; at++)
            {
                if (world.Cars.AlongMps[at] > BrisklyMps) car = at;
            }
        }

        Assert.True(car >= 0, "nobody in the town got up to speed to be stopped");
        var brake = new HandInput(Held: true, Throttle: -1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero);
        for (var tick = 0; tick < StopS * 60 && world.Marks.Count == 0; tick++)
        {
            world.HandOnCar(car, brake);
            loop.Advance(1);
        }

        Assert.True(world.Marks.Count > 0, $"car {car} stopped on the whole pedal and left the road as it found it");
    }

    /// <summary>Fast enough that a locked stop drags the wheels for a car length or more.</summary>
    const float BrisklyMps = 10f;

    /// <summary>How long the town is given to get a car up to that, and a hard stop to take.</summary>
    const int LongestS = 60, StopS = 2;
}
