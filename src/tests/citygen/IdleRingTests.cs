using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// The idle ring as a picture (GEN-1b): that the escort keeps station with what it is escorting, and that
/// nothing on the map ever comes to a standstill.
/// </summary>
/// <remarks>
/// <b>This is the one map whose whole job is being looked at</b>, and both of its failure modes are quiet:
/// a convoy that stretches into three unrelated cars, and a convoy so tight that the escorted car runs out
/// of granted road on a bend and stops behind an escort that is still moving. Neither throws, neither fails
/// a gate, and both are only ever seen by somebody who happened to be watching the start menu.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class IdleRingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Five minutes, which is a dozen laps: long enough for a gap that is going to drift to have.</summary>
    const int Ticks = 18_000;

    /// <summary>
    /// The second the cars spend getting off their spawns, and no more. <b>Short on purpose</b>: standing
    /// closer than the road would grant is a stall in the opening seconds, on the very frame the start menu
    /// opens over, and a settle long enough to skip it is a settle that hides what this is watching for.
    /// </summary>
    const int SettleTicks = 60;

    /// <summary>
    /// <b>Nothing on the ring ever stops.</b> A car held at a standstill on a map with nothing to give way
    /// to, nothing to wait for and nobody on foot is a car the convoy's own spacing has stalled.
    /// </summary>
    [Fact]
    public void NoCarOnTheRingComesToAStandstill()
    {
        using var world = new TownWorld(Towns.Of(IdlePlan.Name), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        // The slowest each car ever got and what was holding it there, so a failure names the car, the
        // figure and the term rather than the tick it happened to be caught on.
        var slowestMps = new float[world.Cars.Count];
        var heldBy = new DrivingHold[world.Cars.Count];
        Array.Fill(slowestMps, float.PositiveInfinity);

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance(1);
            if (tick < SettleTicks) continue;

            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.AlongMps[car] >= slowestMps[car]) continue;

                slowestMps[car] = world.Cars.AlongMps[car];
                heldBy[car] = world.Cars.Hold[car];
            }
        }

        for (var car = 0; car < world.Cars.Count; car++)
        {
            Assert.True(
                slowestMps[car] > Config.Driving.StopSpeedMps,
                $"car {car} ({IdlePlan.PartOf(car)}) got down to {slowestMps[car]:F2} m/s, held by {heldBy[car]}");
        }
    }

    /// <summary>
    /// <b>The escort keeps station with what it is escorting</b> — it is held under its charge's own pace
    /// (`IdlePlan.EscortPaceShare`), so the three are still one convoy a dozen laps later rather than three
    /// cars that happen to be on the same road.
    /// </summary>
    /// <remarks>
    /// <b>What is asserted is that the convoy is together and not how far apart</b>: the gap is the ordinary
    /// following distance, which is the road's answer and not this map's. A lap is the bound because a
    /// convoy that has come apart on a closed circuit ends up spread around the whole of it.
    /// </remarks>
    [Fact]
    public void TheEscortIsStillWithItsChargeADozenLapsLater()
    {
        using var world = new TownWorld(Towns.Of(IdlePlan.Name), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(Ticks);

        var apartM = ApartOnTheRingM(world, IdlePlan.Escorted - 1, IdlePlan.Escorted)
            + ApartOnTheRingM(world, IdlePlan.Escorted, IdlePlan.Escorted + 1);

        Assert.True(
            apartM < IdlePlan.RingM(Config) * 0.25f,
            $"the convoy is {apartM:F0} m end to end on a {IdlePlan.RingM(Config):F0} m ring");
    }

    /// <summary>How far apart two of the ring's cars stand, straight across rather than along the road.</summary>
    static float ApartOnTheRingM(TownWorld world, int car, int other) =>
        (world.Cars.PositionM[car] - world.Cars.PositionM[other]).Length();
}
