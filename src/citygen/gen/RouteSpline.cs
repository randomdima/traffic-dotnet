using System.Numerics;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>A route drawn as one smooth curve</b> (GEN-63): a cubic Hermite spline through as few of the route's corners as keep
/// it on the ground the route was found over and no tighter than it may bend — its two ends, every crossing's corners and
/// the corner it is drawn through — and through any other only where the curve without it strays off that ground or
/// bends too tight: the route's own corner nearest there, which the route turned through more gradually.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each corner's tangent is the chord between its neighbours</b> (Catmull–Rom), an end's the chord to the one beside it
/// and a crossing's the crossing's own line, every piece leaving and arriving as long as its chord — so two corners of one
/// crossing are joined by that straight exactly, and the curve holds the crossing's line for its approach either side.
/// </para>
/// <para>
/// <b>The curve is drawn whole or not at all</b>: one that strays or bends too tight between two corners with none of the
/// route's left between them is none, and the caller tries another route rather than mending this one.
/// </para>
/// </remarks>
internal static class RouteSpline
{
    /// <summary>How many times a step of the drawn line a piece is read at, for where it strays and how tight it bends.</summary>
    const int ReadsPerStep = 4;

    /// <summary>How near either end of a piece, as a share of the tightest radius, a corner taken into it may stand.</summary>
    const float NearestCornerShare = 0.5f;

    /// <summary>How far off a crossing's line a corner may stand and still be read as on it: a float's error over a town.</summary>
    const float OnALineM = 0.01f;

    /// <param name="through">The corner the curve passes whatever the ground, or −1.</param>
    /// <param name="leaves">The way the curve leaves its first corner, or zero where it is free to.</param>
    /// <param name="arrives">The way it arrives at its last, or zero.</param>
    /// <param name="clear">Whether the curve may pass a place off its crossings.</param>
    /// <param name="tightestM">The tightest radius the curve may bend at.</param>
    /// <param name="toleranceM">How far the drawn line's straights may stand off the curve.</param>
    /// <param name="stepM">How far apart the curve is read for where it strays, a few times over.</param>
    /// <param name="strayed">Told where the curve strays or bends too tight with nothing left to take in, when it is none.</param>
    public static Polyline? Of(
        IReadOnlyList<RouteCorner> corners, int through, Vector2 leaves, Vector2 arrives, Func<Vector2, bool> clear,
        float tightestM, float toleranceM, float stepM, Action<Vector2>? strayed = null)
    {
        var held = new bool[corners.Count];
        for (var corner = 0; corner < corners.Count; corner++)
        {
            held[corner] = corner == 0 || corner == corners.Count - 1 || corner == through || corners[corner].Crossing >= 0;
        }

        var anchors = new SortedSet<int>(Enumerable.Range(0, corners.Count).Where(corner => held[corner]));
        var refused = new bool[corners.Count];
        while (true)
        {
            var at = anchors.ToList();
            var pieces = Pieces(corners, at, leaves, arrives);
            var settled = true;
            for (var piece = 0; piece < pieces.Length; piece++)
            {
                if (OnACrossing(corners, at, piece) || Strays(pieces[piece], clear, tightestM, stepM) is not { } stray) continue;

                settled = false;
                var (from, to) = (at[piece], at[piece + 1]);

                // None nearer either end than a share of the radius: the piece it would leave is too short to turn in.
                var (fromM, toM) = (corners[from].AtM, corners[to].AtM);
                var inside = Enumerable.Range(from + 1, to - from - 1).Where(corner =>
                    !refused[corner]
                    && MathF.Min(Vector2.Distance(corners[corner].AtM, fromM), Vector2.Distance(corners[corner].AtM, toM)) >= tightestM * NearestCornerShare);

                // A corner taken in that the curve bends too tight beside, or strays beside with nothing left to take in,
                // is taken back out, and never again: the route turned through more gradually beside it.
                if (TakenIn() is { } end && (stray.Tight || !inside.Any()))
                {
                    anchors.Remove(end);
                    refused[end] = true;
                    continue;
                }

                if (!inside.Any())
                {
                    strayed?.Invoke(stray.AtM);
                    return null;
                }

                anchors.Add(inside.MinBy(corner => Vector2.Distance(corners[corner].AtM, stray.AtM)));

                // The end of the piece that was taken in, the nearer the stray where both were.
                int? TakenIn() =>
                    !held[from] && (held[to] || Vector2.Distance(fromM, stray.AtM) <= Vector2.Distance(toM, stray.AtM)) ? from
                    : !held[to] ? to
                    : null;
            }

            if (settled) return Drawn(pieces, toleranceM, stepM);
        }
    }

    static Hermite[] Pieces(IReadOnlyList<RouteCorner> corners, List<int> anchors, Vector2 leaves, Vector2 arrives)
    {
        // A crossing's corner leaves on the crossing's line, and so does one standing on that line beside it — the room
        // a route held past its approach — any other on the chord between its neighbours. An end not given its way is
        // on its neighbour's turned back about the chord to it, so the curve does not bend where it starts.
        var count = anchors.Count;
        var tangents = new Vector2[count];
        for (var at = 0; at < count; at++)
        {
            tangents[at] = at == 0 && leaves != Vector2.Zero ? leaves
                : at == count - 1 && arrives != Vector2.Zero ? arrives
                : OnACrossing(corners, anchors, at) ? Unit(PointM(at + 1) - PointM(at))
                : at > 0 && OnACrossing(corners, anchors, at - 1) ? Unit(PointM(at) - PointM(at - 1))
                : OnTheLineOf(at + 1, at + 2, at) ?? OnTheLineOf(at - 2, at - 1, at)
                  ?? (at > 0 && at < count - 1 ? Unit(PointM(at + 1) - PointM(at - 1)) : Vector2.Zero);
        }

        var endM = PointM(1) - PointM(0);
        if (tangents[0] == Vector2.Zero) tangents[0] = tangents[1] == Vector2.Zero ? Unit(endM) : TurnedBack(tangents[1], endM);
        endM = PointM(count - 1) - PointM(count - 2);
        if (tangents[^1] == Vector2.Zero) tangents[^1] = TurnedBack(tangents[^2], endM);

        var pieces = new Hermite[anchors.Count - 1];
        for (var piece = 0; piece < pieces.Length; piece++)
        {
            var (fromM, toM) = (corners[anchors[piece]].AtM, corners[anchors[piece + 1]].AtM);
            var chordM = Vector2.Distance(fromM, toM);
            pieces[piece] = new Hermite(fromM, tangents[piece] * chordM, toM, tangents[piece + 1] * chordM);
        }

        return pieces;

        Vector2 PointM(int at) => corners[anchors[at]].AtM;

        // The line of the crossing two anchors are corners of, where the anchor at hand stands on it.
        Vector2? OnTheLineOf(int one, int other, int at)
        {
            if (one < 0 || other >= count || !OnACrossing(corners, anchors, one)) return null;

            var line = Unit(PointM(other) - PointM(one));
            var offM = PointM(at) - PointM(one);
            return MathF.Abs((line.X * offM.Y) - (line.Y * offM.X)) <= OnALineM ? line : null;
        }
    }

    /// <summary>Whether a piece runs between two corners of one crossing: that crossing's own straight.</summary>
    static bool OnACrossing(IReadOnlyList<RouteCorner> corners, List<int> anchors, int piece) =>
        piece + 1 < anchors.Count && corners[anchors[piece]].Crossing >= 0 && corners[anchors[piece]].Crossing == corners[anchors[piece + 1]].Crossing;

    /// <summary>The first place a piece strays off the ground it may pass, else the first it bends too tight at, else none.</summary>
    static (Vector2 AtM, bool Tight)? Strays(in Hermite piece, Func<Vector2, bool> clear, float tightestM, float stepM)
    {
        var reads = Reads(piece, stepM);
        for (var read = 1; read < reads; read++)
        {
            var atM = piece.At(read / (float)reads);
            if (!clear(atM)) return (atM, false);
        }

        for (var read = 0; read <= reads; read++)
        {
            if (piece.RadiusM(read / (float)reads) < tightestM) return (piece.At(read / (float)reads), true);
        }

        return null;
    }

    /// <summary>A neighbour's tangent mirrored about the chord to it: the end of a curve that does not bend there.</summary>
    static Vector2 TurnedBack(Vector2 neighbour, Vector2 chordM)
    {
        var along = Unit(chordM);
        return Unit((2f * Vector2.Dot(neighbour, along) * along) - neighbour);
    }

    /// <summary>
    /// <b>The curve as the line a road is laid round</b>: every corner of it, and between them a point each step that keeps
    /// the straight to the next within the tolerance of the curve — the chord of a circle as wide as the curve bends there
    /// — each step no longer than the one before by more than <see cref="StepGrowth"/>, so two legs either side of a point
    /// are near one length and the circle through it and its neighbours is the curve's (<see cref="RoadLines"/>).
    /// </summary>
    static Polyline Drawn(Hermite[] pieces, float toleranceM, float stepM)
    {
        var (atM, alongM, stepsM, corner) = (new List<Vector2>(), new List<float>(), new List<float>(), new List<bool>());
        foreach (var piece in pieces)
        {
            var reads = Reads(piece, stepM);
            for (var read = atM.Count == 0 ? 0 : 1; read <= reads; read++)
            {
                var s = read / (float)reads;
                var pointM = read == 0 ? piece.FromM : read == reads ? piece.ToM : piece.At(s);
                alongM.Add(atM.Count == 0 ? 0f : alongM[^1] + Vector2.Distance(atM[^1], pointM));
                atM.Add(pointM);
                stepsM.Add(MathF.Sqrt(8f * piece.RadiusM(s) * toleranceM));
                corner.Add(read == 0 || read == reads);
            }
        }

        for (var at = 1; at < atM.Count; at++) stepsM[at] = MathF.Min(stepsM[at], stepsM[at - 1] + (StepGrowth * (alongM[at] - alongM[at - 1])));
        for (var at = atM.Count - 2; at >= 0; at--) stepsM[at] = MathF.Min(stepsM[at], stepsM[at + 1] + (StepGrowth * (alongM[at + 1] - alongM[at])));

        var pointsM = new List<Vector2> { atM[0] };
        var (lastM, lastIsCorner) = (0f, true);
        for (var at = 1; at < atM.Count; at++)
        {
            if (corner[at])
            {
                // A leg into a corner shorter than half a step is taken into the leg before it.
                if (!lastIsCorner && alongM[at] - lastM < stepsM[at] * 0.5f) pointsM.RemoveAt(pointsM.Count - 1);

                pointsM.Add(atM[at]);
                (lastM, lastIsCorner) = (alongM[at], true);
            }
            else if (alongM[at] - lastM >= stepsM[at])
            {
                pointsM.Add(atM[at]);
                (lastM, lastIsCorner) = (alongM[at], false);
            }
        }

        return new Polyline([.. pointsM]);
    }

    /// <summary>How much longer a step of the drawn line may be than the one before it, as a share of its own length.</summary>
    const float StepGrowth = 0.25f;

    static int Reads(in Hermite piece, float stepM) =>
        Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(piece.FromM, piece.ToM) / stepM)) * ReadsPerStep;

    static Vector2 Unit(Vector2 runM)
    {
        var lengthM = runM.Length();
        return lengthM > 1e-6f ? runM / lengthM : Vector2.Zero;
    }

    /// <summary>One piece of the curve: its two ends and the tangent it leaves and arrives on, each as long as the piece's chord.</summary>
    readonly record struct Hermite(Vector2 FromM, Vector2 FromTangentM, Vector2 ToM, Vector2 ToTangentM)
    {
        public Vector2 At(float s)
        {
            var (s2, s3) = (s * s, s * s * s);
            return (FromM * ((2f * s3) - (3f * s2) + 1f)) + (FromTangentM * (s3 - (2f * s2) + s))
                   + (ToM * ((-2f * s3) + (3f * s2))) + (ToTangentM * (s3 - s2));
        }

        /// <summary>How tight it bends this far along it: the radius of the circle it follows there.</summary>
        public float RadiusM(float s)
        {
            var velocity = (FromM * ((6f * s * s) - (6f * s))) + (FromTangentM * ((3f * s * s) - (4f * s) + 1f))
                           + (ToM * ((-6f * s * s) + (6f * s))) + (ToTangentM * ((3f * s * s) - (2f * s)));
            var bending = (FromM * ((12f * s) - 6f)) + (FromTangentM * ((6f * s) - 4f))
                          + (ToM * ((-12f * s) + 6f)) + (ToTangentM * ((6f * s) - 2f));
            var turning = MathF.Abs((velocity.X * bending.Y) - (velocity.Y * bending.X));
            var speed = velocity.Length();
            return turning > 1e-9f ? speed * speed * speed / turning : float.PositiveInfinity;
        }
    }
}
