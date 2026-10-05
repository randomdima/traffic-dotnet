using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen.Traced;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// <b>A bridge's own marks are drawn over its deck</b> (TER-7b, PHY-1a), asked of one street carried over another
/// (<see cref="TracedPlanTests.OverAStreet"/>): what the layers write for the level above is the bridge's and nothing
/// of the street under it, whose lanes and claims cross the bridge's own in plan.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class OverlayLevelsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Vector2 UiPx = new(1600f, 900f);

    const float PixelsPerMetre = 2f;

    /// <summary>The buffers every reading here is taken into, laid once for the class (<see cref="WireframeTests"/>).</summary>
    static readonly OverlayQuad[] Over = new OverlayQuad[TownRenderer.OverlayCapacity];

    static readonly OverlayQuad[] Under = new OverlayQuad[TownRenderer.UnderlayCapacity];

    static readonly OverlayQuad[] UnderAbove = new OverlayQuad[TownRenderer.UnderlayAboveCapacity];

    /// <summary><b>The nodes layer draws a bridge's lanes over its deck</b>, and no lane of the street under it.</summary>
    [Fact]
    public void ABridgesLanesAreDrawnOverItsDeck()
    {
        using var world = new TownWorld(TracedPlanTests.OverAStreet(), Config);

        AssertAllOnTheBridge(world, DrawnAbove(world, DebugLayer.Nodes));
    }

    /// <summary><b>The claims layer draws the ground a car on a bridge holds over its deck</b>, and none a car under it holds.</summary>
    [Fact]
    public void AClaimOnABridgeIsDrawnOverItsDeck()
    {
        using var world = new TownWorld(TracedPlanTests.OverAStreet(), Config);
        world.LayTheClaims();

        AssertAllOnTheBridge(world, DrawnAbove(world, DebugLayer.Claims));
    }

    /// <summary>What one layer writes for the level above, framed on the whole fixture.</summary>
    static ReadOnlySpan<OverlayQuad> DrawnAbove(TownWorld world, DebugLayer layer)
    {
        var switches = new DebugSwitches();
        switches.Toggle(layer);
        var draw = new ScreenDraw(Over);
        var ground = new ScreenDraw(Under);
        var above = new ScreenDraw(UnderAbove);
        new DebugOverlay().Draw(
            ref draw, ref ground, ref above, world, mesh: null, Config, switches, new DebugPick(), -Vector2.One,
            -Vector2.One, UiPx, TracedPlanTests.OverAStreetCrossingM, UiPx / PixelsPerMetre, PixelsPerMetre);

        return UnderAbove.AsSpan(0, above.Written);
    }

    /// <summary>Every quad stands on the bridge's own road — between its two ends and inside its width — and there is one.</summary>
    static void AssertAllOnTheBridge(TownWorld world, ReadOnlySpan<OverlayQuad> drawn)
    {
        var plan = world.Plan;
        var bridge = plan.Bridges.Road[0];
        var arcs = plan.Roads.SegmentsOf(bridge);
        var fromX = MathF.Min(arcs[0].StartM.X, arcs[^1].EndM.X);
        var toX = MathF.Max(arcs[0].StartM.X, arcs[^1].EndM.X);
        var halfWidthM = plan.Roads.WidthM[bridge] * 0.5f;

        Assert.False(drawn.IsEmpty, "nothing was drawn on the level above");
        foreach (var quad in drawn)
        {
            Assert.InRange(quad.Centre.X, fromX, toX);
            Assert.InRange(quad.Centre.Y, arcs[0].StartM.Y - halfWidthM, arcs[0].StartM.Y + halfWidthM);
        }
    }
}
