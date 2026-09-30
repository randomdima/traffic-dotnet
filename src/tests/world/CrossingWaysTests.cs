using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The junction each end of a town's zebras is cut into the walk as, and the paint between them (WLK-15,
/// <see cref="CrossingWays"/>). Where the paint itself goes is <c>CrossingsTests</c>', and what the walk is
/// laid beside is <see cref="PedestrianWaysTests"/>'.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class CrossingWaysTests
{
    /// <summary>
    /// <b>A crossing's paint runs from the kerb to the kerb</b> (WLK-15): what a zebra is painted over is the
    /// carriageway, whose edge is the driven ground's own boundary (TER-7b) — so the stretch a walker's
    /// secondary claims hold of the road ends where the tarmac does, and what carries on from there is the
    /// junction's.
    /// </summary>
    /// <remarks>
    /// <b>Weighed at the figure two pieces are one line at</b> (<see cref="LineTolerance.JoinedM"/>): the
    /// point was struck by a search for the nearest place on a chain and is read back by the same search, so
    /// it stands a hair off itself, and what the claim is about is which line it stands on.
    /// </remarks>
    [Fact]
    public void ACrossingsPaintRunsFromTheKerbToTheKerb()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var pavement = PavementLanes.Of(plan, config);
        var ways = CrossingWays.Of(plan, pavement, config);
        var boundary = KerbLines.Of(plan.Paving(config).Perimeter(config), config);

        var paint = 0;
        var worstM = 0f;
        foreach (var way in ways.Ways)
        {
            if (way.Kind != FootEdgeKind.Crossing) continue;

            paint++;
            worstM = MathF.Max(worstM, OffM(boundary, way.FromM));
            worstM = MathF.Max(worstM, OffM(boundary, way.OntoM));
        }

        // The staging and not the claim: a fixture the walk crosses nowhere would weigh nothing, and pass.
        Assert.True(paint > 0, "nothing in the fixture is crossed");
        Assert.InRange(worstM, 0f, LineTolerance.JoinedM);
    }

    /// <summary>
    /// <b>A crossing is set off from every lane of the walk beside it and arrives on every one</b> (WLK-15):
    /// a junction hands over at a point per connected lane, and its connections run from each of them to the
    /// paint and from the paint to each of them — so a walker on either lane of a pavement may cross, and one
    /// off the paint may take either.
    /// </summary>
    /// <remarks>
    /// <b>Which lane a connection came from is read off the lines themselves</b> and not off how far its
    /// point stands from the kerb: a course is the whole shape's own offset (WLK-1), so at a corner it
    /// stands nearer or further off the boundary than the figure it was struck at, while the point still
    /// stands on the line it was dropped onto.
    /// </remarks>
    [Fact]
    public void ACrossingIsSetOffFromEveryLaneOfTheWalkAndArrivesOnEveryOne()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var pavement = PavementLanes.Of(plan, config);
        var foot = FootGraph.Build(pavement, CrossingWays.Of(plan, pavement, config), config);
        var courses = Courses(pavement, config);
        var every = new List<int>();
        for (var lane = 0; lane < pavement.Count; lane++) every.Add(lane);

        var asked = 0;
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            if (foot.KindOf(edge) != FootEdgeKind.Crossing) continue;

            asked++;
            Assert.Equal(every, LanesAt(foot, courses, foot.EdgesIn(foot.FromNode(edge)), leaving: false));
            Assert.Equal(every, LanesAt(foot, courses, foot.EdgesOut(foot.ToNode(edge)), leaving: true));
        }

        // The staging and not the claim: a fixture the walk crosses nowhere would ask nothing, and pass.
        Assert.True(asked > 0, "nothing in the fixture is crossed");
    }

    /// <summary>
    /// <b>No connection turns a walk back down the lane beside the one it came in on</b> (WLK-13, WLK-15):
    /// a pavement's two lanes are walked opposite ways (WLK-8), so a way from one of them to the other at the
    /// same junction is a walker arriving and at once retracing their steps — which is turning round and not
    /// a turn. <b>No connection stands both of its ends on the walk</b>, and the two lanes are joined across
    /// the carriageway instead.
    /// </summary>
    /// <remarks>
    /// The other connections a junction lays each stand one end on the walk and one on the kerb — onto the
    /// paint or off it — or, where two crossings were merged into one place (WLK-3), both ends on the kerb,
    /// which is one zebra handing over to the next.
    /// </remarks>
    [Fact]
    public void NoConnectionTurnsAWalkBackDownTheLaneBesideIt()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var pavement = PavementLanes.Of(plan, config);
        var ways = CrossingWays.Of(plan, pavement, config);
        var courses = Courses(pavement, config);

        var asked = 0;
        foreach (var way in ways.Ways)
        {
            if (way.Kind != FootEdgeKind.Pavement) continue;

            asked++;
            var onTheWalk = 0;
            foreach (var endM in (ReadOnlySpan<Vector2>)[way.FromM, way.OntoM])
            {
                if (LaneOf(courses, endM) >= 0) onTheWalk++;
            }

            Assert.True(
                onTheWalk < 2,
                $"the connection at {way.FromM.X:F0},{way.FromM.Y:F0} stands both of its ends on the walk, "
                + "so it runs between two lanes of one pavement");
        }

        // The staging and not the claim: a fixture the walk crosses nowhere would ask nothing, and pass.
        Assert.True(asked > 0, "nothing in the fixture is crossed");
    }

    /// <summary>
    /// <b>Two crossings merged into one junction hand over to each other</b> (WLK-3, WLK-15): their mouths
    /// stand within a merge of one another, so they are one place — and what runs between them is the
    /// junction's own connection rather than a stride of pavement with a hand-over at each end of it.
    /// </summary>
    /// <remarks>
    /// <b>Told from the other connections by standing no end on the walk</b>: every other one a junction
    /// lays runs between a course and the kerb, and this one runs from the kerb to the kerb.
    /// </remarks>
    [Fact]
    public void TwoCrossingsMergedIntoOneJunctionHandOverToEachOther()
    {
        var config = new SimConfig { Road = new RoadFigures { FootNodeMergeM = MergedWithinM } };
        var plan = Towns.Of(Towns.Fixture);
        var pavement = PavementLanes.Of(plan, config);
        var foot = FootGraph.Build(pavement, CrossingWays.Of(plan, pavement, config), config);
        var boundary = KerbLines.Of(plan.Paving(config).Perimeter(config), config);

        // Nearer the tarmac than the nearest lane of the walk stands off it, which is what tells a point on
        // the kerb from one on a course whatever the graph's weld moved it by.
        var kerbM = config.WalkingLaneAtM(0) * 0.5f;
        var asked = 0;
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            if (foot.KindOf(edge) != FootEdgeKind.Pavement) continue;
            if (OffM(boundary, foot.AnchorM(foot.FromNode(edge))) > kerbM) continue;
            if (OffM(boundary, foot.AnchorM(foot.ToNode(edge))) > kerbM) continue;

            asked++;
            Assert.True(
                Crossings(foot, foot.EdgesIn(foot.FromNode(edge))) > 0
                && Crossings(foot, foot.EdgesOut(foot.ToNode(edge))) > 0,
                $"the connection at {foot.AnchorM(foot.FromNode(edge)).X:F0},"
                + $"{foot.AnchorM(foot.FromNode(edge)).Y:F0} stands on no walk and joins no two crossings");
        }

        // The staging and not the claim: a fixture with no two crossings near each other would ask nothing.
        Assert.True(asked > 0, $"no two of the fixture's crossings stand within {MergedWithinM:F0} m");
    }

    /// <summary>
    /// The figure the fixture is read at, wide enough that some of its crossings stand at one junction
    /// (<see cref="RoadFigures.FootNodeMergeM"/>).
    /// </summary>
    const float MergedWithinM = 12f;

    /// <summary>
    /// <b>No connection goes beyond the pavement</b> (WLK-15): the band of every connection a junction lays — onto
    /// the paint, off it, and from one crossing onto another — stands on ground the town answers as something other
    /// than the verge, a touch in from its edges. <b>Asked at crossings merged from as far apart as the fixture
    /// carries</b>, where the lines of two paints meet furthest out.
    /// </summary>
    [Fact]
    public void NoConnectionGoesBeyondThePavement()
    {
        var config = new SimConfig { Road = new RoadFigures { FootNodeMergeM = MergedWithinM } };
        var plan = Towns.Of(Towns.Fixture);
        var ground = new GroundShapes(plan.Paving(config), config);
        var ways = CrossingWays.Of(plan, PavementLanes.Of(plan, config), config);

        var inM = (config.WalkingLaneWidthM * 0.5f) - config.RibbonTouchM;
        var asked = 0;
        foreach (var way in ways.Ways)
        {
            if (way.Kind != FootEdgeKind.Pavement) continue;

            asked++;
            var lengthM = Spline.TotalLengthM(way.Arcs);
            for (var atM = 0f; atM <= lengthM; atM += config.RibbonTouchM)
            {
                var at = Spline.SampleAt(way.Arcs, atM);
                foreach (var edgeM in (ReadOnlySpan<Vector2>)[at.PositionM + (at.Right * inM), at.PositionM - (at.Right * inM)])
                {
                    Assert.True(
                        ground.At(edgeM) is not (Ground.Grass or Ground.Water),
                        $"the connection from {way.FromM.X:F1},{way.FromM.Y:F1} to {way.OntoM.X:F1},{way.OntoM.Y:F1} "
                        + $"stands on {ground.At(edgeM)} at {edgeM.X:F1},{edgeM.Y:F1}");
                }
            }
        }

        // The staging and not the claim: a fixture whose walk crosses nothing would ask nothing.
        Assert.True(asked > 0, "the fixture's crossings lay no connection");
    }

    /// <summary>
    /// <b>A course out of reach costs its own lane and not the crossing</b> (WLK-15): a junction hands over
    /// at a point per connected lane, so a lane whose course answers from further off than
    /// <see cref="RoadFigures.CrossingMeetsTheWalkWithinM"/> is left out of the place — which still stands,
    /// and whose paint is still laid over the road.
    /// </summary>
    /// <remarks>
    /// <b>Read at a reach no lane of the fixture can answer within</b>, which is what makes every junction on
    /// it the case at once. A lane really standing out of reach is a corner the move's own rounding cut the
    /// course away from (TER-3c.10), which is a town's geometry rather than a figure, and how many a town has
    /// is the census's to report.
    /// </remarks>
    [Fact]
    public void ACourseOutOfReachCostsItsOwnLaneAndNotTheCrossing()
    {
        var shipped = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var pavement = PavementLanes.Of(plan, shipped);
        var reached = CrossingWays.Of(plan, pavement, shipped);

        var config = new SimConfig { Road = new RoadFigures { CrossingMeetsTheWalkWithinM = OutOfReachM } };
        var ways = CrossingWays.Of(plan, pavement, config);
        var courses = Courses(pavement, shipped);

        Assert.Equal(reached.Junctions, ways.Junctions);
        Assert.Equal(Paint(reached), Paint(ways));
        Assert.Equal(0, ways.Refused);
        Assert.Equal(ways.Junctions * pavement.Count, ways.Unreached);
        foreach (var way in ways.Ways)
        {
            if (way.Kind == FootEdgeKind.Crossing) continue;

            Assert.True(
                LaneOf(courses, way.FromM) < 0 && LaneOf(courses, way.OntoM) < 0,
                $"the connection at {way.FromM.X:F0},{way.FromM.Y:F0} runs to a walk standing further off "
                + $"than the {OutOfReachM:F1} m this was read at");
        }
    }

    /// <summary>
    /// A reach no lane of a pavement answers within: a junction stands half the walk's own width off the
    /// kerb, so every course of it passes half a lane away — further than this — while the paint's own end
    /// still stands on the boundary and the mouths are unmoved.
    /// </summary>
    const float OutOfReachM = 0.5f;

    /// <summary>How many stretches of a town's crossings are paint, which is two to a zebra that was laid.</summary>
    static int Paint(CrossingWays ways)
    {
        var paint = 0;
        foreach (var way in ways.Ways)
        {
            if (way.Kind == FootEdgeKind.Crossing) paint++;
        }

        return paint;
    }

    /// <summary>
    /// <b>Parting the walk for a junction keeps every metre of it</b> (WLK-15): the walk beside a road is the
    /// line the move laid and a junction is cut into it, so what the cut adds is the places it hands over at
    /// — the same ground walked as more lanes, plus the junction's own connections.
    /// </summary>
    /// <remarks>
    /// <b>Weighed against the same town laid without them</b> rather than against the rings, because what is
    /// being asked is what the cut did: a reading that lost a stretch of kerb or laid one twice parts
    /// company with the town that has no crossings on it by exactly that much.
    /// </remarks>
    [Fact]
    public void PartingTheWalkForAJunctionKeepsEveryMetreOfIt()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var pavement = PavementLanes.Of(plan, config);
        var ways = CrossingWays.Of(plan, pavement, config);

        var uncut = FootGraph.Build(pavement, CrossingWays.None, config);
        var crossed = FootGraph.Build(pavement, ways, config);

        // The lines themselves and not the straights between their ends: a connection is the curve that joins
        // two poses (WLK-14), so it covers more ground than the chord it bridges.
        var waysM = 0d;
        foreach (var way in ways.Ways) waysM += Spline.TotalLengthM(way.Arcs);

        Assert.True(crossed.EdgeCount > uncut.EdgeCount, "the crossings parted nothing");
        Assert.InRange(Math.Abs(Walked(crossed) - Walked(uncut) - waysM), 0d, LineTolerance.JoinedM);
    }

    /// <summary>
    /// <b>A junction's connections carry the walk on at both of their ends</b> (WLK-14, WLK-15): each leaves
    /// the lane it sets off from along that lane's own heading and joins the one it arrives on along its, so
    /// a walker steps onto a crossing and off it without pivoting — and what no curve would join that way is
    /// not laid at all.
    /// </summary>
    /// <remarks>
    /// <b>Read off the graph and not off the construction</b>: the question is what a body would be held on,
    /// so it is asked of the lines the town hands out, at the nodes they really meet at. <b>Against a line at
    /// the place and not against all of them</b> — where the walk turns a corner within a weld of a hand-over
    /// point the place carries two headings, and the corner there is the pavement's own.
    /// </remarks>
    [Fact]
    public void AJunctionsConnectionsCarryTheWalkOnAtBothOfTheirEnds()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var pavement = PavementLanes.Of(plan, config);
        var ways = CrossingWays.Of(plan, pavement, config);
        var foot = FootGraph.Build(pavement, ways, config);
        var weldM = config.Network.FootGraphNodeWeldM;
        var nodes = Nodes(foot, weldM);

        var asked = 0;
        foreach (var way in ways.Ways)
        {
            var lengthM = Spline.TotalLengthM(way.Arcs);
            foreach (var end in (ReadOnlySpan<float>)[0f, lengthM])
            {
                var atM = Spline.SampleAt(way.Arcs, end).PositionM;
                var runsRad = Spline.SampleAt(way.Arcs, end).HeadingRad;
                var node = At(nodes, foot, atM, weldM);
                Assert.True(node >= 0, $"nothing in the graph stands at {atM.X:F0},{atM.Y:F0}");

                asked++;
                var worstRad = float.MaxValue;
                foreach (var headingRad in Headings(foot, node))
                {
                    worstRad = MathF.Min(worstRad, MathF.Abs(Spline.WrapRad(headingRad - runsRad)));
                }

                Assert.True(
                    worstRad <= LineTolerance.StraightOnRad,
                    $"nothing at {atM.X:F0},{atM.Y:F0} runs the way the line there does, the nearest being "
                    + $"{worstRad * 180f / MathF.PI:F1} degrees off it");
            }
        }

        // The staging and not the claim: a fixture the walk crosses nowhere would weigh nothing, and pass.
        Assert.True(asked > 0, "nothing in the fixture is crossed");
    }

    /// <summary>The headings every line at one node runs there, arriving and leaving alike.</summary>
    static List<float> Headings(FootGraph foot, int node)
    {
        var headingRad = new List<float>();
        foreach (var edge in foot.EdgesOut(node)) headingRad.Add(Spline.SampleAt(foot.ArcsOf(edge), 0f).HeadingRad);

        foreach (var edge in foot.EdgesIn(node))
        {
            headingRad.Add(Spline.SampleAt(foot.ArcsOf(edge), foot.LengthM(edge)).HeadingRad);
        }

        return headingRad;
    }

    /// <summary>The graph's nodes over a grid, so a place can be looked up by where it stands.</summary>
    static Dictionary<(int X, int Y), List<int>> Nodes(FootGraph foot, float cellM)
    {
        var over = new Dictionary<(int X, int Y), List<int>>();
        for (var node = 0; node < foot.NodeCount; node++)
        {
            var cell = Cell(foot.AnchorM(node), cellM);
            if (!over.TryGetValue(cell, out var here)) over[cell] = here = [];

            here.Add(node);
        }

        return over;
    }

    /// <summary>The node standing at one place, or -1 where the graph has none there.</summary>
    static int At(Dictionary<(int X, int Y), List<int>> nodes, FootGraph foot, Vector2 pointM, float weldM)
    {
        var cell = Cell(pointM, weldM);
        for (var y = -1; y <= 1; y++)
        {
            for (var x = -1; x <= 1; x++)
            {
                if (!nodes.TryGetValue((cell.X + x, cell.Y + y), out var here)) continue;

                foreach (var node in here)
                {
                    if (Vector2.Distance(foot.AnchorM(node), pointM) <= weldM) return node;
                }
            }
        }

        return -1;
    }

    static (int X, int Y) Cell(Vector2 pointM, float cellM) =>
        ((int)MathF.Floor(pointM.X / cellM), (int)MathF.Floor(pointM.Y / cellM));

    /// <summary>
    /// Which lanes of the pavement a set of connections stands on at its far end, in order and each once —
    /// which is what says a crossing is reached from every one of them.
    /// </summary>
    /// <remarks>
    /// <b>What stands on no lane is left out</b>: at a junction two crossings were merged into (WLK-3) one
    /// of them hands over to the other on the boundary, which is a connection to a mouth rather than to a
    /// walk.
    /// </remarks>
    static List<int> LanesAt(FootGraph foot, KerbLines[] courses, ReadOnlySpan<int> edges, bool leaving)
    {
        var lanes = new List<int>();
        foreach (var edge in edges)
        {
            var lane = LaneOf(courses, foot.AnchorM(leaving ? foot.ToNode(edge) : foot.FromNode(edge)));
            if (lane >= 0 && !lanes.Contains(lane)) lanes.Add(lane);
        }

        lanes.Sort();
        return lanes;
    }

    /// <summary>The lane of pavement one point stands on, or -1 where it stands on none of them.</summary>
    static int LaneOf(KerbLines[] courses, Vector2 pointM)
    {
        for (var lane = 0; lane < courses.Length; lane++)
        {
            if (OffM(courses[lane], pointM) <= LineTolerance.JoinedM) return lane;
        }

        return -1;
    }

    /// <summary>The town's pavement as the lines a place is dropped onto, which is how a junction meets it.</summary>
    static KerbLines[] Courses(PavementLanes pavement, SimConfig config)
    {
        var courses = new KerbLines[pavement.Count];
        for (var lane = 0; lane < pavement.Count; lane++)
        {
            courses[lane] = KerbLines.Of(pavement.RingsOf(lane), pavement.LooseOf(lane), config);
        }

        return courses;
    }

    /// <summary>How many of a set of stretches are paint, which is what a junction's own connections join.</summary>
    static int Crossings(FootGraph foot, ReadOnlySpan<int> edges)
    {
        var crossings = 0;
        foreach (var edge in edges)
        {
            if (foot.KindOf(edge) == FootEdgeKind.Crossing) crossings++;
        }

        return crossings;
    }

    /// <summary>How far one point stands off a line, which is nought where it was struck on it.</summary>
    static float OffM(KerbLines line, Vector2 pointM) =>
        line.NearestTo(pointM, out var on) ? Vector2.Distance(on.PositionM, pointM) : float.PositiveInfinity;

    /// <summary>
    /// How much walk a graph holds, every lane of it, which is what a cut may not change. <b>Summed at
    /// double precision</b>: a town's walk runs to tens of kilometres over thousands of lanes, and the
    /// figure the two readings are weighed apart at is a centimetre.
    /// </summary>
    static double Walked(FootGraph foot)
    {
        var lengthM = 0d;
        for (var edge = 0; edge < foot.EdgeCount; edge++) lengthM += foot.LengthM(edge);

        return lengthM;
    }
}
