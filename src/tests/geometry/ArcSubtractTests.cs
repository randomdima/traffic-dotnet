using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>One closed shape with another taken out of it</b> (<see cref="ArcSubtract"/>), asked of the five
/// shapes a cut can leave: a shape with a hole punched clean through it, a shape with a bite out of one
/// edge, a shape the cut severs in two, a shape swallowed whole, and a shape the cut never reaches.
/// </summary>
/// <remarks>
/// <b>The length of the answer is what most of these check</b>, because it is the one figure that carries
/// every part of the construction at once: a crossing missed, a stretch kept on the wrong side, a piece cut
/// at the wrong distance or a boundary carried across twice all come out as a different number of metres.
/// <b>Which way the hole is walked is checked as an area instead</b>, because that is the only thing a
/// winding can be read off — a hole walked the same way round as its shape fills solid.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class ArcSubtractTests
{
    const float ToleranceM = 1e-3f;

    /// <summary>How long a side of the shape being cut is, and of the shape cutting it. Metres.</summary>
    const float SideM = 40f;

    const float CutSideM = 10f;

    /// <summary>
    /// The cell both shapes' pieces are binned at: the size of the features being asked about, which here is
    /// the shape doing the cutting.
    /// </summary>
    const float CellM = CutSideM;

    /// <summary>
    /// <b>A shape with a smaller one taken out of its middle comes back as the two rings it is</b>: its own
    /// boundary untouched, because nothing crosses it, and the cut's boundary carried whole as the hole it
    /// now bounds.
    /// </summary>
    [Fact]
    public void ASquareLessASquareInsideItComesBackAsTwoRings()
    {
        var (rings, loose) = ArcSubtract.Of([Square(SideM)], [Square(CutSideM, new Vector2(15f, 15f))], CellM);

        Assert.Empty(loose);
        Assert.Equal(2, rings.Length);
        Assert.Equal((4f * SideM) + (4f * CutSideM), TotalLengthM(rings), ToleranceM);
    }

    /// <summary>
    /// <b>And the hole is walked the other way round</b>, which is what makes the pair one shape with a hole
    /// rather than two shapes drawn over one another. It is read as the area the rings fill
    /// (<see cref="ShellFill"/>), that being the one answer a winding changes: walked the same way round as
    /// the shape it stands in, the hole fills solid and the area comes back the whole square's.
    /// </summary>
    [Fact]
    public void TheHoleIsWalkedTheOtherWayRoundFromTheShapeItStandsIn()
    {
        var (rings, _) = ArcSubtract.Of([Square(SideM)], [Square(CutSideM, new Vector2(15f, 15f))], CellM);

        Assert.Equal(
            (SideM * SideM) - (CutSideM * CutSideM), FilledM2(rings), SideM * SideM * FillTolerance);
    }

    /// <summary>How much of the area a fill may be short, the boundary being read as chords: a part in ten thousand.</summary>
    const float FillTolerance = 1e-4f;

    /// <summary>
    /// <b>A cut reaching in over one edge takes that edge's length out and puts its own three sides in.</b>
    /// The cut stands half in and half out, so what the answer is made of is the part of each boundary the
    /// other does not cover — and neither shape contributes a piece nothing bounds.
    /// </summary>
    [Fact]
    public void ACutOverOneEdgeTradesThatEdgeForItsOwnThreeSides()
    {
        var overM = CutSideM * 0.5f;
        var (rings, loose) = ArcSubtract.Of(
            [Square(SideM)], [Square(CutSideM, new Vector2(15f, -overM))], CellM);

        // What the shape gives up is the stretch of its edge the cut covers; what it takes on is the cut's
        // far side and the two walls reaching in to it. The cut's own outermost side is on neither.
        var takenM = CutSideM;
        var givenM = CutSideM + (2f * overM);

        Assert.Empty(loose);
        Assert.Equal(
            (4f * SideM) - takenM + givenM, Spline.TotalLengthM(Assert.Single(rings)), ToleranceM);
    }

    /// <summary>
    /// <b>A cut running clean across the shape severs it, and both halves come back.</b> Nothing here maps a
    /// ring to a ring: one ring went in and two come out, which is a fact about the ground and not about the
    /// lines.
    /// </summary>
    [Fact]
    public void ACutRunningRightAcrossTheShapeLeavesTwoRings()
    {
        var overM = CutSideM * 0.5f;
        var acrossM = SideM + CutSideM;
        var (rings, loose) = ArcSubtract.Of(
            [Square(SideM)], [Rectangle(new Vector2(15f, -overM), new Vector2(CutSideM, acrossM))], CellM);

        Assert.Empty(loose);
        Assert.Equal(2, rings.Length);
        Assert.Equal(2f * (2f * (15f + SideM)), TotalLengthM(rings), ToleranceM);
    }

    /// <summary>
    /// <b>A shape standing wholly inside what is taken from it comes back as nothing at all</b> — not as a
    /// ring of no area, and not as a run the walk could not close.
    /// </summary>
    [Fact]
    public void AShapeSwallowedByTheCutComesBackAsNothing()
    {
        var (rings, loose) = ArcSubtract.Of(
            [Square(CutSideM, new Vector2(15f, 15f))], [Square(SideM)], CellM);

        Assert.Empty(rings);
        Assert.Empty(loose);
    }

    /// <summary>
    /// <b>And a cut that never reaches the shape takes nothing off it</b>, the shape coming back as the one
    /// ring it went in as rather than as the pair of them drawn together.
    /// </summary>
    [Fact]
    public void ACutStandingClearOfTheShapeTakesNothingOffIt()
    {
        var farM = SideM * 3f;
        var (rings, loose) = ArcSubtract.Of(
            [Square(SideM)], [Square(CutSideM, new Vector2(farM, farM))], CellM);

        Assert.Empty(loose);
        Assert.Equal(4f * SideM, Spline.TotalLengthM(Assert.Single(rings)), ToleranceM);
    }

    /// <summary>
    /// <b>A cut standing off a corner the shape turns in on its own ground is weighed against the corner
    /// and not against one of the two pieces that meet there.</b> The cut stands in the wedge behind a
    /// sliver of hole, so every one of its stretches is nearest the sliver's apex and nothing else — and
    /// the apex is ground on one of the pieces' reading and not on the other's.
    /// </summary>
    /// <remarks>
    /// <b>It is the one place a shape's own boundary cannot answer for it piece by piece.</b> Read off
    /// whichever of the two the search happened to name, the cut is kept or dropped on a coin toss; read off
    /// both together it is the corner's own bisector, which is where the ground actually is. <b>Said as a
    /// length</b>, because a stretch dropped for standing on the wrong side is a stretch missing from the
    /// answer and nothing else changes.
    /// </remarks>
    [Fact]
    public void ACutBehindASliversApexIsWeighedAgainstTheApexAndNotOneSideOfIt()
    {
        var apexM = new Vector2(20f, 20f);
        var sliver = Ring(apexM, new Vector2(24f, 22f), new Vector2(24f, 18f));
        var cutM = 1f;

        var (rings, loose) = ArcSubtract.Of(
            [Square(SideM), sliver], [Square(cutM, new Vector2(18.5f, 19.5f))], cutM);

        Assert.Empty(loose);
        Assert.Equal(3, rings.Length);
        Assert.Equal(
            (4f * SideM) + Spline.TotalLengthM(sliver) + (4f * cutM), TotalLengthM(rings), ToleranceM);
    }

    static float TotalLengthM(ArcSeg[][] rings)
    {
        var lengthM = 0f;
        foreach (var ring in rings) lengthM += Spline.TotalLengthM(ring);

        return lengthM;
    }

    /// <summary>How much ground a set of rings covers, as the triangles that cover it.</summary>
    static float FilledM2(ArcSeg[][] rings)
    {
        var (pointsM, triangles) = ShellFill.Of(rings);
        var areaM2 = 0f;
        for (var at = 0; at < triangles.Length; at += 3)
        {
            var one = pointsM[triangles[at]];
            var two = pointsM[triangles[at + 1]];
            var three = pointsM[triangles[at + 2]];
            areaM2 += MathF.Abs(Cross(two - one, three - one)) * 0.5f;
        }

        return areaM2;
    }

    static float Cross(Vector2 one, Vector2 other) => (one.X * other.Y) - (one.Y * other.X);

    /// <summary>
    /// A square walked with its ground on the walker's right, which is the winding every ring here keeps
    /// (<see cref="BandShell.Chains"/>).
    /// </summary>
    static ArcSeg[] Square(float sideM) => Square(sideM, Vector2.Zero);

    /// <summary>
    /// A closed ring of straights through the corners given, in the order given — so a hole is the same
    /// corners the other way round.
    /// </summary>
    static ArcSeg[] Ring(params Vector2[] cornersM)
    {
        var ring = new ArcSeg[cornersM.Length];
        for (var at = 0; at < cornersM.Length; at++)
        {
            var fromM = cornersM[at];
            var runM = cornersM[(at + 1) % cornersM.Length] - fromM;
            ring[at] = new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), runM.Length(), 0f);
        }

        return ring;
    }

    static ArcSeg[] Square(float sideM, Vector2 atM) => Rectangle(atM, new Vector2(sideM, sideM));

    static ArcSeg[] Rectangle(Vector2 atM, Vector2 sizeM) =>
    [
        new ArcSeg(atM, 0f, sizeM.X, 0f),
        new ArcSeg(atM + new Vector2(sizeM.X, 0f), MathF.PI * 0.5f, sizeM.Y, 0f),
        new ArcSeg(atM + sizeM, MathF.PI, sizeM.X, 0f),
        new ArcSeg(atM + new Vector2(0f, sizeM.Y), MathF.PI * -0.5f, sizeM.Y, 0f),
    ];
}
