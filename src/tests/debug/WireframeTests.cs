using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// The layer that draws the ground's own triangles (OBS-2o), asked of the fixture town: when it draws
/// them, and how often it works them out.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class WireframeTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>The window the framings below are read on, since what the layer drops is a size on the glass.</summary>
    static readonly Vector2 UiPx = new(1600f, 900f);

    /// <summary>How many quads the layer laid under the bodies, at one framing of the whole town's middle.</summary>
    static int Drawn(DebugOverlay overlay, TownWorld world, GroundMesh mesh, float pixelsPerMetre)
    {
        var switches = new DebugSwitches();
        switches.Toggle(ref switches.Wireframe);

        var over = new OverlayQuad[TownRenderer.OverlayCapacity];
        var under = new OverlayQuad[TownRenderer.UnderlayCapacity];
        var draw = new ScreenDraw(over);
        var ground = new ScreenDraw(under);

        overlay.Draw(
            ref draw, ref ground, world, mesh, Config, switches, world.Plan.WorldSizeM * 0.5f,
            UiPx / pixelsPerMetre, pixelsPerMetre);

        return ground.Written;
    }

    /// <summary>
    /// <b>A triangle too small to read is not drawn, and the pair is the claim</b>: the same mesh in the
    /// same place draws at a framing that can show a cut and draws nothing at all at one that cannot.
    /// Without that floor a town-wide framing is a wash over the whole map, which says nothing about where
    /// the cuts fell and costs the buffer to say it.
    /// </summary>
    [Fact]
    public void AMeshTooSmallToReadIsNotDrawn()
    {
        var plan = Towns.Of(Towns.Fixture);
        using var world = new TownWorld(plan, Config);
        var mesh = GroundMesh.Build(plan, Config);

        Assert.NotEqual(0, Drawn(new DebugOverlay(), world, mesh, pixelsPerMetre: 20f));

        // A pixel to two hundred metres: every triangle in the town is a fraction of one, the sheet of
        // grass under all of it included.
        Assert.Equal(0, Drawn(new DebugOverlay(), world, mesh, pixelsPerMetre: 0.005f));
    }

    /// <summary>
    /// <b>The triangles are laid once and copied after that.</b> The mesh does not move once the town is
    /// laid, so a frame that has not moved the camera pays a copy of memory rather than a walk of the whole
    /// town's ground — the rule the town's own graphs are cached under, and what makes this layer
    /// affordable at a district framing.
    /// </summary>
    [Fact]
    public void AFrameThatHasNotMovedRelaysNoTriangles()
    {
        var plan = Towns.Of(Towns.Fixture);
        using var world = new TownWorld(plan, Config);
        var mesh = GroundMesh.Build(plan, Config);
        var overlay = new DebugOverlay();

        var first = Drawn(overlay, world, mesh, pixelsPerMetre: 20f);
        var again = Drawn(overlay, world, mesh, pixelsPerMetre: 20f);

        Assert.False(overlay.Relaid, "the same framing laid the mesh a second time");
        Assert.Equal(first, again);
    }
}
