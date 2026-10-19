using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Rail;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>The railway, drawn</b> (RAIL-3, TER-7b): every track a strip of its art laid along its own line, between the decks
/// and the driven ground, so a road crossing it is laid over it — and, with the paint, the strip's two rails alone laid back
/// across every road and walk that crosses a track, and a stop line short of the ballast on both sides.
/// </summary>
/// <remarks>
/// <para>
/// <b>A track is a line and its strip is swept along it</b>, the way a road's ribbon is swept along its lane's: every chord
/// a quad, mitred to the next at the point they share so the strip runs round a bend without a gap, its picture repeating
/// along the track by how far along it the corner stands (<see cref="GroundVertex.OnTheTrack"/>) rather than where in the
/// world. What the picture leaves clear past its ballast is not drawn, and the ground beneath shows.
/// </para>
/// <para>
/// <b>Across a road only the rails are laid back</b>, the owner's word (2026-10-19): each rail's own band of the strip's
/// picture, so the rail over the tarmac is the rail on the ballast.
/// </para>
/// </remarks>
internal sealed partial class GroundMesh
{
    /// <summary>The width of a level crossing's stop line, across its road.</summary>
    const float StopLineWidthM = 0.4f;

    /// <summary>
    /// The pictures the ground is painted with: the five surfaces anchored to the world, then the track's strip — in the
    /// order <see cref="Surface"/> files them.
    /// </summary>
    public static string[] SurfaceFiles() => [.. ProjectPaths.GroundSurfaceFiles(), RailLooks.Shared.Track.SpritePath];

    /// <summary>
    /// Every track's strip, stopped short of every road and walk that crosses it at grade — where the road's own layers
    /// show and only the rails are laid back (<see cref="CrossingPaint"/>) — each stop cut along the road's edge.
    /// </summary>
    void Railway(CityPlan plan, LevelCrossings crossings, TrackLine[] tracks, SimConfig config)
    {
        var look = TrackLook(config);
        var halfM = look.FootprintM.Y * 0.5f;
        for (var track = 0; track < tracks.Length; track++)
        {
            var cuts = new List<(float FromM, float ToM, float Skew)>();
            for (var meeting = 0; meeting < crossings.Count; meeting++)
            {
                if (crossings.Track[meeting] != track) continue;

                var (atM, halfOverM) = (crossings.AlongTrackM[meeting], crossings.HalfOverM(plan, meeting));
                cuts.Add((atM - halfOverM, atM + halfOverM, SkewOf(plan, crossings, tracks[track], meeting)));
            }

            cuts.Sort((one, other) => one.FromM.CompareTo(other.FromM));
            var (fromM, fromSkew) = (0f, 0f);
            foreach (var (cutFromM, cutToM, skew) in cuts)
            {
                if (cutFromM > fromM) TrackStrip(tracks[track], fromM, cutFromM, 0f, halfM, 0f, 1f, look.FootprintM.X, plan.WorldSizeM, fromSkew, skew);
                if (cutToM > fromM) (fromM, fromSkew) = (cutToM, skew);
            }

            TrackStrip(tracks[track], fromM, tracks[track].LengthM, 0f, halfM, 0f, 1f, look.FootprintM.X, plan.WorldSizeM, fromSkew, 0f);
        }
    }

    /// <summary>
    /// How far along a track a crossing road's edge stands for every metre off the track's line: nought square across,
    /// and the cotangent of the slant otherwise — what a strip stopped at that edge cuts its end along.
    /// </summary>
    static float SkewOf(CityPlan plan, LevelCrossings crossings, TrackLine track, int meeting)
    {
        var road = Spline.SampleAt(plan.Roads.SegmentsOf(crossings.Road[meeting]), crossings.AlongRoadM[meeting]).Direction;
        var along = track.AlongAt(crossings.AlongTrackM[meeting]);
        return Vector2.Dot(road, along) / Vector2.Dot(road, new Vector2(-along.Y, along.X));
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
    /// <b>The rails laid back over every road crossing a track</b>, and a stop line across the road a setback short of the
    /// ballast at either end of each crossing (RAIL-3) — paint, laid with the rest of it.
    /// </summary>
    void CrossingPaint(CityPlan plan, LevelCrossings crossings, TrackLine[] tracks, SimConfig config, Vector3 paint)
    {
        var rail = config.Rail;
        var look = TrackLook(config);
        var widthM = look.FootprintM.Y;
        var railOffM = (rail.GaugeM + rail.RailHeadWidthM) * 0.5f;
        var bandHalfM = rail.CrossingRailBandM * 0.5f;
        for (var meeting = 0; meeting < crossings.Count; meeting++)
        {
            var track = tracks[crossings.Track[meeting]];
            var (atM, halfOverM) = (crossings.AlongTrackM[meeting], crossings.HalfOverM(plan, meeting));
            var skew = SkewOf(plan, crossings, track, meeting);
            foreach (var offM in (ReadOnlySpan<float>)[-railOffM, railOffM])
            {
                var middle = 0.5f + (offM / widthM);
                TrackStrip(
                    track, atM - halfOverM, atM + halfOverM, offM, bandHalfM, middle - (bandHalfM / widthM), middle + (bandHalfM / widthM),
                    look.FootprintM.X, plan.WorldSizeM, skew, skew);
            }
        }

        for (var crossing = 0; crossing < crossings.Crossings; crossing++)
        {
            var road = crossings.OfRoad[crossing];
            var arcs = plan.Roads.SegmentsOf(road);
            var halfWidthM = plan.Roads.WidthM[road] * 0.5f;
            foreach (var stopM in (ReadOnlySpan<float>)[crossings.FromRoadM[crossing] - rail.StopLineSetbackM, crossings.ToRoadM[crossing] + rail.StopLineSetbackM])
            {
                var at = Spline.SampleAt(arcs, stopM);
                OrientedRect(at.PositionM, at.Direction, new Vector2(StopLineWidthM * 0.5f, halfWidthM), Surface.Paint, paint);
            }
        }
    }

    /// <summary>
    /// <b>A band of the track's strip swept along a stretch of its line</b>: <paramref name="offsetM"/> off the line and a
    /// half-width either side of that, showing the strip's picture from <paramref name="acrossFrom"/> to
    /// <paramref name="acrossTo"/> of its width — the whole strip, or one rail's band of it. Cut to the map, and cut again
    /// wherever a chord ends or the along coordinate starts over (<see cref="GroundVertex.TrackPicturesFiled"/>).
    /// </summary>
    /// <remarks>
    /// <b>Each point's corners stand on its mitre</b>: the two chords' normals halved, lengthened so the strip keeps its
    /// width across both — so two chords' quads share the line their corners stand on, with no gap outside the bend and no
    /// fold inside it, a track never being bent tight enough for a mitre to reach past the next point (RAIL-2).
    /// </remarks>
    /// <param name="fromSkew">How far along the track the stretch's first end stands for every metre off the line, so it
    /// is cut along a crossing road's edge (<see cref="SkewOf"/>); <paramref name="toSkew"/> the same of its last.</param>
    void TrackStrip(
        TrackLine track, float fromM, float toM, float offsetM, float halfM, float acrossFrom, float acrossTo, float pictureM,
        Vector2 sizeM, float fromSkew = 0f, float toSkew = 0f)
    {
        var pointM = track.PointM;
        var atM = track.AtM;
        var restartM = GroundVertex.TrackPicturesFiled * pictureM;
        for (var chord = 0; chord + 1 < pointM.Length; chord++)
        {
            var (chordFromM, chordToM) = (atM[chord], atM[chord + 1]);
            if (chordToM <= fromM || chordFromM >= toM || chordToM <= chordFromM) continue;
            if (!MapCut.Shares(pointM[chord], pointM[chord + 1], sizeM, out var enters, out var leaves) || leaves <= enters) continue;

            var lengthM = chordToM - chordFromM;
            var (startM, endM) = (pointM[chord], pointM[chord + 1]);
            var (startMitre, endMitre) = (Mitre(pointM, chord), Mitre(pointM, chord + 1));
            var pieceFromM = MathF.Max(fromM, chordFromM + (enters * lengthM));
            var lastM = MathF.Min(toM, chordFromM + (leaves * lengthM));
            while (pieceFromM < lastM)
            {
                var pieceToM = MathF.Min(lastM, (MathF.Floor(pieceFromM / restartM) + 1f) * restartM);
                var picturesFrom = (pieceFromM % restartM) / pictureM;
                var picturesTo = picturesFrom + ((pieceToM - pieceFromM) / pictureM);
                foreach (var (alongM, pictures) in (ReadOnlySpan<(float, float)>)[(pieceFromM, picturesFrom), (pieceToM, picturesTo)])
                {
                    var skew = alongM == fromM ? fromSkew : alongM == toM ? toSkew : 0f;
                    foreach (var (offM, across) in (ReadOnlySpan<(float, float)>)[(offsetM - halfM, acrossFrom), (offsetM + halfM, acrossTo)])
                    {
                        var share = (alongM + (skew * offM) - chordFromM) / lengthM;
                        var mitre = Vector2.Lerp(startMitre, endMitre, Math.Clamp(share, 0f, 1f));
                        Corner(Vector2.Lerp(startM, endM, share) + (mitre * offM), GroundVertex.OnTheTrack(pictures + (skew * offM / pictureM), across));
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
