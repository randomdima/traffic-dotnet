using TrafficSimulation.Bench;
using TrafficSimulation.CityGen.Exam;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Bench;

/// <summary>
/// <b>The scenario map, card by card</b>: every scenario of <see cref="ExamCards"/> staged on the map laid for
/// it, and each asserted to go the way it expects of the engine. The verdicts are the ones
/// <c>--bench exam</c> prints — the instrument and the test read one run of one machine.
/// </summary>
/// <remarks>
/// <b>A tier of its own and outside <c>all</c></b> (<see cref="Tier.Exam"/>), as the frames are: it is the
/// whole town driven end to end, run when a change can have moved how traffic meets, and every card passes.
/// </remarks>
[Trait(Tier.Key, Tier.Exam)]
[Trait(Priority.Key, Priority.P2)]
public class ExamTests(ExamFixture exam) : IClassFixture<ExamFixture>
{
    public static TheoryData<int> Cards
    {
        get
        {
            var cards = new TheoryData<int>();
            for (var card = 0; card < ExamCards.All.Length; card++) cards.Add(card);
            return cards;
        }
    }

    /// <summary>
    /// <b>Every card goes as it expects</b>: every car it sends gets where it was sent through the junction it
    /// was staged at, everybody on foot gets where they were sent, nothing touches anything, and the card's own
    /// claims hold. The message is the card, what it expects, and what the town did instead.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cards))]
    public void EveryCardGoesAsItExpects(int card)
    {
        var of = ExamCards.All[card];
        var wrong = exam.Run.Drive.Verdict(card);
        Assert.True(wrong is null, $"{exam.Run.Drive.Name(card)} — {of.Expects} ({of.Rules}): {wrong}");
    }
}

/// <summary>The exam, run once and shared by the class: one town ticked through the whole window answers every card.</summary>
public sealed class ExamFixture : IDisposable
{
    internal ExamRun Run { get; } = new(SimConfig.Shipped());

    public void Dispose() => Run.Dispose();
}
