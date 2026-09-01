using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;
using Xunit;

using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>The driving exam, asserted</b>: every card of <see cref="ExamCards"/> is staged on the map laid for
/// it, every car is ordered through the crossing its card names, and the verdict on each is the one
/// <c>--bench exam</c> prints — the instrument and the test read one run of one machine.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class JunctionExamTests : IClassFixture<ExamRun>
{
    readonly ExamRun _run;

    public JunctionExamTests(ExamRun run) => _run = run;

    public static TheoryData<int> Cards
    {
        get
        {
            var cards = new TheoryData<int>();
            for (var card = 0; card < ExamCards.Count; card++) cards.Add(card);
            return cards;
        }
    }

    /// <summary>
    /// <b>Every card is driven as its card says</b>: every car staged gets to the place its own card sent
    /// it, and the subject gets there the way the card claims it must.
    /// </summary>
    /// <remarks>
    /// <b>A card carrying a finding is asserted to still fail.</b> What this build does instead is written
    /// on the card, and the day the engine passes that card this test says so — which is what keeps the
    /// findings a set that empties rather than a list nobody re-reads.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Cards))]
    public void EveryCardIsDrivenAsItsCardSays(int card)
    {
        var wrong = _run.Drive.Verdict(card);
        var finding = ExamCards.All[card].Finding;
        if (finding.Length == 0)
        {
            Assert.True(wrong is null, wrong);
            return;
        }

        Assert.True(
            wrong is not null,
            $"{_run.Drive.Name(card)} passes now and the finding written on it is out of date — delete it: {finding}");
    }
}

/// <summary>
/// The exam run, laid once and shared by the class: a town is a second of work and there are
/// thirty-six questions to ask of it.
/// </summary>
/// <remarks>
/// <b>It is driven the way the game drives it</b> — a town, a loop and the watch that stages the cards on
/// it (<see cref="ExamWatch"/>) — so what this asserts is what <c>--bench exam</c> prints and what the
/// panel shows on a run of <c>--map Exam</c>.
/// </remarks>
public sealed class ExamRun : IDisposable
{
    readonly TownWorld _world;

    public ExamRun()
    {
        var config = SimConfig.Shipped();
        _world = new TownWorld(Maps.Plan(ExamPlan.Name, config, BuildingCatalog.Shared.OrdinaryFootprintsM()), config);
        Watch = new ExamWatch(config, _world);

        var loop = new SimLoop<TownWorld>(_world, config);
        for (var tick = 0; tick < ExamDrive.Ticks; tick++)
        {
            loop.Advance();
            Watch.Saw(_world);
        }
    }

    internal ExamWatch Watch { get; }

    internal ExamDrive Drive => Watch.Drive;

    public void Dispose() => _world.Dispose();
}
