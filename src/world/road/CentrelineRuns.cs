using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The lines the town's lane paint runs down</b> (TER-6): one chain of arcs for each line two lanes of a
/// stretch of carriageway meet along — the line its two ways meet on, and the line between each two lanes
/// running one way — carried on through the junctions a driver going straight past is offered no choice at.
/// </summary>
/// <remarks>
/// <para>
/// <b>A run is not a road.</b> A road ends at every node something meets it at, and a car park is a node cut
/// into a road that already stood (GEN-52) — so a street with three lots down it is four roads and one
/// carriageway. What tells an end from an interruption is whether anything a driver could turn onto stands
/// at the node: a junction whose only other arms are bays is an injection into a street rather than a place
/// the street stops, and the same is true of the two-armed nodes a bridge or a loop left behind.
/// </para>
/// <para>
/// <b>The run is what the dashes are centred on</b>, which is why it is joined here rather than laid in
/// pieces and left to meet: a run of marks is centred on its own stretch, so four pieces would put four
/// phases down one street and a half dash at every lot.
/// </para>
/// <para>
/// <b>The ground between two roads is crossed on the same biarc the movements over it are drawn on</b>
/// (<see cref="LaneLines"/>). The two lanes either side of that ground are that biarc offset half a lane
/// each way, so the line their ribbons meet along is the biarc itself; at a cut junction it is a piece of
/// the road's own arc and nothing is drawn again (GEN-52).
/// </para>
/// </remarks>
internal sealed class CentrelineRuns
{
    readonly int[] _arcOffsets;
    readonly ArcSeg[] _arcs;
    readonly int[] _roadOffsets;
    readonly int[] _roads;
    readonly int[] _ends;

    CentrelineRuns(int[] arcOffsets, ArcSeg[] arcs, int[] roadOffsets, int[] roads, int[] ends)
    {
        _arcOffsets = arcOffsets;
        _arcs = arcs;
        _roadOffsets = roadOffsets;
        _roads = roads;
        _ends = ends;
    }

    public int Count => _arcOffsets.Length - 1;

    /// <summary>One run as the single line it is, joints and all.</summary>
    public ReadOnlySpan<ArcSeg> Of(int run) =>
        _arcs.AsSpan(_arcOffsets[run], _arcOffsets[run + 1] - _arcOffsets[run]);

    /// <summary>And the roads it is painted down, in the order it runs through them.</summary>
    public ReadOnlySpan<int> RoadsOf(int run) =>
        _roads.AsSpan(_roadOffsets[run], _roadOffsets[run + 1] - _roadOffsets[run]);

    /// <summary>
    /// The road end a run sets off from, as <see cref="JunctionArms.End"/> names one: the arm whose paint
    /// the line stops short of at nought metres (TER-6).
    /// </summary>
    public int HeadEnd(int run) => _ends[run * 2];

    /// <summary>And the one it arrives at, which is the arm its far end stops short of.</summary>
    public int TailEnd(int run) => _ends[(run * 2) + 1];

    /// <summary>
    /// The stretch of one run the line is painted down: the whole of it, less what the arm at either end
    /// has paint of its own on — the crossing's band, the clear road behind it and the bar. An arm carrying
    /// no crossing takes nothing off, the line running to the junction it ends at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The line stops behind the outermost of that paint and not at the near edge of it</b> (TER-6): a
    /// dash inside the band shows through the stripes' gaps, and one in the clear road reaches the bar a
    /// driver holds at.
    /// </para>
    /// <para>
    /// <b>Measured off the band that is there and not off a figure for where one would be.</b> Where a walk
    /// meets a road is the kerb ends' to say (<see cref="KerbEnds"/>), and it is cut off the end of the road's
    /// kerb rather than off the end of its lane — which is further back, and further by however early the
    /// mouth began to widen. A trim taken at a fixed figure would leave the dashes running into the bar on
    /// exactly the arms whose mouths open soonest.
    /// </para>
    /// <para>
    /// <b>Asked of what each arm holds behind and not of the paint</b> (<see cref="KerbEnds.HeldM"/>): what a
    /// line stops short of is the bar, and on a road crossed midway the bar and the zebra are not in the same
    /// place (WLK-10a). A trim read off that road's paint would be taken from both ends of a run towards a
    /// band in the middle of it, which is no line at all — and would leave the dashes running into the bars
    /// it was for. <b>The half-band is the band's own</b> and not the figure every band would have: where an
    /// arm holds behind its kerb end there is no paint there and no half to take.
    /// </para>
    /// </remarks>
    public (float FromM, float ToM) PaintedM(int run, Crossings crossings, SimConfig config)
    {
        var arcs = Of(run);
        var lengthM = Spline.TotalLengthM(arcs);

        // How far behind the band's own middle the far edge of the bar stands, less the half-band the band
        // itself is: a place carrying no paint carries no depth either (WLK-10a), and the bar behind it
        // stands that much nearer the junction.
        var behindM = config.Road.StopBarSetbackM + config.Road.StopBarThicknessM;

        var fromM = Clear(arcs, lengthM, crossings, HeadEnd(run), behindM, 0f);
        var toM = Clear(arcs, lengthM, crossings, TailEnd(run), -behindM, lengthM);
        return (fromM, MathF.Max(fromM, toM));
    }

    /// <summary>
    /// Where the paint at one arm leaves off along the run, read off the crossing standing there: its own
    /// place on the line, taken <paramref name="behindM"/> further in. An arm with no crossing carries no
    /// bar either (<see cref="StopBars"/>) and leaves the line its whole run.
    /// </summary>
    static float Clear(
        ReadOnlySpan<ArcSeg> arcs, float lengthM, Crossings crossings, int end, float behindM, float wholeM)
    {
        var at = crossings.At(Road(end), JunctionArms.AtTo(end));
        if (at == Crossings.None) return wholeM;

        var atM = Spline.ProjectM(arcs, crossings.CentreM[at], lengthM * 0.5f, lengthM);

        // The band's own half, taken the way the bar was: which end of the run this is decides the sign of
        // both, and a place with no paint at it has no half to take.
        var halfBandM = MathF.CopySign(crossings.DepthM[at] * 0.5f, behindM);
        return Math.Clamp(atM + behindM + halfBandM, 0f, lengthM);
    }

    /// <summary>
    /// <b>Every line the town's lane paint runs down</b>: one between each pair of lanes that touch, the line
    /// the two ways meet on among them, each carried on through a junction as far as the carriageway beyond
    /// is laid the same — the same lanes each way, so the line stands at the same place across it.
    /// </summary>
    /// <remarks>
    /// <b>A line is counted in from the kerb the run's own traffic keeps to</b>, so a run walked against one
    /// of its roads finds its line from the other kerb of that road and the two readings are one line.
    /// </remarks>
    public static CentrelineRuns Lay(CityPlan plan, SimConfig config)
    {
        var roads = plan.Roads;
        var arcOffsets = new List<int> { 0 };
        var arcs = new List<ArcSeg>();
        var roadOffsets = new List<int> { 0 };
        var run = new List<int>();
        var ends = new List<int>();
        if (roads.Count == 0) return new CentrelineRuns([.. arcOffsets], [], [.. roadOffsets], [], []);

        var arms = JunctionArms.Of(plan);
        var firstLine = new int[roads.Count + 1];
        var longest = 0;
        for (var road = 0; road < roads.Count; road++)
        {
            longest = Math.Max(longest, roads.SegmentsOf(road).Length);
            firstLine[road + 1] = firstLine[road] + LinesOn(roads, road);
        }

        var laid = new bool[firstLine[^1]];
        var reversed = new ArcSeg[longest];
        var offset = new ArcSeg[longest];
        for (var road = 0; road < roads.Count; road++)
        {
            for (var line = 1; line <= LinesOn(roads, road); line++)
            {
                if (laid[firstLine[road] + line - 1]) continue;

                // Back to the head of the run first, so the line is laid in one direction from one end of the
                // carriageway — a run walked forward from wherever this loop happened to reach it would be two
                // runs meeting at that road, phased against each other at the joint.
                var head = road;
                var headForward = true;
                while (true)
                {
                    var back = Across(arms, roads, head, !headForward);
                    if (back == JunctionArms.NoEnd || Road(back) == road) break;

                    head = Road(back);
                    headForward = JunctionArms.AtTo(back);
                }

                // The run is walked the way this road runs, so it counts the line as the road does.
                var fromKerb = line;
                var at = head;
                var forward = headForward;
                ends.Add(End(head, atTo: !headForward));
                while (true)
                {
                    var own = forward ? fromKerb : LinesOn(roads, at) + 1 - fromKerb;
                    laid[firstLine[at] + own - 1] = true;
                    run.Add(at);

                    var arcsOf = roads.SegmentsOf(at);
                    if (!forward)
                    {
                        Spline.ReverseInto(arcsOf, reversed);
                        arcsOf = reversed.AsSpan(0, arcsOf.Length);
                    }

                    Spline.OffsetInto(arcsOf, roads.LineBetweenLanesM(at, fromKerb) * config.RoadSideSign, offset);
                    var drawn = offset.AsSpan(0, arcsOf.Length);

                    if (arcs.Count > arcOffsets[^1]) Joined(arcs, drawn[0]);
                    foreach (var arc in drawn) arcs.Add(arc);

                    var on = Across(arms, roads, at, forward);
                    if (on == JunctionArms.NoEnd) break;

                    var next = Road(on);
                    var nextForward = !JunctionArms.AtTo(on);
                    var nextOwn = nextForward ? fromKerb : LinesOn(roads, next) + 1 - fromKerb;
                    if (laid[firstLine[next] + nextOwn - 1]) break;

                    at = next;
                    forward = nextForward;
                }

                ends.Add(End(at, atTo: forward));
                arcOffsets.Add(arcs.Count);
                roadOffsets.Add(run.Count);
            }
        }

        return new CentrelineRuns([.. arcOffsets], [.. arcs], [.. roadOffsets], [.. run], [.. ends]);
    }

    /// <summary>
    /// How many lines are painted down a road: one between each pair of its lanes that touch, and none where it
    /// is one lane, driven both ways over one line, or not laid at all.
    /// </summary>
    static int LinesOn(CityPlan.RoadArrays roads, int road) =>
        roads.SegmentsOf(road).Length == 0 ? 0 : roads.LinesBetweenLanes(road);

    /// <summary>The lanes one road is driven in, ahead of a walk along it and behind, as the walk counts them.</summary>
    static (int Ahead, int Behind) Ways(CityPlan.RoadArrays roads, int road, bool forward) =>
        forward
            ? (roads.LanesWithTheRoad(road), roads.LanesAgainstTheRoad(road))
            : (roads.LanesAgainstTheRoad(road), roads.LanesWithTheRoad(road));

    /// <inheritdoc cref="JunctionArms.End"/>
    static int End(int road, bool atTo) => JunctionArms.End(road, atTo);

    /// <inheritdoc cref="JunctionArms.Road"/>
    static int Road(int end) => JunctionArms.Road(end);

    /// <summary>The junction one end of a road stands at.</summary>
    static int Arrives(CityPlan.RoadArrays roads, int road, bool atTo) =>
        atTo ? roads.ToJunction[road] : roads.FromJunction[road];

    /// <summary>
    /// The end the carriageway carries on out of a junction as, walking a road toward that end, or
    /// <see cref="JunctionArms.NoEnd"/> where it stops there: at a junction anything else meets
    /// (<see cref="JunctionArms.Across"/>), and where what carries on is not laid the same — not the same lanes
    /// each way at the same width, so no line of one stands where a line of the other does.
    /// </summary>
    static int Across(JunctionArms arms, CityPlan.RoadArrays roads, int road, bool forward)
    {
        var across = arms.Across(Arrives(roads, road, forward), End(road, forward));
        if (across == JunctionArms.NoEnd || LinesOn(roads, Road(across)) == 0) return JunctionArms.NoEnd;

        var next = Road(across);
        var nextForward = !JunctionArms.AtTo(across);
        return Ways(roads, road, forward) == Ways(roads, next, nextForward)
               && roads.LaneWidthM(road) == roads.LaneWidthM(next)
            ? across
            : JunctionArms.NoEnd;
    }

    /// <summary>
    /// The junction's own ground, crossed: the biarc between the two poses the roads either side of it hand
    /// over at, which is what every movement over that ground is drawn on and, at a cut junction, the road's
    /// own arc carried through (GEN-52).
    /// </summary>
    static void Joined(List<ArcSeg> into, in ArcSeg onto)
    {
        var last = into[^1];
        if (Vector2.DistanceSquared(last.EndM, onto.StartM) <= LineTolerance.RoundingM * LineTolerance.RoundingM)
        {
            return;
        }

        Span<ArcSeg> across = stackalloc ArcSeg[2];
        var pieces = Spline.BiarcInto(
            last.EndM, last.HeadingAtRad(last.LengthM), onto.StartM, onto.HeadingRad, across);
        for (var piece = 0; piece < pieces; piece++) into.Add(across[piece]);
    }
}
