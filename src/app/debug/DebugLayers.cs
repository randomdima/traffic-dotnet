using System.Numerics;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// One switch of <see cref="DebugSwitches"/> by name — the one number a menu row, a <c>--ui</c> word and a
/// section of the menu all say it by (<see cref="DebugLayers"/>).
/// </summary>
internal enum DebugLayer : byte
{
    CarLines,
    TurnCircles,
    WalkerLines,
    Nodes,
    Claims,
    Collision,
    SolverGrid,
    Perimeter,
    Ribbons,
    Grid,
    Shell,
    Wireframe,
    Ruler,
}

/// <summary>One colour a layer draws in and the word for what it means, as the menu's key beside that layer's row.</summary>
internal readonly record struct LegendKey(string Word, Vector4 Colour);

/// <summary>
/// What one switch is called, the <c>--ui</c> word that throws it, what it draws in a line, and what its
/// colours mean.
/// </summary>
/// <remarks>
/// <b>Printable ASCII only</b>: the glyph sheet carries that range and nothing else, so a dash outside it is
/// drawn as a space and reads as a missing word.
/// </remarks>
internal readonly record struct LayerEntry(DebugLayer Layer, string Name, string Word, string Says, LegendKey[] Key);

/// <summary>
/// Which of the menu's own figures a section carries under its switches: the road's trims, the shell probe's
/// two figures, or the ground's own layers.
/// </summary>
internal enum SectionFigures : byte
{
    None,
    Trims,
    Shell,
    Ground,
}

/// <summary>
/// <b>OBS-2y — one section a thing a debug session is opened to look at</b>: its switches, and the figures that
/// session turns while it looks.
/// </summary>
/// <param name="Word">What <c>--ui menu-&lt;word&gt;</c> opens it by.</param>
internal readonly record struct DebugSection(string Name, string Word, DebugLayer[] Layers, SectionFigures Figures);

/// <summary>
/// <b>Every switch this slice owns, and the sections the menu shows them in</b> (OBS-2c, OBS-2y). One list, read
/// by the menu, by <c>--ui</c> and by the suite, so a row drawn, a word typed and a layer thrown cannot name three
/// different things.
/// </summary>
internal static class DebugLayers
{
    /// <summary>Indexed by <see cref="DebugLayer"/>, and the suite holds the two in step.</summary>
    public static readonly LayerEntry[] All =
    [
        new(DebugLayer.CarLines, "Car lines", "car-lines",
            "The road each car is on and the piece after it, its pass and circles, and where it must stop",
            [new("route", Theme.AgentLine(0)), new("ahead / stop", Theme.HeldLine)]),
        new(DebugLayer.TurnCircles, "Turn circles", "turn-circles",
            "The circle each car's front wheels say it is turning, worked from its axles",
            [new("centre and arc", Theme.TurnCircle)]),
        new(DebugLayer.WalkerLines, "Walker lines", "walker-lines",
            "The way each walker is on and the one after it, to where it hands over, and any pass",
            [new("route", Theme.AgentLine(2))]),
        new(DebugLayer.Nodes, "Nodes and links", "nodes",
            "Every lane, movement and pavement the router plans over",
            [new("lane", Theme.DrivingNodes), new("reverse", Theme.DrivingReverse), new("walk", Theme.WalkingNodes),
             new("kerb end", Theme.KerbEnd)]),
        new(DebugLayer.Claims, "Lane claims", "claims",
            "Ground held this tick: colour is whose, shade is how strong, outline is only crossed",
            [new("held", Theme.AgentLine(3)), new("furniture", Theme.LaneObstruction)]),
        new(DebugLayer.Collision, "Collision shapes", "collision",
            "The shape the solver holds for every body, not the one it is drawn at",
            [new("shape", Theme.Collision)]),
        new(DebugLayer.SolverGrid, "Solver grid", "solver-grid",
            "The broad phase's cells, shaded by how many bodies each holds",
            [new("static", Theme.SolverStaticEdge), new("moving", Theme.SolverMovingEdge)]),
        new(DebugLayer.Perimeter, "Tarmac perimeter", "perimeter",
            "The outside of all the driven ground, and the layers struck off it",
            [new("boundary", Theme.Perimeter), new("layer", Theme.PerimeterOutset), new("open run", Theme.PerimeterLoose)]),
        new(DebugLayer.Ribbons, "Tarmac ribbons", "ribbons",
            "The ground each lane, movement and bay way covers",
            [new("ribbon", Theme.Perimeter with { W = 0.5f })]),
        new(DebugLayer.Grid, "Geometry grid", "grid",
            "The cells a question about the lines is narrowed with, shaded by how many",
            [new("cell", Theme.GeometryGridCell)]),
        new(DebugLayer.Shell, ShellProbe.Named, "shell",
            "A shape struck off the boundary at the two figures below",
            [new("probe", Theme.ShellProbe)]),
        new(DebugLayer.Wireframe, "Ground wireframe", "wireframe",
            "The triangles the ground is drawn out of",
            [new("edge", Theme.Wireframe with { W = 0.9f })]),
        new(DebugLayer.Ruler, "Ruler", "ruler",
            "A click lays a tape between two places, a right click drops them",
            [new("tape", Theme.RulerTape)]),
    ];

    /// <summary>
    /// The sections, in the order the menu lists them. <b>Every switch is in exactly one</b>, which the suite
    /// holds: a layer in two sections is one switch with two rows, and a layer in none is a switch nobody can
    /// reach.
    /// </summary>
    public static readonly DebugSection[] Sections =
    [
        new("Cars", "cars", [DebugLayer.CarLines, DebugLayer.TurnCircles], SectionFigures.Trims),
        new("Walkers", "walkers", [DebugLayer.WalkerLines], SectionFigures.None),
        new("Junctions", "junctions", [DebugLayer.Nodes, DebugLayer.Claims], SectionFigures.None),
        new("Physics", "physics", [DebugLayer.Collision, DebugLayer.SolverGrid], SectionFigures.None),
        new("Road shape", "road",
            [DebugLayer.Perimeter, DebugLayer.Ribbons, DebugLayer.Grid, DebugLayer.Shell], SectionFigures.Shell),
        new("Ground mesh", "ground", [DebugLayer.Wireframe], SectionFigures.Ground),
        new("Tools", "tools", [DebugLayer.Ruler], SectionFigures.None),
    ];

    /// <summary>The most switches any one section holds, which is what the menu keeps room for.</summary>
    public static readonly int MostInASection = Most();

    public static LayerEntry Of(DebugLayer layer) => All[(int)layer];

    /// <summary>The layer a <c>--ui</c> word throws, if it names one.</summary>
    public static DebugLayer? Worded(string word)
    {
        foreach (var entry in All)
        {
            if (string.Equals(entry.Word, word, StringComparison.Ordinal)) return entry.Layer;
        }

        return null;
    }

    /// <summary>The section a <c>--ui menu-&lt;word&gt;</c> opens, or −1.</summary>
    public static int SectionWorded(string word)
    {
        for (var section = 0; section < Sections.Length; section++)
        {
            if (string.Equals(Sections[section].Word, word, StringComparison.Ordinal)) return section;
        }

        return -1;
    }

    /// <summary>How many of one section's switches are on, which is what its row in the menu says without being opened.</summary>
    public static int CountOn(DebugSwitches switches, int section)
    {
        var on = 0;
        foreach (var layer in Sections[section].Layers)
        {
            if (switches[layer]) on++;
        }

        return on;
    }

    static int Most()
    {
        var most = 0;
        foreach (var section in Sections) most = Math.Max(most, section.Layers.Length);

        return most;
    }
}
