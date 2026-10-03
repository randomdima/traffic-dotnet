using System.Numerics;
using TrafficSimulation.CityGen.Traced;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A road way's lanes are what OSM's tagging rules say</b> (Key:lanes, Key:width, Proposed_features/placement):
/// the count each way and its assumption where untagged, the side each way keeps, every lane's width, where the
/// way lies across them, each lane's own <c>:lanes</c> entry, and the mitred line a lane of a polyline way is.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class OsmCarriagewayTests
{
    const float LaneM = OsmCarriageway.AssumedLaneWidthM;

    /// <summary><b>An untagged street is one lane each way</b>, each at the assumed width, the way down its middle.</summary>
    [Fact]
    public void AnUntaggedStreetIsOneLaneEachWay()
    {
        var carriageway = Read(("highway", "residential"));

        Assert.Equal([OsmLaneWay.Backward, OsmLaneWay.Forward], carriageway.Lanes.Select(lane => lane.Way));
        Assert.Equal(2 * LaneM, carriageway.WidthM);
        Assert.Equal(0f, carriageway.CentreOffsetM);
        Assert.Equal(OsmLanesFrom.Assumed, carriageway.LanesFrom);
    }

    /// <summary><b>An untagged service road is one lane both ways share</b>, as Key:lanes assumes of it.</summary>
    [Fact]
    public void AnUntaggedServiceRoadIsOneSharedLane()
    {
        Assert.Equal([OsmLaneWay.Both], Read(("highway", "service")).Lanes.Select(lane => lane.Way));
    }

    /// <summary><b>An untagged one-way motorway is two lanes</b>.</summary>
    [Fact]
    public void AnUntaggedOneWayMotorwayIsTwoLanes()
    {
        Assert.Equal([OsmLaneWay.Forward, OsmLaneWay.Forward], Read(("highway", "motorway")).Lanes.Select(lane => lane.Way));
    }

    /// <summary>
    /// <b>An odd count with no side tagged gives the odd lane to the way the way is drawn</b>, and the lanes
    /// against it keep to its left.
    /// </summary>
    [Fact]
    public void AnOddCountGivesTheOddLaneToTheWayItIsDrawn()
    {
        var carriageway = Read(("highway", "primary"), ("lanes", "3"));

        Assert.Equal([OsmLaneWay.Backward, OsmLaneWay.Forward, OsmLaneWay.Forward], carriageway.Lanes.Select(lane => lane.Way));
        Assert.Equal(-LaneM, carriageway.LaneOffsetM(0));
    }

    /// <summary>
    /// <b>A count more than both sides claim is lanes driven both ways down the middle</b>: six lanes of which
    /// two run each way leave a tidal pair, driven whichever way the time of day says.
    /// </summary>
    [Fact]
    public void ACountMoreThanBothSidesClaimIsLanesDrivenBothWays()
    {
        var carriageway = Read(("highway", "primary"), ("lanes", "6"), ("lanes:forward", "2"), ("lanes:backward", "2"));

        Assert.Equal(
            [OsmLaneWay.Backward, OsmLaneWay.Backward, OsmLaneWay.Both, OsmLaneWay.Both, OsmLaneWay.Forward, OsmLaneWay.Forward],
            carriageway.Lanes.Select(lane => lane.Way));
    }

    /// <summary>
    /// <b><c>placement:forward</c> counts a lane both ways share as a forward lane</b>: a way along the right edge
    /// of a street's one shared lane has the carriageway's middle half a lane to its left.
    /// </summary>
    [Fact]
    public void PlacementForwardCountsASharedLane()
    {
        var carriageway = Read(("highway", "residential"), ("lanes", "1"), ("placement:forward", "right_of:1"));

        Assert.Equal(-0.5f * LaneM, carriageway.CentreOffsetM, 1e-5f);
    }

    /// <summary><b>A way drawn against its traffic has every lane running against it</b>.</summary>
    [Fact]
    public void AWayDrawnAgainstItsTrafficRunsAgainstIt()
    {
        var carriageway = Read(("highway", "secondary"), ("oneway", "-1"), ("lanes", "2"));

        Assert.All(carriageway.Lanes, lane => Assert.Equal(OsmLaneWay.Backward, lane.Way));
    }

    /// <summary><b>A tagged width is the carriageway's, shared evenly between its lanes</b>.</summary>
    [Fact]
    public void ATaggedWidthIsSharedBetweenTheLanes()
    {
        var carriageway = Read(("highway", "tertiary"), ("lanes", "4"), ("width", "10"));

        Assert.All(carriageway.Lanes, lane => Assert.Equal(2.5f, lane.WidthM));
        Assert.Equal(OsmWidthFrom.Carriageway, carriageway.WidthFrom);
    }

    /// <summary>
    /// <b>A way drawn along the left edge of its first lane has the carriageway's middle half its width to the
    /// right</b> (<c>placement=left_of:1</c>).
    /// </summary>
    [Fact]
    public void AWayAlongItsFirstLanesLeftEdgeHasTheMiddleToItsRight()
    {
        var carriageway = Read(("highway", "primary"), ("oneway", "yes"), ("lanes", "3"), ("placement", "left_of:1"));

        Assert.Equal(1.5f * LaneM, carriageway.CentreOffsetM, 1e-5f);
    }

    /// <summary>
    /// <b><c>placement:forward</c> counts only the forward lanes</b>: on one lane back and two forward, the way
    /// along the forward lanes' left edge stands a lane in from the left of three.
    /// </summary>
    [Fact]
    public void PlacementForwardCountsOnlyTheForwardLanes()
    {
        var carriageway = Read(
            ("highway", "primary"), ("lanes", "3"), ("lanes:forward", "2"), ("lanes:backward", "1"), ("placement:forward", "left_of:1"));

        Assert.Equal(0.5f * LaneM, carriageway.CentreOffsetM, 1e-5f);
    }

    /// <summary>
    /// <b>A backward lane's <c>:lanes</c> entry is counted as its own traffic looks</b>: from the middle of the
    /// road out, so the first entry is the backward lane next to the forward ones.
    /// </summary>
    [Fact]
    public void ABackwardLanesEntryIsCountedAsItsTrafficLooks()
    {
        var carriageway = Read(
            ("highway", "primary"), ("lanes:forward", "1"), ("lanes:backward", "2"), ("turn:lanes:backward", "left|through"));

        Assert.Equal(["through", "left", null], carriageway.Lanes.Select(lane => lane.Turn));
    }

    /// <summary>
    /// <b>A lane of a polyline way is the way stood off at every corner by the mitre</b>: one lane's width to the
    /// right of a way running east then turning south (y runs south) passes the corner's mitre point.
    /// </summary>
    [Fact]
    public void ALaneOfAPolylineIsMitredAtEveryCorner()
    {
        var laneM = new Vector2[3];
        OsmCarriageway.OffsetInto([new(0f, 0f), new(10f, 0f), new(10f, 10f)], 1f, laneM);

        Vector2[] mitredM = [new(0f, 1f), new(9f, 1f), new(9f, 10f)];
        for (var at = 0; at < mitredM.Length; at++) Assert.InRange(Vector2.Distance(laneM[at], mitredM[at]), 0f, 1e-5f);
    }

    static OsmCarriageway Read(params (string Key, string Value)[] tags) =>
        OsmCarriageway.Read(tags.ToDictionary(tag => tag.Key, tag => tag.Value))!;
}
