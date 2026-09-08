namespace TrafficSimulation.Tests;

/// <summary>
/// <b>What breaks if this class goes red and nobody notices.</b> Every test class carries exactly one as
/// <c>[Trait(Priority.Key, …)]</c>, beside its <see cref="Tier"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A tier is what a question costs; a priority is what its answer is worth.</b> The two are orthogonal
/// on purpose — the cheapest class in the suite guards the solver and one of the dearest checks where a
/// lamp is drawn — and neither can be read off the other. `qq tests --upto=p4` cuts by the second, which is
/// how a run is made short without deciding that a whole tier does not matter.
/// </para>
/// <para>
/// <b>It is a ladder of consequence and not of feeling.</b> Each rung answers one question — <em>what is
/// the town if this is wrong?</em> — and the answers get less severe going down, from "it is not a
/// simulation" to "something is a few pixels out". A class that could be argued into two rungs takes the
/// lower-numbered one: the cost of over-stating a priority is a run somebody did not need, and the cost of
/// under-stating one is a defect nobody looked for.
/// </para>
/// <para>
/// <b>The budget is spent from the top.</b> The suite everybody runs has five minutes
/// (<c>docs/verification.md</c>), and when a new test would take it past that, what is dropped or made
/// cheaper is chosen from the bottom of this ladder and never from the top.
/// </para>
/// </remarks>
public static class Priority
{
    /// <summary>The trait key. One key for all ten, so a run can be cut at a rung.</summary>
    public const string Key = "P";

    /// <summary>
    /// <b>The engine is not one.</b> The two rules in <c>docs/goals.md</c> — the tick allocates, the frame's
    /// crossings scale with the town — the solver's own integrity, and the clock everything is stepped by.
    /// Red here and nothing below it means anything.
    /// </summary>
    public const string P0 = "P0";

    /// <summary>
    /// <b>The town is unsafe.</b> Bodies end up inside one another, a metre of ground is granted to two
    /// claimants, a body is hurt by arithmetic that is wrong — or the geometry every one of those is
    /// computed with is. A town that ships like this is one nobody can watch without seeing it.
    /// </summary>
    public const string P1 = "P1";

    /// <summary>
    /// <b>An agent does not do its job.</b> A trip never completes, a casualty is never delivered, a wreck
    /// is never towed, a driver never gives way. The town runs and is not a town.
    /// </summary>
    public const string P2 = "P2";

    /// <summary>
    /// <b>The town is laid wrong.</b> What the generator owes whatever seed it was given, and the shallow
    /// bar every map is held to: reachable, on the map, off the water, furnished.
    /// </summary>
    public const string P3 = "P3";

    /// <summary>
    /// <b>Something derived from the plan disagrees with it.</b> The graphs, the networks and the registries
    /// an agent reads instead of the plan — a lie here is a town that is right and an agent that cannot see
    /// it.
    /// </summary>
    public const string P4 = "P4";

    /// <summary>
    /// <b>What is drawn is in the wrong place.</b> The ground's triangles, a body's sprite, a mark on the
    /// road. The simulation is right and the picture of it is not.
    /// </summary>
    public const string P5 = "P5";

    /// <summary>
    /// <b>The player cannot do something.</b> The menu, the panels, the camera, the pointer and the orders
    /// given through them.
    /// </summary>
    public const string P6 = "P6";

    /// <summary>
    /// <b>An instrument lies.</b> A probe, a claim table, a caption or a meter reporting something other
    /// than what happened — which is worse than no instrument, and is still not the town being wrong.
    /// </summary>
    public const string P7 = "P7";

    /// <summary>
    /// <b>Authored data is malformed.</b> A catalogue, a sheet or a JSON file that does not say what the
    /// code reading it expects. It fails loudly at start-up rather than quietly in a town.
    /// </summary>
    public const string P8 = "P8";

    /// <summary>
    /// <b>A detail is off.</b> Where a lamp sits on a body, the pitch of a debug comb, the fit of a roof —
    /// things a person notices on a second look and nothing depends on.
    /// </summary>
    public const string P9 = "P9";

    /// <summary>Every rung, most severe first — what <c>TierTests</c> checks a class against.</summary>
    public static readonly string[] Ladder = [P0, P1, P2, P3, P4, P5, P6, P7, P8, P9];
}
