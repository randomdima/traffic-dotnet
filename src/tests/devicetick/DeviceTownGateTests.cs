using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A town ticked with the device in step keeps the claims every town keeps</b> (<see cref="TownWatch"/>), asked of one
/// whose agents, bodies and claims are the device's (<see cref="DeviceTown"/>) — with all it keeps there and hands back only
/// where it changed, and the claims layer it writes back a tick late — and holds the walkers whose side of the claims the
/// device keeps as the device left them.
/// </summary>
/// <remarks>
/// What is this gate's own is the town and the run; what each claim is, is the watch's, which the soak and every map's panel
/// read too.
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P2)]
public class DeviceTownGateTests(TickedOnTheDevice ticked) : IClassFixture<TickedOnTheDevice>
{
    [Fact]
    public void NoBodyIsLeftInsideAnother() => Assert.Equal(ClaimVerdict.Kept, ticked.Watch.Verdict(TownWatch.NothingInsideAnything));

    [Fact]
    public void NobodyGoesOnIntoGroundItWasRefused() => Assert.Equal(ClaimVerdict.Kept, ticked.Watch.Verdict(TownWatch.NothingIsPastItsGrant));

    /// <summary>
    /// The device keeps the walkers' side of the claims as its own (<see cref="DeviceWalkers.Keeps"/>), and every rebuild the
    /// town takes up everything it made of them — or the town would write its own stale fields back over the device's.
    /// </summary>
    [Fact]
    public void TheTownHoldsEveryWalkerAsTheDeviceLeftIt()
    {
        Assert.True(ticked.WalkersKept);
        Assert.Equal(0L, ticked.WalkersUnlike);
    }
}

/// <summary>The suite's built town, its walkers out of their doors, ticked with the device in step on lavapipe and watched every tick.</summary>
public sealed class TickedOnTheDevice : IDisposable
{
    /// <summary>Long enough for queues to form and passes to be asked, kept and made.</summary>
    const int Ticks = 600;

    readonly Lavapipe _device = new();

    public TickedOnTheDevice()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Built, config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance((int)(config.Building.DwellMaxS * config.Sim.TickRateHz) + 120);
        using var town = new DeviceTown(_device.Vk, config, world);
        var walkers = town.Claims.Rebuild.Walkers;
        walkers.ChecksWhatWasTaken = true;
        world.Device = town;
        Watch = new TownWatch(world, config);
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            Watch.Saw(world);
        }

        world.Device = null;
        (WalkersKept, WalkersUnlike) = (walkers.Keeps, walkers.KeptUnlike);
    }

    internal TownWatch Watch { get; }

    /// <summary>Whether the device kept the walkers' side of the claims, and how many walkers the town held otherwise right after a rebuild was taken back.</summary>
    internal bool WalkersKept { get; }

    internal long WalkersUnlike { get; }

    public void Dispose() => _device.Dispose();
}
