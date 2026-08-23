using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Screen;

/// <summary>
/// The third pipeline's own buffer: what a quad comes out as, and the two properties the whole
/// interface rests on — that a plain rectangle samples something opaque, and that a full buffer stops
/// rather than grows.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class ScreenDrawTests
{
    /// <summary>
    /// <b>The solid cell has to be opaque.</b> It is the cell every panel, bar, tape and debug line
    /// is drawn with, and getting its index wrong is a bug that draws the text on a panel and not the
    /// panel — which is exactly what happened once.
    /// </summary>
    [Fact]
    public void TheSolidCellIsInsideTheSheetAndPastEveryGlyph()
    {
        var lastGlyph = GlyphSheet.UvOf((char)(GlyphSheet.LastChar - 1));

        Assert.True(GlyphSheet.SolidUv.X is >= 0f and <= 1f);
        Assert.True(GlyphSheet.SolidUv.Y is >= 0f and <= 1f);
        Assert.True(GlyphSheet.SolidUv.Y > lastGlyph.Y, "the solid cell must be on a row after the printable range");

        // And the disc beside it, inset so the opaque cell next door cannot bleed into its rim.
        Assert.True(GlyphSheet.DiscUvMin.X > GlyphSheet.SolidUv.X);
        Assert.True(GlyphSheet.DiscUvSize.X < GlyphSheet.CellUv.X);
        Assert.True(GlyphSheet.DiscUvMin.X + GlyphSheet.DiscUvSize.X <= 1f);
    }

    [Fact]
    public void ARectangleIsOneScreenSpaceQuadCoveringWhatItWasAsked()
    {
        var quads = new OverlayQuad[8];
        var draw = new ScreenDraw(quads);

        draw.Rect(new Vector2(10f, 20f), new Vector2(100f, 40f), Vector4.One);

        Assert.Equal(1, draw.Written);
        Assert.Equal(new Vector2(60f, 40f), quads[0].Centre);
        Assert.Equal(new Vector2(50f, 20f), quads[0].HalfSize);
        Assert.Equal(1u, quads[0].Screen);
        Assert.Equal(GlyphSheet.SolidUv, quads[0].UvMin);
    }

    /// <summary>A line in metres is a rotated quad, so a debug mark keeps its width whatever the camera is doing.</summary>
    [Fact]
    public void ALineInMetresIsARotatedQuadInWorldSpace()
    {
        var quads = new OverlayQuad[8];
        var draw = new ScreenDraw(quads);

        draw.LineM(new Vector2(0f, 0f), new Vector2(10f, 0f), 0.5f, Vector4.One);

        Assert.Equal(1, draw.Written);
        Assert.Equal(0u, quads[0].Screen);
        Assert.Equal(new Vector2(5f, 0f), quads[0].Centre);
        Assert.Equal(5f, quads[0].HalfSize.X, 4);
        Assert.Equal(0.25f, quads[0].HalfSize.Y, 4);
        Assert.Equal(0f, quads[0].Rotation, 4);
    }

    /// <summary>
    /// <b>Two pieces of a band round a bend share their cut.</b> A band is drawn as a run of pieces, and
    /// what makes the run one shape rather than a row of blocks is that the end of each is cut on the same
    /// line the next one begins on — the line's own cross-section, and not the chord's.
    /// </summary>
    /// <remarks>
    /// Butted as plain rectangles the two pivot about the centreline instead, opening a notch on the
    /// outside of the joint and doubling the blend on the inside. Both are half the band's width times the
    /// turn across a piece, so it is a wide band round a tight bend — a car's reservation through a
    /// junction join — that shows it worst.
    /// </remarks>
    [Fact]
    public void TwoPiecesOfABandRoundABendShareTheirCut()
    {
        const float radiusM = 8f;
        const float widthM = 3f;
        var arc = new ArcSeg(Vector2.Zero, 0f, radiusM * MathF.PI * 0.5f, 1f / radiusM);
        var stepM = 2f;

        var quads = new OverlayQuad[4];
        var draw = new ScreenDraw(quads);
        draw.BandM(arc.PointAtM(0f), arc.PointAtM(stepM), arc.Curvature * stepM, widthM, Vector4.One);
        draw.BandM(arc.PointAtM(stepM), arc.PointAtM(stepM * 2f), arc.Curvature * stepM, widthM, Vector4.One);

        Assert.Equal(2, draw.Written);
        Same(Corner(quads[0], 1, 1), Corner(quads[1], -1, 1));
        Same(Corner(quads[0], 1, -1), Corner(quads[1], -1, -1));

        // And the cut they share is the band's own cross-section: half a width either side of the line,
        // square to it.
        var on = arc.PointAtM(stepM);
        var across = new Vector2(-MathF.Sin(arc.HeadingAtRad(stepM)), MathF.Cos(arc.HeadingAtRad(stepM)));
        Same(on + across * (widthM * 0.5f), Corner(quads[1], -1, 1));
        Same(on - across * (widthM * 0.5f), Corner(quads[1], -1, -1));
    }

    static void Same(Vector2 expected, Vector2 actual) =>
        Assert.True((expected - actual).Length() <= ToleranceM, $"{actual} is not {expected}");

    /// <summary>A millimetre on the ground, which is a hundredth of a pixel at the closest framing the camera has.</summary>
    const float ToleranceM = 1e-3f;

    /// <summary>One corner of a quad, worked out the way the vertex shader works it out.</summary>
    static Vector2 Corner(in OverlayQuad quad, float alongSide, float acrossSide)
    {
        var along = new Vector2(MathF.Cos(quad.Rotation), MathF.Sin(quad.Rotation));
        var across = new Vector2(-along.Y, along.X);

        return quad.Centre
               + along * (alongSide * (quad.HalfSize.X - acrossSide * quad.Taper))
               + across * (acrossSide * quad.HalfSize.Y);
    }

    /// <summary>A space is a cell of nothing, and a quad of nothing is a quad the buffer did not need.</summary>
    [Fact]
    public void TextEmitsOneQuadPerVisibleCharacter()
    {
        var quads = new OverlayQuad[32];
        var draw = new ScreenDraw(quads);

        var widthPx = draw.Text(Vector2.Zero, "ab cd", 24f, Vector4.One);

        Assert.Equal(4, draw.Written);
        Assert.Equal(GlyphSheet.WidthPx(5, 24f), widthPx, 3);
    }

    /// <summary>
    /// A full buffer drops the overflow rather than growing: the buffer is laid once for the busiest
    /// frame, and a frame at sixty hertz has nothing useful to do with a resize.
    /// </summary>
    [Fact]
    public void AFullBufferStopsRatherThanGrowing()
    {
        var quads = new OverlayQuad[3];
        var draw = new ScreenDraw(quads);

        for (var quad = 0; quad < 10; quad++) draw.Rect(Vector2.Zero, Vector2.One, Vector4.One);

        Assert.Equal(3, draw.Written);
        Assert.True(draw.Full);
    }

    /// <summary>
    /// Rule 2 is about the frame as well as the tick: a read-out that printed a figure sixty times a
    /// second through <c>ToString</c> would be the largest allocator in the build.
    /// </summary>
    [Fact]
    public void WritingAReadOutAllocatesNothing()
    {
        var quads = new OverlayQuad[4096];

        // Once through first, so the JIT has compiled everything the measured pass runs.
        Fill(quads);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 100; pass++) Fill(quads);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    static void Fill(OverlayQuad[] quads)
    {
        var draw = new ScreenDraw(quads);
        Span<char> text = stackalloc char[64];
        var line = new TextBuffer(text);
        line.Add("frame");
        line.PadTo(14);
        line.Add(12.3456d, "F3");
        line.Add(" ms");

        draw.Rect(new Vector2(4f), new Vector2(200f, 40f), Vector4.One);
        draw.Text(new Vector2(8f), line.Written, 15f, Vector4.One);
        draw.RingM(Vector2.Zero, 2f, 0.1f, Vector4.One);
    }
}
