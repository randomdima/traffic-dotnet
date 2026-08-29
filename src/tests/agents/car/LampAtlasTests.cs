using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The sheet the lamps are cut into and the arithmetic that indexes it (CAR-14a). <b>What is checked
/// here is the agreement between the picture and the numbers</b> — a cut taken at one resolution and
/// drawn at another, or a cell counted one way by the bake and another by the renderer, is a lamp on
/// every car in the town and nothing else in the suite would say so.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class LampAtlasTests
{
    static readonly CarCatalog Catalogue = CarCatalog.Shared;

    /// <summary>
    /// <b>Every car in the fleet is drawn at one resolution</b>, and it is the one a lamp is cut and
    /// drawn at (<see cref="LampAtlas.ArtPxPerM"/>). A sprite authored finer or coarser would have its
    /// lamps cut off the texels beside the ones the artist drew them on, further out the longer the car.
    /// </summary>
    [Fact]
    public void EverySpriteIsDrawnAtTheFleetsOwnResolution()
    {
        foreach (var variant in Catalogue.Variants)
        {
            var size = Image.Identify(variant.SpritePath).Size;
            var alongPxPerM = size.Width / variant.FootprintM.X;
            var acrossPxPerM = size.Height / variant.FootprintM.Y;

            // Within a texel over the whole body, which is what "the same grid" can mean when the
            // footprint is a measurement in metres and the picture is a whole number of texels.
            Assert.True(
                MathF.Abs(alongPxPerM - LampAtlas.ArtPxPerM) * variant.FootprintM.X < 1f,
                $"{variant.Id} is drawn at {alongPxPerM:F1} px/m along, not {LampAtlas.ArtPxPerM}.");
            Assert.True(
                MathF.Abs(acrossPxPerM - LampAtlas.ArtPxPerM) * variant.FootprintM.Y < 1f,
                $"{variant.Id} is drawn at {acrossPxPerM:F1} px/m across, not {LampAtlas.ArtPxPerM}.");
        }
    }

    /// <summary>
    /// <b>A lens is a section of the picture and so has to be on it</b>: a rectangle reaching past the
    /// sprite is a lamp measured off the edge of the car, which cuts a clipped patch and draws it
    /// centred, moving the lamp.
    /// </summary>
    [Fact]
    public void EveryLensIsWithinTheSpriteItIsMeasuredOff()
    {
        foreach (var variant in Catalogue.Variants)
        {
            var half = variant.FootprintM * 0.5f;
            foreach (var lens in variant.Lenses)
            {
                var least = lens.AtBodyM - (lens.SizeM * 0.5f);
                var most = lens.AtBodyM + (lens.SizeM * 0.5f);

                Assert.True(lens.SizeM.X > 0f && lens.SizeM.Y > 0f, $"{variant.Id} draws a lens of no size.");
                Assert.True(
                    least.X >= -half.X && least.Y >= -half.Y && most.X <= half.X && most.Y <= half.Y,
                    $"{variant.Id}'s {lens.Fitting} lens reaches past the picture it is measured off.");
            }
        }
    }

    /// <summary>
    /// <b>A lens fits its cell.</b> The cell is one square for every lamp in the town, and a lens larger
    /// than it would be cut down to fit — silently, since the bake has nowhere else to put the texels.
    /// </summary>
    [Fact]
    public void EveryLensFitsTheCellItIsCutInto()
    {
        foreach (var variant in Catalogue.Variants)
        {
            foreach (var lens in variant.Lenses)
            {
                Assert.True(
                    lens.SizeM.X <= LampAtlas.CellM && lens.SizeM.Y <= LampAtlas.CellM,
                    $"{variant.Id}'s {lens.Fitting} lens is {lens.SizeM} m, past the {LampAtlas.CellM:F2} m cell.");
            }
        }
    }

    /// <summary>
    /// <b>The shipped sheet is the grid the renderer indexes into</b>: a row a look, two columns a lens.
    /// A sheet cut to a different size draws every lamp in the town as a slice of the wrong cell, and it
    /// is the one mistake no arithmetic in the build would notice.
    /// </summary>
    [Fact]
    public void TheShippedSheetIsTheGridTheRendererCountsOn()
    {
        var atlas = Image.Identify(ProjectPaths.LampAtlasFile()).Size;

        Assert.Equal(LampAtlas.Columns * LampAtlas.CellPx, atlas.Width);
        Assert.Equal(Catalogue.SheetCount * LampAtlas.CellPx, atlas.Height);
    }

    /// <summary>
    /// <b>Nothing in the sheet is dark</b>. Every texel of it is drawn over the lamp's own glow
    /// (<c>LampSprites</c>), so a texel darker than that light is a hole punched in it — which is what a
    /// lens whose glass the artist painted deep against a bright highlight used to draw down the middle
    /// of its own glow. A lit lens shades with how solidly it is drawn (<see cref="LampAtlasBake.Dimmest"/>).
    /// </summary>
    [Fact]
    public void NoTexelOfALitLensIsDarkerThanTheLightAroundIt()
    {
        using var sheet = Image.Load<Rgba32>(ProjectPaths.LampAtlasFile());
        var least = (byte)(LampAtlasBake.Dimmest * 255f);

        sheet.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    // Every lamp colour has a channel at full, so the dimmest a burning texel can be
                    // drawn is that channel at Dimmest. Anything under it is bodywork, not light.
                    var texel = row[x];
                    if (texel.A == 0) continue;

                    Assert.True(
                        Math.Max(texel.R, Math.Max(texel.G, texel.B)) >= least,
                        $"the cell at {x / LampAtlas.CellPx},{y / LampAtlas.CellPx} draws " +
                        $"{texel.R},{texel.G},{texel.B} at alpha {texel.A}.");
                }
            }
        });
    }

    /// <summary>
    /// The bake fills a fitting's cells by walking <see cref="LampAtlas.ColourAt"/> and the renderer
    /// picks one with <see cref="LampAtlas.StateOf"/>. <b>They are one table read both ways round</b>,
    /// and a lamp whose two readings disagreed would burn the wrong colour.
    /// </summary>
    [Fact]
    public void EveryColourACellHoldsIsTheColourThatFindsIt()
    {
        foreach (var fitting in Enum.GetValues<CarLampFitting>())
        {
            for (var state = 0; state < LampAtlas.StatesOf(fitting); state++)
            {
                Assert.Equal(state, LampAtlas.StateOf(fitting, LampAtlas.ColourAt(fitting, state)));
            }
        }
    }
}
