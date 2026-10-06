using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The plans answered on the device</b> (<c>world/road/kernels/claims.glsl</c>), beside the host's: the reservations
/// the town laid sent across as rows, every hold answered pass after pass as the host answers them
/// (<see cref="LaneOccupancy.AnswerThePlans"/>), and cut where its last answer says — read back to be held to the
/// host's. One submit and one wait.
/// </summary>
/// <remarks>
/// <b>The device runs every pass the host could</b>: the first, and then on to the first odd pass at the bound. A pass
/// after the answers have stopped moving answers them all the same again, so the passes the host never needed change
/// nothing, and no count is read back between them.
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
    }

    /// <summary>One hold as the kernels read it (<c>claims_rows.glsl</c>'s <c>HoldRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct HoldRow(
        uint FirstRow, uint EndRow, float StandingMarginM, uint Flags, float KeptOffWhatItMayPassM, float PassesBelowMps,
        float LaidCutLineM, float LaidMarginM, uint LaidCutBy, int LaidCutOn, float Pad0 = 0f, float Pad1 = 0f);

    /// <summary>What one hold was answered (<c>claims_rows.glsl</c>'s <c>ClaimAnswer</c>): <see cref="CutBy"/> a row, or <see cref="NoRow"/>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ClaimAnswer(
        float CutLineM, float MarginM, uint CutBy, int CutOn, float CutWayM, float Pad0, float Pad1, float Pad2)
    {
        public const uint NoRow = uint.MaxValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ClaimsPush(uint HoldCount, uint PlansHidden, uint Pass, uint Pad);

    /// <summary>The stamps an answering is timed between.</summary>
    internal enum Stage
    {
        Began,
        Answered,
        Cut,
    }

    const uint Answered = 1u;
    const uint OnACall = 2u;

    readonly Vk _vk;
    readonly List<GpuBuffer> _owned = [];
    readonly GpuBuffer _rows, _bodyRuns, _plannedRuns, _bodyOrder, _plannedOrder, _holds, _shown, _answers, _moved;
    readonly GpuBuffer _rowsIn, _bodyRunsIn, _plannedRunsIn, _bodyOrderIn, _plannedOrderIn, _holdsIn, _shownIn;
    readonly GpuBuffer _rowsOut, _answersOut, _movedOut;
    readonly ComputeKernel _answer, _show, _cut;
    readonly DescriptorSet _answerSet, _showSet, _cutSet;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    readonly int[] _listed;
    readonly int _passes;
    int _rowCount;
    int _holdCount;
    int _bodiesListed;
    int _plannedListed;

    /// <param name="mostPasses">The host's own bound (<see cref="Core.Config.RoadFigures.MostSettlingPasses"/>).</param>
    public DeviceClaims(Vk vk, int ways, int slots, int holds, int mostPasses)
    {
        _vk = vk;
        _passes = (mostPasses % 2 == 1 ? mostPasses : mostPasses + 1) + 1;
        _listed = new int[slots];

        _rows = Device<ClaimRow>(slots);
        _bodyRuns = Device<UInt2>(ways);
        _plannedRuns = Device<UInt2>(ways);
        _bodyOrder = Device<uint>(slots);
        _plannedOrder = Device<uint>(slots);
        _holds = Device<HoldRow>(holds);
        _shown = Device<float>(holds);
        _answers = Device<ClaimAnswer>(holds);
        _moved = Device<uint>(_passes);

        _rowsIn = Staging<ClaimRow>(slots);
        _bodyRunsIn = Staging<UInt2>(ways);
        _plannedRunsIn = Staging<UInt2>(ways);
        _bodyOrderIn = Staging<uint>(slots);
        _plannedOrderIn = Staging<uint>(slots);
        _holdsIn = Staging<HoldRow>(holds);
        _shownIn = Staging<float>(holds);

        _rowsOut = Readback<ClaimRow>(slots);
        _answersOut = Readback<ClaimAnswer>(holds);
        _movedOut = Readback<uint>(_passes);

        GpuBuffer[] set = [_rows, _bodyRuns, _plannedRuns, _bodyOrder, _plannedOrder, _holds, _shown, _answers, _moved];
        var pushBytes = (uint)Marshal.SizeOf<ClaimsPush>();
        _answer = new ComputeKernel(vk, "claims_answer.comp", set.Length, pushBytes);
        _show = new ComputeKernel(vk, "claims_show.comp", set.Length, pushBytes);
        _cut = new ComputeKernel(vk, "claims_cut.comp", set.Length, pushBytes);
        _answerSet = _answer.Bind(set);
        _showSet = _show.Bind(set);
        _cutSet = _cut.Bind(set);
        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
    }

    /// <summary>How many passes an answering runs.</summary>
    public int Passes => _passes;

    /// <summary>Every reservation as the last answering left it — its cut taken.</summary>
    public ReadOnlySpan<ClaimRow> Rows => _rowsOut.Span<ClaimRow>()[.._rowCount];

    /// <summary>What every hold came to on the last pass.</summary>
    public ReadOnlySpan<ClaimAnswer> Answers => _answersOut.Span<ClaimAnswer>()[.._holdCount];

    /// <summary>How many holds moved in each pass of the last answering.</summary>
    public ReadOnlySpan<uint> Moved => _movedOut.Span<uint>();

    /// <summary>The device's time between two stages of the last answering, in milliseconds.</summary>
    public double BetweenMs(Stage from, Stage to) => _timer.BetweenMs((int)from, (int)to);

    /// <summary>
    /// <b>The reservations as the host laid them</b>, every plan whole and none answered yet, written into what the device
    /// is sent: every slot a row, every way's two lists in their order, every hold with its holder's terms.
    /// </summary>
    public void Lay(LaneOccupancy claims)
    {
        var slots = claims.Slots;
        var slotWays = claims.SlotWays;
        var gone = claims.SlotsGone;
        var standsToM = claims.SlotStandsToM;
        var rows = _rowsIn.Span<ClaimRow>();
        for (var slot = 0; slot < slots.Length; slot++)
        {
            ref readonly var claim = ref slots[slot];
            var proposed = !claim.HasBody && !claim.Secondary && claims.IsAnswered(claim.Hold);
            rows[slot] = ClaimRow.Of(claim, slotWays[slot], gone[slot], proposed ? standsToM[slot] : claim.FromM);
        }

        _rowCount = slots.Length;
        _bodiesListed = LayTheLists(claims, bodies: true, _bodyRunsIn.Span<UInt2>(), _bodyOrderIn.Span<uint>());
        _plannedListed = LayTheLists(claims, bodies: false, _plannedRunsIn.Span<UInt2>(), _plannedOrderIn.Span<uint>());

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
                (uint)first, (uint)end, claims.StandingMarginM(hold), (answered ? Answered : 0u) | (terms.OnACall ? OnACall : 0u),
                terms.KeptOffWhatItMayPassM, terms.PassesBelowMps, endsAtM, marginM, ClaimAnswer.NoRow, claims.HoldCutOn(hold));
            shown[hold] = answered ? float.NegativeInfinity : float.PositiveInfinity;
        }
    }

    /// <returns>How many entries the lists hold between them.</returns>
    int LayTheLists(LaneOccupancy claims, bool bodies, Span<UInt2> runs, Span<uint> order)
    {
        var next = 0;
        for (var way = 0; way < claims.WayCount; way++)
        {
            var count = claims.ListOf(way, bodies, _listed);
            runs[way] = new UInt2((uint)next, (uint)count);
            for (var at = 0; at < count; at++) order[next + at] = (uint)_listed[at];
            next += count;
        }

        return next;
    }

    /// <summary><b>Every pass answered and the holds cut</b>, and the rows, the answers and what moved read back.</summary>
    public void Answer()
    {
        var vk = _vk;
        var commands = _batch.Begin();
        _timer.Reset(commands);
        vk.Copy(commands, _rowsIn, _rows, Bytes<ClaimRow>(_rowCount));
        vk.Copy(commands, _bodyRunsIn, _bodyRuns, _bodyRunsIn.SizeBytes);
        vk.Copy(commands, _plannedRunsIn, _plannedRuns, _plannedRunsIn.SizeBytes);
        vk.Copy(commands, _bodyOrderIn, _bodyOrder, Bytes<uint>(_bodiesListed));
        vk.Copy(commands, _plannedOrderIn, _plannedOrder, Bytes<uint>(_plannedListed));
        vk.Copy(commands, _holdsIn, _holds, Bytes<HoldRow>(_holdCount));
        vk.Copy(commands, _shownIn, _shown, Bytes<float>(_holdCount));
        vk.Fill(commands, _moved, 0);
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        var groups = (uint)Math.Max(1, (_holdCount + 63) / 64);
        for (var pass = 0; pass < _passes; pass++)
        {
            var push = new ClaimsPush((uint)_holdCount, pass == 0 ? 1u : 0u, (uint)pass, 0u);
            _answer.Dispatch(commands, _answerSet, groups, push);
            vk.BetweenKernels(commands);
            _show.Dispatch(commands, _showSet, groups, push);
            vk.BetweenKernels(commands);
        }

        _timer.Stamp(commands, (int)Stage.Answered);
        _cut.Dispatch(commands, _cutSet, groups, new ClaimsPush((uint)_holdCount, 0u, 0u, 0u));
        vk.BeforeTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Cut);

        vk.Copy(commands, _rows, _rowsOut, Bytes<ClaimRow>(_rowCount));
        vk.Copy(commands, _answers, _answersOut, Bytes<ClaimAnswer>(_holdCount));
        vk.Copy(commands, _moved, _movedOut, _moved.SizeBytes);
        _batch.SubmitAndWait();
        _timer.Read();
    }

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
        _answer.Dispose();
        _show.Dispose();
        _cut.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
