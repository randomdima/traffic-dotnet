using System.Diagnostics;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using static TrafficSimulation.App.DeviceTick.DeviceClaims;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The rebuild laid in the town's place</b> (<see cref="ITownClaims.ClaimsAt"/>): what only the town lays gathered stage
/// by stage as the shadow gathers it, every producer sent what it reads once — the cars' lines read where the car tick
/// keeps them, the walkers kept on the device — then bodies, lights, plans, walks and answers in one submit, and what they
/// came to written back into the town before it lays the backing; the backing and the grants are recorded at the head of
/// the tick's own submit (<see cref="RecordTheRest"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Read back as far as the last rebuild's counts and a margin</b> (<see cref="DeviceClaims.RecordReadBounded"/>), since
/// how many bodies are laid is the device's alone and a copy's size is the host's to say: every row, hold, answer and what
/// cut each hold, the lists' order, how each unit was numbered, what each plan came to, the channels under each car, what
/// each car on a pass found and what each walk came to. A rebuild that laid past the margin has the rest read in a second
/// submit (<see cref="ReadPast"/>), counted.
/// </para>
/// <para>
/// <b>A car on a pass is planned by the town as though its pass were clear</b>, and the device asks whether a body stands in
/// it (<c>rebuild_asks.comp</c>): where one does, that car's plan is never filed and its claim is the hold short of the body,
/// as <c>Following.Plan</c> lays it.
/// </para>
/// </remarks>
internal sealed partial class DeviceRebuild
{
    /// <summary>The most cars on a pass one rebuild sends asks for: past it, the town lays its own claims.</summary>
    public const int MostCarsOnAPass = 512;

    /// <summary>The car tick whose lines the plans read and whose grant tables the grants are written into — none for the shadow.</summary>
    readonly DeviceCarTick? _carTick;

    readonly bool _ownsTheClaims;

    /// <summary>What a rebuild in the town hands back besides the claims.</summary>
    GpuBuffer _unitHoldsOut = null!;

    /// <summary>What the rebuild says of each car and each walker, packed (<c>rebuild_agents_out.comp</c>), on the device and read back.</summary>
    GpuBuffer _carsOut = null!, _carsRead = null!, _walkersOut = null!, _walkersRead = null!;

    Bound _agentsOutKernel;

    /// <summary>What a rebuild says of one car (<c>rebuild_agents_out.comp</c>'s <c>CarOut</c>): its plan, and bits.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly record struct CarOut(float ClaimToM, float CommittedToM, float HorizonM, uint Bits)
    {
        public const uint LaidAHold = 1u, HeldInItsPass = 2u;
        public const int ChannelsShift = 8, WheelsShift = 16;
    }

    /// <summary>
    /// What a rebuild says of one walker (<c>rebuild_agents_out.comp</c>'s <c>WalkOut</c>): bits, and what its hold came to —
    /// where it ended, what it keeps off what cut it and the way it was cut on (infinity, nothing and none with no hold).
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly record struct WalkOut(uint Bits, float EndsAtM, float MarginM, int CutOn)
    {
        public const uint LaidAHold = 1u, CommittedAStraight = 2u;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct AgentsPush(uint CarCount, uint WalkerCount, uint FirstWalkerUnit);

    /// <summary>Every hold as the town keeps it once answered (<c>rebuild_holds_out.comp</c>), on the device and read back.</summary>
    GpuBuffer _holdsOut = null!, _holdsRead = null!;

    Bound _holdsOutKernel;

    /// <summary>One hold as the town keeps it once answered (<c>rebuild_holds_out.comp</c>'s <c>HoldOut</c>), field for field.</summary>
    /// <param name="CutterToM">How far the claim that cut it reached when it did — the rest of it being its row, which only a cut moves the end of.</param>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly record struct HoldOut(
        uint FirstRow, uint EndRow, float StandingMarginM, uint Flags, float KeptOffWhatItMayPassM, float PassesBelowMps, float CutLineM,
        float MarginM, uint CutBy, int CutOn, float CutterToM, uint Pad = 0u);

    /// <summary>How many rows and holds the next rebuild in the town reads back in its own submit, and how many rows there is room for.</summary>
    int _rowsBound, _holdsBound, _slotRoom;

    /// <summary>
    /// How much more than the last rebuild laid the next is read back as: a thirty-second more, and this many rows or holds
    /// besides — a bound on the work, and never on what is laid. A town's claims move a few hundred rows from one rebuild to
    /// the next; every row read past what was laid is bus time spent on nothing.
    /// </summary>
    const int ReadPastShare = 32, ReadPastSlack = 1024;

    /// <summary>How many rebuilds in the town laid past what their submit read back, so the rest was read in a second.</summary>
    public long ReadPast { get; private set; }

    /// <summary>
    /// <b>What may not be mirrored exactly</b>: how many times a car at a bay was found on other channels by the device than
    /// it was on — which the town puts it on once the claims come back, after the passes are laid, where its own rebuild puts
    /// it on them before. A manoeuvre swept from its bay in the same rebuild read the atlas on the channels it had.
    /// </summary>
    public long OnOtherChannelsAtABay { get; private set; }

    /// <summary>
    /// <b>What may not be mirrored exactly</b>: how many walkers the device committed to a straight its own walk was read
    /// off, where the town's walk would not have committed them — so that walk was planned down a straight the town does not
    /// hold, and is sent the town's next time.
    /// </summary>
    public long StraightsTheTownWouldNotCommit { get; private set; }

    /// <summary>How many of the host's rows, holds, units and edits the rebuild's own submit sent — the backing's are sent after.</summary>
    int _rowsSent, _holdsSent, _unitsSent, _rowEditsSent, _holdEditsSent;

    int _askCount;

    /// <summary>Room for one car on a pass's asks, as the town writes them.</summary>
    BodyAsk[] _scratchAsks = [];

    /// <summary>Whether a backing was asked of a car this laying, so what it cut of the answers is looked for.</summary>
    bool _backsUp;

    /// <summary>How many rebuilds in the town asked a car backing up for its ground, which the town lays after the answers.</summary>
    public long BackedUp { get; private set; }

    void InTheTownOf(TownWorld world)
    {
        _priming = false;
        var tables = _claims.OnTheDevice;
        _holdsOut = Device<HoldOut>(_holdRoom);
        _holdsRead = Readback<HoldOut>(_holdRoom);
        _holdsOutKernel = Bound.Of<NoPush>(_vk, "rebuild_holds_out.comp", [tables.Holds, tables.Answers, _holdCutters, _holdsOut, tables.Tally]);
        _slotRoom = world.Occupancy.Capacity;
        (_rowsBound, _holdsBound) = Bounds(world.Occupancy.SlotCount, world.Occupancy.HoldCount);
        _unitHoldsOut = Readback<uint>(_units);
        _carsOut = Device<CarOut>(_cars);
        _carsRead = Readback<CarOut>(_cars);
        _walkersOut = Device<WalkOut>(_walkersRoom);
        _walkersRead = Readback<WalkOut>(_walkersRoom);
        var walkerCounts = _walkers.OnTheDevice.Counts;
        _agentsOutKernel = Bound.Of<AgentsPush>(
            _vk, "rebuild_agents_out.comp",
            [_plans.OnTheDevice.Results, _askAnswers, _bodies.OnTheDevice.Counts, _carsOut, walkerCounts, _walkersOut, _unitHolds, tables.Answers]);
        _scratchAsks = new BodyAsk[Overtaking.MostAsksOfAPass(world.Atlas.Marks)];
    }

    /// <summary>How many asks there is room for: <see cref="MostCarsOnAPass"/> cars' worth.</summary>
    static int AskRoom(TownWorld world) => Math.Min(world.Cars.Capacity, MostCarsOnAPass) * Overtaking.MostAsksOfAPass(world.Atlas.Marks);

    /// <summary>Whether this rebuild's cars on a pass fit the room for their asks.</summary>
    public static bool HasRoomForThePasses(TownWorld world)
    {
        var cars = world.Cars;
        var onAPass = 0;
        for (var car = 0; car < cars.Count; car++)
        {
            if (cars.Pass[car].Begun) onAPass++;
        }

        return onAPass <= MostCarsOnAPass;
    }

    /// <summary>The host's time writing what the last rebuild in the town sent, in milliseconds — the stages' gathering included.</summary>
    public double LaidMs { get; private set; }

    /// <summary>Of it, the host's time from the rebuild's beginning to every walker stationed, in milliseconds — the town's own stationing, all but the cars' bodies sent.</summary>
    public double StationedMs { get; private set; }

    /// <summary>The host's time recording, submitting and waiting for it, in milliseconds.</summary>
    public double WaitedMs { get; private set; }

    /// <summary>The host's time writing what came back into the town, in milliseconds.</summary>
    public double MirroredMs { get; private set; }

    /// <summary>How many bytes the last rebuild in the town sent, and how many it read back.</summary>
    public (ulong Sent, ulong Read) Crossed { get; private set; }

    /// <summary>How many cars on a pass the last rebuild in the town found a body inside.</summary>
    public int HeldInTheirPasses { get; private set; }

    /// <summary>How many rows the last rebuild in the town laid, how many of them bodies and passes, and how many holds.</summary>
    public (int Rows, int Bodies, int Holds) Counted =>((int)_claims.TallyRead[Tally.Rows], (int)_claims.TallyRead[Tally.PassesEnd], (int)_claims.TallyRead[Tally.Holds]);

    long _laidFrom;

    /// <summary>
    /// <b>The town at one stage of a rebuild the device lays</b> (<see cref="ITownClaims.ClaimsAt"/>): what each producer is
    /// sent once the town has it, what the town laid itself since the stage before, and at
    /// <see cref="ClaimsStage.PlansLaid"/> the rebuild through the answers recorded, submitted, waited for and written back.
    /// </summary>
    public void InTheTown(TownWorld town, ClaimsStage stage)
    {
        var claims = town.Occupancy;
        switch (stage)
        {
            case ClaimsStage.Begun:
                _laidFrom = Stopwatch.GetTimestamp();
                _carCount = town.Cars.Count;
                _walkerCount = town.People.Count;
                _hostRowCount = _hostHoldCount = _rowEditCount = _holdEditCount = _askCount = 0;
                _hostUnitCount = (int)StandingUnit + 1;
                _bodies.Lay(town);

                // Every body reads its occupant's line and every plan its own car's, where the car tick keeps them; no
                // stage of a laying lays a line, so one made current here serves both.
                for (var car = 0; car < _carCount; car++) _carTick!.LayTheLineOf(town.Cars, car);
                break;

            case ClaimsStage.Stationed:
                StationedMs = Stopwatch.GetElapsedTime(_laidFrom).TotalMilliseconds;
                _walkers.LayTheStationed(town);
                break;

            case ClaimsStage.CarsLaid:
                _carsLaidSlots = claims.SlotCount;
                break;

            case ClaimsStage.PassesLaid:
                _passesLaidSlots = claims.SlotCount;
                LayTheUnit(PassesUnit, claims, _carsLaidSlots, claims.HoldCount, Numbering.Passes, 0u);
                _passes = new HostRange(0, _hostRowCount, 0, 0);
                break;

            case ClaimsStage.PlansDue:
                _plans.Lay(town);
                _timeS = town.ElapsedS;
                _plansDueSlots = claims.SlotCount;
                _plansDueHolds = claims.HoldCount;
                KeepWhatWasLaid(claims, _passesLaidSlots, _plansDueSlots, _plansDueHolds);
                break;

            case ClaimsStage.DrivesDue:
                Watch(town, stage);
                break;

            case ClaimsStage.WalksDue:
                _walkers.LayWhatChanged(town);
                LayTheCarsClaims(town);
                _planned = new HostRange(_passes.RowCount, _hostRowCount - _passes.RowCount, 0, _hostHoldCount);
                break;

            case ClaimsStage.PlansLaid:
                RecordThroughTheAnswers(town);
                break;

            case ClaimsStage.PlansAnswered:
                _answeredSlots = claims.SlotCount;
                _answeredHolds = claims.HoldCount;
                _backsUp = false;
                foreach (var car in town.CarsAskedThisLaying) _backsUp |= town.Cars.Action[car] == CarAction.BackUp;
                if (_backsUp)
                {
                    KeepWhatWasLaid(claims, 0, _answeredSlots, _answeredHolds);
                    BackedUp++;
                }
                break;

            case ClaimsStage.WalkersDue:
                var (rowsAt, holdsAt) = (_hostRowCount, _hostHoldCount);
                LayTheUnit(_hostUnitCount++, claims, _answeredSlots, _answeredHolds, Numbering.Backing, 0u);
                _backing = new HostRange(rowsAt, _hostRowCount - rowsAt, holdsAt, _hostHoldCount - holdsAt);
                _answeredEdits = _backsUp ? WhatWasCut(claims, 0, _answeredSlots, _answeredHolds, laid: false) : new HostRange(_rowEditCount, 0, _holdEditCount, 0);
                break;

            case ClaimsStage.GrantsRead:
                Pending = true;
                break;
        }
    }

    /// <summary>
    /// <b>Every body, light, plan and walk laid and every plan answered and cut, in one submit</b>: what was gathered sent,
    /// the chain recorded as far as the cut, everything it came to read back, and written into the town.
    /// </summary>
    void RecordThroughTheAnswers(TownWorld town)
    {
        var laid = Stopwatch.GetTimestamp();
        var vk = _vk;
        var commands = _batch.Begin();
        _timer.Reset(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        _rowsSent = _hostRowCount;
        _holdsSent = _hostHoldCount;
        _unitsSent = _hostUnitCount;
        _rowEditsSent = _rowEditCount;
        _holdEditsSent = _holdEditCount;
        var sent = _bodies.SentBytes + _plans.SentBytes + _walkers.ResidentSentBytes + _carTick!.LinesMarkedBytes
                   + Sent<ClaimRow>.BytesOf(_hostRowCount) + Sent<uint>.BytesOf(_hostRowCount) + Sent<HoldRow>.BytesOf(_hostHoldCount)
                   + Sent<uint>.BytesOf(_hostHoldCount) + Sent<HostUnit>.BytesOf(_hostUnitCount) + Sent<RowEdit>.BytesOf(_rowEditCount)
                   + Sent<HoldEdit>.BytesOf(_holdEditCount) + _carClaims.MarkedBytes + Sent<BodyAskRow>.BytesOf(_askCount);
        _bodies.RecordSend(commands);
        _plans.RecordSend(commands);
        _walkers.RecordTheResidentSend(commands);
        _carTick.RecordTheLinesSent(commands);
        _hostRows.Send(commands, _hostRowCount);
        _hostRowUnit.Send(commands, _hostRowCount);
        _hostHolds.Send(commands, _hostHoldCount);
        _hostHoldUnit.Send(commands, _hostHoldCount);
        _hostUnits.Send(commands, _hostUnitCount);
        _rowEdits.Send(commands, _rowEditCount);
        _holdEdits.Send(commands, _holdEditCount);
        _carClaims.Send(commands);
        _asks.Send(commands, _askCount);
        vk.Fill(commands, _wayCounts, 0);
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Sent);

        RecordTheBodies(commands);
        _timer.Stamp(commands, (int)Stage.BodiesLaid);
        RecordThePlans(commands);
        _timer.Stamp(commands, (int)Stage.PlansLaid);

        _claims.RecordAnswers(commands);
        _capture.DispatchIndirect(commands, _claims.OnTheDevice.Tally, Tally.HoldsGroups, default(NoPush));
        vk.BetweenKernels(commands);
        _claims.RecordCut(commands);
        _timer.Stamp(commands, (int)Stage.Answered);

        _holdsOutKernel.DispatchIndirect(commands, _claims.OnTheDevice.Tally, Tally.HoldsGroups, default(NoPush));
        _agentsOutKernel.Dispatch(
            commands, Math.Max(_carCount, _walkerCount), new AgentsPush((uint)_carCount, (uint)_walkerCount, (uint)(_stretches + 1 + _carCount)));
        GrantedWithTheAnswers = !AnyCarBacksUp(town);
        if (GrantedWithTheAnswers) _grant.Dispatch(commands, _carCount, new UnitsPush((uint)_carCount, (uint)(_stretches + 1)));
        vk.BeforeTransfer(commands);
        _claims.RecordReadBounded(commands, _rowsBound, 0);
        _claims.CopyPast<HoldOut>(commands, _holdsOut, _holdsRead, 0, _holdsBound);
        vk.Copy(commands, _unitHolds, _unitHoldsOut, Bytes<uint>(_units));
        vk.Copy(commands, _carsOut, _carsRead, Bytes<CarOut>(_carCount));
        vk.Copy(commands, _walkersOut, _walkersRead, Bytes<WalkOut>(_walkerCount));
        if (GrantedWithTheAnswers) vk.Copy(commands, _grants, _grantsOut, Bytes<DeviceCarTick.GrantRow>(_carCount));
        _timer.Stamp(commands, (int)Stage.Granted);
        _timer.Stamp(commands, (int)Stage.Read);
        _batch.SubmitAndWait();
        _timer.Read();
        var waited = Stopwatch.GetTimestamp();

        _claims.TakeTheCounts();
        ReadWhatWasLaidPast();
        MirrorInto(town);
        var rows = _claims.Rows.Length;
        var holds = _claims.Holds.Length;
        Crossed = (sent, Bytes<ClaimRow>(rows) + Bytes<uint>(rows) + Bytes<HoldOut>(holds) + Bytes<uint>(_units) + Bytes<CarOut>(_carCount)
                         + Bytes<WalkOut>(_walkerCount) + (GrantedWithTheAnswers ? Bytes<DeviceCarTick.GrantRow>(_carCount) : 0ul));
        LaidMs = Stopwatch.GetElapsedTime(_laidFrom, laid).TotalMilliseconds;
        WaitedMs = Stopwatch.GetElapsedTime(laid, waited).TotalMilliseconds;
        MirroredMs = Stopwatch.GetElapsedTime(waited).TotalMilliseconds;
        Rebuilds++;
    }

    /// <summary>
    /// The rows and holds the rebuild laid past what its submit read back, read in a second submit — and the next rebuild
    /// read as far as this one's counts and a margin.
    /// </summary>
    void ReadWhatWasLaidPast()
    {
        var (rows, holds) = (_claims.Rows.Length, _claims.Holds.Length);
        if (rows > _rowsBound || holds > _holdsBound)
        {
            ReadPast++;
            var commands = _batch.Begin();
            _claims.RecordReadPast(commands, _rowsBound, holds);
            _claims.CopyPast<HoldOut>(commands, _holdsOut, _holdsRead, _holdsBound, holds);
            _batch.SubmitAndWait();
        }

        (_rowsBound, _holdsBound) = Bounds(rows, holds);
    }

    /// <summary>How many rows and holds a rebuild after one that laid so many is read back as, within the claims' room.</summary>
    (int Rows, int Holds) Bounds(int rows, int holds) =>
        (Math.Min(_slotRoom, rows + (rows / ReadPastShare) + ReadPastSlack),
         Math.Min(_holdRoom, holds + (holds / ReadPastShare) + ReadPastSlack));

    /// <summary>
    /// <b>What the device laid and answered written into the town</b>, as the town's own laying would have left it: every
    /// slot, hold and list (<see cref="LaneOccupancy.MirrorTheSlot"/>), the hold each car's claim and each walker's walk
    /// laid, what each car's plan came to, the channels found under each car, and how the answers went.
    /// </summary>
    void MirrorInto(TownWorld town)
    {
        var occupancy = town.ReadersOccupancy;
        var rows = _claims.Rows;
        var holds = _holdsRead.Span<HoldOut>()[.._claims.Holds.Length];
        occupancy.BeginTheMirror(rows.Length, holds.Length);
        for (var slot = 0; slot < rows.Length; slot++)
        {
            ref readonly var row = ref rows[slot];
            occupancy.MirrorTheSlot(slot, (int)row.Way, row.Claim, row.IsGone, row.StandsToM);
        }

        for (var hold = 0; hold < holds.Length; hold++)
        {
            ref readonly var held = ref holds[hold];
            var terms = new HolderTerms(held.KeptOffWhatItMayPassM, (held.Flags & HoldRow.OnACall) != 0, held.PassesBelowMps);
            var cutBy = held.CutBy == ClaimAnswer.NoRow ? LaneClaim.Nothing : rows[(int)held.CutBy].Claim with { ToM = held.CutterToM };
            occupancy.MirrorTheHold(
                hold, (int)held.FirstRow, (int)held.EndRow, held.StandingMarginM, (held.Flags & HoldRow.Answered) != 0, terms, held.CutLineM,
                held.MarginM, cutBy, held.CutOn);
        }

        // Every row was listed, each way's two lists a run of the order in turn: a run ends where its way or its kind does.
        var order = _claims.Order;
        var first = 0;
        for (var at = 1; at <= order.Length; at++)
        {
            ref readonly var head = ref rows[(int)order[first]];
            if (at < order.Length && rows[(int)order[at]].Way == head.Way && rows[(int)order[at]].HasBody == head.HasBody) continue;

            occupancy.MirrorTheList((int)head.Way, head.HasBody, order[first..at]);
            first = at;
        }

        var unitHolds = _unitHoldsOut.Span<uint>();
        var carsOut = _carsRead.Span<CarOut>();
        var firstCar = _stretches + 1;
        HeldInTheirPasses = 0;
        foreach (var car in town.CarsAskedThisLaying)
        {
            ref readonly var claim = ref _carClaims.LaidRow(car);
            ref readonly var said = ref carsOut[car];
            if ((claim.Flags & CarClaimRow.PlansOnTheDevice) != 0)
            {
                town.PlannedOnTheDevice(car, said.ClaimToM, said.CommittedToM, said.HorizonM);
                var laid = (said.Bits & CarOut.LaidAHold) != 0;
                town.HeldOnTheDevice(car, laid ? (int)unitHolds[firstCar + car] : LaneOccupancy.NoHold, inItsPass: false);
            }
            else if (claim.AskCount > 0 && (said.Bits & CarOut.HeldInItsPass) != 0)
            {
                HeldInTheirPasses++;
                town.HeldOnTheDevice(car, (int)unitHolds[firstCar + car], inItsPass: true);
            }
            else
            {
                town.HeldOnTheDevice(
                    car, claim.HoldUnit >= 0 ? (int)unitHolds[claim.HoldUnit] + claim.HoldOffset : LaneOccupancy.NoHold, inItsPass: false);
            }
        }

        var walks = _walkersRead.Span<WalkOut>();
        var firstWalker = firstCar + _carCount;
        for (var person = 0; person < _walkerCount; person++)
        {
            var hold = (walks[person].Bits & WalkOut.LaidAHold) != 0 ? (int)unitHolds[firstWalker + person] : LaneOccupancy.NoHold;
            var committed = (walks[person].Bits & WalkOut.CommittedAStraight) != 0;
            if (committed) _walkers.SendTheStraightAgain(person);
            if (!town.WalkedOnTheDevice(person, hold, committed)) StraightsTheTownWouldNotCommit++;
        }

        var cars = town.Cars;
        for (var car = 0; car < _carCount; car++)
        {
            var channels = (byte)(carsOut[car].Bits >> CarOut.ChannelsShift);
            if (channels != cars.Channels[car] && CarActions.IsAtABay(cars.Action[car])) OnOtherChannelsAtABay++;

            town.OnTheGroundOfItsBody(car, channels, (byte)(carsOut[car].Bits >> CarOut.WheelsShift));
        }

        town.AnsweredOnTheDevice(LaidAgain(_claims.Moved, _config.Road.MostSettlingPasses, out var settled), settled);
    }

    /// <summary>
    /// How many answers moved past the first pass before the town's own answering would have stopped
    /// (<see cref="LaneOccupancy.AnswerThePlans"/>) — on the first pass nothing moved, or on the first odd one at the bound
    /// — and whether that was because nothing moved.
    /// </summary>
    static int LaidAgain(ReadOnlySpan<uint> moved, int mostPasses, out bool settled)
    {
        var laidAgain = 0;
        settled = true;
        for (var pass = 1; pass < moved.Length; pass++)
        {
            laidAgain += (int)moved[pass];
            if (moved[pass] == 0) return laidAgain;
            if (pass >= mostPasses && pass % 2 == 1)
            {
                settled = false;
                return laidAgain;
            }
        }

        return laidAgain;
    }

    /// <summary>
    /// <b>The rest of a rebuild laid in the town, recorded at the head of the tick's own submit</b>: the backing the town laid
    /// after the answers and what it cut of them, then every asked car's grant written where the car tick reads it. The
    /// caller's next kernels and copies are barriered against it.
    /// </summary>
    public void RecordTheRest(CommandBuffer commands)
    {
        var vk = _vk;
        Pending = false;
        _hostRows.Send(commands, _rowsSent, _hostRowCount - _rowsSent);
        _hostRowUnit.Send(commands, _rowsSent, _hostRowCount - _rowsSent);
        _hostHolds.Send(commands, _holdsSent, _hostHoldCount - _holdsSent);
        _hostHoldUnit.Send(commands, _holdsSent, _hostHoldCount - _holdsSent);
        _hostUnits.Send(commands, _unitsSent, _hostUnitCount - _unitsSent);
        _rowEdits.Send(commands, _rowEditsSent, _rowEditCount - _rowEditsSent);
        _holdEdits.Send(commands, _holdEditsSent, _holdEditCount - _holdEditsSent);
        vk.AfterTransfer(commands);
        RecordTheBacking(commands);
        if (!GrantedWithTheAnswers) _grant.Dispatch(commands, _carCount, new UnitsPush((uint)_carCount, (uint)(_stretches + 1)));
        vk.BetweenKernels(commands);
        vk.BeforeTransfer(commands);
    }

    /// <summary>What the last rebuild in the town said of every walker, as its own submit read it back.</summary>
    public ReadOnlySpan<WalkOut> WalksRead => _walkersRead.Span<WalkOut>()[.._walkerCount];

    /// <summary>
    /// <b>Whether the last rebuild in the town read every asked car's grant with the answers</b>, in its own submit, and
    /// handed them back (<see cref="GrantsRead"/>) — so the town's tail reads no grant off the claims. Not where a car
    /// backs up: the town lays the backing after the answers, and it may take a plan queued behind it.
    /// </summary>
    public bool GrantedWithTheAnswers { get; private set; }

    /// <summary>Whether any car this laying asked is backing up, whose ground the town lays after the answers.</summary>
    static bool AnyCarBacksUp(TownWorld town)
    {
        foreach (var car in town.CarsAskedThisLaying)
        {
            if (town.Cars.Action[car] == CarAction.BackUp) return true;
        }

        return false;
    }
}
