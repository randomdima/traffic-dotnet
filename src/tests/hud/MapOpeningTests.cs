using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// <b>OBS-2n — what is on screen between the row being clicked and the town standing.</b> It is the one
/// state of this interface nothing else can be asked about: the town behind it is the one being left,
/// every panel drawn over it is about a run that is ending, and a click that fell through would open a
/// second map.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class MapOpeningTests
{
    static readonly Vector2 Window = new(1600f, 1000f);

    /// <summary>An interface over a town, with a map picked on the menu and not standing yet.</summary>
    static Interface Opening()
    {
        var ui = new Interface(new TrimFigures());
        ui.TownChanged();
        ui.Opening.Opens("Odesa");
        return ui;
    }

    /// <summary>A camera the draw and the click can be handed, on a town this suite never looks at.</summary>
    static Camera2D Camera() => new(SimConfig.Shipped(), new Vector2(400f), Window);

    /// <summary>The frame the card is drawn against: no town, since nothing here reads one.</summary>
    static InterfaceFrame Frame(Vector2 uiPx) => new()
    {
        World = null,
        Config = SimConfig.Shipped(),
        Camera = Camera(),
        UiPx = uiPx,
        PointerPx = uiPx * 0.5f,
        MapName = "Odesa",
    };

    /// <summary>The card is up from the pick and gone the moment the town it named is standing.</summary>
    [Fact]
    public void TheCardStandsFromThePickUntilTheTownIsStanding()
    {
        var ui = Opening();
        Assert.True(ui.Opening.Showing);
        Assert.Equal("Odesa", ui.Opening.Map);

        ui.TownChanged();
        Assert.False(ui.Opening.Showing);
    }

    /// <summary>
    /// <b>Nothing under the card takes a click</b> — not the town, and not the gear, the screen button or
    /// the map list, none of which are drawn. A second map picked while the first is on the wire is a run
    /// opening two towns.
    /// </summary>
    [Fact]
    public void EveryClickIsTakenAndNoneOfThemAsksForAnything()
    {
        var ui = Opening();
        ui.Menu.Lay(Window, Chrome.GearAt(Window));
        ui.Menu.Show();

        Vector2[] corners =
        [
            new(40f, 600f),
            Chrome.GearAt(Window).AtPx + (Chrome.GearAt(Window).SizePx * 0.5f),
            Chrome.ScreenAt(Window, atTheStart: false).AtPx + new Vector2(2f),
            ui.Menu.RowMiddlePx(1),
        ];

        foreach (var atPx in corners)
        {
            var taken = ui.Click(atPx, Window, primary: true, hasTown: true, Camera(), out var choice);

            Assert.Equal(ClickTaken.Yes, taken);
            Assert.Equal(MenuAction.None, choice.Action);
        }

        Assert.False(ui.TakeScreenAsked());
    }

    /// <summary>
    /// <b>The card is the whole of what the interface draws</b>, and it stands in the middle of the window.
    /// A read-out, a legend and a map list over the town being left are furniture about a run that is over.
    /// </summary>
    [Fact]
    public void TheCardIsAllThatIsDrawnAndItStandsInTheMiddle()
    {
        var ui = Opening();
        ui.Status.Show();
        ui.Menu.Show();

        var into = new OverlayQuad[4096];
        var under = new OverlayQuad[256];
        var written = ui.Draw(into, under, Frame(Window), out var underWritten);

        Assert.Equal(0, underWritten);
        Assert.True(written > 0, "the card was the only thing to draw and nothing was drawn");

        var card = ui.Opening.Box;
        Assert.Equal(Window.X * 0.5f, card.AtPx.X + (card.SizePx.X * 0.5f), 1);
        Assert.Equal(Window.Y * 0.5f, card.AtPx.Y + (card.SizePx.Y * 0.5f), 1);

        // Everything written lies on the card, which is the whole of "no panel is drawn under it": the
        // shadow is the one thing outside the box, and it is the theme's own offset off two edges.
        for (var quad = 0; quad < written; quad++)
        {
            var lowest = into[quad].Centre - into[quad].HalfSize;
            var highest = into[quad].Centre + into[quad].HalfSize;

            Assert.True(
                lowest.X >= card.AtPx.X && lowest.Y >= card.AtPx.Y &&
                highest.X <= card.Right + Theme.PaddingPx && highest.Y <= card.Bottom + Theme.PaddingPx,
                $"a quad spanning {lowest} to {highest} was drawn off the card at {card.AtPx}, {card.SizePx}");
        }
    }

    /// <summary>
    /// <b>A window narrower than the card's own line is the card's width</b>, which is the rule every panel
    /// here answers to (OBS-2k): a card laid at its own width on a handset runs off the glass.
    /// </summary>
    [Fact]
    public void TheCardIsNeverWiderThanTheWindow()
    {
        var ui = Opening();
        var narrow = new Vector2(220f, 480f);

        var into = new OverlayQuad[1024];
        var under = new OverlayQuad[16];
        ui.Draw(into, under, Frame(narrow), out _);

        Assert.True(
            ui.Opening.Box.SizePx.X <= narrow.X - (Theme.MarginPx * 2f),
            $"the card came out {ui.Opening.Box.SizePx.X} px wide in a {narrow.X} px window");
    }
}
