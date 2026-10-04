using System.Text.Json;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Tools.OsmScan.Meta;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>A traced map's footprints laid again off the enrichment's layers, in place</b>: every building's outline, its
/// courtyards, its height and what it is for (<see cref="FootprintUses"/>), kept where it stands wholly inside the map's
/// own frame as a crop keeps one. Run by <c>qq osm --footprints</c>. An edit like any other: nothing but the footprints
/// is touched, so the crop and the dropped stumps stand where an import would lose them.
/// </summary>
internal static class Footprints
{
    public static int Run(string root, string map)
    {
        var path = Path.Combine(root, "towns", "traced", $"{map}.map");
        var traced = TracedMap.Read(path);
        var folder = Path.Combine(root, "towns", "traced", map);
        using (var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "manifest.json"))))
        {
            var laidOff = manifest.RootElement.GetProperty("surveyOsmBase").GetString();
            if (laidOff != traced.OsmBase)
            {
                throw new InvalidDataException($"{map}: the layers were laid off the survey of {laidOff}, and the map was imported off {traced.OsmBase}.");
            }
        }

        var plane = new Plane(traced.Frame);
        var (widthM, heightM) = ((float)traced.Frame.WidthM, (float)traced.Frame.HeightM);
        var footprints = Import.Footprints(
            folder, plane, outline => outline.All(atM => atM.X >= 0 && atM.Y >= 0 && atM.X <= widthM && atM.Y <= heightM));

        Crop.Describe(traced, path);
        traced = traced with { Footprints = footprints };
        traced.Write(path);
        Crop.Describe(traced, path);
        foreach (var group in footprints.Use.GroupBy(use => use).OrderByDescending(group => group.Count()))
        {
            Console.WriteLine($"  {group.Key,-12} {group.Count(),7}");
        }

        return 0;
    }
}
