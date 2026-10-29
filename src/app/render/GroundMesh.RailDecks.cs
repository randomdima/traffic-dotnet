using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.App.Render;

/// <summary>
/// <b>A rail bridge, laid as a road's is</b> (RAIL-8, TER-3b.2), from art of its own: on the ground its piers' noses
/// where they stand in the water and a wing wall turned back along the bank at each corner where it lands; on the level
/// above its deck, a side along either shoulder of its ballast — the walkway and the parapet — and a post at every corner
/// where it lands. The track and its rails are laid on it after (<see cref="Track"/>, <see cref="Rails"/>).
/// </summary>
/// <remarks>
/// <b>Two tracks side by side are one deck</b>: a side, a pier, a wall or a post standing on another track's deck is not
/// laid (<see cref="ChordGrid"/>), so a double line is one bridge with its sides outside both tracks.
/// </remarks>
internal sealed partial class GroundMesh
{
    /// <summary>One stretch of a track on a bridge and on the map, and which of its ends land on the track at grade.</summary>
    readonly record struct RailSpan(TrackLine Line, bool LandsAtStart, bool LandsAtEnd)
    {
        public bool Lands(bool atStart) => atStart ? LandsAtStart : LandsAtEnd;

        /// <summary>Where one end of the span stands, and the way into the span from it.</summary>
        public (Vector2 AtM, Vector2 Inward) End(bool atStart) =>
            atStart ? (Line.PointM[0], Line.AlongAt(0f)) : (Line.PointM[^1], -Line.AlongAt(Line.LengthM));
    }

    /// <summary>
    /// <b>Every stretch of every track on a bridge</b>, cut to the map: an end lands where the track carries on at grade,
    /// and not where it runs off the map or off the track's own end.
    /// </summary>
    static List<RailSpan> RailSpans(TrackLine[] tracks, Vector2 sizeM)
    {
        var spans = new List<RailSpan>();
        var line = new List<Vector2>();
        var landsAtStart = false;
        foreach (var track in tracks)
        {
            var pointM = track.PointM;
            var atM = track.AtM;
            for (var chord = 0; chord + 1 < pointM.Length; chord++)
            {
                if (atM[chord + 1] <= atM[chord]) continue;

                var bridged = track.LevelAt((atM[chord] + atM[chord + 1]) * 0.5f) != TownMap.RailArrays.AtGrade;
                if (!bridged || !MapCut.Shares(pointM[chord], pointM[chord + 1], sizeM, out var enters, out var leaves) || leaves <= enters)
                {
                    Close(landsAtEnd: !bridged);
                    continue;
                }

                if (line.Count == 0)
                {
                    line.Add(Vector2.Lerp(pointM[chord], pointM[chord + 1], enters));
                    landsAtStart = chord > 0 && enters <= 0f;
                }

                line.Add(Vector2.Lerp(pointM[chord], pointM[chord + 1], leaves));
                if (leaves < 1f) Close(landsAtEnd: false);
            }

            Close(landsAtEnd: false);
        }

        return spans;

        void Close(bool landsAtEnd)
        {
            if (line.Count >= 2) spans.Add(new RailSpan(new TrackLine([.. line]), landsAtStart, landsAtEnd));
            line.Clear();
        }
    }

    /// <summary>Every span's deck, as the ground a side, a pier, a wall or a post of another span may not stand on.</summary>
    static ChordGrid RailDeckGrid(List<RailSpan> spans, SimConfig config)
    {
        var grid = new ChordGrid(config.Grid.Main.CellM);
        for (var span = 0; span < spans.Count; span++) grid.Add(span, spans[span].Line.PointM, config.RailDeckHalfM);
        return grid;
    }

    /// <summary>
    /// <b>The ground's half of every rail bridge</b> (RAIL-8): the noses of its piers where they stand in the water, as a
    /// road bridge's are (<see cref="Piers"/>), and a wing wall at each corner where it lands.
    /// </summary>
    void RailPiers(List<RailSpan> spans, ChordGrid decks, CityPlan.RingArrays water, SimConfig config)
    {
        var halfM = config.RailDeckHalfM;
        var depthM = BridgeLooks.Shared[BridgeLookKind.RailPierNose].FootprintM.Y;
        var wallM = config.Rail.BridgeDeckOverhangM * config.View.RailBridgeParapetShare;
        for (var at = 0; at < spans.Count; at++)
        {
            var line = spans[at].Line;
            var lengthM = line.LengthM;
            var piers = (int)MathF.Ceiling(lengthM / config.View.BridgePierPitchM) - 1;
            for (var pier = 1; pier <= piers; pier++)
            {
                var alongM = pier * lengthM / (piers + 1);
                var placeM = line.PlaceAt(alongM);
                foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
                {
                    var outward = Heading.RightOf(line.AlongAt(alongM)) * side;
                    var centreM = placeM + (outward * (halfM - (depthM * PierUnderShare) + (depthM * 0.5f)));
                    if (!Covers(water, centreM + (outward * (depthM * 0.5f))) || decks.OnAnother(at, centreM)) continue;

                    Fitting(BridgeLookKind.RailPierNose, centreM, -outward);
                }
            }

            foreach (var atStart in (ReadOnlySpan<bool>)[true, false])
            {
                if (!spans[at].Lands(atStart)) continue;

                var (endM, inward) = spans[at].End(atStart);
                foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
                {
                    if (decks.OnAnother(at, endM + (Heading.RightOf(inward) * (side * halfM)))) continue;

                    WingWall(endM, inward, side, halfM, wallM, Surface.RailBridgeParapet, BridgeLookKind.RailParapet,
                        config.View.RailBridgeParapetShare, config);
                }
            }
        }
    }

    /// <summary>
    /// <b>The level above's half of every rail bridge</b> (RAIL-8): each span's deck stroked out to its width, its side
    /// laid along either shoulder of the ballast but where it stands on another span's deck, and a post at each corner
    /// where it lands.
    /// </summary>
    void RailDecks(List<RailSpan> spans, ChordGrid decks, SimConfig config)
    {
        var halfM = config.RailDeckHalfM;
        var ballastHalfM = config.Rail.BallastWidthM * 0.5f;
        var sideMiddleM = (ballastHalfM + halfM) * 0.5f;
        var wallM = config.Rail.BridgeDeckOverhangM * config.View.RailBridgeParapetShare;
        var pictureM = BridgeLooks.Shared[BridgeLookKind.RailParapet].PictureM;
        foreach (var span in spans) Stroke(span.Line.PointM, halfM * 2f, closed: false, Surface.Deck, Plain);

        var run = new List<Vector2>();
        for (var at = 0; at < spans.Count; at++)
        {
            var pointM = spans[at].Line.PointM;
            foreach (var onTheRight in (ReadOnlySpan<bool>)[false, true])
            {
                var side = onTheRight ? 1f : -1f;
                for (var point = 0; point < pointM.Length; point++)
                {
                    if (!decks.OnAnother(at, pointM[point] + (Mitre(pointM, point) * (side * sideMiddleM))))
                    {
                        run.Add(pointM[point]);
                        continue;
                    }

                    Band(run.ToArray(), ballastHalfM, halfM, Surface.RailBridgeParapet, pictureM, onTheRight);
                    run.Clear();
                }

                Band(run.ToArray(), ballastHalfM, halfM, Surface.RailBridgeParapet, pictureM, onTheRight);
                run.Clear();
            }

            foreach (var atStart in (ReadOnlySpan<bool>)[true, false])
            {
                if (!spans[at].Lands(atStart)) continue;

                var (endM, inward) = spans[at].End(atStart);
                foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
                {
                    var centreM = Post(BridgeLookKind.RailEndPost, endM, inward, Heading.RightOf(inward) * side, halfM - (wallM * 0.5f));
                    if (!decks.OnAnother(at, centreM)) Fitting(BridgeLookKind.RailEndPost, centreM, -Heading.RightOf(inward) * side);
                }
            }
        }
    }

    /// <summary>
    /// <b>Lines' chords filed by the cells of the town's grid their boxes touch</b>, each with how far either side of it its
    /// ground reaches and whose it is: so whether a place stands on another track's ballast or another span's deck is asked
    /// of the few chords near it.
    /// </summary>
    sealed class ChordGrid(float cellM)
    {
        readonly Dictionary<(int X, int Y), List<int>> _cells = [];
        readonly List<(int Owner, Vector2 FromM, Vector2 ToM, float HalfM)> _chords = [];

        public void Add(int owner, ReadOnlySpan<Vector2> line, float halfM)
        {
            for (var at = 0; at + 1 < line.Length; at++)
            {
                var chord = _chords.Count;
                _chords.Add((owner, line[at], line[at + 1], halfM));
                var leastM = Vector2.Min(line[at], line[at + 1]) - new Vector2(halfM);
                var mostM = Vector2.Max(line[at], line[at + 1]) + new Vector2(halfM);
                for (var x = Cell(leastM.X); x <= Cell(mostM.X); x++)
                {
                    for (var y = Cell(leastM.Y); y <= Cell(mostM.Y); y++)
                    {
                        if (!_cells.TryGetValue((x, y), out var filed)) _cells[(x, y)] = filed = [];
                        filed.Add(chord);
                    }
                }
            }
        }

        /// <summary>Whether a place stands on the ground of any line but <paramref name="owner"/>'s.</summary>
        public bool OnAnother(int owner, Vector2 pointM)
        {
            if (!_cells.TryGetValue((Cell(pointM.X), Cell(pointM.Y)), out var filed)) return false;

            foreach (var chord in filed)
            {
                var (of, fromM, toM, halfM) = _chords[chord];
                if (of == owner) continue;

                var runM = toM - fromM;
                var share = Math.Clamp(Vector2.Dot(pointM - fromM, runM) / MathF.Max(runM.LengthSquared(), float.Epsilon), 0f, 1f);
                if (Vector2.DistanceSquared(pointM, fromM + (runM * share)) < halfM * halfM) return true;
            }

            return false;
        }

        int Cell(float m) => (int)MathF.Floor(m / cellM);
    }
}
