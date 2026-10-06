using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Zones;

/// <summary>
/// <b>A town's buildings stand along the walk as its zones say</b> (GEN-58, GEN-54, <see cref="ZoneBuildings"/>): a zone's
/// looks on the walk beside it and nothing where its zone builds nothing; the deepest zone holding a place its zone, a
/// district laid as the zone round it unless it says otherwise; as much of the frontage built as the zone says, each
/// front as far back and as turned as it says and each building drawn afresh as often as it says; a town's planned count
/// thinning what its zones stand; and what the map sets down standing first, everything else clear of it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class ZoneBuildingsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Where the street runs, west to east along this line, from x = 100 to x = 900.</summary>
    const float StreetY = 500f;

    static readonly Vector2 HouseM = new(10f, 8f);

    static readonly Vector2 VillaM = new(14f, 10f);

    static readonly Vector2 SchoolM = new(24f, 16f);

    /// <summary>Two house prefabs and a school prefab.</summary>
    static readonly BuildingSizes Sizes = new(
        new Vector2[BuildingSizes.Uses], [HouseM, VillaM, SchoolM], [0.5f, 0.5f, 0.5f],
        [BuildingLook.House, BuildingLook.House, BuildingLook.School]);

    /// <summary>Where the walk's outer face runs on the north side of the street.</summary>
    static float FaceY => StreetY - Config.CityGen.TracedLaneWidthM - Config.WalkOuterM;

    /// <summary><b>A zone's look stands along the walk beside it</b>: houses north of the street, and none south of it, which only the whole map holds.</summary>
    [Fact]
    public void AZonesLookStandsAlongTheWalkBesideIt()
    {
        var buildings = Laid(Zoned.Terrace(North(150f, 850f)));

        Assert.NotEmpty(buildings.Prefab);
        Assert.All(Enumerable.Range(0, buildings.Count), building => Assert.True(buildings.CentreM[building].Y < StreetY && buildings.Prefab[building] != 2));
    }

    /// <summary>
    /// <b>Every building fronts the walk on the building line, square to the street</b>, where its zone stands it no
    /// further back: its front wall as far off the walk's outer face as the building line stands, and its way in as far in
    /// front of that as the way in stands.
    /// </summary>
    [Fact]
    public void ABuildingFrontsTheWalkOnTheBuildingLine()
    {
        var buildings = Laid(Zoned.Terrace(North(150f, 850f)));

        for (var building = 0; building < buildings.Count; building++)
        {
            var frontY = buildings.CentreM[building].Y + (Sizes.PrefabM[buildings.Prefab[building]].Y * 0.5f);
            Assert.Equal(FaceY - (Config.BuildingLineM - Config.WalkOuterM), frontY, 2e-2f);
            Assert.Equal(0f, MathF.Sin(buildings.HeadingRad[building]), 1e-3f);
            Assert.Equal(Config.BuildingLineM - Config.BuildingWayInM, buildings.EntryPointM[building].Y - frontY, 2e-2f);
        }
    }

    /// <summary>
    /// <b>A front stands as far off the carriageway as its zone says</b>: six metres past the building line, every front
    /// is six metres further back than a terrace's — and its way in is on the walk all the same.
    /// </summary>
    [Fact]
    public void AFrontStandsAsFarBackAsItsZoneSays()
    {
        var buildings = Laid(Set(Zoned.Terrace(North(150f, 850f)), (ZoneParam.FrontM, Config.BuildingLineM + 6f)));

        Assert.NotEmpty(buildings.Prefab);
        for (var building = 0; building < buildings.Count; building++)
        {
            var frontY = buildings.CentreM[building].Y + (Sizes.PrefabM[buildings.Prefab[building]].Y * 0.5f);
            Assert.Equal(FaceY - (Config.BuildingLineM - Config.WalkOuterM) - 6f, frontY, 5e-2f);
            Assert.Equal(FaceY + (Config.WalkOuterM - Config.BuildingWayInM), buildings.EntryPointM[building].Y, 5e-2f);
        }
    }

    /// <summary><b>A building turns off square no further than its zone's skew</b>, either way, and turns by a draw.</summary>
    [Fact]
    public void ABuildingTurnsNoFurtherThanItsZonesSkew()
    {
        var buildings = Laid(Set(Zoned.Terrace(North(150f, 850f)), (ZoneParam.SkewDeg, 10f), (ZoneParam.Frontage, 0.5f)));

        var turnsDeg = buildings.HeadingRad.Select(headingRad => MathF.Asin(MathF.Sin(headingRad)) * 180f / MathF.PI).ToArray();
        Assert.NotEmpty(turnsDeg);
        Assert.All(turnsDeg, turnDeg => Assert.InRange(MathF.Abs(turnDeg), 0f, 10f + 1e-3f));
        Assert.Contains(turnsDeg, turnDeg => MathF.Abs(turnDeg) > 1f);
    }

    /// <summary>
    /// <b>A zone of no variety repeats the building before</b>: along a terrace whose zone draws nothing afresh, every house
    /// is the first one's prefab.
    /// </summary>
    [Fact]
    public void AZoneOfNoVarietyRepeatsTheBuildingBefore()
    {
        var buildings = Laid(Set(Zoned.Terrace(North(150f, 850f)), (ZoneParam.Variety, 0f)));

        Assert.Single(buildings.Prefab.Distinct());
    }

    /// <summary><b>A zone builds each look at its share</b>: houses and schools half and half both stand along one street.</summary>
    [Fact]
    public void AZoneBuildsEachLookAtItsShare()
    {
        var buildings = Laid(Set(Zoned.Terrace(North(150f, 850f)), (ZoneParams.Of(BuildingLook.House), 0.5f), (ZoneParams.Of(BuildingLook.School), 0.5f)));

        Assert.Contains(2, buildings.Prefab);
        Assert.Contains(buildings.Prefab, prefab => prefab != 2);
    }

    /// <summary><b>Ground whose zone builds nothing builds nothing</b>: a park north of the street stands no building, and houses south of it do.</summary>
    [Fact]
    public void OpenGroundBuildsNothing()
    {
        var buildings = Laid(new Zoned.Zone(ZoneKind.Park, North(150f, 850f), []), Zoned.Terrace(South(150f, 850f)));

        Assert.NotEmpty(buildings.Prefab);
        Assert.All(Enumerable.Range(0, buildings.Count), building => Assert.True(buildings.CentreM[building].Y > StreetY));
    }

    /// <summary>
    /// <b>The deepest zone holding a place is its zone</b>: a school's grounds inside a terrace's quarter build the school
    /// along their own frontage and houses either side of it.
    /// </summary>
    [Fact]
    public void TheDeepestZoneHoldingAPlaceIsItsZone()
    {
        var buildings = Laid(Zoned.Terrace(North(150f, 850f)), Zoned.Terrace(North(400f, 600f), BuildingLook.School) with { Parent = 1 });

        Assert.All(Enumerable.Range(0, buildings.Count), building =>
        {
            var x = buildings.CentreM[building].X;
            if (x > 400f + SchoolM.X && x < 600f - SchoolM.X) Assert.Equal(2, buildings.Prefab[building]);
            if (x < 400f - SchoolM.X || x > 600f + SchoolM.X) Assert.NotEqual(2, buildings.Prefab[building]);
        });
    }

    /// <summary>
    /// <b>A zone whose frontage is all built stands each building against the last</b>: along a terrace of one prefab,
    /// every house's middle is a house's width from the one before.
    /// </summary>
    [Fact]
    public void ATerraceStandsEachBuildingAgainstTheLast()
    {
        var buildings = Laid(Set(Zoned.Terrace(North(150f, 850f)), (ZoneParam.Variety, 0f)));

        var alongM = buildings.CentreM.Select(centreM => centreM.X).Order().ToArray();
        var widthM = Sizes.PrefabM[buildings.Prefab[0]].X;
        Assert.All(Enumerable.Range(1, alongM.Length - 1), at => Assert.Equal(widthM, alongM[at] - alongM[at - 1], 2e-2f));
    }

    /// <summary><b>No building stands where none of the zone's frontage is built</b>.</summary>
    [Fact]
    public void NoBuildingStandsWhereNoneOfTheFrontageIsBuilt()
    {
        var buildings = Laid(Set(Zoned.Terrace(North(150f, 850f)), (ZoneParam.Frontage, 0f)));

        Assert.Empty(buildings.Prefab);
    }

    /// <summary>
    /// <b>A zone inside another is ground of its own</b>: a terrace along the whole street builds along it, and nothing in
    /// the park inside it north of the street.
    /// </summary>
    [Fact]
    public void AZoneInsideAnotherIsGroundOfItsOwn()
    {
        var terrace = Zoned.Terrace([new(0f, 0f), new(1000f, 0f), new(1000f, 1000f), new(0f, 1000f)]);

        var buildings = Laid(terrace, new Zoned.Zone(ZoneKind.Park, North(150f, 850f), [], Parent: 1));

        Assert.NotEmpty(buildings.Prefab);
        Assert.All(Enumerable.Range(0, buildings.Count), building => Assert.False(buildings.CentreM[building] is { X: > 150f and < 850f, Y: > 100f and < StreetY }));
    }

    /// <summary>
    /// <b>The ground behind the frontage is built as the zone says, and no more</b> (<see cref="ZoneParam.Interior"/>): a
    /// quarter building a tenth of its ground behind its frontage stands buildings a front row's depth and more behind
    /// the walk, covering no more than its tenth and a building — and one building none behind stands none there.
    /// </summary>
    [Fact]
    public void TheGroundBehindIsBuiltAsTheZoneSays()
    {
        var quarter = North(150f, 850f);
        var areaM2 = (850f - 150f) * (StreetY - 6f - 100f);
        var behindY = FaceY - Config.CityGen.ZoneBehindClearM;

        var built = Laid(Set(Zoned.Terrace(quarter), (ZoneParam.Interior, 0.1f), (ZoneParam.BehindM2, 100f)));
        var none = Laid(Set(Zoned.Terrace(quarter), (ZoneParam.Interior, 0f)));

        var behind = Enumerable.Range(0, built.Count).Where(building => built.CentreM[building].Y < behindY).ToArray();
        Assert.NotEmpty(behind);
        Assert.InRange(behind.Sum(building => built.SizeM[building].X * built.SizeM[building].Y), 0f, (0.1f * areaM2) + (SchoolM.X * SchoolM.Y));
        Assert.DoesNotContain(Enumerable.Range(0, none.Count), building => none.CentreM[building].Y < behindY);
    }

    /// <summary>
    /// <b>A building behind the frontage is laid square to the zone's bearing</b> where it says one: thirty degrees east
    /// of north, whatever the street does.
    /// </summary>
    [Fact]
    public void ABuildingBehindIsLaidOnTheZonesBearing()
    {
        var buildings = Laid(Set(Zoned.Terrace(North(150f, 850f)), (ZoneParam.Interior, 0.1f), (ZoneParam.BearingDeg, 30f)));
        var bearingRad = MathF.Atan2(-MathF.Cos(MathF.PI / 6f), MathF.Sin(MathF.PI / 6f));

        var behind = Enumerable.Range(0, buildings.Count).Where(building => buildings.CentreM[building].Y < FaceY - Config.CityGen.ZoneBehindClearM).ToArray();
        Assert.NotEmpty(behind);
        Assert.All(behind, building => Assert.Equal(0f, MathF.Sin(buildings.HeadingRad[building] - bearingRad), 1e-3f));
    }

    /// <summary><b>A town that plans fewer buildings than its zones stand stands no more than it plans</b>, and one that plans none stands none.</summary>
    [Fact]
    public void ATownStandsNoMoreThanItPlans()
    {
        var all = Laid(Zoned.Terrace(North(150f, 850f))).Count;

        var planned = Laid([(ZoneParam.Buildings, all / 2)], Zoned.Terrace(North(150f, 850f))).Count;
        var none = Laid([(ZoneParam.Buildings, 0)], Zoned.Terrace(North(150f, 850f))).Count;

        Assert.InRange(planned, 1, all / 2);
        Assert.Equal(0, none);
    }

    /// <summary>
    /// <b>A building the map sets down stands where it is set, and the zones build clear of it</b>: a school set down in
    /// a terrace's frontage stands there, and no house overlaps it.
    /// </summary>
    [Fact]
    public void ABuildingSetDownStandsAndTheZonesBuildClearOfIt()
    {
        var schoolM = new Vector2(500f, FaceY - (Config.BuildingLineM - Config.WalkOuterM) - (SchoolM.Y * 0.5f));
        var setDown = new TownMap.StoodArrays { Look = [BuildingLook.School], CentreM = [schoolM], SizeM = [SchoolM], HeadingRad = [0f] };

        var buildings = Laid([], setDown, Zoned.Terrace(North(150f, 850f)));

        Assert.Equal((schoolM, 2), (buildings.CentreM[0], buildings.Prefab[0]));
        Assert.All(Enumerable.Range(1, buildings.Count - 1), building =>
            Assert.True(MathF.Abs(buildings.CentreM[building].X - schoolM.X) >= (SchoolM.X + Sizes.PrefabM[buildings.Prefab[building]].X) * 0.5f - Config.CityGen.ZonePartyWallM * 2f));
    }

    /// <summary>A zone with settings said over its own.</summary>
    static Zoned.Zone Set(Zoned.Zone zone, params (ZoneParam Param, float Value)[] said) =>
        zone with { Settings = [.. zone.Settings.Where(setting => said.All(over => over.Param != setting.Item1)), .. said] };

    /// <summary>The ground north of the street between two places along it, reaching to its walk.</summary>
    static Vector2[] North(float fromX, float toX) => [new(fromX, 100f), new(toX, 100f), new(toX, StreetY - 6f), new(fromX, StreetY - 6f)];

    static Vector2[] South(float fromX, float toX) => [new(fromX, StreetY + 6f), new(toX, StreetY + 6f), new(toX, 900f), new(fromX, 900f)];

    static CityPlan.BuildingArrays Laid(params Zoned.Zone[] zones) => Laid([], TownMap.StoodArrays.None, zones);

    static CityPlan.BuildingArrays Laid((ZoneParam, float)[] town, params Zoned.Zone[] zones) => Laid(town, TownMap.StoodArrays.None, zones);

    /// <summary>The buildings a plan stands along a street of a lane each way, zoned so.</summary>
    static CityPlan.BuildingArrays Laid((ZoneParam, float)[] town, TownMap.StoodArrays setDown, params Zoned.Zone[] zones)
    {
        var survey = new Survey
        {
            Name = "Zoned", Seed = 1, WidthM = 1000f, HeightM = 1000f, PointsM = [100f, StreetY, 900f, StreetY],
            Ways =
            [
                new SurveyWay
                {
                    Highway = "residential", LanesForward = 1, LanesBackward = 1, LanesShared = 0,
                    CarriagewayM = 2f * Config.CityGen.TracedLaneWidthM, CentreOffsetM = 0f, Points = [0, 1],
                },
            ],
            Sea = [],
            Zones = Zoned.Town(1000f, 1000f, town, zones),
            Buildings = setDown,
        };

        return TownPlan.Lay(survey, Config, Sizes).Buildings;
    }
}
