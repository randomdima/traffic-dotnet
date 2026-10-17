using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>The lines a car's rear axle is driven along into a bay and out of one</b> (GEN-4f), laid from where the
/// car stands and on the circle it is given — nothing here is laid with the town.
/// </summary>
/// <remarks>
/// <para>
/// <b>A shape is one or two pieces, each a chain driven in one gear</b>, written into a caller's span one after
/// the other. A piece's arcs run the way the axle travels, so a piece driven in reverse runs backwards along
/// the car; a pose's heading here is always the way the axle is travelling.
/// </para>
/// <para>
/// <b>Which shape a car takes is not decided here.</b> These say what each shape is from one pose and whether
/// it exists at all; what it takes of the street is read off the ground by whoever asks, and so is whether
/// that ground is free.
/// </para>
/// </remarks>
internal static class BayManoeuvre
{
    public const int MostPieces = 2;

    /// <summary>The most arcs one piece takes: a straight, a swing, the turn and a straight.</summary>
    public const int MostArcsPerPiece = 4;

    public const int MostArcs = MostPieces * MostArcsPerPiece;

    /// <summary>A pose a piece is laid from or to: where the rear axle is, and which way it is travelling.</summary>
    public readonly record struct Pose(Vector2 AtM, float HeadingRad);

    /// <summary>
    /// <b>One shape as laid</b>: how many arcs each of its pieces took, in order, and which is driven in reverse.
    /// A shape with no arcs is one that does not exist from where it was asked.
    /// </summary>
    public readonly record struct Shape(int FirstArcs, int SecondArcs, bool FirstReverse, bool SecondReverse)
    {
        public static Shape None => default;

        public bool Exists => FirstArcs > 0;

        public int Pieces => SecondArcs > 0 ? 2 : FirstArcs > 0 ? 1 : 0;

        public int ArcCount => FirstArcs + SecondArcs;

        public int ArcsOf(int piece) => piece == 0 ? FirstArcs : SecondArcs;

        public int FirstArcOf(int piece) => piece == 0 ? 0 : FirstArcs;

        public bool IsReverse(int piece) => piece == 0 ? FirstReverse : SecondReverse;
    }

    /// <summary>
    /// <b>Into the bay nose first, forwards</b>: straight on from where the car is, a swing away from the bay
    /// where the room asks for one, the turn in, and <paramref name="straightensUpM"/> or more of straight to the
    /// pose — every turn on <paramref name="radiusM"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The swing is the least that lets the turn in end square</b>: a car standing nearer the bay than its own
    /// circle cannot turn in off the line it is on, and swinging out by an angle moves the turn's end that much
    /// further in. None where the room is there, and <see cref="Shape.None"/> past
    /// <paramref name="mostSwingRad"/>.
    /// </para>
    /// <para>
    /// <b>None, too, where the car has already passed where the turn has to begin</b>, or where the bay does not
    /// stand square enough to its line to be turned into (<see cref="BayTemplate.SquareEnough"/>).
    /// </para>
    /// </remarks>
    /// <param name="to">The axle's pose in the bay, heading into it.</param>
    public static Shape NoseIn(
        Pose from, Pose to, float radiusM, float straightensUpM, float mostSwingRad, Span<ArcSeg> into)
    {
        if (!Towards(from, to, out var sideSign, out var turnRad, out var aheadM, out var besideM)) return Shape.None;

        var sinTurn = MathF.Sin(turnRad);
        var cosTurn = MathF.Cos(turnRad);
        var swingCos = (((besideM - (straightensUpM * sinTurn)) / radiusM) + 1f + cosTurn) * 0.5f;
        var swingRad = swingCos >= 1f ? 0f : MathF.Acos(MathF.Max(swingCos, -1f));
        if (swingRad > mostSwingRad) return Shape.None;

        var intoTheBayM = (besideM - (radiusM * ((2f * MathF.Cos(swingRad)) - 1f - cosTurn))) / sinTurn;
        var onM = aheadM - (intoTheBayM * cosTurn) - (2f * radiusM * MathF.Sin(swingRad)) - (radiusM * sinTurn);
        if (onM < -LineTolerance.RoundingM) return Shape.None;

        var laid = 0;
        var at = from.AtM;
        var headingRad = from.HeadingRad;
        Lay(ref laid, ref at, ref headingRad, MathF.Max(onM, 0f), 0f, into);
        Lay(ref laid, ref at, ref headingRad, swingRad * radiusM, -sideSign / radiusM, into);
        Lay(ref laid, ref at, ref headingRad, (turnRad + swingRad) * radiusM, sideSign / radiusM, into);
        Lay(ref laid, ref at, ref headingRad, intoTheBayM, 0f, into);
        return new Shape(laid, 0, FirstReverse: false, SecondReverse: false);
    }

    /// <summary>
    /// <b>Into the bay backwards</b>: on past it forwards to where the turn back in begins, and from there one
    /// turn on <paramref name="radiusM"/> in reverse and <paramref name="straightensUpM"/> or more of straight
    /// to the pose. <b>One piece where the car already stands at that place</b>, the reverse alone.
    /// </summary>
    /// <param name="to">The axle's pose in the bay, heading into it — the way it travels backing in.</param>
    public static Shape BackIn(Pose from, Pose to, float radiusM, float straightensUpM, Span<ArcSeg> into)
    {
        if (!Towards(from, to, out _, out var turnRad, out var aheadM, out var besideM)) return Shape.None;

        var sinTurn = MathF.Sin(turnRad);
        var tangentM = radiusM * MathF.Tan((MathF.PI - turnRad) * 0.5f);
        var cornerM = aheadM - (besideM * MathF.Cos(turnRad) / sinTurn);
        var pullM = cornerM + tangentM;
        if (pullM < -LineTolerance.RoundingM || (besideM / sinTurn) - tangentM < straightensUpM) return Shape.None;

        var first = 0;
        var at = from.AtM;
        var headingRad = from.HeadingRad;
        Lay(ref first, ref at, ref headingRad, MathF.Max(pullM, 0f), 0f, into);

        var back = Spline.StraightArcStraightInto(at, headingRad + MathF.PI, to.AtM, to.HeadingRad, radiusM, into[first..]);
        if (back == 0) return Shape.None;

        return first == 0
            ? new Shape(back, 0, FirstReverse: true, SecondReverse: false)
            : new Shape(first, back, FirstReverse: false, SecondReverse: true);
    }

    /// <summary>
    /// <b>Out of the bay onto a street's line</b>, forwards or in <paramref name="reverse"/>: straight out along
    /// the bay, one turn on <paramref name="radiusM"/> and <paramref name="runOutM"/> of straight along the street
    /// the way the car will then drive it.
    /// </summary>
    /// <remarks>
    /// <b>It lands on the line it was given where the circle fits, and past it where the circle does not</b> — by
    /// the least that does, up to <paramref name="mostAsideM"/>: a car standing nearer the lane than its own
    /// circle comes out across it and straightens up on the far side of its middle, which is ground it then
    /// drives back off. <see cref="Shape.None"/> where even that is not enough, or where the street's line and
    /// the bay's are too near parallel to turn between.
    /// </remarks>
    /// <param name="from">The axle's pose in the bay, heading the way it travels out.</param>
    /// <param name="onM">A point on the street's line the car lands on.</param>
    /// <param name="alongRad">The way the car travels along that line once it has landed.</param>
    public static Shape OutOfTheBay(
        Pose from, bool reverse, Vector2 onM, float alongRad, float radiusM, float runOutM, float mostAsideM,
        Span<ArcSeg> into)
    {
        var travel = Heading.Unit(from.HeadingRad);
        var along = Heading.Unit(alongRad);
        var across = Spline.Cross(travel, along);
        if (MathF.Abs(across) < LineTolerance.RoundingM) return Shape.None;

        // <b>Away from the bay is the way the car travels out</b>, taken square off the street's line.
        var normal = new Vector2(-along.Y, along.X);
        var away = Vector2.Dot(travel, normal) >= 0f ? normal : -normal;

        var turnRad = Spline.WrapRad(alongRad - from.HeadingRad);
        var tangentM = radiusM * MathF.Abs(MathF.Tan(turnRad * 0.5f));
        var outToTheLineM = Spline.Cross(onM - from.AtM, along) / across;
        var perAsideM = Spline.Cross(away, along) / across;
        var asideM = perAsideM <= 0f ? 0f : MathF.Max(0f, (tangentM - outToTheLineM) / perAsideM);
        if (asideM > mostAsideM) return Shape.None;

        var cornerM = from.AtM + (travel * (outToTheLineM + (asideM * perAsideM)));
        var landM = cornerM + (along * (tangentM + runOutM));
        var laid = Spline.StraightArcStraightInto(from.AtM, from.HeadingRad, landM, alongRad, radiusM, into);
        return laid == 0 ? Shape.None : new Shape(laid, 0, reverse, SecondReverse: false);
    }

    /// <summary>
    /// <b>Into a place alongside the kerb, forwards</b> (GEN-4n): straight on from where the car is, then an S — a turn
    /// toward the kerb on <paramref name="radiusM"/> and one back as far as the place's own line turns — and
    /// <paramref name="straightensUpM"/> of straight to the pose. <see cref="Shape.None"/> where the place does not lie
    /// alongside the car's line or the car is nearer it than the S takes.
    /// </summary>
    /// <param name="to">The axle's pose in the place, heading along the kerb.</param>
    public static Shape AlongsideIn(Pose from, Pose to, float radiusM, float straightensUpM, Span<ArcSeg> into)
    {
        if (!Beside(from, to, out var sideSign, out var aheadM, out var besideM, out var deltaRad)
            || !S(besideM, deltaRad, radiusM, straightensUpM, out var firstRad, out var secondRad, out var runM))
        {
            return Shape.None;
        }

        var onM = aheadM - runM;
        if (onM < -LineTolerance.RoundingM) return Shape.None;

        var laid = 0;
        var at = from.AtM;
        var headingRad = from.HeadingRad;
        Lay(ref laid, ref at, ref headingRad, MathF.Max(onM, 0f), 0f, into);
        LayS(ref laid, ref at, ref headingRad, sideSign, firstRad, secondRad, radiusM, straightensUpM, into);
        return new Shape(laid, 0, FirstReverse: false, SecondReverse: false);
    }

    /// <summary>
    /// <b>Into a place alongside the kerb, backwards</b> (GEN-4n): on past it forwards to where the S back in begins,
    /// and from there the S in reverse and <paramref name="straightensUpM"/> of straight to the pose — one piece where
    /// the car already stands there. <see cref="Shape.None"/> where the place does not lie alongside the car's line or
    /// the car has already passed where the S begins.
    /// </summary>
    /// <param name="to">The axle's pose in the place, heading the way it travels backing in — back along the kerb.</param>
    public static Shape AlongsideBackIn(Pose from, Pose to, float radiusM, float straightensUpM, Span<ArcSeg> into)
    {
        // Backing in, the axle travels against the car's heading, and the place lies to the same side of the street
        // as it does going forwards — to the other hand of the travel.
        var backwards = new Pose(from.AtM, from.HeadingRad + MathF.PI);
        if (!Beside(backwards, to, out var sideSign, out _, out var besideM, out var deltaRad)
            || !S(besideM, deltaRad, radiusM, straightensUpM, out var firstRad, out var secondRad, out var runM))
        {
            return Shape.None;
        }

        var ahead = Heading.Unit(from.HeadingRad);
        var startsM = to.AtM + (ahead * runM) - (new Vector2(-ahead.Y, ahead.X) * (-sideSign * besideM));
        var pullM = Vector2.Dot(startsM - from.AtM, ahead);
        if (pullM < -LineTolerance.RoundingM) return Shape.None;

        var first = 0;
        var at = from.AtM;
        var headingRad = from.HeadingRad;
        Lay(ref first, ref at, ref headingRad, MathF.Max(pullM, 0f), 0f, into);

        var back = 0;
        var reverseHeadingRad = headingRad + MathF.PI;
        LayS(ref back, ref at, ref reverseHeadingRad, sideSign, firstRad, secondRad, radiusM, straightensUpM, into[first..]);
        return first == 0
            ? new Shape(back, 0, FirstReverse: true, SecondReverse: false)
            : new Shape(first, back, FirstReverse: false, SecondReverse: true);
    }

    /// <summary>
    /// <b>Out of a place alongside the kerb onto the street's line</b> (GEN-4n): <paramref name="backM"/> straight back
    /// first where it is asked — the room behind the car, where what stands in front leaves too little for the S — then
    /// an S out on <paramref name="radiusM"/> onto the line and <paramref name="runOutM"/> of straight along it.
    /// <see cref="Shape.None"/> where the line does not run alongside the car.
    /// </summary>
    /// <param name="from">The axle's pose in the place, heading along the kerb.</param>
    /// <param name="onM">A point on the street's line the car lands on.</param>
    /// <param name="alongRad">The way the car travels along that line once it has landed.</param>
    public static Shape AlongsideOut(
        Pose from, float backM, Vector2 onM, float alongRad, float radiusM, float runOutM, Span<ArcSeg> into)
    {
        var first = 0;
        var at = from.AtM;
        var reverseHeadingRad = from.HeadingRad + MathF.PI;
        Lay(ref first, ref at, ref reverseHeadingRad, backM, 0f, into);

        // The line's own point, moved along it to abeam of where the S begins: an S is laid by how far it carries the
        // car across, and how far ahead it lands is the S's to say.
        var start = new Pose(at, from.HeadingRad);
        var along = Heading.Unit(alongRad);
        var abeamM = onM + (along * Vector2.Dot(at - onM, along));
        if (!Beside(start, new Pose(abeamM, alongRad), out var sideSign, out _, out var besideM, out var deltaRad)
            || !S(besideM, deltaRad, radiusM, runOutM, out var firstRad, out var secondRad, out var runM))
        {
            return Shape.None;
        }

        // Laid again to the place on the line the first one ran out abeam of: a line not quite alongside the car stands
        // further off or nearer there than abeam of where the S begins.
        if (!Beside(start, new Pose(abeamM + (along * runM), alongRad), out sideSign, out _, out besideM, out deltaRad)
            || !S(besideM, deltaRad, radiusM, runOutM, out firstRad, out secondRad, out _))
        {
            return Shape.None;
        }

        var outward = 0;
        var headingRad = from.HeadingRad;
        LayS(ref outward, ref at, ref headingRad, sideSign, firstRad, secondRad, radiusM, runOutM, into[first..]);
        return first == 0
            ? new Shape(outward, 0, FirstReverse: false, SecondReverse: false)
            : new Shape(first, outward, FirstReverse: true, SecondReverse: false);
    }

    /// <summary>
    /// <b>Where a pose alongside stands from another</b>: which hand of the first's travel it lies to (+1 left, −1
    /// right), how far ahead and how far to that hand, and how far its heading turns toward that hand. False where the
    /// two do not run alongside (<see cref="BayTemplate.Alongside"/>).
    /// </summary>
    static bool Beside(Pose from, Pose to, out float sideSign, out float aheadM, out float besideM, out float deltaRad)
    {
        var ahead = Heading.Unit(from.HeadingRad);
        var left = new Vector2(-ahead.Y, ahead.X);
        var offM = to.AtM - from.AtM;
        aheadM = Vector2.Dot(offM, ahead);
        var leftM = Vector2.Dot(offM, left);
        sideSign = leftM >= 0f ? 1f : -1f;
        besideM = sideSign * leftM;
        deltaRad = sideSign * Spline.WrapRad(to.HeadingRad - from.HeadingRad);
        return BayTemplate.Alongside(deltaRad);
    }

    /// <summary>
    /// <b>The S that carries a car <paramref name="besideM"/> to one hand and turns it <paramref name="deltaRad"/> toward
    /// it</b>: a turn toward that hand on <paramref name="radiusM"/>, one back, and <paramref name="postM"/> of straight —
    /// how far each turn runs and how far ahead the whole of it ends. False where no such S exists, or its first turn
    /// would run past square.
    /// </summary>
    static bool S(float besideM, float deltaRad, float radiusM, float postM, out float firstRad, out float secondRad, out float aheadM)
    {
        firstRad = 0f;
        secondRad = 0f;
        aheadM = 0f;
        var cosFirst = (1f + MathF.Cos(deltaRad) - ((besideM - (postM * MathF.Sin(deltaRad))) / radiusM)) * 0.5f;
        if (cosFirst is < 0f or > 1f) return false;

        firstRad = MathF.Acos(cosFirst);
        secondRad = firstRad - deltaRad;
        if (secondRad < 0f) return false;

        aheadM = (radiusM * ((2f * MathF.Sin(firstRad)) - MathF.Sin(deltaRad))) + (postM * MathF.Cos(deltaRad));
        return true;
    }

    /// <summary>An S laid on from where the last piece ended (<see cref="S"/>): the turn toward the hand, the turn back, the straight.</summary>
    static void LayS(
        ref int laid, ref Vector2 atM, ref float headingRad, float sideSign, float firstRad, float secondRad, float radiusM,
        float postM, Span<ArcSeg> into)
    {
        Lay(ref laid, ref atM, ref headingRad, firstRad * radiusM, sideSign / radiusM, into);
        Lay(ref laid, ref atM, ref headingRad, secondRad * radiusM, -sideSign / radiusM, into);
        Lay(ref laid, ref atM, ref headingRad, postM, 0f, into);
    }

    /// <summary>
    /// <b>Where one pose stands from another, in the first one's own frame</b>: which hand the second heading
    /// turns to (+1 to the left of the travel, −1 to the right), how far it turns, and how far ahead and to that
    /// hand the second pose stands. False where the turn is not square enough for a bay, or the pose is behind
    /// the hand it turns to.
    /// </summary>
    static bool Towards(Pose from, Pose to, out float sideSign, out float turnRad, out float aheadM, out float besideM)
    {
        var ahead = Heading.Unit(from.HeadingRad);
        var bay = Heading.Unit(to.HeadingRad);
        var across = Spline.Cross(ahead, bay);
        sideSign = across >= 0f ? 1f : -1f;
        turnRad = MathF.Atan2(sideSign * across, Vector2.Dot(ahead, bay));

        var offM = to.AtM - from.AtM;
        aheadM = Vector2.Dot(offM, ahead);
        besideM = Vector2.Dot(offM, new Vector2(-ahead.Y, ahead.X) * sideSign);
        return BayTemplate.SquareEnough(turnRad) && besideM > 0f;
    }

    /// <summary>One piece of a chain laid on from where the last one ended — or nothing where it has no length.</summary>
    static void Lay(ref int laid, ref Vector2 atM, ref float headingRad, float lengthM, float curvature, Span<ArcSeg> into)
    {
        if (lengthM <= LineTolerance.RoundingM) return;

        var piece = new ArcSeg(atM, headingRad, lengthM, curvature);
        into[laid++] = piece;
        atM = piece.EndM;
        headingRad = piece.HeadingAtRad(lengthM);
    }
}
