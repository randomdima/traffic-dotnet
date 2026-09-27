using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// What the broad phase's grid says about the lattice it laid — the shape the overlay draws it by (OBS-2x),
/// and the box-stamping that makes a cell of it a cell the bodies in it actually reach.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class CellGridTests
{
    /// <summary>A level of the grid a body can be wider than.</summary>
    static readonly GridLevel Level = new WorldGrid(8f).Split(2);

    /// <summary>
    /// Four boxes over ground of an awkward corner: one inside a cell, one crossing a boundary, one wider
    /// than a cell in both directions, and one off on its own so the set spans more than it fills.
    /// </summary>
    static CellGrid Laid(out Vector2[] leastM, out Vector2[] mostM)
    {
        Vector2[] centresM = [new(13.7f, -6.2f), new(19.9f, -5.1f), new(21.3f, 3.4f), new(48.6f, 11.8f)];
        Vector2[] halfM = [new(1.2f, 0.9f), new(1.8f, 1.1f), new(6.5f, 5.25f), new(0.4f, 0.4f)];

        leastM = new Vector2[centresM.Length];
        mostM = new Vector2[centresM.Length];
        for (var body = 0; body < centresM.Length; body++)
        {
            leastM[body] = centresM[body] - halfM[body];
            mostM[body] = centresM[body] + halfM[body];
        }

        var grid = new CellGrid();
        grid.Rebuild([0, 1, 2, 3], leastM, mostM, Level);
        return grid;
    }

    /// <summary>
    /// <b>Every body's box lies inside the lattice the grid reports.</b> It is what a picture of the grid
    /// stands on: a body outside the reported extent is a body in a cell nothing draws, so the layer would
    /// show an empty town while the solver was busy.
    /// </summary>
    [Fact]
    public void TheReportedLatticeCoversEveryBodyItIndexed()
    {
        var window = Laid(out var leastM, out var mostM).Window;

        for (var body = 0; body < leastM.Length; body++)
        {
            Assert.True(
                leastM[body].X >= window.LeastM.X && leastM[body].Y >= window.LeastM.Y,
                $"body {body} starts before the lattice");
            Assert.True(
                mostM[body].X <= window.MostM.X && mostM[body].Y <= window.MostM.Y,
                $"body {body} reaches past the lattice");
        }
    }

    /// <summary>
    /// <b>A cell that names a body is ground that body's box reaches.</b> Asked of the boxes and not of the
    /// arithmetic that binned them: a wash drawn on a cell says a query asked there would find what the cell
    /// names, and a cell naming a body a metre away is a picture that sends a reader after a bug in the
    /// solver.
    /// </summary>
    [Fact]
    public void ACellNamesOnlyBodiesWhoseBoxReachesIt()
    {
        var grid = Laid(out var leastM, out var mostM);
        var window = grid.Window;

        for (var y = window.FromY; y <= window.ToY; y++)
        {
            for (var x = window.FromX; x <= window.ToX; x++)
            {
                var cellLeastM = Level.CornerM(x, y);
                var cellMostM = Level.CornerM(x + 1, y + 1);
                foreach (var body in grid.Items(x, y))
                {
                    Assert.False(
                        mostM[body].X < cellLeastM.X || leastM[body].X > cellMostM.X ||
                        mostM[body].Y < cellLeastM.Y || leastM[body].Y > cellMostM.Y,
                        $"cell {x},{y} names body {body}, which is nowhere near it");
                }
            }
        }
    }

    /// <summary>
    /// <b>And a body wider than a cell is in every cell it covers, not in the one its centre falls in.</b>
    /// That is the whole difference between this grid and the bucket index beside it, and it is the reading
    /// the picture of it is opened for: a lorry across four cells is work in four cells.
    /// </summary>
    [Fact]
    public void ABodyWiderThanACellIsInEveryCellItCovers()
    {
        var grid = Laid(out var leastM, out var mostM);
        var window = grid.Window;
        const int wide = 2;

        var across = Level.CellsAcross(mostM[wide].X - leastM[wide].X);
        var down = Level.CellsAcross(mostM[wide].Y - leastM[wide].Y);

        var cells = 0;
        for (var y = window.FromY; y <= window.ToY; y++)
        {
            for (var x = window.FromX; x <= window.ToX; x++)
            {
                if (grid.Items(x, y).IndexOf(wide) >= 0) cells++;
            }
        }

        Assert.True(cells >= across * down, $"a box {across} by {down} cells wide reached {cells} of them");
    }
}
