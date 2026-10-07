using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The plans answered on the device</b> (<c>world/road/kernels/claims.glsl</c>), beside the host's: the reservations
/// laid as rows and holds — sent across as the town laid them (<see cref="Lay"/>), or numbered and filed on the device by
/// the rebuild itself (<see cref="DeviceRebuild"/>) — every way's two lists laid from them, every hold answered pass after
/// pass as the host answers them (<see cref="LaneOccupancy.AnswerThePlans"/>), and cut where its last answer says.
/// </summary>
/// <remarks>
/// <para>
/// <b>The lists are the device's own</b> (<c>claims_lists.glsl</c>): the rows counted onto their ways, the counts
/// scanned, every row filed into its way's run and each run sorted as the host keeps it — nearest first, laid first
/// first — so only the rows and the holds cross.
/// </para>
/// <para>
/// <b>The device runs every pass the host could</b>: the first, and then on to the first odd pass at the bound. A pass
/// after the answers have stopped moving answers them all the same again, so the passes the host never needed change
/// nothing, and no count is read back between them.
/// </para>
/// <para>
/// <b>How many rows and holds there are is the tally's</b> (<see cref="Tally"/>, <c>claims_tally.glsl</c>): every kernel
/// over them stops at its word and is dispatched with the groups beside it, so a rebuild numbered on the device is
/// answered without its counts ever crossing.
/// </para>
/// </remarks>
internal sealed class DeviceClaims : IDisposable
{
    /// <summary>One reservation as the kernels read it (<c>claims_rows.glsl</c>'s <c>ClaimRow</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ClaimRow(
        float FromM, float ToM, float AlongMps, float LineFromM, float AheadM, float CommittedToM, float StandsToM, int Occupant,
        int Hold, int Onward, uint Way, uint Flags)
    {
        public const uint Gone = 1u << 10;

        public bool IsGone => (Flags & Gone) != 0;

        public static ClaimRow Of(in LaneClaim claim, int way, bool gone, float standsToM) => new(
            claim.FromM, claim.ToM, claim.AlongMps, claim.LineFromM, claim.AheadM, claim.CommittedToM, standsToM, claim.Occupant,
            claim.Hold, claim.Onward, (uint)way,
            (uint)claim.Priority | ((uint)claim.Of << 4) | (claim.OnItsLine ? 1u << 6 : 0u) | (claim.Secondary ? 1u << 7 : 0u)
            | (claim.Still ? 1u << 8 : 0u) | (claim.Passing ? 1u << 9 : 0u) | (gone ? Gone : 0u));

        /// <summary>
        /// <b>A slot as the device holds it</b>: a main claim of a plan answered with the others carries how far along its
        /// way its holder's body already reaches (<see cref="LaneOccupancy.SlotStandsToM"/>), and every other its own near
        /// edge.
        /// </summary>
        public static ClaimRow OfSlot(LaneOccupancy claims, int slot)
        {
            ref readonly var claim = ref claims.Slots[slot];
            var proposed = !claim.HasBody && !claim.Secondary && claims.IsAnswered(claim.Hold);
            return Of(claim, claims.SlotWays[slot], claims.SlotsGone[slot], proposed ? claims.SlotStandsToM[slot] : claim.FromM);
        }
    }

    /// <summary>One hold as the kernels read it (<c>claims_rows.glsl</c>'s <c>HoldRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct HoldRow(
        uint FirstRow, uint EndRow, float StandingMarginM, uint Flags, float KeptOffWhatItMayPassM, float PassesBelowMps,
        float LaidCutLineM, float LaidMarginM, uint LaidCutBy, int LaidCutOn, float Pad0 = 0f, float Pad1 = 0f)
    {
        public const uint Answered = 1u;
        public const uint OnACall = 2u;
    }

    /// <summary>What one hold was answered (<c>claims_rows.glsl</c>'s <c>ClaimAnswer</c>): <see cref="CutBy"/> a row, or <see cref="NoRow"/>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ClaimAnswer(
        float CutLineM, float MarginM, uint CutBy, int CutOn, float CutWayM, float Pad0, float Pad1, float Pad2)
    {
        public const uint NoRow = uint.MaxValue;
    }

    /// <summary>
    /// <b>The tally's words</b> (<c>claims_tally.glsl</c>): the counts every kernel over the rows or the holds stops at,
    /// the rebuild's own counts on the way to them, and where each indirect dispatch's groups start.
    /// </summary>
    internal static class Tally
    {
        public const int Rows = 0;
        public const int Holds = 1;
        public const int Candidates = 2;
        public const int Bodies = 3;
        public const int PassesEnd = 4;
        public const int PlannedHolds = 5;
        public const int PlannedRows = 6;
        public const int CandidatesGroups = 8;
        public const int RowsGroups = 11;
        public const int HoldsGroups = 14;
        public const int Words = 17;

        /// <summary>Where a word lies in the buffer, as an indirect dispatch names it.</summary>
        public static ulong BytesTo(int word) => (ulong)word * sizeof(uint);
    }

    /// <summary>The claims' own tables on the device, for whatever lays them there or reads them after.</summary>
    internal readonly record struct Tables(
        GpuBuffer Rows, GpuBuffer Holds, GpuBuffer Shown, GpuBuffer Answers, GpuBuffer BodyRuns, GpuBuffer PlannedRuns,
        GpuBuffer Order, GpuBuffer Tally);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ClaimsPush(uint PlansHidden, uint Pass, uint CarCount, uint Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ListsPush(uint WayCount, uint Pad);

    /// <summary>The stamps a host-fed answering is timed between.</summary>
    internal enum Stage
    {
        Began,
        Listed,
        Answered,
        Cut,
    }

    readonly Vk _vk;
    readonly int _ways;
    readonly List<GpuBuffer> _owned = [];
    readonly GpuBuffer _rows, _counts, _starts, _cursor, _order, _bodyRuns, _plannedRuns, _holds, _shown, _answers, _moved, _tally;
    readonly GpuBuffer _blockSums, _totals;
    readonly GpuBuffer? _rowsIn, _holdsIn, _shownIn, _tallyIn;
    readonly GpuBuffer _rowsOut, _holdsOut, _answersOut, _movedOut, _orderOut, _bodyRunsOut, _plannedRunsOut, _tallyOut;
    readonly ComputeKernel _count, _file, _sort, _answer, _show, _cut, _grant;
    readonly DescriptorSet _countSet, _fileSet, _sortSet, _answerSet, _showSet, _cutSet, _grantSet;
    readonly GpuBuffer _holders, _grants;
    int _carCount;
    readonly ScanKernels _scans;
    readonly PrefixScan _scan;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    readonly int _passes;
    int _rowCount;
    int _holdCount;

    /// <param name="mostPasses">The host's own bound (<see cref="Core.Config.RoadFigures.MostSettlingPasses"/>).</param>
    /// <param name="cars">How many cars' grants a host-fed answering reads back (<see cref="Holders"/>).</param>
    /// <param name="sentFromTheHost">
    /// Whether the claims are laid by the host and sent (<see cref="Lay"/>), which needs room to write them into — or only
    /// ever laid on the device, by whatever records into <see cref="OnTheDevice"/>.
    /// </param>
    public DeviceClaims(Vk vk, int ways, int slots, int holds, int mostPasses, int cars, bool sentFromTheHost = true)
    {
        if (2 * ways > ScanKernels.MostCounts) throw new ArgumentOutOfRangeException(nameof(ways), ways, "more ways than one scan covers");

        _vk = vk;
        _ways = ways;
        _passes = (mostPasses % 2 == 1 ? mostPasses : mostPasses + 1) + 1;

        _rows = Device<ClaimRow>(slots);
        _counts = Device<uint>(2 * ways);
        _starts = Device<uint>(2 * ways);
        _cursor = Device<uint>(2 * ways);
        _order = Device<uint>(slots);
        _bodyRuns = Device<UInt2>(ways);
        _plannedRuns = Device<UInt2>(ways);
        _holds = Device<HoldRow>(holds);
        _shown = Device<float>(holds);
        _answers = Device<ClaimAnswer>(holds);
        _moved = Device<uint>(_passes);
        _blockSums = Device<uint>(ScanKernels.Block);
        _totals = Device<uint>(1);
        _tally = Own(
            vk.CreateBuffer(
                Bytes<uint>(Tally.Words),
                BufferUsageFlags.StorageBufferBit | BufferUsageFlags.IndirectBufferBit | BufferUsageFlags.TransferSrcBit
                | BufferUsageFlags.TransferDstBit, HostAccess.None));

        if (sentFromTheHost)
        {
            _rowsIn = Staging<ClaimRow>(slots);
            _holdsIn = Staging<HoldRow>(holds);
            _shownIn = Staging<float>(holds);
            _tallyIn = Staging<uint>(Tally.Words);
        }

        _rowsOut = Readback<ClaimRow>(slots);
        _holdsOut = Readback<HoldRow>(holds);
        _answersOut = Readback<ClaimAnswer>(holds);
        _movedOut = Readback<uint>(_passes);
        _orderOut = Readback<uint>(slots);
        _bodyRunsOut = Readback<UInt2>(ways);
        _plannedRunsOut = Readback<UInt2>(ways);
        _tallyOut = Readback<uint>(Tally.Words);

        GpuBuffer[] lists = [_rows, _counts, _cursor, _order, _bodyRuns, _plannedRuns, _starts, _tally];
        var listsPush = (uint)Marshal.SizeOf<ListsPush>();
        _count = new ComputeKernel(vk, "claims_count.comp", lists.Length, listsPush);
        _file = new ComputeKernel(vk, "claims_file.comp", lists.Length, listsPush);
        _sort = new ComputeKernel(vk, "claims_sort.comp", lists.Length, listsPush);
        _countSet = _count.Bind(lists);
        _fileSet = _file.Bind(lists);
        _sortSet = _sort.Bind(lists);

        GpuBuffer[] answers = [_rows, _bodyRuns, _plannedRuns, _order, _order, _holds, _shown, _answers, _moved, _tally];
        var answersPush = (uint)Marshal.SizeOf<ClaimsPush>();
        _answer = new ComputeKernel(vk, "claims_answer.comp", answers.Length, answersPush);
        _show = new ComputeKernel(vk, "claims_show.comp", answers.Length, answersPush);
        _cut = new ComputeKernel(vk, "claims_cut.comp", answers.Length, answersPush);
        _answerSet = _answer.Bind(answers);
        _showSet = _show.Bind(answers);
        _cutSet = _cut.Bind(answers);

        _holders = Own(vk.CreateBuffer(Bytes<Vector2>(cars), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));
        _grants = Own(vk.CreateBuffer(Bytes<Vector4>(cars), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        GpuBuffer[] grants = [.. answers, _holders, _grants];
        _grant = new ComputeKernel(vk, "claims_grant.comp", grants.Length, answersPush);
        _grantSet = _grant.Bind(grants);

        _scans = new ScanKernels(vk);
        _scan = new PrefixScan(vk, _scans, _starts, _blockSums, _totals, 0);
        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
    }

    /// <summary>How many passes an answering runs.</summary>
    public int Passes => _passes;

    /// <summary>The claims' tables on the device: what a rebuild numbered there files its rows and holds into.</summary>
    public Tables OnTheDevice => new(_rows, _holds, _shown, _answers, _bodyRuns, _plannedRuns, _order, _tally);

    /// <summary>
    /// The claims as the last answering cut them, on the device, for a kernel that reads them after their cut
    /// (<see cref="DeviceCarTick"/>): every slot's row, each way's planned run, and the order the runs index.
    /// </summary>
    public (GpuBuffer Rows, GpuBuffer PlannedRuns, GpuBuffer PlannedOrder) Cut => (_rows, _plannedRuns, _order);

    /// <summary>Every reservation as the last answering left it — its cut taken — as many as were read back.</summary>
    public ReadOnlySpan<ClaimRow> Rows => _rowsOut.Span<ClaimRow>()[.._rowCount];

    /// <summary>Every hold as it was laid, as many as were read back.</summary>
    public ReadOnlySpan<HoldRow> Holds => _holdsOut.Span<HoldRow>()[.._holdCount];

    /// <summary>What every hold came to on the last pass — or, for a hold laid where it stands, what it was laid to.</summary>
    public ReadOnlySpan<ClaimAnswer> Answers => _answersOut.Span<ClaimAnswer>()[.._holdCount];

    /// <summary>How many holds moved in each pass of the last answering.</summary>
    public ReadOnlySpan<uint> Moved => _movedOut.Span<uint>();

    /// <summary>The tally as the last answering left it (<see cref="Tally"/>).</summary>
    public ReadOnlySpan<uint> TallyRead => _tallyOut.Span<uint>()[..Tally.Words];

    /// <summary>
    /// <b>Each car's hold this rebuild and where its claim began</b>, written by the caller before
    /// <see cref="Answer"/>: the hold's bits (minus one for none) and the metre, one pair a car.
    /// </summary>
    public Span<Vector2> Holders(int cars)
    {
        _carCount = cars;
        return _holders.Span<Vector2>()[..cars];
    }

    /// <summary>
    /// What each car with a hold answered with the plans got off it (<c>DrivingGround.ReadTheGrant</c>): its authority,
    /// the margin it keeps, and what cut it (a <c>HeadwayKind</c>).
    /// </summary>
    public ReadOnlySpan<Vector4> Grants => _grants.Span<Vector4>()[.._carCount];

    /// <summary>One way's bodies, or its planned pieces, as the last answering laid them before it answered — rows, nearest first.</summary>
    public ReadOnlySpan<uint> ListOf(int way, bool bodies)
    {
        var run = (bodies ? _bodyRunsOut : _plannedRunsOut).Span<UInt2>()[way];
        return _orderOut.Span<uint>().Slice((int)run.X, (int)run.Y);
    }

    /// <summary>The device's time between two stages of the last host-fed answering, in milliseconds.</summary>
    public double BetweenMs(Stage from, Stage to) => _timer.BetweenMs((int)from, (int)to);

    /// <summary>
    /// <b>The reservations as the host laid them</b>, every plan whole and none answered yet, written into what the device
    /// is sent: every slot a row, every hold with its holder's terms, and the tally.
    /// </summary>
    public void Lay(LaneOccupancy claims)
    {
        if (claims.WayCount != _ways) throw new InvalidOperationException("The claims are over another town's ways.");
        if (_rowsIn is null || _holdsIn is null || _shownIn is null || _tallyIn is null)
        {
            throw new InvalidOperationException("These claims are laid on the device and have no room for the host's.");
        }

        var rows = _rowsIn.Span<ClaimRow>();
        for (var slot = 0; slot < claims.SlotCount; slot++) rows[slot] = ClaimRow.OfSlot(claims, slot);

        _rowCount = claims.SlotCount;
        var holds = _holdsIn.Span<HoldRow>();
        var shown = _shownIn.Span<float>();
        _holdCount = claims.HoldCount;
        for (var hold = 0; hold < _holdCount; hold++)
        {
            var (first, end) = claims.PiecesOf(hold);
            var terms = claims.TermsOf(hold);
            var answered = claims.IsAnswered(hold);
            var endsAtM = claims.HoldEndsAtM(hold, out var marginM, out _);
            holds[hold] = new HoldRow(
                (uint)first, (uint)end, claims.StandingMarginM(hold), (answered ? HoldRow.Answered : 0u) | (terms.OnACall ? HoldRow.OnACall : 0u),
                terms.KeptOffWhatItMayPassM, terms.PassesBelowMps, endsAtM, marginM, ClaimAnswer.NoRow, claims.HoldCutOn(hold));
            shown[hold] = answered ? float.NegativeInfinity : float.PositiveInfinity;
        }

        var tally = _tallyIn.Span<uint>();
        tally.Clear();
        tally[Tally.Rows] = (uint)_rowCount;
        tally[Tally.Holds] = (uint)_holdCount;
        GroupsAt(tally, Tally.RowsGroups, Groups(_rowCount));
        GroupsAt(tally, Tally.HoldsGroups, Groups(_holdCount));
    }

    /// <summary>
    /// <b>The reservations as the town's own answering cut them</b>, every slot a row as it stands — for a kernel that reads
    /// the claims after their cut (<see cref="DeviceCarTick"/>) where the town answered them itself. Sent and listed by
    /// <see cref="RecordAsCut"/>.
    /// </summary>
    public void LayAsCut(LaneOccupancy claims)
    {
        if (_rowsIn is null || _tallyIn is null) throw new InvalidOperationException("These claims are laid on the device and have no room for the host's.");

        var rows = _rowsIn.Span<ClaimRow>();
        for (var slot = 0; slot < claims.SlotCount; slot++)
        {
            rows[slot] = ClaimRow.Of(claims.Slots[slot], claims.SlotWays[slot], claims.SlotsGone[slot], claims.Slots[slot].FromM);
        }

        _rowCount = claims.SlotCount;
        _holdCount = 0;
        var tally = _tallyIn.Span<uint>();
        tally.Clear();
        tally[Tally.Rows] = (uint)_rowCount;
        GroupsAt(tally, Tally.RowsGroups, Groups(_rowCount));
        GroupsAt(tally, Tally.HoldsGroups, 0u);
    }

    /// <summary>The rows <see cref="LayAsCut"/> laid sent and every way's lists laid over them, recorded into a caller's submit.</summary>
    public void RecordAsCut(CommandBuffer commands)
    {
        _vk.Copy(commands, _rowsIn!, _rows, Bytes<ClaimRow>(_rowCount));
        _vk.Copy(commands, _tallyIn!, _tally, Bytes<uint>(Tally.Words));
        _vk.AfterTransfer(commands);
        RecordLists(commands);
    }

    /// <summary>
    /// <b>The lists laid, every pass answered and the holds cut</b>, and the rows, the lists, the answers and what moved
    /// read back. One submit and one wait.
    /// </summary>
    public void Answer()
    {
        var vk = _vk;
        var commands = _batch.Begin();
        _timer.Reset(commands);
        vk.Copy(commands, _rowsIn!, _rows, Bytes<ClaimRow>(_rowCount));
        vk.Copy(commands, _holdsIn!, _holds, Bytes<HoldRow>(_holdCount));
        vk.Copy(commands, _shownIn!, _shown, Bytes<float>(_holdCount));
        vk.Copy(commands, _tallyIn!, _tally, Bytes<uint>(Tally.Words));
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        RecordLists(commands);
        _timer.Stamp(commands, (int)Stage.Listed);
        RecordAnswers(commands);
        _timer.Stamp(commands, (int)Stage.Answered);
        RecordCut(commands);
        _grant.Dispatch(commands, _grantSet, Groups(_carCount), new ClaimsPush(0u, 0u, (uint)_carCount, 0u));
        vk.BeforeTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Cut);

        RecordRead(commands, _rowCount, _holdCount);
        _batch.SubmitAndWait();
        _timer.Read();
    }

    /// <summary>
    /// <b>Every way's two lists laid from the rows the tally counts</b>, recorded: counted, scanned, filed and each way's
    /// run sorted — and made visible to whatever reads them next.
    /// </summary>
    public void RecordLists(CommandBuffer commands)
    {
        var vk = _vk;
        vk.BeforeTransfer(commands);
        vk.Fill(commands, _counts, 0);
        vk.AfterTransfer(commands);
        var lists = new ListsPush((uint)_ways, 0u);
        _count.DispatchIndirect(commands, _countSet, _tally, Tally.BytesTo(Tally.RowsGroups), lists);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, _counts, _starts, _counts.SizeBytes);
        vk.AfterTransfer(commands);
        _scan.Record(commands, 2 * _ways);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, _starts, _cursor, _starts.SizeBytes);
        vk.AfterTransfer(commands);
        _file.DispatchIndirect(commands, _fileSet, _tally, Tally.BytesTo(Tally.RowsGroups), lists);
        vk.BetweenKernels(commands);
        _sort.Dispatch(commands, _sortSet, Groups(_ways), lists);
        vk.BetweenKernels(commands);
    }

    /// <summary><b>Every pass the host could run</b>, recorded over the holds the tally counts, each answered then shown.</summary>
    public void RecordAnswers(CommandBuffer commands)
    {
        var vk = _vk;
        vk.BeforeTransfer(commands);
        vk.Fill(commands, _moved, 0);
        vk.AfterTransfer(commands);
        for (var pass = 0; pass < _passes; pass++)
        {
            var push = new ClaimsPush(pass == 0 ? 1u : 0u, (uint)pass, 0u, 0u);
            _answer.DispatchIndirect(commands, _answerSet, _tally, Tally.BytesTo(Tally.HoldsGroups), push);
            vk.BetweenKernels(commands);
            _show.DispatchIndirect(commands, _showSet, _tally, Tally.BytesTo(Tally.HoldsGroups), push);
            vk.BetweenKernels(commands);
        }
    }

    /// <summary>Every hold answered with the plans cut where its last answer says, recorded.</summary>
    public void RecordCut(CommandBuffer commands)
    {
        _cut.DispatchIndirect(commands, _cutSet, _tally, Tally.BytesTo(Tally.HoldsGroups), new ClaimsPush(0u, 0u, 0u, 0u));
        _vk.BetweenKernels(commands);
    }

    /// <summary>
    /// The first <paramref name="rows"/> rows and <paramref name="holds"/> holds, their answers, the lists, what moved and
    /// the tally copied back, recorded after a transfer barrier.
    /// </summary>
    public void RecordRead(CommandBuffer commands, int rows, int holds)
    {
        var vk = _vk;
        _rowCount = rows;
        _holdCount = holds;
        vk.Copy(commands, _rows, _rowsOut, Bytes<ClaimRow>(rows));
        vk.Copy(commands, _holds, _holdsOut, Bytes<HoldRow>(holds));
        vk.Copy(commands, _answers, _answersOut, Bytes<ClaimAnswer>(holds));
        vk.Copy(commands, _moved, _movedOut, _moved.SizeBytes);
        vk.Copy(commands, _order, _orderOut, Bytes<uint>(rows));
        vk.Copy(commands, _bodyRuns, _bodyRunsOut, _bodyRuns.SizeBytes);
        vk.Copy(commands, _plannedRuns, _plannedRunsOut, _plannedRuns.SizeBytes);
        vk.Copy(commands, _tally, _tallyOut, _tally.SizeBytes);
    }

    static void GroupsAt(Span<uint> tally, int at, uint groups)
    {
        tally[at] = groups;
        tally[at + 1] = 1u;
        tally[at + 2] = 1u;
    }

    static uint Groups(int count) => (uint)Math.Max(1, (count + 63) / 64);

    GpuBuffer Device<T>(int count) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None));

    GpuBuffer Staging<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferSrcBit, HostAccess.Writes));

    GpuBuffer Readback<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferDstBit, HostAccess.Reads));

    static unsafe ulong Bytes<T>(int count) where T : unmanaged => (ulong)(Math.Max(1, count) * sizeof(T));

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _batch.Dispose();
        _scans.Dispose();
        foreach (var kernel in (ReadOnlySpan<ComputeKernel>)[_count, _file, _sort, _answer, _show, _cut, _grant]) kernel.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
