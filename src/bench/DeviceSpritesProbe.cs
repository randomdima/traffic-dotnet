using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The frame's moving sprites emitted on the device beside the host's fill, on a running town</b>: once the town has
/// aged as the sprite probe ages it, at the framings that probe takes (<see cref="SpriteProbe"/>) and the nearer of them
/// again about a car under way, the host's
/// <see cref="TownSprites.FillMoving"/> against the device's emit (<see cref="DeviceSprites"/>) — timed, and held to one
/// answer frame by frame.
/// </summary>
/// <remarks>
/// The device's figure is its own time from the first kernel to the last, read off the queue's stamps: what laying the
/// shadow's rows and reading the runs back cost is the shadow's and is printed apart. An instance differs where any of
/// its fields does.
/// </remarks>
internal static class DeviceSpritesProbe
{
    static readonly Vector2 UiPx = new(1600f, 900f);

    /// <summary>Each framing's short side, as the sprite probe takes them; the whole town follows.</summary>
    static readonly float[] ViewsM = [45f, 70f, 300f, 1500f];

    const float AgeS = 60f;

    /// <summary>
    /// The frames each framing is emitted and compared at, a second of the town apart, and the emits timed at each — after
    /// a few that are not, so the figure is a device already at work rather than one woken from the host's turn.
    /// </summary>
    const int Rounds = 5;

    const int TickAFrame = 60;
    const int EmitsWarming = 3;
    const int EmitsTimed = 10;
    const double SettleS = 0.25;

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        var ageTicks = (long)(AgeS * config.Sim.TickRateHz);
        while (loop.Tick < ageTicks) loop.Advance();

        var looks = TownSprites.Load(config);
        looks.ReadAspects(SheetAtlas.Pack(looks.Sheets));
        var room = TownSprites.RoomFor(plan, config);
        var over = new SpriteInstance[room.Over];
        var above = new SpriteInstance[Math.Max(1, room.Above)];

        using var vk = Vk.Open("traffic-dotnet", validation: false);
        using var onDevice = DeviceSprites.For(vk, config, world, looks, room);

        var openingM = Opening.LooksAtM(world.Terrain, config, plan.WorldSizeM, ViewsM[0] * 0.5f);
        var framings = new List<(string Where, float ViewM, Vector2 CentreM, int Follows, float PxPerM)>();
        foreach (var viewM in ViewsM) framings.Add(("opening", viewM, openingM, -1, MathF.Min(UiPx.X, UiPx.Y) / viewM));
        var townPxPerM = MathF.Min(UiPx.X / plan.WorldSizeM.X, UiPx.Y / plan.WorldSizeM.Y);
        framings.Add(("town", MathF.Min(UiPx.X, UiPx.Y) / townPxPerM, plan.WorldSizeM * 0.5f, -1, townPxPerM));

        // And where the traffic is, which the opening on a traced town may not be: behind a car under way in the thick of
        // it, near enough for its tyres and its lamps.
        var followed = BusiestCarUnderWay(world.Cars);
        foreach (var viewM in ViewsM[..^1]) framings.Add(("traffic", viewM, Vector2.Zero, followed, MathF.Min(UiPx.X, UiPx.Y) / viewM));

        var rows = framings.Select(_ => new Row()).ToArray();
        var layMs = new List<double>();
        var readMs = new List<double>();
        for (var round = 0; round < Rounds; round++)
        {
            loop.Advance(TickAFrame);
            var laying = Stopwatch.StartNew();
            onDevice.Lay(world);
            layMs.Add(laying.Elapsed.TotalMilliseconds);

            for (var at = 0; at < framings.Count; at++)
            {
                var (_, _, fixedM, follows, pxPerM) = framings[at];
                var centreM = follows >= 0 ? world.Cars.PositionM[follows] : fixedM;
                var spanM = UiPx / pxPerM;
                var (overCount, aboveCount) = looks.FillMoving(world, config, centreM, spanM, pxPerM, over, above);
                for (var emit = 0; emit < EmitsWarming + EmitsTimed; emit++)
                {
                    onDevice.Emit(centreM, spanM, pxPerM, world.ElapsedS, readBack: false);
                    if (emit >= EmitsWarming) rows[at].Add(onDevice);
                }

                var emitting = Stopwatch.StartNew();
                onDevice.Emit(centreM, spanM, pxPerM, world.ElapsedS);
                readMs.Add(emitting.Elapsed.TotalMilliseconds);
                rows[at].Drawn += overCount + aboveCount;
                rows[at].Differ += Differing(over.AsSpan(0, overCount), onDevice.Over)
                                   + Differing(above.AsSpan(0, aboveCount), onDevice.Above);
            }
        }

        Console.WriteLine(
            $"device sprites probe — {map} on {vk.DeviceName}, {world.Cars.Count} cars, {world.People.Count} walkers, " +
            $"{world.Heads.Count} heads, aged {AgeS:F0} s and {Rounds} frames a second apart; µs a fill on a {UiPx.X:F0}x{UiPx.Y:F0} interface");
        Console.WriteLine(
            $"shadow only: laying the rows median {Median(layMs):F2} ms, an emit submitted, waited for and both rooms read back median {Median(readMs):F2} ms");
        Console.WriteLine(
            $"{"about",-9}{"view m",8}{"px/m",7}{"drawn",9}{"host µs",10}{"device µs",11}{"count",8}{"scan",8}{"write",8}{"differ",8}");
        for (var at = 0; at < framings.Count; at++)
        {
            var (where, viewM, fixedM, follows, pxPerM) = framings[at];
            var centreM = follows >= 0 ? world.Cars.PositionM[follows] : fixedM;
            var spanM = UiPx / pxPerM;
            var hostUs = Us(() => looks.FillMoving(world, config, centreM, spanM, pxPerM, over, above).Over);
            var row = rows[at];
            Console.WriteLine(
                $"{where,-9}{viewM,8:F0}{pxPerM,7:F1}{row.Drawn / Rounds,9}{hostUs,10:F1}{Median(row.WholeUs),11:F1}" +
                $"{Median(row.CountUs),8:F1}{Median(row.ScanUs),8:F1}{Median(row.WriteUs),8:F1}{row.Differ,8}");
        }
    }

    /// <summary>Of the cars under way, the one with the most others within a street's framing of it; the first car where none is.</summary>
    static int BusiestCarUnderWay(CarFleet cars)
    {
        const float NearM = 40f;
        const int MostAsked = 1_000;
        var busiest = 0;
        var mostNear = -1;
        var asked = 0;
        for (var car = 0; car < cars.Count && asked < MostAsked; car++)
        {
            if (!cars.Driven[car] || cars.AlongMps[car] <= 1f) continue;

            asked++;
            var near = 0;
            for (var other = 0; other < cars.Count; other++)
            {
                if (Vector2.DistanceSquared(cars.PositionM[car], cars.PositionM[other]) < NearM * NearM) near++;
            }

            if (near <= mostNear) continue;

            busiest = car;
            mostNear = near;
        }

        return cars.Count > 0 ? busiest : -1;
    }

    /// <summary>One framing's readings over every round.</summary>
    sealed class Row
    {
        public readonly List<double> WholeUs = [];
        public readonly List<double> CountUs = [];
        public readonly List<double> ScanUs = [];
        public readonly List<double> WriteUs = [];
        public int Drawn;
        public int Differ;

        public void Add(DeviceSprites onDevice)
        {
            WholeUs.Add(onDevice.BetweenMs(DeviceSprites.Stage.Began, DeviceSprites.Stage.Written) * 1e3);
            CountUs.Add(onDevice.BetweenMs(DeviceSprites.Stage.Laid, DeviceSprites.Stage.Counted) * 1e3);
            ScanUs.Add(onDevice.BetweenMs(DeviceSprites.Stage.Counted, DeviceSprites.Stage.Scanned) * 1e3);
            WriteUs.Add(onDevice.BetweenMs(DeviceSprites.Stage.Scanned, DeviceSprites.Stage.Written) * 1e3);
        }
    }

    /// <summary>How many instances of a run differ, counting every one past the shorter run's end.</summary>
    static int Differing(ReadOnlySpan<SpriteInstance> host, ReadOnlySpan<SpriteInstance> onDevice)
    {
        var differ = Math.Abs(host.Length - onDevice.Length);
        for (var at = 0; at < Math.Min(host.Length, onDevice.Length); at++)
        {
            if (host[at] != onDevice[at]) differ++;
        }

        return differ;
    }

    /// <summary>Microseconds a call, called until <see cref="SettleS"/> has passed.</summary>
    static double Us(Func<int> fill)
    {
        for (var warm = 0; warm < 5; warm++) fill();

        var calls = 0;
        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt).TotalSeconds < SettleS)
        {
            fill();
            calls++;
        }

        return Stopwatch.GetElapsedTime(startedAt).TotalMicroseconds / calls;
    }

    static double Median(List<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? 0d : sorted[sorted.Length / 2];
    }
}
