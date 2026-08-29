using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Screen;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// The menu is laid out in arithmetic and drawn from the rectangles that arithmetic produced, so the
/// things a reader would otherwise have to check by looking at it are checked here instead: that the
/// popup hangs off the button that opens it, that tabbing moves nothing, that a group opens and shuts,
/// and that a list longer than the window scrolls rather than growing off the bottom of it.
/// </summary>
/// <remarks>
/// <b>Each of these was wrong at some point and none of them could fail loudly.</b> A layout that is
/// plainly wrong in a picture produces no error anywhere.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class MenuLayoutTests
{
    static readonly Vector2 Window = new(1600f, 1000f);

    static Rect Gear(Vector2 uiPx) => Chrome.GearAt(uiPx);

    static Menu Laid(Vector2 uiPx)
    {
        var menu = new Menu();
        menu.Lay(uiPx, Gear(uiPx));
        return menu;
    }

    static MenuChoice Click(Menu menu, Vector2 atPx) => menu.Click(atPx, new DebugSwitches());

    /// <summary>
    /// <b>The popup comes out of the button that opened it</b>: under the gear, and aligned to the
    /// gear's trailing edge. A panel that opened in the middle of the screen would be a panel with no
    /// visible connection to what was pressed.
    /// </summary>
    [Fact]
    public void ThePopupHangsUnderTheGear()
    {
        var menu = Laid(Window);
        var gear = Gear(Window);

        Assert.Equal(gear.Right, menu.Box.Right, 3);
        Assert.Equal(gear.Bottom + Theme.GapPx, menu.Box.AtPx.Y, 3);
        Assert.True(menu.Box.Bottom <= Window.Y, $"the panel reaches {menu.Box.Bottom} of {Window.Y}");
    }

    /// <summary>
    /// <b>The way out is the third tab</b>, and it is the one thing on the menu that does not come
    /// back — which is why it is a button standing in the tab strip rather than a page behind one.
    /// </summary>
    [Fact]
    public void TheThirdTabLeavesTheGame()
    {
        var menu = Laid(Window);

        Assert.Equal(MenuAction.Quit, Click(menu, menu.TabMiddlePx(Menu.ExitTab)).Action);
        Assert.Equal(Menu.Maps, menu.Page);

        Assert.Equal(MenuAction.None, Click(menu, menu.TabMiddlePx(Menu.Debug)).Action);
        Assert.Equal(Menu.Debug, menu.Page);
    }

    /// <summary>
    /// The panel is one width and one place on the screen for both of its pages, so tabbing moves
    /// nothing: a menu whose tabs walk out from under the pointer is a menu that mis-clicks.
    /// </summary>
    [Fact]
    public void TabbingMovesNothingSideways()
    {
        var menu = Laid(Window);
        var box = menu.Box;

        for (var page = 0; page <= Menu.Debug; page++)
        {
            menu.OpenAt(page);
            menu.Lay(Window, Gear(Window));
            Assert.Equal(box.AtPx.X, menu.Box.AtPx.X);
            Assert.Equal(box.SizePx.X, menu.Box.SizePx.X);
        }
    }

    /// <summary>
    /// <b>The maps are open and the scenarios are not</b>, and clicking a group's own header is what
    /// swaps them: a menu of two cities should not read as a menu of two cities and a laboratory.
    /// </summary>
    [Fact]
    public void TheScenariosAreBehindAGroupThatStartsShut()
    {
        var menu = Laid(Window);

        Assert.True(menu.IsGroupOpen(Menu.MainMaps));
        Assert.False(menu.IsGroupOpen(Menu.Scenarios));

        var places = MapCatalogue.On(MapKind.Place).Length;
        Assert.Equal(places + 2, menu.RowCount);

        // The scenarios group is the row after the last place, and pressing it lays every scenario
        // under it without moving anything above it.
        Assert.Equal(MenuAction.None, Click(menu, menu.RowMiddlePx(places + 1)).Action);
        Assert.True(menu.IsGroupOpen(Menu.Scenarios));
        Assert.Equal(places + MapCatalogue.On(MapKind.Scenario).Length + 2, menu.RowCount);
    }

    /// <summary>
    /// A row under a group header opens that map, and the row a click lands on is the row the layout
    /// drew there — which is the one question a hit test and a layout can disagree about.
    /// </summary>
    [Fact]
    public void AMapRowOpensTheMapItNames()
    {
        var menu = Laid(Window);
        var places = MapCatalogue.On(MapKind.Place);
        Assert.NotEmpty(places);

        var first = Click(menu, menu.RowMiddlePx(1));
        Assert.Equal(MenuAction.OpenMap, first.Action);
        Assert.Equal(places[0].Name, first.Name);
    }

    /// <summary>
    /// A page with more rows than the window has room for is every map at once on a short display, and
    /// it was the panel drawn past the bottom edge of the screen with the last of them under it.
    /// </summary>
    [Fact]
    public void APageTallerThanTheWindowScrollsInsteadOfGrowingPastIt()
    {
        var shortWindow = new Vector2(1400f, 320f);
        var menu = new Menu();
        menu.OpenGroup(Menu.Scenarios);
        menu.Lay(shortWindow, Gear(shortWindow));

        var places = MapCatalogue.On(MapKind.Place);
        var scenarios = MapCatalogue.On(MapKind.Scenario);
        Assert.True(menu.Box.Bottom <= shortWindow.Y, $"the panel reaches {menu.Box.Bottom} of {shortWindow.Y}");
        Assert.Equal(places.Length + scenarios.Length + 2, menu.RowCount);

        var first = Click(menu, menu.RowMiddlePx(1));
        Assert.Equal(MenuAction.OpenMap, first.Action);
        Assert.Equal(places[0].Name, first.Name);

        // Two notches down, and the row the layout puts under the pointer is the row the hit test
        // reads back — which is the same question the unscrolled page is asked above.
        menu.Scroll(-2f);
        var scrolled = Click(menu, menu.RowMiddlePx(places.Length + 2));
        Assert.Equal(MenuAction.OpenMap, scrolled.Action);
        Assert.Equal(scenarios[0].Name, scrolled.Name);
    }

    /// <summary>
    /// <b>The row that is drawn and the switch that is toggled are the same one.</b> They were two
    /// switch statements, and a layer inserted in the middle of the list toggled its neighbour.
    /// </summary>
    [Fact]
    public void EachDebugRowTogglesTheSwitchItNames()
    {
        var menu = Laid(Window);
        menu.OpenAt(Menu.Debug);
        menu.Lay(Window, Gear(Window));

        var switches = new DebugSwitches();
        menu.Click(menu.LineMiddlePx(0), switches);
        Assert.True(switches.CarLines);

        menu.Click(menu.LineMiddlePx(4), switches);
        Assert.True(switches.Collision);

        menu.Click(menu.LineMiddlePx(5), switches);
        Assert.True(switches.TurnCircles);

        // And the one that starts on goes off, which no other row can be mistaken for.
        menu.Click(menu.LineMiddlePx(7), switches);
        Assert.False(switches.TrackFigures);
    }
}
