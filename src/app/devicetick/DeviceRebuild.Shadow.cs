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
    /// <summary>The host's own unit that always exists: the passes.</summary>
    const int PassesUnit = 0;

    /// <summary>How many cars and walkers the rebuild being watched lays.</summary>
    int _carCount, _walkerCount;

    /// <summary>The moment the lights are asked their colours at (<see cref="TownWorld.ElapsedS"/>).</summary>
    float _timeS;

    /// <summary>Whether the next rebuild first sends the town's grants as they stand, which a car the laying does not ask keeps.</summary>
    bool _priming = true;

    /// <summary>How much of the host's table the rebuild being watched has written.</summary>
    int _hostRowCount, _hostHoldCount, _hostUnitCount;

    HostRange _passes;

    /// <summary>How many rows and holds of the device's the shadow reads back: as many as the town laid.</summary>
    int _rowsToRead, _holdsToRead;

    /// <summary>The town's numbering where each run the host lays itself began.</summary>
    int _carsLaidSlots, _passesLaidSlots, _plansDueSlots, _answeredSlots, _answeredHolds;

    /// <summary>Each car's claim as the plans began to be laid: asked, planning down its line, on a pass.</summary>
    bool[] _askedNow = [], _plansItsLine = [], _onAPass = [];

    /// <summary>How many pieces of the last rebuild's bodies were grown into a body already on their way rather than laid as one.</summary>
    public long Grown => _claims.TallyRead[Tally.Candidates] - _claims.TallyRead[Tally.Bodies];

    /// <summary>How many rows the host laid itself and sent in the last rebuild.</summary>
    public int HostRowsSent => _hostRowCount;

    /// <summary>And how many holds.</summary>
    public int HostHoldsSent => _hostHoldCount;

    /// <summary>How many cars' backing the last rebuild laid (<c>rebuild_backing.comp</c>).</summary>
    public int BackingAsked => _backingCount;

    void ShadowOf()
    {
        _askedNow = new bool[_cars];
        _plansItsLine = new bool[_cars];
        _layingCars = new int[_cars];
        _onAPass = new bool[_cars];
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
                _hostRowCount = _hostHoldCount = _askCount = 0;
                _hostUnitCount = PassesUnit + 1;

                // Held beside the town's own, every car is laid every laying.
                for (var car = 0; car < _carCount; car++) _layingCars[car] = car;
                _layingCount = _carCount;
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
                break;

            case ClaimsStage.DrivesDue:
                AskTheStanding(town);
                foreach (var car in LayingCars) _askedNow[car] = false;
                foreach (var car in town.CarsAskedThisLaying) _askedNow[car] = true;
                foreach (var car in LayingCars)
                {
                    _plansItsLine[car] = _askedNow[car] && town.PlansItsLine(car);
                    _onAPass[car] = town.Cars.Pass[car].Begun;
                }

                break;

            case ClaimsStage.WalksDue:
                _walkers.Lay(town, DeviceWalkers.Piece.Plan);
                LayTheCarsClaims(town);
                break;

            case ClaimsStage.PlansAnswered:
                _answeredSlots = claims.SlotCount;
                _answeredHolds = claims.HoldCount;
                AskTheBacking(town, takes: false);
                break;

            case ClaimsStage.WalkersDue:
                _walkers.Lay(town, DeviceWalkers.Piece.Read);
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
    /// <b>Every car's claim as the host left it before any grant is read</b>: whether the laying asked it; whether its claim
    /// is the device's plan — past its pass, where it is on one, with where a body inside the pass is looked for; whether
    /// it is laid where it stands; and for any other, what the host left it granted.
    /// </summary>
    void LayTheCarsClaims(TownWorld town)
    {
        foreach (var car in LayingCars)
        {
            _carClaims.Lay(car, _plansItsLine[car] && _onAPass[car] ? OnAPass(town, car) : ClaimOf(town, car));
        }

        if (_carTick is { ChecksTheVisits: true }) HoldTheUnlaidCars(town);
    }

    /// <summary>
    /// One car's claim as the host leaves it (<see cref="LayTheCarsClaims"/>), but of a car on a pass planned past it, whose
    /// asks the laying sends (<see cref="OnAPass"/>).
    /// </summary>
    CarClaimRow ClaimOf(TownWorld town, int car)
    {
        if (!_askedNow[car]) return new CarClaimRow(0u, 0f, 0f, 0f, 0f, 0f, 0u);
        if (_plansItsLine[car]) return new CarClaimRow(CarClaimRow.Asked | CarClaimRow.PlansOnTheDevice, 0f, 0f, 0f, 0f, 0f, 0u);

        var cars = town.Cars;
        var flags = CarClaimRow.Asked;
        var authorityM = cars.AuthorityM[car];
        if (_standingOf[car] >= 0)
        {
            // A begun manoeuvre with nothing standing in it holds nothing, and is granted the whole of it (BayManoeuvring.Hold).
            flags |= CarClaimRow.Stands;
            if (_standing.Rows[_standingOf[car]].Kind == (uint)StandingClaim.Manoeuvre) authorityM = float.PositiveInfinity;
        }

        return new CarClaimRow(
            flags, authorityM, cars.HorizonM[car], cars.ClaimFromM[car], cars.CommittedToM[car], cars.GrantMarginM[car],
            (uint)cars.GrantCutBy[car]);
    }

    /// <summary>
    /// A car on a pass: planned past it by the device as though the pass were clear, and whether a body stands inside it
    /// asked of the device's bodies (<see cref="TownWorld.BodyAsksOfItsPass"/>).
    /// </summary>
    CarClaimRow OnAPass(TownWorld town, int car)
    {
        var askFirst = _askCount;
        var askCount = town.BodyAsksOfItsPass(car, _scratchAsks);
        if (!_asks.MakeRoomFor(_askCount + askCount))
        {
            throw new InvalidOperationException($"The cars on a pass asked past the {_asks.Room} stretches the rebuild has room for.");
        }

        var asks = _asks.Rows;
        for (var at = 0; at < askCount; at++) asks[_askCount++] = BodyAskRow.Of(_scratchAsks[at]);

        return new CarClaimRow(
            CarClaimRow.Asked | CarClaimRow.PlansOnTheDevice | CarClaimRow.OnAPass, 0f, 0f, 0f, 0f, 0f, 0u, askFirst, (uint)askCount);
    }

    /// <summary>A run of the host's own laying, every slot and hold from where the host was at the run's start to where it is now.</summary>
    void LayTheUnit(int unitAt, LaneOccupancy claims, int firstSlot, int firstHold, uint numbering, uint unit) =>
        LayTheRun(unitAt, claims, firstSlot, claims.SlotCount, firstHold, claims.HoldCount, numbering, unit);

    void LayTheRun(int unitAt, LaneOccupancy claims, int firstSlot, int endSlot, int firstHold, int endHold, uint numbering, uint unit)
    {
        var rowsAt = _hostRowCount;
        var holdsAt = _hostHoldCount;
        var rows = rowsAt + (endSlot - firstSlot);
        if (!_hostRows.MakeRoomFor(rows) || !_hostRowUnit.MakeRoomFor(rows) || holdsAt + (endHold - firstHold) > _hostHolds.Rows.Length)
        {
            throw new InvalidOperationException($"The host laid {endSlot - firstSlot} rows and {endHold - firstHold} holds itself, past the {_hostRows.Room} the rebuild has room for.");
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
    /// walker's reading, every figure to the bit (<see cref="Same"/>).
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
            if (!Same(host.AlongMps, device.AlongMps) || device with { AlongMps = host.AlongMps } != host)
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
            var sameCutter = Same(hostCutter.AlongMps, cutter.AlongMps) && cutter with { AlongMps = hostCutter.AlongMps } == hostCutter;
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
                       && row.Is(DeviceWalkers.WalkerRow.PassBegun) == pass.Begun && Same(people.DestinationM[person].X, row.DestinationM.X)
                       && Same(people.DestinationM[person].Y, row.DestinationM.Y);
            if (!same)
            {
                walkers.Add($"rebuild {rebuild} walker {person}: town granted {people.GrantM[person]} crossing {people.OnCrossing[person]} {people.Action[person]} {pass} aiming {people.DestinationM[person]}; device {row.GrantM} {row.OnCrossing} {(PersonAction)row.Action} aiming {row.DestinationM}");
            }
        }
    }

    /// <summary>The same float to the bit: the device's trigonometry, quotients and roots are the host's (core log).</summary>
    static bool Same(float host, float onDevice) => BitConverter.SingleToInt32Bits(host) == BitConverter.SingleToInt32Bits(onDevice);
}
