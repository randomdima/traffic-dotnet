using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// The line struck a distance outside a closed ring. Each test asks it for a shape whose offset is known
/// without deriving it a second way — a circle's own radius, a square's own corners, a slot narrower than
/// twice the offset — and the rule that makes an offset a boundary is asked of the answer itself: no point
/// of it stands nearer the ring than the offset it was struck at.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class ExtrusionTests
{
    /// <summary>
    /// A circle walked with its inside on the walker's right, which is the hand a perimeter keeps the town
    /// on — so the offset that takes a line outwards is the negative one, here as there.
    /// </summary>
    static ArcSeg[] Circle(float radiusM) =>
        [new ArcSeg(new Vector2(radiusM, 0f), MathF.PI * 0.5f, 2f * MathF.PI * radiusM, 1f / radiusM)];

    /// <summary>A closed ring of straights through the corners given, walked on the same hand.</summary>
    static ArcSeg[] Ring(params Vector2[] cornersM)
    {
        var ring = new ArcSeg[cornersM.Length];
        for (var corner = 0; corner < cornersM.Length; corner++)
        {
            var runM = cornersM[(corner + 1) % cornersM.Length] - cornersM[corner];
            ring[corner] = new ArcSeg(cornersM[corner], MathF.Atan2(runM.Y, runM.X), runM.Length(), 0f);
        }

        return ring;
    }

    /// <summary>How far a place stands off the nearest point of a chain, solved rather than walked to.</summary>
    static float OffTheChainM(ArcSeg[] chain, Vector2 pointM)
    {
        var lengthM = Spline.TotalLengthM(chain);
        var atM = Spline.ProjectM(chain, pointM, lengthM * 0.5f, lengthM);
        return (Spline.SampleAt(chain, atM).PositionM - pointM).Length();
    }

    /// <summary>
    /// <b>A circle extruded outwards is a circle of the radius plus the offset.</b> Asked of the answer's own
    /// line against the centre the circle was drawn about, rather than against a second derivation of it.
    /// </summary>
    [Theory]
    [InlineData(20f, 5f)]
    [InlineData(20f, 0.5f)]
    [InlineData(100f, 12f)]
    public void ACircleGrowsByTheOffsetItIsStruckAt(float radiusM, float outM)
    {
        var extruded = Extrusion.Of(Circle(radiusM), -outM, smoothM: 0f);

        Assert.NotEmpty(extruded);
        var lengthM = Spline.TotalLengthM(extruded);
        for (var atM = 0f; atM < lengthM; atM += 0.5f)
        {
            Assert.Equal(radiusM + outM, Spline.SampleAt(extruded, atM).PositionM.Length(), 0.05f);
        }
    }

    /// <summary>
    /// <b>The extrusion shuts on itself</b>, whatever it dropped on the way round — a boundary in pieces is
    /// not one, and the layer that draws it reads a line with two ends as a fault in the town.
    /// </summary>
    [Fact]
    public void TheExtrusionShutsOnItself()
    {
        var extruded = Extrusion.Of(
            Ring(new Vector2(0f, 0f), new Vector2(40f, 0f), new Vector2(40f, 40f), new Vector2(0f, 40f)),
            -5f, smoothM: 2.5f);

        var gapM = (extruded[^1].EndM - extruded[0].StartM).Length();
        Assert.True(gapM <= 0.01f, $"the ring shut {gapM:F3} m from where it set off");
    }

    /// <summary>
    /// <b>The rule that makes an offset a boundary</b>, asked of every metre of the answer: nothing of it
    /// stands nearer the ring than the offset. The shape is the one a piece-by-piece offset cannot do — a
    /// slot narrower than twice the offset, where each side lays a line past the other, and a right angle
    /// at every corner, where a straight drawn across the turn cuts a third of the distance off it.
    /// </summary>
    [Fact]
    public void NoMetreOfTheExtrusionStandsNearerThanTheOffset()
    {
        // A 40 m square with a 4 m slot cut 20 m into one side of it.
        var ring = Ring(
            new Vector2(0f, 0f), new Vector2(40f, 0f), new Vector2(40f, 40f), new Vector2(22f, 40f),
            new Vector2(22f, 20f), new Vector2(18f, 20f), new Vector2(18f, 40f), new Vector2(0f, 40f));

        const float outM = 5f;
        var extruded = Extrusion.Of(ring, -outM, smoothM: 0f);

        Assert.NotEmpty(extruded);
        var lengthM = Spline.TotalLengthM(extruded);
        for (var atM = 0f; atM < lengthM; atM += 0.1f)
        {
            var pointM = Spline.SampleAt(extruded, atM).PositionM;
            var offM = OffTheChainM(ring, pointM);
            Assert.True(offM >= outM - 0.05f, $"{atM:F1} m along, the extrusion stood {offM:F2} m off the ring");
        }
    }

    /// <summary>
    /// <b>A slot the offset cannot fit into is bridged rather than entered.</b> The same shape read the other
    /// way round: what the fold rule drops is a length of line, so the answer runs straight across the mouth
    /// of a 4 m slot instead of down it and back.
    /// </summary>
    [Fact]
    public void ASlotNarrowerThanTwiceTheOffsetIsBridged()
    {
        var ring = Ring(
            new Vector2(0f, 0f), new Vector2(40f, 0f), new Vector2(40f, 40f), new Vector2(22f, 40f),
            new Vector2(22f, 20f), new Vector2(18f, 20f), new Vector2(18f, 40f), new Vector2(0f, 40f));

        var extruded = Extrusion.Of(ring, -5f, smoothM: 0f);

        var lengthM = Spline.TotalLengthM(extruded);
        for (var atM = 0f; atM < lengthM; atM += 0.1f)
        {
            var pointM = Spline.SampleAt(extruded, atM).PositionM;
            Assert.False(
                pointM.X is > 16f and < 24f && pointM.Y is > 24f and < 44f,
                $"the extrusion ran into the slot at {pointM.X:F1}, {pointM.Y:F1}");
        }
    }

    /// <summary>
    /// <b>Nothing of a shape's outward extrusion stands inside the shape</b> — which the distance to the ring
    /// cannot say on its own. A slit is a ring that runs out along a line and back, and the offset comes
    /// round the half turn at its tip into the shape: those points are honestly the offset from the ring, and
    /// from everything else too, so what keeps them out is the side they stand on and nothing else.
    /// </summary>
    [Fact]
    public void NothingOfTheExtrusionStandsInsideTheShape()
    {
        // A 40 m square with a half-metre slit cut 15 m into one side — deep enough that the turn at its tip
        // swings clear of every other edge, and narrow enough that neither of its own sides survives.
        var ring = Ring(
            new Vector2(0f, 0f), new Vector2(40f, 0f), new Vector2(40f, 40f), new Vector2(20.25f, 40f),
            new Vector2(20.25f, 25f), new Vector2(19.75f, 25f), new Vector2(19.75f, 40f), new Vector2(0f, 40f));

        var extruded = Extrusion.Of(ring, -5f, smoothM: 0f);

        Assert.NotEmpty(extruded);
        var lengthM = Spline.TotalLengthM(extruded);
        for (var atM = 0f; atM < lengthM; atM += 0.1f)
        {
            var pointM = Spline.SampleAt(extruded, atM).PositionM;
            Assert.False(
                pointM.X is > 0f and < 40f && pointM.Y is > 0f and < 40f,
                $"{atM:F1} m along, the extrusion stood inside the square at {pointM.X:F1}, {pointM.Y:F1}");
        }
    }

    /// <summary>
    /// <b>Nothing the line does over less than the window survives the smoothing.</b> Struck at no offset at
    /// all, so that the smoothing is the whole of what the answer is: a square's corner turns its right angle
    /// at a point, and smoothed over a window it turns the same right angle over about that window of line.
    /// </summary>
    [Theory]
    [InlineData(2f)]
    [InlineData(6f)]
    public void ACornerTurnsOverTheWindowItIsSmoothedBy(float smoothM)
    {
        var smoothed = Extrusion.Of(
            Ring(new Vector2(0f, 0f), new Vector2(60f, 0f), new Vector2(60f, 60f), new Vector2(0f, 60f)),
            offsetM: 0f, smoothM);

        // The metres of the answer whose heading is neither of the two a corner stands between: what the
        // corners were spread over, read off the line itself.
        var lengthM = Spline.TotalLengthM(smoothed);
        var turningM = 0f;
        for (var atM = 0f; atM < lengthM; atM += 0.05f)
        {
            var offRad = MathF.Abs(MathF.IEEERemainder(Spline.SampleAt(smoothed, atM).HeadingRad, MathF.PI * 0.5f));
            if (offRad > 0.02f) turningM += 0.05f;
        }

        Assert.Equal(smoothM * 4f, turningM, smoothM * 4f * 0.25f);
    }

    /// <summary>
    /// <b>A ring the offset leaves nothing of comes back as nothing</b>, rather than as a line turned inside
    /// out: every point of a circle taken further inwards than its own radius stands nearer the ring than the
    /// offset, so the fold rule drops the lot.
    /// </summary>
    [Fact]
    public void ARingOffsetPastItsOwnSizeComesBackEmpty()
    {
        Assert.Empty(Extrusion.Of(Circle(4f), 6f, smoothM: 0f));
    }
}
