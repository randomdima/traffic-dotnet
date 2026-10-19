using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Rail;

/// <summary>
/// <b>What a level crossing does to the town, which is hold ground</b> (RAIL-1): a secondary claim at rung
/// <see cref="ClaimPriority.Hard"/> — above every plan, a call's included — over every way of the town that crosses the
/// railway, while a train closes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Held as a light holds</b> (<c>SignalHolds</c>, TLT-1), over each way's stretch (<see cref="CrossingStretches"/>):
/// from where its holder stands to wait — a lane's stop bar — to the far edge of the ballast, a walk's a setback past it. A car or a walker meeting it reads it as a light's hold and waits with no clock running
/// (<see cref="Refusal.ByALight"/>): a crossing's lights are lights.
/// </para>
/// <para>
/// <b>It ends at the first body on the way it holds</b> (RAIL-6, TLT-2a), so nothing already over the stop line is held on
/// the track. <b>And ground its holder can no longer stop short of is never taken from it</b>, at any rung: a car that
/// could not stop when the crossing closed is let over, which the warning time is long enough to make rare.
/// </para>
/// <para>
/// <b>The stretches are found when the town is laid</b>: every way's line against every track's, the meetings of one
/// way with one railway line near each other one stretch.
/// </para>
/// </remarks>
internal sealed class CrossingHolds
{
    readonly int[] _way;
    readonly float[] _fromM;
    readonly float[] _toM;

    /// <summary>Stretches + 1 entries, over the meetings each stretch is closed by.</summary>
    readonly int[] _meetingOffsets;

    readonly int[] _track;
    readonly float[] _atM;

    /// <summary>How far along its track each meeting's way reaches either way of where the two lines cross.</summary>
    readonly float[] _reachM;

    /// <summary>Whether each stretch is driven, which a crossing's arm closes, rather than walked.</summary>
    readonly bool[] _driven;

    readonly int[] _holds;
    readonly float[] _heldToM;

    /// <summary>When each stretch was last found closed, in town seconds.</summary>
    readonly float[] _closedAtS;

    readonly RailFigures _rail;

    public static CrossingHolds None(SimConfig config) => new([], [], [], [], [0], [], [], [], config.Rail);

    CrossingHolds(
        int[] way, bool[] driven, float[] fromM, float[] toM, int[] meetingOffsets, int[] track, float[] atM, float[] reachM, RailFigures rail)
    {
        (_way, _driven, _fromM, _toM, _meetingOffsets, _track, _atM, _reachM, _rail) = (way, driven, fromM, toM, meetingOffsets, track, atM, reachM, rail);
        _holds = LaneOccupancy.NoHolds(way.Length);
        _heldToM = new float[way.Length];
        _closedAtS = new float[way.Length];
        Array.Fill(_closedAtS, float.NegativeInfinity);
    }

    /// <summary>How many stretches the crossings can hold at once — every one, which the reservations are sized with room for.</summary>
    public int Count => _way.Length;

    /// <summary>How many stretches are held now, for an instrument: a walk of every one.</summary>
    public int HeldCount
    {
        get
        {
            var held = 0;
            foreach (var hold in _holds)
            {
                if (hold != LaneOccupancy.NoHold) held++;
            }

            return held;
        }
    }

    /// <param name="roads">What level each lane and join is driven on: one over the ground crosses no track at grade.</param>
    public static CrossingHolds Of(CityPlan plan, WayLines lines, TownWays ways, RoadGraph roads, SimConfig config)
    {
        var rails = plan.Rails;
        if (rails.Count == 0) return None(config);

        var rail = config.Rail;
        var chords = new TrackChords(rails, config.Grid.Main.CellM);
        var crossings = LevelCrossings.Of(plan, config);
        var lights = new List<CrossingLight>();
        var (way, driven, fromM, toM, offsets) = (new List<int>(), new List<bool>(), new List<float>(), new List<float>(), new List<int> { 0 });
        var (track, atM, reachM) = (new List<int>(), new List<float>(), new List<float>());
        var found = new List<(float AlongPieceM, int Track, float AlongTrackM, float Sin)>();
        var meetings = new List<CrossingMeeting>();
        var stretches = new List<CrossingStretch>();
        for (var each = 0; each < lines.WayCount; each++)
        {
            if (ways.KindOf(each) == WayKind.Rail) continue;

            var level = ways.KindOf(each) switch
            {
                WayKind.Lane => roads.LaneLevel[ways.RoadLaneOf(each)],
                WayKind.Connector => roads.ConnectorLevel(ways.RoadConnectorOf(each)),
                _ => CityPlan.RoadArrays.Ground,
            };
            if (level != CityPlan.RoadArrays.Ground) continue;

            var walked = ways.KindOf(each) is WayKind.Footway or WayKind.Mitre;
            var wayLine = lines.LineOf(each, out var wayWidthM);
            var kind = walked ? CrossingLineKind.Walked : CrossingLineKind.Driven;
            CrossingStretches.Along(wayLine, ways.LengthM(each), kind, chords, crossings, rails, rail, found, meetings, stretches);
            foreach (var stretch in stretches)
            {
                if (stretch.Approach >= 0) lights.Add(CrossingLight.Over(wayLine, stretch, config));
                way.Add(each);
                driven.Add(!walked);
                fromM.Add(stretch.FromM);
                toM.Add(stretch.ToM);
                for (var meeting = stretch.FirstMeeting; meeting <= stretch.LastMeeting; meeting++)
                {
                    track.Add(meetings[meeting].Track);
                    atM.Add(meetings[meeting].AlongTrackM);
                    reachM.Add(wayWidthM * 0.5f / meetings[meeting].Sin);
                }

                offsets.Add(track.Count);
            }
        }

        return new CrossingHolds([.. way], [.. driven], [.. fromM], [.. toM], [.. offsets], [.. track], [.. atM], [.. reachM], rail)
        {
            Lights = [.. lights],
        };
    }

    /// <summary>The X light over every driven stretch that meets a barrier coming in, which the crossing's gear shows (RAIL-7).</summary>
    public CrossingLight[] Lights { get; private init; } = [];

    /// <summary>
    /// <b>Whether a stretch is closed</b> by any of the trains running (RAIL-1) — now, or at any time over the next
    /// <paramref name="laterS"/>.
    /// </summary>
    public bool Closed(int stretch, ReadOnlySpan<TrackRun> runs, float laterS)
    {
        for (var meeting = _meetingOffsets[stretch]; meeting < _meetingOffsets[stretch + 1]; meeting++)
        {
            foreach (ref readonly var run in runs)
            {
                if (run.Track == _track[meeting] && run.Closes(_atM[meeting], _reachM[meeting], _rail, laterS)) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <b>Every stretch whose turn it is, held while a train closes it now or at any time before its next turn</b> — a
    /// driven one for as long again as its arm takes to be raised once it opens (RAIL-10) — and let go otherwise. <b>A
    /// hold that would be placed as it stands is kept</b>, so nothing waiting at it is woken by a crossing that did not
    /// change (as <c>SignalHolds.Lay</c>).
    /// </summary>
    /// <remarks><b>Laid after the bodies and before any plan</b> of a turn, and every plan asked with it is answered against it.</remarks>
    public void Lay(LaneOccupancy occupancy, ReadOnlySpan<TrackRun> runs, TickGroups turns, long tick, float nowS)
    {
        foreach (var stretch in turns.InTurn(tick, _way.Length))
        {
            var hold = _holds[stretch];
            if (Closed(stretch, runs, turns.IntervalS)) _closedAtS[stretch] = nowS;
            else if (!_driven[stretch] || nowS - _closedAtS[stretch] >= _rail.BarrierS)
            {
                occupancy.Release(hold);
                _holds[stretch] = LaneOccupancy.NoHold;
                continue;
            }

            var reachM = occupancy.FirstBody(_way[stretch], _fromM[stretch], _toM[stretch], LaneOccupancy.Nobody, LaneRoster.Crossing, out var bodyM)
                ? bodyM
                : _toM[stretch];
            if (hold != LaneOccupancy.NoHold && reachM == _heldToM[stretch] && !occupancy.CutOf(hold).Refused) continue;

            occupancy.Release(hold);
            hold = occupancy.BeginHold(stretch, LaneRoster.Crossing);
            occupancy.Place(hold, _way[stretch], _fromM[stretch], reachM, ClaimPriority.Hard);
            (_holds[stretch], _heldToM[stretch]) = (hold, reachM);
        }
    }
}
