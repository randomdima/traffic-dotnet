using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Gates;

/// <summary>
/// <b>What the shape of the reservations must be</b>, asked of every way of a town that is running rather than
/// of a staged pair: no metre planned by two holders (TER-4c.3), no hold in two pieces (TER-5c.2), no main
/// claim over a mark without its secondary claim (TER-5c.1), no plan laid over a body (TER-4c.1), no rung
/// that grows along a hold (TER-5g.1) — and nothing dropped for want of room.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is what the planned layer exists to keep</b> — one that could give the same metre to two holders
/// would be no mechanism at all. <b>The physical layer is not asked it</b>: bodies are a record of where
/// things are, and two of them over one metre is a collision for the solver's gates, not a reservation gone
/// wrong (TER-4c.2).
/// </para>
/// <para>
/// <b>Asked every tick and not at the end</b>, since the reservations are rebuilt from nothing every tick and a
/// tick that laid two plans over one metre is a tick somebody drove on.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Perf)]
[Trait(Priority.Key, Priority.P0)]
[Collection(Simulation.SolverCollection.Name)]
public class ClaimGateTests
{
    /// <summary>Long enough for the queues, the junctions and the crossings to be busy at once.</summary>
    const int Ticks = 1_800;

    /// <summary>How many reservations one way may hold before the reading is a bound rather than the way.</summary>
    const int MostOnAWay = 128;

    /// <summary>
    /// What two edges met on one metre may be out by: a way's metre and a line's are one figure carried two
    /// ways, which is exact to the last few bits rather than bitwise, while a real overlap is metres.
    /// </summary>
    const float SeamM = 1e-3f;

    /// <summary>
    /// <b>No metre of any way is planned by two holders</b> (TER-4c.3) — except two secondary claims, which are
    /// two holders whose ground each lies over a third way and meet on their own ways where they meet at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoMetreOfAnyWayIsPlannedTwice(string map) => Watch(map, Disjoint);

    /// <summary>
    /// <b>No hold is in two pieces</b> (TER-5c.2): a hold is one stretch of its holder's line, so its pieces,
    /// read in the order the line runs over them, each begin where the one before it ended — and a car's
    /// begins at its own nose.
    /// </summary>
    /// <remarks>
    /// <b>A hole is ground nothing can be cut at</b>: a plan is weighed against whatever else is on its ways,
    /// and metres it holds past a gap in itself are metres whose holder cannot be seen coming.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoHoldIsInTwoPieces(string map) => Watch(map, OneStretch);

    /// <summary>
    /// <b>A main claim over a mark places the whole of its secondary claim</b> (TER-5c.1): wherever a hold's
    /// main claim lies over a mark, the same hold holds all of the other way's section as a secondary claim —
    /// or the main claims of that way, which read nothing but their own, could not see it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void EveryMainClaimOverAMarkPlacesItsSecondaryClaim(string map) => Watch(map, Seconded);

    /// <summary>
    /// <b>No plan is laid over a body</b> (TER-4c.1): a hold is cut at the first body in front of it on every
    /// way it is laid on, so none of its own pieces lies over anybody else's body — except a closure, which is
    /// laid round the scene it closes (SRV-6).
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoPlanIsLaidOverABody(string map) => Watch(map, ClearOfTheBodies);

    /// <summary>
    /// <b>The rung never grows along a hold</b> (TER-5g.1): read in the order the line runs over them, no
    /// piece of one hold is stronger than a piece before it — or the far side of a junction is held and the
    /// road to it is not.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NoHoldIsStrongerThanTheRoadToIt(string map) => Watch(map, Levelled);

    /// <summary>
    /// <b>Nothing is dropped for want of room</b>: a body or a plan the reservations had no room for is one
    /// nobody could see, and the bounds are sized so that it never happens.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryMapWorthAGate), MemberType = typeof(Towns))]
    public void NothingIsDroppedForWantOfRoom(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        new SimLoop<TownWorld>(world, config).Advance(Ticks);

        Assert.Equal(0, world.Occupancy.Dropped);
        Assert.Equal(0, world.Atlas.Dropped);
    }

    /// <summary>A town ticked, and every tick's reservations handed to one question, which counts what it looked at.</summary>
    static void Watch(string map, Func<TownWorld, string, int, long> ask)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        var looked = 0L;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            looked += ask(world, map, tick);
        }

        // The census, without which a town that planned nothing would keep every one of these perfectly.
        Assert.True(looked > 0, $"{map}: nothing was planned in {Ticks} ticks");
    }

    static long Disjoint(TownWorld world, string map, int tick)
    {
        var looked = 0L;
        Span<LaneClaim> slots = stackalloc LaneClaim[MostOnAWay];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyPlannedTo(way, slots);
            looked += count;

            for (var one = 0; one < count; one++)
            {
                for (var other = one + 1; other < count; other++)
                {
                    if (slots[one].Occupant == slots[other].Occupant && slots[one].Of == slots[other].Of) continue;
                    if (slots[one].Secondary && slots[other].Secondary) continue;
                    if (slots[one].ToM <= slots[other].FromM + SeamM || slots[other].ToM <= slots[one].FromM + SeamM) continue;

                    Assert.Fail(
                        $"{map}: at tick {tick}, {world.Ways.KindOf(way)} way {way} is planned twice — "
                        + $"{Named(slots[one])} and {Named(slots[other])}");
                }
            }
        }

        return looked;
    }

    static long OneStretch(TownWorld world, string map, int tick)
    {
        var holds = Pieces(world);
        foreach (var (hold, pieces) in holds)
        {
            pieces.Sort(static (one, other) => one.LineFromM.CompareTo(other.LineFromM));

            var first = pieces[0];
            if (first.Of == LaneRoster.Driving && first.Priority != ClaimPriority.Closed
                && world.DriveHold(first.Occupant) == hold)
            {
                Assert.True(
                    MathF.Abs(first.LineFromM - world.Cars.ClaimFromM[first.Occupant]) <= SeamM,
                    $"{map}: at tick {tick} car {first.Occupant}'s plan begins at {first.LineFromM:0.000} m of its "
                    + $"line and its nose is at {world.Cars.ClaimFromM[first.Occupant]:0.000} m");
            }

            for (var index = 1; index < pieces.Count; index++)
            {
                var endedAtM = pieces[index - 1].LineFromM + (pieces[index - 1].ToM - pieces[index - 1].FromM);
                Assert.True(
                    MathF.Abs(pieces[index].LineFromM - endedAtM) <= SeamM,
                    $"{map}: at tick {tick} {Named(pieces[index])} begins at {pieces[index].LineFromM:0.000} m of "
                    + $"its holder's line, and the piece before it ended at {endedAtM:0.000} m");
            }
        }

        return holds.Count;
    }

    static long Seconded(TownWorld world, string map, int tick)
    {
        var looked = 0L;
        Span<LaneClaim> slots = stackalloc LaneClaim[MostOnAWay];
        Span<LaneClaim> crossed = stackalloc LaneClaim[MostOnAWay];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyPlannedTo(way, slots);
            for (var at = 0; at < count; at++)
            {
                ref readonly var piece = ref slots[at];
                if (piece.Secondary) continue;

                foreach (ref readonly var mark in world.Occupancy.Marks.Of(way))
                {
                    if (mark.MineFromM >= piece.ToM - SeamM || mark.MineToM <= piece.FromM + SeamM) continue;

                    looked++;
                    var held = false;
                    var over = world.Occupancy.CopyPlannedTo(mark.OnWay, crossed);
                    for (var other = 0; other < over && !held; other++)
                    {
                        held = crossed[other].Secondary && crossed[other].Hold == piece.Hold
                               && crossed[other].FromM <= mark.FromM + SeamM && crossed[other].ToM >= mark.ToM - SeamM;
                    }

                    Assert.True(
                        held,
                        $"{map}: at tick {tick} {Named(piece)} on {world.Ways.KindOf(way)} way {way} lies over the "
                        + $"mark at {mark.MineFromM:0.000}–{mark.MineToM:0.000} m and does not hold "
                        + $"{mark.FromM:0.000}–{mark.ToM:0.000} m of {world.Ways.KindOf(mark.OnWay)} way {mark.OnWay}");
                }
            }
        }

        return looked;
    }

    static long ClearOfTheBodies(TownWorld world, string map, int tick)
    {
        var looked = 0L;
        Span<LaneClaim> planned = stackalloc LaneClaim[MostOnAWay];
        Span<LaneClaim> bodies = stackalloc LaneClaim[MostOnAWay];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyPlannedTo(way, planned);
            var standing = world.Occupancy.CopyBodiesTo(way, bodies);
            for (var at = 0; at < count; at++)
            {
                ref readonly var piece = ref planned[at];
                if (piece.Secondary || piece.Priority == ClaimPriority.Closed) continue;

                looked++;
                for (var body = 0; body < standing; body++)
                {
                    if (bodies[body].Occupant == piece.Occupant && bodies[body].Of == piece.Of) continue;
                    if (bodies[body].ToM <= piece.FromM + SeamM || bodies[body].FromM >= piece.ToM - SeamM) continue;

                    Assert.Fail(
                        $"{map}: at tick {tick} {Named(piece)} on {world.Ways.KindOf(way)} way {way} lies over the "
                        + $"body of {bodies[body].Of} {bodies[body].Occupant} at "
                        + $"{bodies[body].FromM:0.000}–{bodies[body].ToM:0.000} m");
                }
            }
        }

        return looked;
    }

    static long Levelled(TownWorld world, string map, int tick)
    {
        var holds = Pieces(world);
        foreach (var (_, pieces) in holds)
        {
            pieces.Sort(static (one, other) => one.LineFromM.CompareTo(other.LineFromM));
            for (var index = 1; index < pieces.Count; index++)
            {
                Assert.True(
                    pieces[index].Priority >= pieces[index - 1].Priority,
                    $"{map}: at tick {tick} {Named(pieces[index])} is held stronger than "
                    + $"{Named(pieces[index - 1])} on the road to it");
            }
        }

        return holds.Count;
    }

    /// <summary>Every hold's main claims — never its secondary claims — by the hold they are of.</summary>
    static Dictionary<int, List<LaneClaim>> Pieces(TownWorld world)
    {
        var holds = new Dictionary<int, List<LaneClaim>>();
        Span<LaneClaim> slots = stackalloc LaneClaim[MostOnAWay];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyPlannedTo(way, slots);
            for (var at = 0; at < count; at++)
            {
                if (slots[at].Secondary) continue;

                if (!holds.TryGetValue(slots[at].Hold, out var pieces)) holds[slots[at].Hold] = pieces = [];
                pieces.Add(slots[at]);
            }
        }

        return holds;
    }

    static string Named(in LaneClaim claim) =>
        $"{claim.Of} {claim.Occupant}'s {(claim.Secondary ? "secondary claim" : "main claim")} "
        + $"{claim.FromM:0.000}–{claim.ToM:0.000} m at {claim.Priority}";
}
