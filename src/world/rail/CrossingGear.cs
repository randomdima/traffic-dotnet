using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Rail;

/// <summary>
/// <b>A crossing's X light over one lane coming in</b> (RAIL-7), drawn flat across it as a junction's head is
/// (<c>SignalHeads</c>), its x along the driver's right: midway between the lane's stop bar and the barrier it meets.
/// </summary>
internal readonly record struct CrossingLight(Vector2 CentreM, float HeadingRad, int Approach)
{
    public static CrossingLight Over(ReadOnlySpan<ArcSeg> line, CrossingStretch stretch, SimConfig config)
    {
        var alongM = (stretch.FromM + config.Road.StopBarThicknessM + stretch.BarrierM - (config.Rail.BarrierWidthM * 0.5f)) * 0.5f;
        var at = Spline.SampleAt(line, alongM);
        var lamps = Heading.RightOf(at.Direction);
        return new CrossingLight(at.PositionM, Trig.Atan2(lamps.Y, lamps.X), stretch.Approach);
    }
}

/// <summary>
/// <b>What a level crossing shows, and the arm that stops a car</b> (RAIL-7, RAIL-10): on each approach a barrier pivoting
/// at its drive on the walk at the traffic's right and lowered across the lanes coming in, an X light over each of those
/// lanes that flashes while it is closed — and when each crossing last closed and opened, which is all its gear is moved off.
/// </summary>
/// <remarks>
/// <para>
/// <b>Closed as its holds are</b>: by any train out whose front is a warning time short of one of its meetings, until its
/// last wagon has cleared it (<see cref="TrackRun.Closes"/>), the meeting reaching across the track as far as the road and
/// its walk do. A crossing's gear is laid once off its road, an approach each way it is driven.
/// </para>
/// <para>
/// <b>The arm stands square across the road</b>, as near the rails as a clearance allows (<see cref="LevelCrossings.Barrier"/>),
/// and swings between lying across the lanes and <see cref="RailFigures.BarrierRaisedDeg"/> up. <b>It is a body of its
/// own</b>, the whole arm lying down, in the world while it is anything but all the way up.
/// </para>
/// </remarks>
internal sealed class CrossingGear
{
    readonly LevelCrossings _crossings;
    readonly CityPlan _plan;
    readonly RailFigures _rail;

    /// <summary>Each crossing's last closing and opening, in town seconds; NaN until it first closes.</summary>
    readonly float[] _closedAtS, _openedAtS;

    /// <summary>How far down its arms were when each crossing last turned, so one turned mid-swing swings on from there.</summary>
    readonly float[] _loweredAtTurn;

    readonly bool[] _closed;

    public static CrossingGear None(CityPlan plan, SimConfig config) => new(plan, LevelCrossings.None, config.Rail, [], [], [], [], []);

    CrossingGear(
        CityPlan plan, LevelCrossings crossings, RailFigures rail, int[] crossing, Vector2[] pivotM, Vector2[] downAlong, float[] armM,
        CrossingLight[] lights)
    {
        (_plan, _crossings, _rail) = (plan, crossings, rail);
        (Crossing, PivotM, DownAlong, ArmM, Lights) = (crossing, pivotM, downAlong, armM, lights);
        ArmHeadingRad = new float[crossing.Length];
        for (var each = 0; each < crossing.Length; each++) ArmHeadingRad[each] = Trig.Atan2(downAlong[each].Y, downAlong[each].X);

        _closedAtS = new float[crossings.Crossings];
        _openedAtS = new float[crossings.Crossings];
        _loweredAtTurn = new float[crossings.Crossings];
        _closed = new bool[crossings.Crossings];
        Array.Fill(_closedAtS, float.NaN);
        Array.Fill(_openedAtS, float.NaN);
    }

    /// <summary>The crossing each approach's gear is of.</summary>
    public int[] Crossing { get; }

    /// <summary>
    /// Where each arm pivots — the road's end of its drive — the way it lies lowered, across the lanes it closes, and how
    /// long it is: from its pivot to the road's middle, or to its far kerb on a road driven one way.
    /// </summary>
    public Vector2[] PivotM { get; }

    public Vector2[] DownAlong { get; }

    public float[] ArmM { get; }

    /// <summary>The heading an arm lies on.</summary>
    public float[] ArmHeadingRad { get; }

    /// <summary>Every X light, its <see cref="CrossingLight.Approach"/> the gear it shows, as an index of this.</summary>
    public CrossingLight[] Lights { get; }

    public int Count => Crossing.Length;

    /// <param name="lights">The lights over the lanes coming in (<see cref="CrossingHolds.Lights"/>), each of an approach of <paramref name="crossings"/>.</param>
    public static CrossingGear Of(CityPlan plan, LevelCrossings crossings, ReadOnlySpan<CrossingLight> lights, SimConfig config)
    {
        var rail = config.Rail;
        var looks = RailLooks.Shared;
        if (MathF.Abs(looks.Barrier.FootprintM.Y - rail.BarrierWidthM) > 1e-3f)
        {
            throw new InvalidDataException($"{looks.Barrier.Id} is drawn {looks.Barrier.FootprintM.Y} m wide, and a barrier's body is {rail.BarrierWidthM} m.");
        }

        var driveM = looks.Drive.FootprintM;
        var gearOf = new int[2 * crossings.Crossings];
        Array.Fill(gearOf, -1);
        var (crossing, pivotM, downAlong, armM) = (new List<int>(), new List<Vector2>(), new List<Vector2>(), new List<float>());
        for (var each = 0; each < crossings.Crossings; each++)
        {
            var road = crossings.OfRoad[each];
            var halfWidthM = plan.Roads.WidthM[road] * 0.5f;
            var flow = plan.Roads.Flow[road];
            foreach (var withTheRoad in (ReadOnlySpan<bool>)[true, false])
            {
                if ((withTheRoad && flow == RoadFlow.AgainstTheRoad) || (!withTheRoad && flow == RoadFlow.WithTheRoad)) continue;

                var edge = crossings.Barrier(each, withTheRoad);
                var pivot = edge.AcrossM(halfWidthM + rail.GearPastKerbM) - (edge.TowardTheKerb * (driveM.X * 0.5f));
                gearOf[LevelCrossings.Approach(each, withTheRoad)] = crossing.Count;
                crossing.Add(each);
                pivotM.Add(pivot);
                downAlong.Add(-edge.TowardTheKerb);
                armM.Add(Vector2.Distance(pivot, edge.AcrossM(flow == RoadFlow.BothWays ? 0f : -halfWidthM)));
            }
        }

        var shown = new List<CrossingLight>(lights.Length);
        foreach (var light in lights)
        {
            if (gearOf[light.Approach] >= 0) shown.Add(light with { Approach = gearOf[light.Approach] });
        }

        return new CrossingGear(plan, crossings, rail, [.. crossing], [.. pivotM], [.. downAlong], [.. armM], [.. shown]);
    }

    /// <summary>
    /// <b>Every crossing closed or opened as the trains now stand</b> (RAIL-1): its closing or its opening time noted as it
    /// turns, and how far down its arms were then. Asked every tick, so what is drawn turns on the tick the holds' rule
    /// says it does.
    /// </summary>
    public void Update(ReadOnlySpan<TrackRun> runs, float nowS)
    {
        for (var each = 0; each < _closed.Length; each++)
        {
            var closed = Closes(each, runs);
            if (closed == _closed[each]) continue;

            _loweredAtTurn[each] = LoweredOf(each, nowS);
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
    /// <b>How far down an approach's arm is</b>, nought up to one across its lanes (RAIL-7): lowered over the barrier's time
    /// once its crossing has closed and its lights have flashed a while, and raised over the same from the moment it opens
    /// — each from wherever the arm stood when the crossing turned.
    /// </summary>
    public float Lowered(int gear, float nowS) => LoweredOf(Crossing[gear], nowS);

    float LoweredOf(int crossing, float nowS)
    {
        var from = _loweredAtTurn[crossing];
        if (_closed[crossing]) return Math.Clamp(from + ((nowS - _closedAtS[crossing] - _rail.BarrierDelayS) / _rail.BarrierS), from, 1f);

        return float.IsNaN(_openedAtS[crossing]) ? 0f : Math.Clamp(from - ((nowS - _openedAtS[crossing]) / _rail.BarrierS), 0f, from);
    }

    /// <summary>How far up from lying across the road an approach's arm stands: <see cref="RailFigures.BarrierRaisedDeg"/> all the way up.</summary>
    public float UpRad(int gear, float nowS) => (1f - Lowered(gear, nowS)) * _rail.BarrierRaisedDeg * (MathF.PI / 180f);

    /// <summary>Whether an approach's arm is a body now (RAIL-10): anything but all the way up.</summary>
    public bool Blocks(int gear, float nowS) => Lowered(gear, nowS) > 0f;

    /// <summary>Where an approach's arm's body stands while it is one: the middle of the arm lying down.</summary>
    public Vector2 ArmMiddleM(int gear) => PivotM[gear] + (DownAlong[gear] * (ArmM[gear] * 0.5f));

    /// <summary>Whether an approach's X lights are lit now: the first half of every flash while its crossing is closed.</summary>
    public bool Lit(int gear, float nowS)
    {
        var crossing = Crossing[gear];
        return _closed[crossing] && ((int)MathF.Floor((nowS - _closedAtS[crossing]) * 2f / _rail.FlashS) & 1) == 0;
    }
}
