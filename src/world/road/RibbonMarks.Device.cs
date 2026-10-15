using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Road;

/// <summary>One piece of a way's line as <c>ribbon_marks.comp</c> reads it (<see cref="RibbonPiece.ForTheDevice"/>), field for field.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MarkPiece(
    Vector2 StartM, float HeadingRad, float Curvature, float HeadingCos, float HeadingSin, float LengthM, float HalfM,
    Vector2 LeastM, Vector2 MostM, int Way, float WayFromM, uint Straight, float Pad);

/// <summary>What two pieces of two ways share, in each way's own metres, as a device appends it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MarkRow(int One, int Other, float OneFromM, float OneToM, float OtherFromM, float OtherToM);

/// <summary>
/// <b>What a device is handed to work the marks out on</b> (<see cref="RibbonMarks.Filing"/>): every piece, the cells they
/// are filed under, and which cell each entry is of — the pairs the host would weigh, to be weighed there.
/// </summary>
internal sealed class MarkFiling
{
    public required MarkPiece[] Pieces { get; init; }

    /// <summary>Where each cell's entries begin, and one past the last cell.</summary>
    public required uint[] CellFirst { get; init; }

    /// <summary>Each cell's pieces, in piece order.</summary>
    public required uint[] InCell { get; init; }

    /// <summary>The cell each entry is of.</summary>
    public required uint[] EntryCell { get; init; }

    /// <summary>Every way's channels, four ways a word — empty where every way is on the ground's alone.</summary>
    public required uint[] Channels { get; init; }

    public required GridWindow Window { get; init; }

    /// <summary>How far apart the metres a piece is walked at stand.</summary>
    public required float StepM { get; init; }

    /// <summary>How deep inside both two ribbons their ground has to lie to be shared.</summary>
    public required float TouchM { get; init; }
}

/// <summary>
/// <b>A device that works the marks out</b> (<c>ribbon_marks.comp</c>): begun as the town is laid, and answered as the
/// device does — at once on a head that waits, as frames go by on a page.
/// </summary>
internal interface IMarker
{
    IMarking Mark(MarkFiling filing);
}

/// <summary>One working out of the marks in flight.</summary>
internal interface IMarking : IDisposable
{
    /// <summary>Whether the rows are in, taken on as far as the device has answered — waited for where the head can wait.</summary>
    bool Answered();

    /// <summary>What every pair of pieces was found to share, in whatever order the device found them.</summary>
    ReadOnlySpan<MarkRow> Rows { get; }
}

internal static partial class RibbonMarks
{
    /// <summary>
    /// <b>The marks' pairs laid out for a device to weigh</b> (<see cref="IMarker"/>): every piece, filed under the cells
    /// its box covers as <see cref="Of"/> files them, so the device weighs the pairs the host would.
    /// </summary>
    public static MarkFiling Filing(IRibbonLines lines, float stepM, float touchM, GridLevel nearLevel)
    {
        var pieces = PiecesOf(lines);
        var near = new Near(pieces, nearLevel);
        var devicePieces = new MarkPiece[pieces.Length];
        for (var at = 0; at < pieces.Length; at++) devicePieces[at] = pieces[at].ForTheDevice();

        var cellFirst = MemoryMarshal.Cast<int, uint>(near.CellFirst).ToArray();
        var entryCell = new uint[near.InCell.Length];
        for (var cell = 0; cell + 1 < cellFirst.Length; cell++)
        {
            entryCell.AsSpan((int)cellFirst[cell], (int)(cellFirst[cell + 1] - cellFirst[cell])).Fill((uint)cell);
        }

        var channels = RibbonAtlas.ChannelsOf(lines);
        var packed = new uint[(channels.Length + 3) / 4];
        for (var way = 0; way < channels.Length; way++) packed[way >> 2] |= (uint)channels[way] << ((way & 3) << 3);

        OpenWatch.Passed($"town: the marks' {pieces.Length} pieces and their cells");
        return new MarkFiling
        {
            Pieces = devicePieces,
            CellFirst = cellFirst,
            InCell = MemoryMarshal.Cast<int, uint>(near.InCell).ToArray(),
            EntryCell = entryCell,
            Channels = packed,
            Window = near.Window,
            StepM = stepM,
            TouchM = touchM,
        };
    }

    /// <summary>The marks off what a device found every pair of pieces to share, merged and filed as <see cref="Of"/> files them.</summary>
    /// <remarks>
    /// <b>In whatever order the device found them</b>: a pair's section is the least and most of its shares, and each way's
    /// sections are put in order once filed, so the marks do not turn on which thread found a pair first.
    /// </remarks>
    public static WayCrossings OfRows(IRibbonLines lines, ReadOnlySpan<MarkRow> rows, float[] lengthM)
    {
        var pairing = new Pairing();
        foreach (var row in rows) Share(pairing, row.One, row.OneFromM, row.OneToM, row.Other, row.OtherFromM, row.OtherToM);

        OpenWatch.Passed($"town: the marks' {pairing.Pairs.Count} pairs weighed");
        return Filed(lines, [pairing], lengthM);
    }
}
