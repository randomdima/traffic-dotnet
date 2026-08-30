using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// The packing, checked without a device: where a sheet landed, that no two landed on top of one
/// another, and that the one sheet which tiles was kept out of the pages altogether.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class SheetAtlasTests
{
    static SheetSource Sized(int width, int height) => SheetSource.Generated(new byte[width * height * 4], width, height);

    /// <summary>The town's one tiling sheet, by the file it ships as: the packer measures what it is handed.</summary>
    static SheetSource Tread() => SheetSource.File(ProjectPaths.TreadFile(), repeats: true, mipped: true);

    [Fact]
    public void EverySheetLandsInsideAPage()
    {
        var sheets = new List<SheetSource>();
        for (var sheet = 0; sheet < 40; sheet++) sheets.Add(Sized(100 + (sheet * 17), 60 + (sheet * 23)));

        var atlas = SheetAtlas.Pack(sheets);

        Assert.True(atlas.Pages >= 1);
        for (var sheet = 0; sheet < sheets.Count; sheet++)
        {
            var place = atlas.Places[sheet];
            Assert.InRange(place.Layer, 0f, atlas.Pages - 1f);
            Assert.InRange(place.OriginUv.X + place.ScaleUv.X, 0f, 1f);
            Assert.InRange(place.OriginUv.Y + place.ScaleUv.Y, 0f, 1f);
        }
    }

    /// <summary>
    /// Two sheets sharing texels would draw one another's pictures, and the gutter is what the check
    /// is made against: rectangles a texel apart are as bad as rectangles that overlap.
    /// </summary>
    [Fact]
    public void NoTwoSheetsShareAPageTexel()
    {
        var sheets = new List<SheetSource>();
        for (var sheet = 0; sheet < 60; sheet++) sheets.Add(Sized(300 + (sheet * 11), 200 + (sheet * 29)));

        var atlas = SheetAtlas.Pack(sheets);

        for (var left = 0; left < sheets.Count; left++)
        {
            for (var right = left + 1; right < sheets.Count; right++)
            {
                if (atlas.Places[left].Layer != atlas.Places[right].Layer) continue;

                Assert.False(Overlaps(atlas.Places[left], atlas.Places[right]),
                    $"Sheets {left} and {right} overlap on page {atlas.Places[left].Layer}.");
            }
        }
    }

    [Fact]
    public void TheSheetThatTilesIsKeptOutOfThePages()
    {
        var sheets = new List<SheetSource> { Sized(64, 64), Tread(), Sized(64, 64) };

        var atlas = SheetAtlas.Pack(sheets);

        Assert.Equal(1, atlas.TileSheet);
        Assert.Equal(1f, atlas.Places[1].Tiles);
        Assert.Equal(Vector2.One, atlas.Places[1].ScaleUv);
        Assert.Equal(0f, atlas.Places[0].Tiles);
        Assert.Equal(0f, atlas.Places[2].Tiles);
    }

    /// <summary>The sprite shader has one tile sampler, so a town with two tiling sheets is a build error and not a wrong picture.</summary>
    [Fact]
    public void ASecondTilingSheetIsRefused()
    {
        var sheets = new List<SheetSource> { Tread(), Tread() };

        Assert.Throws<InvalidOperationException>(() => SheetAtlas.Pack(sheets));
    }

    /// <summary>A sheet is measured by the packer, so the aspects the town shapes its quads by come out of the same table.</summary>
    [Fact]
    public void ASheetKeepsItsOwnSize()
    {
        var atlas = SheetAtlas.Pack([Sized(300, 120)]);

        Assert.Equal(300f, atlas.Places[0].WidthPx);
        Assert.Equal(120f, atlas.Places[0].HeightPx);
    }

    /// <summary>
    /// What the shipped art costs on the GPU. A page is 64 MB and the pages are the whole of the
    /// sprite memory, so this count <em>is</em> the atlas's price: the shipped art takes three, at
    /// around three quarters full, and a fourth is worth being told about before it is paid for.
    /// </summary>
    [Fact]
    public void TheShippedArtPacksIntoAHandfulOfPages()
    {
        var atlas = SheetAtlas.Pack(TownSprites.Load().Sheets);

        Assert.InRange(atlas.Pages, 1, 4);
    }

    static bool Overlaps(SheetPlace left, SheetPlace right) =>
        left.OriginUv.X < right.OriginUv.X + right.ScaleUv.X &&
        right.OriginUv.X < left.OriginUv.X + left.ScaleUv.X &&
        left.OriginUv.Y < right.OriginUv.Y + right.ScaleUv.Y &&
        right.OriginUv.Y < left.OriginUv.Y + left.ScaleUv.Y;
}
