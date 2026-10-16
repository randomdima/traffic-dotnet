using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Tests.Gates;

/// <summary>
/// <b>Rule 2 asked of a town stepped on the device</b> (<see cref="DeviceWorld"/>), a window's world by default: its
/// steady state allocates nothing. <see cref="AllocationGateTests"/> steps the managed world.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P0)]
public class DeviceAllocationGateTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>A whole tick of the town with its world on the device, warmed, allocates nothing.</summary>
    /// <remarks>
    /// <b>Warmed as the managed gate is</b> (<see cref="Towns.SettleTicks"/>): 600 ticks left one allocation of about a
    /// hundred bytes some seconds on, and none in the 2 400 after it — a first time, not a leak.
    /// </remarks>
    [Fact]
    public void AWholeTickOnTheDeviceAllocatesNothing()
    {
        using var world = DeviceTowns.OnTheDevice(device, Towns.Built);
        var loop = new SimLoop<TownWorld>(world, DeviceTowns.Config);
        loop.Advance(DeviceTowns.SettleTicks);

        var before = GC.GetAllocatedBytesForCurrentThread() + world.AllocatedAsideBytes;
        loop.Advance(DeviceTowns.MeasuredTicks);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread() + world.AllocatedAsideBytes);
    }
}

/// <summary>
/// <b>Rule 1 asked of a town stepped on the device</b>: a tick's crossings of the managed→native wall do not grow with
/// the town. <see cref="CrossingGateTests"/> steps the managed world, which crosses nothing; a device tick crosses to
/// record, submit and wait, so what is held here is that the count is the same for a larger town.
/// </summary>
/// <remarks>The counter is <c>[Conditional("DEBUG")]</c>, so this is the device tier's Debug take.</remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P0)]
public class DeviceCrossingGateTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>
    /// The most crossings any one tick on the device takes is the same for a town and for that town with four times its
    /// people, each with a car of their own (PER-29) — what a tick records and submits is the step's shape, never one
    /// call per body.
    /// </summary>
    /// <remarks>
    /// <b>One town at two sizes, and not two towns</b>: a town with no walkers skips the walkers' passes, which is a
    /// different shape of step and not a smaller one. <b>Warmed first</b>, since a list grown mid-window is laid and
    /// bound again on that tick.
    /// </remarks>
    [Fact]
    public void ATickOnTheDeviceCrossesTheWallAsOftenWhateverTheTownsSize()
    {
        var (small, smallCars) = MostCrossingsATick(Towns.Built);
        var (large, largeCars) = MostCrossingsATick(Towns.LayFresh(Towns.Brief(
            Towns.CitySeed, buildings: Towns.BuildingsBuilt, people: Grown * Towns.PeopleBuilt)));

        // The staging and not the claim: a brief that laid no more cars would ask nothing.
        Assert.True(largeCars > smallCars, $"the larger town stands {largeCars} cars against {smallCars}");
        Assert.Equal(small, large);
    }

    /// <summary>How many times the larger town's people are the built town's.</summary>
    const int Grown = 4;

    (long Most, int Cars) MostCrossingsATick(CityPlan plan)
    {
        using var world = DeviceTowns.OnTheDevice(device, plan);
        var loop = new SimLoop<TownWorld>(world, DeviceTowns.Config);
        loop.Advance(DeviceTowns.SettleTicks);

        var most = 0L;
        for (var tick = 0; tick < DeviceTowns.MeasuredTicks; tick++)
        {
            var before = Vk.Crossings;
            loop.Advance();
            most = Math.Max(most, Vk.Crossings - before);
        }

        return (most, world.Cars.Count);
    }
}

/// <summary>A town handed to the device, warmed and then measured, as both device gates take it.</summary>
static file class DeviceTowns
{
    public static readonly SimConfig Config = SimConfig.Shipped();

    public static readonly int SettleTicks = Towns.SettleTicks(Towns.City);

    public const int MeasuredTicks = 600;

    public static TownWorld OnTheDevice(Lavapipe device, CityPlan plan)
    {
        var world = new TownWorld(plan, Config);
        world.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        return world;
    }
}
