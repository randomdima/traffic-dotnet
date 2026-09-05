using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>What a body driving geometry of its own can see</b>: a template is laid over no way, so the ground
/// under each place along it is looked up and the claims say who has it (<see cref="GroundAhead"/>).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CarLookingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const int Asking = 7;

    const int Somebody = 3;

    /// <summary>A lane of the fixture town long enough to lay a stretch down, and no claims over that town.</summary>
    static (RoadGraph Roads, LaneOccupancy Claims, int Lane) ALane()
    {
        var roads = RoadGraph.Build(Towns.Of(Towns.Fixture), Config);
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (roads.LaneLengthM[lane] < 60f) continue;

            var claims = new LaneOccupancy(TownWays.OfTheRoad(roads), mostSlots: 8);
            claims.Begin();
            return (roads, claims, lane);
        }

        throw new InvalidOperationException($"{Towns.Fixture} has no lane 60 m long");
    }

    /// <summary>The ground under a place is the lane's, and what is claimed of that lane is the answer.</summary>
    [Fact]
    public void GroundInsideSomebodyElsesStretchIsTaken()
    {
        var (roads, claims, lane) = ALane();
        claims.ClaimUnderWay(claims.Ways.OfRoadLane(lane), 20f, 26f, 26f, 0f, Somebody);

        var onIt = Spline.SampleAt(roads.ArcsOf(lane), 23f).PositionM;
        Assert.True(GroundAhead.TakenAt(roads, claims, onIt, Config.Car.WidthM * 0.5f, Asking, out var found));
        Assert.Equal(Somebody, found.Occupant);

        var pastIt = Spline.SampleAt(roads.ArcsOf(lane), 40f).PositionM;
        Assert.False(GroundAhead.TakenAt(roads, claims, pastIt, Config.Car.WidthM * 0.5f, Asking, out _));
    }

    /// <summary>
    /// <b>The asker's own stretch is not something to be held off.</b> A car walks the ground its own next
    /// manoeuvre would put it on, and it is standing on some of that ground already.
    /// </summary>
    [Fact]
    public void ACarsOwnGroundIsNotTakenFromIt()
    {
        var (roads, claims, lane) = ALane();
        claims.ClaimUnderWay(claims.Ways.OfRoadLane(lane), 20f, 26f, 26f, 0f, Asking);

        var onIt = Spline.SampleAt(roads.ArcsOf(lane), 23f).PositionM;
        Assert.False(GroundAhead.TakenAt(roads, claims, onIt, Config.Car.WidthM * 0.5f, Asking, out _));
    }

    /// <summary>
    /// <b>Ground well off the lane's own band is nobody's.</b> A point beside a carriageway is nearest that
    /// carriageway too, and reading its occupants would refuse a bay template the road it stands off.
    /// </summary>
    [Fact]
    public void GroundClearOfTheLanesBandIsNobodys()
    {
        var (roads, claims, lane) = ALane();
        claims.ClaimUnderWay(claims.Ways.OfRoadLane(lane), 20f, 26f, 26f, 0f, Somebody);

        var on = Spline.SampleAt(roads.ArcsOf(lane), 23f);
        var beside = on.PositionM + (on.Right * (roads.LaneWidthM[lane] + Config.Car.WidthM));
        Assert.False(GroundAhead.TakenAt(roads, claims, beside, Config.Car.WidthM * 0.5f, Asking, out _));
    }

    /// <summary>
    /// A candidate is walked from its near end, so what comes back is the <b>first</b> stretch that is
    /// taken and never the nearest — and a candidate over clear ground gets the whole of its reach.
    /// </summary>
    [Fact]
    public void ACandidateIsClearUpToTheFirstGroundSomebodyHas()
    {
        var (roads, claims, lane) = ALane();
        var arcs = roads.ArcsOf(lane);
        var line = arcs.ToArray();

        var halfWidthM = Config.Car.WidthM * 0.5f;
        Assert.Equal(30f, GroundAhead.ClearM(roads, claims, line, 0f, 30f, halfWidthM, Asking), tolerance: 1e-3f);

        claims.ClaimUnderWay(claims.Ways.OfRoadLane(lane), 20f, 26f, 26f, 0f, Somebody);

        // The lane's own metres and the line's are the same metres here, because the line is the lane.
        var clearM = GroundAhead.ClearM(roads, claims, line, 0f, 30f, halfWidthM, Asking);
        Assert.InRange(clearM, 20f - halfWidthM - 1f, 20f);
    }

    /// <summary>
    /// <b>The far end of a candidate is walked like every metre before it.</b> The step lands on that end
    /// only where the reach is a whole number of steps, so a stretch beginning between the last step and the
    /// end is one a walk that stopped at the last step called clear — and the body holding it is one the
    /// template comes to rest inside.
    /// </summary>
    [Fact]
    public void TheFarEndOfACandidateIsWalked()
    {
        var (roads, claims, lane) = ALane();
        var line = roads.ArcsOf(lane).ToArray();
        var halfWidthM = Config.Car.WidthM * 0.5f;

        // Half a metre past the last whole-metre step the walk takes, which is where GroundAhead's own step
        // leaves off. The stretch begins clear of that step's own body and inside the end's.
        const float lastStepM = 22f;
        const float reachM = lastStepM + 0.5f;
        var takenFromM = lastStepM + halfWidthM + 0.1f;

        claims.ClaimUnderWay(claims.Ways.OfRoadLane(lane), takenFromM, takenFromM + 3f, takenFromM + 3f, 0f, Somebody);

        Assert.Equal(
            lastStepM, GroundAhead.ClearM(roads, claims, line, 0f, reachM, halfWidthM, Asking),
            tolerance: 1e-3f);
    }

    /// <summary>
    /// <b>A car crossing a junction is on a join and on no lane at all</b> (TER-5c.1), so a template that
    /// asked only the lane nearest each of its samples was a manoeuvre that could not see one car in the box
    /// it was swinging through.
    /// </summary>
    [Fact]
    public void GroundInsideAJunctionIsTakenByWhoeverIsCrossingIt()
    {
        var roads = RoadGraph.Build(Towns.Of(Towns.Fixture), Config);
        var claims = new LaneOccupancy(TownWays.OfTheRoad(roads), mostSlots: 8);
        claims.Begin();

        var (slot, arcs) = AJoin(roads);
        var lengthM = roads.JoinLengthM(slot);
        var acrossTheBoxM = Spline.SampleAt(arcs, lengthM * 0.5f).PositionM;

        var halfWidthM = Config.Car.WidthM * 0.5f;
        Assert.False(GroundAhead.TakenAt(roads, claims, acrossTheBoxM, halfWidthM, Asking, out _));

        claims.ClaimUnderWay(claims.Ways.OfRoadTurn(slot), (lengthM * 0.5f) - 2f, (lengthM * 0.5f) + 2f, (lengthM * 0.5f) + 2f, 0f, Somebody);

        Assert.True(GroundAhead.TakenAt(roads, claims, acrossTheBoxM, halfWidthM, Asking, out var found));
        Assert.Equal(Somebody, found.Occupant);
    }

    /// <summary>A junction's join of the fixture town with enough length to stand a body in the middle of.</summary>
    static (int Slot, ArcSeg[] Arcs) AJoin(RoadGraph roads)
    {
        for (var slot = 0; slot < roads.TurnCount; slot++)
        {
            var arcs = roads.JoinArcs(slot);
            if (arcs.Length == 0 || roads.JoinLengthM(slot) < Config.Car.LengthM) continue;

            return (slot, arcs.ToArray());
        }

        throw new InvalidOperationException($"{Towns.Fixture} has no join a car's length long");
    }
}

/// <summary>
/// The same looking asked of a running town, and the one reading the claims could not give before everything
/// on a lane was in it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class CarLookingInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A driver sees somebody standing in its lane, and knows that is what it is.</b> It is the reading
    /// no ray could give — a cast found a shape and a shape on a lane is a shape — and it is the one that
    /// must never be read as an obstruction, because that is a body `E-4` would swerve round.
    /// </summary>
    /// <remarks>
    /// <b>Asked of a busy town and not of the fixture.</b> A body on a crossing holds the lane it is in and
    /// the one it is walking into (`PER-15`), so meeting one is a matter of being the car in that lane —
    /// which on a map with a car a street happens well inside a minute, and on the fixture's thin traffic
    /// takes several.
    /// </remarks>
    [Fact]
    public void ADriverSeesSomebodyOnFootAsSomebodyOnFoot()
    {
        using var world = new TownWorld(Towns.Of("Odesa"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);

        for (var tick = 0; tick < 3_600; tick++)
        {
            loop.Advance();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.Context[car].Ahead == HeadwayKind.Walker) return;
            }
        }

        Assert.Fail("no driver in a minute of Odesa ever read a walker in its lane");
    }
}
