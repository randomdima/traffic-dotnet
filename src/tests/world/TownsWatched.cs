using System.Collections.Concurrent;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>One minute of each town, read every tick by each class that only watches it</b> —
/// <see cref="LightsInATownTests"/> and <see cref="PlansInATownTests"/>.
/// </summary>
/// <remarks>
/// Run apart they were the same town stepped twice: a run is the same world to the bit from the same plan
/// (<see cref="ManagedWorldTests"/>), and a watch reads it between ticks and writes nothing back. Each watch keeps its
/// first finding and stops looking.
/// </remarks>
internal static class TownsWatched
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// A minute of town: every light round its cycle four times with the traffic coming up to it, and every car come up
    /// to a bend and a junction.
    /// </summary>
    const int Ticks = 3_600;

    static readonly ConcurrentDictionary<string, Lazy<(LightsInATownTests.Watch Lights, PlansInATownTests.Watch Plans)>> Runs = new();

    /// <summary>Every red bar of the town, held or not (TLT-1).</summary>
    public static LightsInATownTests.Watch Lights(string map) => Of(map).Lights;

    /// <summary>Every car's plan, drawn back or not (TER-4c.1).</summary>
    public static PlansInATownTests.Watch Plans(string map) => Of(map).Plans;

    static (LightsInATownTests.Watch Lights, PlansInATownTests.Watch Plans) Of(string map) =>
        Runs.GetOrAdd(map, named => new(() => Run(named))).Value;

    static (LightsInATownTests.Watch, PlansInATownTests.Watch) Run(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var lights = new LightsInATownTests.Watch(world, map);
        var plans = new PlansInATownTests.Watch(world, map);
        for (var tick = 0; tick < Ticks; tick++)
        {
            plans.Before();
            loop.Advance();
            lights.Saw(tick);
            plans.Saw(tick);
        }

        return (lights, plans);
    }
}
