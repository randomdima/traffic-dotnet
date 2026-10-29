using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Gen;

/// <summary><b>A generated town's blocks are not all its district's kind</b> (GEN-77).</summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
public class BlockUsesTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Lazy<CityPlan> Plan = new(() => Towns.LayFresh(Towns.Brief(Towns.CitySeed, buildings: 900, country: true)));

    static bool Given(ZoneKind kind) => kind is ZoneKind.Park or ZoneKind.Civic or ZoneKind.Commercial;

    /// <summary><b>A block's use is a zone under a district the town is built in</b>, and never under the country's.</summary>
    [Fact]
    public void EveryUseStandsInADistrictTheTownIsBuiltIn()
    {
        var zones = Plan.Value.Zones;
        var given = 0;
        for (var zone = 0; zone < zones.Count; zone++)
        {
            if (!Given(zones.Kind[zone])) continue;

            given++;
            var parent = zones.Kind[zones.Parent[zone]];
            Assert.True(parent is ZoneKind.OldTown or ZoneKind.Residential or ZoneKind.HighRise or ZoneKind.Suburb or ZoneKind.Industrial,
                $"a {zones.Kind[zone]} block stands under a {parent} zone");
        }

        Assert.True(given > 0, "no block was given a use of its own");
    }

    /// <summary><b>No civic block stands in an industrial district</b>.</summary>
    [Fact]
    public void NoCivicBlockStandsInAnIndustrialDistrict()
    {
        var zones = Plan.Value.Zones;
        for (var zone = 0; zone < zones.Count; zone++)
        {
            if (zones.Kind[zone] == ZoneKind.Civic) Assert.NotEqual(ZoneKind.Industrial, zones.Kind[zones.Parent[zone]]);
        }
    }

    /// <summary><b>A shop block fronts a road between districts</b>: that share of its outline within that road's corridor.</summary>
    [Fact]
    public void EveryShopBlockFrontsARoadBetweenDistricts()
    {
        var plan = Plan.Value;
        var mains = new List<Vector2>();
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.Class[road] != RoadClass.Arterial) continue;

            var arcs = plan.Roads.SegmentsOf(road);
            for (var atM = 0f; atM < Spline.TotalLengthM(arcs); atM += 5f) mains.Add(Spline.SampleAt(arcs, atM).PositionM);
        }

        var reachM = TownGen.CorridorM(Config, LayoutEdge.MostLanes);
        var zones = plan.Zones;
        for (var zone = 0; zone < zones.Count; zone++)
        {
            if (zones.Kind[zone] != ZoneKind.Commercial) continue;

            var outline = zones.OutlineOf(zone).ToArray();
            var fronting = outline.Count(pointM => mains.Exists(mainM => Vector2.Distance(mainM, pointM) <= reachM + 1f));
            Assert.True(fronting >= outline.Length * Config.CityGen.BlockFrontsAShare,
                $"a shop block at {outline[0]} fronts a main road along {fronting} of {outline.Length} points");
        }
    }
}
