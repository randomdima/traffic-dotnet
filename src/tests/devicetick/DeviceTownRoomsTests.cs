using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.Tests.World;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A rebuild that passes a room is laid again, and the town drives as one whose rooms never ran short</b>
/// (<see cref="DeviceRebuild"/>'s rooms): the rows, a car's plan and a walk begun with room for almost nothing and grown as
/// rebuilds pass them, and every few rebuilds after told they have none — each pass undone before the rebuild is laid again.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P2)]
public class DeviceTownRoomsTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>Ten seconds: two hundred rebuilds, every seventh laid twice.</summary>
    const int Ticks = 600;

    /// <summary>Room for almost nothing: the first rebuild passes all three.</summary>
    static readonly DeviceRebuild.Rooms Cramped = new(Rows: 64, Pieces: 1, Proposed: 1);

    /// <summary>
    /// How often a rebuild is told it has no room once its rooms fit: not the first, whose every walker is new to the device
    /// and sent whole, so a pass is undone among walkers the device already keeps.
    /// </summary>
    const int TellsNoRoomEvery = 7;

    /// <summary>
    /// <b>Every tick, every car and walker stands, moves and faces to the bit as in the town whose rooms were never passed,
    /// and holds the same hold</b> — over a run in which rebuilds passed their rooms and were laid again.
    /// </summary>
    [Fact]
    public void ARebuildLaidAgainLeavesTheTownAsOneThatFitted()
    {
        var config = SimConfig.Shipped();
        using var roomy = new TownWorld(Towns.Built, config);
        using var cramped = new TownWorld(Towns.Built, config);
        var roomyLoop = new SimLoop<TownWorld>(roomy, config);
        var crampedLoop = new SimLoop<TownWorld>(cramped, config);
        var warm = (int)(config.Building.DwellMaxS * config.Sim.TickRateHz) + 120;
        roomyLoop.Advance(warm);
        crampedLoop.Advance(warm);
        using var roomyDevice = new DeviceTown(device.Vk, config, roomy);
        using var crampedDevice = new DeviceTown(device.Vk, config, cramped, Cramped);
        crampedDevice.Claims.Rebuild.TellsNoRoomEvery = TellsNoRoomEvery;
        roomy.Device = roomyDevice;
        cramped.Device = crampedDevice;

        var unlike = new List<string>();
        for (var tick = 0; tick < Ticks && unlike.Count == 0; tick++)
        {
            roomyLoop.Advance();
            crampedLoop.Advance();
            TownsInStep.Compare(roomy, cramped, tick, unlike);
        }

        roomy.Device = cramped.Device = null;
        Assert.True(crampedDevice.Claims.Rebuild.RebuildsLaidAgain > 1, "no rebuild but the first was laid again");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }
}
