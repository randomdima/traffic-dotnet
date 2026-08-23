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
[Collection(TrafficSimulation.Tests.Simulation.SolverCollection.Name)]
[Trait(Tier.Key, Tier.Town)]
public class WalkedLineTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static TownWorld Open(string map) => new(Towns.Fresh(map), Config);

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    /// <summary>
    /// <b>Every point of every walked line stands on ground a body may stand on.</b> The network is
    /// derived from the plan and the lanes are cut back by the ground, so a point that is not walkable is
    /// a line laid off it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryPointOfEveryWalkIsOnWalkableGround(string map)
    {
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var lines = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            var line = world.People.WalkedLineOf(person);
            for (var point = 0; point < world.People.WalkedCount[person]; point++)
            {
                Assert.True(world.Terrain.At(line[point]).Walkable,
                    $"{map}: walker {person} is sent to {line[point]}, which is {world.Terrain.GroundAt(line[point])}");
            }

            if (world.People.WalkedCount[person] > 0) lines++;
        }

        // A walked line is a route over the pavement's own network, so a map with no pavement lays none:
        // the proving ground is roads and cars and the people beside them walk straight at where they are
        // going. Everywhere there is a network, one walker not walking is the failure this counts.
        Assert.True(lines > 0 || world.Foot.EdgeCount == 0, $"{map} lays no walked line at all");
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
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var legs = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            var count = world.People.WalkedCount[person];
            var line = world.People.WalkedLineOf(person);
            var ways = world.People.WalkedWayOf(person);
            var alongsM = world.People.WalkedAlongOf(person);

            for (var point = 1; point < count; point++)
            {
                // Along one way the points are that way's own metres, and they only ever grow.
                if (ways[point] == ways[point - 1])
                {
                    Assert.True(
                        alongsM[point] >= alongsM[point - 1] - ToleranceM,
                        $"{map}: walker {person} is sent from {alongsM[point - 1]:F2} m back to "
                        + $"{alongsM[point]:F2} m along way {ways[point]}");
                }

                if (point < 2) continue;

                // And across a hand-over, where the metres are two different ways' and cannot be compared,
                // what may not happen is the body turning round: a leg that runs back over the one before it.
                var back = line[point - 1] - line[point - 2];
                var on = line[point] - line[point - 1];
                if (back.LengthSquared() < 1e-4f || on.LengthSquared() < 1e-4f) continue;

                legs++;
                var turnDeg = MathF.Acos(
                    Math.Clamp(Vector2.Dot(Vector2.Normalize(back), Vector2.Normalize(on)), -1f, 1f)) * 180f / MathF.PI;
                Assert.True(
                    turnDeg < HairpinDeg,
                    $"{map}: walker {person} turns {turnDeg:F0}° between its points {point - 1} and {point}, on "
                    + $"ways {ways[point - 1]} and {ways[point]}");
            }
        }

        Assert.True(legs > 0 || world.Foot.EdgeCount == 0, $"{map} lays no walked line long enough to have a leg");
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
    public void NoWalkCrossesACarriagewayOffThePaint(string map)
    {
        var plan = Towns.Of(map);
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var strideM = plan.CellSizeM * 0.25f;
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
                    Assert.True(ground.Walkable || !ground.Drivable,
                        $"{map}: walker {person}'s line runs over {world.Terrain.GroundAt(atM)} at {atM}");
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
        using var world = Open(map);
        if (world.People.Count == 0) return;

        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(3_600);

        Assert.True(world.WalkArrivals > 0, $"{map}: no walker finished a walk in a minute");
    }
}
