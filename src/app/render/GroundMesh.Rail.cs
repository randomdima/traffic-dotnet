using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The railway, drawn</b> (RAIL-3, TER-7b): at grade every track's bed of ballast, the grass faded over its edges, and
/// its sleepers and rails over that, all under the roads and walks that cross it; its two rails laid again over them —
/// the track, then the road, then the rails. On a bridge the deck is what the sleepers lie on, and no bed is laid.
/// </summary>
/// <remarks>
/// <para>
/// <b>A track is a line and its strips are swept along it</b>, the way a road's ribbon is swept along its lane's: every
/// chord a quad, mitred to the next at the point they share so the strip runs round a bend without a gap, its picture
/// repeating along the track by how far along it the corner stands (<see cref="GroundVertex.OnTheTrack"/>) rather than
/// where in the world. Each strip reads its own band of the one picture (<see cref="TrackSheet"/>), and what it leaves
/// clear is not drawn: the ground beneath shows.
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
    /// The pictures the ground is painted with: the five surfaces anchored to the world, then the track's strip, then the
    /// shore, then the two kerbs' strips — in the order <see cref="Surface"/> files them.
    /// </summary>
    public static string[] SurfaceFiles() =>
    [
        .. ProjectPaths.GroundSurfaceFiles(), RailLooks.Shared.Track.SpritePath, ProjectPaths.ShoreSurfaceFile(),
        KerbLooks.Shared.Kerb.SpritePath, KerbLooks.Shared.Edging.SpritePath,
    ];

    /// <summary>
    /// Every track's sleepers and rails where it stands at grade, under the whole of the town's ground, or on a bridge,
    /// over it — laid whole: a road crossing it at grade is laid over it, and its rails over that (<see cref="Rails"/>).
    /// </summary>
    void Track(TrackLine[] tracks, Vector2 sizeM, bool bridged)
    {
        var laid = TrackSheet.Laid;
        var halfM = RailLooks.Shared.Track.FootprintM.Y * 0.5f;
        foreach (var track in tracks) TrackStrip(track, 0f, halfM, laid.Track.From, laid.Track.To, laid.PictureM, sizeM, bridged);
    }

    /// <summary>
    /// <b>Every track's bed of ballast where it stands at grade</b> (RAIL-13), the band the town keeps clear wide (RAIL-4),
    /// under the sleepers and the whole of the town's ground: a road crossing it is laid over it, and a bridge's deck is
    /// what its sleepers lie on instead.
    /// </summary>
    void Ballast(TrackLine[] tracks, SimConfig config, Vector2 sizeM)
    {
        var laid = TrackSheet.Laid;
        var halfM = BallastLook(config).FootprintM.Y * 0.5f;
        foreach (var track in tracks) TrackStrip(track, 0f, halfM, laid.Bed.From, laid.Bed.To, laid.PictureM, sizeM, bridged: false);
    }

    /// <summary>
    /// <b>The grass faded out onto every track's ballast from its shoulders</b> (RAIL-3), as it is onto a shore
    /// (<see cref="Fade"/>): whole at the shoulder and gone <see cref="ViewFigures.BallastFadeM"/> in, so the track lies in
    /// the grass rather than on it. Laid where the track stands at grade and on the map, and not where a shoulder stands
    /// on another track's ballast.
    /// </summary>
    void BallastFade(TrackLine[] tracks, SimConfig config, Vector2 sizeM)
    {
        var halfM = config.Rail.BallastWidthM * 0.5f;
        var innerM = halfM - config.View.BallastFadeM;
        var ballast = new ChordGrid(config.Grid.Main.CellM);
        for (var track = 0; track < tracks.Length; track++) ballast.Add(track, tracks[track].PointM, halfM);

        var whole = GroundVertex.Fading(Surface.Grass, 1f);
        var gone = GroundVertex.Fading(Surface.Grass, 0f);
        var run = new List<Vector2>();
        for (var track = 0; track < tracks.Length; track++)
        {
            var pointM = tracks[track].PointM;
            var atM = tracks[track].AtM;
            foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
            {
                var sides = side > 0f ? new Sides(new Vector2(-innerM, halfM), gone, whole) : new Sides(new Vector2(halfM, -innerM), whole, gone);
                for (var chord = 0; chord + 1 < pointM.Length; chord++)
                {
                    var (fromM, toM) = (pointM[chord], pointM[chord + 1]);
                    var shoulderM = ((fromM + toM) * 0.5f) + (LeftOf(fromM, toM) * (side * halfM));
                    if (atM[chord + 1] <= atM[chord]
                        || tracks[track].LevelAt((atM[chord] + atM[chord + 1]) * 0.5f) != TownMap.RailArrays.AtGrade
                        || !MapCut.Shares(fromM, toM, sizeM, out var enters, out var leaves) || leaves <= enters
                        || ballast.OnAnother(track, shoulderM))
                    {
                        Struck();
                        continue;
                    }

                    if (run.Count == 0) run.Add(Vector2.Lerp(fromM, toM, enters));
                    run.Add(Vector2.Lerp(fromM, toM, leaves));
                    if (leaves < 1f) Struck();
                }

                Struck();

                void Struck()
                {
                    Stroke(CollectionsMarshal.AsSpan(run), closed: false, sides);
                    run.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Both rails of every track, each its own band of the sleepers' picture, laid over the ground where it stands at grade
    /// or on a bridge (<paramref name="bridged"/>) — so a bridge's deck is laid between the two.
    /// </summary>
    void Rails(TrackLine[] tracks, SimConfig config, Vector2 sizeM, bool bridged)
    {
        var rail = config.Rail;
        var laid = TrackSheet.Laid;
        var widthM = RailLooks.Shared.Track.FootprintM.Y;
        var bandShare = laid.Track.To - laid.Track.From;
        var railOffM = (rail.GaugeM + rail.RailHeadWidthM) * 0.5f;
        var bandHalfM = rail.RailBandM * 0.5f;
        foreach (var track in tracks)
        {
            foreach (var offM in (ReadOnlySpan<float>)[-railOffM, railOffM])
            {
                var (fromM, toM) = ((widthM * 0.5f) + offM - bandHalfM, (widthM * 0.5f) + offM + bandHalfM);
                TrackStrip(track, offM, bandHalfM, laid.Track.From + (bandShare * fromM / widthM),
                    laid.Track.From + (bandShare * toM / widthM), laid.PictureM, sizeM, bridged);
            }
        }
    }

    /// <summary>
    /// The bed's art, held to the ballast the town keeps clear (RAIL-4): the bed is drawn across the band nobody is let
    /// onto, and a picture laid at another width would draw one band and keep another.
    /// </summary>
    static RailLook BallastLook(SimConfig config)
    {
        var look = RailLooks.Shared.Ballast;
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
