using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Straight segments filed by the square cells their boxes cover</b>, so a straight is asked only of the segments near
/// it — each numbered in the order it was added, and one cut shorter keeping its number.
/// </summary>
/// <remarks>
/// <b>Its cell is the question's reach</b>, so a straight of that reach is asked of a few cells: the town's grid
/// (SIM-8) is a few metres a cell, and a street a block long would be filed under a hundred of them.
/// </remarks>
internal sealed class SegmentCells(float cellM)
{
    readonly List<(Vector2 FromM, Vector2 ToM)> _segments = [];
    readonly Dictionary<(int, int), List<int>> _in = [];
    readonly HashSet<int> _met = [];

    public (Vector2 FromM, Vector2 ToM) this[int segment] => _segments[segment];

    public void Add(Vector2 fromM, Vector2 toM)
    {
        _segments.Add((fromM, toM));
        var ((x0, y0), (x1, y1)) = Cells(fromM, toM);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (!_in.TryGetValue((x, y), out var list)) _in[(x, y)] = list = [];
                list.Add(_segments.Count - 1);
            }
        }
    }

    /// <summary>One segment made shorter: it is filed where it was, which covers where it is.</summary>
    public void Cut(int segment, Vector2 fromM, Vector2 toM) => _segments[segment] = (fromM, toM);

    /// <summary>
    /// Every segment a straight crosses, each strictly between its own ends (<see cref="Polygons.SegmentsCross"/>): how far
    /// along the straight, as a share of it, which segment, and where.
    /// </summary>
    public void Crossing(Vector2 fromM, Vector2 toM, List<(float Along, int Segment, Vector2 AtM)> into)
    {
        into.Clear();
        _met.Clear();
        var run = toM - fromM;
        var ((x0, y0), (x1, y1)) = Cells(fromM, toM);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (!_in.TryGetValue((x, y), out var list)) continue;

                foreach (var segment in list)
                {
                    var (oneM, otherM) = _segments[segment];
                    if (!_met.Add(segment) || !Polygons.SegmentsCross(fromM, toM, oneM, otherM)) continue;

                    var other = otherM - oneM;
                    var along = Cross(oneM - fromM, other) / Cross(run, other);
                    into.Add((along, segment, fromM + (run * along)));
                }
            }
        }
    }

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    ((int X, int Y) Least, (int X, int Y) Most) Cells(Vector2 oneM, Vector2 otherM)
    {
        var (leastM, mostM) = (Vector2.Min(oneM, otherM), Vector2.Max(oneM, otherM));
        return (((int)MathF.Floor(leastM.X / cellM), (int)MathF.Floor(leastM.Y / cellM)),
            ((int)MathF.Floor(mostM.X / cellM), (int)MathF.Floor(mostM.Y / cellM)));
    }
}
