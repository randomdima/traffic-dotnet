using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// Where the pedestrian nodes stand, which of them are one place, and the six points each of them hands its
/// ways over at. <b>What is laid between two of those points is
/// <see cref="PedestrianWaysTests"/>'</b>, so nothing here asks about a line.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class PedestrianNodesTests
{
    /// <summary>A merge distance nothing is inside, so every node is a place of its own and no pair welds.</summary>
    const float ApartEnoughM = 0.001f;


    /// <summary>
    /// Enough buildings on the suite's own brief that the generator counts car parks off them (GEN-53) —
    /// the one case here that needs a town with a bay in it.
    /// </summary>
    const int BuildingsWithLots = 48;

    /// <summary>
    /// <b>A pedestrian node stands a setback back from its road's end and its figure off the carriageway</b>
    /// (WLK-2): the two figures the whole network is placed by, measured back off the road's own line.
    /// </summary>
    [Fact]
    public void APedestrianNodeStandsASetbackBackFromItsRoadsEndAndAFigureOffItsCarriageway()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var nodes = FootJunctions.Lay(plan, config);
        var roads = plan.Roads;

        var stood = 0;
        for (var road = 0; road < roads.Count; road++)
        {
            var line = roads.SegmentsOf(road);
            if (line.Length == 0) continue;

            var lengthM = Spline.TotalLengthM(line);
            var asideM = (roads.WidthM[road] * 0.5f) + config.Road.FootNodeAsideM;
            foreach (var atTo in (ReadOnlySpan<bool>)[false, true])
            {
                var end = JunctionArms.End(road, atTo);
                if (!nodes.StandsAt(end)) continue;

                // The clamp is its own claim below, so what is asked here is only of the ends the middle of
                // the road left room for.
                if (config.Road.FootNodeBackM > lengthM * 0.5f) continue;

                stood++;
                var backM = atTo ? lengthM - nodes.AlongM(end) : nodes.AlongM(end);
                Assert.Equal(config.Road.FootNodeBackM, backM, LineTolerance.RoundingM);

                var onTheLineM = nodes.OnTheLineAt(end);
                foreach (var hand in (ReadOnlySpan<int>)[-1, 1])
                {
                    var offM = Vector2.Distance(nodes.AtM(FootJunctions.Node(end, hand)), onTheLineM);
                    Assert.Equal(asideM, offM, LineTolerance.RoundingM);
                }
            }
        }

        Assert.True(stood > 0, "the fixture town stands no pedestrian node at all");
    }

    /// <summary>
    /// <b>A pedestrian node never stands past the middle of its own road</b> (WLK-2): a street the junctions
    /// at either end of it reach more than half way into would otherwise stand its two pairs the wrong way
    /// round, and the walk down it would run backwards.
    /// </summary>
    [Fact]
    public void APedestrianNodeNeverStandsPastTheMiddleOfItsRoad()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var nodes = FootJunctions.Lay(plan, config);
        var roads = plan.Roads;

        var stood = 0;
        for (var road = 0; road < roads.Count; road++)
        {
            var line = roads.SegmentsOf(road);
            if (line.Length == 0) continue;

            var middleM = Spline.TotalLengthM(line) * 0.5f;
            foreach (var atTo in (ReadOnlySpan<bool>)[false, true])
            {
                var end = JunctionArms.End(road, atTo);
                if (!nodes.StandsAt(end)) continue;

                stood++;
                var alongM = nodes.AlongM(end);
                Assert.InRange(alongM, atTo ? middleM : 0f, atTo ? Spline.TotalLengthM(line) : middleM);
            }
        }

        Assert.True(stood > 0, "the fixture town stands no pedestrian node at all");
    }

    /// <summary>
    /// <b>A node is dropped onto its own road's kerb and never onto the arm opposite</b> (WLK-9): the place
    /// it stands off stands the figure it was struck at away from it, which is what a road's own edge is and
    /// what a kerb reached across the crotch of a fork is not.
    /// </summary>
    [Fact]
    public void ANodeIsDroppedOntoItsOwnRoadsKerb()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var connectors = FootConnectors.Lay(plan, config);

        var stood = 0;
        for (var node = 0; node < connectors.NodeCount; node++)
        {
            if (!connectors.StandsAt(node)) continue;

            stood++;
            var offM = Vector2.Distance(connectors.AtM(node), connectors.KerbAtM(node));
            Assert.True(
                MathF.Abs(offM - config.Road.FootNodeAsideM) <= LineTolerance.OnePlaceM,
                $"a node stands {offM:F2} m off the kerb it found rather than " +
                $"{config.Road.FootNodeAsideM:F2} m, at " +
                $"{connectors.AtM(node).X:F0},{connectors.AtM(node).Y:F0}");
        }

        Assert.True(stood > 0, "the fixture town stands no pedestrian node at all");
    }

    /// <summary>
    /// <b>A roundabout's ring stands no pedestrian node and its arms stand theirs</b> (WLK-2): nothing
    /// fronts onto a ring, so there is no walk beside it to stand a corner on — but an arm really does meet
    /// it, so the mouth is a corner like any other.
    /// </summary>
    [Fact]
    public void ARoundaboutsRingStandsNoNodeAndItsArmsStandTheirs()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.City);
        var nodes = FootJunctions.Lay(plan, config);
        var arms = JunctionArms.Of(plan);
        var roads = plan.Roads;

        var circulating = 0;
        var onto = 0;
        foreach (var ring in plan.Roundabouts.Road)
        {
            circulating++;
            foreach (var atTo in (ReadOnlySpan<bool>)[false, true])
            {
                Assert.False(
                    nodes.StandsAt(JunctionArms.End(ring, atTo)),
                    $"a pedestrian node stands on the ring road {ring} of a roundabout");

                // And every ordinary arm meeting the ring there keeps its own.
                var junction = atTo ? roads.ToJunction[ring] : roads.FromJunction[ring];
                if (junction < 0) continue;

                for (var road = 0; road < roads.Count; road++)
                {
                    if (roads.IsABay(road) || arms.Circulates(road)) continue;

                    foreach (var armAtTo in (ReadOnlySpan<bool>)[false, true])
                    {
                        if ((armAtTo ? roads.ToJunction[road] : roads.FromJunction[road]) != junction) continue;

                        onto++;
                        Assert.True(
                            nodes.StandsAt(JunctionArms.End(road, armAtTo)),
                            "no pedestrian node stands where an arm meets the roundabout at " +
                            $"{plan.Junctions.CentreM[junction].X:F0},{plan.Junctions.CentreM[junction].Y:F0}");
                    }
                }
            }
        }

        Assert.True(circulating > 0, "the city has no roundabout");
        Assert.True(onto > 0, "the city's roundabout has no arm onto it");
    }

    /// <summary>
    /// <b>A roundabout's arm reaches no node round its junction</b> (WLK-2): the two ends of the ring stand
    /// between the arm's own two sides, and an arm's mouth is ground the walk goes round rather than ground
    /// it turns at — so neither wedge there bounds a corner.
    /// </summary>
    /// <remarks>
    /// <b>The pair of points is still handed over</b> (WLK-3): what it runs to is missing rather than merged
    /// into this same place, which are two different answers and stay two.
    /// </remarks>
    [Fact]
    public void ARoundaboutsArmReachesNoNodeRoundItsJunction()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.City);
        var nodes = FootJunctions.Lay(plan, config);
        var roads = plan.Roads;
        var arms = JunctionArms.Of(plan);

        var onto = 0;
        foreach (var ring in plan.Roundabouts.Road)
        {
            foreach (var atTo in (ReadOnlySpan<bool>)[false, true])
            {
                var junction = atTo ? roads.ToJunction[ring] : roads.FromJunction[ring];
                if (junction < 0) continue;

                for (var road = 0; road < roads.Count; road++)
                {
                    if (roads.IsABay(road) || arms.Circulates(road)) continue;

                    foreach (var armAtTo in (ReadOnlySpan<bool>)[false, true])
                    {
                        if ((armAtTo ? roads.ToJunction[road] : roads.FromJunction[road]) != junction) continue;

                        var end = JunctionArms.End(road, armAtTo);
                        onto++;
                        foreach (var hand in (ReadOnlySpan<int>)[-1, 1])
                        {
                            Assert.Equal(FootJunctions.NoNode, nodes.WedgeNeighbour(FootJunctions.Node(end, hand)));
                        }
                    }
                }
            }
        }

        Assert.True(onto > 0, "the city's roundabout has no arm onto it");
    }

    /// <summary>
    /// <b>A bay stands no pedestrian node at either of its ends</b> (WLK-2, GEN-53): it is ground a car is put down
    /// on, joined to nothing, and the walk goes round the rank rather than arriving anywhere in it.
    /// </summary>
    [Fact]
    public void ABayStandsNoPedestrianNode()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.LayFresh(Towns.Brief(Towns.CitySeed, buildings: BuildingsWithLots));
        var nodes = FootJunctions.Lay(plan, config);

        Assert.True(plan.CarParks.Road.Length > 0, "the town laid no car park");
        foreach (var road in plan.CarParks.Road)
        {
            Assert.False(nodes.StandsAt(JunctionArms.End(road, atTo: false)), $"a pedestrian node stands at bay {road}'s mouth");
            Assert.False(nodes.StandsAt(JunctionArms.End(road, atTo: true)), $"a pedestrian node stands at bay {road}'s far end");
        }
    }


    /// <summary>
    /// <b>Two pedestrian nodes standing within the merge distance of one another are one place</b>
    /// (WLK-3) — which is the whole of what stops the corner of a crossroads being four places a walk has
    /// to choose between when it is one place to arrive at.
    /// </summary>
    [Fact]
    public void TwoPedestrianNodesWithinTheMergeDistanceAreOnePlace()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var nodes = FootJunctions.Lay(plan, config);
        var mergeM = config.Road.FootNodeMergeM;

        var merged = 0;
        for (var node = 0; node < nodes.NodeCount; node++)
        {
            if (!nodes.StandsAt(node / 2)) continue;

            for (var other = node + 1; other < nodes.NodeCount; other++)
            {
                if (!nodes.StandsAt(other / 2)) continue;

                var apartM = Vector2.Distance(nodes.AtM(node), nodes.AtM(other));
                if (apartM > mergeM) continue;

                merged++;
                Assert.True(
                    nodes.JunctionOf(node) == nodes.JunctionOf(other),
                    $"two pedestrian nodes {apartM:F2} m apart, inside the {mergeM:F2} m they merge at, " +
                    $"are two places at {nodes.AtM(node).X:F0},{nodes.AtM(node).Y:F0}");
            }
        }

        Assert.True(merged > 0, "the fixture town merges no pedestrian node at all");
    }

    /// <summary>
    /// <b>A merged place stands midway between the nodes merged into it, and its nodes' points do not
    /// move</b> (WLK-3): the corner a walk arrives at is one point between the two, and what each of them
    /// hands over at was struck off the boundary and stays where the boundary put it.
    /// </summary>
    [Fact]
    public void AMergedPlaceStandsBetweenItsNodesWhileTheirPointsStayPut()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var nodes = FootJunctions.Lay(plan, config);
        var walks = WalkLines.Of(plan, config);
        var connectors = FootConnectors.Lay(nodes, walks, config);

        // The same points struck with the merge as good as off: what it may move is the place, and a point
        // read either way has to be the same point.
        var alone = FootConnectors.Lay(
            nodes, walks, new SimConfig { Road = new RoadFigures { FootNodeMergeM = ApartEnoughM } });

        var merged = 0;
        var middleM = new Dictionary<int, (Vector2 SumM, int Count)>();
        for (var node = 0; node < connectors.NodeCount; node++)
        {
            if (!connectors.StandsAt(node)) continue;

            var place = connectors.PlaceOf(node);
            var (sumM, count) = middleM.TryGetValue(place, out var so) ? so : (Vector2.Zero, 0);
            middleM[place] = (sumM + nodes.AtM(node), count + 1);

            for (var kind = 0; kind < FootConnectors.Kinds; kind++)
            {
                for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
                {
                    Assert.Equal(
                        0f,
                        Vector2.Distance(
                            connectors.PointM(node, (FootConnectorKind)kind, lane),
                            alone.PointM(node, (FootConnectorKind)kind, lane)),
                        LineTolerance.RoundingM);
                }
            }
        }

        foreach (var (place, (sumM, count)) in middleM)
        {
            if (count > 1) merged++;

            Assert.Equal(
                0f, Vector2.Distance(sumM / count, connectors.PlaceAtM(place)), LineTolerance.RoundingM);
        }

        Assert.True(merged > 0, "the fixture town merges no pedestrian node at all");
    }

    /// <summary>
    /// <b>A pair whose way ran to a node the merge made this same place is dropped, and every other pair is
    /// kept</b> (WLK-3): two nodes at one corner leave a place handing over to four ways and not six,
    /// because the walk that would have joined them joins the corner to itself.
    /// </summary>
    [Fact]
    public void APairThatWouldJoinAPlaceToItselfIsDropped()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var nodes = FootJunctions.Lay(plan, config);
        var connectors = FootConnectors.Lay(nodes, WalkLines.Of(plan, config), config);

        var dropped = 0;
        var kept = 0;
        for (var node = 0; node < connectors.NodeCount; node++)
        {
            if (!connectors.StandsAt(node)) continue;

            var end = node / 2;
            var hand = node % 2 == 1 ? +1 : -1;
            var road = JunctionArms.Road(end);
            Span<int> reaches =
            [
                FootJunctions.Node(end, -hand),
                FootJunctions.Node(JunctionArms.End(road, !JunctionArms.AtTo(end)), -hand),
                nodes.WedgeNeighbour(node),
            ];

            for (var kind = 0; kind < FootConnectors.Kinds; kind++)
            {
                var reached = reaches[kind];
                var joinsItself = reached >= 0 && connectors.StandsAt(reached)
                                               && connectors.PlaceOf(reached) == connectors.PlaceOf(node);

                Assert.Equal(!joinsItself, connectors.HandsOver(node, (FootConnectorKind)kind));
                if (joinsItself) dropped++;
                else kept++;
            }
        }

        Assert.True(dropped > 0, "the fixture town drops no pair at all");
        Assert.True(kept > 0, "the fixture town keeps no pair at all");
    }

    /// <summary>
    /// <b>A point standing on the point its lane arrives at is welded into one point midway between the
    /// two</b> (WLK-12): what was a stride of lane between two places becomes one place, and both ends of it
    /// stand there.
    /// </summary>
    [Fact]
    public void APointStandingOnThePointItsLaneArrivesAtIsOnePointMidwayBetweenThem()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var nodes = FootJunctions.Lay(plan, config);
        var walks = WalkLines.Of(plan, config);
        var welded = FootConnectors.Lay(nodes, walks, config);

        // The same points with the weld as good as off, which is where each of them stood before it moved.
        var apart = FootConnectors.Lay(
            nodes, walks, new SimConfig { Road = new RoadFigures { FootConnectorMergeM = ApartEnoughM } });

        var shared = 0;
        for (var node = 0; node < welded.NodeCount; node++)
        {
            if (!welded.StandsAt(node)) continue;

            for (var which = 0; which < FootConnectors.Kinds; which++)
            {
                var kind = (FootConnectorKind)which;
                for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
                {
                    if (!welded.Shares(node, kind, lane)) continue;

                    shared++;
                    var onto = welded.Reaches(node, kind);
                    Assert.True(welded.Shares(onto, kind, lane));

                    var middleM = (apart.PointM(node, kind, lane) + apart.PointM(onto, kind, lane)) * 0.5f;
                    Assert.Equal(
                        0f, Vector2.Distance(welded.PointM(node, kind, lane), middleM),
                        LineTolerance.RoundingM);
                    Assert.Equal(
                        0f, Vector2.Distance(welded.PointM(onto, kind, lane), middleM),
                        LineTolerance.RoundingM);
                }
            }
        }

        Assert.True(shared > 0, "the fixture town welds no point at all");
    }

    /// <summary>
    /// <b>Every lane still walked stands further from the point it arrives at than the weld</b> (WLK-12):
    /// the rule read the other way round on the town's own figure, which is the whole of what says a lane
    /// laid between two points has a length to it.
    /// </summary>
    [Fact]
    public void NoLaneNearerThanTheWeldIsStillWalked()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var connectors = FootConnectors.Lay(
            FootJunctions.Lay(plan, config), WalkLines.Of(plan, config), config);

        var kept = 0;
        for (var node = 0; node < connectors.NodeCount; node++)
        {
            if (!connectors.StandsAt(node)) continue;

            for (var which = 0; which < FootConnectors.Kinds; which++)
            {
                var kind = (FootConnectorKind)which;
                if (!connectors.HandsOver(node, kind) || connectors.Reaches(node, kind) < 0) continue;

                for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
                {
                    if (connectors.Shares(node, kind, lane)) continue;

                    kept++;
                    var apartM = connectors.ApartM(node, kind, lane);
                    Assert.True(
                        apartM > config.Road.FootConnectorMergeM,
                        $"a {kind} lane {apartM:F2} m from the point it arrives at is still walked at " +
                        $"{connectors.PointM(node, kind, lane).X:F0},{connectors.PointM(node, kind, lane).Y:F0}");
                }
            }
        }

        Assert.True(kept > 0, "the fixture town walks no lane at all");
    }

    /// <summary>
    /// <b>A node's crossing pair stands on the driven ground's own boundary</b> (WLK-9): the way over the
    /// carriageway sets off from the kerb, so the two points it sets off from are on the kerb and not a
    /// pavement's width behind it.
    /// </summary>
    [Fact]
    public void ACrossingPairStandsOnTheBoundary()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var walks = WalkLines.Of(plan, config);
        var kerbs = walks.Boundary;
        var connectors = FootConnectors.Lay(FootJunctions.Lay(plan, config), walks, config);

        var stood = 0;
        var worstM = 0f;
        var atM = Vector2.Zero;
        for (var node = 0; node < connectors.NodeCount; node++)
        {
            if (!connectors.StandsAt(node)) continue;

            stood++;
            for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
            {
                var pointM = connectors.PointM(node, FootConnectorKind.Crossing, lane);
                if (!kerbs.NearestTo(pointM, out var at)) continue;

                var offM = Vector2.Distance(pointM, at.PositionM);
                if (offM <= worstM) continue;

                worstM = offM;
                atM = pointM;
            }
        }

        Assert.True(stood > 0, "the fixture town stands no pedestrian node at all");
        Assert.True(
            worstM <= LineTolerance.JoinedM,
            $"a crossing's point stands {worstM:F3} m off the boundary at {atM.X:F0},{atM.Y:F0}");
    }

    /// <summary>
    /// <b>A road's or a junction's pair stands on the two courses its lanes are walked on</b> (WLK-9): the
    /// point is struck off the boundary and then dropped onto the course, so the lane laid between two of
    /// them is a stretch of that line and needs nothing fitted to its ends (WLK-11).
    /// </summary>
    /// <remarks>
    /// <b>Asked of the pairs the placement left where it put them</b>, which is every pair no weld has moved
    /// a point of (WLK-12): a lane that pinched out was carried onto the place it meets its own far end at,
    /// and where that leaves the pair is the weld's answer rather than this rule's.
    /// </remarks>
    [Fact]
    public void ARoadOrJunctionPairStandsOnTheCoursesItsLanesAreWalkedOn()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var walks = WalkLines.Of(plan, config);
        var connectors = FootConnectors.Lay(
            FootJunctions.Lay(plan, config), walks,
            new SimConfig { Road = new RoadFigures { FootConnectorMergeM = ApartEnoughM } });

        var stood = 0;
        for (var node = 0; node < connectors.NodeCount; node++)
        {
            if (!connectors.StandsAt(node)) continue;

            foreach (var kind in (ReadOnlySpan<FootConnectorKind>)[FootConnectorKind.Road, FootConnectorKind.Junction])
            {
                for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
                {
                    stood++;
                    var pointM = connectors.PointM(node, kind, lane);
                    Assert.True(walks.Course(lane).NearestTo(pointM, out var at), "the course answered nowhere");
                    Assert.True(
                        Vector2.Distance(pointM, at.PositionM) <= LineTolerance.JoinedM,
                        $"a {kind} point of lane {lane} stands " +
                        $"{Vector2.Distance(pointM, at.PositionM):F3} m off its own course at " +
                        $"{pointM.X:F0},{pointM.Y:F0}");
                }
            }
        }

        Assert.True(stood > 0, "the fixture town stands no road or junction pair at all");
    }

    /// <summary>
    /// <b>The road pair stands down the road along the kerb and the junction pair the other way</b>
    /// (WLK-9): which side of the node a pair is on is what tells the walk down the street from the walk
    /// round the corner.
    /// </summary>
    /// <remarks>
    /// <b>Read along the kerb and not along the road</b>. The two part company at a mouth, which is the
    /// whole reason the rule is stated off the boundary: a fillet turns the kerb into the corner while the
    /// arm's centreline runs straight on, so a point a setback round that corner is barely behind the node
    /// as the <em>road</em> measures it and squarely behind it as the kerb does.
    /// <para>
    /// <b>And read at the kerb the node found</b> (<see cref="FootConnectors.KerbAtM"/>) rather than at the
    /// boundary nearest the node, which in the crotch of a fork is a different kerb and so a different way
    /// round (WLK-9).
    /// </para>
    /// </remarks>
    [Fact]
    public void TheRoadPairStandsDownTheKerbAndTheJunctionPairBackAlongIt()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var nodes = FootJunctions.Lay(plan, config);
        var walks = WalkLines.Of(plan, config);
        var kerbs = walks.Boundary;
        var connectors = FootConnectors.Lay(nodes, walks, config);

        var stood = 0;
        for (var node = 0; node < connectors.NodeCount; node++)
        {
            if (!connectors.StandsAt(node) || !kerbs.NearestTo(connectors.KerbAtM(node), out var at)) continue;

            stood++;
            var outward = nodes.OutwardUnitAt(node / 2);
            var downTheKerb = Vector2.Dot(at.Direction, outward) >= 0f ? at.Direction : -at.Direction;
            var atM = at.PositionM;
            var roadM = Along(kerbs, connectors.PointM(node, FootConnectorKind.Road, 0), atM, downTheKerb);
            var junctionM = Along(kerbs, connectors.PointM(node, FootConnectorKind.Junction, 0), atM, downTheKerb);

            Assert.True(
                roadM > 0f && junctionM < 0f,
                $"the node at {atM.X:F0},{atM.Y:F0} reaches {roadM:F2} m down its kerb and {junctionM:F2} m " +
                "back along it");
        }

        Assert.True(stood > 0, "the fixture town stands no pedestrian node at all");
    }

    /// <summary>
    /// How far down the kerb from the node a point stands: <b>its own place on the boundary</b> and not the
    /// point itself, so the lane it is offset by does not read as a step along the line.
    /// </summary>
    static float Along(KerbLines kerbs, Vector2 pointM, Vector2 fromM, Vector2 downTheKerb) =>
        kerbs.NearestTo(pointM, out var at) ? Vector2.Dot(at.PositionM - fromM, downTheKerb) : 0f;
}
