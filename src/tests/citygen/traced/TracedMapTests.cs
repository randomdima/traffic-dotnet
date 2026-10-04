using System.Numerics;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A traced map's file reads back what was written, and is cut down in place</b> (GEN-57, <see cref="TracedMap"/>):
/// every road, lane, turn, control, crossing and tree as written, every place to the millimetre and every
/// footprint's point to the centimetre; a file cut short refused; and a crop keeping what stands in its frame, a road
/// running on past it as far as the engine reads it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class TracedMapTests
{
    /// <summary><b>What is written is read back</b>, every road, lane, turn, control, crossing and tree as it was given.</summary>
    [Fact]
    public void AMapReadsBackWhatWasWritten()
    {
        var written = Everything();

        var read = RoundTrip(written);

        Assert.Equal((written.Name, written.Description, written.Licence, written.OsmBase, written.Relation),
                     (read.Name, read.Description, read.Licence, read.OsmBase, read.Relation));
        Assert.Equal((written.Frame.Lat0Deg, written.Frame.WestM, written.Frame.SouthM, written.Frame.WidthM, written.Frame.MarginM),
                     (read.Frame.Lat0Deg, read.Frame.WestM, read.Frame.SouthM, read.Frame.WidthM, read.Frame.MarginM));
        Assert.Equal(written.Roads.Select(Said), read.Roads.Select(Said));
        Assert.Equal(written.Coast, read.Coast);
        Assert.Equal(written.Turns.Restrictions.Select(turn => (turn.Relation, turn.From, turn.Via, turn.To, turn.Only)),
                     read.Turns.Restrictions.Select(turn => (turn.Relation, turn.From, turn.Via, turn.To, turn.Only)));
        Assert.Equal(written.Turns.LaneLinks.Select(link => (link.Relation, link.From, link.Via, link.To, link.FromLane, link.ToLane)),
                     read.Turns.LaneLinks.Select(link => (link.Relation, link.From, link.Via, link.To, link.FromLane, link.ToLane)));
        Assert.Equal(written.Controls.Point, read.Controls.Point);
        Assert.Equal(written.Controls.Control, read.Controls.Control);
        Assert.Equal(written.Controls.Cluster, read.Controls.Cluster);
        Assert.Equal(written.Crossings.Way, read.Crossings.Way);
        Assert.Equal(written.Crossings.AtM, read.Crossings.AtM);
        Assert.Equal(written.Crossings.Kind, read.Crossings.Kind);
        Assert.Equal(written.Crossings.Painted, read.Crossings.Painted);
        Assert.Equal(written.Crossings.Junction, read.Crossings.Junction);
        Assert.Equal(written.Footprints.RingOffsets, read.Footprints.RingOffsets);
        Assert.Equal(written.Footprints.PointOffsets, read.Footprints.PointOffsets);
        Assert.Equal(written.Footprints.Traced, read.Footprints.Traced);
        Assert.Equal(written.Footprints.HeightM, read.Footprints.HeightM);
        Assert.Equal(written.Footprints.Use, read.Footprints.Use);
        Assert.Equal(written.TreeM, read.TreeM);
    }

    /// <summary>
    /// <b>A place reads back to the millimetre, and a footprint's to the centimetre</b>, however far across the map
    /// it stands: each is written as its step from the one before, and the steps add up to where it was.
    /// </summary>
    [Fact]
    public void PlacesReadBackToTheirSteps()
    {
        Vector2[] footprintM = [new(17_349.996f, 39_421.004f), new(17_312.25f, 39_400.5f), new(0.004f, 0.006f)];
        var written = Map([new(17_349.9996, 39_421.0004), new(0.0004, 0.0006), new(-512.3456, 40_000.0)]) with
        {
            Footprints = new TracedMap.FootprintArrays
            {
                RingOffsets = [0, 1], PointOffsets = [0, footprintM.Length], PointM = footprintM, Traced = [false], HeightM = [0f],
                Use = [FootprintUse.Unknown],
            },
        };

        var read = RoundTrip(written);

        for (var at = 0; at < written.PointM.Length; at++)
        {
            Assert.Equal(Math.Round(written.PointM[at].X * 1000) / 1000, read.PointM[at].X, 1e-9);
            Assert.Equal(Math.Round(written.PointM[at].Y * 1000) / 1000, read.PointM[at].Y, 1e-9);
        }

        for (var at = 0; at < footprintM.Length; at++)
        {
            Assert.Equal(MathF.Round(footprintM[at].X * 100f) / 100f, read.Footprints.PointM[at].X, 1e-3f);
            Assert.Equal(MathF.Round(footprintM[at].Y * 100f) / 100f, read.Footprints.PointM[at].Y, 1e-3f);
        }
    }

    /// <summary><b>A map cut short is refused</b> rather than read as a smaller place — what a page holds of one before it is opened.</summary>
    [Fact]
    public void AMapCutShortIsRefused()
    {
        using var stream = new MemoryStream();
        Everything().Write(stream);

        Assert.Throws<InvalidDataException>(() => TracedMap.Read(stream.GetBuffer().AsSpan(0, (int)stream.Length - 1), "test"));
    }

    /// <summary>
    /// <b>A crop moves every place by its corner and keeps the frame's projection</b>: a point 300 m east and 320 m
    /// south of the map's corner stands 100 m east and 120 m south of a crop's at (200, 200), and the frame's west and
    /// north edges move by as much.
    /// </summary>
    [Fact]
    public void ACropMovesEveryPlaceByItsCorner()
    {
        var map = Map([new(300, 320), new(400, 320)], Road(1, 0, 1));

        var cropped = map.Cropped(200, 200, 500, 400);

        Assert.Equal(new Vector2D(100, 120), cropped.PointM[cropped.Roads[0].Points[0]]);
        Assert.Equal(map.Frame.WestM + 200, cropped.Frame.WestM);
        Assert.Equal(map.Frame.SouthM + map.Frame.HeightM - 200, cropped.Frame.SouthM + cropped.Frame.HeightM);
        Assert.Equal((map.Frame.Lat0Deg, map.Frame.Lon0Deg), (cropped.Frame.Lat0Deg, cropped.Frame.Lon0Deg));
    }

    /// <summary>
    /// <b>A road running on past a crop keeps one point beyond its frame at either end</b> — all the engine reads
    /// of it, since it is cut where it crosses — and a road wholly outside is left out with its points.
    /// </summary>
    [Fact]
    public void ARoadPastTheCropKeepsOnePointBeyondIt()
    {
        // The crop's frame runs 200..700 m east of the old corner.
        var map = Map(
            [new(20, 400), new(100, 400), new(300, 400), new(500, 400), new(800, 400), new(950, 400), new(900, 700), new(950, 700)],
            Road(1, 0, 1, 2, 3, 4, 5), Road(2, 6, 7));

        var cropped = map.Cropped(200, 200, 500, 400);

        var road = Assert.Single(cropped.Roads);
        Assert.Equal([-100.0, 100.0, 300.0, 600.0], road.Points.Select(point => cropped.PointM[point].X));
        Assert.Equal(4, cropped.PointM.Length);
    }

    /// <summary><b>A road crossing a crop's edge is cut on the new frame's own edge</b>, which is where it leaves the map.</summary>
    [Fact]
    public void ARoadAcrossTheCropIsCutOnItsEdge()
    {
        var map = Map([new(100, 400), new(900, 400)], Road(1, 0, 1));

        var survey = Survey.Of(map.Cropped(200, 200, 500, 400), SimConfig.Shipped());

        var way = Assert.Single(survey.Ways);
        Assert.Equal(0f, survey.PointM(way.Points[0]).X);
        Assert.Equal(500f, survey.PointM(way.Points[^1]).X);
    }

    /// <summary>
    /// <b>A crop keeps a footprint wholly inside its frame, and a tree, a crossing, a control or a turn only where it
    /// still stands on it</b>: one footprint astride the edge, one tree, crossing, control and turn outside are gone.
    /// </summary>
    [Fact]
    public void ACropKeepsWhatStandsInItsFrame()
    {
        var map = Map([new(300, 400), new(500, 400), new(900, 700), new(950, 700)], Road(1, 0, 1), Road(2, 2, 3)) with
        {
            Turns = new OsmTurns
            {
                Restrictions =
                [
                    new OsmTurnRestriction { Relation = 1, From = 1, Via = 1, To = 1, Only = false },
                    new OsmTurnRestriction { Relation = 2, From = 2, Via = 3, To = 2, Only = false },
                ],
                LaneLinks = [],
            },
            Controls = new TracedMap.ControlArrays { Point = [1, 2], Control = [SurveyControl.Signals, SurveyControl.Signs], Cluster = [0, 0] },
            Crossings = new TracedMap.CrossingArrays
            {
                Way = [1, 2], AtM = [new(400, 400), new(920, 700)], Kind = [SurveyCrossingKind.Zebra, SurveyCrossingKind.Zebra],
                Painted = [true, true], Junction = [1, 3],
            },
            Footprints = new TracedMap.FootprintArrays
            {
                RingOffsets = [0, 1, 2], PointOffsets = [0, 3, 6],
                PointM = [new(300, 300), new(320, 300), new(320, 320), new(690, 300), new(710, 300), new(710, 320)],
                Traced = [false, true], HeightM = [9f, 12f], Use = [FootprintUse.Apartments, FootprintUse.Unknown],
            },
            TreeM = [new(250, 250), new(150, 250)],
        };

        var cropped = map.Cropped(200, 200, 500, 400);

        Assert.Equal([1L], cropped.Turns.Restrictions.Select(turn => turn.Relation));
        Assert.Equal([SurveyControl.Signals], cropped.Controls.Control);
        Assert.Equal([1L], cropped.Crossings.Way);
        Assert.Equal([new Vector2(100, 100), new Vector2(120, 100), new Vector2(120, 120)], cropped.Footprints.PointM);
        Assert.Equal([new Vector2(50, 50)], cropped.TreeM);
    }

    /// <summary>
    /// <b>A stump runs from its dead end back to the first place three ways meet</b>, on through a place where two ways
    /// only run on into each other: a street through a junction with a lane off it in two ways, the second ending in a
    /// building, is one stump of both ways, ending inside.
    /// </summary>
    [Fact]
    public void AStumpRunsBackToWhereThreeWaysMeet()
    {
        var stumps = Driveway().Stumps();

        var stump = Assert.Single(stumps, stump => stump.EndM == new Vector2D(300, 500));
        Assert.Equal([(2, 0, false), (1, 0, false)], stump.Cuts);
        Assert.True(stump.EndsInside);
        Assert.Equal(200f, stump.LengthM);
    }

    /// <summary><b>A road running off the map is no stump</b>: its last point past the frame is where it leaves, not a dead end.</summary>
    [Fact]
    public void ARoadRunningOffTheMapIsNoStump()
    {
        var stumps = Map([new(100, 400), new(500, 400), new(1200, 400)], Road(1, 0, 1, 2)).Stumps();

        Assert.Equal([new Vector2D(100, 400)], stumps.Select(stump => stump.EndM));
    }

    /// <summary>
    /// <b>Dropping a stump drops its roads and what stood on them</b>: the lane's two ways go, the street stays whole, and
    /// the control and crossing on the lane go with it.
    /// </summary>
    [Fact]
    public void DroppingAStumpDropsWhatStoodOnIt()
    {
        var map = Driveway();

        var dropped = map.Without(map.Stumps().Where(stump => stump.EndsInside));

        Assert.Equal([1L], dropped.Roads.Select(road => road.OsmId));
        Assert.Empty(dropped.Controls.Point);
        Assert.Empty(dropped.Crossings.Way);
    }

    /// <summary>
    /// A street from (100, 300) to (500, 300) through a junction at (300, 300), and a lane off it south in two ways to a
    /// dead end at (300, 500) inside a building — a control on its bend and a crossing over its second way.
    /// </summary>
    static TracedMap Driveway() =>
        Map([new(100, 300), new(300, 300), new(500, 300), new(300, 400), new(300, 500)], Road(1, 0, 1, 2), Road(2, 1, 3), Road(3, 3, 4)) with
        {
            Controls = new TracedMap.ControlArrays { Point = [3], Control = [SurveyControl.Signs], Cluster = [0] },
            Crossings = new TracedMap.CrossingArrays
            {
                Way = [3], AtM = [new(300, 450)], Kind = [SurveyCrossingKind.Zebra], Painted = [true], Junction = [TracedMap.NoJunction],
            },
            Footprints = new TracedMap.FootprintArrays
            {
                RingOffsets = [0, 1], PointOffsets = [0, 4], PointM = [new(280, 480), new(320, 480), new(320, 520), new(280, 520)],
                Traced = [false], HeightM = [9f], Use = [FootprintUse.House],
            },
        };

    /// <summary>Everything a road says, as one line.</summary>
    static string Said(TracedRoad road) =>
        $"{road.OsmId} {road.Highway} {road.Bridge} {road.Tunnel} {road.Roundabout} {road.Marked} {road.Measured} "
        + $"{road.Carriageway.CentreOffsetM} {road.Carriageway.LanesFrom} {road.Carriageway.WidthFrom} "
        + string.Join(" ", road.Carriageway.Lanes.Select(lane => $"{lane.Way}:{lane.WidthM}:{lane.Arrows}"))
        + $" [{string.Join(",", road.Points)}]";

    static TracedMap RoundTrip(TracedMap map)
    {
        using var stream = new MemoryStream();
        map.Write(stream);
        return TracedMap.Read(stream.GetBuffer().AsSpan(0, (int)stream.Length), "test");
    }

    /// <summary>A map of a thousand metres by eight hundred and its margin, holding the points and roads given and nothing else.</summary>
    static TracedMap Map(Vector2D[] pointM, params TracedRoad[] roads) => new()
    {
        Name = "Hand",
        Description = "a few ways laid by hand",
        Licence = "© OpenStreetMap contributors",
        OsmBase = "2026-10-03T09:16:51Z",
        Relation = 12888405,
        Frame = new OsmFrame { Lat0Deg = 46.5, Lon0Deg = 30.7, WestM = -500.25, SouthM = -400.5, WidthM = 1000, HeightM = 800, MarginM = 60 },
        PointM = pointM,
        Roads = roads,
        Coast = [],
        Turns = OsmTurns.None,
        Controls = TracedMap.ControlArrays.None,
        Crossings = TracedMap.CrossingArrays.None,
        Footprints = TracedMap.FootprintArrays.None,
        TreeM = [],
    };

    static TracedRoad Road(long id, params int[] points) => new()
    {
        OsmId = id,
        Highway = "residential",
        Carriageway = OsmCarriageway.Read(new Dictionary<string, string> { ["highway"] = "residential" })!,
        Points = points,
    };

    /// <summary>A map holding one of everything a map holds, each with something other than its default.</summary>
    static TracedMap Everything() =>
        Map([new(10, 20), new(30, 20), new(30, 40), new(900, 700), new(950, 750), new(990, 790)],
            Road(-7, 0, 1),
            new TracedRoad
            {
                OsmId = 25481554, Highway = "primary", Bridge = true, Roundabout = true, Marked = true,
                Carriageway = new OsmCarriageway
                {
                    Lanes =
                    [
                        new OsmLane { Way = OsmLaneWay.Backward, WidthM = 3.25f, Arrows = OsmArrows.Left | OsmArrows.Through },
                        new OsmLane { Way = OsmLaneWay.Both, WidthM = 3f },
                        new OsmLane { Way = OsmLaneWay.Forward, WidthM = 3.75f, Arrows = OsmArrows.Right },
                    ],
                    CentreOffsetM = -1.625f, LanesFrom = OsmLanesFrom.Tagged, WidthFrom = OsmWidthFrom.Lanes,
                },
                Measured = (14.25f, MeasuredFrom.Imagery),
                Points = [1, 2, 0],
            }) with
        {
            Coast = [[3, 4, 5]],
            Turns = new OsmTurns
            {
                Restrictions = [new OsmTurnRestriction { Relation = 1670163, From = -7, Via = 1, To = 25481554, Only = true }],
                LaneLinks = [new OsmLaneLink { Relation = 15417414, From = 25481554, Via = 0, To = -7, FromLane = 2, ToLane = 1 }],
            },
            Controls = new TracedMap.ControlArrays { Point = [1], Control = [SurveyControl.Signals], Cluster = [10980417] },
            Crossings = new TracedMap.CrossingArrays
            {
                Way = [-7, 25481554], AtM = [new(1.5f, 2.5f), new(100.25f, 7f)], Kind = [SurveyCrossingKind.Zebra, SurveyCrossingKind.Signals],
                Painted = [true, null], Junction = [1, TracedMap.NoJunction],
            },
            Footprints = new TracedMap.FootprintArrays
            {
                RingOffsets = [0, 2], PointOffsets = [0, 4, 7],
                PointM = [new(100, 100), new(140, 100), new(140, 140), new(100, 140), new(110, 110), new(120, 110), new(120, 120)],
                Traced = [true], HeightM = [12.5f], Use = [FootprintUse.Garages],
            },
            TreeM = [new(40.5f, 60.25f)],
        };
}
