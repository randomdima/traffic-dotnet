using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The town's ground said as one boundary and a table of distances off it</b> (TER-3c.3, TER-7): the
/// shell's rings (<see cref="LaneShell"/>) with every kerb corner turned on them, and every line the ground
/// has — the kerb, the kerb line, the lane a walker follows, the pavement's outer edge — that one ring moved
/// by a figure.
/// </summary>
/// <remarks>
/// <para>
/// <b>One curve, so a width is a subtraction.</b> Two of these lines are offsets of the same ring by amounts
/// differing by a constant, so the band between them is exactly that constant wide wherever the fold rule
/// left both of them standing. Laid as separate unions of separate pieces, the same two lines were a walk
/// apart only where nothing conspired against it, and nothing could measure whether they were.
/// </para>
/// <para>
/// <b>Nought is the kerb.</b> The rings run down the lines cars are driven on and the edge of what they lay
/// stands half a band out, so every distance here is measured from that edge — the only place a reader with
/// a ruler could measure one from. A negative distance is inside the tarmac and is how a kerb line is
/// struck; a positive one is out over the pavement and the grass.
/// </para>
/// <para>
/// <b>The corner is turned on the ring and not laid beside it</b> (TER-5). A kerb turns a corner on an arc
/// tangent to both carriageways, and a ring corner pre-turned on that radius less its own half-width comes
/// out, once moved, as exactly that arc — so the fillet is a property of the boundary rather than a shape
/// somebody else has to remember to draw. Every distance inherits it, each at its own radius.
/// </para>
/// <para>
/// <b>Build-time to lay, tick-time to ask.</b> Laying the rings walks the town; <see cref="OffTheKerbM"/> is
/// asked per wheel per tick and allocates nothing.
/// </para>
/// </remarks>
internal sealed class GroundRings
{
    /// <summary>
    /// How far the lines are smoothed over. <b>Nought: a distance that is smoothed is not the distance it
    /// says it is</b>, and every reader of these lines measures against the figure it asked for. What
    /// smoothing is for is a picture of a whole town read at a glance, which is the debug layer's own ask
    /// and carries its own window (<c>DebugOverlay.ExtrudedSmoothM</c>).
    /// </summary>
    public const float SmoothM = 0f;

    readonly LaneShell _shell;
    readonly float _reachM;
    readonly Segments _kerb;

    GroundRings(LaneShell shell, float reachM, Segments kerb)
    {
        _shell = shell;
        _reachM = reachM;
        _kerb = kerb;
    }

    /// <summary>
    /// The rings, with every corner the ground turns already turned on them, and the kerb indexed for the
    /// one question a tick asks.
    /// </summary>
    public static GroundRings Of(Paving paving, SimConfig config)
    {
        var shell = paving.Perimeter(config).Rounded(config);
        var reachM = config.RoadFootprintM;
        return new GroundRings(shell, reachM, new Segments(shell.Extruded(0f, SmoothM), reachM));
    }

    /// <summary>
    /// <b>The rings that stand <paramref name="outM"/> off the kerb</b>, one for one with the shell's own
    /// and empty where the distance left a ring nothing.
    /// </summary>
    public ReadOnlySpan<ArcSeg[]> At(float outM) => _shell.Extruded(outM, SmoothM);

    /// <summary>The shell the distances are taken off, for a reader that wants the lines themselves.</summary>
    public LaneShell Shell => _shell;

    /// <summary>
    /// <b>How far a point stands off the kerb</b> — negative on the tarmac, positive off it, and
    /// <see cref="Reach"/> where the boundary is further away than the answer distinguishes.
    /// </summary>
    /// <remarks>
    /// <b>The sign is the nearest boundary's own hand and not a crossing count.</b> A ring walks with the
    /// ground on its right throughout, on the ring round the town and on the ring round every block it
    /// encloses (<see cref="LaneShell"/>), so which side of the nearest piece of boundary a point stands is
    /// the whole of the answer — and it needs no ray, no ordering of the rings and no knowing which of them
    /// is the outermost. Where the nearest place is a corner rather than a piece, the two pieces meeting
    /// there answer together, which is what keeps the sign right in the wedge outside a sharp one.
    /// </remarks>
    public float OffTheKerbM(Vector2 pointM) => _kerb.OffM(pointM, _reachM);

    /// <summary>
    /// How far off the kerb the answer is still measured. <b>Past it the question is not asked</b>: nothing
    /// the table names stands further off the kerb than a road and its two pavements, so a point further
    /// than that from every kerb in the town is grass wherever it is, and a caller that needs to know
    /// whether deep tarmac is tarmac asks the bands that lay it rather than this.
    /// </summary>
    public float Reach => _reachM;

    /// <summary>
    /// <b>The kerb as the straights it is, binned by where they stand.</b> Everything an extrusion hands
    /// back is a straight (<c>Extrusion.Straights</c>), so a distance to one is a distance to a segment and
    /// the whole index is a uniform grid of them.
    /// </summary>
    /// <remarks>
    /// <b>The cell is the reach</b>, so everything within one stands in the nine cells about the point —
    /// the same bargain <c>Extrusion.Stations</c> strikes, and the reason a query is nine cell walks rather
    /// than a search that grows.
    /// </remarks>
    sealed class Segments
    {
        readonly Vector2[] _fromM;
        readonly Vector2[] _toM;
        readonly int[] _cellStart;
        readonly int[] _entry;
        readonly Vector2 _originM;
        readonly float _inverseCellM;
        readonly int _width;
        readonly int _height;

        public Segments(ReadOnlySpan<ArcSeg[]> rings, float cellM)
        {
            var count = 0;
            foreach (var ring in rings) count += ring.Length;

            _fromM = new Vector2[count];
            _toM = new Vector2[count];
            var at = 0;
            var leastM = new Vector2(float.MaxValue);
            var mostM = new Vector2(float.MinValue);
            foreach (var ring in rings)
            {
                foreach (var arc in ring)
                {
                    _fromM[at] = arc.StartM;
                    _toM[at] = arc.EndM;
                    leastM = Vector2.Min(leastM, Vector2.Min(arc.StartM, arc.EndM));
                    mostM = Vector2.Max(mostM, Vector2.Max(arc.StartM, arc.EndM));
                    at++;
                }
            }

            if (count == 0)
            {
                leastM = Vector2.Zero;
                mostM = Vector2.Zero;
            }

            var spanM = mostM - leastM;
            _originM = leastM;
            _inverseCellM = 1f / cellM;
            _width = (int)(spanM.X * _inverseCellM) + 1;
            _height = (int)(spanM.Y * _inverseCellM) + 1;

            // A piece is written into every cell its own box touches, so a query reading the nine about it
            // sees every piece that could stand within a cell of it however that piece happens to lie.
            _cellStart = new int[(_width * _height) + 1];
            for (var segment = 0; segment < count; segment++)
            {
                var (leastCell, mostCell) = Box(segment);
                for (var y = leastCell.Y; y <= mostCell.Y; y++)
                {
                    for (var x = leastCell.X; x <= mostCell.X; x++) _cellStart[(y * _width) + x + 1]++;
                }
            }

            for (var cell = 0; cell < _width * _height; cell++) _cellStart[cell + 1] += _cellStart[cell];

            var cursor = new int[_width * _height];
            _entry = new int[_cellStart[^1]];
            for (var segment = 0; segment < count; segment++)
            {
                var (leastCell, mostCell) = Box(segment);
                for (var y = leastCell.Y; y <= mostCell.Y; y++)
                {
                    for (var x = leastCell.X; x <= mostCell.X; x++)
                    {
                        var cell = (y * _width) + x;
                        _entry[_cellStart[cell] + cursor[cell]++] = segment;
                    }
                }
            }
        }

        ((int X, int Y) Least, (int X, int Y) Most) Box(int segment) =>
            (Cell(Vector2.Min(_fromM[segment], _toM[segment])), Cell(Vector2.Max(_fromM[segment], _toM[segment])));

        /// <summary>
        /// The nearest place on the kerb, and which side of it the point stands, as one signed distance.
        /// </summary>
        /// <remarks>
        /// <b>The side is summed over every piece equally near</b>, which is what a corner is: two pieces
        /// meeting at a point, whose two outward normals together say which way is out of the wedge between
        /// them. Taken off whichever single piece a float preferred instead, a point in the wedge outside a
        /// sharp corner reads inside the ground as often as not.
        /// </remarks>
        public float OffM(Vector2 pointM, float reachM)
        {
            var nearestSq = reachM * reachM;
            var footM = Vector2.Zero;
            var outward = Vector2.Zero;
            var found = false;
            var atX = Math.Clamp((int)((pointM.X - _originM.X) * _inverseCellM), 0, _width - 1);
            var atY = Math.Clamp((int)((pointM.Y - _originM.Y) * _inverseCellM), 0, _height - 1);
            for (var y = Math.Max(0, atY - 1); y <= Math.Min(_height - 1, atY + 1); y++)
            {
                for (var x = Math.Max(0, atX - 1); x <= Math.Min(_width - 1, atX + 1); x++)
                {
                    var cell = (y * _width) + x;
                    for (var entry = _cellStart[cell]; entry < _cellStart[cell + 1]; entry++)
                    {
                        Weigh(_entry[entry], pointM, ref nearestSq, ref footM, ref outward, ref found);
                    }
                }
            }

            if (!found) return reachM;

            var offM = MathF.Sqrt(MathF.Max(nearestSq, 0f));

            // A point standing exactly on the boundary has no side, and nought is the right answer for it.
            var side = Vector2.Dot(pointM - footM, outward);
            return side < 0f ? -offM : offM;
        }

        /// <summary>
        /// One piece weighed against the nearest found so far. <b>A piece nearer by more than a rounding
        /// takes the answer; one within a rounding of it joins in</b>, which is how a corner comes to answer
        /// with both of its pieces.
        /// </summary>
        void Weigh(
            int segment, Vector2 pointM, ref float nearestSq, ref Vector2 footM, ref Vector2 outward,
            ref bool found)
        {
            var fromM = _fromM[segment];
            var runM = _toM[segment] - fromM;
            var lengthSq = runM.LengthSquared();
            var alongM = lengthSq > 0f
                ? Math.Clamp(Vector2.Dot(pointM - fromM, runM) / lengthSq, 0f, 1f)
                : 0f;
            var atM = fromM + (runM * alongM);
            var distanceSq = Vector2.DistanceSquared(pointM, atM);
            if (found && distanceSq > nearestSq + SameSq) return;

            // Out of the ground is the piece's left, the ground standing on its right throughout.
            var left = new Vector2(runM.Y, -runM.X);
            if (lengthSq > 0f) left /= MathF.Sqrt(lengthSq);

            if (!found || distanceSq < nearestSq - SameSq)
            {
                nearestSq = distanceSq;
                footM = atM;
                outward = left;
                found = true;
                return;
            }

            nearestSq = MathF.Min(nearestSq, distanceSq);
            outward += left;
        }

        /// <summary>
        /// How near two pieces have to stand to one point to be one corner of it: a millimetre, which is
        /// what two computations of one distance disagree by (<see cref="Kerbs.RoundingM"/>), squared.
        /// </summary>
        const float SameSq = Kerbs.RoundingM * Kerbs.RoundingM;

        (int X, int Y) Cell(Vector2 pointM) =>
        (
            Math.Clamp((int)((pointM.X - _originM.X) * _inverseCellM), 0, _width - 1),
            Math.Clamp((int)((pointM.Y - _originM.Y) * _inverseCellM), 0, _height - 1)
        );
    }
}
