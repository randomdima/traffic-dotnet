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
    /// <b>A red holds its bar every time the claims are laid while it is red</b> (TLT-1, TER-4c.1): the first metre past
    /// the bar is the light's, or the ground of a plan that beats it there — one whose holder can no longer stop short
    /// of it, or a call or a closure — unless a body travelling the lane has already started over it (TLT-2a).
    /// </summary>
    /// <remarks>
    /// <b>What it guards is a plan answered at the bar and laid a hair past it.</b> A metre carried to the line and
    /// back came home past where it was refused, and the hair it took off the light was the whole of the light's
    /// hold: the plan was then answered again against a road with no light on it, and drove the red at speed.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ARedHoldsItsBarEveryTimeTheClaimsAreLaid(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        for (var tick = 0; tick < TicksWatched; tick++)
        {
            loop.Advance();
            if (!world.ClaimsLaidThisTick) continue;

            for (var bar = 0; bar < world.Bars.Count; bar++)
            {
                var lane = world.Bars.Lane[bar];
                if (!RedThroughTheTick(world, lane)) continue;

                var barM = world.Bars.AlongM[bar] - (world.Bars.ThicknessM[bar] * 0.5f);
                var way = world.Ways.OfRoadLane(lane);
                var lengthM = world.Roads.LaneLengthM[lane];
                if (world.Occupancy.FirstBody(way, barM, lengthM, LaneOccupancy.Nobody, LaneRoster.Signal, out var startedM)
                    && LaneOccupancy.DmOf(startedM) <= LaneOccupancy.DmOf(barM))
                {
                    continue;
                }

                Assert.True(
                    HeldAt(world.Occupancy, way, barM),
                    $"{map}: tick {tick}, the bar of lane {lane} at {barM:0.00} m is red and held by nothing that beats the light; "
                    + $"the way holds {Said(world.Occupancy, way)}");
            }
        }
    }

    /// <summary>Whether a lane is showing anything but green both where the tick began and where it ended — a tick a light changed in is not read.</summary>
    static bool RedThroughTheTick(TownWorld world, int lane) =>
        world.Signals.ForApproach(lane, world.ElapsedS) != SignalColour.Green
        && world.Signals.ForApproach(lane, world.ElapsedS - Config.TickSeconds) != SignalColour.Green;

    /// <summary>Everything on one way, for a message that has to say why the bar was not held.</summary>
    static string Said(LaneOccupancy occupancy, int way)
    {
        var said = new List<string>();
        Add(occupancy.Bodies(way));
        Add(occupancy.Mains(way));
        Add(occupancy.Secondaries(way));
        return string.Join(", ", said);

        void Add(ReadOnlySpan<Claim> run)
        {
            foreach (var claim in run) said.Add($"{claim.Kind} p{(byte)claim.Rung} {claim.FromM:0.0}–{claim.ToM:0.0}");
        }
    }

    /// <summary>Whether the metre is the light's own, or planned by a holder that beats a light there (TER-5e).</summary>
    static bool HeldAt(LaneOccupancy occupancy, int way, float atM)
    {
        var atDm = LaneOccupancy.DmOf(atM);
        if (occupancy.FirstSecondary(way, atM, atM + LaneOccupancy.MetresOf(1), ClaimPriority.Signal, out var lightFromM, out _)
            && LaneOccupancy.DmOf(lightFromM) <= atDm)
        {
            return true;
        }

        foreach (var plan in occupancy.Mains(way))
        {
            if (plan.FromDm <= atDm && plan.ToDm > atDm && plan.Rung < ClaimPriority.Signal) return true;
        }

        return false;
    }

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>A minute of town: every light round its cycle four times with the traffic coming up to it.</summary>
    const int TicksWatched = 3_600;
}
