using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.Bench;

/// <summary>
/// What a tick costs, and what a tick allocates. The second figure is this engine's rule 2 as a
/// number: a standing town must show a flat allocation counter, because a collection landing inside
/// a measured sample is a figure nobody can quote.
/// </summary>
/// <remarks>
/// <para>
/// The window is CPU time rather than wall clock, the steady state rather than the first tick, and it
/// is 1 200 warm-up ticks before 900 measured — stated here because it is the first thing anyone asks
/// of a managed engine's numbers.
/// </para>
/// <para>
/// <b>The tick count is what makes the roster old enough, and never what makes the code settled.</b>
/// Twelve hundred ticks of an empty loop is twenty milliseconds, which is under a single tiered
/// promotion: the compiler is settled by <see cref="Warmup.TheEmptyLoop"/> before the first row is
/// taken, and by the clock, because that is what tiering runs on.
/// </para>
/// </remarks>
internal static class TickProbe
{
    public const int WarmupTicks = 1_200;
    public const int MeasuredTicks = 900;

    /// <summary>The roster the warm-up runs at: the largest any row asks for, so no row meets cold code.</summary>
    public const int WarmAgents = 1_000;

    public static void Run(SimConfig config)
    {
        Warmup.TheEmptyLoop(config);
        Console.WriteLine($"tick probe — {WarmupTicks} warm-up ticks, {MeasuredTicks} measured, {config.Sim.TickRateHz} Hz");
        Console.WriteLine($"{"agents",8}{"µs/tick",12}{"timed",10}{"instr",9}{"B/tick",10}{"decisions/tick",18}");

        foreach (var agents in (ReadOnlySpan<int>)[0, 1, 100, 1_000])
        {
            var plain = Measure(config, agents, timed: false);
            var timed = Measure(config, agents, timed: true);
            Console.WriteLine($"{agents,8}{plain.Microseconds,12:F2}{timed.Microseconds,10:F2}" +
                              $"{timed.Microseconds - plain.Microseconds,9:F2}{plain.BytesPerTick,10:F1}" +
                              $"{plain.DecisionsPerTick,18:F2}");
        }

        Console.WriteLine("A town is not open yet, so these are the loop's own cost. B/tick must be 0.");
        Console.WriteLine("instr is what the read-out's phase timing costs: six timestamps a tick, 0.11 µs flat, " +
                          "read off the empty rows — on the busy ones it is under the two paths' own codegen.");
    }

    /// <summary>Allocated bytes per tick over a standing roster — the gate, and it is expected to be exactly zero.</summary>
    public static double AllocatedBytesPerTick(SimConfig config, int agents, int ticks)
    {
        var loop = new SimLoop<EmptyWorld>(new EmptyWorld(agents), config);
        loop.Advance(WarmupTicks);

        var before = GC.GetAllocatedBytesForCurrentThread();
        loop.Advance(ticks);
        return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)ticks;
    }

    /// <summary>
    /// One roster's answer, with the read-out's own phase timing either on or off. <b>The pair is what
    /// prices the instrument</b>: the empty loop does the same work every tick, so the difference
    /// between the two runs is the timestamps and nothing else — which no window of a real town could
    /// say, because a town is not the same town twice.
    /// </summary>
    static (double Microseconds, double BytesPerTick, double DecisionsPerTick) Measure(
        SimConfig config, int agents, bool timed)
    {
        var loop = new SimLoop<EmptyWorld>(new EmptyWorld(agents), config) { Timed = timed };
        loop.Advance(WarmupTicks);

        // CPU time and not wall clock, and Environment.CpuUsage rather than a Process object, which
        // would allocate inside the window it is measuring.
        var decisionsBefore = loop.World.Decisions;
        var cpuBefore = Environment.CpuUsage.TotalTime;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        loop.Advance(MeasuredTicks);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var cpu = Environment.CpuUsage.TotalTime - cpuBefore;
        var decisions = loop.World.Decisions - decisionsBefore;

        return (cpu.TotalMicroseconds / MeasuredTicks, allocated / (double)MeasuredTicks, decisions / (double)MeasuredTicks);
    }
}
