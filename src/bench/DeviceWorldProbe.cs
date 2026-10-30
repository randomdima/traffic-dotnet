using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>One town soaked with its world on the device, beside the same town with its world in managed code</b>
/// (<see cref="DeviceWorld"/>, <see cref="SoakProbe.Sample"/>): what each did, how long each took, and whether the device's
/// kept every claim the map makes. The two need not agree to the body — the device's arithmetic is its own — but a town
/// whose world is on the device keeps what the town keeps.
/// </summary>
internal static class DeviceWorldProbe
{
    public static bool Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        Console.WriteLine(
            $"device world probe — {map} on {vk.DeviceName}, {SoakProbe.WarmupTicks} warm-up ticks, " +
            $"{SoakProbe.MeasuredTicks} measured");
        Laid(map, config, vk);
        Console.WriteLine($"{"world",-9}{"walkers",9}{"cars",6}{"down",6}{"wrecked",9}{"km driven",12}{"touches",9}{"s",8}");

        var managedAt = Stopwatch.GetTimestamp();
        var managed = SoakProbe.Sample(map, config);
        var managedS = Stopwatch.GetElapsedTime(managedAt).TotalSeconds;
        Print("managed", managed, managedS);

        var deviceAt = Stopwatch.GetTimestamp();
        var device = SoakProbe.Sample(map, config, layTheWorld: laying => new DeviceWorld(vk, laying));
        var deviceS = Stopwatch.GetElapsedTime(deviceAt).TotalSeconds;
        Print("device", device, deviceS);
        Apart(map, config, vk);

        return ScenarioReport.Print(map, [device], SoakProbe.MeasuredTicks / config.Sim.TickRateHz);
    }

    /// <summary>
    /// <b>The town laid on the device alone</b>, first, so a town too big to soak beside its managed world still says what it
    /// costs: the buffers its laying holds, and — past the warm-up — what a tick records and how long one takes.
    /// </summary>
    static void Laid(string map, SimConfig config, Vk vk)
    {
        var plan = Maps.Plan(map, config, ShippedArt.Sizes);
        using var device = new TownWorld(plan, config);
        var (deviceBefore, mappedBefore) = vk.BufferBytes;
        device.HandTheWorldTo(laying => new DeviceWorld(vk, laying));
        var (deviceAfter, mappedAfter) = vk.BufferBytes;
        Console.WriteLine(
            $"laid on the device: {(deviceAfter - deviceBefore) / 1e6:F1} MB device-local, " +
            $"{(mappedAfter - mappedBefore) / 1e6:F1} MB mapped, of which the solver's step " +
            $"{((DeviceWorld)device.World).SolverBytes / 1e6:F1} MB");
        var loop = new SimLoop<TownWorld>(device, config);
        loop.Advance(SoakProbe.WarmupTicks);
        Recorded(loop);
    }

    /// <summary>
    /// The two worlds stepped side by side off one plan, and how far apart their cars and walkers stand at a few ticks
    /// along — nought for as long as the device's arithmetic is the host's, and growing once it is not.
    /// </summary>
    static void Apart(string map, SimConfig config, Vk vk)
    {
        var plan = Maps.Plan(map, config, ShippedArt.Sizes);
        using var managed = new TownWorld(plan, config);
        using var device = new TownWorld(plan, config);
        device.HandTheWorldTo(laying => new DeviceWorld(vk, laying));
        var managedLoop = new SimLoop<TownWorld>(managed, config);
        var deviceLoop = new SimLoop<TownWorld>(device, config);
        Console.Write("apart, the furthest car and walker (m) at tick");
        foreach (var tick in (ReadOnlySpan<int>)[1, 2, 10, 60, 600])
        {
            while (managedLoop.Tick < tick)
            {
                managedLoop.Advance();
                deviceLoop.Advance();
            }

            var carM = 0f;
            for (var car = 0; car < managed.Cars.Count; car++)
            {
                carM = MathF.Max(carM, Vector2.Distance(managed.Cars.PositionM[car], device.Cars.PositionM[car]));
            }

            var walkerM = 0f;
            for (var person = 0; person < managed.People.Count; person++)
            {
                walkerM = MathF.Max(walkerM, Vector2.Distance(managed.People.PositionM[person], device.People.PositionM[person]));
            }

            Console.Write($"  {tick}: {carM:G3} / {walkerM:G3}");
        }

        Console.WriteLine();

        // The step ahead shown, so the fleets the host fills from stand where the device draws (SimFigures.WorldStepsAhead).
        var looks = TownSprites.Load(config);
        looks.ReadAspects(SheetAtlas.Pack(looks.Sheets));
        Drawn(device, deviceLoop, config, plan, looks);
        DrawnTurning(device, deviceLoop, config, plan, looks);
    }

    /// <summary>How long <see cref="DrawnTurning"/> runs the town on for a locomotive with fans to come: a few trains' gaps.</summary>
    const int TurningWaitTicks = 60 * 60 * 10;

    /// <summary>
    /// What a device tick records, on average over a stretch of ticks: dispatches, barriers and submits — the figure the
    /// step's latency follows, read whatever else shares the device (counted in DEBUG only) — and the wall clock it took.
    /// </summary>
    static void Recorded(SimLoop<TownWorld> loop)
    {
        const int ticks = 120;
        var (dispatches, barriers, submits) = (Vk.Dispatches, Vk.Barriers, ComputeBatch.Begun);
        var at = Stopwatch.GetTimestamp();
        for (var tick = 0; tick < ticks; tick++) loop.Advance();
        var tickMs = Stopwatch.GetElapsedTime(at).TotalMilliseconds / ticks;
        Console.WriteLine(
            $"recorded a tick: {(Vk.Dispatches - dispatches) / (float)ticks:F1} dispatches, " +
            $"{(Vk.Barriers - barriers) / (float)ticks:F1} barriers, {(ComputeBatch.Begun - submits) / (float)ticks:F2} submits; " +
            $"{tickMs:F2} ms a tick");
    }

    /// <summary>
    /// What moves in the device's town drawn off the device's own state (<see cref="DeviceSprites"/>) beside the host's
    /// fill of the same town and view, which reads the fleets the view was read into: how many instances each drew and how
    /// many differ, at a street and at the whole town.
    /// </summary>
    static void Drawn(TownWorld town, SimLoop<TownWorld> loop, SimConfig config, CityPlan plan, TownSprites looks)
    {
        var uiPx = new Vector2(1600f, 900f);
        var townPxPerM = MathF.Min(uiPx.X / plan.WorldSizeM.X, uiPx.Y / plan.WorldSizeM.Y);

        Console.Write("drawn, instances host / device / differing:");
        Compare(town, loop, config, plan, looks, "street", Busiest, 1600f / 45f);
        Compare(town, loop, config, plan, looks, "town", _ => plan.WorldSizeM * 0.5f, townPxPerM);
        Console.WriteLine();
    }

    /// <summary>Where the fastest car is, which the street is framed on.</summary>
    static Vector2 Busiest(TownWorld town)
    {
        var busiest = 0;
        for (var car = 1; car < town.Cars.Count; car++)
        {
            if (town.Cars.VelocityMps[car].LengthSquared() > town.Cars.VelocityMps[busiest].LengthSquared()) busiest = car;
        }

        return town.Cars.PositionM[busiest];
    }

    /// <summary>
    /// And a locomotive whose fans turn (TRN-13), framed as the street is, once the town has run on until one is out
    /// (<see cref="TurningWaitTicks"/> at the most): the first trains come long after the moment <see cref="Drawn"/> is
    /// taken at, so neither of its framings draws a wagon.
    /// </summary>
    static void DrawnTurning(TownWorld town, SimLoop<TownWorld> loop, SimConfig config, CityPlan plan, TownSprites looks)
    {
        var wagons = town.World.View.Wagons;
        var turning = -1;
        for (var tick = 0; turning < 0 && tick < TurningWaitTicks; tick++)
        {
            loop.Advance();
            for (var wagon = 0; wagon < wagons.Length && turning < 0; wagon++)
            {
                if (wagons[wagon].IsInWorld && looks.Rolling.Vehicles[wagons[wagon].Look].Fans.Length > 0) turning = wagon;
            }
        }

        Console.Write("drawn, a locomotive's fans turning:");
        if (turning < 0)
        {
            Console.WriteLine($" none out in {TurningWaitTicks / config.Sim.TickRateHz:F0} s");
            return;
        }

        Compare(town, loop, config, plan, looks, $"at {town.ElapsedS:F0} s", framed => framed.World.View.Wagons[turning].PositionM, 1600f / 45f);
        Console.WriteLine();
    }

    /// <summary>
    /// One framing's instances, host / device / differing, and the puffs of smoke the device drew besides (CAR-54): the
    /// device's alone, so they are counted apart and the rest held to the host's. Taken a puff's life and two track points
    /// on from when the emit is laid, since the cars' tracks are laid from then (<see cref="DeviceWorld.DrawWith"/>).
    /// </summary>
    static void Compare(
        TownWorld town, SimLoop<TownWorld> loop, SimConfig config, CityPlan plan, TownSprites looks, string where,
        Func<TownWorld, Vector2> framedOn, float pxPerM)
    {
        var room = looks.RoomFor(plan, config);
        var over = new SpriteInstance[room.Over];
        var above = new SpriteInstance[Math.Max(1, room.Above)];
        var sprites = ((DeviceWorld)town.World).DrawWith(town, looks, room);
        var spanM = new Vector2(1600f, 900f) / pxPerM;
        var warmTicks = (int)MathF.Ceiling((config.Smoke.FullLifeS + (2f * config.Smoke.TrackEveryS)) * config.Sim.TickRateHz);
        for (var tick = 0; tick < warmTicks; tick++) loop.Advance();

        // The step ahead shown, so the fleets the host fills from stand where the device draws (SimFigures.WorldStepsAhead).
        town.LayTheClaims();
        var centreM = framedOn(town);

        // Everything framed picked out, so its rings (CTL-1, SpriteRings) are held to the host's as well.
        town.SelectIn(centreM, spanM, 0f, add: false);
        var (overCount, aboveCount) = looks.FillMoving(town, config, centreM, spanM, pxPerM, over, above);
        var fastest = Fastest(sprites, town, centreM, spanM, pxPerM);
        var (deviceOver, overPuffs) = Unsmoked(sprites.Over, looks.SmokeSheet);
        var (deviceAbove, abovePuffs) = Unsmoked(sprites.Above, looks.SmokeSheet);
        var differ = Differing(over.AsSpan(0, overCount), deviceOver) + Differing(above.AsSpan(0, aboveCount), deviceAbove);
        Console.Write(
            $"  {where} {overCount + aboveCount} / {deviceOver.Length + deviceAbove.Length} / {differ} (+{overPuffs + abovePuffs} puffs)" +
            $" [{Walkers(over.AsSpan(0, overCount), looks)} walkers; µs lay {fastest.Lay * 1e3:F0}, count {fastest.Count * 1e3:F0}," +
            $" scan {fastest.Scan * 1e3:F0}, write {fastest.Write * 1e3:F0}, order {fastest.Order * 1e3:F0}]");
    }

    /// <summary>How many emits of one framing <see cref="Fastest"/> times, the fastest of each pass kept.</summary>
    const int TimedEmits = 20;

    /// <summary>The device's time each pass of an emit takes at this framing, the fastest of <see cref="TimedEmits"/>.</summary>
    static DeviceSprites.PassesMs Fastest(DeviceSprites sprites, TownWorld town, Vector2 centreM, Vector2 spanM, float pxPerM)
    {
        var fastest = new DeviceSprites.PassesMs(double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue);
        for (var emit = 0; emit < TimedEmits; emit++)
        {
            sprites.Emit(town, centreM, spanM, pxPerM);
            var ms = sprites.EmitMs;
            fastest = new DeviceSprites.PassesMs(
                Math.Min(fastest.Lay, ms.Lay), Math.Min(fastest.Count, ms.Count), Math.Min(fastest.Scan, ms.Scan),
                Math.Min(fastest.Write, ms.Write), Math.Min(fastest.Order, ms.Order));
        }

        return fastest;
    }

    /// <summary>How many of a run's instances are walkers, standing or down — the order pass's whole input.</summary>
    static int Walkers(ReadOnlySpan<SpriteInstance> run, TownSprites looks)
    {
        var walkers = 0;
        foreach (var instance in run)
        {
            if (instance.Sheet < looks.People.SheetCount) walkers++;
        }

        return walkers;
    }

    /// <summary>A run of the device's without its puffs of smoke, which no host fill draws, and how many there were.</summary>
    static (SpriteInstance[] Others, int Puffs) Unsmoked(ReadOnlySpan<SpriteInstance> onDevice, int smokeSheet)
    {
        var rest = new List<SpriteInstance>(onDevice.Length);
        foreach (var instance in onDevice)
        {
            if (instance.Sheet != smokeSheet) rest.Add(instance);
        }

        return ([.. rest], onDevice.Length - rest.Count);
    }

    static int Differing(ReadOnlySpan<SpriteInstance> host, ReadOnlySpan<SpriteInstance> onDevice)
    {
        var differ = Math.Abs(host.Length - onDevice.Length);
        for (var at = 0; at < Math.Min(host.Length, onDevice.Length); at++)
        {
            if (host[at] != onDevice[at]) differ++;
        }

        return differ;
    }

    static void Print(string world, TownWatch watch, double seconds) => Console.WriteLine(
        $"{world,-9}{watch.Walkers,9}{watch.Cars,6}{watch.Down,6}{watch.Wrecked,9}{watch.DrivenM / 1000f,12:F1}" +
        $"{watch.Touches,9}{seconds,8:F1}");
}
