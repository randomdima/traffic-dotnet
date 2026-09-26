using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

internal sealed partial class GroundShapes
{
    /// <summary>
    /// How many lines may pass within reach of one point before the index's answer stops being the whole
    /// one, over which the walk falls back to every line in the set rather than truncate.
    /// </summary>
    /// <remarks>
    /// <b>Deliberately far past what a junction can hold.</b> A seven-armed crossroads of two-way streets
    /// admits forty-two movements and fourteen lane ends, and a town whose junctions stand three metres apart
    /// puts its neighbours' lines within reach of the same point — so the figure that looked generous against
    /// one crossroads was not obviously generous against a city. It can afford to be: the frame is not zeroed
    /// (<c>SkipLocalsInit</c>), so the room nothing fills costs the stack pointer and not a kilobyte of
    /// stores, and the fallback it holds off walks every driven line in the town.
    /// </remarks>
    const int MostDrivenNear = 256;

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

    /// <summary>
    /// <b>The ground the town is driven over</b>: every lane and every line a car is turned through a box
    /// on, in one set, because they lay one surface. A junction has no shape of its own (TER-5) — the
    /// tarmac inside one is the band swept by the movements the town admits there — so it has no kind of
    /// its own either, and a skewed, one-way or five-armed box is answered for correctly without anything
    /// here knowing what shape it made.
    /// </summary>
    /// <remarks>
    /// <b>The lanes and not the roads.</b> A road's own band runs the whole length between the junctions at
    /// its ends and its lanes are cut back from them, so the road claims ground no car is driven over: a
    /// sliver at every mouth in the town, which the boundary leaves outside the driven ground and the road
    /// called carriageway. Asked of the lanes, the ground answered and the ground bounded are the same
    /// lines, and the two cannot disagree.
    /// </remarks>
    Movements _driven;

    /// <summary>
    /// <b>And the lines a car is driven into or out of a bay on</b>, answered as the ground a car idles on
    /// that a walker may stand on (TER-3). <b>Empty in every town this build lays</b>: it is the driven
    /// numbering past the movements (<see cref="Paving.DrivenCount"/>), which ends there, and a bay's way is
    /// a lane of the arm it is cut as (GEN-53) and is answered with <see cref="_driven"/>.
    /// </summary>
    /// <remarks>
    /// <b>A set of its own and not part of <see cref="_driven"/></b>, because the two answer different
    /// kinds: a bay is ground a car idles on that a walker may stand on (TER-3) and a carriageway is not.
    /// It is asked second for the same reason — a bay's way starts on the carriageway it leaves, so asked
    /// first it would call the near lane of every street a car park is on a car park.
    /// </remarks>
    Movements _bays;

    /// <summary>
    /// <b>Every line the town is driven on</b>, in the two sets the answer tells apart, laid over the index
    /// that says which of them reach a point. <b>Read off the plan's own lines</b> (<see cref="LaneLines"/>,
    /// <see cref="Paving"/>) and never re-derived: the ground under a line, the line itself and the boundary
    /// round the lot of them (<see cref="LaneShell"/>) are one geometry (TER-7).
    /// </summary>
    void LayTheDriven(Paving paving, SimConfig config)
    {
        var connectors = paving.Lanes.LaneCount + paving.Lanes.ConnectorCount;
        var bucketM = config.Terrain.GroundBucketM;
        _driven = Movements.Lay(paving, 0, connectors, bucketM);
        _bays = Movements.Lay(paving, connectors, paving.DrivenCount, bucketM);
    }

    /// <summary>
    /// One set of movements: their lines, their half-widths and the index that says which of them a point
    /// is near. <b>A street's, a junction's and a car park's are the same construction</b> — what tells
    /// them apart is which ground they answer and when, not how the band is measured.
    /// </summary>
    readonly struct Movements(ChainIndex index, ArcSeg[] arcs, int[] arcAt, float[] halfM, float[] lengthM, float farthestM)
    {
        public ChainIndex.Scan NewScan() => index.NewScan();

        /// <summary>
        /// <b>How far the point stands off the nearest of these bands</b> — nought anywhere on the ground
        /// they lay, and more than <paramref name="reachM"/> where none of them comes that near.
        /// </summary>
        /// <remarks>
        /// <b>A distance and not a yes.</b> Every ground beside the driven bands is the ground within one
        /// figure of them (TER-7b), so which layer a point stands in is this one figure compared against
        /// the table the picture is laid from — where a query per layer is a second walk of the same index
        /// answering the same question at a second offset, and two walks that could disagree.
        ///
        /// <b>The working set is not zeroed.</b> <see cref="ChainIndex.Near"/> fills every slot below the
        /// count it returns before anything reads one, and nothing here reads past that count — so
        /// initialising the frame is two kilobytes of stores a query pays and never reads, twice over, on the
        /// path every wheel of every car asks four times a tick.
        /// </remarks>
        [SkipLocalsInit]
        public float OffM(ChainIndex.Scan scan, Vector2 pointM, float reachM)
        {
            if (halfM.Length == 0) return float.MaxValue;

            Span<int> near = stackalloc int[MostDrivenNear];
            Span<float> alongM = stackalloc float[MostDrivenNear];
            var found = index.Near(scan, pointM, farthestM + reachM, near, alongM);
            var count = Math.Min(found, near.Length);
            var offM = float.MaxValue;
            for (var at = 0; at < count; at++)
            {
                offM = MathF.Min(offM, OffOneM(near[at], alongM[at], pointM));

                // On the ground itself there is nothing nearer to find, and it is where most of the
                // town's queries stand: every wheel of every car, four times a tick.
                if (offM <= 0f) return 0f;
            }

            // The index answered with more lines than there was room for, so what it gave back is part of
            // the answer rather than the answer (the same bargain <see cref="Roads"/> keeps).
            if (found <= near.Length) return offM;

            for (var line = 0; line < halfM.Length; line++)
            {
                var atM = Spline.ProjectM(ArcsOf(line), pointM, lengthM[line] * 0.5f, lengthM[line]);
                offM = MathF.Min(offM, OffOneM(line, atM, pointM));
                if (offM <= 0f) return 0f;
            }

            return offM;
        }

        /// <summary>
        /// How far the point stands off one line's own band — <b>along its ends as well as across it</b>, so
        /// the ground round the point of hand-over between a lane and the box is the same whichever of the
        /// two is asked.
        /// </summary>
        /// <remarks>
        /// <b>The tarmac is squared off at both ends and the distance turns that end</b>, the way a road's
        /// is (<see cref="Weigh"/>): a band ends where its own line does (TER-7a), and what stands a
        /// distance outside a square end is that end grown by the distance. Only a projection the chain had
        /// to clamp says the point is beyond the line at all — asked as a bare distance along, every point
        /// on a bend answers a little off perpendicular and the whole band reads as outside itself.
        /// </remarks>
        float OffOneM(int line, float atM, Vector2 pointM)
        {
            var on = Spline.SampleAt(ArcsOf(line), atM);
            var offsetM = pointM - on.PositionM;
            var alongM = Vector2.Dot(offsetM, on.Direction);
            var beyondM = atM <= 0f
                ? MathF.Max(0f, -alongM)
                : atM >= lengthM[line] ? MathF.Max(0f, alongM) : 0f;

            var acrossM = MathF.Abs(Vector2.Dot(offsetM, on.Right)) - halfM[line];
            return OffTheBandM(acrossM, beyondM);
        }

        ReadOnlySpan<ArcSeg> ArcsOf(int line) => arcs.AsSpan(arcAt[line], arcAt[line + 1] - arcAt[line]);

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

            var at = 0;
            for (var driven = fromLine; driven < toLine; driven++)
            {
                var line = paving.ArcsOfDriven(driven);
                if (line.Length == 0) continue;

                foreach (var arc in line) arcs.Add(arc);

                arcAt[at + 1] = arcs.Count;
                halfM[at] = paving.DrivenWidthM(driven) * 0.5f;
                lengthM[at] = paving.DrivenLengthM(driven);
                farthestM = MathF.Max(farthestM, halfM[at]);
                index.Add(at, line, lengthM[at]);
                at++;
            }

            return new Movements(index.Seal(bucketM), [.. arcs], arcAt, halfM, lengthM, farthestM);
        }
    }
}
