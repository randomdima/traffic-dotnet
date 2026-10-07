using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using static TrafficSimulation.App.DeviceTick.DeviceClaims;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// The rebuild in shadow: what of the town each stage of its own rebuild hands the device — every producer's input as
/// the town has it at that stage, and what only the host lays, in the host's own numbering — and, once the device has
/// rebuilt, what it made of them that the town did not.
/// </summary>
internal sealed partial class DeviceRebuild
{
    /// <summary>The host's own units that always exist: the passes, and the claims laid where they stand among the plans.</summary>
    const int PassesUnit = 0;

    const uint StandingUnit = 1u;

    /// <summary>How many cars and walkers the rebuild being watched lays.</summary>
    int _carCount, _walkerCount;

    /// <summary>The moment the lights are asked their colours at (<see cref="TownWorld.ElapsedS"/>).</summary>
    float _timeS;

    /// <summary>Whether the next rebuild first sends the town's grants as they stand, which a car the laying does not ask keeps.</summary>
    bool _priming = true;

    /// <summary>How much of the host's table and of its edits the rebuild being watched has written.</summary>
    int _hostRowCount, _hostHoldCount, _hostUnitCount, _rowEditCount, _holdEditCount;

    HostRange _passes, _planned, _backing, _laidEdits, _answeredEdits;

    /// <summary>How many rows and holds of the device's the shadow reads back: as many as the town laid.</summary>
    int _rowsToRead, _holdsToRead;

    /// <summary>The town's numbering where each run the host lays itself began.</summary>
    int _carsLaidSlots, _passesLaidSlots, _plansDueSlots, _plansDueHolds, _drivesDueHolds, _answeredSlots, _answeredHolds;

    /// <summary>Each car's claim as the plans began to be laid: asked, planning down its line, on a pass.</summary>
    bool[] _askedNow = [], _plansItsLine = [], _onAPass = [];

    /// <summary>What every row and hold was before a run the host lays itself, to find what that run cut of them.</summary>
    float[] _toMBefore = [];

    bool[] _goneBefore = [];

    PlanAnswer[] _answerBefore = [];

    /// <summary>How many pieces of the last rebuild's bodies were grown into a body already on their way rather than laid as one.</summary>
    public long Grown => _claims.TallyRead[Tally.Candidates] - _claims.TallyRead[Tally.Bodies];

    /// <summary>How many rows the host laid itself and sent in the last rebuild.</summary>
    public int HostRowsSent => _hostRowCount;

    /// <summary>
    /// How many rows and holds the host's own laying cut, and sent as cut, in the last rebuild: before the answers — what
    /// a hand or a tow takes of a light — and after them, what the backing takes.
    /// </summary>
    public (int BeforeTheAnswers, int AfterThem) EditsSent =>
        (_laidEdits.RowCount + _laidEdits.HoldCount, _answeredEdits.RowCount + _answeredEdits.HoldCount);

    /// <summary>How many rows and holds the backing laid in the last rebuild.</summary>
    public int BackingSent => _backing.RowCount + _backing.HoldCount;

    void ShadowOf()
    {
        _askedNow = new bool[_cars];
        _plansItsLine = new bool[_cars];
        _onAPass = new bool[_cars];
        _answerBefore = new PlanAnswer[_holdRoom];
    }

    /// <summary>
    /// <b>The town at one stage of its own rebuild</b> (<see cref="TownWorld.WatchingTheClaims"/>): what each producer is
    /// sent, what the host laid itself since the stage before, and once every grant is read, the whole rebuild recorded,
    /// submitted and waited for.
    /// </summary>
    public void Watch(TownWorld town, ClaimsStage stage)
    {
        var claims = town.Occupancy;
        switch (stage)
        {
            case ClaimsStage.Begun:
                _carCount = town.Cars.Count;
                _walkerCount = town.People.Count;
                _hostRowCount = _hostHoldCount = _rowEditCount = _holdEditCount = 0;
                _hostUnitCount = (int)StandingUnit + 1;
                if (_priming) Prime(town);
                _bodies.Lay(town);
                _walkers.Lay(town, DeviceWalkers.Piece.Station);
                break;

            case ClaimsStage.Stationed:
                _walkers.Lay(town, DeviceWalkers.Piece.Body);
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
                _drivesDueHolds = claims.HoldCount;
                LayTheUnit((int)StandingUnit, claims, _plansDueSlots, _plansDueHolds, Numbering.Plans, (uint)_stretches);
                _laidEdits = WhatWasCut(claims, _passesLaidSlots, _plansDueSlots, _plansDueHolds, laid: true);
                for (var car = 0; car < _carCount; car++) _askedNow[car] = false;
                foreach (var car in town.CarsAskedThisLaying) _askedNow[car] = true;
                for (var car = 0; car < _carCount; car++)
                {
                    _plansItsLine[car] = _askedNow[car] && town.PlansItsLine(car);
                    _onAPass[car] = town.Cars.Pass[car].Begun;
                }

                break;

            case ClaimsStage.WalksDue:
                _walkers.Lay(town, DeviceWalkers.Piece.Plan);
                LayTheCarsClaims(town);
                _planned = new HostRange(_passes.RowCount, _hostRowCount - _passes.RowCount, 0, _hostHoldCount);
                break;

            case ClaimsStage.PlansAnswered:
                _answeredSlots = claims.SlotCount;
                _answeredHolds = claims.HoldCount;
                KeepWhatWasLaid(claims, 0, _answeredSlots, _answeredHolds);
                break;

            case ClaimsStage.WalkersDue:
                _walkers.Lay(town, DeviceWalkers.Piece.Read);
                var (rowsAt, holdsAt) = (_hostRowCount, _hostHoldCount);
                LayTheUnit(_hostUnitCount++, claims, _answeredSlots, _answeredHolds, Numbering.Backing, 0u);
                _backing = new HostRange(rowsAt, _hostRowCount - rowsAt, holdsAt, _hostHoldCount - holdsAt);
                _answeredEdits = WhatWasCut(claims, 0, _answeredSlots, _answeredHolds, laid: false);
                break;

            case ClaimsStage.GrantsRead:
                _rowsToRead = claims.SlotCount;
                _holdsToRead = claims.HoldCount;
                if (Chained) Pending = true;
                else Record();

                break;
        }
    }

    /// <summary>
    /// <b>Every car's claim as the host left it before any grant is read</b>: whether the laying asked it, whether its claim
    /// is the device's plan, a car on a pass's plan past it laid by the host as a unit of its own, and for every claim the
    /// host laid, what it left the car granted and which hold its grant is read off.
    /// </summary>
    void LayTheCarsClaims(TownWorld town)
    {
        var cars = town.Cars;
        var rows = _carClaims;
        for (var car = 0; car < _carCount; car++)
        {
            if (!_askedNow[car])
            {
                rows.Lay(car, new CarClaimRow(0u, -1, -1, 0, 0f, 0f, 0f, 0f, 0f, 0u));
                continue;
            }

            if (_plansItsLine[car] && !_onAPass[car])
            {
                rows.Lay(car, new CarClaimRow(CarClaimRow.Asked | CarClaimRow.PlansOnTheDevice, -1, -1, 0, 0f, 0f, 0f, 0f, 0f, 0u));
                continue;
            }

            var hold = town.PlanHoldOf(car);
            var hostUnit = -1;
            var (holdUnit, holdOffset) = (-1, 0);
            var (askFirst, askCount) = (0, 0);
            if (_plansItsLine[car] && _carTick != null)
            {
                // Planned as though its pass were clear: whether a body stands in it is the device's to ask.
                askFirst = _askCount;
                askCount = town.BodyAsksOfItsPass(car, _scratchAsks);
                var asks = _asks.Rows;
                if (_askCount + askCount > asks.Length)
                {
                    throw new InvalidOperationException($"The cars on a pass asked past the {asks.Length} stretches the rebuild has room for.");
                }

                for (var at = 0; at < askCount; at++) asks[_askCount++] = BodyAskRow.Of(_scratchAsks[at]);
            }

            if (_plansItsLine[car])
            {
                hostUnit = _hostUnitCount++;
                var unit = (uint)(_stretches + 1 + car);
                if (hold == LaneOccupancy.NoHold) LayTheRun(hostUnit, town.Occupancy, 0, 0, 0, 0, Numbering.Plans, unit);
                else
                {
                    var (first, end) = town.Occupancy.PiecesOf(hold);
                    LayTheRun(hostUnit, town.Occupancy, first, end, hold, hold + 1, Numbering.Plans, unit);
                    (holdUnit, holdOffset) = ((int)unit, 0);
                }
            }
            else if (hold != LaneOccupancy.NoHold && hold >= _plansDueHolds && hold < _drivesDueHolds)
            {
                (holdUnit, holdOffset) = (_stretches, hold - _plansDueHolds);
            }

            rows.Lay(car, new CarClaimRow(
                CarClaimRow.Asked, hostUnit, holdUnit, holdOffset, cars.AuthorityM[car], cars.HorizonM[car], cars.ClaimFromM[car],
                cars.CommittedToM[car], cars.GrantMarginM[car], (uint)cars.GrantCutBy[car], askFirst, (uint)askCount));
        }
    }

    /// <summary>A run of the host's own laying, every slot and hold from where the host was at the run's start to where it is now.</summary>
    void LayTheUnit(int unitAt, LaneOccupancy claims, int firstSlot, int firstHold, uint numbering, uint unit) =>
        LayTheRun(unitAt, claims, firstSlot, claims.SlotCount, firstHold, claims.HoldCount, numbering, unit);

    void LayTheRun(int unitAt, LaneOccupancy claims, int firstSlot, int endSlot, int firstHold, int endHold, uint numbering, uint unit)
    {
        var rowsAt = _hostRowCount;
        var holdsAt = _hostHoldCount;
        if (rowsAt + (endSlot - firstSlot) > _hostRows.Rows.Length || holdsAt + (endHold - firstHold) > _hostHolds.Rows.Length)
        {
            throw new InvalidOperationException($"The host laid {endSlot - firstSlot} rows and {endHold - firstHold} holds itself, past the {_hostRows.Rows.Length} the rebuild has room for.");
        }

        for (var slot = firstSlot; slot < endSlot; slot++)
        {
            _hostRows.Rows[_hostRowCount] = ClaimRow.OfSlot(claims, slot);
            _hostRowUnit.Rows[_hostRowCount++] = (uint)unitAt;
        }

        for (var hold = firstHold; hold < endHold; hold++)
        {
            _hostHolds.Rows[_hostHoldCount] = HoldRowOf(claims, hold);
            _hostHoldUnit.Rows[_hostHoldCount++] = (uint)unitAt;
        }

        _hostUnits.Rows[unitAt] = new HostUnit(
            (uint)rowsAt, (uint)(endSlot - firstSlot), (uint)holdsAt, (uint)(endHold - firstHold), firstSlot, firstHold, numbering, unit);
    }

    /// <summary>One of the host's holds as the kernels read it, its cut named by the slot that made it.</summary>
    static HoldRow HoldRowOf(LaneOccupancy claims, int hold)
    {
        var (first, end) = claims.PiecesOf(hold);
        var terms = claims.TermsOf(hold);
        var endsAtM = claims.HoldEndsAtM(hold, out var marginM, out var cutBy);
        var cutOn = claims.HoldCutOn(hold);
        var flags = (claims.IsAnswered(hold) ? HoldRow.Answered : 0u) | (terms.OnACall ? HoldRow.OnACall : 0u);
        return new HoldRow(
            (uint)first, (uint)end, claims.StandingMarginM(hold), flags, terms.KeptOffWhatItMayPassM, terms.PassesBelowMps, endsAtM, marginM,
            SlotOf(claims, cutBy, cutOn), cutOn);
    }

    /// <summary>
    /// <b>The slot a claim kept as a hold's cut was laid in</b>: the one on its way laid by the same holder, of the same
    /// hold and kind, from the same metre — whatever it was cut back to since — or <see cref="ClaimAnswer.NoRow"/>.
    /// </summary>
    static uint SlotOf(LaneOccupancy claims, in LaneClaim claim, int way)
    {
        if (!claim.Found) return ClaimAnswer.NoRow;

        var slots = claims.Slots;
        var ways = claims.SlotWays;
        for (var slot = 0; slot < slots.Length; slot++)
        {
            if (ways[slot] == way && SameClaim(slots[slot], claim)) return (uint)slot;
        }

        return ClaimAnswer.NoRow;
    }

    static bool SameClaim(in LaneClaim one, in LaneClaim other) =>
        one.Occupant == other.Occupant && one.Of == other.Of && one.Hold == other.Hold && one.FromM == other.FromM
        && one.Priority == other.Priority && one.Secondary == other.Secondary && one.Passing == other.Passing;

    /// <summary>Every row's extent and every hold's answer as they stand, to find later what a run of the host's own cut.</summary>
    void KeepWhatWasLaid(LaneOccupancy claims, int firstSlot, int endSlot, int endHold)
    {
        // Grown to twice the most a laying has laid, so a town settles into a size and is not grown again.
        if (_toMBefore.Length < endSlot)
        {
            var room = Math.Min(claims.Capacity, 2 * endSlot);
            Array.Resize(ref _toMBefore, room);
            Array.Resize(ref _goneBefore, room);
        }

        var slots = claims.Slots;
        var gone = claims.SlotsGone;
        for (var slot = firstSlot; slot < endSlot; slot++)
        {
            _toMBefore[slot] = slots[slot].ToM;
            _goneBefore[slot] = gone[slot];
        }

        for (var hold = 0; hold < endHold; hold++)
        {
            var endsAtM = claims.HoldEndsAtM(hold, out var marginM, out var cutBy);
            _answerBefore[hold] = new PlanAnswer(endsAtM, marginM, cutBy, claims.HoldCutOn(hold));
        }
    }

    /// <summary>
    /// <b>What a run of the host's own laying cut of what was down before it</b> (<c>LaneOccupancy.TakeFrom</c>): every row
    /// now ending sooner or gone, and every hold now answered otherwise — as laid, before the answers, or as answered after.
    /// </summary>
    HostRange WhatWasCut(LaneOccupancy claims, int firstSlot, int endSlot, int endHold, bool laid)
    {
        var (rowsAt, holdsAt) = (_rowEditCount, _holdEditCount);
        var slots = claims.Slots;
        var gone = claims.SlotsGone;
        for (var slot = firstSlot; slot < endSlot; slot++)
        {
            if (slots[slot].ToM == _toMBefore[slot] && gone[slot] == _goneBefore[slot]) continue;
            if (_rowEditCount == _rowEdits.Rows.Length)
            {
                throw new InvalidOperationException($"The host cut more than the {_rowEdits.Rows.Length} rows the rebuild has room to send.");
            }

            _rowEdits.Rows[_rowEditCount++] = new RowEdit((uint)slot, slots[slot].ToM, gone[slot] ? 1u : 0u);
        }

        for (var hold = 0; hold < endHold; hold++)
        {
            var endsAtM = claims.HoldEndsAtM(hold, out var marginM, out var cutBy);
            var cutOn = claims.HoldCutOn(hold);
            if (new PlanAnswer(endsAtM, marginM, cutBy, cutOn) == _answerBefore[hold]) continue;

            _holdEdits.Rows[_holdEditCount++] = new HoldEdit(
                (uint)hold, laid ? 1u : 0u, 0u, 0u,
                new ClaimAnswer(endsAtM, marginM, SlotOf(claims, cutBy, cutOn), cutOn, float.PositiveInfinity, 0f, 0f, 0f),
                cutBy.Found ? ClaimRow.Of(cutBy, cutOn, false, cutBy.FromM) : default);
        }

        return new HostRange(rowsAt, _rowEditCount - rowsAt, holdsAt, _holdEditCount - holdsAt);
    }

    /// <summary>
    /// Every car's grant, cutter and told bits as the town has them before its first watched rebuild, sent with it: a car
    /// that laying does not ask keeps them, as the town's does.
    /// </summary>
    void Prime(TownWorld town)
    {
        var grants = _primedGrants.Span<DeviceCarTick.GrantRow>();
        var cutters = _primedCutters.Span<ClaimRow>();
        var told = _primedTold.Span<uint>();
        for (var car = 0; car < _carCount; car++) grants[car] = TheTownsGrant(town, car, out cutters[car], out told[car]);
    }

    /// <summary>
    /// <b>A car's grant as the town hands its tick</b> (<c>DeviceCarTick</c>'s grant, cutter and told rows): what it was
    /// granted and what cut it, the way its hold was cut on, and the place it was sent to.
    /// </summary>
    public static DeviceCarTick.GrantRow TheTownsGrant(TownWorld town, int car, out ClaimRow cutter, out uint told)
    {
        var cars = town.Cars;
        var claims = town.Occupancy;
        var hold = town.PlanHoldOf(car);
        var cut = LaneClaim.Nothing;
        var cutOn = -1;
        if (hold != LaneOccupancy.NoHold)
        {
            claims.HoldEndsAtM(hold, out _, out cut);
            cutOn = claims.HoldCutOn(hold);
        }

        town.SceneOf(car, out var placeM, out var reachM);
        cutter = cut.Found ? ClaimRow.Of(cut, cutOn, false, cut.FromM) : default;
        told = (hold != LaneOccupancy.NoHold ? DeviceCarTick.ToldRow.HasAHold : 0u) | (cut.Found ? DeviceCarTick.ToldRow.Cut : 0u);
        return new DeviceCarTick.GrantRow(
            cars.AuthorityM[car], cars.HorizonM[car], cars.ClaimFromM[car], cars.CommittedToM[car], cars.GrantMarginM[car],
            (uint)cars.GrantCutBy[car], cutOn, reachM, placeM);
    }

    /// <summary>What one kind of the rebuild's answer differed in, over every rebuild held: how many were asked, how many differed, and the first few.</summary>
    internal sealed class Held(string what)
    {
        public readonly List<string> Differ = [];

        public long Asked;

        public long Differed;

        public bool Kept => Differed == 0;

        public void Add(string why)
        {
            Differed++;
            if (Differ.Count < 6) Differ.Add(why);
        }

        public string Said => $"{Differed} of {Asked} {what} differ: {string.Join("; ", Differ)}";
    }

    /// <summary>
    /// <b>The device's rebuild held to the town's</b>, once both are done (<see cref="ClaimsStage.GrantsRead"/>): every slot's
    /// row in the town's numbering, every hold and what it came to, every car's grant, cutter and told bits, and every
    /// walker's reading. Exact everywhere but a pace along a heading — a walker's, or a body grown over one — and where a
    /// walker aims, which the device's own trigonometry finds (<see cref="Near"/>).
    /// </summary>
    public void HoldToTheTown(TownWorld town, Held rows, Held holds, Held answers, Held grants, Held walkers)
    {
        var claims = town.Occupancy;
        var tally = _claims.TallyRead;
        var rebuild = Rebuilds;
        rows.Asked++;
        holds.Asked++;
        if (tally[Tally.Rows] != claims.SlotCount)
        {
            rows.Add(
                $"rebuild {rebuild}: town {claims.SlotCount} slots — bodies to {_carsLaidSlots}, passes to {_passesLaidSlots}, plans from {_plansDueSlots} to {_answeredSlots} — "
                + $"device {tally[Tally.Rows]}: bodies to {tally[Tally.Bodies]}, passes to {tally[Tally.PassesEnd]}, plans to {tally[Tally.PlannedRows]}");
        }

        if (tally[Tally.Holds] != claims.HoldCount) holds.Add($"rebuild {rebuild}: town {claims.HoldCount} holds, device {tally[Tally.Holds]}");

        var laid = _claims.Rows;
        for (var slot = 0; slot < claims.SlotCount; slot++)
        {
            rows.Asked++;
            var host = ClaimRow.OfSlot(claims, slot);
            var device = laid[slot];
            if (!Near(host.AlongMps, device.AlongMps) || device with { AlongMps = host.AlongMps } != host)
            {
                rows.Add($"rebuild {rebuild} slot {slot}: town {host}; device {device}");
            }
        }

        var held = _claims.Holds;
        var answered = _claims.Answers;
        for (var hold = 0; hold < claims.HoldCount; hold++)
        {
            holds.Asked++;
            answers.Asked++;
            var host = HoldRowOf(claims, hold);
            var device = held[hold];
            if (host with { LaidCutLineM = 0f, LaidMarginM = 0f, LaidCutBy = 0u, LaidCutOn = 0 }
                != device with { LaidCutLineM = 0f, LaidMarginM = 0f, LaidCutBy = 0u, LaidCutOn = 0 })
            {
                holds.Add($"rebuild {rebuild} hold {hold}: town {host}; device {device}");
            }

            var endsAtM = claims.HoldEndsAtM(hold, out var marginM, out var cutBy);
            var answer = answered[hold];
            var sameCutter = cutBy.Found
                ? answer.CutBy != ClaimAnswer.NoRow && answer.CutBy < claims.SlotCount && SameClaim(claims.Slots[(int)answer.CutBy], cutBy)
                : answer.CutBy == ClaimAnswer.NoRow;
            if (endsAtM != answer.CutLineM || marginM != answer.MarginM || claims.HoldCutOn(hold) != answer.CutOn || !sameCutter)
            {
                answers.Add($"rebuild {rebuild} hold {hold}: town {endsAtM} kept {marginM} on {claims.HoldCutOn(hold)} by {cutBy}; device {answer}");
            }
        }

        for (var car = 0; car < town.Cars.Count; car++)
        {
            grants.Asked++;
            var host = TheTownsGrant(town, car, out var hostCutter, out var hostTold);
            var device = GrantsRead[car] with { PlaceReachM = host.PlaceReachM, PlaceM = host.PlaceM };
            var cutter = CuttersRead[car];
            var sameCutter = Near(hostCutter.AlongMps, cutter.AlongMps) && cutter with { AlongMps = hostCutter.AlongMps } == hostCutter;
            if (device != host || !sameCutter || ToldRead[car] != hostTold)
            {
                grants.Add($"rebuild {rebuild} car {car}: town {host} {hostCutter} told {hostTold}; device {GrantsRead[car]} {cutter} told {ToldRead[car]}");
            }
        }

        var people = town.People;
        var read = _walkers.Walkers;
        for (var person = 0; person < people.Count; person++)
        {
            walkers.Asked++;
            var row = read[person];
            var pass = people.Pass[person];
            var same = row.GrantM == people.GrantM[person] && row.OnCrossing == people.OnCrossing[person]
                       && row.Action == (uint)people.Action[person] && row.PassSlot == pass.Slot && row.PassFromM == pass.FromM
                       && row.PassClearM == pass.ClearM && row.PassAsideM == pass.AsideM
                       && row.Is(DeviceWalkers.WalkerRow.PassBegun) == pass.Begun && Near(people.DestinationM[person].X, row.DestinationM.X)
                       && Near(people.DestinationM[person].Y, row.DestinationM.Y);
            if (!same)
            {
                walkers.Add($"rebuild {rebuild} walker {person}: town granted {people.GrantM[person]} crossing {people.OnCrossing[person]} {people.Action[person]} {pass} aiming {people.DestinationM[person]}; device {row.GrantM} {row.OnCrossing} {(PersonAction)row.Action} aiming {row.DestinationM}");
            }
        }
    }

    /// <summary>Within a part in a thousand and a hundredth absolute: a figure the device's own trigonometry finds, as every kernel test reads it.</summary>
    static bool Near(float host, float onDevice) => host == onDevice || MathF.Abs(host - onDevice) <= 1e-2f + (1e-3f * MathF.Abs(host));
}
