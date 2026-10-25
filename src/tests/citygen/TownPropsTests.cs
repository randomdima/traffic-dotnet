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
/// standing as it is set, each laid at the girth of a look of its own kind (GEN-6e) and none where the town is handed no
/// looks, and the same every time the map is laid.
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
        var plan = TownPlan.Lay(Surveyed(), Config, ArtSizes.None);
        var props = new CityPlan.PropArrays
        {
            CentreM = [new(500f, StreetY + 10f), new(500f, StreetY + 1f)], RadiusM = [1.4f, 1.4f], BodyRadiusM = [1.4f, 1.4f], BearingRad = [0f, 0f],
            Kind = [(byte)PropKind.WildNature, (byte)PropKind.WildNature], Look = [0, 0],
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
        var plan = TownPlan.Lay(Surveyed(Zoned.Terrace(North)), Config, Housed);

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
        var plan = TownPlan.Lay(Surveyed(new Zoned.Zone(ZoneKind.Parking, North, []), new Zoned.Zone(ZoneKind.Wild, South, [])), Config, Bare);

        Assert.DoesNotContain(plan.Scenery.CentreM, centreM => Inside(North, centreM));
        Assert.Contains(plan.Scenery.CentreM, centreM => Inside(South, centreM));
    }

    /// <summary><b>A prop the map sets down stands where it is set</b>, a body like any verge's.</summary>
    [Fact]
    public void APropSetDownStandsWhereItIsSet()
    {
        var setDown = new TownMap.PropArrays { Kind = [PropKind.UrbanFurniture], CentreM = [new(450f, 300f)], BearingRad = [0f] };

        var plan = TownPlan.Lay(Surveyed(setDown), Config, Bare);

        Assert.Equal(new Vector2(450f, 300f), plan.Props.CentreM[0]);
    }

    /// <summary>
    /// <b>A prop is laid at its look's girth</b> (GEN-6e): every prop and every piece of scenery stands as a disc half as
    /// wide across as the look it wears keeps.
    /// </summary>
    [Fact]
    public void APropIsLaidAtItsLooksGirth()
    {
        var plan = TownPlan.Lay(Surveyed(new Zoned.Zone(ZoneKind.Wild, South, [])), Config, Bare);

        Assert.NotEmpty(plan.Props.CentreM);
        Assert.NotEmpty(plan.Scenery.CentreM);
        for (var prop = 0; prop < plan.Props.Count; prop++) Assert.Equal(Looks.GirthM[plan.Props.Look[prop]] * 0.5f, plan.Props.RadiusM[prop]);
        for (var piece = 0; piece < plan.Scenery.Count; piece++) Assert.Equal(Looks.GirthM[plan.Scenery.Look[piece]] * 0.5f, plan.Scenery.RadiusM[piece]);
    }

    /// <summary>
    /// <b>A prop stands as its look's body and not as its girth</b> (GEN-6e): a street tree's trunk under its crown, and
    /// the girth where the look states no collider.
    /// </summary>
    [Fact]
    public void APropStandsAsItsLooksBody()
    {
        var plan = TownPlan.Lay(Surveyed(), Config, Bare);

        Assert.Contains(plan.Props.Look, look => Looks.BodyM[look] < Looks.GirthM[look]);
        for (var prop = 0; prop < plan.Props.Count; prop++) Assert.Equal(Looks.BodyM[plan.Props.Look[prop]] * 0.5f, plan.Props.BodyRadiusM[prop]);
    }

    /// <summary><b>A prop wears a look of its own kind</b> (GEN-6b, GEN-6e), and scenery a wild one.</summary>
    [Fact]
    public void APropWearsALookOfItsOwnKind()
    {
        var plan = TownPlan.Lay(Surveyed(new Zoned.Zone(ZoneKind.Wild, South, [])), Config, Bare);

        Assert.NotEmpty(plan.Props.CentreM);
        for (var prop = 0; prop < plan.Props.Count; prop++) Assert.Equal((PropKind)plan.Props.Kind[prop], Looks.Kind[plan.Props.Look[prop]]);
        Assert.All(plan.Scenery.Look, look => Assert.Equal(PropKind.WildNature, Looks.Kind[look]));
    }

    /// <summary>
    /// <b>A town handed no looks lays no prop</b> (GEN-6e): not along its kerbs, not over its open ground, and not where
    /// its map sets one down — a body nothing draws is the one outcome that reads as a renderer bug.
    /// </summary>
    [Fact]
    public void ATownHandedNoLooksLaysNoProp()
    {
        var setDown = new TownMap.PropArrays { Kind = [PropKind.UrbanFurniture], CentreM = [new(450f, 300f)], BearingRad = [0f] };

        var plan = TownPlan.Lay(Surveyed(setDown, new Zoned.Zone(ZoneKind.Wild, South, [])), Config, ArtSizes.None);

        Assert.Empty(plan.Props.CentreM);
        Assert.Empty(plan.Scenery.CentreM);
    }

    /// <summary><b>The same map stands the same props and scenery every time it is laid</b> (GEN-1): they are drawn off the map's seed.</summary>
    [Fact]
    public void TheSameMapStandsTheSameProps()
    {
        var survey = Surveyed(Zoned.Terrace(North));

        var first = TownPlan.Lay(survey, Config, Housed);
        var second = TownPlan.Lay(survey, Config, Housed);

        Assert.NotEmpty(first.Props.CentreM);
        Assert.NotEmpty(first.Scenery.CentreM);
        Assert.Equal(first.Props.CentreM, second.Props.CentreM);
        Assert.Equal(first.Props.Look, second.Props.Look);
        Assert.Equal(first.Scenery.CentreM, second.Scenery.CentreM);
        Assert.Equal(first.Scenery.Look, second.Scenery.Look);
    }

    /// <summary><b>Scenery stands no body</b>: the world stood off a town holds its props as bodies and nothing of its scenery.</summary>
    [Fact]
    public void SceneryStandsNoBody()
    {
        var plan = TownPlan.Lay(Surveyed(), Config, Bare);

        using var world = new TownWorld(plan, Config);

        Assert.NotEmpty(plan.Scenery.CentreM);
        Assert.Equal(plan.Props.Count, world.StaticBodyCount);
    }

    /// <summary>
    /// <b>A tree-lined street's trees stand a pitch apart along it</b> (GEN-6f): down the straight of the street, every
    /// crowned look on either side stands a whole number of pitches from the one before it — a tree missing from the row
    /// leaves its place empty and moves none of the others.
    /// </summary>
    [Fact]
    public void ATreeLinedStreetsTreesStandAPitchApart()
    {
        var config = new SimConfig { CityGen = new CityGenFigures { PropTreeLinedShare = 1f, PropFurnitureShare = 1f } };
        var pitchM = config.CityGen.PropStreetTreePitchM;

        var plan = TownPlan.Lay(Surveyed(), config, Lined);

        foreach (var north in (ReadOnlySpan<bool>)[true, false])
        {
            var alongM = new List<float>();
            for (var prop = 0; prop < plan.Props.Count; prop++)
            {
                var atM = plan.Props.CentreM[prop];
                if (Lined.Props.BodyM[plan.Props.Look[prop]] < Lined.Props.GirthM[plan.Props.Look[prop]]
                    && atM.Y < StreetY == north && atM.X is > 200f and < 800f) alongM.Add(atM.X);
            }

            alongM.Sort();
            Assert.True(alongM.Count > 1, $"the {(north ? "north" : "south")} side's row holds {alongM.Count} trees");
            for (var tree = 1; tree < alongM.Count; tree++)
            {
                var pitches = (alongM[tree] - alongM[tree - 1]) / pitchM;
                Assert.Equal(MathF.Round(pitches), pitches, 1e-3f);
            }
        }
    }

    /// <summary>
    /// <b>A run is one look laid again a gap of grass along the kerb</b> (GEN-6h): with every station a run of two, most
    /// of a straight verge's bins stand exactly a girth and a gap from another — and with none a run, not one does, the
    /// stations being drawn anywhere in their step.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(0f)]
    public void ARunIsOneLookLaidAgainAGapAlong(float runShare)
    {
        var config = new SimConfig
        {
            CityGen = new CityGenFigures
            {
                PropTreeLinedShare = 0f, PropFurnitureShare = 1f, PropRunShare = runShare, PropRunMost = 2, PropVergePitchM = 20f,
            },
        };
        var runM = Binned.Props.GirthM[0] + config.CityGen.PropRunGapM;

        var plan = TownPlan.Lay(Surveyed(), config, Binned);

        var straight = plan.Props.CentreM.Where(atM => atM.X is > 200f and < 800f).ToArray();
        var inRuns = straight.Count(atM => straight.Any(other => MathF.Abs(Vector2.Distance(atM, other) - runM) < 1e-3f));
        Assert.NotEmpty(straight);
        if (runShare > 0f) Assert.True(inRuns * 2 > straight.Length, $"{inRuns} of {straight.Length} bins stand in a run");
        else Assert.Equal(0, inRuns);
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
            if (!(intoM <= Config.CityGen.ClaimCellM))
            {
                Assert.Fail($"a crown at {centreM} reaches {intoM:F2} m into building {building}");
            }
        }
    }

    static bool Inside(Vector2[] box, Vector2 atM) => atM.X > box[0].X && atM.X < box[1].X && atM.Y > box[0].Y && atM.Y < box[2].Y;

    /// <summary>One house prefab, of 12 by 8 m.</summary>
    static readonly BuildingSizes Houses = new(new Vector2[BuildingSizes.Uses], [new(12f, 8f)], [0.5f], [BuildingLook.House], []);

    /// <summary>
    /// Two looks of each kind, every one a girth of its own: a bollard to a great tree, the tree standing on its trunk.
    /// </summary>
    static readonly PropSizes Looks = new(
        [PropKind.WildNature, PropKind.UrbanNature, PropKind.UrbanFurniture, PropKind.WildNature, PropKind.UrbanNature, PropKind.UrbanFurniture],
        [4.5f, 1.2f, 0.25f, 1.8f, 2.4f, 2.09f],
        [0.5f, 1.2f, 0.25f, 1.8f, 2.4f, 2.09f]);

    /// <summary>A weed, a lime on its trunk, a pot, and a bin: what a street is lined with and what fills between.</summary>
    static readonly ArtSizes Lined = new(
        BuildingSizes.None,
        new PropSizes(
            [PropKind.WildNature, PropKind.UrbanNature, PropKind.UrbanNature, PropKind.UrbanFurniture],
            [1f, 3.4f, 1f, 0.5f],
            [1f, 0.5f, 1f, 0.5f]));

    /// <summary>One bin and nothing else, so every verge prop is the same look.</summary>
    static readonly ArtSizes Binned = new(BuildingSizes.None, new PropSizes([PropKind.UrbanFurniture], [0.5f], [0.5f]));

    /// <summary>The looks and no roof: a town that stands its props and no building.</summary>
    static readonly ArtSizes Bare = new(BuildingSizes.None, Looks);

    /// <summary>The looks and the house.</summary>
    static readonly ArtSizes Housed = new(Houses, Looks);

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
