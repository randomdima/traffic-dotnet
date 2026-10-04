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

    /// <summary>The way a car crossing it is going, which is what the bar is laid square across.</summary>
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

    public static StopBars Lay(LaneLines lanes, Crossings crossings, SimConfig config)
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
            // <b>And struck along the band's own axis rather than along the lane</b> (TER-6): a band is laid
            // square to the walk that placed it (WLK-10), so which way it is deep is its own fact and holds
            // wherever along the arm it ended up standing. <b>The lane says only which of the two edges the
            // traffic meets first</b>, which is a sign and not a bearing — so reading it at the lane's end
            // costs nothing even where the arm bends between there and the paint.
            var arcs = lanes.ArcsOf(at);
            var lengthM = lanes.LaneLengthM[at];
            var axis = crossings.Axis[crossing];
            var arriving = Spline.SampleAt(arcs, lengthM).Direction;
            var towards = Vector2.Dot(axis, arriving) >= 0f ? axis : -axis;
            var bandM = crossings.CentreM[crossing] - (towards * crossings.DepthM[crossing] * 0.5f);

            var alongM = Spline.ProjectM(arcs, bandM, lengthM, lengthM) - setbackM - (thicknessM * 0.5f);
            if (alongM <= 0f) continue;

            var stop = Spline.SampleAt(arcs, alongM);
            centreM.Add(stop.PositionM);
            approach.Add(stop.Direction);
            spanM.Add(lanes.LaneWidthM[at]);
            laidM.Add(thicknessM);
            lane.Add(at);
            standsM.Add(alongM);
        }

        return new StopBars([.. centreM], [.. approach], [.. spanM], [.. laidM], [.. lane], [.. standsM]);
    }
}
