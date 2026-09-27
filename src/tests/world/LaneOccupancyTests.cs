using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The reservations as arithmetic: bodies go in and are read back nearest first, a main claim is settled on
/// its own way against main and secondary claims alike, and a rebuild leaves nothing of the tick before it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P1)]
public class LaneOccupancyTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Of("Test"), Config);

    /// <summary>The reservations over the fixture's road, with the marks given and room for a handful of each.</summary>
    static LaneOccupancy Index(WayCrossings? marks = null, int mostSlots = 32, int mostHolds = 8)
    {
        var ways = TownWays.OfTheRoad(Roads);
        return new LaneOccupancy(ways, mostSlots, mostHolds, marks ?? WayCrossings.None);
    }

    /// <summary>Three ways long enough to lay these tests' stretches on, distinct and in no relation to each other.</summary>
    static (int A, int B, int C) ThreeWays()
    {
        var found = new List<int>();
        for (var lane = 0; lane < Roads.LaneCount && found.Count < 3; lane++)
        {
            if (Roads.LaneLengthM[lane] >= 60f) found.Add(TownWays.OfTheRoad(Roads).OfRoadLane(lane));
        }

        Assert.Equal(3, found.Count);
        return (found[0], found[1], found[2]);
    }

    /// <summary>
    /// A table of marks over the fixture's ways, each pair filed under both ways as the atlas files it —
    /// <paramref name="mine"/> of <paramref name="one"/> over <paramref name="theirs"/> of <paramref name="other"/>.
    /// </summary>
    static WayCrossings Marks(params (int One, (float FromM, float ToM) Mine, int Other, (float FromM, float ToM) Theirs)[] pairs)
    {
        var filed = new List<CrossedSection>[TownWays.OfTheRoad(Roads).Count];
        foreach (var (one, mine, other, theirs) in pairs)
        {
            (filed[one] ??= []).Add(new CrossedSection(other, theirs.FromM, theirs.ToM, mine.FromM, mine.ToM));
            (filed[other] ??= []).Add(new CrossedSection(one, mine.FromM, mine.ToM, theirs.FromM, theirs.ToM));
        }

        return Table(filed);
    }

    /// <summary>
    /// <b>One mark filed under <paramref name="one"/> and not under <paramref name="other"/></b> — a table the
    /// atlas never lays, for asking what the reservations read of a way besides the one asked about.
    /// </summary>
    static WayCrossings FiledUnderOne(int one, (float FromM, float ToM) mine, int other, (float FromM, float ToM) theirs)
    {
        var filed = new List<CrossedSection>[TownWays.OfTheRoad(Roads).Count];
        filed[one] = [new CrossedSection(other, theirs.FromM, theirs.ToM, mine.FromM, mine.ToM)];
        return Table(filed);
    }

    static WayCrossings Table(List<CrossedSection>?[] filed)
    {
        var wayCount = filed.Length;
        var offsets = new int[wayCount + 1];
        for (var way = 0; way < wayCount; way++) offsets[way + 1] = offsets[way] + (filed[way]?.Count ?? 0);

        var sections = new CrossedSection[offsets[wayCount]];
        for (var way = 0; way < wayCount; way++)
        {
            if (filed[way] is not { } marks) continue;

            marks.Sort(static (first, second) => first.MineFromM.CompareTo(second.MineFromM));
            marks.CopyTo(sections, offsets[way]);
        }

        return new WayCrossings(offsets, sections) { MostCrossedByOne = 2 };
    }

    /// <summary>One piece a holder asks for, from <paramref name="fromM"/> of a way that begins its line.</summary>
    static PlannedAsk Ask(
        int hold, int occupant, ClaimPriority rung, float fromM, float aheadM = 0f,
        float committedToM = float.NegativeInfinity, bool held = false, LaneRoster of = LaneRoster.Driving) =>
        new(hold, occupant, of, rung, fromM, LineFromM: fromM, AheadM: aheadM, CommittedToM: committedToM,
            AlongMps: 5f, Held: held);

    /// <summary>A whole hold of one piece: answered, laid over what the answer gave, and finished.</summary>
    static float Plan(LaneOccupancy index, int way, float toM, Func<int, PlannedAsk> ask)
    {
        var hold = index.BeginHold(standingMarginM: 0f);
        var asked = ask(hold);
        var reachM = index.Reach(asked, way, toM, asked.FromM, out var cutBy);
        index.Take(asked, way, reachM);
        index.EndHold(hold, reachM < toM ? reachM : float.PositiveInfinity, 0f, cutBy);
        return reachM;
    }

    static float EndsAtM(LaneOccupancy index, int hold) => index.HoldEndsAtM(hold, out _, out _);

    /// <summary>The order bodies go in is not the order they are read back in — the near edge is.</summary>
    [Fact]
    public void TheNearestBodyInFrontIsTheOneWithTheLeastNearEdge()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 40f, 44f, 3f, 7, LaneRoster.Driving, onItsLine: true);
        index.LayBody(way, 12f, 16f, 0f, 3, LaneRoster.Driving, onItsLine: true);
        index.LayBody(way, 25f, 29f, 1f, 5, LaneRoster.Driving, onItsLine: false);

        Assert.True(index.AheadBody(way, 0f, 60f, excluding: LaneOccupancy.Nobody, out var found));
        Assert.Equal(3, found.Occupant);

        Assert.True(index.AheadBody(way, 20f, 60f, LaneOccupancy.Nobody, out found));
        Assert.Equal(5, found.Occupant);
        Assert.False(index.AheadBody(way, 50f, 60f, LaneOccupancy.Nobody, out _));
    }

    /// <summary>A body never finds itself in front of itself, which is what the exclusion is for.</summary>
    [Fact]
    public void TheAskerIsNeverWhatIsInFrontOfIt()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 10f, 14f, 0f, 1, LaneRoster.Driving, onItsLine: true);
        index.LayBody(way, 30f, 34f, 0f, 2, LaneRoster.Driving, onItsLine: true);

        Assert.True(index.AheadBody(way, 0f, 60f, excluding: 1, out var found));
        Assert.Equal(2, found.Occupant);
    }

    /// <summary>
    /// <b>An occupant is named with its roster</b>: car 4 and walker 4 are two bodies, and excluding one of
    /// them is not excluding the other.
    /// </summary>
    [Fact]
    public void ABodyIsExcludedOnlyUnderItsOwnRoster()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 20f, 21f, 0f, 4, LaneRoster.Walking, onItsLine: false);

        Assert.True(index.AheadBody(way, 0f, 60f, excluding: 4, out var found));
        Assert.Equal(LaneRoster.Walking, found.Of);
        Assert.False(index.AheadBody(way, 0f, 60f, excluding: 4, out _, LaneRoster.Walking));
    }

    /// <summary>
    /// <b>Bodies are never compared</b> (TER-4c.2): two of them over one metre are both laid, because the
    /// physical layer is a record of what is there and not of what anybody was allowed.
    /// </summary>
    [Fact]
    public void TwoBodiesMayLieOverOneMetre()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 10f, 15f, 0f, 1, LaneRoster.Driving, onItsLine: true);
        index.LayBody(way, 12f, 18f, 0f, 2, LaneRoster.Driving, onItsLine: false);

        Span<LaneClaim> bodies = stackalloc LaneClaim[4];
        Assert.Equal(2, index.CopyBodiesTo(way, bodies));
    }

    /// <summary>
    /// <b>One body is one stretch of one way</b> (TER-5c.2): laid twice under the same name it grows to cover
    /// both, and it is on its line where either laying said so.
    /// </summary>
    [Fact]
    public void ABodyLaidTwiceOnOneWayGrowsToCoverBoth()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 10f, 12f, 0f, 1, LaneRoster.Driving, onItsLine: false);
        index.LayBody(way, 11f, 16f, 0f, 1, LaneRoster.Driving, onItsLine: true);

        Span<LaneClaim> bodies = stackalloc LaneClaim[4];
        Assert.Equal(1, index.CopyBodiesTo(way, bodies));
        Assert.Equal((10f, 16f, true), (bodies[0].FromM, bodies[0].ToM, bodies[0].OnItsLine));
    }

    /// <summary><b>A hold is cut at the near edge of the first body in front of it</b> (TER-4c.1), and says which body that was.</summary>
    [Fact]
    public void AHoldIsCutAtTheFirstBodyInFrontOfIt()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 30f, 34f, 0f, 2, LaneRoster.Driving, onItsLine: true);
        index.LayBody(way, 20f, 24f, 0f, 3, LaneRoster.Walking, onItsLine: false);

        var hold = index.BeginHold(0f);
        var reachM = index.Reach(Ask(hold, 1, ClaimPriority.Special, 5f), way, 50f, 5f, out var cutBy);

        Assert.Equal(20f, reachM);
        Assert.Equal((3, LaneRoster.Walking, true), (cutBy.Occupant, cutBy.Of, cutBy.HasBody));
    }

    /// <summary>
    /// <b>A body the holder's own already reaches past cuts nothing</b>: it is beside or behind the holder,
    /// and a plan is laid in front of a body and never through the one next to it.
    /// </summary>
    [Fact]
    public void ABodyBesideTheHolderCutsNothing()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 8f, 12f, 0f, 2, LaneRoster.Driving, onItsLine: false);

        var hold = index.BeginHold(0f);
        Assert.Equal(50f, index.Reach(Ask(hold, 1, ClaimPriority.Firm, 12f), way, 50f, standsToM: 12f, out var cutBy));
        Assert.False(cutBy.Found);
    }

    /// <summary>
    /// <b>A stronger rung keeps the ground, and the weaker hold is cut back to where the two met</b>
    /// (TER-5e) — its end on its own line is the metre the stronger one took from.
    /// </summary>
    [Fact]
    public void AStrongerRungTakesTheGroundAndCutsTheWeakerWhereTheyMet()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        var weaker = index.BeginHold(0f);
        index.Take(Ask(weaker, 1, ClaimPriority.FirmAcross, 0f), way, 40f);
        index.EndHold(weaker, float.PositiveInfinity, 0f, LaneClaim.Nothing);

        Assert.Equal(50f, Plan(index, way, 50f, hold => Ask(hold, 2, ClaimPriority.FirmStraight, 25f)));
        Assert.Equal(25f, EndsAtM(index, weaker));
    }

    /// <summary>And the weaker of the two, asking second, is answered short of the stronger one's near edge.</summary>
    [Fact]
    public void AWeakerRungIsAnsweredShortOfAStrongerOne()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        Plan(index, way, 50f, hold => Ask(hold, 2, ClaimPriority.FirmStraight, 25f));

        var weaker = index.BeginHold(0f);
        var reachM = index.Reach(Ask(weaker, 1, ClaimPriority.FirmAcross, 0f), way, 40f, 0f, out var cutBy);
        Assert.Equal(25f, reachM);
        Assert.Equal(2, cutBy.Occupant);
    }

    /// <summary>
    /// <b>Which of two holds keeps the ground does not turn on which was laid first</b> (TER-4c.3): the
    /// comparison is symmetric, so the same pair laid in either order ends the same way.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheOrderTwoHoldsAreLaidInDecidesNothing(bool strongerFirst)
    {
        var (way, _, _) = ThreeWays();
        var index = Index();
        index.Begin();

        var stronger = (Func<int, PlannedAsk>)(hold => Ask(hold, 2, ClaimPriority.Crossing, 25f));
        var weaker = (Func<int, PlannedAsk>)(hold => Ask(hold, 1, ClaimPriority.Firm, 0f));
        if (strongerFirst)
        {
            Plan(index, way, 50f, stronger);
            Plan(index, way, 40f, weaker);
        }
        else
        {
            Plan(index, way, 40f, weaker);
            Plan(index, way, 50f, stronger);
        }

        Assert.Equal(25f, index.PlannedToM(way, 0f, occupant: 1, LaneRoster.Driving));
        Assert.Equal(50f, index.PlannedToM(way, 25f, occupant: 2, LaneRoster.Driving));
    }

    /// <summary><b>Ground its holder can no longer stop short of beats every rung</b> (TER-5e), a call included.</summary>
    [Fact]
    public void CommittedGroundBeatsEveryRung()
    {
        var committed = Ask(0, 1, ClaimPriority.FirmAcross, 20f, aheadM: 10f, committedToM: 30f);
        var call = new LaneClaim(20f, 40f, 5f, 2, ClaimPriority.Special, AheadM: 1f);

        Assert.True(LaneOccupancy.Beats(committed, 20f, false, call, false));
    }

    /// <summary>
    /// <b>Of two holders that can no longer stop, the one that gets there first keeps the ground</b> —
    /// whatever either one's rung, and whichever was given the box before: both are going in, and the one
    /// further off is the one with road left to brake on.
    /// </summary>
    [Fact]
    public void OfTwoCommittedHoldersTheNearerKeepsTheGround()
    {
        var nearer = Ask(0, 1, ClaimPriority.FirmAcross, 20f, aheadM: 3f, committedToM: 30f);
        var further = new LaneClaim(
            20f, 40f, 5f, 2, ClaimPriority.FirmStraight, AheadM: 12f, CommittedToM: 30f, Held: true);

        Assert.True(LaneOccupancy.Beats(nearer, 20f, false, further, false));
    }

    /// <summary>
    /// <b>A holder whose body is already on the ground keeps it</b> against a higher rung: a plan over metres
    /// somebody stands on cannot be driven until that body leaves them.
    /// </summary>
    [Fact]
    public void AHolderStandingOnTheGroundKeepsItAgainstAHigherRung()
    {
        var standing = Ask(0, 1, ClaimPriority.FirmAcross, 20f, aheadM: 0f);
        var straight = new LaneClaim(20f, 40f, 5f, 2, ClaimPriority.FirmStraight, AheadM: 1f);

        Assert.True(LaneOccupancy.Beats(standing, 20f, askStands: true, straight, otherStands: false));
        Assert.False(LaneOccupancy.Beats(standing, 20f, askStands: false, straight, otherStands: false));
    }

    /// <summary>
    /// <b>Of two equal movements, the one given the box last time keeps it</b> — even against one that has
    /// come nearer since, so a box does not change hands under a car on its way into it.
    /// </summary>
    [Fact]
    public void ABoxAlreadyGivenStaysWithItsHolderAgainstAnEqualOneNearer()
    {
        var given = Ask(0, 1, ClaimPriority.Firm, 20f, aheadM: 15f, held: true);
        var nearer = new LaneClaim(20f, 40f, 5f, 2, ClaimPriority.Firm, AheadM: 2f);

        Assert.True(LaneOccupancy.Beats(given, 20f, false, nearer, false));
    }

    /// <summary>And with nothing else between them, whoever has less of its own line to cover gets there first.</summary>
    [Fact]
    public void OfTwoEqualHoldersTheNearerKeepsTheGround()
    {
        var nearer = Ask(0, 1, ClaimPriority.Firm, 20f, aheadM: 2f);
        var further = new LaneClaim(20f, 40f, 5f, 2, ClaimPriority.Firm, AheadM: 15f);

        Assert.True(LaneOccupancy.Beats(nearer, 20f, false, further, false));
    }

    /// <summary>
    /// <b>The comparison is total and antisymmetric</b>: of any two different holders meeting on one metre,
    /// exactly one keeps it — over every combination of the terms it is made on.
    /// </summary>
    [Fact]
    public void OfAnyTwoHoldersExactlyOneKeepsTheGround()
    {
        ClaimPriority[] rungs = [ClaimPriority.Special, ClaimPriority.Crossing, ClaimPriority.FirmStraight, ClaimPriority.FirmAcross];
        float[] aheads = [0f, 4f];
        bool[] flags = [false, true];

        var terms = new List<(ClaimPriority Rung, float AheadM, bool Committed, bool Held, bool Stands)>();
        foreach (var rung in rungs)
        foreach (var aheadM in aheads)
        foreach (var committed in flags)
        foreach (var held in flags)
        foreach (var stands in flags) terms.Add((rung, aheadM, committed, held, stands));

        foreach (var one in terms)
        {
            foreach (var other in terms)
            {
                var ask = Ask(0, 1, one.Rung, 20f, one.AheadM, one.Committed ? 30f : float.NegativeInfinity, one.Held);
                var claim = new LaneClaim(
                    20f, 40f, 5f, 2, other.Rung, AheadM: other.AheadM,
                    CommittedToM: other.Committed ? 30f : float.NegativeInfinity, Held: other.Held);
                var flipped = Ask(0, 2, other.Rung, 20f, other.AheadM, other.Committed ? 30f : float.NegativeInfinity, other.Held);
                var back = new LaneClaim(
                    20f, 40f, 5f, 1, one.Rung, AheadM: one.AheadM,
                    CommittedToM: one.Committed ? 30f : float.NegativeInfinity, Held: one.Held);

                Assert.NotEqual(
                    LaneOccupancy.Beats(ask, 20f, one.Stands, claim, other.Stands),
                    LaneOccupancy.Beats(flipped, 20f, other.Stands, back, one.Stands));
            }
        }
    }

    /// <summary>
    /// <b>A main claim over a mark places the secondary claim whole</b> (TER-5c.1): the other way carries the
    /// whole of the section the mark names, under the same holder, as a secondary claim.
    /// </summary>
    [Fact]
    public void AMainClaimOverAMarkPlacesTheSecondaryClaimWhole()
    {
        var (a, b, _) = ThreeWays();
        var index = Index(Marks((a, (20f, 24f), b, (30f, 35f))));

        index.Begin();
        Plan(index, a, 50f, hold => Ask(hold, 1, ClaimPriority.Firm, 0f));

        Span<LaneClaim> planned = stackalloc LaneClaim[4];
        Assert.Equal(1, index.CopyPlannedTo(b, planned));
        Assert.Equal((30f, 35f, 1, true), (planned[0].FromM, planned[0].ToM, planned[0].Occupant, planned[0].Secondary));
    }

    /// <summary>
    /// <b>A main claim meeting a stronger secondary claim on its own way is answered where that begins</b> —
    /// a car in a turn stopping at the ground the oncoming straight crosses it at, however little of the
    /// straight's own way it would have shared.
    /// </summary>
    [Fact]
    public void AMainClaimIsAnsweredAtAStrongerSecondaryClaim()
    {
        var (a, b, _) = ThreeWays();
        var index = Index(Marks((a, (20f, 24f), b, (30f, 35f))));

        index.Begin();
        Plan(index, b, 50f, hold => Ask(hold, 2, ClaimPriority.FirmStraight, 34f));

        var hold = index.BeginHold(0f);
        var reachM = index.Reach(Ask(hold, 1, ClaimPriority.FirmAcross, 0f), a, 50f, 0f, out var cutBy);
        Assert.Equal(20f, reachM);
        Assert.Equal((2, true), (cutBy.Occupant, cutBy.Secondary));
    }

    /// <summary>
    /// <b>And a stronger main claim taking a secondary claim cuts its holder at its own side of the mark</b>,
    /// the metre its main claim placed the secondary one from.
    /// </summary>
    [Fact]
    public void AHoldWhoseSecondaryClaimIsTakenIsCutAtTheMark()
    {
        var (a, b, _) = ThreeWays();
        var index = Index(Marks((a, (20f, 24f), b, (30f, 35f))));

        index.Begin();
        var weaker = index.BeginHold(0f);
        index.Take(Ask(weaker, 1, ClaimPriority.FirmAcross, 0f), a, 50f);
        index.EndHold(weaker, float.PositiveInfinity, 0f, LaneClaim.Nothing);

        Assert.Equal(50f, Plan(index, b, 50f, hold => Ask(hold, 2, ClaimPriority.FirmStraight, 34f)));
        Assert.Equal(20f, EndsAtM(index, weaker));
        Assert.Equal(20f, index.PlannedToM(a, 0f, occupant: 1, LaneRoster.Driving));
    }

    /// <summary>
    /// <b>Two secondary claims on one way do not meet</b>: two holders whose ground each lies over a third way
    /// meet on their own ways where they meet at all — two cars through one box whose turns cross a third
    /// movement both go, and neither is refused ground that neither of them drives.
    /// </summary>
    [Fact]
    public void TwoSecondaryClaimsOnOneWayDoNotMeet()
    {
        var (a, b, c) = ThreeWays();
        var index = Index(Marks((a, (20f, 24f), b, (30f, 35f)), (c, (10f, 14f), b, (32f, 37f))));

        index.Begin();
        Assert.Equal(50f, Plan(index, a, 50f, hold => Ask(hold, 1, ClaimPriority.Firm, 0f)));
        Assert.Equal(50f, Plan(index, c, 50f, hold => Ask(hold, 2, ClaimPriority.FirmStraight, 0f)));

        Span<LaneClaim> planned = stackalloc LaneClaim[4];
        Assert.Equal(2, index.CopyPlannedTo(b, planned));
    }

    /// <summary>
    /// <b>A main claim is answered off its own way alone</b> (TER-5c.1): ground held across a mark reaches it
    /// only as the secondary claim placed back on its own way. Filed from one side only, the mark carries none
    /// back, and the stronger main claim over the far side cuts nothing.
    /// </summary>
    [Fact]
    public void AMainClaimIsAnsweredOffItsOwnWayAlone()
    {
        var (a, b, _) = ThreeWays();
        var index = Index(FiledUnderOne(a, (20f, 24f), b, (30f, 35f)));

        index.Begin();
        Plan(index, b, 50f, hold => Ask(hold, 2, ClaimPriority.FirmStraight, 30f));

        var hold = index.BeginHold(0f);
        Assert.Equal(50f, index.Reach(Ask(hold, 1, ClaimPriority.FirmAcross, 0f), a, 50f, 0f, out var cutBy));
        Assert.False(cutBy.Found);
    }

    /// <summary>
    /// <b>And a secondary claim placed cuts nothing where it lies</b>: what it meets there is weighed on the
    /// main claim's way, through the secondary claim the other side places back — never on the way it is placed
    /// on.
    /// </summary>
    [Fact]
    public void ASecondaryClaimPlacedCutsNothingWhereItLies()
    {
        var (a, b, _) = ThreeWays();
        var index = Index(FiledUnderOne(a, (20f, 24f), b, (30f, 35f)));

        index.Begin();
        var weaker = index.BeginHold(0f);
        index.Take(Ask(weaker, 2, ClaimPriority.FirmAcross, 30f), b, 50f);
        index.EndHold(weaker, float.PositiveInfinity, 0f, LaneClaim.Nothing);

        Plan(index, a, 50f, hold => Ask(hold, 1, ClaimPriority.FirmStraight, 0f));
        Assert.Equal(50f, index.PlannedToM(b, 30f, occupant: 2, LaneRoster.Driving));
    }

    /// <summary>
    /// <b>A hold is one stretch</b> (TER-5c.2): cut on its first way, it gives up everything past the cut —
    /// its main claims on the ways after and the secondary claims those had placed.
    /// </summary>
    [Fact]
    public void AHoldCutGivesUpEverythingPastTheCut()
    {
        var (a, b, c) = ThreeWays();
        var index = Index(Marks((b, (5f, 8f), c, (40f, 44f))));

        index.Begin();
        var cut = index.BeginHold(0f);
        index.Take(new PlannedAsk(cut, 1, LaneRoster.Driving, ClaimPriority.Firm, 30f, 0f, 0f, float.NegativeInfinity, 5f), a, 60f);
        index.Take(new PlannedAsk(cut, 1, LaneRoster.Driving, ClaimPriority.Firm, 0f, 30f, 30f, float.NegativeInfinity, 5f), b, 20f);
        index.EndHold(cut, float.PositiveInfinity, 0f, LaneClaim.Nothing);

        Plan(index, a, 60f, hold => Ask(hold, 2, ClaimPriority.Special, 45f, aheadM: 0f));

        Assert.Equal(15f, EndsAtM(index, cut));
        Span<LaneClaim> planned = stackalloc LaneClaim[4];
        Assert.Equal(0, index.CopyPlannedTo(b, planned));
        Assert.Equal(0, index.CopyPlannedTo(c, planned));
    }

    /// <summary>
    /// <b>A hold's own ground is never an answer to it</b> — its main claims or its secondary claims —
    /// so an answer read while it is laid is the answer it would get taken up.
    /// </summary>
    [Fact]
    public void AHoldsOwnGroundIsNoAnswerToIt()
    {
        var (a, b, _) = ThreeWays();
        var index = Index(Marks((a, (20f, 24f), b, (30f, 35f))));

        index.Begin();
        var hold = index.BeginHold(0f);
        index.Take(Ask(hold, 1, ClaimPriority.Firm, 0f), a, 50f);
        index.EndHold(hold, float.PositiveInfinity, 0f, LaneClaim.Nothing);

        Assert.Equal(50f, index.Reach(Ask(hold, 1, ClaimPriority.Firm, 0f), a, 50f, 0f, out _));
    }

    /// <summary>
    /// <b>A hold reopened holds nothing and was answered nothing</b>: its main and secondary claims come off
    /// the ways, and what cut it is forgotten, until it is laid again.
    /// </summary>
    [Fact]
    public void AReopenedHoldHoldsNothingUntilItIsLaidAgain()
    {
        var (a, b, _) = ThreeWays();
        var index = Index(Marks((a, (20f, 24f), b, (30f, 35f))));

        index.Begin();
        index.LayBody(a, 40f, 44f, 0f, 9, LaneRoster.Driving, onItsLine: true);
        var hold = index.BeginHold(0f);
        var reachM = index.Reach(Ask(hold, 1, ClaimPriority.Firm, 0f), a, 50f, 0f, out var cutBy);
        index.Take(Ask(hold, 1, ClaimPriority.Firm, 0f), a, reachM);
        index.EndHold(hold, reachM, 0f, cutBy);

        index.ReopenHold(hold);

        Span<LaneClaim> planned = stackalloc LaneClaim[4];
        Assert.Equal((0, 0), (index.CopyPlannedTo(a, planned), index.CopyPlannedTo(b, planned)));
        Assert.Equal(float.PositiveInfinity, EndsAtM(index, hold));
    }

    /// <summary>
    /// <b>A hold laid again is cut like any other</b>: a stronger hold takes its ground from the pieces it was
    /// laid again with, not from the ones it gave up.
    /// </summary>
    [Fact]
    public void AHoldLaidAgainIsCutByAStrongerOneLikeAnyOther()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        var weaker = index.BeginHold(0f);
        index.Take(Ask(weaker, 1, ClaimPriority.FirmAcross, 0f), way, 20f);
        index.EndHold(weaker, 20f, 0f, LaneClaim.Nothing);

        index.ReopenHold(weaker);
        index.Take(Ask(weaker, 1, ClaimPriority.FirmAcross, 0f), way, 40f);
        index.EndHold(weaker, float.PositiveInfinity, 0f, LaneClaim.Nothing);

        Plan(index, way, 50f, hold => Ask(hold, 2, ClaimPriority.FirmStraight, 25f));
        Assert.Equal(25f, index.PlannedToM(way, 0f, occupant: 1, LaneRoster.Driving));
    }

    /// <summary><b>Nothing survives a rebuild</b>, which is the guarantee that makes the index need no release path.</summary>
    [Fact]
    public void ARebuildLeavesNothingOfTheTickBeforeIt()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 10f, 14f, 0f, 1, LaneRoster.Driving, onItsLine: true);
        Plan(index, way, 50f, hold => Ask(hold, 2, ClaimPriority.Firm, 30f));

        index.Begin();
        Assert.Equal((0, 0), (index.SlotCount, index.HoldCount));
        Assert.False(index.AheadBody(way, 0f, 60f, LaneOccupancy.Nobody, out _));
        Assert.Equal(30f, index.PlannedToM(way, 30f, 2, LaneRoster.Driving));
    }

    /// <summary>
    /// <b>Past its bound the index drops rather than grows, and counts what it dropped</b> — a body or a plan
    /// nobody could see, which is a gate's failure and never an outcome.
    /// </summary>
    [Fact]
    public void PastItsBoundTheIndexDropsAndCountsIt()
    {
        var (way, _, _) = ThreeWays();
        var index = Index(mostSlots: 2, mostHolds: 1);

        index.Begin();
        index.LayBody(way, 10f, 14f, 0f, 1, LaneRoster.Driving, onItsLine: true);
        index.LayBody(way, 20f, 24f, 0f, 2, LaneRoster.Driving, onItsLine: true);
        index.LayBody(way, 30f, 34f, 0f, 3, LaneRoster.Driving, onItsLine: true);
        Assert.NotEqual(LaneOccupancy.NoHold, index.BeginHold(0f));
        Assert.Equal(LaneOccupancy.NoHold, index.BeginHold(0f));

        Assert.Equal(2, index.SlotCount);
        Assert.Equal(2, index.Dropped);
    }

    /// <summary>
    /// <b>A way is named once among the ways somebody is on</b>, however many reservations are laid on it —
    /// or every reader counts what is on it as many times.
    /// </summary>
    [Fact]
    public void AWayLaidOnTwiceIsNamedOnce()
    {
        var (way, _, _) = ThreeWays();
        var index = Index();

        index.Begin();
        index.LayBody(way, 10f, 14f, 0f, 1, LaneRoster.Driving, onItsLine: true);
        Plan(index, way, 50f, hold => Ask(hold, 2, ClaimPriority.Firm, 30f));

        var named = 0;
        foreach (var listed in index.OccupiedWays)
        {
            if (listed == way) named++;
        }

        Assert.Equal(1, named);
    }
}
