using System.Globalization;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>How a town's walk was built as its zones say</b> (GEN-58, <c>--bench fit</c>): how many places along the walk were
/// asked what stands behind them and how many build nothing, how many it stood were thinned to the town's plan, and a row
/// a look — the buildings stood, the places of that look that stood none, and the ground they cover. It gates nothing:
/// how well a catalogue and a map's zones build a town is a fact about the two.
/// </summary>
/// <remarks><b>With <c>--out FILE</c> it writes every building stood</b>, one line each: its look, its sides and its prefab.</remarks>
internal static class TracedFit
{
    public static void Run(string map, SimConfig config, string? outPath)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var survey = Survey.Of(Maps.Read(map), config);
        var paving = plan.Paving(config);
        var zones = new ZoneTree(survey.Zones, config.Zones);
        var laying = ZoneBuildings.Laid(zones, survey, BuildingStage.Yards.None(config), paving, new GroundShapes(paving, config), BuildingCatalog.Roofs, config);
        var buildings = laying.Buildings;
        var looks = BuildingCatalog.Roofs.PrefabLook;

        Console.WriteLine(
            $"{map}: {zones.Count} zones; {laying.Places} places on the walk asked, {laying.Open} building nothing; " +
            $"{laying.Thinned} thinned to the plan; {buildings.Count} buildings stood, {laying.Behind} of them behind the frontage");
        Console.WriteLine($"  {"look",-12}{"stood",8}{"refused",9}{"ground km2",12}");
        foreach (var look in Enum.GetValues<BuildingLook>())
        {
            var (stood, refused, groundM2) = (laying.StoodByLook[(int)look], laying.RefusedByLook[(int)look], 0f);
            if (stood + refused == 0) continue;

            for (var building = 0; building < buildings.Count; building++)
            {
                if (buildings.Prefab[building] >= 0 && looks[buildings.Prefab[building]] == look) groundM2 += buildings.SizeM[building].X * buildings.SizeM[building].Y;
            }

            Console.WriteLine($"  {look,-12}{stood,8}{refused,9}{groundM2 / 1e6f,12:F2}");
        }

        if (outPath is null) return;

        using var rows = new StreamWriter(outPath);
        rows.WriteLine("look,size_x,size_y,prefab");
        for (var building = 0; building < buildings.Count; building++)
        {
            var (prefab, sizeM) = (buildings.Prefab[building], buildings.SizeM[building]);
            if (prefab < 0) continue;

            rows.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{looks[prefab]},{sizeM.X:F2},{sizeM.Y:F2},{prefab}"));
        }

        Console.WriteLine($"  every building stood written to {outPath}");
    }
}
