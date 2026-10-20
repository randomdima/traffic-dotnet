using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Rail;

/// <summary>
/// <b>Every chord of every track at grade as a straight, filed by the cells of the town's grid its box touches</b>, so a
/// line asks only the chords near each of its pieces where it crosses the railway (<see cref="LevelCrossings"/>,
/// <see cref="CrossingHolds"/>). A chord on a bridge is crossed by nothing under it (RAIL-8), and is left out. Laid once with
/// the town.
/// </summary>
internal sealed class TrackChords
{
    readonly float _cellM;
    readonly Dictionary<(int X, int Y), List<int>> _cells = [];
    readonly List<int> _near = [];

    /// <summary>The track each chord is of, where along it the chord starts, and the chord.</summary>
    public readonly record struct Chord(int Track, float StartM, ArcSeg Seg);

    public List<Chord> Chords { get; } = [];

    public TrackChords(CityPlan.RailArrays rails, float cellM)
    {
        _cellM = cellM;
        for (var track = 0; track < rails.Count; track++)
        {
            var lineM = rails.TrackOf(track);
            var levels = rails.Level.AsSpan(rails.PointOffsets[track], lineM.Length);
            var atM = 0f;
            for (var point = 0; point + 1 < lineM.Length; point++)
            {
                var runM = lineM[point + 1] - lineM[point];
                var lengthM = runM.Length();
                if (lengthM <= 0f) continue;

                if (levels[point] != TownMap.RailArrays.AtGrade)
                {
                    atM += lengthM;
                    continue;
                }

                var seg = new ArcSeg(lineM[point], Trig.Atan2(runM.Y, runM.X), lengthM, 0f);
                foreach (var cell in Cells(Vector2.Min(seg.StartM, seg.EndM), Vector2.Max(seg.StartM, seg.EndM)))
                {
                    if (!_cells.TryGetValue(cell, out var list)) _cells[cell] = list = [];
                    list.Add(Chords.Count);
                }

                Chords.Add(new Chord(track, atM, seg));
                atM += lengthM;
            }
        }
    }

    /// <summary>
    /// <b>Where a piece of a line crosses any track</b>, each as how far along the piece, the track, how far along the
    /// track, and how square the two meet — the sine of the angle between them, held off nought.
    /// </summary>
    public void Crossings(in ArcSeg piece, List<(float AlongPieceM, int Track, float AlongTrackM, float Sin)> into)
    {
        into.Clear();
        Near(piece);
        Span<float> alongPieceM = stackalloc float[2];
        Span<float> alongChordM = stackalloc float[2];
        foreach (var at in _near)
        {
            var chord = Chords[at];
            var found = Spline.CrossingsOf(piece, chord.Seg, alongPieceM, alongChordM);
            for (var each = 0; each < found; each++)
            {
                if (alongPieceM[each] < 0f || alongPieceM[each] > piece.LengthM || alongChordM[each] < 0f || alongChordM[each] > chord.Seg.LengthM) continue;

                var way = Heading.Unit(piece.HeadingAtRad(alongPieceM[each]));
                var sin = MathF.Abs((way.X * chord.Seg.StartUnit.Y) - (way.Y * chord.Seg.StartUnit.X));
                into.Add((alongPieceM[each], chord.Track, chord.StartM + alongChordM[each], MathF.Max(sin, LeastSin)));
            }
        }
    }

    /// <summary>The squarest a crossing is ever read as: a line running along a track meets it over a long stretch, and not nowhere.</summary>
    const float LeastSin = 0.05f;

    /// <summary>The chords filed in the cells a piece's box touches — the box of its two ends grown by its length where it bends.</summary>
    void Near(in ArcSeg piece)
    {
        _near.Clear();
        var bowM = piece.Curvature == 0f ? 0f : piece.LengthM;
        var (leastM, mostM) = (Vector2.Min(piece.StartM, piece.EndM) - new Vector2(bowM), Vector2.Max(piece.StartM, piece.EndM) + new Vector2(bowM));
        foreach (var cell in Cells(leastM, mostM))
        {
            if (!_cells.TryGetValue(cell, out var list)) continue;

            foreach (var chord in list)
            {
                if (!_near.Contains(chord)) _near.Add(chord);
            }
        }
    }

    IEnumerable<(int X, int Y)> Cells(Vector2 leastM, Vector2 mostM)
    {
        for (var y = (int)MathF.Floor(leastM.Y / _cellM); y <= (int)MathF.Floor(mostM.Y / _cellM); y++)
        {
            for (var x = (int)MathF.Floor(leastM.X / _cellM); x <= (int)MathF.Floor(mostM.X / _cellM); x++) yield return (x, y);
        }
    }
}
