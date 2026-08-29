using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.Bench;
using Xunit;
using Xunit.Abstractions;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>Every car anybody may be handed can drive the road</b>, taken off the fleet lap
/// (<see cref="TrackLap.Fleet"/>) by the instrument that measures it: one of every look the fleet ships, on
/// the same circuit the track probe measures, with nobody on foot anywhere on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the question the measured lap cannot answer.</b> That one deliberately stands six of the
/// nominal car so a difference between its rows is a difference about drive layout (CAR-11a); this one
/// varies everything a variant states — 1050 kg to 4200, a 3.4 m hatchback to a 4.0 m pickup, an
/// acceleration factor of 0.32 to 1.75 — and asks only whether each of them drove the lap.
/// </para>
/// <para>
/// <b>The claims are <see cref="FleetWatch"/>'s</b> and are the same ones <c>--bench fleet</c> prints and
/// the panel draws on a run of <c>--map Fleet</c>. What is asserted is what has to hold of any car on any
/// of these shapes, and the numbers themselves are quoted beside them.
/// </para>
/// <para>
/// <b>Nobody is on foot on it</b>, which is what the other two laps carry people for: they measure a driver
/// stopping for what is in front of it, and this one measures the car. What slows a car here is the shape
/// it is taking and whoever is ahead of it, and nothing else.
/// </para>
/// <para>
/// <b>It is watched from the standing start.</b> Sixteen cars on one single-lane circuit with tops from 45
/// to 109 m/s end up one queue behind the armoured car, and no car may cross the centreline to get past a
/// moving one (CAR-6.2b) — so what is measured is the part of the run in which every car still has road in
/// front of it, and the start is also the one pull away from rest the lap has.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class FleetLapTests(ITestOutputHelper output)
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>One run of the fleet lap, shared by every case here — two questions about one set of laps.</summary>
    static readonly LapWatch Ran = TrackProbe.Measure(Config, TrackLap.Fleet);

    [Fact]
    public void EveryClaimTheFleetLapMakesIsKept()
    {
        Claims.AssertKept(Ran, output);
    }

    /// <summary>
    /// <b>Every look is on the table.</b> The lap stands one car of each and the claims above are read over
    /// all of them, so a run that quietly stood fewer would keep every one of those claims while saying
    /// nothing about the looks it left off.
    /// </summary>
    [Fact]
    public void EveryLookTheFleetShipsIsOnTheLap()
    {
        var metrics = Ran.Metrics;
        Assert.Equal(TrackPlan.FleetCars, metrics.Cars);

        var looks = new HashSet<string>();
        for (var car = 0; car < metrics.Cars; car++)
        {
            ref readonly var build = ref metrics.BuildOf(car);
            output.WriteLine(
                $"{metrics.LookOf(car)}: {build.MassKg:F0} kg, {metrics.FiguresOfCar(car).Passes} passes over "
                + $"{metrics.Laps(car):F1} laps, top {metrics.TopMps(car):F2} m/s, best pull "
                + $"{metrics.FiguresOfCar(car).PulledBestMps2:F2} m/s²");

            Assert.True(looks.Add(metrics.LookOf(car)), $"{metrics.LookOf(car)} is on the lap twice");
        }
    }
}
