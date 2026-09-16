using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// Where every lane in the town begins and ends (TER-5d), and the one property the whole arrangement rests
/// on: <b>a point is a function of the seed and the link and of nothing else</b>, so the generator laying a
/// town and <see cref="Paving"/> deriving its lanes off that town read back draw the same points.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class ConnectionPointTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// <b>Every point stands at the standoff from its own node</b>, on the line square to the bearing its
    /// arm was drawn with, half a lane off the middle of it (GEN-15). That is the whole of where a point is,
    /// and it is what the disc the junction is drawn on is sized from rather than the other way round.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryPointStandsAStandoffOutAndHalfALaneAcross(string map)
    {
        var ground = Towns.Of(map).Ground;

        Span<ConnectionPoint> points = stackalloc ConnectionPoint[ConnectionPoints.MostPerArm];
        for (var road = 0; road < ground.Roads.Count; road++)
        {
            foreach (var atFrom in (bool[])[true, false])
            {
                var arm = ConnectionPoints.ArmOf(ground, Config, road, atFrom);

                // <b>An arm whose lead bends stands its points a chord out and not a standoff</b> — a ring
                // arm, which is a piece of the circle GEN-19 sized rather than a straight off its tangent,
                // and an arm read off a road a junction was cut into on a bend (GEN-52). Both are that arm's
                // own construction and neither is this rule's subject.
                if (arm.Curvature != 0f) continue;

                var count = ConnectionPoints.At(ground, Config, arm, road, points);
                for (var point = 0; point < count; point++)
                {
                    var off = points[point].AtM - arm.NodeM;
                    Assert.Equal(Config.CityGen.ConnectionStandoffM, Vector2.Dot(off, arm.OutwardUnit), 3);

                    var across = Vector2.Dot(off, Heading.RightOf(arm.OutwardUnit));
                    Assert.Equal(Config.LaneOffsetM, MathF.Abs(across), 3);
                }
            }
        }
    }

    /// <summary>
    /// <b>A two-way road ends one lane each way and a one-way road ends one lane</b> (TER-4d), and the two
    /// of a two-way arm stand either side of its line — which is what makes the offset a fact about the
    /// direction a car is driving rather than about the road's own.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void AnArmCarriesOnePointForEachWayItsRoadIsDriven(string map)
    {
        var ground = Towns.Of(map).Ground;

        Span<ConnectionPoint> points = stackalloc ConnectionPoint[ConnectionPoints.MostPerArm];
        for (var road = 0; road < ground.Roads.Count; road++)
        {
            foreach (var atFrom in (bool[])[true, false])
            {
                var arm = ConnectionPoints.ArmOf(ground, Config, road, atFrom);
                var count = ConnectionPoints.At(ground, Config, arm, road, points);

                Assert.Equal(ground.Roads.LanesOn(road), count);
                if (count < 2) continue;

                Assert.NotEqual(points[0].End, points[1].End);
                Assert.Equal(-1f, Vector2.Dot(points[0].DrivenUnit, points[1].DrivenUnit), 3);
            }
        }
    }

    /// <summary>
    /// <b>A one-way street's point stands where the driven half puts it</b> (TER-4d,
    /// <c>RoadStage.OntoTheDrivenHalf</c>) and not on the node's own line: the street is a lane wide and
    /// sits on the half of the corridor its traffic drives, which is the same half a lane, off the same
    /// side, that a two-way street carries that direction's lane at.
    /// </summary>
    [Fact]
    public void AOneWayStreetsPointStandsOnTheHalfItIsDriven()
    {
        var ground = Towns.Of(Towns.City).Ground;

        Span<ConnectionPoint> points = stackalloc ConnectionPoint[ConnectionPoints.MostPerArm];
        var oneWays = 0;
        for (var road = 0; road < ground.Roads.Count; road++)
        {
            if (ground.Roads.Flow[road] == RoadFlow.BothWays) continue;

            var arm = ConnectionPoints.ArmOf(ground, Config, road, atFrom: true);

            // <b>A ring arc is not a street the scatter took</b> (GEN-18, GEN-19): the whole of a circle is
            // one direction laid at one place, and its lane is the circle itself rather than a half of a
            // carriageway — so it stands on no half and is none of this rule's business.
            if (arm.OnTheLine) continue;

            oneWays++;
            Assert.Equal(1, ConnectionPoints.At(ground, Config, arm, road, points));

            var across = Vector2.Dot(points[0].AtM - arm.StandM, Heading.RightOf(points[0].DrivenUnit));
            Assert.Equal(Config.LaneOffsetM * Config.RoadSideSign, across, 3);
        }

        Assert.True(oneWays > 0, "the suite's own city scattered no one-way street to ask about");
    }

    /// <summary>
    /// <b>A bridge and a ring arc take their bearings rather than drawing one</b> (GEN-14a, GEN-19). Both
    /// are shapes settled before anything here runs, so a deck's own bridgehead may not be jittered off it —
    /// a bridge leaves square along its chord and a ring piece square to the circle it was sized as.
    /// </summary>
    [Fact]
    public void ABridgeLeavesAlongItsOwnChordAndIsNotJittered()
    {
        var ground = Towns.Of(Towns.City).Ground;
        Assert.True(ground.Bridges.Count > 0, "the suite's own city bridged nothing to ask about");

        foreach (var road in ground.Bridges.Road)
        {
            var fromM = ground.Junctions.CentreM[ground.Roads.FromJunction[road]];
            var toM = ground.Junctions.CentreM[ground.Roads.ToJunction[road]];
            var chord = Vector2.Normalize(toM - fromM);

            Assert.Equal(1f, Vector2.Dot(ConnectionPoints.ArmOf(ground, Config, road, true).OutwardUnit, chord), 3);
            Assert.Equal(-1f, Vector2.Dot(ConnectionPoints.ArmOf(ground, Config, road, false).OutwardUnit, chord), 3);
        }
    }

    /// <summary>
    /// And a ring piece leaves square to its own radius, which is what keeps the arms of a roundabout on the
    /// circle GEN-19 sized rather than on a bearing somebody drew beside it.
    /// </summary>
    /// <remarks>
    /// <b>Asked of a ring laid here rather than of one a town happened to open.</b> Whether a brief's town
    /// has a roundabout on it is a fact about that brief, and the rule under test is about the shape: five
    /// nodes on one circle, each joined to the next, is the whole of what a ring is.
    /// </remarks>
    [Fact]
    public void ARingArcLeavesSquareToItsOwnRadius()
    {
        const int nodes = 5;
        var centreM = new Vector2(400f, 300f);
        const float radiusM = 30f;

        var ground = ARingOf(nodes, centreM, radiusM);
        for (var road = 0; road < nodes; road++)
        {
            var arm = ConnectionPoints.ArmOf(ground, Config, road, atFrom: true);
            var radius = Vector2.Normalize(arm.NodeM - centreM);

            Assert.Equal(0f, Vector2.Dot(arm.OutwardUnit, radius), 3);

            // And round the circle the way the piece is driven, rather than back against it.
            var onward = ground.Junctions.CentreM[ground.Roads.ToJunction[road]] - arm.NodeM;
            Assert.True(Vector2.Dot(arm.OutwardUnit, onward) > 0f, $"ring piece {road} leaves the wrong way round");
        }
    }

    /// <summary>
    /// Nodes evenly round a circle, each joined to the next by <b>one arc of that circle</b>, which is what a
    /// ring piece is (GEN-19) and what its lead's own bend is read from.
    /// </summary>
    static GroundPieces ARingOf(int nodes, Vector2 centreM, float radiusM)
    {
        var nodeM = new Vector2[nodes];
        for (var node = 0; node < nodes; node++)
        {
            var (sin, cos) = MathF.SinCos(MathF.Tau * node / nodes);
            nodeM[node] = centreM + (new Vector2(cos, sin) * radiusM);
        }

        var from = new int[nodes];
        var to = new int[nodes];
        var road = new int[nodes];
        var offsets = new int[nodes + 1];
        var arcs = new ArcSeg[nodes];
        for (var at = 0; at < nodes; at++)
        {
            from[at] = at;
            to[at] = (at + 1) % nodes;
            road[at] = at;

            // Laid the way the nodes go round: the heading climbs along the arc, so the bend is positive
            // (<see cref="ArcSeg.HeadingAtRad"/>) and the arc sets off half its own sweep inside the chord.
            var chord = nodeM[to[at]] - nodeM[at];
            var halfRad = MathF.Asin(MathF.Min(1f, chord.Length() * 0.5f / radiusM));
            arcs[at] = new ArcSeg(
                nodeM[at], MathF.Atan2(chord.Y, chord.X) - halfRad, 2f * halfRad * radiusM, 1f / radiusM);
            offsets[at + 1] = at + 1;
        }

        var bare = GroundPieces.None(seed: 7, new Vector2(800f, 600f), Config.PavementWidthM);
        return bare.With(
            new CityPlan.RoadArrays
            {
                FromJunction = from, ToJunction = to, WidthM = new float[nodes],
                Flow = CityPlan.RoadArrays.AllBothWays(nodes), SegmentOffsets = offsets,
                Segments = arcs,
            },
            bare.Bridges,
            new CityPlan.JunctionArrays
            {
                CentreM = nodeM, RadiusM = new float[nodes], Lit = new bool[nodes],
                PhaseOffsetS = new float[nodes],
            },
            bare.JunctionCorners,
            new CityPlan.RoundaboutArrays { RingOffsets = [0, nodes], Road = road },
            bare.Crosswalks,
            bare.StopLines);
    }
}
