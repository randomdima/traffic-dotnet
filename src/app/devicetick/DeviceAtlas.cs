using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The atlas on the device</b>: <see cref="RibbonAtlas"/>' own tables in one storage buffer
/// (<c>world/road/kernels/atlas.glsl</c>), and which ways a body stands over answered off them — what every claims
/// rebuild asks of every car's wheels and every walker's disc (TER-4c.2). Laid at load and never written again.
/// </summary>
/// <remarks>
/// <para>
/// A box's edge and a disc's are a quotient and a root the device is allowed to round its own way, and which lattice
/// point an edge reaches is decided on their last bit, so both are put right to the nearest float
/// (<c>core/geometry/kernels/rounded.glsl</c>).
/// </para>
/// <para>
/// <b>Laid whole off the host's atlas, or filed here off the ribbons</b> (<see cref="IAtlasFiler"/>,
/// <c>DeviceAtlas.Filing.cs</c>): the same tables either way, and the second never has the host find a point.
/// </para>
/// </remarks>
internal sealed partial class DeviceAtlas : IDisposable
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
    AskPush _push;

    /// <summary>
    /// The atlas's tables in binding order, for a kernel that binds them after its own (<c>ATLAS_BINDING</c>) — a word
    /// standing in for them while the device files them (<see cref="Filed"/>).
    /// </summary>
    public GpuBuffer[] Tables { get; private set; }

    /// <summary>Where each table begins in the run of words (<see cref="Lay"/>).</summary>
    readonly uint[] _head = new uint[TableCount];

    /// <summary>An atlas the device will file (<see cref="IAtlasFiler"/>): a word standing in for its tables until it has.</summary>
    public DeviceAtlas(Vk vk)
    {
        _vk = vk;
        Tables = [Room<uint>(1)];
    }

    /// <summary>The host's whole atlas, laid at once.</summary>
    public DeviceAtlas(Vk vk, AtlasTable table) : this(vk)
    {
        Lay(table, table.PointStart.Length, table.Entries.Length);
        unsafe
        {
            Tables[0].Upload<RibbonAtlas.Entry, EntryRow>(table.Entries, &EntryRow.Of, _head[3] * sizeof(uint));
        }

        Tables[0].Upload(table.PointStart, _head[2] * sizeof(uint));
    }

    GridLevel _level;
    GridWindow _cells;

    /// <summary>
    /// <b>The tables as the device holds them</b>, copied back and handed to <paramref name="read"/> — for a probe or a test
    /// that holds the device's filing to the host's. Only on a head that waits.
    /// </summary>
    public void ReadBack(Action<AtlasTable> read)
    {
        var words = Tables[0];
        using var copy = _vk.CreateBuffer(words.SizeBytes, BufferUse.Storage | BufferUse.TransferDst, HostAccess.Reads);
        using var batch = new ComputeBatch(_vk);
        if (!batch.Waits) throw new NotSupportedException("A device's atlas is read back only where the host can wait for it.");

        var commands = batch.Begin();
        _vk.BeforeTransfer(commands);
        _vk.Copy(commands, words, copy, words.SizeBytes);
        batch.SubmitAndWait();

        var all = copy.Span<uint>();
        var slotFirst = MemoryMarshal.Cast<uint, int>(all.Slice((int)all[1], (int)(all[2] - all[1])));
        var slots = slotFirst.Length - 1;
        var lengthM = MemoryMarshal.Cast<uint, float>(all.Slice((int)all[4], (int)(all[5] - all[4])));
        var shift = _push.Shift;
        read(new AtlasTable
        {
            Level = _level,
            Cells = _cells,
            Shift = shift,
            Mask = _push.Mask,
            StartShift = _push.StartShift,
            RowStartsShift = _push.RowStartsShift,
            StartsPerCell = _push.StartsPerCell,
            MetresPerOutside = _push.MetresPerOutside,
            AlongPerMetre = _push.AlongPerMetre,
            CellSlot = MemoryMarshal.Cast<uint, int>(all.Slice((int)all[0], _cells.Count)),
            SlotFirst = slotFirst,
            PointStart = MemoryMarshal.Cast<uint, ushort>(all.Slice((int)all[2], (int)(all[3] - all[2])))[..(slots * _push.StartsPerCell)],
            // An entry row is the host's entry, byte for byte.
            Entries = MemoryMarshal.Cast<uint, RibbonAtlas.Entry>(all.Slice((int)all[3], 2 * slotFirst[slots])),
            LengthM = lengthM,
            WayChannels = _push.HasChannels != 0
                ? MemoryMarshal.Cast<uint, byte>(all.Slice((int)all[5], (int)(words.SizeBytes / sizeof(uint)) - (int)all[5]))[..lengthM.Length]
                : [],
        });
    }

    /// <summary>The push constants an ask of <paramref name="bodies"/> bodies is made with.</summary>
    public AskPush PushFor(int bodies) => _push with { BodyCount = (uint)bodies };

    /// <summary>How many tables the run of words holds, each one's first word named in the run's own head.</summary>
    const int TableCount = 6;

    /// <summary>
    /// The tables laid into one run of words, and the figures an ask is made with: every table but the starts and the
    /// entries, which are <paramref name="starts"/> and <paramref name="entries"/> long and written after.
    /// </summary>
    void Lay(AtlasTable frame, int starts, int entries)
    {
        Tables = [Packed(frame, starts, entries)];
        _level = frame.Level;
        _cells = frame.Cells;

        var cells = frame.Cells;
        _push = new AskPush
        {
            CellsFromX = cells.FromX,
            CellsFromY = cells.FromY,
            CellsWidth = cells.Width,
            CellsHeight = cells.Height,
            PerCellM = frame.Level.PerCellM,
            CellM = frame.Level.CellM,
            Shift = frame.Shift,
            Mask = frame.Mask,
            StartShift = frame.StartShift,
            RowStartsShift = frame.RowStartsShift,
            StartsPerCell = frame.StartsPerCell,
            MetresPerOutside = frame.MetresPerOutside,
            AlongPerMetre = frame.AlongPerMetre,
            HasChannels = frame.WayChannels.Length > 0 ? 1u : 0u,
        };
    }

    /// <summary>
    /// <b>Every table in one run of words</b> (<c>atlas.glsl</c>'s <c>AtlasWords</c>), in the order of its <c>…At</c> head:
    /// the cell slots, the slots' firsts, the point starts two to a word, the entries two words each, the ways' lengths and
    /// their channels four to a word. One binding, where six would take more storage buffers than WebGPU gives a kernel.
    /// </summary>
    /// <remarks>Each table is staged straight into its own stretch of the buffer, as each was into a buffer of its own.</remarks>
    GpuBuffer Packed(AtlasTable frame, int starts, int entries)
    {
        var head = _head.AsSpan();
        head[0] = WordsOf<int>(frame.CellSlot.Length);
        head[1] = WordsOf<int>(frame.SlotFirst.Length);
        head[2] = WordsOf<ushort>(starts);
        head[3] = WordsOf<EntryRow>(entries);
        head[4] = WordsOf<float>(frame.LengthM.Length);
        head[5] = WordsOf<byte>(frame.WayChannels.Length);

        var at = (uint)TableCount;
        for (var section = 0; section < TableCount; section++) (head[section], at) = (at, at + head[section]);

        var buffer = Room<uint>((int)at);
        Upload(buffer, (ReadOnlySpan<uint>)head, 0);
        Upload(buffer, frame.CellSlot, head[0]);
        Upload(buffer, frame.SlotFirst, head[1]);
        Upload(buffer, frame.LengthM, head[4]);
        Upload(buffer, frame.WayChannels, head[5]);
        return buffer;
    }

    static void Upload<T>(GpuBuffer buffer, ReadOnlySpan<T> rows, uint atWord) where T : unmanaged =>
        buffer.Upload(rows, atWord * sizeof(uint));

    /// <summary>How many words hold <paramref name="count"/> of these, the last padded.</summary>
    static uint WordsOf<T>(int count) where T : unmanaged => (uint)((count * Marshal.SizeOf<T>() + sizeof(uint) - 1) / sizeof(uint));

    GpuBuffer Room<T>(int count, HostAccess host = HostAccess.None) where T : unmanaged
    {
        var buffer = _vk.CreateBuffer(
            (ulong)(Math.Max(1, count) * Marshal.SizeOf<T>()),
            BufferUse.Storage | BufferUse.TransferDst | BufferUse.TransferSrc, host);
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        _filing?.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
