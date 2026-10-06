using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>Everything <see cref="RibbonAtlas.UnderBox(System.Numerics.Vector2, System.Numerics.Vector2, float, float, Span{WayCover}, byte)"/>
/// and <see cref="RibbonAtlas.UnderDisc(System.Numerics.Vector2, float, Span{WayCover}, byte)"/> read, as data</b> — for a
/// device that answers the same question off its own copy, in the same order and with the same arithmetic. The atlas
/// handed over, and never a second filing of the ribbons.
/// </summary>
internal readonly ref struct AtlasTable
{
    /// <summary>The level the points stand at the middles of.</summary>
    public required GridLevel Level { get; init; }

    /// <summary>The main cells the ribbons reach.</summary>
    public required GridWindow Cells { get; init; }

    /// <summary>How many halvings a main cell is cut into to reach the points.</summary>
    public required int Shift { get; init; }

    /// <summary>A point's row or column within its main cell, as a mask.</summary>
    public required int Mask { get; init; }

    /// <summary>How many points of a row one start stands for, as a shift.</summary>
    public required int StartShift { get; init; }

    /// <summary>How many starts one row of a kept cell has, as a shift.</summary>
    public required int RowStartsShift { get; init; }

    /// <summary>How many starts one kept cell has, one past the last included.</summary>
    public required int StartsPerCell { get; init; }

    /// <summary>What one step of <see cref="RibbonAtlas.Entry.Outside"/> is in metres.</summary>
    public required float MetresPerOutside { get; init; }

    /// <summary>How many steps of <see cref="RibbonAtlas.Entry.Along"/> make a metre.</summary>
    public required float AlongPerMetre { get; init; }

    /// <summary>Where each main cell's entries are kept, or −1 for a cell no ribbon covers a point of.</summary>
    public required ReadOnlySpan<int> CellSlot { get; init; }

    /// <summary>Where each kept cell's entries begin; one past the last cell is the total.</summary>
    public required ReadOnlySpan<int> SlotFirst { get; init; }

    /// <summary>Where each run of points of a kept cell has its entries, counted from the cell's first.</summary>
    public required ReadOnlySpan<ushort> PointStart { get; init; }

    public required ReadOnlySpan<RibbonAtlas.Entry> Entries { get; init; }

    /// <summary>Every way's length, which a stretch is held to.</summary>
    public required ReadOnlySpan<float> LengthM { get; init; }

    /// <summary>Every way's channels, or empty where every way is on the ground's alone and no read asks.</summary>
    public required ReadOnlySpan<byte> WayChannels { get; init; }
}

internal sealed partial class RibbonAtlas
{
    /// <inheritdoc cref="AtlasTable"/>
    public AtlasTable Table => new()
    {
        Level = _level,
        Cells = _cells,
        Shift = _shift,
        Mask = _mask,
        StartShift = _startShift,
        RowStartsShift = _rowStartsShift,
        StartsPerCell = _startsPerCell,
        MetresPerOutside = _metresPerOutside,
        AlongPerMetre = AlongPerMetre,
        CellSlot = _cellSlot,
        SlotFirst = _slotFirst,
        PointStart = _pointStart,
        Entries = _entries,
        LengthM = _lengthM,
        WayChannels = _wayChannels,
    };
}
