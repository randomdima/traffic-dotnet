using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>Who a traced map's town stands, set in place</b> (<see cref="TracedMap.Population"/>): how many people at its doors
/// and how many cars on its lanes. Run by <c>qq osm --people N --cars N</c>; a count left out keeps the map's own. An edit
/// like any other: nothing else is touched, and an import lays nobody.
/// </summary>
internal static class Population
{
    public static int Run(string root, string map, int? people, int? cars)
    {
        var path = Path.Combine(root, "towns", "traced", $"{map}.map");
        var traced = TracedMap.Read(path);
        var was = traced.Population;
        traced = traced with { Population = new TracedPopulation(people ?? was.People, cars ?? was.Cars) };
        traced.Write(path);

        Crop.Describe(traced, path);
        Console.WriteLine($"  population: {was.People} people and {was.Cars} cars, now {traced.Population.People} and {traced.Population.Cars}");
        return 0;
    }
}
