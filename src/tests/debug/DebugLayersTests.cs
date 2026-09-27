using TrafficSimulation.App.Debug;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// The catalogue the menu, <c>--ui</c> and the switches all read (OBS-2c, OBS-2y): that it names every switch
/// once, and that each name throws the switch it names.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P8)]
public class DebugLayersTests
{
    static readonly DebugLayer[] Layers = Enum.GetValues<DebugLayer>();

    /// <summary>
    /// <b>Every switch is in exactly one section</b> (OBS-2y): in two it is one switch with two rows, and in
    /// none it is a switch nobody can reach from the menu.
    /// </summary>
    [Fact]
    public void EveryLayerIsInExactlyOneSection()
    {
        foreach (var layer in Layers)
        {
            var holding = 0;
            foreach (var section in DebugLayers.Sections) holding += Array.IndexOf(section.Layers, layer) >= 0 ? 1 : 0;

            Assert.True(holding == 1, $"{layer} is in {holding} sections");
        }
    }

    /// <summary>The catalogue is read by a layer's own number, so its rows stand in the enum's order and there is one a layer.</summary>
    [Fact]
    public void TheCatalogueStandsInTheOrderOfTheLayersItNames()
    {
        Assert.Equal(Layers.Length, DebugLayers.All.Length);
        foreach (var layer in Layers) Assert.Equal(layer, DebugLayers.Of(layer).Layer);
    }

    /// <summary>
    /// <b>Each layer is its own switch</b> (OBS-2c): thrown by its own word, it turns on that switch and no
    /// other — the one thing a table of refs into a class of fields can get wrong without anything failing.
    /// </summary>
    [Fact]
    public void EachWordThrowsTheOneSwitchItNames()
    {
        foreach (var entry in DebugLayers.All)
        {
            var switches = new DebugSwitches();
            switches.Toggle(DebugLayers.Worded(entry.Word)!.Value);

            foreach (var other in Layers) Assert.Equal(other == entry.Layer, switches[other]);
        }
    }

    /// <summary>A section is opened by its own word and by no other section's.</summary>
    [Fact]
    public void EachSectionWordOpensThatSection()
    {
        for (var section = 0; section < DebugLayers.Sections.Length; section++)
        {
            Assert.Equal(section, DebugLayers.SectionWorded(DebugLayers.Sections[section].Word));
        }
    }
}
