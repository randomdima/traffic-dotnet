using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.World;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// The inspector (OBS-2t), asked of the fixture town: what a click pins, in what order the layers are asked,
/// and when a pin is let go. <b>A body is whatever the engine's own table found under the pointer</b>
/// (<see cref="EngineLayers"/>), handed in here as the device would read it out.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P8)]
public class InspectorTests(StandingTown town) : IClassFixture<StandingTown>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Vector2 UiPx = new(1600f, 900f);

    const float PixelsPerMetre = 20f;

    /// <summary>A body's row as the table reads it out: a rail vehicle, which no layer of the overlay knows by name.</summary>
    const int WagonBody = 4321;

    static readonly EngineLayers.Picks AWagon = new(
        new EngineLayers.PickRow(
            WagonBody + 1, new BodyTag(BodyKind.Train, 2).Packed, (uint)BodyFlags.Enabled | ((uint)CollisionLayer.Train << 8), 0,
            new Vector4(10f, 20f, 1f, 0f), new Vector4(6f, 1.2f, 0.3f, 6.5f), new Vector4(15f, 0f, 0f, 0f)),
        default);

    /// <summary>The two buffers every frame here is drawn into, laid once for the class (<see cref="WireframeTests"/>).</summary>
    static readonly OverlayQuad[] Over = new OverlayQuad[TownRenderer.OverlayCapacity];

    static readonly OverlayQuad[] Under = new OverlayQuad[TownRenderer.UnderlayCapacity];

    static readonly OverlayQuad[] UnderAbove = new OverlayQuad[TownRenderer.UnderlayAboveCapacity];

    /// <summary>One frame of the overlay framed on a place, with the pointer standing on it and the table's find handed in.</summary>
    static void Frame(
        DebugOverlay overlay, TownWorld world, DebugSwitches switches, DebugPick pick, Vector2 atM, in EngineLayers.Picks picked = default)
    {
        var draw = new ScreenDraw(Over);
        var ground = new ScreenDraw(Under);
        var groundAbove = new ScreenDraw(UnderAbove);
        overlay.Draw(
            ref draw, ref ground, ref groundAbove, world, mesh: null, Config, switches, pick, atM, UiPx * 0.5f, UiPx, atM,
            UiPx / PixelsPerMetre, PixelsPerMetre, picked);
    }

    static DebugSwitches On(params DebugLayer[] layers)
    {
        var switches = new DebugSwitches();
        foreach (var layer in layers) switches.Toggle(layer);

        return switches;
    }

    /// <summary>A click on a body the table found pins that body, whatever it is, and pointing at one without a click pins nothing.</summary>
    [Fact]
    public void AClickOnABodyPinsThatBodyAndPointingAloneDoesNot()
    {
        var world = town.World;
        var overlay = new DebugOverlay();
        var switches = On(DebugLayer.Collision);
        var pick = new DebugPick();
        var atM = new Vector2(10f, 20f);

        Frame(overlay, world, switches, pick, atM, AWagon);
        Assert.Equal(DebugThing.Body, pick.Hovered.Thing);
        Assert.Equal(DebugThing.None, pick.Pinned.Thing);

        pick.Click(atM);
        Frame(overlay, world, switches, pick, atM, AWagon);
        Assert.True(pick.Pinned.Same(new DebugTarget(DebugThing.Body, WagonBody)), $"pinned {pick.Pinned}");
    }

    /// <summary>
    /// <b>What is drawn over is found first</b>: with the claims and the network on, a click where the table found a body
    /// standing on a lane pins the body and not the lane under it.
    /// </summary>
    [Fact]
    public void ABodyIsFoundBeforeTheWayItStandsOn()
    {
        var world = town.World;
        var (_, atM) = EmptyLane(world);
        var overlay = new DebugOverlay();
        var pick = new DebugPick();

        pick.Click(atM);
        Frame(overlay, world, On(DebugLayer.Claims, DebugLayer.Nodes), pick, atM, AWagon);

        Assert.Equal(DebugThing.Body, pick.Pinned.Thing);
    }

    /// <summary>With the claims on, a click on a lane nobody stands on pins that lane's way.</summary>
    [Fact]
    public void AClickOnALaneWithTheClaimsOnPinsItsWay()
    {
        var world = town.World;
        var (way, atM) = EmptyLane(world);
        var overlay = new DebugOverlay();
        var pick = new DebugPick();

        pick.Click(atM);
        Frame(overlay, world, On(DebugLayer.Claims), pick, atM);

        Assert.True(pick.Pinned.Same(new DebugTarget(DebugThing.Way, Way: way)), $"pinned {pick.Pinned} for way {way}");
    }

    /// <summary>A click on nothing any layer draws lets the pin go.</summary>
    [Fact]
    public void AClickOnNothingLetsThePinGo()
    {
        var world = town.World;
        var overlay = new DebugOverlay();
        var switches = On(DebugLayer.Collision);
        var pick = new DebugPick();
        var atM = new Vector2(10f, 20f);

        pick.Click(atM);
        Frame(overlay, world, switches, pick, atM, AWagon);
        Assert.Equal(DebugThing.Body, pick.Pinned.Thing);

        var offTheMapM = new Vector2(-1000f);
        pick.Click(offTheMapM);
        Frame(overlay, world, switches, pick, offTheMapM);
        Assert.Equal(DebugThing.None, pick.Pinned.Thing);
    }

    /// <summary>
    /// <b>A pin no layer that is on could find is let go</b>, even while another layer is on: its card would be a
    /// reading of a thing nothing on the glass is drawing.
    /// </summary>
    [Fact]
    public void APinNoLayerThatIsOnCouldFindIsLetGo()
    {
        var world = town.World;
        var overlay = new DebugOverlay();
        var switches = On(DebugLayer.Collision);
        var pick = new DebugPick();
        var atM = new Vector2(10f, 20f);

        pick.Click(atM);
        Frame(overlay, world, switches, pick, atM, AWagon);
        Assert.Equal(DebugThing.Body, pick.Pinned.Thing);

        switches.Toggle(DebugLayer.Collision);
        switches.Toggle(DebugLayer.Grid);
        Frame(overlay, world, switches, pick, atM, AWagon);
        Assert.Equal(DebugThing.None, pick.Pinned.Thing);
    }

    /// <summary>
    /// <b>Rule 2: an inspected frame allocates nothing</b> — the pointer on a way with the claims listed and a
    /// rail vehicle pinned, every card written into buffers the overlay already holds.
    /// </summary>
    [Fact]
    public void AnInspectedFrameAllocatesNothing()
    {
        var world = town.World;
        var (_, atM) = EmptyLane(world);
        var overlay = new DebugOverlay();
        var switches = On(DebugLayer.CarLines, DebugLayer.WalkerLines, DebugLayer.Claims);
        var pick = new DebugPick();
        pick.Click(atM);
        var pinned = new EngineLayers.Picks(default, AWagon.Pointed);
        Frame(overlay, world, switches, pick, atM, AWagon);
        for (var pass = 0; pass < 2; pass++) Frame(overlay, world, switches, pick, atM, pinned);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 16; pass++) Frame(overlay, world, switches, pick, atM, pinned);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(DebugThing.Body, pick.Pinned.Thing);
        Assert.Equal(DebugThing.Way, pick.Hovered.Thing);
    }

    /// <summary>The middle of the first carriageway lane that no body stands over, and its way.</summary>
    static (int Way, Vector2 AtM) EmptyLane(TownWorld world)
    {
        var ways = world.Ways;
        for (var way = 0; way < ways.Count; way++)
        {
            if (ways.KindOf(way) != WayKind.Lane) continue;

            var atM = Spline.SampleAt(world.LineOfWay(way, out _), ways.LengthM(way) * 0.5f).PositionM;
            if (world.CarAt(atM) < 0 && world.PersonAt(atM) < 0) return (way, atM);
        }

        throw new InvalidOperationException("the fixture has no lane without a body over its middle");
    }
}
