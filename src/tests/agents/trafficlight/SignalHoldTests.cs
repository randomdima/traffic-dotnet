using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.TrafficLight;

/// <summary>
/// <b>A light's hold as the claims state it</b> (TLT-1): a secondary claim placed at its own rung, which the ladder alone
/// says who it holds — and which meets main claims on the way it is placed on and nothing else.
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

    /// <summary>The claims over the fixture's road, with one mark between the two ways where one is given.</summary>
    static LaneOccupancy Index(int a = -1, int b = -1)
    {
        if (a < 0) return new LaneOccupancy(Ways, WayCrossings.None);

        // A stretch of each way over the other, filed under both as the atlas files a mark.
        var filed = new CrossedSection[Ways.Count][];
        filed[a] = [new CrossedSection(b, 30f, 35f, 20f, 24f)];
        filed[b] = [new CrossedSection(a, 20f, 24f, 30f, 35f)];

        var offsets = new int[Ways.Count + 1];
        for (var way = 0; way < Ways.Count; way++) offsets[way + 1] = offsets[way] + (filed[way]?.Length ?? 0);

        var sections = new CrossedSection[offsets[Ways.Count]];
        filed[a].CopyTo(sections, offsets[a]);
        filed[b].CopyTo(sections, offsets[b]);
        return new LaneOccupancy(Ways, new WayCrossings(offsets, sections));
    }

    /// <summary>A light's hold placed over [<paramref name="fromM"/>, <paramref name="toM"/>) of one way.</summary>
    static int Light(LaneOccupancy index, int way, float fromM, float toM)
    {
        var hold = index.BeginHold(0, LaneRoster.Signal);
        index.Place(hold, way, fromM, toM, ClaimPriority.Signal);
        return hold;
    }

    /// <summary>One piece a holder asks for, up one way from <paramref name="fromM"/>.</summary>
    static int Asked(LaneOccupancy index, int occupant, int way, float fromM, float toM, ClaimPriority rung, LaneRoster of = LaneRoster.Driving)
    {
        var hold = index.BeginHold(occupant, of);
        index.Ask(hold, way, fromM, toM, fromM, rung);
        return hold;
    }

    /// <summary>Whether a holder asking for the ground a light holds, at <paramref name="rung"/>, keeps it.</summary>
    static bool Keeps(ClaimPriority rung)
    {
        var (a, _) = TwoWays();
        var index = Index();
        Light(index, a, 10f, 20f);
        var hold = Asked(index, 1, a, 10f, 20f, rung);
        index.Answer();
        return !index.CutOf(hold).Refused;
    }

    /// <summary>
    /// <b>A light holds every movement a box admits and everybody on foot</b> (TER-5g, PER-27): somebody setting out over
    /// a crossing, straight on, ordinary and the turn across are all refused the ground it holds.
    /// </summary>
    /// <remarks>
    /// <b>The rungs are named by their byte</b>, because a theory's parameters are as public as the test and the ladder is
    /// not: 5 is a walker, and 6, 7 and 8 are straight on, ordinary and the turn across.
    /// </remarks>
    [Theory]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    [InlineData((byte)7)]
    [InlineData((byte)8)]
    public void ALightHoldsEveryMovementAndEverybodyOnFoot(byte rung) => Assert.False(Keeps((ClaimPriority)rung));

    /// <summary>
    /// <b>And gives way to a call and ground its holder can no longer stop short of</b>: anything answering a call goes
    /// through, and the amber is what a car that cannot stop drives on.
    /// </summary>
    [Fact]
    public void ALightGivesWayToACallAndGroundThatCannotStop()
    {
        Assert.True(Keeps(ClaimPriority.Special));
        Assert.True(Keeps(ClaimPriority.Committed));
    }

    /// <summary>
    /// <b>A main claim is answered where a light's hold begins</b> — the bar — and says what cut it, so a car held there is
    /// a car waiting at a light and not in a queue.
    /// </summary>
    [Fact]
    public void AMainClaimIsAnsweredWhereALightsHoldBegins()
    {
        var (a, _) = TwoWays();
        var index = Index();
        Light(index, a, 20f, 50f);
        var hold = Asked(index, 1, a, 0f, 60f, ClaimPriority.FirmStraight);
        index.Answer();

        Assert.Equal((20f, true), (index.CutOf(hold).CutLineM, index.CutOf(hold).ByALight));
    }

    /// <summary>
    /// <b>A light's hold follows no mark</b>: it is placed on the way it governs and on no other, so the ground that way
    /// shares — a zebra across an approach — is held by nothing of it.
    /// </summary>
    [Fact]
    public void ALightsHoldFollowsNoMark()
    {
        var (a, b) = TwoWays();
        var index = Index(a, b);
        Light(index, a, 0f, 50f);
        index.Answer();

        Assert.Empty(index.Secondaries(b).ToArray());
    }

    /// <summary>
    /// <b>And meets no secondary claim</b> (TER-5c.1): a walker on a zebra across a held approach places its secondary
    /// claim over the light's and both stand — a light holding the traffic costs nobody the paint.
    /// </summary>
    [Fact]
    public void ALightsHoldMeetsNoSecondaryClaim()
    {
        var (a, b) = TwoWays();
        var index = Index(a, b);
        var light = Light(index, a, 0f, 50f);
        var walk = Asked(index, 3, b, 25f, 40f, ClaimPriority.Afoot, LaneRoster.Walking);
        index.Answer();

        Assert.Equal((false, false), (index.CutOf(light).Refused, index.CutOf(walk).Refused));
        Assert.Equal(2, index.Secondaries(a).Length);
    }

    /// <summary>
    /// <b>A light's hold ends at the first body on its way</b> (TLT-1): a car already past the bar has started and
    /// finishes, and a walker still on the paint is let off it.
    /// </summary>
    [Fact]
    public void ALightsHoldEndsAtTheFirstBodyOnItsWay()
    {
        var (a, _) = TwoWays();
        var index = Index();
        index.LayBody(a, 10f, 14f, 1, LaneRoster.Driving);
        SignalHolds.HoldTheStretch(index, 0, a, 0f, SignalHolds.ReachOf(index, a, 0f, 50f));
        index.Answer();

        var held = Assert.Single(index.Secondaries(a).ToArray());
        Assert.Equal((0, 100), (held.FromDm, held.ToDm));
    }

    /// <summary>
    /// <b>Ground that beats a light's hold takes it</b>: a call taken over it leaves nothing of the light there, so no
    /// metre is planned by two holders (TER-4c.3).
    /// </summary>
    [Fact]
    public void ACallTakesALightsHold()
    {
        var (a, _) = TwoWays();
        var index = Index();
        Light(index, a, 20f, 50f);
        Asked(index, 1, a, 0f, 60f, ClaimPriority.Special);
        index.Answer();

        Assert.False(index.FirstSecondary(a, 0f, 60f, ClaimPriority.Signal, out _, out _));
    }
}
