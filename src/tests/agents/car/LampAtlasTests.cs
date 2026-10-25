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
[Trait(Priority.Key, Priority.P8)]
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

        Assert.Equal(LampAtlas.SheetColumns * LampAtlas.CellPx, atlas.Width);
        Assert.Equal(LampAtlas.SheetRows(Catalogue.SheetCount) * LampAtlas.CellPx, atlas.Height);
    }

    /// <summary>
    /// <b>A lit lens is the dull one brighter</b>: over the texels its zone map gives it, every lit state of a zoned lens
    /// burns on average stronger (<see cref="Strength"/>) than the art it was cut from — the lamp the artist drew, switched
    /// on, rather than a picture of some other lamp. By how much is the art's: a lens painted near white has little room
    /// left (<see cref="LampAtlasBake.Brighten"/>).
    /// </summary>
    [Fact]
    public void EveryLitLensIsBrighterThanTheArtItWasCutFrom()
    {
        using var sheet = Image.Load<Rgba32>(ProjectPaths.LampAtlasFile());
        for (var row = 0; row < Catalogue.SheetCount; row++)
        {
            var variant = Catalogue.Variants[row];
            if (variant.ZonesPath is null) continue;

            using var sprite = Image.Load<Rgba32>(variant.SpritePath);
            var perM = new System.Numerics.Vector2(sprite.Width / variant.FootprintM.X, sprite.Height / variant.FootprintM.Y);
            for (var lens = 0; lens < variant.Lenses.Length; lens++)
            {
                var fitting = variant.Lenses[lens].Fitting;
                if (!LampAtlasBake.IsZoned(fitting)) continue;

                var centrePx = ((variant.FootprintM * 0.5f) + variant.Lenses[lens].AtBodyM) * perM;
                var originX = (int)MathF.Round(centrePx.X) - (LampAtlas.CellPx / 2);
                var originY = (int)MathF.Round(centrePx.Y) - (LampAtlas.CellPx / 2);
                for (var state = 0; state < LampAtlas.StatesOf(fitting); state++)
                {
                    var (column, sheetRow) = LampAtlas.CellOf(row, lens, state, variant.Lenses);
                    var (lit, drawn, count) = (0f, 0f, 0);
                    for (var y = 0; y < LampAtlas.CellPx; y++)
                    {
                        for (var x = 0; x < LampAtlas.CellPx; x++)
                        {
                            if (sheet[(column * LampAtlas.CellPx) + x, (sheetRow * LampAtlas.CellPx) + y].A == 0) continue;

                            lit += Strength(sheet[(column * LampAtlas.CellPx) + x, (sheetRow * LampAtlas.CellPx) + y]);
                            drawn += Strength(sprite[originX + x, originY + y]);
                            count++;
                        }
                    }

                    if (count == 0) continue;

                    Assert.True(
                        lit > drawn,
                        $"{variant.Id}'s {fitting} lens {lens} burns {lit / count:F2} over art of {drawn / count:F2}.");
                }
            }
        }
    }

    /// <summary>
    /// <b>Every clear texel of a lit cell carries the colour of a lit texel of that cell</b>: the sheet is sampled
    /// linearly at straight alpha, so the colour under a clear texel is mixed into the lens's edge, and the black an
    /// image clears to there is a dark ring between the lit lens and its glow.
    /// </summary>
    [Fact]
    public void TheClearTexelsRoundALitLensCarryItsColour()
    {
        using var sheet = Image.Load<Rgba32>(ProjectPaths.LampAtlasFile());
        var lit = new HashSet<(byte, byte, byte)>();
        for (var cellY = 0; cellY < sheet.Height; cellY += LampAtlas.CellPx)
        {
            for (var cellX = 0; cellX < sheet.Width; cellX += LampAtlas.CellPx)
            {
                lit.Clear();
                for (var texel = 0; texel < LampAtlas.CellPx * LampAtlas.CellPx; texel++)
                {
                    var at = sheet[cellX + (texel % LampAtlas.CellPx), cellY + (texel / LampAtlas.CellPx)];
                    if (at.A > 0) lit.Add((at.R, at.G, at.B));
                }

                if (lit.Count == 0) continue;

                for (var texel = 0; texel < LampAtlas.CellPx * LampAtlas.CellPx; texel++)
                {
                    var at = sheet[cellX + (texel % LampAtlas.CellPx), cellY + (texel / LampAtlas.CellPx)];
                    Assert.True(
                        at.A > 0 || lit.Contains((at.R, at.G, at.B)),
                        $"the cell at {cellX / LampAtlas.CellPx},{cellY / LampAtlas.CellPx} carries {at.R},{at.G},{at.B} " +
                        "under a clear texel, the colour of none of its lit ones.");
                }
            }
        }
    }

    /// <summary>
    /// How strong a texel's light is: its strongest channel. Not its luminance, which a lens that takes its light's
    /// hue — clear glass burning amber — loses however brightly it burns.
    /// </summary>
    static float Strength(Rgba32 texel) => Math.Max(texel.R, Math.Max(texel.G, texel.B)) / 255f;

    /// <summary>
    /// <b>A zone map is the size of the sprite it maps</b>, texel for texel: it is the same cell of the same sheet cut and
    /// shrunk the same way, and one of another size says which texel is a lamp about some other picture.
    /// </summary>
    [Fact]
    public void EveryZoneMapIsTheSizeOfItsSprite()
    {
        foreach (var variant in Catalogue.Variants)
        {
            if (variant.ZonesPath is not { } zonesPath) continue;

            Assert.Equal(Image.Identify(variant.SpritePath).Size, Image.Identify(zonesPath).Size);
        }
    }

    /// <summary>
    /// <b>A zoned lamp burns the texels its zone map paints as it and no others</b> (CAR-14a): every texel of each of its
    /// lens's cells lies, at the place on the body the lens's quad lays it back on, on its fitting's zone — and every
    /// lens the file authors finds one, or it is lit over its rectangle on a car that has a map saying where it is.
    /// </summary>
    [Fact]
    public void AZonedLampBurnsOnlyItsZone()
    {
        using var sheet = Image.Load<Rgba32>(ProjectPaths.LampAtlasFile());
        for (var row = 0; row < Catalogue.SheetCount; row++)
        {
            var variant = Catalogue.Variants[row];
            if (variant.ZonesPath is not { } zonesPath) continue;

            using var zones = Image.Load<Rgba32>(zonesPath);
            var perM = new System.Numerics.Vector2(zones.Width / variant.FootprintM.X, zones.Height / variant.FootprintM.Y);
            for (var lens = 0; lens < variant.Lenses.Length; lens++)
            {
                var fitting = variant.Lenses[lens].Fitting;
                if (!LampAtlasBake.IsZoned(fitting)) continue;

                var centrePx = ((variant.FootprintM * 0.5f) + variant.Lenses[lens].AtBodyM) * perM;
                var originX = (int)MathF.Round(centrePx.X) - (LampAtlas.CellPx / 2);
                var originY = (int)MathF.Round(centrePx.Y) - (LampAtlas.CellPx / 2);
                var burnt = 0;
                for (var state = 0; state < LampAtlas.StatesOf(fitting); state++)
                {
                    var (column, sheetRow) = LampAtlas.CellOf(row, lens, state, variant.Lenses);
                    for (var y = 0; y < LampAtlas.CellPx; y++)
                    {
                        for (var x = 0; x < LampAtlas.CellPx; x++)
                        {
                            if (sheet[(column * LampAtlas.CellPx) + x, (sheetRow * LampAtlas.CellPx) + y].A == 0) continue;

                            burnt++;
                            Assert.True(
                                LampAtlasBake.Holds(zones[originX + x, originY + y], fitting),
                                $"{variant.Id}'s {fitting} lens {lens} burns {x},{y} of its cell, off its zone.");
                        }
                    }
                }

                Assert.True(burnt > 0, $"{variant.Id}'s zone map gives its {fitting} lens {lens} no texel to burn.");
            }
        }
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
