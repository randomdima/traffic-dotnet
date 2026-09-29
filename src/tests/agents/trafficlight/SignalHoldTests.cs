using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.TrafficLight;

/// <summary>
/// <b>A light's hold as the reservations state it</b> (TLT-1): a secondary claim placed at its own rung, which
/// the ladder alone says who it holds — and which meets main claims on the way it is placed on and nothing
/// else.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class SignalHoldTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Of("Test"), Config);

    static readonly TownWays Ways = TownWays.OfTheRoad(Roads);

    /// <summary>Two ways long enough to lay these tests' stretches on, distinct and in no relation to each other.</summary>
    static (int A, int B) TwoWays()
    {
        var found = new List<int>();
        for (var lane = 0; lane < Roads.LaneCount && found.Count < 2; lane++)
        {
            if (Roads.LaneLengthM[lane] >= 60f) found.Add(Ways.OfRoadLane(lane));
        }

        Assert.Equal(2, found.Count);
        return (found[0], found[1]);
    }

    /// <summary>The reservations over the fixture's road, with one mark between the two ways where one is given.</summary>
    static LaneOccupancy Index(int a = -1, int b = -1)
    {
        if (a < 0) return new LaneOccupancy(Ways, 32, 8, WayCrossings.None);

        // A stretch of each way over the other, filed under both as the atlas files a mark.
        var filed = new CrossedSection[Ways.Count][];
        filed[a] = [new CrossedSection(b, 30f, 35f, 20f, 24f)];
        filed[b] = [new CrossedSection(a, 20f, 24f, 30f, 35f)];

        var offsets = new int[Ways.Count + 1];
        for (var way = 0; way < Ways.Count; way++) offsets[way + 1] = offsets[way] + (filed[way]?.Length ?? 0);

        var sections = new CrossedSection[offsets[Ways.Count]];
        filed[a].CopyTo(sections, offsets[a]);
        filed[b].CopyTo(sections, offsets[b]);
        return new LaneOccupancy(Ways, 32, 8, new WayCrossings(offsets, sections));
    }

    /// <summary>A holder of one roster asking for one way from <paramref name="fromM"/> at one rung.</summary>
    static PlannedAsk Ask(
        int hold, int occupant, ClaimPriority rung, float fromM, LaneRoster of = LaneRoster.Driving,
        float committedToM = float.NegativeInfinity) =>
        new(hold, occupant, of, rung, fromM, fromM, 0f, committedToM, 5f);

    /// <summary>A light's hold placed over [<paramref name="fromM"/>, <paramref name="toM"/>) of one way.</summary>
    static void Light(LaneOccupancy index, int way, float fromM, float toM)
    {
        var hold = index.BeginHold(0f);
        index.Place(Ask(hold, 0, ClaimPriority.Signal, fromM, LaneRoster.Signal), way, toM);
        index.EndHold(hold, float.PositiveInfinity, 0f, LaneClaim.Nothing);
    }

    /// <summary>The light's claim over the same metre another holder asks for.</summary>
    static LaneClaim Held() =>
        new(10f, 20f, 0f, 0, ClaimPriority.Signal, LaneRoster.Signal, Secondary: true, CommittedToM: float.NegativeInfinity);

    static bool Keeps(ClaimPriority rung, bool committed = false) =>
        LaneOccupancy.Beats(
            Ask(0, 1, rung, 10f, committedToM: committed ? 20f : float.NegativeInfinity), 10f, false, Held(), false);

    /// <summary>
    /// <b>A light holds every movement a box admits and everybody on foot</b> (TER-5g, PER-27): somebody
    /// setting out over a crossing, straight on, ordinary and the turn across are all refused the ground it holds.
    /// </summary>
    /// <remarks>
    /// <b>The rungs are named by their byte</b>, because a theory's parameters are as public as the test and the
    /// ladder is not: 5 is a walker, and 6, 7 and 8 are straight on, ordinary and the turn across.
    /// </remarks>
    [Theory]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    [InlineData((byte)8)]
    public void ALightHoldsEveryMovementAndEverybodyOnFoot(byte rung) => Assert.False(Keeps((ClaimPriority)rung));

    /// <summary>
    /// <b>And gives way to a call and ground its holder can no longer stop short of</b>: anything answering a
    /// call goes through, and the amber is what a car that cannot stop drives on.
    /// </summary>
    [Fact]
    public void ALightGivesWayToACallAndGroundThatCannotStop()
    {
        Assert.True(Keeps(ClaimPriority.Special));
        Assert.True(Keeps(ClaimPriority.Firm, committed: true));
    }

    /// <summary>
    /// <b>A main claim is answered where a light's hold begins</b> — the bar — and says what cut it, so a car
    /// held there is a car waiting at a light and not in a queue.
    /// </summary>
    [Fact]
    public void AMainClaimIsAnsweredWhereALightsHoldBegins()
    {
        var (a, _) = TwoWays();
        var index = Index();

        index.Begin();
        Light(index, a, 20f, 50f);

        var hold = index.BeginHold(0f);
        var reachM = index.Reach(Ask(hold, 1, ClaimPriority.FirmStraight, 0f), a, 60f, 0f, out var cutBy);

        Assert.Equal(20f, reachM);
        Assert.Equal((LaneRoster.Signal, true), (cutBy.Of, cutBy.Secondary));
    }

    /// <summary>
    /// <b>A light's hold follows no mark</b>: it is placed on the way it governs and on no other, so the ground
    /// that way shares — a zebra across an approach — is held by nothing of it.
    /// </summary>
    [Fact]
    public void ALightsHoldFollowsNoMark()
    {
        var (a, b) = TwoWays();
        var index = Index(a, b);

        index.Begin();
        Light(index, a, 0f, 50f);

        Span<LaneClaim> planned = stackalloc LaneClaim[4];
        Assert.Equal(0, index.CopyPlannedTo(b, planned));
    }

    /// <summary>
    /// <b>And meets no secondary claim</b> (TER-5c.1): a walker on a zebra across a held approach places its
    /// secondary claim over the light's and both stand — a light holding the traffic costs nobody the paint.
    /// </summary>
    [Fact]
    public void ALightsHoldMeetsNoSecondaryClaim()
    {
        var (a, b) = TwoWays();
        var index = Index(a, b);

        index.Begin();
        Light(index, a, 0f, 50f);

        var walk = index.BeginHold(0f);
        var ask = Ask(walk, 3, ClaimPriority.Afoot, 25f, LaneRoster.Walking);
        Assert.Equal(40f, index.Reach(ask, b, 40f, 25f, out _));
        index.Take(ask, b, 40f);

        Span<LaneClaim> planned = stackalloc LaneClaim[4];
        Assert.Equal(2, index.CopyPlannedTo(a, planned));
    }

    /// <summary>
    /// <b>A light's hold ends at the first body travelling its way and lies over one only across it</b> (TLT-1):
    /// a car driving over a red zebra breaks nothing of the red, and a walker still on the paint is let off it.
    /// </summary>
    [Fact]
    public void ALightsHoldEndsAtTheFirstBodyTravellingItsWay()
    {
        var (a, _) = TwoWays();
        var index = Index();

        index.Begin();
        index.LayBody(a, 10f, 14f, 0f, 1, LaneRoster.Driving, onItsLine: false);
        index.LayBody(a, 30f, 31f, 1f, 2, LaneRoster.Walking, onItsLine: true);
        SignalHolds.HoldTheStretch(index, 0, a, 0f, 50f);

        Span<LaneClaim> planned = stackalloc LaneClaim[4];
        Assert.Equal(1, index.CopyPlannedTo(a, planned));
        Assert.Equal((0f, 30f), (planned[0].FromM, planned[0].ToM));
    }

    /// <summary>
    /// <b>Ground that beats a light's hold takes it</b>: a call laid over it leaves nothing of the light there, so
    /// no metre is planned by two holders (TER-4c.3).
    /// </summary>
    [Fact]
    public void ACallTakesALightsHold()
    {
        var (a, _) = TwoWays();
        var index = Index();

        index.Begin();
        Light(index, a, 20f, 50f);

        var hold = index.BeginHold(0f);
        var ask = Ask(hold, 1, ClaimPriority.Special, 0f);
        Assert.Equal(60f, index.Reach(ask, a, 60f, 0f, out _));
        index.Take(ask, a, 60f);

        Assert.False(index.AheadPlanned(a, 0f, 60f, LaneRoster.Signal, out _));
    }
}
