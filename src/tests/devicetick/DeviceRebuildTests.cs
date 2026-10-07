using System.Numerics;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;
using static TrafficSimulation.App.DeviceTick.DeviceRebuild;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A whole claims rebuild chained on the device, asked the town's own question</b>: every rebuild of the walked town
/// recorded into one submit (<see cref="DeviceRebuild"/>) — each producer sent the town as it stands at its own stage,
/// what they lay numbered, answered and cut on the device, every grant read there — and held to what the town made of the
/// same rebuild (<see cref="RebuildsOnTheDevice"/>).
/// </summary>
/// <remarks>
/// <b>Exact but for a pace along a heading and where a walker aims</b>, which the device's own trigonometry finds: a row's
/// speed down its way where a walker's body laid it, or a car's grown over it, and a walker's destination. The numbering,
/// every hold, every answer and every grant are compared to the bit.
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceRebuildTests(RebuildsOnTheDevice run) : IClassFixture<RebuildsOnTheDevice>
{
    /// <summary>
    /// <b>Every slot is laid where the town lays it and comes out as the town's does</b>: the same count, and slot by slot
    /// the same holder, way, stretch, line metres, arrival, rung and flags — bodies grown into one another where the town
    /// grows them, every pass, light, plan and walk in the town's order, cut back or gone where the town's is.
    /// </summary>
    [Fact]
    public void EverySlotIsNumberedAndCutAsTheTownsIs() => Assert.True(run.Rows.Kept, run.Rows.Said + run.Seen);

    /// <summary>
    /// <b>Every hold is numbered as the town numbers it</b>, over the same run of slots, with the same stand-off, the same
    /// holder's terms, and answered with the plans or laid where it stands as the town's is.
    /// </summary>
    [Fact]
    public void EveryHoldIsNumberedAsTheTownsIs() => Assert.True(run.Holds.Kept, run.Holds.Said + run.Seen);

    /// <summary>
    /// <b>Every hold comes to what the town's came to</b>: the same metre of its holder's line, kept off what cut it by as
    /// much, cut on the same way by the claim in the same slot — after the plans are answered, and after the backing.
    /// </summary>
    [Fact]
    public void EveryHoldIsAnsweredAsTheTownsIs() => Assert.True(run.Answers.Kept, run.Answers.Said + run.Seen);

    /// <summary>
    /// <b>Every car's grant is read where its tick reads it as the town's tick reads it</b>: its authority, horizon,
    /// claim, commitment, margin, what cut it and on which way, the row that cut it, and whether it has a hold and was cut
    /// — for a car the laying asked, and kept from the laying before for one it did not.
    /// </summary>
    [Fact]
    public void EveryCarsGrantIsReadAsTheTownReadsIt() => Assert.True(run.Grants.Kept, run.Grants.Said + run.Seen);

    /// <summary>
    /// <b>Every walker reads the device's answer as the town reads its own</b>: granted as far, on or held off the same
    /// zebra, its pass and action as the town left them, and aimed at the same place to the device's own trigonometry.
    /// </summary>
    [Fact]
    public void EveryWalkerReadsItsAnswerAsTheTownReadsIt() => Assert.True(run.Walkers.Kept, run.Walkers.Said + run.Seen);
}

/// <summary>
/// <b>The walked town run once with every claims rebuild chained on the device beside it</b>, each held to the town's own
/// once both are done, and what differed kept a kind at a time for the class to read.
/// </summary>
/// <remarks>
/// <para>
/// <b>The walked town, once its walkers are out of their doors</b>: the one town the suite stands walkers in, cars in bays
/// and owners walking to them — so walkers' bodies are grown into the cars they are held as, cars manoeuvre and the host
/// lays their holds, and walkers plan over the zebras the cars plan through.
/// </para>
/// <para>
/// <b>With a car stood too near a wreck to step out round it</b>, which backs up for the room into the plans of whoever
/// queues behind it (CAR-50, <see cref="World.OvertakeInATownTests.StandTooNearAWreck"/>), <b>and a hand at another car's
/// wheel driving it flat out over a light's bar</b> (CTL-5d): a town left to itself backs nobody up and has no hand on a
/// wheel, and what the host lays after the answers and what it cuts of what was laid before it are only asked where
/// somebody does.
/// </para>
/// </remarks>
public sealed class RebuildsOnTheDevice : IDisposable
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>How long the town is left before the rebuilds are watched: its walkers out of their doors.</summary>
    static int WalkersOutTicks => (int)(Config.Building.DwellMaxS * Config.Sim.TickRateHz) + 120;

    /// <summary>Twenty seconds of the walked town once its walkers are out: four hundred rebuilds.</summary>
    const int WatchedTicks = 1_200;

    /// <summary>A hand holding the throttle down and the wheel straight (CTL-5d).</summary>
    static readonly HandInput FlatOut = new(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero);

    readonly Lavapipe _device = new();

    internal Held Rows { get; } = new("slots");

    internal Held Holds { get; } = new("holds");

    internal Held Answers { get; } = new("answers");

    internal Held Grants { get; } = new("cars' grants");

    internal Held Walkers { get; } = new("walkers' readings");

    /// <summary>What the run asked of, for a message to say how much it covered.</summary>
    internal string Seen { get; }

    public RebuildsOnTheDevice()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WalkersOutTicks);
        var (driver, _, _, beside) = World.OvertakeInATownTests.StandTooNearAWreck(world, loop);
        World.OvertakeInATownTests.PutDown(world, beside, World.OvertakeInATownTests.FarOffTheTownM, 0f);
        var hand = OnARedApproach(world, driver);

        using var onDevice = new DeviceRebuild(_device.Vk, Config, world);
        long grown = 0, hostRows = 0, backing = 0, cutBefore = 0, cutAfter = 0;
        world.WatchingTheClaims = (town, stage) =>
        {
            onDevice.Watch(town, stage);
            if (stage != ClaimsStage.GrantsRead) return;

            onDevice.HoldToTheTown(town, Rows, Holds, Answers, Grants, Walkers);
            grown += onDevice.Grown;
            hostRows += onDevice.HostRowsSent;
            backing += onDevice.BackingSent;
            cutBefore += onDevice.EditsSent.BeforeTheAnswers;
            cutAfter += onDevice.EditsSent.AfterThem;
        };

        for (var tick = 0; tick < WatchedTicks; tick++)
        {
            world.HandOnCar(hand, FlatOut);
            loop.Advance(1);
        }

        world.WatchingTheClaims = null;
        Seen = $" ({onDevice.Rebuilds} rebuilds of {world.Cars.Count} cars and {world.People.Count} walkers, {grown} bodies grown into "
               + $"another, {hostRows} rows the host laid, {backing} rows and holds of backing, {cutBefore} cut by the host before the "
               + $"answers and {cutAfter} after)";
    }

    /// <summary>
    /// A driven car put down on an approach its light holds, its nose at the bar, to be driven flat out over it by a hand:
    /// what it cannot stop short of is laid where it stands (TER-5e) over the light's stretch, and takes it.
    /// </summary>
    static int OnARedApproach(TownWorld world, int notThis)
    {
        var lights = world.SignalHolds.Table;
        var cars = world.Cars;
        for (var stretch = 0; stretch < lights.Way.Length; stretch++)
        {
            if (!lights.ForCars[stretch]) continue;
            if (SignalHolds.ColourOf(world.Signals, lights.Subject[stretch], true, world.ElapsedS) == SignalColour.Green) continue;

            for (var car = 0; car < cars.Count; car++)
            {
                if (car == notThis || !cars.Driven[car] || cars.Broken[car] || world.OnTheHook(car)) continue;

                var at = Spline.SampleAt(world.Roads.ArcsOf(lights.Subject[stretch]), lights.FromM[stretch] - cars.BuildOf(car).HalfLengthM);
                cars.PositionM[car] = at.PositionM;
                cars.HeadingRad[car] = at.HeadingRad;
                cars.VelocityMps[car] = Vector2.Zero;
                world.PhysicsForInstruments.Release(cars.Body[car], at.PositionM, at.HeadingRad);
                return car;
            }
        }

        Assert.Fail("the walked town had no light holding an approach, or no car to put on it");
        return -1;
    }

    public void Dispose() => _device.Dispose();
}
