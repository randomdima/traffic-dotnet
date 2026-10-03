using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A survey is laid as the place it surveyed, and nothing of it is lost</b> (GEN-57): a junction where ways
/// share a point and nowhere else however close two stand, one road through a place traffic merely carries on
/// through, every surveyed point on its road, every piece kept, no road longer than a traced map lays one, and
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
    /// <b>Two places however close are two junctions, each where the survey put it</b>: a side street either
    /// side of a main road, staggered by a few metres, meets it at two tees with the short road between them.
    /// </summary>
    [Fact]
    public void PlacesHoweverCloseAreTwoJunctionsWhereTheSurveyPutThem()
    {
        const float staggerM = 3f;
        var plan = Laid(
            [100, 500, 500, 500, 500 + staggerM, 500, 900, 500, 500, 100, 500 + staggerM, 900],
            Way(0, 1, 2, 3), Way(4, 1), Way(5, 2));

        Assert.Equal(3, ArmsAt(plan, new Vector2(500f, 500f)));
        Assert.Equal(3, ArmsAt(plan, new Vector2(500f + staggerM, 500f)));
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
    /// <b>No point a way was surveyed through is thinned away</b>: a jog of half a metre is a jog in the road,
    /// which passes every surveyed point to within its corners' own sag.
    /// </summary>
    [Fact]
    public void EveryPointAWayWasSurveyedThroughIsOnItsRoad()
    {
        float[] pointsM = [100, 500, 300, 500, 310, 500.5f, 500, 500.5f];
        var plan = Laid(pointsM, Way(0, 1, 2, 3));

        var line = plan.Roads.SegmentsOf(0);
        var lengthM = Spline.TotalLengthM(line);
        foreach (var at in (ReadOnlySpan<int>)[1, 2])
        {
            var pointM = new Vector2(pointsM[2 * at], pointsM[(2 * at) + 1]);
            var onM = Spline.ProjectM(line, pointM, lengthM * 0.5f, lengthM);
            Assert.InRange(Vector2.Distance(Spline.SampleAt(line, onM).PositionM, pointM), 0f, SagM);
        }
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
    /// <b>A single lane both ways share is laid over one line</b>: one lane each way, both on the road's own line
    /// and each the whole of its width.
    /// </summary>
    [Fact]
    public void ASharedLaneIsLaidOverOneLine()
    {
        var plan = Laid([100, 500, 900, 500], Shared(0, 1));

        Assert.True(plan.Roads.DrivenOverOneLine(0));
        Assert.Equal((LaneM, 0f), (plan.Roads.LaneWidthM(0), plan.Roads.LaneOffsetM(0, 0)));
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

        var lanes = plan.Paving(Config).Lanes;
        var (west, east) = (RoadEndingAt(plan, West), RoadEndingAt(plan, East));
        var joined = Enumerable.Range(0, lanes.ConnectorCount)
            .Where(connector => lanes.LaneRoad[lanes.ConnectorFromLane[connector]] == west && lanes.LaneRoad[lanes.ConnectorToLane[connector]] == east)
            .Select(connector => (lanes.LaneFromKerb[lanes.ConnectorFromLane[connector]], lanes.LaneFromKerb[lanes.ConnectorToLane[connector]]));
        Assert.Equal([((byte)0, (byte)1)], joined);
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

    /// <summary>
    /// How far a corner of half a metre's jog rounded at half a street's carriageway stands off the point it
    /// rounds: its sag, a millimetre or two, read with room.
    /// </summary>
    const float SagM = 0.01f;

    /// <summary>A survey of the ways given over the points given, on a map reaching a little past the furthest of them.</summary>
    static CityPlan Laid(float[] pointsM, params SurveyWay[] ways) => Laid(pointsM, OsmTurns.None, ways);

    /// <summary>The same, with where OSM says a car may turn over them.</summary>
    static CityPlan Laid(float[] pointsM, OsmTurns turns, params SurveyWay[] ways)
    {
        var furthestM = Vector2.Zero;
        for (var point = 0; point < pointsM.Length; point += 2)
        {
            furthestM = Vector2.Max(furthestM, new Vector2(pointsM[point], pointsM[point + 1]));
        }

        var survey = new Survey
        {
            Name = "Traced", Relation = 1, WidthM = furthestM.X + 100f, HeightM = furthestM.Y + 100f, PointsM = pointsM, Ways = ways, Sea = [],
            Turns = turns,
        };

        return TracedPlan.Lay(survey, Config);
    }

    const float LaneM = OsmCarriageway.AssumedLaneWidthM;

    static SurveyWay Way(params int[] points) => Carried(1, 1, LaneM, 0f, points);

    static SurveyWay OneWay(params int[] points) => Carried(1, 0, LaneM, 0f, points);

    static SurveyWay Shared(params int[] points) => new()
    {
        Highway = "service", LanesForward = 0, LanesBackward = 0, LanesShared = 1, CarriagewayM = LaneM,
        CentreOffsetM = 0f, Points = points,
    };

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
