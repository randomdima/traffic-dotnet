using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The town's bodies stepped by the device in the town's place</b> (<see cref="DeviceTownBodies"/> as
/// <see cref="TownWorld.Device"/>), held tick for tick to the same town stepped by the host: where every car and walker
/// is left, which bodies freeze and wake, which pairs begin touching, and what the touches break and hurt.
/// </summary>
/// <remarks>
/// <para>
/// <b>The device takes no agent here</b>: the town ticks every one, and the device steps the bodies — so this is phase 4
/// alone, and both towns spend the same impulses. Both solve in contact order (<see cref="DeviceSolver.SolveInContactOrder"/>).
/// </para>
/// <para>
/// <b>What parts them is the device's own trigonometry</b>: a body its step turned points by this device's sine and
/// cosine, a float off the host's, and a car's controller carries that to a millimetre within seconds. So where every
/// body is, what freezes and what begins touching are asked over the first seconds (<see cref="ShovedCarPark"/>); the
/// half minute after is asked only what nothing excuses — no body left inside another, and nobody hurt whom the host's
/// town left whole.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceTownBodiesTests(Lavapipe device, ShovedCarPark park) : IClassFixture<Lavapipe>, IClassFixture<ShovedCarPark>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>A second of the suite's city, every car touring from its first tick.</summary>
    const int DrivenTicks = 60;

    /// <summary>
    /// <b>Over the first second of a touring city every car is where the host's step left it</b>, within the solver's own
    /// agreement — where it is, how it moves, turns and points — and settling onto its springs as the host's does.
    /// </summary>
    [Fact]
    public void OverTheFirstSecondEveryDrivenCarIsWhereTheHostsStepLeftIt()
    {
        using var two = new TwoTowns(device, Towns.Of(Towns.City));
        var asked = new Asked("car-ticks left");
        for (var tick = 0; tick < DrivenTicks; tick++)
        {
            two.Advance();
            for (var car = 0; car < two.Host.Cars.Count; car++) asked.Ask($"tick {tick} car {car}", TwoTowns.CarApart(two.Host, two.OnDevice, car));
        }

        Assert.True(asked.Kept, asked.Said);
    }

    /// <summary>
    /// <b>Every walker in the world is where the host's step left it</b>, within the solver's own agreement, as the car
    /// park's walkers leave their doors and walk.
    /// </summary>
    [Fact]
    public void EveryWalkerIsWhereTheHostsStepLeftIt() => Assert.True(park.Walkers.Kept, park.Walkers.Said);

    /// <summary>
    /// <b>Through the shove every car is where the host's step left it</b>, within the solver's own agreement: the shoved
    /// cars, the frozen ones they hit and woke, and the wrecks as they settle and freeze again.
    /// </summary>
    [Fact]
    public void ThroughTheShoveEveryCarIsWhereTheHostsStepLeftIt() => Assert.True(park.Cars.Kept, park.Cars.Said);

    /// <summary>
    /// <b>The same pairs begin touching as on the host, in the same order and with the same normal</b> (SOL-6), tick for
    /// tick through the shove and its crashes — the pairs the arbiter judges.
    /// </summary>
    [Fact]
    public void TheSamePairsBeginTouchingAsOnTheHost() => Assert.True(park.Touches.Kept, park.Touches.Said);

    /// <summary>
    /// <b>The table freezes and wakes what the host's does</b> (SOL-37): every body frozen or not, frozen through the step
    /// or not and in the same freezing, through the bays settling, the shove waking what it reaches and the wrecks
    /// settling again.
    /// </summary>
    [Fact]
    public void TheTableFreezesAndWakesWhatTheHostsDoes() => Assert.True(park.Freezing.Kept, park.Freezing.Said);

    /// <summary><b>The shove breaks the cars and hurts the people the host's does</b>, and no others, through its crashes.</summary>
    [Fact]
    public void TheShoveBreaksAndHurtsWhatTheHostsDoes() => Assert.True(park.Damage.Kept, park.Damage.Said);

    /// <summary>
    /// <b>Over half a minute of the shoved car park no body stays inside another</b> (PHY-1, read as the soak reads it):
    /// none deeper than the allowance for a second on end.
    /// </summary>
    [Fact]
    public void OverHalfAMinuteNoBodyStaysInsideAnother() => Assert.True(park.Stuck.Kept, park.Stuck.Said);

    /// <summary>
    /// <b>Over half a minute nothing is broken and nobody hurt whom the host's town left whole</b> — though the two have
    /// parted by millimetres by then.
    /// </summary>
    [Fact]
    public void OverHalfAMinuteNothingIsHurtThatTheHostLeftWhole() => Assert.True(park.HurtLater.Kept, park.HurtLater.Said);

    /// <summary>
    /// <b>A step of the town's bodies on the device allocates nothing</b> once the town is under way (rule 2) — what is
    /// laid, the submit, and what is handed back into the fleets and the table — with walkers walking, wrecks settled
    /// against what they hit and their contacts asked every step.
    /// </summary>
    [Fact]
    public void SteppingTheBodiesOnTheDeviceAllocatesNothing()
    {
        using var two = new TwoTowns(device, Towns.Built);
        var counted = new Counted(two.Bodies);
        two.OnDevice.Device = counted;
        for (var tick = 0; tick < ShovedCarPark.UnderWayTicks; tick++)
        {
            if (tick == ShovedCarPark.ShoveAtTicks) two.ShoveTheParkedCars();

            two.Advance();
        }

        counted.AllocatedBytes = 0;
        for (var tick = 0; tick < DrivenTicks; tick++) two.Advance();

        Assert.True(two.OnDevice.PhysicsForInstruments.Contacts.Count > 0, "the wrecks should still be touching what they hit");
        Assert.Equal(0L, counted.AllocatedBytes);
    }

    /// <summary>The device's phase, with what each of its steps allocated on this thread counted.</summary>
    sealed class Counted(DeviceTownBodies bodies) : ITownDevice
    {
        public long AllocatedBytes;

        public bool Takes(int agent) => false;

        public void TickTheRest(TownWorld town)
        {
        }

        public bool StepsWithTheTick => false;

        public bool DecidesATickBehind => false;

        public void StepBodies(TownWorld town, float dtS)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            bodies.StepBodies(town, dtS);
            AllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }
}

/// <summary>What was asked over a run, and what was not as asked.</summary>
internal sealed class Asked(string what)
{
    readonly List<string> _differ = [];

    public int Count { get; private set; }

    public bool Kept => _differ.Count == 0;

    /// <summary>What else the run saw, said beside what differed.</summary>
    public string Also { get; set; } = string.Empty;

    public string Said => $"{_differ.Count} of {Count} {what} differently{Also}: {string.Join("; ", _differ.Take(6))}";

    /// <summary>One case asked: <paramref name="why"/> is what differed, or null where nothing did.</summary>
    public void Ask(string which, string? why)
    {
        Count++;
        if (why != null) _differ.Add($"{which}: {why}");
    }
}

/// <summary>
/// <b>The car park shoved</b>: the suite's built town, stepped by the host and — from the tick its cars have frozen in
/// their bays, so the device takes over what the host froze — by the device; then every other car shoved at the car
/// beside it, so cars crash into frozen cars, wake them and what they lean on, break, throw their drivers clear and
/// settle again; and run on for half a minute, every tick held to the host's.
/// </summary>
public sealed class ShovedCarPark : IDisposable
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// When the device is handed the town's bodies: the tick every car has stood its rest time in its bay, so it takes
    /// over a town of frozen cars the host froze.
    /// </summary>
    static int HandedOverAtTicks => Config.SolverRestTicks;

    /// <summary>When the shove comes: once every car has stood its rest time in its bay, and then as long again.</summary>
    internal static int ShoveAtTicks => 2 * Config.SolverRestTicks;

    /// <summary>When the car park is under way: the shove's crashes over, its wrecks settled, and walkers out of their doors.</summary>
    internal static int UnderWayTicks => ShoveAtTicks + AgreedTicks;

    /// <summary>Three seconds from the shove: the crashes, the wakes and the wrecks settling — what is held to the host's tick for tick.</summary>
    const int AgreedTicks = 180;

    /// <summary>Half a minute of the car park, asked only for what nothing excuses.</summary>
    const int LongRunTicks = 1_800;

    /// <summary>How long the walkers are held to the host's: long enough for most to have left their doors.</summary>
    const int WalkersTicks = 600;

    readonly Lavapipe _device = new();

    internal Asked Walkers { get; } = new("walker-ticks left");

    internal Asked Cars { get; } = new("car-ticks left");

    internal Asked Touches { get; } = new("steps began touching");

    internal Asked Freezing { get; } = new("body-ticks frozen");

    internal Asked Damage { get; } = new("bodies broken or hurt");

    internal Asked Stuck { get; } = new("bodies stood inside another");

    internal Asked HurtLater { get; } = new("bodies broken or hurt");

    public ShovedCarPark()
    {
        using var two = new TwoTowns(_device, Towns.Built, handedAtOnce: false);
        var cars = two.Host.Cars;
        var people = two.Host.People;
        var deepFor = new int[cars.Capacity + people.Capacity];
        var brokenOnDevice = new bool[cars.Capacity];
        var brokenOnHost = new bool[cars.Capacity];
        var hurtOnDevice = new bool[people.Capacity];
        var hurtOnHost = new bool[people.Capacity];
        var agreedUntil = ShoveAtTicks + AgreedTicks;
        var began = 0;
        for (var tick = 0; tick < LongRunTicks; tick++)
        {
            if (tick == HandedOverAtTicks) two.HandOver();
            if (tick == ShoveAtTicks) two.ShoveTheParkedCars();

            two.Advance();
            if (tick < WalkersTicks) AskTheWalkers(two, tick);
            if (tick < agreedUntil)
            {
                for (var car = 0; car < cars.Count; car++) Cars.Ask($"tick {tick} car {car}", TwoTowns.CarApart(two.Host, two.OnDevice, car));
                Touches.Ask($"tick {tick}", TwoTowns.TouchesApart(two.Host, two.OnDevice));
                foreach (var _ in two.Host.PhysicsForInstruments.BeganTouchingThisStep()) began++;
                AskTheFreezing(two, tick);
            }

            if (tick == agreedUntil - 1) AskTheDamage(two);

            AskTheOverlap(two.OnDevice, deepFor, tick);
            for (var car = 0; car < cars.Count; car++)
            {
                brokenOnDevice[car] |= two.OnDevice.Cars.Broken[car];
                brokenOnHost[car] |= cars.Broken[car];
            }

            for (var person = 0; person < people.Count; person++)
            {
                hurtOnDevice[person] |= two.OnDevice.People.Wounded[person];
                hurtOnHost[person] |= people.Wounded[person];
            }
        }

        Touches.Also = $", of {began} pairs that began on the host";
        for (var car = 0; car < cars.Count; car++)
        {
            HurtLater.Ask($"car {car}", brokenOnDevice[car] && !brokenOnHost[car] ? "broken on the device only" : null);
        }

        for (var person = 0; person < people.Count; person++)
        {
            HurtLater.Ask($"walker {person}", hurtOnDevice[person] && !hurtOnHost[person] ? "hurt on the device only" : null);
        }
    }

    void AskTheWalkers(TwoTowns two, int tick)
    {
        var host = two.Host.People;
        var onDevice = two.OnDevice.People;
        for (var person = 0; person < host.Count; person++)
        {
            if (host.Inside[person].Any) continue;

            var same = Vector2.Distance(host.PositionM[person], onDevice.PositionM[person]) <= DeviceBodyPhase.AgreesWithinM
                       && Vector2.Distance(host.VelocityMps[person], onDevice.VelocityMps[person]) <= DeviceBodyPhase.AgreesWithinMps;
            Walkers.Ask(
                $"tick {tick} walker {person}",
                same ? null : $"host at {host.PositionM[person]} moving {host.VelocityMps[person]}, device at {onDevice.PositionM[person]} moving {onDevice.VelocityMps[person]}");
        }
    }

    void AskTheFreezing(TwoTowns two, int tick)
    {
        for (var car = 0; car < two.Host.Cars.Count; car++)
        {
            Freezing.Ask($"tick {tick} car {car}", TwoTowns.FreezingApart(two.Host, two.Host.Cars.Body[car], two.OnDevice, two.OnDevice.Cars.Body[car]));
        }

        for (var person = 0; person < two.Host.People.Count; person++)
        {
            Freezing.Ask(
                $"tick {tick} walker {person}",
                TwoTowns.FreezingApart(two.Host, two.Host.People.Body[person], two.OnDevice, two.OnDevice.People.Body[person]));
        }
    }

    void AskTheDamage(TwoTowns two)
    {
        for (var car = 0; car < two.Host.Cars.Count; car++)
        {
            var host = two.Host.Cars.Broken[car];
            var onDevice = two.OnDevice.Cars.Broken[car];
            Damage.Ask($"car {car}", host == onDevice ? null : $"broken on the host {host}, on the device {onDevice}");
        }

        for (var person = 0; person < two.Host.People.Count; person++)
        {
            var host = two.Host.People.Wounded[person];
            var onDevice = two.OnDevice.People.Wounded[person];
            Damage.Ask($"walker {person}", host == onDevice ? null : $"hurt on the host {host}, on the device {onDevice}");
        }
    }

    /// <summary>Every body in the world deeper into another than the allowance for a second on end, counted the tick it reaches the second.</summary>
    void AskTheOverlap(TownWorld town, int[] deepFor, int tick)
    {
        var physics = town.PhysicsForInstruments;
        for (var car = 0; car < town.Cars.Count; car++) AskTheOverlap(physics, town.Cars.Body[car], ref deepFor[car], $"tick {tick} car {car}");

        for (var person = 0; person < town.People.Count; person++)
        {
            if (town.People.Inside[person].Any) continue;

            AskTheOverlap(physics, town.People.Body[person], ref deepFor[town.Cars.Capacity + person], $"tick {tick} walker {person}");
        }
    }

    void AskTheOverlap(PhysicsWorld physics, BodyId body, ref int deepFor, string which)
    {
        var deepM = physics.OverlapOf(body);
        deepFor = deepM > SoakProbe.OverlapAllowanceM ? deepFor + 1 : 0;
        if (deepFor > 0 && deepFor != SoakProbe.StuckAfterTicks) return;

        Stuck.Ask(which, deepFor == SoakProbe.StuckAfterTicks ? $"{deepM * 1000f:F1} mm deep for a second" : null);
    }

    public void Dispose() => _device.Dispose();
}

/// <summary>One town stepped by the host and the same town stepped by the device in its place, a tick at a time.</summary>
internal sealed class TwoTowns : IDisposable
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>How fast a shoved car is sent at the car beside it: a car park bump hard enough to break something.</summary>
    const float ShoveMps = 6f;

    /// <summary>How far the car beside may stand for a car to be shoved at it: the next bay, and not across the aisle.</summary>
    const float BesideWithinM = 5f;

    readonly SimLoop<TownWorld> _hostLoop;
    readonly SimLoop<TownWorld> _deviceLoop;
    readonly DeviceTyres _tyres;
    readonly DeviceTownBodies _bodies;

    /// <param name="handedAtOnce">Whether the device steps its town from the first tick, or only once <see cref="HandOver"/> is called.</param>
    public TwoTowns(Lavapipe device, CityPlan plan, bool handedAtOnce = true)
    {
        Host = new TownWorld(plan, Config);
        OnDevice = new TownWorld(plan, Config);
        _hostLoop = new SimLoop<TownWorld>(Host, Config);
        _deviceLoop = new SimLoop<TownWorld>(OnDevice, Config);

        var cars = OnDevice.Cars;
        var builds = Enumerable.Range(0, CarCatalog.Shared.SheetCount).Select(variant => cars.BuildOfVariant(variant)).ToArray();
        _tyres = new DeviceTyres(device.Vk, Config, OnDevice.WheelGrounds, builds, cars.Capacity);
        _bodies = new DeviceTownBodies(device.Vk, Config, OnDevice, _tyres);
        _bodies.Solver.SolveInContactOrder = true;
        if (handedAtOnce) HandOver();
    }

    /// <summary>The device's town's bodies stepped by the device from the next tick on, as it stands then.</summary>
    public void HandOver() => OnDevice.Device = _bodies;

    public TownWorld Host { get; }

    public TownWorld OnDevice { get; }

    public DeviceTownBodies Bodies => _bodies;

    public void Advance()
    {
        _hostLoop.Advance();
        _deviceLoop.Advance();
    }

    /// <summary>
    /// Every other car sent at the nearest car standing in the next bay, as the host's town has them — the same push to
    /// the same body in both towns, spent on its body between two ticks.
    /// </summary>
    public void ShoveTheParkedCars()
    {
        var cars = Host.Cars;
        for (var car = 0; car < cars.Count; car += 2)
        {
            var beside = NearestCar(cars.PositionM[car], car);
            if (beside < 0) continue;

            var impulseNs = Vector2.Normalize(cars.PositionM[beside] - cars.PositionM[car]) * (cars.MassKg[car] * ShoveMps);
            Host.PhysicsForInstruments.ApplyCentralImpulse(Host.Cars.Body[car], impulseNs);
            OnDevice.PhysicsForInstruments.ApplyCentralImpulse(OnDevice.Cars.Body[car], impulseNs);
        }
    }

    int NearestCar(Vector2 fromM, int besides)
    {
        var nearest = -1;
        var nearestM = BesideWithinM;
        for (var car = 0; car < Host.Cars.Count; car++)
        {
            var apartM = Vector2.Distance(Host.Cars.PositionM[car], fromM);
            if (car == besides || apartM >= nearestM) continue;

            nearest = car;
            nearestM = apartM;
        }

        return nearest;
    }

    /// <summary>
    /// Where the device's town left a car that the host's did not, or null where they agree: within the solver's own
    /// agreement where it is, how fast it moves and turns and which way it heads, the way it points within the device's
    /// sine and cosine, and its acceleration within the trigonometry every kernel test reads it to.
    /// </summary>
    /// <remarks>
    /// <b>Not whether it stands at exactly no motion</b>: a body decaying to rest passes through subnormal speeds, which a
    /// kernel flushes to nought a few ticks before the host's arithmetic reaches it.
    /// </remarks>
    public static string? CarApart(TownWorld host, TownWorld onDevice, int car)
    {
        var a = host.Cars;
        var b = onDevice.Cars;
        var forwardA = host.PhysicsForInstruments.RotationOf(a.Body[car]);
        var forwardB = onDevice.PhysicsForInstruments.RotationOf(b.Body[car]);
        var same = Vector2.Distance(a.PositionM[car], b.PositionM[car]) <= DeviceBodyPhase.AgreesWithinM
                   && Vector2.Distance(a.VelocityMps[car], b.VelocityMps[car]) <= DeviceBodyPhase.AgreesWithinMps
                   && MathF.Abs(a.YawRateRadPerS[car] - b.YawRateRadPerS[car]) <= DeviceBodyPhase.AgreesWithinMps
                   && MathF.Abs(Spline.WrapRad(a.HeadingRad[car] - b.HeadingRad[car])) <= DeviceBodyPhase.AgreesWithinRad
                   && MathF.Abs(forwardA.X - forwardB.X) <= DeviceBodyPhase.PointsWithin
                   && MathF.Abs(forwardA.Y - forwardB.Y) <= DeviceBodyPhase.PointsWithin
                   && Near(a.AccelerationMps2[car].X, b.AccelerationMps2[car].X)
                   && Near(a.AccelerationMps2[car].Y, b.AccelerationMps2[car].Y);
        return same
            ? null
            : $"host at {a.PositionM[car]} moving {a.VelocityMps[car]} heading {a.HeadingRad[car]} turning {a.YawRateRadPerS[car]} "
              + $"accelerating {a.AccelerationMps2[car]}; device at {b.PositionM[car]} moving {b.VelocityMps[car]} heading "
              + $"{b.HeadingRad[car]} turning {b.YawRateRadPerS[car]} accelerating {b.AccelerationMps2[car]}";
    }

    /// <summary>
    /// What began touching in the device's town's last step that did not in the host's, or null where the same pairs
    /// began in the same order, each normal within the device's sine and cosine.
    /// </summary>
    public static string? TouchesApart(TownWorld host, TownWorld onDevice)
    {
        var began = new List<Touch>();
        foreach (var touch in host.PhysicsForInstruments.BeganTouchingThisStep()) began.Add(touch);

        var at = 0;
        var same = true;
        foreach (var touch in onDevice.PhysicsForInstruments.BeganTouchingThisStep())
        {
            same &= at < began.Count && began[at].First == touch.First && began[at].Second == touch.Second
                    && Vector2.Distance(began[at].Normal, touch.Normal) <= DeviceBodyPhase.PointsWithin;
            at++;
        }

        if (same && at == began.Count) return null;

        var onTheDevice = new List<Touch>();
        foreach (var touch in onDevice.PhysicsForInstruments.BeganTouchingThisStep()) onTheDevice.Add(touch);
        return $"host [{string.Join(", ", began)}], device [{string.Join(", ", onTheDevice)}]";
    }

    /// <summary>Whether a body is frozen, frozen through the last step and in which freezing, on the host and on the device alike — or how not.</summary>
    public static string? FreezingApart(TownWorld host, BodyId hostBody, TownWorld onDevice, BodyId deviceBody)
    {
        var a = host.PhysicsForInstruments;
        var b = onDevice.PhysicsForInstruments;
        var (frozenA, throughA, atA) = (a.IsFrozen(hostBody), a.FrozenThroughLastStep(hostBody), a.FrozeAtStep(hostBody));
        var (frozenB, throughB, atB) = (b.IsFrozen(deviceBody), b.FrozenThroughLastStep(deviceBody), b.FrozeAtStep(deviceBody));
        return frozenA == frozenB && throughA == throughB && (!frozenA || atA == atB)
            ? null
            : $"host frozen {frozenA} through the step {throughA} at {atA}, device frozen {frozenB} through the step {throughB} at {atB}";
    }

    /// <summary>Within a part in a thousand and a hundredth absolute — the device's trigonometry, as every kernel test reads it.</summary>
    static bool Near(float host, float onDevice) => host == onDevice || MathF.Abs(host - onDevice) <= 1e-2f + (1e-3f * MathF.Abs(host));

    public void Dispose()
    {
        _bodies.Dispose();
        _tyres.Dispose();
        Host.Dispose();
        OnDevice.Dispose();
    }
}
