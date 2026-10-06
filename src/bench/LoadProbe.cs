using System.Diagnostics;
using System.Runtime.InteropServices;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
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

        // <b>Each stage is printed as it finishes</b>, so a town that fails further on still says what
        // laying it cost and which plan it was.
        Console.WriteLine($"load — {plan.Name}, seed {plan.Seed}");
        Console.WriteLine();
        Say("plan", planMs,
            $"{plan.Roads.Count} roads, {plan.Junctions.Count} junctions ({plan.Junctions.Lit.Count(lit => lit)} lit), " +
            $"{plan.Crosswalks.Count} zebras of its own, {plan.Bridges.Count} bridges " +
            $"({plan.Roads.Level.Count(level => level != CityPlan.RoadArrays.Ground)} on the level above), " +
            $"{plan.Roundabouts.Count} roundabouts, " +
            $"{plan.Props.Count} props and {plan.Scenery.Count} of scenery, {plan.Buildings.Count} buildings");
        foreach (var (stage, ms) in plan.LaidMs) Say($"  {stage}", ms, string.Empty);
        Console.WriteLine($"  {"digest",-12}{Digest(plan):x16}   the plan's shapes, equal across processes and builds");
        // The paving's rings and its merge are laid on first asking, by whichever of the town and the ground asks first,
        // and asked here so that neither stage is charged with them.
        var pavingAt = Stopwatch.GetTimestamp();
        var rings = plan.Paving(config).Rings(config);
        var merged = plan.Paving(config).Perimeter(config).Loose;
        var pavingMs = Stopwatch.GetElapsedTime(pavingAt).TotalMilliseconds;
        Say("paving", pavingMs, "the walk's rings, and every driven band merged into the town's driven ground");
        Console.WriteLine(
            $"  {"open",-12}{merged.Length,8} merged, " +
            $"{rings.Carriageway.Loose.Length} carriageway, {rings.Walk.Loose.Length} walk — runs the boundary could not close");

        // Where to point --at, rather than a second probe's walk of the same runs.
        EndingAt("merged", merged);
        EndingAt("carriageway", rings.Carriageway.Loose);
        EndingAt("walk", rings.Walk.Loose);

        var world = new TownWorld(plan, config);
        Say("world", world.StoodMs, $"{world.AgentCount} agents, {world.StaticBodyCount} static bodies");
        Say("  roads", world.RoadsMs, $"{world.Roads.LaneCount} lanes");
        Say("  foot", world.FootMs, $"{world.Foot.EdgeCount} lanes");
        Say("  walking", world.WalkingMs, $"{world.Walking.Runs.LinkCount} runs");
        Say("  atlas", world.AtlasMs,
            $"{world.Atlas.PointCount} points, {world.Atlas.EntryCount} entries, {world.Atlas.Bytes / 1048576.0:F1} MiB, " +
            $"at most {world.Atlas.MostWaysAtAPoint} ways at a point of {RibbonAtlas.MostWaysUnderABody} under a body");
        Say("  the rest", world.StoodMs - world.RoadsMs - world.FootMs - world.WalkingMs - world.AtlasMs,
            "fleets, the tables they are numbered in, the roster and the spawn");

        var groundAt = Stopwatch.GetTimestamp();
        var ground = GroundMesh.Build(plan, config);
        var groundMs = Stopwatch.GetElapsedTime(groundAt).TotalMilliseconds;

        var corners = 0;
        var triangles = 0;
        foreach (var part in ground.Parts)
        {
            corners += part.Corners;
            triangles += part.Triangles;
        }

        Say("ground", groundMs, $"{triangles} triangles over {corners} corners");
        Say("  merge", ground.MergeMs, "every driven band cut against every band near it");
        Say("  boundary", ground.BoundaryMs - ground.MergeMs, "that shape moved into each layer's own rings");
        Say("  lines", ground.LinesMs, "each layer's line read off its rings, and thinned for its fill");
        Say("  ends", ground.EndsMs, "the kerb ends, the bands across them and the bars behind");
        Say("  layers", ground.LaidMs - ground.BoundaryMs - ground.LinesMs - ground.EndsMs, $"{GroundParts.Count} layers cut and welded");
        for (var part = 0; part < GroundParts.Count; part++)
        {
            Say($"    {GroundParts.Words[part]}", ground.Parts[part].LaidMs, $"{ground.Parts[part].Triangles} triangles");
        }

        Console.WriteLine();
        Say("open", planMs + pavingMs + world.StoodMs + groundMs, "what a map picked on the menu costs");
    }

    /// <summary>How many places a run the boundary could not close is named at.</summary>
    const int Listed = 8;

    static void Say(string stage, double ms, string beside) =>
        Console.WriteLine($"  {stage,-12}{ms,8:F0} ms   {beside}");

    /// <summary>Where the first few runs of one outline that would not close end, or nothing where every one closed.</summary>
    static void EndingAt(string outline, ReadOnlySpan<ArcSeg[]> loose)
    {
        if (loose.IsEmpty) return;

        var places = loose.ToArray().Take(Listed).Select(run => $"{run[^1].EndM.X:F0} {run[^1].EndM.Y:F0}");
        Console.WriteLine($"  {"",-12}{"",8}   {outline} runs ending at {string.Join("; ", places)}");
    }

    /// <summary>
    /// <b>The plan's shapes as one number that another process and another build agree on</b>, which is what
    /// says a change to how a town is laid left the town where it was. Folded byte by byte and not with
    /// <c>HashCode</c>, whose seed is drawn per process.
    /// </summary>
    static ulong Digest(CityPlan plan)
    {
        var hash = 14695981039346656037UL;
        Fold(ref hash, plan.Roads.Segments);
        Fold(ref hash, plan.Roads.FromJunction);
        Fold(ref hash, plan.Roads.ToJunction);
        Fold(ref hash, plan.Junctions.CentreM);
        Fold(ref hash, plan.Junctions.Lit);
        Fold(ref hash, plan.CarParks.Street);
        Fold(ref hash, plan.CarParks.Road);
        Fold(ref hash, plan.Buildings.CentreM);
        Fold(ref hash, plan.Buildings.HeadingRad);
        Fold(ref hash, plan.Props.CentreM);
        Fold(ref hash, plan.Scenery.CentreM);
        Fold(ref hash, plan.Spawns.PositionM);
        return hash;
    }

    static void Fold<T>(ref ulong hash, ReadOnlySpan<T> values)
        where T : unmanaged
    {
        foreach (var value in MemoryMarshal.AsBytes(values)) hash = (hash ^ value) * 1099511628211UL;
    }
}
