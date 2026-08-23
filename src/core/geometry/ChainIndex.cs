using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// A fixed set of arc chains laid over a uniform grid, so that <b>which of them a point is nearest</b>
/// costs the ground around the point rather than the whole network.
/// </summary>
/// <remarks>
/// <para>
/// <b>The answer is the whole-network scan's, exactly.</b> The grid decides which chains are looked at
/// and nothing else: the survivors are put in ascending order and measured by the same arithmetic and
/// the same strictly-nearer test the scan used, so a tie still goes to the lower id and a town routed
/// through this one is routed the way it always was. What makes that safe is the stopping rule — the
/// ring is grown until the best distance found <em>fits inside the ring already searched</em>, and a
/// chain nearer than that has a piece inside that ring by construction.
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
    /// <summary>How finely a piece is walked when its box is taken. Build-time only.</summary>
    const float SampleStepM = 1f;

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

    int[] _candidate;
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
        _candidate = new int[Math.Max(64, Math.Min(chainId.Length, 256))];
    }

    /// <summary>How many chains were registered. A census, so a caller can say what its index is of.</summary>
    public int ChainCount => _chainId.Length;

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

    /// <summary>The slots whose pieces reach the ring, each once, in ascending order.</summary>
    /// <remarks>
    /// Ascending is the load-bearing part and is why they are sorted rather than measured as they are
    /// met: the grid hands cells back row by row, and measuring in that order would give a tie to
    /// whichever chain the grid happened to reach first instead of to the lower id.
    /// </remarks>
    void Gather(Vector2 pointM, float radiusM)
    {
        _generation++;
        _candidateCount = 0;

        var reach = new Vector2(radiusM);
        if (!Range(pointM - reach, pointM + reach, out var fromX, out var fromY, out var toX, out var toY)) return;

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
                    if (_candidateCount == _candidate.Length) Array.Resize(ref _candidate, _candidate.Length * 2);

                    _candidate[_candidateCount++] = slot;
                }
            }
        }

        Order(_candidate.AsSpan(0, _candidateCount));
    }

    /// <summary>
    /// An insertion sort, because that is what the list actually is — a point stands beside a handful of
    /// lines — and because the order it leaves behind is the whole of what keeps a tie reproducible.
    /// </summary>
    static void Order(Span<int> slots)
    {
        for (var at = 1; at < slots.Length; at++)
        {
            var slot = slots[at];
            var into = at - 1;
            while (into >= 0 && slots[into] > slot)
            {
                slots[into + 1] = slots[into];
                into--;
            }

            slots[into + 1] = slot;
        }
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
    /// One chain measured, kept only if it is <em>strictly</em> nearer than what stands — which over an
    /// ascending walk is what leaves a tie with the lower id.
    /// </summary>
    void Measure(int slot, Vector2 pointM, ref int best, ref float bestDistanceSq, ref float alongM)
    {
        var arcs = _arcs.AsSpan(_arcStart[slot], _arcStart[slot + 1] - _arcStart[slot]);
        var lengthM = _lengthM[slot];
        var atM = Spline.ProjectM(arcs, pointM, lengthM * 0.5f, lengthM);
        var distanceSq = (Spline.SampleAt(arcs, atM).PositionM - pointM).LengthSquared();
        if (distanceSq >= bestDistanceSq) return;

        bestDistanceSq = distanceSq;
        alongM = atM;
        best = _chainId[slot];
    }

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

            var spanM = Vector2.Max(_mostM - _leastM, Vector2.Zero);
            while (Cells(spanM, cellM) > MostCells) cellM *= 2f;

            var inverse = 1f / cellM;
            var width = (int)MathF.Floor(spanM.X * inverse) + 1;
            var height = (int)MathF.Floor(spanM.Y * inverse) + 1;
            var cells = width * height;

            // A counting sort, and the two passes are one method so they cannot disagree about which
            // cells a chain reaches — a count that missed one is a run written past its end.
            var counts = new int[cells];
            for (var slot = 0; slot < slots; slot++) Bin(slot, inverse, width, height, counts, null, null);

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
            for (var slot = 0; slot < slots; slot++) Bin(slot, inverse, width, height, null, cursor, entries);

            return new ChainIndex(
                [.. _arcs], [.. _arcStart], [.. _lengthM], [.. _chainId], cellM, _leastM, width, height, start,
                entries);
        }

        /// <summary>
        /// One chain's cells, either counted or written. A chain reaching the same cell in two of its
        /// pieces is entered twice and measured once — the query stamps a chain the first time it meets
        /// it, so the duplicate costs a slot in the table and nothing in the answer.
        /// </summary>
        void Bin(int slot, float inverse, int width, int height, int[]? counts, int[]? cursor, int[]? entries)
        {
            for (var index = _arcStart[slot]; index < _arcStart[slot + 1]; index++)
            {
                var least = new Vector2(float.MaxValue);
                var most = new Vector2(float.MinValue);
                Box(_arcs[index], ref least, ref most);

                var fromX = Cell(least.X - _leastM.X, inverse, width);
                var fromY = Cell(least.Y - _leastM.Y, inverse, height);
                var toX = Cell(most.X - _leastM.X, inverse, width);
                var toY = Cell(most.Y - _leastM.Y, inverse, height);
                for (var y = fromY; y <= toY; y++)
                {
                    for (var x = fromX; x <= toX; x++)
                    {
                        var cell = y * width + x;
                        if (counts is not null) counts[cell]++;
                        else entries![cursor![cell]++] = slot;
                    }
                }
            }
        }

        static int Cell(float offsetM, float inverse, int extent) =>
            Math.Clamp((int)MathF.Floor(offsetM * inverse), 0, extent - 1);

        static long Cells(Vector2 spanM, float cellM) =>
            ((long)MathF.Floor(spanM.X / cellM) + 1) * ((long)MathF.Floor(spanM.Y / cellM) + 1);

        /// <summary>One piece's box: the piece walked, grown by half a step so what falls between samples is inside it.</summary>
        static void Box(ArcSeg arc, ref Vector2 leastM, ref Vector2 mostM)
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
    }
}
