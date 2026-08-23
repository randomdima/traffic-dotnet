using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Gates;

/// <summary>
/// Rule 1, as a test: the crossings of the managed→native wall are counted, bounded and flat in the
/// size of the town. A frame is five of them once there is a frame — acquire, wait, reset, submit,
/// present — and none of the five takes the size of the town as an argument.
/// </summary>
/// <remarks>
/// <para>
/// What this holds is the half of the claim that needs no device in the room: <em>the tick crosses
/// nothing at all</em>. A simulation phase that reaches the driver is the defect it catches, and it
/// catches it on a machine with no GPU. The frame's own five are counted by the run itself and
/// printed by it, because a test that opened a window would be a test that could not run here.
/// </para>
/// <para>
/// The counter is <c>[Conditional("DEBUG")]</c>, so a measured Release run pays nothing for it and
/// reads zero. That is why this asserts a difference rather than a total.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Perf)]
[Collection(Simulation.SolverCollection.Name)]
public class CrossingGateTests
{
    [Fact]
    public void ATickCrossesTheWallNoTimesAtAll()
    {
        var loop = new SimLoop<EmptyWorld>(new EmptyWorld(agentCount: 1_000), SimConfig.Shipped());
        loop.Advance(60);

        var before = Vk.Crossings;
        loop.Advance(1_000);

        Assert.Equal(before, Vk.Crossings);
    }

    /// <summary>
    /// And a tick of a <em>real</em> town, which is where a phase could plausibly reach for the
    /// driver: the bodies are the solver's, the sprites are written into mapped memory, and neither
    /// is a call.
    /// </summary>
    [Fact]
    public void ATickOfARealTownCrossesTheWallNoTimesAtAll()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Fresh(Towns.Fixture), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(60);

        var before = Vk.Crossings;
        loop.Advance(600);

        Assert.Equal(before, Vk.Crossings);
    }

    /// <summary>
    /// <b>And the interface, which is the case the design was most at risk of losing.</b> Every
    /// panel, every read-out and every debug layer is written into a span; the recording never sees
    /// them, and the frame's five calls do not know whether a panel is open.
    /// </summary>
    /// <remarks>
    /// This is the half of the M9 exit condition that can be held without a device in the room. The
    /// other half — that the count is still five with the interface open — is the run's own read-out,
    /// and the offscreen path prints it beside every shot.
    /// </remarks>
    [Fact]
    public void DrawingTheWholeInterfaceCrossesTheWallNoTimesAtAll()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Fresh(Towns.Fixture), config);
        var uiPx = new Vector2(1600f, 900f);
        var camera = new Camera2D(config, world.Plan.WorldSizeM, uiPx);

        var ui = new Interface();
        ui.Menu.Show();
        ui.Switches.Toggle(ref ui.Switches.FrameReadout);
        ui.Switches.Toggle(ref ui.Switches.CarLines);
        ui.Switches.Toggle(ref ui.Switches.WalkerLines);
        ui.Switches.Toggle(ref ui.Switches.Collision);
        ui.Switches.Toggle(ref ui.Switches.Nodes);
        ui.Switches.Toggle(ref ui.Switches.Reservations);
        ui.Switches.Toggle(ref ui.Switches.Ruler);
        ui.Ruler.Click(camera.CentreM);
        ui.Ruler.Click(camera.CentreM + new Vector2(40f, 10f));

        var quads = new OverlayQuad[TownRenderer.OverlayCapacity];
        var ground = new OverlayQuad[TownRenderer.UnderlayCapacity];
        var frame = new InterfaceFrame
        {
            World = world,
            Config = config,
            Camera = camera,
            UiPx = uiPx,
            PointerPx = uiPx * 0.5f,
            MapName = world.Plan.Name,
            WorldSeed = world.Plan.Seed,
            AgentSeed = world.Plan.Seed,
        };

        // Once through first, so that anything laid lazily — the town's own graphs, which the nodes
        // switch asks for — is already laid when the count is taken.
        ui.Draw(quads, ground, frame, out _);

        var before = Vk.Crossings;
        var written = ui.Draw(quads, ground, frame, out var underneath);

        Assert.Equal(before, Vk.Crossings);
        Assert.True(written > 0, "every layer is on and the interface drew nothing");
        Assert.True(written <= TownRenderer.OverlayCapacity);

        // The ground marks are a buffer and a draw of their own, and they are what the nodes and
        // reservation switches draw into — an empty one with every layer on would be the split silently
        // doing nothing.
        Assert.True(underneath > 0, "every layer is on and nothing was drawn under the bodies");
        Assert.True(underneath <= TownRenderer.UnderlayCapacity);
    }
}
