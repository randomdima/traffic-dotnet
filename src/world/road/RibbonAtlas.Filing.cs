using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>One piece of a way's line as a device finds its points</b> (<c>atlas_points.glsl</c>): the piece, where it starts on its
/// way, how far either side of it a point is filed, and the box of lattice points it is asked over — what
/// <see cref="RibbonAtlas"/>' own search works out for it before it reads a point (<c>Find</c>). Row for row with the
/// kernel's struct, sixty-four bytes.
/// </summary>
/// <param name="StartUnit">The piece's own <see cref="ArcSeg.StartUnit"/>, so no heading is the device's trigonometry.</param>
/// <param name="WayFromM">Where on its way the piece begins: the lengths before it, summed in order as the host sums them.</param>
/// <param name="HalfM">Half the way's width and the reach (<see cref="RibbonAtlas.ReachOf"/>).</param>
/// <param name="FirstOfWay">The way's first piece, and one past its last: the pieces a point two of them read is weighed between.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct FilingPiece(
    Vector2 StartM, Vector2 StartUnit, float LengthM, float Curvature, float WayFromM, float HalfM, int Way, int FromRow,
    int ToRow, int FromColumn, int ToColumn, int FirstOfWay, int PastOfWay, float WayLengthM)
{
    /// <summary>How many rows of lattice points the piece is asked over: none where its box holds no column.</summary>
    public int Rows => FromColumn <= ToColumn && FromRow <= ToRow ? ToRow - FromRow + 1 : 0;
}

/// <summary>
/// <b>The atlas laid out for a device that files the points itself</b> (<c>DeviceAtlas</c>): every piece of every way the
/// atlas files and the rows each is asked over, and the frame the points are filed into — <see cref="RibbonAtlas"/>' own
/// search and filing as data, so the device finds and files the points the host would.
/// </summary>
internal sealed class AtlasFiling
{
    public required GridLevel Level { get; init; }

    /// <summary>The main cells the ribbons reach.</summary>
    public required GridWindow Cells { get; init; }

    /// <summary>Every way's length, which a point's metre is held to.</summary>
    public required float[] LengthM { get; init; }

    /// <summary>Every way's channels, or none where every way is on the ground's alone.</summary>
    public required byte[] WayChannels { get; init; }

    /// <summary>Every piece of every way filed, a way's pieces in order and the ways in order.</summary>
    public required FilingPiece[] Pieces { get; init; }

    /// <summary>Where each piece's rows begin among all of them, one past the last piece's the total.</summary>
    public required int[] RowFirst { get; init; }

    /// <summary>How many halvings a main cell is cut into to reach the points.</summary>
    public int Shift => Level.Depth - Cells.Level.Depth;

    public int Mask => (1 << Shift) - 1;

    public int StartShift => RibbonAtlas.StartShiftOf(Shift);

    /// <summary>How many starts a kept cell has, one past the last run's included.</summary>
    public int StartsPerCell => RibbonAtlas.StartsPerCellOf(Shift);

    /// <summary>How many runs of points a cell has, each one start's.</summary>
    public int RunsPerCell => StartsPerCell - 1;

    /// <summary>How far past its way's band a point is still filed under it (<see cref="RibbonAtlas.ReachOf"/>).</summary>
    public float ReachM => RibbonAtlas.ReachOf(Level.CellM);

    /// <summary>The table a device keeps, but its starts and entries, which the device files itself.</summary>
    public AtlasTable FrameOf(int[] cellSlot, int[] slotFirst) => new()
    {
        Level = Level,
        Cells = Cells,
        Shift = Shift,
        Mask = Mask,
        StartShift = StartShift,
        RowStartsShift = Shift - StartShift,
        StartsPerCell = StartsPerCell,
        MetresPerOutside = ReachM / RibbonAtlas.OutsidePerReach,
        AlongPerMetre = RibbonAtlas.AlongPerMetre,
        CellSlot = cellSlot,
        SlotFirst = slotFirst,
        PointStart = [],
        Entries = [],
        LengthM = LengthM,
        WayChannels = WayChannels,
    };
}

/// <summary>
/// <b>The town's furniture an atlas a device files is checked against</b> (TER-4c.4): every prop's disc, and which ways the
/// traffic drives — what <c>TownWorld.RefuseFurnitureOnTheRoad</c> asks of the host's own atlas.
/// </summary>
internal readonly record struct AtlasFurniture(Vector2[] CentreM, float[] RadiusM, int Count, bool[] DrivenWay);

/// <summary>
/// <b>Where an atlas is filed by a device that keeps it</b> (<see cref="AtlasFiling"/>): begun here and finished on the device's
/// own time, so a page that cannot wait for it goes on laying the town.
/// </summary>
internal interface IAtlasFiler
{
    /// <summary>The atlas begun on the device, and the furniture checked against it once it is filed.</summary>
    void File(AtlasFiling filing, AtlasFurniture furniture);
}

internal sealed partial class RibbonAtlas
{
    /// <summary>
    /// <b>The atlas laid out for a device to file</b> (<see cref="AtlasFiling"/>): the frame <see cref="Lay"/> files into, and
    /// every piece of every way it files with the box its search reads (<c>Find</c>).
    /// </summary>
    public static AtlasFiling Filing(IRibbonLines lines, GridLevel level)
    {
        var (lengthM, cells) = Framed(lines, level);
        var reachM = ReachOf(level.CellM);

        var count = 0;
        for (var way = 0; way < lines.WayCount; way++)
        {
            if (Files(lines, way, lengthM[way])) count += lines.LineOf(way, out _).Length;
        }

        var pieces = new FilingPiece[count];
        var at = 0;
        for (var way = 0; way < lines.WayCount; way++)
        {
            if (!Files(lines, way, lengthM[way])) continue;

            var arcs = lines.LineOf(way, out var widthM);
            var halfM = (widthM * 0.5f) + reachM;
            var first = at;
            var startM = 0f;
            foreach (var arc in arcs)
            {
                var leastM = new Vector2(float.MaxValue);
                var mostM = new Vector2(float.MinValue);
                Bound(arc, halfM, ref leastM, ref mostM);
                pieces[at++] = new FilingPiece(
                    arc.StartM, arc.StartUnit, arc.LengthM, arc.Curvature, startM, halfM, way,
                    level.FirstMiddleFrom(leastM.Y), level.LastMiddleTo(mostM.Y), level.FirstMiddleFrom(leastM.X),
                    level.LastMiddleTo(mostM.X), first, first + arcs.Length, lengthM[way]);
                startM += arc.LengthM;
            }
        }

        var rowFirst = new int[count + 1];
        for (var piece = 0; piece < count; piece++) rowFirst[piece + 1] = checked(rowFirst[piece] + pieces[piece].Rows);

        return new AtlasFiling
        {
            Level = level,
            Cells = cells,
            LengthM = lengthM,
            WayChannels = ChannelsOf(lines),
            Pieces = pieces,
            RowFirst = rowFirst,
        };
    }

    /// <summary>
    /// Whether a way is filed at all: one with ground to file. <b>A walk is filed in a town nobody walks</b>, because a
    /// wheel on it grips as on any way (TER-7b).
    /// </summary>
    static bool Files(IRibbonLines lines, int way, float lengthM)
    {
        var arcs = lines.LineOf(way, out var widthM);
        return arcs.Length > 0 && widthM > 0f && lengthM > 0f;
    }
}
