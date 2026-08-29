using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using Xunit;
using Xunit.Abstractions;

namespace TrafficSimulation.Tests.Bench;

/// <summary>
/// <b>A scenario's own claims, asserted</b>: the tier reads the watch the game and the probes read, so a
/// map cannot keep a claim in one and break it in another.
/// </summary>
/// <remarks>
/// <b>A claim still waiting fails as loudly as a broken one.</b> A test chooses how long it watches, so a
/// claim its run never got round to answering is a run too short to be asserting anything — and a suite
/// that let it pass would go green on a town that never started.
/// </remarks>
internal static class Claims
{
    /// <summary>Every claim kept, with the whole table written into the test's own output either way.</summary>
    public static void AssertKept(ScenarioWatch watch, ITestOutputHelper output)
    {
        output.WriteLine($"{watch.Name} — {watch.Subject}");
        for (var claim = 0; claim < watch.Claims; claim++)
        {
            output.WriteLine($"  {ScenarioReport.Word(watch.Verdict(claim)),-9}{watch.Asks(claim)}: {Says(watch, claim)}");
        }

        for (var reading = 0; reading < watch.Readings; reading++)
        {
            output.WriteLine($"  {"quoted",-9}{watch.Reading(reading)}: {Reads(watch, reading)}");
        }

        for (var claim = 0; claim < watch.Claims; claim++)
        {
            Assert.True(
                watch.Verdict(claim) == ClaimVerdict.Kept,
                $"{watch.Name}: \"{watch.Asks(claim)}\" is {ScenarioReport.Word(watch.Verdict(claim))} — "
                + Says(watch, claim));
        }
    }

    /// <summary>What the watch says about one claim, as a string, which is what an assertion message wants.</summary>
    public static string Says(ScenarioWatch watch, int claim)
    {
        Span<char> text = stackalloc char[240];
        var line = new TextBuffer(text);
        watch.Says(claim, ref line);
        return new string(line.Written);
    }

    public static string Reads(ScenarioWatch watch, int reading)
    {
        Span<char> text = stackalloc char[240];
        var line = new TextBuffer(text);
        watch.Reads(reading, ref line);
        return new string(line.Written);
    }
}
