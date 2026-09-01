using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
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

    /// <summary>
    /// The popup under the gear, which is what the menu is once a map has been picked. <b>The start menu
    /// is laid by other rules</b> (GEN-1b) and is staged by <see cref="AtTheStart"/>.
    /// </summary>
    static Menu Laid(Vector2 uiPx)
    {
        var menu = new Menu();
        menu.ShutOntoTheTown();
        menu.Show();
        menu.Lay(uiPx, Gear(uiPx));
        return menu;
    }

    static Menu AtTheStart(Vector2 uiPx)
    {
        var menu = new Menu();
        menu.StandAtTheStart();
        menu.Lay(uiPx, Gear(uiPx));
        return menu;
    }

    static readonly float DragPx = SimConfig.Shipped().View.PointerDragPx;

    /// <summary>
    /// A tap: down and up in the same spot. <b>A row is opened on the way up</b> (CTL-1b), since a press
    /// on the map list starts a scroll as readily as it picks a map, so a test that only pressed would be
    /// testing half a gesture.
    /// </summary>
    static MenuChoice Click(Menu menu, Vector2 atPx)
    {
        var trims = new TrimFigures();
        var pressed = menu.Click(atPx, new DebugSwitches(), trims);
        var lifted = menu.Pointer(atPx, held: false, DragPx, trims);
        return pressed.Action == MenuAction.None ? lifted : pressed;
    }

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
    /// <b>GEN-1b — the start menu hangs off nothing and is laid to the window rather than to its rows.</b>
    /// It stands in the middle, and it is laid narrower than the popup under the gear even though its names
    /// are written larger: the popup is as wide as the longest description in the catalogue, and this one
    /// is a share of the window with those descriptions wrapped into it.
    /// </summary>
    [Fact]
    public void TheStartMenuStandsInTheMiddleAndIsLaidToTheWindow()
    {
        var start = AtTheStart(Window);
        var popup = Laid(Window);

        Assert.Equal(Window.X * 0.5f, start.Box.AtPx.X + (start.Box.SizePx.X * 0.5f), 3);
        Assert.Equal(Window.Y * 0.5f, start.Box.AtPx.Y + (start.Box.SizePx.Y * 0.5f), 3);
        Assert.True(
            start.Box.SizePx.X < popup.Box.SizePx.X,
            $"the start menu is {start.Box.SizePx.X} wide against the popup's {popup.Box.SizePx.X}");
        Assert.True(start.Box.AtPx.Y >= 0f && start.Box.Bottom <= Window.Y, "the start menu is off the window");
    }

    /// <summary>
    /// <b>A description too long for the width it is laid at is broken across lines and never cut</b>, and
    /// the row grows by exactly the lines it came to — so the popup, which is laid wide enough for the
    /// longest of them, breaks none and keeps the row height the theme ships.
    /// </summary>
    [Fact]
    public void ADescriptionWrapsToTheStartMenuAndNotToThePopup()
    {
        var start = AtTheStart(Window);
        var popup = Laid(Window);

        // The first map's row, which is the one under the first group header.
        Assert.True(
            start.RowHeightPx(1) > popup.RowHeightPx(1),
            $"a wrapped row is {start.RowHeightPx(1)} against the popup's unwrapped {popup.RowHeightPx(1)}");

        Assert.Equal(Theme.TallRowPx, popup.RowHeightPx(1), 3);
    }

    /// <summary>
    /// <b>And it is one size and one place whatever is open in it.</b> What it has to stay inside is the
    /// field in the middle of the idle ring, so its height is the field's and not the list's: a group shut
    /// takes rows off the list and does not move an edge of the panel, and a list too long for it scrolls.
    /// </summary>
    [Fact]
    public void TheStartMenuIsOneSizeWhateverIsOpenInIt()
    {
        var menu = AtTheStart(Window);
        var box = menu.Box;
        var rows = menu.RowCount;

        // Row 0 is the first group's own header, and clicking a header shuts the group under it.
        Click(menu, menu.RowMiddlePx(0));
        menu.Lay(Window, Gear(Window));

        Assert.True(menu.RowCount < rows, $"shutting a group left {menu.RowCount} rows of {rows}");
        Assert.Equal(box.AtPx.X, menu.Box.AtPx.X, 3);
        Assert.Equal(box.AtPx.Y, menu.Box.AtPx.Y, 3);
        Assert.Equal(box.SizePx.X, menu.Box.SizePx.X, 3);
        Assert.Equal(box.SizePx.Y, menu.Box.SizePx.Y, 3);
        Assert.True(menu.Box.Bottom <= Window.Y, $"the panel grew to {menu.Box.Bottom} of {Window.Y}");
    }

    /// <summary>
    /// <b>And the popup reaches no further than half way down the window however much is open in it.</b>
    /// It is furniture beside a town, and one running from the gear to the bottom edge is the full-screen
    /// panel it replaced — over the very town its rows are questions about.
    /// </summary>
    [Fact]
    public void ThePopupReachesNoFurtherThanHalfWayDownTheWindow()
    {
        var menu = Laid(Window);
        var shutPx = menu.Box.SizePx.Y;

        menu.OpenGroup(Menu.Scenarios);
        menu.Lay(Window, Gear(Window));

        Assert.True(menu.Box.SizePx.Y > shutPx, "opening a group grew the popup by nothing at all");
        Assert.True(
            menu.Box.Bottom <= Window.Y * 0.5f, $"the popup reaches {menu.Box.Bottom} of {Window.Y}");
    }

    /// <summary>
    /// <b>The start menu opens on the whole catalogue and the popup on the places alone</b> (OBS-2a).
    /// Nothing is running behind the start menu, so the mis-click the popup's shut group is protecting
    /// against costs nobody a game there, and what a reader is at it for is reading the list.
    /// </summary>
    [Fact]
    public void TheStartMenuOpensBothGroupsAndThePopupOnlyThePlaces()
    {
        var start = AtTheStart(Window);
        var popup = Laid(Window);

        Assert.True(start.IsGroupOpen(Menu.MainMaps) && start.IsGroupOpen(Menu.Scenarios), "a group is shut");
        Assert.True(popup.IsGroupOpen(Menu.MainMaps), "the popup's places are shut");
        Assert.False(popup.IsGroupOpen(Menu.Scenarios), "the popup's scenarios are open");
    }

    /// <summary>
    /// And it carries the map list and the way out and nothing else: the debug switches and the trim
    /// figures are things to do to a town that is running, and no town is.
    /// </summary>
    [Fact]
    public void TheStartMenuCarriesTheMapListAndTheWayOut()
    {
        var start = AtTheStart(Window);

        Assert.Equal(Menu.Maps, start.Page);
        Assert.Equal(MenuAction.Quit, Click(start, start.TabMiddlePx(Menu.ExitTab)).Action);

        // The pages it does not carry were laid as no rectangle, so nothing lands on them.
        Assert.Equal(MenuAction.None, Click(start, start.TabMiddlePx(Menu.Debug)).Action);
        Assert.Equal(Menu.Maps, start.Page);
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
        var menu = Laid(shortWindow);
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
    /// <b>And it is dragged as well as wheeled</b>, because a handset has no wheel to take (CTL-9): a press
    /// that travels up the list carries the rows with it and opens nothing, and the row it started on is
    /// still the row the layout draws where it has moved to.
    /// </summary>
    [Fact]
    public void DraggingAPageThatScrollsCarriesTheRowsAndOpensNoMap()
    {
        var shortWindow = new Vector2(1400f, 320f);
        var menu = Laid(shortWindow);
        menu.OpenGroup(Menu.Scenarios);
        menu.Lay(shortWindow, Gear(shortWindow));

        var places = MapCatalogue.On(MapKind.Place);
        var wasPx = menu.RowMiddlePx(1);

        // Up the panel by exactly what the group header above the first map takes, which is one row of
        // travel and so one row of list.
        var trims = new TrimFigures();
        var toPx = wasPx - new Vector2(0f, menu.RowHeightPx(0) + Theme.GapPx);
        menu.Click(wasPx, new DebugSwitches(), trims);
        menu.Pointer(toPx, held: true, DragPx, trims);
        Assert.Equal(MenuAction.None, menu.Pointer(toPx, held: false, DragPx, trims).Action);

        var nowPx = menu.RowMiddlePx(1);
        Assert.True(nowPx.Y < wasPx.Y, $"the first map stayed at {nowPx.Y} of {wasPx.Y}");
        Assert.Equal(places[0].Name, Click(menu, nowPx).Name);
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
        menu.Click(menu.LineMiddlePx(0), switches, new TrimFigures());
        Assert.True(switches.CarLines);

        menu.Click(menu.LineMiddlePx(4), switches, new TrimFigures());
        Assert.True(switches.Collision);

        menu.Click(menu.LineMiddlePx(5), switches, new TrimFigures());
        Assert.True(switches.TurnCircles);

        // And the one that starts on goes off, which no other row can be mistaken for.
        menu.Click(menu.LineMiddlePx(7), switches, new TrimFigures());
        Assert.False(switches.TrackFigures);
    }

    static Menu OnTheFigures()
    {
        var menu = Laid(Window);
        menu.OpenAt(Menu.Figures);
        menu.Lay(Window, Gear(Window));
        return menu;
    }

    /// <summary>
    /// <b>The middle of a track is the figure the build ships</b>, which is what makes a decade either side
    /// readable: a click that lands dead centre has to come back at exactly one, not near it, or the panel
    /// cannot be put back where it started by eye.
    /// </summary>
    [Fact]
    public void TheMiddleOfEveryTrackIsTheShippedFigure()
    {
        var menu = OnTheFigures();
        var trims = new TrimFigures();

        for (var trim = 0; trim < TrimFigures.Count; trim++)
        {
            trims.Set(trim, 4f);
            menu.Click(menu.TrimMiddlePx(trim), new DebugSwitches(), trims);
            Assert.Equal(1f, trims.Of(trim), 3);
        }
    }

    /// <summary>
    /// The same rule the debug switches have: the row that is drawn and the figure that moves are one
    /// place, so a trim inserted in the middle of the list cannot drag its neighbour.
    /// </summary>
    [Fact]
    public void EachFigureRowMovesTheTrimItNames()
    {
        var menu = OnTheFigures();
        var trims = new TrimFigures();

        for (var trim = 0; trim < TrimFigures.Count; trim++)
        {
            menu.Click(menu.TrimAtPx(trim, TrimFigures.Most), new DebugSwitches(), trims);
            menu.Pointer(Vector2.Zero, held: false, DragPx, trims);

            for (var other = 0; other < TrimFigures.Count; other++)
            {
                Assert.Equal(other <= trim ? TrimFigures.Most : 1f, trims.Of(other), 3);
            }
        }
    }

    /// <summary>
    /// <b>A figure takes effect under the hand that is moving it</b>, which is what the page is for: a town
    /// that answered only on release would make every drag a guess followed by a wait. <b>A pointer held
    /// still is not a move</b>, though — including one held against a stop, where the clamp means the
    /// pointer keeps travelling and the figure does not.
    /// </summary>
    [Fact]
    public void AFigureTakesEffectAsItIsDraggedAndNotOnlyWhenItIsLetGo()
    {
        var menu = OnTheFigures();
        var trims = new TrimFigures();

        menu.Click(menu.TrimAtPx(0, 3f), new DebugSwitches(), trims);
        Assert.True(menu.TakeFiguresMoved());

        menu.Pointer(menu.TrimAtPx(0, 5f), held: true, DragPx, trims);
        Assert.True(menu.TakeFiguresMoved());
        Assert.Equal(5f, trims.Friction, 2);

        // Taken rather than read: the town is stood up again once for a figure and not every frame after it.
        Assert.False(menu.TakeFiguresMoved());

        menu.Pointer(menu.TrimAtPx(0, 5f), held: true, DragPx, trims);
        Assert.False(menu.TakeFiguresMoved());

        // Past the end of the track, where the clamp holds the figure while the pointer runs on.
        var pastTheStopPx = menu.TrimAtPx(0, TrimFigures.Most) + new Vector2(200f, 0f);
        menu.Pointer(pastTheStopPx, held: true, DragPx, trims);
        Assert.True(menu.TakeFiguresMoved());
        menu.Pointer(pastTheStopPx + new Vector2(500f, 0f), held: true, DragPx, trims);
        Assert.False(menu.TakeFiguresMoved());

        // And letting go is not a second move of a figure already standing where it was left.
        menu.Pointer(pastTheStopPx, held: false, DragPx, trims);
        Assert.False(menu.TakeFiguresMoved());
    }

    /// <summary>The row under the tracks is the way back, and it puts every one of them back at once.</summary>
    [Fact]
    public void TheResetRowPutsEveryFigureBackWhereItShipped()
    {
        var menu = OnTheFigures();
        var trims = new TrimFigures();

        for (var trim = 0; trim < TrimFigures.Count; trim++) trims.Set(trim, TrimFigures.Least);
        Assert.False(trims.Untouched);

        menu.Click(menu.TrimMiddlePx(Menu.ResetRow), new DebugSwitches(), trims);
        Assert.True(trims.Untouched);
        Assert.True(menu.TakeFiguresMoved());
    }
}
