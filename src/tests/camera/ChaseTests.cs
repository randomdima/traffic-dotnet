using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Camera;

/// <summary>
/// MP-9: <b>a player's camera chases its car and nothing else moves it</b> — smoothly through a snapshot's step, at a
/// steady distance behind a steady car, straight onto a car too far off to chase, at the player's own span.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P7)]
public class ChaseTests
{
    static readonly Vector2 UiPx = new(1600f, 900f);
    static readonly SimConfig Config = SimConfig.Shipped();
    const float FrameS = 1f / 60f;

    static Camera2D Camera() => new(Config, new Vector2(2_000f), UiPx);

    [Fact]
    public void ACarStandingStillIsStoodOnOutrightTheFirstFrame()
    {
        var camera = Camera();
        var chase = new Chase(Config);

        chase.Step(camera, UiPx, new Vector2(300f, 400f), Vector2.Zero, FrameS);

        Assert.Equal(new Vector2(300f, 400f), camera.CentreM);
    }

    [Fact]
    public void ASnapshotsStepIsClosedOverSeveralFramesAndNotInOne()
    {
        var camera = Camera();
        var chase = new Chase(Config);
        chase.Step(camera, UiPx, new Vector2(300f, 400f), Vector2.Zero, FrameS);

        chase.Step(camera, UiPx, new Vector2(301f, 400f), Vector2.Zero, FrameS);

        Assert.InRange(camera.CentreM.X, 300f, 300.5f);
    }

    [Fact]
    public void ASteadyCarIsTrailedAtASteadyDistance()
    {
        var camera = Camera();
        var chase = new Chase(Config);
        var speedMps = new Vector2(10f, 0f);
        var carM = new Vector2(300f, 400f);
        var offsets = new List<float>();
        for (var frame = 0; frame < 600; frame++)
        {
            carM += speedMps * FrameS;
            chase.Step(camera, UiPx, carM, speedMps, FrameS);
            offsets.Add(camera.CentreM.X - carM.X);
        }

        Assert.Equal(offsets[^60], offsets[^1], 0.01f);
    }

    [Fact]
    public void ACarFurtherOffThanTheViewIsJumpedTo()
    {
        var camera = Camera();
        var chase = new Chase(Config);
        chase.Step(camera, UiPx, new Vector2(300f, 400f), Vector2.Zero, FrameS);

        chase.Step(camera, UiPx, new Vector2(1_500f, 400f), Vector2.Zero, FrameS);

        Assert.Equal(new Vector2(1_500f, 400f), camera.CentreM);
    }

    [Fact]
    public void ItShowsTheOpeningViewAcrossTheShortSideWhateverTheCameraWasAt()
    {
        var camera = Camera();
        camera.Zoom(5f, UiPx * 0.5f, UiPx);
        var chase = new Chase(Config);

        chase.Step(camera, UiPx, new Vector2(300f, 400f), Vector2.Zero, FrameS);

        Assert.Equal(Config.View.CameraDefaultViewM, camera.ViewSpanM(UiPx).Y, 1e-3f);
    }

    [Fact]
    public void TheWheelZoomsInAsFarAsAReadersCameraAndNoFurther()
    {
        var camera = Camera();
        var chase = new Chase(Config);

        chase.Zoom(200f);
        chase.Step(camera, UiPx, new Vector2(300f, 400f), Vector2.Zero, FrameS);

        Assert.Equal(camera.ZoomInStopPixelsPerMetre, camera.PixelsPerMetre, 1e-3f);
    }

    [Fact]
    public void ADragMovesTheViewWithTheHandAndNoFurtherThanTheMostPan()
    {
        var camera = Camera();
        var chase = new Chase(Config);
        var carM = new Vector2(300f, 400f);
        chase.Step(camera, UiPx, carM, Vector2.Zero, FrameS);

        chase.Drag(new Vector2(-10f * camera.PixelsPerMetre, 0f), camera.PixelsPerMetre);
        chase.Step(camera, UiPx, carM, Vector2.Zero, FrameS);
        var draggedM = camera.CentreM.X - carM.X;
        chase.Drag(new Vector2(-1_000f * camera.PixelsPerMetre, 0f), camera.PixelsPerMetre);
        chase.Step(camera, UiPx, carM, Vector2.Zero, FrameS);

        Assert.Equal(10f, draggedM, 1e-3f);
        Assert.Equal(Config.Multiplayer.MostPanM, camera.CentreM.X - carM.X, 1e-3f);
    }

    [Fact]
    public void AViewLetGoOfComesBackToItsCar()
    {
        var camera = Camera();
        var chase = new Chase(Config);
        var carM = new Vector2(300f, 400f);
        chase.Step(camera, UiPx, carM, Vector2.Zero, FrameS);
        chase.Drag(new Vector2(-30f * camera.PixelsPerMetre, 0f), camera.PixelsPerMetre);
        chase.Step(camera, UiPx, carM, Vector2.Zero, FrameS);

        chase.Release();
        for (var frame = 0; frame < 600; frame++) chase.Step(camera, UiPx, carM, Vector2.Zero, FrameS);

        Assert.Equal(carM.X, camera.CentreM.X, 0.01f);
    }

    [Fact]
    public void AWideScreenSeesNoMoreThanTheWidestViewAlongItsLongSide()
    {
        var wide = new Vector2(4_000f, 600f);
        var camera = new Camera2D(Config, new Vector2(2_000f), wide);
        var chase = new Chase(Config);

        chase.Step(camera, wide, new Vector2(300f, 400f), Vector2.Zero, FrameS);

        Assert.Equal(Config.Multiplayer.WidestViewM, camera.ViewSpanM(wide).X, 1e-2f);
    }
}
