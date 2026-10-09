using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// Rule 2 asked of a town whose tick the device runs (<see cref="DeviceTown"/>): the host's half of it — what is laid and
/// sent, the decisions put off, the rebuild's own rows, what comes back and is written into the town — allocates nothing
/// once the town stands. The same rule <c>AllocationGateTests</c> asks of the town that ticks itself.
/// </summary>
/// <remarks>
/// <b>What it warms past is growth, not a leak</b>: the town's own laying is made on the first rebuild the device lays, and
/// what is kept to the size of the most yet laid — the laying, the step's contacts — grows at a new most. A leak allocates
/// every tick it runs; this is told apart by lengthening the warm-up, as the host's gate is.
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P0)]
public class DeviceAllocationGateTests : IDisposable
{
    readonly Lavapipe _device = new();

    [Fact]
    public void AWholeTickOfATownTheDeviceTicksAllocatesNothing()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Built, config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance((int)(config.Building.DwellMaxS * config.Sim.TickRateHz) + 120);
        using var town = new DeviceTown(_device.Vk, config, world);
        world.Device = town;
        loop.Advance(Towns.SettleTicks(Towns.City));

        var before = GC.GetAllocatedBytesForCurrentThread();
        loop.Advance(600);
        var after = GC.GetAllocatedBytesForCurrentThread();
        world.Device = null;

        Assert.Equal(before, after);
    }

    public void Dispose() => _device.Dispose();
}
