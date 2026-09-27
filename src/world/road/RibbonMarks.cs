using System.Numerics;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The marks, worked out from the ribbons themselves</b> (TER-5c): for every two ways whose ribbons
/// overlap, the stretch of each over which they do — from the first metre of one whose ground lies inside
/// the other's to the last. Build-time: it allocates freely, runs on as many threads as there are, and
/// nothing it produces is written to again.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exact, and not sampled.</b> Each ribbon is its pieces (<see cref="RibbonPiece"/>); a metre of one
/// piece meets another's ground where the slice across that metre does, which is a segment against a
/// rectangle or an annular sector. Each piece is walked a lattice step at a time for the first and last
/// metre that meets, and each end is then closed in on by halving — so a section ends where the two
/// ribbons part, and not at the last point of some lattice that lay inside both. Ground a piece shares over
/// less than a lattice step of its own line can fall between two of those steps, which is the resolution a
/// body is read at too.
/// </para>
/// <para>
/// <b>Shared ground is ground deeper than the touch inside both</b>
/// (<see cref="Core.Config.SimConfig.RibbonTouchM"/>): each ribbon is worn back by it, sides and ends alike,
/// and what the two worn ribbons hold in common is what is marked. <b>A section stops where the two are
/// almost touching</b>, and not where they last touch: a pavement edge whose square end grazes a lane is
/// marked over the graze and not over the whole of itself. Ribbons laid edge to edge — the two lanes of a
/// carriageway, a lane and the way that carries on from its end — share nothing worn and are never marked.
/// </para>
/// <para>
/// <b>One section per pair.</b> Two ribbons that share ground in two places are given the one section that
/// spans both, which holds the ground between as well: a stretch that needs both places needs what lies
/// between them.
/// </para>
/// </remarks>
internal static class RibbonMarks
{
    /// <summary>
    /// How wide a cell of the search for pieces near each other is, in lattice steps. <b>A bound on the work
    /// and not a figure anything reads</b>: every two pieces whose boxes meet are weighed whatever it is.
    /// </summary>
    const int CellInSteps = 16;

    /// <summary>
    /// How many times an end of a section is closed in on once a step has bracketed it — a lattice step
    /// halved this often is under a micrometre.
    /// </summary>
    const int Halvings = 20;

    /// <param name="stepM">
    /// How far apart the metres a piece is walked at stand — the atlas's own lattice step, below which it
    /// resolves nothing either.
    /// </param>
    /// <param name="touchM">How deep inside both two ribbons their ground has to lie to be shared.</param>
    public static WayCrossings Of(IRibbonLines lines, float[] lengthM, float stepM, float touchM)
    {
        var pairings = new List<Pairing>();
        new Near(PiecesOf(lines), stepM * CellInSteps).EachPair(
            () => new Pairing(),
            (pairing, one, other) => Weigh(pairing, one, other, stepM, touchM),
            pairing =>
            {
                lock (pairings) pairings.Add(pairing);
            });

        return Filed(lines.WayCount, pairings, lengthM);
    }

    /// <summary>Every piece of every way's line, swept to half that way's width.</summary>
    static RibbonPiece[] PiecesOf(IRibbonLines lines)
    {
        var pieces = new List<RibbonPiece>();
        for (var way = 0; way < lines.WayCount; way++)
        {
            var arcs = lines.LineOf(way, out var widthM);
            if (widthM <= 0f) continue;

            var fromM = 0f;
            foreach (var arc in arcs)
            {
                if (arc.LengthM > 0f) pieces.Add(new RibbonPiece(way, fromM, arc, widthM * 0.5f));
                fromM += arc.LengthM;
            }
        }

        return [.. pieces];
    }

    static long KeyOf(int oneWay, int otherWay) =>
        oneWay < otherWay ? ((long)oneWay << 32) | (uint)otherWay : ((long)otherWay << 32) | (uint)oneWay;

    /// <summary>
    /// <b>What two pieces of two ways share</b>: the two each worn back by the touch, sides and ends alike,
    /// and the stretch of each whose worn ground meets the other's — shared into the pair's section. Ground two
    /// ways hold only along an edge, or across the square ends where one hands over to the next, is left with
    /// nothing.
    /// </summary>
    static void Weigh(Pairing pairing, in RibbonPiece one, in RibbonPiece other, float stepM, float touchM)
    {
        var worn = new Ground(one.HalfM - touchM, other.HalfM - touchM, touchM);
        if (worn.HalfM <= 0f
            || worn.OtherHalfM <= 0f
            || Apart(one, other, worn)
            || !Stretch(one, other, worn, touchM, one.LengthM - touchM, stepM, out var oneFromM, out var oneToM)
            || !Stretch(
                other, one, worn.Mirrored, touchM, other.LengthM - touchM, stepM, out var otherFromM,
                out var otherToM))
        {
            return;
        }

        Share(
            pairing, one.Way, one.WayFromM + oneFromM, one.WayFromM + oneToM, other.Way,
            other.WayFromM + otherFromM, other.WayFromM + otherToM);
    }

    /// <summary>Two straights whose lines stand further apart than their ground reaches, told without walking either.</summary>
    static bool Apart(in RibbonPiece one, in RibbonPiece other, Ground ground) =>
        one.IsStraight && other.IsStraight
                       && RibbonPiece.StraightsFurtherApart(one, other, ground.HalfM + ground.OtherHalfM);

    /// <summary>
    /// The ground two pieces are weighed at: the half-width of the one being walked, the half-width of the one
    /// it is weighed against, and how far in from each of that one's ends its ground begins.
    /// </summary>
    readonly record struct Ground(float HalfM, float OtherHalfM, float TrimM)
    {
        public Ground Mirrored => new(OtherHalfM, HalfM, TrimM);
    }

    /// <summary>
    /// <b>The first and last metre of <c>[fromM, toM]</c> of a piece whose slice meets the other's
    /// ground</b> — false where none does.
    /// </summary>
    static bool Stretch(
        in RibbonPiece piece, in RibbonPiece other, Ground ground, float fromM, float toM, float stepM,
        out float firstM, out float lastM)
    {
        firstM = lastM = 0f;
        if (!Near.Reaching(piece, other, ground.HalfM, ref fromM, ref toM)) return false;

        var steps = Math.Max(1, (int)MathF.Ceiling((toM - fromM) / stepM));
        var first = -1;
        for (var step = 0; step <= steps; step++)
        {
            if (!MeetsAt(piece, other, ground, At(step))) continue;

            first = step;
            break;
        }

        if (first < 0) return false;

        var last = first;
        for (var step = steps; step > first; step--)
        {
            if (!MeetsAt(piece, other, ground, At(step))) continue;

            last = step;
            break;
        }

        firstM = first == 0 ? fromM : Edge(piece, other, ground, At(first), At(first - 1));
        lastM = last == steps ? toM : Edge(piece, other, ground, At(last), At(last + 1));
        return true;

        float At(int step) => fromM + ((toM - fromM) * step / steps);
    }

    /// <summary>
    /// Where a piece's ground stops meeting the other's, closed in on from a metre that meets towards one that
    /// does not — and the metre that meets is the one kept.
    /// </summary>
    static float Edge(in RibbonPiece piece, in RibbonPiece other, Ground ground, float meetsM, float missesM)
    {
        for (var halving = 0; halving < Halvings; halving++)
        {
            var midM = (meetsM + missesM) * 0.5f;
            if (MeetsAt(piece, other, ground, midM)) meetsM = midM;
            else missesM = midM;
        }

        return meetsM;
    }

    static bool MeetsAt(in RibbonPiece piece, in RibbonPiece other, Ground ground, float onM)
    {
        piece.SliceAt(onM, ground.HalfM, out var fromX, out var fromY, out var toX, out var toY);
        return other.Meets(fromX, fromY, toX, toY, ground.OtherHalfM, ground.TrimM);
    }

    /// <summary>
    /// <b>Which pieces stand near which</b>: every piece filed under each cell of a coarse grid its box
    /// covers, so that two pieces are only ever weighed where their boxes meet.
    /// </summary>
    sealed class Near
    {
        readonly RibbonPiece[] _pieces;
        readonly Vector2 _leastM;
        readonly float _cellM;
        readonly int _columns;
        readonly int _rows;
        readonly int[] _cellFirst;
        readonly int[] _inCell;

        public Near(RibbonPiece[] pieces, float cellM)
        {
            _pieces = pieces;
            _cellM = cellM;
            var leastM = new Vector2(float.MaxValue);
            var mostM = new Vector2(float.MinValue);
            foreach (var piece in pieces)
            {
                leastM = Vector2.Min(leastM, piece.LeastM);
                mostM = Vector2.Max(mostM, piece.MostM);
            }

            if (pieces.Length == 0) leastM = mostM = Vector2.Zero;

            _leastM = leastM;
            _columns = (int)((mostM.X - leastM.X) / cellM) + 1;
            _rows = (int)((mostM.Y - leastM.Y) / cellM) + 1;

            _cellFirst = new int[(_columns * _rows) + 1];
            foreach (var piece in pieces)
            {
                Cells(piece, out var from, out var to);
                for (var row = from / _columns; row <= to / _columns; row++)
                {
                    for (var column = from % _columns; column <= to % _columns; column++)
                    {
                        _cellFirst[(row * _columns) + column + 1]++;
                    }
                }
            }

            for (var cell = 0; cell < _columns * _rows; cell++) _cellFirst[cell + 1] += _cellFirst[cell];

            _inCell = new int[_cellFirst[^1]];
            var cursor = (int[])_cellFirst.Clone();
            for (var index = 0; index < pieces.Length; index++)
            {
                Cells(pieces[index], out var from, out var to);
                for (var row = from / _columns; row <= to / _columns; row++)
                {
                    for (var column = from % _columns; column <= to % _columns; column++)
                    {
                        _inCell[cursor[(row * _columns) + column]++] = index;
                    }
                }
            }
        }

        /// <summary>
        /// Every two pieces of two ways whose boxes meet, once each — in the cell the corner of the two boxes'
        /// overlap falls in — on as many threads as there are.
        /// </summary>
        public void EachPair<TWorking>(
            Func<TWorking> working, Action<TWorking, RibbonPiece, RibbonPiece> pass, Action<TWorking> spent)
        {
            InChunks.Over(
                _columns * _rows,
                working,
                (own, cell) =>
                {
                    for (var at = _cellFirst[cell]; at < _cellFirst[cell + 1]; at++)
                    {
                        var one = _pieces[_inCell[at]];
                        for (var next = at + 1; next < _cellFirst[cell + 1]; next++)
                        {
                            var other = _pieces[_inCell[next]];
                            if (one.Way == other.Way || !one.BoxMeets(other)) continue;
                            if (CellOf(Vector2.Max(one.LeastM, other.LeastM)) != cell) continue;

                            pass(own, one, other);
                        }
                    }
                },
                spent);
        }

        /// <summary>
        /// <c>[fromM, toM]</c> held to the metres of a straight whose slice reaches the other's box at all; an
        /// arc is walked whole.
        /// </summary>
        public static bool Reaching(in RibbonPiece piece, in RibbonPiece other, float halfM, ref float fromM, ref float toM)
        {
            if (!piece.IsStraight) return fromM <= toM;

            piece.SliceAt(0.0, halfM, out var fromX, out var fromY, out var toX, out var toY);
            piece.SliceAt(1.0, halfM, out var onFromX, out var onFromY, out _, out _);
            return Within(
                       Math.Min(fromX, toX), Math.Max(fromX, toX), onFromX - fromX, other.LeastM.X, other.MostM.X,
                       ref fromM, ref toM)
                   && Within(
                       Math.Min(fromY, toY), Math.Max(fromY, toY), onFromY - fromY, other.LeastM.Y, other.MostM.Y,
                       ref fromM, ref toM);

            // One axis of the slice, from least to most at the piece's start and moving on by step a metre,
            // overlapping least to most of the box.
            static bool Within(
                double sliceLeast, double sliceMost, double step, float boxLeast, float boxMost, ref float fromM,
                ref float toM)
            {
                if (step == 0.0) return sliceMost >= boxLeast && sliceLeast <= boxMost && fromM <= toM;

                var reachesM = (float)((boxLeast - sliceMost) / step);
                var passesM = (float)((boxMost - sliceLeast) / step);
                fromM = MathF.Max(fromM, MathF.Min(reachesM, passesM));
                toM = MathF.Min(toM, MathF.Max(reachesM, passesM));
                return fromM <= toM;
            }
        }

        void Cells(in RibbonPiece piece, out int from, out int to)
        {
            from = CellOf(piece.LeastM);
            to = CellOf(piece.MostM);
        }

        int CellOf(Vector2 atM)
        {
            var column = Math.Clamp((int)((atM.X - _leastM.X) / _cellM), 0, _columns - 1);
            var row = Math.Clamp((int)((atM.Y - _leastM.Y) / _cellM), 0, _rows - 1);
            return (row * _columns) + column;
        }
    }

    /// <summary>What one pair of ribbons was found to share, in each one's own metres.</summary>
    struct Shared
    {
        public int One;
        public int Other;
        public float OneFromM;
        public float OneToM;
        public float OtherFromM;
        public float OtherToM;
    }

    /// <summary>One thread's pairs, keyed by the pair and held until every cell has been read.</summary>
    sealed class Pairing
    {
        public readonly Dictionary<long, int> At = [];
        public readonly List<Shared> Pairs = [];
    }

    /// <summary>What every thread found, merged, and filed under both ways of every pair.</summary>
    static WayCrossings Filed(int wayCount, List<Pairing> pairings, float[] lengthM)
    {
        var merged = new Pairing();
        foreach (var pairing in pairings)
        {
            foreach (var pair in pairing.Pairs)
            {
                Share(merged, pair.One, pair.OneFromM, pair.OneToM, pair.Other, pair.OtherFromM, pair.OtherToM);
            }
        }

        var filed = new List<CrossedSection>[wayCount];
        foreach (var pair in merged.Pairs)
        {
            var oneFromM = MathF.Max(0f, pair.OneFromM);
            var oneToM = MathF.Min(lengthM[pair.One], pair.OneToM);
            var otherFromM = MathF.Max(0f, pair.OtherFromM);
            var otherToM = MathF.Min(lengthM[pair.Other], pair.OtherToM);

            // A main claim is answered off its own way only because every mark's other side places a secondary
            // claim back onto it (WayCrossings), so a pair is ground on both its ways or on neither.
            if (oneToM <= oneFromM || otherToM <= otherFromM) continue;

            (filed[pair.One] ??= []).Add(new CrossedSection(pair.Other, otherFromM, otherToM, oneFromM, oneToM));
            (filed[pair.Other] ??= []).Add(new CrossedSection(pair.One, oneFromM, oneToM, otherFromM, otherToM));
        }

        var offsets = new int[wayCount + 1];
        var most = 0;
        for (var way = 0; way < wayCount; way++)
        {
            var count = filed[way]?.Count ?? 0;
            offsets[way + 1] = offsets[way] + count;
            most = Math.Max(most, count);
        }

        var sections = new CrossedSection[offsets[wayCount]];
        for (var way = 0; way < wayCount; way++)
        {
            if (filed[way] is not { } mine) continue;

            mine.Sort(static (one, other) => one.MineFromM.CompareTo(other.MineFromM));
            mine.CopyTo(sections, offsets[way]);
        }

        return new WayCrossings(offsets, sections) { MostCrossedByOne = most };
    }

    static void Share(
        Pairing pairing, int oneWay, float oneFromM, float oneToM, int otherWay, float otherFromM, float otherToM)
    {
        if (oneWay > otherWay)
        {
            (oneWay, otherWay) = (otherWay, oneWay);
            (oneFromM, otherFromM) = (otherFromM, oneFromM);
            (oneToM, otherToM) = (otherToM, oneToM);
        }

        var key = KeyOf(oneWay, otherWay);
        if (!pairing.At.TryGetValue(key, out var at))
        {
            pairing.At[key] = pairing.Pairs.Count;
            pairing.Pairs.Add(new Shared
            {
                One = oneWay, Other = otherWay, OneFromM = oneFromM, OneToM = oneToM, OtherFromM = otherFromM,
                OtherToM = otherToM,
            });
            return;
        }

        var shared = pairing.Pairs[at];
        shared.OneFromM = MathF.Min(shared.OneFromM, oneFromM);
        shared.OneToM = MathF.Max(shared.OneToM, oneToM);
        shared.OtherFromM = MathF.Min(shared.OtherFromM, otherFromM);
        shared.OtherToM = MathF.Max(shared.OtherToM, otherToM);
        pairing.Pairs[at] = shared;
    }
}
