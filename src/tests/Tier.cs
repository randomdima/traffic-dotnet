namespace TrafficSimulation.Tests;

/// <summary>
/// What a test class costs to answer, and therefore when it is run. Every test class carries exactly
/// one as <c>[Trait(Tier.Key, …)]</c>, and the suite is selected by tier.
/// </summary>
/// <remarks>
/// <para>
/// <b>The cut is by what a class asks its question of, never by which feature it belongs to.</b> One
/// slice's folder holds a microsecond of walker arithmetic beside two seconds of Odesa, so the folders
/// go on mirroring <c>src/</c> and the cost lives in a trait. A tier is not a place.
/// </para>
/// <para>
/// The four are docs/verification.md's, and the whole point of naming them is that the cheap ones can be
/// run on every edit while the dear ones are asked for by name. <c>qq tests</c> is what turns a tier into
/// a filter and a build configuration; what each one costs is CLAUDE.md's table and is not restated here.
/// </para>
/// </remarks>
public static class Tier
{
    /// <summary>The trait key. One key for all four, so a tier can be selected and excluded by name.</summary>
    public const string Key = "Tier";

    /// <summary>
    /// Engine-free arithmetic, and the fixture town where a question needs a place to be asked of.
    /// Nothing here reads a shipped city, which is what makes it the tier run after every edit.
    /// </summary>
    public const string Unit = "Unit";

    /// <summary>
    /// A question asked of a <em>shipped</em> city — Odesa, River or Zebras — whether it is read, laid
    /// out over, or ticked. Seconds, because the city is the size of the question.
    /// </summary>
    public const string Town = "Town";

    /// <summary>
    /// The gates in <c>tests/gates/</c>: what is <em>measured</em> over a whole town rather than
    /// asserted about a value. They are serialised, they want a quiet machine, and they are not run
    /// after an edit that could not have moved them.
    /// </summary>
    /// <remarks>
    /// <b>Taken in Release but for one class</b>, which <c>qq tests</c> runs as a take of its own: the
    /// counter <c>CrossingGateTests</c> reads is <c>[Conditional("DEBUG")]</c>, so a Release run of it
    /// compares zero to zero and passes having measured nothing at all. Nothing else here needs the
    /// configuration — allocation is counted by <c>GC.GetAllocatedBytesForCurrentThread</c>, which counts
    /// the same bytes either way, and Release is four times faster at reaching the town the figure is
    /// taken over.
    /// </remarks>
    public const string Perf = "Perf";

    /// <summary>
    /// The visual tier, whose verdict is an agent's. It costs money and about a minute a scenario, so
    /// it is never part of a run somebody did not ask for by name.
    /// </summary>
    public const string E2E = "E2E";
}
