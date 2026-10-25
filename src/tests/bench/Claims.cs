using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;

namespace TrafficSimulation.Tests.Bench;

/// <summary>
/// <b>What a scenario's watch says, as strings</b> — what an assertion message or a printability check wants,
/// read off the same watch the game and the probes read.
/// </summary>
internal static class Claims
{
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
