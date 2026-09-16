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

    /// <summary>
    /// The two buffers every reading here is taken into, laid once for the class. <b>A frame's worth of
    /// quads is megabytes</b>, and a reading that allocated its own would put a collection of this class's
    /// own making under every allocation gate running beside it.
    /// </summary>
    static readonly OverlayQuad[] Over = new OverlayQuad[TownRenderer.OverlayCapacity];

    static readonly OverlayQuad[] Under = new OverlayQuad[TownRenderer.UnderlayCapacity];

    /// <summary>
    /// The fixture's ground, cut once for the class: nothing here writes to a mesh, and cutting the same
    /// town three times is three copies of it in the heap for one reading apiece.
    /// </summary>
    static readonly GroundMesh Mesh = GroundMesh.Build(Towns.Of(Towns.Fixture), Config);

    /// <summary>How many quads the layer laid under the bodies, at one framing of the whole town's middle.</summary>
    static int Drawn(
        DebugOverlay overlay, TownWorld world, float pixelsPerMetre, uint parts = GroundParts.All)
    {
        var switches = new DebugSwitches();
        switches.Toggle(ref switches.Wireframe);
        for (var part = 0; part < GroundParts.Count; part++)
        {
            if ((parts & GroundParts.Bit((GroundPart)part)) == 0) switches.Ground.Toggle((GroundPart)part);
        }

        var draw = new ScreenDraw(Over);
        var ground = new ScreenDraw(Under);

        // No pointer and no pick: what is counted is the layer's own quads, and a reading taken under a
        // pointer nobody is holding would be quads this test cannot account for (OBS-2t).
        overlay.Draw(
            ref draw, ref ground, world, Mesh, Config, switches, new DebugPick(), -Vector2.One, -Vector2.One,
            UiPx, world.Plan.WorldSizeM * 0.5f, UiPx / pixelsPerMetre, pixelsPerMetre);

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
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);

        Assert.NotEqual(0, Drawn(new DebugOverlay(), world, pixelsPerMetre: 20f));

        // A pixel to a kilometre: every triangle in the town is a fraction of one, the sheet of grass
        // under all of it included.
        Assert.Equal(0, Drawn(new DebugOverlay(), world, pixelsPerMetre: 0.001f));
    }

    /// <summary>
    /// <b>A layer taken out of the ground is taken out of this</b> (OBS-2v): what the whole mesh draws is
    /// what its layers draw one at a time, and with none of them showing there is nothing to draw. The
    /// cull is a triangle's own, so the parts cannot pay for each other's.
    /// </summary>
    [Fact]
    public void ALayerTakenOutOfTheGroundIsTakenOutOfTheWireframe()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);

        var apiece = 0;
        for (var part = 0; part < GroundParts.Count; part++)
        {
            apiece += Drawn(new DebugOverlay(), world, 20f, GroundParts.Bit((GroundPart)part));
        }

        Assert.Equal(Drawn(new DebugOverlay(), world, 20f), apiece);
        Assert.Equal(0, Drawn(new DebugOverlay(), world, 20f, parts: 0u));
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
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var overlay = new DebugOverlay();

        var first = Drawn(overlay, world, pixelsPerMetre: 20f);
        var again = Drawn(overlay, world, pixelsPerMetre: 20f);

        Assert.False(overlay.Relaid, "the same framing laid the mesh a second time");
        Assert.Equal(first, again);
    }
}
