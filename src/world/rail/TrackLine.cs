using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;

namespace TrafficSimulation.World.Rail;

/// <summary>
/// <b>One track as a train runs it</b>: the line's points and how far along it each stands, so a place given in
/// metres along the track is a search and an interpolation (RAIL-2, TRN-8).
/// </summary>
/// <remarks>
/// <b>A chord stands for the curve</b>: the plan lays a line through points close enough that no chord of it stands off
/// the curve by more than a tolerance (<see cref="RailLines"/>), so a place between two points is read off the chord
/// between them. Asked every tick for every wagon, so nothing here allocates.
/// </remarks>
internal sealed class TrackLine
{
    readonly Vector2[] _pointM;
    readonly float[] _atM;
    readonly byte[] _level;

    /// <param name="level">The level of the chord each point starts (<see cref="CityPlan.RailArrays.Level"/>); none for a track wholly at grade.</param>
    public TrackLine(ReadOnlySpan<Vector2> pointM, ReadOnlySpan<byte> level = default)
    {
        _pointM = pointM.ToArray();
        _level = level.Length == pointM.Length ? level.ToArray() : new byte[pointM.Length];
        _atM = new float[_pointM.Length];
        for (var point = 1; point < _pointM.Length; point++) _atM[point] = _atM[point - 1] + Vector2.Distance(_pointM[point - 1], _pointM[point]);
    }

    public float LengthM => _atM[^1];

    public ReadOnlySpan<Vector2> PointM => _pointM;

    /// <summary>How far along the track each of its points stands.</summary>
    public ReadOnlySpan<float> AtM => _atM;

    /// <summary>The place a distance along the track stands at, held to the track's own two ends.</summary>
    public Vector2 PlaceAt(float alongM)
    {
        var chord = ChordAt(alongM);
        var lengthM = _atM[chord + 1] - _atM[chord];
        var share = lengthM > 0f ? Math.Clamp((alongM - _atM[chord]) / lengthM, 0f, 1f) : 0f;
        return _pointM[chord] + ((_pointM[chord + 1] - _pointM[chord]) * share);
    }

    /// <summary>The level the track stands at a distance along it: at grade, or on a bridge.</summary>
    public byte LevelAt(float alongM) => _level[ChordAt(alongM)];

    /// <summary>The way the track runs at a distance along it, from its first point toward its last.</summary>
    public Vector2 AlongAt(float alongM)
    {
        var chord = ChordAt(alongM);
        return Vector2.Normalize(_pointM[chord + 1] - _pointM[chord]);
    }

    /// <summary>
    /// <b>How far along the track the place nearest a point stands</b> — a walk of every chord, for what is laid once
    /// (<see cref="LevelCrossings"/>).
    /// </summary>
    public float NearestAlongM(Vector2 pointM)
    {
        var (bestM, bestAwayM2) = (0f, float.PositiveInfinity);
        for (var chord = 0; chord + 1 < _pointM.Length; chord++)
        {
            var runM = _pointM[chord + 1] - _pointM[chord];
            var lengthM2 = runM.LengthSquared();
            var share = lengthM2 > 0f ? Math.Clamp(Vector2.Dot(pointM - _pointM[chord], runM) / lengthM2, 0f, 1f) : 0f;
            var awayM2 = Vector2.DistanceSquared(_pointM[chord] + (runM * share), pointM);
            if (awayM2 >= bestAwayM2) continue;

            (bestM, bestAwayM2) = (_atM[chord] + (share * MathF.Sqrt(lengthM2)), awayM2);
        }

        return bestM;
    }

    /// <summary>
    /// <b>How far along the track the place nearest a point stands, walked from a distance it was last near</b>: chord to
    /// chord the way the point lies, never back, so a wheel read every tick walks a chord or two (TRN-9). Past either end
    /// it says so, in how far the point stands beyond it along the end chord.
    /// </summary>
    /// <returns>The distance along, held to the track; <paramref name="beyondM"/> negative before its start, positive past its end.</returns>
    public float NearestAlongFromM(Vector2 pointM, float nearM, out float beyondM)
    {
        var chord = ChordAt(nearM);
        var step = 0;
        while (true)
        {
            var runM = _pointM[chord + 1] - _pointM[chord];
            var lengthM = _atM[chord + 1] - _atM[chord];
            var alongChordM = lengthM > 0f ? Vector2.Dot(pointM - _pointM[chord], runM) / lengthM : 0f;
            if (alongChordM < 0f && chord > 0 && step <= 0)
            {
                (chord, step) = (chord - 1, -1);
                continue;
            }

            if (alongChordM > lengthM && chord < _pointM.Length - 2 && step >= 0)
            {
                (chord, step) = (chord + 1, 1);
                continue;
            }

            beyondM = alongChordM < 0f && chord == 0 ? alongChordM
                : alongChordM > lengthM && chord == _pointM.Length - 2 ? alongChordM - lengthM
                : 0f;
            return _atM[chord] + Math.Clamp(alongChordM, 0f, lengthM);
        }
    }

    /// <summary>
    /// <b>The way the track runs at a distance along it, eased across its points</b>: a chord's own way, turned toward its
    /// neighbour's over the half of the shorter of the two either side of the point they share — the mean of the two at
    /// the point — so what is square to the track does not jump a chord's turn as a wheel passes a point, and a curve's
    /// chords read as its tangent (TRN-8).
    /// </summary>
    public Vector2 EasedAlongAt(float alongM)
    {
        var chord = ChordAt(alongM);
        var lengthM = ChordLengthM(chord);
        var intoM = Math.Clamp(alongM - _atM[chord], 0f, lengthM);
        var way = ChordWay(chord);
        if (chord > 0)
        {
            var windowM = MathF.Min(ChordLengthM(chord - 1), lengthM) * 0.5f;
            if (intoM < windowM) return Eased(ChordWay(chord - 1), way, 0.5f + (0.5f * intoM / windowM));
        }

        if (chord + 2 < _pointM.Length)
        {
            var windowM = MathF.Min(ChordLengthM(chord + 1), lengthM) * 0.5f;
            var leftM = lengthM - intoM;
            if (leftM < windowM) return Eased(ChordWay(chord + 1), way, 0.5f + (0.5f * leftM / windowM));
        }

        return way;
    }

    float ChordLengthM(int chord) => _atM[chord + 1] - _atM[chord];

    Vector2 ChordWay(int chord)
    {
        var runM = _pointM[chord + 1] - _pointM[chord];
        var lengthM = runM.Length();
        return lengthM > 0f ? runM / lengthM : Vector2.UnitX;
    }

    /// <summary>Two ways blended, <paramref name="share"/> of the second, and made a unit again.</summary>
    static Vector2 Eased(Vector2 one, Vector2 other, float share)
    {
        var blended = (one * (1f - share)) + (other * share);
        var lengthM = blended.Length();
        return lengthM > 0f ? blended / lengthM : other;
    }

    /// <summary>The stretch of the track between two distances along it as a line of its own, its levels kept (<see cref="RailGraph"/>).</summary>
    public TrackLine Piece(float fromM, float toM)
    {
        var points = new List<Vector2> { PlaceAt(fromM) };
        var levels = new List<byte> { LevelAt(fromM) };
        for (var point = 0; point < _pointM.Length; point++)
        {
            if (_atM[point] <= fromM || _atM[point] >= toM) continue;

            points.Add(_pointM[point]);
            levels.Add(_level[point]);
        }

        points.Add(PlaceAt(toM));
        levels.Add(TownMap.RailArrays.AtGrade);
        return new TrackLine([.. points], [.. levels]);
    }

    /// <summary>The chord a distance along the track falls on: the last point at or before it, short of the last point.</summary>
    int ChordAt(float alongM)
    {
        var at = Array.BinarySearch(_atM, alongM);
        if (at < 0) at = ~at - 1;
        return Math.Clamp(at, 0, _pointM.Length - 2);
    }

    /// <summary>Every track of a plan as a line a train runs.</summary>
    public static TrackLine[] Of(CityPlan.RailArrays rails)
    {
        var lines = new TrackLine[rails.Count];
        for (var track = 0; track < rails.Count; track++)
        {
            lines[track] = new TrackLine(rails.TrackOf(track), rails.Level.AsSpan(rails.PointOffsets[track], rails.TrackOf(track).Length));
        }
        return lines;
    }
}
