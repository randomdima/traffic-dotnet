using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The atlas filed on the device</b> (<see cref="IAtlasFiler"/>): the ribbons' points found, counted, filed and put in
/// order by <c>world/road/kernels/atlas_file.comp</c>, <c>atlas_starts.comp</c> and <c>atlas_sort.comp</c> — the tables
/// <see cref="RibbonAtlas"/> files on the host, to the bit where the device rounds as the host does — and the town's
/// furniture checked against them (<c>atlas_furniture.comp</c>, TER-4c.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two batches and one answer between them</b>: the first counts every main cell's entries, which the host reads back to
/// lay the slots (<see cref="RibbonAtlas.SlotsOf"/>) and size the tables; the second files into them. A head that waits
/// files the whole before the world is laid (<see cref="Finish"/>); a page, which cannot, lays the world over a word standing
/// in for the tables and finishes as frames go by (<see cref="Advance"/>, <see cref="Check"/>) — and the queue, which runs
/// both batches before any step of the world's, is what keeps the world from reading the tables unfiled.
/// </para>
/// <para>
/// <b>A run of points counts its entries in a byte</b> (<c>atlas_file.comp</c>'s <c>runCounts</c>): two points of a row and every way
/// at each. A run past that is told by its cell's runs not summing to the cell's count, and refused.
/// </para>
/// </remarks>
internal sealed partial class DeviceAtlas : IAtlasFiler
{
    /// <summary><c>atlas_file.comp</c>'s push constants, field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FilePush
    {
        public uint Mode;
        public uint FirstRow;
        public uint RowCount;
        public uint PieceCount;
        public int CellsFromX;
        public int CellsFromY;
        public int CellsWidth;
        public int Shift;
        public int Mask;
        public int StartShift;
        public int RunsPerCell;
        public int StartsPerCell;
        public float PerCellM;
        public float CellM;
        public float ReachM;
        public float AlongPerMetre;
        public float OutsidePerReach;
        public float StraightCurvature;
        public float LeastFromCentreM;
        public float Tau;
        public uint PointStartsAt;
        public uint EntriesAt;
        public uint CellSlotsAt;
        public uint SlotFirstsAt;
    }

    /// <summary><c>atlas_starts.comp</c>'s push constants.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct StartsPush(uint SlotCount, int RunsPerCell, int StartsPerCell, uint PointStartsAt, uint SlotFirstsAt);

    /// <summary><c>atlas_sort.comp</c>'s push constants.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct SortPush(
        uint FirstRun, uint RunCount, int RunsPerCell, int StartsPerCell, int Mask, uint PointStartsAt, uint EntriesAt,
        uint SlotFirstsAt);

    /// <summary><c>atlas_furniture.comp</c>'s push constants: an ask's, and which of its two passes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FurniturePush
    {
        public AskPush Ask;
        public uint Mode;
        public uint GroundChannel;
    }

    Filing? _filing;

    /// <summary>
    /// <b>Whether the tables are the atlas's own</b>, laid and filed or being filed ahead of anything that reads them — and
    /// not a word standing in for them while the device counts.
    /// </summary>
    public bool Filed => _filing is null || _filing.Laid;

    /// <summary>What the device filed: how many points hold an entry, how many entries, and the most ways at one point.</summary>
    public (long Points, int Entries, int MostWaysAtAPoint) Census { get; private set; }

    public void File(AtlasFiling filing, AtlasFurniture furniture) => _filing = new Filing(this, filing, furniture);

    /// <summary>
    /// The filing taken on as far as the device has answered — the tables laid and filed once the counts have come back:
    /// whether they have (<see cref="Filed"/>). On a head that waits, waits for them.
    /// </summary>
    public bool Advance() => _filing is null || _filing.Advance();

    /// <summary>
    /// What the filing found, once the device has answered it: a run past its byte, or a prop on a driven way, refused as the
    /// host's own filing and check refuse them. Nothing until then; on a head that waits, waits for it.
    /// </summary>
    public void Check()
    {
        if (_filing is null || !_filing.Checked()) return;

        Census = _filing.Census;
        _filing.Dispose();
        _filing = null;
    }

    /// <summary>The whole filing, waited for and checked: for a head that waits, before the world is laid over it.</summary>
    public void Finish()
    {
        Advance();
        Check();
    }

    /// <summary>One filing in flight: its kernels, its working buffers, and which of its two batches it is in.</summary>
    sealed class Filing : IDisposable
    {
        const uint CountCells = 0u, CountRuns = 1u, Fill = 2u;
        const uint FirstProp = 0u, ItsWay = 1u;

        /// <summary>The most threads one dispatch runs: WebGPU's most workgroups along an axis, of 64.</summary>
        const int MostThreads = Group * 65535;

        const int Group = 64;

        /// <summary>The answers copied back after the filing: the fault, the census's two, and the refused prop and its way.</summary>
        const int Answers = 5;

        readonly DeviceAtlas _atlas;
        readonly Vk _vk;
        readonly AtlasFiling _frame;
        readonly AtlasFurniture _furniture;
        readonly ComputeBatch _batch;
        readonly ComputeKernel _file, _starts, _sort, _check;
        readonly List<GpuBuffer> _buffers = [];
        readonly GpuBuffer _pieces, _rowFirst, _cellCounts, _cellCountsOut;
        readonly BoundSet _countSet;
        readonly int _pieceCount, _rowCount;
        FilePush _push;
        GpuBuffer? _answers;
        int _entries;

        public Filing(DeviceAtlas atlas, AtlasFiling filing, AtlasFurniture furniture)
        {
            _atlas = atlas;
            _vk = atlas._vk;
            _furniture = furniture;
            _pieceCount = filing.Pieces.Length;
            _rowCount = filing.RowFirst[^1];
            _frame = new AtlasFiling
            {
                Level = filing.Level,
                Cells = filing.Cells,
                LengthM = filing.LengthM,
                WayChannels = filing.WayChannels,
                Pieces = [],
                RowFirst = [0],
            };

            _batch = new ComputeBatch(_vk);
            _file = new ComputeKernel(_vk, "atlas_file.comp", 5, (uint)Marshal.SizeOf<FilePush>(), sets: 2);
            _starts = new ComputeKernel(_vk, "atlas_starts.comp", 3, (uint)Marshal.SizeOf<StartsPush>());
            _sort = new ComputeKernel(_vk, "atlas_sort.comp", 2, (uint)Marshal.SizeOf<SortPush>());
            _check = new ComputeKernel(_vk, "atlas_furniture.comp", 4, (uint)Marshal.SizeOf<FurniturePush>());

            _pieces = Device<FilingPiece>(_pieceCount);
            _pieces.Upload<FilingPiece>(filing.Pieces);
            _rowFirst = Device<int>(filing.RowFirst.Length);
            _rowFirst.Upload<int>(filing.RowFirst);
            _cellCounts = Device<uint>(filing.Cells.Count);
            _cellCountsOut = Buffer<uint>(filing.Cells.Count, HostAccess.Reads);
            _countSet = _file.Bind([_pieces, _rowFirst, _cellCounts, Device<uint>(1), Device<uint>(1)]);

            var cells = filing.Cells;
            _push = new FilePush
            {
                PieceCount = (uint)_pieceCount,
                RowCount = (uint)_rowCount,
                CellsFromX = cells.FromX,
                CellsFromY = cells.FromY,
                CellsWidth = cells.Width,
                Shift = filing.Shift,
                Mask = filing.Mask,
                StartShift = filing.StartShift,
                RunsPerCell = filing.RunsPerCell,
                StartsPerCell = filing.StartsPerCell,
                PerCellM = filing.Level.PerCellM,
                CellM = filing.Level.CellM,
                ReachM = filing.ReachM,
                AlongPerMetre = RibbonAtlas.AlongPerMetre,
                OutsidePerReach = RibbonAtlas.OutsidePerReach,
                StraightCurvature = RibbonAtlas.StraightCurvature,
                LeastFromCentreM = RibbonAtlas.LeastFromCentreM,
                Tau = MathF.Tau,
            };

            var commands = _batch.Begin();
            _vk.Fill(commands, _cellCounts, 0);
            _vk.AfterTransfer(commands);
            Rows(commands, _countSet, CountCells);
            _vk.BeforeTransfer(commands);
            _vk.Copy(commands, _cellCounts, _cellCountsOut, _cellCounts.SizeBytes);
            _batch.Submit();
        }

        /// <summary>Whether the tables are laid, and their filing recorded ahead of anything that reads them.</summary>
        public bool Laid { get; private set; }

        public (long Points, int Entries, int MostWaysAtAPoint) Census { get; private set; }

        public bool Advance()
        {
            if (Laid) return true;

            _batch.Wait();
            if (!_batch.Readable) return false;

            var (cellSlot, slotFirst) = RibbonAtlas.SlotsOf(
                MemoryMarshal.Cast<uint, int>(_cellCountsOut.Span<uint>())[.._frame.Cells.Count], _frame.Cells.Level);
            var slots = slotFirst.Length - 1;
            _entries = slotFirst[^1];
            _atlas.Lay(_frame.FrameOf(cellSlot, slotFirst), slots * _frame.StartsPerCell, _entries);
            var words = _atlas.Tables[0];
            var head = _atlas._head;
            _push.CellSlotsAt = head[0];
            _push.SlotFirstsAt = head[1];
            _push.PointStartsAt = head[2];
            _push.EntriesAt = head[3];

            var runs = slots * _frame.RunsPerCell;
            var runCounts = Device<uint>((runs + 3) / 4);
            var faults = Device<uint>(1);
            var census = Device<uint>(2);
            var refused = Device<uint>(2);
            _answers = Buffer<uint>(Answers, HostAccess.Reads);
            var props = Device<System.Numerics.Vector4>(_furniture.Count);
            props.Upload<System.Numerics.Vector4>(PropsOf(_furniture));
            var driven = Device<uint>((_furniture.DrivenWay.Length + 31) / 32);
            driven.Upload<uint>(DrivenOf(_furniture.DrivenWay));

            var fillSet = _file.Bind([_pieces, _rowFirst, Device<uint>(1), runCounts, words]);
            var startsSet = _starts.Bind([runCounts, words, faults]);
            var sortSet = _sort.Bind([words, census]);
            var checkSet = _check.Bind([props, driven, refused, words]);

            var commands = _batch.Begin();
            _vk.Fill(commands, runCounts, 0);
            _vk.Fill(commands, words, head[2] * sizeof(uint), (head[3] - head[2]) * sizeof(uint), 0);
            _vk.Fill(commands, faults, 0);
            _vk.Fill(commands, census, 0);
            _vk.Fill(commands, refused, uint.MaxValue);
            _vk.AfterTransfer(commands);
            Rows(commands, fillSet, CountRuns);

            if (slots > MostThreads) throw new InvalidOperationException($"{slots} kept cells are more than one dispatch starts");

            _starts.Dispatch(
                commands, startsSet, Groups(slots),
                new StartsPush((uint)slots, _frame.RunsPerCell, _frame.StartsPerCell, head[2], head[1]));
            _vk.BetweenKernels(commands);
            Rows(commands, fillSet, Fill);
            for (var first = 0; first < runs; first += MostThreads)
            {
                _sort.Dispatch(
                    commands, sortSet, Groups(Math.Min(MostThreads, runs - first)),
                    new SortPush(
                        (uint)first, (uint)runs, _frame.RunsPerCell, _frame.StartsPerCell, _frame.Mask, head[2], head[3], head[1]));
            }

            _vk.BetweenKernels(commands);
            var ask = new FurniturePush
            {
                Ask = _atlas.PushFor(_furniture.Count),
                GroundChannel = CityPlan.RoadArrays.GroundChannel,
            };
            _check.Dispatch(commands, checkSet, Groups(_furniture.Count), ask with { Mode = FirstProp });
            _vk.BetweenKernels(commands);
            _check.Dispatch(commands, checkSet, Groups(_furniture.Count), ask with { Mode = ItsWay });
            _vk.BeforeTransfer(commands);
            _vk.Copy(commands, faults, _answers, [new CopyRun(0, 0, sizeof(uint))]);
            _vk.Copy(commands, census, _answers, [new CopyRun(0, sizeof(uint), 2 * sizeof(uint))]);
            _vk.Copy(commands, refused, _answers, [new CopyRun(0, 3 * sizeof(uint), 2 * sizeof(uint))]);
            _batch.Submit();
            Laid = true;
            return true;
        }

        /// <summary>Whether the filing has answered, and what it found is the host's: refused where the host's would be.</summary>
        public bool Checked()
        {
            if (!Laid) return false;

            _batch.Wait();
            if (!_batch.Readable) return false;

            var answers = _answers!.Span<uint>();
            if (answers[0] != 0)
            {
                throw new InvalidOperationException(
                    $"a run of {1 << _frame.StartShift} points held more than {byte.MaxValue} entries: more ways at a point than the device files");
            }

            Census = (answers[1], _entries, (int)answers[2]);
            var prop = answers[3];
            if (prop != uint.MaxValue)
            {
                throw new InvalidOperationException(
                    $"prop {prop} at {_furniture.CentreM[prop]} stands on way {answers[4]}, which the traffic drives");
            }

            return true;
        }

        /// <summary>The pass over every row of every piece's box, as many dispatches as it takes.</summary>
        void Rows(Recording commands, BoundSet set, uint mode)
        {
            for (var first = 0; first < _rowCount; first += MostThreads)
            {
                _file.Dispatch(
                    commands, set, Groups(Math.Min(MostThreads, _rowCount - first)),
                    _push with { Mode = mode, FirstRow = (uint)first });
            }

            _vk.BetweenKernels(commands);
        }

        static uint Groups(int threads) => (uint)Math.Max(1, (threads + Group - 1) / Group);

        static System.Numerics.Vector4[] PropsOf(AtlasFurniture furniture)
        {
            var props = new System.Numerics.Vector4[furniture.Count];
            for (var prop = 0; prop < props.Length; prop++)
            {
                props[prop] = new System.Numerics.Vector4(furniture.CentreM[prop], furniture.RadiusM[prop], 0f);
            }

            return props;
        }

        static uint[] DrivenOf(bool[] drivenWay)
        {
            var bits = new uint[(drivenWay.Length + 31) / 32];
            for (var way = 0; way < drivenWay.Length; way++)
            {
                if (drivenWay[way]) bits[way >> 5] |= 1u << (way & 31);
            }

            return bits;
        }

        GpuBuffer Device<T>(int count) where T : unmanaged => Buffer<T>(count, HostAccess.None);

        GpuBuffer Buffer<T>(int count, HostAccess host) where T : unmanaged
        {
            var buffer = _vk.CreateBuffer(
                (ulong)(Math.Max(1, count) * Marshal.SizeOf<T>()),
                BufferUse.Storage | BufferUse.TransferDst | BufferUse.TransferSrc, host);
            _buffers.Add(buffer);
            return buffer;
        }

        public void Dispose()
        {
            _batch.Dispose();
            _file.Dispose();
            _starts.Dispose();
            _sort.Dispose();
            _check.Dispose();
            foreach (var buffer in _buffers) buffer.Dispose();
        }
    }
}
