using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>A shell cut into the triangles that cover it</b> (<see cref="ShellFill"/>), asked of the shapes whose
/// answer is written down — a square, a square with a square hole, two squares apart, a disc — and then of
/// the one shape nobody writes down, the boundary a merge of bands comes to.
/// </summary>
/// <remarks>
/// <para>
/// <b>What nearly every claim here is the area of the answer</b>, because a triangulation has exactly one
/// number that carries the whole of it at once. The triangles are summed <em>signed</em> and each is
/// separately asserted to turn the way the ring it came off does, so the sum is the ground covered and not
/// a ground covered twice cancelling a ground missed: a triangle laid over another counts twice, one laid
/// outside the shell counts as ground the shell has not got, and a stretch left unfilled is missing from
/// it. One figure refuses all three.
/// </para>
/// <para>
/// <b>And the count, where the count is the point.</b> A square is two triangles and nothing else, and a
/// square with a hole is eight — a fill that added a point of its own, fanned a ring from one corner or
/// cut a hole out by covering it and taking it back would be right about the area and wrong here.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class ShellFillTests
{
    const float SideM = 20f;

    const float ToleranceM2 = 1e-3f;

    /// <summary>
    /// <b>A square is two triangles</b>, and the fill adds no corner to the four it was given.
    /// </summary>
    [Fact]
    public void ASquareIsFilledWithTwoTriangles()
    {
        var (pointsM, triangles) = ShellFill.Of([Square(Vector2.Zero, SideM)]);

        Assert.Equal(4, pointsM.Length);
        Assert.Equal(2, triangles.Length / 3);
        Assert.Equal(SideM * SideM, FilledM2(pointsM, triangles), ToleranceM2);
    }

    /// <summary>
    /// <b>A hole is ground the shell has not got.</b> The fill comes to the square less the hole, no
    /// triangle of it stands over the hole at all, and the two corners the hole is bridged in by are the
    /// whole of what cutting it in costs — eight triangles over ten corners rather than the six the outline
    /// alone would give.
    /// </summary>
    [Fact]
    public void ASquareWithASquareHoleIsFilledRoundTheHole()
    {
        const float holeM = 6f;
        var holeAtM = new Vector2(7f, 7f);

        var (pointsM, triangles) = ShellFill.Of([Square(Vector2.Zero, SideM), Hole(holeAtM, holeM)]);

        Assert.Equal((SideM * SideM) - (holeM * holeM), FilledM2(pointsM, triangles), ToleranceM2);
        Assert.Equal(8, triangles.Length / 3);
        Assert.False(Covers(pointsM, triangles, holeAtM + new Vector2(holeM * 0.5f, holeM * 0.5f)));
    }

    /// <summary>
    /// <b>A shell that stands in pieces is every piece of it</b>, each filled as the shape it is and neither
    /// joined to the other by a triangle across the ground between them.
    /// </summary>
    [Fact]
    public void TwoSquaresApartAreEachFilled()
    {
        var farM = new Vector2(SideM * 5f, 0f);

        var (pointsM, triangles) =
            ShellFill.Of([Square(Vector2.Zero, SideM), Square(farM, SideM)]);

        Assert.Equal(2f * SideM * SideM, FilledM2(pointsM, triangles), ToleranceM2);
        Assert.Equal(4, triangles.Length / 3);
        Assert.True(Covers(pointsM, triangles, new Vector2(SideM * 0.5f, SideM * 0.5f)));
        Assert.True(Covers(pointsM, triangles, farM + new Vector2(SideM * 0.5f, SideM * 0.5f)));
    }

    /// <summary>
    /// <b>A bend is filled from the inside of it and never past it.</b> A chord falls inside the arc it
    /// stands for, so a disc comes back smaller than the circle and short of it by no more than the sag
    /// asked for taken the whole way round — which is the bound on how much ground a fill can be missing at
    /// any tolerance, and the one thing a caller choosing one needs.
    /// </summary>
    [Theory]
    [InlineData(ShellFill.SagM)]
    [InlineData(0.01f)]
    [InlineData(0.1f)]
    public void ADiscIsFilledToWithinTheSagItWasAskedFor(float sagM)
    {
        const float radiusM = 20f;

        var (pointsM, triangles) = ShellFill.Of([Disc(radiusM)], sagM);
        var filledM2 = FilledM2(pointsM, triangles);

        var circleM2 = MathF.PI * radiusM * radiusM;
        Assert.InRange(filledM2, circleM2 - (2f * MathF.PI * radiusM * sagM), circleM2);
    }

    /// <summary>
    /// <b>The boundary a merge of bands comes to, filled, covers the ground that boundary encloses</b> — the
    /// area read off the arcs themselves, which is the polygon through their ends plus the circle's own
    /// segment at every bend.
    /// </summary>
    /// <remarks>
    /// <b>The one shape here nobody writes down</b>, and the reason the shapes above are: a junction's
    /// boundary is hundreds of short pieces meeting at a degree or two, with the wedge between each pair of
    /// arms enclosed as a hole the bands do not cover. Two derivations of one number that have nothing in
    /// common — a sweep of ears over chords against a sum of sectors over arcs — agreeing to the sag says
    /// the cutting found every hole, kept every piece and laid nothing twice.
    /// </remarks>
    [Fact]
    public void TheFillOfAMergedJunctionComesToTheAreaItsArcsEnclose()
    {
        var shell = Junction();
        var (pointsM, triangles) = shell.Fill(ShellFill.SagM);

        var enclosedM2 = 0.0;
        var boundaryM = 0f;
        foreach (var ring in shell.Chains)
        {
            enclosedM2 += Spline.EnclosedM2(ring);
            boundaryM += Spline.TotalLengthM(ring);
        }

        Assert.Equal(0, shell.Loose.Length);
        Assert.Equal(enclosedM2, FilledM2(pointsM, triangles), boundaryM * ShellFill.SagM);
    }

    /// <summary>
    /// <b>A shell thinned still covers the ground it stands for</b>, to within the budget it was thinned by:
    /// what a thinning takes out is corners, and a corner taken out moves the boundary by no more than the
    /// budget however many of them go.
    /// </summary>
    /// <remarks>
    /// <b>Asked of a disc, because a disc is all bend.</b> A thinning has nothing to take off a straight and
    /// everything to take off a curve, so a ring with no straight in it is where the budget is spent in full
    /// — and the area it then encloses is the one shape whose answer nobody has to derive.
    /// </remarks>
    [Theory]
    [InlineData(0.02f)]
    [InlineData(0.08f)]
    public void AThinnedFillKeepsTheGroundToWithinTheBudgetItWasThinnedBy(float thriftM)
    {
        const float radiusM = 40f;

        var (pointsM, triangles) = ShellFill.Of([Disc(radiusM)], ShellFill.SagM, thriftM);
        var (fullM, full) = ShellFill.Of([Disc(radiusM)], ShellFill.SagM);

        Assert.True(
            pointsM.Length < fullM.Length,
            $"thinning at {thriftM:F2} m kept {pointsM.Length} of {fullM.Length} corners");

        // A chord cuts inside the arc it stands for, so a thinned ring falls short of the disc and never
        // over it — by at most the budget all the way round.
        var circleM2 = MathF.PI * radiusM * radiusM;
        Assert.InRange(
            FilledM2(pointsM, triangles), circleM2 - (2f * MathF.PI * radiusM * thriftM), circleM2);
        Assert.Equal(full.Length / 3, fullM.Length - 2);
    }

    /// <summary>
    /// The ground the triangles cover, <b>signed the way each of them turns</b>: ground covered twice counts
    /// twice and ground covered the wrong way round counts against, so the figure is the area only for a
    /// fill that laid every triangle once, inside, and the way its ring walks.
    /// </summary>
    static double FilledM2(Vector2[] pointsM, int[] triangles)
    {
        var twice = 0.0;
        for (var at = 0; at + 2 < triangles.Length; at += 3)
        {
            var a = pointsM[triangles[at]];
            var b = pointsM[triangles[at + 1]];
            var c = pointsM[triangles[at + 2]];

            var turn = (((double)b.X - a.X) * ((double)c.Y - a.Y))
                - (((double)b.Y - a.Y) * ((double)c.X - a.X));
            Assert.True(turn >= 0.0, $"a triangle at {a} turns the way no ring of a shell does");

            twice += turn;
        }

        return twice * 0.5;
    }

    /// <summary>Whether any triangle of the fill stands over a place.</summary>
    static bool Covers(Vector2[] pointsM, int[] triangles, Vector2 pointM)
    {
        for (var at = 0; at + 2 < triangles.Length; at += 3)
        {
            var a = pointsM[triangles[at]];
            var b = pointsM[triangles[at + 1]];
            var c = pointsM[triangles[at + 2]];

            if (Side(a, b, pointM) >= 0f && Side(b, c, pointM) >= 0f && Side(c, a, pointM) >= 0f) return true;
        }

        return false;
    }

    static float Side(Vector2 fromM, Vector2 toM, Vector2 pointM) =>
        ((toM.X - fromM.X) * (pointM.Y - fromM.Y)) - ((toM.Y - fromM.Y) * (pointM.X - fromM.X));

    /// <summary>A square walked with its own ground on the walker's right, which is what an outside is.</summary>
    static ArcSeg[] Square(Vector2 minM, float sideM) =>
        Closed(
            minM, minM + new Vector2(sideM, 0f), minM + new Vector2(sideM, sideM),
            minM + new Vector2(0f, sideM));

    /// <summary>The same square walked the other way, which is what a hole is.</summary>
    static ArcSeg[] Hole(Vector2 minM, float sideM) =>
        Closed(
            minM, minM + new Vector2(0f, sideM), minM + new Vector2(sideM, sideM),
            minM + new Vector2(sideM, 0f));

    /// <summary>A circle as the one piece it is, turning the way an outside turns.</summary>
    static ArcSeg[] Disc(float radiusM) =>
        [new ArcSeg(Vector2.Zero, 0f, 2f * MathF.PI * radiusM, 1f / radiusM)];

    static ArcSeg[] Closed(params Vector2[] cornersM)
    {
        var ring = new ArcSeg[cornersM.Length];
        for (var at = 0; at < cornersM.Length; at++)
        {
            var runM = cornersM[(at + 1) % cornersM.Length] - cornersM[at];
            ring[at] = new ArcSeg(cornersM[at], MathF.Atan2(runM.Y, runM.X), runM.Length(), 0f);
        }

        return ring;
    }

    /// <summary>
    /// Two streets crossing with the movements that turn between them, each laid as a band of its own width
    /// and merged into the boundary they come to — the shape <c>BandOutsetTests</c> moves and this one
    /// fills.
    /// </summary>
    static BandShell Junction()
    {
        const float widthM = 7f;
        const float reachM = 60f;
        const float cellM = 8f;
        float[] radii = [4.7f, 7.3f, 11.9f];

        List<ArcSeg[]> lines =
        [
            [new ArcSeg(new Vector2(-reachM, 0f), 0f, 2f * reachM, 0f)],
            [new ArcSeg(new Vector2(0f, -reachM), MathF.PI * 0.5f, 2f * reachM, 0f)],
        ];

        foreach (var radiusM in radii)
        {
            lines.Add([new ArcSeg(new Vector2(-radiusM, 0f), 0f, MathF.PI * 0.5f * radiusM, 1f / radiusM)]);
            lines.Add(
                [new ArcSeg(new Vector2(0f, radiusM), MathF.PI, MathF.PI * 0.5f * radiusM, 1f / radiusM)]);
        }

        var widths = new float[lines.Count];
        for (var line = 0; line < widths.Length; line++) widths[line] = widthM - (line * 0.37f);

        var building = new ChainIndex.Builder();
        for (var line = 0; line < lines.Count; line++)
        {
            building.Add(line, lines[line], Spline.TotalLengthM(lines[line]));
        }

        return BandShell.Of([.. lines], widths, building.Seal(new WorldGrid(cellM).Main));
    }
}
