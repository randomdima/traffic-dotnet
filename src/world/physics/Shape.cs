
using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// What two shapes are doing to each other: a normal pointing from the first towards the second, and
/// the one or two places they meet along it, each with its own separation — negative where they are
/// inside one another.
/// </summary>
/// <remarks>
/// Two points and never more, because two convex shapes in a plane touch along a segment at most. A
/// blittable struct with its points as fields rather than an array, so a manifold is never a heap
/// object and never an indirection.
/// </remarks>
internal struct Manifold
{
    public Vector2 Normal;
    public Vector2 Point0;
    public Vector2 Point1;
    public float Separation0;
    public float Separation1;
    public int PointCount;
}

/// <summary>
/// A shape's arithmetic: where it reaches, what a ray does to it, and what its manifold against another
/// shape is. <b>Nothing here holds state</b> — a shape is three numbers passed in, which is what lets the
/// body table stay a structure of arrays with no object per shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>SOL-1 — there is one shape, and it is an oriented box with its corners rounded.</b> A shape is a
/// <em>core</em> rectangle of half-extents <c>extentM</c> grown by a disc of <c>cornerRadiusM</c>, so it
/// reaches <c>extentM + cornerRadiusM</c> along each of its own axes. A building part is a core with no
/// radius; a person and a prop are a radius with <b>no core</b>, which is a circle and is not a second
/// kind of thing. Everything below is written once for the general shape, and the closed forms the
/// degenerate cases take are optimisations of it, held to its own answer by the difference tests.
/// </para>
/// <para>
/// A rotation is a <c>Vector2</c> of <c>(cos, sin)</c> and never an angle: an angle would be two
/// transcendentals per use where a pair of floats is two multiplies.
/// </para>
/// </remarks>
internal static partial class Shape
{
    /// <summary>Turn a vector out of a body's frame into the world's.</summary>
    public static Vector2 Rotate(Vector2 rotation, Vector2 v) =>
        new(rotation.X * v.X - rotation.Y * v.Y, rotation.Y * v.X + rotation.X * v.Y);

    /// <summary>Turn a world vector into a body's frame.</summary>
    public static Vector2 InverseRotate(Vector2 rotation, Vector2 v) =>
        new(rotation.X * v.X + rotation.Y * v.Y, rotation.X * v.Y - rotation.Y * v.X);

    /// <summary>The scalar cross product, which in a plane is the only one there is.</summary>
    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>A yaw rate crossed with an arm: what a spinning body's surface is doing at that point.</summary>
    public static Vector2 CrossYaw(float yawRate, Vector2 arm) => new(-yawRate * arm.Y, yawRate * arm.X);

    public static Vector2 LeftPerpendicular(Vector2 v) => new(-v.Y, v.X);

    /// <summary>A heading as the pair a rotation is applied with. The kernel's, so the solver and the geometry turn a body the same way.</summary>
    public static Vector2 Rotation(float headingRad) => Heading.Unit(headingRad);

    /// <summary>Whether a shape has a core at all, or is the bare disc a person and a prop are.</summary>
    public static bool IsPoint(Vector2 extentM) => extentM.X <= 0f && extentM.Y <= 0f;

    /// <summary>The half-extents of a shape's axis-aligned bounding box, at the rotation it is standing at.</summary>
    public static Vector2 HalfBoundsM(Vector2 rotation, Vector2 extentM, float cornerRadiusM)
    {
        var cos = MathF.Abs(rotation.X);
        var sin = MathF.Abs(rotation.Y);
        return new Vector2(
            extentM.X * cos + extentM.Y * sin + cornerRadiusM,
            extentM.X * sin + extentM.Y * cos + cornerRadiusM);
    }

    /// <summary>
    /// The rotational inertia of a shape about its own centre, per kilogram of it. A body locked against
    /// rotation is given none by its body rather than by its shape.
    /// </summary>
    /// <remarks>
    /// A rounded box is given the inertia of the box it was rounded out of — its outermost half-extents
    /// and not its core's. Rounding a corner takes a few per cent of the area off the place that
    /// contributes most to the second moment, and answering that honestly would move every car's yaw
    /// response the day the radius arrived; the shape a car's handling was tuned against is the one this
    /// keeps. <b>The coreless shape is the one body kind this is wrong for</b> — a disc's own figure is
    /// <c>r²/2</c> and this says <c>2r²/3</c> — and it is unreachable: every coreless body in this town is
    /// a walker or a prop, and both are rotation-locked.
    /// </remarks>
    public static float InertiaPerKg(Vector2 extentM, float cornerRadiusM)
    {
        var alongM = extentM.X + cornerRadiusM;
        var acrossM = extentM.Y + cornerRadiusM;
        return (alongM * alongM + acrossM * acrossM) / 3f;
    }

    /// <summary>
    /// Where two shapes meet, or false where they are further apart than <paramref name="marginM"/>.
    /// The normal points from the first shape towards the second.
    /// </summary>
    /// <remarks>
    /// <b>The three branches are one question asked three ways, not three shapes.</b>
    /// <see cref="RoundedBoxes"/> answers every pair correctly on its own — a coreless shape falls out of
    /// its arithmetic as the disc it is — and the two closed forms are there because the pairs they cover
    /// are most of a town's narrow phase: a walker against a walker, and a car against ninety thousand
    /// props. Anything changed in one of them is changed in all three, and the difference tests hold the
    /// two shortcuts to what the general path says.
    /// </remarks>
    /// <param name="marginM">
    /// How far apart a pair may still be and be given a manifold — the speculative distance. A contact
    /// that exists slightly before the shapes touch is what lets the solver stop an approach in the tick
    /// it would otherwise have crossed, and why nothing here has to sweep.
    /// </param>
    public static bool Collide(
        Vector2 positionA, Vector2 rotationA, Vector2 extentA, float cornerA,
        Vector2 positionB, Vector2 rotationB, Vector2 extentB, float cornerB,
        float marginM, out Manifold manifold)
    {
        var pointA = IsPoint(extentA);
        var pointB = IsPoint(extentB);

        if (pointA && pointB) return TwoDiscs(positionA, cornerA, positionB, cornerB, marginM, out manifold);

        if (pointA) return DiscAndBox(positionA, cornerA, positionB, rotationB, extentB, cornerB, marginM, out manifold);

        if (pointB)
        {
            // The disc is asked about first whichever way round the pair arrived, and the normal is
            // turned back to point from A to B.
            var met = DiscAndBox(positionB, cornerB, positionA, rotationA, extentA, cornerA, marginM, out manifold);
            manifold.Normal = -manifold.Normal;
            return met;
        }

        return RoundedBoxes(positionA, rotationA, extentA, cornerA, positionB, rotationB, extentB, cornerB, marginM, out manifold);
    }

    static bool TwoDiscs(Vector2 centreA, float radiusA, Vector2 centreB, float radiusB, float marginM, out Manifold manifold)
    {
        manifold = default;

        var between = centreB - centreA;
        var apartSquared = between.LengthSquared();
        var reachM = radiusA + radiusB + marginM;
        if (apartSquared > reachM * reachM) return false;

        var apartM = MathF.Sqrt(apartSquared);

        // Two circles exactly on top of each other have no direction of their own; any one will do, and
        // the solver pushes them apart along it.
        manifold.Normal = apartM > 1e-6f ? between / apartM : new Vector2(1f, 0f);
        manifold.Separation0 = apartM - radiusA - radiusB;
        manifold.Point0 = centreA + manifold.Normal * (radiusA + manifold.Separation0 * 0.5f);
        manifold.PointCount = 1;
        return true;
    }

    /// <summary>The normal points from the disc towards the box, which is A towards B for the way this is called.</summary>
    /// <remarks>
    /// The box's corner radius is spent by asking the question of its <em>core</em> with a disc that much
    /// fatter — the Minkowski sum is the same either way round — and then walking the reported point back
    /// onto the box's real surface, which is <paramref name="cornerRadiusM"/> outside the core it was
    /// found against.
    /// </remarks>
    static bool DiscAndBox(
        Vector2 centre, float discRadiusM, Vector2 boxCentre, Vector2 boxRotation, Vector2 half,
        float cornerRadiusM, float marginM, out Manifold manifold)
    {
        manifold = default;

        var radiusM = discRadiusM + cornerRadiusM;
        var local = InverseRotate(boxRotation, centre - boxCentre);
        var nearest = new Vector2(
            Math.Clamp(local.X, -half.X, half.X),
            Math.Clamp(local.Y, -half.Y, half.Y));

        var outward = local - nearest;
        var outwardSquared = outward.LengthSquared();
        if (outwardSquared > 1e-12f)
        {
            var apartM = MathF.Sqrt(outwardSquared);
            var separationM = apartM - radiusM;
            if (separationM > marginM) return false;

            manifold.Normal = -Rotate(boxRotation, outward / apartM);
            manifold.Separation0 = separationM;
            manifold.Point0 = ((boxCentre + Rotate(boxRotation, nearest) + centre + manifold.Normal * radiusM) * 0.5f)
                              - (manifold.Normal * cornerRadiusM);
            manifold.PointCount = 1;
            return true;
        }

        // The centre is inside the core, so there is no direction to leave by — only the nearest way out.
        // Whichever face that is, the depth is the whole of the disc plus how far in the centre is.
        var alongX = half.X - MathF.Abs(local.X);
        var alongY = half.Y - MathF.Abs(local.Y);
        var outLocal = alongX < alongY
            ? new Vector2(local.X >= 0f ? 1f : -1f, 0f)
            : new Vector2(0f, local.Y >= 0f ? 1f : -1f);

        manifold.Normal = -Rotate(boxRotation, outLocal);
        manifold.Separation0 = -(MathF.Min(alongX, alongY) + radiusM);
        manifold.Point0 = centre;
        manifold.PointCount = 1;
        return true;
    }

    /// <summary>
    /// Two oriented boxes, by the separating axis over the four face normals and then by clipping the
    /// incident face against the reference face's sides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference face is simply the deeper of the two axes, with ties going to the first box. A bias
    /// towards the first box was tried first, on the reasoning that two nearly equal separations would
    /// alternate tick to tick and read as a queue that shivers; it is wrong — a margin wide enough to
    /// settle a tie keeps the <em>wrong</em> face whenever the right one wins by less than it. What keeps
    /// a resting pair still is that the positional correction never becomes motion.
    /// </para>
    /// <para>
    /// A pair that is not yet touching is answered by where it is nearest and not by a face. Two boxes
    /// approaching corner to corner are nearest along the line joining the corners, and a face normal
    /// there is a direction nothing is closing along — which the damage arbiter would read a closing
    /// speed against.
    /// </para>
    /// <para>
    /// <b>The two corner radii are one number to everything below</b>: the whole test runs against the
    /// pair's cores at a margin that much wider, and the two radii come off the separations and move the
    /// points at the end. A rounded box against a rounded box is still flat face against flat face
    /// wherever the two are square to each other, which is what keeps a queue resting on two points.
    /// </para>
    /// </remarks>
    static bool RoundedBoxes(
        Vector2 centreA, Vector2 rotationA, Vector2 halfA, float cornerA,
        Vector2 centreB, Vector2 rotationB, Vector2 halfB, float cornerB,
        float marginM, out Manifold manifold)
    {
        manifold = default;

        var coreMarginM = marginM + cornerA + cornerB;

        var separationA = MaxSeparation(centreA, rotationA, halfA, centreB, rotationB, halfB, out var normalA, out var axisA);
        if (separationA > coreMarginM) return false;

        var separationB = MaxSeparation(centreB, rotationB, halfB, centreA, rotationA, halfA, out var normalB, out var axisB);
        if (separationB > coreMarginM) return false;

        // Not yet touching, but near enough for a speculative contact. Only once they overlap is the
        // separating axis the direction between them.
        //
        // <b>The question is asked of the cores and not of the rounded shapes.</b> What the clip below
        // works on is the two cores, so which of the two answers is the right one is the cores' own fact:
        // rounded boxes a hand's depth into each other can still stand a whole radius apart at the core,
        // and a face normal is the wrong direction for that pair however deep the overlap reads.
        if (MathF.Max(separationA, separationB) > ApartM
            && Nearest(centreA, rotationA, halfA, cornerA, centreB, rotationB, halfB, cornerB, out manifold))
        {
            // And it is here, not at the separating axis above, that the margin is finally enforced. A
            // separating axis <em>understates</em> how far apart two disjoint boxes are — corner to
            // corner it understates it by most — so the axis can only ever rule a pair out, and this is
            // the first place the real distance between them is known.
            if (manifold.Separation0 <= marginM) return true;

            manifold = default;
            return false;
        }

        var flipped = separationB > separationA;

        var referenceCentre = flipped ? centreB : centreA;
        var referenceHalf = flipped ? halfB : halfA;
        var referenceAxis = flipped ? axisB : axisA;
        var referenceCorner = flipped ? cornerB : cornerA;
        var normal = flipped ? normalB : normalA;

        var incidentCentre = flipped ? centreA : centreB;
        var incidentRotation = flipped ? rotationA : rotationB;
        var incidentHalf = flipped ? halfA : halfB;
        var incidentCorner = flipped ? cornerA : cornerB;

        // The reference face: its middle, the way it runs, and how far it runs either side of its middle.
        var referenceReachM = referenceAxis == 0 ? referenceHalf.X : referenceHalf.Y;
        var referenceRunM = referenceAxis == 0 ? referenceHalf.Y : referenceHalf.X;
        var faceCentre = referenceCentre + normal * referenceReachM;
        var along = LeftPerpendicular(normal);

        // The incident face is the one facing the reference face most squarely.
        var incidentX = incidentRotation;
        var incidentY = LeftPerpendicular(incidentRotation);
        var facingX = Vector2.Dot(normal, incidentX);
        var facingY = Vector2.Dot(normal, incidentY);

        Vector2 incidentNormal;
        float incidentReachM;
        float incidentRunM;
        if (MathF.Abs(facingX) > MathF.Abs(facingY))
        {
            incidentNormal = facingX > 0f ? -incidentX : incidentX;
            incidentReachM = incidentHalf.X;
            incidentRunM = incidentHalf.Y;
        }
        else
        {
            incidentNormal = facingY > 0f ? -incidentY : incidentY;
            incidentReachM = incidentHalf.Y;
            incidentRunM = incidentHalf.X;
        }

        var incidentAlong = LeftPerpendicular(incidentNormal);
        var incidentMiddle = incidentCentre + incidentNormal * incidentReachM;
        var first = incidentMiddle - incidentAlong * incidentRunM;
        var second = incidentMiddle + incidentAlong * incidentRunM;

        // Clipped to the reference face's own width, in the one parameter that runs along it.
        var faceAt = Vector2.Dot(faceCentre, along);
        var least = faceAt - referenceRunM;
        var most = faceAt + referenceRunM;
        var firstAt = Vector2.Dot(first, along);
        var span = Vector2.Dot(second, along) - firstAt;

        var fromFraction = 0f;
        var toFraction = 1f;
        if (MathF.Abs(span) > 1e-9f)
        {
            var atLeast = (least - firstAt) / span;
            var atMost = (most - firstAt) / span;
            fromFraction = MathF.Max(fromFraction, MathF.Min(atLeast, atMost));
            toFraction = MathF.Min(toFraction, MathF.Max(atLeast, atMost));
        }
        else if (firstAt < least || firstAt > most)
        {
            return false;
        }

        if (fromFraction > toFraction) return false;

        var edge = second - first;
        Add(ref manifold, first + edge * fromFraction, faceCentre, normal, referenceCorner, incidentCorner, marginM);
        if (toFraction - fromFraction > 1e-6f)
        {
            Add(ref manifold, first + edge * toFraction, faceCentre, normal, referenceCorner, incidentCorner, marginM);
        }

        if (manifold.PointCount == 0) return false;

        // The normal that came out of the reference box points away from it; the caller was promised one
        // pointing from A to B.
        manifold.Normal = flipped ? -normal : normal;
        return true;
    }

    /// <summary>
    /// The one place two <em>disjoint</em> boxes are nearest, and how far apart they are there. Between
    /// two convex polygons that do not overlap, the nearest place is always a corner of one against a
    /// side of the other, so walking the four corners of each against the four sides of the other finds
    /// it exactly.
    /// </summary>
    /// <remarks>
    /// Sixteen point-and-segment tests each way, and it runs only for a pair inside the speculative
    /// distance that is not yet overlapping — which in a town is a car standing a centimetre off the one
    /// in front. It returns false where the two are so nearly touching that the direction between them
    /// has no sign left in it, and the separating axis answers instead.
    /// </remarks>
    static bool Nearest(
        Vector2 centreA, Vector2 rotationA, Vector2 halfA, float cornerA,
        Vector2 centreB, Vector2 rotationB, Vector2 halfB, float cornerB,
        out Manifold manifold)
    {
        manifold = default;

        Span<Vector2> cornersA = stackalloc Vector2[4];
        Span<Vector2> cornersB = stackalloc Vector2[4];
        Corners(centreA, rotationA, halfA, cornersA);
        Corners(centreB, rotationB, halfB, cornersB);

        var apartSquared = float.MaxValue;
        var onA = Vector2.Zero;
        var onB = Vector2.Zero;
        for (var corner = 0; corner < 4; corner++)
        {
            for (var side = 0; side < 4; side++)
            {
                Keep(cornersA[corner], OnSegment(cornersB[side], cornersB[(side + 1) & 3], cornersA[corner]));
                Keep(OnSegment(cornersA[side], cornersA[(side + 1) & 3], cornersB[corner]), cornersB[corner]);
            }
        }

        var apartM = MathF.Sqrt(apartSquared);
        if (apartM < 1e-5f) return false;

        // The two cores are nearest here, so the two surfaces are nearest along the same line, one
        // radius in from each end of it.
        manifold.Normal = (onB - onA) / apartM;
        manifold.Separation0 = apartM - cornerA - cornerB;
        manifold.Point0 = ((onA + onB) * 0.5f) + (manifold.Normal * ((cornerA - cornerB) * 0.5f));
        manifold.PointCount = 1;
        return true;

        void Keep(Vector2 first, Vector2 second)
        {
            var between = Vector2.DistanceSquared(first, second);
            if (between >= apartSquared) return;

            apartSquared = between;
            onA = first;
            onB = second;
        }
    }

    static void Corners(Vector2 centreM, Vector2 rotation, Vector2 half, Span<Vector2> into)
    {
        var along = Rotate(rotation, new Vector2(half.X, 0f));
        var across = Rotate(rotation, new Vector2(0f, half.Y));
        into[0] = centreM + along + across;
        into[1] = centreM - along + across;
        into[2] = centreM - along - across;
        into[3] = centreM + along - across;
    }

    static Vector2 OnSegment(Vector2 fromM, Vector2 toM, Vector2 pointM)
    {
        var along = toM - fromM;
        var lengthSquared = along.LengthSquared();
        if (lengthSquared < 1e-12f) return fromM;

        return fromM + along * Math.Clamp(Vector2.Dot(pointM - fromM, along) / lengthSquared, 0f, 1f);
    }

    /// <summary>
    /// One clipped point, kept only where it actually reaches the reference face, and reported halfway
    /// between where the two shapes are rather than on either of them.
    /// </summary>
    /// <remarks>
    /// The point arrives on the incident <em>core</em> and is measured against the reference core's face,
    /// so both radii come off here: the gap the caller is told about is that much smaller, and the two
    /// surfaces it is halfway between stand one radius out from each core.
    /// </remarks>
    static void Add(
        ref Manifold manifold, Vector2 pointM, Vector2 faceCentre, Vector2 normal,
        float referenceCorner, float incidentCorner, float marginM)
    {
        var coreM = Vector2.Dot(pointM - faceCentre, normal);
        var separationM = coreM - referenceCorner - incidentCorner;
        if (separationM > marginM) return;

        var at = pointM - normal * ((coreM + incidentCorner - referenceCorner) * 0.5f);
        if (manifold.PointCount == 0)
        {
            manifold.Point0 = at;
            manifold.Separation0 = separationM;
        }
        else
        {
            manifold.Point1 = at;
            manifold.Separation1 = separationM;
        }

        manifold.PointCount++;
    }

    /// <summary>
    /// How far apart the two boxes are along the reference box's own two axes, taking the axis that
    /// separates them most — which is the axis a separating-axis test is looking for.
    /// </summary>
    /// <param name="normal">The reference box's outward face normal on that axis, pointing towards the other box.</param>
    /// <param name="axis">0 where it is the reference box's own x, 1 where it is its y.</param>
    static float MaxSeparation(
        Vector2 referenceCentre, Vector2 referenceRotation, Vector2 referenceHalf,
        Vector2 otherCentre, Vector2 otherRotation, Vector2 otherHalf,
        out Vector2 normal, out int axis)
    {
        var referenceX = referenceRotation;
        var referenceY = LeftPerpendicular(referenceRotation);
        var otherX = otherRotation;
        var otherY = LeftPerpendicular(otherRotation);
        var between = otherCentre - referenceCentre;

        var acrossX = Vector2.Dot(between, referenceX);
        var otherReachX = otherHalf.X * MathF.Abs(Vector2.Dot(referenceX, otherX))
                          + otherHalf.Y * MathF.Abs(Vector2.Dot(referenceX, otherY));
        var separationX = MathF.Abs(acrossX) - referenceHalf.X - otherReachX;

        var acrossY = Vector2.Dot(between, referenceY);
        var otherReachY = otherHalf.X * MathF.Abs(Vector2.Dot(referenceY, otherX))
                          + otherHalf.Y * MathF.Abs(Vector2.Dot(referenceY, otherY));
        var separationY = MathF.Abs(acrossY) - referenceHalf.Y - otherReachY;

        if (separationX >= separationY)
        {
            normal = acrossX >= 0f ? referenceX : -referenceX;
            axis = 0;
            return separationX;
        }

        normal = acrossY >= 0f ? referenceY : -referenceY;
        axis = 1;
        return separationY;
    }

    /// <summary>
    /// How far apart the separating axis has to put two boxes before they are answered by the place they
    /// are <em>nearest</em> rather than by a face — a fifth of a millimetre, a tenth of the linear slop.
    /// </summary>
    const float ApartM = 0.0005f;

    /// <summary>
    /// Where a segment first meets a shape, as a fraction of its own length, or false where it does not.
    /// </summary>
    /// <remarks>
    /// A segment whose origin lies inside a shape does not meet that shape: it leaves without ever
    /// entering, so there is no place along the segment where it arrived, and what it reports is the next
    /// thing in front of it. Stated and tested rather than assumed (`SOL-19`), because a caster that
    /// starts inside its own body is the ordinary case and would otherwise find itself. Every ray also
    /// excludes its caster by name — one comparison, and it makes the guarantee the API's rather than the
    /// geometry's.
    /// </remarks>
    /// <remarks>
    /// <b>A ray meets the box a rounded one was rounded out of</b>, corners and all: it is measuring what
    /// a driver can see, and the few centimetres of corner it would otherwise cut through are worth less
    /// than a second description of the shape. Where the two answers differ the ray is the pessimistic
    /// one, which is the safe way round for something a stopping distance is drawn from.
    /// </remarks>
    public static bool CastSegment(
        Vector2 fromM, Vector2 travelM, Vector2 centreM, Vector2 rotation, Vector2 extentM,
        float cornerRadiusM, out float fraction)
    {
        return IsPoint(extentM)
            ? CastDisc(fromM, travelM, centreM, cornerRadiusM, out fraction)
            : CastBox(fromM, travelM, centreM, rotation, extentM + new Vector2(cornerRadiusM), out fraction);
    }

    static bool CastDisc(Vector2 fromM, Vector2 travelM, Vector2 centreM, float radiusM, out float fraction)
    {
        fraction = 0f;

        var toCentre = fromM - centreM;
        var travelSquared = travelM.LengthSquared();
        if (travelSquared < 1e-12f) return false;

        var half = Vector2.Dot(toCentre, travelM);
        var outside = toCentre.LengthSquared() - radiusM * radiusM;
        var discriminant = half * half - travelSquared * outside;
        if (discriminant < 0f) return false;

        // An origin inside the circle puts the near root behind the segment's start, which is how the
        // shape it is already in falls out of the answer rather than being struck out of it.
        var met = (-half - MathF.Sqrt(discriminant)) / travelSquared;
        if (met < 0f || met > 1f) return false;

        fraction = met;
        return true;
    }

    static bool CastBox(Vector2 fromM, Vector2 travelM, Vector2 centreM, Vector2 rotation, Vector2 half, out float fraction)
    {
        fraction = 0f;

        var origin = InverseRotate(rotation, fromM - centreM);
        var travel = InverseRotate(rotation, travelM);

        // Inside: the segment leaves without having entered, so there is no place along it where it met
        // the shape. The slabs below would otherwise call that a meeting at no distance.
        if (MathF.Abs(origin.X) <= half.X && MathF.Abs(origin.Y) <= half.Y) return false;

        var entered = 0f;
        var left = 1f;
        for (var axis = 0; axis < 2; axis++)
        {
            var at = axis == 0 ? origin.X : origin.Y;
            var along = axis == 0 ? travel.X : travel.Y;
            var reach = axis == 0 ? half.X : half.Y;

            if (MathF.Abs(along) < 1e-9f)
            {
                if (at < -reach || at > reach) return false;

                continue;
            }

            var enters = (-reach - at) / along;
            var leaves = (reach - at) / along;
            if (enters > leaves) (enters, leaves) = (leaves, enters);

            entered = MathF.Max(entered, enters);
            left = MathF.Min(left, leaves);
            if (left < entered) return false;
        }

        fraction = entered;
        return true;
    }
}
