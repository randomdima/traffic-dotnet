using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// <b>The walking exam, asserted</b>: every card of <see cref="FootwayCards"/> is staged on the map laid
/// for it, every body is ordered to the place its card names, and the verdict on each is the one
/// <c>--bench footway</c> prints — the instrument and the test read one run of one machine.
/// </summary>
/// <remarks>
/// <b>A card carrying a finding is asserted to still fail.</b> What this build does instead is written on
/// the card, and the day the engine passes that card this test says so — which is what keeps the findings
/// a set that empties rather than a list nobody re-reads.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class WalkingExamTests : IClassFixture<FootwayRun>
{
    readonly FootwayRun _run;

    public WalkingExamTests(FootwayRun run) => _run = run;

    public static TheoryData<int> Cards
    {
        get
        {
            var cards = new TheoryData<int>();
            for (var card = 0; card < FootwayCards.Count; card++) cards.Add(card);
            return cards;
        }
    }

    [Theory]
    [MemberData(nameof(Cards))]
    public void EveryCardIsWalkedAsItsCardSays(int card)
    {
        var wrong = _run.Walk.Verdict(card);
        var finding = FootwayCards.All[card].Finding;
        if (finding.Length == 0)
        {
            Assert.True(wrong is null, wrong);
            return;
        }

        Assert.True(
            wrong is not null,
            $"{_run.Walk.Name(card)} passes now and the finding written on it is out of date — delete it: {finding}");
    }
}

/// <summary>
/// The walking exam's run, laid once and shared by the class: a town is a second of work and there are
/// twenty questions to ask of it.
/// </summary>
/// <remarks>
/// <b>It is walked the way the game walks it</b> — a town, a loop and the watch that stages the cards on
/// it (<see cref="FootwayWatch"/>) — so what this asserts is what <c>--bench footway</c> prints and what
/// the panel shows on a run of <c>--map Footway</c>.
/// </remarks>
public sealed class FootwayRun : IDisposable
{
    readonly TownWorld _world;

    public FootwayRun()
    {
        var config = SimConfig.Shipped();
        _world = new TownWorld(
            Maps.Plan(FootwayPlan.Name, config, BuildingCatalog.Shared.OrdinaryFootprintsM()), config);
        Watch = new FootwayWatch(config, _world);

        var loop = new SimLoop<TownWorld>(_world, config);
        for (var tick = 0; tick < FootwayWalk.Ticks; tick++)
        {
            loop.Advance();
            Watch.Saw(_world);
        }
    }

    internal FootwayWatch Watch { get; }

    internal FootwayWalk Walk => Watch.Walk;

    public void Dispose() => _world.Dispose();
}
