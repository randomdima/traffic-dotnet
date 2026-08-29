using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// Where one car park meets the road it hangs off: the stretch of that road's own metres its rectangle
/// stands against, which side of the centreline it is on, and how much ground is left between its near
/// edge and the kerb.
/// </summary>
/// <param name="FromM">
/// The first of those metres, which is <b>the metre one of the lot's own corners stands abeam of</b> and
/// not the lot's centre less the reach of its rectangle. The two agree only on a straight road, and where
/// they part it is by a hand's width — enough to break the kerb line short of the paint the lot starts with.
/// </param>
/// <param name="Side">+1 where the lot stands to the driver's right of the road's own direction, −1 to the left.</param>
/// <param name="OffM">How far off the centreline the lot's <em>centre</em> stands.</param>
/// <param name="FrontsTheKerb">
/// Whether the lot's near edge reaches the carriageway rather than standing behind a walk. <b>A gap
/// narrower than a painted line is no walk</b>: the ground on the far side of the kerb there is the lot's
/// own tarmac, and there is nothing left for a kerb line to be the edge of.
/// </param>
/// <param name="MouthFromM">
/// The first metre of the lot's <b>mouth</b> — the two corners of the one edge that faces the road, which
/// is the stretch the lot's own paint stands on. <b>It is not <see cref="FromM"/>:</b> that is the whole
/// rectangle's shadow, and a lot standing askew to the kerb throws its far corners past its near ones by
/// the depth times the skew. A kerb line broken over the shadow breaks a hand's width past the paint at
/// one end of every such lot.
/// </param>
/// <param name="MouthToM">The last of those metres.</param>
internal readonly record struct LotFrontage(
    int Lot, int Road, float FromM, float ToM, float OffM, float Side, bool FrontsTheKerb,
    float MouthFromM, float MouthToM);

/// <summary>
/// <b>Which road each car park hangs off, and over which of its metres</b> (GEN-4b) — the lot's four
/// corners projected onto the road's own centreline, which is the same measure <see cref="RoadCuts"/> takes
/// the junction discs against.
/// </summary>
/// <remarks>
/// <b>A lot's frontage is derived once.</b> Two slices need it and they need different things of it — the
/// road is cut for the section a lot's bays are reached over (<see cref="ParkingSections"/>), and the kerb
/// line is broken where the lot reaches the carriageway — and a second derivation would eventually
/// disagree with this one about where a car park begins.
/// </remarks>
internal sealed class RoadFrontages
{
    static readonly LotFrontage[] None = [];

    readonly int[] _offsets;
    readonly LotFrontage[] _fronts;

    RoadFrontages(int[] offsets, LotFrontage[] fronts)
    {
        _offsets = offsets;
        _fronts = fronts;
    }

    /// <summary>Every frontage in the town, in lot order.</summary>
    public ReadOnlySpan<LotFrontage> All => _fronts;

    /// <summary>The frontages one road carries, in the order they stand along it.</summary>
    public ReadOnlySpan<LotFrontage> On(int road) =>
        _offsets.Length == 0 ? None : _fronts.AsSpan(_offsets[road], _offsets[road + 1] - _offsets[road]);

    public static RoadFrontages Lay(CityPlan plan, SimConfig config)
    {
        var lots = plan.ParkingLots;
        var roads = plan.Roads;
        if (lots.Count == 0 || roads.Count == 0) return new RoadFrontages([], None);

        var lengthM = RoadLengthsM(plan);
        var found = new List<LotFrontage>(lots.Count);

        // The lot's four corners as they walk its rectangle, so each consecutive pair is one of its edges.
        Span<Vector2> cornersM = stackalloc Vector2[4];
        Span<float> atCornerM = stackalloc float[4];
        for (var lot = 0; lot < lots.Count; lot++)
        {
            var road = Nearest(plan, lengthM, lots.CentreM[lot], out var alongM, out var offM);
            if (road < 0) continue;

            var at = Spline.SampleAt(roads.SegmentsOf(road), alongM);
            var axis = Vector2.Normalize(lots.Axis[lot]);
            var side = new Vector2(-axis.Y, axis.X);
            var half = lots.HalfExtentM[lot];

            // <b>The frontage is the lot's own four corners projected, not its centre plus its size.</b>
            // The two are the same road only where the road is straight: the metre a corner stands abeam
            // of runs away from the metre the centre's projection plus the rectangle's shadow gives, and
            // the kerb line broken by the second one breaks a hand's width from where the lot's paint
            // starts.
            //
            // The mouth is whichever of the rectangle's four edges stands nearest the road.
            cornersM[0] = lots.CentreM[lot] - (axis * half.X) - (side * half.Y);
            cornersM[1] = lots.CentreM[lot] + (axis * half.X) - (side * half.Y);
            cornersM[2] = lots.CentreM[lot] + (axis * half.X) + (side * half.Y);
            cornersM[3] = lots.CentreM[lot] - (axis * half.X) + (side * half.Y);

            var fromM = float.PositiveInfinity;
            var toM = float.NegativeInfinity;
            for (var corner = 0; corner < cornersM.Length; corner++)
            {
                atCornerM[corner] = Spline.ProjectM(roads.SegmentsOf(road), cornersM[corner], alongM, lengthM[road]);
                fromM = MathF.Min(fromM, atCornerM[corner]);
                toM = MathF.Max(toM, atCornerM[corner]);
            }

            var mouthFromM = fromM;
            var mouthToM = toM;
            var mouthAwayM = float.PositiveInfinity;
            for (var edge = 0; edge < cornersM.Length; edge++)
            {
                var next = (edge + 1) % cornersM.Length;
                var midM = (cornersM[edge] + cornersM[next]) * 0.5f;
                var awayM = (Spline.SampleAt(
                    roads.SegmentsOf(road),
                    Spline.ProjectM(roads.SegmentsOf(road), midM, alongM, lengthM[road])).PositionM - midM).Length();
                if (awayM >= mouthAwayM) continue;

                mouthAwayM = awayM;
                mouthFromM = MathF.Min(atCornerM[edge], atCornerM[next]);
                mouthToM = MathF.Max(atCornerM[edge], atCornerM[next]);
            }

            // How far the rectangle reaches across the road, which is what a rectangle at any bearing
            // covers of a line running past it — the lot's own axis need not be the road's.
            var acrossSpanM =
                (MathF.Abs(Vector2.Dot(axis, at.Right)) * half.X) +
                (MathF.Abs(Spline.Cross(axis, at.Right)) * half.Y);

            var gapM = offM - acrossSpanM - (roads.WidthM[road] * 0.5f);
            found.Add(new LotFrontage(
                lot, road, fromM, toM, offM,
                Vector2.Dot(lots.CentreM[lot] - at.PositionM, at.Right) < 0f ? -1f : 1f,
                gapM <= config.Road.PaintLineWidthM, mouthFromM, mouthToM));
        }

        var offsets = new int[roads.Count + 1];
        foreach (var front in found) offsets[front.Road + 1]++;
        for (var road = 1; road < offsets.Length; road++) offsets[road] += offsets[road - 1];

        var cursor = (int[])offsets.Clone();
        var fronts = new LotFrontage[found.Count];
        foreach (var front in found) fronts[cursor[front.Road]++] = front;

        return new RoadFrontages(offsets, fronts);
    }

    /// <summary>
    /// How far a line running out of a car park has to go to meet <b>the kerb it fronts</b> — the
    /// carriageway's own edge curve, which is the line the kerb line's outer face stands on — signed along
    /// <paramref name="towards"/>, or <c>null</c> where the kerb is not within <paramref name="withinM"/>
    /// of where the line ends.
    /// </summary>
    /// <remarks>
    /// <b>A lot is a rectangle and a kerb is a curve</b> (GEN-4b), so the lot's own mouth edge stands off
    /// the carriageway's by up to the sag of the chord it was laid along. Paint ended on the lot's edge
    /// stops that far short of the kerb line at the mouth, which is a gap of most of a line's width in the
    /// one place a driver is looking. What the road owns is the road's to answer, so the ends are asked of
    /// it rather than measured off the rectangle a second time.
    /// </remarks>
    public static float? ReachToTheKerbM(
        CityPlan plan, in LotFrontage front, Vector2 fromM, Vector2 towards, float withinM)
    {
        var arcs = plan.Roads.SegmentsOf(front.Road);
        var edgeM = front.Side * plan.Roads.WidthM[front.Road] * 0.5f;
        var at = Spline.SampleAt(arcs, Spline.ProjectM(
            arcs, fromM, (front.MouthFromM + front.MouthToM) * 0.5f, front.MouthToM - front.MouthFromM));

        // A line running along the kerb rather than out at it never meets it.
        var stepM = Vector2.Dot(towards, at.Right);
        if (MathF.Abs(stepM) < 1e-3f) return null;

        var reachM = (edgeM - Vector2.Dot(fromM - at.PositionM, at.Right)) / stepM;
        return MathF.Abs(reachM) <= withinM ? reachM : null;
    }

    public static float[] RoadLengthsM(CityPlan plan)
    {
        var lengthM = new float[plan.Roads.Count];
        for (var road = 0; road < lengthM.Length; road++)
        {
            lengthM[road] = Spline.TotalLengthM(plan.Roads.SegmentsOf(road));
        }

        return lengthM;
    }

    /// <summary>The road whose centreline passes nearest a place, how far along it that is, and how far off it stands.</summary>
    public static int Nearest(CityPlan plan, float[] lengthM, Vector2 pointM, out float alongM, out float offM)
    {
        var roads = plan.Roads;
        var best = -1;
        alongM = 0f;
        offM = float.PositiveInfinity;

        for (var road = 0; road < roads.Count; road++)
        {
            var centreline = roads.SegmentsOf(road);
            if (centreline.Length == 0) continue;

            var atM = Spline.ProjectM(centreline, pointM, lengthM[road] * 0.5f, lengthM[road]);
            var awayM = (Spline.SampleAt(centreline, atM).PositionM - pointM).Length();
            if (awayM >= offM) continue;

            offM = awayM;
            alongM = atM;
            best = road;
        }

        return best;
    }
}
