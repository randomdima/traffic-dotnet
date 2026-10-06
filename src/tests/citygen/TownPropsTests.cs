using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen.Zones;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>A town's props</b> (GEN-6b, GEN-58, <see cref="TownProps"/>): none within a lattice step of a carriageway, none in a
/// building, the open ground's as scenery that stands no body and as thick as its zone grows, what the map sets down
/// standing as it is set, and the same every time the map is laid.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class TownPropsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Where the street runs, east to west along this line.</summary>
    const float StreetY = 500f;

    /// <summary>
    /// <b>A prop whose crown comes within a lattice step of a carriageway is not laid</b> (TER-4c.4): of two by a
    /// street, the one on its verge stands and the one on its carriageway does not.
    /// </summary>
    [Fact]
    public void APropOnTheCarriagewayIsNotLaid()
    {
        var plan = TownPlan.Lay(Surveyed(), Config, BuildingSizes.None);
        var props = new CityPlan.PropArrays
        {
            CentreM = [new(500f, StreetY + 10f), new(500f, StreetY + 1f)], RadiusM = [1.4f, 1.4f], BearingRad = [0f, 0f],
            Kind = [(byte)PropKind.WildNature, (byte)PropKind.WildNature],
        };

        var kept = TownProps.OffTheDrivenGround(props, plan.Roads, plan.Junctions, Config);

        Assert.Equal([new Vector2(500f, StreetY + 10f)], kept.CentreM);
    }

    /// <summary>
    /// <b>No prop and no scenery stands in a building</b>: a building is claimed before either is laid, to the grain of
    /// <see cref="CityGenFigures.ClaimCellM"/>, so no crown reaches further into one than a cell — where a verge's
    /// prop in front of a building fronting the walk would reach in by its whole girth, and the open ground's would
    /// stand all over it.
    /// </summary>
    [Fact]
    public void NoPropStandsInABuilding()
    {
        var plan = TownPlan.Lay(Surveyed(Zoned.Terrace(North)), Config, Houses);

        Assert.NotEmpty(plan.Buildings.CentreM);
        Assert.NotEmpty(plan.Props.CentreM);
        Assert.NotEmpty(plan.Scenery.CentreM);
        for (var prop = 0; prop < plan.Props.Count; prop++) StandsInNoBuilding(plan, plan.Props.CentreM[prop], plan.Props.RadiusM[prop]);
        for (var piece = 0; piece < plan.Scenery.Count; piece++) StandsInNoBuilding(plan, plan.Scenery.CentreM[piece], plan.Scenery.RadiusM[piece]);
    }

    /// <summary>
    /// <b>The open ground grows as thickly as its zone says</b>: a car park north of the street grows nothing, and a wood
    /// the same size south of it grows something.
    /// </summary>
    [Fact]
    public void TheOpenGroundGrowsAsItsZoneSays()
    {
        var plan = TownPlan.Lay(Surveyed(new Zoned.Zone(ZoneKind.Parking, North, []), new Zoned.Zone(ZoneKind.Wild, South, [])), Config, BuildingSizes.None);

        Assert.DoesNotContain(plan.Scenery.CentreM, centreM => Inside(North, centreM));
        Assert.Contains(plan.Scenery.CentreM, centreM => Inside(South, centreM));
    }

    /// <summary><b>A prop the map sets down stands where it is set</b>, a body like any verge's.</summary>
    [Fact]
    public void APropSetDownStandsWhereItIsSet()
    {
        var setDown = new TownMap.PropArrays { Kind = [PropKind.UrbanFurniture], CentreM = [new(450f, 300f)], BearingRad = [0f] };

        var plan = TownPlan.Lay(Surveyed(setDown), Config, BuildingSizes.None);

        Assert.Equal(new Vector2(450f, 300f), plan.Props.CentreM[0]);
    }

    /// <summary><b>The same map stands the same props and scenery every time it is laid</b> (GEN-1): they are drawn off the map's seed.</summary>
    [Fact]
    public void TheSameMapStandsTheSameProps()
    {
        var survey = Surveyed(Zoned.Terrace(North));

        var first = TownPlan.Lay(survey, Config, Houses);
        var second = TownPlan.Lay(survey, Config, Houses);

        Assert.NotEmpty(first.Props.CentreM);
        Assert.NotEmpty(first.Scenery.CentreM);
        Assert.Equal(first.Props.CentreM, second.Props.CentreM);
        Assert.Equal(first.Props.Kind, second.Props.Kind);
        Assert.Equal(first.Scenery.CentreM, second.Scenery.CentreM);
    }

    /// <summary><b>Scenery stands no body</b>: the world stood off a town holds its props as bodies and nothing of its scenery.</summary>
    [Fact]
    public void SceneryStandsNoBody()
    {
        var plan = TownPlan.Lay(Surveyed(), Config, BuildingSizes.None);

        using var world = new TownWorld(plan, Config);

        Assert.NotEmpty(plan.Scenery.CentreM);
        Assert.Equal(plan.Props.Count, world.StaticBodyCount);
    }

    static void StandsInNoBuilding(CityPlan plan, Vector2 centreM, float radiusM)
    {
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var along = Heading.Unit(plan.Buildings.HeadingRad[building]);
            var offM = centreM - plan.Buildings.CentreM[building];
            var pastM = new Vector2(MathF.Abs(Vector2.Dot(offM, along)), MathF.Abs(Vector2.Dot(offM, Heading.RightOf(along))))
                        - (plan.Buildings.SizeM[building] * 0.5f);
            var outsideM = Vector2.Max(pastM, Vector2.Zero).Length() + MathF.Min(MathF.Max(pastM.X, pastM.Y), 0f);
            var intoM = radiusM - outsideM;
            Assert.True(intoM <= Config.CityGen.ClaimCellM, $"a crown at {centreM} reaches {intoM:F2} m into building {building}");
        }
    }

    static bool Inside(Vector2[] box, Vector2 atM) => atM.X > box[0].X && atM.X < box[1].X && atM.Y > box[0].Y && atM.Y < box[2].Y;

    /// <summary>One house prefab, of 12 by 8 m.</summary>
    static readonly BuildingSizes Houses = new(new Vector2[BuildingSizes.Uses], [new(12f, 8f)], [0.5f], [BuildingLook.House]);

    /// <summary>The ground north of the street from 300 to 700 m, reaching to its walk.</summary>
    static readonly Vector2[] North = [new(300f, 100f), new(700f, 100f), new(700f, StreetY - 6f), new(300f, StreetY - 6f)];

    static readonly Vector2[] South = [new(300f, StreetY + 6f), new(700f, StreetY + 6f), new(700f, 900f), new(300f, 900f)];

    /// <summary>A street of a lane each way from 100 to 900 m along <see cref="StreetY"/>, zoned so.</summary>
    static Survey Surveyed(params Zoned.Zone[] zones) => Surveyed(TownMap.PropArrays.None, zones);

    /// <summary>The same, with props set down on it.</summary>
    static Survey Surveyed(TownMap.PropArrays setDown, params Zoned.Zone[] zones) => new()
    {
        Props = setDown,
        Name = "Zoned", Seed = 1, WidthM = 1000f, HeightM = 1000f, PointsM = [100f, StreetY, 900f, StreetY],
        Ways =
        [
            new SurveyWay
            {
                Highway = "residential", LanesForward = 1, LanesBackward = 1, LanesShared = 0,
                CarriagewayM = 2f * OsmCarriageway.AssumedLaneWidthM, CentreOffsetM = 0f, Points = [0, 1],
            },
        ],
        Sea = [],
        Zones = Zoned.Town(1000f, 1000f, [], zones),
    };
}
