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
        return Vector2.Lerp(_pointM[chord], _pointM[chord + 1], share);
    }

    /// <summary>The level the track stands at a distance along it: at grade, or on a bridge.</summary>
    public byte LevelAt(float alongM) => _level[ChordAt(alongM)];

    /// <summary>Whether any of the track between two distances along it, in either order, stands on a bridge.</summary>
    public bool BridgedBetween(float oneM, float otherM)
    {
        for (var chord = ChordAt(MathF.Min(oneM, otherM)); chord <= ChordAt(MathF.Max(oneM, otherM)); chord++)
        {
            if (_level[chord] != TownMap.RailArrays.AtGrade) return true;
        }

        return false;
    }

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
