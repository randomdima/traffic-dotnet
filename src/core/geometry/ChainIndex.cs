using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// A fixed set of arc chains laid over a uniform grid, so that <b>which of them a point is nearest</b>
/// costs the ground around the point rather than the whole network — and so that <b>which of them could
/// touch a place or cross a line</b> costs the same (<see cref="Around"/>, <see cref="Crossing"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The answer is the whole-network scan's, exactly.</b> The grid decides which chains are looked at
/// and nothing else: the survivors are measured by the same arithmetic the scan used, and a tie is
/// settled on the id rather than on the order they were met in, so a town routed through this one is
/// routed the way it always was whatever order the cells hand them back. What makes that safe is the stopping rule — the
/// ring is grown until the best distance found <em>fits inside the ring already searched</em>, and a
/// chain nearer than that has a piece inside that ring by construction.
/// </para>
/// <para>
/// <b>A candidate query hands back the cells' own answer and measures nothing</b>
/// (<see cref="Around"/>, <see cref="Crossing"/>): every chain with a piece in the cells asked about, each
/// once, which is a <em>superset</em> of what is really within the distance given. The exact question stays
/// the caller's, and that is the division of labour the narrowing is safe under — a caller whose own test
/// is unchanged gets the answer it always got, out of a candidate set the grid bounded. What it must not do
/// is read the grid's order as a ranking: the cells are walked row by row, so a caller that settles
/// anything on which candidate it met first settles it on the lattice.
/// </para>
/// <para>
/// <b>The lattice is the map's and not the set's</b>: the origin is snapped down to a whole cell, so two
/// indexes sealed at one cell size lay their cells on the same lines whatever ground each of them happens
/// to cover. That is what makes a cell a place in the town rather than a place in a set — one debug layer
/// can draw the grid every index is asked over (OBS-2r), and two indexes' answers about one cell are
/// answers about one square of ground.
/// </para>
/// <para>
/// <b>It is built once and never written to again.</b> The networks it serves are laid with the town
/// and immutable afterwards, which is what keeps an index of them one truth rather than a second: there
/// is no state here that could drift from the geometry, because the geometry cannot change. The
/// scratch a query uses is the index's own and is not re-entrant, on the same footing as the solver's
/// broad phase.
/// </para>
/// <para>
/// A piece's box is taken by walking it at <see cref="SampleStepM"/> and grown by half that step, which
/// contains the piece whatever it curves through: no point of an arc is more than half a step along it
/// from a sample, and a chord is never longer than the arc it subtends.
/// </para>
/// </remarks>
internal sealed class ChainIndex
{
    /// <summary>How finely a piece is walked when its cells are taken, and its box with them. Build-time only.</summary>
    const float SampleStepM = 1f;

    /// <summary>
    /// How far round a sample of a piece is taken to belong to the piece: half a step, which is what makes a
    /// walk at <see cref="SampleStepM"/> cover the whole of what it walks — no point of a piece stands
    /// further than that along the piece from a sample, and a chord is never longer than its arc.
    /// </summary>
    const float MarginM = SampleStepM * 0.5f;

    /// <summary>What no index may exceed however far its chains are spread: the cell grows instead.</summary>
    const int MostCells = 1 << 22;

    readonly ArcSeg[] _arcs;
    readonly int[] _arcStart;
    readonly float[] _lengthM;
    readonly int[] _chainId;

    readonly float _cellM;
    readonly float _inverseCellM;
    readonly Vector2 _originM;
    readonly int _width;
    readonly int _height;

    /// <summary>Prefix offsets, one past the last cell, so a cell's run of entries is a subtraction.</summary>
    readonly int[] _cellStart;

    readonly int[] _entrySlot;

    /// <summary>Which query last offered each slot, so a chain crossing several cells is measured once.</summary>
    readonly int[] _stamp;

    readonly int[] _candidate;
    int _candidateCount;
    int _generation;

    ChainIndex(
        ArcSeg[] arcs, int[] arcStart, float[] lengthM, int[] chainId, float cellM, Vector2 originM, int width,
        int height, int[] cellStart, int[] entrySlot)
    {
        _arcs = arcs;
        _arcStart = arcStart;
        _lengthM = lengthM;
        _chainId = chainId;
        _cellM = cellM;
        _inverseCellM = 1f / cellM;
        _originM = originM;
        _width = width;
        _height = height;
        _cellStart = cellStart;
        _entrySlot = entrySlot;
        _stamp = new int[chainId.Length];

        // <b>Room for every chain there is, once.</b> A slot is stamped the first time a query meets it, so
        // the candidate set can never be longer than the set itself — and sized to a guess instead, a query
        // over ground that happens to be busy grows the array, which is an allocation on a path the tick
        // reads (rule 2). It is four bytes a chain.
        _candidate = new int[Math.Max(1, chainId.Length)];
    }

    /// <summary>How many chains were registered. A census, so a caller can say what its index is of.</summary>
    public int ChainCount => _chainId.Length;

    /// <summary>
    /// <b>Every piece over the lattice as a chain of its own</b>, numbered by where it stands in
    /// <paramref name="pieces"/> — so that what is near a place is measured against the pieces near it
    /// rather than against every piece of whatever ring they belong to.
    /// </summary>
    /// <remarks>
    /// <b>It is the shape every question about a shell's boundary is asked in.</b> A town's boundary is one
    /// ring of a hundred thousand pieces; an index of rings answers "what is nearest" and "what crosses this"
    /// with all of them, which is the whole set for every query.
    /// </remarks>
    public static ChainIndex OfPieces(ReadOnlySpan<ArcSeg> pieces, float cellM)
    {
        var building = new Builder();
        for (var at = 0; at < pieces.Length; at++)
        {
            building.Add(at, pieces.Slice(at, 1), MathF.Abs(pieces[at].LengthM));
        }

        return building.Seal(cellM);
    }

    /// <summary>
    /// How wide one cell is. <b>The cell the index settled on and not the one it was asked for</b>: a set
    /// spread far enough to want more cells than any index may hold is binned coarsely instead
    /// (<see cref="MostCells"/>), and a caller drawing or reasoning about the lattice wants the figure the
    /// chains were actually binned at.
    /// </summary>
    public float CellM => _cellM;

    /// <summary>
    /// The corner cell <c>(0, 0)</c> starts at, which stands on the lattice: it is the least corner of the
    /// set snapped <em>down</em> to a whole cell, so the cells of two indexes at one cell size line up.
    /// </summary>
    public Vector2 OriginM => _originM;

    /// <summary>How many cells across and down the lattice runs.</summary>
    public int Width => _width;

    public int Height => _height;

    /// <summary>
    /// <b>How many chains have a piece in one cell</b>, each counted once however many of its pieces are in
    /// there — which is what a picture of the grid is a picture of (OBS-2r). Nought outside the lattice.
    /// </summary>
    /// <remarks>
    /// It counts on the queries' own stamp, so it spends their working set: a caller cannot ask this
    /// <em>between</em> laying a candidate set and reading it. Nothing can — a candidate query copies its
    /// answer out before it returns — and this is the note that keeps it that way.
    /// </remarks>
    public int ChainsInCell(int atX, int atY) => ChainsInCell(atX, atY, []);

    /// <summary>
    /// <b>The same count, with the chains themselves</b> — for a caller that wants to look at what one cell
    /// holds rather than how much of it there is. A span shorter than the answer truncates it, exactly as
    /// the candidate queries' does, and the count returned is still the whole.
    /// </summary>
    public int ChainsInCell(int atX, int atY, Span<int> ids)
    {
        if (atX < 0 || atY < 0 || atX >= _width || atY >= _height) return 0;

        _generation++;
        var cell = (atY * _width) + atX;
        var chains = 0;
        for (var entry = _cellStart[cell]; entry < _cellStart[cell + 1]; entry++)
        {
            var slot = _entrySlot[entry];
            if (_stamp[slot] == _generation) continue;

            _stamp[slot] = _generation;
            if (chains < ids.Length) ids[chains] = _chainId[slot];
            chains++;
        }

        return chains;
    }

    /// <summary>
    /// <b>Every chain that could pass within <paramref name="radiusM"/> of the point</b> — the cells round
    /// it, read off, and nothing measured. Returns how many there are.
    /// </summary>
    /// <remarks>
    /// <b>It is <see cref="Near"/> without the measuring</b>, and the difference is who asks the exact
    /// question. <c>Near</c> projects every candidate onto the point and hands back the ones that really are
    /// within the radius; this hands back the candidates. A caller that already measures something finer
    /// than a distance to a line — two band edges that have to stop at one place, two boxes that have to
    /// overlap — pays for that projection twice over and throws the answer away, so it asks this instead.
    /// <b>A span shorter than the answer truncates it</b>, exactly as <c>Near</c>'s does, and a caller that
    /// cannot afford a missed candidate sizes it to <see cref="ChainCount"/>.
    /// </remarks>
    public int Around(Vector2 pointM, float radiusM, Span<int> ids)
    {
        if (ChainCount == 0) return 0;

        var reach = new Vector2(MathF.Max(radiusM, 0f));
        Fresh();
        Offer(pointM - reach, pointM + reach);
        return Copied(ids);
    }

    /// <summary>
    /// <b>Every chain that could cross this one, or come within <paramref name="withinM"/> of it</b>: the
    /// cells the chain's own pieces reach, grown by that distance, read off. Returns how many there are —
    /// including the chain itself where it was registered here, since a chain shares every one of its own
    /// cells.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two lines that cross share a cell, so nothing that crosses is missed.</b> The crossing point lies
    /// in some cell of the lattice; each line has a piece through that point, and a piece is entered in
    /// every cell its own box touches — so both lines are in that cell's run. The same holds of two lines
    /// standing <paramref name="withinM"/> apart once the boxes are grown by it. What comes back is
    /// therefore a superset, and the pair that actually crosses is found by whatever solves crossings
    /// (<see cref="Spline.CrossingsM"/>) over what is left.
    /// </para>
    /// <para>
    /// <b>The boxes are the builder's own</b> (<see cref="Box"/>), so the cells a query reads are the cells
    /// the chains were written into. Taken any other way — a chord's box, a tighter walk — the two
    /// disagree at a bend and the pair that is missed is the one the whole query is for.
    /// </para>
    /// </remarks>
    public int Crossing(ReadOnlySpan<ArcSeg> chain, float withinM, Span<int> ids)
    {
        if (ChainCount == 0 || chain.Length == 0) return 0;

        var reach = new Vector2(MathF.Max(withinM, 0f));
        Fresh();
        for (var piece = 0; piece < chain.Length; piece++)
        {
            var leastM = new Vector2(float.MaxValue);
            var mostM = new Vector2(float.MinValue);
            Box(chain[piece], ref leastM, ref mostM);
            Offer(leastM - reach, mostM + reach);
        }

        return Copied(ids);
    }

    /// <summary>
    /// The chain whose line passes nearest the point, and how far along it that is — or −1 where nothing
    /// was registered at all.
    /// </summary>
    public int Nearest(Vector2 pointM, out float alongM)
    {
        alongM = 0f;
        if (ChainCount == 0) return -1;

        // The answer is nearly always in the first ring; where it is not, the ring is grown to whatever
        // the best found needs and the question asked again.
        var radiusM = _cellM;
        var acrossM = (_width + _height) * _cellM;
        while (true)
        {
            Gather(pointM, radiusM);
            var best = Weigh(pointM, out alongM, out var bestDistanceSq);

            // Nothing nearer than what was found can lie outside a ring that already holds it, so a best
            // inside the ring is the whole network's answer.
            if (best >= 0 && bestDistanceSq <= radiusM * radiusM) return best;

            // Past the grid's own reach there is nothing further to find, and a point that far out is
            // nearest whatever the widest ring held.
            if (radiusM >= acrossM) return best >= 0 ? best : Everything(pointM, out alongM);

            radiusM = MathF.Max(best >= 0 ? MathF.Sqrt(bestDistanceSq) : 0f, radiusM * 2f);
        }
    }

    /// <summary>
    /// <b>Every</b> chain passing within <paramref name="radiusM"/> of the point, and how far along each of
    /// them that is — for a caller that has to weigh all of them rather than take the nearest. Returns how
    /// many there are.
    /// </summary>
    /// <remarks>
    /// A result larger than the spans is truncated and the caller has silently turned the whole answer into
    /// part of one, exactly as <c>BucketGrid.Query</c>'s is. The order is the grid's and is not the ids';
    /// nothing here is a nearest, so nothing is settled on a tie.
    /// </remarks>
    public int Near(Vector2 pointM, float radiusM, Span<int> ids, Span<float> alongM)
    {
        if (ChainCount == 0) return 0;

        Gather(pointM, radiusM);
        var reachSq = radiusM * radiusM;
        var found = 0;
        for (var index = 0; index < _candidateCount; index++)
        {
            var slot = _candidate[index];
            var arcs = _arcs.AsSpan(_arcStart[slot], _arcStart[slot + 1] - _arcStart[slot]);
            var lengthM = _lengthM[slot];
            var atM = Spline.ProjectM(arcs, pointM, lengthM * 0.5f, lengthM);
            if ((Spline.SampleAt(arcs, atM).PositionM - pointM).LengthSquared() > reachSq) continue;

            if (found < ids.Length)
            {
                ids[found] = _chainId[slot];
                alongM[found] = atM;
            }

            found++;
        }

        return found;
    }

    /// <summary>The slots whose pieces reach the ring, each once, in whatever order the cells gave them.</summary>
    /// <remarks>
    /// <b>The order is not load-bearing and they are deliberately not sorted.</b> The grid hands cells
    /// back row by row, so a tie would otherwise fall to whichever chain it reached first — which is why
    /// <see cref="Measure"/> settles a tie on the id itself rather than on the order it was met in. Sorted
    /// here instead, a point standing beside a busy corner paid an insertion sort of the whole ring, and
    /// paid it again for every ring the search had to grow.
    /// </remarks>
    void Gather(Vector2 pointM, float radiusM)
    {
        var reach = new Vector2(radiusM);
        Fresh();
        Offer(pointM - reach, pointM + reach);
    }

    /// <summary>A new candidate set, so what a query gathers is its own and never the last one's.</summary>
    void Fresh()
    {
        _generation++;
        _candidateCount = 0;
    }

    /// <summary>
    /// The slots in one box of ground offered to the candidate set, each once — <b>added to whatever is
    /// already in it</b>, so a query over several boxes is several calls and the chain in two of them is
    /// still one candidate.
    /// </summary>
    void Offer(Vector2 leastM, Vector2 mostM)
    {
        if (!Range(leastM, mostM, out var fromX, out var fromY, out var toX, out var toY)) return;

        for (var y = fromY; y <= toY; y++)
        {
            for (var x = fromX; x <= toX; x++)
            {
                var cell = y * _width + x;
                for (var entry = _cellStart[cell]; entry < _cellStart[cell + 1]; entry++)
                {
                    var slot = _entrySlot[entry];
                    if (_stamp[slot] == _generation) continue;

                    _stamp[slot] = _generation;
                    _candidate[_candidateCount++] = slot;
                }
            }
        }
    }

    /// <summary>The candidate set as the caller's own ids, truncated to the room it gave.</summary>
    int Copied(Span<int> ids)
    {
        for (var index = 0; index < _candidateCount && index < ids.Length; index++)
        {
            ids[index] = _chainId[_candidate[index]];
        }

        return _candidateCount;
    }

    int Weigh(Vector2 pointM, out float alongM, out float bestDistanceSq)
    {
        alongM = 0f;
        bestDistanceSq = float.MaxValue;
        var best = -1;
        for (var index = 0; index < _candidateCount; index++)
        {
            Measure(_candidate[index], pointM, ref best, ref bestDistanceSq, ref alongM);
        }

        return best;
    }

    /// <summary>The whole set, for the one case the grid cannot bound: a point standing off the far side of it.</summary>
    int Everything(Vector2 pointM, out float alongM)
    {
        alongM = 0f;
        var bestDistanceSq = float.MaxValue;
        var best = -1;
        for (var slot = 0; slot < ChainCount; slot++) Measure(slot, pointM, ref best, ref bestDistanceSq, ref alongM);

        return best;
    }

    /// <summary>
    /// One chain measured, kept only if it is nearer than what stands — or exactly as near and named by a
    /// lower id, <b>which is what makes the answer the walk's order rather than the grid's</b>.
    /// </summary>
    void Measure(int slot, Vector2 pointM, ref int best, ref float bestDistanceSq, ref float alongM)
    {
        var arcs = _arcs.AsSpan(_arcStart[slot], _arcStart[slot + 1] - _arcStart[slot]);
        var lengthM = _lengthM[slot];
        var atM = Spline.ProjectM(arcs, pointM, lengthM * 0.5f, lengthM);
        var distanceSq = (Spline.SampleAt(arcs, atM).PositionM - pointM).LengthSquared();
        if (distanceSq > bestDistanceSq || (distanceSq == bestDistanceSq && _chainId[slot] >= best)) return;

        bestDistanceSq = distanceSq;
        alongM = atM;
        best = _chainId[slot];
    }

    /// <summary>
    /// <b>One piece's box</b>: the piece walked, grown by half a step so what falls between samples is
    /// inside it. It is what the lattice is sized to cover and what a candidate query reads its cells by —
    /// <b>a superset of the cells the piece was written into</b> (<see cref="Builder.Bin"/>), which costs a
    /// query cells and never an answer.
    /// </summary>
    /// <remarks>
    /// <b>Offered to whoever sorts the candidates this index hands back</b> (<c>BandShell.Merge</c>), for
    /// the same reason the query reads its cells by it: a piece's own box is the cheapest thing that can
    /// be trusted to hold the whole of it, and a second way of boxing a piece is a second answer about
    /// which pieces a place could stand on.
    /// </remarks>
    public static void Box(ArcSeg arc, ref Vector2 leastM, ref Vector2 mostM)
    {
        var least = new Vector2(float.MaxValue);
        var most = new Vector2(float.MinValue);
        for (var atM = 0f; ; atM += SampleStepM)
        {
            var pointM = arc.PointAtM(MathF.Min(atM, arc.LengthM));
            least = Vector2.Min(least, pointM);
            most = Vector2.Max(most, pointM);
            if (atM >= arc.LengthM) break;
        }

        var margin = new Vector2(SampleStepM * 0.5f);
        leastM = Vector2.Min(leastM, least - margin);
        mostM = Vector2.Max(mostM, most + margin);
    }

    /// <summary>A corner taken down to the lattice the cell size lays over the map.</summary>
    static Vector2 Snapped(Vector2 cornerM, float cellM) =>
        new(MathF.Floor(cornerM.X / cellM) * cellM, MathF.Floor(cornerM.Y / cellM) * cellM);

    bool Range(Vector2 leastM, Vector2 mostM, out int fromX, out int fromY, out int toX, out int toY)
    {
        fromX = (int)MathF.Floor((leastM.X - _originM.X) * _inverseCellM);
        fromY = (int)MathF.Floor((leastM.Y - _originM.Y) * _inverseCellM);
        toX = (int)MathF.Floor((mostM.X - _originM.X) * _inverseCellM);
        toY = (int)MathF.Floor((mostM.Y - _originM.Y) * _inverseCellM);

        if (toX < 0 || toY < 0 || fromX >= _width || fromY >= _height) return false;

        fromX = Math.Max(fromX, 0);
        fromY = Math.Max(fromY, 0);
        toX = Math.Min(toX, _width - 1);
        toY = Math.Min(toY, _height - 1);
        return true;
    }

    /// <summary>
    /// The chains fed in one at a time, then sealed. Build-time only: it allocates freely, and what it
    /// produces is never written to again.
    /// </summary>
    internal sealed class Builder
    {
        readonly List<ArcSeg> _arcs = [];
        readonly List<int> _arcStart = [0];
        readonly List<float> _lengthM = [];
        readonly List<int> _chainId = [];
        Vector2 _leastM = new(float.MaxValue);
        Vector2 _mostM = new(float.MinValue);

        /// <param name="id">What <see cref="Nearest"/> hands back for this chain — the caller's own numbering, never a slot.</param>
        public void Add(int id, ReadOnlySpan<ArcSeg> arcs, float lengthM)
        {
            if (arcs.Length == 0) return;

            _chainId.Add(id);
            _lengthM.Add(lengthM);
            foreach (var arc in arcs)
            {
                _arcs.Add(arc);
                Box(arc, ref _leastM, ref _mostM);
            }

            _arcStart.Add(_arcs.Count);
        }

        public ChainIndex Seal(float cellSizeM)
        {
            var slots = _chainId.Count;
            var cellM = MathF.Max(cellSizeM, 1e-3f);
            if (slots == 0) return new ChainIndex([], [0], [], [], cellM, Vector2.Zero, 0, 0, [0], []);

            // <b>The origin is snapped down to a whole cell, so the lattice belongs to the map</b> — two
            // indexes sealed at one cell size then bin the same ground into the same cells, whatever each of
            // them covers. The snap is inside the loop because it moves the corner the cells are counted
            // from, and a cell that had to grow is a coarser lattice to snap to.
            var originM = Snapped(_leastM, cellM);
            while (Cells(Vector2.Max(_mostM - originM, Vector2.Zero), cellM) > MostCells)
            {
                cellM *= 2f;
                originM = Snapped(_leastM, cellM);
            }

            var inverse = 1f / cellM;
            var width = (int)MathF.Floor((_mostM.X - originM.X) * inverse) + 1;
            var height = (int)MathF.Floor((_mostM.Y - originM.Y) * inverse) + 1;
            var cells = width * height;

            // A counting sort, and the two passes are one method so they cannot disagree about which
            // cells a chain reaches — a count that missed one is a run written past its end.
            //
            // <b>And one cell is written once per piece</b>, which a walk of the piece cannot promise on its
            // own: a piece is sampled every metre and a cell is a road's width across, so a straight through
            // one is fourteen samples of the same cell. The mark is which piece last claimed a cell.
            var marked = new int[cells];
            var claim = 0;
            var counts = new int[cells];
            for (var slot = 0; slot < slots; slot++)
            {
                Bin(slot, originM, inverse, width, height, marked, ref claim, counts, null, null);
            }

            var start = new int[cells + 1];
            var at = 0;
            for (var cell = 0; cell < cells; cell++)
            {
                start[cell] = at;
                at += counts[cell];
            }

            start[cells] = at;

            var cursor = new int[cells];
            Array.Copy(start, cursor, cells);
            var entries = new int[at];
            for (var slot = 0; slot < slots; slot++)
            {
                Bin(slot, originM, inverse, width, height, marked, ref claim, null, cursor, entries);
            }

            return new ChainIndex(
                [.. _arcs], [.. _arcStart], [.. _lengthM], [.. _chainId], cellM, originM, width, height, start,
                entries);
        }

        /// <summary>
        /// <b>One chain's cells, either counted or written: the cells its pieces actually run through</b> and
        /// not the cells their boxes cover.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A box is not a line, and a diagonal one is not even close.</b> A straight piece running corner
        /// to corner across three cells has a box covering nine of them, so a query reading any of the six it
        /// never enters was handed that chain as a candidate for ground it is nowhere near.
        /// </para>
        /// <para>
        /// <b>What that came to is worth stating, because it is smaller than it looks.</b> A town's lines are
        /// chains of short pieces and a short piece's box is close to the piece; over the shipped city the
        /// boxes held <b>1.11 entries for every cell a piece really runs through</b> (1.03 on the laboratory
        /// map), so as a candidate count this was a tenth. <b>As a picture it was the whole of the fault</b>:
        /// the inflation is all in the few long straight pieces, and one diagonal lane laid as a single arc
        /// washed a three-by-three block of the debug layer's cells over open grass a street from any line.
        /// The index honestly held that, which is what the layer is for.
        /// </para>
        /// <para>
        /// <b>The piece is walked instead, and each sample claims the cells within half a step of it</b> —
        /// which covers the piece whatever it curves through, since no point of it stands more than half a
        /// step along the piece from a sample and a chord is never longer than its arc. The walk is the one
        /// the box was taken with (<see cref="SampleStepM"/>), so this costs the binning nothing it was not
        /// already paying.
        /// </para>
        /// <para>
        /// <b>A query may still read by the box</b> (<see cref="Crossing"/>) and stays sound doing it: where
        /// two lines cross, the crossing lies in one cell, that cell is inside the asking line's box, and the
        /// crossed line was written into it by the walk above — so reading a superset of cells over a table
        /// binned tightly finds every chain that a coarser table would have, and fewer that it would not.
        /// </para>
        /// </remarks>
        void Bin(
            int slot, Vector2 originM, float inverse, int width, int height, int[] marked, ref int claim,
            int[]? counts, int[]? cursor, int[]? entries)
        {
            for (var index = _arcStart[slot]; index < _arcStart[slot + 1]; index++)
            {
                var arc = _arcs[index];
                claim++;
                for (var atM = 0f; ; atM += SampleStepM)
                {
                    var pointM = arc.PointAtM(MathF.Min(atM, arc.LengthM));
                    var fromX = Cell(pointM.X - MarginM - originM.X, inverse, width);
                    var fromY = Cell(pointM.Y - MarginM - originM.Y, inverse, height);
                    var toX = Cell(pointM.X + MarginM - originM.X, inverse, width);
                    var toY = Cell(pointM.Y + MarginM - originM.Y, inverse, height);
                    for (var y = fromY; y <= toY; y++)
                    {
                        for (var x = fromX; x <= toX; x++)
                        {
                            var cell = (y * width) + x;
                            if (marked[cell] == claim) continue;

                            marked[cell] = claim;
                            if (counts is not null) counts[cell]++;
                            else entries![cursor![cell]++] = slot;
                        }
                    }

                    if (atM >= arc.LengthM) break;
                }
            }
        }

        static int Cell(float offsetM, float inverse, int extent) =>
            Math.Clamp((int)MathF.Floor(offsetM * inverse), 0, extent - 1);

        static long Cells(Vector2 spanM, float cellM) =>
            ((long)MathF.Floor(spanM.X / cellM) + 1) * ((long)MathF.Floor(spanM.Y / cellM) + 1);
    }
}
