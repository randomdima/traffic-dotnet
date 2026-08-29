using TrafficSimulation.App.Screen;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>Where one of a scenario's claims stands, read off the run so far.</summary>
internal enum ClaimVerdict : byte
{
    /// <summary>
    /// Nothing the claim is about has happened yet. <b>It is neither answer</b>: a lap nobody has been
    /// round is not a lap driven badly, and a run cut short before its subject arrives has asked the
    /// engine nothing.
    /// </summary>
    Waiting,

    /// <summary>Kept, on everything the run has shown so far.</summary>
    Kept,

    /// <summary>
    /// Broken, as the run stands. <b>A claim about what a town may never do stays broken once it is</b>,
    /// because what answers it is a counter that only ever grows — the deepest overlap, the furthest off a
    /// line, the times somebody was knocked down. A claim about a mean is answered by the mean as it
    /// stands, which is the honest thing to draw on a panel of a run still going.
    /// </summary>
    Broken,
}

/// <summary>
/// <b>VER-11 — a map watched against what it was laid to answer</b>: the claims it makes about itself,
/// each kept or broken off the bodies while the town runs, and the readings that are quoted beside them.
/// </summary>
/// <remarks>
/// <para>
/// <b>One machine, three readers.</b> The same watch answers the panel a player is looking at
/// (<c>ScenarioPanel</c>), the table a headless run prints on its way out
/// (<see cref="ScenarioReport"/>) and the tier that asserts on it — so a claim cannot be kept in one and
/// broken in another. What differs between the three is only how long the town has been watched.
/// </para>
/// <para>
/// <b>A claim fails a run and a reading never does.</b> The distinction is the project's own: what must
/// hold on every map is gated, and what is a fact about one town — the drunks' swerves, how far an
/// articulated pair gets through a dense city — is quoted, because asserting it would be tuning the
/// towns until the instrument could no longer report the thing it was laid to find.
/// </para>
/// <para>
/// <b>Nothing here allocates after construction.</b> It is ticked inside the ordinary loop and read
/// inside the ordinary frame, so both rules apply to it: the figures are counters, and every line it
/// writes goes into the caller's own buffer.
/// </para>
/// </remarks>
/// <param name="name">What this set of claims is about, which is the panel's own section heading.</param>
/// <param name="subject">One line: what the map is laid to answer, or what every town owes.</param>
/// <param name="claims">Each claim, in the order the panel and the table print them.</param>
/// <param name="readings">What is quoted beside them and never gated.</param>
internal abstract class ScenarioWatch(string name, string subject, string[] claims, string[] readings)
{
    public string Name => name;

    public string Subject => subject;

    public int Claims => claims.Length;

    /// <summary>The claim itself, written so that a reader can say whether the town is keeping it.</summary>
    public string Asks(int claim) => claims[claim];

    public int Readings => readings.Length;

    public string Reading(int reading) => readings[reading];

    /// <summary>Where that claim stands, off everything this watch has seen.</summary>
    public abstract ClaimVerdict Verdict(int claim);

    /// <summary>The figures behind the verdict — what actually happened, in the caller's own buffer.</summary>
    public abstract void Says(int claim, ref TextBuffer into);

    /// <summary>And one of the readings, which is a figure and never a verdict.</summary>
    public abstract void Reads(int reading, ref TextBuffer into);

    /// <summary>
    /// One tick of the town, counted. <b>Nothing here judges anything</b>: every verdict above is worked
    /// out from the counters when it is asked for, so a panel drawn every frame and a table printed once
    /// are reading the same run rather than two summaries of it.
    /// </summary>
    public abstract void Saw(TownWorld world);

    public int Kept => Counted(ClaimVerdict.Kept);

    public int Broken => Counted(ClaimVerdict.Broken);

    /// <summary>
    /// How many claims the run has not answered yet, which is what a short run says instead of passing.
    /// <b>Only a broken claim fails a run</b> — never a reading, and never a claim still waiting.
    /// </summary>
    public int Unanswered => Counted(ClaimVerdict.Waiting);

    int Counted(ClaimVerdict wanted)
    {
        var count = 0;
        for (var claim = 0; claim < Claims; claim++)
        {
            if (Verdict(claim) == wanted) count++;
        }

        return count;
    }
}
