using System.Numerics;
using Silk.NET.Maths;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// The pointer and the interface have to be in the same space, and on a scaled desktop neither of
/// them is the space the other arrives in.
/// </summary>
/// <remarks>
/// <b>This is a regression, and the symptom was that nothing on the menu could be clicked.</b> A 2×
/// display hands back an 1800 × 1400 framebuffer for a 900 × 700 window; an interface laid out in the
/// framebuffer's pixels is therefore clicked in the top-left quarter of itself — and one centred on
/// the screen can never be hit at all. The interface now lays itself out in interface pixels, which
/// is what <see cref="AppWindow.InUiPx"/> puts the pointer into. Nothing downstream can detect a
/// mistake here: every coordinate is a plausible one.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class PointerSpaceTests
{
    [Fact]
    public void OnAScaledDisplayThePointerIsAlreadyInInterfacePixels()
    {
        var window = new Vector2D<int>(900, 700);
        var framebuffer = new Vector2D<int>(1800, 1400);

        // The desktop's factor and the interface's are the same 2× here, so the two cancel and the
        // pointer arrives where it already was: the window's own pixels.
        Assert.Equal(new Vector2(450f, 350f), AppWindow.InUiPx(new Vector2(450f, 350f), window, framebuffer, 2f));
        Assert.Equal(Vector2.Zero, AppWindow.InUiPx(Vector2.Zero, window, framebuffer, 2f));
        Assert.Equal(new Vector2(900f, 700f), AppWindow.InUiPx(new Vector2(900f, 700f), window, framebuffer, 2f));
    }

    /// <summary>
    /// <c>--ui-scale</c> asks for a factor the desktop did not give, and then the two do not cancel:
    /// an interface laid out at 4× on a 2× desktop is half the pixels across, and a click has to land
    /// in that half-size layout rather than in the one the window is measured in.
    /// </summary>
    [Fact]
    public void AnAskedForScaleIsWhatThePointerIsPutInto()
    {
        var window = new Vector2D<int>(900, 700);
        var framebuffer = new Vector2D<int>(1800, 1400);

        Assert.Equal(new Vector2(225f, 175f), AppWindow.InUiPx(new Vector2(450f, 350f), window, framebuffer, 4f));
    }

    [Fact]
    public void AnUnscaledDisplayIsLeftAlone()
    {
        var size = new Vector2D<int>(1600, 900);

        Assert.Equal(new Vector2(123f, 456f), AppWindow.InUiPx(new Vector2(123f, 456f), size, size, 1f));
    }

    /// <summary>A window with no size yet — minimised, or between a resize and its first frame — divides by nothing.</summary>
    [Fact]
    public void AWindowWithNoSizeHandsThePointerBackUnchanged()
    {
        Assert.Equal(
            new Vector2(5f, 6f),
            AppWindow.InUiPx(new Vector2(5f, 6f), new Vector2D<int>(0, 0), new Vector2D<int>(800, 600), 1f));
    }

    /// <summary>
    /// And the half of it that has nothing to do with the display: <b>what the menu drew is what a
    /// click is tested against</b>. The rows are laid once and kept, so this asks the layout and the
    /// hit test the same question and expects the same answer.
    /// </summary>
    [Fact]
    public void ClickingTheMiddleOfARowPicksTheRowThatWasDrawnThere()
    {
        var uiPx = new Vector2(1800f, 1400f);
        var menu = new Menu();
        menu.Lay(uiPx, Chrome.GearAt(uiPx));

        // Row 0 is the group the places are under, and the row after it is the first place the
        // catalogue lists.
        var places = MapCatalogue.On(MapKind.Place);
        Assert.NotEmpty(places);

        var chosen = Click(menu, menu.RowMiddlePx(1));

        Assert.Equal(MenuAction.OpenMap, chosen.Action);
        Assert.Equal(places[0].Name, chosen.Name);

        // And a click nowhere near the panel picks nothing rather than the nearest thing.
        Assert.Equal(MenuAction.None, Click(menu, new Vector2(4f, 4f)).Action);
    }

    static MenuChoice Click(Menu menu, Vector2 atPx) => menu.Click(atPx, new DebugSwitches(), new TrimFigures());
}
