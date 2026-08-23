using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// <b>The drunk's lurch, asked of a road and a pose and nothing else.</b> Every claim it makes is
/// geometry — where the body may land, which way it goes, and how far — so the cheapest tier that can
/// answer it is this one, and the proving ground is where what the traffic does about it is watched.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class ReelTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly RoadGraph Roads = RoadGraph.Build(Towns.Of(TrackPlan.Name), Config);

    /// <summary>An empty book over those roads, so a lurch is refused by nothing it was not given.</summary>
    static LaneOccupancy AnEmptyBook()
    {
        var book = new LaneOccupancy(Roads, mostSlots: 64);
        book.Begin();
        return book;
    }

    static float RadiusM => Config.PersonDiameterM * 0.5f;

    /// <summary>The middle of a lane of the proving ground's own straight, and the way that lane runs.</summary>
    static (Vector2 AtM, Vector2 Along, int Lane) OnTheStraight()
    {
        var lane = Roads.NearestLane(Middle(TrackPlan.Straight), out var alongM);
        var on = Spline.SampleAt(Roads.ArcsOf(lane), alongM);
        return (on.PositionM, on.Direction, lane);
    }

    /// <summary>Halfway along one of the lap's roads, taken off the plan the map was laid from.</summary>
    static Vector2 Middle(int road)
    {
        var lap = TrackPlan.Lap()[road];
        return Spline.SampleAt(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lap),
            Spline.TotalLengthM(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lap)) * 0.5f).PositionM;
    }

    /// <summary>A body put down on a lane is a drunk; one put down beside it is a pacer, and the kerb is the line between them.</summary>
    [Fact]
    public void OnlyABodyStandingInACarriagewayReelsDownIt()
    {
        var (atM, along, lane) = OnTheStraight();
        var right = Heading.RightOf(along);
        var halfM = Roads.LaneWidthM[lane] * 0.5f;

        Assert.True(Reel.InTheCarriageway(Roads, atM));
        Assert.True(Reel.InTheCarriageway(Roads, atM + (right * (halfM * 0.9f))));
        Assert.False(Reel.InTheCarriageway(Roads, atM + (right * (halfM + Config.RoadWidthM))));
    }

    /// <summary>
    /// <b>It goes the way it is facing and does not turn round.</b> The two lanes of a carriageway run
    /// opposite ways, so a body that took the nearest lane's answer would reel a few metres one way, a few
    /// the other, and stay where it was put down for ever.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void ALurchGoesTheWayTheBodyIsFacing(float facing)
    {
        var (atM, along, _) = OnTheStraight();
        var draw = new Rng(1, 2);

        var took = Reel.NextLurch(
            Config, Roads, AnEmptyBook(), person: 0, atM, along * facing, RadiusM, ref draw, out var goalM);

        Assert.Equal(Lurch.Taken, took);
        Assert.True(Vector2.Dot(goalM - atM, along * facing) > 0f);
    }

    /// <summary>
    /// <b>It stays on the road and in its own lane.</b> The band is what the throw is measured off, so a
    /// lurch cannot land on ground the road does not have — and the lane running the other way stays clear,
    /// which is the ground the traffic behind gets past on.
    /// </summary>
    [Fact]
    public void EveryLurchLandsInsideTheLaneItStartedIn()
    {
        var (atM, along, lane) = OnTheStraight();
        var halfM = Roads.LaneWidthM[lane] * 0.5f;
        var draw = new Rng(7, 11);
        var book = AnEmptyBook();

        var fromM = atM;
        for (var lurch = 0; lurch < 200; lurch++)
        {
            if (Reel.NextLurch(Config, Roads, book, person: 0, fromM, along, RadiusM, ref draw, out var goalM)
                != Lurch.Taken)
            {
                break;
            }

            var on = Roads.NearestLane(goalM, out var alongM);
            var at = Spline.SampleAt(Roads.ArcsOf(on), alongM);
            Assert.True(
                MathF.Abs(Vector2.Dot(goalM - at.PositionM, at.Right)) <= (Roads.LaneWidthM[on] * 0.5f) - RadiusM + 1e-3f,
                $"lurch {lurch} landed outside the lane's own band");

            fromM = goalM;
        }
    }

    /// <summary>
    /// <b>A bend is not cut across.</b> A body walks at what is in front of it and not along the road, so
    /// the straight line to a lurch's far end sags away from the arc — and a full stride down the hairpin
    /// puts the body on the wrong side of the road with a car coming round it.
    /// </summary>
    [Fact]
    public void ALurchRoundAHairpinIsShorterThanOneDownAStraight()
    {
        var draw = new Rng(3, 5);
        var book = AnEmptyBook();

        Assert.Equal(Lurch.Taken, Lurched(Middle(TrackPlan.Straight), book, ref draw, out var downTheStraightM));
        Assert.Equal(Lurch.Taken, Lurched(Middle(TrackPlan.Turn180), book, ref draw, out var roundTheHairpinM));

        Assert.Equal(
            Config.Person.WalkSpeedMps * Config.Person.LurchS, downTheStraightM, 0.5f);

        Assert.True(
            roundTheHairpinM < downTheStraightM,
            $"the hairpin took {roundTheHairpinM:F1} m against the straight's {downTheStraightM:F1} m");
    }

    /// <summary>
    /// <b>It walks into nothing.</b> A car that has stopped in the lane is something the drunk walks into
    /// rather than the other way round, so nobody is holding off on its behalf and nothing else in the town
    /// would catch it.
    /// </summary>
    [Fact]
    public void ALurchIsRefusedWhereSomethingIsStandingInTheWay()
    {
        var (atM, along, lane) = OnTheStraight();
        var strideM = Config.Person.WalkSpeedMps * Config.Person.LurchS;
        var draw = new Rng(13, 17);

        var book = AnEmptyBook();
        Roads.NearestLane(atM, out var alongM);
        book.Add(
            book.WayOfLane(lane), alongM + (strideM * 0.5f), alongM + (strideM * 0.5f) + Config.Car.LengthM, 0f,
            occupant: 0, LaneUse.Obstruction);

        Assert.Equal(
            Lurch.NoRoom,
            Reel.NextLurch(Config, Roads, book, person: 0, atM, along, RadiusM, ref draw, out _));
    }

    /// <summary>And a body off the road altogether is one the ordinary wander carries, not one that reels.</summary>
    [Fact]
    public void ABodyOffTheRoadHasNoLurchToTake()
    {
        var draw = new Rng(19, 23);

        Assert.Equal(
            Lurch.NoRoad,
            Reel.NextLurch(
                Config, Roads, AnEmptyBook(), person: 0, new Vector2(-1_000f, -1_000f), Vector2.UnitX, RadiusM,
                ref draw, out _));
    }

    static Lurch Lurched(Vector2 fromM, LaneOccupancy book, ref Rng draw, out float strideM)
    {
        var lane = Roads.NearestLane(fromM, out var alongM);
        var on = Spline.SampleAt(Roads.ArcsOf(lane), alongM);

        var took = Reel.NextLurch(Config, Roads, book, person: 0, on.PositionM, on.Direction, RadiusM, ref draw, out var goalM);

        Roads.NearestLane(goalM, out var goalAlongM);
        strideM = goalAlongM - alongM;
        return took;
    }
}
