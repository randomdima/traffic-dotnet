using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// CTL-1's read-out: that it stands at the selected unit rather than in a corner, that it clears the
/// brackets round that unit and stays on the window, that nothing off the picture is labelled, and that
/// what a watch has to say about that one body is said here.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class UnitLabelTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Vector2 Window = new(1600f, 900f);

    /// <summary>A watch with something to say about every body, so a note is drawn without staging a crash.</summary>
    sealed class TalkativeWatch()
        : ScenarioWatch("the stub", "a watch that has something to say", ["a claim"], [])
    {
        public override ClaimVerdict Verdict(int claim) => ClaimVerdict.Kept;

        public override void Says(int claim, ref TextBuffer into) => into.Add("figures");

        public override void Reads(int reading, ref TextBuffer into)
        {
        }

        public override bool Notes(SelectionKind kind, int index, ref TextBuffer into)
        {
            into.Add("something this watch has against this very body");
            return true;
        }

        public override void Saw(TownWorld world)
        {
        }
    }

    static TownWorld Town() => new(Towns.Of(Towns.Fixture), Config);

    /// <summary>A camera looking straight at one unit, so the label's anchor is the middle of the window.</summary>
    static Camera2D LookingAt(TownWorld world, Vector2 pointM)
    {
        var camera = new Camera2D(Config, world.Plan.WorldSizeM, Window);
        camera.SetSpan(80f, Window);
        camera.LookAt(pointM);
        return camera;
    }

    static Rect Label(TownWorld world, Camera2D camera, ReadOnlySpan<ScenarioWatch> watching, out int quads)
    {
        var draw = new ScreenDraw(new OverlayQuad[TownRenderer.OverlayCapacity]);
        var box = UnitLabel.Draw(ref draw, Window, world, Config, camera, watching);
        quads = draw.Written;
        return box;
    }

    [Fact]
    public void NothingSelectedIsNothingDrawn()
    {
        using var world = Town();
        world.SelectNone();

        var box = Label(world, LookingAt(world, world.Cars.PositionM[0]), default, out var quads);

        Assert.Equal(default, box);
        Assert.Equal(0, quads);
    }

    /// <summary>
    /// <b>It stands at the unit and clear of it.</b> A label over the bodywork hides what it is pointing
    /// at, and the brackets are drawn round that same box (<c>SelectionMark</c>).
    /// </summary>
    [Fact]
    public void ItStandsBesideTheUnitAndNotOverIt()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var atM = world.Cars.PositionM[0];
        var camera = LookingAt(world, atM);
        var box = Label(world, camera, default, out _);
        var atPx = camera.ScreenAt(atM, Window);

        Assert.False(box.Contains(atPx), "the label is drawn over the unit it is about");

        ref readonly var build = ref world.Cars.BuildOf(0);
        var clearPx = MathF.Max(build.LengthM, build.WidthM) * 0.5f * camera.PixelsPerMetre;
        Assert.True(
            MathF.Abs(box.AtPx.X - atPx.X) > clearPx || MathF.Abs(box.Right - atPx.X) > clearPx,
            "the label is inside the box the brackets wrap");

        // And level with it, which is what says it belongs to that unit and not to the one above it.
        Assert.InRange(atPx.Y, box.AtPx.Y, box.Bottom);
    }

    /// <summary>
    /// <b>It follows the unit.</b> A label that stayed where it was drawn first is a corner read-out with
    /// extra steps: what it is about has to be in no doubt however many units are on screen.
    /// </summary>
    [Fact]
    public void ItMovesWithTheUnitItIsAbout()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var atM = world.Cars.PositionM[0];
        var camera = LookingAt(world, atM);
        var before = Label(world, camera, default, out _);

        world.Cars.PositionM[0] = atM + new Vector2(6f, 0f);
        var after = Label(world, camera, default, out _);

        Assert.True(after.AtPx.X > before.AtPx.X, "the label did not follow the car it is about");
    }

    /// <summary>
    /// <b>It never runs off the window.</b> A unit against the right edge takes its label on the other
    /// side rather than having it pinned into the margin on top of the unit.
    /// </summary>
    [Fact]
    public void ItStaysOnTheWindowAndFlipsRatherThanCoveringTheUnit()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var atM = world.Cars.PositionM[0];
        var camera = LookingAt(world, atM);

        // Pan so the car sits just inside the right edge, where the label cannot fit beside it.
        camera.PanByPixels(new Vector2(Window.X * 0.5f, 0f) - new Vector2(30f, 0f));
        var atPx = camera.ScreenAt(atM, Window);
        Assert.InRange(atPx.X, 0f, Window.X);

        var box = Label(world, camera, default, out _);
        Assert.True(box.AtPx.X >= 0f, "the label starts off the left edge");
        Assert.True(box.Right <= Window.X, "the label runs off the right edge");
        Assert.True(box.Bottom <= Window.Y, "the label runs off the bottom");
        Assert.False(box.Contains(atPx), "the label was pinned over the unit rather than flipped");
    }

    /// <summary>
    /// <b>A unit the camera has left behind is not labelled.</b> Clamped onto the window it would stand
    /// against an edge the unit is nowhere near, which is a read-out that lies about where its subject is.
    /// </summary>
    [Fact]
    public void AUnitOffThePictureIsNotLabelled()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var camera = LookingAt(world, world.Cars.PositionM[0]);
        camera.PanByPixels(new Vector2(4_000f, 0f));

        Assert.Equal(default, Label(world, camera, default, out var quads));
        Assert.Equal(0, quads);
    }

    /// <summary>
    /// <b>What a watch has against this one body is said beside that body</b> — which is the whole reason
    /// the claims panel says nothing about any unit. A watch with nothing to say adds no row.
    /// </summary>
    [Fact]
    public void AWatchsNoteAboutThisBodyIsDrawnOnItsLabel()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var camera = LookingAt(world, world.Cars.PositionM[0]);
        var quiet = Label(world, camera, default, out var quietQuads);
        var noted = Label(world, camera, new ScenarioWatch[] { new TalkativeWatch() }, out var notedQuads);

        Assert.True(noted.SizePx.Y > quiet.SizePx.Y, "the note took no room on the label");
        Assert.True(notedQuads > quietQuads, "the note drew nothing");
    }

    /// <summary>
    /// <b>A group is counted rather than described</b> (CTL-1b), so no watch is asked about a set: a note
    /// names one body, and a label saying "2 units" is already saying it is not about one.
    /// </summary>
    [Fact]
    public void AGroupIsCountedAndCarriesNoNotes()
    {
        using var world = Town();
        Assert.True(world.Cars.Count >= 2, "the fixture stands fewer cars than a group needs");

        world.Select(new Selection(SelectionKind.Car, 0));
        var camera = LookingAt(world, world.Cars.PositionM[0]);
        ScenarioWatch[] watching = [new TalkativeWatch()];
        var one = Label(world, camera, watching, out _);

        world.SelectAlso(new Selection(SelectionKind.Car, 1));
        var many = Label(world, camera, watching, out _);

        Assert.Equal(2, world.SelectedCount);
        Assert.True(many.SizePx.Y < one.SizePx.Y, "a group's label carried a note about one body");
    }

    /// <summary>
    /// Rule 2: it is laid every frame a unit is selected, and every line on it is written into a buffer on
    /// the stack.
    /// </summary>
    [Fact]
    public void DrawingTheLabelAllocatesNothing()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var camera = LookingAt(world, world.Cars.PositionM[0]);
        var quads = new OverlayQuad[TownRenderer.OverlayCapacity];
        ScenarioWatch[] watching = [new TalkativeWatch()];

        for (var pass = 0; pass < 2; pass++) Fill(world, camera, quads, watching);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 64; pass++) Fill(world, camera, quads, watching);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    static void Fill(TownWorld world, Camera2D camera, OverlayQuad[] quads, ReadOnlySpan<ScenarioWatch> watching)
    {
        var draw = new ScreenDraw(quads);
        UnitLabel.Draw(ref draw, Window, world, Config, camera, watching);
    }
}
