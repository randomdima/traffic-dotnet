using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.App.Shot;
using Xunit;

namespace TrafficSimulation.Tests.Shot;

/// <summary>
/// SHT-3: several frames tiled into one picture, in reading order, with a gutter the town never draws
/// — and never a sheet with nothing in it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class SheetTests
{
    static readonly Rgba32 Gutter = new(255, 0, 255);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(9, 3)]
    public void TheGridIsAsSquareAsTheCellsMake(int cells, int columns) =>
        Assert.Equal(columns, Sheet.Columns(cells));

    /// <summary>Four cells are two abreast with one gutter between them, either way.</summary>
    [Fact]
    public void ASheetIsItsCellsPlusItsGutters()
    {
        Assert.Equal((640, 480), Sheet.SizeOf(1, 640, 480));
        Assert.Equal((640 * 2 + Sheet.GutterPx, 480 * 2 + Sheet.GutterPx), Sheet.SizeOf(4, 640, 480));
        Assert.Equal((640 * 3 + Sheet.GutterPx * 2, 480 * 2 + Sheet.GutterPx), Sheet.SizeOf(5, 640, 480));
    }

    /// <summary>
    /// Reading order, left to right and then down — which is what a review's claims are asked of, cell
    /// by cell, so a sheet laid the other way answers the wrong question about every one of them.
    /// </summary>
    [Fact]
    public void TheCellsAreTiledInReadingOrder()
    {
        var colours = new[] { new Rgba32(255, 0, 0), new Rgba32(0, 255, 0), new Rgba32(0, 0, 255), new Rgba32(255, 255, 0) };
        var cells = Array.ConvertAll(colours, colour => Flat(32, 24, colour));

        try
        {
            using var sheet = Sheet.Of(cells, "four.png");

            Assert.Equal((32 * 2 + Sheet.GutterPx, 24 * 2 + Sheet.GutterPx), (sheet.Width, sheet.Height));
            Assert.Equal(colours[0], sheet[0, 0]);
            Assert.Equal(colours[1], sheet[32 + Sheet.GutterPx, 0]);
            Assert.Equal(colours[2], sheet[0, 24 + Sheet.GutterPx]);
            Assert.Equal(colours[3], sheet[32 + Sheet.GutterPx, 24 + Sheet.GutterPx]);
            Assert.Equal(Gutter, sheet[32, 0]);
        }
        finally
        {
            foreach (var cell in cells) cell.Dispose();
        }
    }

    /// <summary>A cell photographed at another framing cannot be compared with the rest, so it is
    /// refused rather than laid over the gutter.</summary>
    [Fact]
    public void ACellOfAnotherSizeIsRefused()
    {
        using var first = Flat(32, 24, new Rgba32(255, 0, 0));
        using var odd = Flat(16, 24, new Rgba32(0, 255, 0));

        var complaint = Assert.Throws<InvalidOperationException>(() => Sheet.Of([first, odd], "two.png"));

        Assert.Contains("16x24", complaint.Message);
    }

    /// <summary>
    /// The failure that reads as a pass everywhere else: a file of the right name and the right size
    /// with no picture in it at all.
    /// </summary>
    [Fact]
    public void ASheetOfNothingButGutterIsRefused()
    {
        using var empty = Flat(32, 24, Gutter);

        Assert.Throws<InvalidOperationException>(() => Sheet.Of([empty, empty], "two.png"));
    }

    static Image<Rgba32> Flat(int widthPx, int heightPx, Rgba32 colour)
    {
        var image = new Image<Rgba32>(widthPx, heightPx);
        for (var y = 0; y < heightPx; y++)
            for (var x = 0; x < widthPx; x++)
                image[x, y] = colour;

        return image;
    }
}
