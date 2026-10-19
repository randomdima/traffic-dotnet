using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Rail;

/// <summary>
/// <b>What a level crossing shows</b> (RAIL-7): on each approach a barrier pivoting at the kerb on the traffic's right and
/// lowered across the lanes coming in, and a head beside it whose two lamps flash in turn — and when each crossing last
/// closed and opened, which is all its gear is drawn off.
/// </summary>
/// <remarks>
/// <b>Closed as its holds are</b>: by any train out whose front is a warning time short of one of its meetings, until its
/// last wagon has cleared it (<see cref="TrackRun.Closes"/>), the meeting reaching across the track as far as the road and
/// its walk do. A crossing's gear is laid once off its road, a pair of approaches each way it is driven.
/// </remarks>
internal sealed class CrossingGear
{
    readonly LevelCrossings _crossings;
    readonly CityPlan _plan;
    readonly RailFigures _rail;

    /// <summary>Each crossing's last closing and opening, in town seconds; NaN until it first closes.</summary>
    readonly float[] _closedAtS, _openedAtS;

    readonly bool[] _closed;

    public static CrossingGear None(CityPlan plan, SimConfig config) => new(plan, LevelCrossings.None, config.Rail, [], [], [], [], [], []);

    CrossingGear(
        CityPlan plan, LevelCrossings crossings, RailFigures rail, int[] crossing, Vector2[] pivotM, Vector2[] downAlong,
        float[] armM, Vector2[] headM, float[] headHeadingRad)
    {
        (_plan, _crossings, _rail) = (plan, crossings, rail);
        (Crossing, PivotM, DownAlong, ArmM, HeadM, HeadHeadingRad) = (crossing, pivotM, downAlong, armM, headM, headHeadingRad);
        ArmHeadingRad = new float[crossing.Length];
        LampAcrossM = new Vector2[crossing.Length];
        for (var each = 0; each < crossing.Length; each++)
        {
            ArmHeadingRad[each] = MathF.Atan2(downAlong[each].Y, downAlong[each].X);
            LampAcrossM[each] = Heading.RightOf(Heading.Unit(headHeadingRad[each])) * (rail.LampsApartM * 0.5f);
        }
        _closedAtS = new float[crossings.Crossings];
        _openedAtS = new float[crossings.Crossings];
        _closed = new bool[crossings.Crossings];
        Array.Fill(_closedAtS, float.NaN);
        Array.Fill(_openedAtS, float.NaN);
    }

    /// <summary>The crossing each approach's gear is of.</summary>
    public int[] Crossing { get; }

    /// <summary>Where each barrier pivots, the way it points lowered — across the lanes it closes — and how long it is.</summary>
    public Vector2[] PivotM { get; }

    public Vector2[] DownAlong { get; }

    public float[] ArmM { get; }

    /// <summary>Where each head stands, and the way it faces: toward the traffic it is shown to.</summary>
    public Vector2[] HeadM { get; }

    public float[] HeadHeadingRad { get; }

    /// <summary>The heading a lowered barrier lies on, and where a head's second lamp stands off its middle — the first the other way.</summary>
    public float[] ArmHeadingRad { get; }

    public Vector2[] LampAcrossM { get; }

    public int Count => Crossing.Length;

    public static CrossingGear Of(CityPlan plan, LevelCrossings crossings, SimConfig config)
    {
        var rail = config.Rail;
        var (crossing, pivotM, downAlong, armM, headM, headingRad) =
            (new List<int>(), new List<Vector2>(), new List<Vector2>(), new List<float>(), new List<Vector2>(), new List<float>());
        for (var each = 0; each < crossings.Crossings; each++)
        {
            var road = crossings.OfRoad[each];
            var arcs = plan.Roads.SegmentsOf(road);
            var halfWidthM = plan.Roads.WidthM[road] * 0.5f;
            var flow = plan.Roads.Flow[road];
            var oneWay = flow != RoadFlow.BothWays;
            foreach (var withTheRoad in (ReadOnlySpan<bool>)[true, false])
            {
                if ((withTheRoad && flow == RoadFlow.AgainstTheRoad) || (!withTheRoad && flow == RoadFlow.WithTheRoad)) continue;

                // Halfway between the stop line and the ballast, so the barrier stands past the paint a car waits at.
                var gearM = rail.StopLineSetbackM * 0.5f;
                var stopM = withTheRoad ? crossings.FromRoadM[each] - gearM : crossings.ToRoadM[each] + gearM;
                var at = Spline.SampleAt(arcs, stopM);
                var travel = withTheRoad ? at.Direction : -at.Direction;
                var right = Heading.RightOf(travel);
                var kerbM = at.PositionM + (right * halfWidthM);
                crossing.Add(each);
                pivotM.Add(kerbM + (right * (rail.GearPastKerbM * 0.5f)));
                downAlong.Add(-right);
                armM.Add((oneWay ? 2f * halfWidthM : halfWidthM) + (rail.GearPastKerbM * 0.5f));
                headM.Add(kerbM + (right * rail.GearPastKerbM) - (travel * rail.HeadSizeM));
                headingRad.Add(MathF.Atan2(-travel.Y, -travel.X));
            }
        }

        return new CrossingGear(plan, crossings, rail, [.. crossing], [.. pivotM], [.. downAlong], [.. armM], [.. headM], [.. headingRad]);
    }

    /// <summary>
    /// <b>Every crossing closed or opened as the trains now stand</b> (RAIL-1): its closing or its opening time noted as it
    /// turns. Asked every tick, so what is drawn turns on the tick the holds' rule says it does.
    /// </summary>
    public void Update(ReadOnlySpan<TrackRun> runs, float nowS)
    {
        for (var each = 0; each < _closed.Length; each++)
        {
            var closed = Closes(each, runs);
            if (closed == _closed[each]) continue;

            _closed[each] = closed;
            if (closed) _closedAtS[each] = nowS;
            else _openedAtS[each] = nowS;
        }
    }

    bool Closes(int crossing, ReadOnlySpan<TrackRun> runs)
    {
        for (var meeting = _crossings.MeetingOffsets[crossing]; meeting < _crossings.MeetingOffsets[crossing + 1]; meeting++)
        {
            var reachM = _crossings.HalfOverM(_plan, meeting);
            foreach (ref readonly var run in runs)
            {
                if (run.Track == _crossings.Track[meeting] && run.Closes(_crossings.AlongTrackM[meeting], reachM, _rail, laterS: 0f)) return true;
            }
        }

        return false;
    }

    /// <summary>Whether a crossing is closed now.</summary>
    public bool Closed(int crossing) => _closed[crossing];

    /// <summary>
    /// <b>How far down an approach's barrier is</b>, nought raised to one across the road (RAIL-7): lowered over the barrier's
    /// time from the moment its crossing closed, and raised over the same from the moment it opened.
    /// </summary>
    public float Lowered(int gear, float nowS)
    {
        var crossing = Crossing[gear];
        if (_closed[crossing]) return Math.Clamp((nowS - _closedAtS[crossing]) / _rail.BarrierS, 0f, 1f);

        return float.IsNaN(_openedAtS[crossing]) ? 0f : 1f - Math.Clamp((nowS - _openedAtS[crossing]) / _rail.BarrierS, 0f, 1f);
    }

    /// <summary>Which of an approach's two lamps is lit: −1 for neither, an open crossing's; 0 and 1 in turn, a closed one's.</summary>
    public int LampLit(int gear, float nowS)
    {
        var crossing = Crossing[gear];
        if (!_closed[crossing]) return -1;

        return ((int)MathF.Floor((nowS - _closedAtS[crossing]) * 2f / _rail.FlashS) & 1) == 0 ? 0 : 1;
    }
}
