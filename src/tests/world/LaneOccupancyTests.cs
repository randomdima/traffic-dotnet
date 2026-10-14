using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The claims as arithmetic: bodies merged where they overlap, plans taken strongest rung first in one pass, a main
/// piece refused at the first thing of another holder on its way, a secondary claim refused only by a main claim, and a
/// rebuild leaving nothing of the one before.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P1)]
public class LaneOccupancyTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Of("Test"), Config);

    /// <summary>The claims over the fixture's road, with the marks given.</summary>
    static LaneOccupancy Index(WayCrossings? marks = null) => new(TownWays.OfTheRoad(Roads), marks ?? WayCrossings.None);

    /// <summary>Two ways long enough to lay these tests' stretches on, distinct and in no relation to each other.</summary>
    static (int A, int B) TwoWays()
    {
        var found = new List<int>();
        for (var lane = 0; lane < Roads.LaneCount && found.Count < 2; lane++)
        {
            if (Roads.LaneLengthM[lane] >= 60f) found.Add(TownWays.OfTheRoad(Roads).OfRoadLane(lane));
        }

        Assert.Equal(2, found.Count);
        return (found[0], found[1]);
    }

    /// <summary>One mark filed under both its ways as the atlas files it: <paramref name="mine"/> of <paramref name="one"/> over <paramref name="theirs"/> of <paramref name="other"/>.</summary>
    static WayCrossings Mark(int one, (float FromM, float ToM) mine, int other, (float FromM, float ToM) theirs)
    {
        var wayCount = TownWays.OfTheRoad(Roads).Count;
        var offsets = new int[wayCount + 1];
        var sections = new List<CrossedSection>();
        for (var way = 0; way < wayCount; way++)
        {
            if (way == one) sections.Add(new CrossedSection(other, theirs.FromM, theirs.ToM, mine.FromM, mine.ToM));
            if (way == other) sections.Add(new CrossedSection(one, mine.FromM, mine.ToM, theirs.FromM, theirs.ToM));
            offsets[way + 1] = sections.Count;
        }

        return new WayCrossings(offsets, [.. sections]) { MostCrossedByOne = 1 };
    }

    /// <summary>A hold of one piece up a way, its line beginning where the piece does.</summary>
    static int Asked(LaneOccupancy index, int occupant, int way, float fromM, float toM, ClaimPriority rung)
    {
        var hold = index.BeginHold(occupant, LaneRoster.Driving);
        index.Ask(hold, way, fromM, toM, fromM, rung);
        return hold;
    }

    [Fact]
    public void BodiesThatOverlapAreOneClaimHeldByBoth()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 10f, 15f, 1, LaneRoster.Driving);
        index.LayBody(way, 14f, 20f, 2, LaneRoster.Walking);

        var body = Assert.Single(index.Bodies(way).ToArray());
        Assert.Equal((100, 200), (body.FromDm, body.ToDm));
        Assert.Equal(
            [LaneOccupancy.KeyOf(1, LaneRoster.Driving), LaneOccupancy.KeyOf(2, LaneRoster.Walking)],
            index.HoldersOf(body).ToArray());
    }

    /// <summary>One claim may end on the very decimetre the next begins, and the two stay two.</summary>
    [Fact]
    public void BodiesThatOnlyTouchAreTwoClaims()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 10f, 15f, 1, LaneRoster.Driving);
        index.LayBody(way, 15f, 20f, 2, LaneRoster.Driving);

        Assert.Equal(2, index.Bodies(way).Length);
    }

    [Fact]
    public void EveryEdgeIsRoundedUpToTheDecimetre()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 10.01f, 15.04f, 1, LaneRoster.Driving);

        var body = index.Bodies(way)[0];
        Assert.Equal((101, 151), (body.FromDm, body.ToDm));
    }

    [Fact]
    public void AMainPieceIsCutAtTheFirstBodyInFrontOfIt()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 20f, 25f, 9, LaneRoster.Driving);
        var hold = Asked(index, 1, way, 0f, 50f, ClaimPriority.Firm);
        index.Answer();

        var cut = index.CutOf(hold);
        Assert.Equal((20f, ClaimKind.Body), (cut.CutLineM, cut.Kind));
        Assert.Equal((0, 200), (index.Mains(way)[0].FromDm, index.Mains(way)[0].ToDm));
    }

    /// <summary>A body its holder already stands on is where its plan begins past, and never what cuts it.</summary>
    [Fact]
    public void AHoldersOwnBodyCutsNothing()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 0f, 6f, 1, LaneRoster.Driving);
        var hold = Asked(index, 1, way, 4f, 50f, ClaimPriority.Firm);
        index.Answer();

        Assert.False(index.CutOf(hold).Refused);
        Assert.Equal(60, index.Mains(way)[0].FromDm);
    }

    [Fact]
    public void TheStrongerRungKeepsTheGroundThoughAskedSecond()
    {
        var (way, _) = TwoWays();
        var index = Index();
        var weaker = Asked(index, 1, way, 0f, 40f, ClaimPriority.Firm);
        var stronger = Asked(index, 2, way, 10f, 50f, ClaimPriority.FirmStraight);
        index.Answer();

        Assert.False(index.CutOf(stronger).Refused);
        Assert.Equal((10f, ClaimKind.Main, ClaimPriority.FirmStraight), (index.CutOf(weaker).CutLineM, index.CutOf(weaker).Kind, index.CutOf(weaker).Rung));
    }

    [Fact]
    public void OfOneRungTheFirstAskedKeepsTheGround()
    {
        var (way, _) = TwoWays();
        var index = Index();
        var first = Asked(index, 1, way, 10f, 50f, ClaimPriority.Firm);
        var second = Asked(index, 2, way, 0f, 40f, ClaimPriority.Firm);
        index.Answer();

        Assert.False(index.CutOf(first).Refused);
        Assert.Equal(10f, index.CutOf(second).CutLineM);
    }

    /// <summary>Ground a holder can no longer stop short of is taken before any right of way (TER-5e).</summary>
    [Fact]
    public void CommittedGroundIsTakenBeforeACall()
    {
        var (way, _) = TwoWays();
        var index = Index();
        var call = Asked(index, 1, way, 0f, 40f, ClaimPriority.Special);
        var committed = Asked(index, 2, way, 20f, 30f, ClaimPriority.Committed);
        index.Answer();

        Assert.False(index.CutOf(committed).Refused);
        Assert.Equal(20f, index.CutOf(call).CutLineM);
    }

    [Fact]
    public void AMarkPlacesTheOtherWaysSectionAsASecondaryClaim()
    {
        var (a, b) = TwoWays();
        var index = Index(Mark(a, (20f, 24f), b, (30f, 36f)));
        Asked(index, 1, a, 0f, 40f, ClaimPriority.Firm);
        index.Answer();

        var second = Assert.Single(index.Secondaries(b).ToArray());
        Assert.Equal((300, 360, ClaimPriority.Firm), (second.FromDm, second.ToDm, second.Rung));
    }

    /// <summary>
    /// A secondary claim a stronger main claim lies over is refused whole, and the hold is cut where its own main claim
    /// came to the mark — nothing past it laid.
    /// </summary>
    [Fact]
    public void AMainClaimRefusesASecondaryAndTheHoldEndsAtTheMark()
    {
        var (a, b) = TwoWays();
        var index = Index(Mark(a, (20f, 24f), b, (30f, 36f)));
        var crossing = Asked(index, 1, a, 0f, 40f, ClaimPriority.Firm);
        Asked(index, 2, b, 0f, 50f, ClaimPriority.FirmStraight);
        index.Answer();

        Assert.Equal((20f, ClaimKind.Main, b), (index.CutOf(crossing).CutLineM, index.CutOf(crossing).Kind, index.CutOf(crossing).Way));
        Assert.Equal(200, Assert.Single(index.Mains(a).ToArray()).ToDm);
    }

    /// <summary>
    /// <b>Of two holds crossing at a mark, the weaker ends where its own line comes to it</b>, asked first or not: the
    /// mark is filed under both ways, so the weaker's own secondary claim meets the stronger's main claim there.
    /// </summary>
    [Fact]
    public void TheWeakerOfTwoCrossingHoldsEndsWhereItComesToTheMark()
    {
        var (a, b) = TwoWays();
        var index = Index(Mark(a, (20f, 24f), b, (30f, 36f)));
        var weaker = Asked(index, 2, b, 0f, 50f, ClaimPriority.Firm);
        Asked(index, 1, a, 0f, 40f, ClaimPriority.FirmStraight);
        index.Answer();

        Assert.Equal((30f, a), (index.CutOf(weaker).CutLineM, index.CutOf(weaker).Way));
    }

    /// <summary>A secondary claim placed where it stands refuses a weaker main claim at its near edge.</summary>
    [Fact]
    public void ASecondaryClaimRefusesAWeakerMainClaimAtItsNearEdge()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.Place(index.BeginHold(1, LaneRoster.Signal), way, 30f, 36f, ClaimPriority.Signal);
        var weaker = Asked(index, 2, way, 0f, 50f, ClaimPriority.Firm);
        index.Answer();

        Assert.Equal((30f, ClaimKind.Secondary), (index.CutOf(weaker).CutLineM, index.CutOf(weaker).Kind));
    }

    /// <summary>Two secondary claims never meet: of one rung they are merged, each hold whole (TER-5c.1).</summary>
    [Fact]
    public void SecondaryClaimsOfOneRungShareTheirGround()
    {
        var (a, b) = TwoWays();
        var index = Index(Mark(a, (20f, 24f), b, (30f, 36f)));
        var one = Asked(index, 1, a, 0f, 40f, ClaimPriority.Firm);
        var other = Asked(index, 2, a, 22f, 30f, ClaimPriority.Afoot);
        var third = index.BeginHold(3, LaneRoster.Walking);
        index.Place(third, b, 32f, 40f, ClaimPriority.Firm);
        index.Answer();

        Assert.False(index.CutOf(third).Refused);
        var firm = Assert.Single(index.Secondaries(b).ToArray(), claim => claim.Rung == ClaimPriority.Firm);
        Assert.Equal((300, 400, 2), (firm.FromDm, firm.ToDm, (int)firm.Holders));
        _ = (one, other);
    }

    /// <summary>A hold is one stretch (TER-5c.2): refused anywhere, it asks nothing past there.</summary>
    [Fact]
    public void ARefusedHoldAsksNothingFurtherAlongItsLine()
    {
        var (a, b) = TwoWays();
        var index = Index();
        index.LayBody(a, 20f, 25f, 9, LaneRoster.Driving);
        var hold = index.BeginHold(1, LaneRoster.Driving);
        index.Ask(hold, a, 0f, 40f, 0f, ClaimPriority.Firm);
        index.Ask(hold, b, 0f, 40f, 40f, ClaimPriority.Firm);
        index.Answer();

        Assert.Empty(index.Mains(b).ToArray());
    }

    [Fact]
    public void AHoldersOwnClaimsNeverRefuseIt()
    {
        var (way, _) = TwoWays();
        var index = Index();
        var one = Asked(index, 1, way, 0f, 40f, ClaimPriority.Firm);
        var other = Asked(index, 1, way, 10f, 50f, ClaimPriority.Firm);
        index.Answer();

        Assert.False(index.CutOf(one).Refused);
        Assert.False(index.CutOf(other).Refused);
    }

    /// <summary>Ground asked down a way is cut at the top of the first body below where it begins (TER-4c.7).</summary>
    [Fact]
    public void AHoldDownAWayIsCutAtTheNearestBodyBelowIt()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 10f, 14f, 9, LaneRoster.Driving);
        index.LayBody(way, 20f, 24f, 8, LaneRoster.Driving);
        var hold = index.BeginHold(1, LaneRoster.Driving);
        index.AskDown(hold, way, 0f, 40f, 0f, ClaimPriority.Backing);
        index.Answer();

        Assert.Equal((24f, ClaimKind.Body), (index.CutOf(hold).AtM, index.CutOf(hold).Kind));
        Assert.Equal((240, 400), (index.Mains(way)[0].FromDm, index.Mains(way)[0].ToDm));
    }

    /// <summary>What is held is held until its holder lets go of it, and nothing of it after.</summary>
    [Fact]
    public void WhatIsHeldStaysUntilItsHolderLetsGoOfIt()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 10f, 15f, 1, LaneRoster.Driving);
        var hold = Asked(index, 2, way, 20f, 30f, ClaimPriority.Firm);
        index.Answer();

        index.Answer();
        Assert.Single(index.Bodies(way).ToArray());
        Assert.Single(index.Mains(way).ToArray());

        index.LiftBody(1, LaneRoster.Driving);
        index.Release(hold);
        Assert.Empty(index.Bodies(way).ToArray());
        Assert.Empty(index.Mains(way).ToArray());
    }

    /// <summary>A body placed where it has moved to stands there and nowhere else.</summary>
    [Fact]
    public void ABodyPlacedStandsWhereItIsAndNoLongerWhereItWas()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.PlaceBody(1, LaneRoster.Driving, 1, LaneRoster.Driving, [new WayCover(way, 10f, 15f)]);

        index.PlaceBody(1, LaneRoster.Driving, 1, LaneRoster.Driving, [new WayCover(way, 12f, 17f)]);

        var body = Assert.Single(index.Bodies(way).ToArray());
        Assert.Equal((120, 170), (body.FromDm, body.ToDm));
    }

    /// <summary>
    /// A walker held as its own car's ground stands as the car does and beside it, and moving one moves nothing of the
    /// other (PER-29).
    /// </summary>
    [Fact]
    public void TwoBodiesHeldAsOneHolderAreLaidApart()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.PlaceBody(1, LaneRoster.Driving, 1, LaneRoster.Driving, [new WayCover(way, 10f, 15f)]);
        index.PlaceBody(7, LaneRoster.Walking, 1, LaneRoster.Driving, [new WayCover(way, 30f, 31f)]);

        index.PlaceBody(7, LaneRoster.Walking, 1, LaneRoster.Driving, [new WayCover(way, 32f, 33f)]);

        Assert.Equal(
            [(100, 150), (320, 330)], index.Bodies(way).ToArray().Select(body => ((int)body.FromDm, (int)body.ToDm)).ToArray());
    }

    /// <summary>A place a body is put down must be clear of bodies and of ground a holder cannot stop short of, and of nothing else.</summary>
    [Fact]
    public void GroundIsTakenByABodyOrCommittedGroundAndByNoOtherPlan()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 10f, 15f, 1, LaneRoster.Driving);
        Asked(index, 2, way, 20f, 30f, ClaimPriority.Committed);
        Asked(index, 3, way, 40f, 50f, ClaimPriority.Firm);
        index.Answer();

        Assert.Equal(
            (true, true, false),
            (index.IsTaken(way, 12f, 13f), index.IsTaken(way, 25f, 26f), index.IsTaken(way, 45f, 46f)));
    }

    /// <summary>Ground a holder cannot stop short of is taken on the ways its marks reach as well as its own.</summary>
    [Fact]
    public void CommittedGroundOverAMarkTakesTheOtherWaysSection()
    {
        var (a, b) = TwoWays();
        var index = Index(Mark(a, (20f, 24f), b, (30f, 36f)));
        Asked(index, 1, a, 0f, 40f, ClaimPriority.Committed);
        index.Answer();

        Assert.True(index.IsTaken(b, 31f, 32f));
    }

    /// <summary>A pass not yet begun is had whole or not at all (TER-4c.6): refused anywhere, it keeps nothing.</summary>
    [Fact]
    public void AHoldHadWholeKeepsNothingWhereAnyOfItIsRefused()
    {
        var (a, b) = TwoWays();
        var index = Index();
        index.LayBody(b, 10f, 15f, 9, LaneRoster.Driving);
        var whole = index.BeginHold(1, LaneRoster.Driving, whole: true);
        index.Ask(whole, a, 0f, 20f, 0f, ClaimPriority.Firm);
        index.Ask(whole, b, 0f, 20f, 20f, ClaimPriority.Firm);
        var weaker = Asked(index, 2, a, 0f, 20f, ClaimPriority.FirmAcross);
        index.Answer();

        Assert.True(index.CutOf(whole).Refused);
        Assert.False(index.CutOf(weaker).Refused);
    }

    /// <summary>Of one rung, a plan is answered before a hold had whole however late it was asked (TER-4c.6).</summary>
    [Fact]
    public void OfOneRungAPlanIsAnsweredBeforeAHoldHadWhole()
    {
        var (way, _) = TwoWays();
        var index = Index();
        var whole = index.BeginHold(1, LaneRoster.Driving, whole: true);
        index.Ask(whole, way, 0f, 20f, 0f, ClaimPriority.Firm);
        var plan = Asked(index, 2, way, 10f, 30f, ClaimPriority.Firm);
        index.Answer();

        Assert.False(index.CutOf(plan).Refused);
        Assert.True(index.CutOf(whole).Refused);
    }

    [Fact]
    public void AHoldHadWholeIsAskedInOneRun()
    {
        var (a, b) = TwoWays();
        var index = Index();
        var whole = index.BeginHold(1, LaneRoster.Driving, whole: true);
        index.Ask(whole, a, 0f, 20f, 0f, ClaimPriority.Firm);
        Asked(index, 2, b, 0f, 20f, ClaimPriority.Firm);

        Assert.Throws<InvalidOperationException>(() => index.Ask(whole, b, 30f, 40f, 20f, ClaimPriority.Firm));
    }

    /// <summary>Ground swept off its holder's line ends the hold where the holder stops, wherever in it it is refused.</summary>
    [Fact]
    public void ASweptPieceIsCutWhereItsHolderStops()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 20f, 25f, 9, LaneRoster.Driving);
        var hold = index.BeginHold(1, LaneRoster.Driving);
        index.AskSwept(hold, way, 0f, 40f, 3f, ClaimPriority.Committed);
        index.Answer();

        Assert.Equal((3f, 20f), (index.CutOf(hold).CutLineM, index.CutOf(hold).AtM));
    }

    /// <summary>A body is asked after as itself, and never as the run it is merged into.</summary>
    [Fact]
    public void TheFirstBodySeenIsOneBody()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 10f, 15f, 1, LaneRoster.Driving);
        index.LayBody(way, 14f, 20f, 2, LaneRoster.Driving);

        Assert.True(index.FirstBodySeen(way, 0f, 40f, 3, LaneRoster.Driving, out var body));
        Assert.Equal(new BodySeen(way, 10f, 15f), body);
    }

    /// <summary>The instruments name whose claim refused a hold off the place it was refused at.</summary>
    [Fact]
    public void TheClaimARefusalWasMetAtNamesItsHolder()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.LayBody(way, 20f, 25f, 9, LaneRoster.Walking);
        var hold = Asked(index, 1, way, 0f, 50f, ClaimPriority.Firm);
        index.Answer();

        Assert.Equal(LaneOccupancy.KeyOf(9, LaneRoster.Walking), index.HolderAt(index.CutOf(hold)));
    }
}
