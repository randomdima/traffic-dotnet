using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.Tests.World;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A claims rebuild that runs on while the town goes on</b> (<see cref="DeviceTown.RebuildsRunOn"/>) leaves the town as
/// one waited for: its answers taken up after the tick was laid, into the numbers of a laying put aside and under the
/// walkers that tick wrote whole, and the cars the town ticks itself ticked after them, change nothing the town or the
/// device does.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P2)]
public class DeviceTownRunOnTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Ten seconds: a couple of hundred rebuilds, every one of them taken up after its tick where it ran on.</summary>
    const int Ticks = 600;

    /// <summary>Long enough for every car the fixture stands to have a line.</summary>
    const int WarmUpTicks = 120;

    /// <summary>
    /// <b>Every tick, every car and walker stands, moves and faces to the bit as in the town whose rebuilds were waited for,
    /// and holds the hold the same layer numbered for it</b> — over a run in which rebuilds ran on.
    /// </summary>
    [Fact]
    public void ARebuildThatRunsOnLeavesTheTownAsOneWaitedFor()
    {
        var warm = (int)(Config.Building.DwellMaxS * Config.Sim.TickRateHz) + 120;
        var (unlike, ranOn, _) = RunInStep(Towns.Built, (_, loop) => loop.Advance(warm));

        Assert.True(ranOn > 0, "no rebuild ran on");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }

    /// <summary>
    /// <b>And where the town drives a car itself</b> — one stood too near a wreck, backing up for room and then driving its
    /// pass (CAR-50, <see cref="OvertakeInATownTests.StandTooNearAWreck"/>) — whose own tick reads what the rebuild running on
    /// answered it, and is put off until that is written back.
    /// </summary>
    [Fact]
    public void ACarTheTownDrivesIsTickedAsInATownThatWaited()
    {
        var (unlike, ranOn, putOff) = RunInStep(
            Towns.Of(Towns.Fixture), (town, loop) =>
            {
                loop.Advance(WarmUpTicks);
                var (_, _, _, beside) = OvertakeInATownTests.StandTooNearAWreck(town, loop);
                OvertakeInATownTests.PutDown(town, beside, OvertakeInATownTests.FarOffTheTownM, 0f);
            });

        Assert.True(ranOn > 0 && putOff > 0, $"{ranOn} rebuilds ran on and {putOff} cars' ticks were put off");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }

    /// <summary>
    /// One town staged alike twice, then run in step on the device, its rebuilds waited for in one and running on in the
    /// other: what differed, how many rebuilds ran on and how many cars' ticks were put off for them.
    /// </summary>
    (List<string> Unlike, long RanOn, long PutOff) RunInStep(CityPlan plan, Action<TownWorld, SimLoop<TownWorld>> stage)
    {
        using var waitedFor = new TownWorld(plan, Config);
        using var ranOn = new TownWorld(plan, Config);
        var waitedLoop = new SimLoop<TownWorld>(waitedFor, Config);
        var ranOnLoop = new SimLoop<TownWorld>(ranOn, Config);
        stage(waitedFor, waitedLoop);
        stage(ranOn, ranOnLoop);
        using var waitedDevice = new DeviceTown(device.Vk, Config, waitedFor) { RebuildsRunOn = false };
        using var ranOnDevice = new DeviceTown(device.Vk, Config, ranOn);
        waitedFor.Device = waitedDevice;
        ranOn.Device = ranOnDevice;

        var unlike = new List<string>();
        for (var tick = 0; tick < Ticks && unlike.Count == 0; tick++)
        {
            waitedLoop.Advance();
            ranOnLoop.Advance();
            TownsInStep.Compare(waitedFor, ranOn, tick, unlike);
        }

        waitedFor.Device = ranOn.Device = null;
        return (unlike, ranOnDevice.Claims.Rebuild.RanOn, ranOnDevice.CarsPutOff);
    }
}
