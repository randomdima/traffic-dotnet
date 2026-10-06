using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The ground on the device</b>: <see cref="GroundShapes"/>' own tables laid into storage buffers once
/// (<c>citygen/kernels/ground_tables.glsl</c>), and the query answered off them (<c>ground.glsl</c>) — the one a
/// tyre and a line ask of what is underfoot. Laid at load and never written again (SOL-21's bargain, for the ground).
/// </summary>
/// <remarks>
/// <b>It needs doubles on the device</b> (<see cref="Vk.HasFloat64"/>): the carriageway and the walk are answered off
/// <see cref="RingSides"/>, which places a corner and solves a crossing in them, as the host does.
/// </remarks>
internal sealed class DeviceGround : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ArcRow(Vector4 A, Vector4 B)
    {
        public static ArcRow Of(in ArcSeg arc) =>
            new(new Vector4(arc.StartM, arc.StartUnit.X, arc.StartUnit.Y), new Vector4(arc.HeadingRad, arc.LengthM, arc.Curvature, 0f));
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct SlotRow(Vector2 LeastM, Vector2 MostM, int ArcStart, int ArcEnd, int ChainId, float LengthM);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct RoadRow(
        float HalfM, float LengthM, float ReachM, uint LevelAndBay, int DeckFrom, int DeckTo, int PaintFrom, int PaintTo);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct LatticeRow(
        int FromX, int FromY, int Width, int Height, float PerCellM, uint CellBase, uint FilingBase, uint PieceBase,
        double CellM, double Pad);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PieceRow(
        Vector2 FromM, Vector2 ToM, Vector2 RadialM, Vector2 Slope, int SideX, int SideY, int Bends, int Pad);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct RingRow(Vector2 LeastM, Vector2 MostM, int PointStart, int PointEnd, int Cells, int Pad);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct EdgeCellsRow(
        Vector2 LeastM, float CellM, int Columns, int Rows, int StartBase, int EdgeBase, int EmptyBase);

    /// <summary>The ask's push constants (<c>ground_at.comp</c>): everything the query reads that is a number.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct AskPush
    {
        public uint PointCount;
        public int RoadFromX;
        public int RoadFromY;
        public int RoadWidth;
        public int RoadHeight;
        public float RoadPerCellM;
        public float FarthestReachM;
        public uint SlabCount;
        public uint WaterFrom;
        public uint WaterTo;
        public uint ShoreFrom;
        public uint ShoreTo;
    }

    readonly Vk _vk;
    readonly List<GpuBuffer> _owned = [];
    readonly AskPush _push;

    /// <summary>The ground's tables in binding order, for a kernel that binds them after its own (<c>GROUND_BINDING</c>).</summary>
    public GpuBuffer[] Tables { get; }

    public DeviceGround(Vk vk, GroundTable table)
    {
        if (!vk.HasFloat64) throw new NotSupportedException($"{vk.DeviceName} has no doubles in its kernels, and the ground is answered in them.");

        _vk = vk;
        var index = table.RoadIndex;

        var arcs = new ArcRow[index.Arcs.Length];
        for (var arc = 0; arc < arcs.Length; arc++) arcs[arc] = ArcRow.Of(index.Arcs[arc]);

        var slots = new SlotRow[index.ChainId.Length];
        for (var slot = 0; slot < slots.Length; slot++)
        {
            slots[slot] = new SlotRow(
                index.SlotLeastM[slot], index.SlotMostM[slot], index.ArcStart[slot], index.ArcStart[slot + 1],
                index.ChainId[slot], index.LengthM[slot]);
        }

        var roads = new RoadRow[table.RoadHalfM.Length];
        for (var road = 0; road < roads.Length; road++)
        {
            roads[road] = new RoadRow(
                table.RoadHalfM[road], table.RoadLengthM[road], table.RoadReachM[road],
                table.RoadLevel[road] | (table.RoadIsABay[road] ? 1u << 8 : 0u), table.DeckAt[road], table.DeckAt[road + 1],
                table.PaintAt[road], table.PaintAt[road + 1]);
        }

        var decks = new Vector4[table.DeckFromM.Length];
        for (var run = 0; run < decks.Length; run++) decks[run] = new Vector4(table.DeckFromM[run], table.DeckToM[run], table.DeckHalfM[run], 0f);

        var paints = new Vector2[table.PaintFromM.Length];
        for (var run = 0; run < paints.Length; run++) paints[run] = new Vector2(table.PaintFromM[run], table.PaintToM[run]);

        var slabs = new Vector4[table.SlabMinM.Length];
        for (var slab = 0; slab < slabs.Length; slab++) slabs[slab] = new Vector4(table.SlabMinM[slab], table.SlabSizeM[slab].X, table.SlabSizeM[slab].Y);

        var lattices = new LatticeRow[3];
        var cells = new List<int>();
        var filing = new List<int>();
        var pieces = new List<PieceRow>();
        RingSides[] sides = [table.Carriageway, table.Walk, table.Above];
        for (var lattice = 0; lattice < sides.Length; lattice++)
        {
            var window = sides[lattice].Window;
            var (cell, cellPiece, piece) = sides[lattice].Table;
            lattices[lattice] = new LatticeRow(
                window.FromX, window.FromY, window.Width, window.Height, window.Level.PerCellM, (uint)cells.Count,
                (uint)filing.Count, (uint)pieces.Count, window.Level.CellM, 0d);
            cells.AddRange(cell);
            filing.AddRange(cellPiece);
            foreach (var p in piece)
            {
                pieces.Add(new PieceRow(p.FromM, p.ToM, p.RadialM, p.Slope, p.SideX, p.SideY, p.Bends ? 1 : 0, 0));
            }
        }

        var rings = new List<RingRow>();
        var points = new List<Vector2>();
        var edgeSets = new List<EdgeCellsRow>();
        var edgeStart = new List<int>();
        var edgeEdges = new List<int>();
        var edgeEmpty = new List<uint>();
        var (waterFrom, waterTo) = AddRings(table.Water, rings, points, edgeSets, edgeStart, edgeEdges, edgeEmpty);
        var (shoreFrom, shoreTo) = AddRings(table.Shore, rings, points, edgeSets, edgeStart, edgeEdges, edgeEmpty);

        Tables =
        [
            Laid<ArcRow>(arcs),
            Laid<SlotRow>(slots),
            Laid(table.RoadIndex.CellStart),
            Laid(table.RoadIndex.EntrySlot),
            Laid<RoadRow>(roads),
            Laid<Vector4>(decks),
            Laid<Vector2>(paints),
            Laid<Vector4>(slabs),
            Laid<LatticeRow>(lattices),
            Laid<int>(CollectionsMarshal.AsSpan(cells)),
            Laid<int>(CollectionsMarshal.AsSpan(filing)),
            Laid<PieceRow>(CollectionsMarshal.AsSpan(pieces)),
            Laid<RingRow>(CollectionsMarshal.AsSpan(rings)),
            Laid<Vector2>(CollectionsMarshal.AsSpan(points)),
            Laid<EdgeCellsRow>(CollectionsMarshal.AsSpan(edgeSets)),
            Laid<int>(CollectionsMarshal.AsSpan(edgeStart)),
            Laid<int>(CollectionsMarshal.AsSpan(edgeEdges)),
            Laid<uint>(CollectionsMarshal.AsSpan(edgeEmpty)),
        ];

        var roadWindow = table.RoadWindow;
        _push = new AskPush
        {
            RoadFromX = roadWindow.FromX,
            RoadFromY = roadWindow.FromY,
            RoadWidth = roadWindow.Width,
            RoadHeight = roadWindow.Height,
            RoadPerCellM = roadWindow.Level.PerCellM,
            FarthestReachM = table.FarthestReachM,
            SlabCount = (uint)slabs.Length,
            WaterFrom = (uint)waterFrom,
            WaterTo = (uint)waterTo,
            ShoreFrom = (uint)shoreFrom,
            ShoreTo = (uint)shoreTo,
        };
    }

    /// <summary>The push constants an ask of <paramref name="points"/> points is made with.</summary>
    public AskPush PushFor(int points) => _push with { PointCount = (uint)points };

    /// <summary>One set of rings appended to the device's lists, and the run of ring rows it took.</summary>
    static (int From, int To) AddRings(
        GroundShapes.RingTable set, List<RingRow> rings, List<Vector2> points, List<EdgeCellsRow> edgeSets, List<int> edgeStart,
        List<int> edgeEdges, List<uint> edgeEmpty)
    {
        var from = rings.Count;
        for (var ring = 0; ring < set.Rings.Count; ring++)
        {
            var pointStart = points.Count;
            points.AddRange(set.Rings.RingOf(ring));
            var cellsAt = -1;
            if (set.Cells[ring] is { } filed)
            {
                var (leastM, cellM, columns, rows, start, edges, empty) = filed.Table;
                cellsAt = edgeSets.Count;
                edgeSets.Add(new EdgeCellsRow(leastM, cellM, columns, rows, edgeStart.Count, edgeEdges.Count, edgeEmpty.Count));
                edgeStart.AddRange(start);
                edgeEdges.AddRange(edges);
                foreach (var inside in empty) edgeEmpty.Add(inside ? 1u : 0u);
            }

            rings.Add(new RingRow(set.LeastM[ring], set.MostM[ring], pointStart, points.Count, cellsAt, 0));
        }

        return (from, rings.Count);
    }

    /// <summary>A device-local buffer holding these rows, sent once through a staging buffer.</summary>
    GpuBuffer Laid<T>(ReadOnlySpan<T> rows) where T : unmanaged
    {
        var buffer = _vk.CreateBuffer(
            (ulong)(Math.Max(1, rows.Length) * Marshal.SizeOf<T>()),
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        _owned.Add(buffer);
        var copy = rows.ToArray();
        buffer.Upload<T>(copy.Length, into => copy.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    public void Dispose()
    {
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
