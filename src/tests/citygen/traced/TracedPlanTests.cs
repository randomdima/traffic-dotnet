using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A survey is laid as the place it surveyed, and nothing of it is lost</b> (GEN-57): a junction where ways
/// share a point and nowhere else however close two stand, one road through a place traffic merely carries on
/// through, every surveyed point within a tolerance of its road, every piece kept, no road longer than a traced map lays one, and
/// no lane folded back over a corner — each asked of a few ways laid by hand rather than of a city.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class TracedPlanTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary><b>Two ways sharing a point meet there</b>, and the junction they meet at has all four arms.</summary>
    [Fact]
    public void WaysSharingAPointMeetAtAJunctionOfEveryArm()
    {
        var plan = Laid([100, 500, 900, 500, 500, 100, 500, 900, 500, 500], Way(0, 4, 1), Way(2, 4, 3));

        Assert.Equal(4, ArmsAt(plan, new Vector2(500f, 500f)));
    }

    /// <summary>
    /// <b>Two ways that cross without sharing a point do not meet there</b>: OSM says nothing meets where a
    /// bridge passes over a street, so the only junction is the point the two do share.
    /// </summary>
    [Fact]
    public void WaysCrossingWithoutAPointInCommonDoNotMeetThere()
    {
        var plan = Laid([100, 500, 900, 500, 500, 100, 500, 900, 900, 100], Way(0, 1, 4), Way(2, 3), Way(4, 2));

        Assert.Equal(0, ArmsAt(plan, new Vector2(500f, 500f)));
    }

    /// <summary><b>A place two ways merely carry on through is no junction</b> (GEN-51): the two are one road.</summary>
    [Fact]
    public void TwoWaysEndToEndAreOneRoad()
    {
        var plan = Laid([100, 500, 500, 520, 900, 500], Way(0, 1), Way(1, 2));

        Assert.Equal(1, plan.Roads.Count);
    }

    /// <summary>
    /// <b>A one-way way running into a two-way one is a place traffic does not simply carry on through</b>, so
    /// the two stay two roads.
    /// </summary>
    [Fact]
    public void AOneWayWayRunningIntoATwoWayOneStaysTwoRoads()
    {
        var plan = Laid([100, 500, 500, 520, 900, 500], OneWay(0, 1), Way(1, 2));

        Assert.Equal(2, plan.Roads.Count);
    }

    /// <summary>
    /// <b>Junctions nearer each other than a traced map lays two are one, amid them</b>: a side street either side of a
    /// main road, staggered by a few metres, meets it at one junction of four arms, and the short road between the two
    /// tees the survey drew is gone.
    /// </summary>
    [Fact]
    public void JunctionsNearerThanTwoAreLaidAreOneAmidThem()
    {
        const float staggerM = 3f;
        var plan = Laid(Staggered(staggerM), Way(0, 1, 2, 3), Way(4, 1), Way(5, 2));

        Assert.Equal(4, ArmsAt(plan, new Vector2(500f + (staggerM * 0.5f), 500f)));
        Assert.Equal(4, plan.Roads.Count);
    }

    /// <summary><b>Junctions as far apart as a traced map lays two are two</b>, each where the survey put it.</summary>
    [Fact]
    public void JunctionsAsFarApartAsTwoAreLaidAreTwo()
    {
        var staggerM = Config.CityGen.TracedJunctionsMergedM;
        var plan = Laid(Staggered(staggerM), Way(0, 1, 2, 3), Way(4, 1), Way(5, 2));

        Assert.Equal(3, ArmsAt(plan, new Vector2(500f, 500f)));
        Assert.Equal(3, ArmsAt(plan, new Vector2(500f + staggerM, 500f)));
    }

    /// <summary>
    /// <b>A junction gathered from several makes no turn the roads between them did not</b>: where the gap in a dual
    /// carriageway's median is driven only from the far carriageway, a side street meeting the near one is not turned
    /// across it onto the far one.
    /// </summary>
    [Fact]
    public void AGatheredJunctionMakesNoTurnTheRoadsBetweenItsPlacesDidNot()
    {
        var plan = Laid(AcrossAMedian, SideStreet, OneWay(4, 1), OneWay(0, 1, 2), OneWay(5, 4, 3));

        Assert.DoesNotContain(RoadEndingAt(plan, MedianFarWestM), Reached(plan, RoadEndingAt(plan, SideStreetM)));
    }

    /// <summary>
    /// <b>A junction gathered from several makes the turns the roads between them made</b>: across a gap in the median
    /// driven both ways, a side street meeting the near carriageway is turned onto the far one.
    /// </summary>
    [Fact]
    public void AGatheredJunctionMakesTheTurnsTheRoadsBetweenItsPlacesMade()
    {
        var plan = Laid(AcrossAMedian, SideStreet, MedianGap, OneWay(0, 1, 2), OneWay(5, 4, 3));

        Assert.Contains(RoadEndingAt(plan, MedianFarWestM), Reached(plan, RoadEndingAt(plan, SideStreetM)));
    }

    /// <summary>
    /// <b>A restriction onto a road a junction was gathered over still holds</b>: a side street forbidden the gap in the
    /// median is not turned across it onto the far carriageway.
    /// </summary>
    [Fact]
    public void ARestrictionOntoARoadGatheredIntoAJunctionStillHolds()
    {
        var keptOff = new OsmTurnRestriction { Relation = 1, From = SideStreetWay, Via = 1, To = MedianGapWay, Only = false };
        var plan = Laid(AcrossAMedian, Turns(keptOff), SideStreet, MedianGap, OneWay(0, 1, 2), OneWay(5, 4, 3));

        Assert.DoesNotContain(RoadEndingAt(plan, MedianFarWestM), Reached(plan, RoadEndingAt(plan, SideStreetM)));
    }

    /// <summary>
    /// <b>A junction gathered from a signalled place is lit</b>: lights on the far carriageway at the gap in a dual
    /// carriageway's median light the one junction it and the near one are.
    /// </summary>
    [Fact]
    public void AJunctionGatheredFromASignalledPlaceIsLit()
    {
        var plan = Laid(AcrossAMedian, Controlled((4, new PointControl(SurveyControl.Signals, 0))), [],
            SideStreet, MedianGap, OneWay(0, 1, 2), OneWay(5, 4, 3));

        var lit = Enumerable.Range(0, plan.Junctions.Count).Where(junction => plan.Junctions.Lit[junction]).Select(junction => plan.Junctions.CentreM[junction]);
        Assert.Equal([new Vector2(500f, 500f - (MedianM * 0.5f))], lit);
    }

    /// <summary>
    /// <b>Two one-way carriageways joined only to each other are laid</b>: a ring with no place on it to be
    /// walked from is given one, and is laid as the loop it is.
    /// </summary>
    [Fact]
    public void ARingOfOneWayWaysJoinedToNothingElseIsLaid()
    {
        var plan = Laid([100, 100, 300, 100, 300, 300, 100, 300], OneWay(0, 1, 2), OneWay(2, 3, 0));

        Assert.Equal(2, plan.Roads.Count);
    }

    /// <summary>
    /// <b>Every point a way was surveyed through stands within the tolerance of its road</b>
    /// (<see cref="SimConfig.TracedLineToleranceAcrossM"/>): a jog of twice what a street of a lane each way has is a
    /// jog in the road.
    /// </summary>
    [Fact]
    public void EveryPointAWayWasSurveyedThroughStandsWithinTheToleranceOfItsRoad()
    {
        var jogM = 2f * Config.TracedLineToleranceAcrossM(2f * LaneM);
        float[] pointsM = [100, 500, 300, 500, 310, 500 + jogM, 500, 500 + jogM];
        var plan = Laid(pointsM, Way(0, 1, 2, 3));

        var line = plan.Roads.SegmentsOf(0);
        var lengthM = Spline.TotalLengthM(line);
        foreach (var at in (ReadOnlySpan<int>)[1, 2])
        {
            var pointM = new Vector2(pointsM[2 * at], pointsM[(2 * at) + 1]);
            var onM = Spline.ProjectM(line, pointM, lengthM * 0.5f, lengthM);
            Assert.InRange(Vector2.Distance(Spline.SampleAt(line, onM).PositionM, pointM), 0f, Config.TracedLineToleranceAcrossM(plan.Roads.WidthM[0]));
        }
    }

    /// <summary>
    /// <b>A bend a mapper drew as a polygon is laid as one arc</b>, turning through the whole of it — give or take
    /// the angle the tolerance tilts the straights either side through: a node every 6° round a quarter turn of
    /// 60 m between a straight of 300 m and one of 340 m.
    /// </summary>
    [Fact]
    public void ABendDrawnAsAPolygonIsLaidAsOneArc()
    {
        const float radiusM = 60f;
        var pointsM = new List<float> { 100, 500 };
        for (var deg = 0; deg <= 90; deg += 6)
        {
            var (sin, cos) = MathF.SinCos(deg * MathF.PI / 180f);
            pointsM.AddRange([400f + (radiusM * sin), 500f + radiusM - (radiusM * cos)]);
        }

        pointsM.AddRange([400f + radiusM, 900f]);
        var plan = Laid([.. pointsM], Way([.. Enumerable.Range(0, pointsM.Count / 2)]));

        var arc = Assert.Single(plan.Roads.SegmentsOf(0).ToArray(), arc => arc.Curvature != 0f);
        var toleranceM = Config.TracedLineToleranceAcrossM(plan.Roads.WidthM[0]);
        var tiltRad = MathF.Atan(toleranceM / 300f) + MathF.Atan(toleranceM / 340f);
        Assert.Equal(MathF.PI * 0.5f, MathF.Abs(arc.Curvature) * arc.LengthM, tiltRad);
    }

    /// <summary>
    /// <b>No road is longer than a traced map lays one</b> (<see cref="CityGenFigures.TracedRoadLongestM"/>):
    /// a way surveyed further than that without meeting another is cut into roads no longer.
    /// </summary>
    [Fact]
    public void AWayLongerThanARoadIsCutIntoRoadsNoLonger()
    {
        var longestM = Config.CityGen.TracedRoadLongestM;
        var plan = Laid([10, 10, 10 + (2.5f * longestM), 10], Way(0, 1));

        Assert.Equal(3, plan.Roads.Count);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            Assert.True(Spline.TotalLengthM(plan.Roads.SegmentsOf(road)) <= longestM);
        }
    }

    /// <summary>
    /// <b>A road leaves its junction's disc where its survey does</b>, on the survey's own line: a way bending
    /// inside the disc is laid along the leg it leaves on, and not on a chord from the junction's centre.
    /// </summary>
    [Fact]
    public void ARoadLeavesItsJunctionsDiscWhereItsSurveyDoes()
    {
        var plan = Laid([100, 500, 300, 500, 300, 300, 301, 503, 400, 503], Way(0, 1), Way(1, 2), Way(1, 3, 4));

        foreach (var arc in plan.Roads.SegmentsOf(RoadEndingAt(plan, new Vector2(400f, 503f))))
        {
            Assert.Equal(503f, arc.StartM.Y, 1e-3f);
            Assert.Equal(503f, arc.EndM.Y, 1e-3f);
        }
    }

    /// <summary>
    /// <b>A kink of short legs in the corner of two long ones keeps the long ones</b>: they are carried on to the
    /// corner they meet at, and neither is swung off its survey to stand for the kink.
    /// </summary>
    [Fact]
    public void AKinkInTheCornerOfTwoLongLegsKeepsBoth()
    {
        var plan = Laid([100, 500, 480, 500, 480.7f, 499.7f, 481, 499, 481, 100], Way(0, 1, 2, 3, 4));

        foreach (var arc in plan.Roads.SegmentsOf(0))
        {
            if (arc.Curvature != 0f) continue;

            var alongEast = MathF.Abs(arc.StartM.Y - 500f) < 1e-3f && MathF.Abs(arc.EndM.Y - 500f) < 1e-3f;
            var alongSouth = MathF.Abs(arc.StartM.X - 481f) < 1e-3f && MathF.Abs(arc.EndM.X - 481f) < 1e-3f;
            Assert.True(alongEast || alongSouth, $"a straight from {arc.StartM} to {arc.EndM}");
        }
    }

    /// <summary>
    /// <b>No lane folds back over a corner</b>: a way surveyed through a kink whose legs have no room to round
    /// it even with its innermost lane at the tightest a traced lane is laid
    /// (<see cref="CityGenFigures.TracedTightestLaneRadiusM"/>) is eased until every corner has the room.
    /// </summary>
    [Fact]
    public void AKinkTheLegsCannotRoundIsEased()
    {
        var plan = Laid([100, 500, 480, 500, 478, 498, 478, 100], Way(0, 1, 2, 3));

        var tightestM = float.PositiveInfinity;
        foreach (var arc in plan.Roads.SegmentsOf(0))
        {
            if (arc.Curvature != 0f) tightestM = MathF.Min(tightestM, 1f / MathF.Abs(arc.Curvature));
        }

        Assert.True(tightestM - (LaneM * 0.5f) >= Config.CityGen.TracedTightestLaneRadiusM);
    }

    /// <summary>
    /// <b>A road of one lane on its line is rounded no tighter than half its carriageway</b>: nothing beside the
    /// lane covers its ground, so a kink its legs cannot round at that is the straight past it, where a wider
    /// road would keep it.
    /// </summary>
    [Fact]
    public void ALoneLanesCornerIsNoTighterThanHalfItsCarriageway()
    {
        var plan = Laid([100, 500, 480, 500, 478, 498, 478, 100], OneWay(0, 1, 2, 3));

        foreach (var arc in plan.Roads.SegmentsOf(0))
        {
            if (arc.Curvature != 0f) Assert.True(1f / MathF.Abs(arc.Curvature) >= LaneM * 0.5f * (1f - 1e-5f));
        }
    }

    /// <summary>
    /// <b>A single lane both ways share is one lane run the way that comes round</b>: beside a one-way street west to
    /// east, a shared lane drawn west to east by the north is driven east to west, back to where the street began.
    /// </summary>
    [Fact]
    public void ASharedLaneRunsTheWayThatComesRound()
    {
        var plan = Laid([100, 500, 900, 500, 500, 300], OneWay(0, 1), Shared(0, 2, 1));

        var lanes = plan.Paving(Config).Lanes;
        var byTheNorth = Enumerable.Range(0, plan.Roads.Count)
            .Single(road => plan.Roads.SegmentsOf(road).ToArray().Any(arc => arc.StartM.Y < 450f));
        Assert.Equal(
            [(East, West)],
            Enumerable.Range(0, lanes.FirstRoadside).Where(lane => lanes.LaneRoad[lane] == byTheNorth)
                .Select(lane => (plan.Junctions.CentreM[lanes.LaneFromJunction[lane]], plan.Junctions.CentreM[lanes.LaneToJunction[lane]])));
    }

    /// <summary>
    /// <b>A shared lane that is a dead end however it runs is taken out</b>: a spur off a street, which a car could
    /// drive down and not back or back and not down.
    /// </summary>
    [Fact]
    public void ASharedSpurIsTakenOut()
    {
        var plan = Laid(Tee, Way(0, 1), Way(1, 2), Shared(1, 3));

        Assert.Equal(0, ArmsAt(plan, North));
    }

    /// <summary>
    /// <b>A piece of the network of nothing but shared lanes is taken out</b>: a shared loop off a street, which its
    /// shared spur, taken out, leaves joined to nothing.
    /// </summary>
    [Fact]
    public void APieceOfNothingButSharedLanesIsTakenOut()
    {
        var plan = Laid([100, 500, 500, 500, 900, 500, 500, 300, 500, 100, 700, 200],
                        Way(0, 1), Way(1, 2), Shared(1, 3), Shared(3, 4), Shared(3, 5, 4));

        Assert.Equal(0, ArmsAt(plan, new Vector2(500f, 300f)));
    }

    /// <summary>
    /// <b>Lanes driven both ways down the middle are laid as lanes of one way each</b>: a tidal pair between two
    /// lanes each way is laid three each way, the whole carriageway's lanes where OSM puts them.
    /// </summary>
    [Fact]
    public void LanesDrivenBothWaysDownTheMiddleAreSplitBetweenTheWays()
    {
        var plan = Laid([100, 500, 900, 500], new SurveyWay
        {
            Highway = "primary", LanesForward = 2, LanesBackward = 2, LanesShared = 2, CarriagewayM = 6 * LaneM,
            CentreOffsetM = 0f, Points = [0, 1],
        });

        Assert.Equal((3, 3), (plan.Roads.LanesWithTheRoad(0), plan.Roads.LanesAgainstTheRoad(0)));
        Assert.Equal(LaneM, plan.Roads.LaneWidthM(0));
    }

    /// <summary>
    /// <b>A roadside is lane zero</b> (<see cref="LaneLines.IsRoadside"/>): a street of a lane each way and a roadside at
    /// each kerb lays its two lanes side by side about its line, and outside each a lane as wide as the strip on the
    /// strip's own middle.
    /// </summary>
    [Fact]
    public void ARoadsideIsALaneBetweenTheKerbLaneAndTheKerb()
    {
        var lanes = Laid([100, 500, 900, 500], Edged(0, 1)).Paving(Config).Lanes;

        var laid = new List<(float AcrossM, float WidthM, bool Roadside)>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            laid.Add((Spline.SampleAt(lanes.ArcsOf(lane), 100f).PositionM.Y - 500f, lanes.LaneWidthM[lane], lanes.IsRoadside(lane)));
        }

        laid.Sort();
        (float, float, bool)[] expected =
        [
            (-LaneM - (RoadsideM * 0.5f), RoadsideM, true), (-LaneM * 0.5f, LaneM, false),
            (LaneM * 0.5f, LaneM, false), (LaneM + (RoadsideM * 0.5f), RoadsideM, true),
        ];
        Assert.Equal(expected.Length, laid.Count);
        for (var at = 0; at < expected.Length; at++)
        {
            Assert.Equal(expected[at].Item1, laid[at].AcrossM, 1e-3f);
            Assert.Equal((expected[at].Item2, expected[at].Item3), (laid[at].WidthM, laid[at].Roadside));
        }
    }

    /// <summary>
    /// <b>A roadside joins nothing</b>: at a crossroads of two streets with roadsides, no movement leaves one or arrives
    /// on one, and the junction counts none of them among its lanes.
    /// </summary>
    [Fact]
    public void ARoadsideJoinsNothing()
    {
        var plan = Laid([100, 500, 900, 500, 500, 100, 500, 900, 500, 500], Edged(0, 4, 1), Edged(2, 4, 3));
        var lanes = plan.Paving(Config).Lanes;
        var roads = RoadGraph.Build(plan, Config);

        var joined = new List<int>();
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            joined.Add(lanes.ConnectorFromLane[connector]);
            joined.Add(lanes.ConnectorToLane[connector]);
        }

        for (var junction = 0; junction < roads.JunctionCount; junction++)
        {
            joined.AddRange(roads.LanesIntoJunction(junction));
            joined.AddRange(roads.LanesOutOfJunction(junction));
        }

        Assert.Equal(8, lanes.LaneCount - lanes.FirstRoadside);
        Assert.DoesNotContain(joined, lanes.IsRoadside);
    }

    /// <summary>
    /// <b>A roadside runs on into the box to the corner its kerb makes</b> (<see cref="RoadsideLanes"/>): at a
    /// crossroads of two streets alike, each roadside ends where its kerb meets the kerb of the street across, half
    /// that street's carriageway from the middle of the box.
    /// </summary>
    [Fact]
    public void ARoadsideRunsOnIntoTheBoxToTheCornerItsKerbMakes()
    {
        var plan = Laid([100, 500, 900, 500, 500, 100, 500, 900, 500, 500], Edged(0, 4, 1), Edged(2, 4, 3));
        var lanes = plan.Paving(Config).Lanes;
        var middleM = new Vector2(500f, 500f);

        var reachedM = new List<float>();
        for (var lane = lanes.FirstRoadside; lane < lanes.LaneCount; lane++)
        {
            var arcs = lanes.ArcsOf(lane);
            var start = Spline.SampleAt(arcs, 0f);
            var end = Spline.SampleAt(arcs, lanes.LaneLengthM[lane]);
            var inTheBox = Vector2.Distance(start.PositionM, middleM) < Vector2.Distance(end.PositionM, middleM) ? start : end;
            reachedM.Add(MathF.Abs(Vector2.Dot(inTheBox.PositionM - middleM, inTheBox.Direction)));
        }

        Assert.Equal(8, reachedM.Count);
        Assert.All(reachedM, reachM => Assert.Equal(LaneM + RoadsideM, reachM, 1e-3f));
    }

    /// <summary>
    /// <b>The roadsides along the straight side of a tee meet in the middle of the box</b>: no arm leaves between them, so
    /// the kerb runs straight on past the mouth across the way.
    /// </summary>
    [Fact]
    public void TheRoadsidesAlongTheStraightSideOfATeeMeetInTheMiddleOfTheBox()
    {
        var plan = Laid([100, 500, 900, 500, 500, 100, 500, 500], Edged(0, 3, 1), Way(2, 3));
        var lanes = plan.Paving(Config).Lanes;

        var metM = new List<float>();
        for (var lane = lanes.FirstRoadside; lane < lanes.LaneCount; lane++)
        {
            var arcs = lanes.ArcsOf(lane);
            var start = arcs[0].StartM;
            var end = arcs[^1].EndM;
            if (start.Y < 500f) continue;

            metM.Add(MathF.Abs(start.X - 500f) < MathF.Abs(end.X - 500f) ? start.X : end.X);
        }

        Assert.Equal(2, metM.Count);
        Assert.All(metM, atM => Assert.Equal(500f, atM, 1e-3f));
    }

    /// <summary>
    /// <b>Where the kerb runs straight on, a roadside runs on beside the movement the lane beside it makes</b>
    /// (<see cref="RoadsideLanes"/>): across a tee whose street bends by a degree through it, the two roadsides along
    /// the straight side stand in the box the lane's half and the strip's half off that movement, as along their
    /// roads they stand off the lane.
    /// </summary>
    [Fact]
    public void WhereTheKerbRunsStraightOnARoadsideRunsBesideTheMovementOfTheLaneBesideIt()
    {
        var bentM = new Vector2(500f, 500f) + (Heading.Unit(MathF.PI / 180f) * 400f);
        var lanes = Laid([100, 500, 500, 500, bentM.X, bentM.Y, 500, 100], Edged(0, 1), Edged(1, 2), Way(3, 1)).Paving(Config).Lanes;

        var offM = new List<float>();
        for (var lane = lanes.FirstRoadside; lane < lanes.LaneCount; lane++)
        {
            foreach (var (fromM, toM) in (ReadOnlySpan<(float, float)>)[(0f, lanes.RoadFromM(lane)), (lanes.RoadToM(lane), lanes.LaneLengthM[lane])])
            {
                var inTheBoxM = Spline.SampleAt(lanes.ArcsOf(lane), (fromM + toM) * 0.5f).PositionM;
                if (toM <= fromM || inTheBoxM.Y < 500f) continue;

                var nearestM = float.MaxValue;
                for (var connector = 0; connector < lanes.ConnectorCount; connector++)
                {
                    var movement = lanes.ArcsOfConnector(connector);
                    var lengthM = lanes.ConnectorLengthM[connector];
                    var alongM = Spline.ProjectM(movement, inTheBoxM, lengthM * 0.5f, lengthM);
                    nearestM = MathF.Min(nearestM, Vector2.Distance(Spline.SampleAt(movement, alongM).PositionM, inTheBoxM));
                }

                offM.Add(nearestM);
            }
        }

        Assert.Equal(2, offM.Count);
        Assert.All(offM, eachM => Assert.Equal((LaneM + RoadsideM) * 0.5f, eachM, 1e-3f));
    }

    /// <summary>
    /// <b>The kerb turns round the square end of every lane</b> (<see cref="GroundRings"/>): where a street with a
    /// roadside at each kerb dead-ends, every corner of every lane's end stands on the kerb — the tarmac is the outside
    /// of the lanes, and no rounding takes a corner of one off into the pavement.
    /// </summary>
    [Fact]
    public void TheKerbTurnsRoundTheSquareEndOfEveryLane()
    {
        var paving = Laid([100, 500, 900, 500], Edged(0, 1)).Paving(Config);
        var rings = paving.Rings(Config).Carriageway.Rings;
        var lanes = paving.Lanes;

        var offM = new List<float>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            foreach (var atM in (ReadOnlySpan<float>)[0f, lanes.LaneLengthM[lane]])
            {
                var end = Spline.SampleAt(lanes.ArcsOf(lane), atM);
                var halfM = lanes.LaneWidthM[lane] * 0.5f;
                offM.Add(OffTheRingsM(rings, end.PositionM + (end.Right * halfM)));
                offM.Add(OffTheRingsM(rings, end.PositionM - (end.Right * halfM)));
            }
        }

        Assert.Equal(16, offM.Count);
        Assert.All(offM, eachM => Assert.InRange(eachM, 0f, ArcRings.WeldM));
    }

    /// <summary>
    /// <b>A roadside lost along the way stops where its road does</b> (<see cref="RoadsideLanes"/>): a street with a
    /// roadside at each kerb running on through a tee into one with none ends the one along the tee's straight side at
    /// its own line's end, which is where the line beside it is painted to, and it runs on into no box.
    /// </summary>
    [Fact]
    public void ARoadsideLostAlongTheWayStopsWhereItsRoadDoes()
    {
        var plan = Laid(IntoATeeM, Edged(0, 1), Way(1, 2), Way(3, 1));
        var lanes = plan.Paving(Config).Lanes;

        var pastM = new List<float>();
        for (var lane = lanes.FirstRoadside; lane < lanes.LaneCount; lane++)
        {
            var line = plan.Roads.SegmentsOf(lanes.LaneRoad[lane]);
            var arcs = lanes.ArcsOf(lane);
            if (arcs[0].StartM.Y < 500f) continue;

            pastM.Add(MathF.Max(arcs[0].StartM.X, arcs[^1].EndM.X) - MathF.Max(line[0].StartM.X, line[^1].EndM.X));
        }

        Assert.Equal(0f, Assert.Single(pastM), 1e-3f);
    }

    /// <summary>
    /// <b>Where a roadside is lost along the way the kerb turns no corner</b> (<see cref="LaneLines.Tapers"/>): across the
    /// tee a street with a roadside at each kerb runs on through into one with none, its straight side's outline runs on
    /// from one piece to the next on the heading the last left it, and is eased in over the strip rather than stepped
    /// round its square end.
    /// </summary>
    [Fact]
    public void WhereARoadsideIsLostAlongTheWayTheKerbTurnsNoCorner()
    {
        var rings = Laid(IntoATeeM, Edged(0, 1), Way(1, 2), Way(3, 1)).Paving(Config).Rings(Config).Carriageway.Rings;

        var sharpestRad = 0f;
        foreach (var ring in rings)
        {
            for (var piece = 0; piece < ring.Length; piece++)
            {
                var before = ring[piece];
                var after = ring[(piece + 1) % ring.Length];

                // The street's dead ends turn their corners, and stand far off the box; the tee's mouth is across it.
                if (MathF.Abs(before.EndM.X - 500f) > 100f || before.EndM.Y < 500f) continue;

                var turnRad = Spline.WrapRad(after.HeadingRad - before.HeadingAtRad(before.LengthM));
                sharpestRad = MathF.Max(sharpestRad, MathF.Abs(turnRad));
            }
        }

        Assert.InRange(sharpestRad, 0f, LineTolerance.StraightOnRad);
    }

    /// <summary>A street west to east along y = 500 through a tee at x = 500, its arm north to y = 100.</summary>
    static readonly float[] IntoATeeM = [100, 500, 500, 500, 900, 500, 500, 100];

    /// <summary>
    /// <b>A roadside runs on along its street</b> (<see cref="CityGenFigures.TracedRoadsideShortestM"/>): a street with a
    /// roadside at each kerb along one way and none along the next, nothing meeting it between, is one road with both
    /// along all of it.
    /// </summary>
    [Fact]
    public void ARoadsideRunsOnAlongItsStreet()
    {
        var plan = Laid([100, 500, 500, 500, 900, 500], Edged(0, 1), Way(1, 2));

        Assert.Equal((1, RoadsideM, RoadsideM), (plan.Roads.Count, plan.Roads.RoadsideM(0, true), plan.Roads.RoadsideM(0, false)));
    }

    /// <summary>
    /// <b>And past a yard's way meeting it</b>: the same street with a service way leaving it where its roadside stops
    /// runs it on past that.
    /// </summary>
    [Fact]
    public void ARoadsideRunsOnPastAYardsWayMeetingItsStreet()
    {
        var plan = Laid([.. IntoATeeM[..6], 500, 300], Edged(0, 1), Way(1, 2), Way(3, 1) with { Highway = "service" });

        var east = RoadEndingAt(plan, new Vector2(900f, 500f));
        Assert.Equal((RoadsideM, RoadsideM), (plan.Roads.RoadsideM(east, true), plan.Roads.RoadsideM(east, false)));
    }

    /// <summary>
    /// <b>A roadside shorter than the shortest is not laid</b> (<see cref="CityGenFigures.TracedRoadsideShortestM"/>): a
    /// street with a roadside at each kerb along a way 10 m shorter than that, between two with none, is one road with
    /// none.
    /// </summary>
    [Fact]
    public void ARoadsideShorterThanTheShortestIsNotLaid()
    {
        var halfM = (Config.CityGen.TracedRoadsideShortestM - 10f) * 0.5f;
        var plan = Laid([100, 500, 500 - halfM, 500, 500 + halfM, 500, 900, 500], Way(0, 1), Edged(1, 2), Way(2, 3));

        Assert.Equal((1, 0f, 0f), (plan.Roads.Count, plan.Roads.RoadsideM(0, true), plan.Roads.RoadsideM(0, false)));
    }

    /// <summary>
    /// <b>A single roadside stands on one side along its street</b>: two ways of a lane each way drawn toward each other,
    /// each holding one roadside beside the kerb its own traffic keeps to — the street's two sides — are one road with
    /// one roadside.
    /// </summary>
    [Fact]
    public void ASingleRoadsideStandsOnOneSideAlongItsStreet()
    {
        var plan = Laid([100, 500, 600, 500, 900, 500], Kerbed(0, 1), Kerbed(2, 1));

        Assert.Equal((1, RoadsideM), (plan.Roads.Count, plan.Roads.RoadsideM(0, true) + plan.Roads.RoadsideM(0, false)));
    }

    /// <summary>
    /// <b>A roadside is not run on where it would lay its road's walk over another road</b>: the street of
    /// <see cref="ARoadsideRunsOnAlongItsStreet"/> with a road of its own alongside its bare half, that road's
    /// carriageway a quarter of a roadside past where the bare half's walk ends, keeps that half bare.
    /// </summary>
    [Fact]
    public void ARoadsideIsNotRunOnWhereItWouldLayItsWalkOverAnotherRoad()
    {
        var alongsideM = 500f + (2f * LaneM) + Config.PavementWidthM + (RoadsideM * 0.25f);
        var plan = Laid([100, 500, 500, 500, 900, 500, 500, alongsideM, 900, alongsideM], Edged(0, 1), Way(1, 2), Way(3, 4));

        var bare = RoadEndingAt(plan, new Vector2(900f, 500f));
        Assert.Equal((0f, 0f), (plan.Roads.RoadsideM(bare, true), plan.Roads.RoadsideM(bare, false)));
    }

    /// <summary>
    /// <b>A stretch whose lanes change and change back is laid in the lanes either side of it</b>
    /// (<see cref="CityGenFigures.TracedLanesHeldM"/>): a street of two lanes each way mapped with one each way along
    /// 100 m of it, nothing meeting it there, is one road of two each way.
    /// </summary>
    [Fact]
    public void AStretchWhoseLanesChangeAndChangeBackIsLaidInTheLanesEitherSide()
    {
        var plan = Laid([100, 500, 450, 500, 550, 500, 900, 500], EachWay(2,0, 1), EachWay(1,1, 2), EachWay(2,2, 3));

        Assert.Equal((1, 2, 2), (plan.Roads.Count, plan.Roads.LanesWithTheRoad(0), plan.Roads.LanesAgainstTheRoad(0)));
    }

    /// <summary>
    /// <b>And one longer than that keeps its own</b>: the same street mapped with one lane each way along 10 m further
    /// than <see cref="CityGenFigures.TracedLanesHeldM"/> is three roads.
    /// </summary>
    [Fact]
    public void AStretchLongerThanLanesAreHeldKeepsItsOwn()
    {
        var halfM = (Config.CityGen.TracedLanesHeldM + 10f) * 0.5f;
        var plan = Laid([100, 500, 500 - halfM, 500, 500 + halfM, 500, 900, 500], EachWay(2,0, 1), EachWay(1,1, 2), EachWay(2,2, 3));

        Assert.Equal(3, plan.Roads.Count);
    }

    /// <summary>
    /// <b>And one a street meets keeps its own</b>: the stretch of
    /// <see cref="AStretchWhoseLanesChangeAndChangeBackIsLaidInTheLanesEitherSide"/> with a street leaving it halfway
    /// along is a lane each way on both sides of the junction there.
    /// </summary>
    [Fact]
    public void AStretchAStreetMeetsKeepsItsOwnLanes()
    {
        var plan = Laid([100, 500, 450, 500, 500, 500, 550, 500, 900, 500, 500, 100], EachWay(2,0, 1), EachWay(1,1, 2, 3), EachWay(2,3, 4), Way(5, 2));

        var junction = Array.FindIndex(plan.Junctions.CentreM, centreM => Vector2.Distance(centreM, new Vector2(500f, 500f)) < 1e-3f);
        var arms = Enumerable.Range(0, plan.Roads.Count).Where(road => plan.Roads.FromJunction[road] == junction || plan.Roads.ToJunction[road] == junction);
        Assert.All(arms, road => Assert.Equal(2, plan.Roads.LanesOn(road)));
    }

    /// <summary>
    /// <b>A street losing lanes close short of a junction runs into it in the lanes it had</b>
    /// (<see cref="CityGenFigures.TracedLanesHeldShortOfJunctionM"/>): a street of two lanes each way mapped with one each
    /// way along its last 10 m short of that into a tee is one road of two each way, all the way to the tee.
    /// </summary>
    [Fact]
    public void AStreetLosingLanesCloseShortOfAJunctionRunsIntoItInTheLanesItHad()
    {
        var lostAtM = 900f - Config.CityGen.TracedLanesHeldShortOfJunctionM + 10f;
        var plan = Laid([100, 500, lostAtM, 500, 900, 500, 900, 100, 900, 900], EachWay(2,0, 1), EachWay(1,1, 2), Way(3, 2, 4));

        var west = RoadEndingAt(plan, new Vector2(100f, 500f));
        var reachedM = plan.Junctions.CentreM[plan.Roads.FromJunction[west]].X + plan.Junctions.CentreM[plan.Roads.ToJunction[west]].X - 100f;
        Assert.Equal((2, 2, 900f), (plan.Roads.LanesWithTheRoad(west), plan.Roads.LanesAgainstTheRoad(west), reachedM));
    }

    /// <summary>
    /// <b>And one gaining lanes into it gains them</b>: a street of a lane each way mapped with two each way along the
    /// same last stretch into the tee keeps them, two roads short of the tee.
    /// </summary>
    [Fact]
    public void AStreetGainingLanesCloseShortOfAJunctionGainsThem()
    {
        var gainedAtM = 900f - Config.CityGen.TracedLanesHeldShortOfJunctionM + 10f;
        var plan = Laid([100, 500, gainedAtM, 500, 900, 500, 900, 100, 900, 900], EachWay(1,0, 1), EachWay(2,1, 2), Way(3, 2, 4));

        Assert.Equal(4, plan.Roads.Count);
    }

    /// <summary>How far a place stands off the nearest piece of any of these rings.</summary>
    static float OffTheRingsM(ArcSeg[][] rings, Vector2 pointM)
    {
        var nearestM = float.MaxValue;
        foreach (var ring in rings)
        {
            foreach (var piece in ring)
            {
                ReadOnlySpan<ArcSeg> one = [piece];
                var alongM = Spline.ProjectM(one, pointM, piece.LengthM * 0.5f, piece.LengthM);
                nearestM = MathF.Min(nearestM, Vector2.Distance(Spline.SampleAt(one, alongM).PositionM, pointM));
            }
        }

        return nearestM;
    }

    /// <summary>
    /// <b>A walk crossing a street with a roadside crosses it kerb to kerb</b> (<see cref="KerbEnds"/>): at a crossroads
    /// of two such streets, every station's two nodes stand the whole carriageway apart, the roadsides and all.
    /// </summary>
    [Fact]
    public void AStationAcrossAStreetWithARoadsideReachesKerbToKerb()
    {
        var plan = Laid([100, 500, 900, 500, 500, 100, 500, 900, 500, 500], Edged(0, 4, 1), Edged(2, 4, 3));

        var crossed = plan.Paving(Config).RoadEnds(Config).CrossedM.ToArray();
        Assert.Equal(4, crossed.Length);
        foreach (var nodes in crossed) Assert.Equal((2 * LaneM) + (2 * RoadsideM), Vector2.Distance(nodes.NearM, nodes.FarM), 1e-3f);
    }

    /// <summary><b>A road is laid as wide as OSM's lanes are</b>, every lane at its own width.</summary>
    [Fact]
    public void ARoadIsLaidAsWideAsOsmsLanes()
    {
        const float laneM = 2.75f;
        var plan = Laid([100, 500, 900, 500], Carried(2, 1, laneM, 0f, 0, 1));

        Assert.Equal(3 * laneM, plan.Roads.WidthM[0]);
    }

    /// <summary>
    /// <b>A road OSM places off its way is laid along its carriageway's middle</b> (placement): a way drawn along
    /// the left edge of its lanes running east has its carriageway's middle half of it to the south.
    /// </summary>
    [Fact]
    public void ARoadOsmPlacesOffItsWayIsLaidAlongItsCarriagewaysMiddle()
    {
        var plan = Laid([100, 500, 900, 500], Carried(2, 0, LaneM, LaneM, 0, 1));

        var line = plan.Roads.SegmentsOf(0);
        Assert.Equal(500f + LaneM, Spline.SampleAt(line, Spline.TotalLengthM(line) * 0.5f).PositionM.Y, 1e-3f);
    }

    /// <summary>
    /// <b>Where OSM widens a carriageway is a place</b>: two ways end to end with the same lanes at two widths
    /// stay two roads, so each is laid at its own.
    /// </summary>
    [Fact]
    public void WhereOsmWidensACarriagewayIsAPlace()
    {
        var plan = Laid([100, 500, 500, 520, 900, 500], Carried(1, 1, LaneM, 0f, 0, 1), Carried(1, 1, LaneM + 0.5f, 0f, 1, 2));

        Assert.Equal(2, plan.Roads.Count);
    }

    /// <summary>
    /// <b>A piece joined to nothing the rest of the town is joined to is kept</b>: it is part of the place, and
    /// what joined it was a way this map does not lay.
    /// </summary>
    [Fact]
    public void APieceJoinedToNothingElseIsKept()
    {
        var plan = Laid(
            [100, 500, 900, 500, 500, 100, 500, 900, 500, 500, 100, 800, 300, 800],
            Way(0, 4, 1), Way(2, 4, 3), Way(5, 6));

        Assert.Equal(5, plan.Roads.Count);
    }

    /// <summary>
    /// <b>A turn a restriction forbids is not made</b>: at a junction of three arms, no lane of the way it is made
    /// from joins a lane of the way it is made onto.
    /// </summary>
    [Fact]
    public void ATurnARestrictionForbidsIsNotMade()
    {
        var plan = Laid(Tee, Turns(Restricted(only: false, onto: NorthWay)), Way(0, 1) with { OsmId = WestWay },
            Way(1, 2) with { OsmId = EastWay }, Way(1, 3) with { OsmId = NorthWay });

        Assert.DoesNotContain(RoadEndingAt(plan, North), Reached(plan, RoadEndingAt(plan, West)));
    }

    /// <summary><b>An only_ restriction leaves its turn the one made</b> off its way at its junction.</summary>
    [Fact]
    public void AnOnlyRestrictionLeavesItsTurnTheOneMade()
    {
        var plan = Laid(Tee, Turns(Restricted(only: true, onto: EastWay)), Way(0, 1) with { OsmId = WestWay },
            Way(1, 2) with { OsmId = EastWay }, Way(1, 3) with { OsmId = NorthWay });

        Assert.Equal([RoadEndingAt(plan, East)], Reached(plan, RoadEndingAt(plan, West)));
    }

    /// <summary>
    /// <b>A lane marked for one turn makes that one</b>: of two lanes into a junction marked left and through, the
    /// lane marked left does not carry straight on, which an unmarked inner lane would.
    /// </summary>
    [Fact]
    public void ALaneMarkedForOneTurnMakesThatOne()
    {
        var plan = Laid(Tee, OsmTurns.None, Carried(2, 0, LaneM, 0f, 0, 1) with { OsmId = WestWay, Arrows = [OsmArrows.Left, OsmArrows.Through] },
            Way(1, 2) with { OsmId = EastWay }, Way(1, 3) with { OsmId = NorthWay });

        Assert.Equal([RoadEndingAt(plan, North)], Reached(plan, RoadEndingAt(plan, West), fromKerb: 1));
    }

    /// <summary>
    /// <b>A way's arrows are for the junction it ends at</b>: one running on through a junction is driven out of
    /// there as unmarked lanes are, its lane marked left carrying straight on as an inner lane does.
    /// </summary>
    [Fact]
    public void AWaysArrowsAreForTheJunctionItEndsAt()
    {
        var plan = Laid(Tee, OsmTurns.None, Carried(2, 0, LaneM, 0f, 0, 1, 2) with { Arrows = [OsmArrows.Left, OsmArrows.Through] },
            Way(1, 3));

        Assert.Contains(RoadEndingAt(plan, East), Reached(plan, RoadEndingAt(plan, West), fromKerb: 1));
    }

    /// <summary>
    /// <b>A turn whose lanes OSM names joins those and no others</b>: two lanes onto two, the right one in joined to
    /// the left one out, where lane for lane would join each to its own.
    /// </summary>
    [Fact]
    public void ATurnWhoseLanesOsmNamesJoinsThoseAndNoOthers()
    {
        var link = new OsmLaneLink { Relation = 1, From = WestWay, Via = 1, To = EastWay, FromLane = 2, ToLane = 1 };
        var plan = Laid(Tee, new OsmTurns { Restrictions = [], LaneLinks = [link] },
            Carried(2, 0, LaneM, 0f, 0, 1) with { OsmId = WestWay }, Carried(2, 0, LaneM, 0f, 1, 2) with { OsmId = EastWay },
            Way(1, 3) with { OsmId = NorthWay });

        Assert.Equal([(0, 1)], Joined(plan, RoadEndingAt(plan, West), RoadEndingAt(plan, East)));
    }

    /// <summary>
    /// <b>A lane joins the lane of its own number on the road it takes</b> (TER-5j): of three lanes running on past a
    /// road of one forking off to the near side, too slightly to be a turn, the kerb lane alone takes it — the second
    /// and third do not merge into its one lane.
    /// </summary>
    [Fact]
    public void ARoadOfOneLaneForkingOffIsTakenFromTheKerbLaneAlone()
    {
        var plan = Laid(Fork, OsmTurns.None, Carried(3, 0, LaneM, 0f, 0, 1), Carried(3, 0, LaneM, 0f, 1, 2), OneWay(1, 3));

        Assert.Equal([(0, 0)], Joined(plan, RoadEndingAt(plan, ForkEast), RoadEndingAt(plan, ForkBranch)));
    }

    /// <summary>
    /// <b>A turn to the far side is numbered from the line</b> (TER-5j): two lanes each way turning onto two, the lane
    /// beside the line joins the lane beside the line and not the kerb lane too.
    /// </summary>
    [Fact]
    public void ATurnToTheFarSideJoinsTheLaneBesideTheLine()
    {
        var plan = Laid(Tee, Carried(2, 2, LaneM, 0f, 0, 1), Carried(2, 2, LaneM, 0f, 1, 2), Carried(2, 2, LaneM, 0f, 1, 3));

        Assert.Equal([(1, 1)], Joined(plan, RoadEndingAt(plan, West), RoadEndingAt(plan, North)));
    }

    /// <summary>
    /// <b>A lane lost at a node ends there</b> (TER-5j): of three lanes running on in two, bearing slightly to the near
    /// side, the kerb two join their own and the third joins nothing — it does not merge.
    /// </summary>
    [Fact]
    public void ALaneLostAtANodeEndsThere()
    {
        var plan = Laid(BendingNear, Carried(3, 0, LaneM, 0f, 0, 1), Carried(2, 0, LaneM, 0f, 1, 2));

        Assert.Equal([(0, 0), (1, 1)], Joined(plan, RoadEndingAt(plan, West), RoadEndingAt(plan, BendingNearEnd)).Order().ToArray());
    }

    /// <summary>
    /// <b>Straight on is numbered from the kerb where nothing forks</b> (TER-5j): of two lanes running on in one, bearing
    /// slightly to the far side, the kerb lane joins it — not the lane beside the line its few degrees bear to.
    /// </summary>
    [Fact]
    public void StraightOnIsNumberedFromTheKerbWhereNothingForks()
    {
        var plan = Laid(BendingFar, Carried(2, 0, LaneM, 0f, 0, 1), Carried(1, 0, LaneM, 0f, 1, 2));

        Assert.Equal([(0, 0)], Joined(plan, RoadEndingAt(plan, West), RoadEndingAt(plan, BendingFarEnd)));
    }

    /// <summary>
    /// <b>The ground a lane lost at a node stops on is paved</b> (TER-5j): of three lanes running on in two, the third's
    /// end is eased onto the second lane on — a taper, which nothing drives — so the box keeps no kerbed hole there.
    /// </summary>
    [Fact]
    public void TheGroundALostLaneStopsOnIsPaved()
    {
        var plan = Laid(BendingNear, Carried(3, 0, LaneM, 0f, 0, 1), Carried(2, 0, LaneM, 0f, 1, 2));
        var lanes = plan.Paving(Config).Lanes;
        var lost = LaneOf(RoadEndingAt(plan, West), fromKerb: 2);
        var onto = LaneOf(RoadEndingAt(plan, BendingNearEnd), fromKerb: 1);

        var eased = Assert.Single(lanes.Tapers);
        Assert.Equal(Spline.SampleAt(lanes.ArcsOf(lost), lanes.LaneLengthM[lost]).PositionM, eased.Line[0].StartM, new VectorWithin(SagM));
        Assert.Equal(lanes.ArcsOf(onto)[0].StartM, eased.Line[^1].EndM, new VectorWithin(SagM));

        int LaneOf(int road, int fromKerb) =>
            Enumerable.Range(0, lanes.LaneCount).Single(lane => lanes.LaneRoad[lane] == road && lanes.LaneFromKerb[lane] == fromKerb);
    }

    /// <summary>
    /// <b>A lane gained at a node is taken by the last lane in as well as its own</b> (TER-5j): two lanes running on in
    /// three, the second joins the second and the third.
    /// </summary>
    [Fact]
    public void ALaneGainedAtANodeIsTakenByTheLastLaneIn()
    {
        var plan = Laid(BendingNear, Carried(2, 0, LaneM, 0f, 0, 1), Carried(3, 0, LaneM, 0f, 1, 2));

        Assert.Equal([(0, 0), (1, 1), (1, 2)], Joined(plan, RoadEndingAt(plan, West), RoadEndingAt(plan, BendingNearEnd)).Order().ToArray());
    }

    /// <summary>
    /// <b>Lanes gained two or more at a node are reached by none</b> (TER-5j): one lane running on in three joins the
    /// kerb lane alone — a car gets onto the other two by moving across.
    /// </summary>
    [Fact]
    public void LanesGainedTwoAtANodeAreReachedByNone()
    {
        var plan = Laid(BendingNear, Carried(1, 0, LaneM, 0f, 0, 1), Carried(3, 0, LaneM, 0f, 1, 2));

        Assert.Equal([(0, 0)], Joined(plan, RoadEndingAt(plan, West), RoadEndingAt(plan, BendingNearEnd)));
    }

    /// <summary>
    /// <b>A junction the survey reads as signalled carries lights, and no other does</b> (GEN-57): of a street's two
    /// side turnings, the one whose point the pack signals is lit.
    /// </summary>
    [Fact]
    public void AJunctionTheSurveySignalsIsLit()
    {
        var plan = Laid(Ladder, Controlled((1, new PointControl(SurveyControl.Signals, 0)), (2, new PointControl(SurveyControl.Signs, 0))), [],
            Way(0, 1, 2, 3), Way(1, 4), Way(2, 5));

        var lit = Enumerable.Range(0, plan.Junctions.Count).Where(junction => plan.Junctions.Lit[junction]).Select(junction => plan.Junctions.CentreM[junction]);
        Assert.Equal([new Vector2(400f, 500f)], lit);
    }

    /// <summary>
    /// <b>Junctions controlled as one share a clock</b>: a street's two signalled turnings the survey reads as one set
    /// of lights start their cycle at the same place.
    /// </summary>
    [Fact]
    public void JunctionsControlledAsOneShareAClock()
    {
        var plan = Laid(Ladder, Controlled((1, new PointControl(SurveyControl.Signals, 77)), (2, new PointControl(SurveyControl.Signals, 77))), [],
            Way(0, 1, 2, 3), Way(1, 4), Way(2, 5));

        var offsetS = Enumerable.Range(0, plan.Junctions.Count).Where(junction => plan.Junctions.Lit[junction])
            .Select(junction => plan.Junctions.PhaseOffsetS[junction]).Distinct();
        Assert.Single(offsetS);
    }

    /// <summary>
    /// <b>A painted crossing is laid across the road its way runs along, nearest where OSM puts it</b>: one mapped 3 m
    /// off a street's line, 200 m along it, is a zebra on the street's line there, its stripes laid along the street.
    /// </summary>
    [Fact]
    public void APaintedCrossingIsLaidOnItsWaysRoadNearestWhereOsmPutsIt()
    {
        var plan = Laid([100, 500, 900, 500], [], [new SurveyCrossing(WestWay, new Vector2(300f, 503f), SurveyCrossingKind.Zebra, true, CityPlan.NoRecord)],
            Way(0, 1) with { OsmId = WestWay });

        var crossing = Assert.Single(plan.Crosswalks.CentreM);
        Assert.Equal(new Vector2(300f, 500f), crossing, new VectorWithin(SagM));
        Assert.Equal(1f, MathF.Abs(plan.Crosswalks.Axis[0].X), 1e-4f);
    }

    /// <summary><b>A crossing that is not painted is not laid</b>, and the town paints no other.</summary>
    [Fact]
    public void ACrossingThatIsNotPaintedIsNotLaid()
    {
        var plan = Laid([100, 500, 900, 500], [], [new SurveyCrossing(WestWay, new Vector2(300f, 500f), SurveyCrossingKind.Unmarked, false, CityPlan.NoRecord)],
            Way(0, 1) with { OsmId = WestWay });

        Assert.Empty(plan.Crosswalks.CentreM);
    }

    /// <summary>
    /// <b>A bridge is a road of its own on the level above</b> (GEN-57, GEN-14a): a street carried over another on a
    /// bridge way is three roads — its two approaches on the ground and the bridge between its bridgeheads — and the
    /// bridge alone is driven above, with a deck its whole length.
    /// </summary>
    [Fact]
    public void ABridgeIsARoadOfItsOwnOnTheLevelAbove()
    {
        var plan = OverAStreet();

        var over = Enumerable.Range(0, plan.Roads.Count).Where(road => plan.Roads.LevelOf(road) == CityPlan.RoadArrays.Over).ToArray();
        var bridge = Assert.Single(over);
        Assert.Equal(4, plan.Roads.Count);
        Assert.Equal([bridge], plan.Bridges.Road);
        Assert.Equal(Spline.TotalLengthM(plan.Roads.SegmentsOf(bridge)), plan.Bridges.ToM[0] - plan.Bridges.FromM[0], 1e-3f);
    }

    /// <summary>
    /// <b>A tree is laid where the survey maps it, and none on the road</b> (GEN-57, TER-4c.4): of two trees mapped by a
    /// street, the one on its kerbside verge stands as a tree and the one on its carriageway is not laid.
    /// </summary>
    [Fact]
    public void ATreeIsLaidWhereTheSurveyMapsItAndNoneOnTheRoad()
    {
        var plan = TracedPlan.Lay(
            Surveyed([100, 500, 900, 500], OsmTurns.None, [], [], [new Vector2(500f, 510f), new Vector2(500f, 501f)], Way(0, 1)), Config,
            BuildingSizes.None);

        Assert.Equal([new Vector2(500f, 510f)], plan.Props.CentreM);
        Assert.Equal([(byte)PropKind.WildNature], plan.Props.Kind);
    }

    /// <summary>
    /// <b>A road running off the map ends on its edge, at a junction that runs off and stands nothing off</b> (GEN-2b,
    /// GEN-57): a street from inside the map to the place on its east edge the survey left it at.
    /// </summary>
    [Fact]
    public void ARoadRunningOffTheMapEndsOnItsEdge()
    {
        var plan = TracedPlan.Lay(RunningOff(), Config, BuildingSizes.None);

        var edge =Array.FindIndex(plan.Junctions.CentreM, centreM => centreM == EdgeM);
        var line = plan.Roads.SegmentsOf(0);
        Assert.True(plan.Junctions.RunsOff(edge));
        Assert.Equal(0f, plan.Junctions.RadiusM[edge]);
        Assert.Equal(0f, MathF.Min(Vector2.Distance(line[0].StartM, EdgeM), Vector2.Distance(line[^1].EndM, EdgeM)), 1e-3f);
    }

    /// <summary>
    /// <b>The ground of a road running off the map turns round past its edge</b> (GEN-2b): the driven ground's boundary
    /// reaches <see cref="SimConfig.PastTheMapEdgeM"/> past the east edge the street runs square off.
    /// </summary>
    [Fact]
    public void TheGroundOfARoadRunningOffTheMapTurnsRoundPastItsEdge()
    {
        var plan = TracedPlan.Lay(RunningOff(), Config, BuildingSizes.None);

        var furthestM = float.MinValue;
        foreach (var ring in plan.Paving(Config).Perimeter(Config).Chains)
        {
            foreach (var piece in ring) furthestM = MathF.Max(furthestM, MathF.Max(piece.StartM.X, piece.EndM.X));
        }

        Assert.Equal(EdgeM.X + Config.PastTheMapEdgeM, furthestM, ArcRings.WeldM);
    }

    /// <summary>Where the street <see cref="RunningOff"/> lays leaves the map: on its east edge.</summary>
    static readonly Vector2 EdgeM = new(500f, 300f);

    /// <summary>A street from inside a map of 500 by 600 m to its east edge, which the survey says it leaves the map at.</summary>
    static Survey RunningOff() => new()
    {
        Name = "Traced", Relation = 1, WidthM = EdgeM.X, HeightM = 600f, PointsM = [100f, EdgeM.Y, EdgeM.X, EdgeM.Y],
        Ways = [Way(0, 1)], LeavesTheMap = [false, true], Sea = [],
    };

    /// <summary>
    /// <b>No tree's crown leaves the map</b> (GEN-57): of two trees mapped well clear of the street, the one a metre in
    /// from the map's east edge is not laid.
    /// </summary>
    [Fact]
    public void NoTreesCrownLeavesTheMap()
    {
        var plan = TracedPlan.Lay(
            Surveyed([100, 500, 900, 500], OsmTurns.None, [], [], [new Vector2(500f, 300f), new Vector2(999f, 300f)], Way(0, 1)), Config,
            BuildingSizes.None);

        Assert.Equal([new Vector2(500f, 300f)], plan.Props.CentreM);
    }

    /// <summary>
    /// <b>A ring of roundabout ways is one roundabout of those roads</b> (GEN-19): a square ring with a street into
    /// each of two corners circulates on its four sides and on nothing else.
    /// </summary>
    [Fact]
    public void ARingOfRoundaboutWaysIsOneRoundabout()
    {
        var plan = Laid(
            [400, 400, 600, 400, 600, 600, 400, 600, 100, 400, 900, 600],
            OneWay(0, 1, 2) with { Roundabout = true }, OneWay(2, 3, 0) with { Roundabout = true }, Way(4, 0), Way(2, 5));

        var ring = Assert.Single(Enumerable.Range(0, plan.Roundabouts.Count));
        var circulating = plan.Roundabouts.RoadsOf(ring).ToArray();
        Assert.All(circulating, road => Assert.Equal(1, plan.Roads.LanesOn(road)));
        Assert.Equal(plan.Roads.Count - 2, circulating.Length);
    }

    /// <summary>
    /// A street from point 0 to point 3 with two turnings north off it, at point 1 to point 4 and at point 2 to
    /// point 5.
    /// </summary>
    static readonly float[] Ladder = [100, 500, 400, 500, 600, 500, 900, 500, 400, 100, 600, 100];

    /// <summary>The controls of a survey's points, every point not named unsigned.</summary>
    static PointControl[] Controlled(params (int Point, PointControl Control)[] controls)
    {
        var all = new PointControl[16];
        foreach (var (point, control) in controls) all[point] = control;
        return all;
    }

    /// <summary>Two places within a tolerance.</summary>
    sealed class VectorWithin(float toleranceM) : IEqualityComparer<Vector2>
    {
        public bool Equals(Vector2 one, Vector2 other) => Vector2.Distance(one, other) <= toleranceM;

        public int GetHashCode(Vector2 obj) => 0;
    }

    /// <summary>
    /// A junction of three arms at point 1: west from point 0, east to point 2 and north to point 3, traffic keeping
    /// right so a car from the west turns left to the north.
    /// </summary>
    static readonly float[] Tee = [100, 500, 500, 500, 900, 500, 500, 100];

    static readonly Vector2 West = new(100f, 500f);

    static readonly Vector2 East = new(900f, 500f);

    static readonly Vector2 North = new(500f, 100f);

    const long WestWay = 1;

    const long EastWay = 2;

    const long NorthWay = 3;

    /// <summary>
    /// A road of three points running east to west through point 1, from point 0 to point 2, and a branch leaving it at
    /// point 1 for point 3, 30° off to the near side — inside <see cref="RoadFigures.TurnStraightToleranceDeg"/>.
    /// </summary>
    static readonly float[] Fork = [900, 500, 500, 500, 100, 500, 100, 269];

    static readonly Vector2 ForkEast = new(900f, 500f);

    static readonly Vector2 ForkBranch = new(100f, 269f);

    /// <summary>
    /// A road east from point 0 through point 1 to point 2, bending at point 1 a few degrees to the near side — straight
    /// on, and numbered from the kerb.
    /// </summary>
    static readonly float[] BendingNear = [100, 500, 500, 500, 900, 540];

    static readonly Vector2 BendingNearEnd = new(900f, 540f);

    /// <summary>The same road bending at point 1 a few degrees to the far side instead.</summary>
    static readonly float[] BendingFar = [100, 500, 500, 500, 900, 460];

    static readonly Vector2 BendingFarEnd = new(900f, 460f);

    /// <summary>
    /// A main road west to east along y = 500 from point 0 through point 1 and point 2 to point 3, a side street from
    /// point 4 north of it into point 1 and one from point 5 south of it into point 2, this far east.
    /// </summary>
    static float[] Staggered(float staggerM) => [100, 500, 500, 500, 500 + staggerM, 500, 900, 500, 500, 100, 500 + staggerM, 900];

    /// <summary>
    /// <b>A side street meeting a dual carriageway through the gap in its median</b>: the eastbound carriageway along
    /// y = 500 from point 0 through point 1 to point 2, the westbound one <see cref="MedianM"/> north of it from point 5
    /// through point 4 to point 3, the side street from point 6 south of it into point 1, and the gap from point 1 to
    /// point 4 — half as long as junctions are gathered across.
    /// </summary>
    static readonly float[] AcrossAMedian =
        [100, 500, 500, 500, 900, 500, 100, 500 - MedianM, 500, 500 - MedianM, 900, 500 - MedianM, 500, 900];

    /// <summary>How far apart <see cref="AcrossAMedian"/>'s carriageways stand.</summary>
    static float MedianM => Config.CityGen.TracedJunctionsMergedM * 0.5f;

    static readonly Vector2 MedianFarWestM = new(100f, 500f - MedianM);

    static readonly Vector2 SideStreetM = new(500f, 900f);

    const long SideStreetWay = 4;

    const long MedianGapWay = 5;

    static SurveyWay SideStreet => Way(6, 1) with { OsmId = SideStreetWay };

    static SurveyWay MedianGap => Way(1, 4) with { OsmId = MedianGapWay };

    static OsmTurns Turns(OsmTurnRestriction restriction) => new() { Restrictions = [restriction], LaneLinks = [] };

    static OsmTurnRestriction Restricted(bool only, long onto) =>
        new() { Relation = 1, From = WestWay, Via = 1, To = onto, Only = only };

    /// <summary>Every road a lane of one road is joined onto — or one lane of it, counted from the kerb.</summary>
    static HashSet<int> Reached(CityPlan plan, int fromRoad, int? fromKerb = null)
    {
        var lanes = plan.Paving(Config).Lanes;
        var reached = new HashSet<int>();
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var from = lanes.ConnectorFromLane[connector];
            if (lanes.LaneRoad[from] == fromRoad && (fromKerb is null || lanes.LaneFromKerb[from] == fromKerb))
            {
                reached.Add(lanes.LaneRoad[lanes.ConnectorToLane[connector]]);
            }
        }

        return reached;
    }

    /// <summary>Every pair of lanes a movement joins from one road onto another, each counted from its kerb.</summary>
    static (int FromKerb, int ToKerb)[] Joined(CityPlan plan, int fromRoad, int toRoad)
    {
        var lanes = plan.Paving(Config).Lanes;
        return [.. Enumerable.Range(0, lanes.ConnectorCount)
            .Where(connector => lanes.LaneRoad[lanes.ConnectorFromLane[connector]] == fromRoad
                && lanes.LaneRoad[lanes.ConnectorToLane[connector]] == toRoad)
            .Select(connector => ((int)lanes.LaneFromKerb[lanes.ConnectorFromLane[connector]], (int)lanes.LaneFromKerb[lanes.ConnectorToLane[connector]]))];
    }

    /// <summary>How near a place laid on a straight road is read as the place asked for: a float's rounding, with room.</summary>
    const float SagM = 0.01f;

    /// <summary>A survey of the ways given over the points given, on a map reaching a little past the furthest of them.</summary>
    static CityPlan Laid(float[] pointsM, params SurveyWay[] ways) => Laid(pointsM, OsmTurns.None, ways);

    /// <summary>
    /// <b>A street carried over another on a bridge</b>: west to east along y = 500, on a bridge way from x = 400 to
    /// x = 600, over a street running north to south along x = 500 — one lane each way on both.
    /// </summary>
    internal static CityPlan OverAStreet() => Laid(
        [100, 500, 400, 500, 600, 500, 900, 500, 500, 100, 500, 900], Way(0, 1), Way(1, 2) with { Bridge = true }, Way(2, 3), Way(4, 5));

    /// <summary>
    /// <b>A street of two lanes east into a tee</b>, west to east along y = 500 from x = 100, through the tee at x = 900
    /// and on east two lanes to x = 1300 and one lane to x = 1700 — its arm to the north, a lane each way, turned onto
    /// from the inner lane alone — and a bridge carried over it at x = 300, so the town stands a car on its kerb lane
    /// short of the bridge (<see cref="TracedBridgeCars"/>). <paramref name="againstWest"/> lanes run back west along its
    /// western half.
    /// </summary>
    internal static CityPlan TwoLanesIntoATee(int againstWest = 0) => Laid(
        [100, 500, 900, 500, 1300, 500, 900, 100, 300, 100, 300, 300, 300, 700, 300, 900, 1700, 500],
        Carried(2, againstWest, LaneM, 0f, 0, 1), Carried(2, 0, LaneM, 0f, 1, 2), OneWay(2, 8), Way(1, 3), Way(4, 5),
        Way(5, 6) with { Bridge = true }, Way(6, 7));

    /// <summary>Where the north arm of <see cref="TwoLanesIntoATee"/> ends.</summary>
    internal static readonly Vector2 TeeNorthM = new(900f, 100f);

    /// <summary>Where the street of <see cref="TwoLanesIntoATee"/> runs on in one lane, east of the tee.</summary>
    internal static readonly Vector2 TeeOneLaneM = new(1300f, 500f);

    /// <summary>A street of these lanes each way, west to east, joined to nothing.</summary>
    internal static CityPlan AStreet(int forward, int backward) =>
        Laid([100, 500, 900, 500], Carried(forward, backward, LaneM, 0f, 0, 1));

    /// <summary>Where the bridge of <see cref="OverAStreet"/> crosses the street under it.</summary>
    internal static readonly Vector2 OverAStreetCrossingM = new(500f, 500f);

    /// <summary>The same, with where OSM says a car may turn over them.</summary>
    static CityPlan Laid(float[] pointsM, OsmTurns turns, params SurveyWay[] ways) => Laid(pointsM, turns, [], [], ways);

    /// <summary>The same, with the controls and crossings a survey's pack lays over them.</summary>
    static CityPlan Laid(float[] pointsM, PointControl[] controls, SurveyCrossing[] crossings, params SurveyWay[] ways) =>
        Laid(pointsM, OsmTurns.None, controls, crossings, ways);

    static CityPlan Laid(float[] pointsM, OsmTurns turns, PointControl[] controls, SurveyCrossing[] crossings, params SurveyWay[] ways) =>
        TracedPlan.Lay(Surveyed(pointsM, turns, controls, crossings, [], ways), Config, BuildingSizes.None);

    /// <summary>The survey those are, with the trees given mapped over it.</summary>
    static Survey Surveyed(
        float[] pointsM, OsmTurns turns, PointControl[] controls, SurveyCrossing[] crossings, Vector2[] treeM, params SurveyWay[] ways)
    {
        var furthestM = Vector2.Zero;
        for (var point = 0; point < pointsM.Length; point += 2)
        {
            furthestM = Vector2.Max(furthestM, new Vector2(pointsM[point], pointsM[point + 1]));
        }

        return new Survey
        {
            Name = "Traced", Relation = 1, WidthM = furthestM.X + 100f, HeightM = furthestM.Y + 100f, PointsM = pointsM, Ways = ways, Sea = [],
            Turns = turns, Controls = controls, Crossings = crossings, TreeM = treeM,
        };
    }

    const float LaneM = OsmCarriageway.AssumedLaneWidthM;

    static SurveyWay Way(params int[] points) => Carried(1, 1, LaneM, 0f, points);

    static SurveyWay OneWay(params int[] points) => Carried(1, 0, LaneM, 0f, points);

    static SurveyWay Shared(params int[] points) => new()
    {
        Highway = "service", LanesForward = 0, LanesBackward = 0, LanesShared = 1, CarriagewayM = LaneM,
        CentreOffsetM = 0f, Points = points,
    };

    /// <summary>A residential way of a lane each way and a roadside at each kerb.</summary>
    static SurveyWay Edged(params int[] points) => Carried(1, 1, LaneM, 0f, points) with
    {
        CarriagewayM = (2 * LaneM) + (2 * RoadsideM), RoadsideAlongM = RoadsideM, RoadsideAgainstM = RoadsideM,
    };

    static readonly float RoadsideM = Config.CityGen.TracedRoadsideWidthM;

    /// <summary>A residential way of a lane each way and one roadside, beside the kerb the traffic along it keeps to.</summary>
    static SurveyWay Kerbed(params int[] points) => Carried(1, 1, LaneM, 0f, points) with
    {
        CarriagewayM = (2 * LaneM) + RoadsideM, RoadsideAlongM = RoadsideM,
    };

    /// <summary>A residential way of this many lanes each way.</summary>
    static SurveyWay EachWay(int lanes, params int[] points) => Carried(lanes, lanes, LaneM, 0f, points);

    /// <summary>A residential way of these lanes, each this wide, its carriageway's middle this far right of it.</summary>
    static SurveyWay Carried(int forward, int backward, float laneM, float centreOffsetM, params int[] points) => new()
    {
        Highway = "residential", LanesForward = forward, LanesBackward = backward, LanesShared = 0,
        CarriagewayM = (forward + backward) * laneM, CentreOffsetM = centreOffsetM, Points = points,
    };

    /// <summary>A road with an end at the junction standing at a place.</summary>
    static int RoadEndingAt(CityPlan plan, Vector2 atM)
    {
        var junction = Array.FindIndex(plan.Junctions.CentreM, centreM => Vector2.Distance(centreM, atM) < 1e-3f);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.FromJunction[road] == junction || plan.Roads.ToJunction[road] == junction) return road;
        }

        return -1;
    }

    /// <summary>How many road ends stand at the junction at a place, or nought where no junction stands there.</summary>
    static int ArmsAt(CityPlan plan, Vector2 atM)
    {
        var junction = Array.FindIndex(plan.Junctions.CentreM, centreM => Vector2.Distance(centreM, atM) < 1e-3f);
        if (junction < 0) return 0;

        var arms = 0;
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.FromJunction[road] == junction) arms++;
            if (plan.Roads.ToJunction[road] == junction) arms++;
        }

        return arms;
    }
}
