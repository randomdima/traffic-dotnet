using System.Numerics;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// CTL-1a: what the selection says about where the unit is going. The claims are that the path drawn is
/// the <em>whole</em> one the unit is holding, that the goal is marked as what it is — wrapped where it is
/// entered, crossed where it is ground — and that a hand at the wheel is drawn no path at all.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class SelectionPathTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>A street framing, so the marks are drawn at a readable weight and the pitch is not culled.</summary>
    const float PixelsPerMetre = 24f;

    /// <summary>The fixture map: one screen, one of every kind of ground, with buildings to be sent into.</summary>
    static TownWorld Town() => new(Towns.Of(Towns.Fixture), Config);

    static OverlayQuad[] Marks(TownWorld world)
    {
        var into = new OverlayQuad[TownRenderer.OverlayCapacity];
        var draw = new ScreenDraw(into);
        SelectionPath.Draw(ref draw, world, Config, PixelsPerMetre);
        return into[..draw.Written];
    }

    static OverlayQuad[] Of(OverlayQuad[] marks, Vector4 colour) =>
        [.. marks.Where(mark => mark.Colour == colour)];

    /// <summary>A walker with a line in hand, ordered somewhere it can be walked to, and the tick that takes the order.</summary>
    static int AWalkerUnderOrders(TownWorld world, SimLoop<TownWorld> loop, Vector2 toM)
    {
        var walker = OutOfDoors.AWalker(world, loop, Config);
        world.Select(new Selection(SelectionKind.Person, walker));
        world.Order(walker, toM);
        loop.Advance(2);
        return walker;
    }

    [Fact]
    public void NothingSelectedIsNothingDrawn()
    {
        using var world = Town();
        world.SelectNone();

        Assert.Empty(Marks(world));
    }

    /// <summary>
    /// CTL-5: a hand at the wheel substitutes the behaviour wholesale, so the unit has no goal under it —
    /// and a line drawn from a route nobody is following any more is the picture arguing with the town.
    /// </summary>
    [Fact]
    public void AHandAtTheWheelIsDrawnNoPath()
    {
        using var world = Town();
        var loop = new SimLoop<TownWorld>(world, Config);

        var car = ACarUnderWay(world, loop);
        Assert.True(car >= 0, "no car on the fixture town set off inside a minute of town time");

        world.Select(new Selection(SelectionKind.Car, car));
        Assert.NotEmpty(Marks(world));

        world.Hands(new HandInput(Held: true, Throttle: 1f, Steer: 0f, Handbrake: false, WalkDirection: Vector2.Zero));
        Assert.Empty(Marks(world));
    }

    /// <summary>
    /// <b>The whole line and not the two stretches a layer draws</b>: the last point the walker was given is
    /// on screen, and the goal itself carries the cross that says the walk ends there.
    /// </summary>
    [Fact]
    public void AnOrderedWalkerIsDrawnEveryPointOfItsLineAndACrossOnTheGoal()
    {
        using var world = Town();
        var loop = new SimLoop<TownWorld>(world, Config);

        // Somewhere another walker is standing is somewhere this one can be walked to.
        var other = OutOfDoors.AWalker(world, loop, Config);
        var toM = world.People.PositionM[other] + new Vector2(6f, 0f);
        var walker = AWalkerUnderOrders(world, loop, toM);

        var count = world.People.WalkedCount[walker];
        Assert.True(count > 0, "the order laid no line to draw");

        var marks = Marks(world);
        var lastM = world.People.WalkedLineOf(walker)[count - 1];
        Assert.Contains(
            Of(marks, Theme.SelectionPath),
            mark => (mark.Centre - lastM).Length() < 2f);

        // Two bars crossed on the place itself, and nothing wrapped: an ordered walk to open ground has
        // nothing to put brackets round.
        var goal = Of(marks, Theme.SelectionGoal);
        Assert.Equal(2, goal.Length);
        foreach (var bar in goal) Assert.True((bar.Centre - world.People.GoalM[walker]).Length() < 0.1f);
    }

    /// <summary>
    /// CTL-3's order drawn: a goal that is <em>entered</em> is wrapped in the same brackets the unit itself
    /// wears, standing outside the thing they are drawn round.
    /// </summary>
    [Fact]
    public void AWalkerSentIntoABuildingIsShownTheBuildingWrapped()
    {
        using var world = Town();
        var loop = new SimLoop<TownWorld>(world, Config);
        var buildings = world.Plan.Buildings;
        Assert.True(buildings.Count > 0, "the fixture town stood no building to be sent into");

        var walker = AWalkerUnderOrders(world, loop, buildings.CentreM[0]);
        Assert.Equal(0, world.People.DestinationBuilding[walker]);

        // Four brackets of two arms each, clear of the footprint and not far off it: a mark inside the
        // building is a mark hidden under the thing it is pointing at.
        var goal = Of(Marks(world), Theme.SelectionGoal);
        Assert.Equal(8, goal.Length);

        var halfM = buildings.SizeM[0] * 0.5f;
        foreach (var bracket in goal)
        {
            var offset = Vector2.Abs(bracket.Centre - buildings.CentreM[0]);
            Assert.True(
                offset.X > halfM.X || offset.Y > halfM.Y,
                $"a bracket stands {offset.X:F2} m by {offset.Y:F2} m from the middle of a "
                + $"{buildings.SizeM[0].X:F2} m by {buildings.SizeM[0].Y:F2} m building, which is on it rather than round it");
        }
    }

    /// <summary>
    /// A car is drawn the route its line has not been grown onto yet, which is the half of a drive the
    /// layers never show: the lanes past the one it is driving are on screen, out to the last one planned.
    /// </summary>
    [Fact]
    public void ACarIsDrawnTheLanesItsLineHasNotReachedYet()
    {
        using var world = Town();
        var loop = new SimLoop<TownWorld>(world, Config);

        var car = ACarUnderWay(world, loop);
        Assert.True(car >= 0, "no car on the fixture town set off inside a minute of town time");

        world.Select(new Selection(SelectionKind.Car, car));
        var marks = Of(Marks(world), Theme.SelectionPath);

        var route = world.Cars.RouteOf(car);
        var lastLane = route[world.Cars.RouteCount[car] - 1];
        var endM = world.Roads.EndOf(lastLane).PositionM;
        Assert.Contains(marks, mark => (mark.Centre - endM).Length() < 4f);
    }

    /// <summary>
    /// CTL-1a: <b>where the route the car holds runs out, the rest of the way is planned</b> — asked from
    /// the end of what it is holding and arriving where the car is actually going. The fixture town is far
    /// too small to fill a car's own queue, so the question is put to the town directly, from the middle of
    /// a route as if the queue had stopped there.
    /// </summary>
    [Fact]
    public void TheRestOfACarsWayIsPlannedFromTheEndOfWhatItHolds()
    {
        using var world = Town();
        var loop = new SimLoop<TownWorld>(world, Config);

        var car = ACarUnderWay(world, loop);
        Assert.True(car >= 0, "no car on the fixture town set off inside a minute of town time");

        var held = world.Cars.RouteOf(car)[world.Cars.RouteTaken[car]..world.Cars.RouteCount[car]];
        Assert.True(held.Length >= 2, "the car holds too little route to ask about the rest of it");

        // Never the last lane it holds: there is nothing beyond the end of a route to plan, so a car
        // holding only two lanes has to be asked about the first of them rather than the middle.
        var stopped = Math.Min(held.Length / 2, held.Length - 2);
        var rest = world.RouteBeyond(slot: 0, car, held[stopped]);
        Assert.False(rest.IsEmpty, "nothing was planned past the lane the route was cut at");

        // The road joins it on from where the drawing stopped, and it ends where the car's own route does.
        Assert.NotEqual(RoadGraph.NoTurn, world.Roads.TurnSlot(held[stopped], rest[0]));
        Assert.Equal(held[^1], rest[^1]);
    }

    /// <summary>
    /// The same for a walk: the points past where the line stops carry on towards the goal rather than
    /// starting the walk again from somewhere else.
    /// </summary>
    [Fact]
    public void TheRestOfAWalkIsLaidFromTheEndOfTheLineInHand()
    {
        using var world = Town();
        var loop = new SimLoop<TownWorld>(world, Config);

        var other = OutOfDoors.AWalker(world, loop, Config);
        var toM = world.People.PositionM[other] + new Vector2(6f, 0f);
        var walker = AWalkerUnderOrders(world, loop, toM);

        var line = world.People.WalkedLineOf(walker);
        var count = world.People.WalkedCount[walker];
        Assert.True(count >= 2, "the order laid too little line to ask about the rest of it");

        var stopped = line[count / 2];
        var rest = world.WalkBeyond(slot: 0, walker, stopped);
        Assert.False(rest.IsEmpty, "nothing was laid past the point the line was cut at");

        // The line in hand ends on the goal itself, which is the one hop off the network a walk takes; what
        // is laid past the cut is the network's own last point, which is the one before it.
        var goalM = world.People.GoalM[walker];
        Assert.True((line[count - 1] - goalM).Length() < 0.1f, "the ordered line does not end on its goal");
        Assert.True(
            (rest[^1] - line[count - 2]).Length() < 0.5f,
            $"the rest of the walk ends {(rest[^1] - line[count - 2]).Length():F2} m from where the "
            + "line it was cut from does");
    }

    /// <summary>
    /// And nothing is planned past a route that ends where the car is going, which is what
    /// <see cref="CarFleet.RouteRunsOut"/> is asked before the drawing asks for any of it: a search from the
    /// end of such a route comes back with the way round the block.
    /// </summary>
    [Fact]
    public void ARouteThatReachesItsDestinationIsNotDrawnOnPast()
    {
        using var world = Town();
        var loop = new SimLoop<TownWorld>(world, Config);

        var car = ACarUnderWay(world, loop);
        Assert.True(car >= 0, "no car on the fixture town set off inside a minute of town time");
        Assert.False(world.Cars.RouteRunsOut[car], "a leg across the fixture town filled a car's whole queue");
    }

    /// <summary>
    /// The first car with route left in it, once somebody has walked to one and driven off in it — which is
    /// a trip's worth of town time on a map where everybody starts indoors (GEN-7).
    /// </summary>
    static int ACarUnderWay(TownWorld world, SimLoop<TownWorld> loop)
    {
        var mostTicks = (int)MathF.Ceiling(60f / Config.TickSeconds);
        for (var waited = 0; waited < mostTicks; waited++)
        {
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.RouteCount[car] > world.Cars.RouteTaken[car]) return car;
            }

            loop.Advance(1);
        }

        return -1;
    }
}
