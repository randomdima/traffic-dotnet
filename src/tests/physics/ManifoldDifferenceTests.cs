using System.Numerics;
using Box2D.NET;
using TrafficSimulation.World.Physics;
using Xunit;
using static Box2D.NET.B2Geometries;
using static Box2D.NET.B2Manifolds;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// This engine's narrow phase against the incumbent's, over randomised poses. <b>Circle-circle and
/// circle-box have closed forms; box-box does not, and it is where a solver is wrong subtly</b> — the
/// classic failure is a reference face that alternates between ticks, which reads as a queue that
/// shivers and as nothing at all in a test written against intuition.
/// </summary>
/// <remarks>
/// <para>
/// What is compared is <b>what the solver and the damage arbiter actually read</b>: whether the pair is
/// touching at all, which way the normal points, and how deep the deepest point is. The number of points
/// and where they sit along a face are a manifold's own business and two correct implementations may
/// differ on them.
/// </para>
/// <para>
/// The reference is a test-only package reference and reaches no shipped assembly. Rotations are handed
/// over as the pair of numbers rather than as an angle for the reason
/// <see cref="CastDifferenceTests.TheReferenceRoundsAnAngleAndThisEngineDoesNot"/> records.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class ManifoldDifferenceTests
{
    /// <summary>Box2D v3's speculative distance is four linear slops, and so is this engine's — see SimConfig.SolverSpeculativeM.</summary>
    const float MarginM = 0.02f;

    const float ToleranceM = 1e-3f;

    /// <summary>
    /// The regime a town is actually in: two cars just about to touch, just touching, or a hand's depth
    /// into each other. The pair is laid along a random direction at a drawn depth rather than at a
    /// random offset, because a random offset over boxes this size is mostly <em>deep</em> overlap, which
    /// is not what the town does and not where a manifold has to be right to the millimetre.
    /// </summary>
    /// <remarks>
    /// <b>Run twice, square-cornered and rounded</b> (CAR-12b): the reference carries a radius on a
    /// polygon exactly as this engine now carries one on a box, and half a metre of it is over what the
    /// most rounded car in the shipped fleet asks for. A rounded pair that only agreed where the radius
    /// was zero would be a shape nothing had checked.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    public void TwoBoxesJustMeetingAgreeWithTheReference(float cornerM)
    {
        var draw = new Random(20260819);
        var rounding = Rounding(cornerM);
        var agreed = 0;
        var touching = 0;

        for (var pose = 0; pose < 20_000; pose++)
        {
            var cornerA = rounding();
            var cornerB = rounding();
            var halfA = new Vector2(0.5f + (float)draw.NextDouble() * 2f, 0.5f + (float)draw.NextDouble() * 2f);
            var halfB = new Vector2(0.5f + (float)draw.NextDouble() * 2f, 0.5f + (float)draw.NextDouble() * 2f);
            var rotationA = Shape.Rotation((float)draw.NextDouble() * MathF.Tau - MathF.PI);
            var rotationB = Shape.Rotation((float)draw.NextDouble() * MathF.Tau - MathF.PI);

            var towards = (float)draw.NextDouble() * MathF.Tau - MathF.PI;
            var direction = new Vector2(MathF.Cos(towards), MathF.Sin(towards));

            // How far each box reaches along the line between them, so the pair is laid at a drawn depth
            // rather than at a drawn distance: a distance drawn off the circumradius would leave nine in
            // ten of them apart, and a run of poses that never met would agree about nothing.
            var apartM = Reach(direction, rotationA, halfA) + cornerA
                         + Reach(direction, rotationB, halfB) + cornerB
                         - ((float)draw.NextDouble() * (DeepestM + MarginM) - MarginM);
            var betweenM = direction * apartM;

            var met = Shape.Collide(
                Vector2.Zero, rotationA, halfA, cornerA,
                betweenM, rotationB, halfB, cornerB, MarginM, out var manifold);

            var boxA = b2MakeRoundedBox(halfA.X, halfA.Y, cornerA);
            var boxB = b2MakeRoundedBox(halfB.X, halfB.Y, cornerB);
            var reference = b2CollidePolygons(
                ref boxA, At(Vector2.Zero, rotationA), ref boxB, At(betweenM, rotationB));

            Same(pose, met, manifold, reference, ref agreed, ref touching);
        }

        Assert.True(touching > 5_000, $"only {touching} of 20 000 box pairs touched, so the agreement says little");
        Assert.Equal(20_000, agreed);
    }

    /// <summary>
    /// A corner radius up to <paramref name="cornerM"/>, drawn from <b>a stream of its own</b> so that the
    /// square-cornered run is the same twenty thousand poses it was before a radius existed. Sharing the
    /// pose stream would have made the two rows of every theory below two different experiments.
    /// </summary>
    static Func<float> Rounding(float cornerM)
    {
        var draw = new Random(4271);
        return () => cornerM * (float)draw.NextDouble();
    }

    /// <summary>How far a box reaches from its own centre along one direction.</summary>
    static float Reach(Vector2 direction, Vector2 rotation, Vector2 half) =>
        half.X * MathF.Abs(Vector2.Dot(direction, rotation))
        + half.Y * MathF.Abs(Vector2.Dot(direction, Shape.LeftPerpendicular(rotation)));

    /// <summary>
    /// And over the whole range, including depths no town produces: <b>the two implementations always
    /// agree on whether the pair is touching</b>, which is the half of the answer the begin-touch report
    /// and therefore the damage arbiter rest on.
    /// </summary>
    /// <remarks>
    /// The normal is deliberately not compared here. Past about a quarter of a metre of overlap the two
    /// candidate faces are routinely within a fraction of a millimetre of each other in depth, and which
    /// of the two a separating-axis test picks at that point is a coin the reference tosses as well —
    /// both answers are separating axes and neither is the wrong one. It is <b>the shallow regime that
    /// has to be right to the millimetre</b>, and the test above is where that is asserted.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    public void TwoBoxesAtAnyDepthAgreeWithTheReferenceOnWhetherTheyTouch(float cornerM)
    {
        var draw = new Random(1959);
        var rounding = Rounding(cornerM);
        var touching = 0;

        for (var pose = 0; pose < 20_000; pose++)
        {
            var cornerA = rounding();
            var cornerB = rounding();
            var halfA = new Vector2(0.5f + (float)draw.NextDouble() * 2f, 0.5f + (float)draw.NextDouble() * 2f);
            var halfB = new Vector2(0.5f + (float)draw.NextDouble() * 2f, 0.5f + (float)draw.NextDouble() * 2f);
            var rotationA = Shape.Rotation((float)draw.NextDouble() * MathF.Tau - MathF.PI);
            var rotationB = Shape.Rotation((float)draw.NextDouble() * MathF.Tau - MathF.PI);
            var betweenM = new Vector2((float)draw.NextDouble() * 8f - 4f, (float)draw.NextDouble() * 8f - 4f);

            var met = Shape.Collide(
                Vector2.Zero, rotationA, halfA, cornerA,
                betweenM, rotationB, halfB, cornerB, MarginM, out _);

            var boxA = b2MakeRoundedBox(halfA.X, halfA.Y, cornerA);
            var boxB = b2MakeRoundedBox(halfB.X, halfB.Y, cornerB);
            var reference = b2CollidePolygons(
                ref boxA, At(Vector2.Zero, rotationA), ref boxB, At(betweenM, rotationB));

            Assert.True(
                met == reference.pointCount > 0,
                $"pose {pose}: this engine {(met ? "met" : "missed")}, the reference found {reference.pointCount} points");
            if (met) touching++;
        }

        Assert.True(touching > 2_000, $"only {touching} of 20 000 box pairs touched, so the agreement says little");
    }

    /// <summary>
    /// How deep the just-meeting poses are allowed to get. A soak of the shipped towns peaks at 189 mm,
    /// so a quarter of a metre covers what the town does with room over.
    /// </summary>
    const float DeepestM = 0.25f;

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    public void ABoxAndADiscMeetWhereTheReferenceSaysTheyMeet(float cornerM)
    {
        var draw = new Random(1955);
        var rounding = Rounding(cornerM);
        var agreed = 0;
        var touching = 0;

        for (var pose = 0; pose < 20_000; pose++)
        {
            var corner = rounding();
            var half = new Vector2(0.5f + (float)draw.NextDouble() * 2f, 0.5f + (float)draw.NextDouble() * 2f);
            var radiusM = 0.2f + (float)draw.NextDouble() * 1.5f;
            var rotation = Shape.Rotation((float)draw.NextDouble() * MathF.Tau - MathF.PI);
            var betweenM = new Vector2((float)draw.NextDouble() * 6f - 3f, (float)draw.NextDouble() * 6f - 3f);

            // The box first, so the normal runs box to disc in both — b2CollidePolygonAndCircle's does.
            var met = Shape.Collide(
                Vector2.Zero, rotation, half, corner,
                betweenM, Shape.Rotation(0f), Vector2.Zero, radiusM, MarginM, out var manifold);

            var box = b2MakeRoundedBox(half.X, half.Y, corner);
            var circle = new B2Circle(new B2Vec2(0f, 0f), radiusM);
            var reference = b2CollidePolygonAndCircle(
                ref box, At(Vector2.Zero, rotation), in circle, At(betweenM, Shape.Rotation(0f)));

            Same(pose, met, manifold, reference, ref agreed, ref touching);
        }

        Assert.True(touching > 2_000, $"only {touching} of 20 000 box-disc pairs touched, so the agreement says little");
        Assert.Equal(20_000, agreed);
    }

    [Fact]
    public void TwoDiscsMeetWhereTheReferenceSaysTheyMeet()
    {
        var draw = new Random(1972);
        var agreed = 0;
        var touching = 0;

        for (var pose = 0; pose < 20_000; pose++)
        {
            var first = 0.2f + (float)draw.NextDouble() * 1.5f;
            var second = 0.2f + (float)draw.NextDouble() * 1.5f;
            var betweenM = new Vector2((float)draw.NextDouble() * 5f - 2.5f, (float)draw.NextDouble() * 5f - 2.5f);

            var met = Shape.Collide(
                Vector2.Zero, Shape.Rotation(0f), Vector2.Zero, first,
                betweenM, Shape.Rotation(0f), Vector2.Zero, second, MarginM, out var manifold);

            var circleA = new B2Circle(new B2Vec2(0f, 0f), first);
            var circleB = new B2Circle(new B2Vec2(0f, 0f), second);
            var reference = b2CollideCircles(
                in circleA, At(Vector2.Zero, Shape.Rotation(0f)), in circleB, At(betweenM, Shape.Rotation(0f)));

            Same(pose, met, manifold, reference, ref agreed, ref touching);
        }

        Assert.True(touching > 2_000, $"only {touching} of 20 000 disc pairs touched, so the agreement says little");
        Assert.Equal(20_000, agreed);
    }

    /// <summary>
    /// SOL-1: <b>the two closed forms are shortcuts and not shapes.</b> A disc is a rounded box with no
    /// core, so the general path answers a disc pair and a disc-against-box pair on its own — and where it
    /// does not agree with the shortcut that is actually taken, one of the two is wrong and neither the
    /// reference tests above nor a town would say which.
    /// </summary>
    /// <remarks>
    /// The core given to the general path is a hair rather than nothing, because <em>nothing</em> is what
    /// selects the shortcut. That hair makes the general shape a tenth of a millimetre bigger, so a pair
    /// sitting within a hair of the speculative distance may be met by one and missed by the other; those
    /// are counted and left, and everything decided by more than that has to agree exactly.
    /// </remarks>
    [Fact]
    public void TheDiscShortcutsAgreeWithTheGeneralShape()
    {
        var draw = new Random(2026);
        var hair = new Vector2(1e-4f);
        var checkedPairs = 0;
        var onTheLine = 0;

        for (var pose = 0; pose < 20_000; pose++)
        {
            var discA = 0.2f + (float)draw.NextDouble() * 1.5f;
            var discB = 0.2f + (float)draw.NextDouble() * 1.5f;
            var half = new Vector2(0.5f + (float)draw.NextDouble() * 2f, 0.5f + (float)draw.NextDouble() * 2f);
            var corner = (float)draw.NextDouble() * 0.5f;
            var rotation = Shape.Rotation((float)draw.NextDouble() * MathF.Tau - MathF.PI);
            var betweenM = new Vector2((float)draw.NextDouble() * 6f - 3f, (float)draw.NextDouble() * 6f - 3f);

            // Two discs, then a disc against a rounded box, each way round the shortcut can be entered.
            checkedPairs += Alike(
                pose, Shape.Collide(Vector2.Zero, rotation, Vector2.Zero, discA, betweenM, rotation, Vector2.Zero, discB, MarginM, out var shortcut),
                shortcut,
                Shape.Collide(Vector2.Zero, rotation, hair, discA, betweenM, rotation, hair, discB, MarginM, out var general),
                general, ref onTheLine);

            checkedPairs += Alike(
                pose, Shape.Collide(Vector2.Zero, rotation, Vector2.Zero, discA, betweenM, rotation, half, corner, MarginM, out var shortcutBox),
                shortcutBox,
                Shape.Collide(Vector2.Zero, rotation, hair, discA, betweenM, rotation, half, corner, MarginM, out var generalBox),
                generalBox, ref onTheLine);
        }

        Assert.Equal(40_000, checkedPairs + onTheLine);
        Assert.True(onTheLine < 40, $"{onTheLine} of 40 000 pairs sat on the speculative distance, which is too many to be the hair");
    }

    /// <summary>
    /// The same answer, or a pair standing on the speculative distance itself — where the hair of core the
    /// general shape carries is enough to decide it, and which of the two answers is right is not a
    /// question about the shapes.
    /// </summary>
    static int Alike(
        int pose, bool metShortcut, in Manifold shortcut, bool metGeneral, in Manifold general, ref int onTheLine)
    {
        if (metShortcut != metGeneral)
        {
            var separationM = metShortcut ? Deepest(shortcut) : Deepest(general);
            Assert.True(
                MathF.Abs(separationM - MarginM) < 1e-3f,
                $"pose {pose}: the shortcut {(metShortcut ? "met" : "missed")} at {separationM:F5} m and the " +
                "general shape did not, nowhere near the speculative distance");

            onTheLine++;
            return 0;
        }

        if (!metShortcut) return 1;

        Assert.Equal(Deepest(general), Deepest(shortcut), 2e-3f);
        Assert.True(
            Vector2.Dot(shortcut.Normal, general.Normal) > 0.999f,
            $"pose {pose}: the shortcut's normal {shortcut.Normal} against the general shape's {general.Normal}");

        return 1;
    }

    /// <summary>
    /// Whether the two manifolds say the same thing about the pair: touching or not, the same way round,
    /// and the same deepest point.
    /// </summary>
    static void Same(int pose, bool met, in Manifold manifold, in B2Manifold reference, ref int agreed, ref int touching)
    {
        Assert.True(
            met == reference.pointCount > 0,
            $"pose {pose}: this engine {(met ? "met" : "missed")}, the reference found {reference.pointCount} points");

        if (!met)
        {
            agreed++;
            return;
        }

        touching++;
        var theirNormal = new Vector2(reference.normal.X, reference.normal.Y);
        Assert.True(
            Vector2.Dot(manifold.Normal, theirNormal) > 0.999f,
            $"pose {pose}: normal {manifold.Normal} at {Deepest(manifold):F5} m against the reference's " +
            $"{theirNormal} at {Deepest(reference):F5} m");

        Assert.Equal(Deepest(reference), Deepest(manifold), ToleranceM);
        agreed++;
    }

    static float Deepest(in Manifold manifold) =>
        manifold.PointCount > 1 ? MathF.Min(manifold.Separation0, manifold.Separation1) : manifold.Separation0;

    static float Deepest(in B2Manifold manifold)
    {
        var deepest = float.MaxValue;
        for (var point = 0; point < manifold.pointCount; point++)
        {
            deepest = MathF.Min(deepest, manifold.points[point].separation);
        }

        return deepest;
    }

    static B2Transform At(Vector2 positionM, Vector2 rotation) =>
        new(new B2Vec2(positionM.X, positionM.Y), new B2Rot(rotation.X, rotation.Y));
}
