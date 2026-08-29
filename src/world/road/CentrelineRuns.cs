using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>A stretch of one road, in that road's own metres.</summary>
internal readonly record struct RoadStretch(float FromM, float ToM);

/// <summary>
/// <b>The stretches of each road its centreline is dashed over</b>: what is left once the junctions the
/// road runs through are taken off it, and the crossings struck across it, and the ground between a stop
/// bar and the junction that bar is painted for.
/// </summary>
/// <remarks>
/// <para>
/// <b>A dash marks the middle of a carriageway, and none of those is one.</b> The metres between a bar and
/// the disc behind it are ground the turning movements are driven over rather than a lane, so a dashed
/// line laid down them draws a lane running into the box — which is what the markings rule forbids when it
/// says a centreline stops before a junction. A dash over a zebra is the same paint twice and shows as a
/// bright streak down the bars.
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

    public static CentrelineRuns Lay(CityPlan plan)
    {
        var roads = plan.Roads;
        if (roads.Count == 0) return new CentrelineRuns([], None);

        var lengthM = RoadFrontages.RoadLengthsM(plan);
        var blocked = Blocked(plan, lengthM);
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
            foreach (var cut in cuts) spans.Add((cut.EnterM, cut.ExitM));

            // A bar carries the junction it was painted for, so the ground it closes off reaches from the
            // bar itself to the far side of that disc — one span, and the zebra between the two is inside it.
            for (var bar = 0; bar < plan.StopLines.Count; bar++)
            {
                if (plan.StopLines.Road[bar] != road) continue;

                var atM = Spline.ProjectM(centreline, plan.StopLines.CentreM[bar], lengthM[road] * 0.5f, lengthM[road]);
                var halfM = plan.StopLines.ThicknessM[bar] * 0.5f;
                var (fromM, toM) = (atM - halfM, atM + halfM);
                foreach (var cut in cuts)
                {
                    if (cut.Junction != plan.StopLines.Junction[bar]) continue;

                    (fromM, toM) = (MathF.Min(fromM, cut.EnterM), MathF.Max(toM, cut.ExitM));
                }

                spans.Add((fromM, toM));
            }

            spans.Sort(static (first, second) => first.FromM.CompareTo(second.FromM));
            var openM = 0f;
            foreach (var (fromM, toM) in spans)
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
    /// The paint on each road that is not a bar: the crossings struck across it, which carry no road of
    /// their own and are found the way a lot's frontage is.
    /// </summary>
    /// <remarks>
    /// A crossing on a junction's arm is inside the span its bar already closes off; this is what covers
    /// the one struck mid-block, which has no bar and no junction (TER-5b).
    /// </remarks>
    static List<(float FromM, float ToM)>?[] Blocked(CityPlan plan, float[] lengthM)
    {
        var spans = new List<(float FromM, float ToM)>?[plan.Roads.Count];
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var road = RoadFrontages.Nearest(plan, lengthM, plan.Crosswalks.CentreM[crossing], out var alongM, out _);
            if (road < 0) continue;

            var reachM = plan.Crosswalks.DepthM[crossing] * 0.5f;
            (spans[road] ??= []).Add((alongM - reachM, alongM + reachM));
        }

        return spans;
    }
}
