using System.Numerics;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// OBS-2e and OBS-2f: the legend's length is held while its marks answer the zoom, and both
/// instruments are graduated on one ladder of round numbers.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class ScaleLegendTests
{
    [Theory]
    [InlineData(0.7f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(3.4f, 2f)]
    [InlineData(7f, 5f)]
    [InlineData(12f, 10f)]
    [InlineData(430f, 200f)]
    public void TheLadderIsOneTwoOrFiveTimesAPowerOfTen(float roughM, float expectedM) =>
        Assert.Equal(expectedM, Ladder.StepM(roughM), 4);

    [Fact]
    public void TheLadderSurvivesNonsense()
    {
        Assert.Equal(1f, Ladder.StepM(0f));
        Assert.Equal(1f, Ladder.StepM(-5f));
        Assert.Equal(1f, Ladder.StepM(float.NaN));
    }

    /// <summary>Every figure carries the unit that suits it: centimetres, metres or kilometres.</summary>
    [Theory]
    [InlineData(0.42f, "42 cm")]
    [InlineData(7.5f, "7.5 m")]
    [InlineData(70f, "70 m")]
    [InlineData(2400f, "2.40 km")]
    public void ADistanceIsWrittenWithTheUnitThatSuitsIt(float metres, string expected)
    {
        Span<char> text = stackalloc char[24];
        var written = new TextBuffer(text);

        Ladder.WriteDistance(ref written, metres);

        Assert.Equal(expected, written.Written.ToString());
    }

    /// <summary>
    /// <b>The bar's length is held and it is the number of marks that changes.</b> Two framings an
    /// order of magnitude apart come out about the same length on screen and stand for very different
    /// distances — which is the pair of reference frames, asserted.
    /// </summary>
    [Fact]
    public void TheBarHoldsItsLengthWhileTheMarksAnswerTheZoom()
    {
        var far = LegendWidthPx(pixelsPerMetre: 2f);
        var near = LegendWidthPx(pixelsPerMetre: 40f);

        Assert.True(far > 100f && far < 210f, $"a district framing drew a {far:F0} px bar");
        Assert.True(near > 100f && near < 210f, $"a close framing drew a {near:F0} px bar");
    }

    /// <summary>The whole instrument stays inside the frame it is drawn in — bottom-right, and not off the edge.</summary>
    [Fact]
    public void TheLegendKeepsToTheBottomRightCornerAndInsideTheFrame()
    {
        var uiPx = new Vector2(1024f, 768f);
        var quads = new OverlayQuad[512];
        var draw = new ScreenDraw(quads);

        ScaleLegend.Draw(ref draw, uiPx, pixelsPerMetre: 8f);

        Assert.True(draw.Written > 0);
        for (var quad = 0; quad < draw.Written; quad++)
        {
            var reach = Vector2.Abs(quads[quad].HalfSize);
            var lowest = quads[quad].Centre + reach;
            var highest = quads[quad].Centre - reach;

            Assert.True(lowest.X <= uiPx.X && lowest.Y <= uiPx.Y, $"quad {quad} runs off the frame");
            Assert.True(highest.X >= uiPx.X * 0.5f, $"quad {quad} is not in the right-hand half");
            Assert.True(highest.Y >= uiPx.Y * 0.5f, $"quad {quad} is not in the bottom half");
        }
    }

    /// <summary>How wide the bar came out, read back off the quads the legend emitted.</summary>
    static float LegendWidthPx(float pixelsPerMetre)
    {
        var quads = new OverlayQuad[512];
        var draw = new ScreenDraw(quads);
        ScaleLegend.Draw(ref draw, new Vector2(1024f, 768f), pixelsPerMetre);

        // The bar is the first pair of quads: the shadow behind it, then the bar itself.
        return quads[1].HalfSize.X * 2f;
    }
}
