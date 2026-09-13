using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

internal sealed partial class LaneShell
{
    sealed partial class Merge
    {
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
        /// more arrive — which is every place the town lays two bands at once — the pairing is made over the
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
        /// <b>A run with ends is handed back as one</b> (<see cref="LaneShell.Loose"/>). The boundary of a
        /// union of closed bands is closed, so a run that does not come back to where it set off is a
        /// crossing that was missed, and shutting it with a straight would bury the one reading that says so.
        /// </para>
        /// </remarks>
        public (ArcSeg[][] Chains, ArcSeg[][] Loose) Strung()
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

                var run = Run(at, next, walked);
                if (run.Length > 0) loose.Add(Joined(run, false));
            }

            for (var at = 0; at < _kept.Count; at++)
            {
                if (walked[at]) continue;

                var ring = Run(at, next, walked);
                var shut = Shut(ring);
                (shut ? chains : loose).Add(Joined(ring, shut));
            }

            return ([.. chains], [.. loose]);
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
        /// was handed were places the town turns at.
        /// </para>
        /// </remarks>
        static ArcSeg[] Joined(ArcSeg[] run, bool shut)
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

        static bool Shut(ArcSeg[] ring) =>
            ring.Length > 1 && Vector2.DistanceSquared(ring[^1].EndM, ring[0].StartM) <= WeldM * WeldM;

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

            return (next, previous);
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
            var (cellX, cellY) = Cell(pointM);
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
        /// to keep twice rather than drop twice (<see cref="Covered"/>) is exactly that pair.
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

        static (int X, int Y) Cell(Vector2 pointM) =>
            ((int)MathF.Floor(pointM.X / WeldM), (int)MathF.Floor(pointM.Y / WeldM));
    }
}
