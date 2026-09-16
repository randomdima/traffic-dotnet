using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>A bag of stretches strung end to end into the rings they are.</b> Whatever cut a set of chains into
/// pieces and threw the pieces it did not want away is left holding stretches that meet at their own ends,
/// and this is the walk that makes rings of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>It knows nothing about what the stretches were cut out of.</b> The merge of a union of bands
/// (<see cref="BandShell"/>) and the outset of a closed shape (<see cref="ArcOutset"/>) are two different
/// questions about what to keep and one question about what to do with what was kept — so the keeping is
/// each caller's and the stringing is here. A third construction that cuts chains and drops stretches
/// strings its answer with this one or it has a second opinion about what a ring is.
/// </para>
/// <para>
/// <b>What it asks of a caller is that a stretch stops where the next one starts.</b> That is what a cut at
/// a crossing gives: the piece that carries on and the piece that takes over both end at the place the two
/// curves met, so the walk is followed by the ends themselves and nothing has to decide which piece ought
/// to follow which. A caller that moved an end after cutting it has already broken this.
/// </para>
/// <para>
/// <b>Every stretch is walked with its own ground on its right</b>, which is what the pairing at a place
/// reads: the way on is the hardest turn to the walker's left, because the outside of a union hugs the
/// outside. A caller handing over stretches walked either way round gets rings that are not the shape's.
/// </para>
/// <para>
/// <b>And a ring comes back with its pieces meeting</b> (<see cref="Tightened"/>). The walk closes across
/// the holes a cut left and across the hair two readings of one crossing stand apart, so a ring that is
/// closed is not therefore a ring that joins — and whatever is laid <em>along</em> one rather than filled
/// from it breaks at every joint that does not. Closing them is the walk's because the gaps are.
/// </para>
/// </remarks>
internal sealed class ArcRings
{
    /// <summary>
    /// How near two cut ends stand to be the same end. <b>A tenth of a metre, which is the crossing's own
    /// error and not a search radius</b>: the two pieces that stop at one crossing each read that place
    /// off their own curve, and two curves meeting at a degree or two put the same point that far apart
    /// in the last bits of a float. Read as a search radius instead, a ring takes whatever end is
    /// nearest and the shape comes back wired through itself.
    /// </summary>
    public const float WeldM = 0.1f;

    /// <summary>
    /// <b>The shortest stretch worth keeping, and so the nearest two cuts stand before they are one cut</b>.
    /// The weld itself: a stretch shorter than that is its own two ends — both of them weld to one place, so
    /// it can never be walked into a ring.
    /// </summary>
    /// <remarks>
    /// <b>Two boundaries running along one another cross wherever the last bits of a float say they do</b>,
    /// which is a handful of crossings a few centimetres apart rather than the one place they really
    /// share. Cut at every one of them and weighed stretch by stretch, the sliver between each pair is
    /// dropped for being short and what is left is a hole the width of the whole cluster — wider than the
    /// weld, and so two ends the ring cannot be closed through. <b>It is the caller's figure as much as this
    /// one's</b>: whatever cuts the chains passes over a stretch this short, and whatever strings them joins
    /// across the hole that leaves.
    /// </remarks>
    public const float LeastPieceM = WeldM;

    /// <summary>
    /// <b>The most boundary one place can lose to that figure</b>: a piece passed over for being short, and
    /// the tail of the piece running onto it dropped against the same figure. The two of them stand side by
    /// side, so it is twice the least piece.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is wider than the weld and no choice of figures makes it narrower.</b> A kept stretch may not
    /// be shorter than the weld or both its ends stand at one place, so a hole left by dropping one is a
    /// weld wide at its worst — and a weld cannot close two of those at once. What closes them instead is
    /// a pass of its own, over the ends nothing else wanted, once every crossing the caller did find has
    /// been settled (<see cref="Lost"/>).
    /// </para>
    /// <para>
    /// <b>Where the two meet is a junction's fan of movements.</b> A movement's line finishes on an arc a
    /// couple of hand's breadths long, and offset to the inside of its own turn that arc comes out a few
    /// centimetres — short enough to be passed over whole, with the piece running onto it ending in a tail
    /// of a centimetre or two dropped against the same figure. The pair of them put the two ends of the
    /// ring a weld and a little apart, and a ring twenty kilometres long came back open.
    /// </para>
    /// <para>
    /// <b>It is the least a caller may lose and not the most</b> (<see cref="Of"/>). A caller whose own
    /// arithmetic loses more says so, because what it may lose is a fact about that construction rather
    /// than about this walk.
    /// </para>
    /// </remarks>
    public const float LeastLostM = LeastPieceM * 2f;

    /// <summary>
    /// <b>How far a ring's own shape may move when two of its pieces are read as the one piece they
    /// are</b> (<see cref="Joined"/>). A millimetre: two pieces laid along one line are two sums of the
    /// same numbers, and what separates them is the arithmetic's error rather than a bend.
    /// </summary>
    const float JoinM = LineTolerance.RoundingM;

    readonly List<ArcSeg> _kept;
    readonly float _lostM;

    ArcRings(List<ArcSeg> kept, float lostM)
    {
        _kept = kept;
        _lostM = MathF.Max(lostM, LeastLostM);
    }

    /// <summary>
    /// The rings a bag of stretches closes into, and the runs it could not. <b>The list is taken over and
    /// written into</b> (<see cref="Doubled"/>), because a copy of it is a copy of the whole answer.
    /// </summary>
    /// <param name="lostM">
    /// <b>How much boundary one place may be short before the hole is a hole and not a handover</b>, which
    /// is <see cref="LeastLostM"/> unless the caller's own arithmetic loses more than that
    /// (<see cref="ArcOutset"/>). Below the figure it is given, two ends nothing else wanted are the two
    /// sides of one place (<see cref="Lost"/>); above it they are two ends of the shape and the run is
    /// handed back open.
    /// </param>
    public static (ArcSeg[][] Chains, ArcSeg[][] Loose) Of(List<ArcSeg> kept, float lostM = LeastLostM) =>
        new ArcRings(kept, lostM).Strung();

    /// <summary>
    /// <b>The stretches the merge kept, strung end to end into the rings they are.</b> A stretch stops
    /// where another ribbon's boundary crossed it and the stretch that takes over starts at that same
    /// crossing, so the ring is followed by the ends themselves and nothing has to decide which piece
    /// ought to follow which.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The ends are gathered into places first, and every place is settled on its own</b>
    /// (<see cref="Linked"/>). A crossing generally has one stretch arriving and one leaving: two
    /// boundaries crossing leave four quarters round the point, three of them inside one band or the
    /// other and one outside both, so of the four half-pieces meeting there exactly two are kept. Where
    /// more arrive — which is every place a caller lays two bands at once — the pairing is made over the
    /// whole place at once rather than by whichever stretch happened to be asked first, and it is made
    /// outermost first: <b>the outside of a union hugs the outside</b>, so the way on is the hardest turn
    /// to the walker's left.
    /// </para>
    /// <para>
    /// <b>Only then is anything walked</b>, and where a run starts stops being a question. Walked as it
    /// goes, a walk that sets off in the middle of a ring reaches the end of it, stops on a stretch it has
    /// already taken, and leaves the half behind it to be found later as a second run: one ring comes back
    /// as two open ones, and the reading says the merge failed where it was the walk that did.
    /// </para>
    /// <para>
    /// <b>A run with ends is handed back as one</b> rather than shut with a straight. The boundary of a
    /// union of closed bands is closed, so a run that does not come back to where it set off is a
    /// crossing that was missed, and shutting it with a straight would bury the one reading that says so.
    /// </para>
    /// </remarks>
    (ArcSeg[][] Chains, ArcSeg[][] Loose) Strung()
    {
        Doubled();
        var (next, previous) = Linked();
        var walked = new bool[_kept.Count];
        var chains = new List<ArcSeg[]>();
        var loose = new List<ArcSeg[]>();

        // The open runs first, from their own heads: a stretch nothing leads into is the start of one,
        // and taking them first leaves nothing but whole rings behind.
        for (var at = 0; at < _kept.Count; at++)
        {
            if (previous[at] >= 0) continue;

            Take(Run(at, next, walked), chains, loose);
        }

        for (var at = 0; at < _kept.Count; at++)
        {
            if (walked[at]) continue;

            Take(Run(at, next, walked), chains, loose);
        }

        return ([.. chains], [.. loose]);
    }

    /// <summary>
    /// <b>One walked run filed as the ring it is or the hole it is not</b> — dropped where it is too
    /// short to have two ends at all, and handed back with its pieces meeting where it is a ring
    /// (<see cref="Tightened"/>).
    /// </summary>
    /// <remarks>
    /// <b>A run is handed back exactly as it was walked.</b> Its ends are the fault it is reported for, and
    /// closing a ring's joints is a thing done to a shape that is closed: a run's are two ends of the
    /// boundary and not two sides of one place.
    /// </remarks>
    void Take(ArcSeg[] run, List<ArcSeg[]> chains, List<ArcSeg[]> loose)
    {
        if (run.Length == 0) return;

        var shut = Shut(run);
        if (!shut && TooShortToHaveTwoEnds(run)) return;

        var chain = Joined(run, shut);
        if (!shut)
        {
            loose.Add(chain);
            return;
        }

        chains.Add(Tightened(chain));
    }

    /// <summary>
    /// <b>Whether a run is shorter than the boundary one place can lose</b> (<see cref="LeastLostM"/>), which
    /// makes its two ends one place and not a hole: a length the construction cannot tell from a point is
    /// not a length of boundary nothing accounts for.
    /// </summary>
    /// <remarks>
    /// <b>It is the figure the loss is bounded by and not the weld</b>, because that is what a run this
    /// short is made of. The town lays a movement whose own radius is barely wider than the lane it
    /// carries, and its ribbon's inner edge folds into a hook a few centimetres across; what its
    /// neighbours leave of that hook is a stretch or two, and how much of it survived the cut is decided
    /// by the same two drops the pass before this one joins across (<see cref="Lost"/>).
    /// </remarks>
    bool TooShortToHaveTwoEnds(ReadOnlySpan<ArcSeg> run)
    {
        var lengthM = 0f;
        foreach (var arc in run) lengthM += arc.LengthM;

        return lengthM <= _lostM + LineTolerance.RoundingM;
    }

    /// <summary>One run followed from a stretch until it runs out or comes back to where it set off.</summary>
    ArcSeg[] Run(int first, int[] next, bool[] walked)
    {
        var run = new List<ArcSeg>();
        for (var at = first; at >= 0 && !walked[at]; at = next[at])
        {
            walked[at] = true;
            run.Add(_kept[at]);
        }

        return [.. run];
    }

    /// <summary>
    /// <b>One run handed back as the pieces it really turns at</b> (<see cref="Spline.JoinedInto"/>):
    /// the stretches that carry on the same circle written as the one stretch they are, and on a ring
    /// the seam between its last piece and its first among them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Whatever cut the chains joins its answer here</b>, the outset (<see cref="ArcOutset"/>) as much
    /// as the merge: a run of cuts down one curve is that curve to one construction exactly as it is to
    /// the other, and two readings of what a ring's corners are would be two shapes.
    /// </para>
    /// <para>
    /// <para>
    /// <b>A cut is not a corner.</b> A stretch stops wherever any other band's boundary crossed it,
    /// which is every place the ground changed hands <em>and</em> every place it did not — two lanes of
    /// one carriageway are cut at every junction either of them passes, a bay's way is cut by each of
    /// its neighbours, and the boundary carries on down the same line through all of it. Three quarters
    /// of the pieces a town's rings came back in were a piece stopping and the same curve starting
    /// again.
    /// </para>
    /// <para>
    /// <b>It is the shape and not a drawing of it that is joined.</b> Whatever reads the boundary —
    /// the walk laid off it, the mesh it bounds, the picture of it — reads the ground's own corners
    /// rather than the merge's working, and nothing downstream has to work out which of the points it
    /// was handed were places the shape turns at.
    /// </para>
    /// </remarks>
    public static ArcSeg[] Joined(ReadOnlySpan<ArcSeg> run, bool shut)
    {
        var joined = new ArcSeg[run.Length];
        var pieces = Spline.JoinedInto(run, JoinM, joined);

        // A ring has no first piece: the walk set off wherever its lowest-numbered stretch happened to
        // be, which is as likely to be the middle of a straight as a corner, and that straight comes
        // back as the two ends of the run. So the last piece takes the first — and having taken it,
        // reaches further and may take the next as well, which is this loop and not a second case.
        var head = 0;
        while (shut && pieces - head > 1
            && Spline.CarriesOn(joined[pieces - 1], joined[head], JoinM, out var over))
        {
            joined[pieces - 1] = over;
            head++;
        }

        return joined[head..pieces];
    }

    /// <summary>
    /// <b>One ring with its pieces made to meet</b>: every joint closed onto the place its two sides agree
    /// on, which is the middle of the two ends standing there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A ring is walked by ends that stand near one another rather than on one another</b>
    /// (<see cref="WeldM"/>, <see cref="Lost"/>), so the shape it hands back has a centimetre of gap at a
    /// junction here and there and a tenth of a metre where a cut passed a piece over. <b>That is a hole in
    /// everything laid along the ring</b> — the kerb struck down it, the line a frame draws of it — and it
    /// is fatal to the corner it is the corner of: the arc an offset puts round a corner stands at exactly
    /// the distance moved, so struck about the vertex one side names it stands a centimetre inside the
    /// vertex the other names, the fold test deletes it for standing inside, and the ring comes back open
    /// at that corner with twenty kilometres of boundary hanging off the hole.
    /// </para>
    /// <para>
    /// <b>The middle of the two ends, so that no joint is moved further than half of what it was wrong
    /// by</b>, and each piece is then the one arc that leaves its own start pose and reaches its own new end
    /// (<see cref="Spline.ArcThrough"/>). What that changes is a centimetre on a shape that only ever knew
    /// itself to a weld; what it buys is a ring a line can be laid along.
    /// </para>
    /// </remarks>
    public static ArcSeg[] Tightened(ReadOnlySpan<ArcSeg> ring)
    {
        if (ring.Length < 2) return ring.ToArray();

        var endsM = new Vector2[ring.Length];
        for (var at = 0; at < ring.Length; at++)
        {
            endsM[at] = (ring[at].EndM + ring[(at + 1) % ring.Length].StartM) * 0.5f;
        }

        var tight = new ArcSeg[ring.Length];
        for (var at = 0; at < ring.Length; at++)
        {
            var fromM = endsM[(at + ring.Length - 1) % ring.Length];
            var toM = endsM[at];

            // A piece shorter than what its ends moved cannot be re-struck between them and is left where
            // it stands: what it is asked to reach may be behind it.
            tight[at] = Vector2.Distance(fromM, toM) < WeldM
                ? ring[at]
                : Spline.ArcThrough(fromM, ring[at].HeadingRad, toM);
        }

        return tight;
    }

    /// <summary>
    /// <b>Every piece of every chain in one numbering</b>, which is the shape a cutting pass and an index
    /// of pieces both want — the inverse of what this type does, and beside it because the pair is one
    /// round trip.
    /// </summary>
    public static ArcSeg[] Flat(ReadOnlySpan<ArcSeg[]> chains)
    {
        var pieces = 0;
        foreach (var chain in chains) pieces += chain.Length;

        var flat = new ArcSeg[pieces];
        var at = 0;
        foreach (var chain in chains)
        {
            chain.CopyTo(flat, at);
            at += chain.Length;
        }

        return flat;
    }

    /// <summary>
    /// <b>Whether a run comes back to where it set off</b>, which is the whole of what makes it a ring.
    /// </summary>
    /// <remarks>
    /// <b>However few stretches it is made of.</b> Neither <see cref="Linked"/> nor <see cref="Lost"/>
    /// will let a stretch take itself up — a place with one boundary arriving and the same one leaving
    /// offers no pair — so a ring the merge kept as a single stretch is the one shape the walk can never
    /// close. Which the town does lay: a movement whose own radius is barely wider than the lane it
    /// carries folds its ribbon's inner edge into a hook a few centimetres across, and what is left of
    /// that hook once its neighbours have cut it is one stretch. It is short enough to be dropped as no
    /// hole at all (<see cref="TooShortToHaveTwoEnds"/>), which is where it goes.
    /// </remarks>
    static bool Shut(ArcSeg[] ring) =>
        Vector2.DistanceSquared(ring[^1].EndM, ring[0].StartM) <= WeldM * WeldM;

    /// <summary>
    /// <b>Every stretch given the one that takes over where it stops</b>, place by place: at each, the
    /// stretches arriving are paired with the stretches leaving, the outermost turn taken first and each
    /// stretch used once.
    /// </summary>
    /// <remarks>
    /// <b>Settled over the place and not stretch by stretch.</b> Asked in turn, a stretch takes whatever
    /// is free when it asks, so a place where three stretches arrive hands the outermost way on to
    /// whichever of them was numbered lowest and leaves the one that should have had it with an end.
    /// Weighing every pair the place offers and taking them in order of how far out each turns costs the
    /// square of a handful and is the same answer whatever order the stretches were made in.
    /// </remarks>
    (int[] Next, int[] Previous) Linked()
    {
        var next = new int[_kept.Count];
        var previous = new int[_kept.Count];
        Array.Fill(next, -1);
        Array.Fill(previous, -1);

        var places = Places(out var arrivingAt, out var leavingAt);
        var arriving = new List<int>[places];
        var leaving = new List<int>[places];
        for (var place = 0; place < places; place++)
        {
            arriving[place] = [];
            leaving[place] = [];
        }

        for (var stretch = 0; stretch < _kept.Count; stretch++)
        {
            arriving[arrivingAt[stretch]].Add(stretch);
            leaving[leavingAt[stretch]].Add(stretch);
        }

        var pairs = new List<(float TurnRad, int From, int Onto)>();
        for (var place = 0; place < places; place++)
        {
            pairs.Clear();
            foreach (var from in arriving[place])
            {
                foreach (var onto in leaving[place])
                {
                    if (onto == from) continue;

                    pairs.Add((TurnRad(from, onto), from, onto));
                }
            }

            pairs.Sort((one, other) => one.TurnRad.CompareTo(other.TurnRad));
            foreach (var (_, from, onto) in pairs)
            {
                if (next[from] >= 0 || previous[onto] >= 0) continue;

                next[from] = onto;
                previous[onto] = from;
            }
        }

        Lost(next, previous);
        return (next, previous);
    }

    /// <summary>
    /// <b>And then the ends the cut lost, joined up</b>: a stretch nothing takes up and a stretch nothing
    /// leads into, standing nearer to each other than the boundary one place can lose
    /// (<see cref="LeastLostM"/>), are the two sides of a hole rather than two ends of the shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Last, and only over the ends nothing else wanted — which is what keeps it from wiring the shape
    /// through itself.</b> By the time it is asked, every crossing the merge found has been settled at its
    /// own place and by how far out each pair turns. What is left over is a stretch arriving where nothing
    /// leaves and a stretch leaving where nothing arrives, which is the shape a dropped piece of ribbon
    /// leaves at one place and the shape nothing else leaves anywhere.
    /// </para>
    /// <para>
    /// <b>The nearest pair first, and a stretch may not take itself up</b> — the same two rules the places
    /// are settled by. The first is what makes the answer the geometry's rather than the order the
    /// stretches were made in; the second is what keeps a hook out of the rings handed back, a
    /// stretch whose own two ends are all that stand at a place being a length of boundary too short to
    /// be a hole rather than a ring the shape has (<see cref="TooShortToHaveTwoEnds"/>).
    /// </para>
    /// <para>
    /// <b>What it does not do is move anything.</b> The hole stays exactly as wide as it was and the ring
    /// is walked across it — which is what the ring already does at every piece the cut passed over, a
    /// weld at a time. Closing it by running a stretch on to meet the next would put that stretch's end
    /// past the crossing it stops at, and the handover there is the ring.
    /// </para>
    /// </remarks>
    void Lost(int[] next, int[] previous)
    {
        var starts = new Dictionary<(int X, int Y), List<int>>();
        for (var at = 0; at < _kept.Count; at++)
        {
            if (previous[at] >= 0) continue;

            var cell = Cell(_kept[at].StartM, _lostM);
            if (!starts.TryGetValue(cell, out var here)) starts[cell] = here = [];

            here.Add(at);
        }

        if (starts.Count == 0) return;

        var pairs = new List<(float OffSq, int From, int Onto)>();
        for (var from = 0; from < _kept.Count; from++)
        {
            if (next[from] >= 0) continue;

            Reaching(pairs, starts, from);
        }

        pairs.Sort(Nearest);
        foreach (var (_, from, onto) in pairs)
        {
            if (next[from] >= 0 || previous[onto] >= 0) continue;

            next[from] = onto;
            previous[onto] = from;
        }
    }

    /// <summary>Every start within a lost length of where one stretch ends, gathered as the pairs they would make.</summary>
    void Reaching(
        List<(float OffSq, int From, int Onto)> pairs, Dictionary<(int X, int Y), List<int>> starts, int from)
    {
        var endM = _kept[from].EndM;
        var (cellX, cellY) = Cell(endM, _lostM);
        for (var y = -1; y <= 1; y++)
        {
            for (var x = -1; x <= 1; x++)
            {
                if (!starts.TryGetValue((cellX + x, cellY + y), out var here)) continue;

                foreach (var onto in here)
                {
                    if (onto == from) continue;

                    var offSq = Vector2.DistanceSquared(_kept[onto].StartM, endM);
                    if (offSq <= _lostM * _lostM) pairs.Add((offSq, from, onto));
                }
            }
        }
    }

    /// <summary>Nearest first, and between two pairs as near as each other the stretches' own order.</summary>
    static int Nearest((float OffSq, int From, int Onto) one, (float OffSq, int From, int Onto) other)
    {
        var by = one.OffSq.CompareTo(other.OffSq);
        if (by != 0) return by;

        by = one.From.CompareTo(other.From);
        return by != 0 ? by : one.Onto.CompareTo(other.Onto);
    }

    /// <summary>
    /// How far one stretch turns to take up another, negative to the walker's left — which is out of the
    /// shape, every stretch being walked with its own ground on the right.
    /// </summary>
    float TurnRad(int from, int onto)
    {
        var stretch = _kept[from];
        var arriving = Heading.Unit(stretch.HeadingAtRad(stretch.LengthM));
        var leaving = _kept[onto].StartUnit;
        return MathF.Atan2(
            (arriving.X * leaving.Y) - (arriving.Y * leaving.X), Vector2.Dot(arriving, leaving));
    }

    /// <summary>
    /// <b>Every end of every kept stretch gathered into the places they stand at</b> — one place per
    /// crossing, whichever stretches reach it — with the place each stretch arrives at and leaves from.
    /// </summary>
    int Places(out int[] arrivingAt, out int[] leavingAt)
    {
        var at = new Dictionary<(int X, int Y), List<int>>(_kept.Count * 2);
        var standing = new List<Vector2>();
        arrivingAt = new int[_kept.Count];
        leavingAt = new int[_kept.Count];

        for (var stretch = 0; stretch < _kept.Count; stretch++)
        {
            arrivingAt[stretch] = Place(at, standing, _kept[stretch].EndM, null);
        }

        // A start is offered the places an end already stands at before any of its own: a walk arrives
        // at a place to leave it, and a place with nothing to take up is one an arrival is waiting at.
        var waiting = new int[standing.Count];
        foreach (var place in arrivingAt) waiting[place]++;

        for (var stretch = 0; stretch < _kept.Count; stretch++)
        {
            var place = Place(at, standing, _kept[stretch].StartM, waiting);
            leavingAt[stretch] = place;
            if (place < waiting.Length) waiting[place]--;
        }

        return standing.Count;
    }

    /// <summary>
    /// The place one end stands at, made if no end already stands within a weld of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The cell narrows the search and the distance settles it.</b> Taking whatever place the
    /// neighbouring cells hold instead, an end joins the first place the lattice happens to offer, which
    /// at a crowded corner is a place the width of a cell away — and the two ends that really are one
    /// crossing end up in two.
    /// </para>
    /// <para>
    /// <b>Except that a place still <paramref name="waiting"/> for a stretch to leave it takes one
    /// first.</b> Which of two places within a weld an end joins is not a geometric question — the weld
    /// is exactly the figure that says the two are one place — so where the answer decides whether a ring
    /// can be walked, it is decided by that. Two boundaries crossing at a shallow angle read the crossing
    /// a hand apart, and a bay's mouth puts three such ends in a row: the middle one is as near the
    /// place behind it as the place in front, and taken by the nearer it leaves a stretch arriving
    /// nowhere while another has two ways on.
    /// </para>
    /// </remarks>
    static int Place(
        Dictionary<(int X, int Y), List<int>> at, List<Vector2> standing, Vector2 pointM, int[]? waiting)
    {
        var (cellX, cellY) = Cell(pointM, WeldM);
        var bestSq = WeldM * WeldM;
        var best = -1;
        var bestWaiting = 0;
        for (var y = -1; y <= 1; y++)
        {
            for (var x = -1; x <= 1; x++)
            {
                if (!at.TryGetValue((cellX + x, cellY + y), out var here)) continue;

                foreach (var place in here)
                {
                    var offSq = Vector2.DistanceSquared(standing[place], pointM);
                    if (offSq > WeldM * WeldM) continue;

                    var waits = waiting is not null && place < waiting.Length && waiting[place] > 0 ? 1 : 0;
                    if (waits < bestWaiting || (waits == bestWaiting && offSq > bestSq)) continue;

                    bestSq = offSq;
                    bestWaiting = waits;
                    best = place;
                }
            }
        }

        if (best >= 0) return best;

        if (!at.TryGetValue((cellX, cellY), out var cell)) at[(cellX, cellY)] = cell = [];

        cell.Add(standing.Count);
        standing.Add(pointM);
        return standing.Count - 1;
    }

    /// <summary>
    /// <b>One stretch of boundary said twice is said once.</b> Two stretches that run from the same place
    /// to the same place are one stretch, and the copy the lower-numbered line did not lay is dropped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because a ring is walked by its ends and two copies leave every crossing with two ways on.</b>
    /// The town numbers one line twice wherever a bay is entered and left down it (GEN-4f), which lays one
    /// ribbon twice — and the merge keeps a piece where the ground a hair outside it is on no band, which
    /// is true of both copies.
    /// </para>
    /// <para>
    /// <b>Said twice is the same question as stands at one place</b>, so it is asked of the places
    /// themselves (<see cref="Places"/>) rather than of a lattice the ends are rounded to. Two edges the
    /// town laid a couple of millimetres apart are one stretch of boundary by the first reading and two
    /// by the second, whichever cells they happen to round into — and the pair the cover test is allowed
    /// to keep twice rather than drop twice (<see cref="BandShell"/>'s cover test) is exactly that pair.
    /// </para>
    /// </remarks>
    void Doubled()
    {
        Places(out var arrivingAt, out var leavingAt);

        var said = new HashSet<(int From, int To)>(_kept.Count);
        var kept = 0;
        for (var at = 0; at < _kept.Count; at++)
        {
            if (said.Add((leavingAt[at], arrivingAt[at]))) _kept[kept++] = _kept[at];
        }

        _kept.RemoveRange(kept, _kept.Count - kept);
    }

    static (int X, int Y) Cell(Vector2 pointM, float sizeM) =>
        ((int)MathF.Floor(pointM.X / sizeM), (int)MathF.Floor(pointM.Y / sizeM));
}
