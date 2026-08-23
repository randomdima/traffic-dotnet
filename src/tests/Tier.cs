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
/// The four are docs/verification.md's, and the whole point of naming them is that the cheap ones can
/// be run on every edit: the unit tier is four hundred milliseconds and the untiered suite was four
/// minutes. <c>qq tests</c> is what turns a tier into a filter and a build configuration.
/// </para>
/// </remarks>
public static class Tier
{
    /// <summary>The trait key. One key for all four, so a tier can be selected and excluded by name.</summary>
    public const string Key = "Tier";

    /// <summary>
    /// Engine-free arithmetic, and the fixture town where a question needs a place to be asked of.
    /// Milliseconds — nothing here reads a shipped city.
    /// </summary>
    public const string Unit = "Unit";

    /// <summary>
    /// A question asked of a <em>shipped</em> city — Odesa, River or Zebras — whether it is read, laid
    /// out over, or ticked. Seconds, because the city is the size of the question.
    /// </summary>
    public const string Town = "Town";

    /// <summary>
    /// The gates in <c>tests/gates/</c>: what is <em>measured</em> over a whole town rather than
    /// asserted about a value. They are serialised, they want a quiet machine, and they are the two
    /// minutes nobody spends after an edit that could not have moved them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This tier is taken in Debug, and both reasons are worth knowing.</b> The counter
    /// <c>CrossingGateTests</c> reads is <c>[Conditional("DEBUG")]</c>, so a Release run of it compares
    /// zero to zero and passes having measured nothing at all.
    /// </para>
    /// <para>
    /// And <see cref="Gates.AllocationGateTests.AskingTheGroundOfAWholeCityAllocatesNothing"/> fails off
    /// a <em>Release</em> build whenever the gates are run as their own group — 856 bytes on a thread
    /// that has by then allocated eight hundred megabytes — while passing in Debug, passing in Release
    /// run alone, and passing in Release behind the whole suite. Until that is understood, Release is
    /// the configuration that cannot say whether rule 2 holds, which is the reverse of what taking a
    /// figure off a Release build is supposed to buy.
    /// </para>
    /// </remarks>
    public const string Perf = "Perf";

    /// <summary>
    /// The visual tier, whose verdict is an agent's. It costs money and about a minute a scenario, so
    /// it is never part of a run somebody did not ask for by name.
    /// </summary>
    public const string E2E = "E2E";
}
