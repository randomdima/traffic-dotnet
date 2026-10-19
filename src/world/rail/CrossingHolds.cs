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
/// <b>Held as a light holds</b> (<c>SignalHolds</c>, TLT-1), from where its holder stands to wait — a lane's stop line, a
/// setback short of the ballast — to the far edge of the ballast, and a walk's from a setback either side, a walk being
/// walked both ways. A car or a walker meeting it reads it as a light's hold and waits with no clock running
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

    readonly int[] _holds;
    readonly float[] _heldToM;
    readonly RailFigures _rail;

    public static CrossingHolds None(SimConfig config) => new([], [], [], [0], [], [], [], config.Rail);

    CrossingHolds(
        int[] way, float[] fromM, float[] toM, int[] meetingOffsets, int[] track, float[] atM, float[] reachM, RailFigures rail)
    {
        (_way, _fromM, _toM, _meetingOffsets, _track, _atM, _reachM, _rail) = (way, fromM, toM, meetingOffsets, track, atM, reachM, rail);
        _holds = LaneOccupancy.NoHolds(way.Length);
        _heldToM = new float[way.Length];
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
        var (way, fromM, toM, offsets) = (new List<int>(), new List<float>(), new List<float>(), new List<int> { 0 });
        var (track, atM, reachM) = (new List<int>(), new List<float>(), new List<float>());
        var found = new List<(float AlongPieceM, int Track, float AlongTrackM, float Sin)>();
        var meetings = new List<(float AlongWayM, int Track, float AlongTrackM, float Sin)>();
        for (var each = 0; each < lines.WayCount; each++)
        {
            var level = ways.KindOf(each) switch
            {
                WayKind.Lane => roads.LaneLevel[ways.RoadLaneOf(each)],
                WayKind.Connector => roads.ConnectorLevel(ways.RoadConnectorOf(each)),
                _ => CityPlan.RoadArrays.Ground,
            };
            if (level != CityPlan.RoadArrays.Ground) continue;

            meetings.Clear();
            var pieceStartM = 0f;
            var wayLine = lines.LineOf(each, out var wayWidthM);
            foreach (var piece in wayLine)
            {
                chords.Crossings(piece, found);
                foreach (var (alongPieceM, onTrack, alongTrackM, sin) in found) meetings.Add((pieceStartM + alongPieceM, onTrack, alongTrackM, sin));

                pieceStartM += piece.LengthM;
            }

            if (meetings.Count == 0) continue;

            meetings.Sort((one, other) => one.AlongWayM.CompareTo(other.AlongWayM));
            var walked = ways.KindOf(each) is WayKind.Footway or WayKind.Mitre;
            var halfWidthM = wayWidthM * 0.5f;
            for (var first = 0; first < meetings.Count;)
            {
                var line = rails.Line[meetings[first].Track];
                var last = first;
                while (last + 1 < meetings.Count && rails.Line[meetings[last + 1].Track] == line
                       && meetings[last + 1].AlongWayM - meetings[last].AlongWayM
                       <= LevelCrossings.OneCrossingSpacings * rail.TrackSpacingM / meetings[last + 1].Sin)
                {
                    last++;
                }

                var nearM = meetings[first].AlongWayM - (rail.BallastWidthM * 0.5f / meetings[first].Sin) - rail.StopLineSetbackM;
                var farM = meetings[last].AlongWayM + (rail.BallastWidthM * 0.5f / meetings[last].Sin) + (walked ? rail.StopLineSetbackM : 0f);
                way.Add(each);
                fromM.Add(MathF.Max(0f, nearM));
                toM.Add(MathF.Min(ways.LengthM(each), farM));
                for (var meeting = first; meeting <= last; meeting++)
                {
                    track.Add(meetings[meeting].Track);
                    atM.Add(meetings[meeting].AlongTrackM);
                    reachM.Add(halfWidthM / meetings[meeting].Sin);
                }

                offsets.Add(track.Count);
                first = last + 1;
            }
        }

        return new CrossingHolds([.. way], [.. fromM], [.. toM], [.. offsets], [.. track], [.. atM], [.. reachM], rail);
    }

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
    /// <b>Every stretch whose turn it is, held while a train closes it now or at any time before its next turn</b>, and let
    /// go otherwise. <b>A hold that would be placed as it stands is kept</b>, so nothing waiting at it is woken by a
    /// crossing that did not change (as <c>SignalHolds.Lay</c>).
    /// </summary>
    /// <remarks><b>Laid after the bodies and before any plan</b> of a turn, and every plan asked with it is answered against it.</remarks>
    public void Lay(LaneOccupancy occupancy, ReadOnlySpan<TrackRun> runs, TickGroups turns, long tick)
    {
        foreach (var stretch in turns.InTurn(tick, _way.Length))
        {
            var hold = _holds[stretch];
            if (!Closed(stretch, runs, turns.IntervalS))
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
