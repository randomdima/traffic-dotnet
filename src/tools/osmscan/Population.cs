using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>Who a traced map's town stands, set in place</b> (<see cref="ZoneParam.People"/>, <see cref="ZoneParam.Cars"/>):
/// how many people at its doors and how many cars on its lanes, said by its whole map's zone. Run by
/// <c>qq osm --people N --cars N</c>; a count left out keeps the map's own. An edit like any other: nothing else is
/// touched, and an import lays nobody.
/// </summary>
internal static class Population
{
    public static int Run(string root, string map, int? people, int? cars)
    {
        var path = Scan.MapFile(root, map);
        var traced = TownMap.Read(path);
        var (wasPeople, wasCars) = (traced.People, traced.Cars);
        var zones = traced.Zones.With(TownMap.ZoneArrays.Root, ZoneParam.People, people ?? wasPeople).With(TownMap.ZoneArrays.Root, ZoneParam.Cars, cars ?? wasCars);
        traced = traced with { Zones = zones };
        traced.Write(path);

        Crop.Describe(traced, path);
        Console.WriteLine($"  population: {wasPeople} people and {wasCars} cars, now {traced.People} and {traced.Cars}");
        return 0;
    }
}
