using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>One place a car park asks its road to be cut: how far along the road's own centreline, and which node it becomes.</summary>
internal readonly record struct SectionCut(float AlongM, int Node);

/// <summary>
/// <b>Where a road is cut for the car parks hanging off it</b> (GEN-4h) — a node at each end of every
/// parking section, so the frontage a lot's bays are reached over is a stretch of the network in its own
/// right and a leg aimed at one of those bays is routed to a node like every other leg.
/// </summary>
/// <remarks>
/// <para>
/// <b>A section is a stretch and is therefore bounded by two nodes.</b> The bays of one lot stand along
/// tens of metres of kerb and are reached from both directions, so there is no single point on the road
/// that all of them lie ahead of; the pair of cuts is what gives every bay of the section a lane that
/// arrives at a node with the whole frontage still in front of it. Set back from the frontage by the
/// run-in the entry template wants, so the last dozen metres before a bay are the section's own ground and
/// not the street before it.
/// </para>
/// <para>
/// <b>Lots merge into one section when their setbacks touch.</b> Two lots facing each other across a
/// carriageway are one section by construction — they project onto the same metres of the same
/// centreline — and so are two lots sharing a kerb closer together than the run-in, which could not have
/// two nodes between them anyway.
/// </para>
/// <para>
/// <b>It is asked of the plan and not of the graph.</b> Where a road is cut is what makes a lane, so
/// nothing here may read a lane; a lot's frontage is its rectangle projected onto the road's own
/// centreline, which is the same measure <see cref="RoadCuts"/> takes the junction discs against.
/// </para>
/// </remarks>
internal sealed class ParkingSections
{
    static readonly SectionCut[] None = [];

    readonly int[] _offsets;
    readonly SectionCut[] _cuts;

    ParkingSections(int[] offsets, SectionCut[] cuts, Vector2[] centreM)
    {
        _offsets = offsets;
        _cuts = cuts;
        CentreM = centreM;
    }

    /// <summary>Where each of the nodes stands, in the order they were numbered.</summary>
    public Vector2[] CentreM { get; }

    public int NodeCount => CentreM.Length;

    /// <summary>The cuts one road carries, in the order they stand along it.</summary>
    public ReadOnlySpan<SectionCut> On(int road) =>
        _offsets.Length == 0 ? None : _cuts.AsSpan(_offsets[road], _offsets[road + 1] - _offsets[road]);

    public static ParkingSections Lay(CityPlan plan, SimConfig config, int firstNode)
    {
        var lots = plan.ParkingLots;
        var roads = plan.Roads;
        if (lots.Count == 0 || roads.Count == 0) return new ParkingSections([], None, []);

        var setbackM = config.ParkingSectionSetbackM;
        var frontage = new List<(float FromM, float ToM)>[roads.Count];
        var lengthM = RoadFrontages.RoadLengthsM(plan);

        // Near enough to the road to be reached off it, measured against the lot's own diagonal so the
        // test does not turn on which way round the rectangle was laid.
        foreach (var front in RoadFrontages.Lay(plan, config).All)
        {
            var reachM = (roads.WidthM[front.Road] * 0.5f) + config.Road.PavementWidthM +
                         lots.HalfExtentM[front.Lot].Length();
            if (front.OffM > reachM) continue;

            (frontage[front.Road] ??= []).Add((front.FromM - setbackM, front.ToM + setbackM));
        }

        var shortestM = config.ParkingSectionShortestStretchM;
        var blocked = Blocked(plan, config, lengthM, shortestM);
        var offsets = new int[roads.Count + 1];
        var cuts = new List<SectionCut>();
        var centreM = new List<Vector2>();
        for (var road = 0; road < roads.Count; road++)
        {
            offsets[road + 1] = offsets[road];
            var spans = frontage[road];
            if (spans is null) continue;

            spans.Sort(static (first, second) => first.FromM.CompareTo(second.FromM));
            var openM = spans[0].FromM;
            var closeM = spans[0].ToM;
            for (var span = 1; span <= spans.Count; span++)
            {
                if (span < spans.Count && spans[span].FromM <= closeM)
                {
                    closeM = MathF.Max(closeM, spans[span].ToM);
                    continue;
                }

                Cut(road, openM, outwardIsBack: true);
                Cut(road, closeM, outwardIsBack: false);
                if (span < spans.Count) (openM, closeM) = spans[span];
            }

            offsets[road + 1] = cuts.Count;
        }

        return new ParkingSections(offsets, [.. cuts], [.. centreM]);

        // <b>A cut that cannot stand where it was asked for moves away from its own frontage and never into
        // it</b>: the run-in every bay's way in wants is the metres between the cut and the first bay, so a
        // cut walked inwards to make room is a bay with nowhere to turn in from. Where moving it runs off
        // the end of the road, the section goes without and keeps the node its road already ends at.
        void Cut(int road, float alongM, bool outwardIsBack)
        {
            alongM = Clear(blocked[road], alongM, outwardIsBack);
            if (alongM <= 0f || alongM >= lengthM[road]) return;

            cuts.Add(new SectionCut(alongM, firstNode + centreM.Count));
            centreM.Add(Spline.SampleAt(plan.Roads.SegmentsOf(road), alongM).PositionM);
        }
    }

    /// <summary>
    /// The nearest metre to <paramref name="alongM"/> in the one direction allowed that lies on none of the
    /// stretches a cut may not stand on.
    /// </summary>
    /// <remarks>
    /// It terminates because every step lands on a boundary of one of them and each is passed at most once,
    /// which the round count is the guard for rather than the reason.
    /// </remarks>
    static float Clear(List<(float FromM, float ToM)>? blocked, float alongM, bool outwardIsBack)
    {
        if (blocked is null) return alongM;

        for (var round = 0; round <= blocked.Count; round++)
        {
            var moved = false;
            foreach (var (fromM, toM) in blocked)
            {
                if (alongM <= fromM || alongM >= toM) continue;

                alongM = outwardIsBack ? fromM : toM;
                moved = true;
            }

            if (!moved) return alongM;
        }

        return alongM;
    }

    /// <summary>
    /// The metres of each road no cut may stand on: the ground its junctions already take, the zebras
    /// struck across it and the bars painted on its approaches, each opened out by the shortest stretch a
    /// cut has to leave standing.
    /// </summary>
    /// <remarks>
    /// <b>A crossing adds no node</b> (<see cref="RoadGraph"/>) and is therefore a band of a lane rather
    /// than a boundary of one — so a lane end laid inside one splits the approach from the paint, and the
    /// braking distance a driver has to see the crossing over is on the lane behind the one it is on. A
    /// junction is the opposite case and the same answer: it is a node already, and a second one inside its
    /// disc would be a stretch of no length between them.
    /// </remarks>
    static List<(float FromM, float ToM)>[] Blocked(CityPlan plan, SimConfig config, float[] lengthM, float shortestM)
    {
        var roads = plan.Roads;
        var painted = new List<(float FromM, float ToM)>[roads.Count];

        var discs = RoadCuts.JunctionIndex(plan, paddingM: 0f);
        var junctionCuts = new List<RoadCut>();
        for (var road = 0; road < roads.Count; road++)
        {
            var centreline = roads.SegmentsOf(road);
            if (centreline.Length == 0) continue;

            RoadCuts.Along(
                plan, discs, centreline, lengthM[road], paddingM: 0f, roads.FromJunction[road],
                roads.ToJunction[road], junctionCuts);
            foreach (var cut in junctionCuts)
            {
                (painted[road] ??= []).Add((cut.EnterM - shortestM, cut.ExitM + shortestM));
            }
        }

        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            Note(plan.Crosswalks.CentreM[crossing], plan.Crosswalks.DepthM[crossing] * 0.5f);
        }

        for (var bar = 0; bar < plan.StopLines.Count; bar++)
        {
            var road = plan.StopLines.Road[bar];
            if (road < 0 || road >= roads.Count) continue;

            var centreline = roads.SegmentsOf(road);
            if (centreline.Length == 0) continue;

            var atM = Spline.ProjectM(centreline, plan.StopLines.CentreM[bar], lengthM[road] * 0.5f, lengthM[road]);
            Keep(road, atM, plan.StopLines.ThicknessM[bar] * 0.5f);
        }

        return painted;

        void Note(Vector2 atM, float reachM)
        {
            var road = RoadFrontages.Nearest(plan, lengthM, atM, out var alongM, out _);
            if (road < 0) return;

            Keep(road, alongM, reachM);
        }

        void Keep(int road, float alongM, float reachM) =>
            (painted[road] ??= []).Add((alongM - reachM - shortestM, alongM + reachM + shortestM));
    }
}
