using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The lines laid between the points the pedestrian nodes hand their ways over at (WLK-11). Where those
/// points stand is <see cref="PedestrianNodesTests"/>', and the paint a zebra is made of is
/// <see cref="CrossingsTests"/>'.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class PedestrianWaysTests
{
    /// <summary>
    /// <b>A way is laid for every pair still handed over to a node that stands, and for no other</b>
    /// (WLK-11, WLK-3): one line and not two, the two ends of a way naming each other — so a pair whose own
    /// node the merge made this same place has none, and neither has one whose node is not there.
    /// </summary>
    [Fact]
    public void AWayIsLaidForEveryPairHandedOverToANodeThatStands()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var ways = FootWays.Lay(plan, config);
        var connectors = ways.Connectors;

        var wanted = new HashSet<(int Node, int Onto, FootConnectorKind Kind)>();
        for (var node = 0; node < connectors.NodeCount; node++)
        {
            if (!connectors.StandsAt(node)) continue;

            for (var which = 0; which < FootConnectors.Kinds; which++)
            {
                var kind = (FootConnectorKind)which;
                var onto = connectors.Reaches(node, kind);
                if (!connectors.HandsOver(node, kind) || onto < 0 || !connectors.StandsAt(onto)) continue;

                wanted.Add((Math.Min(node, onto), Math.Max(node, onto), kind));
            }
        }

        var laid = new HashSet<(int Node, int Onto, FootConnectorKind Kind)>();
        for (var way = 0; way < ways.Count; way++)
        {
            laid.Add((ways.FromNode(way), ways.OntoNode(way), ways.KindOf(way)));
        }

        // The staging and not the claim: a fixture standing no node would leave both sets empty, and pass.
        Assert.NotEmpty(wanted);
        Assert.Equal(wanted.Count, ways.Count);
        Assert.Equal(wanted.Order(), laid.Order());
    }

    /// <summary>
    /// <b>A crossing is the straight between its two points</b> (WLK-11): what it runs over is the
    /// carriageway, which the boundary is the edge of rather than a line across.
    /// </summary>
    [Fact]
    public void ACrossingIsTheStraightBetweenItsPoints()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var ways = FootWays.Lay(plan, config);
        var connectors = ways.Connectors;

        var crossed = 0;
        for (var way = 0; way < ways.Count; way++)
        {
            if (ways.KindOf(way) != FootConnectorKind.Crossing) continue;

            crossed++;
            for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
            {
                var line = ways.LaneOf(way, lane);
                var fromM = connectors.PointM(ways.SetsOffAt(way, lane), FootConnectorKind.Crossing, lane);
                var ontoM = connectors.PointM(ways.ArrivesAt(way, lane), FootConnectorKind.Crossing, lane);

                Assert.Equal(1, line.Length);
                Assert.Equal(0f, line[0].Curvature, LineTolerance.RoundingM);
                Assert.Equal(0f, Vector2.Distance(line[0].StartM, fromM), LineTolerance.RoundingM);
                Assert.Equal(0f, Vector2.Distance(line[0].EndM, ontoM), LineTolerance.RoundingM);
            }
        }

        Assert.True(crossed > 0, "the fixture town lays no crossing at all");
    }

    /// <summary>
    /// <b>A walk down a road or round a junction that falls back to the straight between its points is
    /// shorter than the pavement is wide</b> (WLK-11): the straight is what a way is laid as where its two
    /// ends fall on two different lines of its course, so what it may not be is long enough to leave the
    /// pavement the course runs down.
    /// </summary>
    /// <remarks>
    /// <b>Weighed and not counted.</b> A course really does come apart where the move swallowed a corner
    /// tighter than the offset, and two points a stride either side of such a corner are a stride apart along
    /// any line between them — so the chord is the walk there. It is a straight long enough to cut the shape
    /// that is the defect, and <b>how many of either a town has is the census's to report</b>.
    /// </remarks>
    [Fact]
    public void AWalkThatCannotFollowItsCourseIsShorterThanThePavementIsWide()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var ways = FootWays.Lay(plan, config);

        var followed = 0;
        for (var way = 0; way < ways.Count; way++)
        {
            if (ways.KindOf(way) == FootConnectorKind.Crossing) continue;

            for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
            {
                var line = ways.LaneOf(way, lane);
                if (line.Length == 0) continue;

                followed++;
                if (ways.AlongTheKerb(way, lane)) continue;

                Assert.True(
                    Spline.TotalLengthM(line) <= config.PavementWidthM,
                    $"a walk {ways.KindOf(way)} is {Spline.TotalLengthM(line):F2} m of straight between its " +
                    $"ends at {line[0].StartM.X:F0},{line[0].StartM.Y:F0}");
            }
        }

        Assert.True(followed > 0, "the fixture town lays no walk along its boundary at all");
    }

    /// <summary>
    /// <b>A walk along the boundary keeps the offset its ends were struck at, over its whole length</b>
    /// (WLK-11, WLK-9): the line is that boundary moved off itself, so nowhere along it does the walk stand
    /// nearer the tarmac than the point it set off from.
    /// </summary>
    /// <remarks>
    /// <b>Read as a distance to the nearest boundary and not to the line it was moved off.</b> Which piece
    /// is nearest is a question the placement already answered at the ends; asked again from the middle of a
    /// mouth it can answer with a different kerb, which is why what is claimed is that the walk is never
    /// <em>inside</em> its offset rather than that it is exactly on it.
    /// </remarks>
    [Fact]
    public void AWalkAlongTheBoundaryStandsNoNearerTheTarmacThanItsOffset()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var kerbs = KerbLines.Of(plan, config);
        var ways = FootWays.Lay(plan, config);
        var insideM = config.WalkingLaneAtM(0) - LineTolerance.OnePlaceM;

        var walked = 0;
        for (var way = 0; way < ways.Count; way++)
        {
            if (ways.KindOf(way) == FootConnectorKind.Crossing) continue;

            for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
            {
                var line = ways.LaneOf(way, lane);
                if (line.Length == 0) continue;

                walked++;
                var lengthM = Spline.TotalLengthM(line);
                for (var stepM = 0f; stepM <= lengthM; stepM += StepM)
                {
                    var atM = Spline.SampleAt(line, stepM).PositionM;
                    if (!kerbs.NearestTo(atM, out var kerb)) continue;

                    Assert.True(
                        Vector2.Distance(atM, kerb.PositionM) >= insideM,
                        $"a walk {ways.KindOf(way)} stands " +
                        $"{Vector2.Distance(atM, kerb.PositionM):F2} m off the boundary at {atM.X:F0},{atM.Y:F0}");
                }
            }
        }

        Assert.True(walked > 0, "the fixture town lays no walk along its boundary at all");
    }

    /// <summary>
    /// <b>The two lanes of a way are walked opposite ways</b> (WLK-8): a way is walked a lane each way, so
    /// its two lanes set off from its two ends and run against one another all the way along.
    /// </summary>
    [Fact]
    public void TheTwoLanesOfAWayAreWalkedOppositeWays()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var ways = FootWays.Lay(plan, config);

        var walked = 0;
        for (var way = 0; way < ways.Count; way++)
        {
            var one = ways.LaneOf(way, 0);
            var other = ways.LaneOf(way, 1);
            if (one.Length == 0 || other.Length == 0) continue;

            walked++;
            Assert.True(
                ways.SetsOffAt(way, 0) == ways.ArrivesAt(way, 1)
                && ways.SetsOffAt(way, 1) == ways.ArrivesAt(way, 0),
                $"a walk {ways.KindOf(way)}'s two lanes both set off at node {ways.SetsOffAt(way, 0)} — " +
                $"{Spline.TotalLengthM(one):F2} m from {one[0].StartM.X:F0},{one[0].StartM.Y:F0} and " +
                $"{Spline.TotalLengthM(other):F2} m from {other[0].StartM.X:F0},{other[0].StartM.Y:F0}, " +
                $"followed {ways.AlongTheKerb(way, 0)} and {ways.AlongTheKerb(way, 1)}");

            // The lines only lie beside each other where both of them are the way their kind is laid as: one
            // lane that followed its course and one that fell back to the chord over a swallowed corner are
            // two different shapes, and nothing about the pair can be read off them.
            if (ways.KindOf(way) != FootConnectorKind.Crossing
                && (!ways.AlongTheKerb(way, 0) || !ways.AlongTheKerb(way, 1))) continue;

            // And the lines run against each other where they lie beside each other, which is at the way's
            // own ends: one lane leaves there and the other arrives. Read as the chord between the far ends
            // instead, a walk round a hairpin says nothing — both of its lanes turn most of the way round.
            var arriving = Spline.SampleAt(other, Spline.TotalLengthM(other)).Direction;
            Assert.True(
                Vector2.Dot(one[0].StartUnit, arriving) < 0f,
                $"a walk {ways.KindOf(way)}'s two lanes both leave its end at " +
                $"{one[0].StartM.X:F0},{one[0].StartM.Y:F0}");
        }

        Assert.True(walked > 0, "the fixture town lays no walk at all");
    }

    /// <summary>
    /// <b>A pavement's lane nearer the kerb is the one walked with the kerb on the hand the town keeps</b>
    /// (WLK-8, TER-4a): a walker keeps the hand a driver keeps, so the lane against the tarmac is walked
    /// with the tarmac on that hand and the lane behind it the other way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read off the boundary rather than off the pair</b>, which is the frame a reader can picture: a
    /// walker on the kerb-side lane has the road on their keeping hand exactly as a driver has the kerb on
    /// theirs. The other lane is pinned by the pair running against it
    /// (<see cref="TheTwoLanesOfAWayAreWalkedOppositeWays"/>), and a crossing's pair lies along the kerb
    /// rather than across it — the rule placing all three is one rule with no case for a kind.
    /// </para>
    /// <para>
    /// <b>Only where the query found the lane's own kerb</b>, which is where it answers at the offset the
    /// lane was struck at. Which piece of boundary is nearest a point is a different question a step away
    /// from where the placement asked it, and at a mouth it can answer with the kerb across the road.
    /// </para>
    /// </remarks>
    [Fact]
    public void APavementsKerbSideLaneIsWalkedWithTheKerbOnTheHandTheTownKeeps()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var kerbs = KerbLines.Of(plan, config);
        var ways = FootWays.Lay(plan, config);
        var ownKerbM = config.WalkingLaneAtM(0) + LineTolerance.RoundingM;

        var walked = 0;
        for (var way = 0; way < ways.Count; way++)
        {
            if (ways.KindOf(way) == FootConnectorKind.Crossing) continue;

            var line = ways.LaneOf(way, 0);
            if (line.Length == 0 || !kerbs.NearestTo(line[0].StartM, out var kerb)) continue;

            var toTheKerbM = kerb.PositionM - line[0].StartM;
            if (toTheKerbM.Length() > ownKerbM) continue;

            walked++;
            var keeps = Heading.RightOf(line[0].StartUnit) * config.RoadSideSign;
            Assert.True(
                Vector2.Dot(keeps, toTheKerbM) > 0f,
                $"a walk {ways.KindOf(way)} keeps its kerb on the wrong hand at " +
                $"{line[0].StartM.X:F0},{line[0].StartM.Y:F0}");
        }

        Assert.True(walked > 0, "the fixture town lays no walk beside a kerb at all");
    }

    /// <summary>
    /// <b>Nothing is laid down a lane whose two ends are one point, and something down every other</b>
    /// (WLK-12, WLK-11): a walk between a place and itself is not a walk, and the shared point is the whole
    /// of what joins the ways either side of it.
    /// </summary>
    [Fact]
    public void ALaneWeldedToOnePointIsTheOnlyOneNothingIsLaidDown()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var ways = FootWays.Lay(plan, config);
        var connectors = ways.Connectors;

        var welded = 0;
        for (var way = 0; way < ways.Count; way++)
        {
            for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
            {
                var shares = connectors.Shares(ways.FromNode(way), ways.KindOf(way), lane);
                if (shares) welded++;

                var atM = connectors.PointM(ways.FromNode(way), ways.KindOf(way), lane);
                Assert.True(
                    shares == (ways.LaneOf(way, lane).Length == 0),
                    $"a walk {ways.KindOf(way)} lane {lane} at {atM.X:F0},{atM.Y:F0} is laid as " +
                    $"{ways.LaneOf(way, lane).Length} pieces and welded {shares}");
            }
        }

        Assert.True(welded > 0, "the fixture town welds no lane at all");
    }

    /// <summary>
    /// <b>A lane crosses itself nowhere</b> (WLK-11): a walk is a line somebody walks from one end of to the
    /// other, so one that passes the same place twice is not a walk at all.
    /// </summary>
    /// <remarks>
    /// <b>It is the shape a stretch offset on its own comes back as</b> at a corner the offset swallows: the
    /// fold is deeper than the two pieces that opened it, so no crossing is found to cut it back at and what
    /// is laid is the chord across it — a knot of two or three pieces over the pavement. Taken off the whole
    /// shape's own offset instead there is nothing to fold, because the shape's cut has already been made.
    /// </remarks>
    [Fact]
    public void ALaneCrossesItselfNowhere()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var ways = FootWays.Lay(plan, config);

        var weighed = 0;
        Span<float> alongOneM = stackalloc float[2];
        Span<float> alongOtherM = stackalloc float[2];
        for (var way = 0; way < ways.Count; way++)
        {
            for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
            {
                var line = ways.LaneOf(way, lane);
                weighed += line.Length;

                // Every pair but the ones sharing a joint, which meet there by construction.
                for (var one = 0; one < line.Length; one++)
                {
                    for (var other = one + 2; other < line.Length; other++)
                    {
                        var found = Spline.CrossingsOf(line[one], line[other], alongOneM, alongOtherM);
                        if (found == 0) continue;

                        var atM = line[one].PointAtM(alongOneM[0]);
                        Assert.Fail(
                            $"a walk {ways.KindOf(way)} crosses itself at {atM.X:F0},{atM.Y:F0}");
                    }
                }
            }
        }

        Assert.True(weighed > 0, "the fixture town lays no walk at all");
    }

    /// <summary>How finely a walk is read along its own length, which is a stride of it.</summary>
    const float StepM = 1f;
}
