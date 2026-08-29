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
/// under each place along it is looked up and the book is asked who has it (<see cref="GroundAhead"/>).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CarLookingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const int Asking = 7;

    const int Somebody = 3;

    /// <summary>A lane of the fixture town long enough to lay a stretch down, and an empty book over that town.</summary>
    static (RoadGraph Roads, LaneOccupancy Book, int Lane) ALane()
    {
        var roads = RoadGraph.Build(Towns.Of(Towns.Fixture), Config);
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (roads.LaneLengthM[lane] < 60f) continue;

            var book = new LaneOccupancy(roads, mostSlots: 8);
            book.Begin();
            return (roads, book, lane);
        }

        throw new InvalidOperationException($"{Towns.Fixture} has no lane 60 m long");
    }

    /// <summary>The ground under a place is the lane's, and what the book says about that lane is the answer.</summary>
    [Fact]
    public void GroundInsideSomebodyElsesStretchIsTaken()
    {
        var (roads, book, lane) = ALane();
        book.Add(book.WayOfLane(lane), 20f, 26f, 0f, Somebody, LaneUse.Reserved);

        var onIt = Spline.SampleAt(roads.ArcsOf(lane), 23f).PositionM;
        Assert.True(GroundAhead.TakenAt(roads, book, onIt, Config.Car.WidthM * 0.5f, Asking, out var found));
        Assert.Equal(Somebody, found.Occupant);

        var pastIt = Spline.SampleAt(roads.ArcsOf(lane), 40f).PositionM;
        Assert.False(GroundAhead.TakenAt(roads, book, pastIt, Config.Car.WidthM * 0.5f, Asking, out _));
    }

    /// <summary>
    /// <b>The asker's own stretch is not something to be held off.</b> A car walks the ground its own next
    /// manoeuvre would put it on, and it is standing on some of that ground already.
    /// </summary>
    [Fact]
    public void ACarsOwnGroundIsNotTakenFromIt()
    {
        var (roads, book, lane) = ALane();
        book.Add(book.WayOfLane(lane), 20f, 26f, 0f, Asking, LaneUse.Reserved);

        var onIt = Spline.SampleAt(roads.ArcsOf(lane), 23f).PositionM;
        Assert.False(GroundAhead.TakenAt(roads, book, onIt, Config.Car.WidthM * 0.5f, Asking, out _));
    }

    /// <summary>
    /// <b>Ground well off the lane's own band is nobody's.</b> A point beside a carriageway is nearest that
    /// carriageway too, and reading its occupants would refuse a bay template the road it stands off.
    /// </summary>
    [Fact]
    public void GroundClearOfTheLanesBandIsNobodys()
    {
        var (roads, book, lane) = ALane();
        book.Add(book.WayOfLane(lane), 20f, 26f, 0f, Somebody, LaneUse.Reserved);

        var on = Spline.SampleAt(roads.ArcsOf(lane), 23f);
        var beside = on.PositionM + (on.Right * (roads.LaneWidthM[lane] + Config.Car.WidthM));
        Assert.False(GroundAhead.TakenAt(roads, book, beside, Config.Car.WidthM * 0.5f, Asking, out _));
    }

    /// <summary>
    /// A candidate is walked from its near end, so what comes back is the <b>first</b> stretch that is
    /// taken and never the nearest — and a candidate over clear ground gets the whole of its reach.
    /// </summary>
    [Fact]
    public void ACandidateIsClearUpToTheFirstGroundSomebodyHas()
    {
        var (roads, book, lane) = ALane();
        var arcs = roads.ArcsOf(lane);
        var line = arcs.ToArray();

        var halfWidthM = Config.Car.WidthM * 0.5f;
        Assert.Equal(30f, GroundAhead.ClearM(roads, book, line, 0f, 30f, halfWidthM, Asking), tolerance: 1e-3f);

        book.Add(book.WayOfLane(lane), 20f, 26f, 0f, Somebody, LaneUse.Reserved);

        // The lane's own metres and the line's are the same metres here, because the line is the lane.
        var clearM = GroundAhead.ClearM(roads, book, line, 0f, 30f, halfWidthM, Asking);
        Assert.InRange(clearM, 20f - halfWidthM - 1f, 20f);
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
        var book = new LaneOccupancy(roads, mostSlots: 8);
        book.Begin();

        var (slot, arcs) = AJoin(roads);
        var lengthM = roads.JoinLengthM(slot);
        var acrossTheBoxM = Spline.SampleAt(arcs, lengthM * 0.5f).PositionM;

        var halfWidthM = Config.Car.WidthM * 0.5f;
        Assert.False(GroundAhead.TakenAt(roads, book, acrossTheBoxM, halfWidthM, Asking, out _));

        book.Add(book.WayOfTurn(slot), (lengthM * 0.5f) - 2f, (lengthM * 0.5f) + 2f, 0f, Somebody, LaneUse.Reserved);

        Assert.True(GroundAhead.TakenAt(roads, book, acrossTheBoxM, halfWidthM, Asking, out var found));
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
/// The same looking asked of a running town, and the one reading the book could not give before everything
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
