using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>What the merge of bands owes its caller about the lines it is handed</b> (<see cref="BandShell"/>) —
/// as opposed to what it makes of them, which is <c>LaneShellTests</c>' over towns and
/// <c>BandOutsetTests</c>' and <c>ShellFillTests</c>' over the shapes it comes to.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class BandShellTests
{
    const float WidthM = 7f;

    const float ReachM = 60f;

    const float CellM = 8f;

    /// <summary>
    /// <b>A line with no pieces lays no band, and it does not shift the numbering of the lines that do.</b>
    /// The index the merge is given holds only the lines that reach a cell, so a set with empty lines in it
    /// is an index shorter than the set — and the merge asks that index for line numbers rather than for
    /// slots, which is what makes the two the same shell.
    /// </summary>
    /// <remarks>
    /// <b>It is the map the game opens on.</b> A town where nothing meets at a node lays a movement with no
    /// line at every arm of it (<c>IdlePlan</c>), so half the idle ring's driven lines are empty — and a
    /// merge that took the index's count for the set's could not lay the boundary of the one map every run
    /// starts over.
    /// </remarks>
    [Fact]
    public void ALineWithNoPiecesLaysNoBandAndLeavesTheNumberingAlone()
    {
        var drawn = Shell([Across(0f), Along(0f)]);
        var withEmpties = Shell([Across(0f), [], Along(0f), []]);

        Assert.Equal(0, drawn.Loose.Length);
        Assert.Equal(0, withEmpties.Loose.Length);
        Assert.Equal(drawn.Chains.Length, withEmpties.Chains.Length);
        Assert.Equal(BoundaryM(drawn), BoundaryM(withEmpties), 1e-3f);
    }

    static float BoundaryM(BandShell shell)
    {
        var lengthM = 0f;
        foreach (var ring in shell.Chains) lengthM += Spline.TotalLengthM(ring);

        return lengthM;
    }

    static ArcSeg[] Across(float atM) => [new ArcSeg(new Vector2(-ReachM, atM), 0f, 2f * ReachM, 0f)];

    static ArcSeg[] Along(float atM) =>
        [new ArcSeg(new Vector2(atM, -ReachM), MathF.PI * 0.5f, 2f * ReachM, 0f)];

    static BandShell Shell(ArcSeg[][] lines)
    {
        var widthM = new float[lines.Length];
        Array.Fill(widthM, WidthM);

        var building = new ChainIndex.Builder();
        for (var line = 0; line < lines.Length; line++)
        {
            building.Add(line, lines[line], Spline.TotalLengthM(lines[line]));
        }

        return BandShell.Of(lines, widthM, building.Seal(CellM), CellM);
    }
}
