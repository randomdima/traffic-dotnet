using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>One run of <see cref="Towns.Built"/>, read every tick by each class that only watches it</b> —
/// <see cref="OwnCarsInATownTests"/> for ten minutes and <see cref="LegEndsInATownTests"/> for the first seven of them.
/// </summary>
/// <remarks>
/// Run apart they were the same town stepped twice, the shorter watch a prefix of the longer: a run is the same world
/// to the bit from the same plan (<see cref="ManagedWorldTests"/>), and a watch reads it between ticks and writes
/// nothing back. Each watch still stops at its own count.
/// </remarks>
internal static class BuiltTownWatched
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Lazy<(OwnCarsInATownTests.Watch People, LegEndsInATownTests.Watch Legs)> Run = new(() =>
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var people = new OwnCarsInATownTests.Watch(world);
        var legs = new LegEndsInATownTests.Watch(world);
        var ticks = Math.Max(OwnCarsInATownTests.WatchedTicks, LegEndsInATownTests.WatchedTicks);
        for (var tick = 0; tick < ticks; tick++)
        {
            loop.Advance(1);
            if (tick < OwnCarsInATownTests.WatchedTicks) people.SawEverybody();
            if (tick < LegEndsInATownTests.WatchedTicks) legs.SawEveryCar();
        }

        return (people, legs);
    });

    /// <summary>Who walked and drove where (PER-29).</summary>
    public static OwnCarsInATownTests.Watch People => Run.Value.People;

    /// <summary>Where each leg began and ended (GEN-4f, SRV-5).</summary>
    public static LegEndsInATownTests.Watch Legs => Run.Value.Legs;
}
