using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Rail;

/// <summary>
/// <b>Where the roads meet the railway at grade</b> (RAIL-1): every place a road's line crosses a track's — a
/// <em>meeting</em> — and the level crossings they make, one a road and a railway line however many of the line's
/// tracks the road crosses there. What is drawn of a crossing is laid off these (RAIL-3, RAIL-7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Read off the plan once, as the lines cross</b> (<see cref="TrackChords"/>). A road on a deck where it meets a track
/// is not at grade and makes no crossing, and neither does a bay, which nobody drives along.
/// </para>
/// <para>
/// <b>A crossing's stretch of its road is the ballast's</b>: from where the road meets the first track's ballast to
/// where it leaves the last's, measured along the road — which on a road crossing at a slant is longer than the band is
/// wide, by what the slant costs. Its stop lines stand a setback short of that at both ends.
/// </para>
/// </remarks>
internal sealed class LevelCrossings
{
    /// <summary>How far apart two meetings of one road with one line may stand along the road and be one crossing: tracks a spacing apart, a slant's worth over.</summary>
    public const float OneCrossingSpacings = 3f;

    public static LevelCrossings None { get; } = new()
    {
        Track = [], Road = [], Crossing = [], AlongTrackM = [], AlongRoadM = [], CentreM = [], SinAcross = [],
        Line = [], OfRoad = [], FromRoadM = [], ToRoadM = [], MeetingOffsets = [0],
    };

    /// <summary>The track each meeting is on.</summary>
    public required int[] Track { get; init; }

    /// <summary>And the road.</summary>
    public required int[] Road { get; init; }

    /// <summary>The crossing each meeting is one of.</summary>
    public required int[] Crossing { get; init; }

    public required float[] AlongTrackM { get; init; }

    public required float[] AlongRoadM { get; init; }

    /// <summary>Where the two lines cross.</summary>
    public required Vector2[] CentreM { get; init; }

    /// <summary>How square the road meets the track: the sine of the angle between them, 1 square across.</summary>
    public required float[] SinAcross { get; init; }

    public int Count => Track.Length;

    /// <summary>The railway line each crossing is over (TRN-5).</summary>
    public required int[] Line { get; init; }

    /// <summary>The road each crossing carries.</summary>
    public required int[] OfRoad { get; init; }

    /// <summary>Where along its road the crossing's ballast starts and ends.</summary>
    public required float[] FromRoadM { get; init; }

    public required float[] ToRoadM { get; init; }

    /// <summary>Crossings + 1 entries, over the meetings, each crossing's in the order they stand along its road.</summary>
    public required int[] MeetingOffsets { get; init; }

    public int Crossings => Line.Length;

    /// <summary>
    /// How far along its track a meeting's road reaches either way, kerb to kerb and the walk either side, at the track's
    /// line — the stretch of track the road covers.
    /// </summary>
    public float HalfOverM(CityPlan plan, int meeting) =>
        ((plan.Roads.WidthM[Road[meeting]] * 0.5f) + plan.PavementWidthM) / SinAcross[meeting];

    /// <summary>The crossings each plan laid, read once a plan: the ground, the gear and the room a frame is laid for all ask.</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CityPlan, LevelCrossings> Laid = new();

    public static LevelCrossings Of(CityPlan plan, SimConfig config) => Laid.GetValue(plan, laid => Read(laid, config));

    static LevelCrossings Read(CityPlan plan, SimConfig config)
    {
        var rails = plan.Rails;
        if (rails.Count == 0) return None;

        var chords = new TrackChords(rails, config.Grid.Main.CellM);
        var meetings = new List<(int Track, int Road, float AlongTrackM, float AlongRoadM, Vector2 CentreM, float Sin)>();
        var found = new List<(float AlongPieceM, int Track, float AlongTrackM, float Sin)>();
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.IsABay(road) || plan.Roads.LevelOf(road) != CityPlan.RoadArrays.Ground) continue;

            var pieceStartM = 0f;
            foreach (var piece in plan.Roads.SegmentsOf(road))
            {
                chords.Crossings(piece, found);
                foreach (var (alongPieceM, track, alongTrackM, sin) in found)
                {
                    var alongRoadM = pieceStartM + alongPieceM;
                    if (!OnADeck(plan, road, alongRoadM)) meetings.Add((track, road, alongTrackM, alongRoadM, piece.PointAtM(alongPieceM), sin));
                }

                pieceStartM += piece.LengthM;
            }
        }

        meetings.Sort((one, other) => one.Road != other.Road ? one.Road.CompareTo(other.Road)
            : rails.Line[one.Track] != rails.Line[other.Track] ? rails.Line[one.Track].CompareTo(rails.Line[other.Track])
            : one.AlongRoadM.CompareTo(other.AlongRoadM));

        var crossing = new int[meetings.Count];
        var (line, ofRoad, fromRoadM, toRoadM, offsets) = (new List<int>(), new List<int>(), new List<float>(), new List<float>(), new List<int> { 0 });
        var ballastHalfM = config.Rail.BallastWidthM * 0.5f;
        for (var meeting = 0; meeting < meetings.Count; meeting++)
        {
            var here = meetings[meeting];
            var reachM = ballastHalfM / here.Sin;
            var joins = meeting > 0 && ofRoad[^1] == here.Road && line[^1] == rails.Line[here.Track]
                        && here.AlongRoadM - meetings[meeting - 1].AlongRoadM <= OneCrossingSpacings * config.Rail.TrackSpacingM / here.Sin;
            if (!joins)
            {
                if (meeting > 0) offsets.Add(meeting);
                line.Add(rails.Line[here.Track]);
                ofRoad.Add(here.Road);
                fromRoadM.Add(here.AlongRoadM - reachM);
                toRoadM.Add(here.AlongRoadM + reachM);
            }

            toRoadM[^1] = MathF.Max(toRoadM[^1], here.AlongRoadM + reachM);
            fromRoadM[^1] = MathF.Min(fromRoadM[^1], here.AlongRoadM - reachM);
            crossing[meeting] = line.Count - 1;
        }

        if (meetings.Count > 0) offsets.Add(meetings.Count);

        return new LevelCrossings
        {
            Track = [.. meetings.Select(m => m.Track)], Road = [.. meetings.Select(m => m.Road)], Crossing = crossing,
            AlongTrackM = [.. meetings.Select(m => m.AlongTrackM)], AlongRoadM = [.. meetings.Select(m => m.AlongRoadM)],
            CentreM = [.. meetings.Select(m => m.CentreM)], SinAcross = [.. meetings.Select(m => m.Sin)],
            Line = [.. line], OfRoad = [.. ofRoad], FromRoadM = [.. fromRoadM], ToRoadM = [.. toRoadM], MeetingOffsets = [.. offsets],
        };
    }

    /// <summary>Whether a place along a road stands on one of its decks, which carries it over whatever is below.</summary>
    static bool OnADeck(CityPlan plan, int road, float alongRoadM)
    {
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            if (plan.Bridges.Road[bridge] == road && alongRoadM >= plan.Bridges.FromM[bridge] && alongRoadM <= plan.Bridges.ToM[bridge]) return true;
        }

        return false;
    }
}
