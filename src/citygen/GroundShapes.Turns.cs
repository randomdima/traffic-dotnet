using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

internal sealed partial class GroundShapes
{
    /// <summary>
    /// How many lines may pass within reach of one point before the index's answer stops being the whole
    /// one. A four-armed crossroads of two-way streets admits twelve movements and every one of them runs
    /// near the middle of it; this is that with room to spare, and the walk falls back to every line in the
    /// set rather than truncate.
    /// </summary>
    const int MostTurnsNear = 32;

    /// <summary>
    /// How far a point stands off a band, given how far it is outside the band's own half-width and how far
    /// past its end. <b>The corner is an arc and not a right angle</b>, because the band is grown by a
    /// distance and a distance turns a corner.
    /// </summary>
    static float OffTheBandM(float acrossM, float pastM)
    {
        var outM = MathF.Max(0f, acrossM);
        return pastM <= 0f ? outM : MathF.Sqrt((outM * outM) + (pastM * pastM));
    }

    Movements _lanes;
    Movements _turns;
    Movements _bays;

    /// <summary>
    /// <b>Whether a point stands on a lane</b> — the carriageway, which is the band every lane of every
    /// street lays and nothing else.
    /// </summary>
    /// <remarks>
    /// <b>The lanes and not the roads.</b> A road's own band runs the whole length between the junctions at
    /// its ends and its lanes are cut back from them, so the road claims ground no car is driven over: a
    /// sliver at every mouth in the town, which the boundary leaves outside the driven ground and the road
    /// called carriageway. Asked of the lanes, the ground answered and the ground bounded are the same
    /// lines, and the two cannot disagree.
    /// </remarks>
    bool Lanes(Vector2 pointM, float outM) => _lanes.Covers(pointM, outM);

    /// <summary>
    /// <b>Whether a point stands on a line a car is turned through a box on</b> — which is the whole of what
    /// a junction is on the ground (TER-5). There is no disc and no box: the tarmac inside an intersection
    /// is the band swept by the movements the town admits there, so a skewed, one-way or five-armed
    /// junction is answered for correctly without anything here knowing what shape it made.
    /// </summary>
    /// <remarks>
    /// <b>Asked at nought for the tarmac and at a walk for the pavement beside it</b> (TER-3c.3). A
    /// movement swings wider than either of the arms it runs between, so the arms' own bands do not carry
    /// the pavement past the box: what the walk is laid off is the tarmac, and a box's tarmac is these
    /// lines. Left out, the pavement the town walks turned a junction's corner over open grass, because
    /// the wrap follows the movement and the drawn band followed the arms.
    /// </remarks>
    bool Turns(Vector2 pointM, float outM) => _turns.Covers(pointM, outM);

    /// <summary>
    /// <b>And whether it stands on a line a car is driven into or out of a bay on</b> — which is the whole
    /// of what a car park is on the ground, for exactly the reason a junction is the movements that cross
    /// in it (GEN-4b, <see cref="Paving"/>).
    /// </summary>
    /// <remarks>
    /// <b>A separate set from the turns and not a flag on them</b>, because the two answer at different
    /// rungs of <see cref="At"/>: a bay's way starts on the carriageway it leaves, so asked before the
    /// roads it would call the near lane of every street a car park is on a car park.
    /// </remarks>
    bool BayWays(Vector2 pointM, float outM) => _bays.Covers(pointM, outM);

    /// <summary>
    /// <b>Every line the town is driven on</b>, in the three sets the answer tells apart — the lanes, the
    /// lines through a box, and the ways into a bay — laid over the index that answers which of them reach
    /// a point. <b>Read off the plan's own lines</b> (<see cref="LaneLines"/>, <see cref="Paving"/>) and
    /// never re-derived: the ground under a line, the line itself and the boundary round the lot of them
    /// (<see cref="LaneShell"/>) are one geometry (TER-7).
    /// </summary>
    void LayTheTurns(Paving paving, Vector2 worldSizeM, SimConfig config)
    {
        var lanes = paving.Lanes.LaneCount;
        var connectors = lanes + paving.Lanes.ConnectorCount;
        var bucketM = config.Terrain.GroundBucketM;
        _lanes = Movements.Lay(paving, 0, lanes, bucketM);
        _turns = Movements.Lay(paving, lanes, connectors, bucketM);
        _bays = Movements.Lay(paving, connectors, paving.DrivenCount, bucketM);
    }

    /// <summary>
    /// One set of movements: their lines, their half-widths and the index that says which of them a point
    /// is near. <b>A junction's and a car park's are the same construction asked twice</b> — what tells
    /// them apart is which ground they answer and when, not how the band is measured.
    /// </summary>
    readonly struct Movements(ChainIndex index, ArcSeg[] arcs, int[] arcAt, float[] halfM, float[] lengthM, float farthestM)
    {
        public bool Covers(Vector2 pointM, float outM)
        {
            if (halfM.Length == 0) return false;

            Span<int> near = stackalloc int[MostTurnsNear];
            Span<float> alongM = stackalloc float[MostTurnsNear];
            var found = index.Near(pointM, farthestM + outM, near, alongM);
            var count = Math.Min(found, near.Length);
            for (var at = 0; at < count; at++)
            {
                if (Sweeps(near[at], alongM[at], pointM, outM)) return true;
            }

            // The index answered with more lines than there was room for, so what it gave back is part of
            // the answer rather than the answer (the same bargain <see cref="Roads"/> keeps).
            if (found <= near.Length) return false;

            for (var turn = 0; turn < halfM.Length; turn++)
            {
                var atM = Spline.ProjectM(ArcsOf(turn), pointM, lengthM[turn] * 0.5f, lengthM[turn]);
                if (Sweeps(turn, atM, pointM, outM)) return true;
            }

            return false;
        }

        /// <summary>
        /// One line's own band, grown by the offset asked for — <b>along its ends as well as across it</b>,
        /// so the ground round the point of hand-over between a lane and the box is the same whichever of
        /// the two is asked.
        /// </summary>
        /// <remarks>
        /// <b>The tarmac is squared off at both ends and the offset turns that end</b>, the way a road's is
        /// (<see cref="Weigh"/>): a band ends where its own line does (TER-7a), and what stands a distance
        /// outside a square end is that end grown by the distance. Only a projection the chain had to clamp
        /// says the point is beyond the line at all — asked as a bare distance along, every point on a bend
        /// answers a little off perpendicular and the whole band reads as outside itself.
        /// </remarks>
        bool Sweeps(int turn, float atM, Vector2 pointM, float outM)
        {
            var on = Spline.SampleAt(ArcsOf(turn), atM);
            var offsetM = pointM - on.PositionM;
            var alongM = Vector2.Dot(offsetM, on.Direction);
            var beyondM = atM <= 0f
                ? MathF.Max(0f, -alongM)
                : atM >= lengthM[turn] ? MathF.Max(0f, alongM) : 0f;

            var acrossM = MathF.Abs(Vector2.Dot(offsetM, on.Right)) - halfM[turn];
            return OffTheBandM(acrossM, beyondM) <= outM;
        }

        ReadOnlySpan<ArcSeg> ArcsOf(int turn) => arcs.AsSpan(arcAt[turn], arcAt[turn + 1] - arcAt[turn]);

        public static Movements Lay(Paving paving, int fromLine, int toLine, float bucketM)
        {
            var laid = 0;
            for (var line = fromLine; line < toLine; line++)
            {
                if (paving.ArcsOfDriven(line).Length > 0) laid++;
            }

            var arcAt = new int[laid + 1];
            var halfM = new float[laid];
            var lengthM = new float[laid];
            var arcs = new List<ArcSeg>();
            var index = new ChainIndex.Builder();
            var farthestM = 0f;

            var turn = 0;
            for (var driven = fromLine; driven < toLine; driven++)
            {
                var line = paving.ArcsOfDriven(driven);
                if (line.Length == 0) continue;

                foreach (var arc in line) arcs.Add(arc);

                arcAt[turn + 1] = arcs.Count;
                halfM[turn] = paving.DrivenWidthM(driven) * 0.5f;
                lengthM[turn] = paving.DrivenLengthM(driven);
                farthestM = MathF.Max(farthestM, halfM[turn]);
                index.Add(turn, line, lengthM[turn]);
                turn++;
            }

            return new Movements(index.Seal(bucketM), [.. arcs], arcAt, halfM, lengthM, farthestM);
        }
    }
}
