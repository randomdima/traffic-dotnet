using System.Numerics;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// OBS-2m's read-out: that it stands in the bottom-left corner, describes a unit of either kind, counts a
/// group rather than describing it, carries what a watch has against one body, and says all of it whether
/// or not the unit is on the picture.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class UnitPanelTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Vector2 Window = new(1600f, 900f);

    /// <summary>The pointer parked off every panel, so nothing is drawn hovered by accident.</summary>
    static readonly Vector2 Pointer = Window * 0.5f;

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

    static UnitPanel Drawn(TownWorld world, out int quads)
    {
        var panel = new UnitPanel();
        quads = Fill(panel, world);
        return panel;
    }

    static int Fill(UnitPanel panel, TownWorld world, ReadOnlySpan<ScenarioWatch> watching = default)
    {
        var draw = new ScreenDraw(new OverlayQuad[TownRenderer.OverlayCapacity]);
        panel.Draw(ref draw, Window, Pointer, world, watching);
        return draw.Written;
    }

    [Fact]
    public void NothingPickedOutIsNothingDrawn()
    {
        using var world = Town();
        world.SelectNone();

        var panel = Drawn(world, out var quads);

        Assert.Equal(default, panel.Box);
        Assert.Equal(0, quads);
    }

    /// <summary>
    /// <b>A group is counted rather than described</b> (CTL-1b): there is no such thing as the speed of
    /// thirty cars, so a set says how many of each kind it holds and nothing else.
    /// </summary>
    [Fact]
    public void AGroupIsCountedAndNotDescribed()
    {
        using var world = Town();
        Assert.True(world.Cars.Count >= 2, "the fixture stands fewer cars than a group needs");

        world.Select(new Selection(SelectionKind.Car, 0));
        var one = Drawn(world, out _).Rows;

        world.SelectAlso(new Selection(SelectionKind.Car, 1));
        var many = Drawn(world, out _);

        Assert.Equal(2, world.SelectedCount);
        Assert.True(many.Rows < one, "a group was described unit by unit");
        Assert.True(many.Rows > 0, "a group said nothing at all");
    }

    /// <summary>
    /// <b>What a watch has against this one body is written here</b> (OBS-2i) — which is the whole reason
    /// the claims section says nothing about any unit. A watch is asked about one unit and never about a
    /// set: a note names a body, and a panel saying "2 units" is already saying it is not about one.
    /// </summary>
    [Fact]
    public void AWatchsNoteAboutThisBodyIsOnThePanelAndNotOnAGroups()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));
        ScenarioWatch[] watching = [new TalkativeWatch()];

        var quiet = new UnitPanel();
        Fill(quiet, world);
        var noted = new UnitPanel();
        Fill(noted, world, watching);

        Assert.Equal(quiet.Rows + 1, noted.Rows);

        world.SelectAlso(new Selection(SelectionKind.Car, 1));
        var group = new UnitPanel();
        Fill(group, world, watching);
        var quietGroup = new UnitPanel();
        Fill(quietGroup, world);

        Assert.Equal(quietGroup.Rows, group.Rows);
    }

    [Fact]
    public void ItStandsInTheBottomLeftCorner()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var box = Drawn(world, out _).Box;

        Assert.Equal(Theme.MarginPx, box.AtPx.X, tolerance: 0.01f);
        Assert.Equal(Window.Y - Theme.MarginPx, box.Bottom, tolerance: 0.01f);
    }

    /// <summary>Both kinds of unit are described, in each one's own figures rather than in a shared subset.</summary>
    [Fact]
    public void ACarAndAWalkerAreBothDescribed()
    {
        using var world = Town();
        Assert.True(world.People.Count >= 1, "the fixture stands nobody to describe");

        world.Select(new Selection(SelectionKind.Car, 0));
        var car = Drawn(world, out _).Rows;

        world.Select(new Selection(SelectionKind.Person, 0));
        var walker = Drawn(world, out _).Rows;

        Assert.True(car > 0, "a car was described in no rows at all");
        Assert.True(walker > 0, "a walker was described in no rows at all");
    }

    /// <summary>
    /// <b>It does not need the unit on the picture.</b> Somebody riding in a car is not drawn and wears no
    /// brackets (PHY-7), and what they are doing is exactly what a reader who has just watched them get in
    /// is asking.
    /// </summary>
    [Fact]
    public void AUnitWithNothingDrawnOnScreenIsStillDescribed()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Person, 0));

        world.People.Inside[0] = new Contained(ContainerKind.Car, 0);
        Assert.False(
            SelectionMark.BoxOf(world, Config, world.Lead, out _, out _, out _),
            "the fixture drew a body that is inside something");

        Assert.True(Drawn(world, out _).Rows > 0, "a unit nothing is drawn for said nothing");
    }

    /// <summary>The title shuts the body, which is the whole of what a click on the panel does.</summary>
    [Fact]
    public void TheTitleShutsTheBodyAndTheRowsGoWithIt()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var panel = new UnitPanel();
        Fill(panel, world);
        var open = panel.Box;

        Assert.True(panel.Click(open.AtPx + new Vector2(open.SizePx.X * 0.5f, Theme.GapPx + 2f)));
        Fill(panel, world);

        Assert.False(panel.Open);
        Assert.Equal(0, panel.Rows);
        Assert.True(panel.Box.SizePx.Y < open.SizePx.Y, "the shut panel is as tall as the open one");
        Assert.Equal(Window.Y - Theme.MarginPx, panel.Box.Bottom, tolerance: 0.01f);
    }

    /// <summary>
    /// A click that lands on the panel is the panel's: a read-out about a car that could be clicked through
    /// would be the one panel in the interface that deselected its own subject.
    /// </summary>
    [Fact]
    public void AClickOnThePanelIsTakenAndOneOffItIsNot()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var panel = Drawn(world, out _);

        Assert.True(panel.Click(panel.Box.AtPx + panel.Box.SizePx * 0.5f));
        Assert.False(panel.Click(new Vector2(panel.Box.Right + 10f, panel.Box.AtPx.Y - 10f)));
    }

    /// <summary>Rule 2: it is laid every frame a unit is picked out, and every row is written on the stack.</summary>
    [Fact]
    public void DrawingThePanelAllocatesNothing()
    {
        using var world = Town();
        world.Select(new Selection(SelectionKind.Car, 0));

        var panel = new UnitPanel();
        var quads = new OverlayQuad[TownRenderer.OverlayCapacity];
        ScenarioWatch[] watching = [new TalkativeWatch()];
        for (var pass = 0; pass < 2; pass++) Lay(panel, world, quads, watching);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 64; pass++) Lay(panel, world, quads, watching);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    static void Lay(UnitPanel panel, TownWorld world, OverlayQuad[] quads, ReadOnlySpan<ScenarioWatch> watching)
    {
        var draw = new ScreenDraw(quads);
        panel.Draw(ref draw, Window, Pointer, world, watching);
    }
}
