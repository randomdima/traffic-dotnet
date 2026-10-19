using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The railway, drawn</b> (RAIL-3, TER-7b): every track a strip of its art laid whole along its own line under the
/// roads and walks that cross it, and its two rails laid again over them — the track, then the road, then the rails.
/// </summary>
/// <remarks>
/// <para>
/// <b>A track is a line and its strip is swept along it</b>, the way a road's ribbon is swept along its lane's: every chord
/// a quad, mitred to the next at the point they share so the strip runs round a bend without a gap, its picture repeating
/// along the track by how far along it the corner stands (<see cref="GroundVertex.OnTheTrack"/>) rather than where in the
/// world. What the picture leaves clear past its ballast is not drawn, and the ground beneath shows.
/// </para>
/// <para>
/// <b>The rails are each rail's own band of the strip's picture</b>, laid along the whole track so nothing has to know where
/// a road crosses it: over the ballast they land on the rail already drawn there, and over the tarmac and the walk the
/// rail is the rail on the ballast.
/// </para>
/// </remarks>
internal sealed partial class GroundMesh
{
    /// <summary>
    /// The pictures the ground is painted with: the five surfaces anchored to the world, then the track's strip — in the
    /// order <see cref="Surface"/> files them.
    /// </summary>
    public static string[] SurfaceFiles() => [.. ProjectPaths.GroundSurfaceFiles(), RailLooks.Shared.Track.SpritePath];

    /// <summary>
    /// Every track's strip where it stands at grade, under the whole of the town's ground, or on a bridge, over it — laid
    /// whole: a road crossing it at grade is laid over it, and its rails over that (<see cref="Rails"/>).
    /// </summary>
    void Track(TrackLine[] tracks, SimConfig config, Vector2 sizeM, bool bridged)
    {
        var look = TrackLook(config);
        foreach (var track in tracks) TrackStrip(track, 0f, look.FootprintM.Y * 0.5f, 0f, 1f, look.FootprintM.X, sizeM, bridged);
    }

    /// <summary>
    /// <b>The deck every track on a bridge is borne on</b> (RAIL-8), its ballast and parapets wide, rimmed as a road's deck
    /// is: every run's rim first and every run's deck over them, so two tracks side by side are one deck.
    /// </summary>
    void RailDecks(TrackLine[] tracks, SimConfig config)
    {
        var widthM = config.Rail.BallastWidthM + (2f * config.Rail.BridgeDeckOverhangM);
        var rimM = config.Road.EdgeLineWidthM * 2f;
        foreach (var (shade, inM) in (ReadOnlySpan<(Vector3, float)>)[(Edge, 0f), (Plain, rimM)])
        {
            foreach (var track in tracks)
            {
                var pointM = track.PointM;
                var atM = track.AtM;
                for (var from = 0; from + 1 < pointM.Length;)
                {
                    var to = from;
                    while (to + 1 < pointM.Length && track.LevelAt((atM[to] + atM[to + 1]) * 0.5f) != TownMap.RailArrays.AtGrade) to++;
                    if (to > from) Stroke(pointM[from..(to + 1)], widthM - inM, closed: false, Surface.Deck, shade);
                    from = to + 1;
                }
            }
        }
    }

    /// <summary>
    /// Both rails of every track, each its own band of the strip's picture, laid over the ground where it stands at grade
    /// or on a bridge (<paramref name="bridged"/>) — so a bridge's deck is laid between the two.
    /// </summary>
    void Rails(TrackLine[] tracks, SimConfig config, Vector2 sizeM, bool bridged)
    {
        var rail = config.Rail;
        var look = TrackLook(config);
        var widthM = look.FootprintM.Y;
        var railOffM = (rail.GaugeM + rail.RailHeadWidthM) * 0.5f;
        var bandHalfM = rail.RailBandM * 0.5f;
        foreach (var track in tracks)
        {
            foreach (var offM in (ReadOnlySpan<float>)[-railOffM, railOffM])
            {
                var middle = 0.5f + (offM / widthM);
                TrackStrip(track, offM, bandHalfM, middle - (bandHalfM / widthM), middle + (bandHalfM / widthM), look.FootprintM.X, sizeM, bridged);
            }
        }
    }

    /// <summary>
    /// The track's art, held to the ballast the town keeps clear (RAIL-4): the strip is drawn across the band nobody is
    /// let onto, and a picture laid at another width would draw one band and keep another.
    /// </summary>
    static RailLook TrackLook(SimConfig config)
    {
        var look = RailLooks.Shared.Track;
        if (MathF.Abs(look.FootprintM.Y - config.Rail.BallastWidthM) > 1e-3f)
        {
            throw new InvalidDataException($"{look.Id} is drawn {look.FootprintM.Y} m across, and the ballast is {config.Rail.BallastWidthM} m.");
        }

        return look;
    }

    /// <summary>
    /// <b>A level crossing's stop bars</b> (RAIL-7): a junction's bar across every lane coming in at grade, where the
    /// crossing's hold of that lane begins (<see cref="CrossingStretches"/>).
    /// </summary>
    public static StopBars CrossingBars(CityPlan plan, LaneLines lanes, SimConfig config)
    {
        var holds = new List<(int Lane, float HeldFromM)>();
        if (plan.Rails.Count == 0) return StopBars.Across(lanes, holds, config);

        var stretches = new CrossingStretchFinder(plan, config);
        for (var lane = 0; lane < lanes.FirstRoadside; lane++)
        {
            if (lanes.LaneLevel[lane] != CityPlan.RoadArrays.Ground) continue;

            foreach (var stretch in stretches.Along(lanes.ArcsOf(lane), lanes.LaneLengthM[lane], CrossingLineKind.Driven))
            {
                if (stretch.FromM > 0f) holds.Add((lane, stretch.FromM));
            }
        }

        return StopBars.Across(lanes, holds, config);
    }

    /// <summary>
    /// <b>A level crossing's stretch of a road painted as a junction's is</b> (RAIL-7): the stretches of a line down it,
    /// from stop bar to stop bar, which its lines are laid unbroken across.
    /// </summary>
    sealed class CrossingStretchFinder(CityPlan plan, SimConfig config)
    {
        readonly TrackChords _chords = new(plan.Rails, config.Grid.Main.CellM);
        readonly LevelCrossings _crossings = LevelCrossings.Of(plan, config);
        readonly List<(float AlongPieceM, int Track, float AlongTrackM, float Sin)> _found = [];
        readonly List<CrossingMeeting> _meetings = [];
        readonly List<CrossingStretch> _stretches = [];

        public List<CrossingStretch> Along(ReadOnlySpan<ArcSeg> line, float lengthM, CrossingLineKind kind)
        {
            if (plan.Rails.Count == 0)
            {
                _stretches.Clear();
                return _stretches;
            }

            CrossingStretches.Along(line, lengthM, kind, _chords, _crossings, plan.Rails, config.Rail, _found, _meetings, _stretches);
            return _stretches;
        }
    }

    /// <summary>
    /// <b>A band of the track's strip swept along its line</b>, every chord or those on a bridge or off one
    /// (<paramref name="bridged"/>): <paramref name="offsetM"/> off the
    /// line and a half-width either side of that, showing the strip's picture from <paramref name="acrossFrom"/> to
    /// <paramref name="acrossTo"/> of its width — the whole strip, or one rail's band of it. Cut to the map, and cut again
    /// wherever a chord ends or the along coordinate starts over (<see cref="GroundVertex.TrackPicturesFiled"/>).
    /// </summary>
    /// <remarks>
    /// <b>Each point's corners stand on its mitre</b>: the two chords' normals halved, lengthened so the strip keeps its
    /// width across both — so two chords' quads share the line their corners stand on, with no gap outside the bend and no
    /// fold inside it, a track never being bent tight enough for a mitre to reach past the next point (RAIL-2).
    /// </remarks>
    void TrackStrip(
        TrackLine track, float offsetM, float halfM, float acrossFrom, float acrossTo, float pictureM, Vector2 sizeM, bool? bridged = null)
    {
        var pointM = track.PointM;
        var atM = track.AtM;
        var restartM = GroundVertex.TrackPicturesFiled * pictureM;
        for (var chord = 0; chord + 1 < pointM.Length; chord++)
        {
            var (chordFromM, chordToM) = (atM[chord], atM[chord + 1]);
            if (chordToM <= chordFromM) continue;
            if (bridged is { } onABridge && (track.LevelAt((chordFromM + chordToM) * 0.5f) != TownMap.RailArrays.AtGrade) != onABridge) continue;
            if (!MapCut.Shares(pointM[chord], pointM[chord + 1], sizeM, out var enters, out var leaves) || leaves <= enters) continue;

            var lengthM = chordToM - chordFromM;
            var (startM, endM) = (pointM[chord], pointM[chord + 1]);
            var (startMitre, endMitre) = (Mitre(pointM, chord), Mitre(pointM, chord + 1));
            var pieceFromM = chordFromM + (enters * lengthM);
            var lastM = chordFromM + (leaves * lengthM);
            while (pieceFromM < lastM)
            {
                var pieceToM = MathF.Min(lastM, (MathF.Floor(pieceFromM / restartM) + 1f) * restartM);
                var picturesFrom = (pieceFromM % restartM) / pictureM;
                var picturesTo = picturesFrom + ((pieceToM - pieceFromM) / pictureM);
                foreach (var (alongM, pictures) in (ReadOnlySpan<(float, float)>)[(pieceFromM, picturesFrom), (pieceToM, picturesTo)])
                {
                    var share = (alongM - chordFromM) / lengthM;
                    var mitre = Vector2.Lerp(startMitre, endMitre, share);
                    foreach (var (offM, across) in (ReadOnlySpan<(float, float)>)[(offsetM - halfM, acrossFrom), (offsetM + halfM, acrossTo)])
                    {
                        Corner(Vector2.Lerp(startM, endM, share) + (mitre * offM), GroundVertex.OnTheTrack(pictures, across));
                    }
                }

                // The four just laid: the near and far side where the piece starts, then where it ends.
                var first = _vertices.Count - 4;
                Quad(first, first + 2, first + 3, first + 1);
                pieceFromM = pieceToM;
            }
        }
    }

    /// <summary>
    /// The way a strip's corners stand off its line at a point, a metre of offset to a metre off the line along each
    /// chord: the two chords' left normals halved and lengthened by the cosine of half the turn between them.
    /// </summary>
    static Vector2 Mitre(ReadOnlySpan<Vector2> pointM, int point)
    {
        var before = point > 0 ? LeftOf(pointM[point - 1], pointM[point]) : LeftOf(pointM[point], pointM[point + 1]);
        var after = point + 1 < pointM.Length ? LeftOf(pointM[point], pointM[point + 1]) : before;
        var halved = Vector2.Normalize(before + after);
        return halved / Vector2.Dot(halved, after);
    }

    static Vector2 LeftOf(Vector2 fromM, Vector2 toM)
    {
        var along = Vector2.Normalize(toM - fromM);
        return new Vector2(-along.Y, along.X);
    }

    /// <summary>A corner of the track's strip, laid as it is: a strip's corners are never shared by another shape.</summary>
    void Corner(Vector2 positionM, uint shade) => _vertices.Add(new GroundVertex(positionM, shade));
}
