using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

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
/// wide, by what the slant costs. <b>Its barriers stand square across the road at both ends, as near the rails as a
/// clearance</b> (<see cref="Barrier"/>): the clearance at the end of their line nearest a rail, more at the road's middle
/// where it crosses at a slant. Its stop bars are each lane's own, short of its barrier (<see cref="CrossingStretches"/>).
/// </para>
/// </remarks>
internal sealed class LevelCrossings
{
    /// <summary>How far apart two meetings of one road with one line may stand along the road and be one crossing: tracks a spacing apart, a slant's worth over.</summary>
    public const float OneCrossingSpacings = 3f;

    public static LevelCrossings None { get; } = new()
    {
        Track = [], Road = [], Crossing = [], AlongTrackM = [], AlongRoadM = [], CentreM = [], SinAcross = [],
        Line = [], OfRoad = [], FromRoadM = [], ToRoadM = [], MeetingOffsets = [0], Barriers = [],
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

    /// <summary>Two a crossing, in <see cref="Approach"/>'s order: the lines its barriers stand on.</summary>
    public required CrossingEdge[] Barriers { get; init; }

    /// <summary>Which of <see cref="Barriers"/> traffic coming in at one end of a crossing — <paramref name="withTheRoad"/>, or against it — meets.</summary>
    public static int Approach(int crossing, bool withTheRoad) => (2 * crossing) + (withTheRoad ? 0 : 1);

    /// <summary>
    /// <b>The line square across the road a barrier stands on</b> (RAIL-7), on the side traffic coming in at that end of a
    /// crossing arrives from: as near the rails as <see cref="RailFigures.BarrierRailClearanceM"/> allows, measured along
    /// the road, at whichever end of its line — the tip of its arm or the walk its drive stands on — comes nearest.
    /// </summary>
    public CrossingEdge Barrier(int crossing, bool withTheRoad) => Barriers[Approach(crossing, withTheRoad)];

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

        var barriers = new CrossingEdge[2 * line.Count];
        for (var each = 0; each < line.Count; each++)
        {
            var road = ofRoad[each];
            var arcs = plan.Roads.SegmentsOf(road);
            var halfWidthM = plan.Roads.WidthM[road] * 0.5f;
            var (tipM, walkM) = (plan.Roads.Flow[road] == RoadFlow.BothWays ? 0f : -halfWidthM, halfWidthM + plan.PavementWidthM);
            var (first, last) = (offsets[each], offsets[each + 1] - 1);
            foreach (var withTheRoad in (ReadOnlySpan<bool>)[true, false])
            {
                var way = withTheRoad ? 1f : -1f;
                var alongM = meetings[withTheRoad ? first : last].AlongRoadM;
                var edge = default(CrossingEdge);
                for (var pass = 0; pass < BarrierPasses; pass++)
                {
                    var at = Spline.SampleAt(arcs, alongM);
                    edge = new CrossingEdge(at.PositionM, at.Direction * way);
                    alongM += way * (NearestRailM(meetings, first, last, rails, config.Rail, edge, tipM, walkM) - config.Rail.BarrierRailClearanceM);
                }

                barriers[Approach(each, withTheRoad)] = edge;
            }
        }

        return new LevelCrossings
        {
            Track = [.. meetings.Select(m => m.Track)], Road = [.. meetings.Select(m => m.Road)], Crossing = crossing,
            AlongTrackM = [.. meetings.Select(m => m.AlongTrackM)], AlongRoadM = [.. meetings.Select(m => m.AlongRoadM)],
            CentreM = [.. meetings.Select(m => m.CentreM)], SinAcross = [.. meetings.Select(m => m.Sin)],
            Line = [.. line], OfRoad = [.. ofRoad], FromRoadM = [.. fromRoadM], ToRoadM = [.. toRoadM], MeetingOffsets = [.. offsets],
            Barriers = barriers,
        };
    }

    /// <summary>
    /// How many times a barrier's line is moved along its road to stand its clearance off the rails: once is exact on a
    /// straight road, and the next takes up what a bend in it made of the first.
    /// </summary>
    const int BarrierPasses = 3;

    /// <summary>
    /// <b>How near the rails of a crossing's meetings a line across the road comes</b>, measured along the way traffic
    /// comes in at it, over <paramref name="tipM"/> to <paramref name="walkM"/> across it toward the right-hand kerb — the
    /// nearer of its two ends, its middle lying between. A rail is its track's line a rail's offset either side, where
    /// each end's way along the road crosses it.
    /// </summary>
    static float NearestRailM(
        List<(int Track, int Road, float AlongTrackM, float AlongRoadM, Vector2 CentreM, float Sin)> meetings, int first, int last,
        CityPlan.RailArrays rails, RailFigures rail, CrossingEdge edge, float tipM, float walkM)
    {
        var railOffM = (rail.GaugeM * 0.5f) + rail.RailHeadWidthM;
        var leastM = float.PositiveInfinity;
        for (var meeting = first; meeting <= last; meeting++)
        {
            foreach (var acrossM in (ReadOnlySpan<float>)[tipM, walkM])
            {
                var track = rails.TrackOf(meetings[meeting].Track);
                if (!Crosses(track, edge.AcrossM(acrossM), edge.Travel, meetings[meeting].CentreM, out var atM, out var along)) continue;

                leastM = MathF.Min(leastM, atM - (railOffM / MathF.Abs(Cross(edge.Travel, along))));
            }
        }

        return leastM;
    }

    /// <summary>
    /// Where the line through <paramref name="fromM"/> along <paramref name="way"/> crosses a track, as how far along it from
    /// there, and the way the track runs: of every chord it crosses, the one nearest the meeting at <paramref name="nearM"/>.
    /// </summary>
    static bool Crosses(ReadOnlySpan<Vector2> lineM, Vector2 fromM, Vector2 way, Vector2 nearM, out float atM, out Vector2 along)
    {
        var nearestM = float.PositiveInfinity;
        (atM, along) = (0f, way);
        for (var point = 0; point + 1 < lineM.Length; point++)
        {
            var chordM = lineM[point + 1] - lineM[point];
            var meets = Cross(way, chordM);
            if (meets == 0f) continue;

            var share = Cross(lineM[point] - fromM, way) / meets;
            if (share is < 0f or > 1f) continue;

            var hereM = Cross(lineM[point] - fromM, chordM) / meets;
            var offM = Vector2.Distance(fromM + (way * hereM), nearM);
            if (offM >= nearestM) continue;

            (nearestM, atM, along) = (offM, hereM, Vector2.Normalize(chordM));
        }

        return !float.IsPositiveInfinity(nearestM);
    }

    static float Cross(Vector2 one, Vector2 other) => (one.X * other.Y) - (one.Y * other.X);

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

/// <summary>
/// <b>A line square across the road at one end of a level crossing</b> (<see cref="LevelCrossings.Barrier"/>): where it crosses
/// the road's line, and the way the traffic coming in there travels.
/// </summary>
internal readonly record struct CrossingEdge(Vector2 MiddleM, Vector2 Travel)
{
    /// <summary>The way along the line toward the kerb on that traffic's right.</summary>
    public Vector2 TowardTheKerb => Heading.RightOf(Travel);

    /// <summary>The place on the line a distance across the road from its middle, toward the right-hand kerb.</summary>
    public Vector2 AcrossM(float acrossM) => MiddleM + (TowardTheKerb * acrossM);
}
