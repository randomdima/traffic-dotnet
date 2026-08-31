using System.Numerics;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// Where a click goes before the town under it sees one. <b>The popups are the only thing on this
/// interface with two states</b>, so this is where the interface can be wrong about which of them it is
/// in — and nothing downstream could tell: every coordinate is a plausible one, and a town selected
/// through a panel looks exactly like a town somebody meant to select.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class InterfaceClickTests
{
    static readonly Vector2 Window = new(1600f, 1000f);

    static Vector2 Middle(Rect box) => box.AtPx + box.SizePx * 0.5f;

    /// <summary>
    /// An interface over a town somebody picked, with the menu laid where a draw would have laid it. <b>The
    /// map is opened first</b>: until one is, the menu is the one the game starts on and answers to none of
    /// the rules below (GEN-1b).
    /// </summary>
    static Interface Running()
    {
        var ui = new Interface(new TrimFigures());
        ui.TownChanged();
        ui.Menu.Lay(Window, Chrome.GearAt(Window));
        return ui;
    }

    /// <summary>And the interface a run opens on: the start menu, over the idle ring nobody picked.</summary>
    static Interface AtTheStart()
    {
        var ui = new Interface(new TrimFigures());
        ui.TownChanged(behindTheMenu: true);
        ui.Menu.Lay(Window, Chrome.GearAt(Window));
        return ui;
    }

    static ClickTaken Click(Interface ui, Vector2 atPx, bool hasTown = true) =>
        ui.Click(atPx, Window, primary: true, hasTown, out _);

    /// <summary>
    /// <b>The button that opens a popup shuts it.</b> A gear that only ever opened was a gear somebody
    /// pressed twice and then went looking for a close button.
    /// </summary>
    [Fact]
    public void TheGearOpensAndShutsTheMenu()
    {
        var ui = Running();
        ui.Menu.Shut();

        Assert.Equal(ClickTaken.Yes, Click(ui, Middle(Chrome.GearAt(Window))));
        Assert.True(ui.Menu.Open);

        Assert.Equal(ClickTaken.Yes, Click(ui, Middle(Chrome.GearAt(Window))));
        Assert.False(ui.Menu.Open);
    }

    /// <summary>Two panels hanging off two corners at once is a screen with more chrome than town on it.</summary>
    [Fact]
    public void OpeningOnePopupShutsTheOther()
    {
        var ui = Running();
        ui.Menu.Show();

        Click(ui, Middle(Chrome.HelpAt(Window)));
        Assert.True(ui.Controls.Open);
        Assert.False(ui.Menu.Open);

        Click(ui, Middle(Chrome.GearAt(Window)));
        Assert.True(ui.Menu.Open);
        Assert.False(ui.Controls.Open);
    }

    /// <summary>
    /// <b>A click off an open popup shuts it, and is taken.</b> Dismissing a panel and selecting the car
    /// that happened to be under the pointer are two intentions, and one click is one of them.
    /// </summary>
    [Fact]
    public void AClickOffAnOpenPopupShutsItAndTheTownDoesNotAlsoSeeIt()
    {
        var ui = Running();
        ui.Menu.Show();

        Assert.Equal(ClickTaken.Yes, Click(ui, new Vector2(40f, 600f)));
        Assert.False(ui.Menu.Open);

        // And with everything shut the same click belongs to the town.
        Assert.Equal(ClickTaken.No, Click(ui, new Vector2(40f, 600f)));
    }

    /// <summary>A click on the panel itself acts on the panel and never reaches the town behind it.</summary>
    [Fact]
    public void AClickOnTheMenuStaysOnTheMenu()
    {
        var ui = Running();
        ui.Menu.Show();

        Assert.Equal(ClickTaken.Yes, Click(ui, Middle(ui.Menu.Box)));
        Assert.True(ui.Menu.Open);
    }

    /// <summary>
    /// <b>GEN-1b: the start menu cannot be shut</b> — not by a click off it, not by the gear it does not
    /// hang from, and not by anything else. What is behind it is a ring nobody chose, so shutting it would
    /// leave a screen with no way back to the map list.
    /// </summary>
    [Fact]
    public void TheStartMenuCannotBeClickedAway()
    {
        var ui = AtTheStart();

        Assert.Equal(ClickTaken.Yes, Click(ui, new Vector2(40f, 600f)));
        Assert.True(ui.Menu.Open);

        Assert.Equal(ClickTaken.Yes, Click(ui, Middle(Chrome.GearAt(Window))));
        Assert.True(ui.Menu.Open);

        ui.Menu.Shut();
        ui.Menu.Toggle();
        Assert.True(ui.Menu.Open);
    }

    /// <summary>
    /// And it stands in the middle of the window rather than under the gear, because it is the whole of
    /// what is on screen rather than a popup beside a town.
    /// </summary>
    [Fact]
    public void TheStartMenuStandsInTheMiddleOfTheWindow()
    {
        var start = AtTheStart();

        Assert.Equal(Window.X * 0.5f, Middle(start.Menu.Box).X, 0.5f);
        Assert.Equal(Window.Y * 0.5f, Middle(start.Menu.Box).Y, 0.5f);
    }

    /// <summary>
    /// <b>And the map it opens is laid out of it.</b> Picking a map moves nothing the layout was keyed on —
    /// same window, same button — so the panel kept the narrow centred rectangles it was laid with and the
    /// gear's popup was the start menu for the rest of the run.
    /// </summary>
    [Fact]
    public void AMapPickedLeavesThePopupAndNotTheStartMenu()
    {
        var ui = AtTheStart();
        var startPx = ui.Menu.Box.SizePx.X;

        ui.TownChanged();
        ui.Menu.Show();

        Assert.False(ui.Menu.AtTheStart);
        Assert.NotEqual(startPx, ui.Menu.Box.SizePx.X);
        Assert.Equal(Chrome.GearAt(Window).Right, ui.Menu.Box.Right, 3);
        Assert.Equal(Chrome.GearAt(Window).Bottom + Theme.GapPx, ui.Menu.Box.AtPx.Y, 3);
    }

    /// <summary>
    /// <b>GEN-1b: a map picked shuts the menu onto it, and the idle ring the game opens on does not.</b>
    /// A run that stood its own town up and then dropped the reader into it would be the game making the
    /// choice the menu is there to offer.
    /// </summary>
    [Fact]
    public void AMapPickedShutsTheMenuAndTheOneBehindItDoesNot()
    {
        var ui = Running();
        ui.Menu.Show();
        ui.TownChanged();
        Assert.False(ui.Menu.Open);
        Assert.False(ui.Menu.AtTheStart);

        ui.TownChanged(behindTheMenu: true);
        Assert.True(ui.Menu.Open);
        Assert.True(ui.Menu.AtTheStart);
    }

    /// <summary>
    /// The wheel is the menu's only where the menu is. A camera that stopped zooming because a panel
    /// was open somewhere else on the screen is a town that has gone stiff for no visible reason.
    /// </summary>
    [Fact]
    public void TheWheelIsThePanelsOnlyOverThePanel()
    {
        var ui = Running();
        ui.Menu.Show();

        Assert.True(ui.WheelIsThePanels(Middle(ui.Menu.Box)));
        Assert.False(ui.WheelIsThePanels(new Vector2(40f, 600f)));

        ui.Menu.Shut();
        Assert.False(ui.WheelIsThePanels(Middle(ui.Menu.Box)));
    }
}
