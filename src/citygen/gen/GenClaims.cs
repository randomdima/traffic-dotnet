using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// The ground the statics have taken, cell by cell, while a town is being laid. <b>It is not the plan's
/// terrain</b>: a building stands on grass and the map says so, so what a building occupies cannot be read
/// back off the cells and has to be remembered while the stages that place things run.
/// </summary>
/// <remarks>
/// <b>This is what makes GEN-3 hold by construction.</b> A slot claims its own footprint <em>and</em> the
/// walkable padding around it, and every later placement asks this before it stands anything — so a
/// building never lands on a building, a car park never overlaps one, and a prop is never inside either.
/// Nothing is ever placed and taken back. <b>The cells are the town's grid's</b> (SIM-8), at the level the
/// brief's own cell fits within, from the world's origin to its far edge. <b>A bit a cell</b>, because a traced city's
/// cells are tens of millions; a claim is written on one thread and read on any.
/// </remarks>
internal readonly struct GenClaims(ulong[] taken, GridWindow window)
{
    public static GenClaims Over(WorldGrid grid, Vector2 worldSizeM, float cellSizeM)
    {
        var level = grid.Within(cellSizeM);
        var window = GridWindow.Of(
            level, 0, 0, Math.Max(1, level.CellsAcross(worldSizeM.X)), Math.Max(1, level.CellsAcross(worldSizeM.Y)));
        return new GenClaims(new ulong[(window.Count + 63) / 64], window);
    }

    /// <summary>Whether every cell under a rectangle on a bearing is still free, the edge of the world counting as taken.</summary>
    public bool IsFree(Vector2 centreM, Vector2 axis, Vector2 halfExtentM)
    {
        var side = new Vector2(-axis.Y, axis.X);
        var stepM = window.Level.CellM * 0.5f;
        var (alongs, acrosses) = (Steps(halfExtentM.X, stepM), Steps(halfExtentM.Y, stepM));
        for (var along = 0; along <= alongs; along++)
        {
            for (var across = 0; across <= acrosses; across++)
            {
                var cell = CellAt(centreM + (axis * Sample(along, halfExtentM.X, stepM)) + (side * Sample(across, halfExtentM.Y, stepM)));
                if (cell < 0 || IsTaken(cell)) return false;
            }
        }

        return true;
    }

    public void Claim(Vector2 centreM, Vector2 axis, Vector2 halfExtentM)
    {
        var side = new Vector2(-axis.Y, axis.X);
        var stepM = window.Level.CellM * 0.5f;
        var (alongs, acrosses) = (Steps(halfExtentM.X, stepM), Steps(halfExtentM.Y, stepM));
        for (var along = 0; along <= alongs; along++)
        {
            for (var across = 0; across <= acrosses; across++)
            {
                var cell = CellAt(centreM + (axis * Sample(along, halfExtentM.X, stepM)) + (side * Sample(across, halfExtentM.Y, stepM)));
                if (cell >= 0) taken[cell >> 6] |= 1UL << cell;
            }
        }
    }

    /// <summary>
    /// How many half-cell steps cross a span of twice <paramref name="halfM"/>: the samples are one more, <b>the last held
    /// to the far edge</b>, so a span that is no whole number of steps is still asked at both its edges.
    /// </summary>
    static int Steps(float halfM, float stepM) => (int)MathF.Ceiling(halfM * 2f / stepM);

    static float Sample(int step, float halfM, float stepM) => MathF.Min(-halfM + (step * stepM), halfM);

    /// <summary>Whether every cell under a disc is still free — what a prop asks for the girth it keeps.</summary>
    public bool IsFree(Vector2 centreM, float radiusM)
    {
        var stepM = window.Level.CellM * 0.5f;
        var steps = Steps(radiusM, stepM);
        for (var down = 0; down <= steps; down++)
        {
            var alongM = Sample(down, radiusM, stepM);
            var acrossM = MathF.Sqrt(MathF.Max(0f, (radiusM * radiusM) - (alongM * alongM)));
            var across = Steps(acrossM, stepM);
            for (var over = 0; over <= across; over++)
            {
                var cell = CellAt(centreM + new Vector2(alongM, Sample(over, acrossM, stepM)));
                if (cell < 0 || IsTaken(cell)) return false;
            }
        }

        return true;
    }

    bool IsTaken(int cell) => (taken[cell >> 6] & (1UL << cell)) != 0;

    int CellAt(Vector2 pointM)
    {
        var (x, y) = window.Level.CellOf(pointM);
        return window.Holds(x, y) ? window.IndexOf(x, y) : -1;
    }
}
