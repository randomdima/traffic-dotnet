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
    /// The first car with a line under it, or −1 where a minute of town time produced none.
    /// </summary>
    /// <remarks>
    /// <b>A line and not a route.</b> A route is a queue of lanes towards a destination, and no town this
    /// build lays gives a car one — there is no bay to be sent to — so what a driving car holds is the line
    /// the tour laid it (<c>LaneTour</c>), which is what the path is drawn from either way.
    /// </remarks>
    static int ACarUnderWay(TownWorld world, SimLoop<TownWorld> loop)
    {
        var mostTicks = (int)MathF.Ceiling(60f / Config.TickSeconds);
        for (var waited = 0; waited < mostTicks; waited++)
        {
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.Driven[car] && world.Cars.Line[car].LaneCount > 0) return car;
            }

            loop.Advance(1);
        }

        return -1;
    }
}
