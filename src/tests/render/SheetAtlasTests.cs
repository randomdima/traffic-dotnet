using System.Numerics;
using System.Text.RegularExpressions;
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
    /// <b>Each shader's table of places is <see cref="SheetAtlas.Slots"/> long</b>: one shorter reads a sheet past
    /// its end as nothing, and one longer binds past the buffer the town uploads.
    /// </summary>
    [Theory]
    [InlineData("src/app/render/kernels/quad.glsl", @"SheetPlace place\[(\d+)\]")]
    [InlineData("src/runtime/shaders/town.wgsl", @"array<Place, (\d+)>")]
    public void EachShadersTableHoldsTheAtlasSlots(string shader, string declaration)
    {
        var source = File.ReadAllText(Path.Combine(ProjectPaths.Root, shader));

        Assert.Equal(SheetAtlas.Slots, int.Parse(Regex.Match(source, declaration).Groups[1].Value));
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
    /// sprite memory, so this count <em>is</em> the atlas's price: the shipped art takes five, the
    /// building roofs three and a third of them at their art grid (the owner, 2026-10-20), and a sixth
    /// is worth being told about before it is paid for.
    /// </summary>
    [Fact]
    public void TheShippedArtPacksIntoAHandfulOfPages()
    {
        var atlas = SheetAtlas.Pack(TownSprites.Load(SimConfig.Shipped()).Sheets);

        Assert.InRange(atlas.Pages, 1, 5);
    }

    static bool Overlaps(SheetPlace left, SheetPlace right) =>
        left.OriginUv.X < right.OriginUv.X + right.ScaleUv.X &&
        right.OriginUv.X < left.OriginUv.X + left.ScaleUv.X &&
        left.OriginUv.Y < right.OriginUv.Y + right.ScaleUv.Y &&
        right.OriginUv.Y < left.OriginUv.Y + left.ScaleUv.Y;
}
