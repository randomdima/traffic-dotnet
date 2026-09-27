using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The lattice the town's geometry is asked over</b> (OBS-2r): the cells of the grid every driven line
/// is indexed on (<see cref="ChainIndex"/>), and how many of those lines each cell holds.
/// </summary>
internal sealed partial class DebugOverlay
{
    /// <summary>
    /// <b>A hairline, and thinner than the wireframe's</b> — a cell edge is not a cut in anything and is
    /// read as the ruling behind the picture, so it has to be visible without being a thing to follow.
    /// </summary>
    const float GridLineM = WireLineM;

    /// <summary>Held to a pixel on screen, on the same terms the wireframe is: under it a ruling dots.</summary>
    const float GridLineFloorPx = 1f;

    /// <summary>
    /// <b>How large a cell has to come out on the glass before the lattice is drawn at all.</b> Under it the
    /// ruling is denser than the thing it is ruled over and says nothing about which cell anything is in;
    /// at a town-wide framing a city's cells are a wash that costs the whole buffer. Pulling the camera in
    /// is what this layer is read at, and pulling it out thins the picture rather than filling it.
    /// </summary>
    const float LeastCellPx = 8f;

    /// <summary>
    /// How many cells are drawn at one framing. <b>The cache's own room, three quads a cell</b> — a wash, and
    /// the two edges the cell owns — so a frame that would overrun it draws nothing instead of a lattice cut
    /// off down the middle of the view.
    /// </summary>
    const int MostCellsDrawn = TownQuadCapacity / 3;

    /// <summary>
    /// <b>Every cell of the grid in view, and the wash that says how busy it is.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the index's own window of the grid and not one laid again here</b> (SIM-8). An index steps a
    /// level coarser where a set is spread too far for the one it was asked for, so a lattice drawn from a
    /// level named here is a picture of cells nothing is asked over — which is the same rule the rest of this
    /// slice keeps: read the producer, never a copy of its shape.
    /// </para>
    /// <para>
    /// <b>The wash is scaled to the busiest cell in the frame</b> and not to a figure. What the picture is
    /// opened for is where the lines crowd — a junction with six movements over it, a car park whose bays
    /// all reach into one cell — and crowding is a comparison rather than a count, so the darkest cell on
    /// screen is the one to look at whatever framing it is read at. A fixed scale would say "dark
    /// everywhere" over a city and "empty everywhere" over a lane.
    /// </para>
    /// <para>
    /// <b>Every driven line the town has</b> (<see cref="CityGen.Paving.DrivenLines"/>) — the lanes, the
    /// movements through every box and the ways into every bay. <b>The lanes' own index would be the wrong
    /// picture</b>, and it read as one: drawn over a car park, the cells the bays' ways run through came back
    /// unwashed beside the very lines they hold, because "which lane is this car on" is a question that may
    /// not answer with a bay's way and that index holds lanes alone. A picture of a grid has to be a picture
    /// of everything binned into it, or the empty cell is the layer's and not the town's.
    /// </para>
    /// </remarks>
    static void Grid(
        ref ScreenDraw draw, TownWorld world, SimConfig config, Vector2 viewCentreM, Vector2 viewSpanM,
        float pixelsPerMetre)
    {
        var grid = world.Plan.Paving(config).DrivenLines(config);
        var window = grid.Window;
        var cellM = window.Level.CellM;
        if (cellM * pixelsPerMetre < LeastCellPx) return;

        var halfSpanM = viewSpanM * 0.5f;
        if (!window.TryOverlap(viewCentreM - halfSpanM, viewCentreM + halfSpanM, out var inView)) return;

        var across = inView.ToX - inView.FromX + 1;
        var down = inView.ToY - inView.FromY + 1;
        if ((long)across * down > MostCellsDrawn) return;

        // The busiest cell in the frame, read before anything is drawn: the wash is a comparison and the
        // thing it is compared against has to be the whole of what is on screen.
        var mostLines = 0;
        for (var y = inView.FromY; y <= inView.ToY; y++)
        {
            for (var x = inView.FromX; x <= inView.ToX; x++) mostLines = Math.Max(mostLines, grid.ChainsInCell(x, y));
        }

        var lineM = MathF.Max(GridLineM, GridLineFloorPx / pixelsPerMetre);
        for (var y = inView.FromY; y <= inView.ToY; y++)
        {
            for (var x = inView.FromX; x <= inView.ToX; x++)
            {
                var cornerM = window.Level.CornerM(x, y);
                var lines = grid.ChainsInCell(x, y);
                if (lines > 0 && mostLines > 0)
                {
                    // One band the cell's own width, which is a filled square in a single quad.
                    var middleM = cornerM + new Vector2(cellM * 0.5f);
                    var wash = Theme.GeometryGridCell;
                    var share = LeastWash + ((1f - LeastWash) * Crowding(lines, mostLines));
                    draw.BandM(
                        middleM - new Vector2(cellM * 0.5f, 0f), middleM + new Vector2(cellM * 0.5f, 0f), 0f,
                        cellM, new Vector4(wash.X, wash.Y, wash.Z, wash.W * share));
                }

                // <b>Each cell draws the two edges it owns</b> — the one along its low side and the one up
                // its low edge — so a shared edge is drawn once and the last row's far edges are the only
                // ones missing, which is the frame's boundary rather than the grid's.
                draw.LineM(cornerM, cornerM + new Vector2(cellM, 0f), lineM, Theme.GeometryGrid);
                draw.LineM(cornerM, cornerM + new Vector2(0f, cellM), lineM, Theme.GeometryGrid);
                if (draw.Full) return;
            }
        }
    }

    /// <summary>
    /// <b>What a cell holding one line is washed at, as a share of what the busiest one is.</b> A half,
    /// which is a floor and not a scale: <em>whether a cell holds anything at all</em> is the half of the
    /// reading that says where a query looks, so an occupied cell has to be unmistakable before the
    /// comparison between two occupied ones gets any of the range.
    /// </summary>
    /// <remarks>
    /// <b>It was a third and that was measured, not guessed.</b> A cell holding one lane beside a car park's
    /// sixteen came out at a tenth of an alpha over textured grass — present, and invisible to the eye
    /// reading the picture, which is the same thing as absent. The layer was read as drawing the wrong index
    /// twice over before the cells were counted and found to be right.
    /// </remarks>
    const float LeastWash = 0.5f;

    /// <summary>
    /// <b>How crowded one cell is against the busiest in the frame, on a log scale</b> — nought where it
    /// holds one line and one where it is the busiest.
    /// </summary>
    /// <remarks>
    /// <b>Counts of lines in a cell span orders of magnitude and a linear scale is unreadable across them.</b>
    /// A car park puts every way into its bays through two or three cells — forty-odd lines — and the street
    /// it hangs off has three; read linearly that street is drawn at a fortieth of the wash, which is to say
    /// at the floor, and a picture of a town came back with the car parks lit and every road looking like
    /// ground no line is indexed over. That was read as the layer drawing the wrong index and it was the
    /// scale. Logarithmically the same street reads at a third of the way up, which is what it is.
    /// </remarks>
    static float Crowding(int lines, int mostLines) =>
        mostLines <= 1 ? 1f : MathF.Log(lines) / MathF.Log(mostLines);
}
