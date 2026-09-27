using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>The boundary of a merged set of bands, moved off itself</b> (<see cref="ArcOutset"/>) — which is the
/// one shape <c>ArcOutsetTests</c> cannot ask about, because it is not a shape anybody writes down.
/// </summary>
/// <remarks>
/// <para>
/// <b>What a merge hands over is a boundary written in hundreds of short pieces meeting at a degree or
/// two</b> (<see cref="BandShell.Chains"/>), and that is the whole of what this adds: a square's corners
/// turn through a right angle and its sides are forty metres, so every construction in the offset is
/// exercised at a scale where nothing is marginal. A junction's are a third of a metre and three degrees,
/// where the moved line folds through its own neighbour by millimetres — <b>and a millimetre of fold
/// carries a third of a metre of boundary with it</b> (<see cref="ArcOutset"/>'s own graze figure).
/// </para>
/// <para>
/// <b>One crossing and not a town</b>, because the feature is the turn: a street crossing a street with
/// movements arcing between them puts a short bend between two long straights, which is the corner every
/// one of a town's sixteen open runs stood at. A town has thousands of them and costs a minute to lay;
/// what a shipped city's boundary comes to is <c>--bench outset</c>'s to say and not the suite's.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class BandOutsetTests
{
    const float WidthM = 7f;

    /// <summary>The distance the boundary is moved, an example and a wide one: the move with corners to fail at.</summary>
    const float MovedM = 5f;

    /// <summary>A cell about a band's own width, so the index is neither one bucket nor a bucket a piece.</summary>
    const float CellM = 8f;

    /// <summary>The radii a junction's turns are cut on, which is what puts a short bend between two straights.</summary>
    static readonly float[] Radii = [4.7f, 7.3f, 11.9f];

    /// <summary>How far each street runs either side of the crossing.</summary>
    const float ReachM = 60f;

    /// <summary>
    /// <b>The boundary of a junction moved off itself is closed</b>, whichever way and however far it is
    /// moved: every run the walk hands back is a ring and none is left with two ends.
    /// </summary>
    /// <remarks>
    /// <b>The merge's own answer is asserted first</b>, because a shape that did not close before it was
    /// moved says nothing about the move. Inward only as far as this shape can be moved inward: a junction's
    /// narrowest band is four metres across, so a deeper inset is asking what half of nothing comes to —
    /// and what an inset does to a corner is <c>ArcOutsetTests</c>' to answer, on a square whose answer is
    /// written down.
    /// </remarks>
    [Theory]
    [InlineData(MovedM)]
    [InlineData(MovedM * 0.5f)]
    [InlineData(-MovedM * 0.2f)]
    public void TheOutsetOfAMergedJunctionCloses(float movedM)
    {
        var shell = Junction();
        var (rings, loose) = shell.Outset(movedM, roundedM: 0f);

        Assert.Equal(0, shell.Loose.Length);
        Assert.NotEmpty(rings);
        Assert.Equal(0, loose.Length);
    }

    /// <summary>
    /// Two streets crossing, a third running past on the skew, and the movements that turn between them —
    /// each laid as a band of its own width and merged into the boundary they come to.
    /// </summary>
    static BandShell Junction()
    {
        List<ArcSeg[]> lines =
        [
            [new ArcSeg(new Vector2(-ReachM, 0f), 0f, 2f * ReachM, 0f)],
            [new ArcSeg(new Vector2(0f, -ReachM), MathF.PI * 0.5f, 2f * ReachM, 0f)],
            [new ArcSeg(new Vector2(-ReachM, 22f), 0.1f, 2f * ReachM, -1f / 900f)],
        ];

        // The turns, at the radii a junction is actually cut on: each lays a short bend between two long
        // straights, which is the corner every one of a town's open runs stood at.
        foreach (var radiusM in Radii)
        {
            lines.Add([new ArcSeg(new Vector2(-radiusM, 0f), 0f, MathF.PI * 0.5f * radiusM, 1f / radiusM)]);
            lines.Add(
                [new ArcSeg(new Vector2(0f, radiusM), MathF.PI, MathF.PI * 0.5f * radiusM, 1f / radiusM)]);
        }

        var widthM = new float[lines.Count];
        for (var line = 0; line < widthM.Length; line++) widthM[line] = WidthM - (line * 0.37f);

        var building = new ChainIndex.Builder();
        for (var line = 0; line < lines.Count; line++)
        {
            building.Add(line, lines[line], Spline.TotalLengthM(lines[line]));
        }

        return BandShell.Of([.. lines], widthM, building.Seal(new WorldGrid(CellM).Main));
    }
}
