using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Gates;

/// <summary>
/// TER-4c.3 as a gate: <b>no metre of any way is in two claims at once</b>, asked of every way of a town
/// that is running rather than of a staged pair.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the rule the claims exist to keep</b> (TER-4c.1) — one that could grant the same metre twice
/// would be no mechanism at all — and until the claims were one table over one numbering it could not even
/// be asked: the carriageway and the footway were two records that nothing compared, so a walker held ground
/// a car was standing on and each of them was right about its own book.
/// </para>
/// <para>
/// <b>Every kind of way and both rosters</b>, because the whole point of one table is that a car on a kerb
/// and the walker beside it are the same kind of fact. What is asserted is exactly what
/// <see cref="LaneOccupancy"/> promises and nothing more: the stretches on a way are disjoint, they abut on
/// an exact metre, and a refused ask is not one of them (TER-5g — it is nobody's ground and binds nobody).
/// </para>
/// <para>
/// <b>Asked every tick and not at the end</b>, since the claims are rebuilt from nothing every tick and a
/// tick that laid two claims over one metre is a tick some driver read.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Perf)]
[Collection(Simulation.SolverCollection.Name)]
public class ClaimGateTests
{
    /// <summary>Long enough for the queues, the junctions and the crossings to be busy at once.</summary>
    const int Ticks = 600;

    /// <summary>How many stretches one way may hold before the reading is a bound rather than the way.</summary>
    const int MostOnAWay = 64;

    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void NoMetreOfAnyWayIsEverInTwoClaims(string map)
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Of(map), config);
        var loop = new SimLoop<TownWorld>(world, config);

        var laid = 0L;
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            laid += Disjoint(world, map, tick);
        }

        // The census, without which a town that claimed nothing would keep this perfectly.
        Assert.True(laid > 0, $"{map}: not one claim was laid in {Ticks} ticks");
    }

    /// <summary>Every way of the town walked once, and the count of what was on them.</summary>
    static long Disjoint(TownWorld world, string map, int tick)
    {
        var laid = 0L;
        Span<LaneClaim> slots = stackalloc LaneClaim[MostOnAWay];
        foreach (var way in world.Occupancy.OccupiedWays)
        {
            var count = world.Occupancy.CopyTo(way, slots);
            laid += count;

            for (var one = 0; one < count; one++)
            {
                if (slots[one].IsRejected) continue;

                for (var other = one + 1; other < count; other++)
                {
                    if (slots[other].IsRejected) continue;

                    // Touching is not overlapping: the stretches are half open, so a metre shared by two
                    // edges is the seam between them and belongs to the one in front.
                    if (slots[one].ToM <= slots[other].FromM || slots[other].ToM <= slots[one].FromM)
                    {
                        continue;
                    }

                    Assert.Fail(
                        $"{map}: at tick {tick}, {world.Ways.KindOf(way)} way {way} is claimed twice — "
                        + $"{slots[one].Of} {slots[one].Occupant} holds "
                        + $"{slots[one].FromM:0.000}–{slots[one].ToM:0.000} m at p{(int)slots[one].Priority} "
                        + $"and {slots[other].Of} {slots[other].Occupant} holds "
                        + $"{slots[other].FromM:0.000}–{slots[other].ToM:0.000} m at "
                        + $"p{(int)slots[other].Priority}");
                }
            }
        }

        return laid;
    }
}
