using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>What a path is drawn as, whoever is drawing it</b> — the line, the marks down it that say which way
/// it runs, and the comb those marks stand on. The layers draw an agent's own two pieces of route with it
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
    /// a reservation is drawn at the lane's own width.
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
    /// The marks down one stretch of a chain, standing on the town's own comb (<see cref="FirstMarkM"/>).
    /// A tick rather than a chevron where the ground under them carries both directions on one line, since
    /// a chevron there is a direction the ground does not have.
    /// </summary>
    /// <remarks>
    /// A pass of its own and not a mark dropped as the line is walked: a mark stands where the metres say,
    /// and where the chords drawing the line happen to fall is a question about the zoom.
    /// </remarks>
    public static void Marks(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float pitchM, bool bothWays,
        Vector4 colour)
    {
        if (arcs.Length == 0 || !float.IsFinite(pitchM)) return;

        var sizeM = pitchM * MarkSizeFraction;
        var widthM = PathLineM * MarkWidthFactor;
        var start = Spline.SampleAt(arcs, fromM);
        for (var atM = fromM + FirstMarkM(start.PositionM, start.Direction, pitchM); atM <= toM; atM += pitchM)
        {
            var mark = Spline.SampleAt(arcs, atM);
            if (bothWays) draw.TickM(mark.PositionM, mark.Direction, sizeM, widthM, colour);
            else draw.ChevronM(mark.PositionM, mark.Direction, sizeM, widthM, colour);
        }
    }

    /// <summary>One stretch of a chain as a path: the line it is, and the marks that say which way it runs.</summary>
    public static void Chained(
        ref ScreenDraw draw, scoped ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float pitchM, bool bothWays,
        float sagM, Vector4 colour)
    {
        Banded(ref draw, arcs, fromM, toM, sagM, PathLineM, colour);
        Marks(ref draw, arcs, fromM, toM, pitchM, bothWays, colour);
    }

    /// <summary>
    /// One run of straight line, with chevrons down it rather than an arrowhead on the end: they say which
    /// way it runs along its whole length, which is what tells a walker on its line from a walker beside it.
    /// </summary>
    /// <remarks>
    /// The marks stand on the town's own comb (<see cref="FirstMarkM"/>), so they stand still while the
    /// agent walks through them, two bodies on one stretch put theirs in the same places, and a body's own
    /// line marks the pavement on the stones the network layer under it already marked.
    /// </remarks>
    public static void Chevroned(ref ScreenDraw draw, Vector2 fromM, Vector2 toM, float pitchM, Vector4 colour)
    {
        draw.LineM(fromM, toM, PathLineM, colour);

        var alongM = toM - fromM;
        var lengthM = alongM.Length();
        if (lengthM <= 1e-3f || !float.IsFinite(pitchM)) return;

        for (var at = FirstMarkM(fromM, alongM / lengthM, pitchM); at < lengthM; at += pitchM)
        {
            draw.ChevronM(
                fromM + (alongM / lengthM * at), alongM, pitchM * MarkSizeFraction, PathLineM * MarkWidthFactor,
                colour);
        }
    }

    /// <summary>
    /// How far past the start of a run its first mark stands. <b>The marks stand on a comb laid over the
    /// town and not over the line</b>: one falls wherever the distance from the world origin along the
    /// line's own bearing is a whole number of pitches, so nothing about where a line begins enters it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two consequences, and they are the reason for the rule. Lines running the same way put their marks
    /// on the same stones however they are cut — the marks of the lanes of one carriageway stack square
    /// across it instead of drifting against each other, and a piece of an agent's route lands on the
    /// stones the town layer already drew. And the two directions of one line put their marks on the same
    /// stones as each other, because the comb of a bearing and the comb of its reverse are the same comb.
    /// </para>
    /// <para>
    /// The bearing is the one the run sets off on, so a run that bends walks off its own comb as it turns.
    /// That is the price and it is small: what bends in this town is a junction join or a corner, metres
    /// long, and the lines a reader is comparing across are the straights between them.
    /// </para>
    /// </remarks>
    public static float FirstMarkM(Vector2 atM, Vector2 direction, float pitchM)
    {
        var alongM = Vector2.Dot(atM, direction);
        return pitchM - (alongM - (MathF.Floor(alongM / pitchM) * pitchM));
    }
}
