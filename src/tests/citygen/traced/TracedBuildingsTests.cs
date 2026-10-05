using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A traced town's footprints are worn as prefabs on the walk</b> (GEN-57, GEN-54, <see cref="TracedBuildings"/>):
/// two houses surveyed at different setbacks from one street stand on one line, square to it, their ways in on the
/// walk; a shed in a yard is moved onto the walk and one beyond reach stands nowhere; a footprint wears the prefab of
/// its look nearest its size and roundness; and what a footprint is drawn as is read off its use and height.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class TracedBuildingsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Where the street runs, east to west along this line.</summary>
    const float StreetY = 500f;

    /// <summary>
    /// <b>Two houses a few metres apart in setback stand on one line, square to the street</b>: each one's front
    /// wall on the building line and its way in on the walk's outer lane, the wall and the way in as far apart as
    /// the building line and the way in are.
    /// </summary>
    [Fact]
    public void HousesSurveyedAtDifferentSetbacksStandOnOneLine()
    {
        var faceY = StreetY - Config.CityGen.TracedLaneWidthM - Config.WalkOuterM;
        var buildings = Fitted(BuildingSizes.None, Box(300f, faceY - 2f, 12f, 8f), Box(400f, faceY - 5f, 12f, 8f));

        Assert.Equal(2, buildings.Count);
        var frontsY = Enumerable.Range(0, 2).Select(building => buildings.CentreM[building].Y + (Across(buildings, building) * 0.5f)).ToArray();
        Assert.Equal(frontsY[0], frontsY[1], 1e-3f);
        for (var building = 0; building < 2; building++)
        {
            Assert.Equal(0f, MathF.Sin(buildings.HeadingRad[building]), 1e-3f);
            Assert.Equal(Config.BuildingLineM - Config.BuildingWayInM, buildings.EntryPointM[building].Y - frontsY[building], 1e-3f);
        }
    }

    /// <summary>
    /// <b>A building in a yard is moved onto the walk, and one beyond reach stands nowhere</b>: a shed twenty metres
    /// behind the walk stands with its front on the building line, square to the street, and one further back than
    /// <see cref="CityGenFigures.TracedFrontageReachM"/> is not stood.
    /// </summary>
    [Fact]
    public void AYardBuildingIsMovedOntoTheWalkAndOneBeyondReachStandsNowhere()
    {
        var faceY = StreetY - Config.CityGen.TracedLaneWidthM - Config.WalkOuterM;
        var buildings = Fitted(
            BuildingSizes.None, Box(300f, faceY - 20f, 4f, 3f), Box(500f, faceY - Config.CityGen.TracedFrontageReachM - 5f, 4f, 3f));

        Assert.Equal(1, buildings.Count);
        Assert.Equal(300f, buildings.CentreM[0].X, 1e-2f);
        Assert.Equal(faceY - (Config.BuildingLineM - Config.WalkOuterM), buildings.CentreM[0].Y + (Across(buildings, 0) * 0.5f), 2e-2f);
        Assert.Equal(0f, MathF.Sin(buildings.HeadingRad[0]), 1e-3f);
    }

    /// <summary>
    /// <b>A footprint wears the prefab of its look nearest its size</b>, a larger one weighing more against it: a
    /// house of 13 by 8.5 m on the street is offered a 10 by 6 m prefab and a 14 by 9 m one, and wears the second.
    /// </summary>
    [Fact]
    public void AFootprintWearsThePrefabNearestItsSize()
    {
        var sizes = new BuildingSizes(
            [], new Vector2[BuildingSizes.Uses], [new(10f, 6f), new(14f, 9f)], [0.5f, 0.5f], [BuildingLook.House, BuildingLook.House]);

        var buildings = Fitted(sizes, Box(300f, StreetY - 10f, 13f, 8.5f));

        Assert.Equal([1], buildings.Prefab);
        Assert.Equal(new Vector2(14f, 9f), buildings.SizeM[0]);
    }

    /// <summary>
    /// <b>A prefab is laid as it was drawn, door to the street</b>: a house 6 m along the street and 12 m deep is
    /// offered a prefab drawn 12 m wide and 6 m deep and one drawn 6 by 12, and wears the second as drawn, never the
    /// first turned a quarter onto a door that would face along the street.
    /// </summary>
    [Fact]
    public void APrefabIsLaidAsItWasDrawnDoorToTheStreet()
    {
        var sizes = new BuildingSizes(
            [], new Vector2[BuildingSizes.Uses], [new(12f, 6f), new(6f, 12f)], [0.5f, 0.5f], [BuildingLook.House, BuildingLook.House]);

        var buildings = Fitted(sizes, Box(300f, StreetY - 10f, 6f, 12f));

        Assert.Equal([1], buildings.Prefab);
        Assert.Equal(new Vector2(6f, 12f), buildings.SizeM[0]);
    }

    /// <summary>
    /// <b>A round footprint wears a round prefab</b>: a silo of 12 m surveyed as a sixteen-sided ring on the street is
    /// offered a square prefab of its size and a round one, and wears the round one.
    /// </summary>
    [Fact]
    public void ARoundFootprintWearsARoundPrefab()
    {
        var sizes = new BuildingSizes(
            [], new Vector2[BuildingSizes.Uses], [new(12f, 12f), new(12f, 12f)], [0.5f, 6f], [BuildingLook.House, BuildingLook.House]);
        var centreM = new Vector2(300f, StreetY - 15f);
        Vector2[] ring = [.. Enumerable.Range(0, 16).Select(at => centreM + (6f * new Vector2(MathF.Cos(at * MathF.PI / 8f), MathF.Sin(at * MathF.PI / 8f))))];

        Assert.Equal([1], Fitted(sizes, ring).Prefab);
    }

    /// <summary>
    /// <b>What a building is drawn as</b>: a home no taller than a house's storeys is a house, past them a block of
    /// flats and past a tower's a tower; a home nothing says the height of is a shed or a house by its ground; a
    /// works is a shed where it is no bigger than one; and a building nothing says anything of covering a works'
    /// ground is a works.
    /// </summary>
    [Theory]
    [InlineData((byte)FootprintUse.Residential, 6f, 120f, (byte)BuildingLook.House)]
    [InlineData((byte)FootprintUse.Residential, 15f, 120f, (byte)BuildingLook.Apartments)]
    [InlineData((byte)FootprintUse.Apartments, 30f, 600f, (byte)BuildingLook.Tower)]
    [InlineData((byte)FootprintUse.Residential, 0f, 12f, (byte)BuildingLook.Shed)]
    [InlineData((byte)FootprintUse.Residential, 0f, 450f, (byte)BuildingLook.Apartments)]
    [InlineData((byte)FootprintUse.Industrial, 0f, 12f, (byte)BuildingLook.Shed)]
    [InlineData((byte)FootprintUse.Unknown, 0f, 2000f, (byte)BuildingLook.Industrial)]
    public void ABuildingIsDrawnAsItsUseAndHeightSay(byte use, float heightM, float areaM2, byte look) =>
        Assert.Equal((BuildingLook)look, TracedBuildings.LookOf((FootprintUse)use, heightM, areaM2, Config.CityGen));

    static float Across(CityPlan.BuildingArrays buildings, int building) =>
        MathF.Abs(MathF.Cos(buildings.HeadingRad[building])) > 0.5f ? buildings.SizeM[building].Y : buildings.SizeM[building].X;

    /// <summary>A rectangle as wide as given and as deep, its south wall at <paramref name="southY"/> and centred on <paramref name="x"/>.</summary>
    static Vector2[] Box(float x, float southY, float widthM, float depthM) =>
        [new(x - (widthM * 0.5f), southY - depthM), new(x + (widthM * 0.5f), southY - depthM), new(x + (widthM * 0.5f), southY), new(x - (widthM * 0.5f), southY)];

    /// <summary>The buildings a traced plan stands on those outlines, worn as the prefabs <paramref name="sizes"/> offers.</summary>
    static CityPlan.BuildingArrays Fitted(BuildingSizes sizes, params Vector2[][] outlines) =>
        TracedPlan.Lay(Surveyed(outlines), Config, sizes).Buildings;

    /// <summary>A street of a lane each way from 100 to 900 m along <see cref="StreetY"/>, and homes of no stated height on these outlines.</summary>
    static Survey Surveyed(params Vector2[][] outlines)
    {
        var offsets = new List<int> { 0 };
        foreach (var outline in outlines) offsets.Add(offsets[^1] + outline.Length);

        return new Survey
        {
            Name = "Traced", Relation = 1, WidthM = 1000f, HeightM = 1000f, PointsM = [100f, StreetY, 900f, StreetY],
            Ways =
            [
                new SurveyWay
                {
                    Highway = "residential", LanesForward = 1, LanesBackward = 1, LanesShared = 0,
                    CarriagewayM = 2f * OsmCarriageway.AssumedLaneWidthM, CentreOffsetM = 0f, Points = [0, 1],
                },
            ],
            Sea = [],
            Footprints = new CityPlan.FootprintArrays
            {
                RingOffsets = [.. Enumerable.Range(0, outlines.Length + 1)],
                Rings = new CityPlan.RingArrays { Offsets = [.. offsets], PointM = [.. outlines.SelectMany(outline => outline)] },
                Traced = new bool[outlines.Length],
                HeightM = new float[outlines.Length],
                Use = [.. outlines.Select(_ => FootprintUse.House)],
            },
        };
    }
}
