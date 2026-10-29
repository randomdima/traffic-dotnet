using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>A town's blocks</b>: the ground inside each closed ring of the walk's outer face (<see cref="GroundRings.WalkEdge"/>)
/// that runs round it the way a block's does — the other way to the town's own outside — as outlines walked a step apart
/// and filed by square, so a place can be asked which block it stands in.
/// </summary>
/// <remarks>
/// <b>A block is what the streets make</b>, so it is known only once the walk is laid; what is asked of it before is asked of
/// a district or a zone.
/// </remarks>
internal sealed class Blocks
{
    /// <summary>
    /// How finely a block's face is walked for its outline: the outline's resolution, which only says how near it
    /// follows the face, and not a figure of the town.
    /// </summary>
    public const float StepM = 5f;

    /// <summary>How wide a square the outlines are filed by: a block or so.</summary>
    const float SquareM = 100f;

    readonly Dictionary<(int, int), List<int>> _bySquare = [];

    Blocks(List<Vector2[]> outlines)
    {
        Outlines = outlines;
        for (var block = 0; block < outlines.Count; block++)
        {
            var (leastM, mostM) = Polygons.Box(outlines[block]);
            for (var y = Square(leastM.Y); y <= Square(mostM.Y); y++)
            {
                for (var x = Square(leastM.X); x <= Square(mostM.X); x++)
                {
                    if (!_bySquare.TryGetValue((x, y), out var filed)) _bySquare[(x, y)] = filed = [];
                    filed.Add(block);
                }
            }
        }
    }

    /// <summary>Each block's outline, its points a step apart along the face.</summary>
    public List<Vector2[]> Outlines { get; }

    public static Blocks Of(GroundRings rings)
    {
        var outlines = new List<Vector2[]>();
        foreach (var face in rings.WalkEdge)
        {
            var points = Along(face);
            if (points.Count >= 3 && Polygons.SignedAreaM2(points.ToArray()) < 0f) outlines.Add([.. points]);
        }

        return new Blocks(outlines);
    }

    /// <summary>The block a place stands in, or −1 where it stands in none — on a street, or outside the town.</summary>
    public int At(Vector2 atM)
    {
        if (!_bySquare.TryGetValue((Square(atM.X), Square(atM.Y)), out var filed)) return -1;

        foreach (var block in filed)
        {
            if (Polygons.Encloses(Outlines[block], atM)) return block;
        }

        return -1;
    }

    static int Square(float m) => (int)MathF.Floor(m / SquareM);

    static List<Vector2> Along(ReadOnlySpan<ArcSeg> arcs)
    {
        var points = new List<Vector2>();
        var totalM = Spline.TotalLengthM(arcs);
        for (var atM = StepM * 0.5f; atM < totalM; atM += StepM) points.Add(Spline.SampleAt(arcs, atM).PositionM);
        return points;
    }
}
