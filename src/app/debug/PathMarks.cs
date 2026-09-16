using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>What a path is drawn as, whoever is drawing it</b> — the line, the marks down it that say which way
/// it runs, and the grid those marks stand on. The layers draw an agent's own two pieces of route with it
/// (OBS-2h) and the interface draws the selected unit's whole one (CTL-1a); one vocabulary, so a route
/// drawn by either lands on the same stones at the same weight.
/// </summary>
internal static class PathMarks
{
    /// <summary>
    /// <b>Everything drawn over the town is drawn at a size in metres</b>, so it zooms with the town under
    /// it exactly as a kerb or a car does. A mark every few metres and a short one: a run of small marks
    /// close together says which way the line runs without burying the line itself.
    /// </summary>
    const float MarkPitchM = 1.5f;

    /// <summary>How long a mark is against the pitch it stands at, and how heavy against the line it sits on. Heavier than the line, because a mark drawn at the line's own width reads as a kink in it.</summary>
    public const float MarkSizeFraction = 0.24f;

    public const float MarkWidthFactor = 1.25f;

    /// <summary>
    /// <b>How near a mark another mark saying the same thing may stand</b> (<see cref="MarkClaims"/>).
    /// Nearer than this the two are a blot rather than two answers: a chevron is a third of a metre long
    /// and half of one across, and what a reader has to be able to see is the gap between them.
    /// </summary>
    public const float MarkApartM = 1f;

    /// <summary>
    /// Under this a mark on screen is a smudge and not a direction, so none is drawn. It is what keeps
    /// the town layer inside its own quad budget at a district framing, where a metric pitch otherwise puts
    /// three marks on the ground for every one that can be read.
    /// </summary>
    const float MarkVisiblePx = 2f;

    /// <summary>
    /// What a path is drawn at, whoever is drawing it: an agent's own route and the town's network under
    /// it. <b>One width and one mark</b> — the layers are telling one another's picture apart by colour,
    /// and a line that is also a little thicker reads as a different kind of line rather than as a
    /// different owner of the same one.
    /// </summary>
    public const float PathLineM = 0.09f;

    /// <summary>
    /// The dot where two pieces of a route meet, and the dot where the drawing stops. Sized off the line
    /// they sit on rather than off the body, because a car and a walker draw the same picture and only the
    /// bodies differ.
    /// </summary>
    public const float JoinDiscM = PathLineM * 1.5f;

    public const float EndDiscM = PathLineM * 2.2f;

    /// <summary>
    /// <b>How long a barb off a line is, and how far apart barbs stand down it</b>
    /// (<see cref="Barbed"/>). Longer than a mark and further apart: a barb is read one at a time, being an
    /// answer about one side of the line rather than a direction the run as a whole has, and a row of them
    /// as fine as the marks reads as a band drawn beside the line instead of as a row of answers.
    /// </summary>
    public const float BarbM = 0.7f;

    const float BarbPitchM = 5f;

    /// <summary>
    /// How far a chord drawn across a bend may bow off it, on screen — the one figure here that is not a
    /// size on the ground, because it is a fidelity and not a mark. A quarter of a pixel is less than a
    /// line this wide can show at any framing.
    /// </summary>
    /// <remarks>
    /// It is a sag and not a step: a step chosen in pixels faceted a junction join at a close framing —
    /// the drawn corner was <em>tighter</em> than the one the car drives — while chopping a straight lane
    /// into a hundred quads that one quad draws. What each piece is stepped at is
    /// <see cref="Spline.ChordForSagM"/>, off its own curvature.
    /// </remarks>
    public const float SagPx = 0.25f;

    /// <summary>
    /// The pitch to walk a line at, or <see cref="float.PositiveInfinity"/> where the marks have shrunk
    /// out of sight — a pitch every mark pass turns back at, so the caller needs no second reading of the
    /// zoom. A metric pitch puts marks a few metres apart however far the camera is, and at a town-wide
    /// framing that is tens of thousands of quads nobody can see.
    /// </summary>
    public static float MarkPitchAt(float pixelsPerMetre) =>
        MarkPitchM * MarkSizeFraction * pixelsPerMetre >= MarkVisiblePx ? MarkPitchM : float.PositiveInfinity;

    /// <summary>The pitch to stand barbs at, on the same terms (<see cref="MarkPitchAt"/>).</summary>
    public static float BarbPitchAt(float pixelsPerMetre) =>
        BarbM * pixelsPerMetre >= MarkVisiblePx ? BarbPitchM : float.PositiveInfinity;

    /// <summary>
    /// One stretch of a chain of arcs, as the run of quads that draws it: <b>every piece stepped at the
    /// chord its own curvature affords</b>, so a straight is one quad however long it is and a junction
    /// join gets the points its bend needs. Everything drawn along the ground goes through here, whatever
    /// width it is drawn at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stepped piece by piece and not by distance along the whole chain, because the chain a route is
    /// made of is a straight lane and then a biarc through the box: a step taken from the curvature under
    /// the last point would carry a straight's chord into the bend that follows it.
    /// </para>
    /// <para>
    /// <b>Each piece is cut square to the line at both ends</b> (<see cref="ScreenDraw.BandM"/>), so the
    /// pieces share their cuts and a band is one shape. Butted as rectangles they pivot about the
    /// centreline instead, and a lane-wide band round a junction join comes out as a fan of blocks with a
    /// notch outside every joint and a double-blended wedge inside it — the wider the band the worse, and
    /// a claim is drawn at the lane's own width.
    /// </para>
    /// </remarks>
    public static void Banded(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float sagM, float widthM,
        Vector4 colour)
    {
        if (arcs.Length == 0 || toM <= fromM) return;

        var previousM = Spline.SampleAt(arcs, fromM).PositionM;
        var pieceStartM = 0f;
        foreach (var arc in arcs)
        {
            var lastM = MathF.Min(toM, pieceStartM + arc.LengthM);

            // Never shorter than the line is wide: a chord that fine says nothing a quad can show, and a
            // curvature out of a degenerate arc would otherwise ask for chords of no length at all.
            var stepM = MathF.Max(PathLineM, Spline.ChordForSagM(arc.Curvature, sagM));
            for (var atM = MathF.Max(fromM, pieceStartM); atM < lastM;)
            {
                var onwardM = MathF.Min(lastM, atM + stepM);
                var onM = arc.PointAtM(onwardM - pieceStartM);
                draw.BandM(previousM, onM, arc.Curvature * (onwardM - atM), widthM, colour);
                previousM = onM;
                atM = onwardM;
            }

            pieceStartM = pieceStartM + arc.LengthM;
        }
    }

    /// <summary>
    /// The marks down one stretch of a chain, standing where it crosses the town's own grid
    /// (<see cref="MarkGrid"/>) and where the pass has not already marked that stone
    /// (<see cref="MarkClaims"/>). A tick rather than a chevron where the ground under them carries both
    /// directions on one line, since a chevron there is a direction the ground does not have.
    /// </summary>
    /// <remarks>
    /// A pass of its own and not a mark dropped as the line is walked: a mark stands where the metres say,
    /// and where the chords drawing the line happen to fall is a question about the zoom.
    /// </remarks>
    public static void Marks(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float pitchM, bool bothWays,
        Vector4 colour, MarkClaims claims)
    {
        if (arcs.Length == 0 || !float.IsFinite(pitchM)) return;

        var sizeM = pitchM * MarkSizeFraction;
        var widthM = PathLineM * MarkWidthFactor;
        var grid = new MarkGrid(fromM, toM, pitchM);
        while (grid.MoveNext(arcs))
        {
            var mark = Spline.SampleAt(arcs, grid.AtM);
            if (!claims.Take(mark.PositionM, mark.Direction)) continue;

            if (bothWays) draw.TickM(mark.PositionM, mark.Direction, sizeM, widthM, colour);
            else draw.ChevronM(mark.PositionM, mark.Direction, sizeM, widthM, colour);
        }
    }

    /// <summary>
    /// <b>The barbs down one stretch of a chain</b>: at each place on the grid, a short dash square off the
    /// line and standing to one side of it — <b>which side being the whole of what a barb says</b>.
    /// </summary>
    /// <remarks>
    /// It is drawn for a line that divides rather than for one that leads, where what a reader has to be
    /// able to see is which of the two sides the line claims. A chevron cannot say it: a chevron is about
    /// the line's own direction, and reading a side off a direction asks the reader to know which hand the
    /// thing that drew it keeps its answer on.
    /// </remarks>
    public static void Barbed(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float pitchM,
        bool toTheRight, float widthM, Vector4 colour)
    {
        if (arcs.Length == 0 || !float.IsFinite(pitchM)) return;

        var grid = new MarkGrid(fromM, toM, pitchM);
        while (grid.MoveNext(arcs))
        {
            var barb = Spline.SampleAt(arcs, grid.AtM);
            var across = toTheRight ? barb.Right : -barb.Right;
            draw.LineM(barb.PositionM, barb.PositionM + (across * BarbM), widthM, colour);
        }
    }

    /// <summary>
    /// <b>The normals down one stretch of a chain</b>: at each place on the grid, an arrow square off the
    /// line and pointing to the side the chain claims — the barb (<see cref="Barbed"/>) with a head on it.
    /// </summary>
    /// <remarks>
    /// <b>The head is what makes it a normal rather than a barb.</b> A barb answers <em>which side</em> and
    /// is read against the line it hangs off; a normal is a direction in its own right, and a reader looking
    /// at a boundary's normals is looking at the field of them rather than at any one — a run of heads all
    /// turned the same way across a corner says the corner came out the way it should, and one head turned
    /// out at the grass says it did not, without the line underneath having to be traced to work out which
    /// way it was walked.
    /// </remarks>
    public static void Normals(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float pitchM,
        bool toTheRight, float widthM, Vector4 colour)
    {
        if (arcs.Length == 0 || !float.IsFinite(pitchM)) return;

        var grid = new MarkGrid(fromM, toM, pitchM);
        while (grid.MoveNext(arcs))
        {
            var at = Spline.SampleAt(arcs, grid.AtM);
            var across = toTheRight ? at.Right : -at.Right;
            var tipM = at.PositionM + (across * BarbM);
            draw.LineM(at.PositionM, tipM, widthM, colour);
            draw.ChevronM(tipM, across, BarbM * MarkSizeFraction * 2f, widthM * MarkWidthFactor, colour);
        }
    }

    /// <summary>
    /// <b>One stretch of a chain as a boundary</b>: the line it is, and the normals that say which side of it
    /// the shape is on (<see cref="Normals"/>). The pair <see cref="Chained"/> is for a path — one
    /// vocabulary, so every boundary anything draws is drawn the same and a reader learns it once.
    /// </summary>
    /// <remarks>
    /// <b>Normals and not marks, because a boundary leads nowhere.</b> Which way a ring is walked is an
    /// artefact of how it was made; what it claims is a side, and that is the one thing a line on its own
    /// cannot say. A chain drawn here is walked with its own ground on the right throughout
    /// (<see cref="BandShell.Chains"/>), which is why the hand is not the caller's to choose: a boundary
    /// whose normals had to be pointed by whoever drew it would be a boundary with two answers.
    /// </remarks>
    public static void Bounded(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float sagM, float pitchM,
        float widthM, Vector4 colour, Vector4 normal)
    {
        Banded(ref draw, arcs, fromM, toM, sagM, widthM, colour);
        Normals(ref draw, arcs, fromM, toM, pitchM, toTheRight: true, widthM, normal);
    }

    /// <summary>One stretch of a chain as a path: the line it is, and the marks that say which way it runs.</summary>
    public static void Chained(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float pitchM, bool bothWays,
        float sagM, Vector4 colour, MarkClaims claims)
    {
        Banded(ref draw, arcs, fromM, toM, sagM, PathLineM, colour);
        Marks(ref draw, arcs, fromM, toM, pitchM, bothWays, colour, claims);
    }

    /// <summary>
    /// One run of straight line, with chevrons down it rather than an arrowhead on the end: they say which
    /// way it runs along its whole length, which is what tells a walker on its line from a walker beside it.
    /// </summary>
    /// <remarks>
    /// The marks stand on the town's own grid (<see cref="MarkGrid"/>), so they stand still while the
    /// agent walks through them, two bodies on one stretch put theirs in the same places, and a body's own
    /// line marks the pavement on the stones the network layer under it already marked.
    /// </remarks>
    public static void Chevroned(ref ScreenDraw draw, Vector2 fromM, Vector2 toM, float pitchM, Vector4 colour)
    {
        draw.LineM(fromM, toM, PathLineM, colour);

        var alongM = toM - fromM;
        var lengthM = alongM.Length();
        if (lengthM <= 1e-3f || !float.IsFinite(pitchM)) return;

        Span<ArcSeg> line = [new ArcSeg(fromM, MathF.Atan2(alongM.Y, alongM.X), lengthM, 0f)];
        Marks(ref draw, line, 0f, lengthM, pitchM, bothWays: false, colour, MarkClaims.None);
    }
}

/// <summary>
/// <b>The places down one stretch of a chain of arcs that carry a mark</b>, as distances along the chain.
/// <b>A mark stands where the line crosses the town's own grid</b> — the lattice of lines a pitch apart,
/// square to the world axes and laid from the origin — so <b>where a mark falls is a fact about the ground
/// the line crosses and about nothing the line itself is</b>: not where it was cut, not how much of it is
/// being drawn, not which bend it came out of.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two lines over the same ground therefore carry the same marks</b>, which is the whole of the rule:
/// the lanes of one carriageway mark one line of the grid across it, a movement running along a lane
/// stands its marks on the lane's, the two directions of one stretch agree because they cross the same
/// lines, and an agent's own line lands on the stones the network layer under it already drew.
/// </para>
/// <para>
/// <b>Marks laid off a line's own start are a picture of where the lines were cut instead</b> — and a
/// chain of short pieces, which is what a roundabout ring and every junction join are, comes out with a
/// mark on every piece however short it is, because each of them is a start.
/// </para>
/// <para>
/// <b>The bearing decides how far apart the marks are, and that is the price of the rule</b>: a line
/// square to the grid crosses one family of its lines and is marked every pitch, and one at forty-five
/// degrees crosses both and is marked about seven tenths of that. A line through a corner of the grid
/// meets both families within a stroke, and there the line across the world keeps the mark
/// (<see cref="Crossings"/>).
/// </para>
/// </remarks>
internal struct MarkGrid(float fromM, float toM, float pitchM)
{
    /// <summary>How far a piece is walked between samples: half a pitch, so no step can cross two lines of one family.</summary>
    const float StepFraction = 0.5f;

    /// <summary>How near a line of the other family a mark may stand before the two are the same mark, as a share of the pitch along the line.</summary>
    const float ApartFraction = 0.3f;

    int _piece = -1;

    /// <summary>How much of the chain the pieces taken so far account for, which is where the next one begins.</summary>
    float _pieceFromM = 0f;

    /// <summary>How far along the chain the piece being walked begins, its own arithmetic starting from nought there.</summary>
    float _arcFromM = 0f;

    /// <summary>And where this piece's walk stops: where it ends, or where the stretch asked for does.</summary>
    float _walkToM = 0f;

    /// <summary>The last sample taken: how far along the chain it stood and where it stood.</summary>
    float _sampleM = 0f;
    Vector2 _sampleAtM = Vector2.Zero;

    /// <summary>A second crossing found in the same step, held back until the first of the two has been read.</summary>
    float _heldM = float.NaN;

    /// <summary>How far along the chain the mark stands.</summary>
    public float AtM { get; private set; } = 0f;

    public bool MoveNext(scoped ReadOnlySpan<ArcSeg> arcs)
    {
        if (!float.IsNaN(_heldM))
        {
            AtM = _heldM;
            _heldM = float.NaN;
            return true;
        }

        while (_sampleM < _walkToM || OntoTheNextPiece(arcs))
        {
            var ontoM = MathF.Min(_walkToM, _sampleM + (pitchM * StepFraction));
            var ontoAtM = arcs[_piece].PointAtM(ontoM - _arcFromM);
            var (firstM, secondM) = Crossings(ontoAtM, ontoM);

            _sampleAtM = ontoAtM;
            _sampleM = ontoM;
            if (float.IsNaN(firstM)) continue;

            AtM = firstM;
            _heldM = secondM;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Where one step crosses the grid: at most one line of each family, the step being half a pitch, and
    /// in the order the line meets them.
    /// </summary>
    /// <remarks>
    /// <b>A line through a corner of the grid meets both families within a stroke</b>, and two marks a
    /// finger apart read as a fault in the picture rather than as one mark. The line across the world keeps
    /// the mark and the one down it gives way — <b>decided off where the crossing stands and never off
    /// which way the line is being walked</b>, so a line marked one way is marked the same way back and two
    /// lines over one piece of ground still agree.
    /// </remarks>
    (float FirstM, float SecondM) Crossings(Vector2 ontoAtM, float ontoM)
    {
        var stepM = ontoM - _sampleM;
        var across = CrossedAt(_sampleAtM.X, ontoAtM.X);
        var down = CrossedAt(_sampleAtM.Y, ontoAtM.Y);

        if (!float.IsNaN(down))
        {
            // The stroke measured across the world rather than along the line, which is what this step's
            // own bearing turns it into — so how near is near enough is asked in the coordinate the
            // answer is read in.
            var apartM = pitchM * ApartFraction * MathF.Abs(ontoAtM.X - _sampleAtM.X) / stepM;
            var standsAtM = float.Lerp(_sampleAtM.X, ontoAtM.X, down);
            if (MathF.Abs(MathF.IEEERemainder(standsAtM, pitchM)) < apartM) down = float.NaN;
        }

        var acrossM = _sampleM + (across * stepM);
        var downM = _sampleM + (down * stepM);
        if (float.IsNaN(across)) return (downM, float.NaN);
        if (float.IsNaN(down)) return (acrossM, float.NaN);

        return acrossM <= downM ? (acrossM, downM) : (downM, acrossM);
    }

    /// <summary>
    /// How far through a step one coordinate passes a line of the grid, as a fraction of it, or
    /// <see cref="float.NaN"/> where it passes none.
    /// </summary>
    float CrossedAt(float fromAtM, float ontoAtM)
    {
        var from = MathF.Floor(fromAtM / pitchM);
        var onto = MathF.Floor(ontoAtM / pitchM);
        if (from == onto) return float.NaN;

        return ((MathF.Max(from, onto) * pitchM) - fromAtM) / (ontoAtM - fromAtM);
    }

    /// <summary>The next piece of the chain any of the stretch falls on, walked from where the stretch takes it up.</summary>
    bool OntoTheNextPiece(scoped ReadOnlySpan<ArcSeg> arcs)
    {
        while (++_piece < arcs.Length)
        {
            var pieceFromM = _pieceFromM;
            _pieceFromM += arcs[_piece].LengthM;

            var walkFromM = MathF.Max(fromM, pieceFromM);
            _walkToM = MathF.Min(toM, _pieceFromM);
            if (_walkToM <= walkFromM) continue;

            _arcFromM = pieceFromM;
            _sampleM = walkFromM;
            _sampleAtM = arcs[_piece].PointAtM(walkFromM - pieceFromM);
            return true;
        }

        return false;
    }
}
