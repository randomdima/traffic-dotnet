using System.Diagnostics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// What a tick of a <em>real</em> town costs and allocates, on every map this engine ships. The
/// empty-loop figure and the solver's own are both taken against rigs; this one is taken against the
/// thing the rules are actually about.
/// </summary>
/// <remarks>
/// <b>Rule 2 wants zero and every column here is this engine's own code</b>, which is what changed when
/// the solver stopped being a package: the several hundred bytes a step used to allocate were the one
/// figure this engine could only report and never fix. The two shares are still printed apart, because
/// what allocates and what does not is the thing a gate has to be able to name.
/// </remarks>
internal static class TownProbe
{
    public const int WarmupTicks = 600;

    public const int MeasuredTicks = 900;

    public static void Run(SimConfig config)
    {
        Console.WriteLine($"town probe — {WarmupTicks} warm-up ticks, {MeasuredTicks} measured, {config.Solver.VelocityIterations} solver iterations");
        Console.WriteLine($"{"map",-10}{"walkers",9}{"cars",6}{"statics",9}{"in book",9}{"stand ms",10}{"B/tick",10}{"ours B",9}{"solver B",10}{"µs/tick",10}{"gen0",7}");

        // The capacity a big world grows outlives the world that grew it, and so does the compiler's
        // opinion of the tick: nothing is measured until a town has been stood up and run
        // (<see cref="Warmup"/>), or the first row is a measurement of the JIT.
        Warmup.TheProcess(config);

        var samples = new TownSample[ProjectPaths.ShippedMaps().Length];
        var maps = ProjectPaths.ShippedMaps();
        for (var map = 0; map < maps.Length; map++)
        {
            var sample = Sample(maps[map], config);
            samples[map] = sample;
            Console.WriteLine($"{maps[map],-10}{sample.Walkers,9}{sample.Cars,6}{sample.Statics,9}{sample.InTheBook,9}{sample.StandMs,10:F0}{sample.BytesPerTick,10:F1}" +
                              $"{sample.OwnBytesPerTick,9:F1}{sample.SolverBytesPerTick,10:F1}{sample.MicrosecondsPerTick,10:F1}{sample.Gen0Collections,7}");
        }

        Console.WriteLine("Rule 2 is two claims and both now hold on the same row: nothing allocated, flat in the size " +
                          "of the town, with the solver's step inside the figure rather than beside it.");

        Ranked(maps, samples);
    }

    /// <summary>
    /// The same tick, ranked by the five phases the brief fixes and by the two things inside them this
    /// town can say more about: which kind of agent phase 3 spent itself on, and how much of phase 4
    /// was the solver's own step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>These are the read-out's rows, in the read-out's order and to the read-out's arithmetic.</b>
    /// A probe that ranked the tick differently from the panel would leave two accounts of one
    /// measurement, and the first disagreement between them would be nobody's to settle.
    /// </para>
    /// <para>
    /// <b>It is a second window and must not be subtracted from the first.</b> The ranking needs the
    /// timing on, so it runs 900 ticks later in the same town's life — and this town is not a constant:
    /// Odesa's untimed window read 1 235 µs and the timed one 1 098 on the same run, which is the
    /// town's own age and variance rather than a saving. What the phase timing costs is measured where
    /// the workload holds still, on the empty loop (<c>--bench tick</c>), and not by differencing two
    /// windows of a town.
    /// </para>
    /// </remarks>
    static void Ranked(string[] maps, TownSample[] samples)
    {
        Console.WriteLine();
        Console.WriteLine("the tick ranked by phase — µs per tick, on a second window with the timing on: not " +
                          "comparable with the µs/tick above, which is a different window of a moving town");
        Console.WriteLine($"{"map",-10}{"tick",8}{"input",8}{"index",8}{"agents",8}{"walkers",9}{"cars",8}{"bodies",8}" +
                          $"{"solver",8}{"own",8}{"contacts",10}{"other",8}");

        for (var map = 0; map < maps.Length; map++)
        {
            var sample = samples[map];
            var phases = sample.Phases;
            var sub = sample.Sub;
            Console.WriteLine(
                $"{maps[map],-10}{Micro(phases, phases.WholeTicks),8:F1}{Micro(phases, phases.InputTicks),8:F1}" +
                $"{Micro(phases, phases.IndexTicks),8:F1}{Micro(phases, phases.AgentTicks),8:F1}" +
                $"{Micro(phases, sub.WalkerTicks),9:F1}{Micro(phases, sub.CarTicks),8:F1}" +
                $"{Micro(phases, phases.BodyTicks),8:F1}{Micro(phases, sub.SolverTicks),8:F1}" +
                $"{Micro(phases, phases.BodyTicks - sub.SolverTicks),8:F1}{Micro(phases, phases.ContactTicks),10:F1}" +
                $"{Micro(phases, phases.OtherTicks),8:F1}");
        }

        Console.WriteLine("tick is measured whole rather than added up, so other is a real residual: it reading " +
                          "as nothing is what says the five phases still cover the tick.");
    }

    static double Micro(in PhaseTimes phases, long ticks) => phases.MillisecondsPer(ticks) * 1000d;

    /// <summary>
    /// One map's answer. <see cref="OwnBytesPerTick"/> is phases 1–3 — everything this engine wrote —
    /// and <see cref="SolverBytesPerTick"/> is what is left, which is phase 4's step.
    /// </summary>
    public readonly record struct TownSample(
        int Walkers, int Cars, int Statics, int InTheBook, double StandMs, double BytesPerTick, double OwnBytesPerTick,
        double MicrosecondsPerTick, int Gen0Collections, PhaseTimes Phases, TickParts Sub)
    {
        public double SolverBytesPerTick => BytesPerTick - OwnBytesPerTick;
    }

    public static TownSample Sample(string map, SimConfig config)
    {
        var plan = TownReader.ReadFile(ProjectPaths.TownFile(map));

        var started = Stopwatch.GetTimestamp();
        using var world = new TownWorld(plan, config);
        var standMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        var gen0 = GC.CollectionCount(0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        started = Stopwatch.GetTimestamp();
        loop.Advance(MeasuredTicks);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // A second window, with the timing on. It is not folded into the first because a timed tick is
        // not the tick this build runs — the difference between the two is the instrument, and it is
        // reported rather than hidden inside the figure it inflates.
        loop.Timed = true;
        world.Timed = true;
        loop.Advance(MeasuredTicks);
        var phases = loop.Phases;
        var sub = world.Sub;

        // Off again before the allocation windows below, which run the phases by hand and without a
        // step: they would leave phase 3's own accounting open and add their ticks to the ranking.
        loop.Timed = false;
        world.Timed = false;

        return new TownSample(
            world.People.Count, world.Cars.Count, world.StaticBodyCount, world.StandingSlots, standMs, allocated / (double)MeasuredTicks,
            OwnBytesPerTick(world, config), elapsed.TotalMicroseconds / MeasuredTicks, GC.CollectionCount(0) - gen0,
            phases, sub);
    }

    /// <summary>
    /// Phase 5 alone, and it needs its own measurement because <see cref="OwnBytesPerTick"/> cannot
    /// reach it: that one never steps, so the solver reports no contacts and the arbiter walks an empty
    /// list however long it is run for.
    /// </summary>
    /// <remarks>
    /// What is being watched here is the walk over the begin-touch events — a struct enumerator over
    /// arrays the world already owns, which is the only shape of it that keeps rule 2. An iterator, a
    /// <c>ToArray</c> inside the dependency, or a boxed context would all show up as a figure above zero
    /// on a town whose cars are queueing, and on no other kind of town.
    /// </remarks>
    public static double ContactBytesPerTick(TownWorld world, SimConfig config, int ticks)
    {
        var dtS = config.TickSeconds;
        for (var tick = 0; tick < ticks / 10; tick++) Sweep(world, dtS);

        var allocated = 0L;
        for (var tick = 0; tick < ticks; tick++)
        {
            world.StepBodies(dtS);
            var before = GC.GetAllocatedBytesForCurrentThread();
            world.ResolveContacts();
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        return allocated / (double)ticks;

        // The step is what produces the contacts, so phase 5 cannot be warmed without it.
        static void Sweep(TownWorld town, float seconds)
        {
            town.StepBodies(seconds);
            town.ResolveContacts();
        }
    }

    /// <summary>
    /// Phases 1–3 alone, run over the town as it now stands. <b>This is not a tick</b> and is not
    /// offered as one: no body moves, so the decisions repeat. What it isolates is the only share of
    /// the figure this engine can do anything about, and separating the two is the difference between
    /// a rule that is being kept and a rule that is being blamed on a dependency.
    /// </summary>
    public static double OwnBytesPerTick(TownWorld world, SimConfig config)
    {
        Decide(world, config, MeasuredTicks / 10);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Decide(world, config, MeasuredTicks);
        return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)MeasuredTicks;

        static void Decide(TownWorld town, SimConfig figures, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                town.ReadPlayerInput();
                town.RebuildProximityIndex();
                for (var agent = 0; agent < town.AgentCount; agent++)
                {
                    town.TickAgent(agent);
                    town.DecideAgent(agent, figures.Sim.AgentDecisionIntervalS);
                }

                town.ResolveContacts();
            }
        }
    }
}
