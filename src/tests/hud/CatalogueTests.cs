using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// <b>OBS-2a — the list the menu reads is the list the command line reads</b>, guarded in both
/// directions: every entry names something that exists, and everything that exists is an entry.
/// </summary>
/// <remarks>
/// A check nobody can launch is a check nobody runs, and a map that opens one way and not the other
/// is two lists pretending to be one. Both halves fail here rather than in a menu somebody is
/// looking at.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class CatalogueTests
{
    [Fact]
    public void EveryShippedMapIsDescribedAndEveryDescribedMapIsShipped()
    {
        var shipped = ProjectPaths.ShippedMaps();

        foreach (var map in shipped)
        {
            Assert.Contains(MapCatalogue.Catalogued.ToArray(), entry => entry.Name == map);
        }

        foreach (var entry in MapCatalogue.Catalogued.ToArray())
        {
            Assert.Contains(entry.Name, shipped);
            Assert.False(string.IsNullOrWhiteSpace(entry.Description), $"{entry.Name} has no description");
        }
    }

    /// <summary>
    /// The menu cuts the maps into places and scenarios and shows nothing else, so the two pages
    /// between them have to hold every map — a map on neither page is a map the menu cannot open.
    /// </summary>
    [Fact]
    public void EveryMapIsOnExactlyOneMenuPage()
    {
        var places = MapCatalogue.On(MapKind.Place);
        var scenarios = MapCatalogue.On(MapKind.Scenario);

        Assert.Equal(ProjectPaths.ShippedMaps().Length, places.Length + scenarios.Length);
        Assert.NotEmpty(places);
        Assert.NotEmpty(scenarios);
    }

    [Fact]
    public void EveryCheckIsFoundByTheNameItIsListedUnder()
    {
        Assert.NotEmpty(CheckCatalogue.Shipped);

        foreach (var check in CheckCatalogue.Shipped)
        {
            Assert.True(CheckCatalogue.TryFind(check.Name, out var found), $"{check.Name} is listed but cannot be found");
            Assert.Equal(check.Name, found.Name);
            Assert.NotNull(found.Run);
            Assert.False(string.IsNullOrWhiteSpace(check.Description), $"{check.Name} has no description");
        }

        Assert.False(CheckCatalogue.TryFind("no-such-check", out _));
    }

    /// <summary>
    /// The glyph sheet carries printable ASCII and nothing else, so a string with anything else in it
    /// is drawn with a hole in it — an em dash reads as a missing word rather than as a missing glyph,
    /// which is why this is checked rather than trusted.
    /// </summary>
    [Fact]
    public void EveryStringTheInterfaceDrawsIsPrintableAscii()
    {
        foreach (var entry in MapCatalogue.Catalogued.ToArray())
        {
            Printable(entry.Name);
            Printable(entry.Description);
        }

        foreach (var check in CheckCatalogue.Shipped)
        {
            Printable(check.Name);
            Printable(check.Description);
        }
    }

    static void Printable(string text)
    {
        foreach (var character in text)
        {
            Assert.True(
                character >= GlyphSheet.FirstChar && character < GlyphSheet.LastChar,
                $"'{character}' (U+{(int)character:X4}) in \"{text}\" is not on the glyph sheet");
        }
    }
}
