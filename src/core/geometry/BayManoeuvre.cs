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
