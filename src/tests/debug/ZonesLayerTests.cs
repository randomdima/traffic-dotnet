using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.World;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// The zones layer (OBS-2z), asked of the fixture town: what a still frame draws out of what the layer kept, and what it
/// costs the heap.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P8)]
public class ZonesLayerTests(StandingTown town) : IClassFixture<StandingTown>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Vector2 UiPx = new(1600f, 900f);

    const float PixelsPerMetre = 2f;

    /// <summary>The buffers every frame here is drawn into, laid once for the class (<see cref="WireframeTests"/>).</summary>
    static readonly OverlayQuad[] Over = new OverlayQuad[TownRenderer.OverlayCapacity];

    static readonly OverlayQuad[] Under = new OverlayQuad[TownRenderer.UnderlayCapacity];

    static readonly OverlayQuad[] UnderAbove = new OverlayQuad[TownRenderer.UnderlayAboveCapacity];

    static readonly DebugSwitches Zoned = Switched();

    static readonly DebugPick NoPick = new();

    static DebugSwitches Switched()
    {
        var switches = new DebugSwitches();
        switches.Toggle(DebugLayer.Zones);
        return switches;
    }

    /// <summary>One frame of the layer with no pointer, and how many quads it laid under the bodies.</summary>
    static int Frame(DebugOverlay overlay, TownWorld world, Vector2 centreM)
    {
        var draw = new ScreenDraw(Over);
        var ground = new ScreenDraw(Under);
        var groundAbove = new ScreenDraw(UnderAbove);
        overlay.Draw(
            ref draw, ref ground, ref groundAbove, world, null, Config, Zoned, NoPick, -Vector2.One, -Vector2.One,
            UiPx, centreM, UiPx / PixelsPerMetre, PixelsPerMetre);

        return ground.Written;
    }

    /// <summary>
    /// <b>A still frame draws the quads a fresh overlay lays</b>: the second frame at one framing is copied out of what
    /// the first kept, and it is the same wash and outlines, quad for quad.
    /// </summary>
    [Fact]
    public void AStillFrameDrawsWhatAFreshOverlayLays()
    {
        var world = town.World;
        var centreM = world.Plan.WorldSizeM * 0.5f;

        var laid = Under.AsSpan(0, Frame(new DebugOverlay(), world, centreM)).ToArray();
        var kept = new DebugOverlay();
        Frame(kept, world, centreM);
        var still = Under.AsSpan(0, Frame(kept, world, centreM)).ToArray();

        Assert.NotEmpty(laid);
        Assert.Equal(laid, still);
    }

    /// <summary>
    /// <b>Rule 2: a frame of the zones allocates nothing</b>, panned or still — the wash laid again into the frame's own
    /// buffer, and kept in room the layer already holds.
    /// </summary>
    [Fact]
    public void DrawingTheZonesAllocatesNothing()
    {
        var world = town.World;
        var overlay = new DebugOverlay();
        var centreM = world.Plan.WorldSizeM * 0.5f;
        for (var pass = 0; pass < 4; pass++) Pan(overlay, world, centreM);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 16; pass++) Pan(overlay, world, centreM);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    /// <summary>A frame panned and then held: the layer laid again, and then copied out.</summary>
    static void Pan(DebugOverlay overlay, TownWorld world, Vector2 centreM)
    {
        for (var step = 0; step < 4; step++)
        {
            var atM = centreM + new Vector2(step * 50f, 0f);
            Frame(overlay, world, atM);
            Frame(overlay, world, atM);
        }
    }
}
