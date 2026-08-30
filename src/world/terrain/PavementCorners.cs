using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Terrain;

/// <summary>
/// One re-entrant corner of the pavement: the point two of its pieces run into one another at, the
/// outward normal of each there, and the radius of the arc that rounds the spike of verge between them.
/// </summary>
internal readonly record struct PavementCorner(Vector2 CornerM, Vector2 NormalA, Vector2 NormalB, float RadiusM)
{
    /// <summary>
    /// Where the arc's centre stands: on the bisector, a radius clear of both edges, which is
    /// <c>radius / (1 + nA·nB)</c> along the sum of the normals.
    /// </summary>
    public Vector2 ArcCentreM => CornerM + (RadiusM / (1f + Vector2.Dot(NormalA, NormalB)) * (NormalA + NormalB));

    /// <summary>Where the arc meets each edge: a radius back along that edge's own normal.</summary>
    public Vector2 TangentAM => ArcCentreM - (RadiusM * NormalA);

    public Vector2 TangentBM => ArcCentreM - (RadiusM * NormalB);
}

/// <summary>
/// The pavement's inner corners (TER-3c.4), <b>solved against the finished ground</b>: every place the
/// outline of one piece of pavement crosses into another is a corner, whatever the two pieces are.
/// </summary>
/// <remarks>
/// <para>
/// <b>A corner is a fact about the pair of shapes and nothing else</b>, so nothing here knows a band from
/// a wrap: each piece's outline is walked, the crossings into the rest are found, and the wedge of verge
/// outside both is measured off the two outward normals there. A pair the generator has never put
/// together before is rounded the first time it appears, and a map recording no corners of its own is
/// rounded exactly as one that does.
/// </para>
/// <para>
/// <b>Signed distance is the whole vocabulary.</b> Inside is negative, the outward normal is the
/// gradient, and the three kinds of piece differ in that one function alone — which is what lets the
/// crossing search, the spike test and the normals be written once each.
/// </para>
/// <para>
/// <b>Load-time work.</b> It walks sixty-odd kilometres of outline over a city and allocates while it
/// does; it runs once when the ground is laid and never on a tick.
/// </para>
/// </remarks>
internal static class PavementCorners
{
    /// <summary>How finely an outline is walked. Well under the narrowest band, so no crossing is stepped over.</summary>
    const float StepM = 0.5f;

    /// <summary>How close two solved corners stand to be the same one — every crossing is found from both sides.</summary>
    const float SameCornerM = 0.5f;

    /// <summary>How far round a crossing the ground is read to tell a spike of verge from a corner of pavement.</summary>
    const float SpikeReachM = 0.4f;

    const int SpikeRays = 16;

    /// <summary>How near a bisected crossing a piece's own edge must stand to be the edge that was crossed.</summary>
    const float OnTheEdgeM = 0.05f;

    /// <summary>Nearly straight, and there is no corner to round: ten degrees off flat.</summary>
    const float FlatDot = 0.985f;

    /// <summary>Nearly folded back on itself, where the arc's centre runs away and the wedge is a hairline.</summary>
    const float FoldedDot = -0.95f;

    public static List<PavementCorner> Solve(CityPlan plan, SimConfig config)
    {
        var corners = new List<PavementCorner>();
        var walkM = plan.PavementWidthM > 0f ? plan.PavementWidthM : config.Road.PavementWidthM;
        if (walkM <= 0f) return corners;

        var pieces = Lay(plan, config, walkM);
        if (pieces.Count < 2) return corners;

        var grid = new PieceGrid(pieces, plan.WorldSizeM);
        var outline = new List<Vector2>();
        var runs = new List<int>();
        for (var piece = 0; piece < pieces.Count; piece++)
        {
            Outline(pieces[piece], outline, runs);
            for (var run = 0; run + 1 < runs.Count; run++)
            {
                for (var step = runs[run]; step + 1 < runs[run + 1]; step++)
                {
                    var wasInside = Inside(pieces, grid, outline[step], piece);
                    if (wasInside == Inside(pieces, grid, outline[step + 1], piece)) continue;

                    Crossing(pieces, grid, piece, wasInside ? outline[step + 1] : outline[step],
                        wasInside ? outline[step] : outline[step + 1], walkM, config, corners);
                }
            }
        }

        return corners;
    }

    /// <summary>
    /// One crossing, given the outline samples either side of it. The point is bisected onto the edge it
    /// crosses, the piece it crosses into is whichever edge stands on that point, and what comes out is a
    /// corner only if the ground beyond the two of them is a spike rather than a corner of pavement.
    /// </summary>
    static void Crossing(List<Piece> pieces, PieceGrid grid, int piece, Vector2 outsideM, Vector2 insideM,
        float walkM, SimConfig config, List<PavementCorner> corners)
    {
        for (var halving = 0; halving < 12; halving++)
        {
            var middleM = (outsideM + insideM) * 0.5f;
            if (Inside(pieces, grid, middleM, piece)) insideM = middleM;
            else outsideM = middleM;
        }

        var atM = (outsideM + insideM) * 0.5f;
        var other = Crossed(pieces, grid, atM, piece);
        if (other < 0) return;

        var normalA = Normal(pieces[piece], atM);
        var normalB = Normal(pieces[other], atM);
        var opening = Vector2.Dot(normalA, normalB);
        if (opening > FlatDot || opening < FoldedDot) return;
        if (!IsSpike(pieces, grid, atM)) return;

        foreach (var already in corners)
        {
            if (Vector2.DistanceSquared(already.CornerM, atM) < SameCornerM * SameCornerM) return;
        }

        corners.Add(new PavementCorner(atM, normalA, normalB, RadiusM(opening, walkM, config)));
    }

    /// <summary>
    /// The arc a wedge is turned on: half the walk, <b>bounded by how far the fillet would reach in</b>
    /// (TER-3c.4). A right angle turns on the full half-width and stands 0.83 m deep; a wedge sharp enough
    /// that the same arc would drive a spike deeper into the verge than half the walk turns on whatever
    /// radius holds it to that depth.
    /// </summary>
    static float RadiusM(float opening, float walkM, SimConfig config)
    {
        var halfWedgeRad = (MathF.PI - MathF.Acos(Math.Clamp(opening, -1f, 1f))) * 0.5f;
        var sin = MathF.Sin(halfWedgeRad);
        var boundedM = sin >= 1f ? float.MaxValue : walkM * 0.5f * sin / (1f - sin);

        return MathF.Min(config.PavementCornerRadiusM, boundedM);
    }

    /// <summary>
    /// Whether the ground round a crossing is a spike of verge rather than a corner of pavement. Two
    /// pieces meeting leave one or the other, and the difference is which side the ground is on — so it is
    /// read off the ground itself and not off the way either outline happened to be wound.
    /// </summary>
    static bool IsSpike(List<Piece> pieces, PieceGrid grid, Vector2 atM)
    {
        var paved = 0;
        for (var ray = 0; ray < SpikeRays; ray++)
        {
            var angleRad = MathF.Tau * ray / SpikeRays;
            var aroundM = atM + (SpikeReachM * new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad)));
            if (Inside(pieces, grid, aroundM, Nothing)) paved++;
        }

        return paved * 2 > SpikeRays;
    }

    /// <summary>No piece: what <see cref="Inside"/> is asked to leave out when the whole union is the question.</summary>
    const int Nothing = -1;

    /// <summary>Whether any piece but one holds a point.</summary>
    static bool Inside(List<Piece> pieces, PieceGrid grid, Vector2 pointM, int except)
    {
        foreach (var piece in grid.At(pointM))
        {
            if (piece != except && Distance(pieces[piece], pointM) <= 0f) return true;
        }

        return false;
    }

    /// <summary>The piece whose own edge stands on a point, which is the edge the outline crossed.</summary>
    static int Crossed(List<Piece> pieces, PieceGrid grid, Vector2 atM, int except)
    {
        var crossed = Nothing;
        var nearestM = OnTheEdgeM;
        foreach (var piece in grid.At(atM))
        {
            if (piece == except) continue;

            var offM = MathF.Abs(Distance(pieces[piece], atM));
            if (offM >= nearestM) continue;

            nearestM = offM;
            crossed = piece;
        }

        return crossed;
    }

    /// <summary>The outward normal, as the gradient of the piece's own distance field.</summary>
    static Vector2 Normal(in Piece piece, Vector2 pointM)
    {
        const float NudgeM = 0.01f;
        var gradient = new Vector2(
            Distance(piece, pointM + new Vector2(NudgeM, 0f)) - Distance(piece, pointM - new Vector2(NudgeM, 0f)),
            Distance(piece, pointM + new Vector2(0f, NudgeM)) - Distance(piece, pointM - new Vector2(0f, NudgeM)));

        return gradient.LengthSquared() > 0f ? Vector2.Normalize(gradient) : Vector2.UnitX;
    }

    /// <summary>
    /// How far a point stands outside a piece of pavement, negative within it. A band is its centreline
    /// grown by a half-width, a disc is a disc, and a wrap is the rounded rectangle a car park's walk is.
    /// </summary>
    static float Distance(in Piece piece, Vector2 pointM)
    {
        switch (piece.Kind)
        {
            case Kind.Disc:
                return (pointM - piece.CentreM).Length() - piece.HalfM.X;

            case Kind.Wrap:
                var local = new Vector2(
                    Vector2.Dot(pointM - piece.CentreM, piece.Axis),
                    Vector2.Dot(pointM - piece.CentreM, Heading.RightOf(piece.Axis)));
                var off = Vector2.Abs(local) - piece.HalfM + new Vector2(piece.RadiusM);
                return Vector2.Max(off, Vector2.Zero).Length() + MathF.Min(MathF.Max(off.X, off.Y), 0f) - piece.RadiusM;

            default:
                var arcs = piece.Arcs.Span;
                var alongM = Spline.ProjectM(arcs, pointM, 0f, float.MaxValue);
                return (Spline.SampleAt(arcs, alongM).PositionM - pointM).Length() - piece.HalfM.X;
        }
    }

    /// <summary>
    /// A piece's outline, walked at <see cref="StepM"/> into <paramref name="into"/> as one or more runs,
    /// <paramref name="runs"/> holding where each begins and one past the end of the last.
    /// </summary>
    /// <remarks>
    /// <b>A band is two runs and not one loop, because its ends are left off.</b> A road runs into the
    /// disc at each of its junctions, so a cap is ground already inside the union — and a segment drawn
    /// straight across the carriageway from one side's end to the other's is not on the outline at all.
    /// </remarks>
    static void Outline(in Piece piece, List<Vector2> into, List<int> runs)
    {
        into.Clear();
        runs.Clear();
        runs.Add(0);

        if (piece.Kind == Kind.Disc)
        {
            var steps = Math.Max(8, (int)MathF.Ceiling(MathF.Tau * piece.HalfM.X / StepM));
            for (var step = 0; step <= steps; step++)
            {
                var angleRad = MathF.Tau * step / steps;
                into.Add(piece.CentreM + (piece.HalfM.X * new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad))));
            }

            runs.Add(into.Count);
            return;
        }

        if (piece.Kind == Kind.Wrap)
        {
            var across = Heading.RightOf(piece.Axis);
            var straightM = piece.HalfM - new Vector2(piece.RadiusM);
            var baseRad = MathF.Atan2(piece.Axis.Y, piece.Axis.X);
            var steps = Math.Max(4, (int)MathF.Ceiling(piece.RadiusM * MathF.PI * 0.5f / StepM));
            foreach (var quadrant in (ReadOnlySpan<int>)[0, 1, 2, 3])
            {
                var signU = quadrant is 0 or 3 ? 1f : -1f;
                var signV = quadrant is 0 or 1 ? 1f : -1f;
                var pivotM = piece.CentreM + (piece.Axis * (straightM.X * signU)) + (across * (straightM.Y * signV));
                for (var step = 0; step <= steps; step++)
                {
                    var angleRad = baseRad + (MathF.PI * 0.5f * (quadrant + ((float)step / steps)));
                    into.Add(pivotM + (piece.RadiusM * new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad))));
                }
            }

            into.Add(into[0]);
            runs.Add(into.Count);
            return;
        }

        var arcs = piece.Arcs.Span;
        var lengthM = Spline.TotalLengthM(arcs);
        var samples = Math.Max(2, (int)MathF.Ceiling(lengthM / StepM));
        foreach (var offsetM in (ReadOnlySpan<float>)[piece.HalfM.X, -piece.HalfM.X])
        {
            for (var sample = 0; sample <= samples; sample++)
            {
                var on = Spline.SampleAt(arcs, lengthM * sample / samples);
                into.Add(on.PositionM + (on.Right * offsetM));
            }

            runs.Add(into.Count);
        }
    }

    /// <summary>
    /// Every piece of pavement the ground is drawn from, in the shape it is drawn in: the band either
    /// side of each carriageway, the ring round each junction, the walk a bridge deck carries over and the
    /// wrap round each car park.
    /// </summary>
    static List<Piece> Lay(CityPlan plan, SimConfig config, float walkM)
    {
        var pieces = new List<Piece>();
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            pieces.Add(Piece.Band(plan.Roads.SegmentsOf(road).ToArray(), (plan.Roads.WidthM[road] * 0.5f) + walkM));
        }

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            pieces.Add(Piece.Disc(plan.Junctions.CentreM[junction], plan.Junctions.RadiusM[junction] + walkM));
        }

        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var road = plan.Bridges.Road[bridge];
            if (road < 0) continue;

            var deckWalkM = plan.Bridges.PavementWidthM[bridge] > 0f ? plan.Bridges.PavementWidthM[bridge] : walkM;
            if (MathF.Abs(deckWalkM - walkM) < 0.01f) continue;

            pieces.Add(Piece.Band(plan.Roads.SegmentsOf(road).ToArray(), (plan.Roads.WidthM[road] * 0.5f) + deckWalkM));
        }

        for (var lot = 0; lot < plan.ParkingLots.Count; lot++)
        {
            var halfM = plan.ParkingLots.HalfExtentM[lot] + new Vector2(walkM);
            var radiusM = MathF.Min(config.PavementCornerRadiusM, MathF.Min(halfM.X, halfM.Y));
            pieces.Add(Piece.Wrap(plan.ParkingLots.CentreM[lot], plan.ParkingLots.Axis[lot], halfM, radiusM));
        }

        return pieces;
    }

    enum Kind : byte
    {
        Band,
        Disc,
        Wrap,
    }

    /// <summary>
    /// One piece of the pavement. <see cref="HalfM"/> carries what each kind is measured by — a band's
    /// half-width, a disc's radius, a wrap's half-extent — so one distance function serves all three.
    /// </summary>
    readonly record struct Piece(Kind Kind, Vector2 CentreM, Vector2 Axis, Vector2 HalfM, float RadiusM,
        ReadOnlyMemory<ArcSeg> Arcs)
    {
        public static Piece Band(ArcSeg[] arcs, float halfWidthM) =>
            new(Kind.Band, Vector2.Zero, Vector2.UnitX, new Vector2(halfWidthM), 0f, arcs);

        public static Piece Disc(Vector2 centreM, float radiusM) =>
            new(Kind.Disc, centreM, Vector2.UnitX, new Vector2(radiusM), radiusM, default);

        public static Piece Wrap(Vector2 centreM, Vector2 axis, Vector2 halfM, float radiusM) =>
            new(Kind.Wrap, centreM, axis.LengthSquared() > 0f ? Vector2.Normalize(axis) : Vector2.UnitX,
                halfM, radiusM, default);
    }

    /// <summary>
    /// Which pieces reach which square of the town, so asking what covers a point costs a handful of
    /// distance functions rather than every piece of pavement in the city.
    /// </summary>
    sealed class PieceGrid
    {
        const float SquareM = 16f;

        readonly List<int>[] _squares;
        readonly int _wide;
        readonly int _high;

        public PieceGrid(List<Piece> pieces, Vector2 worldM)
        {
            _wide = Math.Max(1, (int)(worldM.X / SquareM) + 1);
            _high = Math.Max(1, (int)(worldM.Y / SquareM) + 1);
            _squares = new List<int>[_wide * _high];

            for (var piece = 0; piece < pieces.Count; piece++)
            {
                // One box for a disc or a wrap, and one per arc for a band: a road's own box is most of a
                // district and would put every road in every square it bends anywhere near.
                if (pieces[piece].Kind != Kind.Band)
                {
                    Fill(piece, Box(pieces[piece]));
                    continue;
                }

                var reachM = new Vector2(pieces[piece].HalfM.X);
                foreach (var arc in pieces[piece].Arcs.Span)
                {
                    var leastM = new Vector2(float.MaxValue);
                    var mostM = new Vector2(float.MinValue);
                    var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM));
                    for (var step = 0; step <= steps; step++)
                    {
                        var onM = arc.PointAtM(arc.LengthM * step / steps);
                        leastM = Vector2.Min(leastM, onM);
                        mostM = Vector2.Max(mostM, onM);
                    }

                    Fill(piece, (leastM - reachM, mostM + reachM));
                }
            }
        }

        public ReadOnlySpan<int> At(Vector2 pointM)
        {
            var square = _squares[(Square(pointM.Y, _high) * _wide) + Square(pointM.X, _wide)];
            return square is null ? default : CollectionsMarshal.AsSpan(square);
        }

        void Fill(int piece, (Vector2 LeastM, Vector2 MostM) box)
        {
            for (var y = Square(box.LeastM.Y, _high); y <= Square(box.MostM.Y, _high); y++)
            {
                for (var x = Square(box.LeastM.X, _wide); x <= Square(box.MostM.X, _wide); x++)
                {
                    var square = _squares[(y * _wide) + x] ??= [];
                    if (!square.Contains(piece)) square.Add(piece);
                }
            }
        }

        static (Vector2 LeastM, Vector2 MostM) Box(in Piece piece)
        {
            var reachM = piece.Kind == Kind.Disc
                ? new Vector2(piece.HalfM.X)
                : new Vector2(
                    (MathF.Abs(piece.Axis.X) * piece.HalfM.X) + (MathF.Abs(piece.Axis.Y) * piece.HalfM.Y),
                    (MathF.Abs(piece.Axis.Y) * piece.HalfM.X) + (MathF.Abs(piece.Axis.X) * piece.HalfM.Y));

            return (piece.CentreM - reachM, piece.CentreM + reachM);
        }

        static int Square(float atM, int limit) => Math.Clamp((int)(atM / SquareM), 0, limit - 1);
    }
}
