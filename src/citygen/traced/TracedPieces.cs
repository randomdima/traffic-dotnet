using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>What a traced road's merged line keeps of its line as rounded at one of its two ends (<see cref="TracedPieces"/>).</summary>
internal enum RoadEnd : byte
{
    /// <summary>The piece it ends in, as it was: the end's pose and the bend the road arrives there in.</summary>
    Piece,

    /// <summary>The end's pose: where it stands and the heading the road meets its junction on.</summary>
    Pose,

    /// <summary>Where the end stands, and nothing more.</summary>
    Place,
}

/// <summary>
/// <b>A traced road's laid line in the fewest pieces that keep its survey</b> (GEN-57): a run of its pieces that one
/// arc, two, one corner between straights, or two bends and the straight between them join from the pose it starts on
/// to the pose it ends on within the road's tolerance of the survey both ways (<see cref="CityGenFigures.TracedLineToleranceM"/>)
/// is laid as those — and, at an end that keeps only its place, a run from the end one arc, or a straight and an arc,
/// and the road whole one arc.
/// </summary>
/// <remarks>
/// <para>
/// <b>A corner's arc is laid symmetric about it</b> (<see cref="TracedAlignment"/>), so where its two legs differ
/// the longer keeps a straight the shorter does not, a road of one gentle bend is three pieces, and a sector of a
/// ring is a stub, an arc and a stub. None of it is the survey's: it is what rounding corners costs.
/// </para>
/// <para>
/// <b>Between two pieces kept a run keeps both their poses</b>: one arc meets both only where the turn between them
/// is twice the chord's, and a biarc always does — so a run is one arc where that is so, and otherwise two, which
/// saves a piece only over three. <b>A road is designed as straights and the bends between them</b>: a run of
/// two bends with a straight drawn wavering between them is the two bends and the straight tangent to both, and one
/// that turns a single corner — a stub left before a bend, or a bend drawn as two corners — the arc tangent to the
/// two poses' lines and the straights left along them, either of which saves a piece over four. Merged again, a line
/// has new joints between which runs open that did not before.
/// <b>At the road's own ends it keeps what its junction needs</b>
/// (<see cref="RoadEnd"/>): a junction lays its movements off the poses its roads meet it on and the bend they
/// arrive in, so how much of an end is kept is the junction's to say (<see cref="TracedStreets"/>).
/// </para>
/// <para>
/// <b>Read both ways, as the corners are</b>: every surveyed point the run stands for, and the middle of every leg
/// between two, off the arcs; and the arcs, sampled, off the surveyed line — a gentle arc can pass every point of a
/// long straight leg and bow off its middle. <b>A run may stand as far off as the pieces it replaces</b>: a corner
/// rounded at its least radius sags past the tolerance, and a run over it may sag as far.
/// </para>
/// </remarks>
internal static class TracedPieces
{
    /// <summary>
    /// The line in the fewest pieces, its two ends where they were and keeping what each is asked to: the whole of it
    /// one arc where both keep only their place and that keeps the survey, and otherwise the fewest of every way of
    /// cutting the line into runs, each laid in fewer pieces or as it was.
    /// </summary>
    /// <remarks>
    /// Each run keeps the poses at its two ends, but for the line's own two where those keep only their place, so the
    /// runs are weighed apart, and the fewest is counted off the line's first piece forward, a run's end at a time.
    /// </remarks>
    /// <param name="surveyedM">The surveyed line in the line's own frame, first point to last at its two ends.</param>
    /// <param name="mapM">The map's extent from its origin, off which no run is laid (GEN-2b).</param>
    /// <param name="leastRadiusM">No arc laid tighter, which is where the road's lanes would fold.</param>
    public static ArcSeg[] Fewest(
        ReadOnlySpan<ArcSeg> line, ReadOnlySpan<Vector2> surveyedM, Vector2 mapM, float leastRadiusM, float toleranceM,
        RoadEnd atStart, RoadEnd atEnd)
    {
        if (line.Length < 2) return line.ToArray();

        var fit = new Fit(surveyedM, line, mapM, leastRadiusM, toleranceM);
        if (atStart == RoadEnd.Place && atEnd == RoadEnd.Place && fit.Whole(out var whole)) return [whole];

        // A run never takes in an end piece kept as it was.
        var (firstMerged, lastMerged) = (atStart == RoadEnd.Piece ? 1 : 0, atEnd == RoadEnd.Piece ? line.Length - 2 : line.Length - 1);

        // fewestTo[k]: the fewest pieces the line's first k are laid in, the fewest of them bent the closer, the last run
        // of them pieces runFrom[k] to k − 1 laid as runOf[k] — or, where that has none, piece k − 1 as it was.
        var fewestTo = new (int Pieces, int Bent)[line.Length + 1];
        var runFrom = new int[line.Length + 1];
        var runOf = new ArcSeg[line.Length + 1][];
        Span<ArcSeg> run = stackalloc ArcSeg[3];
        for (var upTo = 1; upTo <= line.Length; upTo++)
        {
            var (pieces, bent) = fewestTo[upTo - 1];
            (fewestTo[upTo], runFrom[upTo]) = ((pieces + 1, bent + Bent(line.Slice(upTo - 1, 1))), upTo - 1);
            for (var first = firstMerged; first < upTo - 1 && upTo - 1 <= lastMerged; first++)
            {
                var mostPieces = fewestTo[upTo].Pieces - fewestTo[first].Pieces;
                if (mostPieces < 1) continue;

                var (freeStart, freeEnd) = (atStart == RoadEnd.Place && first == 0, atEnd == RoadEnd.Place && upTo == line.Length);
                var laid = fit.Run(first, upTo - 1, run, mostPieces, freeStart && !freeEnd, freeEnd && !freeStart);
                if (laid == 0) continue;

                var now = (fewestTo[first].Pieces + laid, fewestTo[first].Bent + Bent(run[..laid]));
                if (now.CompareTo(fewestTo[upTo]) >= 0) continue;

                (fewestTo[upTo], runFrom[upTo], runOf[upTo]) = (now, first, run[..laid].ToArray());
            }
        }

        var fewest = new ArcSeg[fewestTo[^1].Pieces];
        for (int upTo = line.Length, laid = fewest.Length; upTo > 0; upTo = runFrom[upTo])
        {
            ReadOnlySpan<ArcSeg> pieces = runOf[upTo] ?? line.Slice(upTo - 1, 1);
            laid -= pieces.Length;
            pieces.CopyTo(fewest.AsSpan(laid));
        }

        return fewest;
    }

    /// <summary>
    /// How many of a run's pieces bend at all, however faintly — which of two runs of as many pieces the one laid
    /// straight where the other only nearly is has fewer of.
    /// </summary>
    static int Bent(ReadOnlySpan<ArcSeg> run)
    {
        var bent = 0;
        foreach (var piece in run)
        {
            if (piece.Curvature != 0f) bent++;
        }

        return bent;
    }

    /// <summary>
    /// <b>Where the line's corners stand</b>, which is what a road keeps of them (<see cref="CityPlan.RoadArrays.ThroughOffsets"/>):
    /// for each arc, where the lines it leaves and arrives on meet — and nothing for a straight, or an arc of half a
    /// turn or more, whose two lines meet nowhere ahead of it.
    /// </summary>
    public static Vector2[] Corners(ReadOnlySpan<ArcSeg> line)
    {
        var cornersM = new List<Vector2>(line.Length);
        foreach (var piece in line)
        {
            var halfTurnRad = MathF.Abs(piece.Curvature * piece.LengthM) * 0.5f;
            if (halfTurnRad < 1e-4f || halfTurnRad >= MathF.PI * 0.5f) continue;

            cornersM.Add(piece.StartM + (piece.StartUnit * (MathF.Tan(halfTurnRad) / MathF.Abs(piece.Curvature))));
        }

        return [.. cornersM];
    }

    /// <summary>
    /// How near a run's arriving heading must be to the one it replaces, as an angle — what a float's own rounding
    /// leaves of a biarc solved to meet it, and far under any heading a lane would show.
    /// </summary>
    const float PoseMatchRad = 1e-4f;

    /// <summary>
    /// How near a run's end must stand to the place it replaces to, where it is solved for rather than aimed at: a
    /// millimetre, what a float's rounding leaves of the solve a few kilometres from the origin.
    /// </summary>
    const float PoseMatchM = 1e-3f;

    /// <summary>
    /// How much tighter and wider than a run's own bends its arcs are tried at, the bend itself first: a step either way
    /// is what a bend drawn as a polygon is rounded off its true radius by at most.
    /// </summary>
    static readonly float[] BendScales = [1f, 1f / 1.5f, 1.5f];

    /// <summary>
    /// The least a piece bends that counts as a bend: its curvature as a share of the tightest a road may turn —
    /// a thousandth, a radius a thousand times the road's least.
    /// </summary>
    const float StraightBend = 1e-3f;

    /// <summary>How many times a line is merged again from what its last merge laid, which opens runs between new joints.</summary>
    public const int MergeRounds = 3;

    /// <summary>
    /// How many places along each arc a candidate is read at against the survey: enough that the furthest an arc
    /// bows off a straight leg is read to under a hundredth of itself.
    /// </summary>
    const int SamplesAnArc = 16;

    /// <summary>
    /// How many shares of the chord between two poses a biarc's first tangent is tried at — one sixteenth, two, on
    /// to fifteen — after the biarc of equal tangents, the first biarc that keeps the survey taken.
    /// </summary>
    const int BiarcSteps = 16;

    /// <summary>
    /// How many headings a whole road's one arc is first read at, across the range it is sought over, before the
    /// nearest of them is closed on.
    /// </summary>
    const int WholeScanSteps = 8;

    /// <summary>
    /// The iterations the golden section between the nearest two readings runs for: (√5 − 1) ⁄ 2 to the 16th is
    /// under a two-thousandth of the step it closes on.
    /// </summary>
    const int WholeSteps = 16;

    /// <summary>(√5 − 1) ⁄ 2, the share of its interval a golden section keeps each step.</summary>
    const float Golden = 0.618034f;

    /// <summary>
    /// <b>An arc of one curvature off one pose, a straight, and an arc of another onto a second pose</b>, the straight
    /// tangent to both circles — false where none is, or what is laid does not arrive on the second pose.
    /// </summary>
    /// <remarks>
    /// The straight's heading ψ has the two circles' tangent points on one line along it, which is
    /// <c>(centre₂ − centre₁) × ψ = 1 / k₁ − 1 / k₂</c>: two headings, the shorter way round tried first.
    /// </remarks>
    internal static bool ArcStraightArc(
        Vector2 fromM, float fromRad, float firstCurvature, Vector2 toM, float toRad, float lastCurvature, Span<ArcSeg> into)
    {
        var lastCentreM = toM + (Heading.RightOf(Heading.Unit(toRad)) / lastCurvature);
        Span<(ArcSeg Arc, float HeadingRad)> arcs = stackalloc (ArcSeg, float)[2];
        foreach (var (first, headingRad) in arcs[..ArcsOnto(fromM, fromRad, firstCurvature, lastCentreM, (1f / firstCurvature) - (1f / lastCurvature), arcs)])
        {
            var toLastM = lastCentreM - (Heading.RightOf(Heading.Unit(headingRad)) / lastCurvature);
            var straightM = Vector2.Dot(toLastM - first.EndM, Heading.Unit(headingRad));
            if (!(straightM > 0f)) continue;

            var turnRad = Spline.WrapRad(toRad - headingRad);
            if (turnRad * lastCurvature < 0f) turnRad += MathF.CopySign(MathF.Tau, lastCurvature);

            into[0] = first;
            into[1] = new ArcSeg(first.EndM, headingRad, straightM, 0f);
            into[2] = new ArcSeg(into[1].EndM, headingRad, turnRad / lastCurvature, lastCurvature);
            if (Vector2.Distance(into[2].EndM, toM) <= PoseMatchM && Arrives(into[2], toRad)) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>An arc of one curvature off a pose, and the straight from where it points at a place on to it</b> — false
    /// where the place stands inside the arc's circle.
    /// </summary>
    /// <remarks>
    /// The arc's circle stands about <c>pose + right / k</c>, and a heading ψ on it is at <c>centre − right(ψ) / k</c>:
    /// the straight off it runs through the place where <c>(place − centre) × ψ = 1 / k</c>, which is two headings,
    /// the nearer round the arc tried first.
    /// </remarks>
    static bool ArcThenStraight(Vector2 fromM, float fromRad, float curvature, Vector2 toM, Span<ArcSeg> into)
    {
        Span<(ArcSeg Arc, float HeadingRad)> arcs = stackalloc (ArcSeg, float)[2];
        foreach (var (arc, headingRad) in arcs[..ArcsOnto(fromM, fromRad, curvature, toM, 1f / curvature, arcs)])
        {
            var straightM = Vector2.Dot(toM - arc.EndM, Heading.Unit(headingRad));
            into[0] = arc;
            into[1] = new ArcSeg(arc.EndM, headingRad, straightM, 0f);
            if (straightM > 0f && Vector2.Distance(into[1].EndM, toM) <= PoseMatchM) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>A straight from a place, and the arc of one curvature off it onto a pose</b> — the arc and straight laid back
    /// from the pose to the place, and turned round.
    /// </summary>
    static bool StraightThenArc(Vector2 fromM, Vector2 toM, float toRad, float curvature, Span<ArcSeg> into)
    {
        Span<ArcSeg> back = stackalloc ArcSeg[2];
        if (!ArcThenStraight(toM, toRad + MathF.PI, -curvature, fromM, back)) return false;

        Spline.ReverseInto(back, into[..2]);
        return true;
    }

    /// <summary>
    /// The arcs of one curvature off a pose that end on a heading ψ for which <c>(towardM − centre) × ψ</c> is
    /// <paramref name="crossM"/>, the shorter way round first: two, or none where it stands too near the centre.
    /// </summary>
    static int ArcsOnto(
        Vector2 fromM, float fromRad, float curvature, Vector2 towardM, float crossM, Span<(ArcSeg Arc, float HeadingRad)> into)
    {
        var centreM = fromM + (Heading.RightOf(Heading.Unit(fromRad)) / curvature);
        var awayM = towardM - centreM;
        var sine = crossM / awayM.Length();
        if (!(MathF.Abs(sine) < 1f)) return 0;

        var awayRad = MathF.Atan2(awayM.Y, awayM.X);
        var offRad = MathF.Asin(sine);
        var found = 0;
        foreach (var psi in (ReadOnlySpan<float>)[awayRad + offRad, awayRad + MathF.PI - offRad])
        {
            var turnRad = Spline.WrapRad(psi - fromRad);
            if (turnRad * curvature < 0f) turnRad += MathF.CopySign(MathF.Tau, curvature);

            into[found++] = (new ArcSeg(fromM, fromRad, turnRad / curvature, curvature), Spline.WrapRad(psi));
        }

        if (into[1].Arc.LengthM < into[0].Arc.LengthM) (into[0], into[1]) = (into[1], into[0]);
        return found;
    }

    /// <summary>
    /// The biarc between two poses whose first arc's tangent runs <paramref name="leavingM"/> from the first: the
    /// second's is then what keeps the two tangent at their joint — false where none is, or the two arcs laid do not
    /// arrive on the second pose.
    /// </summary>
    static bool Biarc(Vector2 fromM, float fromRad, Vector2 toM, float toRad, float leavingM, Span<ArcSeg> into)
    {
        var (from, to) = (Heading.Unit(fromRad), Heading.Unit(toRad));
        var chord = toM - fromM;

        // |chord − d₀·T₀ − d₁·T₁| = d₀ + d₁, solved for d₁.
        var below = (2f * Vector2.Dot(chord, to)) + (2f * leavingM * (1f - Vector2.Dot(from, to)));
        if (MathF.Abs(below) < 1e-6f) return false;

        var arrivingM = (Vector2.Dot(chord, chord) - (2f * leavingM * Vector2.Dot(chord, from))) / below;
        if (!(arrivingM > 0f)) return false;

        var jointM = (((fromM + (from * leavingM)) * arrivingM) + ((toM - (to * arrivingM)) * leavingM)) / (leavingM + arrivingM);
        into[0] = Spline.ArcThrough(fromM, fromRad, jointM);
        into[1] = Spline.ArcThrough(jointM, into[0].HeadingAtRad(into[0].LengthM), toM);
        return Arrives(into[1], toRad);
    }

    /// <summary>
    /// <b>A straight off one pose, an arc, and a straight onto a second</b>, the arc tangent to the two poses' lines this
    /// far back from where they meet — no further than the shorter leg, and a straight of no length left out: the
    /// pieces laid, or nought where the lines meet behind either pose or nowhere.
    /// </summary>
    /// <remarks>
    /// The whole of the shorter leg is an arc and the one straight the longer leaves, which is the only arc and straight
    /// there is between the two poses either way round.
    /// </remarks>
    internal static int Corner(Vector2 fromM, float fromRad, Vector2 toM, float toRad, float tangentM, Span<ArcSeg> into)
    {
        if (!Meet(fromM, fromRad, toM, toRad, out var leavingM, out var arrivingM)) return 0;

        tangentM = MathF.Min(tangentM, MathF.Min(leavingM, arrivingM));
        var turnRad = Spline.WrapRad(toRad - fromRad);
        var curvature = MathF.CopySign(MathF.Tan(MathF.Abs(turnRad) * 0.5f) / tangentM, turnRad);
        var laid = 0;
        var atM = fromM;
        if (leavingM - tangentM > 0f)
        {
            into[laid] = new ArcSeg(atM, fromRad, leavingM - tangentM, 0f);
            atM = into[laid++].EndM;
        }

        into[laid] = new ArcSeg(atM, fromRad, turnRad / curvature, curvature);
        atM = into[laid++].EndM;
        if (arrivingM - tangentM > 0f) into[laid++] = new ArcSeg(atM, toRad, arrivingM - tangentM, 0f);

        return Vector2.Distance(into[laid - 1].EndM, toM) <= PoseMatchM && Arrives(into[laid - 1], toRad) ? laid : 0;
    }

    /// <summary>
    /// How far ahead of one pose and behind a second their two lines meet — false where they meet behind either, or
    /// nowhere.
    /// </summary>
    static bool Meet(Vector2 fromM, float fromRad, Vector2 toM, float toRad, out float leavingM, out float arrivingM)
    {
        var (from, to) = (Heading.Unit(fromRad), Heading.Unit(toRad));
        var across = Spline.Cross(from, to);
        (leavingM, arrivingM) = (0f, 0f);
        if (MathF.Abs(across) < 1e-6f) return false;

        // fromM + leaving · from + arriving · to = toM.
        var chord = toM - fromM;
        (leavingM, arrivingM) = (Spline.Cross(chord, to) / across, Spline.Cross(from, chord) / across);
        return leavingM > 0f && arrivingM > 0f;
    }

    static bool Arrives(in ArcSeg last, float toRad) =>
        MathF.Abs(Spline.WrapRad(last.HeadingAtRad(last.LengthM) - toRad)) <= PoseMatchRad;

    /// <summary>
    /// One line and its survey, with which surveyed points stand along each piece: point <c>p</c> stands
    /// <c>_alongM[p]</c> along the line, so a run of pieces stands for the points whose distances it spans.
    /// </summary>
    readonly ref struct Fit
    {
        readonly ReadOnlySpan<Vector2> _surveyedM;
        readonly ReadOnlySpan<ArcSeg> _line;
        readonly Vector2 _mapM;
        readonly float _leastRadiusM;
        readonly float _toleranceM;
        readonly float[] _startM;
        readonly float[] _alongM;

        /// <summary>How far each piece and the survey it stands for stand apart, as it was laid.</summary>
        readonly float[] _laidOffM;

        public Fit(ReadOnlySpan<Vector2> surveyedM, ReadOnlySpan<ArcSeg> line, Vector2 mapM, float leastRadiusM, float toleranceM)
        {
            _surveyedM = surveyedM;
            _line = line;
            (_mapM, _leastRadiusM, _toleranceM) = (mapM, leastRadiusM, toleranceM);

            _startM = new float[line.Length + 1];
            for (var piece = 0; piece < line.Length; piece++) _startM[piece + 1] = _startM[piece] + line[piece].LengthM;

            // In order along the line, each searched for near the last: a road doubling back on itself passes a
            // point twice, and the survey's own order says which pass it is.
            _alongM = new float[surveyedM.Length];
            for (var point = 1; point < surveyedM.Length; point++)
            {
                var windowM = Vector2.Distance(surveyedM[point - 1], surveyedM[point]) + (2f * toleranceM) + 1f;
                _alongM[point] = Spline.ProjectM(line, surveyedM[point], _alongM[point - 1], windowM);
            }

            _laidOffM = new float[line.Length];
            for (var piece = 0; piece < line.Length; piece++)
            {
                _laidOffM[piece] = OffM(line.Slice(piece, 1), Stretch(piece, piece), float.PositiveInfinity);
            }
        }

        /// <summary>
        /// <b>The whole line one arc between its two ends</b>, or a straight, as near its survey as one comes — sought
        /// over the heading it leaves its first end on, about the arc through its two ends and its own middle.
        /// </summary>
        /// <remarks>
        /// Not between the line's own two end headings: those are its corners' legs, which can leave a bend the
        /// survey starts in at once several degrees off it. <b>Sought no further off than an arc keeping the survey
        /// can be</b>: turning the heading off the chord moves the arc anywhere in the middle three fifths of it an
        /// eighth of the chord a radian at least, and one that keeps the survey passes within twice what it may stand
        /// off of the line's middle.
        /// </remarks>
        public bool Whole(out ArcSeg arc)
        {
            var fromM = _line[0].StartM;
            var toM = _line[^1].EndM;
            var chord = toM - fromM;
            var chordM = chord.Length();
            var chordRad = MathF.Atan2(chord.Y, chord.X);
            var stretchM = Stretch(0, _line.Length - 1);
            var mayM = MayStandOffM(0, _line.Length - 1);

            // The arc through both ends and the line's middle leaves at half the turn the chord to the middle and
            // the chord on from it make.
            var middleM = Spline.SampleAt(_line, _startM[^1] * 0.5f).PositionM;
            var throughRad = Spline.WrapRad(MathF.Atan2(middleM.Y - fromM.Y, middleM.X - fromM.X) - chordRad);
            var onRad = Spline.WrapRad(MathF.Atan2(toM.Y - middleM.Y, toM.X - middleM.X) - chordRad);
            var seedRad = throughRad - onRad;

            var reachRad = MathF.Min(MathF.PI * 0.5f, 16f * mayM / MathF.Max(chordM, 1e-3f));
            var (low, high) = (MathF.Max(-MathF.PI * 0.5f, seedRad - reachRad), MathF.Min(MathF.PI * 0.5f, seedRad + reachRad));
            var stopM = 4f * mayM;

            var stepRad = (high - low) / WholeScanSteps;
            var (nearestRad, nearestM) = (low, float.PositiveInfinity);
            for (var step = 0; step <= WholeScanSteps; step++)
            {
                var offRad = low + (step * stepRad);
                var offM = OffM(fromM, chordRad + offRad, toM, stretchM, MathF.Min(nearestM, stopM));
                if (offM < nearestM) (nearestRad, nearestM) = (offRad, offM);
            }

            arc = default;
            if (!(nearestM <= stopM)) return false;

            (low, high) = (nearestRad - stepRad, nearestRad + stepRad);
            var (inner, outer) = (high - (Golden * (high - low)), low + (Golden * (high - low)));
            var (innerOffM, outerOffM) = (OffM(fromM, chordRad + inner, toM, stretchM, stopM), OffM(fromM, chordRad + outer, toM, stretchM, stopM));
            for (var step = 0; step < WholeSteps; step++)
            {
                if (innerOffM <= outerOffM)
                {
                    (high, outer, outerOffM) = (outer, inner, innerOffM);
                    inner = high - (Golden * (high - low));
                    innerOffM = OffM(fromM, chordRad + inner, toM, stretchM, stopM);
                }
                else
                {
                    (low, inner, innerOffM) = (inner, outer, outerOffM);
                    outer = low + (Golden * (high - low));
                    outerOffM = OffM(fromM, chordRad + outer, toM, stretchM, stopM);
                }
            }

            arc = Spline.ArcThrough(fromM, chordRad + ((low + high) * 0.5f), toM);
            return Keeps([arc], stretchM, mayM);
        }

        float OffM(Vector2 fromM, float headingRad, Vector2 toM, ReadOnlySpan<Vector2> stretchM, float stopM)
        {
            ReadOnlySpan<ArcSeg> arc = [Spline.ArcThrough(fromM, headingRad, toM)];
            return Laid(arc) ? OffM(arc, stretchM, stopM) : float.PositiveInfinity;
        }

        /// <summary>
        /// <b>Pieces <paramref name="first"/> to <paramref name="last"/> in no more than <paramref name="mostPieces"/></b>,
        /// into <paramref name="into"/>: from a free start one arc onto the pose the last arrives on, to a free end one
        /// arc off the pose the first leaves on — and otherwise, or where that does not keep the survey, between the
        /// two poses one arc where that meets both, else a corner's arc and straight, a biarc, a corner between
        /// straights, or two bends and a straight, the fewest bent of as many first; or nought where none does in so few.
        /// </summary>
        public int Run(int first, int last, Span<ArcSeg> into, int mostPieces, bool freeStart, bool freeEnd)
        {
            var (from, to) = (_line[first], _line[last]);
            var toRad = to.HeadingAtRad(to.LengthM);
            var stretchM = Stretch(first, last);
            var mayM = MayStandOffM(first, last);

            if (freeStart)
            {
                // Laid back from the pose the line goes on in to the place it starts, and turned round.
                var back = Spline.ArcThrough(to.EndM, toRad + MathF.PI, from.StartM);
                into[0] = new ArcSeg(from.StartM, Spline.WrapRad(back.HeadingAtRad(back.LengthM) + MathF.PI), back.LengthM, -back.Curvature);
                if (Keeps(into[..1], stretchM, mayM)) return 1;
            }

            if (freeEnd)
            {
                into[0] = Spline.ArcThrough(from.StartM, from.HeadingRad, to.EndM);
                if (Keeps(into[..1], stretchM, mayM)) return 1;
            }

            into[0] = Spline.ArcThrough(from.StartM, from.HeadingRad, to.EndM);
            if (Arrives(into[0], toRad) && Keeps(into[..1], stretchM, mayM)) return 1;
            if (mostPieces < 2) return 0;

            // A road is designed as straights and the bends between them: the run's own first and last bends, a step
            // tighter or wider, with the straight that is tangent to both — or, at a free end, from the end onto one,
            // which is as few pieces as a biarc with one of them straight.
            var (firstBend, lastBend) = (Bend(first, last, forward: true), Bend(first, last, forward: false));
            foreach (var scale in BendScales)
            {
                if (freeStart && lastBend != 0f && StraightThenArc(from.StartM, to.EndM, toRad, lastBend * scale, into) && Keeps(into[..2], stretchM, mayM)) return 2;
                if (freeEnd && firstBend != 0f && ArcThenStraight(from.StartM, from.HeadingRad, firstBend * scale, to.EndM, into) && Keeps(into[..2], stretchM, mayM)) return 2;
            }

            if (Corner(from.StartM, from.HeadingRad, to.EndM, toRad, float.PositiveInfinity, into) == 2 && Keeps(into[..2], stretchM, mayM)) return 2;

            if (Spline.BiarcInto(from.StartM, from.HeadingRad, to.EndM, toRad, into) == 2 && Arrives(into[1], toRad) && Keeps(into[..2], stretchM, mayM)) return 2;

            var chordM = Vector2.Distance(from.StartM, to.EndM);
            for (var step = 1; step < BiarcSteps; step++)
            {
                if (Biarc(from.StartM, from.HeadingRad, to.EndM, toRad, chordM * step / BiarcSteps, into) && Keeps(into[..2], stretchM, mayM)) return 2;
            }

            if (mostPieces < 3) return 0;

            // Rounded to pass as far inside where the two lines meet as the run itself does, a step tighter or wider:
            // an arc turning θ passes d inside its tangents' meeting d · cot(θ / 4) along them from it.
            if (Meet(from.StartM, from.HeadingRad, to.EndM, toRad, out var leavingM, out _))
            {
                var meetingM = from.StartM + (Heading.Unit(from.HeadingRad) * leavingM);
                var insideM = OffRunM(_line[first..(last + 1)], meetingM, _startM[last + 1] - _startM[first]);
                var tangentM = insideM / MathF.Tan(MathF.Abs(Spline.WrapRad(toRad - from.HeadingRad)) * 0.25f);
                foreach (var scale in BendScales)
                {
                    if (Corner(from.StartM, from.HeadingRad, to.EndM, toRad, tangentM * scale, into) == 3 && Keeps(into[..3], stretchM, mayM)) return 3;
                }
            }

            if (firstBend == 0f || lastBend == 0f) return 0;

            foreach (var firstScale in BendScales)
            {
                foreach (var lastScale in BendScales)
                {
                    if (ArcStraightArc(from.StartM, from.HeadingRad, firstBend * firstScale, to.EndM, toRad, lastBend * lastScale, into)
                        && Keeps(into[..3], stretchM, mayM)) return 3;
                }
            }

            return 0;
        }

        /// <summary>The curvature of the first piece of a run that bends, from its start or its end — nought where none does.</summary>
        float Bend(int first, int last, bool forward)
        {
            for (var piece = forward ? first : last; piece >= first && piece <= last; piece += forward ? 1 : -1)
            {
                if (MathF.Abs(_line[piece].Curvature) * _leastRadiusM > StraightBend) return _line[piece].Curvature;
            }

            return 0f;
        }

        /// <summary>How far a run in place of these pieces may stand off: the tolerance, or as far as any of them did.</summary>
        float MayStandOffM(int first, int last)
        {
            var mayM = _toleranceM;
            for (var piece = first; piece <= last; piece++) mayM = MathF.Max(mayM, _laidOffM[piece]);
            return mayM;
        }

        bool Keeps(ReadOnlySpan<ArcSeg> run, ReadOnlySpan<Vector2> stretchM, float mayM) =>
            Laid(run) && OffM(run, stretchM, mayM) <= mayM;

        /// <summary>
        /// Whether a run is laid at all: none of it tighter than the least radius, turning half a turn, or off the map
        /// — a biarc between two poses on a road along the map's edge bows past it by as much as it stands off the
        /// survey.
        /// </summary>
        bool Laid(ReadOnlySpan<ArcSeg> run)
        {
            foreach (var piece in run)
            {
                if (!(piece.LengthM > 0f) || MathF.Abs(piece.Curvature) * _leastRadiusM > 1f) return false;
                if (MathF.Abs(piece.Curvature * piece.LengthM) >= MathF.PI || !OnTheMap(piece)) return false;
            }

            return true;
        }

        /// <summary>
        /// Whether a piece stands on the map its whole length: its two ends, and each place an arc runs square to an
        /// axis between them, which is where it stands furthest along the other.
        /// </summary>
        bool OnTheMap(in ArcSeg piece)
        {
            if (!OnTheMap(piece.StartM) || !OnTheMap(piece.EndM)) return false;
            if (piece.Curvature == 0f) return true;

            var roundM = 2f * MathF.PI / MathF.Abs(piece.Curvature);
            for (var quarter = 0; quarter < 4; quarter++)
            {
                var alongM = (((quarter * MathF.PI * 0.5f) - piece.HeadingRad) / piece.Curvature) % roundM;
                if (alongM < 0f) alongM += roundM;
                if (alongM <= piece.LengthM && !OnTheMap(piece.PointAtM(alongM))) return false;
            }

            return true;
        }

        bool OnTheMap(Vector2 pointM) => pointM.X >= 0f && pointM.Y >= 0f && pointM.X <= _mapM.X && pointM.Y <= _mapM.Y;

        /// <summary>
        /// How far a run and a stretch of the survey stand apart, read both ways — or anything past
        /// <paramref name="stopM"/> as soon as one is.
        /// </summary>
        /// <remarks>
        /// <b>The middle first</b>: a run keeps the poses at its two ends, so where it leaves the survey furthest is
        /// toward its middle, and a run that does not keep it is mostly told so by one reading.
        /// </remarks>
        static float OffM(ReadOnlySpan<ArcSeg> run, ReadOnlySpan<Vector2> stretchM, float stopM)
        {
            var runM = Spline.TotalLengthM(run);
            var furthestM = OffRunM(run, stretchM[stretchM.Length / 2], runM);
            if (furthestM > stopM) return furthestM;

            for (var point = 0; point < stretchM.Length; point++)
            {
                furthestM = MathF.Max(furthestM, OffRunM(run, stretchM[point], runM));
                if (point > 0) furthestM = MathF.Max(furthestM, OffRunM(run, (stretchM[point - 1] + stretchM[point]) * 0.5f, runM));
                if (furthestM > stopM) return furthestM;
            }

            var leg = 1;
            foreach (var piece in run)
            {
                for (var sample = 0; sample <= SamplesAnArc; sample++)
                {
                    furthestM = MathF.Max(furthestM, StretchOffM(piece.PointAtM(piece.LengthM * sample / SamplesAnArc), stretchM, ref leg));
                    if (furthestM > stopM) return furthestM;
                }
            }

            return furthestM;
        }

        /// <summary>
        /// The surveyed line between pieces <paramref name="first"/> and <paramref name="last"/>: the points standing
        /// along them, and where the legs either side cross their two ends, placed along the leg as far as the end's
        /// distance is between its two points'.
        /// </summary>
        Vector2[] Stretch(int first, int last)
        {
            var (fromM, toM) = (_startM[first], _startM[last + 1]);
            var (lo, hi) = (0, _surveyedM.Length - 1);
            while (lo < hi && _alongM[lo + 1] <= fromM) lo++;
            while (hi > lo && _alongM[hi - 1] >= toM) hi--;

            var stretchM = new Vector2[hi - lo + 1];
            _surveyedM[lo..(hi + 1)].CopyTo(stretchM);
            if (stretchM.Length > 1)
            {
                stretchM[0] = Between(lo, fromM);
                stretchM[^1] = Between(hi - 1, toM);
            }

            return stretchM;
        }

        Vector2 Between(int point, float atM)
        {
            var spanM = _alongM[point + 1] - _alongM[point];
            var along = spanM > 0f ? Math.Clamp((atM - _alongM[point]) / spanM, 0f, 1f) : 0f;
            return Vector2.Lerp(_surveyedM[point], _surveyedM[point + 1], along);
        }

        static float OffRunM(ReadOnlySpan<ArcSeg> run, Vector2 pointM, float runM)
        {
            Spline.ProjectM(run, pointM, 0f, runM, out var offSq);
            return MathF.Sqrt(offSq);
        }

        /// <summary>
        /// How far a place stands off a stretch of the survey, read from the leg the last place along the run was
        /// nearest and on while the legs come nearer — so a run is read against the survey once, and where it stops
        /// short of the nearest leg it reads further off than it is, never nearer.
        /// </summary>
        static float StretchOffM(Vector2 pointM, ReadOnlySpan<Vector2> stretchM, ref int leg)
        {
            if (stretchM.Length == 1) return Vector2.Distance(pointM, stretchM[0]);

            leg = Math.Clamp(leg, 1, stretchM.Length - 1);
            var offM = LegOffM(pointM, stretchM[leg - 1], stretchM[leg]);
            while (leg + 1 < stretchM.Length)
            {
                var onM = LegOffM(pointM, stretchM[leg], stretchM[leg + 1]);
                if (onM > offM) break;

                (offM, leg) = (onM, leg + 1);
            }

            return offM;
        }

        static float LegOffM(Vector2 pointM, Vector2 fromM, Vector2 toM)
        {
            var legM = toM - fromM;
            var lengthSq = legM.LengthSquared();
            var along = lengthSq > 0f ? Math.Clamp(Vector2.Dot(pointM - fromM, legM) / lengthSq, 0f, 1f) : 0f;
            return Vector2.Distance(pointM, fromM + (legM * along));
        }
    }
}
