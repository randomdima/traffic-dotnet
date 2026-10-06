using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// The packing, checked without a device: where a sheet landed, that no two landed on top of one
/// another, and what an underlay paints under the art.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class SheetAtlasTests
{
    static SheetSource Sized(int width, int height) => SheetSource.Generated(new byte[width * height * 4], width, height);

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

    /// <summary>The uniform block lays a place at two <c>vec4</c>s (std140), whatever the struct's own fields add up to.</summary>
    [Fact]
    public void APlaceIsTwoVec4sWide()
    {
        Assert.Equal(32, System.Runtime.CompilerServices.Unsafe.SizeOf<SheetPlace>());
    }

    /// <summary>
    /// <b>An underlay shows where the art is clear and nowhere it is opaque</b>, and the art keeps its
    /// own texels at the centre of the margin the sheet was padded by.
    /// </summary>
    [Fact]
    public void AnUnderlayShowsOnlyWhereTheArtIsClear()
    {
        // Four by two of art: an opaque white left half, a clear right half.
        var art = new byte[4 * 2 * 4];
        for (var row = 0; row < 2; row++)
        {
            for (var column = 0; column < 2; column++) art.AsSpan(((row * 4) + column) * 4, 4).Fill(byte.MaxValue);
        }

        var red = new Texel(255, 0, 0, 255);
        var underlay = new SheetUnderlay(1, 1, [new TexelBox(Vector2.Zero, new Vector2(6f, 4f))], red);
        var source = SheetSource.Generated(art, 4, 2) with { Underlay = underlay };

        var sheet = SheetAtlas.Decode(source);

        Assert.Equal((6, 4), SheetAtlas.Measure(source));
        Assert.Equal(new Texel(255, 255, 255, 255), sheet[(1 * 6) + 1]);
        Assert.Equal(red, sheet[(1 * 6) + 4]);
        Assert.Equal(red, sheet[0]);
    }

    /// <summary>A box edge between texels covers the texel it cuts by the share it covers, so a painted edge is no harder than the art's.</summary>
    [Fact]
    public void ABoxCoversATexelItCutsByItsShare()
    {
        var underlay = new SheetUnderlay(0, 0, [new TexelBox(new Vector2(0.5f, 0f), new Vector2(2f, 1f))], new Texel(0, 0, 0, 255));

        var sheet = underlay.Under(new Texel[2], 2, 1);

        Assert.Equal(128, sheet[0].A);
        Assert.Equal(255, sheet[1].A);
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
        var atlas = SheetAtlas.Pack(TownSprites.Load(SimConfig.Shipped()).Sheets);

        Assert.InRange(atlas.Pages, 1, 4);
    }

    static bool Overlaps(SheetPlace left, SheetPlace right) =>
        left.OriginUv.X < right.OriginUv.X + right.ScaleUv.X &&
        right.OriginUv.X < left.OriginUv.X + left.ScaleUv.X &&
        left.OriginUv.Y < right.OriginUv.Y + right.ScaleUv.Y &&
        right.OriginUv.Y < left.OriginUv.Y + left.ScaleUv.Y;
}
