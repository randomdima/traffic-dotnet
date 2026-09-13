using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

internal sealed partial class LaneShell
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
    /// middle of every road in the town. Taken a millimetre out, the seam's two copies each land inside the
    /// other lane and both go, which is what makes a carriageway one shape rather than two bands touching.
    /// </para>
    /// <para>
    /// <b>What stands at one place is weighed over that place at once and not band by band</b>
    /// (<see cref="Run"/>). The bands whose edges stand where this one does are a run, read off in the order
    /// their edges stand in, and the run has one lowest number wherever the question is asked from. Weighed
    /// in pairs instead, a band a hair beyond the figure covers the edge while the band between them hands
    /// that same edge back to it, and every copy of one stretch of the town's outline is dropped.
    /// </para>
    /// <para>
    /// <b>Nothing is asked about which ribbon a piece belongs to.</b> A ribbon is cut against its own other
    /// pieces as readily as against a neighbour's, so a line that bends tighter than half its own width —
    /// whose inner edge runs back through the band it bounds — is covered by the same rule that covers the
    /// seam, and needs no case.
    /// </para>
    /// </remarks>
    sealed partial class Merge
    {
        /// <summary>
        /// How far outside a piece the cover test is taken. <b>A millimetre, because the error it has to
        /// clear is the arithmetic's and not the town's</b>: two edges the plan lays at one place are two
        /// sums of the same numbers and stand a few hundredths of a millimetre apart, while the narrowest
        /// thing the town lays is metres wide. Anything between those two is the same answer.
        /// </summary>
        const float ProbeM = LineTolerance.RoundingM;

        /// <summary>
        /// <b>How far a ring's own shape may move when two of its pieces are read as the one piece they
        /// are</b> (<see cref="Joined"/>). The same millimetre, for the same reason: two pieces the town
        /// laid along one line are two sums of the same numbers, and what separates them is the
        /// arithmetic's error rather than a bend.
        /// </summary>
        const float JoinM = LineTolerance.RoundingM;

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
        /// Narrower than this the two bands touch and the slit is inside the town; wider, it is ground the
        /// town does not drive and the boundary goes round it.
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
        /// How near two cut ends stand to be the same end. <b>A tenth of a metre, which is the crossing's own
        /// error and not a search radius</b>: the two pieces that stop at one crossing each read that place
        /// off their own curve, and two curves meeting at a degree or two put the same point that far apart
        /// in the last bits of a float. Read as a search radius instead, a ring takes whatever end is
        /// nearest and the town comes back wired through itself.
        /// </summary>
        const float WeldM = 0.1f;

        /// <summary>
        /// <b>The shortest stretch worth keeping, and so the nearest two cuts stand before they are one cut</b>
        /// (<see cref="Kept"/>). The weld itself: a stretch shorter than that is its own two ends — both of
        /// them weld to one place, so it can never be walked into a ring — and the hole dropping one leaves is
        /// exactly what the weld closes.
        /// </summary>
        /// <remarks>
        /// <b>Two boundaries running along one another cross wherever the last bits of a float say they do</b>,
        /// which is a handful of crossings a few centimetres apart rather than the one place they really
        /// share. Cut at every one of them and weighed stretch by stretch, the sliver between each pair is
        /// dropped for being short and what is left is a hole the width of the whole cluster — wider than the
        /// weld, and so two ends the ring cannot be closed through.
        /// </remarks>
        const float LeastPieceM = WeldM;

        /// <summary>
        /// <b>How squarely a band has to face the walker before the gap between them is a slit</b> rather
        /// than a crossing. Half, which is a third of a turn: a band lying along this edge faces square
        /// across it either way round, and one whose square end is cutting across faces along it — and a slit
        /// is a thing two bands lying along each other have.
        /// </summary>
        const float Squarely = 0.5f;

        /// <summary>Two circles cross at two places and two lines at one, so a pair of pieces has no more than two.</summary>
        const int MostCrossings = 2;

        readonly Paving _paving;
        readonly ArcSeg[][] _ribbons;
        readonly float[] _halfM;
        readonly float[] _lengthM;
        readonly float _mostHalfM;

        /// <summary>The town's own index of the lines, which answers what covers a place.</summary>
        readonly ChainIndex _lines;

        /// <summary>And an index of the ribbons, which answers which of them could cross which.</summary>
        readonly ChainIndex _edges;

        /// <summary>Where one ribbon's pieces start in the flat numbering the cuts are held under.</summary>
        readonly int[] _firstPiece;

        /// <summary>The distances along each piece that another boundary crosses it, or null where none does.</summary>
        readonly List<float>?[] _cutAtM;

        /// <summary>
        /// <b>Where each piece's own middle stands, and how far from it the piece reaches</b> — which is
        /// half its length, since two places on one curve are never further apart than the curve between
        /// them. A pair of pieces standing further apart than their two reaches and a weld has neither a
        /// crossing nor an end on the other, so the solve is skipped without being approximated
        /// (<see cref="Crossed"/>).
        /// </summary>
        readonly Vector2[] _middleM;
        readonly float[] _reachM;

        readonly int[] _near;
        readonly float[] _alongM;
        readonly int[] _candidate;

        /// <summary>How far the place being weighed stands off each near band, and which way that band faces there.</summary>
        readonly float[] _offTheEdgeM;
        readonly float[] _facing;

        /// <summary>And where each band's own edge stands, along the way the stretch faces out.</summary>
        readonly float[] _edgeStandsM;

        /// <summary>Those bands in that order, innermost first, which is the order a run of one edge is read in.</summary>
        readonly int[] _outward;

        readonly List<ArcSeg> _kept = [];

        public Merge(
            Paving paving, SimConfig config, ArcSeg[][] ribbons, float[] halfM, float[] lengthM,
            float mostHalfM)
        {
            _paving = paving;
            _ribbons = ribbons;
            _halfM = halfM;
            _lengthM = lengthM;
            _mostHalfM = mostHalfM;
            _lines = paving.DrivenLines(config);

            var building = new ChainIndex.Builder();
            _firstPiece = new int[ribbons.Length + 1];
            for (var line = 0; line < ribbons.Length; line++)
            {
                building.Add(line, ribbons[line], Reach(ribbons[line]));
                _firstPiece[line + 1] = _firstPiece[line] + ribbons[line].Length;
            }

            _edges = building.Seal(config.NearestChainCellM);
            _cutAtM = new List<float>?[_firstPiece[^1]];
            _middleM = new Vector2[_firstPiece[^1]];
            _reachM = new float[_firstPiece[^1]];
            for (var line = 0; line < ribbons.Length; line++)
            {
                for (var piece = 0; piece < ribbons[line].Length; piece++)
                {
                    var at = _firstPiece[line] + piece;
                    _middleM[at] = ribbons[line][piece].PointAtM(ribbons[line][piece].LengthM * 0.5f);
                    _reachM[at] = MathF.Abs(ribbons[line][piece].LengthM) * 0.5f;
                }
            }

            _near = new int[ribbons.Length];
            _alongM = new float[ribbons.Length];
            _candidate = new int[ribbons.Length];
            _offTheEdgeM = new float[ribbons.Length];
            _facing = new float[ribbons.Length];
            _edgeStandsM = new float[ribbons.Length];
            _outward = new int[ribbons.Length];
        }

        /// <summary>The rings the merge closed, and the runs it could not.</summary>
        public (ArcSeg[][] Chains, ArcSeg[][] Loose) Run()
        {
            for (var line = 0; line < _ribbons.Length; line++) Crossed(line);

            Alike();
            for (var line = 0; line < _ribbons.Length; line++) Uncovered(line);

            return Strung();
        }

        /// <summary>
        /// <b>One ribbon cut against every ribbon whose cells it reaches</b>, itself among them: a ribbon
        /// crosses its own boundary wherever it folds. Each pair is taken once and both sides of it are cut,
        /// which is what makes the two stops one place.
        /// </summary>
        void Crossed(int line)
        {
            var ribbon = _ribbons[line];
            if (ribbon.Length == 0) return;

            var offered = _edges.Crossing(ribbon, 0f, _candidate);
            Span<float> here = stackalloc float[MostCrossings];
            Span<float> there = stackalloc float[MostCrossings];

            for (var at = 0; at < offered && at < _candidate.Length; at++)
            {
                var other = _candidate[at];
                if (other < line) continue;

                var against = _ribbons[other];
                for (var mine = 0; mine < ribbon.Length; mine++)
                {
                    if (ribbon[mine].LengthM <= LeastPieceM) continue;

                    var ours = _firstPiece[line] + mine;
                    for (var theirs = 0; theirs < against.Length; theirs++)
                    {
                        if (other == line && theirs <= mine) continue;
                        if (against[theirs].LengthM <= LeastPieceM) continue;

                        // Neither a crossing nor an end standing on a piece can be further off it than
                        // this, so what the reach passes over is nothing rather than a small thing.
                        var yours = _firstPiece[other] + theirs;
                        var apartM = _reachM[ours] + _reachM[yours] + WeldM;
                        if (Vector2.DistanceSquared(_middleM[ours], _middleM[yours]) > apartM * apartM)
                        {
                            continue;
                        }

                        var found = Spline.CrossingsOf(ribbon[mine], against[theirs], here, there);
                        for (var cut = 0; cut < found; cut++)
                        {
                            Cut(line, mine, here[cut]);
                            Cut(other, theirs, there[cut]);
                        }

                        Ends(line, mine, ribbon[mine], against[theirs]);
                        Ends(other, theirs, against[theirs], ribbon[mine]);
                    }
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
        void Ends(int line, int piece, in ArcSeg mine, in ArcSeg theirs)
        {
            On(line, piece, mine, theirs.StartM);
            On(line, piece, mine, theirs.EndM);
        }

        /// <summary>One place cut into a piece, where it stands on it at all.</summary>
        void On(int line, int piece, in ArcSeg mine, Vector2 pointM)
        {
            Span<ArcSeg> one = [mine];
            var atM = Spline.ProjectM(one, pointM, mine.LengthM * 0.5f, mine.LengthM);
            if (atM <= 0f || atM >= mine.LengthM) return;
            if (Vector2.DistanceSquared(mine.PointAtM(atM), pointM) > WeldM * WeldM) return;

            Cut(line, piece, atM);
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

            foreach (var pointM in places) Alongside(pointM);
        }

        /// <summary>One place cut into every piece of every ribbon standing within a weld of it.</summary>
        void Alongside(Vector2 pointM)
        {
            var offered = _edges.Around(pointM, WeldM, _candidate);
            for (var at = 0; at < offered && at < _candidate.Length; at++)
            {
                var alongside = _ribbons[_candidate[at]];
                for (var piece = 0; piece < alongside.Length; piece++)
                {
                    if (alongside[piece].LengthM <= LeastPieceM) continue;

                    // Weighed off its own middle before it is projected onto: a city cuts a hundred thousand
                    // times and a ribbon is a dozen pieces, of which one is anywhere near.
                    var reachM = _reachM[_firstPiece[_candidate[at]] + piece] + WeldM;
                    if (Vector2.DistanceSquared(_middleM[_firstPiece[_candidate[at]] + piece], pointM)
                        > reachM * reachM)
                    {
                        continue;
                    }

                    On(_candidate[at], piece, alongside[piece], pointM);
                }
            }
        }

        /// <summary>One crossing filed against the piece it cuts.</summary>
        void Cut(int line, int piece, float atM)
        {
            var at = _firstPiece[line] + piece;
            (_cutAtM[at] ??= []).Add(atM);
        }

        /// <summary>
        /// The stretches one ribbon's pieces are left in by their cuts, each kept where the ground a hair
        /// outside its middle is on no ribbon.
        /// </summary>
        void Uncovered(int line)
        {
            var ribbon = _ribbons[line];
            for (var piece = 0; piece < ribbon.Length; piece++)
            {
                if (ribbon[piece].LengthM <= LeastPieceM) continue;

                var cuts = _cutAtM[_firstPiece[line] + piece];
                cuts?.Sort();
                Kept(line, ribbon[piece], cuts);
            }
        }

        /// <summary>
        /// One piece split at its cuts, and each stretch of it weighed. <b>A cut within a weld of the one
        /// behind it is the same cut</b> (<see cref="LeastPieceM"/>) and is passed over, so a cluster of them
        /// cuts the piece once instead of leaving a hole as wide as the cluster.
        /// </summary>
        void Kept(int line, in ArcSeg piece, List<float>? cutAtM)
        {
            var cuts = cutAtM?.Count ?? 0;
            var fromM = 0f;
            for (var cut = 0; cut <= cuts; cut++)
            {
                var toM = cut < cuts ? MathF.Min(cutAtM![cut], piece.LengthM) : piece.LengthM;
                if (toM - fromM <= LeastPieceM) continue;

                var stretch = new ArcSeg(
                    piece.PointAtM(fromM), piece.HeadingAtRad(fromM), toM - fromM, piece.Curvature);
                if (!Covered(line, stretch)) _kept.Add(stretch);

                fromM = toM;
            }
        }

        /// <summary>
        /// <b>Whether a stretch of one ribbon's boundary has the town on the other side of it too</b>, asked
        /// a hair outside its middle (<see cref="ProbeM"/>) — in which case it is inside the merged shape
        /// rather than on the edge of it, and goes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Which side of the boundary the question is asked from is the whole of it.</b> Two bands the
        /// town means to touch — the two lanes of a carriageway, a movement and the lane it carries on from,
        /// a bay's way and its neighbour's — are laid at one place and come out a fraction of a millimetre
        /// apart, so the edge they share lies on the boundary of both and strictly inside neither. Asked on
        /// the line, both copies of that seam are kept and every road in the town has a line down the middle
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
        /// carriageway and is inside the town; a band lying the same way round is another copy of one edge,
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
        bool Covered(int line, in ArcSeg stretch)
        {
            var middleM = stretch.LengthM * 0.5f;
            var outM = -Heading.RightOf(Heading.Unit(stretch.HeadingAtRad(middleM)));
            var pointM = stretch.PointAtM(middleM) + (outM * ProbeM);

            var found = Reading(pointM, outM);
            var (from, to) = Run(line, found);
            for (var at = 0; at < found; at++)
            {
                var slot = _outward[at];
                if (_near[slot] == line)
                {
                    // A ribbon folded through its own band, which is covered like anything else.
                    if (_offTheEdgeM[slot] < -CoincidentM) return true;

                    continue;
                }

                // Outside the run it is another band and not another copy of this edge, so the only question
                // it answers is whether it has this place on it.
                if (at < from || at > to)
                {
                    if (_offTheEdgeM[slot] < 0f) return true;

                    continue;
                }

                if (_facing[slot] < -Squarely) return true;
                if (_facing[slot] > Squarely && _near[slot] < line
                    && MathF.Abs(_edgeStandsM[slot]) <= CoincidentM)
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
        /// of them drop every copy of one stretch of the town's outline. Read as the run they are, whichever
        /// of them the question is asked from, the answer is the same run and it has one lowest number.
        /// </remarks>
        (int From, int To) Run(int line, int found)
        {
            var inward = Inward(found);
            var from = inward + 1;
            var to = inward;

            var standsM = 0f;
            for (var at = inward; at >= 0; at--)
            {
                if (_edgeStandsM[_outward[at]] < standsM - SameEdgeM(line, _outward[at])) break;

                standsM = _edgeStandsM[_outward[at]];
                from = at;
            }

            standsM = 0f;
            for (var at = inward + 1; at < found; at++)
            {
                if (_edgeStandsM[_outward[at]] > standsM + SameEdgeM(line, _outward[at])) break;

                standsM = _edgeStandsM[_outward[at]];
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
        /// are strung (<see cref="Doubled"/>). The error is spent on the side that has an answer.
        /// </remarks>
        float SameEdgeM(int line, int slot) =>
            _facing[slot] < -Squarely ? TouchingM
            : _facing[slot] > 0f && _near[slot] > line ? CoincidentM + ProbeM
            : CoincidentM;

        /// <summary>
        /// <b>Every band near a place, ordered by where its own edge stands</b> — measured along the way the
        /// stretch faces out of the town, so that inside and outside of it are the two ends of one order.
        /// </summary>
        /// <remarks>
        /// <b>Where a band's edge stands is not how far the place is off that band.</b> The distance is the
        /// same for a band the place is a millimetre outside of and one whose own ground starts a millimetre
        /// further out, and the two are opposite readings about the town: which of them it is, is the way
        /// that band faces where the place stands.
        /// </remarks>
        int Reading(Vector2 pointM, Vector2 outM)
        {
            var found = _lines.Near(pointM, _mostHalfM + WeldM, _near, _alongM);
            if (found > _near.Length) found = _near.Length;

            for (var at = 0; at < found; at++)
            {
                // The probe's own step comes off the reading: a place a millimetre outside one edge stands
                // a millimetre outside the edge that shares it, so what a coincident band reads is
                // <see cref="ProbeM"/> and not nought.
                _offTheEdgeM[at] = OffTheBandM(_near[at], _alongM[at], pointM, out var facingM) - ProbeM;
                _facing[at] = facingM.LengthSquared() > 0f
                    ? Vector2.Dot(Vector2.Normalize(facingM), outM)
                    : 0f;
                _edgeStandsM[at] = _facing[at] > 0f ? -_offTheEdgeM[at] : _offTheEdgeM[at];
                _outward[at] = at;
            }

            for (var at = 1; at < found; at++)
            {
                var slot = _outward[at];
                var sorted = at - 1;
                for (; sorted >= 0 && _edgeStandsM[_outward[sorted]] > _edgeStandsM[slot]; sorted--)
                {
                    _outward[sorted + 1] = _outward[sorted];
                }

                _outward[sorted + 1] = slot;
            }

            return found;
        }

        /// <summary>Where the edge being weighed stands in that order: the last band whose edge is inside it.</summary>
        int Inward(int found)
        {
            var at = found - 1;
            while (at >= 0 && _edgeStandsM[_outward[at]] > 0f) at--;

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
            var on = Spline.SampleAt(_paving.ArcsOfDriven(other), alongM);
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

        /// <summary>How far a ribbon runs, for the index that bins it — a fold's backwards piece still reaches.</summary>
        static float Reach(ReadOnlySpan<ArcSeg> ribbon)
        {
            var lengthM = 0f;
            foreach (var piece in ribbon) lengthM += MathF.Abs(piece.LengthM);

            return lengthM;
        }
    }
}
