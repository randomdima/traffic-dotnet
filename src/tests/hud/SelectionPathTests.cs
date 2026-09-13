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
[Trait(Priority.Key, Priority.P6)]
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

        var car = ACarUnderWay(world, loop, holdingLanes: 2);
        Assert.True(car >= 0, "no car on the fixture town held two lanes of route inside a minute of town time");

        var held = world.Cars.RouteOf(car)[world.Cars.RouteTaken[car]..world.Cars.RouteCount[car]];

        // Never the last lane it holds: there is nothing beyond the end of a route to plan, so a car
        // holding only two lanes has to be asked about the first of them rather than the middle.
        var stopped = Math.Min(held.Length / 2, held.Length - 2);
        var rest = world.RouteBeyond(slot: 0, car, held[stopped]);
        Assert.False(rest.IsEmpty, "nothing was planned past the lane the route was cut at");

        // The road joins it on from where the drawing stopped, and it ends where the car's own route does.
        Assert.NotEqual(RoadGraph.NoConnector, world.Roads.ConnectorBetween(held[stopped], rest[0]));
        Assert.Equal(held[^1], rest[^1]);
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
    /// The first car holding at least <paramref name="holdingLanes"/> lanes of route still to drive, or −1
    /// where a minute of town time produced none.
    /// </summary>
    /// <remarks>
    /// <b>How much route is part of the question.</b> A car one lane from its destination is under way and
    /// is no use to a case about the road past where the queue stopped, so the staging asks for the depth it
    /// needs rather than taking whatever moved first and asserting about it afterwards.
    /// </remarks>
    static int ACarUnderWay(TownWorld world, SimLoop<TownWorld> loop, int holdingLanes = 1)
    {
        var mostTicks = (int)MathF.Ceiling(60f / Config.TickSeconds);
        for (var waited = 0; waited < mostTicks; waited++)
        {
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.RouteCount[car] - world.Cars.RouteTaken[car] >= holdingLanes) return car;
            }

            loop.Advance(1);
        }

        return -1;
    }
}
