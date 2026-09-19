using System.Diagnostics;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What opening a map costs, stage by stage</b> — the three things <c>LaidTown.Lay</c> does, and inside
/// each of them the one or two that are the seconds.
/// </summary>
/// <remarks>
/// <para>
/// <b>A cold open and not a benchmark loop.</b> Every stage here caches its answer — the generator keeps the
/// last plan it laid (<see cref="Maps.Plan"/>), the merge is kept on the paving
/// (<c>Paving.Perimeter</c>) — so the figure a reader wants is the one a process that has just started
/// pays, and running any of it twice would measure the cache. That is why there is no warm-up and no
/// fastest-of-three here, and why the reading is a process rather than a method.
/// </para>
/// <para>
/// <b>Every figure is the object's own.</b> The mesh times its own layers and its own boundary
/// (<see cref="GroundMesh.LaidMs"/>), and the town times its own graphs
/// (<see cref="TownWorld.StoodMs"/>); this probe adds the plan's stopwatch and the arithmetic between
/// them. Nothing is laid a second time to be measured, so what is printed is what an open actually spent.
/// </para>
/// </remarks>
internal static class LoadProbe
{
    public static void Run(string map, SimConfig config)
    {
        var planAt = Stopwatch.GetTimestamp();
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var planMs = Stopwatch.GetElapsedTime(planAt).TotalMilliseconds;

        var groundAt = Stopwatch.GetTimestamp();
        var ground = GroundMesh.Build(plan, config);
        var groundMs = Stopwatch.GetElapsedTime(groundAt).TotalMilliseconds;

        var world = new TownWorld(plan, config);

        Console.WriteLine($"load — {plan.Name}, seed {plan.Seed}");
        Console.WriteLine();

        var corners = 0;
        var triangles = 0;
        foreach (var part in ground.Parts)
        {
            corners += part.Corners;
            triangles += part.Triangles;
        }

        Say("plan", planMs, $"{plan.Roads.Count} roads, {plan.Junctions.Count} junctions, {plan.Props.Count} props");
        Say("ground", groundMs, $"{triangles} triangles over {corners} corners");
        Say("  merge", ground.MergeMs, "every driven band cut against every band near it");
        Say("  boundary", ground.BoundaryMs - ground.MergeMs, "that shape moved into each layer's own rings");
        Say("  layers", ground.LaidMs - ground.BoundaryMs, $"{GroundParts.Count} layers cut and welded");
        Say("world", world.StoodMs, $"{world.AgentCount} agents, {world.StaticBodyCount} static bodies");
        Say("  roads", world.RoadsMs, $"{world.Roads.LaneCount} lanes");
        Say("  foot", world.FootMs, $"{world.Foot.EdgeCount} lanes");
        Say("  walking", world.WalkingMs, $"{world.Walking.Runs.LinkCount} runs");
        Say("  the rest", world.StoodMs - world.RoadsMs - world.FootMs - world.WalkingMs,
            "fleets, the tables they are numbered in, the roster and the spawn");

        Console.WriteLine();
        Say("open", planMs + groundMs + world.StoodMs, "what a map picked on the menu costs");
    }

    static void Say(string stage, double ms, string beside) =>
        Console.WriteLine($"  {stage,-12}{ms,8:F0} ms   {beside}");
}
