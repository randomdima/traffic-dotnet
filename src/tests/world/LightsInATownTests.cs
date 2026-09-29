using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A light's hold as a running town keeps it</b> (TLT-1): laid before any plan, and still there once every
/// plan has been answered and laid against it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P1)]
public class LightsInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A red holds its bar on every tick it is red</b> (TLT-1, TER-4c.1): the first metre past the bar is the
    /// light's, or the ground of a plan that beats it there — one whose holder can no longer stop short of it, or
    /// a call or a closure — unless a body travelling the lane has already started over it (TLT-2a).
    /// </summary>
    /// <remarks>
    /// <b>What it guards is a plan answered at the bar and laid a hair past it.</b> A metre carried to the line and
    /// back came home past where it was refused, and the hair it took off the light was the whole of the light's
    /// hold: the plan was then answered again against a road with no light on it, and drove the red at speed.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ARedHoldsItsBarOnEveryTickItIsRed(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        for (var tick = 0; tick < TicksWatched; tick++)
        {
            loop.Advance();
            for (var bar = 0; bar < world.Bars.Count; bar++)
            {
                var lane = world.Bars.Lane[bar];
                if (!RedThroughTheTick(world, lane)) continue;

                var barM = world.Bars.AlongM[bar] - (world.Bars.ThicknessM[bar] * 0.5f);
                var way = world.Ways.OfRoadLane(lane);
                var lengthM = world.Roads.LaneLengthM[lane];
                if (world.Occupancy.AheadTraveller(way, barM, lengthM, out var started) && started.FromM <= barM) continue;

                Assert.True(
                    HeldAt(world.Occupancy, way, barM),
                    $"{map}: tick {tick}, the bar of lane {lane} at {barM:0.00} m is red and held by nothing that beats the light");
            }
        }
    }

    /// <summary>Whether a lane is showing anything but green both where the tick began and where it ended — a tick a light changed in is not read.</summary>
    static bool RedThroughTheTick(TownWorld world, int lane) =>
        world.Signals.ForApproach(lane, world.ElapsedS) != SignalColour.Green
        && world.Signals.ForApproach(lane, world.ElapsedS - Config.TickSeconds) != SignalColour.Green;

    /// <summary>Whether the metre is the light's own, or planned by a holder that beats a light there (TER-5e).</summary>
    static bool HeldAt(LaneOccupancy occupancy, int way, float atM)
    {
        if (occupancy.AheadPlanned(way, atM, atM + Hair, LaneRoster.Signal, out var light) && light.FromM <= atM) return true;

        return occupancy.AheadPlanned(way, atM, atM + Hair, LaneRoster.Driving, out var plan)
               && plan.FromM <= atM
               && (plan.CommittedAt(atM) || plan.Priority < ClaimPriority.Signal);
    }

    /// <summary>How far past the bar a claim is read for: less than anything a plan is laid in.</summary>
    const float Hair = 1e-3f;

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>A minute of town: every light round its cycle four times with the traffic coming up to it.</summary>
    const int TicksWatched = 3_600;
}
