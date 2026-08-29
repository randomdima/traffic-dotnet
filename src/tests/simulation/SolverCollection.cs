using Xunit;

namespace TrafficSimulation.Tests.Simulation;

/// <summary>
/// Every test that measures a world rather than merely asserting about one belongs here, and they run
/// one at a time.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reason is the measurement.</b> The gates in here are allocation and timing windows over towns of
/// a thousand bodies, and a window taken while three other towns are being built beside it is a window
/// nobody can quote. Serialising them is how the figures stay comparable between runs. This engine's own
/// solver holds no state outside the world that owns it, so two worlds on two threads are two worlds and
/// nothing else needs to be in here for safety.
/// </para>
/// <para>
/// Parallelisation is switched off for this collection rather than for the assembly, so the two hundred
/// tests that measure nothing keep running at once.
/// </para>
/// <para>
/// <b>A class that measures nothing is a bug in here</b>, and a costly one: a class that only asserts about
/// a town lays a single-file tail on the end of the town tier for nothing. <b>The membership test is
/// whether a failure could be caused by something else running</b> — a byte count, a wall clock, a crossing
/// count, or the one class that stands a world of the incumbent package's own, whose worlds live in a
/// static roster (<see cref="Physics.CastDifferenceTests"/>). Everything else runs beside everything else.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SolverCollection
{
    public const string Name = "the solver";
}
