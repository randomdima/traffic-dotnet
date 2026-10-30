using System.Numerics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Terrain;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// The marks on the device are strokes as the host's are (<c>drift_marks.glsl</c>, <see cref="DriftMarks"/>): every knot
/// laid either begins a stroke or carries one on from a knot still in the ring, of its own kind and width, and no further
/// from it than a knot's most.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P7)]
public class DeviceMarkTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Fast enough that a locked stop drags the wheels for a car length or more.</summary>
    const float BrisklyMps = 10f;

    /// <summary>How long the town is given to get a car up to that, and a hard stop to take.</summary>
    const int LongestS = 60, StopS = 2;

    [Fact]
    public void EveryKnotTheDeviceLaysCarriesItsStrokeOnWithinTheFit()
    {
        using var town = new TownWorld(Towns.Of(Towns.Fixture), Config);
        town.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var world = (DeviceWorld)town.World;
        var loop = new SimLoop<TownWorld>(town, Config);

        var car = -1;
        for (var tick = 0; tick < LongestS * 60 && car < 0; tick++)
        {
            loop.Advance(1);
            for (var at = 0; at < town.Cars.Count && car < 0; at++)
            {
                if (town.Cars.AlongMps[at] > BrisklyMps) car = at;
            }
        }

        Assert.True(car >= 0, "nobody in the town got up to speed to be stopped");
        var brake = new HandInput(Held: true, Throttle: -1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero);
        for (var tick = 0; tick < StopS * 60; tick++)
        {
            town.HandOnCar(0, car, brake);
            loop.Advance(1);
        }

        loop.Advance(2);
        var (knots, ring) = world.ReadTheMarks();
        Assert.True(ring.Laid > 0, $"car {car} stopped on the whole pedal and left the road as it found it");

        for (var slot = 0; slot < ring.Laid; slot++)
        {
            var knot = knots[slot];
            if (knot.Previous == DriftMarks.NoKnot || ring.Total - knot.Previous > ring.Laid) continue;

            var before = knots[(int)(knot.Previous % ring.Capacity)];
            Assert.Equal(before.Ploughed, knot.Ploughed);
            Assert.Equal(before.WidthM, knot.WidthM);
            Assert.InRange(Vector2.Distance(before.AtM, knot.AtM), 0f, Config.Marks.KnotMostM + Config.Marks.SpacingM);
        }
    }
}
