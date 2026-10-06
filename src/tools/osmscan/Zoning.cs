using System.Text.Json;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Tools.OsmScan.Meta;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>A traced map's zones laid again off the enrichment's layers, in place</b> (<see cref="ZoneHints"/>): the land uses
/// meeting the map's own frame laid as patches of one kind inside it, each a kind and what its buildings measure, nested
/// as they stand in each other — and the buildings they were measured off written beside the layers, for a probe to weigh the
/// town against (<see cref="SurveyFootprints"/>, <c>footprints.bin</c>). Run by <c>qq osm --zones</c>. An edit like any
/// other: nothing but the zones is touched, so the crop, the dropped stumps and who the town stands stay as they were.
/// </summary>
internal static class Zoning
{
    public static int Run(string root, string map)
    {
        var path = Scan.MapFile(root, map);
        var traced = TownMap.Read(path);
        Crop.Describe(traced, path);
        traced = Zoned(root, map, traced);
        traced.Write(path);
        Crop.Describe(traced, path);
        Tell(traced.Zones);
        return 0;
    }

    /// <summary>
    /// A map with its zones laid off its layers, its whole map's own settings kept — and the buildings measured written to
    /// <c>footprints.bin</c> in the layers' folder.
    /// </summary>
    public static TownMap Zoned(string root, string map, TownMap traced)
    {
        var folder = Laid(root, map, traced);
        var plane = new Plane(traced.Frame);
        var meets = Meets(traced.Frame);
        var buildings = Import.Footprints(folder, plane, outline => meets([.. outline.Select(atM => new Pt(atM.X, atM.Y))]));
        var zones = ZoneHints.Of(folder, plane, buildings, meets, traced);
        for (var at = traced.Zones.ParamOffsets[TownMap.ZoneArrays.Root]; at < traced.Zones.ParamOffsets[TownMap.ZoneArrays.Root + 1]; at++)
        {
            zones = zones.With(TownMap.ZoneArrays.Root, traced.Zones.ParamKey[at], traced.Zones.ParamValue[at]);
        }

        var reference = Path.Combine(folder, SurveyFootprints.File);
        buildings.Write(reference);
        Console.WriteLine($"  {buildings.Count} buildings measured, written to {reference}  {new FileInfo(reference).Length / 1024} KB");
        return traced with { Zones = zones };
    }

    /// <summary>The layers' folder, refused where they were laid off another survey than the map was imported off.</summary>
    public static string Laid(string root, string map, TownMap traced)
    {
        var folder = Path.Combine(root, "towns", "traced", map);
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "manifest.json")));
        var laidOff = manifest.RootElement.GetProperty("surveyOsmBase").GetString();
        if (laidOff != traced.OsmBase)
        {
            throw new InvalidDataException($"{map}: the layers were laid off the survey of {laidOff}, and the map was imported off {traced.OsmBase}.");
        }

        return folder;
    }

    /// <summary>Whether an outline's box meets a map's frame.</summary>
    public static Func<Pt[], bool> Meets(OsmFrame frame)
    {
        var whole = new Box(0, 0, frame.WidthM, frame.HeightM);
        return outline => Box.Of(outline).Meets(whole);
    }

    /// <summary>How many zones there are of each kind, and how many say anything of themselves.</summary>
    public static void Tell(TownMap.ZoneArrays zones)
    {
        var counted = Enumerable.Range(0, zones.Count).GroupBy(zone => zones.Kind[zone]).OrderByDescending(group => group.Count())
            .Select(group => $"{group.Key} {group.Count()} ({group.Count(zone => zones.ParamOffsets[zone + 1] > zones.ParamOffsets[zone])} measured)");
        Console.WriteLine($"  zones: {string.Join(", ", counted)}");
    }
}
