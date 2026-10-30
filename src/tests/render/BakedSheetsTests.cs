using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// <b>An atlas packed from the build's blocks</b> (<see cref="BakedSheets"/>): every box on a block's corner and none over
/// another, every sheet's place its art inside its gutter, a gutter that repeats the sheet's own edge, and a sheet that
/// changed since the bake told apart from the one baked.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class BakedSheetsTests
{
    static SheetSource Generated(int width, int height, byte shade = 0)
    {
        var rgba = new byte[width * height * 4];
        Array.Fill(rgba, shade);
        return SheetSource.Generated(rgba, width, height);
    }

    /// <summary>A record of each sheet boxed as the bake boxes it, every box's blocks end to end.</summary>
    static BakedSheets Baked(IReadOnlyList<SheetSource> sheets)
    {
        var entries = new List<BakedSheets.Entry> { new(BakedSheets.ClearSlot, 1, 1, 4, 4, 0, 0, 0, "-") };
        long at = 16;
        for (var slot = 0; slot < sheets.Count; slot++)
        {
            var (texels, width, height) = SheetAtlas.Prepared(sheets[slot]);
            var (_, boxWidth, boxHeight) = SheetAtlas.Boxed(texels, width, height);
            var (artWidth, artHeight) = SheetAtlas.Measure(sheets[slot]);
            entries.Add(new(slot, artWidth, artHeight, boxWidth, boxHeight, at, at, BakedSheets.Print(sheets[slot].Rgba), "-"));
            at += boxWidth / 4 * (boxHeight / 4) * 16;
        }

        return BakedSheets.Recorded(SheetFormat.Bc7, entries);
    }

    static List<SheetSource> Sheets()
    {
        var sheets = new List<SheetSource>();
        for (var sheet = 0; sheet < 40; sheet++) sheets.Add(Generated(37 + (sheet * 13), 21 + (sheet * 7), (byte)sheet));
        return sheets;
    }

    [Fact]
    public void EveryBoxStandsOnABlocksCornerAndNoneOverAnother()
    {
        var atlas = SheetAtlas.Pack(Sheets(), Baked(Sheets()));
        var boxes = atlas.Boxes().ToArray();

        foreach (var (_, x, y, blocks) in boxes)
        {
            Assert.Equal((0, 0, 0, 0), (x % 4, y % 4, blocks.BoxWidthPx % 4, blocks.BoxHeightPx % 4));
        }

        for (var left = 0; left < boxes.Length; left++)
        {
            for (var right = left + 1; right < boxes.Length; right++)
            {
                var (a, b) = (boxes[left], boxes[right]);
                var overlap = a.Page == b.Page && a.X < b.X + b.Blocks.BoxWidthPx && b.X < a.X + a.Blocks.BoxWidthPx &&
                              a.Y < b.Y + b.Blocks.BoxHeightPx && b.Y < a.Y + a.Blocks.BoxHeightPx;
                Assert.False(overlap, $"boxes {left} and {right} overlap");
            }
        }
    }

    /// <summary>A sheet's place is its art, a gutter into its box, at its art's size.</summary>
    [Fact]
    public void ASheetsPlaceIsItsArtInsideItsGutter()
    {
        var sheets = Sheets();
        var atlas = SheetAtlas.Pack(sheets, Baked(sheets));
        var boxes = atlas.Boxes().ToArray();

        var place = atlas.Places[5];
        var (_, x, y, _) = boxes[5];
        Assert.Equal(new Vector2(x + BakedSheets.GutterPx, y + BakedSheets.GutterPx) / SheetAtlas.PagePx, place.OriginUv);
        Assert.Equal(new Vector2(sheets[5].WidthPx, sheets[5].HeightPx) / SheetAtlas.PagePx, place.ScaleUv);
    }

    [Fact]
    public void EverySheetNothingDrawsSharesTheOneClearBlock()
    {
        var sheets = Sheets();
        sheets[3] = sheets[3] with { Unused = true };
        sheets[9] = sheets[9] with { Unused = true };

        var atlas = SheetAtlas.Pack(sheets, Baked(sheets));

        Assert.Equal(sheets.Count - 2 + 1, atlas.Boxes().Count());
        Assert.Equal(atlas.Places[3].OriginUv, atlas.Places[9].OriginUv);
    }

    /// <summary>The gutter is the sheet's own edge: a box's corner is the art's corner, and its rim the art's rim.</summary>
    [Fact]
    public void AGutterRepeatsTheSheetsOwnEdge()
    {
        var texels = new Texel[3 * 2];
        for (var at = 0; at < texels.Length; at++) texels[at] = new Texel((byte)(at + 1), 0, 0, 255);

        var (box, width, height) = SheetAtlas.Boxed(texels, 3, 2);

        Assert.Equal((12, 12), (width, height));
        Assert.Equal(texels[0], box[0]);
        Assert.Equal(texels[2], box[width - 1]);
        Assert.Equal(texels[5], box[^1]);
    }

    /// <summary>A line reads back as the entry written, and one of the shape that carried a step a sheet was packed at reads as none.</summary>
    [Fact]
    public void ALineReadsBackAsWrittenAndAnOlderShapeAsNone()
    {
        var entry = new BakedSheets.Entry(7, 832, 1288, 840, 1296, 16, 32, 0xd47c4de1cd5d251e, "assets/a/b.webp");

        Assert.Equal(entry, BakedSheets.Entry.Parse(entry.ToString()));
        Assert.Null(BakedSheets.Entry.Parse("7 1 832 1288 840 1296 16 16 d47c4de1cd5d251e assets/a/b.webp"));
    }

    [Fact]
    public void AGeneratedSheetChangedSinceTheBakeIsNotTheOneBaked()
    {
        var sheets = Sheets();
        var baked = Baked(sheets);
        sheets[7] = Generated(sheets[7].WidthPx, sheets[7].HeightPx, shade: 200);

        Assert.True(baked.Matches(Sheets(), out _));
        Assert.False(baked.Matches(sheets, out _));
    }
}
