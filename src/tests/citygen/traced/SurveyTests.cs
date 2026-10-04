using System.Numerics;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>An OSM extract is imported and read into the engine's metres exactly as OSM drew it</b> (GEN-57): projected
/// true along its meridian, placed in the extract's own frame, its lanes taken as OSM means them, and the coast
/// closed against the map's edge with the sea on its right — each asked of a few nodes and ways laid by hand.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class SurveyTests
{
    /// <summary>How far the hand-laid frame reaches past the street on every side.</summary>
    const double MarginM = 60;

    /// <summary>
    /// <b>A metre north along the central meridian is a metre of the meridian's own arc</b>: WGS84's arc from
    /// the equator to 45°, 4 984 944.378 m.
    /// </summary>
    [Fact]
    public void TheProjectionIsTrueAlongItsMeridian()
    {
        var (eastM, northM) = new TransverseMercator(0.0, 30.0).Project(45.0, 30.0);

        Assert.Equal(0.0, eastM, 1e-6);
        Assert.Equal(4_984_944.378, northM, 1e-3);
    }

    /// <summary>
    /// <b>A way OSM draws against its traffic (<c>oneway=-1</c>) is read turned round</b>, so it runs one way
    /// the way its points do, and its lanes each way are read turned with it.
    /// </summary>
    [Fact]
    public void AWayDrawnAgainstItsTrafficIsTurnedRound()
    {
        var survey = Read(Way(1, ("highway", "primary"), ("oneway", "-1"), ("lanes:backward", "2")));

        Assert.True(survey.Ways[0].Oneway);
        Assert.Equal([1, 0], survey.Ways[0].Points);
        Assert.Equal(2, survey.Ways[0].LanesForward);
    }

    /// <summary><b>A roundabout runs one way without saying so</b>, the way OSM draws it.</summary>
    [Fact]
    public void ARoundaboutRunsOneWayWithoutSaying()
    {
        var survey = Read(Way(1, ("highway", "residential"), ("junction", "roundabout")));

        Assert.True(survey.Ways[0].Oneway);
    }

    /// <summary>
    /// <b>Every road way is read, whatever its class</b>, and a road drawn as an area — a surface, with no lane
    /// along it — stays in the extract and is not laid.
    /// </summary>
    [Fact]
    public void EveryRoadWayIsReadAndNoArea()
    {
        var survey = Read(
            Way(1, ("highway", "residential")), Way(2, ("highway", "service")), Way(3, ("highway", "track")),
            Way(4, ("highway", "residential"), ("area", "yes")));

        Assert.Equal([1L, 2L, 3L], survey.Ways.Select(way => way.OsmId));
    }

    /// <summary>
    /// <b>A node stands where the extract's frame puts it</b>: the street's south-west end, the frame's west and
    /// south edges' margin in from the map's south-west corner, with y running south.
    /// </summary>
    [Fact]
    public void ANodeStandsWhereTheFramePutsIt()
    {
        var survey = Read(Way(1, ("highway", "residential")));

        Assert.Equal((float)MarginM, survey.PointM(0).X, 1e-3f);
        Assert.Equal(survey.HeightM - (float)MarginM, survey.PointM(0).Y, 1e-3f);
    }

    /// <summary>
    /// <b>A way running on past the map is laid up to the map's edge and cut there</b>, the cut being where it leaves
    /// the map: a street from near the map's south-west corner to a node north of the map ends on its north edge.
    /// </summary>
    [Fact]
    public void AWayRunningPastTheMapIsCutAtItsEdge()
    {
        var survey = Read(new OsmWay
        {
            Id = 1, Tags = new Dictionary<string, string> { ["highway"] = "residential" }, Nodes = [0, 3],
            Carriageway = OsmCarriageway.Read(new Dictionary<string, string> { ["highway"] = "residential" }),
        });

        var way = Assert.Single(survey.Ways);
        Assert.Equal(0, way.Points[0]);
        Assert.Equal(0f, survey.PointM(way.Points[^1]).Y);
        Assert.Equal([false, true], new[] { survey.Leaves(way.Points[0]), survey.Leaves(way.Points[^1]) });
    }

    /// <summary>
    /// <b>A way's lanes are OSM's own count, each at the traced lane width</b>: a residential street tagged with
    /// nothing is one lane each way, and no roadside.
    /// </summary>
    [Fact]
    public void AWaysLanesAreOsmsOwnCountAtOneWidth()
    {
        var way = Read(Way(1, ("highway", "residential"))).Ways[0];

        Assert.Equal((1, 1, 0), (way.LanesForward, way.LanesBackward, way.LanesShared));
        Assert.Equal((2 * LaneM, 0f, 0f), (way.CarriagewayM, way.RoadsideAlongM, way.RoadsideAgainstM));
    }

    /// <summary>
    /// <b>Every lane is one width whatever OSM tags</b>: two lanes tagged 4.5 m wide each are laid at the traced lane
    /// width.
    /// </summary>
    [Fact]
    public void ALaneTaggedWiderIsLaidAtTheOneWidth()
    {
        var way = Read(Way(1, ("highway", "residential"), ("lanes", "2"), ("width", "9"))).Ways[0];

        Assert.Equal(2 * LaneM, way.CarriagewayM);
    }

    /// <summary>
    /// <b>A coast across the map is closed along the map's edge with the sea on its right</b>: OSM draws a
    /// coastline with the land on its left, so one drawn north up the middle of the map has the sea east of it.
    /// </summary>
    [Fact]
    public void ACoastAcrossTheMapClosesWithTheSeaOnItsRight()
    {
        var survey = Read(Way(1, ("highway", "residential")), Coast(2, 2, 3));

        var sea = Assert.Single(survey.Sea);
        Assert.True(Inside(sea, new Vector2(survey.WidthM * 0.9f, survey.HeightM * 0.5f)));
        Assert.False(Inside(sea, new Vector2(survey.WidthM * 0.1f, survey.HeightM * 0.5f)));
    }

    /// <summary>
    /// <b>An island is left out of the sea</b>: water carries no hole, so a coast closed inside the map is not
    /// drawn, and stays in the extract.
    /// </summary>
    [Fact]
    public void AnIslandIsLeftOutOfTheSea()
    {
        var survey = Read(Way(1, ("highway", "residential")), Coast(2, 4, 5, 6, 4));

        Assert.Empty(survey.Sea);
    }

    /// <summary>
    /// Seven nodes about Odesa: a street's two ends at its corners, a coast's two ends off the map south and
    /// north of its middle, and three points well inside it for an island.
    /// </summary>
    static readonly (int Lat, int Lon)[] Places =
    [
        (464_800_000, 307_300_000), (464_900_000, 307_400_000),
        (464_700_000, 307_350_000), (465_000_000, 307_350_000),
        (464_840_000, 307_340_000), (464_860_000, 307_340_000), (464_850_000, 307_360_000),
    ];

    /// <summary>
    /// <b>Where OSM assumes the count, a measured width says it, past a roadside at either kerb</b>: an untagged
    /// residential street read as wide as two lanes each way and two roadsides holds those, on a map where a
    /// residential street is tagged with two each way.
    /// </summary>
    [Fact]
    public void AnUntaggedStreetHoldsAsManyLanesAsItsWidthPastTwoRoadsides()
    {
        var readM = (4 * LaneM) + (2 * RoadsideM);
        var way = Read(Measured((1, readM, MeasuredFrom.Imagery)), Way(1, ("highway", "residential")), Tagged(2, "residential", 4)).Ways[0];

        Assert.Equal((2, 2, 0), (way.LanesForward, way.LanesBackward, way.LanesShared));
        Assert.Equal((RoadsideM, RoadsideM), (way.RoadsideAlongM, way.RoadsideAgainstM));
        Assert.Equal(readM, way.CarriagewayM, LineTolerance.RoundingM);
        Assert.True(way.LanesFromWidth);
    }

    /// <summary>
    /// <b>A street's width past its lanes is a roadside at each kerb</b>: a street tagged with a lane each way, read as
    /// wide as those and two roadsides and a little under half a roadside more, keeps its lanes and lays the two.
    /// </summary>
    [Fact]
    public void AStreetsWidthPastItsLanesIsARoadsideAtEachKerb()
    {
        var readM = (2 * LaneM) + (2.4f * RoadsideM);
        var way = Read(Measured((1, readM, MeasuredFrom.Imagery)), Way(1, ("highway", "residential"), ("lanes", "2"))).Ways[0];

        Assert.Equal((1, 1, 0), (way.LanesForward, way.LanesBackward, way.LanesShared));
        Assert.Equal((RoadsideM, RoadsideM), (way.RoadsideAlongM, way.RoadsideAgainstM));
        Assert.Equal((2 * LaneM) + (2 * RoadsideM), way.CarriagewayM, LineTolerance.RoundingM);
    }

    /// <summary>
    /// <b>Less than half a roadside past the lanes is none</b>: a street read a little under half a roadside wider
    /// than its two lanes is laid as its lanes.
    /// </summary>
    [Fact]
    public void LessThanHalfARoadsidePastTheLanesIsNone()
    {
        var way = Read(Measured((1, (2 * LaneM) + (0.4f * RoadsideM), MeasuredFrom.Imagery)), Way(1, ("highway", "residential"), ("lanes", "2"))).Ways[0];

        Assert.Equal((2 * LaneM, 0f, 0f), (way.CarriagewayM, way.RoadsideAlongM, way.RoadsideAgainstM));
    }

    /// <summary>
    /// <b>No roadside is laid on a bridge</b>, which nothing parks on: a bridge read as wide as its lanes and two
    /// roadsides is laid as its lanes.
    /// </summary>
    [Fact]
    public void NoRoadsideIsLaidOnABridge()
    {
        var way = Read(
            Measured((1, (2 * LaneM) + (2 * RoadsideM), MeasuredFrom.Surface)),
            Way(1, ("highway", "residential"), ("lanes", "2"), ("bridge", "yes"))).Ways[0];

        Assert.Equal((2 * LaneM, 0f, 0f), (way.CarriagewayM, way.RoadsideAlongM, way.RoadsideAgainstM));
    }

    /// <summary>
    /// <b>No more lanes than any way of its class on the map is tagged with</b>: the same street read 21 m wide, where
    /// a residential street is tagged with two each way at most, is two each way.
    /// </summary>
    [Fact]
    public void NoMoreLanesThanItsClassIsTaggedWithAnywhere()
    {
        var way = Read(Measured((1, 21f, MeasuredFrom.Imagery)), Way(1, ("highway", "residential")), Tagged(2, "residential", 4)).Ways[0];

        Assert.Equal((2, 2), (way.LanesForward, way.LanesBackward));
    }

    /// <summary>
    /// <b>A tagged count is OSM's word, and a single roadside stands beside the kerb its traffic keeps to</b>: a
    /// one-lane one-way street read as wide as its lane and one roadside keeps its lane, the roadside along it.
    /// </summary>
    [Fact]
    public void ATaggedCountKeepsItsLanesAndOneRoadsideStandsBesideItsTraffic()
    {
        var way = Read(
            Measured((1, LaneM + RoadsideM, MeasuredFrom.Imagery)), Way(1, ("highway", "residential"), ("oneway", "yes"), ("lanes", "1")),
            Tagged(2, "residential", 4)).Ways[0];

        Assert.Equal((1, 0, 0), (way.LanesForward, way.LanesBackward, way.LanesShared));
        Assert.Equal((LaneM + RoadsideM, RoadsideM, 0f), (way.CarriagewayM, way.RoadsideAlongM, way.RoadsideAgainstM));
    }

    /// <summary>
    /// <b>A width that would leave a lane too wide is not taken</b>: a service road whose mapped surface is a 60 m
    /// square is one lane both ways share, which would be wider than <see cref="CityGenFigures.TracedWidestLaneM"/>,
    /// so it is laid as its lane.
    /// </summary>
    [Fact]
    public void AWidthLeavingALaneTooWideIsNotTaken()
    {
        var way = Read(Measured((1, 60f, MeasuredFrom.Surface)), Way(1, ("highway", "service"))).Ways[0];

        Assert.Equal(LaneM, way.CarriagewayM);
    }

    /// <summary>
    /// <b>Imagery is not taken under a service road</b>, whose yard's paving reads as its width: it stays OSM's one lane
    /// both ways share.
    /// </summary>
    [Fact]
    public void ImageryUnderAServiceRoadIsNotTaken()
    {
        var way = Read(Measured((1, 9f, MeasuredFrom.Imagery)), Way(1, ("highway", "service"))).Ways[0];

        Assert.Null(way.WidthFrom);
        Assert.Equal(LaneM, way.CarriagewayM);
    }

    /// <summary>
    /// <b>A width that would leave a lane too narrow is not taken</b>: four tagged lanes read 9 m across would be lanes
    /// narrower than <see cref="CityGenFigures.TracedNarrowestLaneM"/>, so they are laid as their lanes.
    /// </summary>
    [Fact]
    public void AWidthLeavingALaneTooNarrowIsNotTaken()
    {
        var way = Read(Measured((1, 9f, MeasuredFrom.Imagery)), Way(1, ("highway", "primary"), ("lanes", "4"))).Ways[0];

        Assert.Equal(4 * LaneM, way.CarriagewayM);
    }

    /// <summary>
    /// <b>One lane both ways share is a lane each way where its measured width holds two past its roadsides</b>: an
    /// untagged unclassified road, which OSM assumes is one shared lane, tagged as wide as two lanes and two roadsides.
    /// </summary>
    [Fact]
    public void OneSharedLaneIsALaneEachWayWhereItsWidthHoldsTwo()
    {
        var way = Read(
            Measured((1, (2 * LaneM) + (2 * RoadsideM), MeasuredFrom.Tag)), Way(1, ("highway", "unclassified")),
            Tagged(2, "unclassified", 2)).Ways[0];

        Assert.Equal((1, 1, 0), (way.LanesForward, way.LanesBackward, way.LanesShared));
    }

    static float LaneM => SimConfig.Shipped().CityGen.TracedLaneWidthM;

    static float RoadsideM => SimConfig.Shipped().CityGen.TracedRoadsideWidthM;

    /// <summary>Nothing known of the place but these widths.</summary>
    static PlaceFacts Measured(params (long Way, float WidthM, MeasuredFrom From)[] widths) => new()
    {
        Widths = new PlaceFacts.WidthArrays
        {
            Way = [.. widths.Select(width => width.Way)], WidthM = [.. widths.Select(width => width.WidthM)],
            From = [.. widths.Select(width => width.From)],
        },
        Controls = PlaceFacts.None.Controls,
        Crossings = PlaceFacts.None.Crossings,
        Footprints = TracedMap.FootprintArrays.None,
        TreeM = [],
    };

    /// <summary>A way of a class tagged with so many lanes, which is what a measured width may make of an untagged one.</summary>
    static OsmWay Tagged(long id, string highway, int lanes) => Way(id, ("highway", highway), ("lanes", $"{lanes}"));

    static Survey Read(params OsmWay[] ways) => Read(PlaceFacts.None, ways);

    /// <summary>
    /// The ways given over the seven nodes, in a frame about the street's south-west end reaching
    /// <see cref="MarginM"/> past the street on every side, imported with the facts given and read.
    /// </summary>
    static Survey Read(PlaceFacts facts, params OsmWay[] ways)
    {
        var nodes = new OsmNodes
        {
            Id = [.. Enumerable.Range(1, Places.Length).Select(id => (long)id)],
            Lat = [.. Places.Select(place => place.Lat)],
            Lon = [.. Places.Select(place => place.Lon)],
        };
        var (lat0, lon0) = (nodes.LatDeg(0), nodes.LonDeg(0));
        var (eastM, northM) = new TransverseMercator(lat0, lon0).Project(nodes.LatDeg(1), nodes.LonDeg(1));
        var extract = new OsmExtract
        {
            Name = "Surveyed",
            Description = "a few ways laid by hand",
            Source = new SurveySource { Relation = 1, OsmBase = "", Licence = "" },
            Nodes = nodes,
            NodeTags = [],
            Frame = new OsmFrame
            {
                Lat0Deg = lat0, Lon0Deg = lon0, WestM = -MarginM, SouthM = -MarginM,
                WidthM = Math.Ceiling(eastM + (2 * MarginM)), HeightM = Math.Ceiling(northM + (2 * MarginM)), MarginM = MarginM,
            },
            Ways = ways,
            Areas = [],
            Relations = [],
            Turns = OsmTurns.None,
        };
        extract.Check("hand-laid");
        return Survey.Of(TracedMapImport.Of(extract, facts), SimConfig.Shipped());
    }

    static OsmWay Way(long id, params (string Key, string Value)[] tags)
    {
        var tagged = tags.ToDictionary(tag => tag.Key, tag => tag.Value);
        return new OsmWay { Id = id, Tags = tagged, Nodes = [0, 1], Carriageway = OsmCarriageway.Read(tagged) };
    }

    static OsmWay Coast(long id, params int[] nodes) =>
        new() { Id = id, Tags = new Dictionary<string, string> { ["natural"] = "coastline" }, Nodes = nodes };

    static bool Inside(float[] ring, Vector2 pointM)
    {
        var inside = false;
        for (int at = 0, before = (ring.Length / 2) - 1; at < ring.Length / 2; before = at++)
        {
            var (a, b) = (new Vector2(ring[2 * at], ring[(2 * at) + 1]), new Vector2(ring[2 * before], ring[(2 * before) + 1]));
            if ((a.Y > pointM.Y) != (b.Y > pointM.Y) && pointM.X < a.X + ((pointM.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y))) inside = !inside;
        }

        return inside;
    }
}
