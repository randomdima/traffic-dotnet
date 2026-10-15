using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>What a car follows, as a world keeps it</b>: the lane its line is on, the join after it and the lane that join
/// leads onto, and where that stretch of the line stops — and the world's own reading of the car on it each step.
/// </summary>
/// <remarks>
/// <para>
/// <b>The stretch is the line's own head to the bit</b> (<see cref="LineAssembler.Assemble"/>): the lane's pieces as it
/// stands (<see cref="Spline.HeadInto"/>), the join's whole, and the next lane's, cut where the line stops. Metres are
/// the line's, from the lane's start, which is where every line is laid and shifted to begin.
/// </para>
/// <para>
/// <b>Read around where it was read last</b>: <see cref="AroundM"/> is what the reading is taken around, and a step's
/// own reading moves it on to <see cref="ProgressM"/> before it reads again; a view read again off the clock reads around
/// the same place and so answers the same. A follow signalled puts both where the car's actor says it is.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct LineFollowed
{
    /// <summary>The lane the line is on, or −1 for a line with no lanes, which no world reads.</summary>
    public int Lane;

    /// <summary>The join off it onto the next lane of the line, or <see cref="RoadGraph.NoConnector"/> where the line ends on it.</summary>
    public int Join;

    /// <summary>How far along its own lane the last lane of the stretch is laid to (<see cref="LineAssembler.LaidToM"/>).</summary>
    public float StopsAtM;

    public float AroundM;

    /// <summary>Where along the line the rear axle was read, in the line's metres.</summary>
    public float ProgressM;

    /// <summary>How far the rear axle stands off the line where it was read.</summary>
    public float OffLineM;

    /// <summary>Which follow the actor signalled this is, handed back with the reading so a stale one is known.</summary>
    public uint Serial;

    readonly uint _pad;

    public static LineFollowed None => new() { Lane = -1, Join = RoadGraph.NoConnector, ProgressM = float.NaN };

    public LineFollowed(int lane, int join, float stopsAtM, float aroundM, uint serial)
    {
        Lane = lane;
        Join = join;
        StopsAtM = stopsAtM;
        AroundM = aroundM;
        ProgressM = aroundM;
        OffLineM = float.NaN;
        Serial = serial;
    }
}

/// <summary>
/// <b>Where a car's rear axle stands on the lanes it follows</b> (<see cref="LineFollowed"/>), read by the world each step
/// so the actor projects nothing (the root decision log, 2026-10-14). The device reads it the same to the bit
/// (<c>world/road/kernels/line_reading.glsl</c>).
/// </summary>
/// <remarks>
/// The arithmetic is <see cref="Spline.ProjectM(ReadOnlySpan{ArcSeg}, Vector2, float, float, out float)"/> and then
/// <see cref="Spline.SampleFrom"/> over the line's own head, walked piece by piece off the lanes rather than off a copy.
/// </remarks>
internal static class LineReading
{
    /// <summary>The reading taken around <see cref="LineFollowed.AroundM"/> and written into <paramref name="followed"/>.</summary>
    public static void Read(RoadGraph roads, ref LineFollowed followed, Vector2 rearAxleM, float windowM)
    {
        if (followed.Lane < 0)
        {
            followed.ProgressM = followed.OffLineM = float.NaN;
            return;
        }

        var nearest = new Nearest(rearAxleM, followed.AroundM, windowM);
        Walk(roads, followed, ref nearest);

        var sample = new Sample(nearest.BestM);
        Walk(roads, followed, ref sample);

        followed.ProgressM = nearest.BestM;
        followed.OffLineM = (sample.PointM - rearAxleM).Length();
    }

    /// <summary>
    /// <b>The point the wheel is aimed at</b> (S-1): what follows sampled a lookahead past the reading, as
    /// <c>CarFollower.Steer</c> samples the car's own line — <paramref name="atM"/> where it was, and NaN where the
    /// lookahead runs past the end of what the car follows, whose line may run on past it.
    /// </summary>
    public static Vector2 Lead(RoadGraph roads, in LineFollowed followed, float lookaheadM, out float atM)
    {
        atM = float.NaN;
        if (followed.Lane < 0 || float.IsNaN(followed.ProgressM)) return default;

        var aheadM = followed.ProgressM + lookaheadM;
        var sample = new Sample(aheadM);
        Walk(roads, followed, ref sample);
        if (sample.RunsPast) return default;

        atM = aheadM;
        return sample.PointM;
    }

    interface IPieces
    {
        /// <returns>Whether the walk goes on.</returns>
        bool Take(in ArcSeg piece);
    }

    static void Walk<T>(RoadGraph roads, in LineFollowed followed, ref T pieces) where T : struct, IPieces
    {
        var join = followed.Join;
        var toM = join == RoadGraph.NoConnector ? followed.StopsAtM : roads.LaneLengthM[followed.Lane];
        if (!Lane(roads.ArcsOf(followed.Lane), toM, ref pieces) || join == RoadGraph.NoConnector) return;

        foreach (ref readonly var arc in roads.ConnectorArcs(join))
        {
            if (!pieces.Take(arc)) return;
        }

        Lane(roads.ArcsOf(roads.ConnectorTo(join)), followed.StopsAtM, ref pieces);
    }

    /// <summary>A lane's pieces as <see cref="Spline.HeadInto"/> lays them.</summary>
    static bool Lane<T>(ReadOnlySpan<ArcSeg> arcs, float toM, ref T pieces) where T : struct, IPieces
    {
        var startM = 0f;
        foreach (ref readonly var arc in arcs)
        {
            var endM = startM + arc.LengthM;
            var takeToM = MathF.Min(toM, endM);
            if (takeToM > startM && !pieces.Take(takeToM == endM ? arc : arc with { LengthM = takeToM - startM }))
            {
                return false;
            }

            startM = endM;
        }

        return true;
    }

    /// <summary>The nearest place inside the window, as <c>Spline.ProjectFrom</c> walks for it.</summary>
    struct Nearest(Vector2 pointM, float aroundM, float windowM) : IPieces
    {
        readonly float _fromM = MathF.Max(0f, aroundM - windowM);
        readonly float _toM = aroundM + windowM;
        float _startM;
        float _bestSq = float.MaxValue;

        public float BestM { get; private set; } = MathF.Max(0f, aroundM - windowM);

        public bool Take(in ArcSeg piece)
        {
            if (_startM > _toM) return false;

            var endM = _startM + piece.LengthM;
            if (endM >= _fromM)
            {
                var alongM = Spline.NearestOnArc(piece, pointM);
                alongM = Math.Clamp(_startM + alongM, _fromM, _toM) - _startM;
                var distanceSq = (piece.PointAtM(alongM) - pointM).LengthSquared();
                if (distanceSq < _bestSq)
                {
                    _bestSq = distanceSq;
                    BestM = _startM + alongM;
                }
            }

            _startM = endM;
            return true;
        }
    }

    /// <summary>The point a distance along, as <see cref="Spline.SampleFrom"/> finds it: the first piece reaching it, or the last.</summary>
    struct Sample(float distanceM) : IPieces
    {
        readonly float _remainingM = MathF.Max(0f, distanceM);
        ArcSeg _piece;
        float _pieceStartM;
        bool _held;

        public readonly Vector2 PointM => _piece.PointAtM(MathF.Min(_remainingM - _pieceStartM, _piece.LengthM));

        /// <summary>Whether the distance lies past the last piece, where the point is held at its end.</summary>
        public readonly bool RunsPast => _remainingM - _pieceStartM > _piece.LengthM;

        public bool Take(in ArcSeg piece)
        {
            if (!_held)
            {
                (_piece, _held) = (piece, true);
                return true;
            }

            if (_remainingM - _pieceStartM <= _piece.LengthM) return false;

            _pieceStartM += _piece.LengthM;
            _piece = piece;
            return true;
        }
    }
}
