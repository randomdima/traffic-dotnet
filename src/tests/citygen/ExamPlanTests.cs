using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// That the exam map is the map its cards asked for: every card is staged at a junction that has the arms
/// it names, the lattice carries the four shapes a junction can be, and no card is staged on top of
/// another.
/// </summary>
/// <remarks>
/// <b>Asked of the plan and not of the file</b>, so a card added or moved is answered by this class before
/// anything is laid. That the file on disk is <em>this</em> plan is <see cref="MapConformanceTests"/>'s
/// question, as it is for the proving grounds.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class ExamPlanTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static CityPlan Laid => Lay.Value;

    static readonly Lazy<CityPlan> Lay = new(() => ExamPlan.Lay(Config));

    [Fact]
    public void ThereIsACardForEveryCellOfTheLattice()
    {
        Assert.Equal(ExamCards.Rows * ExamCards.Columns, ExamCards.Count);
        Assert.Equal(ExamCards.Count, ExamCards.All.Length);
    }

    /// <summary>
    /// <b>Every driver comes from an arm its junction has and leaves by one too.</b> A card naming a
    /// bearing the lattice does not carry there would be a car ordered to a place no route reaches, which
    /// is a harness that fails for a reason that is not the driving.
    /// </summary>
    [Fact]
    public void EveryCardIsStagedWhereItsArmsAre()
    {
        var lattice = ExamLattice.Of(Config);
        var arms = ArmsPerJunction();

        for (var card = 0; card < ExamCards.Count; card++)
        {
            var stage = lattice.Stage(card);
            foreach (var drives in ExamCards.All[card].Drivers)
            {
                Assert.True(
                    arms[stage].Contains(drives.From),
                    $"card {card} ({ExamCards.All[card].Name}) comes from the {drives.From}, "
                    + $"and junction {stage} has arms {string.Join(", ", arms[stage])}");
                Assert.True(
                    arms[stage].Contains(drives.To),
                    $"card {card} ({ExamCards.All[card].Name}) leaves by the {drives.To}, "
                    + $"and junction {stage} has arms {string.Join(", ", arms[stage])}");
            }
        }
    }

    /// <summary>
    /// <b>The lattice carries more than one shape of junction</b>, which is what makes the exam an exam
    /// rather than thirty-six copies of one crossroads: crossroads, T-junctions and the dead ends at the
    /// heads of the spurs (TER-5a).
    /// </summary>
    /// <remarks>
    /// <b>There is no inline junction on it</b> (TER-5b) and the mid-block crossing it carries belongs to
    /// no junction instead (TER-6) — a node with two arms cannot be lit (TLT-3), and its two arms' lane
    /// ends lie over one another under the paint, which is a crossing whose bands no walker can be ordered
    /// along. It is a gap between two rules and is written up in the log.
    /// </remarks>
    [Fact]
    public void TheLatticeCarriesMoreThanOneShapeOfJunction()
    {
        var arms = ArmsPerJunction();
        var shapes = new int[5];
        foreach (var at in arms) shapes[at.Count]++;

        Assert.True(shapes[4] > 0, "no crossroads");
        Assert.True(shapes[3] > 0, "no T-junction");
        Assert.True(shapes[1] > 0, "no dead end");
        Assert.Equal(0, shapes[0]);
        Assert.Equal(0, shapes[2]);
    }

    /// <summary>
    /// <b>No junction is two arms meeting at a right angle</b> (TER-5b). That is a road that turns and not
    /// a place roads meet, and it is the commonest authoring mistake there is — so the lattice's own
    /// corners, which would each be one, are the reason a corner cell is given a spur whether its card
    /// asked for one or not.
    /// </summary>
    [Fact]
    public void NoJunctionIsACornerDressedAsAnIntersection()
    {
        var arms = ArmsPerJunction();
        for (var junction = 0; junction < arms.Count; junction++)
        {
            if (arms[junction].Count != 2) continue;

            Assert.True(
                ExamLattice.Opposite(arms[junction][0]) == arms[junction][1],
                $"junction {junction} has two arms at an angle: {arms[junction][0]} and {arms[junction][1]}");
        }
    }

    /// <summary>
    /// The paint stands on the carriageway, with somewhere to step off onto at both ends of it and road
    /// down both flanks. It is <see cref="CrosswalkGeometryTests"/>'s question asked of the plan rather
    /// than the file, and it names the ground it found so a failure says what was laid there.
    /// </summary>
    [Fact]
    public void EveryCrossingIsPaintedAcrossACarriageway()
    {
        var plan = Laid;
        var terrain = new TerrainGrid(plan, Config);
        Assert.True(plan.Crosswalks.Count > 0);

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var centreM = plan.Crosswalks.CentreM[crossing];
            var axis = plan.Crosswalks.Axis[crossing];
            var across = new Vector2(-axis.Y, axis.X);
            var beyondM = (plan.Crosswalks.SpanM[crossing] * 0.5f) + plan.CellSizeM;

            Assert.Equal(Ground.Crosswalk, terrain.GroundAt(centreM));
            foreach (var end in (ReadOnlySpan<Vector2>)[centreM + (across * beyondM), centreM - (across * beyondM)])
            {
                Assert.True(
                    terrain.At(end).Walkable && !terrain.At(end).Drivable,
                    $"crossing {crossing} at {centreM} ends at {end} on {terrain.GroundAt(end)}: {Across(terrain, centreM, across)}");
            }
        }
    }

    /// <summary>What the ground is every metre across a crossing, which is what a failure above needs to be readable.</summary>
    static string Across(TerrainGrid terrain, Vector2 centreM, Vector2 across)
    {
        var read = new List<string>();
        for (var atM = -8f; atM <= 8f; atM += 1f) read.Add($"{atM:+0;-0}:{terrain.GroundAt(centreM + (across * atM))}");

        return string.Join(" ", read);
    }

    /// <summary>Which bearings each junction of the laid map has an arm on, read off the roads themselves.</summary>
    static List<List<ExamArm>> ArmsPerJunction()
    {
        var plan = Laid;
        var arms = new List<List<ExamArm>>();
        for (var junction = 0; junction < plan.Junctions.Count; junction++) arms.Add([]);

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var from = plan.Roads.FromJunction[road];
            var to = plan.Roads.ToJunction[road];
            var run = plan.Junctions.CentreM[to] - plan.Junctions.CentreM[from];
            arms[from].Add(Bearing(run));
            arms[to].Add(Bearing(-run));
        }

        return arms;
    }

    static ExamArm Bearing(Vector2 run)
    {
        if (MathF.Abs(run.X) > MathF.Abs(run.Y)) return run.X > 0f ? ExamArm.East : ExamArm.West;

        return run.Y > 0f ? ExamArm.South : ExamArm.North;
    }
}
