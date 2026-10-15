using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The atlas on the device</b>: <see cref="RibbonAtlas"/>' own tables laid into storage buffers once
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
/// <b>Laid whole off the host's atlas, or a band at a time as the host files it</b> (<see cref="IAtlasKeeper"/>,
/// <c>RibbonAtlas.Stream</c>): the same words either way, and the second never has the host hold the whole.
/// </para>
/// </remarks>
internal sealed class DeviceAtlas : IDisposable, IAtlasKeeper
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

    /// <summary>The atlas's tables in binding order, for a kernel that binds them after its own (<c>ATLAS_BINDING</c>).</summary>
    public GpuBuffer[] Tables { get; private set; } = [];

    /// <summary>Where each table begins in the run of words (<see cref="Begin"/>).</summary>
    readonly uint[] _head = new uint[TableCount];

    /// <summary>How many starts make a kept cell's, which places a band's starts.</summary>
    int _startsPerCell;

    /// <summary>
    /// The last start a band brought where it ended half way through a word: the next band's first shares that word, and
    /// a page writes whole words.
    /// </summary>
    ushort _carriedStart;

    /// <summary>An atlas to be kept a band at a time (<see cref="IAtlasKeeper"/>): nothing on the device until it is begun.</summary>
    public DeviceAtlas(Vk vk) => _vk = vk;

    /// <summary>The host's whole atlas, laid at once.</summary>
    public DeviceAtlas(Vk vk, AtlasTable table) : this(vk)
    {
        Begin(table, table.PointStart.Length, table.Entries.Length);
        Keep(table);
    }

    public void Begin(AtlasTable frame, int starts, int entries)
    {
        Tables = [Packed(frame, starts, entries)];
        _startsPerCell = frame.StartsPerCell;

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

    /// <summary>The push constants an ask of <paramref name="bodies"/> bodies is made with.</summary>
    public AskPush PushFor(int bodies) => _push with { BodyCount = (uint)bodies };

    /// <summary>How many tables the run of words holds, each one's first word named in the run's own head.</summary>
    const int TableCount = 6;

    /// <summary>
    /// <b>Every table in one run of words</b> (<c>atlas.glsl</c>'s <c>AtlasWords</c>), in the order of its <c>…At</c> head:
    /// the cell slots, the slots' firsts, the point starts two to a word, the entries two words each, the ways' lengths and
    /// their channels four to a word. One binding, where six would take more storage buffers than WebGPU gives a kernel.
    /// </summary>
    /// <remarks>
    /// Each table is staged straight into its own stretch of the buffer, as each was into a buffer of its own; the starts
    /// and the entries a band at a time (<see cref="Keep"/>).
    /// </remarks>
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

    /// <summary>One band's starts and entries, each at its place in the whole's tables.</summary>
    public unsafe void Keep(AtlasTable band)
    {
        var buffer = Tables[0];
        buffer.Upload<RibbonAtlas.Entry, EntryRow>(band.Entries, &EntryRow.Of, (_head[3] * sizeof(uint)) + ((ulong)band.FirstEntry * (ulong)sizeof(EntryRow)));

        // Two starts a word: a band beginning half way through one sends the start before it again, which the band before
        // left there.
        var first = band.FirstSlot * _startsPerCell;
        var starts = band.PointStart;
        if (first % 2 != 0)
        {
            var whole = new ushort[starts.Length + 1];
            whole[0] = _carriedStart;
            starts.CopyTo(whole.AsSpan(1));
            starts = whole;
            first--;
        }

        if (!starts.IsEmpty) _carriedStart = starts[^1];
        buffer.Upload(starts, (_head[2] * sizeof(uint)) + ((ulong)first * sizeof(ushort)));
    }

    static void Upload<T>(GpuBuffer buffer, ReadOnlySpan<T> rows, uint atWord) where T : unmanaged =>
        buffer.Upload(rows, atWord * sizeof(uint));

    /// <summary>How many words hold <paramref name="count"/> of these, the last padded.</summary>
    static uint WordsOf<T>(int count) where T : unmanaged => (uint)((count * Marshal.SizeOf<T>() + sizeof(uint) - 1) / sizeof(uint));

    GpuBuffer Room<T>(int count) where T : unmanaged
    {
        var buffer = _vk.CreateBuffer(
            (ulong)(Math.Max(1, count) * Marshal.SizeOf<T>()),
            BufferUse.Storage | BufferUse.TransferDst, HostAccess.None);
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
