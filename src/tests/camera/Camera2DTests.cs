using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Camera;

/// <summary>
/// The camera alone decides how much world is on screen (OBS-1), so what is asserted here is the
/// shipped feel — and the one behaviour that is easy to get subtly wrong.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class Camera2DTests
{
    static readonly Vector2 UiPx = new(1600f, 900f);
    static readonly Vector2 TownM = new(480f, 320f);

    [Fact]
    public void ItOpensOnTheDefaultSpanAtTheMiddleOfTheTown()
    {
        var config = SimConfig.Shipped();
        var camera = new Camera2D(config, TownM, UiPx);

        Assert.Equal(TownM * 0.5f, camera.CentreM);
        Assert.Equal(config.View.CameraDefaultViewM, camera.ViewSpanM(UiPx).Y, tolerance: 1e-3f);
    }

    /// <summary>
    /// The world point under the pointer is the one that does not move, which is what makes a zoom
    /// feel like leaning in rather than like being pushed — and it is the thing a camera that scales
    /// about the centre instead gets wrong in a way nobody can quite name.
    /// </summary>
    [Fact]
    public void ZoomingKeepsTheWorldUnderThePointerWhereItIs()
    {
        var camera = new Camera2D(SimConfig.Shipped(), TownM, UiPx);
        var pointerPx = new Vector2(1200f, 200f);
        var before = camera.WorldAt(pointerPx, UiPx);

        foreach (var notches in (ReadOnlySpan<float>)[1f, 3f, -2f, -5f])
        {
            camera.Zoom(notches, pointerPx, UiPx);
            Assert.Equal(before.X, camera.WorldAt(pointerPx, UiPx).X, tolerance: 1e-2f);
            Assert.Equal(before.Y, camera.WorldAt(pointerPx, UiPx).Y, tolerance: 1e-2f);
        }
    }

    [Fact]
    public void OneNotchIsTheShippedFactor()
    {
        var config = SimConfig.Shipped();
        var camera = new Camera2D(config, TownM, UiPx);
        var before = camera.PixelsPerMetre;

        camera.Zoom(1f, UiPx * 0.5f, UiPx);

        Assert.Equal(before * config.View.CameraZoomPerNotch, camera.PixelsPerMetre, tolerance: 1e-3f);
    }

    /// <summary>
    /// Both ends of the zoom are the town's own figures rather than numbers of their own: out until
    /// the whole town is on screen, in until a car's art is magnified as far as it is allowed to be.
    /// </summary>
    [Fact]
    public void TheZoomStopsAtTheWholeTownAndAtOneCar()
    {
        var config = SimConfig.Shipped();
        var camera = new Camera2D(config, TownM, UiPx);

        camera.Zoom(-100f, UiPx * 0.5f, UiPx);
        Assert.True(camera.ViewSpanM(UiPx).Y <= MathF.Max(TownM.X, TownM.Y) + 1f);

        camera.Zoom(100f, UiPx * 0.5f, UiPx);
        Assert.Equal(config.View.CarSpritePixelsPerMetre * config.View.CameraMaxSpriteMagnification,
            camera.PixelsPerMetre, tolerance: 1e-3f);
    }

    /// <summary>
    /// <b>The zoom-in stop is the one figure here that is about the display and not about the
    /// interface</b>: it is measured in the display's own pixels, so on a 2× desktop — where an
    /// interface pixel is two of them — it stops at half the interface figure and the art is magnified
    /// no further than it is on a 1× one.
    /// </summary>
    [Fact]
    public void TheZoomInStopFollowsTheDisplaysPixelsAndNotTheInterfaces()
    {
        var config = SimConfig.Shipped();
        var camera = new Camera2D(config, TownM, UiPx) { DevicePxPerUiPx = 2f };

        camera.Zoom(100f, UiPx * 0.5f, UiPx);

        Assert.Equal(config.View.CarSpritePixelsPerMetre * config.View.CameraMaxSpriteMagnification * 0.5f,
            camera.PixelsPerMetre, tolerance: 1e-3f);
    }

    /// <summary>Arrows pan in screen pixels a second, so a pan covers the same distance on screen at any zoom.</summary>
    [Fact]
    public void PanningIsAtTheShippedRateInScreenPixels()
    {
        var config = SimConfig.Shipped();
        var camera = new Camera2D(config, TownM, UiPx);
        var before = camera.CentreM;

        camera.Pan(new Vector2(1f, 0f), seconds: 1f);

        Assert.Equal(config.View.CameraPanPxPerS / camera.PixelsPerMetre, camera.CentreM.X - before.X, tolerance: 1e-3f);
        Assert.Equal(before.Y, camera.CentreM.Y);
    }

    /// <summary>A drag moves the ground exactly as far as the pointer moved, or it slips under the hand.</summary>
    [Fact]
    public void ADragMovesTheGroundWithThePointer()
    {
        var camera = new Camera2D(SimConfig.Shipped(), TownM, UiPx);
        var pointerPx = new Vector2(800f, 450f);
        var under = camera.WorldAt(pointerPx, UiPx);

        camera.PanByPixels(new Vector2(37f, -19f));

        Assert.Equal(under.X, camera.WorldAt(pointerPx + new Vector2(37f, -19f), UiPx).X, tolerance: 1e-3f);
        Assert.Equal(under.Y, camera.WorldAt(pointerPx + new Vector2(37f, -19f), UiPx).Y, tolerance: 1e-3f);
    }

    /// <summary>
    /// And it still does with the town turned (OBS-1c) — which is the one thing a camera that turned
    /// the picture and left the pointer arithmetic upright would get wrong, in a way that reads as a
    /// town sliding sideways out from under the hand.
    /// </summary>
    [Fact]
    public void ADragMovesTheGroundWithThePointerOnATurnedTown()
    {
        var camera = Turned(float.Pi / 5f);
        var pointerPx = new Vector2(800f, 450f);
        var under = camera.WorldAt(pointerPx, UiPx);

        camera.PanByPixels(new Vector2(37f, -19f));

        Assert.Equal(under.X, camera.WorldAt(pointerPx + new Vector2(37f, -19f), UiPx).X, tolerance: 1e-3f);
        Assert.Equal(under.Y, camera.WorldAt(pointerPx + new Vector2(37f, -19f), UiPx).Y, tolerance: 1e-3f);
    }

    /// <summary>
    /// <b>OBS-1c — the town turns about the point it is turned at.</b> The world under that pixel is
    /// what a hand has hold of, and a camera that turned about the middle of the window instead throws
    /// whatever was between two fingers off the screen.
    /// </summary>
    [Fact]
    public void TurningKeepsTheWorldUnderThePivotWhereItIs()
    {
        var camera = new Camera2D(SimConfig.Shipped(), TownM, UiPx);
        var pivotPx = new Vector2(1200f, 200f);
        var before = camera.WorldAt(pivotPx, UiPx);

        foreach (var turnRad in (ReadOnlySpan<float>)[0.4f, 0.9f, -1.7f, -0.3f])
        {
            camera.Turn(turnRad, pivotPx, UiPx);
            Assert.Equal(before.X, camera.WorldAt(pivotPx, UiPx).X, tolerance: 1e-2f);
            Assert.Equal(before.Y, camera.WorldAt(pivotPx, UiPx).Y, tolerance: 1e-2f);
        }
    }

    /// <summary>
    /// Which way the turn goes, stated once: a quarter turn clockwise takes the top of the picture to
    /// the right, so the ground due north of the middle is drawn to the right of it.
    /// </summary>
    [Fact]
    public void AQuarterTurnClockwiseTakesNorthToTheRight()
    {
        var camera = Turned(float.Pi / 2f);
        var northM = camera.CentreM - new Vector2(0f, 10f);

        var atPx = camera.ScreenAt(northM, UiPx) - (UiPx * 0.5f);

        Assert.Equal(10f * camera.PixelsPerMetre, atPx.X, tolerance: 1e-2f);
        Assert.Equal(0f, atPx.Y, tolerance: 1e-2f);
    }

    /// <summary>
    /// <b>What is culled against is the turned view and not the upright one</b> (OBS-1c): on the
    /// quarter turn the window's own axes have swapped, and at anything between them the box the view
    /// covers is larger than the view.
    /// </summary>
    [Fact]
    public void TheCullBoxIsTheViewTurned()
    {
        var spanM = new Camera2D(SimConfig.Shipped(), TownM, UiPx).ViewSpanM(UiPx);

        var quarter = Turned(float.Pi / 2f).CullSpanM(UiPx);
        Assert.Equal(spanM.Y, quarter.X, tolerance: 1e-2f);
        Assert.Equal(spanM.X, quarter.Y, tolerance: 1e-2f);

        var eighth = Turned(float.Pi / 4f).CullSpanM(UiPx);
        Assert.True(eighth.X > spanM.X && eighth.Y > spanM.Y, "a town turned off the axes covers more ground");
    }

    /// <summary>
    /// <b>A turn a degree at a time is still a turn</b> (OBS-1c). A camera that put itself back level
    /// whenever it was near north could never be nudged away from it at all: every step would be undone
    /// before the next arrived, and the compass is what north-up is for instead.
    /// </summary>
    [Fact]
    public void SmallTurnsAddUpRatherThanBeingUndone()
    {
        var camera = new Camera2D(SimConfig.Shipped(), TownM, UiPx);

        for (var step = 0; step < 8; step++) camera.Turn(0.01f, UiPx * 0.5f, UiPx);

        Assert.Equal(0.08f, camera.TurnRad, tolerance: 1e-4f);
    }

    /// <summary>And the compass puts it back from wherever it has got to.</summary>
    [Fact]
    public void FacingNorthUndoesEveryTurn()
    {
        var camera = Turned(2f);

        camera.FaceNorth();

        Assert.False(camera.IsTurned);
        Assert.Equal(0f, camera.TurnRad);
    }

    static Camera2D Turned(float turnRad)
    {
        var camera = new Camera2D(SimConfig.Shipped(), TownM, UiPx);
        camera.Turn(turnRad, UiPx * 0.5f, UiPx);
        return camera;
    }
}
