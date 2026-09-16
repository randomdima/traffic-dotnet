using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>A closed shape moved off itself</b> (<see cref="ArcOutset"/>), asked of the shapes whose answer can
/// be written down: a square, whose every corner opens when it is moved outward and closes when it is moved
/// in; a circle, which has no corner at all; and three shapes the move has to <em>delete</em> something
/// out of — a slot narrower than the distance, a ring smaller than it, and two rings nearer than twice it.
/// </summary>
/// <remarks>
/// <b>The length of the answer is what most of these check</b>, because it is the one figure that carries
/// every part of the construction at once: a mitre too long, a round at the wrong radius, a trim taken off
/// the wrong end, a corner joined twice or a fold left in all come out as a different number of metres. The
/// three that delete something check the count of rings instead, which is the only thing a deletion can be
/// read off.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class ArcOutsetTests
{
    const float ToleranceM = 1e-3f;

    /// <summary>How long a side of the test square is, and how far it is moved. Metres, and no relation between them beyond the second being much the smaller.</summary>
    const float SideM = 40f;

    const float MovedM = 5f;

    /// <summary>
    /// <b>Every corner of a square opens when the shape is moved outward, and what fills it is the arc of
    /// the distance moved about the corner</b> — never the mitre, which would stand <c>d/cos(θ/2)</c> off a
    /// place it was asked to stand <c>d</c> off. The moved square is its four sides and four quarter
    /// circles: <c>4·L + 2·π·d</c>.
    /// </summary>
    [Fact]
    public void MovingASquareOutRoundsTheFourCornersItOpens()
    {
        var (rings, loose) = ArcOutset.Of([Square(SideM)], MovedM, smoothing: 0f);

        Assert.Empty(loose);
        Assert.Equal(
            (4f * SideM) + (2f * MathF.PI * MovedM),
            Spline.TotalLengthM(Assert.Single(rings)),
            ToleranceM);
    }

    /// <summary>
    /// <b>And every corner of it closes when the shape is moved in</b>, which is the same corner solved the
    /// other way round: the two pieces are cut back to the place they meet, so each side loses <c>2·d</c>.
    /// </summary>
    [Fact]
    public void MovingASquareInTrimsTheFourCornersItCloses()
    {
        var (rings, _) = ArcOutset.Of([Square(SideM)], -MovedM, smoothing: 0f);

        Assert.Equal(4f * MovedInSideM, Spline.TotalLengthM(Assert.Single(rings)), ToleranceM);
    }

    /// <summary>
    /// <b>No part of a smoothed answer stands nearer the shape than the distance it was moved off it.</b>
    /// A line laid to keep off something may keep further off than it was asked to and may never come
    /// nearer, so <b>the rounding is one-sided</b>: it fills the notches, which takes the line away, and it
    /// leaves the corners that turn away, which nothing could round without cutting inside them.
    /// </summary>
    /// <remarks>
    /// <b>It is the whole of what the figure may do, and it is asked of the answer rather than of the
    /// construction.</b> Read at a hand's breadth along every piece of the ring — the ends of a fill are on
    /// the offset by construction and it is the middle of one that leaves it.
    /// </remarks>
    [Theory]
    [InlineData(0.25f)]
    [InlineData(1f)]
    public void SmoothingNeverBringsTheAnswerNearerTheShape(float smoothing)
    {
        var shape = SlottedSquare(SideM, slotM: 4f * MovedM, deepM: SideM * 0.5f);
        var (rings, loose) = ArcOutset.Of([shape], MovedM, smoothing);

        Assert.Empty(loose);
        foreach (var piece in Assert.Single(rings))
        {
            for (var atM = 0f; atM <= piece.LengthM; atM += ReadEveryM)
            {
                var pointM = piece.PointAtM(atM);
                Assert.True(
                    OffM(shape, pointM) >= MovedM - ToleranceM,
                    $"{pointM.X:F2}, {pointM.Y:F2} stands {OffM(shape, pointM):F3} m off the shape, "
                    + $"which was moved {MovedM:F1}");
            }
        }
    }

    /// <summary>How finely the answer is read for having come inside: a hand's breadth.</summary>
    const float ReadEveryM = 0.1f;

    /// <summary>How far one place stands off a shape, which is the distance to the nearest place on it.</summary>
    static float OffM(ArcSeg[] shape, Vector2 pointM)
    {
        var lengthM = Spline.TotalLengthM(shape);
        var alongM = Spline.ProjectM(shape, pointM, lengthM * 0.5f, lengthM);
        return Vector2.Distance(Spline.SampleAt(shape, alongM).PositionM, pointM);
    }

    /// <summary>
    /// <b>The smoothing takes the notches out and leaves every corner that turns away from the shape</b>,
    /// which is the same rule told as a count: a slot's two mouths are corners the shape turns in at, so
    /// their offset is a notch and the rounding fills it; the slot's own two far corners turn away, so
    /// their offset is the arc of the distance moved and there is nothing to fill.
    /// </summary>
    [Fact]
    public void SmoothingTakesTheNotchesOutAndLeavesTheCornersThatTurnAway()
    {
        var shape = SlottedSquare(SideM, slotM: 4f * MovedM, deepM: SideM * 0.5f);

        var (plain, _) = ArcOutset.Of([shape], MovedM, smoothing: 0f);
        var (rounded, _) = ArcOutset.Of([shape], MovedM, smoothing: 1f);

        Assert.True(Notches(Assert.Single(plain)) > 0, "the offset had no notch in it to round");
        Assert.Equal(0, Notches(Assert.Single(rounded)));
    }

    /// <summary>
    /// <b>And a shape with no notch in it is not smoothed at all.</b> Every corner of a square turns away
    /// from it, so its offset is already the roundest line there is at that distance and <b>the figure has
    /// nothing it is allowed to do</b> — the same answer at every share of it.
    /// </summary>
    [Fact]
    public void AndAShapeWithNoNotchIsNotSmoothedAtAll()
    {
        var (plain, _) = ArcOutset.Of([Square(SideM)], MovedM, smoothing: 0f);
        var (rounded, _) = ArcOutset.Of([Square(SideM)], MovedM, smoothing: 1f);

        Assert.Equal(
            Spline.TotalLengthM(Assert.Single(plain)),
            Spline.TotalLengthM(Assert.Single(rounded)),
            ToleranceM);
    }

    /// <summary>How many joins of a ring turn in on the shape it was moved off, each of them a notch a fold cut left.</summary>
    static int Notches(ArcSeg[] ring)
    {
        var count = 0;
        for (var at = 0; at < ring.Length; at++)
        {
            if (TurnRad(ring[at], ring[(at + 1) % ring.Length]) < -JoinDeg * MathF.PI / 180f) count++;
        }

        return count;
    }

    /// <summary>What a join may turn by and still be the arithmetic rather than a notch: a tenth of a degree.</summary>
    const float JoinDeg = 0.1f;

    /// <summary>How far one piece turns to take up the next, negative to the walker's left — which with the ground on the right is in on the shape.</summary>
    static float TurnRad(in ArcSeg from, in ArcSeg onto)
    {
        var offRad = onto.HeadingRad - from.HeadingAtRad(from.LengthM);
        return MathF.Atan2(MathF.Sin(offRad), MathF.Cos(offRad));
    }

    /// <summary>And of the square moved the other way, which is the same side with its two corners cut off it.</summary>
    const float MovedInSideM = SideM - (2f * MovedM);

    /// <summary>
    /// <b>A shape with no corner is the one piece it was, moved</b>: a circle comes back as a circle the
    /// distance wider, at its own radius rather than at a radius sampled off anything.
    /// </summary>
    [Fact]
    public void ACircleMovesOutToACircleTheDistanceWider()
    {
        const float RadiusM = 25f;

        var (rings, _) = ArcOutset.Of([Circle(RadiusM)], MovedM, smoothing: 0.5f);

        var piece = Assert.Single(Assert.Single(rings));
        Assert.Equal(1f / (RadiusM + MovedM), piece.Curvature, ToleranceM);
        Assert.Equal(2f * MathF.PI * (RadiusM + MovedM), piece.LengthM, ToleranceM);
    }

    /// <summary>
    /// <b>A feature narrower than twice the distance is not in the answer at all</b>: a square with a slot
    /// cut into it, moved out far enough to fill the slot, is the moved square exactly. <b>Every point of
    /// the slot stands within half its own width of a wall</b>, so the moved shape covers it — and the two
    /// walls' moved lines, which cross each other down the whole length of the slot, are a fold and not a
    /// boundary.
    /// </summary>
    /// <remarks>
    /// <b>It is the test the construction exists for.</b> Moved piece by piece and joined at the corners
    /// alone, the answer keeps both walls and the slot comes back as a bow-tie twice as long as it is —
    /// which is a picture of where the lines went rather than of the shape they bound.
    /// <b>Narrower than the move and not exactly as wide as it</b>, which is the one width at which the two
    /// walls' moved lines lie on one another rather than crossing, and a pair of lines that never cross is
    /// a pair the cut has no place to cut.
    /// </remarks>
    /// <remarks>
    /// <b>What the mouth of the slot leaves is not the straight across it but the scallop between its two
    /// jaws</b>, and that is the answer rather than a fault in it: the nearest ground to a place below the
    /// middle of the mouth is one of the two corners the mouth is, so the line there is the arc about a
    /// corner like the line at any other corner. It is <c>2·d·asin(w/2d)</c> of arc where a straight would
    /// have been <c>w</c>, and <b>a mitre hid it</b> — two straights leaving each jaw cross under the mouth
    /// and cut each other off at exactly the straight the scallop replaces.
    /// </remarks>
    [Fact]
    public void ASlotNarrowerThanTheMoveIsGone()
    {
        const float SlotM = MovedM * 0.8f;

        var (rings, loose) = ArcOutset.Of(
            [SlottedSquare(SideM, SlotM, deepM: SideM * 0.5f)], MovedM, smoothing: 0f);

        Assert.Empty(loose);
        Assert.Equal(
            (4f * SideM) + (2f * MathF.PI * MovedM) - SlotM
                + (2f * MovedM * MathF.Asin(SlotM / (2f * MovedM))),
            Spline.TotalLengthM(Assert.Single(rings)),
            ToleranceM);
    }

    /// <summary>
    /// <b>A shape smaller than the distance it is moved in comes back as nothing.</b> Every place on a
    /// square inset past half its own side is nearer to a wall than the distance, so no stretch of the moved
    /// line is on the answer and there is no ring to hand back.
    /// </summary>
    [Fact]
    public void AShapeMovedInPastItsOwnSizeCollapses()
    {
        var (rings, loose) = ArcOutset.Of([Square(SideM)], -SideM * 0.75f, smoothing: 0f);

        Assert.Empty(rings);
        Assert.Empty(loose);
    }

    /// <summary>
    /// <b>Two shapes nearer than twice the distance come back as one</b>, and the two arcs that ran into
    /// each other between them are not in it: the whole set is moved at once, so the count that goes in has
    /// nothing to do with the count that comes out.
    /// </summary>
    [Fact]
    public void TwoShapesThatRunTogetherComeBackAsOne()
    {
        const float ApartM = MovedM;

        var (rings, loose) = ArcOutset.Of(
            [Square(SideM), Square(SideM, new Vector2(SideM + ApartM, 0f))], MovedM, smoothing: 0f);

        Assert.Empty(loose);
        Assert.Single(rings);
    }

    /// <summary>
    /// <b>And two of them that are one piece each come back as one too.</b> A ring the shape turns all the
    /// way round without a corner is written as a single piece, and a single piece is the one thing a
    /// crossing has to be placed on <em>twice</em> — once going and once coming back.
    /// </summary>
    /// <remarks>
    /// <b>It is the shape a town has most of.</b> Whatever the ground encloses that nothing else touches —
    /// an island a movement curls round, a plot a road passes both sides of — the merge hands back as one
    /// piece, and two of them within twice the distance is a pair of full circles crossing. Drawn from the
    /// moved lines alone that is two rings through one another, which is the bow-tie this construction
    /// exists to delete.
    /// </remarks>
    [Fact]
    public void AndTwoOfThemWrittenAsOnePieceEachDoToo()
    {
        const float RadiusM = 6f;

        var (rings, loose) = ArcOutset.Of(
            [Circle(RadiusM), Circle(RadiusM, new Vector2(2f * RadiusM + MovedM, 0f))], MovedM, smoothing: 0f);

        Assert.Empty(loose);
        Assert.Single(rings);
    }

    /// <summary>
    /// <b>Nothing in a smoothed answer turns further than the corner it fills.</b> A corner of an offset
    /// turns half a circle at the very most — that is the round of a needle, where the shape doubles back on
    /// itself — so a piece sweeping further than that is an arc nothing asked for.
    /// </summary>
    /// <remarks>
    /// <b>The shape is a square with one shallow kink in a side, and the kink is the whole of it.</b> Where
    /// the ring closes by less than the arithmetic can tell, neither moved piece is cut and the second
    /// starts a few centimetres behind where the first stopped — and the smoothing, asked to fill a corner
    /// of half a degree across a gap of five, <b>can only get from one to the other by going all the way
    /// round</b>. A shipped city's boundary came back with twenty-five metres of arc at a radius of four
    /// sitting in the middle of a straight, drawn as a ring with nothing under it.
    /// </remarks>
    [Theory]
    [InlineData(0.2f)]
    [InlineData(1f)]
    public void SmoothingPutsNoArcWhereTheRingMerelyDoublesBack(float smoothing)
    {
        var (rings, loose) = ArcOutset.Of([KinkedSquare(SideM)], MovedM, smoothing);

        Assert.Empty(loose);
        foreach (var piece in Assert.Single(rings))
        {
            Assert.True(
                MathF.Abs(piece.LengthM * piece.Curvature) <= MathF.PI,
                $"a piece of {piece.LengthM:F1} m turns "
                + $"{MathF.Abs(piece.LengthM * piece.Curvature) * 180f / MathF.PI:F0} deg "
                + $"at {piece.StartM.X:F1}, {piece.StartM.Y:F1}");
        }
    }

    /// <summary>
    /// A square walked with its own ground on the walker's right, which is the hand every ring the merge
    /// hands over keeps it on (<see cref="BandShell.Chains"/>) and so the hand this construction reads
    /// "outward" off.
    /// </summary>
    static ArcSeg[] Square(float sideM) => Square(sideM, Vector2.Zero);

    static ArcSeg[] Square(float sideM, Vector2 atM) =>
    [
        new ArcSeg(atM, 0f, sideM, 0f),
        new ArcSeg(atM + new Vector2(sideM, 0f), MathF.PI * 0.5f, sideM, 0f),
        new ArcSeg(atM + new Vector2(sideM, sideM), MathF.PI, sideM, 0f),
        new ArcSeg(atM + new Vector2(0f, sideM), MathF.PI * -0.5f, sideM, 0f),
    ];

    /// <summary>
    /// The same square with a slot of <paramref name="slotM"/> across and <paramref name="deepM"/> deep cut
    /// into the middle of its first side, walked the same way round — so the slot's two walls and its end
    /// are ground on the walker's right like everything else.
    /// </summary>
    static ArcSeg[] SlottedSquare(float sideM, float slotM, float deepM)
    {
        var mouthM = (sideM - slotM) * 0.5f;
        return
        [
            new ArcSeg(Vector2.Zero, 0f, mouthM, 0f),
            new ArcSeg(new Vector2(mouthM, 0f), MathF.PI * 0.5f, deepM, 0f),
            new ArcSeg(new Vector2(mouthM, deepM), 0f, slotM, 0f),
            new ArcSeg(new Vector2(mouthM + slotM, deepM), MathF.PI * -0.5f, deepM, 0f),
            new ArcSeg(new Vector2(mouthM + slotM, 0f), 0f, mouthM, 0f),
            new ArcSeg(new Vector2(sideM, 0f), MathF.PI * 0.5f, sideM, 0f),
            new ArcSeg(new Vector2(sideM, sideM), MathF.PI, sideM, 0f),
            new ArcSeg(new Vector2(0f, sideM), MathF.PI * -0.5f, sideM, 0f),
        ];
    }

    /// <summary>
    /// The same square with its first side laid as three straights over a kink of half a degree — <b>a
    /// corner that closes by less than the moved line can be cut at</b>, which is the shape a merged
    /// boundary has thousands of and a written-down one has none.
    /// </summary>
    static ArcSeg[] KinkedSquare(float sideM)
    {
        var dipM = new Vector2(sideM * 0.625f, KinkM);
        return
        [
            Straight(Vector2.Zero, new Vector2(sideM * 0.375f, 0f)),
            Straight(new Vector2(sideM * 0.375f, 0f), dipM),
            Straight(dipM, new Vector2(sideM, 0f)),
            new ArcSeg(new Vector2(sideM, 0f), MathF.PI * 0.5f, sideM, 0f),
            new ArcSeg(new Vector2(sideM, sideM), MathF.PI, sideM, 0f),
            new ArcSeg(new Vector2(0f, sideM), MathF.PI * -0.5f, sideM, 0f),
        ];
    }

    /// <summary>
    /// How far the kinked side dips: a tenth of a metre over fifteen, which is <b>half a degree</b> — a fold
    /// a quarter of a millimetre deep, under what two computations of one distance agree to.
    /// </summary>
    const float KinkM = -0.1f;

    static ArcSeg Straight(Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        return new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), runM.Length(), 0f);
    }

    /// <summary>The same the other way of saying it: one piece that turns all the way round, ground on the right throughout.</summary>
    static ArcSeg[] Circle(float radiusM) => Circle(radiusM, Vector2.Zero);

    static ArcSeg[] Circle(float radiusM, Vector2 atM) =>
        [new ArcSeg(atM, 0f, 2f * MathF.PI * radiusM, 1f / radiusM)];
}
