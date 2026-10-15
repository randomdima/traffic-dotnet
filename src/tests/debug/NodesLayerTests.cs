using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>The nodes layer (OBS-2d), asked of the fixture town: what laying it again costs the heap.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P8)]
public class NodesLayerTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Vector2 UiPx = new(1600f, 900f);

    /// <summary>The buffers every frame here is drawn into, laid once for the class (<see cref="WireframeTests"/>).</summary>
    static readonly OverlayQuad[] Over = new OverlayQuad[TownRenderer.OverlayCapacity];

    static readonly OverlayQuad[] Under = new OverlayQuad[TownRenderer.UnderlayCapacity];

    static readonly OverlayQuad[] UnderAbove = new OverlayQuad[TownRenderer.UnderlayAboveCapacity];

    static readonly DebugSwitches Noded = Switched();

    static readonly DebugPick NoPick = new();

    static DebugSwitches Switched()
    {
        var switches = new DebugSwitches();
        switches.Toggle(DebugLayer.Nodes);
        return switches;
    }

    /// <summary>
    /// <b>Rule 2: laying the networks again allocates nothing</b> — at a district's framing and a street's, every lane
    /// culled on the box it was given the first time the town was drawn.
    /// </summary>
    [Fact]
    public void RelayingTheNetworksAllocatesNothing()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var overlay = new DebugOverlay();
        for (var pass = 0; pass < 4; pass++) Relay(overlay, world);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 8; pass++) Relay(overlay, world);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    static void Relay(DebugOverlay overlay, TownWorld world)
    {
        foreach (var pixelsPerMetre in (ReadOnlySpan<float>)[1f, 20f])
        {
            overlay.TownChanged();
            var draw = new ScreenDraw(Over);
            var ground = new ScreenDraw(Under);
            var groundAbove = new ScreenDraw(UnderAbove);
            overlay.Draw(
                ref draw, ref ground, ref groundAbove, world, null, Config, Noded, NoPick, -Vector2.One, -Vector2.One, UiPx,
                world.Plan.WorldSizeM * 0.5f, UiPx / pixelsPerMetre, pixelsPerMetre);
        }
    }
}
