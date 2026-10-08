using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The walkers kept as the device's own</b> (<see cref="Keeps"/>), for a town whose walkers the device ticks too: one
/// table every stage of every rebuild runs over in turn — stationed, their bodies and passes laid, their walks planned and,
/// with the answers, their grants read and their aims set — read in where the bodies' step left each walker
/// (<c>walkers_kept_gather.comp</c>), with only what the town changed since written over it (<c>walkers_kept_patch.comp</c>),
/// and only what the stages changed handed back (<c>walkers_kept_out.comp</c>) for the town to take up as its own
/// (<see cref="TownWorld.KeptOnTheDevice"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>What the town holds of a walker is what the device left</b>, but where the town changed it since: every field a stage
/// writes is taken back whole, so a walker whose row the town would lay other than the device last left it is one the town
/// changed — and it is written over whole. Not compared: where the walker stands, its pace and how fast it goes the way it
/// faces, which are read in off the walker tick's table where that holds the town's (<see cref="DeviceWalkerTick.HoldsTheBodyOf"/>);
/// the hold its walk laid, which each rebuild numbers again; and the straight it walks, which the device's walks commit
/// to and the town's never do while the device keeps the walker.
/// </para>
/// <para>
/// <b>Numbers the device's</b> (root decision log, 2026-10-08): a walker's place on its way is the device's projection, and
/// how fast it goes the way it faces its own trigonometry's, where the town's own rebuild stationed it to the bit.
/// </para>
/// </remarks>
internal sealed partial class DeviceWalkers
{
    /// <summary>What the town changed of one kept walker (<c>walkers_kept_patch.comp</c>'s <c>KeptPatch</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct KeptPatch(WalkerRow Row, uint Walker, uint Kept);

    /// <summary>What a rebuild's stages made of one kept walker (<c>walkers_kept_out.comp</c>'s <c>WalkerOut</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkerOut(
        Vector2 DestinationM, Vector2 BackToM, float OnWayM, float OffWayM, float GrantM, float PassFromM, float PassClearM,
        float PassAsideM, int PassSlot, int RouteTaken, int OnWay, int OnCrossing, int HeldAs, uint Flags, uint Action, int Hold,
        uint Events, uint Walker);

    /// <summary>
    /// How much more than the last rebuild handed back the next is read back as: an eighth more, and this many walkers
    /// besides. A rebuild that changed more has the rest read in a second submit (<see cref="KeptReadPast"/>).
    /// </summary>
    const int KeptReadPastShare = 8, KeptReadPastSlack = 256;

    GpuBuffer _keptTable = null!, _keptBefore = null!, _keptPatchesIn = null!, _keptPatches = null!;
    GpuBuffer _keptOutRows = null!, _keptOutCount = null!, _keptOutRead = null!, _keptOutCountRead = null!;
    ComputeKernel? _keptGather, _keptPatch, _keptOut, _keptAims;
    DescriptorSet _keptGatherSet, _keptPatchSet, _keptOutSet, _keptAimsSet;

    /// <summary>Every kept walker's row as the device is known to hold it, and whether it is known at all.</summary>
    WalkerRow[] _known = [];

    bool[] _isKnown = [];

    int _keptPatchCount, _keptOutBound, _keptOutTaken;

    /// <summary>Whether the device keeps every walker's side of the claims as its own (<see cref="DeviceWalkers"/>'s <c>keptFrom</c>).</summary>
    public bool Keeps { get; private set; }

    /// <summary>How many walkers the town changed since the last rebuild, written over the kept table by this one.</summary>
    public int KeptPatched => _keptPatchCount;

    /// <summary>How many walkers the last rebuild's stages changed, handed back.</summary>
    public int KeptChanged => _keptOutTaken;

    /// <summary>How many rebuilds changed more walkers than their submit read back, so the rest was read in a second.</summary>
    public long KeptReadPast { get; private set; }

    /// <summary>
    /// <b>Whether to count, after every rebuild is taken back, the walkers the town would still lay other than the device
    /// left them</b> (<see cref="KeptUnlike"/>) — a check of the taking back, at the cost of laying every walker again.
    /// </summary>
    public bool ChecksWhatWasTaken { get; set; }

    /// <summary>How many walkers the town held other than the device left them, right after a rebuild was taken back, over every rebuild checked.</summary>
    public long KeptUnlike { get; private set; }

    void KeepThem(GpuBuffer table)
    {
        Keeps = true;
        _keptTable = _on[(int)Piece.Station].Walkers;
        _keptBefore = Device<WalkerRow>(_walkers);
        _keptPatchesIn = Staging<KeptPatch>(_walkers);
        _keptPatches = Device<KeptPatch>(_walkers);
        _keptOutRows = Device<WalkerOut>(_walkers);
        _keptOutCount = Device<uint>(1);
        _keptOutRead = Readback<WalkerOut>(_walkers);
        _keptOutCountRead = Readback<uint>(1);
        _keptGather = new ComputeKernel(_vk, "walkers_kept_gather.comp", 3, sizeof(uint));
        _keptGatherSet = _keptGather.Bind([_keptTable, _keptBefore, table]);
        _keptPatch = new ComputeKernel(_vk, "walkers_kept_patch.comp", 3, sizeof(uint));
        _keptPatchSet = _keptPatch.Bind([_keptPatches, _keptTable, _keptBefore]);
        _keptOut = new ComputeKernel(_vk, "walkers_kept_out.comp", 4, sizeof(uint));
        _keptOutSet = _keptOut.Bind([_keptTable, _keptBefore, _keptOutRows, _keptOutCount]);
        _keptAims = new ComputeKernel(_vk, "walkers_kept_aims.comp", 3, sizeof(uint));
        _keptAimsSet = _keptAims.Bind([_keptOutRows, _keptOutCount, table]);
        _known = new WalkerRow[_walkers];
        _isKnown = new bool[_walkers];
        _keptOutBound = _walkers;
    }

    /// <summary>
    /// <b>Kept: what the town changed of its walkers since the device last left them</b>, staged to be written over the
    /// kept table — each walker whose row the town would lay other than the device is known to hold it, or whose body the
    /// walker tick's table does not hold where the town has it — and each walker's route and pass's ground where they changed.
    /// A walker new to the device is written whole, the straight the town walks it down among it.
    /// </summary>
    public void LayTheKept(TownWorld world, DeviceWalkerTick ticked)
    {
        var people = world.People;
        _walkerCount = people.Count;
        _keptPatchCount = 0;
        NameTheOfficers(world);

        var patches = _keptPatchesIn.Span<KeptPatch>();
        for (var person = 0; person < people.Count; person++)
        {
            _routes!.LayRows(person, people.RouteOf(person)[..people.RouteCount[person]]);
            _passes!.LayRows(person, world.Sidestepping.SweptOf(person));
            var row = RowOf(world, person);
            var kept = _isKnown[person];
            if (kept)
            {
                row.Hold = _known[person].Hold;
                if (SameToTheClaims(row, _known[person]) && (row.Is(WalkerRow.Inside) || ticked.HoldsTheBodyOf(people, person))) continue;
            }
            else
            {
                row.Hold = LaneOccupancy.NoHold;
                _straights!.LayRows(person, world.WalkingGround.StraightOf(person), again: true);
            }

            patches[_keptPatchCount++] = new KeptPatch(row, (uint)person, kept ? 1u : 0u);
            _known[person] = row;
            _isKnown[person] = true;
        }
    }

    /// <summary>
    /// Whether two rows of one walker are the same to the claims but for what the kept table reads in or keeps itself: where
    /// it stands, its pace and how fast it goes the way it faces, the hold its walk laid, the straight it walks, and what the
    /// last rebuild did to it.
    /// </summary>
    static bool SameToTheClaims(WalkerRow one, WalkerRow other)
    {
        Unread(ref one);
        Unread(ref other);
        return MemoryMarshal.AsBytes(new ReadOnlySpan<WalkerRow>(in one)).SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<WalkerRow>(in other)));

        static void Unread(ref WalkerRow row)
        {
            row.PositionM = row.DeclaredMps = row.StraightFromM = row.StraightToM = Vector2.Zero;
            row.AlongMps = 0f;
            row.Hold = 0;
            row.StraightRuns = row.Events = 0u;
            row.Flags &= ~WalkerRow.OnAStraight;
        }
    }

    /// <summary>
    /// Kept: what <see cref="LayTheKept"/> staged sent — the patches, and the routes, straights and passes' ground that
    /// changed — and the count of what is handed back cleared. The caller barriers.
    /// </summary>
    public void RecordTheKeptSend(CommandBuffer commands)
    {
        _vk.Copy(commands, _keptPatchesIn, _keptPatches, Bytes<KeptPatch>(_keptPatchCount));
        _routes!.Send(commands);
        _straights!.Send(commands);
        _passes!.Send(commands);
        _vk.Fill(commands, _keptOutCount, 0);
    }

    /// <summary>How many bytes <see cref="RecordTheKeptSend"/> copies across.</summary>
    ulong KeptSentBytes =>
        ((ulong)_keptPatchCount * (ulong)Marshal.SizeOf<KeptPatch>()) + _routes!.MarkedBytes + _straights!.MarkedBytes + _passes!.MarkedBytes;

    /// <summary>
    /// Kept: every walker read in where the bodies' step left it, and what the town changed written over it, recorded and
    /// made visible to the stages.
    /// </summary>
    public void RecordTheKeptIn(CommandBuffer commands)
    {
        _keptGather!.Dispatch(commands, _keptGatherSet, Groups(_walkerCount), (uint)_walkerCount);
        _vk.BetweenKernels(commands);
        if (_keptPatchCount == 0) return;

        _keptPatch!.Dispatch(commands, _keptPatchSet, Groups(_keptPatchCount), (uint)_keptPatchCount);
        _vk.BetweenKernels(commands);
    }

    /// <summary>Kept: every walker the stages changed written out and counted, recorded; the caller barriers before reading it back.</summary>
    public void RecordTheKeptOut(CommandBuffer commands) =>
        _keptOut!.Dispatch(commands, _keptOutSet, Groups(_walkerCount), (uint)_walkerCount);

    /// <summary>
    /// Kept: each walker the last rebuild's stages changed aimed and walking as they left it, in the walker tick's table
    /// (<c>walkers_kept_aims.comp</c>) — recorded in the next tick's submit, after the walkers the town wrote whole over
    /// it, and before the tick; the caller barriers.
    /// </summary>
    public void RecordTheKeptAims(CommandBuffer commands) =>
        _keptAims!.Dispatch(commands, _keptAimsSet, Groups(_walkerCount), (uint)_walkerCount);

    /// <summary>Kept: the count of what the stages changed, and as many of the rows as the last rebuild's count and a margin, copied back.</summary>
    public void RecordReadTheKept(CommandBuffer commands)
    {
        _keptOutBound = Math.Min(_walkerCount, _keptOutBound);
        _vk.Copy(commands, _keptOutCount, _keptOutCountRead, sizeof(uint));
        _vk.Copy(commands, _keptOutRows, _keptOutRead, Bytes<WalkerOut>(_keptOutBound));
    }

    /// <summary>How many bytes <see cref="RecordReadTheKept"/> copies back.</summary>
    public ulong KeptReadBytes => sizeof(uint) + Bytes<WalkerOut>(_keptOutBound);

    /// <summary>
    /// <b>Kept: what the stages made of every walker they changed taken up by the town</b> (<see cref="TownWorld.KeptOnTheDevice"/>)
    /// and as what the device holds — the rows past what the submit read back read in a second, and each one's aim and
    /// walk as the walker tick's table holds them from the next tick's submit (<see cref="RecordTheKeptAims"/>) — and every
    /// walker's hold as the rebuild numbered it (<see cref="TownWorld.WalkedOnTheDevice"/>).
    /// </summary>
    /// <param name="ticked">The walker tick whose table each changed walker's aim and walk are written into.</param>
    /// <param name="unitHolds">The hold each unit's first was numbered, read back.</param>
    /// <param name="firstWalker">The unit the first walker's walk is.</param>
    public void TakeTheKept(TownWorld town, DeviceWalkerTick ticked, ReadOnlySpan<uint> unitHolds, int firstWalker)
    {
        var count = (int)_keptOutCountRead.Span<uint>()[0];
        if (count > _keptOutBound)
        {
            KeptReadPast++;
            var commands = _batch.Begin();
            _vk.Copy(commands, _keptOutRows, _keptOutRead, Bytes<WalkerOut>(count));
            _batch.SubmitAndWait();
        }

        _keptOutTaken = count;
        _keptOutBound = count + (count / KeptReadPastShare) + KeptReadPastSlack;
        foreach (ref readonly var made in _keptOutRead.Span<WalkerOut>()[..count])
        {
            var person = (int)made.Walker;
            ref var known = ref _known[person];
            known.DestinationM = made.DestinationM;
            known.BackToM = made.BackToM;
            known.OnWayM = made.OnWayM;
            known.OffWayM = made.OffWayM;
            known.GrantM = made.GrantM;
            known.PassFromM = made.PassFromM;
            known.PassClearM = made.PassClearM;
            known.PassAsideM = made.PassAsideM;
            known.PassSlot = made.PassSlot;
            known.RouteTaken = made.RouteTaken;
            known.OnWay = made.OnWay;
            known.OnCrossing = made.OnCrossing;
            known.HeldAs = made.HeldAs;
            known.Flags = made.Flags;
            known.Action = made.Action;
            known.Hold = made.Hold;
            town.KeptOnTheDevice(
                person,
                new KeptWalker(
                    made.DestinationM, made.BackToM, made.OnWayM, made.OffWayM, made.GrantM,
                    new Sidestep(made.PassSlot, made.PassFromM, made.PassClearM, made.PassAsideM, known.Is(WalkerRow.PassBegun)),
                    made.RouteTaken, made.OnWay, made.OnCrossing, made.HeldAs,
                    known.Is(WalkerRow.HeldAsDriving) ? LaneRoster.Driving : LaneRoster.Walking, known.Is(WalkerRow.Walking),
                    (PersonAction)made.Action, (made.Events & WalkerRow.MadeItsPass) != 0, (made.Events & WalkerRow.WithdrewItsPass) != 0));
            ticked.KnowTheAim(person, made.DestinationM, known.Is(WalkerRow.Walking));
        }

        for (var person = 0; person < _walkerCount; person++)
        {
            var hold = _known[person].Hold != LaneOccupancy.NoHold ? (int)unitHolds[firstWalker + person] : LaneOccupancy.NoHold;
            town.WalkedOnTheDevice(person, hold, committedAStraight: false);
        }

        if (ChecksWhatWasTaken) KeptUnlike += Unlike(town);
    }

    /// <summary>How many walkers the town would lay other than the device is known to hold them, but for what is never compared.</summary>
    int Unlike(TownWorld town)
    {
        NameTheOfficers(town);
        var unlike = 0;
        for (var person = 0; person < _walkerCount; person++)
        {
            var row = RowOf(town, person);
            if (!SameToTheClaims(row, _known[person])) unlike++;
        }

        return unlike;
    }
}
