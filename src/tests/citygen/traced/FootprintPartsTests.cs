using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A traced footprint is cut into the few rectangles it is built of</b> (GEN-57, <see cref="FootprintParts"/>): a
/// rectangle on any bearing is itself, an L its two arms, a courtyard block its four wings, and a booth smaller than
/// a sentry box nothing.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class FootprintPartsTests
{
    static readonly CityGenFigures Figures = SimConfig.Shipped().CityGen;

    /// <summary>A 20 by 10 m rectangle turned 30° is one part, its own middle, sides and bearing.</summary>
    [Fact]
    public void ARectangleOnAnyBearingIsItself()
    {
        var turn = Matrix3x2.CreateRotation(MathF.PI / 6f) * Matrix3x2.CreateTranslation(100f, 200f);
        Vector2[] outline = [.. new Vector2[] { new(-10, -5), new(10, -5), new(10, 5), new(-10, 5) }.Select(atM => Vector2.Transform(atM, turn))];

        var parts = Cut(outline);

        var part = Assert.Single(parts);
        Assert.Equal(100f, part.CentreM.X, 1e-3f);
        Assert.Equal(200f, part.CentreM.Y, 1e-3f);
        Assert.Equal((10f, 20f), (MathF.Round(MathF.Min(part.SizeM.X, part.SizeM.Y), 3), MathF.Round(MathF.Max(part.SizeM.X, part.SizeM.Y), 3)));
        Assert.Equal(0f, MathF.Sin(2f * (part.HeadingRad - (MathF.PI / 6f))), 1e-4f);
    }

    /// <summary>An L of a 20 by 8 m arm and a 12 by 8 m one is those two arms, each to within a cell of the cut.</summary>
    [Fact]
    public void AnLIsItsTwoArms()
    {
        var parts = Cut([new(0, 0), new(20, 0), new(20, 8), new(8, 8), new(8, 20), new(0, 20)]);

        var cellM = 20f / Figures.TracedFootprintCells;
        float[] areasM2 = [.. parts.Select(part => part.SizeM.X * part.SizeM.Y).Order()];
        Assert.Equal(2, areasM2.Length);
        Assert.Equal(12f * 8f, areasM2[0], 20f * cellM);
        Assert.Equal(20f * 8f, areasM2[1], 20f * cellM);
    }

    /// <summary>A 40 by 30 m block round a 30 by 20 m courtyard is four wings, and none stands in the courtyard.</summary>
    [Fact]
    public void ACourtyardBlockIsItsFourWings()
    {
        var parts = Cut([new(0, 0), new(40, 0), new(40, 30), new(0, 30)], [new(5, 5), new(35, 5), new(35, 25), new(5, 25)]);

        Assert.Equal(4, parts.Count);
        Assert.DoesNotContain(parts, part => Vector2.Distance(part.CentreM, new Vector2(20, 15)) < 5f);
    }

    /// <summary>
    /// <b>A circle is one part rounded at its radius</b>, and a 20 by 10 m rectangle with one corner cut 3 m off at the
    /// junction one square part: a 12 m silo surveyed as sixteen sides reads as a disc to within a tenth of its radius.
    /// </summary>
    [Fact]
    public void ACircleIsOneRoundPartAndACutCornerIsStillSquare()
    {
        Vector2[] ring = [.. Enumerable.Range(0, 16).Select(at => 6f * new Vector2(MathF.Cos(at * MathF.PI / 8f), MathF.Sin(at * MathF.PI / 8f)))];

        var round = Assert.Single(Cut(ring));
        var cut = Assert.Single(Cut([new(0, 0), new(17, 0), new(20, 3), new(20, 10), new(0, 10)]));

        Assert.Equal(1f, round.CornerShare, 0.1f);
        Assert.Equal(0f, cut.CornerM, 1e-3f);
    }

    /// <summary>A booth smaller than <see cref="CityGenFigures.TracedFootprintSmallestM2"/> is no building.</summary>
    [Fact]
    public void ABoothSmallerThanASentryBoxIsNothing()
    {
        var sideM = MathF.Sqrt(Figures.TracedFootprintSmallestM2) * 0.9f;

        Assert.Empty(Cut([new(0, 0), new(sideM, 0), new(sideM, sideM), new(0, sideM)]));
    }

    static List<FootprintPart> Cut(Vector2[] outline, Vector2[]? courtyard = null)
    {
        Vector2[][] rings = courtyard is null ? [outline] : [outline, courtyard];
        var offsets = new List<int> { 0 };
        foreach (var ring in rings) offsets.Add(offsets[^1] + ring.Length);

        var footprints = new CityPlan.FootprintArrays
        {
            RingOffsets = [0, rings.Length],
            Rings = new CityPlan.RingArrays { Offsets = [.. offsets], PointM = [.. rings.SelectMany(ring => ring)] },
            Traced = [false], HeightM = [0f], Use = [FootprintUse.Unknown],
        };
        var parts = new List<FootprintPart>();
        new FootprintParts().Cut(footprints, 0, Figures, parts);
        return parts;
    }
}
