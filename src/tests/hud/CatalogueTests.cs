using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.Bench;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// <b>OBS-2a — the map list the menu reads is the map list the command line reads</b>, guarded in both
/// directions: every entry names something that exists, and everything that exists is an entry.
/// </summary>
/// <remarks>
/// A map that opens one way and not the other is two lists pretending to be one, and a probe listed
/// under a name nothing dispatches is a probe nobody can run. Both fail here rather than in a menu
/// somebody is looking at.
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
    /// The menu cuts the maps into places and scenarios and shows nothing else, so the two groups
    /// between them have to hold every map — a map in neither is a map the menu cannot open.
    /// </summary>
    [Fact]
    public void EveryMapIsInExactlyOneMenuGroup()
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

        foreach (var line in ControlsCard.Strings) Printable(line);
    }

    /// <summary>
    /// <b>And every word a scenario's own panel draws</b> — the claims, the readings and the figures behind
    /// them, which are written by the watches rather than authored in the interface. They are printed to a
    /// console as well, where a superscript is free, so the one place it costs anything is the one place
    /// nothing checks it by eye.
    /// </summary>
    [Fact]
    public void EveryWordAScenarioPanelDrawsIsPrintableAscii()
    {
        var config = SimConfig.Shipped();
        foreach (var map in ProjectPaths.ShippedMaps())
        {
            using var world = new TownWorld(Towns.Of(map), config);
            foreach (var watch in Scenarios.For(world, config))
            {
                Printable(watch.Name);
                Printable(watch.Subject);

                for (var claim = 0; claim < watch.Claims; claim++)
                {
                    Printable(watch.Asks(claim));
                    Printable(Claims.Says(watch, claim));
                }

                for (var reading = 0; reading < watch.Readings; reading++)
                {
                    Printable(watch.Reading(reading));
                    Printable(Claims.Reads(watch, reading));
                }
            }
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
