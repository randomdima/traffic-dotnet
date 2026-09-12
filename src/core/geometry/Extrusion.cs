using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>The line that stands a fixed distance to one side of a closed ring</b>: the ring walked at stations,
/// each station moved sideways by the offset and every corner it turns away from brought round at that offset
/// (<see cref="Round"/>), whatever the move folded through the ring dropped, and what is left smoothed over
/// a window and laid back as a chain.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is not <see cref="Spline.OffsetInto"/>, and the difference is the whole of this file.</b> Offsetting
/// a chain piece by piece moves every piece and asks nothing about the rest of the line, which is exactly
/// right for a lane inside its own road and wrong for a ring: a corner turned the other way sweeps the
/// offset <em>through</em> the ring and comes back as a loop standing inside the shape, and two sides of a
/// gap narrower than twice the offset each produce a line past the other. What makes the answer a boundary
/// rather than a heap of offset pieces is the one rule <see cref="Of"/> enforces — <b>no point of it stands
/// nearer the ring than the offset</b> — and a point that breaks it is dropped rather than trimmed, so the
/// line closes across the fold instead of doubling back through it.
/// </para>
/// <para>
/// <b>Walked and not solved.</b> Where two offset pieces really cross is a solvable question and pairing up
/// which crossings bound the answer is not, so the distance rule is asked of stations along the line instead:
/// it needs no case for a fold inside a fold, and it says the same thing about a ring of ten pieces and a
/// ring of ten thousand. What it costs is that the answer stands within a station of where the fold really
/// closes, which is a centimetre or two on a line that exists to be looked at.
/// </para>
/// <para>
/// <b>The gap a dropped fold leaves is traced, though</b> (<see cref="Traced"/>), and that is not the same
/// thing as solving the crossing. The answer across such a gap is a curve, both ends of it are already
/// known, and the field says where it goes at every point — so it is followed a station at a time rather
/// than guessed at with a straight. Nothing here pairs up a crossing to do it.
/// </para>
/// <para>
/// <b>Smoothing is a window and not a fillet</b>: each station is replaced by the mean of the line within
/// half a window either side of it, so <em>nothing the line does over less than the window survives</em> —
/// a corner the ring turned, and equally the notch left where a fold was dropped, which a corner rounding
/// would leave exactly as it found it. The line it leaves stands a little inside the offset on a bend, by
/// about <c>w²/24R</c>, which at a window of metres and a bend of tens of them is millimetres.
/// </para>
/// </remarks>
internal static class Extrusion
{
    /// <summary>
    /// How finely the ring is walked. A quarter of a metre: the fold the distance rule drops is bounded by
    /// the station, and everything this line is laid against — a kerb, a pavement, a fence — is drawn at
    /// centimetres.
    /// </summary>
    const float StationM = 0.25f;

    /// <summary>
    /// How far inside the offset a station may stand and still be kept. <b>It is a rounding and not a
    /// slack</b>: a point of the offset stands at exactly the offset from its own foot, and what this
    /// covers is that the ring is measured at stations rather than continuously. Read as a slack — a tenth
    /// of the offset, say — the line keeps a bite out of every corner it turns.
    /// </summary>
    const float FoldM = 0.02f;

    /// <summary>
    /// How far the laid chain may bow off the stations it was walked from. Two centimetres: the line is a
    /// polyline by construction and this is what decides how many pieces say so — a hundred for a ring
    /// round a town rather than the ten thousand stations it was walked at.
    /// </summary>
    const float SagM = 0.02f;

    /// <summary>Below this two stations are one place and the second lays no piece.</summary>
    const float ApartM = 1e-3f;

    /// <summary>What no grid may exceed however far a ring is spread: the cell grows instead.</summary>
    const int MostCells = 1 << 22;

    /// <summary>
    /// <b>The ring that stands <paramref name="offsetM"/> to one side of this one</b> — positive to the
    /// driver's right, as <see cref="Spline.OffsetInto"/> is — smoothed over <paramref name="smoothM"/>
    /// metres of its own length. Empty where the offset leaves nothing: a ring smaller than the offset it
    /// was moved inside of is not a ring.
    /// </summary>
    /// <remarks>
    /// <paramref name="ring"/> is read as closed, whether or not its last piece ends exactly where its
    /// first begins, and what comes back is closed.
    /// </remarks>
    public static ArcSeg[] Of(ReadOnlySpan<ArcSeg> ring, float offsetM, float smoothM)
    {
        if (ring.Length == 0) return [];

        ReadOnlySpan<ArcSeg[]> rings = [ring.ToArray()];
        ReadOnlySpan<float[]> standsOffM = [new float[ring.Length]];
        var extruded = Of(rings, standsOffM, MathF.Abs(offsetM), smoothM, toTheRight: offsetM > 0f);
        return extruded.Length > 0 ? extruded[0] : [];
    }

    /// <summary>
    /// <b>Every ring of a set moved <paramref name="outM"/> clear of the band each piece of it lays</b> —
    /// one answer per ring given, empty where the distance left that ring nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A band per piece, because the ring is not the edge of what it encloses.</b> The ring a town's
    /// ground is said in runs down the <em>lines</em> cars are driven on rather than along the edge of what
    /// they lay (<c>LaneShell</c>), and the edge stands half a band out — a lane's width along a lane, a
    /// space's along the way into a bay. A station of the answer is therefore moved by its own band plus
    /// the distance, and <b>kept where it stands that far from every line of every ring</b>.
    /// </para>
    /// <para>
    /// <b>The rule is a clearance from every band and not a distance from every line</b>, and the
    /// difference is the whole of what the band per piece is for. A station moved off a narrow band can
    /// stand its own reach from a wide band's <em>line</em> while standing well inside the ground that band
    /// lays — a bay way beside a carriageway is exactly that — so a rule written in distances keeps
    /// stations that are in the road. Written in clearances (<see cref="RingField"/> answers it, given the
    /// bands) the rule says what it means: <b>no station of the answer stands nearer the ground than the
    /// distance</b>, whoever lays that ground.
    /// </para>
    /// <para>
    /// <b>It was the distance rule until it was measured, and the measurement is what settled it.</b> The
    /// two fail in opposite directions and a worst-case figure could not choose between them, so
    /// <c>--bench shell</c> counts the stations standing over a tenth of a metre off their own figure,
    /// splits them by whether each is nearer the kerb than the figure or further, and says which are
    /// stations of the walk rather than the middle of a straight the closure gave up and drew
    /// (<see cref="Closed"/>). <b>The stations nearer the kerb than the figure — a pavement on the road,
    /// which is the half that costs something — fell by nine in ten</b>, and the rule's own share of them,
    /// the ones standing on a station rather than mid-straight, fell from 1 886 to 4 on a city and from
    /// 1 354 to 11 on the other. What is left over is almost all the closure's.
    /// </para>
    /// <para>
    /// <b>What it costs is build time</b>: the clearance drops more stations, so there are more gaps to
    /// close and the extrusion takes about seven parts in four of what it did. It is paid once when a town
    /// is stood up and never on a tick.
    /// </para>
    /// <para>
    /// <b>The rule is asked of the whole set and not of the ring being moved</b> (<see cref="Clear"/>): a
    /// town's ground has a ring round the outside and one round every block it encloses, and a line that
    /// folded through a neighbour would otherwise stand while one that folded through itself was dropped.
    /// One field over every station of every ring answers both, and costs the same.
    /// </para>
    /// <para>
    /// Two lines struck this way are offsets of one curve by amounts differing by a constant, so <b>the band
    /// between them is exactly the difference between the distances that struck them</b> wherever the fold
    /// rule left both of them standing.
    /// </para>
    /// </remarks>
    /// <param name="toTheRight">
    /// Which side of the rings the answer is struck on. <b>It is said and not read off the sign of the
    /// distance</b>, because nought is a distance a caller asks for — the edge of the ground itself — and
    /// nought has no sign to read.
    /// </param>
    public static ArcSeg[][] Of(
        ReadOnlySpan<ArcSeg[]> rings, ReadOnlySpan<float[]> standsOffM, float outM, float smoothM,
        bool toTheRight = false)
    {
        var hand = toTheRight ? 1f : -1f;
        var clearM = outM;
        var onTheRing = new List<Vector2>[rings.Length];
        var onTheRingBandM = new List<float>[rings.Length];
        var moved = new List<Vector2>[rings.Length];
        var stations = 0;
        var deepestM = 0f;
        for (var at = 0; at < rings.Length; at++)
        {
            var ring = rings[at];
            var stands = standsOffM[at];
            var lengthM = Spline.TotalLengthM(ring);
            if (ring.Length == 0 || lengthM <= StationM * 3f) continue;

            var room = (int)MathF.Ceiling(lengthM / StationM) + ring.Length;
            onTheRing[at] = new List<Vector2>(room);
            onTheRingBandM[at] = new List<float>(room);
            moved[at] = new List<Vector2>(room);
            Walk(ring, stands, hand, clearM, onTheRing[at], onTheRingBandM[at], moved[at]);
            stations += onTheRing[at].Count;
            foreach (var bandM in stands) deepestM = MathF.Max(deepestM, bandM);
        }

        if (stations == 0) return [];

        // <b>One field over every ring of the set</b> (<see cref="RingField"/>), carrying each station's own
        // band so that what it answers is a clearance rather than a distance. It says both halves of the
        // rule at once — how far clear of the bands a moved station stands, and which side of them it
        // stands on — and it says them <em>of the set</em>, which no ring can say of itself.
        var walkedM = new Vector2[rings.Length][];
        var walkedBandM = new float[rings.Length][];
        for (var at = 0; at < rings.Length; at++)
        {
            walkedM[at] = onTheRing[at] is null ? [] : [.. onTheRing[at]];
            walkedBandM[at] = onTheRingBandM[at] is null ? [] : [.. onTheRingBandM[at]];
        }

        // Twice the deepest a station can stand off a line, because a step being held out to that is asked
        // about from further out again (<see cref="Held"/>).
        var field = new RingField(walkedM, MathF.Max((deepestM + clearM) * 2f, StationM), walkedBandM);

        var extruded = new ArcSeg[rings.Length][];
        for (var at = 0; at < rings.Length; at++)
        {
            extruded[at] = [];
            if (onTheRing[at] is null) continue;

            var kept = Clear(field, hand, clearM, [.. moved[at]]);
            if (kept.Length < 3) continue;

            var closed = Unlooped(Closed(field, hand, clearM, kept));
            if (closed.Length < 3) continue;

            extruded[at] = Straights(Corners(Smoothed(closed, smoothM)));
        }

        return extruded;
    }

    /// <summary>
    /// How far a point stands off the rings <b>on the side the offset took it</b> — which is the one figure
    /// the whole rule is written in. Negative means the move landed on the far side and the point is inside
    /// what the rings enclose, whatever its distance.
    /// </summary>
    static float OnItsSideM(RingField field, float hand, Vector2 pointM, float reachM)
    {
        var clearM = field.OffM(pointM, reachM);
        return hand < 0f ? clearM : -clearM;
    }

    /// <summary>
    /// One ring walked at stations: where each station stands on the ring, where the offset takes it, and
    /// how far that was — which <see cref="Clear"/> needs back, since a station's own offset is what says
    /// whether it is still the offset.
    /// </summary>
    static void Walk(
        ReadOnlySpan<ArcSeg> ring, ReadOnlySpan<float> standsOffM, float hand, float clearM,
        List<Vector2> onTheRing, List<float> onTheRingBandM, List<Vector2> moved)
    {
        for (var piece = 0; piece < ring.Length; piece++)
        {
            ref readonly var arc = ref ring[piece];
            var bandM = standsOffM[piece];
            var offsetM = hand * (bandM + clearM);
            var stations = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / StationM));
            var stepM = arc.LengthM / stations;
            for (var station = 0; station < stations; station++)
            {
                var atM = station * stepM;
                var pointM = arc.PointAtM(atM);
                onTheRing.Add(pointM);
                onTheRingBandM.Add(bandM);
                moved.Add(pointM + (Heading.RightOf(Heading.Unit(arc.HeadingAtRad(atM))) * offsetM));
            }

            var next = (piece + 1) % ring.Length;
            Round(arc, ring[next], hand * (standsOffM[next] + clearM), moved);
        }
    }

    /// <summary>
    /// <b>The turn the offset comes round at a corner the ring turns away from it</b>: the line swung about
    /// the corner itself at the offset, which is where the offset of a corner is. Cut off with a straight
    /// instead, a right angle loses a third of the distance the whole line exists to keep.
    /// </summary>
    /// <remarks>
    /// A corner the ring turns <em>into</em> the offset gets none: the two sides run past one another there
    /// and what settles them is the fold rule (<see cref="Clear"/>), not a line drawn between them. The
    /// corner is taken as the piece boundary itself rather than as a station, so the swing is about the
    /// point the answer is measured from and stands at the offset to the millimetre.
    /// </remarks>
    static void Round(in ArcSeg arriving, in ArcSeg leaving, float offsetM, List<Vector2> moved)
    {
        var arrivingRad = arriving.HeadingAtRad(arriving.LengthM);
        var turnRad = Spline.WrapRad(leaving.HeadingRad - arrivingRad);
        if (turnRad * offsetM >= 0f) return;

        var steps = (int)MathF.Ceiling(MathF.Abs(turnRad * offsetM) / StationM);
        for (var step = 1; step < steps; step++)
        {
            var alongRad = arrivingRad + (turnRad * step / steps);
            moved.Add(leaving.StartM + (Heading.RightOf(Heading.Unit(alongRad)) * offsetM));
        }
    }

    /// <summary>
    /// <b>The stations of the offset that are still the offset</b>, in the order they were walked: each one
    /// the distance clear of every band the rings lay, <em>on the side the distance took it</em> — and each
    /// one that is not, pushed out until it is or dropped where it cannot be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The clearance is to the rings and not to the station it came from</b>: a fold is where a far piece
    /// of the ring comes near, which nothing local can see. <b>And the side is not the clearance.</b> A ring
    /// that runs out along a line and back — a slit into the shape, which a town's outline has wherever one
    /// lane's edge doubles back on itself — turns through half a circle at the tip, and the offset comes
    /// round that turn into the shape. Every point of that turn is honestly the distance from its own band
    /// and some of them are that far from everything else too, so a clearance alone keeps them and what is
    /// drawn is a spike across ground the ring was meant to be outside of. <see cref="RingField"/> answers
    /// both in one figure.
    /// </para>
    /// <para>
    /// <b>A station the rule rejects is pushed before it is dropped</b>, and it is pushed <em>in its own
    /// place in the walk</em>. What the rule rejects is a run of stations, not a scatter of them — a whole
    /// tooth of the ring folding away, a slot too narrow for the distance to enter — and the boundary across
    /// such a run is a real line: the locus standing the distance clear of the bands either side of it.
    /// Walking that run and holding each station out to the clearance draws exactly that locus, in the order
    /// the ring walks it. <b>Closed by drawing a straight across instead, the line cut the corner</b> by
    /// however deep the run was — metres, at the mouth of a car park — and every reader of it inherited the
    /// cut.
    /// </para>
    /// </remarks>
    static Vector2[] Clear(RingField field, float hand, float clearM, Vector2[] moved)
    {
        var kept = new List<Vector2>(moved.Length);
        foreach (var atM in moved)
        {
            if (OnItsSideM(field, hand, atM, Reach(field, clearM)) < clearM - FoldM) continue;

            kept.Add(atM);
        }

        return [.. kept];
    }

    /// <summary>
    /// How far the field is asked about a station that wants to stand <paramref name="wantedM"/> off the
    /// rings — <b>never further than the field reaches</b>, since a question asked past that comes back as
    /// "nothing near", which for a station standing inside the ground is the one answer that must not be
    /// given.
    /// </summary>
    static float Reach(RingField field, float wantedM) =>
        MathF.Min(MathF.Max(wantedM, StationM) * 2f, field.CellM);

    /// <summary>
    /// <b>The gap a dropped run leaves, walked and held out to the clearance.</b> What survives the rule is
    /// stations, and a station keeps the clearance by construction — but <em>the straight between two of
    /// them does not</em>, and where a run was dropped the two that bracket it can be metres apart with the
    /// ground bulging between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the difference between the rule holding at the stations and holding at the line.</b> Read
    /// off the stations alone the line keeps its distance and every reader of it measures a chord that does
    /// not: a walking lane laid half a walk out came back over the kerb at a car park's mouth, by a metre
    /// and a half, on a line every station of which stood exactly half a walk clear.
    /// </para>
    /// <para>
    /// <b>The gap is traced and not stepped across</b> (<see cref="Traced"/>). The answer across a dropped
    /// run is a curve — the locus standing the clearance off whatever is nearest — and the station either
    /// side of the gap already stands on it, so what closes the gap is that curve followed from the one to
    /// the other: step along where it is going, put the step back on it, repeat. <b>Chord points pushed out
    /// instead were the whole of the fault</b>: a chord across the mouth of a car park runs metres inside
    /// the answer, each of its points is pushed to wherever the ground happens to be nearest rather than to
    /// where the line is going, and a run of them laid in the order the chord was walked is a star of spikes
    /// across the ground. Guarded against the spikes by dropping any step that did not carry on from the one
    /// before, what was left was the chord — and the cut it took, up to a metre and a half of it.
    /// </para>
    /// <para>
    /// <b>The trace is continuous by construction</b>, which is what the guard was standing in for: each
    /// step is one station along the curve from the last, so there is no ordering to get wrong and nothing
    /// to veto. What it cannot do is arrive — a trace that wanders, stalls or runs out of steps gives up
    /// whole — and the chord is still there for that, being no worse than what it was.
    /// </para>
    /// </remarks>
    static Vector2[] Closed(RingField field, float hand, float clearM, Vector2[] kept)
    {
        var closed = new List<Vector2>(kept.Length);
        for (var station = 0; station < kept.Length; station++)
        {
            closed.Add(kept[station]);

            var next = (station + 1) % kept.Length;
            var runM = kept[next] - kept[station];
            var gapM = runM.Length();
            if (gapM <= StationM * 2f) continue;

            var reachM = Reach(field, clearM);

            // The way the line was travelling as it arrived here, which is the one thing that says which way
            // along the curve the gap lies. Read off the two kept stations and not off the run that was
            // dropped: the raw offset heads *into* the fold at that point, and a trace seeded with it sets
            // off across the ground the fold rule had just thrown away.
            var camePast = (station - 1 + kept.Length) % kept.Length;
            var alongM = kept[station] - kept[camePast];
            if (Traced(field, hand, clearM, reachM, kept[station], kept[next], alongM, gapM, closed)) continue;

            var carriesOnM = MathF.Max(clearM, StationM) * 2f;
            var cameM = kept[station];
            var steps = (int)MathF.Ceiling(gapM / StationM);
            for (var step = 1; step < steps; step++)
            {
                var atM = kept[station] + (runM * step / steps);
                if (!Held(field, hand, clearM, reachM, ref atM)) continue;
                if (Vector2.Distance(atM, cameM) > carriesOnM) continue;

                cameM = atM;
                closed.Add(atM);
            }
        }

        return [.. closed];
    }

    /// <summary>
    /// <b>The line with its cusps taken out</b> — a station the line arrives at and leaves in opposite
    /// directions over a step or two either side, which is a loop of nothing rather than a corner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A ring is walked with the ground on its right throughout, and everything laid along one reads its
    /// own inward side off that</b> (<c>CityGen.GroundLine</c>). A tiny loop breaks it: over the two or
    /// three stations the line spends doubling back, the right of travel points out of the perimeter, and a
    /// reader taking its inward side off the line gets it inverted while the line still looks like a
    /// perfectly good closed curve. On a city that was a fifth of a per cent of the kerb's stations and
    /// two thirds of a per cent of the roadside's.
    /// </para>
    /// <para>
    /// <b>The arms are what tell a loop from a corner, not the angle.</b> The offset of a fold really does
    /// turn through most of a half circle where two branches are trimmed against one another, and that
    /// corner is the answer — so an angle alone would cut the very places the fold rule exists to find.
    /// What a genuine corner has is <em>length</em> either side of it: two branches running away from the
    /// trim. A loop the closure left has neither, being a station or two of line that goes nowhere.
    /// </para>
    /// <para>
    /// <b>One pass and not until it settles.</b> Taking a cusp out joins its two neighbours and can leave
    /// another, so running it to a fixed point is the obvious thing — and it traded one line's cleanliness
    /// for another's: on a city four passes took the roadside line's outward normals from 423 to 355 and the
    /// kerb's from 175 to 214. The kerb is the line every other distance is measured off, so it is the one
    /// to keep clean, and a single pass is what does that.
    /// </para>
    /// </remarks>
    static Vector2[] Unlooped(Vector2[] pointM)
    {
        if (pointM.Length < 3) return pointM;

        var kept = new List<Vector2>(pointM.Length);
        for (var at = 0; at < pointM.Length; at++)
        {
            var cameM = pointM[Round(at - 1, pointM.Length)] - pointM[at];
            var goesM = pointM[Round(at + 1, pointM.Length)] - pointM[at];
            var armM = MathF.Min(cameM.Length(), goesM.Length());
            if (armM > StationM * ArmStations || armM <= ApartM)
            {
                kept.Add(pointM[at]);
                continue;
            }

            // Both arms are short, so the only question left is whether the line turned round here.
            if (Vector2.Dot(cameM, goesM) < 0f) kept.Add(pointM[at]);
        }

        return kept.Count >= 3 ? [.. kept] : pointM;
    }

    /// <summary>
    /// How much line either side of a station makes its corner a corner rather than a loop. Two stations:
    /// a branch of a trimmed fold runs away from the trim for as long as the fold was deep, and a loop the
    /// closure left is the half-metre it took to double back.
    /// </summary>
    const int ArmStations = 2;

    /// <summary>
    /// <b>The answer followed from one side of a gap to the other</b>, a station at a time: step along where
    /// the curve is going, put the step back on the curve, and take the direction it actually moved as where
    /// it is going next. True if it arrived, and <b>nothing is written unless it did</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Where the curve is going is across the normal it is measured on.</b> A point of the answer stands
    /// the clearance off the nearest piece of ring, along that piece's own normal
    /// (<see cref="RingField.Nearest"/>), so the curve runs at right angles to that normal — and which of
    /// the two right angles is the one it came in on. That is a predictor and a corrector and not a guess:
    /// the step is taken along the tangent and then put back on the curve by the same push a station of the
    /// walk is (<see cref="Held"/>), so a curvature the step overshoots costs a station squared and not a
    /// station.
    /// </para>
    /// <para>
    /// <b>It has to arrive, and it is given room to go round.</b> A gap straight across is one station per
    /// station of it; a gap round the tip of a swallowed tooth is the way round, which is longer than the
    /// straight by however deep the tooth was. The budget is the straight several times over, and a trace
    /// that has not arrived by then has not understood the gap — as has one that stalls, one the field has
    /// nothing to say about, and one that doubles back on itself. All of them give up whole rather than
    /// leave half a closure behind, because half a closure is a line that stops in the middle of the ground.
    /// </para>
    /// </remarks>
    static bool Traced(
        RingField field, float hand, float wantedM, float reachM, Vector2 fromM, Vector2 toM, Vector2 alongM,
        float gapM, List<Vector2> closed)
    {
        if (alongM.LengthSquared() <= ApartM * ApartM) return false;

        var wentM = Vector2.Normalize(alongM);
        var atM = fromM;
        var laid = closed.Count;
        var budget = (int)MathF.Ceiling(gapM / StationM * Roundabout) + Roundabout;
        for (var step = 0; step < budget; step++)
        {
            if (!field.Nearest(atM, reachM, out _, out _, out var leftM, out _)) break;
            if (leftM == Vector2.Zero) break;

            // Across the normal, on the side the walk came in on. The normal is the band's own left, so the
            // hand the answer was struck on does not enter into it: both right angles to it are candidates
            // and the one the line is on is the one it was already travelling.
            var acrossM = Heading.RightOf(leftM);
            if (Vector2.Dot(acrossM, wentM) < 0f) acrossM = -acrossM;

            var nextM = atM + (acrossM * StationM);
            if (!Held(field, hand, wantedM, reachM, ref nextM)) break;

            var stepM = nextM - atM;
            if (stepM.LengthSquared() <= ApartM * ApartM) break;
            if (Vector2.Dot(stepM, wentM) <= 0f) break;

            wentM = Vector2.Normalize(stepM);
            atM = nextM;
            if (Vector2.Distance(atM, toM) <= StationM) return true;

            closed.Add(atM);
        }

        closed.RemoveRange(laid, closed.Count - laid);
        return false;
    }

    /// <summary>
    /// How many times the straight across a gap the trace is given to get round it. <b>Four</b>: the way
    /// round a swallowed tooth is the tooth's two flanks and its tip, which is the straight three times over
    /// where the tooth is as deep as it is wide, and deeper teeth than that are what the chord is left for.
    /// </summary>
    const int Roundabout = 4;

    /// <summary>
    /// One station pushed out until it keeps the clearance — or given up on, which drops it and leaves the
    /// stations either side to close over the gap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Pushed and then pushed again</b>, because the band that was nearest is rarely the one that is
    /// nearest once the station has moved: the mouth of a slot has ring on both sides of it, and a station
    /// held off one lands in front of the other.
    /// </para>
    /// <para>
    /// <b>And solved when the pushes will not settle</b> (<see cref="Sought"/>). A push jumps to where one
    /// piece's own offset stands, so where two pieces are equally near it jumps between them for ever — and
    /// two pieces equally near is not an edge case here, it is <em>the fold itself</em>, which is the one
    /// place a step ever needs holding. Ten thousand of the sixteen thousand gaps a city leaves were given
    /// up on for this and closed with a bare straight. What settles them is that the clearance is one
    /// continuous figure however many pieces it is the least of, so it can be sought along a line rather
    /// than jumped to.
    /// </para>
    /// </remarks>
    static bool Held(RingField field, float hand, float wantedM, float reachM, ref Vector2 atM)
    {
        var soughtM = atM;
        var outM = Vector2.Zero;
        for (var push = 0; push < Pushes; push++)
        {
            // <b>Nothing within reach is deep inside and not far outside.</b> A step of a gap stands on the
            // straight between two places that are both on the answer, so it cannot be further out than
            // they are — and where the field has nothing to say about it, what it has nothing to say about
            // is a place in the middle of the ground. Read the other way, the straight across a car park's
            // mouth came back accepted for being further from every line than the answer measures, and the
            // boundary ran through the bays.
            if (!field.Nearest(atM, reachM, out var footM, out var offM, out var leftM, out _)) return false;
            if (leftM == Vector2.Zero) break;

            outM = leftM * (hand < 0f ? 1f : -1f);

            // <b>Moved to the clearance and not merely out to it.</b> A gap's chord runs outside the line
            // as often as inside it — two bays standing apart leave a boundary that dips between them —
            // and a closure that only ever pushes out leaves the chord standing where it was, which is a
            // boundary bulging past its own distance. Everything struck further out then reads as inside
            // it: the pavement swallowed the lane laid half a walk inside the pavement.
            if (MathF.Abs((hand < 0f ? offM : -offM) - wantedM) <= FoldM) break;

            // Out of the band on the hand the distance took it, from the place the clearance was measured
            // to — which is where that band's own offset stands, whichever side the station had wandered on
            // to.
            atM = footM + (leftM * (wantedM * (hand < 0f ? 1f : -1f)));
        }

        if (OnItsSideM(field, hand, atM, reachM) >= wantedM - FoldM) return true;
        if (outM == Vector2.Zero) return false;

        atM = soughtM;
        return Sought(field, hand, wantedM, reachM, outM, ref atM);
    }

    /// <summary>
    /// How many times a step is pushed clear before it is sought along a line instead. <b>Pushed and then
    /// pushed again</b>, because the station that was nearest is rarely the one that is nearest once the step
    /// has moved — a gap across the mouth of a slot has ring on both sides of it.
    /// </summary>
    const int Pushes = 4;

    /// <summary>
    /// <b>The clearance sought along a line rather than jumped to</b>: the step moved out and in along one
    /// normal until it stands the distance clear of whatever is nearest, bracketed and then halved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It works where a push does not because it never names the piece.</b> The clearance is the least of
    /// the distances to every piece within reach, which makes it one continuous figure — it has a corner
    /// where which piece is nearest changes, and no jump — so along any line out of a point it rises and
    /// falls continuously and a bracket round the distance wanted can be halved down to it. A push instead
    /// asks one piece where its own offset is and goes there, which at a fold is two answers taking turns.
    /// </para>
    /// <para>
    /// <b>The line is the normal of whichever piece was last nearest</b>, and it does not have to be the
    /// right one. Out along it the clearance grows and in along it the clearance shrinks, whichever piece is
    /// answering, so the bracket exists as long as the wanted distance lies between the two ends of the
    /// search — which is what the doubling out and in is looking for. Where it does not, the step is given
    /// up on, and the gap falls to the straight.
    /// </para>
    /// </remarks>
    static bool Sought(
        RingField field, float hand, float wantedM, float reachM, Vector2 outM, ref Vector2 atM)
    {
        var fromM = atM;
        var atLeastM = wantedM - FoldM;
        var hereM = OnItsSideM(field, hand, fromM, reachM);

        // Out where the step stands too near, in where it stands too far — doubling, because how far off
        // the answer a step is depends on a curvature and not on a station.
        var sideM = hereM < wantedM ? 1f : -1f;
        var farM = StationM;
        for (var reach = 0; reach < Brackets; reach++)
        {
            var thereM = fromM + (outM * (farM * sideM));
            var clearM = OnItsSideM(field, hand, thereM, reachM);
            if (MathF.Abs(clearM - wantedM) <= FoldM)
            {
                atM = thereM;
                return true;
            }

            if ((clearM - wantedM) * (hereM - wantedM) < 0f)
            {
                // Bracketed: the answer stands between the point and this reach, and halving finds it.
                var nearM = 0f;
                for (var halving = 0; halving < Halvings; halving++)
                {
                    var middleM = (nearM + farM) * 0.5f;
                    var middleAtM = fromM + (outM * (middleM * sideM));
                    if ((OnItsSideM(field, hand, middleAtM, reachM) - wantedM) * (hereM - wantedM) < 0f)
                    {
                        farM = middleM;
                    }
                    else
                    {
                        nearM = middleM;
                    }
                }

                atM = fromM + (outM * ((nearM + farM) * 0.5f * sideM));
                return OnItsSideM(field, hand, atM, reachM) >= atLeastM;
            }

            farM *= 2f;
            if (farM > field.CellM) break;
        }

        return false;
    }

    /// <summary>
    /// How many doublings out along the normal are tried before the step is given up on. Six, which from a
    /// station reaches sixteen metres — past anything a fold can be deep and past the field's own reach,
    /// whichever comes first.
    /// </summary>
    const int Brackets = 6;

    /// <summary>How many times the bracket is halved: twelve, which is a station down to a tenth of a millimetre.</summary>
    const int Halvings = 12;

    /// <summary>
    /// Which side of the ring it was moved to. <b>One ring is moved to one side</b>: a piece moved the
    /// other way from its neighbours is a fold the rule has no reading of, so the first piece that moved at
    /// all says which side the whole of it is on.
    /// </summary>
    static float Hand(ReadOnlySpan<float> arcOffsetM)
    {
        foreach (var offsetM in arcOffsetM)
        {
            if (offsetM != 0f) return offsetM;
        }

        return 0f;
    }

    /// <summary>
    /// Each station replaced by the mean of the ones within half a window either side of it along the line,
    /// <b>each weighted by the metres it stands for</b> — so the stations either side of a dropped fold,
    /// which stand for the whole of it, weigh what the gap is long rather than what a station is.
    /// </summary>
    /// <remarks>
    /// The window is capped at half the ring, past which every station averages the whole of it and the
    /// answer is one point.
    /// </remarks>
    static Vector2[] Smoothed(Vector2[] pointM, float windowM)
    {
        var count = pointM.Length;
        var spanM = new float[count];
        var totalM = 0f;
        for (var at = 0; at < count; at++)
        {
            spanM[at] = (pointM[(at + 1) % count] - pointM[at]).Length();
            totalM += spanM[at];
        }

        var halfM = MathF.Min(windowM * 0.5f, totalM * 0.25f);
        if (halfM <= 0f) return pointM;

        var smoothed = new Vector2[count];
        for (var at = 0; at < count; at++)
        {
            var weight = Stands(spanM, at);
            var sum = pointM[at] * weight;
            Gather(pointM, spanM, at, 1, halfM, ref sum, ref weight);
            Gather(pointM, spanM, at, -1, halfM, ref sum, ref weight);
            smoothed[at] = sum / weight;
        }

        return smoothed;
    }

    /// <summary>
    /// The window walked one way out of a station, taking in each one it reaches. <b>Walked outwards rather
    /// than over a count of neighbours</b>: what the window holds is metres of line, and the stations are a
    /// step apart everywhere except across a dropped fold.
    /// </summary>
    static void Gather(
        Vector2[] pointM, float[] spanM, int at, int way, float halfM, ref Vector2 sum, ref float weight)
    {
        var count = pointM.Length;
        var alongM = 0f;
        for (var step = 1; step < count; step++)
        {
            alongM += spanM[Round(way > 0 ? at + step - 1 : at - step, count)];
            if (alongM > halfM) return;

            var other = Round(at + (way * step), count);
            var carries = Stands(spanM, other);
            sum += pointM[other] * carries;
            weight += carries;
        }
    }

    /// <summary>One step round a closed ring of stations, either way.</summary>
    static int Round(int at, int count) => ((at % count) + count) % count;

    /// <summary>How many metres of the line one station stands for: half the span either side of it.</summary>
    static float Stands(float[] spanM, int at) => (spanM[Round(at - 1, spanM.Length)] + spanM[at]) * 0.5f;

    /// <summary>
    /// <b>The corners of a run of stations</b> — the ones the shape would be missing without, a station
    /// within the sag of the straight between the two that bracket it saying nothing that straight does not.
    /// The run is read as closed and comes back closed, without the first corner written twice.
    /// </summary>
    static List<Vector2> Corners(Vector2[] pointM)
    {
        var count = pointM.Length;
        var corners = new List<Vector2> { pointM[0] };
        var anchor = 0;
        for (var at = 2; at <= count; at++)
        {
            if (Bows(pointM, anchor, at) <= SagM) continue;

            corners.Add(pointM[(at - 1) % count]);
            anchor = at - 1;
        }

        // Never a corner a rounding from the one it set off from: a ring whose last piece is a millimetre
        // long is one a reader sees a dot on.
        while (corners.Count > 1 && (corners[^1] - corners[0]).Length() <= ApartM) corners.RemoveAt(corners.Count - 1);

        return corners;
    }

    /// <summary>The corners laid as the closed chain of straights they are, shut on the one it set off from.</summary>
    static ArcSeg[] Straights(List<Vector2> cornersM)
    {
        var chain = new List<ArcSeg>(cornersM.Count + 1);
        for (var corner = 0; corner < cornersM.Count; corner++)
        {
            var fromM = cornersM[corner];
            var runM = cornersM[(corner + 1) % cornersM.Count] - fromM;
            var pieceM = runM.Length();
            if (pieceM <= ApartM) continue;

            chain.Add(new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), pieceM, 0f));
        }

        return chain.Count >= 3 ? [.. chain] : [];
    }

    /// <summary>How far the stations between two of them stand off the straight that would replace them.</summary>
    static float Bows(Vector2[] pointM, int anchor, int at)
    {
        var count = pointM.Length;
        var fromM = pointM[anchor];
        var run = pointM[at % count] - fromM;
        var runM = run.Length();
        if (runM <= ApartM) return float.MaxValue;

        var along = run / runM;
        var bowM = 0f;
        for (var station = anchor + 1; station < at; station++)
        {
            var off = pointM[station % count] - fromM;
            bowM = MathF.Max(bowM, MathF.Abs((off.X * along.Y) - (off.Y * along.X)));
        }

        return bowM;
    }

}
