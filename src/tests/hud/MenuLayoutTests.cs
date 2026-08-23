using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Screen;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// The menu is laid out in arithmetic and drawn from the rectangles that arithmetic produced, so the
/// things a reader would otherwise have to check by looking at it are checked here instead: that the
/// panel keeps the same padding on every edge, that the footer says what it can do, and that a page
/// longer than the window scrolls rather than growing off the bottom of it.
/// </summary>
/// <remarks>
/// <b>Each of these was wrong at some point and none of them could fail loudly.</b> The footer used to
/// sit two pixels off the bottom of a panel padded by fourteen everywhere else — a layout that is
/// plainly wrong in a picture and produces no error anywhere.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class MenuLayoutTests
{
    static readonly Vector2 Window = new(1600f, 1000f);

    static MenuChoice Click(Menu menu, Vector2 atPx, bool hasTown) =>
        menu.Click(atPx, hasTown, new DebugSwitches(), new RunState());

    /// <summary>The middle of the band the footer occupies, worked out from the panel and the theme alone.</summary>
    static Vector2 FooterMiddlePx(Menu menu, float acrossFraction) => new(
        menu.Box.AtPx.X + menu.Box.SizePx.X * acrossFraction,
        menu.Box.Bottom - Theme.PaddingPx - Theme.RowPx * 0.5f);

    [Fact]
    public void TheFooterIsOnePaddingOffTheBottomOfThePanel()
    {
        var menu = new Menu();
        menu.Lay(Window);

        Assert.Equal(MenuAction.Quit, Click(menu, FooterMiddlePx(menu, 0.5f), hasTown: false).Action);

        // And the band under it is the panel's padding rather than more button: a click there is a
        // click on nothing, which is what says the two edges are the same distance from their content.
        var underPx = new Vector2(menu.Box.AtPx.X + menu.Box.SizePx.X * 0.5f, menu.Box.Bottom - Theme.PaddingPx * 0.5f);
        Assert.Equal(MenuAction.None, Click(menu, underPx, hasTown: false).Action);
    }

    /// <summary>
    /// <b>With a town loaded the footer carries the way back to it</b>, and that is the whole of what
    /// makes the menu safe to open mid-run: the town is not torn down to show it and it is left by
    /// closing it rather than by picking a map.
    /// </summary>
    [Fact]
    public void WithATownLoadedTheFooterClosesOntoIt()
    {
        var menu = new Menu();

        menu.Lay(Window, hasTown: true);
        Assert.Equal(MenuAction.Close, Click(menu, FooterMiddlePx(menu, 0.25f), hasTown: true).Action);
        Assert.Equal(MenuAction.Quit, Click(menu, FooterMiddlePx(menu, 0.75f), hasTown: true).Action);

        // With no map loaded there is nothing to close onto, and the way out takes the whole footer.
        menu.Lay(Window);
        Assert.Equal(MenuAction.Quit, Click(menu, FooterMiddlePx(menu, 0.25f), hasTown: false).Action);
    }

    /// <summary>
    /// The panel is one width and one place on the screen for all seven of its pages, so tabbing
    /// moves nothing: a menu whose tabs walk out from under the pointer is a menu that mis-clicks.
    /// </summary>
    [Fact]
    public void TabbingMovesNothingSideways()
    {
        var menu = new Menu();
        menu.Lay(Window, hasTown: true);
        var box = menu.Box;

        for (var page = 0; page <= Menu.Controls; page++)
        {
            menu.OpenAt(page);
            menu.Lay(Window, hasTown: true);
            Assert.Equal(box.AtPx.X, menu.Box.AtPx.X);
            Assert.Equal(box.SizePx.X, menu.Box.SizePx.X);
        }
    }

    /// <summary>
    /// A page with more rows than the window has room for is the checks page on a short display, and
    /// it was the panel drawn past the bottom edge of the screen with the last checks under it.
    /// </summary>
    [Fact]
    public void APageTallerThanTheWindowScrollsInsteadOfGrowingPastIt()
    {
        var shortWindow = new Vector2(1400f, 420f);
        var menu = new Menu();
        menu.OpenAt(Menu.Checks);
        menu.Lay(shortWindow);

        Assert.True(menu.Box.Bottom <= shortWindow.Y, $"the panel reaches {menu.Box.Bottom} of {shortWindow.Y}");
        Assert.True(CheckCatalogue.Shipped.Length > 3);

        var first = Click(menu, menu.RowMiddlePx(0), hasTown: false);
        Assert.Equal(MenuAction.RunCheck, first.Action);
        Assert.Equal(CheckCatalogue.Shipped[0].Name, first.Name);

        // Three notches down, and the row the layout puts under the pointer is the row the hit test
        // reads back — which is the same question the unscrolled page is asked above.
        menu.Scroll(-3f);
        var scrolled = Click(menu, menu.RowMiddlePx(3), hasTown: false);
        Assert.Equal(MenuAction.RunCheck, scrolled.Action);
        Assert.Equal(CheckCatalogue.Shipped[3].Name, scrolled.Name);
    }

    /// <summary>
    /// The three pages that are about a running town do nothing when there is none, rather than
    /// toggling a switch nothing reads or re-rolling the seed of a world that does not exist.
    /// </summary>
    [Fact]
    public void ThePagesThatNeedATownDoNothingWithoutOne()
    {
        var menu = new Menu();
        var switches = new DebugSwitches();

        menu.OpenAt(Menu.Layers);
        menu.Lay(Window);

        // Down the content column and stopping short of the footer, which is the menu's own and works
        // whether or not a town is loaded.
        var down = menu.Box.AtPx + new Vector2(menu.Box.SizePx.X * 0.75f, 0f);
        var lastPx = menu.Box.SizePx.Y - Theme.PaddingPx * 2f - Theme.RowPx;
        for (var at = Theme.PaddingPx; at < lastPx; at += 4f)
        {
            Assert.Equal(
                MenuAction.None,
                menu.Click(down + new Vector2(0f, at), hasTown: false, switches, new RunState()).Action);
        }

        Assert.False(switches.FrameReadout);
    }
}
