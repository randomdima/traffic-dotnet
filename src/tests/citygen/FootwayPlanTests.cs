using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>That the walking exam's map is the one its cards asked for.</b> A card names an arm of a junction
/// and a place along it; the lattice decides what shape that junction actually came out, and a card
/// written for an arm the cell has not got is a walk staged in a field.
/// </summary>
/// <remarks>
/// <b>Cheap and engine-free, which is why it is here rather than in the run.</b> The exam itself would
/// report a body that could not walk anywhere as a card that failed, and it would take a minute of town to
/// say what a page of arithmetic says now — and it would say it about the walker rather than about the
/// card that was mis-written.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class FootwayPlanTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    public static TheoryData<int> Cards
    {
        get
        {
            var cards = new TheoryData<int>();
            for (var card = 0; card < FootwayCards.Count; card++) cards.Add(card);
            return cards;
        }
    }

    /// <summary>
    /// <b>Every place a card names is on an arm that junction has.</b> A cell in the middle of the lattice
    /// is a crossroads and one on the edge is a T, so which arms exist is a fact about where the card was
    /// written and not about what it asked for — the one authoring mistake this map makes possible.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cards))]
    public void EveryCardStandsItsBodiesOnArmsItsOwnJunctionHas(int card)
    {
        var lattice = FootwayLattice.Of(Config).Ground;
        var of = FootwayCards.All[card];

        foreach (var walker in of.Walkers)
        {
            foreach (var stand in (ReadOnlySpan<WalkStand>)[walker.From, walker.To])
            {
                if (of.Stage == ExamStage.Head)
                {
                    // A dead end has exactly one arm, and it is the one running back to the cell the spur
                    // was laid off (TER-5a).
                    var spur = lattice.Spur(card);
                    Assert.True(spur is not null, $"card {card} is staged at a head its cell was never given");
                    Assert.Equal(ExamGround.BackFromTheHead(spur!.Value), stand.Arm);
                    continue;
                }

                Assert.True(
                    lattice.ArmRoad(card, stand.Arm) != ExamGround.NoRoad,
                    $"card {card} ({of.Name}) stands a body on the {stand.Arm} arm, which its cell has not got");
            }
        }
    }

    /// <summary>
    /// <b>A card about a crossing is staged where there is one.</b> Paint belongs to an arm of a junction
    /// or to the middle of a block (TER-6), so a body standing at "the paint" of an arm without one is
    /// standing in the road.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cards))]
    public void EveryCardAboutPaintIsStagedWhereThereIsSome(int card)
    {
        var lattice = FootwayLattice.Of(Config).Ground;
        var of = FootwayCards.All[card];
        if (of.Stage == ExamStage.Head) return;

        foreach (var walker in of.Walkers)
        {
            foreach (var stand in (ReadOnlySpan<WalkStand>)[walker.From, walker.To])
            {
                if (stand.OutM != FootwayCards.AtThePaint && stand.Ground == WalkGround.Pavement) continue;

                Assert.True(
                    lattice.ArmRoad(card, stand.Arm) != ExamGround.NoRoad,
                    $"card {card} ({of.Name}) stands a body at paint on an arm with no road under it");
            }
        }
    }

    /// <summary>
    /// <b>No two bodies are put down on the same ground.</b> Two bodies at one point are two bodies the
    /// solver has to push apart on the first tick, and a card that begins that way is measuring the
    /// separation rather than the walk.
    /// </summary>
    [Fact]
    public void NoTwoBodiesAreStoodUpOnTopOfOneAnother()
    {
        var lattice = FootwayLattice.Of(Config);
        var apartM = Config.PersonDiameterM;
        var places = new List<(int Card, int Walker, System.Numerics.Vector2 AtM)>();

        for (var card = 0; card < FootwayCards.Count; card++)
        {
            for (var walker = 0; walker < FootwayCards.All[card].Walkers.Length; walker++)
            {
                places.Add((card, walker, lattice.StandM(card, walker)));
            }
        }

        for (var one = 0; one < places.Count; one++)
        {
            for (var two = one + 1; two < places.Count; two++)
            {
                Assert.True(
                    (places[one].AtM - places[two].AtM).Length() > apartM,
                    $"card {places[one].Card} body {places[one].Walker} and card {places[two].Card} body "
                    + $"{places[two].Walker} are stood up on one another at {places[one].AtM}");
            }
        }
    }

    /// <summary>
    /// <b>The whole exam is ordered in one tick</b> (CTL-1b). Orders are queued and taken at the top of the
    /// next tick, and the queue is the selection's own size — so a twenty-first body would be a card nobody
    /// ever told anything, wandering the lattice into somebody else's walk.
    /// </summary>
    [Fact]
    public void EveryBodyTheExamStagesFitsInOneTickOfOrders()
    {
        var bodies = FootwayLattice.Of(Config).Walkers;

        Assert.True(
            bodies <= Config.View.SelectionMaxUnits,
            $"the walking exam stands {bodies} bodies up and one tick of orders carries "
            + $"{Config.View.SelectionMaxUnits}");
    }

    /// <summary>
    /// <b>Every kind of claim a card can make is made by a card.</b> The panel's claims are indexed by
    /// <see cref="WalkAsks"/> itself, so a kind nothing asks is a row that can never be answered.
    /// </summary>
    [Fact]
    public void EveryKindOfClaimIsAskedByACard()
    {
        foreach (var asks in Enum.GetValues<WalkAsks>())
        {
            Assert.True(
                Array.Exists(FootwayCards.All.ToArray(), card => card.Asks == asks),
                $"no card asks {asks}, and the claim about it can never be answered");
        }
    }
}
