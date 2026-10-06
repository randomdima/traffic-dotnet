using System.Globalization;
using TrafficSimulation.CityGen.Map;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>A traced map cut down to a box of degrees, in place</b> (<see cref="TownMap.Cropped"/>): the map is the box
/// and its margin, in whole metres of its own frame and never past it, and its roads run off at its edge.
/// Run by <c>qq osm --crop S,W,N,E</c>. An edit like any other: the map is the master, and nothing it was imported
/// from is touched, so what is cut away comes back only by importing again.
/// </summary>
internal static class Crop
{
    public static int Run(string root, string map, string box)
    {
        var degrees = box.Split(',').Select(part => double.Parse(part, CultureInfo.InvariantCulture)).ToArray();
        if (degrees is not [var south, var west, var north, var east] || south >= north || west >= east)
        {
            throw new ArgumentException($"a box is south,west,north,east in degrees, south below north and west of east: {box}");
        }

        var path = Scan.MapFile(root, map);
        var traced = TownMap.Read(path);
        var frame = traced.Frame;
        var projection = frame.Projection();
        var corners = new[] { (south, west), (south, east), (north, west), (north, east) }.Select(corner => frame.Place(projection, corner.Item1, corner.Item2)).ToArray();

        var leftM = Math.Max(0, (int)Math.Floor(corners.Min(corner => corner.X) - frame.MarginM));
        var topM = Math.Max(0, (int)Math.Floor(corners.Min(corner => corner.Y) - frame.MarginM));
        var rightM = Math.Min((int)frame.WidthM, (int)Math.Ceiling(corners.Max(corner => corner.X) + frame.MarginM));
        var bottomM = Math.Min((int)frame.HeightM, (int)Math.Ceiling(corners.Max(corner => corner.Y) + frame.MarginM));

        Describe(traced, path);
        var cropped = traced.Cropped(leftM, topM, rightM - leftM, bottomM - topM);
        cropped.Write(path);
        Console.WriteLine($"cut to ({leftM}, {topM}) – ({rightM}, {bottomM}) m of the old frame, {box}:");
        Describe(cropped, path);
        return 0;
    }

    /// <summary>One line of what a map holds and what it costs on disk.</summary>
    public static void Describe(TownMap traced, string path) =>
        Console.WriteLine(
            $"  {traced.Name}: {traced.Frame.WidthM:F0} x {traced.Frame.HeightM:F0} m, {traced.Roads.Length} roads over {traced.PointM.Length} points, "
            + $"{traced.Roads.Count(road => road.WidthM is not null)} measured, {traced.Coast.Length} coast ways, "
            + $"{traced.Zones.Count} zones of {traced.Zones.PointM.Length} points and {traced.Zones.ParamKey.Length} settings — "
            + $"{new FileInfo(path).Length / 1024} KB");
}
