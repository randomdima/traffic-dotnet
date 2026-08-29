using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using TrafficSimulation.World.Statics;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// OBJ-5a: <b>the rectangles a roof is collided as, asked of the picture they were measured off</b>. A
/// part list is authored art like a footprint or a hull, and the only thing that can say whether it is
/// still the right art is the art.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class BuildingPartsTests
{
    static readonly BuildingCatalog Catalogue = BuildingCatalog.Load();

    /// <summary>Below this a wall is drawn where nothing stops a car, which is the defect OBJ-5a names.</summary>
    const float MustCover = 0.85f;

    /// <summary>
    /// And above this the parts have gone back to being the box: a cover that spills over the roof stops
    /// a car short of a wall that is not there, which is the same defect facing the other way.
    /// </summary>
    const float MayOverhang = 0.05f;

    [Fact]
    public void EveryRoofIsCollidedAsTheWallsItIsDrawnWith()
    {
        foreach (var variant in Catalogue.Variants)
        {
            Assert.NotEmpty(variant.PartsM);

            using var art = SixLabors.ImageSharp.Image.Load<Rgba32>(variant.SpritePath);

            var roof = 0;
            var covered = 0;
            var spilt = 0;
            for (var y = 0; y < art.Height; y++)
            {
                for (var x = 0; x < art.Width; x++)
                {
                    var drawn = art[x, y].A > 24;
                    var inside = InsideAPart(variant, art.Size, x, y);
                    if (drawn) roof++;
                    if (drawn && inside) covered++;
                    if (!drawn && inside) spilt++;
                }
            }

            Assert.True(
                covered >= roof * MustCover,
                $"{variant.Id} is collided as {covered * 100f / roof:F0}% of the roof it draws");
            Assert.True(
                spilt <= roof * MayOverhang,
                $"{variant.Id} stands walls over {spilt * 100f / roof:F0}% of a roof's worth of open ground");
        }
    }

    /// <summary>
    /// <b>No part reaches past the footprint the roof is drawn at.</b> A part that did would be collision
    /// geometry standing on the pavement outside a picture, which no framing would ever show.
    /// </summary>
    [Fact]
    public void NoPartStandsOutsideItsOwnFootprint()
    {
        foreach (var variant in Catalogue.Variants)
        {
            var half = variant.FootprintM * 0.5f;
            foreach (var part in variant.PartsM)
            {
                var reach = Vector2.Abs(part.AtM) + (part.SizeM * 0.5f);
                Assert.True(
                    reach.X <= half.X + 1e-3f && reach.Y <= half.Y + 1e-3f,
                    $"{variant.Id} has a part reaching {reach * 2f} out of a {variant.FootprintM} footprint");
            }
        }
    }

    static bool InsideAPart(in BuildingVariant variant, SixLabors.ImageSharp.Size art, int x, int y)
    {
        // The pixel's middle, in the picture's own metres off the middle of the footprint.
        var atM = new Vector2(((x + 0.5f) / art.Width) - 0.5f, ((y + 0.5f) / art.Height) - 0.5f)
                  * variant.FootprintM;

        foreach (var part in variant.PartsM)
        {
            var offM = Vector2.Abs(atM - part.AtM);
            if (offM.X <= part.SizeM.X * 0.5f && offM.Y <= part.SizeM.Y * 0.5f) return true;
        }

        return false;
    }
}
