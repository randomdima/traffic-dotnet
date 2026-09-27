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
/// brief's own cell fits within, from the world's origin to its far edge.
/// </remarks>
internal readonly struct GenClaims(bool[] taken, GridWindow window)
{
    public static GenClaims Over(WorldGrid grid, Vector2 worldSizeM, float cellSizeM)
    {
        var level = grid.Within(cellSizeM);
        var window = GridWindow.Of(
            level, 0, 0, Math.Max(1, level.CellsAcross(worldSizeM.X)), Math.Max(1, level.CellsAcross(worldSizeM.Y)));
        return new GenClaims(new bool[window.Count], window);
    }

    /// <summary>Whether every cell under a rectangle on a bearing is still free, the edge of the world counting as taken.</summary>
    public bool IsFree(Vector2 centreM, Vector2 axis, Vector2 halfExtentM)
    {
        var side = new Vector2(-axis.Y, axis.X);
        var stepM = window.Level.CellM * 0.5f;
        for (var alongM = -halfExtentM.X; alongM <= halfExtentM.X; alongM += stepM)
        {
            for (var acrossM = -halfExtentM.Y; acrossM <= halfExtentM.Y; acrossM += stepM)
            {
                var cell = CellAt(centreM + (axis * alongM) + (side * acrossM));
                if (cell < 0 || taken[cell]) return false;
            }
        }

        return true;
    }

    public void Claim(Vector2 centreM, Vector2 axis, Vector2 halfExtentM)
    {
        var side = new Vector2(-axis.Y, axis.X);
        var stepM = window.Level.CellM * 0.5f;
        for (var alongM = -halfExtentM.X; alongM <= halfExtentM.X; alongM += stepM)
        {
            for (var acrossM = -halfExtentM.Y; acrossM <= halfExtentM.Y; acrossM += stepM)
            {
                var cell = CellAt(centreM + (axis * alongM) + (side * acrossM));
                if (cell >= 0) taken[cell] = true;
            }
        }
    }

    /// <summary>Whether every cell under a disc is still free — what a prop asks for the girth it keeps.</summary>
    public bool IsFree(Vector2 centreM, float radiusM)
    {
        var stepM = window.Level.CellM * 0.5f;
        var steps = (int)MathF.Ceiling(radiusM * 2f / stepM);
        for (var down = 0; down <= steps; down++)
        {
            var alongM = MathF.Min(-radiusM + (down * stepM), radiusM);
            var acrossM = MathF.Sqrt(MathF.Max(0f, (radiusM * radiusM) - (alongM * alongM)));
            var across = (int)MathF.Ceiling(acrossM * 2f / stepM);
            for (var over = 0; over <= across; over++)
            {
                var cell = CellAt(
                    centreM + new Vector2(alongM, MathF.Min(-acrossM + (over * stepM), acrossM)));
                if (cell < 0 || taken[cell]) return false;
            }
        }

        return true;
    }

    int CellAt(Vector2 pointM)
    {
        var (x, y) = window.Level.CellOf(pointM);
        return window.Holds(x, y) ? window.IndexOf(x, y) : -1;
    }
}
