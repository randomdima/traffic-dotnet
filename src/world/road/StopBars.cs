using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The bars a driver holds at</b> (TER-6): one across every lane arriving at an arm whose walk is cut, a
/// clear half-metre behind the near edge of the band at that station
/// (<see cref="RoadFigures.StopBarSetbackM"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>A bar is placed by the station at its own arm alone</b> and asks nothing of the junction behind it
/// (TER-6, rule 3): where the walk is cut decides where the stop is, and the two derived apart would differ
/// by metres the first time either moved. It is also the whole of which arms carry one — a bay, a
/// roundabout's ring and every node that forks nothing stand no station (<see cref="Crossings"/>) and so
/// carry no bar.
/// </para>
/// <para>
/// <b>The place the walk meets the road, and not the paint</b> (WLK-10a): a street too short to be crossed
/// twice is crossed once in the middle of it, and a bar is where the traffic holds for the box in front of
/// it rather than what stands in front of a zebra. So what this is handed is what each arm holds behind
/// (<c>CityGen.KerbEnds.HeldM</c>) — the station where a zebra stands at it and the end of the road's own
/// kerb where the paint went elsewhere, that one carrying no band, so the setback is taken clear of the kerb
/// end itself.
/// </para>
/// <para>
/// <b>Read off the lane and never off the road</b> (TER-5i): a lane's own metres and its own width, so one
/// rule covers the street driven both ways and the one-way street standing on the half its traffic drives,
/// and an arm the traffic only leaves on takes no bar while still carrying its crossing (TER-4d).
/// </para>
/// </remarks>
internal sealed class StopBars
{
    readonly Vector2[] _centreM;
    readonly Vector2[] _approach;
    readonly float[] _spanM;
    readonly float[] _thicknessM;
    readonly int[] _lane;
    readonly float[] _alongM;

    StopBars(
        Vector2[] centreM, Vector2[] approach, float[] spanM, float[] thicknessM, int[] lane, float[] alongM)
    {
        _centreM = centreM;
        _approach = approach;
        _spanM = spanM;
        _thicknessM = thicknessM;
        _lane = lane;
        _alongM = alongM;
    }

    public int Count => _centreM.Length;

    /// <summary>The middle of the bar, which stands on the line the lane behind it is driven down.</summary>
    public ReadOnlySpan<Vector2> CentreM => _centreM;

    /// <summary>
    /// The way a car crossing it is going, which is what the bar is laid square across: the axis of the band it is held
    /// behind, so it stands parallel to that band's edge (<see cref="Crossings.BehindM"/>) — or its lane's bearing where
    /// it was stood back behind other paint, or holds for something other than a junction.
    /// </summary>
    public ReadOnlySpan<Vector2> Approach => _approach;

    /// <summary>How far it reaches either way, which is the width of the one lane driving at it.</summary>
    public ReadOnlySpan<float> SpanM => _spanM;

    /// <summary>And how thickly it is painted, the one figure of it that is the paint's rather than the road's.</summary>
    public ReadOnlySpan<float> ThicknessM => _thicknessM;

    /// <summary>The lane it is the bar of, for a reader that wants what stops there.</summary>
    public ReadOnlySpan<int> Lane => _lane;

    /// <summary>
    /// Where along that lane the middle of it stands, in the lane's own metres — the figure the bar was
    /// placed by, kept rather than left to be projected back out of <see cref="CentreM"/> (TER-6, rule 1).
    /// </summary>
    public ReadOnlySpan<float> AlongM => _alongM;

    /// <summary>
    /// <b>Bars stood where something other than a junction holds a lane</b> — a level crossing (RAIL-3) — each square across
    /// its lane with its near edge where the hold begins, as a light's begins at its bar's (TER-6).
    /// </summary>
    public static StopBars Across(LaneLines lanes, IReadOnlyList<(int Lane, float HeldFromM)> holds, SimConfig config)
    {
        var thicknessM = config.Road.StopBarThicknessM;
        var count = thicknessM <= 0f ? 0 : holds.Count;
        var (centreM, approach, spanM, laidM, lane, standsM) =
            (new Vector2[count], new Vector2[count], new float[count], new float[count], new int[count], new float[count]);
        for (var each = 0; each < count; each++)
        {
            var (at, heldFromM) = holds[each];
            var alongM = heldFromM + (thicknessM * 0.5f);
            var stop = Spline.SampleAt(lanes.ArcsOf(at), alongM);
            (centreM[each], approach[each], spanM[each], laidM[each], lane[each], standsM[each]) =
                (stop.PositionM, stop.Direction, lanes.LaneWidthM[at], thicknessM, at, alongM);
        }

        return new StopBars(centreM, approach, spanM, laidM, lane, standsM);
    }

    /// <param name="paint">
    /// The bands the town paints, where they are not what <paramref name="crossings"/> holds behind: a bar standing on
    /// one, or within a setback of it, is stood back behind it instead — another road's zebra spanning this one's end,
    /// as where two roads converge on a box side by side, being paint a driver holds short of like any other.
    /// </param>
    public static StopBars Lay(LaneLines lanes, Crossings crossings, SimConfig config, Crossings? paint = null)
    {
        var setbackM = config.Road.StopBarSetbackM;
        var thicknessM = config.Road.StopBarThicknessM;

        var centreM = new List<Vector2>();
        var approach = new List<Vector2>();
        var spanM = new List<float>();
        var laidM = new List<float>();
        var lane = new List<int>();
        var standsM = new List<float>();
        if (thicknessM <= 0f) return new StopBars([], [], [], [], [], []);

        var bands = paint is null ? null : new PaintedBands(paint, thicknessM + (setbackM * 2f));

        // A roadside is driven by nobody, so nobody is held on it (<see cref="LaneLines.IsRoadside"/>).
        for (var at = 0; at < lanes.FirstRoadside; at++)
        {
            // A lane runs from its road's From end to its To end or back down it, so the end it arrives on
            // is which of the two ways round it is (TER-5i).
            var crossing = crossings.At(lanes.LaneRoad[at], lanes.LaneForward[at]);
            if (crossing == Crossings.None) continue;

            // <b>Measured off the crossing's own near edge and not off the lane's end</b> (TER-6, rule 3):
            // the band is a distance down the road and the bar a distance down a lane half a carriageway
            // off it, and over a bend the two run at different rates — a bar placed by subtracting the
            // band's depth from the lane's metres stands centimetres out of the gap it was meant to leave.
            // <b>And laid parallel to that edge, where its lane crosses the line a setback behind it</b>
            // (TER-6, <see cref="Crossings.BehindM"/>): a band is laid square to the walk that placed it (WLK-10),
            // so a bar square to its lane instead stands nearer the band at one end than the other wherever the
            // arm bends, and the bars of one arm's lanes stand out of line. <b>The lane says only which of the
            // two edges the traffic meets first</b>, which is a sign and not a bearing.
            var arcs = lanes.ArcsOf(at);
            var lengthM = lanes.LaneLengthM[at];
            var arriving = Spline.SampleAt(arcs, lengthM).Direction;
            var nearM = Spline.ProjectM(arcs, crossings.CentreM[crossing], lengthM, lengthM);
            var heldM = crossings.BehindM(crossing, arcs, nearM, arriving, setbackM + (thicknessM * 0.5f), out var towards);

            // A bar stood back behind another band is square to its lane there, the edge it was struck off being behind it.
            var alongM = bands is null ? heldM : ClearOf(bands, arcs, lanes.LaneWidthM[at], heldM, setbackM, thicknessM);
            if (!(alongM > 0f)) continue;

            var stop = Spline.SampleAt(arcs, alongM);
            centreM.Add(stop.PositionM);
            approach.Add(alongM == heldM ? towards : stop.Direction);
            spanM.Add(lanes.LaneWidthM[at]);
            laidM.Add(thicknessM);
            lane.Add(at);
            standsM.Add(alongM);
        }

        return new StopBars([.. centreM], [.. approach], [.. spanM], [.. laidM], [.. lane], [.. standsM]);
    }

    /// <summary>
    /// <b>A bar's place stood back behind every painted band it would stand on</b>, or within a setback of: the bar is
    /// stood that setback short of the nearest the band reaches along the lane, and asked again, until it stands clear or
    /// off the lane's start.
    /// </summary>
    static float ClearOf(
        PaintedBands bands, ReadOnlySpan<ArcSeg> arcs, float laneWidthM, float alongM, float setbackM, float thicknessM)
    {
        while (alongM > 0f)
        {
            var stop = Spline.SampleAt(arcs, alongM);
            var held = new OrientedBox(stop.PositionM, stop.Direction, (thicknessM * 0.5f) + setbackM, laneWidthM * 0.5f);
            var crossing = bands.Under(held);
            if (crossing == Crossings.None) return alongM;

            var band = bands.Band(crossing);
            var reachM = (MathF.Abs(Vector2.Dot(band.Along, stop.Direction)) * band.HalfLengthM) +
                         (MathF.Abs(Vector2.Dot(Heading.RightOf(band.Along), stop.Direction)) * band.HalfWidthM);
            var nearestM = Vector2.Dot(band.CentreM - stop.PositionM, stop.Direction) - reachM;
            var backM = alongM + nearestM - setbackM - (thicknessM * 0.5f);
            if (backM >= alongM) return alongM;

            alongM = backM;
        }

        return alongM;
    }
}
