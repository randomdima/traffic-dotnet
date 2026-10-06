using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Tools.OsmScan.Meta;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>A traced map without the road stumps that run into buildings or are a single lane, in place</b>
/// (<see cref="TownMap.Stumps"/>, <see cref="TownMap.Without"/>): a stump whose dead end stands inside a footprint
/// of the buildings layer,
/// or that runs through one — under an arch into a courtyard — is dropped, and so is one every road of which is a
/// single lane — a driveway, a yard's lane — and again while dropping one leaves another. The engine lays nothing a road
/// may run into or under, and parks nobody: what such a road leads to is parking the engine does not lay.
/// Run by <c>qq osm --drop-stumps</c>; <c>--dry</c> says what it would drop and drops nothing.
/// </summary>
internal static class Stumps
{
    /// <summary>How many of the stumps dropped are named, to be looked at.</summary>
    const int Named = 12;

    /// <summary>
    /// How much of a stump has to stand inside a building for it to run through one rather than past its wall: the
    /// survey's roads and footprints are mapped apart, and drawn beside a wall a road stands a few metres into it at
    /// worst.
    /// </summary>
    const float ThroughM = 5f;

    public static int Run(string root, string map, bool dry)
    {
        var path = Scan.MapFile(root, map);
        var traced = TownMap.Read(path);
        Crop.Describe(traced, path);

        var folder = Zoning.Laid(root, map, traced);
        var buildings = Import.Footprints(folder, new Plane(traced.Frame), _ => true);
        var all = traced.Stumps(buildings);
        var touching = all.Where(stump => stump.InsideM > 0f).ToList();
        Console.WriteLine(
            $"  {all.Count} stumps, {all.Sum(stump => stump.LengthM) / 1000f:F1} km; {all.Count(stump => stump.EndsInside)} end inside a building, "
            + $"{touching.Count} stand in one somewhere ({touching.Count(stump => !stump.EndsInside)} of them ending outside it, "
            + $"{touching.Where(stump => !stump.EndsInside).Count(Into)} by {ThroughM:F0} m or more); {all.Count(stump => stump.SingleLane)} are a single lane");

        var dropped = new List<Stump>();
        while (true)
        {
            var going = traced.Stumps(buildings).Where(Dropped).ToList();
            if (going.Count == 0) break;

            dropped.AddRange(going);
            traced = traced.Without(going);
        }

        Console.WriteLine($"  dropping {dropped.Count} stumps, {dropped.Sum(stump => stump.LengthM) / 1000f:F1} km — "
                          + $"{dropped.Count(Into)} into buildings, {dropped.Count(stump => !Into(stump))} a single lane besides; "
                          + string.Join(", ", ((ReadOnlySpan<float>)[25f, 50f, 100f, 200f]).ToArray().Select(upToM => $"{dropped.Count(stump => stump.LengthM <= upToM)} up to {upToM:F0} m"))
                          + $", {dropped.Count(stump => stump.LengthM > 200f)} longer; the longest:");
        foreach (var stump in dropped.OrderByDescending(stump => stump.LengthM).Take(Named))
        {
            Console.WriteLine(
                $"    {stump.LengthM,6:F1} m, {(Into(stump) ? $"{stump.InsideM:F1} m inside" : "a single lane")}, ending at ({stump.EndM.X:F0}, {stump.EndM.Y:F0})");
        }

        if (dry) return 0;

        traced.Write(path);
        Crop.Describe(traced, path);
        return 0;
    }

    static bool Dropped(Stump stump) => Into(stump) || stump.SingleLane;

    static bool Into(Stump stump) => stump.EndsInside || stump.InsideM >= ThroughM;
}
