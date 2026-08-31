using System.Collections.Concurrent;
using TrafficSimulation.App.Hud;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Bench;

/// <summary>
/// <b>The scenario machinery itself</b>: that every map is watched against something, that every scenario
/// map is watched against what it was laid for, that a watch answers every row it lists, and that nothing
/// is kept before the town has run.
/// </summary>
/// <remarks>
/// <b>What the claims come to is not asked here</b> — that is the tier that runs the map, and a minute of
/// every town would be a second suite. This is about the table: a claim nothing can answer, a row that
/// writes nothing, or a scenario map with no claims of its own are all faults of the same kind.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class ScenarioTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    static readonly ConcurrentDictionary<string, TownWorld> AtRest = new();

    /// <summary>
    /// <b>The town before its first tick, stood up once per map.</b> Standing a city up is the whole cost of
    /// every claim about the table, and four of them were each standing their own.
    /// </summary>
    /// <remarks>
    /// <b>Nothing here ticks it, which is what makes it shareable</b> — the watches are the thing under test
    /// and <see cref="Scenarios.For"/> hands out a new set on every call, so one town at rest answers as many
    /// questions about its table as are put to it. A claim that has to watch the ticks go by stands its own.
    /// </remarks>
    static TownWorld Standing(string map) => AtRest.GetOrAdd(map, opened => new TownWorld(Towns.Of(opened), Config));

    /// <summary>
    /// <b>Every town this build opens is watched.</b> A map with no claims at all would open with a panel
    /// saying nothing and exit saying nothing, which is the one state the whole arrangement exists to
    /// prevent.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryMapIsWatchedAgainstSomething(string map)
    {
        var watching = Scenarios.For(Standing(map), Config);

        Assert.NotEmpty(watching);
        Assert.Contains(watching, watch => watch is TownWatch);
        foreach (var watch in watching) Assert.True(watch.Claims > 0, $"{watch.Name} claims nothing");
    }

    /// <summary>
    /// <b>A map laid to measure one thing is watched against that thing.</b> Two maps are the exception and
    /// say so: the fixture map is where every detailed check is staged rather than a map with a question of
    /// its own, and the idle ring is laid to be looked at and measures nothing at all — so what either of
    /// them keeps is what every town keeps.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryScenarioMapCarriesClaimsOfItsOwn(string map)
    {
        var watching = Scenarios.For(Standing(map), Config);
        var ownClaims = Array.Exists(watching, watch => watch is not TownWatch);

        var kind = MapCatalogue.Describe(map).Kind;
        if (kind == MapKind.Place || map == Towns.Fixture || map == IdlePlan.Name)
        {
            Assert.False(ownClaims, $"{map} is not laid to measure one thing but carries claims of its own");
            return;
        }

        Assert.True(ownClaims, $"{map} is a scenario map with nothing but the town's own claims on it");
    }

    /// <summary>
    /// <b>Every row answers.</b> A claim with no verdict, or a row that writes nothing where its figures
    /// should be, is a line on the panel that says less than the space it takes.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryWatchAnswersEveryRowItLists(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var watching = Scenarios.For(world, Config);

        // A few ticks and not none: a figure is a fact about a run, and the first of them is what says the
        // watch can be read at all.
        for (var tick = 0; tick < 30; tick++)
        {
            loop.Advance();
            foreach (var watch in watching) watch.Saw(world);
        }

        foreach (var watch in watching)
        {
            Assert.False(string.IsNullOrWhiteSpace(watch.Name));
            Assert.False(string.IsNullOrWhiteSpace(watch.Subject));

            for (var claim = 0; claim < watch.Claims; claim++)
            {
                Assert.False(string.IsNullOrWhiteSpace(watch.Asks(claim)), $"{watch.Name}: claim {claim} asks nothing");
                Assert.False(
                    string.IsNullOrWhiteSpace(Claims.Says(watch, claim)),
                    $"{watch.Name}: \"{watch.Asks(claim)}\" says nothing about itself");
            }

            for (var reading = 0; reading < watch.Readings; reading++)
            {
                Assert.False(string.IsNullOrWhiteSpace(watch.Reading(reading)));
                Assert.False(
                    string.IsNullOrWhiteSpace(Claims.Reads(watch, reading)),
                    $"{watch.Name}: reading \"{watch.Reading(reading)}\" is blank");
            }
        }
    }

    /// <summary>
    /// <b>Nothing is kept before the town has run.</b> A claim answered off no ticks at all is a claim
    /// about nothing, and a suite or a panel that read it as kept would be green on a town that never
    /// started.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NothingIsKeptBeforeTheFirstTick(string map)
    {
        foreach (var watch in Scenarios.For(Standing(map), Config))
        {
            for (var claim = 0; claim < watch.Claims; claim++)
            {
                Assert.Equal(ClaimVerdict.Waiting, watch.Verdict(claim));
            }
        }
    }

    /// <summary>
    /// <b>Watching a town allocates nothing</b> (rule 2). Every one of these runs inside the ordinary tick
    /// of a run somebody is watching, so a watch that allocated would be the largest allocator in a build
    /// whose whole claim is that the steady state allocates none.
    /// </summary>
    /// <remarks>
    /// <b>What a claim comes to is worked out when it is asked for and not here</b>, which is what lets
    /// this be true of a staging as well as of a counter: the exam composes a card's verdict once, on the
    /// tick that card is decided, and never inside the tick it is watching.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void WatchingATownAllocatesNothing(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var watching = Scenarios.For(world, Config);

        // Warmed first: the arrays a watch keeps are its own and are taken once, and the first tick of a
        // town is where every one-off in it happens.
        for (var tick = 0; tick < 30; tick++)
        {
            loop.Advance();
            foreach (var watch in watching) watch.Saw(world);
        }

        // The town goes on running between the measurements and never inside one: what a tick of the town
        // itself costs is `AllocationGateTests`'s question, and folding it in here would make this test
        // fail for an answer it is not asking about.
        var allocated = 0L;
        for (var tick = 0; tick < 120; tick++)
        {
            loop.Advance();

            var before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var watch in watching) watch.Saw(world);

            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.Equal(0, allocated);
    }

    /// <summary>
    /// <b>The exam claims one thing for every kind of thing a card can ask</b>, and one more about the
    /// findings. Its claims are indexed by <see cref="ExamAsks"/> itself, so a kind added to the enum
    /// without a claim beside it would quietly be answered by the claim above it.
    /// </summary>
    [Fact]
    public void TheExamClaimsOneThingForEveryKindOfCardThereIs()
    {
        var exam = Assert.Single(
            Array.FindAll(Scenarios.For(Standing(ExamPlan.Name), Config), watch => watch is ExamWatch));

        Assert.Equal(Enum.GetValues<ExamAsks>().Length + 1, exam.Claims);
    }

    /// <summary>
    /// <b>The proving grounds are watched by the instrument that measures them</b> and not by a second one
    /// of the watch's own: the panel that draws the shape table and the claims about that table are one
    /// reading of one lap.
    /// </summary>
    [Theory]
    [InlineData(TrackPlan.Name)]
    [InlineData(TrackPlan.DrunkName)]
    [InlineData(TrackPlan.FleetName)]
    public void TheProvingGroundsCarryTheFiguresThePanelDraws(string map)
    {
        var watching = Scenarios.For(Standing(map), Config);

        var figures = Scenarios.FiguresIn(watching);
        Assert.NotNull(figures);
        Assert.Same(figures, Assert.IsAssignableFrom<LapWatch>(watching[0]).Metrics);
    }
}
