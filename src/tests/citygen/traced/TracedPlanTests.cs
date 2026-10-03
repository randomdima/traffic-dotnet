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
    /// <b>No lane folds back over a corner</b>: a way surveyed through a kink whose legs have no room to round
    /// it even with its innermost lane at the tightest a traced lane is laid
    /// (<see cref="CityGenFigures.TracedTightestLaneRadiusM"/>) is laid as the straight past the kink.
    /// </summary>
    [Fact]
    public void AKinkTheLegsCannotRoundIsTheStraightPastIt()
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
    /// How far a corner of half a metre's jog rounded at half a street's carriageway stands off the point it
    /// rounds: its sag, a millimetre or two, read with room.
    /// </summary>
    const float SagM = 0.01f;

    /// <summary>A survey of the ways given over the points given, on a map reaching a little past the furthest of them.</summary>
    static CityPlan Laid(float[] pointsM, params SurveyWay[] ways)
    {
        var furthestM = Vector2.Zero;
        for (var point = 0; point < pointsM.Length; point += 2)
        {
            furthestM = Vector2.Max(furthestM, new Vector2(pointsM[point], pointsM[point + 1]));
        }

        var survey = new Survey
        {
            Name = "Traced", Relation = 1, WidthM = furthestM.X + 100f, HeightM = furthestM.Y + 100f, PointsM = pointsM, Ways = ways, Sea = [],
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
