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
    /// town takes up everything it made of them — or the town would write its own stale fields back over the device's — the
    /// ones that ran on while the town went on (<see cref="DeviceRebuild.AnswersDue"/>) among them.
    /// </summary>
    [Fact]
    public void TheTownHoldsEveryWalkerAsTheDeviceLeftIt()
    {
        Assert.True(ticked.WalkersKept && ticked.RebuildsRanOn);
        Assert.Equal(0L, ticked.WalkersUnlike);
    }

    /// <summary>
    /// The walker tick's table is the device's (<see cref="DeviceWalkerTick.Resident"/>), written over only where the town
    /// holds a walker other than the device is known to — a rebuild's aims among what it is known to hold, taken up after
    /// the tick was laid where it ran on — so every tick reads every walker as the town would lay it, or a walker walks at
    /// an aim the town has moved on from.
    /// </summary>
    [Fact]
    public void EveryTickReadsEveryWalkerAsTheTownHoldsIt()
    {
        Assert.True(ticked.RebuildsRanOn);
        Assert.Equal(0L, ticked.WalkerTicksUnlike);
    }

    /// <summary>
    /// A car the device ticks has its body and its plan's rows laid on the device off what it holds of the car
    /// (<c>car_claims_gather.comp</c>) — and laid as the town would have laid them, but for the way it faces, which is its
    /// body's own; or a rebuild answers a car the town does not have.
    /// </summary>
    [Fact]
    public void EveryCarTheDeviceLaysIsLaidAsTheTownWouldLayIt()
    {
        Assert.True(ticked.CarsLaidOnTheDevice);
        Assert.Equal(0L, ticked.CarsLaidUnlike);
    }

    /// <summary>
    /// The layer is sent back in part (<see cref="DeviceRebuild.LandsLater"/>): every hold and what cut it, the lists of
    /// the ways the readers asked about and the lights' holds of every signalled way — each as the whole layer holds it, or
    /// a reader asking about a way the device answered reads what it did not lay.
    /// </summary>
    [Fact]
    public void EveryLayerSentInPartHoldsWhatTheWholeDoes()
    {
        Assert.True(ticked.LayersSentInPart);
        Assert.Equal(0L, ticked.LayersUnlike);
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
        town.Walkers.ChecksWhatTheTickReads = true;
        town.Claims.Rebuild.ChecksTheCarsLaid = true;
        town.Claims.Rebuild.ChecksTheLayerSentInPart = true;
        world.Device = town;
        Watch = new TownWatch(world, config);
        for (var tick = 0; tick < Ticks; tick++)
        {
            loop.Advance();
            Watch.Saw(world);
        }

        world.Device = null;
        (WalkersKept, WalkersUnlike) = (walkers.Keeps, walkers.KeptUnlike);
        WalkerTicksUnlike = town.Walkers.TickReadUnlike;
        (CarsLaidOnTheDevice, CarsLaidUnlike) = (town.Claims.Rebuild.CarsLaidOnTheDevice > 0, town.Claims.Rebuild.CarsLaidUnlike);
        (LayersSentInPart, LayersUnlike) = (town.Claims.Rebuild.LayersSentBack > 0, town.Claims.Rebuild.LayersUnlike);
        RebuildsRanOn = town.Claims.Rebuild.RanOn > 0;
    }

    /// <summary>Whether any rebuild ran on while the town went on, its answers written back after the tick was laid.</summary>
    internal bool RebuildsRanOn { get; }

    internal TownWatch Watch { get; }

    /// <summary>Whether the device kept the walkers' side of the claims, and how many walkers the town held otherwise right after a rebuild was taken back.</summary>
    internal bool WalkersKept { get; }

    internal long WalkersUnlike { get; }

    /// <summary>How many walkers a tick read other than the town laid them, over every tick.</summary>
    internal long WalkerTicksUnlike { get; }

    /// <summary>Whether the device laid any car's body and plan itself, and how many it laid other than the town would have.</summary>
    internal bool CarsLaidOnTheDevice { get; }

    internal long CarsLaidUnlike { get; }

    /// <summary>Whether any layer was sent back in part, and how many holds and ways one held other than the whole layer.</summary>
    internal bool LayersSentInPart { get; }

    internal long LayersUnlike { get; }

    public void Dispose() => _device.Dispose();
}
