using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Gen;

/// <summary>
/// <b>A town grades out to the country</b> (GEN-76): built solid about its core, each district further out building less,
/// and only districts on the map's rim left to the country.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class TownExtentTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Vector2 ExtentM = new(4000f, 3000f);

    static readonly Vector2 CoreM = new(1300f, 1700f);

    static readonly Lazy<Partition> Land = new(() =>
    {
        var grid = CellGrid.Over(ExtentM, Config.CityGen.DistrictCellM);
        return Partition.Cut(grid, new TrafficSimulation.CityGen.Gen.Barrier[grid.Count], Config, 7);
    });

    /// <summary>The town graded evenly: no draw moves a district's place or its fill.</summary>
    static readonly SimConfig Even = new() { CityGen = new CityGenFigures { TownEdgeJitterShare = 0f, TownFillSpread = 0f } };

    static float FurthestM => TownExtent.FurthestM(Land.Value, CoreM);

    static float[] Fill(SimConfig config, IReadOnlyList<Polyline>? roadsIn = null) =>
        TownExtent.Fill(Land.Value, CoreM, FurthestM, roadsIn ?? [], config, 7);

    static float AwayM(int district) => Vector2.Distance(Land.Value.CentreM[district], CoreM);

    [Fact]
    public void EveryDistrictWithinTheSolidMiddleBuildsAllItsKindBuilds()
    {
        var fill = Fill(Even);
        var solidM = FurthestM * Even.CityGen.TownSolidShare;
        var within = Enumerable.Range(0, fill.Length).Where(district => AwayM(district) <= solidM).ToArray();

        Assert.NotEmpty(within);
        Assert.All(within, district => Assert.Equal(1f, fill[district]));
    }

    [Fact]
    public void ADistrictFurtherOutNeverBuildsMore()
    {
        var fill = Fill(Even);
        var outward = Enumerable.Range(0, fill.Length).OrderBy(AwayM).ToArray();
        for (var at = 1; at < outward.Length; at++)
        {
            var (nearer, further) = (outward[at - 1], outward[at]);
            Assert.True(fill[further] <= fill[nearer], $"district {further}, {AwayM(further):F0} m out, builds {fill[further]:F2}; {nearer}, {AwayM(nearer):F0} m out, {fill[nearer]:F2}");
        }
    }

    /// <summary><b>A district beside a road into the town builds more</b> than it would with none.</summary>
    [Fact]
    public void ADistrictBesideARoadInBuildsMore()
    {
        var road = new Polyline([CoreM, new Vector2(ExtentM.X, CoreM.Y)]);
        var (without, with) = (Fill(Even), Fill(Even, [road]));
        var beside = Enumerable.Range(0, without.Length)
            .Where(district => road.Nearest(Land.Value.CentreM[district]).AwayM < Config.CityGen.TownRoadwardReachM * 0.5f && without[district] < 1f)
            .ToArray();

        Assert.NotEmpty(beside);
        Assert.All(beside, district => Assert.True(with[district] > without[district], $"district {district} builds {with[district]:F2} beside a road and {without[district]:F2} with none"));
    }

    /// <summary><b>Only the rim is left to the country</b>, and none of it within the solid middle.</summary>
    [Fact]
    public void OnlyDistrictsOnTheRimBeyondTheSolidMiddleAreLeftToTheCountry()
    {
        var all = new SimConfig { CityGen = new CityGenFigures { CountryRimShare = 1f, TownEdgeJitterShare = 0f } };
        var built = TownExtent.Built(Land.Value, CoreM, FurthestM, [], all, 7);
        var grid = Land.Value.Grid;
        var rim = new HashSet<int>();
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var (column, row) = (cell % grid.Columns, cell / grid.Columns);
            if (column == 0 || row == 0 || column == grid.Columns - 1 || row == grid.Rows - 1) rim.Add(Land.Value.Label[cell]);
        }

        var solidM = FurthestM * all.CityGen.TownSolidShare;
        for (var district = 0; district < built.Length; district++)
        {
            if (built[district]) continue;

            Assert.Contains(district, rim);
            Assert.True(AwayM(district) > solidM, $"district {district}, {AwayM(district):F0} m out, is left within the solid middle's {solidM:F0} m");
        }

        Assert.Contains(false, built);
    }

    /// <summary>
    /// <b>Every district a town leaves is the country's</b>: farmland or wild, one zone each, and the districts it builds are
    /// all that is left of the land's.
    /// </summary>
    [Fact]
    public void EveryDistrictATownLeavesIsAZoneOfTheCountry()
    {
        var allTheRim = new SimConfig { CityGen = new CityGenFigures { CountryRimShare = 1f, TownSolidShare = 0f } };
        var town = TownGen.Author(Towns.Brief(Towns.CitySeed, buildings: 300, country: true), allTheRim);
        var zones = town.Map.Zones;
        var country = 0;
        for (var zone = 0; zone < zones.Count; zone++)
        {
            if (zones.Kind[zone] is not (ZoneKind.Farmland or ZoneKind.Wild)) continue;

            country++;
            var outline = zones.OutlineOf(zone);
            var builtUnder = 0;
            foreach (var pointM in outline) builtUnder += town.Partition.At(pointM) >= 0 ? 1 : 0;

            // Its outline is its own cells' edge, a cell's corner each: a corner shared with a built district counts for it.
            Assert.True(builtUnder < outline.Length, $"a {zones.Kind[zone]} zone stands wholly in districts the town is built in");
        }

        Assert.True(country > 0, "a town leaving all its rim it can left no district to the country");
    }
}
