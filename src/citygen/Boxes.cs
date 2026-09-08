using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>One junction's own ground as one closed outline</b>: across each arm where the box takes over from
/// it, and between the arms along the kerb — the pavement's own inner edge, run by run and turn by turn
/// (<see cref="Paving.Next"/>), which is the line the kerb stroke is laid on.
/// </summary>
internal readonly record struct PavedBox(int Junction, ArcSeg[] Outline);

/// <summary>
/// <b>One side of one road over a stretch that is the box's on the other side</b>: where one of an arm's
/// two sides gives way to the box further out than the other, the arm is cut at the further, and the
/// nearer side's kerb runs on into the box past the cut with its own concrete beside it.
/// <see cref="Side"/> is in the road's own frame, positive to its right.
/// </summary>
internal readonly record struct PavedStub(int Road, float FromM, float ToM, float Side);

/// <summary>
/// <b>Where each arm stops being its own cross-section and the box begins</b>, and the box that is left
/// between the arms (TER-7b). A junction has no shape in the answer (TER-5); what has one is the picture,
/// which has to cover the ground inside a box exactly once, and the arms' sections run to the node and
/// crossed one another there.
/// </summary>
/// <remarks>
/// <para>
/// <b>The box's edge between two arms is the kerb the pavement already has there.</b> Each arm's side is
/// wrapped by a run of the pavement that stops where the box takes over — a fillet, a movement swinging
/// out past the kerb, whatever stands there — and hands over to the run that wraps that
/// (<see cref="Paving.Next"/>), turn by turn round to the next arm. Half a walk in from those runs is the
/// kerb line, and the box is what the kerb encloses. Read off the pavement rather than off the corners
/// the plan carries, the box is right wherever the pavement is, and a movement that pokes out past a
/// fillet's arc is inside it.
/// </para>
/// <para>
/// <b>An arm is cut where its further side gives way</b>: cut at the nearer, the section would run into
/// the box; cut at the further, the nearer side's kerb runs on past the cut and the concrete beside it is
/// laid on its own (<see cref="PavedStub"/>).
/// </para>
/// </remarks>
internal static class Boxes
{
    /// <summary>One road as it leaves one junction, measured from the end that stands at the node.</summary>
    readonly record struct Arm(int Road, bool AtStart, float HalfM, float LengthM, float BearingRad);

    /// <summary>
    /// How many hand-overs a box's edge may be walked through between two arms before it is given up:
    /// a corner is a handful of runs, and a walk that has not come round by then has gone astray.
    /// </summary>
    const int LongestWayRound = 64;

    /// <summary>
    /// How nearly opposite a short piece and the kerb beside it have to run for the piece to be a step
    /// back along that kerb rather than across it: the cosine of the angle between them, and a step within
    /// a few degrees of straight back is one. A step square across the kerb — two kerbs a place apart, one
    /// line with a jog in it — scores nought here and is kept.
    /// </summary>
    const float StraightBack = -0.95f;

    /// <param name="enterM">Written: how far into each road's start its box reaches, nought where none does.</param>
    /// <param name="exitM">Written: the same at each road's end.</param>
    /// <param name="stubs">Written: the sides that run on past a cut.</param>
    public static PavedBox[] Lay(
        GroundPieces pieces, SimConfig config, bool[] through, PavedRun[] walk, ArcSeg[][] corners, int[] next,
        int[] turnFrom, float halfWalkM, float[] enterM, float[] exitM, List<PavedStub> stubs)
    {
        var roads = pieces.Roads;
        var reachM = RoadCuts.ReachesM(pieces, config);
        var arms = new List<Arm>[pieces.Junctions.Count];
        for (var junction = 0; junction < arms.Length; junction++) arms[junction] = [];

        for (var road = 0; road < roads.Count; road++)
        {
            var chain = roads.SegmentsOf(road);
            if (chain.Length == 0) continue;

            var lengthM = Spline.TotalLengthM(chain);
            var halfM = roads.WidthM[road] * 0.5f;
            var from = Spline.SampleAt(chain, 0f).Direction;
            var to = -Spline.SampleAt(chain, lengthM).Direction;
            arms[roads.FromJunction[road]].Add(new Arm(road, true, halfM, lengthM, MathF.Atan2(from.Y, from.X)));
            arms[roads.ToJunction[road]].Add(new Arm(road, false, halfM, lengthM, MathF.Atan2(to.Y, to.X)));
        }

        var boxes = new List<PavedBox>();
        var outline = new List<ArcSeg>();
        var cuts = new List<(int Road, bool AtStart, float CutM)>();
        var laid = new List<PavedStub>();
        for (var junction = 0; junction < arms.Length; junction++)
        {
            if (arms[junction].Count < 2 || through[junction]) continue;

            // Nothing of a box stands further from its node than the box reaches along its arms and the
            // walk beside them: a run's end further off than that is some other place's, and a walk of the
            // kerb that gets that far has left the corner and gone off along a street.
            var farM = reachM[junction] + (halfWalkM * 2f);
            arms[junction].Sort((a, b) => a.BearingRad.CompareTo(b.BearingRad));
            outline.Clear();
            cuts.Clear();
            laid.Clear();
            LayOne(pieces, pieces.Junctions.CentreM[junction], farM, arms[junction], walk, corners, next, turnFrom, halfWalkM, outline, cuts, laid);
            TakeBackTheSteps(outline, halfWalkM);
            Weld(outline);

            // <b>An outline that crosses itself is no box.</b> Two arms that meet a step apart and turn no
            // corner, or three of which two all but run on from one another, cut one another at the node
            // and enclose nothing; there the arms meet as they always did, and the picture is painted over
            // itself in one sliver rather than cut into ears that are not there.
            if (outline.Count < 3 || CrossesItself(outline)) continue;

            boxes.Add(new PavedBox(junction, outline.ToArray()));
            foreach (var (road, atStart, cutM) in cuts)
            {
                if (atStart) enterM[road] = MathF.Max(enterM[road], cutM);
                else exitM[road] = MathF.Max(exitM[road], cutM);
            }

            stubs.AddRange(laid);
        }

        return boxes.ToArray();
    }

    /// <summary>
    /// <b>A step back along the kerb is taken out, and the kerb it doubles back along is cut short by it.</b>
    /// Two runs that carry on from one another overlap by up to a place at their weld
    /// (<see cref="Kerbs.OnePlaceM"/>), so the turn between them (<c>Corner.To</c>) is a straight step from
    /// the one kerb's end back to the other's start — along the kerb and not across it. Walked into the
    /// outline as laid, that much of the kerb is laid out, back and out again, and the second and third stand
    /// a hair off the first: an outline that crosses itself at the corner of every crossroads in a laid city,
    /// and no box at any of them.
    /// </summary>
    /// <remarks>
    /// A step is straight, no longer than the half a walk two ends are bridged across
    /// (<see cref="LooseEndBeside"/>), and runs within a few degrees of straight back along the piece before
    /// it or the piece after it (<see cref="StraightBack"/>) — which no piece of a kerb ever does. A step
    /// square across the kerb is not one, and stays: it is two kerbs a place apart joined as one line with a
    /// jog in it. A step longer than the piece it doubles back along is left alone, since what it says about
    /// the outline is not that two pieces overlap.
    /// </remarks>
    static void TakeBackTheSteps(List<ArcSeg> outline, float halfWalkM)
    {
        for (var at = 0; at < outline.Count && outline.Count > 2;)
        {
            var step = outline[at];
            if (step.LengthM > halfWalkM || MathF.Abs(step.Curvature) > 0f)
            {
                at++;
                continue;
            }

            var before = (at + outline.Count - 1) % outline.Count;
            var after = (at + 1) % outline.Count;
            var previous = outline[before];
            var following = outline[after];

            if (Vector2.Dot(step.StartUnit, Heading.Unit(previous.HeadingAtRad(previous.LengthM))) <= StraightBack
                && previous.LengthM > step.LengthM + Kerbs.RoundingM)
            {
                outline[before] = previous with { LengthM = previous.LengthM - step.LengthM };
                outline.RemoveAt(at);
                continue;
            }

            if (Vector2.Dot(step.StartUnit, following.StartUnit) <= StraightBack
                && following.LengthM > step.LengthM + Kerbs.RoundingM)
            {
                outline[after] = new ArcSeg(
                    following.PointAtM(step.LengthM), following.HeadingAtRad(step.LengthM),
                    following.LengthM - step.LengthM, following.Curvature);
                outline.RemoveAt(at);
                continue;
            }

            at++;
        }
    }

    /// <summary>
    /// <b>Two pieces that all but meet are made to meet</b>: a piece no longer than the figure that makes two
    /// pieces one line (<see cref="Kerbs.JoinedM"/>) is dropped, and a piece starting that near the end of the
    /// one before it starts there. A kerb that resumes a millimetre behind the line of the piece before it
    /// crosses that line by a hair on its way out, and a hair is enough to be no box.
    /// </summary>
    static void Weld(List<ArcSeg> outline)
    {
        for (var at = outline.Count - 1; at >= 0 && outline.Count > 2; at--)
        {
            if (outline[at].LengthM <= Kerbs.JoinedM) outline.RemoveAt(at);
        }

        for (var at = 0; at < outline.Count; at++)
        {
            var endM = outline[(at + outline.Count - 1) % outline.Count].EndM;
            var apartM = Vector2.DistanceSquared(outline[at].StartM, endM);
            if (apartM > 0f && apartM <= Kerbs.JoinedM * Kerbs.JoinedM) outline[at] = outline[at] with { StartM = endM };
        }
    }

    /// <summary>Whether an outline, walked at half a walk's pitch, crosses itself anywhere.</summary>
    static bool CrossesItself(List<ArcSeg> outline)
    {
        var pointsM = new List<Vector2>();
        foreach (var arc in outline)
        {
            var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / Kerbs.OnePlaceM));
            for (var step = 0; step < steps; step++) pointsM.Add(arc.PointAtM(arc.LengthM * step / steps));
        }

        for (var i = 0; i < pointsM.Count; i++)
        {
            for (var j = i + 2; j < pointsM.Count; j++)
            {
                if (i == 0 && j == pointsM.Count - 1) continue;
                if (Cross(pointsM[i], pointsM[(i + 1) % pointsM.Count], pointsM[j], pointsM[(j + 1) % pointsM.Count])) return true;
            }
        }

        return false;
    }

    static bool Cross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var d1 = Turn(c, d, a);
        var d2 = Turn(c, d, b);
        var d3 = Turn(a, b, c);
        var d4 = Turn(a, b, d);
        return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
    }

    static float Turn(Vector2 fromM, Vector2 toM, Vector2 pointM) =>
        ((toM.X - fromM.X) * (pointM.Y - fromM.Y)) - ((toM.Y - fromM.Y) * (pointM.X - fromM.X));

    static void LayOne(
        GroundPieces pieces, Vector2 nodeM, float farM, List<Arm> arms, PavedRun[] walk, ArcSeg[][] corners,
        int[] next, int[] turnFrom, float halfWalkM, List<ArcSeg> outline, List<(int Road, bool AtStart, float CutM)> cuts,
        List<PavedStub> stubs)
    {
        var count = arms.Count;

        // Where each arm's two sides give way, right and left as seen looking out from the node: the end
        // of the run that wraps that side nearest the node, and how far out its kerb stops.
        var right = new int[count];
        var left = new int[count];
        var rightM = new float[count];
        var leftM = new float[count];
        var cutM = new float[count];
        for (var at = 0; at < count; at++)
        {
            right[at] = NearestEnd(pieces, arms[at], walk, 1f, halfWalkM, farM, out rightM[at]);
            left[at] = NearestEnd(pieces, arms[at], walk, -1f, halfWalkM, farM, out leftM[at]);
            cutM[at] = MathF.Min(MathF.Max(rightM[at], leftM[at]), arms[at].LengthM);

            var arm = arms[at];
            cuts.Add((arm.Road, arm.AtStart, cutM[at]));

            foreach (var (alongM, side) in (ReadOnlySpan<(float, float)>)[(rightM[at], 1f), (leftM[at], -1f)])
            {
                if (alongM >= cutM[at]) continue;

                var fromM = RoadM(arm, cutM[at]);
                var toM = RoadM(arm, alongM);
                stubs.Add(new PavedStub(arm.Road, MathF.Min(fromM, toM), MathF.Max(fromM, toM), side * (arm.AtStart ? 1f : -1f)));
            }
        }

        // The outline, arm by arm the way round the bearings go: across the arm at its cut, in along its
        // right kerb to where that side's run stops, along the kerb from run to run and turn to turn round
        // to where the next arm's left run stops, and out along that kerb to the next arm's cut.
        for (var at = 0; at < count; at++)
        {
            var nextArm = (at + 1) % count;
            var arm = arms[at];
            Straight(outline, Kerb(pieces, arm, cutM[at], -1f), Kerb(pieces, arm, cutM[at], 1f));
            Stub(pieces, arm, cutM[at], rightM[at], 1f, outline);
            var fromM = right[at] == CityPlan.NoRecord ? Kerb(pieces, arm, rightM[at], 1f) : KerbEndM(walk, right[at], halfWalkM);
            var toM = left[nextArm] == CityPlan.NoRecord ? Kerb(pieces, arms[nextArm], leftM[nextArm], -1f) : KerbEndM(walk, left[nextArm], halfWalkM);
            RoundTheCorner(walk, corners, next, turnFrom, halfWalkM, nodeM, farM, right[at], left[nextArm], fromM, toM, outline);
            Stub(pieces, arms[nextArm], leftM[nextArm], cutM[nextArm], -1f, outline);
        }
    }

    /// <summary>
    /// The kerb from one run's end round to another's: each turn laid from the end reached, and each run
    /// between them as its own line half a walk in on the road's side, walked the way the outline goes.
    /// Where the way round is lost — an end nothing hands over from, a run along a street that is not the
    /// one being made for, a kerb further from the node than the box reaches — the edge is closed straight
    /// to where it was going, which is wrong in one place rather than a box missing.
    /// </summary>
    static void RoundTheCorner(
        PavedRun[] walk, ArcSeg[][] corners, int[] next, int[] turnFrom, float halfWalkM, Vector2 nodeM, float farM,
        int fromEnd, int toEnd, Vector2 fromM, Vector2 toM, List<ArcSeg> outline)
    {
        var laid = outline.Count;
        var end = fromEnd;
        for (var step = 0; step < LongestWayRound && end != CityPlan.NoRecord && toEnd != CityPlan.NoRecord; step++)
        {
            var onto = next[end];
            var bridged = onto == CityPlan.NoRecord;
            if (bridged) onto = LooseEndBeside(walk, end, halfWalkM);
            if (onto == CityPlan.NoRecord) break;

            var turn = bridged ? CityPlan.NoRecord : turnFrom[end] != CityPlan.NoRecord ? turnFrom[end] : turnFrom[onto];
            if (turn != CityPlan.NoRecord) Append(corners[turn], KerbEndM(walk, end, halfWalkM), outline);
            else Bridge(outline, KerbEndM(walk, end, halfWalkM), KerbEndM(walk, onto, halfWalkM), halfWalkM);
            if (onto == toEnd) return;
            if (walk[onto / 2].AlongARoad) break;
            if (Vector2.Distance(KerbEndM(walk, onto ^ 1, halfWalkM), nodeM) > farM) break;

            KerbOf(walk[onto / 2], halfWalkM, onto % 2 == 0, outline);
            end = onto ^ 1;
            if (end == toEnd) return;
        }

        // Lost: close straight from wherever the walk got to. Everything it laid is kerb reached end to end
        // from the arm, so what is wrong is the one straight piece and not the corner.
        Straight(outline, outline.Count > laid ? outline[^1].EndM : fromM, toM);
    }

    /// <summary>
    /// <b>The end nearest this one that nothing hands over to</b>, within half a walk, or none. Two runs the
    /// pavement leaves unpaired — a fillet's line resuming a hand's width past where the arm's stopped, or
    /// two kerbs that cross at a kink instead of leaving a wedge — still stand end to end on the ground, and
    /// the box's edge steps across between their kerbs.
    /// </summary>
    static int LooseEndBeside(PavedRun[] walk, int end, float halfWalkM)
    {
        var placeM = PlaceM(walk, end);
        var nearest = CityPlan.NoRecord;
        var nearestM = halfWalkM;
        for (var other = 0; other < walk.Length * 2; other++)
        {
            if (other / 2 == end / 2) continue;

            var apartM = Vector2.Distance(PlaceM(walk, other), placeM);
            if (apartM >= nearestM) continue;

            nearestM = apartM;
            nearest = other;
        }

        return nearest;
    }

    /// <summary>
    /// <b>The step between two kerbs nothing hands over across</b>: the kerb the walk is on carried straight
    /// on until it stands abreast of where the next one starts, and a square step across from there. The
    /// two kerbs meet at a corner and not along a chord — at a kink where a street narrows, the chord cut
    /// the corner off the box and what stood in the corner was neither tarmac nor concrete.
    /// </summary>
    /// <remarks>
    /// Where the next kerb starts behind the end of this one, or further ahead than half a walk, the two are
    /// joined straight instead; a step back along the kerb is taken out afterwards
    /// (<see cref="TakeBackTheSteps"/>).
    /// </remarks>
    static void Bridge(List<ArcSeg> outline, Vector2 fromM, Vector2 toM, float halfWalkM)
    {
        if (outline.Count > 0)
        {
            var last = outline[^1];
            var along = Heading.Unit(last.HeadingAtRad(last.LengthM));
            var aheadM = Vector2.Dot(toM - fromM, along);
            if (aheadM > Kerbs.JoinedM && aheadM <= halfWalkM)
            {
                var abreastM = fromM + (along * aheadM);
                Straight(outline, fromM, abreastM);
                Straight(outline, abreastM, toM);
                return;
            }
        }

        Straight(outline, fromM, toM);
    }

    static Vector2 PlaceM(PavedRun[] walk, int end)
    {
        var run = walk[end / 2];
        return end % 2 == 0 ? run.Line[0].StartM : run.Line[^1].EndM;
    }

    /// <summary>
    /// A turn laid the way the outline goes: forwards where it starts at the kerb the walk stands at, and
    /// the other way where it ends there — a turn is laid from whichever of its two ends sets off, and a
    /// straight step between two kerbs from whichever keeps the concrete on its across side.
    /// </summary>
    static void Append(ArcSeg[] turn, Vector2 fromM, List<ArcSeg> outline)
    {
        if (Vector2.DistanceSquared(turn[0].StartM, fromM) <= Vector2.DistanceSquared(turn[^1].EndM, fromM))
        {
            outline.AddRange(turn);
            return;
        }

        Span<ArcSeg> back = stackalloc ArcSeg[turn.Length];
        Spline.ReverseInto(turn, back);
        foreach (var arc in back) outline.Add(arc);
    }

    /// <summary>One run's kerb: its line half a walk in on the road's side, from its start or from its end.</summary>
    static void KerbOf(in PavedRun run, float halfWalkM, bool fromStart, List<ArcSeg> outline)
    {
        Span<ArcSeg> kerb = stackalloc ArcSeg[run.Line.Length];
        Spline.OffsetInto(run.Line, run.RoadSide * halfWalkM, kerb);
        if (fromStart)
        {
            foreach (var arc in kerb) outline.Add(arc);
            return;
        }

        Span<ArcSeg> back = stackalloc ArcSeg[run.Line.Length];
        Spline.ReverseInto(kerb, back);
        foreach (var arc in back) outline.Add(arc);
    }

    /// <summary>Where a run's kerb stops at one of its ends: the place, half a walk to the road's side.</summary>
    static Vector2 KerbEndM(PavedRun[] walk, int end, float halfWalkM)
    {
        var run = walk[end / 2];
        var atStart = end % 2 == 0;
        var arc = atStart ? run.Line[0] : run.Line[^1];
        var placeM = atStart ? arc.StartM : arc.EndM;
        var unit = Heading.Unit(atStart ? arc.HeadingRad : arc.HeadingAtRad(arc.LengthM));
        return placeM + (Heading.RightOf(unit) * run.RoadSide * halfWalkM);
    }

    /// <summary>
    /// The end, nearest the node, of a run that wraps one of an arm's sides, and how far out from the node
    /// its kerb stops there — nought, and no end, where no run wraps that side.
    /// </summary>
    /// <remarks>
    /// <b>A run along the side, and not one turning round the road's end.</b> What survives of the line round
    /// an arm's end at a kinked through road is a jog a hand wide at the node, on whichever side the rounding
    /// gave it; read as the side's own run it stood at the node, and the arm was cut there with the fillet
    /// nine metres out.
    /// </remarks>
    static int NearestEnd(
        GroundPieces pieces, in Arm arm, PavedRun[] walk, float side, float halfWalkM, float farM, out float alongM)
    {
        // A run's road lies to the run's right where RoadSide is positive, so a run on the road's own left
        // has the road to its right; looking out from the node that is the left side only where the arm
        // starts at the node.
        var roadSide = side * (arm.AtStart ? -1f : 1f);
        var chain = pieces.Roads.SegmentsOf(arm.Road);
        var nearest = CityPlan.NoRecord;
        alongM = 0f;
        var nearestM = float.MaxValue;
        for (var run = 0; run < walk.Length; run++)
        {
            if (walk[run].Road != arm.Road || walk[run].RoadSide != roadSide || !walk[run].AlongARoad) continue;

            for (var end = run * 2; end < (run * 2) + 2; end++)
            {
                var kerbM = KerbEndM(walk, end, halfWalkM);
                var roadM = Spline.ProjectM(chain, kerbM, RoadM(arm, 0f), arm.LengthM);
                var outM = arm.AtStart ? roadM : arm.LengthM - roadM;
                if (outM >= nearestM || outM > farM) continue;

                nearestM = outM;
                nearest = end;
                alongM = outM;
            }
        }

        return nearest;
    }

    /// <summary>
    /// The stretch of one of an arm's kerbs between two distances from the node, laid from the first to the
    /// second — inward where the first is the further out.
    /// </summary>
    static void Stub(GroundPieces pieces, in Arm arm, float fromM, float toM, float side, List<ArcSeg> outline)
    {
        if (MathF.Abs(toM - fromM) <= Kerbs.RoundingM) return;

        var chain = pieces.Roads.SegmentsOf(arm.Road);
        var lowM = RoadM(arm, MathF.Min(fromM, toM));
        var highM = RoadM(arm, MathF.Max(fromM, toM));
        Span<ArcSeg> piece = stackalloc ArcSeg[chain.Length];
        var laid = Spline.SubChainInto(chain, MathF.Min(lowM, highM), MathF.Max(lowM, highM), piece);
        if (laid == 0) return;

        Span<ArcSeg> offset = stackalloc ArcSeg[laid];
        Spline.OffsetInto(piece[..laid], side * (arm.AtStart ? 1f : -1f) * arm.HalfM, offset);

        // The road runs one way and the outline is walked the other where the two disagree: outward along
        // the road is outward from the node only where the arm starts there.
        var outward = fromM < toM;
        if (outward == arm.AtStart)
        {
            foreach (var arc in offset) outline.Add(arc);
            return;
        }

        Span<ArcSeg> back = stackalloc ArcSeg[laid];
        Spline.ReverseInto(offset, back);
        foreach (var arc in back) outline.Add(arc);
    }

    static void Straight(List<ArcSeg> outline, Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        var lengthM = runM.Length();
        if (lengthM <= Kerbs.RoundingM) return;

        outline.Add(new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), lengthM, 0f));
    }

    /// <summary>A distance out from the node as a distance along the road.</summary>
    static float RoadM(in Arm arm, float alongM) => arm.AtStart ? alongM : arm.LengthM - alongM;

    /// <summary>The point on one of an arm's kerbs a distance out from the node, right or left looking out.</summary>
    static Vector2 Kerb(GroundPieces pieces, in Arm arm, float alongM, float side)
    {
        var on = Spline.SampleAt(pieces.Roads.SegmentsOf(arm.Road), RoadM(arm, alongM));
        var outward = arm.AtStart ? on.Direction : -on.Direction;
        return on.PositionM + (Heading.RightOf(outward) * side * arm.HalfM);
    }
}
