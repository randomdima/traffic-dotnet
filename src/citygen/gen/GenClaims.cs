using System.Numerics;

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
/// Nothing is ever placed and taken back.
/// </remarks>
internal readonly struct GenClaims(bool[] taken, int gridWidth, int gridHeight, float cellSizeM)
{
    public static GenClaims Over(GenRaster raster) =>
        new(new bool[raster.Width * raster.Height], raster.Width, raster.Height, raster.CellSizeM);

    /// <summary>Whether every cell under a rectangle on a bearing is still free, the edge of the world counting as taken.</summary>
    public bool IsFree(Vector2 centreM, Vector2 axis, Vector2 halfExtentM)
    {
        var side = new Vector2(-axis.Y, axis.X);
        var stepM = cellSizeM * 0.5f;
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
        var stepM = cellSizeM * 0.5f;
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
        var stepM = cellSizeM * 0.5f;
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
        var x = (int)MathF.Floor(pointM.X / cellSizeM);
        var y = (int)MathF.Floor(pointM.Y / cellSizeM);
        return x < 0 || y < 0 || x >= gridWidth || y >= gridHeight ? -1 : (y * gridWidth) + x;
    }
}
