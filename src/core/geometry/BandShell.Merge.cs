using System.Numerics;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.Core.Geometry;

internal sealed partial class BandShell
{
    /// <summary>
    /// <b>Every ribbon cut at every crossing it has, and what is left of them strung into rings.</b> It is
    /// the whole of the merge, and it asks two questions: where two ribbon boundaries cross, and whether the
    /// ground just outside a piece is on any ribbon at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both questions are exact and neither is a walk.</b> A crossing is the closed form two arcs have
    /// (<see cref="Spline.CrossingsOf(in ArcSeg, in ArcSeg, Span{float}, Span{float})"/>), so a cut lands
    /// where the boundaries really meet rather than where a probe happened to notice they had. And whether a
    /// place is on a ribbon is a distance to that ribbon's own line, compared against half its width — the
    /// definition of the band, asked of the band, rather than anything reconstructed from its edges.
    /// </para>
    /// <para>
    /// <b>A pair of ribbons is solved once and both of them are cut by it</b> (<see cref="Cut"/>). Solved
    /// twice — once walking each ribbon — the two answers are the same crossing found from two frames, and
    /// two pieces that have to stop at one place stop a hair apart instead. On a shallow crossing that hair
    /// is wide enough to break the ring.
    /// </para>
    /// <para>
    /// <b>And a cut is a place: every boundary standing at one is cut there</b> (<see cref="Alike"/>). A
    /// car park lays a dozen bands along one lane — every bay's way opens with a straight down the lane it
    /// is worked off — and their boundaries stand millimetres apart over metres. Which of them is outermost
    /// changes over the length of a stretch, so neighbours that are cut alike weigh the same gap at the same
    /// station and one of them keeps it, where neighbours cut differently read the same gap from either
    /// side of the figure that settles it and keep it twice or drop it twice.
    /// </para>
    /// <para>
    /// <b>The cover test is asked a hair <em>outside</em> the piece and never on it</b>
    /// (<see cref="ProbeM"/>). Two lanes of one carriageway share an edge exactly: every point of it lies on
    /// the boundary of both bands and inside neither, so a test taken on the line keeps a seam down the
    /// middle of every pair of them. Taken a millimetre out, the seam's two copies each land inside the
    /// other lane and both go, which is what makes a carriageway one shape rather than two bands touching.
    /// </para>
    /// <para>
    /// <b>What stands at one place is weighed over that place at once and not band by band</b>
    /// (<see cref="Run"/>). The bands whose edges stand where this one does are a run, read off in the order
    /// their edges stand in, and the run has one lowest number wherever the question is asked from. Weighed
    /// in pairs instead, a band a hair beyond the figure covers the edge while the band between them hands
    /// that same edge back to it, and every copy of one stretch of the outline is dropped.
    /// </para>
    /// <para>
    /// <b>Nothing is asked about which ribbon a piece belongs to.</b> A ribbon is cut against its own other
    /// pieces as readily as against a neighbour's, so a line that bends tighter than half its own width —
    /// whose inner edge runs back through the band it bounds — is covered by the same rule that covers the
    /// seam, and needs no case.
    /// </para>
    /// </remarks>
    sealed class Merge
    {
        /// <summary>
        /// How far outside a piece the cover test is taken. <b>A millimetre, because the error it has to
        /// clear is the arithmetic's and not the town's</b>: two edges the plan lays at one place are two
        /// sums of the same numbers and stand a few hundredths of a millimetre apart, while the narrowest
        /// thing the town lays is metres wide. Anything between those two is the same answer.
        /// </summary>
        const float ProbeM = LineTolerance.RoundingM;

        /// <summary>
        /// <b>How near a boundary stands to another band before the two are the same edge</b>. Two
        /// millimetres: the town means a movement to carry on from the lane it leaves and means two lanes to
        /// share the edge between them, and what two computations of one place actually leave is a hair of
        /// grass or a hair of overlap. Nearer than this the two are one edge and the tie is settled
        /// (<see cref="Covered"/>); further, each is weighed on its own.
        /// </summary>
        /// <remarks>
        /// <b>It is the arithmetic's error and not a slit worth closing.</b> Widened to a few centimetres it
        /// ties edges that really are apart, and the merge then keeps the inner of two and cuts the corner
        /// off the town; the run of open boundary on a city is worse at every figure above this one that was
        /// measured. What it may not be is nought: the seam between two bands the town laid at one place has
        /// no side to be on, and weighed strictly both copies of it are kept or both are dropped.
        /// </remarks>
        const float CoincidentM = 0.002f;

        /// <summary>
        /// <b>How wide a slit between two bands that face each other is still the two of them touching</b>. Two
        /// centimetres: a lane and the movement that carries on from it, or two movements through one box, are
        /// laid to meet, and what the two computations leave between them is a slit of grass a hair wide.
        /// Narrower than this the two bands touch and the slit is inside the shape; wider, it is ground no
        /// band covers and the boundary goes round it.
        /// </summary>
        /// <remarks>
        /// <b>It is a wider figure than <see cref="CoincidentM"/> because it answers a different question.</b>
        /// That one is how near two edges of bands lying the same way round are one edge, which is settled by
        /// the number and so must not tie bands that really are apart. This one is how wide a gap between two
        /// bands facing each other is nothing, which is settled by the geometry either way round and costs
        /// only the slit itself. The town lays nothing narrower than metres, so a strip of grass a centimetre
        /// across is the arithmetic's and not the plan's — and the boundary of one comes back as a needle two
        /// stretches long with the driven ground on both sides of it.
        /// </remarks>
        const float TouchingM = 0.02f;

        /// <summary>
        /// How near two cut ends stand to be the same end, and the shortest stretch worth keeping — both
        /// the stringing's (<see cref="ArcRings"/>), because what a cut may leave and what a walk can close
        /// are one figure asked from the two ends of the same construction.
        /// </summary>
        const float WeldM = ArcRings.WeldM;

        const float LeastPieceM = ArcRings.LeastPieceM;

        /// <summary>
        /// <b>How squarely a band has to face the walker before the gap between them is a slit</b> rather
        /// than a crossing. Half, which is a third of a turn: a band lying along this edge faces square
        /// across it either way round, and one whose square end is cutting across faces along it — and a slit
        /// is a thing two bands lying along each other have.
        /// </summary>
        const float Squarely = 0.5f;

        /// <summary>Two circles cross at two places and two lines at one, so a pair of pieces has no more than two.</summary>
        const int MostCrossings = 2;

        /// <summary>The lines the bands are laid along, which is what a cover test is really asked of.</summary>
        readonly ArcSeg[][] _along;

        readonly ArcSeg[][] _ribbons;
        readonly float[] _halfM;
        readonly float[] _lengthM;
        readonly float _mostHalfM;

        /// <summary>The caller's index of those lines, which answers which of them are near a place.</summary>
        readonly ChainIndex _lines;

        /// <summary>
        /// <b>And an index of the ribbons' own pieces</b>, numbered as the cuts are, which answers which
        /// pieces could cross which and which stand at a place.
        /// </summary>
        /// <remarks>
        /// <b>Pieces and not ribbons, because what is asked of it is about a piece.</b> A ribbon is a dozen
        /// pieces of which one is near any given place, so an index of ribbons hands back the other eleven
        /// as well and every one of them is then weighed against its own box
        /// (<see cref="Within"/>) — which is the box test the index exists to have done already.
        /// </remarks>
        readonly ChainIndex _edges;

        /// <summary>Where one ribbon's pieces start in the flat numbering the cuts are held under.</summary>
        readonly int[] _firstPiece;

        /// <summary>And which ribbon each of those pieces belongs to, which is what the index hands back.</summary>
        readonly int[] _lineOfPiece;

        /// <summary>The distances along each piece that another boundary crosses it, or null where none does.</summary>
        readonly List<float>?[] _cutAtM;

        /// <summary>
        /// <b>The box each piece stands in</b>, which is the index's own (<see cref="ChainIndex.Box"/>) and
        /// never a second reading of the same piece. Two pieces whose boxes do not meet within a weld have
        /// neither a crossing nor an end on one another, and a place outside one is a place no cut of that
        /// piece can be at — so both are skipped without being approximated.
        /// </summary>
        /// <remarks>
        /// <b>A box and not the reach a piece has about its own middle</b>, which is the same question
        /// answered twice over: half a piece's length bounds how far it reaches, so a long straight admits
        /// every place within half its length of its middle in any direction at all. Both are supersets of
        /// what really touches and the answer is the same either way — a city's boundary comes back piece
        /// for piece — and this one is the index's own, so where a piece stands is stated once.
        /// </remarks>
        readonly Vector2[] _leastM;
        readonly Vector2[] _mostM;

        /// <summary>
        /// <b>One thread's working set for cutting</b>: what it may ask the index of pieces, and the cuts it
        /// has filed. <b>The cuts are held rather than written</b> because a cut lands on whichever piece
        /// stands at the place, which is any piece in the town — so two threads filing at once would be
        /// writing into one another's lists.
        /// </summary>
        /// <remarks>
        /// <b>Held costs nothing, because what is filed is a multiset.</b> Every list of cuts is sorted
        /// before it is read (<see cref="Alike"/>, <see cref="Uncovered"/>), so the order they are drained in
        /// cannot reach the shape: the same cuts filed in any order sort to the same sequence.
        /// </remarks>
        sealed class Cutting(int pieces, ChainIndex.Scan scan)
        {
            public ChainIndex.Scan Scan { get; } = scan;

            public int[] Candidate { get; } = new int[Math.Max(1, pieces)];

            public List<(int At, float AtM)> Cuts { get; } = [];
        }

        /// <summary>
        /// <b>One thread's working set for weighing a stretch</b>: the bands near the place, how far it
        /// stands off each and which way each faces there, and the order their own edges stand in.
        /// </summary>
        /// <remarks>
        /// <b>A set and not fields, because weighing is the half of the merge that writes nothing shared</b>
        /// (<see cref="Uncovered"/>). Every cut has been filed by the time a stretch is weighed, so what is
        /// left is a question asked of the bands — and a question asked of a ribbon is answered without
        /// reading anything another ribbon's answer depends on.
        /// </remarks>
        sealed class Weighing(int bands, ChainIndex.Scan scan)
        {
            public ChainIndex.Scan Scan { get; } = scan;

            public int[] Near { get; } = new int[bands];

            public float[] AlongM { get; } = new float[bands];

            /// <summary>How far the place being weighed stands off each near band, and which way that band faces there.</summary>
            public float[] OffTheEdgeM { get; } = new float[bands];

            public float[] Facing { get; } = new float[bands];

            /// <summary>And where each band's own edge stands, along the way the stretch faces out.</summary>
            public float[] EdgeStandsM { get; } = new float[bands];

            /// <summary>Those bands in that order, innermost first, which is the order a run of one edge is read in.</summary>
            public int[] Outward { get; } = new int[bands];
        }

        readonly List<ArcSeg> _kept = [];

        public Merge(
            ArcSeg[][] along, ChainIndex lines, ArcSeg[][] ribbons, float[] halfM, float[] lengthM,
            float mostHalfM)
        {
            _along = along;
            _ribbons = ribbons;
            _halfM = halfM;
            _lengthM = lengthM;
            _mostHalfM = mostHalfM;
            _lines = lines;

            _firstPiece = new int[ribbons.Length + 1];
            for (var line = 0; line < ribbons.Length; line++)
            {
                _firstPiece[line + 1] = _firstPiece[line] + ribbons[line].Length;
            }

            var pieces = _firstPiece[^1];
            _lineOfPiece = new int[pieces];
            _cutAtM = new List<float>?[pieces];
            _leastM = new Vector2[pieces];
            _mostM = new Vector2[pieces];

            var building = new ChainIndex.Builder();
            for (var line = 0; line < ribbons.Length; line++)
            {
                for (var piece = 0; piece < ribbons[line].Length; piece++)
                {
                    var at = _firstPiece[line] + piece;
                    _lineOfPiece[at] = line;
                    building.Add(at, ribbons[line].AsSpan(piece, 1), MathF.Abs(ribbons[line][piece].LengthM));

                    _leastM[at] = new Vector2(float.MaxValue);
                    _mostM[at] = new Vector2(float.MinValue);
                    ChainIndex.Box(ribbons[line][piece], ref _leastM[at], ref _mostM[at]);
                }
            }

            // <b>Binned as finely as the walk that bins it can tell apart</b> (<see cref="ChainIndex.FinestCellM"/>)
            // and never at the caller's lattice for the lines. What is asked of this index is which pieces
            // stand <em>at</em> a place, within a weld; a cell a road wide hands back every piece running
            // through seventy square metres of a junction and each is then rejected by its own box.
            _edges = building.Seal(ChainIndex.FinestCellM);
            _pieces = pieces;
        }

        readonly int _pieces;

        Cutting NewCutting() => new(_pieces, _edges.NewScan());

        Weighing NewWeighing() => new(_ribbons.Length, _lines.NewScan());

        /// <summary>
        /// <b>One cutting pass over a count, held and then filed</b>: run on as many threads as there are,
        /// each holding its own cuts (<see cref="Cutting"/>), and every one of them filed against the pieces
        /// they cut once the pass is over.
        /// </summary>
        void Held(int count, Action<Cutting, int> pass)
        {
            var cuttings = new List<Cutting>();
            InChunks.Over(
                count,
                NewCutting,
                pass,
                cutting =>
                {
                    lock (cuttings) cuttings.Add(cutting);
                });

            foreach (var cutting in cuttings)
            {
                foreach (var (at, atM) in cutting.Cuts) (_cutAtM[at] ??= []).Add(atM);
            }
        }

        /// <summary>The rings the merge closed, and the runs it could not.</summary>
        public (ArcSeg[][] Chains, ArcSeg[][] Loose) Run()
        {
            // <b>A ribbon at a time, on as many threads as there are</b>, each holding the cuts it makes
            // until the pass is over (<see cref="Cutting"/>): a piece is cut by whichever pieces stand on it,
            // and those belong to any ribbon in the town.
            Held(_ribbons.Length, Crossed);
            Alike();

            // <b>A ribbon at a time, on as many threads as there are.</b> Every cut is filed by now, so
            // weighing reads the bands and writes only its own ribbon's answer — and each answer is kept in
            // its ribbon's own slot and strung in ribbon order, so the shape is the shape one thread would
            // have come to whatever order they finish in.
            var keptOfLine = new List<ArcSeg>[_ribbons.Length];
            InChunks.Over(
                _ribbons.Length,
                NewWeighing,
                (weighing, line) =>
                {
                    keptOfLine[line] = [];
                    Uncovered(weighing, line, keptOfLine[line]);
                });

            foreach (var kept in keptOfLine) _kept.AddRange(kept);

            return ArcRings.Of(_kept);
        }

        /// <summary>
        /// <b>One piece of one ribbon cut against every piece whose cells it reaches</b>, its own ribbon's
        /// among them: a ribbon crosses its own boundary wherever it folds. Each pair is taken once and both
        /// sides of it are cut, which is what makes the two stops one place.
        /// </summary>
        /// <remarks>
        /// <b>Asked a piece at a time and not a ribbon at a time</b>, which is the whole of what the index
        /// being of pieces buys (<see cref="_edges"/>). A ribbon's query hands back every piece near any part
        /// of it, and each of those was then weighed against every piece of this one — the product of two
        /// ribbons, for a pair of pieces that are near each other.
        /// </remarks>
        void Crossed(Cutting cutting, int line)
        {
            var ribbon = _ribbons[line];
            Span<float> here = stackalloc float[MostCrossings];
            Span<float> there = stackalloc float[MostCrossings];

            for (var mine = 0; mine < ribbon.Length; mine++)
            {
                if (ribbon[mine].LengthM <= LeastPieceM) continue;

                var ours = _firstPiece[line] + mine;
                var offered = _edges.Crossing(cutting.Scan, ribbon.AsSpan(mine, 1), 0f, cutting.Candidate);
                for (var at = 0; at < offered && at < cutting.Candidate.Length; at++)
                {
                    // <b>Each unordered pair of pieces once, on the flat numbering</b> — which is the ribbon
                    // rule and the fold rule in one comparison: a piece is never cut against itself, and the
                    // half of every pair this piece does not take is taken when the other one is walked.
                    var theirsAt = cutting.Candidate[at];
                    if (theirsAt <= ours) continue;

                    var other = _lineOfPiece[theirsAt];
                    var theirs = theirsAt - _firstPiece[other];
                    if (_ribbons[other][theirs].LengthM <= LeastPieceM) continue;

                    // Two pieces that cross share a point and both boxes hold it, so what the boxes
                    // pass over is nothing rather than a small thing.
                    if (!Meet(ours, theirsAt)) continue;

                    var found = Spline.CrossingsOf(ribbon[mine], _ribbons[other][theirs], here, there);
                    for (var cut = 0; cut < found; cut++)
                    {
                        Cut(cutting, ours, here[cut]);
                        Cut(cutting, theirsAt, there[cut]);
                    }

                    Ends(cutting, ours, ribbon[mine], _ribbons[other][theirs]);
                    Ends(cutting, theirsAt, _ribbons[other][theirs], ribbon[mine]);
                }
            }
        }

        /// <summary>
        /// <b>And a piece is cut where another piece's own end stands on it.</b> Two pieces that lie along one
        /// another cross nowhere: the boundary of one band runs down the boundary of the next for a stretch
        /// and then one of them stops, and the place it stops is a place the ground changes hands without any
        /// two curves meeting there.
        /// </summary>
        /// <remarks>
        /// <b>It is the one cut a crossing cannot find, and without it the stretch is weighed whole.</b> A
        /// square end laid along another square end — two bays of a row, a movement stopping where the next
        /// one starts — leaves an overlap with a crossing at neither end of it, so the piece is kept or
        /// dropped by whatever its own middle happened to be, and the ring steps sideways by the length of
        /// the overlap.
        /// </remarks>
        static void Ends(Cutting cutting, int at, in ArcSeg mine, in ArcSeg theirs)
        {
            On(cutting, at, mine, theirs.StartM);
            On(cutting, at, mine, theirs.EndM);
        }

        /// <summary>One place cut into a piece, where it stands on it at all.</summary>
        static void On(Cutting cutting, int at, in ArcSeg mine, Vector2 pointM)
        {
            Span<ArcSeg> one = [mine];
            var atM = Spline.ProjectM(one, pointM, mine.LengthM * 0.5f, mine.LengthM);
            if (atM <= 0f || atM >= mine.LengthM) return;
            if (Vector2.DistanceSquared(mine.PointAtM(atM), pointM) > WeldM * WeldM) return;

            Cut(cutting, at, atM);
        }

        /// <summary>
        /// <b>A cut is a place, and every boundary standing at that place is cut there too.</b> The crossings
        /// leave one pair of pieces stopping at each; this is what makes the rest of a bundle stop with them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Because a stretch is weighed at its own middle, and two stretches cut differently have
        /// different middles.</b> Where several bands run along one another — a car park, where every bay's
        /// way opens with a straight down the lane it is worked off — their boundaries stand millimetres
        /// apart, and which of them is outermost changes over the length of a stretch. Cut alike, the
        /// neighbours weigh the same gap at the same station and one of them keeps it; cut apart, one reads a
        /// hair inside where the other reads a hair outside, and the pair is kept twice or dropped twice.
        /// </para>
        /// <para>
        /// <b>A weld is the reach, because the weld is what a place is</b> (<see cref="WeldM"/>): a boundary
        /// nearer than that to a cut ends up at the same place in the walk whether it was cut there or not.
        /// A cut landing where a piece is already cut costs nothing — a stretch shorter than the weld is
        /// passed over (<see cref="Kept"/>) — so the rule may be applied to everything standing near rather
        /// than to whatever the crossing solve happened to name.
        /// </para>
        /// </remarks>
        void Alike()
        {
            var places = new List<Vector2>();
            for (var line = 0; line < _ribbons.Length; line++)
            {
                var ribbon = _ribbons[line];
                for (var piece = 0; piece < ribbon.Length; piece++)
                {
                    var cuts = _cutAtM[_firstPiece[line] + piece];
                    if (cuts is null) continue;

                    // Carried across once each: a pair of boundaries that meet at a shallow angle is solved
                    // to the same place a dozen times over, and the same point cuts the same pieces.
                    cuts.Sort();
                    var lastM = float.NegativeInfinity;
                    foreach (var atM in cuts)
                    {
                        if (atM - lastM <= ProbeM) continue;

                        lastM = atM;
                        places.Add(ribbon[piece].PointAtM(atM));
                    }
                }
            }

            // A place at a time, on as many threads as there are: sweeping one asks the index which pieces
            // stand at it and cuts each, which is the same held-then-filed pass the crossings are.
            Held(places.Count, (cutting, at) => Alongside(cutting, places[at]));
        }

        /// <summary>One place cut into every piece of every ribbon standing within a weld of it.</summary>
        void Alongside(Cutting cutting, Vector2 pointM)
        {
            var offered = _edges.Around(cutting.Scan, pointM, WeldM, cutting.Candidate);
            for (var at = 0; at < offered && at < cutting.Candidate.Length; at++)
            {
                // Weighed against its own box before it is projected onto: a city cuts two thirds of a
                // million places into its ribbons, and the cells a place reads are a metre across.
                var pieceAt = cutting.Candidate[at];
                if (!Within(pieceAt, pointM)) continue;

                var line = _lineOfPiece[pieceAt];
                var piece = pieceAt - _firstPiece[line];
                if (_ribbons[line][piece].LengthM <= LeastPieceM) continue;

                On(cutting, pieceAt, _ribbons[line][piece], pointM);
            }
        }

        /// <summary>
        /// <b>Whether a place could stand on one piece at all</b>: whether it is inside that piece's own
        /// box, grown by the weld. Outside it, nothing of the piece is within a weld of the place, so the
        /// place is no cut of it.
        /// </summary>
        bool Within(int at, Vector2 pointM) =>
            pointM.X >= _leastM[at].X - WeldM && pointM.X <= _mostM[at].X + WeldM
            && pointM.Y >= _leastM[at].Y - WeldM && pointM.Y <= _mostM[at].Y + WeldM;

        /// <summary>And whether two pieces could meet at all: whether their boxes overlap within a weld.</summary>
        bool Meet(int ours, int theirs) =>
            _leastM[ours].X - WeldM <= _mostM[theirs].X && _mostM[ours].X + WeldM >= _leastM[theirs].X
            && _leastM[ours].Y - WeldM <= _mostM[theirs].Y && _mostM[ours].Y + WeldM >= _leastM[theirs].Y;

        /// <summary>One crossing held against the piece it cuts, to be filed when the pass is over.</summary>
        static void Cut(Cutting cutting, int at, float atM) => cutting.Cuts.Add((at, atM));

        /// <summary>
        /// The stretches one ribbon's pieces are left in by their cuts, each kept where the ground a hair
        /// outside its middle is on no ribbon.
        /// </summary>
        void Uncovered(Weighing weighing, int line, List<ArcSeg> kept)
        {
            var ribbon = _ribbons[line];
            for (var piece = 0; piece < ribbon.Length; piece++)
            {
                if (ribbon[piece].LengthM <= LeastPieceM) continue;

                var cuts = _cutAtM[_firstPiece[line] + piece];
                cuts?.Sort();
                Kept(weighing, line, ribbon[piece], cuts, kept);
            }
        }

        /// <summary>
        /// One piece split at its cuts, and each stretch of it weighed. <b>A cut within a weld of the one
        /// behind it is the same cut</b> (<see cref="LeastPieceM"/>) and is passed over, so a cluster of them
        /// cuts the piece once instead of leaving a hole as wide as the cluster.
        /// </summary>
        void Kept(Weighing weighing, int line, in ArcSeg piece, List<float>? cutAtM, List<ArcSeg> kept)
        {
            var cuts = cutAtM?.Count ?? 0;
            var fromM = 0f;
            for (var cut = 0; cut <= cuts; cut++)
            {
                var toM = cut < cuts ? MathF.Min(cutAtM![cut], piece.LengthM) : piece.LengthM;
                if (toM - fromM <= LeastPieceM) continue;

                var stretch = new ArcSeg(
                    piece.PointAtM(fromM), piece.HeadingAtRad(fromM), toM - fromM, piece.Curvature);
                if (!Covered(weighing, line, stretch)) kept.Add(stretch);

                fromM = toM;
            }
        }

        /// <summary>
        /// <b>Whether a stretch of one ribbon's boundary has covered ground on the other side of it too</b>, asked
        /// a hair outside its middle (<see cref="ProbeM"/>) — in which case it is inside the merged shape
        /// rather than on the edge of it, and goes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Which side of the boundary the question is asked from is the whole of it.</b> Two bands the
        /// town means to touch — the two lanes of a carriageway, a movement and the lane it carries on from,
        /// a bay's way and its neighbour's — are laid at one place and come out a fraction of a millimetre
        /// apart, so the edge they share lies on the boundary of both and strictly inside neither. Asked on
        /// the line, both copies of that seam are kept and every pair of them has a line down the middle
        /// of it. Asked a millimetre out, each copy lands inside the other band and both go, which is what
        /// makes a carriageway one shape rather than two bands touching — and the millimetre is what makes
        /// it work, being wider than the arithmetic's disagreement and narrower than anything the town lays.
        /// </para>
        /// <para>
        /// <b>A band standing clear of that place covers the stretch where the place is inside it</b>, which
        /// is all there is to ask of one. What is left over is the bands whose own edges stand where this
        /// edge does — the run (<see cref="Run"/>) — and of those, <b>one facing the walker takes the edge
        /// off it, and so does one lying the same way round whose number is the lower</b>. A band facing the
        /// walker is one the stretch is about to walk into, which is the seam between two lanes of a
        /// carriageway and is inside the shape; a band lying the same way round is another copy of one edge,
        /// and which copy is the real one is a question with no answer, so it goes to the number, which both
        /// of them can read.
        /// </para>
        /// <para>
        /// <b>A copy is only a copy where it stands on this edge and not merely in the same run</b>
        /// (<see cref="CoincidentM"/>). A run may be a centimetre across — a car park's bundle is a dozen
        /// bands each a millimetre or two off the last — and a band at the far end of one is not another copy
        /// of this edge but another edge of the same bundle. Asked of the run rather than of the edge, the
        /// lowest number in a bundle takes every copy in it and the bundle comes back with no outline at all.
        /// </para>
        /// </remarks>
        bool Covered(Weighing weighing, int line, in ArcSeg stretch)
        {
            var middleM = stretch.LengthM * 0.5f;
            var outM = -Heading.RightOf(Heading.Unit(stretch.HeadingAtRad(middleM)));
            var pointM = stretch.PointAtM(middleM) + (outM * ProbeM);

            var found = Reading(weighing, pointM, outM);
            var (from, to) = Run(weighing, line, found);
            for (var at = 0; at < found; at++)
            {
                var slot = weighing.Outward[at];
                if (weighing.Near[slot] == line)
                {
                    // A ribbon folded through its own band, which is covered like anything else.
                    if (weighing.OffTheEdgeM[slot] < -CoincidentM) return true;

                    continue;
                }

                // Outside the run it is another band and not another copy of this edge, so the only question
                // it answers is whether it has this place on it.
                if (at < from || at > to)
                {
                    if (weighing.OffTheEdgeM[slot] < 0f) return true;

                    continue;
                }

                if (weighing.Facing[slot] < -Squarely) return true;
                if (weighing.Facing[slot] > Squarely && weighing.Near[slot] < line
                    && MathF.Abs(weighing.EdgeStandsM[slot]) <= CoincidentM)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// <b>The run of bands this edge is one of</b>, as the first and last of them in the order their own
        /// edges stand in (<see cref="Reading"/>): out from the edge itself in both directions, for as long
        /// as each band's edge stands within the coincidence of the one before it.
        /// </summary>
        /// <remarks>
        /// <b>A run and not a pair, because a car park is a dozen bands laid along one another</b> and every
        /// one of them stands a millimetre or two off the last. Weighed in pairs, a band a hair beyond the
        /// figure covers the edge while the band between them hands the same edge back to it, and the three
        /// of them drop every copy of one stretch of the outline. Read as the run they are, whichever
        /// of them the question is asked from, the answer is the same run and it has one lowest number.
        /// </remarks>
        (int From, int To) Run(Weighing weighing, int line, int found)
        {
            var inward = Inward(weighing, found);
            var from = inward + 1;
            var to = inward;

            var standsM = 0f;
            for (var at = inward; at >= 0; at--)
            {
                if (weighing.EdgeStandsM[weighing.Outward[at]] < standsM - SameEdgeM(weighing, line, weighing.Outward[at])) break;

                standsM = weighing.EdgeStandsM[weighing.Outward[at]];
                from = at;
            }

            standsM = 0f;
            for (var at = inward + 1; at < found; at++)
            {
                if (weighing.EdgeStandsM[weighing.Outward[at]] > standsM + SameEdgeM(weighing, line, weighing.Outward[at])) break;

                standsM = weighing.EdgeStandsM[weighing.Outward[at]];
                to = at;
            }

            return (from, to);
        }

        /// <summary>
        /// <b>How near a band's edge stands before it is the same edge, asked of that band.</b> The
        /// coincidence for a band lying the same way round, with the arithmetic's own error on top of it for
        /// one the line outranks — and the touching figure for a band facing the walker, whose slit is
        /// weighed by the geometry either way round and has no number to settle it.
        /// </summary>
        /// <remarks>
        /// <b>The two of them read the gap between their edges from their own stretch</b>, a couple of
        /// millimetres apart and in floats at a town's coordinates, so the two readings of it differ by a
        /// fraction of a millimetre. A pair that straddles the figure is dropped twice, which is a hole
        /// nothing can close, or kept twice, which is one stretch said twice and is settled where the rings
        /// are strung (<see cref="ArcRings"/>). The error is spent on the side that has an answer.
        /// </remarks>
        static float SameEdgeM(Weighing weighing, int line, int slot) =>
            weighing.Facing[slot] < -Squarely ? TouchingM
            : weighing.Facing[slot] > 0f && weighing.Near[slot] > line ? CoincidentM + ProbeM
            : CoincidentM;

        /// <summary>
        /// <b>Every band near a place, ordered by where its own edge stands</b> — measured along the way the
        /// stretch faces out of the shape, so that inside and outside of it are the two ends of one order.
        /// </summary>
        /// <remarks>
        /// <b>Where a band's edge stands is not how far the place is off that band.</b> The distance is the
        /// same for a band the place is a millimetre outside of and one whose own ground starts a millimetre
        /// further out, and the two are opposite readings about the shape: which of them it is, is the way
        /// that band faces where the place stands.
        /// </remarks>
        int Reading(Weighing weighing, Vector2 pointM, Vector2 outM)
        {
            var found = _lines.Near(weighing.Scan, pointM, _mostHalfM + WeldM, weighing.Near, weighing.AlongM);
            if (found > weighing.Near.Length) found = weighing.Near.Length;

            for (var at = 0; at < found; at++)
            {
                // The probe's own step comes off the reading: a place a millimetre outside one edge stands
                // a millimetre outside the edge that shares it, so what a coincident band reads is
                // <see cref="ProbeM"/> and not nought.
                weighing.OffTheEdgeM[at] =
                    OffTheBandM(weighing.Near[at], weighing.AlongM[at], pointM, out var facingM) - ProbeM;
                weighing.Facing[at] = facingM.LengthSquared() > 0f
                    ? Vector2.Dot(Vector2.Normalize(facingM), outM)
                    : 0f;
                weighing.EdgeStandsM[at] =
                    weighing.Facing[at] > 0f ? -weighing.OffTheEdgeM[at] : weighing.OffTheEdgeM[at];
                weighing.Outward[at] = at;
            }

            for (var at = 1; at < found; at++)
            {
                var slot = weighing.Outward[at];
                var sorted = at - 1;
                for (; sorted >= 0 && weighing.EdgeStandsM[weighing.Outward[sorted]] > weighing.EdgeStandsM[slot]; sorted--)
                {
                    weighing.Outward[sorted + 1] = weighing.Outward[sorted];
                }

                weighing.Outward[sorted + 1] = slot;
            }

            return found;
        }

        /// <summary>Where the edge being weighed stands in that order: the last band whose edge is inside it.</summary>
        static int Inward(Weighing weighing, int found)
        {
            var at = found - 1;
            while (at >= 0 && weighing.EdgeStandsM[weighing.Outward[at]] > 0f) at--;

            return at;
        }

        /// <summary>
        /// <b>How far a place stands off one band's own boundary</b>, negative inside it, with
        /// <paramref name="facingM"/> the way that band faces where the place stands — so a caller standing on
        /// the boundary itself can tell a band it is about to walk into from one it is walking out of.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A band has square ends and this is where that is said</b> (TER-3c.6). Past the end of the line
        /// the nearest thing is the end itself, so the distance is measured to that straight and is never
        /// negative: the ground off the end of a line is not the line's however near it stands. Measured to
        /// the line instead, a place a hair beyond one square end reads as being on the band, and two bands
        /// butted end to end lose the pair of ends between them.
        /// </para>
        /// <para>
        /// <b>Which way a band faces off its own end is the line's own direction and not a bearing off its
        /// edge.</b> Beside the line the band faces out of it, so the way it faces is the way from the line to
        /// the place; off the end there is no such bearing — a place square in front of a square end stands on
        /// the end itself, and the way from the end to the place is the millimetre the caller stepped rather
        /// than an answer about the band. Read that way, a lane and the movement carrying on from it each
        /// looked like a band the other was walking out of, and the seam between them was kept twice.
        /// </para>
        /// </remarks>
        float OffTheBandM(int other, float alongM, Vector2 pointM, out Vector2 facingM)
        {
            var on = Spline.SampleAt(_along[other], alongM);
            var halfM = _halfM[other];
            if (alongM > 0f && alongM < _lengthM[other])
            {
                facingM = pointM - on.PositionM;
                return Vector2.Distance(on.PositionM, pointM) - halfM;
            }

            facingM = alongM <= 0f ? -on.Direction : on.Direction;
            var fromM = OnTheEnd(on.PositionM + (on.Right * halfM), on.PositionM - (on.Right * halfM), pointM);
            return Vector2.Distance(fromM, pointM);
        }

        /// <summary>The place on a band's square end nearest a point.</summary>
        static Vector2 OnTheEnd(Vector2 oneM, Vector2 otherM, Vector2 pointM)
        {
            var acrossM = otherM - oneM;
            var lengthSq = acrossM.LengthSquared();
            if (lengthSq <= 0f) return oneM;

            return oneM + (acrossM * Math.Clamp(Vector2.Dot(pointM - oneM, acrossM) / lengthSq, 0f, 1f));
        }
    }
}
