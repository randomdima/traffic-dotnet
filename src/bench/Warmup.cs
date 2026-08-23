using System.Diagnostics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// A town stood up, ticked and thrown away before any probe takes a figure. <b>What it warms is the
/// runtime and not the town</b>: until it has run, a window taken of a tick is partly a measurement of
/// the just-in-time compiler.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tiered compilation settles on a clock, not on a call count.</b> A method starts quick-jitted,
/// is re-jitted with instrumentation once dynamic PGO wants a profile of it, and reaches optimised code
/// only after that profile has been collected — and each promotion waits on a background timer of about
/// a tenth of a second, not on the thirtieth call. Six hundred ticks of a big town is half a second of
/// wall clock, which is not enough for two promotions, so a probe warmed by tick count alone measures
/// its first map through instrumented code and every later one through optimised.
/// </para>
/// <para>
/// <b>That made the first map in the list carry the whole cost.</b> Measured on Odesa and River: with
/// the warm-up by ticks alone the two read 966 µs and 240 µs a tick, and with the process warmed first
/// they read 519 and 258 — so most of the four-fold gap between the two towns was the compiler, and the
/// figure the probe printed for whichever map ran first was not a figure anybody could quote.
/// </para>
/// <para>
/// <b>It is deliberately not a longer per-map warm-up.</b> A probe's own warm-up decides how old the
/// town it measures is, and stretching it to seconds would measure a different town rather than the
/// same one on settled code. This runs once for the process, on a town of its own, and every map's
/// own warm-up is left alone.
/// </para>
/// </remarks>
internal static class Warmup
{
    /// <summary>
    /// How long the hot paths must have been running before the code under them stops changing. Two
    /// promotions at a tenth of a second each, with room over for the background queue on a busy
    /// machine — measured rather than reasoned: below about two seconds the town figures still move.
    /// </summary>
    const double SecondsToSettle = 2.5;

    /// <summary>Ticks between clock readings. A block, so the check is not itself part of what is warmed.</summary>
    const int TicksPerBlock = 60;

    /// <summary>Enough of the timed path to promote it too, since the phase ranking is measured through it.</summary>
    const int TimedTicks = 180;

    static bool _warm;
    static bool _loopWarm;

    /// <summary>
    /// The empty loop's own instantiation, which is a <em>different</em> body of code from the town's:
    /// <see cref="EmptyWorld"/> is a struct, so <see cref="SimLoop{TWorld}"/> is compiled again for it
    /// and warming the town warms none of it.
    /// </summary>
    public static void TheEmptyLoop(SimConfig config)
    {
        if (_loopWarm) return;

        _loopWarm = true;
        foreach (var timed in (ReadOnlySpan<bool>)[false, true])
        {
            var loop = new SimLoop<EmptyWorld>(new EmptyWorld(TickProbe.WarmAgents), config) { Timed = timed };
            Settle(loop.Advance);
        }
    }

    /// <summary>
    /// Warm the solver and the tick, once per process. Later calls do nothing, so a probe may ask for it
    /// without knowing whether another already has.
    /// </summary>
    public static void TheProcess(SimConfig config)
    {
        if (_warm) return;

        _warm = true;
        SolverProbe.WarmTheProcess(config);

        // The heaviest shipped town, because what is being warmed is the code the busiest map runs and a
        // path only reached by a crowd is one the JIT would otherwise first see inside a measured window.
        var plan = TownReader.ReadFile(ProjectPaths.TownFile(ProjectPaths.ShippedMaps()[0]));
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);

        Settle(loop.Advance);

        // The read-out's own path is a second set of methods, and the phase ranking is taken through it.
        loop.Timed = true;
        world.Timed = true;
        loop.Advance(TimedTicks);
    }

    /// <summary>Run in blocks until the clock says the compiler has finished with what is being run.</summary>
    static void Settle(Action<int> advance)
    {
        var until = Stopwatch.GetTimestamp() + (long)(SecondsToSettle * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < until) advance(TicksPerBlock);
    }
}
