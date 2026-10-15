using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Gates;

/// <summary>
/// <b>What the shape of the claims must be</b>, asked of every way of a town that is running rather than of a staged
/// pair: no metre planned by two holders (TER-4c.3), no main claim over a mark without its secondary claim (TER-5c.1),
/// no plan laid over a body (TER-4c.1) — and nothing the atlas dropped for want of room.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is what the planned layer exists to keep</b> — one that could give the same metre to two holders would be no
/// mechanism at all. <b>The bodies are not asked it</b>: they are a record of where things are, and two of them over one
/// metre is a collision for the solver's gates, not a claim gone wrong (TER-4c.2).
/// </para>
/// <para>
/// <b>Asked on every tick the turns are taken and not at the end</b>, since each agent claims afresh on its own turn
/// and a turn that gave two plans one metre is one somebody drove on.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Perf)]
[Trait(Priority.Key, Priority.P0)]
[Collection(Simulation.SolverCollection.Name)]
public class ClaimGateTests
{
    /// <summary>Long enough for the queues, the junctions and the crossings to be busy at once.</summary>
    const int Ticks = 1_800;

    /// <summary>
    /// <b>No metre of any way is planned by two holders</b> (TER-4c.3): no main claim meets another holder's main claim
    /// or secondary claim. Two secondary claims share their ground, and lie over a third way where they meet at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoMetreOfAnyWayIsPlannedTwice(string map) => Watch(map, Disjoint);

    /// <summary>
    /// <b>A main claim over a mark places the whole of its secondary claim</b> (TER-5c.1): wherever a holder's main claim
    /// lies over a mark, the same holder holds all of the other way's section as a secondary claim — or the main claims
    /// of that way, which read nothing but their own, could not see it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void EveryMainClaimOverAMarkPlacesItsSecondaryClaim(string map) => Watch(map, Seconded);

    /// <summary>
    /// <b>No plan is laid over a body</b> (TER-4c.1): a main piece is cut at the first body in front of it, so none lies
    /// over anybody else's.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoPlanIsLaidOverABody(string map) => Watch(map, ClearOfTheBodies);

    /// <summary>
    /// <b>Nothing is dropped for want of room</b>: a way the atlas had no room for under a body is one nobody could see it
    /// on, and the bounds are sized so that it never happens.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NothingIsDroppedForWantOfRoom(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        new SimLoop<TownWorld>(world, config).Advance(Ticks);

        Assert.Equal(0, world.Atlas.Dropped);
    }

    /// <summary>A town ticked, and the claims after every tick's turns handed to one question, which counts what it looked at.</summary>
    static void Watch(string map, Func<TownWorld, string, int, long> ask)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        var looked = 0L;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            if (world.ClaimsLaidThisTick) looked += ask(world, map, tick);
        }

        // The census, without which a town that planned nothing would keep every one of these perfectly.
        Assert.True(looked > 0, $"{map}: nothing was planned in {Ticks} ticks");
    }

    static long Disjoint(TownWorld world, string map, int tick)
    {
        var claims = world.Occupancy;
        var looked = 0L;
        foreach (var way in claims.OccupiedWays)
        {
            var mains = claims.Mains(way);
            looked += mains.Length;
            foreach (var main in mains)
            {
                var holder = claims.HoldersOf(main)[0];
                foreach (var other in mains)
                {
                    if (claims.HoldersOf(other)[0] == holder || !Overlap(main, other)) continue;

                    Assert.Fail($"{map}: at tick {tick}, {world.Ways.KindOf(way)} way {way} is planned twice — {Named(claims, main)} and {Named(claims, other)}");
                }

                foreach (var second in claims.Secondaries(way))
                {
                    if (!Overlap(main, second) || (second.Holders == 1 && claims.HoldersOf(second)[0] == holder)) continue;

                    Assert.Fail($"{map}: at tick {tick}, {world.Ways.KindOf(way)} way {way} is planned twice — {Named(claims, main)} and {Named(claims, second)}");
                }
            }
        }

        return looked;
    }

    static long Seconded(TownWorld world, string map, int tick)
    {
        var claims = world.Occupancy;
        var looked = 0L;
        foreach (var way in claims.OccupiedWays)
        {
            foreach (var main in claims.Mains(way))
            {
                var holder = claims.HoldersOf(main)[0];
                foreach (ref readonly var mark in claims.Marks.Of(way))
                {
                    if (LaneOccupancy.DmOf(mark.MineFromM) >= main.ToDm || LaneOccupancy.DmOf(mark.MineToM) <= main.FromDm) continue;

                    looked++;
                    var (fromDm, toDm) = (LaneOccupancy.DmOf(mark.FromM), LaneOccupancy.DmOf(mark.ToM));
                    var held = false;
                    foreach (var second in claims.Secondaries(mark.OnWay))
                    {
                        held |= second.FromDm <= fromDm && second.ToDm >= toDm && claims.HoldersOf(second).IndexOf(holder) >= 0;
                    }

                    Assert.True(
                        held,
                        $"{map}: at tick {tick} {Named(claims, main)} on {world.Ways.KindOf(way)} way {way} lies over the mark "
                        + $"at {mark.MineFromM:0.0}–{mark.MineToM:0.0} m and does not hold {mark.FromM:0.0}–{mark.ToM:0.0} m of "
                        + $"{world.Ways.KindOf(mark.OnWay)} way {mark.OnWay}");
                }
            }
        }

        return looked;
    }

    static long ClearOfTheBodies(TownWorld world, string map, int tick)
    {
        var claims = world.Occupancy;
        var looked = 0L;
        foreach (var way in claims.OccupiedWays)
        {
            foreach (var main in claims.Mains(way))
            {
                looked++;
                var holder = claims.HoldersOf(main)[0];
                if (!claims.FirstBody(
                        way, main.FromM, main.ToM, LaneOccupancy.OccupantOf(holder), LaneOccupancy.RosterOf(holder), out var atM))
                {
                    continue;
                }

                Assert.Fail($"{map}: at tick {tick} {Named(claims, main)} on {world.Ways.KindOf(way)} way {way} lies over a body at {atM:0.0} m");
            }
        }

        return looked;
    }

    static bool Overlap(in Claim one, in Claim other) => one.ToDm > other.FromDm && other.ToDm > one.FromDm;

    static string Named(LaneOccupancy claims, in Claim claim)
    {
        var holders = claims.HoldersOf(claim);
        var whose = holders.Length == 0 ? "nobody" : $"{LaneOccupancy.RosterOf(holders[0])} {LaneOccupancy.OccupantOf(holders[0])}";
        return $"{whose}'s {claim.Kind} claim {claim.FromM:0.0}–{claim.ToM:0.0} m at {claim.Rung}";
    }
}
