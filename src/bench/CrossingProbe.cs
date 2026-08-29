using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The crossings map, walked</b>: five isolated streets with a crossing on each — one of them laid off
/// square to its street — and one body apiece sent over its own paint and back for as long as this
/// watches.
/// </summary>
/// <remarks>
/// <b>The staging and the claims are <see cref="ZebraWatch"/>'s</b>, which is the same watch a run of
/// <c>--map Zebras</c> keeps against the map on screen. This only stands the town, ticks it and prints
/// what the watch came to, so the probe and the panel cannot disagree about whether a crossing was walked.
/// </remarks>
internal static class CrossingProbe
{
    /// <summary>
    /// Two minutes. A crossing is a few seconds of walking and the walk to the kerb before it is a few
    /// more, so this is room for every one of the five to be gone over several times rather than once.
    /// </summary>
    public const int Ticks = 7_200;

    public static bool Run(SimConfig config)
    {
        using var world = new TownWorld(TownReader.ReadFile(ProjectPaths.TownFile(ZebraWatch.Map)), config);
        var watch = new ZebraWatch(config, world);
        var town = new TownWatch(world);
        var loop = new SimLoop<TownWorld>(world, config);

        Console.WriteLine(
            $"crossings probe — {ZebraWatch.Map}, {Ticks} ticks ({Ticks / config.Sim.TickRateHz} s) at "
            + $"{config.Sim.TickRateHz} Hz, {world.People.Count} on foot over {world.Plan.Crosswalks.Count} crossings");

        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            watch.Saw(world);
            town.Saw(world);
        }

        return ScenarioReport.Print(ZebraWatch.Map, [watch, town], world.ElapsedS);
    }
}
