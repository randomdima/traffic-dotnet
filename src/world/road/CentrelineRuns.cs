using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>A stretch of one road, in that road's own metres.</summary>
internal readonly record struct RoadStretch(float FromM, float ToM);

/// <summary>
/// A stretch of one road nothing may be dashed over, and <b>the junction that closed it</b> —
/// <see cref="CityPlan.NoRecord"/> where what closed it belongs to no junction.
/// </summary>
internal readonly record struct ClosedStretch(float FromM, float ToM, int Junction);

/// <summary>
/// <b>The stretches of each road its centreline is dashed over</b>: what is left once the junctions the
/// road runs through are taken off it, and the crossings struck across it, and the ground between a stop
/// bar and the junction that bar is painted for.
/// </summary>
/// <remarks>
/// <para>
/// <b>A dash marks the middle of a carriageway, and none of those is one.</b> The metres between the paint
/// on an arm and the disc behind it are ground the turning movements are driven over rather than a lane, so
/// a dashed line laid down them draws a lane running into the box — which is what the markings rule forbids
/// when it says a centreline stops before a junction. A dash over a zebra is the same paint twice and shows
/// as a bright streak down the bars.
/// </para>
/// <para>
/// <b>Every piece of paint that names a junction closes the ground back to it</b>, and not the bar alone: a
/// junction the ranking governs carries no bar (TLT-3) and the same metres, so a rule written round the bar
/// leaves the throat of every unlit junction in the town dashed up to its own mouth.
/// </para>
/// <para>
/// <b>A junction that admits no fork closes nothing at all</b> (TER-6) — neither the ground behind its paint
/// nor its own disc. Nothing turns across either, so both are the middle of a carriageway like the rest of
/// the road: the bundle on its arm closes only itself, and the line runs from the bar through the node and
/// out along the other arm. The exception is the zebra an inline junction lays <em>on</em> the node, which
/// breaks the line the way any paint does — <see cref="ClosesTheGroundBehindItsPaint"/>.
/// </para>
/// <para>
/// <b>A road runs through every junction on it and not only the two it ends at</b> (TER-4), so a run is
/// the complement of what is blocked and never the road with its two ends trimmed.
/// </para>
/// <para>
/// The stretches are exact rather than sampled: every boundary here is a distance somebody else already
/// measured — <see cref="RoadCuts"/> for the discs, the paint registers for the bars and the zebras — so
/// nothing walks the curve asking whether it is clear yet, and no dash lands half on a crossing because
/// the walk stepped over its edge.
/// </para>
/// </remarks>
internal sealed class CentrelineRuns
{
    static readonly RoadStretch[] None = [];

    readonly int[] _offsets;
    readonly RoadStretch[] _runs;

    CentrelineRuns(int[] offsets, RoadStretch[] runs)
    {
        _offsets = offsets;
        _runs = runs;
    }

    /// <summary>The stretches one road is dashed over, in the order they stand along it.</summary>
    public ReadOnlySpan<RoadStretch> On(int road) =>
        _offsets.Length == 0 ? None : _runs.AsSpan(_offsets[road], _offsets[road + 1] - _offsets[road]);

    public static CentrelineRuns Lay(CityPlan plan, SimConfig config)
    {
        var roads = plan.Roads;
        if (roads.Count == 0) return new CentrelineRuns([], None);

        var lengthM = RoadFrontages.RoadLengthsM(plan);
        var blocked = Blocked(plan, lengthM);
        var closes = ClosesTheGroundBehindItsPaint(plan);
        var reachM = RoadCuts.ReachesM(plan, config);
        var discs = RoadCuts.JunctionIndex(plan, paddingM: 0f);
        var cuts = new List<RoadCut>();
        var offsets = new int[roads.Count + 1];
        var runs = new List<RoadStretch>();

        for (var road = 0; road < roads.Count; road++)
        {
            offsets[road + 1] = offsets[road];
            var centreline = roads.SegmentsOf(road);
            if (centreline.Length == 0) continue;

            var spans = blocked[road] ??= [];
            RoadCuts.Along(
                plan, discs, centreline, lengthM[road], paddingM: 0f, roads.FromJunction[road],
                roads.ToJunction[road], cuts);
            // <b>A junction takes the ground it reaches and not the disc it is drawn on</b>: the box is the
            // disc plus the corners flared off it, and an arm carrying no paint has nothing else to stop its
            // dashes short of it. The disc of a junction that forks nothing takes nothing at all — the two
            // arms are one carriageway, so the line runs to the node and picks up on the other side of it.
            foreach (var cut in cuts)
            {
                if (!closes[cut.Junction]) continue;

                var flareM = reachM[cut.Junction] - plan.Junctions.RadiusM[cut.Junction];
                spans.Add(new ClosedStretch(cut.EnterM - flareM, cut.ExitM + flareM, cut.Junction));
            }

            for (var bar = 0; bar < plan.StopLines.Count; bar++)
            {
                if (plan.StopLines.Road[bar] != road) continue;

                var atM = Spline.ProjectM(centreline, plan.StopLines.CentreM[bar], lengthM[road] * 0.5f, lengthM[road]);
                var halfM = plan.StopLines.ThicknessM[bar] * 0.5f;
                spans.Add(new ClosedStretch(atM - halfM, atM + halfM, plan.StopLines.Junction[bar]));
            }

            // Each piece of paint carries the junction it was laid for, so the ground it closes off reaches
            // from the paint itself to the far side of that disc — one span, and whatever stands between the
            // two is inside it. A junction with no fork has no such ground: what is behind its bundle is the
            // road bending, and the gaps inside the bundle are shorter than a dash.
            for (var span = 0; span < spans.Count; span++)
            {
                var closed = spans[span];
                if (closed.Junction < 0 || closed.Junction >= closes.Length || !closes[closed.Junction]) continue;

                foreach (var cut in cuts)
                {
                    if (cut.Junction != closed.Junction) continue;

                    closed = closed with
                    {
                        FromM = MathF.Min(closed.FromM, cut.EnterM), ToM = MathF.Max(closed.ToM, cut.ExitM),
                    };
                }

                spans[span] = closed;
            }

            spans.Sort(static (first, second) => first.FromM.CompareTo(second.FromM));
            var openM = 0f;
            foreach (var (fromM, toM, _) in spans)
            {
                if (fromM > openM) runs.Add(new RoadStretch(openM, MathF.Min(fromM, lengthM[road])));

                openM = MathF.Max(openM, toM);
                if (openM >= lengthM[road]) break;
            }

            if (openM < lengthM[road]) runs.Add(new RoadStretch(openM, lengthM[road]));
            offsets[road + 1] = runs.Count;
        }

        return new CentrelineRuns(offsets, [.. runs]);
    }

    /// <summary>
    /// Which junctions close the ground between their paint and themselves. <b>Every junction that forks</b>,
    /// because the metres behind an arm's paint are ground the movements through the box are driven over —
    /// <b>and the node of one that does not fork, where its own crossing is laid on the disc</b>, which is
    /// what an inline junction carries (TER-6). A lane line is broken by a zebra wherever the zebra stands,
    /// and that one stands past the end of every lane there, so the disc is the only thing that breaks it.
    /// </summary>
    static bool[] ClosesTheGroundBehindItsPaint(CityPlan plan)
    {
        var closes = new bool[plan.Junctions.Count];
        var arms = RoadCuts.ArmsPerJunction(plan);
        for (var junction = 0; junction < closes.Length && junction < arms.Length; junction++)
        {
            closes[junction] = arms[junction] >= 3;
        }

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var junction = plan.Crosswalks.Junction[crossing];
            if (junction < 0 || junction >= closes.Length || closes[junction]) continue;

            var reachM = plan.Junctions.RadiusM[junction] + (plan.Crosswalks.DepthM[crossing] * 0.5f);
            var offM = Vector2.DistanceSquared(plan.Crosswalks.CentreM[crossing], plan.Junctions.CentreM[junction]);
            if (offM <= reachM * reachM) closes[junction] = true;
        }

        return closes;
    }

    /// <summary>
    /// The paint on each road that is not a bar: the crossings struck across it, each carrying the junction
    /// it approaches, and found along the road the way a lot's frontage is.
    /// </summary>
    /// <remarks>
    /// A crossing at a lit junction is inside the span that junction's bar already closes off. What the
    /// junction it carries is for is the one at a junction the ranking governs, where there is no bar and
    /// the crossing is the only paint the throat has (TLT-3); a crossing struck mid-block carries none
    /// (TER-5b) and closes off nothing but its own bars.
    /// </remarks>
    static List<ClosedStretch>?[] Blocked(CityPlan plan, float[] lengthM)
    {
        var spans = new List<ClosedStretch>?[plan.Roads.Count];
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var road = RoadFrontages.Nearest(plan, lengthM, plan.Crosswalks.CentreM[crossing], out var alongM, out _);
            if (road < 0) continue;

            var reachM = plan.Crosswalks.DepthM[crossing] * 0.5f;
            (spans[road] ??= []).Add(
                new ClosedStretch(alongM - reachM, alongM + reachM, plan.Crosswalks.Junction[crossing]));
        }

        return spans;
    }
}
