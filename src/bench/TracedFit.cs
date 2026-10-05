using System.Numerics;
using System.Globalization;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>How well a traced town's footprints are worn as prefabs on its walk</b> (GEN-57, <c>--bench fit</c>): how many
/// rectangles its footprints were cut into, how many were too far from any walk to be moved onto one, and a row a
/// look — the sections offered, how many stood, how far they were moved, and how far the prefab each wears stands off
/// its section in size. It gates nothing: how well a catalogue fits a city is a fact about the two.
/// </summary>
/// <remarks>
/// <b>With <c>--out FILE</c> it writes every section offered</b>, one line each — its look, its row back from the
/// walk, its sides and roundness, how far it was moved, and the prefab it wears or −1 — which is what a catalogue's
/// sizes are read off: what the city asks for, whether or not anything was found to stand there.
/// </remarks>
internal static class TracedFit
{
    /// <param name="atM">A place to say, footprint by footprint within twice a move's reach of it, what became of each section.</param>
    public static void Run(string map, SimConfig config, string? outPath, Vector2? atM = null)
    {
        // The survey's footprints and not the plan's: a town carries the buildings fitted off them and not the footprints.
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var footprints = Survey.Of(Maps.Traced(map), config).Footprints;
        var fitting = TracedBuildings.Fit(
            footprints, plan.Paving(config), new GroundShapes(plan.Paving(config), config), BuildingCatalog.Roofs, config);
        var buildings = fitting.Buildings;
        var offered = fitting.Offered;
        if (atM is { } placeM) Around(footprints, fitting, placeM, config.CityGen.TracedFrontageReachM * 2f);

        Console.WriteLine(
            $"{map}: {fitting.Footprints} footprints cut into {fitting.Parts} rectangles, {fitting.TooFar} of them further " +
            $"than {config.CityGen.TracedFrontageReachM:F0} m from any walk; {offered.Length} sections offered, {buildings.Count} stood");
        Console.WriteLine(
            "  not stood: " + string.Join(", ", Enum.GetValues<TracedBuildings.Fate>().Where(fate => fate != TracedBuildings.Fate.Stood)
                .Select(fate => $"{offered.Count(section => section.Fate == fate)} {fate}")));
        Console.WriteLine($"  {"look",-12}{"offered",9}{"stood",8}{"moved p50",11}{"moved p90",11}{"off p50",9}{"off p90",9}{"round",7}");
        foreach (var look in Enum.GetValues<BuildingLook>())
        {
            var (count, stood, round, moved, off) = (0, 0, 0, new List<float>(), new List<float>());
            foreach (var section in offered)
            {
                if (section.Look != look) continue;

                count++;
                if (section.Building < 0) continue;

                stood++;
                moved.Add(section.MovedM);
                var prefabM = buildings.SizeM[section.Building];
                off.Add(MathF.Abs(MathF.Log(prefabM.X / section.SizeM.X)) + MathF.Abs(MathF.Log(prefabM.Y / section.SizeM.Y)));
                if (buildings.Prefab.Length > 0 && BuildingCatalog.Shared.Variants[BuildingCatalog.Shared.FirstPrefab + buildings.Prefab[section.Building]].CornerRadiusM * 2f >= MathF.Min(prefabM.X, prefabM.Y) * 0.5f) round++;
            }

            if (count == 0) continue;
            if (stood == 0)
            {
                Console.WriteLine($"  {look,-12}{count,9}{0,8}");
                continue;
            }

            moved.Sort();
            off.Sort();
            Console.WriteLine(
                $"  {look,-12}{count,9}{stood,8}{moved[moved.Count / 2],11:F1}{moved[moved.Count * 9 / 10],11:F1}" +
                $"{off[off.Count / 2],9:F2}{off[off.Count * 9 / 10],9:F2}{round,7}");
        }

        if (outPath is null) return;

        using var rows = new StreamWriter(outPath);
        rows.WriteLine("look,row,part_x,part_y,corner_share,moved_m,fate,prefab,prefab_x,prefab_y");
        foreach (var section in offered)
        {
            var prefab = section.Building >= 0 && buildings.Prefab.Length > 0 ? buildings.Prefab[section.Building] : -1;
            var prefabM = section.Building >= 0 ? buildings.SizeM[section.Building] : default;
            rows.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{section.Look},{section.Row},{section.SizeM.X:F2},{section.SizeM.Y:F2},{section.CornerShare:F2},{section.MovedM:F2},{section.Fate},{prefab},{prefabM.X:F2},{prefabM.Y:F2}"));
        }

        Console.WriteLine($"  every section offered written to {outPath}");
    }

    /// <summary>Every footprint whose outline starts within reach of a place: where it stands, how big, and each of its sections' fate.</summary>
    static void Around(CityPlan.FootprintArrays footprints, TracedBuildings.Fitting fitting, Vector2 atM, float reachM)
    {
        for (var footprint = 0; footprint < footprints.Count; footprint++)
        {
            var outline = footprints.Rings.RingOf(footprints.RingOffsets[footprint]);
            var middleM = Vector2.Zero;
            foreach (var pointM in outline) middleM += pointM / outline.Length;
            if (Vector2.Distance(middleM, atM) > reachM) continue;

            var said = fitting.Offered.Where(section => section.Footprint == footprint)
                .Select(section => $"{section.Look} r{section.Row} {section.SizeM.X:F0}x{section.SizeM.Y:F0} moved {section.MovedM:F1} {section.Fate}");
            Console.WriteLine($"  footprint {footprint} at {middleM.X:F0},{middleM.Y:F0} ({footprints.Use[footprint]}): {string.Join("; ", said.DefaultIfEmpty("too far"))}");
        }
    }
}
