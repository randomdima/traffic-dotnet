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

    /// <summary>A middle-drag moves the ground exactly as far as the pointer moved, or it slips under the hand.</summary>
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
}
