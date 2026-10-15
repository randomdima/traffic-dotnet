using System.Diagnostics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// The five phases' own share of the tick, in stopwatch ticks, accumulated since the last reset.
/// Always read ranked by phase, never as a whole tick: the solver's step and the phases this engine
/// wrote are different measurements and one figure would fold them together.
/// </summary>
internal struct PhaseTimes
{
    public long InputTicks;
    public long IndexTicks;
    public long AgentTicks;
    public long BodyTicks;
    public long ContactTicks;

    /// <summary>
    /// The tick end to end, measured once around all five rather than added up from them, so the
    /// residual against <see cref="TotalTicks"/> says whether the five cover it. Measured at under a
    /// tenth of a microsecond on every shipped map — a check, not a row worth reading.
    /// </summary>
    public long WholeTicks;

    /// <summary>
    /// The dearest single tick, and the dearest single phase 2 — the peak a frame budget is spent against, where the
    /// others are means. A maximum, so one preemption by the machine reads as a peak of the town's.
    /// </summary>
    public long WorstTickTicks, WorstIndexTicks;

    /// <summary>Which tick of the loop's <see cref="WorstTickTicks"/> was, so a peak can be told from the turn it fell on.</summary>
    public long WorstTickAt;

    public long Ticks;

    public readonly long TotalTicks => InputTicks + IndexTicks + AgentTicks + BodyTicks + ContactTicks;

    /// <summary>What the five phases did not account for, which is never negative and is usually the instrument.</summary>
    public readonly long OtherTicks => Math.Max(0L, WholeTicks - TotalTicks);

    public readonly double MillisecondsPer(long phaseTicks) =>
        Ticks <= 0 ? 0d : phaseTicks * 1000d / (Stopwatch.Frequency * (double)Ticks);

    /// <summary>A span of stopwatch ticks in µs, for the worst ticks, which are one tick's and not a mean.</summary>
    public static double Microseconds(long stopwatchTicks) => stopwatchTicks * 1e6d / Stopwatch.Frequency;

    public long Mark(ref long into, long since)
    {
        var now = Stopwatch.GetTimestamp();
        into += now - since;
        return now;
    }

    public void Reset() => this = default;
}

/// <summary>
/// The drill-down inside two of the five phases, kept by the world rather than the loop because the
/// loop does not know what an agent is: which kind of agent phase 3 spent its time on, and how much of
/// phase 4 was the solver's own step.
/// </summary>
/// <remarks>
/// The solver's share is the figure most needing to be kept apart — phase 4 is the solver's step
/// wrapped in this engine's impulse and read-back loops, and a single figure would make every finding
/// about one of them a finding about the other.
/// </remarks>
internal struct TickParts
{
    long _stamp;

    public long WalkerTicks;
    public long CarTicks;

    /// <summary>Phase 3's second pass, kept apart from its ticks: the decisions due.</summary>
    public long WalkerDecisionTicks;

    public long CarDecisionTicks;
    public long SolverTicks;

    /// <summary>Where the next <see cref="Mark"/> measures from.</summary>
    public void Begin() => _stamp = Stopwatch.GetTimestamp();

    public void Mark(ref long into)
    {
        var now = Stopwatch.GetTimestamp();
        into += now - _stamp;
        _stamp = now;
    }

    public void Reset() => this = default;
}

/// <summary>
/// One fixed 60 Hz tick in five phases over a stable roster. Agents think in the physics tick, so
/// behaviour and physics share one timeline.
/// </summary>
/// <typeparam name="TWorld">
/// The town, or a rig standing in for one. A type parameter rather than a field of interface type so
/// the phase calls are direct: an allocation-free tick is not survivable with a per-agent interface
/// dispatch the JIT cannot see through.
/// </typeparam>
internal sealed class SimLoop<TWorld> where TWorld : ISimWorld
{
    // Not readonly: a struct world is mutated in place by its own phases, and a readonly field would
    // hand each call a defensive copy whose state is thrown away.
    TWorld _world;

    public SimLoop(TWorld world, SimConfig config)
    {
        _world = world;
        Decisions = new TickGroups(config.Sim.AgentDecisionIntervalS, config.Sim.TickRateHz);
        TickSeconds = config.TickSeconds;
    }

    /// <summary>Ticks advanced since the world was built. The stagger's origin, so it is never reset.</summary>
    public long Tick { get; private set; }

    /// <summary>Which agents think on which tick, and how much world one of their answers is good for.</summary>
    public TickGroups Decisions { get; }

    /// <summary>By reference, so a struct world's own counters are readable without copying it.</summary>
    public ref TWorld World => ref _world;

    public float TickSeconds { get; }

    /// <summary>
    /// Where the tick's time went. Off unless something is looking: four timestamps a tick is not free,
    /// and a run that was not asked for the read-out must not pay for it.
    /// </summary>
    public PhaseTimes Phases;

    public bool Timed { get; set; }

    public void Advance()
    {
        if (Timed)
        {
            AdvanceTimed();
            return;
        }

        _world.ReadPlayerInput();
        _world.RebuildProximityIndex();
        Decide();
        _world.StepBodies(TickSeconds);
        _world.ResolveContacts();
        Tick++;
    }

    void AdvanceTimed()
    {
        var began = Stopwatch.GetTimestamp();
        var stamp = began;
        _world.ReadPlayerInput();
        stamp = Phases.Mark(ref Phases.InputTicks, stamp);
        _world.RebuildProximityIndex();
        var indexedAt = Phases.Mark(ref Phases.IndexTicks, stamp);
        Phases.WorstIndexTicks = Math.Max(Phases.WorstIndexTicks, indexedAt - stamp);
        Decide();
        stamp = Phases.Mark(ref Phases.AgentTicks, indexedAt);
        _world.StepBodies(TickSeconds);
        stamp = Phases.Mark(ref Phases.BodyTicks, stamp);
        _world.ResolveContacts();

        // The last mark's own timestamp is the tick's end, so measuring the whole costs no sixth call.
        var wholeTicks = Phases.Mark(ref Phases.ContactTicks, stamp) - began;
        Phases.WholeTicks += wholeTicks;
        if (wholeTicks > Phases.WorstTickTicks) (Phases.WorstTickTicks, Phases.WorstTickAt) = (wholeTicks, Tick);
        Phases.Ticks++;
        Tick++;
    }

    /// <summary>
    /// Phase 3 as two passes: <b>every agent's tick, then every decision due</b>.
    /// </summary>
    /// <remarks>
    /// A decision reads what the whole of 3a left, never a roster ticked up to itself.
    /// </remarks>
    void Decide()
    {
        _world.TickTheAgents();
        _world.DecideTheAgents(Tick, Decisions);
    }

    public void Advance(int ticks)
    {
        for (var i = 0; i < ticks; i++) Advance();
    }
}
