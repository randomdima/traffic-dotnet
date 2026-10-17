using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Tools.OsmScan.Meta;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>A traced map without the ways that are a place's own ground, in place</b> (<see cref="TownMap.WithoutWays"/>): every
/// road whose way the roads layer reads as a <c>zone_way</c> — a car park's aisle, a service way inside a yard, a garage
/// block, a market or a cemetery — is dropped whole, but for one on a level above the ground. Such a way is how one place is got about, not a route through the
/// town: drawn to the car park's own shape, it doubles back on itself tighter than a car steers, and a tour drawn down
/// it is a car lost off its line. The ground of a car park is a zone of its own, which the engine does not lay yet.
/// Run by <c>qq osm --drop-zone-ways</c>; <c>--dry</c> says what it would drop and drops nothing. A street left ending
/// where one was dropped is a stump for <see cref="Stumps"/>.
/// </summary>
internal static class ZoneWays
{
    public static int Run(string root, string map, bool dry)
    {
        var path = Scan.MapFile(root, map);
        var traced = TownMap.Read(path);
        Crop.Describe(traced, path);

        var records = Import.Layer<RoadRecord>(Path.Combine(root, "towns", "traced", map), "roads");
        // One carried over something is a ramp or a flyover drawn inside a bridge's outline, and a route however it is served.
        var zoneWays = records.Where(record => record.Role == "zone_way" && record.Layer == 0).ToDictionary(record => record.Way);
        var going = traced.Roads.Where(road => zoneWays.ContainsKey(road.OsmId)).ToList();
        Console.WriteLine(
            $"  dropping {going.Count} of {traced.Roads.Length} roads, the ground of a place: "
            + string.Join(", ", going.GroupBy(road => zoneWays[road.OsmId].ZoneKind ?? "no zone").OrderByDescending(kind => kind.Count()).Select(kind => $"{kind.Key} {kind.Count()}")));
        if (dry) return 0;

        traced = traced.WithoutWays(zoneWays.Keys.ToHashSet());
        traced.Write(path);
        Crop.Describe(traced, path);
        return 0;
    }
}
