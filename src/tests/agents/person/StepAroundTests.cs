using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// PER-24's geometry, against a pose and a body — no book, no terrain and no town, which is the whole of
/// what the step is: an aim moved sideways by what is in the way of it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class StepAroundTests
{
    /// <summary>A walker at the origin walking east, with +y down: its right is +y.</summary>
    static readonly Vector2 AimM = new(10f, 0f);

    const float ClearanceM = 1.25f;

    [Fact]
    public void ABodyDeadAheadIsPassedOnTheRight()
    {
        var bodyM = new Vector2(3f, 0f);

        Assert.True(StepAround.IsInTheWay(Vector2.Zero, AimM, bodyM, ClearanceM));

        var passM = StepAround.PassM(Vector2.Zero, AimM, bodyM, ClearanceM, onTheRight: true);
        Assert.Equal(ClearanceM, passM.Y, 3);
        Assert.Equal(bodyM.X, passM.X, 3);
    }

    /// <summary>
    /// <b>The least that gets past</b>: the pass point clears the body by the clearance and by nothing more,
    /// which is the whole of what "minimal divergence" is checkable as.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ThePassPointIsExactlyTheClearanceOffTheBody(bool onTheRight)
    {
        var bodyM = new Vector2(3f, 0.4f);
        var passM = StepAround.PassM(Vector2.Zero, AimM, bodyM, ClearanceM, onTheRight);

        Assert.Equal(ClearanceM, (passM - bodyM).Length(), 3);
        Assert.Equal(onTheRight ? bodyM.Y + ClearanceM : bodyM.Y - ClearanceM, passM.Y, 3);
    }

    /// <summary>
    /// <b>A body already clear across the walk is not stepped round.</b> The step lasts as long as the thing
    /// that caused it: read the other way, a walker that has stepped far enough stops stepping, which is
    /// what stops the divergence growing tick after tick.
    /// </summary>
    [Fact]
    public void ABodyClearOfTheWalkIsNothingToStepRound()
    {
        Assert.False(
            StepAround.IsInTheWay(Vector2.Zero, AimM, new Vector2(3f, ClearanceM), ClearanceM));
        Assert.False(
            StepAround.IsInTheWay(Vector2.Zero, AimM, new Vector2(3f, -ClearanceM), ClearanceM));
    }

    /// <summary>
    /// <b>The body a walk ends at is what the walk was for.</b> A paramedic walks at a casualty, and one
    /// that stepped round it would arrive beside the thing it came to collect and never reach it.
    /// </summary>
    [Fact]
    public void ABodyAtTheEndOfTheWalkIsNotSteppedRound() =>
        Assert.False(StepAround.IsInTheWay(Vector2.Zero, AimM, AimM, ClearanceM));

    /// <summary>
    /// <b>Nor one standing within the room the step would take.</b> An aim inside that circle is an aim the
    /// step can never reach — the walker would come round the body and round it again — and an officer
    /// closing a road stands a stride from the casualty that raised the scene.
    /// </summary>
    [Fact]
    public void AnAimInsideTheClearanceOfTheBodyIsNotSteppedAwayFrom() =>
        Assert.False(
            StepAround.IsInTheWay(Vector2.Zero, AimM, AimM - new Vector2(ClearanceM * 0.5f, 0f), ClearanceM));

    /// <summary>Behind is not in the way, however close it is: the feet have already got past it.</summary>
    [Fact]
    public void ABodyBehindIsNotInTheWay() =>
        Assert.False(StepAround.IsInTheWay(Vector2.Zero, AimM, new Vector2(-0.5f, 0f), ClearanceM));

    /// <summary>
    /// <b>The side is read off the walk and not off the axes.</b> Walking the other way down the same
    /// pavement, the right is the other side of the street — which is the whole reason the frame is taken
    /// from the aim every tick rather than held anywhere.
    /// </summary>
    [Fact]
    public void TheRightIsTheWalkersRightAndNotTheWorlds()
    {
        var walkingWest = new Vector2(-10f, 0f);
        var passM = StepAround.PassM(Vector2.Zero, walkingWest, new Vector2(-3f, 0f), ClearanceM, onTheRight: true);

        Assert.Equal(-ClearanceM, passM.Y, 3);
    }

    /// <summary>
    /// <b>The kerb is a line to be grazed and not a wall.</b> A step round a body standing on a pavement
    /// lane's own line reaches a quarter of a body past the kerb, so a rule that refused the carriageway
    /// outright would turn nearly every step in the town the other way — and the graze is what the step is
    /// short by, with the middle of the body still at the channel.
    /// </summary>
    [Theory]
    [InlineData(0.25f, true)]
    [InlineData(-0.25f, true)]
    [InlineData(-1f, false)]
    [InlineData(-2f, false)]
    public void ACarriagewayIsGrazedAndNeverEntered(float fromTheKerbM, bool clear)
    {
        var lane = Roads.NearestLane(Middle, out var alongM);
        var on = Spline.SampleAt(Roads.ArcsOf(lane), alongM);

        // Out from the middle of the lane to its own kerb line, and then the distance being asked about:
        // positive is the pavement side of it and negative is into the traffic.
        var atM = on.PositionM + (on.Right * ((Roads.LaneWidthM[lane] * 0.5f) + fromTheKerbM));

        Assert.Equal(clear, StepAround.IsClearOfTheTraffic(Roads, atM, Config.PersonRoadGrazeM));
    }

    /// <summary>Ground no lane is anywhere near is a walker's to step onto, which is most of a town.</summary>
    [Fact]
    public void GroundAwayFromEveryLaneIsClear() =>
        Assert.True(StepAround.IsClearOfTheTraffic(Roads, Middle + new Vector2(0f, 400f), Config.PersonRoadGrazeM));

    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Of(TrackPlan.Name), Config);

    /// <summary>The middle of the proving ground's own straight, which is a lane with a kerb either side.</summary>
    static Vector2 Middle
    {
        get
        {
            var lap = TrackPlan.Lap()[TrackPlan.Straight];
            var arcs = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lap);
            return Spline.SampleAt(arcs, Spline.TotalLengthM(arcs) * 0.5f).PositionM;
        }
    }

    /// <summary>
    /// A walker standing on its own aim has no walk to diverge from, and the answer is the aim it was
    /// given: a body with nowhere to go does not step round anything.
    /// </summary>
    [Fact]
    public void AWalkOfNoLengthIsNotDivergedFrom()
    {
        var atM = new Vector2(4f, 4f);

        Assert.False(StepAround.IsInTheWay(atM, atM, new Vector2(4.2f, 4f), ClearanceM));
        Assert.Equal(atM, StepAround.PassM(atM, atM, new Vector2(4.2f, 4f), ClearanceM, onTheRight: true));
    }
}
