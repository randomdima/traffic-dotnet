using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.App.Shot;
using Xunit;

namespace TrafficSimulation.Tests.Shot;

/// <summary>
/// SHT-1 and SHT-2: the band goes under the frame and never into it, and it carries everything the
/// same picture would have to be asked for again with.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CaptionTests
{
    static ShotCaption ACaption(string? note = null) => new()
    {
        Label = "crossroad",
        Map = "Test",
        Ui = "nodes",
        Note = note,
        SpanM = new Vector2(60f, 45f),
        CentreM = new Vector2(120f, 90f),
        PxPerM = 10.67f,
        Tick = 1200,
        Seconds = 20,
        Seed = 7,
    };

    /// <summary>
    /// <b>The picture is left alone.</b> Every pixel of the frame is where it was, and the band is
    /// underneath it — which is what lets the same frame be compared against another build's.
    /// </summary>
    [Fact]
    public void TheFrameIsUntouchedAndTheBandIsUnderIt()
    {
        using var frame = Chequered(320, 240);
        var caption = ACaption();

        using var composed = Caption.Under(frame, caption, Caption.Rows(caption));

        Assert.Equal(frame.Width, composed.Width);
        Assert.Equal(frame.Height + Caption.HeightPx(frame.Width, 2), composed.Height);
        for (var y = 0; y < frame.Height; y++)
            for (var x = 0; x < frame.Width; x++)
                Assert.Equal(frame[x, y], composed[x, y]);
    }

    /// <summary>A cell with a note is one row taller, and every cell of a sheet is given the taller
    /// band: cells of two heights cannot be tiled.</summary>
    [Fact]
    public void ANoteIsARowOfItsOwn()
    {
        Assert.Equal(2, Caption.Rows(ACaption()));
        Assert.Equal(3, Caption.Rows(ACaption("the stripes must be square to the kerb")));
        Assert.True(Caption.HeightPx(640, 3) > Caption.HeightPx(640, 2));

        using var frame = Chequered(320, 240);
        using var plain = Caption.Under(frame, ACaption(), 3);
        using var noted = Caption.Under(frame, ACaption("look at the kerb"), 3);

        Assert.Equal(plain.Height, noted.Height);
    }

    /// <summary>
    /// The figures row is what makes the picture reproducible: the framing, the moment and the seed.
    /// </summary>
    [Fact]
    public void TheFiguresRowSaysHowToTakeThePictureAgain()
    {
        var figures = ACaption().Figures();

        Assert.Contains("60x45 m", figures);
        Assert.Contains("120,90", figures);
        Assert.Contains("10.7 px/m", figures);
        Assert.Contains("tick 1200", figures);
        Assert.Contains("seed 7", figures);
    }

    /// <summary>A frame taken at tick zero is the plan and not a moment, so it is not dated as one.</summary>
    [Fact]
    public void AFrameOfThePlanIsNotDatedAsAMoment()
    {
        var figures = (ACaption() with { Tick = 0, Seconds = 0 }).Figures();

        Assert.Contains("tick 0", figures);
        Assert.DoesNotContain("(0.0 s)", figures);
    }

    /// <summary>The head names the cell, the map and what was switched on for it.</summary>
    [Fact]
    public void TheHeadNamesTheCellTheMapAndTheLayers()
    {
        Assert.Equal("crossroad — Test · ui nodes", ACaption().Head());
        Assert.Equal("Test", (ACaption() with { Label = null, Ui = string.Empty }).Head());
    }

    /// <summary>Something was actually lettered: a band of flat panel colour is a caption that failed
    /// silently, which is the one way this could go wrong and still look like it worked.</summary>
    [Fact]
    public void TheBandIsLetteredAndNotLeftBlank()
    {
        using var frame = Chequered(640, 480);
        var caption = ACaption("the stripes must be square to the kerb");

        using var composed = Caption.Under(frame, caption, Caption.Rows(caption));

        var colours = new HashSet<uint>();
        for (var y = frame.Height; y < composed.Height; y++)
            for (var x = 0; x < composed.Width; x++)
                colours.Add(composed[x, y].PackedValue);

        Assert.True(colours.Count > 8, $"the band carries {colours.Count} colours — nothing was written into it");
    }

    /// <summary>A line that will not fit loses its tail rather than running off the band.</summary>
    [Fact]
    public void ALineThatWillNotFitIsCutRatherThanRunOff()
    {
        const string sentence = "the pavement must sweep round the corner rather than kinking at it";
        const int textPx = 13;

        var cut = GlyphStamp.Fit(sentence, GlyphStamp.WidthPx(20, textPx), textPx);

        Assert.Equal(20, cut.Length);
        Assert.EndsWith("...", cut);
        Assert.Equal(sentence, GlyphStamp.Fit(sentence, GlyphStamp.WidthPx(sentence.Length, textPx), textPx));
    }

    /// <summary>Two colours in a lattice, which is a picture rather than a fill.</summary>
    static Image<Rgba32> Chequered(int widthPx, int heightPx)
    {
        var frame = new Image<Rgba32>(widthPx, heightPx);
        for (var y = 0; y < heightPx; y++)
            for (var x = 0; x < widthPx; x++)
                frame[x, y] = (x / 8 + y / 8) % 2 == 0 ? new Rgba32(20, 120, 40) : new Rgba32(90, 90, 90);

        return frame;
    }
}
