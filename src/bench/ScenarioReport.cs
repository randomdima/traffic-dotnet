using TrafficSimulation.App.Screen;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What a scenario came to, written out where a script can read it</b>: every claim with the verdict
/// the run reached, the readings quoted under them, and one last line that says whether the map kept
/// what it claims.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the panel's table with no window under it</b> — the same
/// <see cref="ScenarioWatch"/> answers both, so a headless run and a run somebody is watching cannot
/// disagree about a claim.
/// </para>
/// <para>
/// <b>The last line is the one a caller reads</b>, and the exit code is taken off it: a broken claim
/// fails the run, and a claim the run was too short to answer is printed as unanswered rather than
/// counted as kept. A reading fails nothing at all.
/// </para>
/// </remarks>
internal static class ScenarioReport
{
    /// <summary>Where the claim itself starts, and how wide it is before the figures begin.</summary>
    const int VerdictWidth = 10;

    const int ClaimWidth = 72;

    /// <summary>
    /// The whole report, and whether the map kept what it claims. <b>Every claim is printed</b>, kept
    /// ones included: a table that showed only the failures could not be told from a table nothing was
    /// asked of.
    /// </summary>
    public static bool Print(string map, ReadOnlySpan<ScenarioWatch> watching, float watchedS)
    {
        if (watching.Length == 0)
        {
            Console.WriteLine($"scenario — {map} makes no claim about itself, so there is nothing to keep.");
            return true;
        }

        Console.WriteLine();
        Console.WriteLine($"scenario — {map}, {watchedS:F0} s watched");

        Span<char> text = stackalloc char[200];
        var kept = 0;
        var broken = 0;
        var unanswered = 0;

        foreach (var watch in watching)
        {
            Console.WriteLine($"{watch.Name,-18} {watch.Subject}");
            kept += watch.Kept;
            broken += watch.Broken;
            unanswered += watch.Unanswered;

            for (var claim = 0; claim < watch.Claims; claim++)
            {
                var line = new TextBuffer(text);
                line.Add("  ");
                line.Add(Word(watch.Verdict(claim)));
                line.PadTo(2 + VerdictWidth);
                line.Add(watch.Asks(claim));
                line.PadTo(2 + VerdictWidth + ClaimWidth);
                watch.Says(claim, ref line);
                Console.Out.WriteLine(line.Written);
            }

            for (var reading = 0; reading < watch.Readings; reading++)
            {
                var line = new TextBuffer(text);
                line.Add("  quoted");
                line.PadTo(2 + VerdictWidth);
                line.Add(watch.Reading(reading));
                line.PadTo(2 + VerdictWidth + ClaimWidth);
                watch.Reads(reading, ref line);
                Console.Out.WriteLine(line.Written);
            }
        }

        Console.WriteLine(
            $"scenario {map} — {kept} claim(s) kept, {broken} broken, {unanswered} unanswered: " +
            (broken > 0 ? "FAILED" : "PASSED"));
        return broken == 0;
    }

    /// <summary>
    /// The verdict as the one word a reader scans for. <b>A broken claim shouts and the other two do
    /// not</b>: a table read down its left edge should have exactly one thing in it that catches the eye.
    /// </summary>
    public static string Word(ClaimVerdict verdict) => verdict switch
    {
        ClaimVerdict.Kept => "kept",
        ClaimVerdict.Broken => "BROKEN",
        _ => "waiting",
    };
}
