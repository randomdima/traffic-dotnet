using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// <b>The town's furniture</b>: every prop a disc and every part of a building a box, laid once off the plan and never
/// moved. Both layers read it off the same laying and neither sends it to the other (root <c>decision-log.md</c>,
/// 2026-10-11): a world stands its solver's statics off it (<see cref="PhysicsWorld.AddStatics"/>), and the actor
/// layer asks it where a body may be put down (<see cref="InBox"/>).
/// </summary>
internal sealed class StaticBodies(GridLevel level)
{
    Vector2[] _centreM = new Vector2[Room];
    float[] _headingRad = new float[Room];
    Vector2[] _rotation = new Vector2[Room];
    Vector2[] _extentM = new Vector2[Room];
    float[] _cornerRadiusM = new float[Room];
    Bounds[] _bounds = new Bounds[Room];
    int[] _rows = [];
    readonly CellGrid _grid = new();
    bool _stale;

    const int Room = 1024;

    public int Count { get; private set; }

    /// <summary>A prop: a disc, the canopy it is.</summary>
    public void AddDisc(Vector2 centreM, float radiusM) => Add(centreM, 0f, Vector2.Zero, radiusM);

    /// <summary>
    /// One part of a building, turned the way the plan turned it, its corners rounded at <paramref name="cornerRadiusM"/>
    /// inside the size given.
    /// </summary>
    public void AddBox(Vector2 centreM, Vector2 sizeM, float headingRad, float cornerRadiusM = 0f) => Add(
        centreM, headingRad, Vector2.Max((sizeM * 0.5f) - new Vector2(cornerRadiusM), Vector2.Zero), cornerRadiusM);

    /// <param name="extentM">The half-extents of the shape's core, which <paramref name="cornerRadiusM"/> is rolled around (SOL-1).</param>
    void Add(Vector2 centreM, float headingRad, Vector2 extentM, float cornerRadiusM)
    {
        if (Count == _centreM.Length) Grow();

        var row = Count++;
        _centreM[row] = centreM;
        _headingRad[row] = headingRad;
        _rotation[row] = Shape.Rotation(headingRad);
        _extentM[row] = extentM;
        _cornerRadiusM[row] = cornerRadiusM;
        var half = Shape.HalfBoundsM(_rotation[row], extentM, cornerRadiusM);
        _bounds[row] = new Bounds(centreM - half, centreM + half);
        _stale = true;
    }

    /// <summary>Every row stood in a solver's table, in the order laid, and its grid settled.</summary>
    internal void StandIn(PhysicsWorld world)
    {
        for (var row = 0; row < Count; row++)
        {
            world.AddStatic(_centreM[row], _headingRad[row], _extentM[row], _cornerRadiusM[row]);
        }
    }

    /// <summary>
    /// Whether any of the furniture stands inside an axis-aligned box — asked where a body is to be put down, never in a
    /// tick, so it walks the grid rather than keeping an answer anything has to maintain.
    /// </summary>
    /// <remarks>
    /// <b>The furniture's own shape, and never its bounds</b>: a building turned off the grid is bounded by a box far
    /// bigger than it, and that box covers the pavement at its own door.
    /// </remarks>
    public bool InBox(Vector2 leastM, Vector2 mostM)
    {
        if (_stale) Settle();

        if (!_grid.TryRange(leastM, mostM, out var range)) return false;

        var centreM = (leastM + mostM) * 0.5f;
        var halfM = (mostM - leastM) * 0.5f;
        var square = Shape.Rotation(0f);
        for (var y = range.FromY; y <= range.ToY; y++)
        {
            for (var x = range.FromX; x <= range.ToX; x++)
            {
                foreach (var row in _grid.Items(x, y))
                {
                    if (_bounds[row].Misses(leastM, mostM)) continue;

                    if (Shape.Collide(
                            centreM, square, halfM, 0f, _centreM[row], _rotation[row], _extentM[row], _cornerRadiusM[row],
                            0f, out _))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    void Settle()
    {
        if (_rows.Length != Count)
        {
            _rows = new int[Count];
            for (var row = 0; row < Count; row++) _rows[row] = row;
        }

        _grid.Rebuild(_rows, _bounds, level);
        _grid.ForgetTheRebuild();
        _stale = false;
    }

    void Grow()
    {
        var room = _centreM.Length * 2;
        Array.Resize(ref _centreM, room);
        Array.Resize(ref _headingRad, room);
        Array.Resize(ref _rotation, room);
        Array.Resize(ref _extentM, room);
        Array.Resize(ref _cornerRadiusM, room);
        Array.Resize(ref _bounds, room);
    }
}
