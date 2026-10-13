using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The atlas on the device</b>: <see cref="RibbonAtlas"/>' own tables laid into storage buffers once
/// (<c>world/road/kernels/atlas.glsl</c>), and which ways a body stands over answered off them — what every claims
/// rebuild asks of every car's wheels and every walker's disc (TER-4c.2). Laid at load and never written again.
/// </summary>
/// <remarks>
/// <b>It needs doubles on the device</b> (<see cref="Vk.HasFloat64"/>): a box's edge and a disc's are a quotient and a
/// root the device is allowed to round its own way, and which lattice point an edge reaches is decided on their last bit,
/// so both are put right in doubles (<c>core/geometry/kernels/rounded.glsl</c>).
/// </remarks>
internal sealed class DeviceAtlas : IDisposable
{
    /// <summary>One entry as the device reads it: outside, column and along packed low to high, and the way.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct EntryRow(uint OutsideColumnAlong, int Way)
    {
        public static EntryRow Of(in RibbonAtlas.Entry entry) =>
            new(entry.Outside | ((uint)entry.Column << 8) | ((uint)entry.Along << 16), entry.Way);
    }

    /// <summary>
    /// One body asked (<c>atlas_under.comp</c>): a car's box on its own forward unit vector — the host's, so no corner is
    /// the device's own trigonometry — or a disc, whose radius is <see cref="HalfLengthM"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct BodyRow(
        Vector2 CentreM, Vector2 Forward, float HalfLengthM, float HalfWidthM, uint Channels, uint IsADisc)
    {
        public static BodyRow Box(Vector2 centreM, Vector2 forward, float halfLengthM, float halfWidthM, byte channels) =>
            new(centreM, forward, halfLengthM, halfWidthM, channels, 0u);

        public static BodyRow Disc(Vector2 centreM, float radiusM, byte channels) =>
            new(centreM, Vector2.Zero, radiusM, 0f, channels, 1u);
    }

    /// <summary>What one body was found over: how many ways, and how many were dropped for want of room.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CountRow(uint Count, uint Dropped);

    /// <summary>One car's four wheels asked (<c>atlas_wheels.comp</c>), in the tyre model's order, and how far its body is held either side of each.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WheelsRow(
        Vector2 Wheel0M, Vector2 Wheel1M, Vector2 Wheel2M, Vector2 Wheel3M, float AroundAWheelM, uint Channels);

    /// <summary>The ask's push constants (<c>atlas_under.comp</c>): every figure the read takes that is a number.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct AskPush
    {
        public uint BodyCount;
        public int CellsFromX;
        public int CellsFromY;
        public int CellsWidth;
        public int CellsHeight;
        public float PerCellM;
        public float CellM;
        public int Shift;
        public int Mask;
        public int StartShift;
        public int RowStartsShift;
        public int StartsPerCell;
        public float MetresPerOutside;
        public float AlongPerMetre;
        public uint HasChannels;
    }

    readonly Vk _vk;
    readonly List<GpuBuffer> _owned = [];
    readonly AskPush _push;

    /// <summary>The atlas's tables in binding order, for a kernel that binds them after its own (<c>ATLAS_BINDING</c>).</summary>
    public GpuBuffer[] Tables { get; }

    public DeviceAtlas(Vk vk, AtlasTable table)
    {
        if (!vk.HasFloat64) throw new NotSupportedException($"{vk.DeviceName} has no doubles in its kernels, and a body's edges are put right in them.");

        _vk = vk;
        Tables =
        [
            Laid(table.CellSlot),
            Laid(table.SlotFirst),
            Laid<uint>(InWords(table.PointStart)),
            LaidEntries(table.Entries),
            Laid(table.LengthM),
            Laid<uint>(InWords(table.WayChannels)),
        ];

        var cells = table.Cells;
        _push = new AskPush
        {
            CellsFromX = cells.FromX,
            CellsFromY = cells.FromY,
            CellsWidth = cells.Width,
            CellsHeight = cells.Height,
            PerCellM = table.Level.PerCellM,
            CellM = table.Level.CellM,
            Shift = table.Shift,
            Mask = table.Mask,
            StartShift = table.StartShift,
            RowStartsShift = table.RowStartsShift,
            StartsPerCell = table.StartsPerCell,
            MetresPerOutside = table.MetresPerOutside,
            AlongPerMetre = table.AlongPerMetre,
            HasChannels = table.WayChannels.Length > 0 ? 1u : 0u,
        };
    }

    /// <summary>The push constants an ask of <paramref name="bodies"/> bodies is made with.</summary>
    public AskPush PushFor(int bodies) => _push with { BodyCount = (uint)bodies };

    /// <summary>Narrow values packed into words, low first, the last word padded: a kernel reads a word and shifts.</summary>
    static uint[] InWords<T>(ReadOnlySpan<T> values) where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(values);
        var words = new uint[(bytes.Length + sizeof(uint) - 1) / sizeof(uint)];
        bytes.CopyTo(MemoryMarshal.AsBytes(words.AsSpan()));
        return words;
    }

    /// <summary>A device-local buffer holding these rows, sent once through a staging buffer and no copy of them besides.</summary>
    GpuBuffer Laid<T>(ReadOnlySpan<T> rows) where T : unmanaged
    {
        var buffer = Room<T>(rows.Length);
        buffer.Upload(rows, PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    /// <summary>The entries, each turned into the device's row as it is staged (<see cref="EntryRow.Of"/>).</summary>
    unsafe GpuBuffer LaidEntries(ReadOnlySpan<RibbonAtlas.Entry> entries)
    {
        var buffer = Room<EntryRow>(entries.Length);
        buffer.Upload<RibbonAtlas.Entry, EntryRow>(
            entries, &EntryRow.Of, PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    GpuBuffer Room<T>(int count) where T : unmanaged
    {
        var buffer = _vk.CreateBuffer(
            (ulong)(Math.Max(1, count) * Marshal.SizeOf<T>()),
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
