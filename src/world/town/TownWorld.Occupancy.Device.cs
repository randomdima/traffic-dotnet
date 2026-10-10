using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A claims rebuild a device lays</b> (<see cref="ITownClaims"/>): <see cref="RebuildLaneOccupancy"/> stage
/// for stage, the town laying here only what it alone lays — every walker stationed, the passes, and every grant the device
/// did not read — and the device every body, light, plan and walk, what each car holds where it stands off what it asks
/// (<see cref="AsksWhereItStands"/>), every answer and the backing after them (<see cref="TakeTheBackingAsk"/>), handed
/// back into the reservations.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same rebuild, not a likeness of it</b>: what the town lays it lays with its own code in its own order, and what
/// the device writes back is what the town's own laying would have left — the reservations, every car's plan and hold,
/// every walker's hold, the channels each car stands on, and how the answers went.
/// </para>
/// <para>
/// <b>A car on a pass is planned past it as though its pass were clear</b>: whether a body stands inside it is asked of the
/// device's bodies (<see cref="BodyAsksOfItsPass"/>), and where one does its plan is the hold short of it.
/// </para>
/// <para>
/// <b>A device that keeps the walkers</b> (<see cref="ITownClaims.KeepsTheWalkers"/>) stations them, lays their passes and
/// reads their grants itself, and the town takes up what it made of each (<see cref="KeptOnTheDevice"/>).
/// </para>
/// <para>
/// <b>The instruments' watch is not handed this rebuild</b> (<see cref="WatchingTheClaims"/>): what it reads of each stage
/// is the town's own laying, and the bodies, lights, plans and walks are not laid here.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    void RebuildLaneOccupancyOn(ITownClaims device)
    {
        device.ClaimsAt(this, ClaimsStage.Begun);
        LayIntoTheLaying();
        var walkers = !device.KeepsTheWalkers;
        for (var person = 0; walkers && person < People.Count; person++) StationTheWalker(person);

        device.ClaimsAt(this, ClaimsStage.Stationed);
        for (var person = 0; walkers && person < People.Count; person++)
        {
            if (!People.Inside[person].Any) HoldTheWalkersGround(person, out _);
        }

        device.ClaimsAt(this, ClaimsStage.WalkersLaid);
        AskTheCars();
        device.ClaimsAt(this, ClaimsStage.CarsLaid);
        LayThePasses(walkers);
        device.ClaimsAt(this, ClaimsStage.PassesLaid);
        device.ClaimsAt(this, ClaimsStage.PlansDue);

        // What a car holds where it stands is the device's to lay, off what it asks (AsksWhereItStands).
        foreach (var car in CarsAsked)
        {
            if (!PlansDownItsLine(car)) _ground.ClearTheClaim(car);
        }

        device.ClaimsAt(this, ClaimsStage.DrivesDue);

        // And every plan down a line is the device's, past a car's pass where it is on one.
        foreach (var car in CarsAsked)
        {
            if (PlansDownItsLine(car)) _ground.ClearTheClaim(car);
        }

        device.ClaimsAt(this, ClaimsStage.WalksDue);
        device.ClaimsAt(this, ClaimsStage.PlansLaid);
        var landsLater = device.LandsLater;
        LayIntoTheLayer(landsLater);
        device.ClaimsAt(this, ClaimsStage.PlansAnswered);
        var granted = device.ReadTheGrants;
        if (!granted)
        {
            foreach (var car in CarsAsked) _ground.ReadTheGrant(car);
        }

        device.ClaimsAt(this, ClaimsStage.WalkersDue);

        // A device that keeps the walkers read their grants where it read the cars'; where it read none, the town reads
        // them off the walkers the device left.
        if (walkers) ReadTheWalkersGrants(granted);
        else if (!granted) ReadTheWalkersGrants();

        device.ClaimsAt(this, ClaimsStage.GrantsRead);
    }

    /// <summary>
    /// <b>What the town lays itself in a rebuild a device lays goes into a laying of its own</b>, begun empty: the few rows
    /// only the town lays, numbered from nothing as the device reads them, while the layer every reader reads is left as it
    /// stands until the device's answers are written back into it.
    /// </summary>
    void LayIntoTheLaying()
    {
        // A table that grows: a laying holds a few dozen rows, and settles into what the town's busiest needs.
        _laying ??= new LaneOccupancy(_ways, LayingRoom, LayingRoom / 16, _atlas.Marks, grows: true);
        if (_withTheLayer == null)
        {
            (_planHolds, _walkHolds) = (new LayerBound<int>(_ground.PlanHold), new LayerBound<int>(_walkingGround.WalkHold));
            _withTheLayer =
            [
                _planHolds, _walkHolds, new LayerBound<int>(_walkingGround.HeldAsTables.Occupant),
                new LayerBound<LaneRoster>(_walkingGround.HeldAsTables.Roster), new LayerBound<long>(_overtaking.LaidFor),
                new LayerBound<long>(_switching.LaidFor), new LayerBound<long>(_bays.LaidFor),
            ];
        }

        foreach (var bound in _withTheLayer) bound.Save();
        _laying.Begin();
        LayOn(_laying);
    }

    /// <summary>The room a laying of the town's own begins with.</summary>
    const int LayingRoom = 1024;

    /// <summary>
    /// What the town keeps of each agent that means something only with the layer it was laid with
    /// (<see cref="LayerBound{T}"/>): the hold numbers, whose ground a walker's body was laid as, which ask a pass's ground
    /// was laid for.
    /// </summary>
    ILayerBound[]? _withTheLayer;

    LayerBound<int>? _planHolds, _walkHolds;

    /// <summary>
    /// A car's hold number as the readers read it once the laying put aside lands (<see cref="LandTheLaying"/>) — or, where
    /// <paramref name="landing"/> is false, as they read it now.
    /// </summary>
    public int PlanHoldOf(int car, bool landing) => landing && _planHolds != null ? _planHolds.Adopted(car) : _ground.PlanHold[car];

    /// <summary>A walker's hold number as the readers read it once the laying put aside lands, or as they read it now.</summary>
    public int WalkHold(int person, bool landing) => landing && _walkHolds != null ? _walkHolds.Adopted(person) : _walkingGround.WalkHold[person];

    /// <summary>
    /// <b>Back onto the layer every reader reads</b> once the device has answered — written back into it already, or, where
    /// it <paramref name="landsLater"/>, left as it stands with every agent's numbers as they were laid for it, the laying's
    /// own put aside until its layer lands (<see cref="LandTheLaying"/>).
    /// </summary>
    void LayIntoTheLayer(bool landsLater)
    {
        if (landsLater)
        {
            foreach (var bound in _withTheLayer!) bound.Park();
        }

        LayOn(_layer);
    }

    /// <summary>
    /// <b>A rebuild a device laid, landed</b> once its answers are written into the layer every reader reads: every agent's
    /// numbers taken up as laid for it, but where the town has changed one since.
    /// </summary>
    public void LandTheLaying()
    {
        foreach (var bound in _withTheLayer!) bound.Adopt();
    }

    /// <summary>
    /// <b>Back onto a laying put aside</b> (<see cref="LayIntoTheLayer"/>), for a device's answers written back after it while
    /// every reader reads the layer before: every agent's numbers as laid for it, until <see cref="PutTheLayingAsideAgain"/> —
    /// what the readers' numbers came to meanwhile kept for after.
    /// </summary>
    public void TakeUpTheLaying()
    {
        foreach (var bound in _withTheLayer!) bound.Resume();
    }

    /// <summary>The laying taken up (<see cref="TakeUpTheLaying"/>) put aside again, and the readers' numbers back.</summary>
    public void PutTheLayingAsideAgain()
    {
        foreach (var bound in _withTheLayer!) bound.Repark();
    }

    void LayOn(LaneOccupancy occupancy)
    {
        _occupancy = occupancy;
        _ground.Occupancy = occupancy;
        _walkingGround.Occupancy = occupancy;
    }

    /// <summary>
    /// <b>What a car whose claim is laid where it stands asks of a device laying it in the town's place</b>
    /// (<see cref="PlanTheDrive"/>): the ground a hand or a truck can no longer stop short of; a manoeuvre's hold before it
    /// is begun, laid to end where the car's claim was cleared to; or where a begun one looks for a body in what is left of
    /// it. Asked once every claim is cleared, of a car that does not plan down its line.
    /// </summary>
    /// <param name="count">How many of <paramref name="stretches"/> or <paramref name="asks"/> it filled.</param>
    public StandingClaim AsksWhereItStands(int car, Span<CommittedStretch> stretches, Span<BodyAsk> asks, out int count)
    {
        count = 0;
        switch (Cars.Action[car])
        {
            case CarAction.Hand:
                count = _ground.WhatCannotBeStoppedShortOf(car, car, stretches);
                return count > 0 ? StandingClaim.Committed : StandingClaim.None;

            case CarAction.Towed:
                count = _ground.WhatCannotBeStoppedShortOf(car, LaidAs(car), stretches);
                return count > 0 ? StandingClaim.Committed : StandingClaim.None;
        }

        if (!IsManoeuvring(car) || !_bays.HoldsWhereItStands(car)) return StandingClaim.None;
        if (!_manoeuvres.IsBegun(car)) return StandingClaim.Waiting;

        count = _bays.AsksOfTheManoeuvre(car, asks);
        return StandingClaim.Manoeuvre;
    }

    /// <summary>Where a car on a pass looks for a body inside it, for a device laying the bodies (<see cref="Following.AsksOfItsPass"/>).</summary>
    public int BodyAsksOfItsPass(int car, Span<BodyAsk> into) => _following.AsksOfItsPass(car, into);

    /// <summary>
    /// A car's plan down its line as a device laid it (<see cref="Following.Plan"/>): where it begins, how far it reaches,
    /// what it is committed to, and its horizon.
    /// </summary>
    /// <remarks>
    /// <b>Where it begins is the device's</b>, its nose off the reading the device holds, where the town's own clearing read
    /// the nose off a reading that may be older (<see cref="DrivingGround.ClearTheClaim"/>).
    /// </remarks>
    public void PlannedOnTheDevice(int car, float claimFromM, float claimToM, float committedToM, float horizonM)
    {
        Cars.ClaimFromM[car] = claimFromM;
        Cars.ClaimToM[car] = claimToM;
        Cars.CommittedToM[car] = committedToM;
        Cars.HorizonM[car] = horizonM;
    }

    /// <summary>
    /// The hold a car's claim laid this rebuild, as a device numbered it — and, where a device found a body inside its pass,
    /// held short of it with nothing granted until its grant is read (<see cref="DrivingGround.ClearTheClaim"/>).
    /// </summary>
    public void HeldOnTheDevice(int car, int hold, bool inItsPass)
    {
        _ground.PlanHold[car] = hold;
        if (inItsPass) Cars.AuthorityM[car] = 0f;
    }

    /// <summary>
    /// The hold a walker's walk laid this rebuild, as a device numbered it — and, where the device committed it to a
    /// straight, the same straight committed here (<see cref="WalkingGround.TheStraightAhead"/>).
    /// </summary>
    /// <returns>False where the device committed the walker to a straight and the town's own walk would not have.</returns>
    public bool WalkedOnTheDevice(int person, int hold, bool committedAStraight)
    {
        _walkingGround.WalkHold[person] = hold;
        if (!committedAStraight) return true;

        var (fromM, toM) = (_walkingGround.StraightFromM(person), _walkingGround.StraightToM(person));
        Span<LineWay> walk = stackalloc LineWay[WalkingGround.MostWaysAlongAWalk];
        WaysOfTheWalk(person, walk);
        return !Same(_walkingGround.StraightFromM(person), fromM) || !Same(_walkingGround.StraightToM(person), toM);

        // A straight never committed to is walked to no number, which is the same as itself here.
        static bool Same(System.Numerics.Vector2 one, System.Numerics.Vector2 other) => one.X.Equals(other.X) && one.Y.Equals(other.Y);
    }

    /// <summary>
    /// What a device read under a car's wheels when it laid its body (<see cref="LayTheCarsBody"/>): the channels of the
    /// ways they stand on, put on where they differ, and which of them stand on one, kept for its tyres.
    /// </summary>
    public void OnTheGroundOfItsBody(int car, byte channels, byte wheelsOnAWay)
    {
        if (channels != Cars.Channels[car]) PutOnChannels(car, channels);
        Cars.WheelsOnAWay[car] = wheelsOnAWay;
    }

    /// <summary>
    /// A car's grant as a device read it off what its hold came to (<see cref="DrivingGround.ReadTheGrant"/>, done there):
    /// how far past its claim's start it may go, what it keeps off what cut it, and what that was.
    /// </summary>
    public void GrantedOnTheDevice(int car, float authorityM, float marginM, HeadwayKind cutBy)
    {
        Cars.AuthorityM[car] = authorityM;
        Cars.GrantMarginM[car] = marginM;
        Cars.GrantCutBy[car] = cutBy;
    }

    /// <summary>
    /// A walker's hold as a device read what it came to (<see cref="WalkingGround.ReadTheGrant"/>'s reading, done there):
    /// whether it laid one, where that ended, what it keeps off what cut it, and the way it was cut on — granted to the
    /// walker where the town would read it (<see cref="ReadTheWalkersGrants"/>).
    /// </summary>
    public void WalkerGrantedOnTheDevice(int person, bool laidAHold, float endsAtM, float marginM, int cutOn) =>
        _walkGrantedOnTheDevice[person] = (laidAHold, endsAtM, marginM, cutOn);

    /// <summary>Each walker's hold as a device read what it came to, this rebuild (<see cref="WalkerGrantedOnTheDevice"/>).</summary>
    (bool LaidAHold, float EndsAtM, float MarginM, int CutOn)[] _walkGrantedOnTheDevice = [];

    /// <summary>
    /// <b>What a device that keeps the walkers made of one this rebuild</b> (<see cref="ITownClaims.KeepsTheWalkers"/>), taken
    /// up as the town's own: stationed on its walk (<see cref="StationTheWalker"/>) — a way taken being progress — its body
    /// laid as whoever's ground it is, its pass laid or let go, and, where the device read the grants, what it was granted,
    /// its pass's word and where it aims.
    /// </summary>
    /// <param name="restartsTheWalk">
    /// Whether a way taken restarts its walk's clock here — or the clock is the device's, taken with it, where it was left
    /// on the device a rebuild or more and the device has run it since.
    /// </param>
    public void KeptOnTheDevice(int person, in KeptWalker kept, bool restartsTheWalk = true)
    {
        var people = People;
        if (kept.RouteTaken != people.RouteTaken[person])
        {
            people.RouteTaken[person] = kept.RouteTaken;
            if (restartsTheWalk) _progress.Restart(person);
        }

        people.DestinationM[person] = kept.DestinationM;
        people.OnWayM[person] = kept.OnWayM;
        people.OffWayM[person] = kept.OffWayM;
        people.OnWay[person] = kept.OnWay;
        people.GrantM[person] = kept.GrantM;
        people.OnCrossing[person] = kept.OnCrossing;
        people.Pass[person] = kept.Pass;
        people.Walking[person] = kept.Walking;
        people.Action[person] = kept.Action;
        _walkingBack.WalksBackTo(person, kept.BackToM);
        _walkingGround.HoldTheGroundAs(person, kept.HeldAs, kept.HeldAsRoster);
        _sidestepping.CountTheWord(kept.MadeItsPass, kept.WithdrewItsPass);
    }

    /// <summary>
    /// <b>The ground a car asked this laying means to back up over</b> (<see cref="BackingUp.Asks"/>), for a device laying
    /// the backing beside the town's own — read and never written.
    /// </summary>
    public bool AsksToBackUp(int car, out BackingAsk ask) => _backingUp.Asks(car, IsUnderWay(car), out ask);

    /// <summary>
    /// <b>And taken by a device that lays the backing in the town's place</b> (<see cref="BackingUp.TakeTheAsk"/>): what the
    /// car asked for cleared, and how much it was had left for the device to say (<see cref="BackedUpOnTheDevice"/>).
    /// </summary>
    public bool TakeTheBackingAsk(int car, out BackingAsk ask) => _backingUp.TakeTheAsk(car, IsUnderWay(car), out ask);

    /// <summary>How far back down its way a device laid a car's backing (<see cref="BackingUp.Had"/>).</summary>
    public void BackedUpOnTheDevice(in BackingAsk ask, float reachM) => _backingUp.Had(ask, reachM);

    /// <summary>How a device's answering of the plans went (<see cref="AnswerThePlans"/>): answers moved past the first pass, and whether they settled.</summary>
    public void AnsweredOnTheDevice(int laidAgain, bool settled)
    {
        PlansLaidAgain += laidAgain;
        if (!settled) Unsettled++;
    }
}

/// <summary>How a car's claim laid where it stands is laid (<see cref="TownWorld.AsksWhereItStands"/>).</summary>
internal enum StandingClaim
{
    /// <summary>Nothing: at rest under a hand or a truck, or a manoeuvre's car not driven or broken.</summary>
    None,

    /// <summary>A hold of what a hand or a truck can no longer stop short of, never over a body — not the car's own hold.</summary>
    Committed,

    /// <summary>A manoeuvre not yet begun: the car's hold, with no ground, ended where its claim was cleared to.</summary>
    Waiting,

    /// <summary>A begun manoeuvre: the car's hold short of the first body standing in what is left of it, or none.</summary>
    Manoeuvre,
}

/// <summary>
/// What a device that keeps the walkers made of one in a rebuild (<see cref="TownWorld.KeptOnTheDevice"/>): where it aims,
/// where it walks back to, where on its way it stands and how far off it, what it was granted, its pass, how far down its
/// route it has come, the way it is afoot on and the zebra it is on or held off, whose ground its body was laid as, whether
/// it walks and what it is doing — and whether its pass was made or withdrawn.
/// </summary>
internal readonly record struct KeptWalker(
    Vector2 DestinationM, Vector2 BackToM, float OnWayM, float OffWayM, float GrantM, Sidestep Pass, int RouteTaken, int OnWay,
    int OnCrossing, int HeldAs, LaneRoster HeldAsRoster, bool Walking, PersonAction Action, bool MadeItsPass, bool WithdrewItsPass);
