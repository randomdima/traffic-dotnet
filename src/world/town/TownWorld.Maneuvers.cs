using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The driver's head.</b> It selects an entry of the catalogue, runs it to its defined exit, and
/// hands its exit state to the next one. <b>Nothing in this town drives a car outside a manoeuvre</b> —
/// there is no "and otherwise" branch here, because there is no such state in the catalogue.
/// </summary>
/// <remarks>
/// <para>
/// Four jobs, in the order §1.5 arbitrates them: the hard rules, which the sensing half raises because
/// by the time a procedure noticed one it would be too late; the running entry's own procedure; the
/// watchdog, which sends an entry past its bound to the next rung of the ladder; and interruption and
/// resumption — a reactive entry suspends the planned one, and the planned one is <b>re-entered through
/// its own <c>Sa</c></b> afterwards, never resumed mid-procedure.
/// </para>
/// <para>
/// It owns no geometry, no speed policy and no steering. Those are the standing rules, which run
/// underneath every entry and are <c>TownWorld.Driving.cs</c>'s.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    readonly ManeuverTrace _trace = new();

    /// <summary>What a driver has to hand: the ground questions and the templates. It knows nothing of routes, prices or trips.</summary>
    readonly ManeuverDesk _desk;

    /// <summary>The parametrised chain the planner hands each leg, and the cursor into it.</summary>
    readonly DrivePlan _drivePlans;

    /// <summary>
    /// What every car did and where it handed over. An instrument and never a rule: nothing in the town
    /// reads it, and the three faults it is for are in <see cref="ManeuverTrace"/>.
    /// </summary>
    public ManeuverTrace Trace => _trace;

    /// <summary>
    /// How many lane-and-crossing pairs the town came to. A crossing no lane was matched to is a zebra
    /// nobody slows for, so the count is printed beside the trace rather than assumed.
    /// </summary>
    public int CrossingsOnLanes => _furniture.CrossingsOnLanes;

    /// <summary>How many times a car has taken a rung of the ladder, and what each rung came to.</summary>
    public long LaddersClimbed { get; private set; }

    public long BackOffsTaken { get; private set; }

    public long SwervesTaken { get; private set; }

    public long PlacesGivenUp { get; private set; }

    public long ReroutesTaken { get; private set; }

    public long GroundRecoveries { get; private set; }

    public long LegsSettled { get; private set; }

    public long CarsAbandoned { get; private set; }

    // the plan (MAN-1, MAN-2)

    /// <summary>
    /// <b>The chain this leg is</b>: leave the bay it stands in, run the route, park in the bay it holds,
    /// stand in it. Everything between those — the queues, the junctions, the crossings — is not planned
    /// and cannot be, because past the next junction it is a prediction about other agents; those entries
    /// are reached from `P-4`'s own exits as the road produces them.
    /// </summary>
    void PlanTheLeg(int car)
    {
        _drivePlans.Clear(car);

        // Whether the bay can actually be left is `P-2`'s own `Sa` and not the planner's: a step whose
        // entry state does not hold is re-derived where it is reached, and asking the question twice is
        // how the two answers come to disagree.
        var standingIn = _parking.BayOf(car);
        if (standingIn >= 0) _drivePlans.Add(car, Maneuver.LeaveTheBay, standingIn);

        _drivePlans.Add(car, Maneuver.RunTheLine);

        var going = _parking.ReservationOf(car);
        if (going < 0) return;

        _drivePlans.Add(car, Maneuver.ParkInTheBay, going);
        _drivePlans.Add(car, Maneuver.StandParked, going);
    }

    /// <summary>
    /// The next step of the chain, taken. <b>An empty chain answers `P-4`</b> — which re-derives from the
    /// pose the car actually reached, and is what MAN-3 asks for anyway.
    /// </summary>
    void TakeTheNextStep(int car)
    {
        var step = _drivePlans.Take(car);

        // A chain re-derived in the middle of a leg still carries the step the car is already driving —
        // a retarget rebuilds the whole skeleton, including the `P-4` the car is on. Taking it would be
        // asking a manoeuvre to hand over to itself, and the step after it would be reached a tick late.
        while (step.Id != Maneuver.None && step.Id == Cars.Doing[car]) step = _drivePlans.Take(car);

        GoTo(car, step.Id == Maneuver.None ? Maneuver.RunTheLine : step.Id, step.Subject);
    }

    // installing an entry

    /// <summary>
    /// Hand over to a named entry. <b><c>Sa</c> is checked, not assumed</b>: an entry named as a
    /// successor whose entry state no longer holds means the chain has a gap in it, and a gap is
    /// re-derived rather than driven through (MAN-1, MAN-3).
    /// </summary>
    void GoTo(int car, Maneuver next, int subject)
    {
        if (Install(car, next, subject)) return;

        // A <em>recovery</em> whose `Sa` does not hold is a rung to skip, and skipping rungs is precisely
        // what the ladder does. Sending it back to `P-4` instead is a loop with no exit: `E-7` refused
        // for having already rerouted twice hands to `P-4`, replans the same blocked road, jams on it and
        // asks for `E-7` again, for the rest of the run and without ever climbing high enough to reach a
        // rung that ends the leg.
        if (Maneuvers.IsReactive(next))
        {
            Escalate(car);
            return;
        }

        if (Install(car, RederiveFrom(car, next), PlannedStep.NoSubject)) return;
        if (Install(car, Maneuver.SettleForHere, PlannedStep.NoSubject)) return;

        Install(car, Maneuver.AbandonTheCar, PlannedStep.NoSubject);
    }

    /// <summary>
    /// <c>Sa</c>, the one order the entry asks the town for, and the take-up — in that order, so a
    /// refusal has written nothing. <b>An order the town cannot carry is a refusal too</b>, which is what
    /// lets `E-6` mean "retarget, and if there is nowhere to retarget to, take the next rung".
    /// </summary>
    bool Install(int car, Maneuver id, int subject)
    {
        if (id == Maneuver.None) return false;

        // <b>A claim belongs to the entry that took it</b>, so it is given back here rather than by each
        // entry on the way out of itself: an exit an entry forgot would be a stretch of road nobody could
        // ever use again. The two entries that want one take it in their own `Sa`, below, and `P-2` renews
        // it every tick it goes on waiting.
        Cars.ClaimWay[car] = CarFleet.NoWay;

        var start = ManeuverCatalogue.Begin(id, SceneOf(car), _desk, subject);
        if (!start.CanEnter) return false;
        if (start.Order != DriveOrder.None && !Carry(car, start.Order, start.Subject)) return false;

        Enter(car, id, subject);
        return true;
    }

    /// <summary>
    /// What to do when a named successor's <c>Sa</c> does not hold. A car off drivable ground has exactly
    /// one answer; a car that cannot park where it meant to has a destination problem rather than a
    /// driving one; and getting back onto the road covers everything else, because `P-4` draws its line
    /// from the actual pose and picks the route up from there.
    /// </summary>
    Maneuver RederiveFrom(int car, Maneuver wanted)
    {
        if (!_desk.StandsOnDrivableGround(car)) return Maneuver.ReturnToLegalGround;

        return wanted switch
        {
            Maneuver.ParkInTheBay or Maneuver.StandParked or Maneuver.SquareUpInTheBay => Maneuver.GiveUpThePlace,
            Maneuver.RunTheLine => Maneuver.SettleForHere,
            _ => Maneuver.RunTheLine,
        };
    }

    /// <summary>
    /// The one place a car's entry changes, so the trace sees every hand-over, the per-entry counters are
    /// reset in exactly one place, and the driver thinks on the next tick rather than at the end of the
    /// interval the entry it has just left was scheduled on.
    /// </summary>
    void Enter(int car, Maneuver next, int subject)
    {
        var doing = Cars.Doing[car];
        Cars.About[car] = subject;
        Cars.Limits[car] = DriveLimits.None;
        Cars.SinceDecisionS[car] = 0f;

        // Taking up a planned entry is the end of any interruption, however the reactive one left: `E-3`
        // names `P-4` as its successor rather than resuming, and a suspension left standing behind it is
        // an entry a later hand-back would return to years after it stopped applying.
        if (!Maneuvers.IsReactive(next)) Cars.Suspended[car] = Maneuver.None;

        if (doing == next) return;

        // In one spot: the pair has just swapped back, the car has not covered its own length since the
        // swap before, and it did not spend long there. All three matter — a car creeping through a queue
        // really does hold and run and hold again.
        var atM = Cars.PositionM[car];
        var inOneSpot = Cars.Was[car] == next
                        && (atM - Cars.ChangedAtM[car]).Length() < _config.Car.LengthM
                        && Cars.InManeuverS[car] < ShuttleWindowS;

        _trace.Changed(doing, next, inOneSpot);
        Cars.Was[car] = doing;
        Cars.Doing[car] = next;
        Cars.ChangedAtM[car] = atM;
        Cars.InManeuverS[car] = 0f;
        CountTheEntry(next);
    }

    /// <summary>
    /// How quickly a pair has to swap a car back for the trace to call it a loop rather than traffic. A
    /// second: several times the settling the arbitration itself asks for, and far less than a car spends
    /// holding at anything real.
    /// </summary>
    const float ShuttleWindowS = 1f;

    /// <summary>The town's own tallies, taken where an entry is taken up rather than where it acts, so each is counted exactly once.</summary>
    void CountTheEntry(Maneuver entry)
    {
        switch (entry)
        {
            case Maneuver.BackOff: BackOffsTaken++; return;
            case Maneuver.GoRound: SwervesTaken++; return;
            case Maneuver.GiveUpThePlace: PlacesGivenUp++; return;
            case Maneuver.Reroute: ReroutesTaken++; return;
            case Maneuver.ReturnToLegalGround: GroundRecoveries++; return;
            case Maneuver.SettleForHere: LegsSettled++; return;
            case Maneuver.AbandonTheCar: CarsAbandoned++; return;
        }
    }

    /// <summary>A car under a hand at the wheel, a wreck, or one whose driver has got out: the catalogue does not apply to it (S-7).</summary>
    void LeaveTheCatalogue(int car)
    {
        Cars.Doing[car] = Maneuver.None;
        Cars.Suspended[car] = Maneuver.None;
        Cars.Limits[car] = DriveLimits.None;
        Cars.About[car] = PlannedStep.NoSubject;
        Cars.ClaimWay[car] = CarFleet.NoWay;
        _drivePlans.Clear(car);
    }

    // interruption and resumption (§1.6)

    /// <summary>
    /// A reactive entry takes over and the planned one is remembered. <b>Only one interruption deep</b>:
    /// a reactive entry that itself needs help does not stack another on top — it escalates, which is the
    /// ladder's job and is bounded by construction.
    /// </summary>
    void Interrupt(int car, Maneuver reactive)
    {
        if (!Maneuvers.IsReactive(Cars.Doing[car])) Cars.Suspended[car] = Cars.Doing[car];

        Install(car, reactive, PlannedStep.NoSubject);
    }

    /// <summary>
    /// The obligation is discharged: re-enter the interrupted entry <b>through its own <c>Sa</c></b>, and
    /// re-derive the plan if that no longer holds.
    /// </summary>
    void HandBackToThePlan(int car)
    {
        var back = Cars.Suspended[car];
        Cars.Suspended[car] = Maneuver.None;
        GoTo(car, back == Maneuver.None ? Maneuver.RunTheLine : back, Cars.About[car]);
    }

    /// <summary>Everything a leg holds on the ladder's behalf, dropped when the leg ends or begins.</summary>
    void RestTheLadder(int car)
    {
        Cars.Rung[car] = 0;
        Cars.BackOffs[car] = 0;
        Cars.Reroutes[car] = 0;
        Cars.Recoveries[car] = 0;
        Cars.BlockedS[car] = 0f;
        Cars.ClimbedFromM[car] = Cars.PositionM[car];
    }

    // the decision (§1.5a) and the watchdog

    /// <summary>
    /// One turn of the driver's own head: the entry in charge runs its procedure, the watchdog decides
    /// whether standing still is waiting or being stuck, and whatever the entry named happens.
    /// </summary>
    /// <remarks>
    /// <b>Naming and escalating are on the same clock and the sensing is not.</b> A body moves every
    /// physics tick and always will; a procedure runs when the driver thinks, which is every
    /// <c>AgentDecisionIntervalS</c> — or every tick, for the entries that declare themselves
    /// unschedulable because they are negotiating with something that is itself moving or steering to a
    /// pose.
    /// </remarks>
    void DecideDriver(int car, float sinceLastDecisionS)
    {
        // A car with nobody in it takes no action, and a hand at the wheel suspends every manoeuvre —
        // neither is a car for the catalogue to have opinions about.
        if (!Cars.Driven[car] || Cars.Broken[car] || Handed(Roster.AgentOfCar(car)))
        {
            Cars.BlockedS[car] = 0f;
            return;
        }

        // The elapsed the entry integrates over is the driver's own and not the loop's nominal interval:
        // an entry asked on every tick would otherwise run its clocks at six times real time.
        var elapsedS = Cars.SinceDecisionS[car] > 0f ? Cars.SinceDecisionS[car] : sinceLastDecisionS;
        Cars.SinceDecisionS[car] = 0f;

        // The ladder rewinds on road covered, never on manoeuvres completed: a jammed car completes
        // manoeuvres continuously, with the body exactly where it started.
        var atM = Cars.PositionM[car];
        if ((atM - Cars.ClimbedFromM[car]).Length() > _config.CarLadderRewindM)
        {
            Cars.Rung[car] = 0;
            Cars.BackOffs[car] = 0;
            Cars.ClimbedFromM[car] = atM;
        }

        var scene = SceneOf(car);
        var limits = DriveLimits.None;
        var outcome = ManeuverCatalogue.Tick(Cars.Doing[car], scene, _desk, elapsedS, ref limits);
        Cars.Limits[car] = limits;

        // The bar for moving is a rate — the same one the speed profile calls a stop. A per-tick distance
        // is a speed in disguise.
        var moving = Cars.VelocityMps[car].Length() > _config.Driving.StopSpeedMps;
        if (moving || WaitingForAReasonItCanSee(car)) Cars.BlockedS[car] = 0f;
        else Cars.BlockedS[car] += elapsedS;

        // And the patience of a car that never stops: a body reeling down the lane in front is got past on
        // the same wait as one standing in it, and a car crawling behind it spends no blocked clock at all.
        if (scene.HeldBackBySomethingSlow) Cars.HeldBackS[car] += elapsedS;
        else Cars.HeldBackS[car] = 0f;

        // The watchdog runs in every phase, not only while touring. Entries with no watchdog are how two
        // cars nose to nose hold each other for a whole run.
        if (outcome.Kind == ManeuverOutcomeKind.Running
            && ManeuverCatalogue.Watched(Cars.Doing[car])
            && Cars.BlockedS[car] >= ManeuverCatalogue.FuseS(Cars.Doing[car], scene) * Cars.FuseJitter[car])
        {
            outcome = ManeuverOutcome.Escalate(ManeuverReason.Bounded);
        }

        Apply(car, outcome);
    }

    /// <summary>What the entry named, done. Every arm of this is one row of §1.2's exits and there is no other.</summary>
    void Apply(int car, in ManeuverOutcome outcome)
    {
        switch (outcome.Kind)
        {
            case ManeuverOutcomeKind.Running:
                return;

            case ManeuverOutcomeKind.Succeeded:
                // Nothing is given back here — not the ladder, and not the back-off. **Completing a
                // manoeuvre is not the same as getting somewhere**: a jammed car completes them
                // continuously, three successes in as many ticks with the body exactly where it started.
                // What earns the ladder back is road actually covered.
                if (outcome.Next == Maneuver.None) TakeTheNextStep(car);
                else GoTo(car, outcome.Next, Cars.About[car]);

                return;

            case ManeuverOutcomeKind.Failed:
                GoTo(car, outcome.Next, PlannedStep.NoSubject);
                return;

            case ManeuverOutcomeKind.Resume:
                HandBackToThePlan(car);
                return;

            case ManeuverOutcomeKind.Escalate:
                Escalate(car);
                return;

            case ManeuverOutcomeKind.Finished:
                EndTheLeg(car);
                return;
        }
    }

    /// <summary>
    /// One rung, and the next one that names something if this one's <c>Sa</c> refuses. <b>A recovery
    /// whose entry condition fails takes the next rung and never the plan</b> — sending a refused
    /// recovery back to `P-4` is a loop with no exit.
    /// </summary>
    void Escalate(int car)
    {
        Cars.BlockedS[car] = 0f;
        if (!Maneuvers.IsReactive(Cars.Doing[car])) Cars.Suspended[car] = Cars.Doing[car];

        var state = LadderStateOf(car);
        var rung = Cars.Rung[car];
        while (rung < DrivingLadder.Rungs)
        {
            var next = DrivingLadder.Next(state, ref rung);
            Cars.Rung[car] = rung;
            if (!Install(car, next, _drivePlans.SubjectFor(car, next))) continue;

            LaddersClimbed++;
            return;
        }

        Install(car, Maneuver.AbandonTheCar, PlannedStep.NoSubject);
        LaddersClimbed++;
    }

    /// <summary>
    /// What the ladder is allowed to ask about the car in front of it — <b>every field a question about
    /// the pose or the holdings</b>, so that a rung which presumes an obstruction can check for one. The
    /// walked answers are taken here and nowhere else: climbing is rare, and asking them every tick would
    /// put a terrain walk on the town's hottest path.
    /// </summary>
    LadderState LadderStateOf(int car)
    {
        var scene = SceneOf(car);
        var bay = _parking.BayOf(car);
        var reserved = _parking.ReservationOf(car);
        var jammed = scene.SomethingToBackAwayFrom;

        return new LadderState(
            ObstructionHasPriority: scene.ObstructionHasPriority,
            SomethingToBackAwayFrom: jammed,
            RoomBehindM: jammed ? _desk.RoomAlongTheAxisM(car, !scene.Reverse) : 0f,
            BackOffsLeft: scene.BackOffsLeft,
            InItsOwnBay: bay >= 0 && Cars.Doing[car] == Maneuver.LeaveTheBay
                         && Cars.ProgressM[car] <= _config.Car.LengthM * 0.5f,
            AtItsOwnBay: reserved >= 0
                         && (_parking.CentreM(reserved) - Cars.PositionM[car]).Length() <= _config.Car.LengthM * 2f,
            HoldsAPlace: reserved >= 0,
            OnARoute: scene.OnARoute,
            ReroutesLeft: scene.ReroutesLeft,
            AStraightCanSaveIt: !scene.OnDrivableGround && _desk.StraightToLegalGround(car, out _, out _),
            SomewhereLegalToStop: scene.OnDrivableGround && !scene.AcrossALane);
    }

    // what the catalogue is allowed to see

    /// <summary>
    /// The driving state of §1.1, gathered from what the sensing half of the tick wrote down. <b>Nothing
    /// here is worked out twice</b>: every field is either a reading the speed profile was given or a
    /// holding the town already knows, so the entry a car is in and the term that bound its speed can
    /// never disagree.
    /// </summary>
    DriveScene SceneOf(int car) => new()
    {
        Config = _config,
        Car = car,
        AlongMps = Cars.AlongMps[car],
        ProgressM = Cars.ProgressM[car],
        Line = Cars.Line[car],
        Reverse = Cars.LineIsReverse[car],
        Hold = Cars.Hold[car],
        Context = Cars.Context[car],
        ToTheBoxM = Cars.ToTheBoxM[car],
        BoxIsOurs = Cars.BoxIsOurs[car],
        InsideTheBox = Cars.InsideTheBox[car],
        LightAheadM = Cars.LightAheadM[car],
        BayHeld = _parking.BayOf(car),
        BayReserved = _parking.ReservationOf(car),
        OnTheFinalApproach = IsOnTheFinalApproach(car),
        LaneOn = Cars.LaneOf(car),
        RouteReversesHere = RouteReversesHere(car),
        InManeuverS = Cars.InManeuverS[car],
        BlockedS = Cars.BlockedS[car],
        HeldBackS = Cars.HeldBackS[car],
        WaitedS = Cars.WaitedS[car],
        GapIsClear = !IsWaitingInTheMouth(car) || _desk.GapIsClear(car),
        OnDrivableGround = _desk.StandsOnDrivableGround(car),
        BackOffsLeft = _config.Ladder.BackOffAttemptsPerJam - Cars.BackOffs[car],
        PlannedMps = Cars.PlannedMps[car],
        ReroutesLeft = _config.Ladder.ReroutesPerLeg - Cars.Reroutes[car],
        RecoveriesUsed = Cars.Recoveries[car],
    };

    /// <summary>
    /// The one window in which the gap question is worth its cost: a car on its way out of a bay whose
    /// body has not yet crossed the mouth. Everywhere else the answer is <c>true</c>, which is what makes
    /// the entry reading it do nothing.
    /// </summary>
    bool IsWaitingInTheMouth(int car) =>
        Cars.Doing[car] == Maneuver.LeaveTheBay
        && Cars.Line[car].LaneCount == 0
        && Cars.ProgressM[car] < _config.Car.LengthM * 0.5f;

    /// <summary>
    /// Whether the next lane this leg takes is the reverse of the one under the car — the route reversing
    /// direction of travel, which is `P-11`'s whole scenario and the only movement a junction is the only
    /// place for.
    /// </summary>
    bool RouteReversesHere(int car)
    {
        var lanes = Cars.Line[car].LaneCount;
        if (lanes == 0) return false;

        var chain = Cars.ChainOf(car);
        var ahead = LaneAheadSlot(car, Cars.ProgressM[car]);
        var next = ahead + 1 < lanes ? chain[ahead + 1] : Cars.PeekNextRouteLane(car);
        return next >= 0 && _roads.LaneReverse[chain[ahead]] == next;
    }

    /// <summary>
    /// <b>Whether anything at all is timing this car</b> — motion, the watchdog's own clock, a light that
    /// will change, or the give-way patience it is spending in its bay. It asserts nothing; what it is
    /// for is the trace, where a standing car nothing is running for is a fault no other counter can show.
    /// </summary>
    bool IsClocked(int car) =>
        Cars.VelocityMps[car].Length() > _config.Driving.StopSpeedMps
        || WaitingForAReasonItCanSee(car)
        || (Cars.Driven[car] && !Cars.Broken[car] && !Handed(Roster.AgentOfCar(car)));

    /// <summary>
    /// The two waits that spend no clock at all: <b>a light</b>, which will change on its own, and the
    /// <b>gap a car waits for in the bay it still holds</b>, which is bounded by give-way patience and is
    /// the one piece of road that car is entitled to occupy.
    /// </summary>
    /// <remarks>
    /// Everything else that stands still spends the clock, including a lawful yield: waiting for a
    /// junction somebody else is in is correct right up until it has been correct for half a minute, and
    /// after that it is a jam rather than traffic (`E-1`).
    /// </remarks>
    bool WaitingForAReasonItCanSee(int car) =>
        Cars.LightAheadM[car] <= _config.Car.LengthM * QueueLengthInCars
        || (Cars.Doing[car] == Maneuver.LeaveTheBay && Cars.Limits[car].HoldStill);

    /// <summary>
    /// How long a queue at a light reaches back, in cars. <b>The test is "a red ahead, within a queue's
    /// length of it"</b>, so it has to cover the whole queue and not only its front — and a body bogged
    /// on the verge beside that junction meets it too.
    /// </summary>
    const float QueueLengthInCars = 20f;

    /// <summary>The direction the body is pointing, which every line is read back through.</summary>
    Vector2 ForwardOf(int car)
    {
        var headingRad = Cars.HeadingRad[car];
        return Heading.Unit(headingRad);
    }
}
