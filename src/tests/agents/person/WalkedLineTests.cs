using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// The walked line: that a walk is a route over the pavement's own network laid as points, that those
/// points keep to ground a body may stand on, and that a carriageway is only ever crossed on the paint.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class WalkedLineTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// <b>One run of one map, read by every claim below.</b> Each of them holds what it was broken by, or
    /// null, beside the census that says the run had anything to say about it at all.
    /// </summary>
    /// <remarks>
    /// A claim is recorded rather than thrown on, so one broken claim still lets the other three be
    /// answered off the same run — and the field is written once, on the first line that broke it.
    /// </remarks>
    sealed class Watched
    {
        public string? OffWalkableGround, DoubledBack, OffThePaint;
        public int Lines, Legs, FootEdges, People;
        public long Arrivals;
    }

    static readonly ConcurrentDictionary<string, Watched> Runs = new();

    /// <summary>The run all four claims are read off, taken once per map.</summary>
    static Watched Of(string map) => Runs.GetOrAdd(map, Watch);

    /// <summary>
    /// <b>A minute of the town, walked once instead of four times.</b> The three claims about the shape of a
    /// line are read where they have always been read — ten seconds in, with every walker mid-walk — and the
    /// run then goes on to the minute the fourth needs, which is how long it takes a walk to finish.
    /// </summary>
    /// <remarks>
    /// <b>The moment is the reason this is a watch and not a shared world.</b> Read at the end of the minute
    /// instead, the shape claims say nothing on a map whose walkers have all arrived by then: Zebras lays one
    /// walker a street and every one of them is standing still at sixty seconds, so the census that guards
    /// this against passing vacuously is the thing that would have to be given up to share a single moment.
    /// </remarks>
    static Watched Watch(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(TicksLaid);

        var found = new Watched { FootEdges = world.Foot.EdgeCount, People = world.People.Count };
        EveryPointIsOnWalkableGround(world, map, found);
        NoWalkDoublesBack(world, map, found);
        NoWalkCrossesOffThePaint(world, map, found);

        loop.Advance(TicksWalked - TicksLaid);
        found.Arrivals = world.WalkArrivals;
        return found;
    }

    /// <summary>Ten seconds: long enough for every walker to be on a line, short enough that none has left it.</summary>
    const int TicksLaid = 600;

    /// <summary>A minute: long enough for a line to be laid, walked and finished on every map that lays one.</summary>
    const int TicksWalked = 3_600;

    /// <summary>
    /// <b>Every point of every walked line stands on ground a body may stand on.</b> The network is
    /// derived from the plan and the lanes are cut back by the ground, so a point that is not walkable is
    /// a line laid off it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryPointOfEveryWalkIsOnWalkableGround(string map)
    {
        var found = Of(map);

        Assert.Null(found.OffWalkableGround);

        // A walked line is a route over the pavement's own network, so a map with no pavement lays none:
        // the proving ground is roads and cars and the people beside them walk straight at where they are
        // going. Everywhere there is a network, one walker not walking is the failure this counts.
        Assert.True(found.Lines > 0 || found.FootEdges == 0, $"{map} lays no walked line at all");
    }

    /// <summary>What <see cref="EveryPointOfEveryWalkIsOnWalkableGround"/> watches for.</summary>
    static void EveryPointIsOnWalkableGround(TownWorld world, string map, Watched found)
    {
        for (var person = 0; person < world.People.Count; person++)
        {
            var line = world.People.WalkedLineOf(person);
            for (var point = 0; point < world.People.WalkedCount[person]; point++)
            {
                if (world.Terrain.At(line[point]).Walkable) continue;

                found.OffWalkableGround ??=
                    $"{map}: walker {person} is sent to {line[point]}, which is {world.Terrain.GroundAt(line[point])}";
            }

            if (world.People.WalkedCount[person] > 0) found.Lines++;
        }
    }

    /// <summary>
    /// <b>A walk never doubles back on itself.</b> Its points run one way along the ground: each leg goes
    /// on from the one before it rather than back over it, and two points on one way stand in that way's
    /// own order.
    /// </summary>
    /// <remarks>
    /// <b>It is what a hand-over between two stretches can quietly break.</b> A corner puts a body down a
    /// margin into the lane it leads onto, and where that lane was stationed from its own walked start
    /// instead the walk began behind the point the corner had just reached — 87 of Odesa's lines, by up to
    /// 2.28 m, walked backwards before starting again.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AWalkNeverDoublesBackOnItself(string map)
    {
        var found = Of(map);

        Assert.Null(found.DoubledBack);
        Assert.True(found.Legs > 0 || found.FootEdges == 0, $"{map} lays no walked line long enough to have a leg");
    }

    /// <summary>What <see cref="AWalkNeverDoublesBackOnItself"/> watches for.</summary>
    static void NoWalkDoublesBack(TownWorld world, string map, Watched found)
    {
        for (var person = 0; person < world.People.Count; person++)
        {
            var count = world.People.WalkedCount[person];
            var line = world.People.WalkedLineOf(person);
            var ways = world.People.WalkedWayOf(person);
            var alongsM = world.People.WalkedAlongOf(person);

            for (var point = 1; point < count; point++)
            {
                // Along one way the points are that way's own metres, and they only ever grow.
                if (ways[point] == ways[point - 1] && alongsM[point] < alongsM[point - 1] - ToleranceM)
                {
                    found.DoubledBack ??=
                        $"{map}: walker {person} is sent from {alongsM[point - 1]:F2} m back to "
                        + $"{alongsM[point]:F2} m along way {ways[point]}";
                }

                if (point < 2) continue;

                // And across a hand-over, where the metres are two different ways' and cannot be compared,
                // what may not happen is the body turning round: a leg that runs back over the one before it.
                var back = line[point - 1] - line[point - 2];
                var on = line[point] - line[point - 1];
                if (back.LengthSquared() < 1e-4f || on.LengthSquared() < 1e-4f) continue;

                found.Legs++;
                var turnDeg = MathF.Acos(
                    Math.Clamp(Vector2.Dot(Vector2.Normalize(back), Vector2.Normalize(on)), -1f, 1f)) * 180f / MathF.PI;
                if (turnDeg >= HairpinDeg)
                {
                    found.DoubledBack ??=
                        $"{map}: walker {person} turns {turnDeg:F0}° between its points {point - 1} and {point}, on "
                        + $"ways {ways[point - 1]} and {ways[point]}";
                }
            }
        }
    }

    /// <summary>A centimetre: the arc arithmetic's, not the walk's.</summary>
    const float ToleranceM = 0.01f;

    /// <summary>
    /// Past this a body is not rounding a corner but going back the way it came. A pavement corner is a
    /// right angle taken over several points, so no honest leg comes near it.
    /// </summary>
    const float HairpinDeg = 135f;

    /// <summary>
    /// <b>A carriageway is crossed on the paint and nowhere else.</b> Walked between its points, the line
    /// may stand on ground that is walkable, or on a crossing — which is walkable <em>and</em> drivable —
    /// and never on a carriageway that is only drivable.
    /// </summary>
    /// <remarks>
    /// The stride is the walked line's own sampling rule rather than the cells', because the line clips
    /// cell corners: a sample a cell apart walks past the corner of a carriageway a body is already over.
    /// A quarter of a cell either side of a kerb is the classifier's own tolerance (TER-7), so a point
    /// that is off the walk by less than that is not a line in the road.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoWalkCrossesACarriagewayOffThePaint(string map) => Assert.Null(Of(map).OffThePaint);

    /// <summary>What <see cref="NoWalkCrossesACarriagewayOffThePaint"/> watches for.</summary>
    static void NoWalkCrossesOffThePaint(TownWorld world, string map, Watched found)
    {
        var strideM = world.Plan.CellSizeM * 0.25f;
        for (var person = 0; person < world.People.Count; person++)
        {
            var line = world.People.WalkedLineOf(person);
            for (var point = 1; point < world.People.WalkedCount[person]; point++)
            {
                var fromM = line[point - 1];
                var toM = line[point];
                var runM = Vector2.Distance(fromM, toM);
                var steps = Math.Max(1, (int)MathF.Ceiling(runM / strideM));

                for (var step = 0; step <= steps; step++)
                {
                    var atM = Vector2.Lerp(fromM, toM, step / (float)steps);
                    var ground = world.Terrain.At(atM);
                    if (ground.Walkable || !ground.Drivable) continue;

                    found.OffThePaint ??=
                        $"{map}: walker {person}'s line runs over {world.Terrain.GroundAt(atM)} at {atM}";
                }
            }
        }
    }

    /// <summary>
    /// The lines are walked and not merely laid: walkers reach where they were going and are given
    /// somewhere else.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void WalkersReachWhereTheyWereGoing(string map)
    {
        var found = Of(map);
        if (found.People == 0) return;

        Assert.True(found.Arrivals > 0, $"{map}: no walker finished a walk in a minute");
    }
}
