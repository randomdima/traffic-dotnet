using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The lattice the solver's bodies are binned into</b> (OBS-2x): the cells of the two grids its broad
/// phase asks — the town's furniture and the roster the last step integrated — and how many bodies each of
/// them holds.
/// </summary>
/// <remarks>
/// It is the other half of the answer the geometry grid gives (OBS-2r), asked of the other index: that one
/// says which lines a question about the road is narrowed to, and this says which bodies a question about a
/// collision is. <b>They are two stores on one grid</b> (SIM-8), so at the main level their rulings fall on
/// the same lines, and a cell washed in both is one square of ground both indexes are asked about.
/// </remarks>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// <b>Only the cells the index actually holds something in are drawn.</b> That is not a thinning of the
    /// picture for the picture's sake: a cell of either grid is live only while its stamp matches the
    /// current rebuild (<see cref="CellGrid"/>), so an empty cell is not a cell of the index at all and a
    /// ruling through it would be a lattice this layer laid rather than one the solver asks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The static half is cached with the town and the moving half is laid every frame.</b> The furniture
    /// is indexed once, when the last static body has been added, and the roster is reindexed twice a step —
    /// a cell of it copied out of the cache would be ground the traffic left several frames ago.
    /// </para>
    /// <para>
    /// <b>Each cell draws the edges no live neighbour has already drawn</b>: its two low edges always, and a
    /// high edge only where the cell beyond it holds nothing. A shared edge drawn from both sides is two
    /// hairlines in the same place, which at this weight is a seam brighter than the boundary of the block —
    /// and the boundary of the block is the reading.
    /// </para>
    /// </remarks>
    static void SolverCells(
        ref ScreenDraw draw, CellGrid grid, Vector4 wash, Vector4 edge, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var window = grid.Window;
        var cellM = window.Level.CellM;
        if (cellM * pixelsPerMetre < LeastCellPx) return;

        var halfSpanM = viewSpanM * 0.5f;
        if (!window.TryOverlap(viewCentreM - halfSpanM, viewCentreM + halfSpanM, out var inView)) return;

        if ((long)(inView.ToX - inView.FromX + 1) * (inView.ToY - inView.FromY + 1) > MostCellsDrawn) return;

        // The busiest cell in the frame, on the same terms as the geometry grid's wash (OBS-2r): crowding is
        // a comparison, and a fixed scale would read as empty everywhere over a street.
        var mostBodies = 0;
        for (var y = inView.FromY; y <= inView.ToY; y++)
        {
            for (var x = inView.FromX; x <= inView.ToX; x++) mostBodies = Math.Max(mostBodies, grid.Items(x, y).Length);
        }

        if (mostBodies == 0) return;

        var lineM = MathF.Max(GridLineM, GridLineFloorPx / pixelsPerMetre);
        for (var y = inView.FromY; y <= inView.ToY; y++)
        {
            for (var x = inView.FromX; x <= inView.ToX; x++)
            {
                var bodies = grid.Items(x, y).Length;
                if (bodies == 0) continue;

                var cornerM = window.Level.CornerM(x, y);
                var middleM = cornerM + new Vector2(cellM * 0.5f);
                var share = LeastWash + ((1f - LeastWash) * Crowding(bodies, mostBodies));
                draw.BandM(
                    middleM - new Vector2(cellM * 0.5f, 0f), middleM + new Vector2(cellM * 0.5f, 0f), 0f, cellM,
                    new Vector4(wash.X, wash.Y, wash.Z, wash.W * share));

                draw.LineM(cornerM, cornerM + new Vector2(cellM, 0f), lineM, edge);
                draw.LineM(cornerM, cornerM + new Vector2(0f, cellM), lineM, edge);
                var farM = cornerM + new Vector2(cellM);
                if (!Holds(grid, x + 1, y)) draw.LineM(cornerM + new Vector2(cellM, 0f), farM, lineM, edge);
                if (!Holds(grid, x, y + 1)) draw.LineM(cornerM + new Vector2(0f, cellM), farM, lineM, edge);
                if (draw.Full) return;
            }
        }
    }

    /// <summary>The town's furniture as the solver bins it, which does not move once the town is laid.</summary>
    static void SolverStatics(
        ref ScreenDraw draw, TownWorld world, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre) =>
        SolverCells(
            ref draw, world.PhysicsForInstruments.StaticIndex, Theme.SolverStaticCell, Theme.SolverStaticEdge,
            viewCentreM, viewSpanM, pixelsPerMetre);

    /// <summary>
    /// And the roster the last step integrated, which is the half a frame pays for. <b>A body standing
    /// inside a container or otherwise switched off is in neither picture</b>, because it is in neither
    /// index: what the layer draws is the work the step did and not the town's census.
    /// </summary>
    static void SolverMovers(
        ref ScreenDraw draw, TownWorld world, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre) =>
        SolverCells(
            ref draw, world.PhysicsForInstruments.MovingIndex, Theme.SolverMovingCell, Theme.SolverMovingEdge,
            viewCentreM, viewSpanM, pixelsPerMetre);

    /// <summary>Whether a cell of this grid is on it at all and holds anything, which is what decides a shared edge.</summary>
    static bool Holds(CellGrid grid, int x, int y) => grid.Items(x, y).Length > 0;
}
