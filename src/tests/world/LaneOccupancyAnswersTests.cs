using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>Every plan answered at once</b> (<see cref="LaneOccupancy.ShowTheAnswers"/>): plans laid whole, then answered
/// pass after pass against what the others came to, as the town answers them.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class LaneOccupancyAnswersTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Of("Test"), Config);

    /// <summary>One plan of one piece: where on which way it begins and ends, and what it asks with.</summary>
    readonly record struct OnePiece(int Way, float FromM, float ToM, int Occupant, ClaimPriority Rung, float AheadM = 0f);

    static LaneOccupancy Index(WayCrossings? marks = null) =>
        new(TownWays.OfTheRoad(Roads), mostSlots: 32, mostHolds: 8, marks ?? WayCrossings.None);

    static (int A, int B) TwoWays()
    {
        var found = new List<int>();
        for (var lane = 0; lane < Roads.LaneCount && found.Count < 2; lane++)
        {
            if (Roads.LaneLengthM[lane] >= 60f) found.Add(TownWays.OfTheRoad(Roads).OfRoadLane(lane));
        }

        return (found[0], found[1]);
    }

    static PlannedAsk Ask(int hold, in OnePiece plan) =>
        new(hold, plan.Occupant, LaneRoster.Driving, plan.Rung, plan.FromM, plan.FromM, plan.AheadM, float.NegativeInfinity, 5f);

    /// <summary>Every plan proposed whole in the order given, then answered as the town answers them; the holds, in that order.</summary>
    static int[] Answered(LaneOccupancy index, params OnePiece[] plans)
    {
        var holds = new int[plans.Length];
        for (var at = 0; at < plans.Length; at++)
        {
            holds[at] = index.BeginHold(standingMarginM: 0f);
            index.Propose(Ask(holds[at], plans[at]), plans[at].Way, plans[at].ToM, plans[at].FromM);
            index.EndHold(holds[at], float.PositiveInfinity, 0f, LaneClaim.Nothing);
        }

        index.AnswerThePlans(Config.Road.MostSettlingPasses, out _);
        return holds;
    }

    static float EndsAtM(LaneOccupancy index, int hold) => index.HoldEndsAtM(hold, out _, out _);

    /// <summary>
    /// <b>Which of two plans keeps the ground they share does not turn on which was laid first</b> (TER-4c.3): the
    /// straighter movement has it either way round, and the other ends where the two met.
    /// </summary>
    [Fact]
    public void TheGroundGoesTheSameWayWhicheverPlanWasLaidFirst()
    {
        var (way, _) = TwoWays();
        var straight = new OnePiece(way, 10f, 50f, Occupant: 1, ClaimPriority.FirmStraight);
        var across = new OnePiece(way, 0f, 40f, Occupant: 2, ClaimPriority.FirmAcross);

        var index = Index();
        index.Begin();
        var laidFirst = Answered(index, straight, across);
        var oneWay = (EndsAtM(index, laidFirst[0]), EndsAtM(index, laidFirst[1]));

        index.Begin();
        var laidLast = Answered(index, across, straight);
        var otherWay = (EndsAtM(index, laidLast[1]), EndsAtM(index, laidLast[0]));

        Assert.Equal((float.PositiveInfinity, 10f), oneWay);
        Assert.Equal(oneWay, otherWay);
    }

    /// <summary>
    /// <b>A plan refused by one that is itself cut short has the ground back</b>: the stronger plan stops at a body
    /// before it reaches the weaker one's ground, so the weaker keeps all it asked for.
    /// </summary>
    [Fact]
    public void APlanRefusedOnlyByOneCutShortKeepsItsGround()
    {
        var (way, _) = TwoWays();
        var index = Index();
        index.Begin();
        index.LayBody(way, 30f, 34f, 0f, occupant: 9, LaneRoster.Driving, onItsLine: true);

        var holds = Answered(
            index, new OnePiece(way, 0f, 55f, Occupant: 1, ClaimPriority.FirmStraight),
            new OnePiece(way, 36f, 60f, Occupant: 2, ClaimPriority.FirmAcross, AheadM: 1f));

        Assert.Equal((30f, float.PositiveInfinity), (EndsAtM(index, holds[0]), EndsAtM(index, holds[1])));
    }

    /// <summary>
    /// <b>A secondary claim a stronger main claim keeps the ground of cuts its own hold where its main claim came to
    /// the mark</b> (TER-5c.1), the whole of the hold past there going with it.
    /// </summary>
    [Fact]
    public void ASecondaryClaimTakenCutsItsHoldAtTheMark()
    {
        var (a, b) = TwoWays();
        var marks = Table(a, (20f, 24f), b, (30f, 35f));
        var index = Index(marks);
        index.Begin();

        var holds = Answered(
            index, new OnePiece(a, 0f, 50f, Occupant: 1, ClaimPriority.FirmAcross, AheadM: 5f),
            new OnePiece(b, 25f, 45f, Occupant: 2, ClaimPriority.FirmStraight));

        Span<LaneClaim> planned = stackalloc LaneClaim[4];
        Assert.Equal(20f, EndsAtM(index, holds[0]));
        Assert.Equal(20f, planned[..index.CopyPlannedTo(a, planned)].ToArray().Single(claim => claim.Occupant == 1).ToM);
    }

    /// <summary>One mark between two ways, filed under both as the atlas files it.</summary>
    static WayCrossings Table(int one, (float FromM, float ToM) mine, int other, (float FromM, float ToM) theirs)
    {
        var count = TownWays.OfTheRoad(Roads).Count;
        var offsets = new int[count + 1];
        var filed = new List<CrossedSection>[count];
        filed[one] = [new CrossedSection(other, theirs.FromM, theirs.ToM, mine.FromM, mine.ToM)];
        filed[other] = [new CrossedSection(one, mine.FromM, mine.ToM, theirs.FromM, theirs.ToM)];
        for (var way = 0; way < count; way++) offsets[way + 1] = offsets[way] + (filed[way]?.Count ?? 0);

        var sections = new CrossedSection[offsets[count]];
        for (var way = 0; way < count; way++) filed[way]?.CopyTo(sections, offsets[way]);

        return new WayCrossings(offsets, sections) { MostCrossedByOne = 2 };
    }
}
