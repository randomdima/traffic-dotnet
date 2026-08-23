using Xunit;

namespace TrafficSimulation.Tests.Simulation;

/// <summary>
/// Every test that measures a world rather than merely asserting about one belongs here, and they run
/// one at a time.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reason is now the measurement and no longer the solver.</b> While the solver was a package it
/// was found to be unsafe from two threads even in two separate worlds — a pool index past its own count
/// inside its island builder, which is shared state being written twice — and this collection existed to
/// keep the suite out of it. This engine's own solver holds no state outside the world that owns it, so
/// two worlds on two threads are two worlds.
/// </para>
/// <para>
/// What is left is worth as much: the gates in here are allocation and timing windows over towns of a
/// thousand bodies, and a window taken while three other towns are being built beside it is a window
/// nobody can quote. Serialising them is how the figures stay comparable between runs.
/// </para>
/// <para>
/// Parallelisation is switched off for this collection rather than for the assembly, so the two hundred
/// tests that measure nothing keep running at once.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SolverCollection
{
    public const string Name = "the solver";
}
