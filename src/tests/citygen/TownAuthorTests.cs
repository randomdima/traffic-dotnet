using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>A map authored off a brief is the brief's town, read off a file</b> (GEN-58, <see cref="TownAuthor"/>): its water
/// a course and its wheel a whole map's zone, every district a zone of the sector it is, and the town its written map
/// lays the town the map in memory lays — so a brief authored once and shipped is the town the suite lays off it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class TownAuthorTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A brief's map is a wheel with a zone a district</b>: the whole map's zone a wheel saying the town's counts, and
    /// every district of its wheel a zone saying which sector it is.
    /// </summary>
    [Fact]
    public void ABriefsMapIsAWheelWithAZoneADistrict()
    {
        var brief = Towns.Brief(Towns.CitySeed, buildings: 200, people: 60);

        var zones = TownAuthor.Of(brief, Config).Zones;

        Assert.Equal(ZoneKind.Wheel, zones.Kind[TownMap.ZoneArrays.Root]);
        Assert.Equal((200f, 60f), (zones.Own(TownMap.ZoneArrays.Root, ZoneParam.Buildings), zones.Own(TownMap.ZoneArrays.Root, ZoneParam.People)));
        var spokes = (int)zones.Own(TownMap.ZoneArrays.Root, ZoneParam.Spokes)!.Value;
        var sectors = Enumerable.Range(1, zones.Count - 1).Where(zone => zones.Own(zone, ZoneParam.Sector) is not null).ToArray();
        Assert.Equal(spokes * 2, sectors.Length);
        Assert.All(sectors, zone => Assert.Equal(TownMap.ZoneArrays.Root, zones.Parent[zone]));
    }

    /// <summary><b>A brief's river is a course on its map</b>, which a road may span (GEN-14b).</summary>
    [Fact]
    public void ABriefsRiverIsACourse()
    {
        var map = TownAuthor.Of(Towns.Brief(Towns.CitySeed), Config);

        Assert.Equal([TownMap.WaterBody.River], map.Courses.Kind);
        Assert.True(Survey.Of(map, Config).Bridgeable);
    }

    /// <summary>
    /// <b>The town a map's file lays is the town the map in memory lays</b>: authored, written, read back and laid, the
    /// same roads, junctions and buildings stand at the same places.
    /// </summary>
    [Fact]
    public void ItsFileLaysTheTownItsMapDoes()
    {
        var map = TownAuthor.Of(Towns.Brief(Towns.CitySeed, buildings: 200), Config);
        using var bytes = new MemoryStream();
        map.Write(bytes);
        var read = TownMap.Read(bytes.ToArray(), "test");

        var (inMemory, offTheFile) = (TownPlan.Lay(map, Config, BuildingCatalog.Roofs), TownPlan.Lay(read, Config, BuildingCatalog.Roofs));

        Assert.Equal(inMemory.Junctions.CentreM, offTheFile.Junctions.CentreM);
        Assert.Equal(inMemory.Roads.Segments.Select(segment => segment.StartM), offTheFile.Roads.Segments.Select(segment => segment.StartM));
        Assert.Equal(inMemory.Buildings.CentreM, offTheFile.Buildings.CentreM);
    }
}
