using System.Numerics;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Screen;
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

    /// <summary>An interface over a standing town, with the menu laid where a draw would have laid it.</summary>
    static Interface Running()
    {
        var ui = new Interface();
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
    /// <b>GEN-1b: with no town there is nothing to shut the menu onto</b>, so a click off it is dropped
    /// rather than leaving an empty screen with no way back to the map list.
    /// </summary>
    [Fact]
    public void WithNoTownTheMenuCannotBeClickedAway()
    {
        var ui = Running();
        ui.Menu.Show();

        Assert.Equal(ClickTaken.Yes, Click(ui, new Vector2(40f, 600f), hasTown: false));
        Assert.True(ui.Menu.Open);
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
