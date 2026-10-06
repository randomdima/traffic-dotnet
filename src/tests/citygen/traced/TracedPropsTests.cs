using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A traced town's props are laid as a generated town's are</b> (GEN-6b, GEN-57, <see cref="TracedProps"/>): none
/// within a lattice step of a carriageway, none in a building, the open ground's as scenery that stands no body, and
/// the same every time the map is laid.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class TracedPropsTests
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
        var plan = TracedPlan.Lay(Surveyed(), Config, BuildingSizes.None);
        var props = new CityPlan.PropArrays
        {
            CentreM = [new(500f, StreetY + 10f), new(500f, StreetY + 1f)], RadiusM = [1.4f, 1.4f], BearingRad = [0f, 0f],
            Kind = [(byte)PropKind.WildNature, (byte)PropKind.WildNature],
        };

        var kept = TracedProps.OffTheDrivenGround(props, plan.Roads, plan.Junctions, Config);

        Assert.Equal([new Vector2(500f, StreetY + 10f)], kept.CentreM);
    }

    /// <summary>
    /// <b>No prop and no scenery stands in a building</b>: a building is claimed before either is laid, to the grain of
    /// <see cref="CityGenFigures.TracedClaimCellM"/>, so no crown reaches further into one than a cell — where a verge's
    /// prop in front of a building fronting the walk would reach in by its whole girth, and the open ground's would
    /// stand all over it.
    /// </summary>
    [Fact]
    public void NoPropStandsInABuilding()
    {
        var plan = TracedPlan.Lay(Surveyed(Box(500f, StreetY - 10f, 40f, 40f)), Config, BuildingSizes.None);

        Assert.NotEmpty(plan.Buildings.CentreM);
        Assert.NotEmpty(plan.Props.CentreM);
        Assert.NotEmpty(plan.Scenery.CentreM);
        for (var prop = 0; prop < plan.Props.Count; prop++) StandsInNoBuilding(plan, plan.Props.CentreM[prop], plan.Props.RadiusM[prop]);
        for (var piece = 0; piece < plan.Scenery.Count; piece++) StandsInNoBuilding(plan, plan.Scenery.CentreM[piece], plan.Scenery.RadiusM[piece]);
    }

    /// <summary><b>The same map stands the same props and scenery every time it is laid</b> (GEN-57): they are drawn off the map's own number.</summary>
    [Fact]
    public void TheSameMapStandsTheSameProps()
    {
        var survey = Surveyed(Box(500f, StreetY - 10f, 40f, 40f));

        var first = TracedPlan.Lay(survey, Config, BuildingSizes.None);
        var second = TracedPlan.Lay(survey, Config, BuildingSizes.None);

        Assert.NotEmpty(first.Props.CentreM);
        Assert.NotEmpty(first.Scenery.CentreM);
        Assert.Equal(first.Props.CentreM, second.Props.CentreM);
        Assert.Equal(first.Props.Kind, second.Props.Kind);
        Assert.Equal(first.Scenery.CentreM, second.Scenery.CentreM);
    }

    /// <summary><b>Scenery stands no body</b>: the world stood off a traced town holds its props as bodies and nothing of its scenery.</summary>
    [Fact]
    public void SceneryStandsNoBody()
    {
        var plan = TracedPlan.Lay(Surveyed(), Config, BuildingSizes.None);

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
            Assert.True(intoM <= Config.CityGen.TracedClaimCellM, $"a crown at {centreM} reaches {intoM:F2} m into building {building}");
        }
    }

    /// <summary>A rectangle as wide as given and as deep, its south wall at <paramref name="southY"/> and centred on <paramref name="x"/>.</summary>
    static Vector2[] Box(float x, float southY, float widthM, float depthM) =>
        [new(x - (widthM * 0.5f), southY - depthM), new(x + (widthM * 0.5f), southY - depthM), new(x + (widthM * 0.5f), southY), new(x - (widthM * 0.5f), southY)];

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
