using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>Claims held until their holder lets go of them</b> (the owner's ruling, root decision log): a hold holds what it held
/// and came to what it came to; a stronger piece takes held ground its holder has not committed to, and of one rung what is
/// held keeps; a body laid on held ground cuts the hold there; a cut hold is woken where what cut it changes; and a holder
/// commits and lets go of its own ground where it lies.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P1)]
public class LaneOccupancyKeptTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Of("Test"), Config);

    static readonly TownWays Ways = TownWays.OfTheRoad(Roads);

    static int AWay()
    {
        for (var lane = 0; lane < Roads.LaneCount; lane++)
        {
            if (Roads.LaneLengthM[lane] >= 60f) return Ways.OfRoadLane(lane);
        }

        throw new InvalidOperationException("no lane long enough");
    }

    static int Asked(LaneOccupancy index, int occupant, int way, float fromM, float toM, ClaimPriority rung)
    {
        var hold = index.BeginHold(occupant, LaneRoster.Driving);
        index.Ask(hold, way, fromM, toM, fromM, rung);
        return hold;
    }

    /// <summary>One plan of one piece, its line metres the way's, answered — and its hold.</summary>
    static (LaneOccupancy Index, int Way, int Hold) KeptAt(float fromM, float toM, ClaimPriority rung)
    {
        var way = AWay();
        var index = new LaneOccupancy(Ways, WayCrossings.None);
        var hold = index.BeginPlan(1, LaneRoster.Driving);
        index.Ask(hold, way, fromM, toM, fromM, rung);
        index.Answer();
        return (index, way, hold);
    }

    static (int FromDm, int ToDm, ClaimPriority Rung)[] MainsOn(LaneOccupancy index, int way) =>
        index.Mains(way).ToArray().Select(main => ((int)main.FromDm, (int)main.ToDm, main.Rung)).ToArray();

    [Fact]
    public void AKeptHoldHoldsWhatItHeldAndComesToWhatItCameTo()
    {
        var (index, way, hold) = KeptAt(20f, 30f, ClaimPriority.Firm);

        Assert.True(index.CanKeep(hold, 1, LaneRoster.Driving));
        index.Answer();

        Assert.Equal([(200, 300, ClaimPriority.Firm)], MainsOn(index, way));
        Assert.False(index.CutOf(hold).Refused);
    }

    [Fact]
    public void AKeptHoldLetGoOfHoldsNothing()
    {
        var (index, way, hold) = KeptAt(20f, 30f, ClaimPriority.Firm);

        index.Release(hold);

        Assert.Empty(index.Mains(way).ToArray());
        Assert.False(index.CanKeep(hold, 1, LaneRoster.Driving));
    }

    [Fact]
    public void AStrongerPieceTakesWhatAWeakerHoldKeepsFromWhereItBegins()
    {
        var (index, way, kept) = KeptAt(20f, 40f, ClaimPriority.Firm);

        var stronger = Asked(index, 2, way, 30f, 50f, ClaimPriority.FirmStraight);
        index.Answer();

        Assert.False(index.CutOf(stronger).Refused);
        Assert.Equal((30f, ClaimPriority.FirmStraight), (index.CutOf(kept).AtM, index.CutOf(kept).Rung));
        Assert.Equal(
            [(200, 300, ClaimPriority.Firm), (300, 500, ClaimPriority.FirmStraight)], MainsOn(index, way));
    }

    [Fact]
    public void OfOneRungWhatIsHeldKeeps()
    {
        var (index, way, kept) = KeptAt(20f, 40f, ClaimPriority.Firm);

        var asked = Asked(index, 2, way, 30f, 50f, ClaimPriority.Firm);
        index.Answer();

        Assert.False(index.CutOf(kept).Refused);
        Assert.Equal((30f, ClaimKind.Main), (index.CutOf(asked).AtM, index.CutOf(asked).Kind));
    }

    [Fact]
    public void GroundAHolderCannotStopShortOfIsNeverTaken()
    {
        var (index, way, kept) = KeptAt(20f, 40f, ClaimPriority.Committed);

        var asked = Asked(index, 2, way, 30f, 50f, ClaimPriority.Special);
        index.Answer();

        Assert.False(index.CutOf(kept).Refused);
        Assert.Equal((30f, ClaimPriority.Committed), (index.CutOf(asked).AtM, index.CutOf(asked).Rung));
    }

    [Fact]
    public void ABodyLaidOnKeptGroundCutsTheHoldWhereItStands()
    {
        var (index, way, kept) = KeptAt(20f, 40f, ClaimPriority.Firm);

        index.LayBody(way, 25f, 28f, 2, LaneRoster.Driving);
        index.Answer();

        Assert.Equal((25f, ClaimKind.Body), (index.CutOf(kept).AtM, index.CutOf(kept).Kind));
        Assert.Equal([(200, 250, ClaimPriority.Firm)], MainsOn(index, way));
    }

    [Fact]
    public void ACutHoldIsWokenOnlyWhereWhatCutItMoves()
    {
        var way = AWay();
        var index = new LaneOccupancy(Ways, WayCrossings.None);
        index.PlaceBody(2, LaneRoster.Driving, 2, LaneRoster.Driving, [new WayCover(way, 30f, 35f)]);
        var hold = index.BeginPlan(1, LaneRoster.Driving);
        index.Ask(hold, way, 20f, 50f, 20f, ClaimPriority.Firm);
        index.Answer();

        index.PlaceBody(2, LaneRoster.Driving, 2, LaneRoster.Driving, [new WayCover(way, 30f, 35f)]);
        Assert.True(index.CanKeep(hold, 1, LaneRoster.Driving));
        index.Answer();

        index.PlaceBody(2, LaneRoster.Driving, 2, LaneRoster.Driving, [new WayCover(way, 40f, 45f)]);
        Assert.True(index.IsWoken(hold));
    }

    [Fact]
    public void AKeptHoldIsNeverTakenShortOfItsCommittedLine()
    {
        var (index, way, kept) = KeptAt(20f, 40f, ClaimPriority.Firm);
        index.CommitTo(kept, 30f);

        var asked = Asked(index, 2, way, 25f, 50f, ClaimPriority.Special);
        index.Answer();

        Assert.False(index.CutOf(kept).Refused);
        Assert.Equal((25f, ClaimPriority.Committed), (index.CutOf(asked).AtM, index.CutOf(asked).Rung));
    }

    [Fact]
    public void AKeptHoldPastItsCommittedLineIsTakenByAStrongerPiece()
    {
        var (index, way, kept) = KeptAt(20f, 40f, ClaimPriority.Firm);
        index.CommitTo(kept, 30f);

        var stronger = Asked(index, 2, way, 32f, 50f, ClaimPriority.Special);
        index.Answer();

        Assert.False(index.CutOf(stronger).Refused);
        Assert.Equal((32f, ClaimPriority.Special), (index.CutOf(kept).AtM, index.CutOf(kept).Rung));
        Assert.Equal(
            [(200, 300, ClaimPriority.Committed), (300, 320, ClaimPriority.Firm), (320, 500, ClaimPriority.Special)],
            MainsOn(index, way));
    }

    [Fact]
    public void GroundAHolderHasPassedIsLetGoOf()
    {
        var (index, way, hold) = KeptAt(20f, 40f, ClaimPriority.Firm);

        index.TrimBehind(hold, 25f);
        Assert.Equal([(250, 400, ClaimPriority.Firm)], MainsOn(index, way));

        index.TrimBehind(hold, 45f);
        Assert.Empty(index.Mains(way).ToArray());
    }
}
