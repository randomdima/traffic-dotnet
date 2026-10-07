using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The town's walkers ticked on the device</b> (<see cref="DeviceWalkerTick"/>), asked the town's own question: every
/// tick of the suite's city with people on it, every walker laid as the town's tick reads it, and what it left held to
/// what the town's tick and its casualties' slide left.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceWalkerTickTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Ten seconds of the suite's city with people on it.</summary>
    const int Ticks = 600;

    /// <summary>
    /// <b>Every walker faces, declares and pushes as the town's tick left it</b>, once the town's casualties have slid —
    /// but a walker that boarded something or stepped off it between, which is a decision's.
    /// </summary>
    [Fact]
    public void EveryWalkerTicksAsTheTownTicksIt()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        using var walkers = new DeviceWalkerTick(device.Vk, Config, world.People.Capacity);
        using var batch = new ComputeBatch(device.Vk);

        var count = 0;
        var differ = new List<string>();
        var asked = 0;
        loop.WatchingTheAgents = (town, stage) =>
        {
            if (stage == AgentsStage.Due) count = walkers.Lay(town);
        };
        world.BeforeTheImpulses = town =>
        {
            var commands = batch.Begin();
            walkers.Record(commands, count);
            walkers.RecordRead(commands);
            batch.SubmitAndWait();
            walkers.ReadTimes();
            for (var person = 0; person < count; person++)
            {
                asked++;
                var why = walkers.Disagreement(town, person);
                if (why != null) differ.Add($"tick {loop.Tick}: {why}");
            }
        };

        for (var at = 0; at < Ticks; at++) loop.Advance();

        Assert.True(differ.Count == 0, $"{differ.Count} of {asked} walker-ticks differ: {string.Join("; ", differ.Take(6))}");
    }
}
