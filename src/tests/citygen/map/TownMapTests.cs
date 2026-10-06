using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Map;

/// <summary>
/// <b>A map's file reads back what was written, and is cut down in place</b> (GEN-58, <see cref="TownMap"/>): every road,
/// water, set-down thing and zone as written, every line's place to the millimetre, what is set down to the centimetre,
/// every zone's place to the metre and every setting to its step; a file cut short or a zone tree out of order refused;
/// and a crop keeping what stands in its frame, a road running on past it as far as the engine reads it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class TownMapTests
{
    /// <summary><b>What is written is read back</b>: every road, the coast, the waters, what is set down and every zone.</summary>
    [Fact]
    public void AMapReadsBackWhatWasWritten()
    {
        var written = Everything();

        var read = RoundTrip(written);

        Assert.Equal((written.Name, written.Description, written.Licence, written.OsmBase, written.Seed),
                     (read.Name, read.Description, read.Licence, read.OsmBase, read.Seed));
        Assert.Equal((written.Frame.Lat0Deg, written.Frame.WestM, written.Frame.SouthM, written.Frame.WidthM, written.Frame.MarginM),
                     (read.Frame.Lat0Deg, read.Frame.WestM, read.Frame.SouthM, read.Frame.WidthM, read.Frame.MarginM));
        Assert.Equal(written.Roads.Select(Said), read.Roads.Select(Said));
        Assert.Equal(written.Coast, read.Coast);
        Assert.Equal(written.Waters.Kind, read.Waters.Kind);
        Assert.Equal(written.Waters.PointM, read.Waters.PointM);
        Assert.Equal(written.Courses.Across, read.Courses.Across);
        Assert.Equal(written.Courses.PointM, read.Courses.PointM);
        Assert.Equal((written.Courses.NearM[0], written.Courses.FarM[0]), (read.Courses.NearM[0], read.Courses.FarM[0]));
        Assert.Equal(written.Buildings.Look, read.Buildings.Look);
        Assert.Equal(written.Buildings.CentreM, read.Buildings.CentreM);
        Assert.Equal(written.Buildings.SizeM, read.Buildings.SizeM);
        Assert.Equal(written.Props.Kind, read.Props.Kind);
        Assert.Equal(written.Props.CentreM, read.Props.CentreM);
        Assert.Equal(written.Lots.CentreM, read.Lots.CentreM);
        Assert.Equal(written.Lots.SizeM, read.Lots.SizeM);
        Assert.Equal(written.Zones.Parent, read.Zones.Parent);
        Assert.Equal(written.Zones.Kind, read.Zones.Kind);
        Assert.Equal(written.Zones.ParamOffsets, read.Zones.ParamOffsets);
        Assert.Equal(written.Zones.ParamKey, read.Zones.ParamKey);
        Assert.Equal(written.Zones.ParamValue, read.Zones.ParamValue);
        Assert.Equal(written.Zones.RingOffsets, read.Zones.RingOffsets);
        Assert.Equal(written.Zones.PointM, read.Zones.PointM);
    }

    /// <summary>
    /// <b>A setting is held to its step as it is added</b> (<see cref="ZoneParams.StepOf"/>): a share to a thousandth and a
    /// distance to a decimetre, so a map in memory is the map it reads back as.
    /// </summary>
    [Fact]
    public void ASettingIsHeldToItsStep()
    {
        var zones = TownMap.ZoneArrays.Whole(new Vector2(100, 100), ZoneKind.Town, [(ZoneParam.Frontage, 0.12345f), (ZoneParam.FrontM, 7.77f)]);

        Assert.Equal([0.123f, 7.8f], zones.ParamValue);
    }

    /// <summary>
    /// <b>A heading reads back to its step of a turn</b>: a set-down building's bearing is a sixty-five-thousandth of a
    /// turn, a ten-thousandth of a radian, however it was given.
    /// </summary>
    [Fact]
    public void AHeadingReadsBackToItsStep()
    {
        var written = Everything() with
        {
            Buildings = new TownMap.StoodArrays { Look = [BuildingLook.House], CentreM = [new(50, 50)], SizeM = [new(10, 8)], HeadingRad = [-1.2345f] },
        };

        var read = RoundTrip(written);

        Assert.Equal(MathF.Tau - 1.2345f, read.Buildings.HeadingRad[0], 1e-4f);
    }

    /// <summary>
    /// <b>A road's place reads back to the millimetre, and a zone's to the metre</b>, however far across the map it
    /// stands: each is written as its step from the one before, and the steps add up to where it was.
    /// </summary>
    [Fact]
    public void PlacesReadBackToTheirSteps()
    {
        var written = Map([new(17_349.9996, 39_421.0004), new(0.0004, 0.0006), new(-512.3456, 40_000.0)]);

        var read = RoundTrip(written);

        for (var at = 0; at < written.PointM.Length; at++)
        {
            Assert.Equal(Math.Round(written.PointM[at].X * 1000) / 1000, read.PointM[at].X, 1e-9);
            Assert.Equal(Math.Round(written.PointM[at].Y * 1000) / 1000, read.PointM[at].Y, 1e-9);
        }
    }

    /// <summary><b>A zone's outline is held to the metre as it is added</b>: it is a hint of what stands along the walk, and no edge of it is a kerb.</summary>
    [Fact]
    public void AZoneIsHeldToTheMetre()
    {
        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(-1, ZoneKind.Town, [], [[new(0.4f, 0.6f), new(17_349.4f, 0.4f), new(17_312.5f, 39_400.5f)]]);

        Assert.Equal([new Vector2(0, 1), new Vector2(17_349, 0), new Vector2(17_312, 39_400)], zones.Arrays().PointM);
    }

    /// <summary><b>A map cut short is refused</b> rather than read as a smaller place — what a page holds of one before it is opened.</summary>
    [Fact]
    public void AMapCutShortIsRefused()
    {
        using var stream = new MemoryStream();
        Everything().Write(stream);

        Assert.Throws<InvalidDataException>(() => TownMap.Read(stream.GetBuffer().AsSpan(0, (int)stream.Length - 1), "test"));
    }

    /// <summary><b>A zone written before the zone it stands in is refused</b>: a tree is read root first, every parent before its zones.</summary>
    [Fact]
    public void AZoneBeforeItsParentIsRefused()
    {
        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(-1, ZoneKind.Town, [], [Square(0, 0, 100)]);
        zones.Add(2, ZoneKind.Park, [], [Square(10, 10, 10)]);
        zones.Add(0, ZoneKind.Residential, [], [Square(0, 0, 50)]);

        Assert.Throws<InvalidDataException>(() => (Everything() with { Zones = zones.Arrays() }).Check("test"));
    }

    /// <summary><b>The first zone is the whole map and the only one</b>: a second zone of a whole map's kind is refused.</summary>
    [Fact]
    public void ASecondWholeMapIsRefused()
    {
        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(-1, ZoneKind.Town, [], [Square(0, 0, 100)]);
        zones.Add(0, ZoneKind.Wheel, [], [Square(0, 0, 50)]);

        Assert.Throws<InvalidDataException>(() => (Everything() with { Zones = zones.Arrays() }).Check("test"));
    }

    /// <summary><b>Saying a setting again replaces it</b>, and leaves every other setting and zone as it was.</summary>
    [Fact]
    public void ASettingSaidAgainReplacesIt()
    {
        var zones = Everything().Zones.With(TownMap.ZoneArrays.Root, ZoneParam.People, 12);

        Assert.Equal(12f, zones.Own(TownMap.ZoneArrays.Root, ZoneParam.People));
        Assert.Equal(129f, zones.Own(TownMap.ZoneArrays.Root, ZoneParam.Cars));
        Assert.Equal(Everything().Zones.Own(1, ZoneParam.Frontage), zones.Own(1, ZoneParam.Frontage));
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
    /// <b>A crop cuts a zone to its frame, moved by its corner, and leaves one wholly outside</b>: of a zone astride the
    /// frame's edge and one beyond it, the first is kept as far as the edge — and a zone whose parent was left out stands
    /// in its parent's parent, the whole map's zone being the new frame.
    /// </summary>
    [Fact]
    public void ACropCutsTheZonesToItsFrame()
    {
        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(-1, ZoneKind.Town, [(ZoneParam.People, 5)], [TownMap.WholeOutline(new Vector2(1000, 800))]);
        var far = zones.Add(0, ZoneKind.Residential, [], [Square(850, 650, 140)]);
        zones.Add(far, ZoneKind.Park, [], [Square(690, 300, 20)]);
        zones.Add(far, ZoneKind.Park, [], [Square(900, 700, 50)]);
        var map = Map([new(300, 400), new(500, 400)], Road(1, 0, 1)) with { Zones = zones.Arrays() };

        var cropped = map.Cropped(200, 200, 500, 400).Zones;

        Assert.Equal([ZoneKind.Town, ZoneKind.Park], cropped.Kind);
        Assert.Equal([-1, 0], cropped.Parent);
        Assert.Equal(TownMap.WholeOutline(new Vector2(500, 400)), cropped.OutlineOf(0).ToArray());
        Assert.Equal([new Vector2(490, 100), new Vector2(500, 100), new Vector2(500, 120), new Vector2(490, 120)], cropped.OutlineOf(1).ToArray());
        Assert.Equal(5f, cropped.Own(TownMap.ZoneArrays.Root, ZoneParam.People));
    }

    /// <summary>
    /// <b>A stump runs from its dead end back to the first place three ways meet</b>, on through a place where two ways
    /// only run on into each other: a street through a junction with a lane off it in two ways, the second ending in a
    /// building, is one stump of both ways, ending inside.
    /// </summary>
    [Fact]
    public void AStumpRunsBackToWhereThreeWaysMeet()
    {
        var stumps = Driveway().Stumps(House);

        var stump = Assert.Single(stumps, stump => stump.EndM == new Vector2D(300, 500));
        Assert.Equal([(2, 0, false), (1, 0, false)], stump.Cuts);
        Assert.True(stump.EndsInside);
        Assert.Equal(200f, stump.LengthM);
    }

    /// <summary><b>A road running off the map is no stump</b>: its last point past the frame is where it leaves, not a dead end.</summary>
    [Fact]
    public void ARoadRunningOffTheMapIsNoStump()
    {
        var stumps = Map([new(100, 400), new(500, 400), new(1200, 400)], Road(1, 0, 1, 2)).Stumps(SurveyFootprints.None);

        Assert.Equal([new Vector2D(100, 400)], stumps.Select(stump => stump.EndM));
    }

    /// <summary><b>Dropping a stump drops its roads</b>: the lane's two ways go, and the street stays whole.</summary>
    [Fact]
    public void DroppingAStumpDropsItsRoads()
    {
        var map = Driveway();

        var dropped = map.Without(map.Stumps(House).Where(stump => stump.EndsInside));

        Assert.Equal([1L], dropped.Roads.Select(road => road.OsmId));
    }

    /// <summary>
    /// A street from (100, 300) to (500, 300) through a junction at (300, 300), and a lane off it south in two ways to a
    /// dead end at (300, 500) inside <see cref="House"/>.
    /// </summary>
    static TownMap Driveway() =>
        Map([new(100, 300), new(300, 300), new(500, 300), new(300, 400), new(300, 500)], Road(1, 0, 1, 2), Road(2, 1, 3), Road(3, 3, 4));

    static readonly SurveyFootprints House = new()
    {
        RingOffsets = [0, 1], PointOffsets = [0, 4], PointM = [new(280, 480), new(320, 480), new(320, 520), new(280, 520)],
        HeightM = [9f], Use = [FootprintUse.House], Look = [BuildingLook.House],
    };

    /// <summary>Everything a road says, as one line.</summary>
    static string Said(TracedRoad road) =>
        $"{road.OsmId} {road.Highway} {road.Bridge} {road.Roundabout} {road.LanesForward}+{road.LanesBackward}+{road.LanesShared} "
        + $"{road.LanesTagged} {road.Marked} {road.CentreOffsetShare} {road.WidthM} [{string.Join(",", road.Points)}]";

    static TownMap RoundTrip(TownMap map)
    {
        using var stream = new MemoryStream();
        map.Write(stream);
        return TownMap.Read(stream.GetBuffer().AsSpan(0, (int)stream.Length), "test");
    }

    /// <summary>A square outline of a side, its north-west corner given.</summary>
    static Vector2[] Square(float xM, float yM, float sideM) => [new(xM, yM), new(xM + sideM, yM), new(xM + sideM, yM + sideM), new(xM, yM + sideM)];

    /// <summary>A map of a thousand metres by eight hundred and its margin, holding the points and roads given and nothing else.</summary>
    static TownMap Map(Vector2D[] pointM, params TracedRoad[] roads) =>
        TownMap.Bare("Hand", "a few ways laid by hand", 12888405, new Vector2(1000, 800), ZoneKind.Town, []) with
        {
            Licence = "© OpenStreetMap contributors",
            OsmBase = "2026-10-03T09:16:51Z",
            Frame = new OsmFrame { Lat0Deg = 46.5, Lon0Deg = 30.7, WestM = -500.25, SouthM = -400.5, WidthM = 1000, HeightM = 800, MarginM = 60 },
            PointM = pointM,
            Roads = roads,
        };

    static TracedRoad Road(long id, params int[] points) => new()
    {
        OsmId = id, Highway = "residential", LanesForward = 1, LanesBackward = 1, LanesShared = 0, Points = points,
    };

    /// <summary>A map holding one of everything a map holds, each with something other than its default.</summary>
    static TownMap Everything()
    {
        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(-1, ZoneKind.Town, [(ZoneParam.People, 300), (ZoneParam.Cars, 129)], [TownMap.WholeOutline(new Vector2(1000, 800))]);
        var quarter = zones.Add(0, ZoneKind.Garages, [(ZoneParam.Frontage, 0.375f), (ZoneParams.Of(BuildingLook.Garages), 1f)], [Square(0, 0, 900), Square(100, 100, 40)]);
        zones.Add(quarter, ZoneKind.Park, [(ZoneParam.Growth, 0.5f)], [Square(110, 110, 10)]);
        return Map([new(10, 20), new(30, 20), new(30, 40), new(900, 700), new(950, 750), new(990, 790)],
            Road(-7, 0, 1),
            new TracedRoad
            {
                OsmId = 25481554, Highway = "primary", Bridge = true, Roundabout = true, LanesForward = 2, LanesBackward = 3, LanesShared = 1,
                LanesTagged = true, Marked = true, CentreOffsetShare = -0.125f, WidthM = 14.25f, Points = [1, 2, 0],
            }) with
        {
            Coast = [[3, 4, 5]],
            Waters = new TownMap.WaterArrays { Kind = [TownMap.WaterBody.Lake], PointOffsets = [0, 3], PointM = [new(500, 0), new(520, 0), new(510.5f, 800)] },
            Courses = new TownMap.CourseArrays
            {
                Kind = [TownMap.WaterBody.River], Across = [new(0.6f, -0.8f)], NearM = [12.345678f], FarM = [-12.5f], PointOffsets = [0, 2],
                PointM = [new(-200.123456f, 300.987654f), new(1200.5f, 333.333333f)],
            },
            Buildings = new TownMap.StoodArrays { Look = [BuildingLook.School], CentreM = [new(200.25f, 300.5f)], SizeM = [new(30, 18.5f)], HeadingRad = [0f] },
            Props = new TownMap.PropArrays { Kind = [PropKind.UrbanFurniture], CentreM = [new(12.5f, 7.25f)], BearingRad = [0f] },
            Lots = new TownMap.LotArrays { CentreM = [new(600, 600)], SizeM = [new(40, 20)], HeadingRad = [0f] },
            Zones = zones.Arrays(),
        };
    }
}
