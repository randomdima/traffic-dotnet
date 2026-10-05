using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The marks, worked out from the ribbons themselves</b> (TER-5c): for every two ways on a channel in common whose
/// ribbons overlap, the stretch of each over which they do — from the first metre of one whose ground lies inside
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
/// <para>
/// <b>A zebra is the one place the ribbons do not say how far a mark runs</b> (TER-5c.3). Each of its walking
/// lanes is marked against every way the traffic drives under either of them, all of the lane against the
/// whole of what the zebra covers of that way — so it is held as one piece of ground from both sides.
/// </para>
/// </remarks>
internal static class RibbonMarks
{
    /// <summary>What <see cref="IRibbonLines.ZebraOf"/> answers for a way that paints no zebra.</summary>
    public const int NoZebra = -1;

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
    /// <param name="nearLevel">
    /// The level of the grid the search for pieces near each other is filed at (SIM-8). <b>A bound on the
    /// work and not a figure anything reads</b>: every two pieces whose boxes meet are weighed whatever it is.
    /// </param>
    public static WayCrossings Of(IRibbonLines lines, float[] lengthM, float stepM, float touchM, GridLevel nearLevel)
    {
        // Two ways on no channel in common — a bridge's lane and the road under it — share no ground however they lie
        // in plan.
        var channels = RibbonAtlas.ChannelsOf(lines);
        var pairings = new List<Pairing>();
        new Near(PiecesOf(lines), nearLevel).EachPair(
            () => new Pairing(),
            (pairing, one, other) =>
            {
                if (channels.Length == 0 || (channels[one.Way] & channels[other.Way]) != 0) Weigh(pairing, one, other, stepM, touchM);
            },
            pairing =>
            {
                lock (pairings) pairings.Add(pairing);
            });

        return Filed(lines, pairings, lengthM);
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
    /// <b>Which pieces stand near which</b>: every piece filed under each cell of the grid its box covers, so
    /// that two pieces are only ever weighed where their boxes meet.
    /// </summary>
    sealed class Near
    {
        readonly RibbonPiece[] _pieces;
        readonly GridWindow _window;
        readonly int[] _cellFirst;
        readonly int[] _inCell;

        public Near(RibbonPiece[] pieces, GridLevel level)
        {
            _pieces = pieces;
            var leastM = new Vector2(float.MaxValue);
            var mostM = new Vector2(float.MinValue);
            foreach (var piece in pieces)
            {
                leastM = Vector2.Min(leastM, piece.LeastM);
                mostM = Vector2.Max(mostM, piece.MostM);
            }

            if (pieces.Length == 0) leastM = mostM = Vector2.Zero;

            _window = GridWindow.Over(level, leastM, mostM);

            _cellFirst = new int[_window.Count + 1];
            foreach (var piece in pieces)
            {
                var range = Cells(piece);
                for (var row = range.FromY; row <= range.ToY; row++)
                {
                    for (var column = range.FromX; column <= range.ToX; column++)
                    {
                        _cellFirst[_window.IndexOf(column, row) + 1]++;
                    }
                }
            }

            for (var cell = 0; cell < _window.Count; cell++) _cellFirst[cell + 1] += _cellFirst[cell];

            _inCell = new int[_cellFirst[^1]];
            var cursor = (int[])_cellFirst.Clone();
            for (var index = 0; index < pieces.Length; index++)
            {
                var range = Cells(pieces[index]);
                for (var row = range.FromY; row <= range.ToY; row++)
                {
                    for (var column = range.FromX; column <= range.ToX; column++)
                    {
                        _inCell[cursor[_window.IndexOf(column, row)]++] = index;
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
                _window.Count,
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
                            if (_window.IndexAt(Vector2.Max(one.LeastM, other.LeastM)) != cell) continue;

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

        CellRange Cells(in RibbonPiece piece)
        {
            _window.TryRange(piece.LeastM, piece.MostM, out var range);
            return range;
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
        public readonly Dictionary<long, int> At = new(PairHash.Instance);
        public readonly List<Shared> Pairs = [];
    }

    /// <summary>
    /// <b>A pair's key hashed from both halves mixed.</b> A <see cref="long"/>'s own hash is its two halves
    /// xored, and two ways that share ground are nearly always numbered close together, so their pairs would
    /// fall in a few buckets and every lookup walk them — the square of the town's pairs.
    /// </summary>
    sealed class PairHash : IEqualityComparer<long>
    {
        public static readonly PairHash Instance = new();

        public bool Equals(long one, long other) => one == other;

        public int GetHashCode(long key) => (int)(((ulong)key * 0x9E3779B97F4A7C15UL) >> 32);
    }

    /// <summary>What every thread found, merged, and filed under both ways of every pair.</summary>
    static WayCrossings Filed(IRibbonLines lines, List<Pairing> pairings, float[] lengthM)
    {
        var wayCount = lines.WayCount;
        var merged = new Pairing();
        foreach (var pairing in pairings)
        {
            foreach (var pair in pairing.Pairs)
            {
                Share(merged, pair.One, pair.OneFromM, pair.OneToM, pair.Other, pair.OtherFromM, pair.OtherToM);
            }
        }

        var filed = new List<CrossedSection>[wayCount];
        foreach (var pair in ZebrasWhole(lines, merged, lengthM).Pairs)
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

            // Ordered by way where two begin together — every mark of a zebra's lane begins at its kerb — so the
            // order secondary claims are placed in does not turn on which thread found a pair first.
            mine.Sort(static (one, other) => one.MineFromM != other.MineFromM
                ? one.MineFromM.CompareTo(other.MineFromM)
                : one.OnWay.CompareTo(other.OnWay));
            mine.CopyTo(sections, offsets[way]);
        }

        return new WayCrossings(offsets, sections) { MostCrossedByOne = most };
    }

    /// <summary>
    /// <b>Every zebra marked whole</b> (TER-5c.3): the pairs of one of its walking lanes and a driven way
    /// taken out, and in their place every walking lane of that zebra against every driven way any of them lay
    /// over — the whole lane, and the stretch of the driven way from where the first of them came onto it to
    /// where the last left it. Every other pair is kept as the ribbons found it.
    /// </summary>
    /// <remarks>
    /// <b>Driven ways and no others</b>: the pavement a zebra hands over to at a kerb shares ground with its end
    /// the way any two walks do, and is weighed over that ground and not over the road.
    /// </remarks>
    static Pairing ZebrasWhole(IRibbonLines lines, Pairing merged, float[] lengthM)
    {
        var paint = new Dictionary<int, List<int>>();
        for (var way = 0; way < lines.WayCount; way++)
        {
            var zebra = lines.ZebraOf(way);
            if (zebra == NoZebra) continue;

            if (!paint.TryGetValue(zebra, out var lanes)) paint[zebra] = lanes = [];
            lanes.Add(way);
        }

        if (paint.Count == 0) return merged;

        var kept = new Pairing();

        // Keyed by zebra and driven way: every lane of the zebra is marked over the one stretch, so the stretch
        // is gathered from all of them first.
        var under = new Dictionary<long, (float FromM, float ToM)>();
        foreach (var pair in merged.Pairs)
        {
            if (!Under(lines, pair, out var zebra, out var driven, out var fromM, out var toM))
            {
                Share(kept, pair.One, pair.OneFromM, pair.OneToM, pair.Other, pair.OtherFromM, pair.OtherToM);
                continue;
            }

            var key = ((long)zebra << 32) | (uint)driven;
            under[key] = under.TryGetValue(key, out var was)
                ? (MathF.Min(was.FromM, fromM), MathF.Max(was.ToM, toM))
                : (fromM, toM);
        }

        foreach (var (key, stretch) in under)
        {
            var driven = (int)(uint)key;
            foreach (var lane in paint[(int)(key >> 32)])
            {
                Share(kept, lane, 0f, lengthM[lane], driven, stretch.FromM, stretch.ToM);
            }
        }

        return kept;
    }

    /// <summary>Whether a pair is a zebra's walking lane over a driven way, and the stretch of the driven way it covers.</summary>
    static bool Under(
        IRibbonLines lines, in Shared pair, out int zebra, out int driven, out float fromM, out float toM)
    {
        zebra = lines.ZebraOf(pair.One);
        if (zebra != NoZebra && lines.ZebraOf(pair.Other) == NoZebra && lines.IsDriven(pair.Other))
        {
            (driven, fromM, toM) = (pair.Other, pair.OtherFromM, pair.OtherToM);
            return true;
        }

        zebra = lines.ZebraOf(pair.Other);
        if (zebra != NoZebra && lines.ZebraOf(pair.One) == NoZebra && lines.IsDriven(pair.One))
        {
            (driven, fromM, toM) = (pair.One, pair.OneFromM, pair.OneToM);
            return true;
        }

        (driven, fromM, toM) = (0, 0f, 0f);
        return false;
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
