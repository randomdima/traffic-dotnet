using System.Runtime.InteropServices;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using static TrafficSimulation.App.DeviceTick.DeviceHoldings;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The marks worked out on the device</b> (<see cref="IMarker"/>, TER-5c): every pair of pieces whose boxes meet weighed by
/// <c>world/road/kernels/ribbon_marks.comp</c>, a thread an entry of the pieces' cells, and what each pair shares appended
/// as a row the host merges and files (<see cref="RibbonMarks.OfRows"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>In floats where the host works in doubles</b>, each pair in its own frame (the kernel says how), so a device's marks
/// stand within millimetres of the host's and are not the host's to the bit.
/// </para>
/// <para>
/// <b>The rows are as many as the device finds</b>: laid for twice the pieces, and where more were found the pass is run
/// again with room for every one — a second answer to wait for, which a page waits for a frame at a time.
/// </para>
/// </remarks>
internal sealed class DeviceMarks(Vk vk) : IMarker
{
    public IMarking Mark(MarkFiling filing) => new Marking(vk, filing);

    /// <summary><c>ribbon_marks.comp</c>'s push constants, field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MarksPush
    {
        public uint EntryFrom;
        public uint EntryCount;
        public int WindowFromX;
        public int WindowFromY;
        public int WindowWidth;
        public int WindowHeight;
        public float PerCellM;
        public float StepM;
        public float TouchM;
        public float RoundingM;
        public uint RowRoom;
        public uint HasChannels;
    }

    /// <summary>One working out in flight: its kernel, its buffers, and the room its rows were laid in.</summary>
    sealed class Marking : IMarking
    {
        /// <summary>The most threads one dispatch runs: WebGPU's most workgroups along an axis, of 64.</summary>
        const int MostThreads = Group * 65535;

        readonly Vk _vk;
        readonly ComputeBatch _batch;
        readonly ComputeKernel _kernel;
        readonly DeviceHoldings _held;
        readonly GpuBuffer _pieces, _entryCell, _cellFirst, _inCell, _channels;
        readonly int _entries;
        MarksPush _push;
        GpuBuffer _rows = null!, _count = null!, _rowsOut = null!, _countOut = null!;
        MarkRow[]? _found;

        public Marking(Vk vk, MarkFiling filing)
        {
            _vk = vk;
            _entries = filing.EntryCell.Length;
            _batch = new ComputeBatch(vk);
            _kernel = new ComputeKernel(vk, "ribbon_marks.comp", 7, (uint)Marshal.SizeOf<MarksPush>());
            _held = new DeviceHoldings(vk);
            _pieces = _held.Laid<MarkPiece>(filing.Pieces, BufferUse.TransferSrc);
            _entryCell = _held.Laid<uint>(filing.EntryCell, BufferUse.TransferSrc);
            _cellFirst = _held.Laid<uint>(filing.CellFirst, BufferUse.TransferSrc);
            _inCell = _held.Laid<uint>(filing.InCell, BufferUse.TransferSrc);
            _channels = _held.Laid<uint>(filing.Channels.Length > 0 ? filing.Channels : [0u], BufferUse.TransferSrc);
            var window = filing.Window;
            _push = new MarksPush
            {
                EntryCount = (uint)_entries,
                WindowFromX = window.FromX,
                WindowFromY = window.FromY,
                WindowWidth = window.Width,
                WindowHeight = window.Height,
                PerCellM = window.Level.PerCellM,
                StepM = filing.StepM,
                TouchM = filing.TouchM,
                RoundingM = LineTolerance.RoundingM,
                HasChannels = filing.Channels.Length > 0 ? 1u : 0u,
            };

            Weigh(Math.Max(1 << 16, 2 * filing.Pieces.Length));
        }

        public ReadOnlySpan<MarkRow> Rows => _found ?? throw new InvalidOperationException("The marks were read before the device answered.");

        public bool Answered()
        {
            if (_found is not null) return true;

            _batch.Wait();
            if (!_batch.Readable) return false;

            var found = (int)_countOut.Span<uint>()[0];
            if (found > (int)_push.RowRoom)
            {
                Weigh(found);
                return false;
            }

            _found = _rowsOut.Span<MarkRow>()[..found].ToArray();
            return true;
        }

        /// <summary>Every entry's pairs weighed into rows laid for so many, and the rows and their count copied back.</summary>
        void Weigh(int room)
        {
            _rows = _held.Device<MarkRow>(room);
            _count = _held.Device<uint>(1);
            _rowsOut = _held.Readback<MarkRow>(room, BufferUse.Storage | BufferUse.TransferSrc);
            _countOut = _held.Readback<uint>(1, BufferUse.Storage | BufferUse.TransferSrc);
            _push.RowRoom = (uint)room;
            var set = _kernel.Bind([_pieces, _entryCell, _cellFirst, _inCell, _channels, _rows, _count]);

            var commands = _batch.Begin();
            _vk.Fill(commands, _count, 0);
            _vk.AfterTransfer(commands);
            for (var first = 0; first < _entries; first += MostThreads)
            {
                _kernel.Dispatch(commands, set, Groups(Math.Min(MostThreads, _entries - first)), _push with { EntryFrom = (uint)first });
            }

            _vk.BeforeTransfer(commands);
            _vk.Copy(commands, _count, _countOut, _count.SizeBytes);
            _vk.Copy(commands, _rows, _rowsOut, _rows.SizeBytes);
            _batch.Submit();
        }

        public void Dispose()
        {
            _batch.Dispose();
            _kernel.Dispose();
            _held.Dispose();
        }
    }
}
