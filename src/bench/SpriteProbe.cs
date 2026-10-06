using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What filling a frame's sprites costs</b>, part by part, at framings from a street to the whole town —
/// the CPU half of drawing, which no GPU timer sees and a windowed profile spreads thin over a frame the
/// display holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>The town is aged first</b>, so its cars stand spread over its streets and car parks as a running one's
/// do, and the framings are taken about where a window opens on it (<see cref="Opening.LooksAtM"/>).
/// </para>
/// <para>
/// <b>Each part is called on its own</b> until its figure settles, then the whole fill as a frame calls it. A
/// part alone runs with a warmer cache than it does between its neighbours, so the parts sum to a little under
/// the whole.
/// </para>
/// </remarks>
internal static class SpriteProbe
{
    /// <summary>The interface a framing is laid out on: a desktop window's.</summary>
    static readonly Vector2 UiPx = new(1600f, 900f);

    /// <summary>Each framing's short side: a hand-driven car's, the default, a district, a quarter; the whole town follows.</summary>
    static readonly float[] ViewsM = [45f, 70f, 300f, 1500f];

    const float AgeS = 60f;
    const double SettleS = 0.25;
    const double FrameUs = 1e6 / 120.0;

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);

        var ageTicks = (long)(AgeS * config.Sim.TickRateHz);
        var agedAt = Stopwatch.GetTimestamp();
        while (loop.Tick < ageTicks) loop.Advance();
        var tickUs = Stopwatch.GetElapsedTime(agedAt).TotalMicroseconds / ageTicks;

        var looks = TownSprites.Load(config);
        looks.ReadAspects(SheetAtlas.Pack(looks.Sheets));
        looks.Lay(plan, world.Uses, config);
        var room = TownSprites.RoomFor(plan, config);
        var under = new SpriteInstance[room.Under];
        var into = new SpriteInstance[room.Over];
        var above = new SpriteInstance[Math.Max(1, room.Above)];
        var level = world.Levelled ? CityPlan.RoadArrays.Ground : CarSprites.EveryLevel;
        var inView = new int[world.Cars.Capacity];
        var peopleInView = new int[world.People.Capacity];
        var openingM = Opening.LooksAtM(world.Terrain, config, plan.WorldSizeM, ViewsM[0] * 0.5f);

        Console.WriteLine(
            $"sprite probe — {map}, {world.Cars.Count} cars, {world.People.Count} walkers, aged {AgeS:F0} s " +
            $"at {tickUs:F0} µs a tick; µs a fill on a {UiPx.X:F0}x{UiPx.Y:F0} interface, a frame being {FrameUs:F0} µs");
        const double PerMb = 1024.0 * 1024.0;
        var instanceBytes = System.Runtime.CompilerServices.Unsafe.SizeOf<SpriteInstance>();
        Console.WriteLine(
            $"instances written a frame in flight {room.Written} = {room.Written * instanceBytes / PerMb:F1} MB; " +
            $"buildings and props laid once {room.Standing} = {room.Standing * instanceBytes / PerMb:F1} MB");
        Console.WriteLine(
            $"{"view m",8}{"px/m",7}{"marks",8}{"scenery",8}{"standing",9}{"people",8}{"find",8}{"tyres",8}{"bodies",8}" +
            $"{"arms",8}{"lamps",8}{"signals",8}{"whole",9}{"frame%",8}{"drawn",8}{"cars",7}{"alloc B",8}");

        foreach (var viewM in ViewsM) Row(viewM, openingM, MathF.Min(UiPx.X, UiPx.Y) / viewM);

        var townPxPerM = MathF.Min(UiPx.X / plan.WorldSizeM.X, UiPx.Y / plan.WorldSizeM.Y);
        Row(MathF.Min(UiPx.X, UiPx.Y) / townPxPerM, plan.WorldSizeM * 0.5f, townPxPerM);

        void Row(float viewM, Vector2 centreM, float pxPerM)
        {
            var spanM = UiPx / pxPerM;
            var leastPartM = config.View.CarPartLeastPx / pxPerM;
            var marks = Us(() => MarkSprites.Fill(world.Marks, looks.RubberBrushSheet, looks.SoilBrushSheet, centreM, spanM, under), out _);
            var scenery = Us(() => looks.Scenery.Fill(centreM, spanM, under), out _);
            var standing = Us(() => looks.Standing.Range(centreM, spanM).Count, out _);
            var reachM = (spanM * 0.5f) + new Vector2(looks.CarReachM);
            var find = Us(() => world.PeopleIn(centreM - reachM, centreM + reachM, peopleInView).Length, out var walkers)
                + Us(() => world.CarsIn(centreM - reachM, centreM + reachM, inView).Length, out var listed);
            var people = Us(() => PersonSprites.Fill(
                world.People, peopleInView.AsSpan(0, walkers), looks.People, looks.Aspects, looks.FirstDownSheet, centreM,
                spanM, into), out _);
            var tyres = Us(() => looks.WidestTyreM < leastPartM ? 0 : CarSprites.FillFrontTyres(
                world.Cars, inView.AsSpan(0, listed), looks.RubberSheet, leastPartM, centreM, spanM, into, level), out _);
            var bodies = Us(() => CarSprites.Fill(
                world.Cars, inView.AsSpan(0, listed), looks.Cars, looks.FirstCarSheet, looks.CarSheetScales, centreM, spanM,
                into, level), out var cars);
            var arms = Us(() => CarSprites.FillBeams(
                world.Cars, inView.AsSpan(0, listed), looks.Cars, world.Recovery, looks.FirstBeamSheet, centreM, spanM, into,
                level), out _);
            var lamps = Us(() => looks.WidestGlowM < leastPartM ? 0 : LampSprites.Fill(
                world.Cars, inView.AsSpan(0, listed), looks.Cars, config, looks.LensSheet, looks.LampGlowSheet,
                world.ElapsedS, world.HandDriven, world.HandDrivenCar, centreM, spanM, into, level, leastPartM), out _);
            var signals = Us(() => SignalSprites.Fill(world, config, looks.FirstHeadSheet, centreM, spanM, into), out _);
            var whole = Us(() => looks.Fill(world, config, centreM, spanM, pxPerM, under, into, above).Drawn, out var drawn);
            var allocatedAt = GC.GetAllocatedBytesForCurrentThread();
            looks.Fill(world, config, centreM, spanM, pxPerM, under, into, above);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedAt;

            Console.WriteLine(
                $"{viewM,8:F0}{pxPerM,7:F1}{marks,8:F1}{scenery,8:F1}{standing,9:F1}{people,8:F1}{find,8:F1}{tyres,8:F1}{bodies,8:F1}" +
                $"{arms,8:F1}{lamps,8:F1}{signals,8:F1}{whole,9:F1}{100.0 * whole / FrameUs,7:F1}%{drawn,8}{cars,7}{allocated,7}");
        }
    }

    /// <summary>Microseconds a call, called until <see cref="SettleS"/> has passed, and what the last call wrote.</summary>
    static double Us(Func<int> fill, out int written)
    {
        for (var warm = 0; warm < 5; warm++) fill();

        written = 0;
        var calls = 0;
        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt).TotalSeconds < SettleS)
        {
            written = fill();
            calls++;
        }

        return Stopwatch.GetElapsedTime(startedAt).TotalMicroseconds / calls;
    }
}
