using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>Everything a manoeuvre is allowed to know</b>, gathered once by the standing rules before any
/// procedure runs. It is the driving state of §1.1 — pose, motion, ground, line relation, holdings,
/// plan and counters — as one blittable value, so an entry's <c>Sa</c> and its exits are arithmetic on
/// facts rather than queries into a town.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here is a decision.</b> Every field is a reading taken this tick by the sensing half of
/// the driver, and the same reading is what the speed profile was given — so the entry a car is in and
/// the term that bound its speed can never disagree.
/// </para>
/// <para>
/// <b>One field is conditional and says so</b>: <see cref="RouteReversesHere"/> is only meaningful on a
/// route, and reads <c>false</c> where it was not asked — the answer that makes the entry reading it do
/// nothing.
/// </para>
/// </remarks>
internal readonly record struct DriveScene
{
    /// <summary>Every figure the catalogue reads. <b>A literal in an entry's procedure is a defect.</b></summary>
    public required SimConfig Config { get; init; }

    public required int Car { get; init; }

    /// <summary>
    /// <b>The car this scene is about</b> (CAR-11): its own body, brakes and turning circle. An entry that
    /// asks how far this car needs to stop, or whether a gap will take it, is asking about the body it is
    /// in and never about the nominal one the streets were drawn for.
    /// </summary>
    public required CarBuild Build { get; init; }

    /// <summary>Speed along the direction the line is being driven in — negative where the car is going the other way.</summary>
    public required float AlongMps { get; init; }

    /// <summary>Where the rear axle is along the line (CAR-4a).</summary>
    public required float ProgressM { get; init; }

    public required DrivenLine Line { get; init; }

    /// <summary>Whether the line in hand is driven backwards. A property of the line and never of the car.</summary>
    public required bool Reverse { get; init; }

    /// <summary>Which of the things that limit a car limited this one — the term the entry is named off.</summary>
    public required DrivingHold Hold { get; init; }

    /// <summary>What the book and the paint told the speed profile: headway, stop point, ground, crossing.</summary>
    public required DriveContext Context { get; init; }

    /// <summary>How far ahead the box the car's own line enters stands, or infinity where its line enters none.</summary>
    public required float ToTheBoxM { get; init; }

    /// <summary>Whether that box is this car's to enter — the claim held, a green, or the car already in it.</summary>
    public required bool BoxIsOurs { get; init; }

    /// <summary>Whether the body is <em>in</em> the box rather than approaching one, which is what puts it on the short fuse.</summary>
    public required bool InsideTheBox { get; init; }

    /// <summary>How far ahead stands a light showing this car anything but green, or infinity where there is none.</summary>
    public required float LightAheadM { get; init; }

    /// <summary>The bay this car is standing in, or −1.</summary>
    public required int BayHeld { get; init; }

    /// <summary>The bay this leg has booked, or −1.</summary>
    public required int BayBooked { get; init; }

    /// <summary>Whether the line in hand finishes on the way into the bay this leg is aimed at.</summary>
    public required bool OnTheFinalApproach { get; init; }

    /// <summary>
    /// How far ahead of the rear axle that way begins — where the line leaves the road for the bay — or
    /// infinity where the line finishes on the road. <b>Negative once the car is on it</b>, which is what
    /// hands the last dozen metres of a leg over to the entry that is asked on every tick.
    /// </summary>
    public required float ToTheBayM { get; init; }

    /// <summary>
    /// <b>How far ahead of the rear axle the place this car has been sent to stands</b>, along the line it
    /// is driving — `P-18`'s whole <c>Sa</c>, and infinity for every car that has not been sent anywhere.
    /// <b>Negative once the car is past it</b>, exactly as <see cref="ToTheBayM"/> is.
    /// </summary>
    /// <remarks>
    /// It is a place and never a body. The catalogue is not handed a town (<see cref="ManeuverDesk"/>), so
    /// what a driver knows about a casualty in the road is the one thing a driver could see about it: where
    /// it is on the line in front. Whether anybody is still there, and what the crew does when they arrive,
    /// is the town's.
    /// </remarks>
    public required float ToTheSceneM { get; init; }

    /// <summary>
    /// <b>Whether this driver is answering a call</b> (AMB-4) — the blue light, as the one thing about it
    /// the catalogue needs: an ambulance on a call does not wait out its patience before crossing the
    /// centreline to get past something.
    /// </summary>
    public required bool Urgent { get; init; }

    /// <summary>The lane the car is on, or −1 where it is on none.</summary>
    public required int LaneOn { get; init; }

    /// <summary>
    /// <b>Whether this leg comes back the other way from the end of the line it is holding</b> (GEN-4l):
    /// the route reverses at the stretch the line finishes on, and what is past its end is a turn rather
    /// than a lane. Where a bay was booked to turn in the plan already holds the two steps that make it;
    /// where none was, `P-19` is what is left.
    /// </summary>
    public required bool TurnsBackHere { get; init; }

    /// <summary>How long the car has been in the entry it is in. MAN-4's bound, for every entry that carries a time.</summary>
    public required float InManeuverS { get; init; }

    /// <summary>
    /// How long it has stood still with nothing it can see timing it — the watchdog's own clock, and the
    /// bound of any entry whose standing still is what has to be bounded rather than its running.
    /// </summary>
    /// <remarks>
    /// It is not <see cref="InManeuverS"/>: the default entry is the one a car spends its whole journey in,
    /// so a clock started when it was entered says how long the car has been driving and not how long it
    /// has been stuck. A red ahead and a bay's own give-way wait spend none of it.
    /// </remarks>
    public required float BlockedS { get; init; }

    /// <summary>
    /// How long it has been held below the pace this road affords by something slow in front of it —
    /// <b>the same patience as <see cref="BlockedS"/>, for the case where the car never stops</b>.
    /// </summary>
    public required float HeldBackS { get; init; }

    /// <summary>Whether the whole body — centre, nose and tail — stands on ground a car may drive on.</summary>
    public required bool OnDrivableGround { get; init; }

    /// <summary>Back-offs left in this jam, reroutes left in this leg, and recoveries already spent on it.</summary>
    public required int BackOffsLeft { get; init; }

    /// <summary>
    /// What the profile would have asked for with the road to itself — every term but the grant.
    /// <b>It is the pace the road affords here</b>, which is what says whether the thing in front is slow.
    /// </summary>
    public required float PlannedMps { get; init; }

    public required int ReroutesLeft { get; init; }

    public required int RecoveriesUsed { get; init; }

    /// <summary>A line with arcs but no lanes under it, which is what a manoeuvre's own geometry always is here.</summary>
    public bool OnATemplate => Line.ArcCount > 0 && Line.LaneCount == 0;

    /// <summary>On a route: a line with lanes under it, which the road-going entries hold and never lay.</summary>
    public bool OnARoute => Line.LaneCount > 0;

    /// <summary>At rest by the same bar the speed profile calls a stop, so the two can never disagree.</summary>
    public bool AtRest => MathF.Abs(AlongMps) <= Config.Driving.StopSpeedMps;

    /// <summary>What is left of the line ahead of the rear axle.</summary>
    public float ToTheEndM => Line.LengthM - ProgressM;

    /// <summary>
    /// <b>The road this car needs to come to rest</b> from the speed it is doing, on the ground it is
    /// standing on — the same arithmetic every reservation in the town is sized by, so an entry that hands
    /// over "once it is near enough to stop for" means the same distance the road does.
    /// </summary>
    public float StoppingM
    {
        get
        {
            var mps = MathF.Max(0f, AlongMps);
            return mps * mps / (2f * CarFollower.BrakingMps2(Config, Build, Context.GroundCoefficient));
        }
    }

    /// <summary>
    /// The line has been driven to its end and the car has stopped there — the end condition every
    /// template shares, and the one place a manoeuvre driving geometry of its own is done.
    /// </summary>
    public bool LineIsSpent => ToTheEndM <= Config.Driving.StopSpeedMps && AtRest;

    /// <summary>
    /// <b>The car has stopped where its line runs out</b> — which is a car length short of the end rather
    /// than on it, because what a driver stops at the end of a road is its nose. It is the end condition
    /// for a line over the town's own lanes, where <see cref="LineIsSpent"/> is a template's.
    /// </summary>
    public bool StoppedAtTheEnd => AtRest && ToTheEndM <= Build.LengthM;

    /// <summary>
    /// <b>The place this car was sent to is near enough to be stopped for</b> — `P-18`'s hand-over, asked
    /// the same way wherever it is asked from (AMB-5).
    /// </summary>
    /// <remarks>
    /// <b>It is a property of the scene rather than a test inside the entry that asks it</b>, because a
    /// casualty lies where they were struck — very often a crossing on this town's geometry — and whatever
    /// stretch of road that turns out to be, the entry driving over it has to ask the question in the same
    /// words. Asked in one entry only, an ambulance passes its own casualty at four metres and goes round
    /// the block to try again.
    /// </remarks>
    public bool SceneIsNearEnoughToStopFor =>
        ToTheSceneM <= StoppingM + (Build.LengthM * SceneHandOverInCarLengths);

    /// <summary>
    /// How much slack over the stopping distance a scene is handed over at: two car lengths, which is the
    /// room the entry needs to have come to rest in rather than a margin on the arithmetic.
    /// </summary>
    const float SceneHandOverInCarLengths = 2f;

    /// <summary>
    /// Whether the body is in somebody else's way where it stands: inside a junction box, or committed to
    /// a template laid across the lane behind it. <b>It is what decides the fuse the car is watched on</b>
    /// — a car in that position is itself the obstruction, and patience is the wrong answer.
    /// </summary>
    public bool AcrossALane =>
        InsideTheBox || (OnATemplate && ProgressM > Build.HalfLengthM);

    /// <summary>
    /// `E-3`'s own <c>Sa</c>: <b>there must be something to back away from</b>. Four states count — something
    /// in the way that had no business being there, a boundary the car may not cross while it is itself
    /// standing across a lane, a template it can no longer follow, and a line it has lost.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Traffic is not one of them</b> (<see cref="NobodyEntitledIsInTheWay"/>). A driver queueing behind
    /// another driver, or held by the ground a crossing movement has claimed, is waiting for something that
    /// is going to move — and reversing away from it neither clears the queue nor changes the decision that
    /// is going to be re-taken from further back. What it does instead is put a car into ground the traffic
    /// behind is entitled to be standing in.
    /// </para>
    /// <para>
    /// <b>Being the obstruction is a reason of a different kind</b> (<see cref="AcrossALane"/>). A car held
    /// at a boundary it cannot cross while it stands in a box or across a lane is backing out of the very
    /// ground it is blocking — and refusing it the one recovery that clears the box is not patience, it is a
    /// car that stands there until the ladder abandons it in the road.
    /// </para>
    /// </remarks>
    public bool SomethingToBackAwayFrom =>
        (NobodyEntitledIsInTheWay && Context.HeadwayM <= Build.LengthM * 2f)
        || (Hold is DrivingHold.Waiting && AcrossALane)
        || Hold is DrivingHold.LostLine
        || OnATemplate;

    /// <summary>
    /// Whether waiting is the right answer for a car that is <b>stuck</b> — the ladder's first rung, which
    /// hands the car a place to hold at rather than a recovery (`TER-5e`: the ground is somebody else's).
    /// </summary>
    /// <remarks>
    /// <b>It is wider than <see cref="NobodyEntitledIsInTheWay"/>, and deliberately.</b> The rungs under
    /// this one are a back-off and then giving the journey up, so standing still a moment longer for
    /// anything that is <em>going somewhere</em> beats both, whether or not that thing was entitled to be in
    /// the way. What may be got <em>past</em> is <see cref="WorthGoingRound"/>, which reads the narrower
    /// question — and a body reeling down the middle of a carriageway is exactly the case where the two
    /// answer differently.
    /// </remarks>
    public bool ObstructionHasPriority =>
        !NobodyEntitledIsInTheWay || Context.HeadwaySpeedMps > Config.Driving.StopSpeedMps;

    /// <summary>Whether what is in front of this car has no claim on the ground it is standing on.</summary>
    /// <remarks>
    /// <b>It is asked what it is and never how fast it is going.</b> A driver on the same road going the
    /// same way is entitled to be there however long he has stood — he is held by something, and what holds
    /// him is not this car's to drive round — and so is a body the index cannot name at all.
    /// <b>Somebody in the lane is not</b> (`PER-1`): a walker is an agent like any other, and one on a
    /// carriageway with nothing painted under it is in the way of a road it was never entitled to. Paint is
    /// where a walker's priority lives, and the grant a car is given already ends short of a crossing
    /// somebody is on (TER-4c.1) long before this question is reached.
    /// </remarks>
    public bool NobodyEntitledIsInTheWay =>
        float.IsPositiveInfinity(LightAheadM)
        && Hold != DrivingHold.Waiting
        && Context.Ahead is not (HeadwayKind.Queue or HeadwayKind.Claimed or HeadwayKind.Unknown);

    /// <summary>
    /// Whether what is in front is something to get past rather than to wait behind: <b>a wreck, a car
    /// nobody is in, a body shoved off its own line, somebody standing in the lane</b>. It is the lane
    /// index's answer and never a guess from a speed — a car that has stood still for a minute at an unlit
    /// junction is a queue, and a car with nobody in it is not one however recently it stopped.
    /// </summary>
    /// <remarks>
    /// <b>A walker is one of them, and the ground under it is what keeps it safe rather than a rule that
    /// refuses to look.</b> `E-4` lays its swerve and asks the book whose the ground under every point of it
    /// is; a body on the carriageway is a stretch of that book with a margin round it
    /// (<c>Person.RoadClaimMargin</c>), so a swerve that would come near one is refused by the same
    /// arithmetic that refuses one over a wreck — and naming a second rule that refused the same movement
    /// would make the first useless (SIM-7).
    /// </remarks>
    public bool InTheWayIsAnObstruction => Context.Ahead is HeadwayKind.Obstruction or HeadwayKind.Walker;

    /// <summary>
    /// <b>Whether getting past what is in front is worth the other side of the road</b> — `E-4`'s whole
    /// question, and `P-4`'s reason for handing it over. Nothing here lays or checks any geometry: whether
    /// the swerve <em>fits</em> is the desk's, and this is only whether it is wanted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the whole question and there is no attempt budget behind it.</b> `P-4` names `E-4` off this
    /// and `E-4` enters on it, so a car cannot be handed over to something that then refuses it — which is a
    /// pair of entries passing a car to and fro in one spot for as long as the obstruction lasts. A count of
    /// swerves left was exactly that: a car that has spent them stands at the next obstruction until it
    /// gives the journey up, and a car that cannot move cannot earn any measure of them back either. What
    /// bounds the wrong side of the road is the wait below — every swerve costs the obstruction wait — and
    /// the ground the shape is laid over, which is walked before the car commits to it.
    /// </para>
    /// <para>
    /// <b>The two doors are the two things that can be in the way, and both are waited out first.</b>
    /// Something in the road may be about to be gone — a body crossing a carriageway is out of the lane in
    /// a second, and swinging past it costs the wrong side of the road to save nothing — so what earns a
    /// swerve is the obstruction wait, and the same wait on both doors. What differs is which clock can
    /// answer it: a car behind something stopped stands still and spends <see cref="BlockedS"/>, and a car
    /// behind something slow never stops at all, so it spends <see cref="HeldBackS"/> instead.
    /// </para>
    /// <para>
    /// <b>How much slower is enough is one figure and the rest is geometry.</b> A car barely faster than
    /// what it is behind needs a pass straight of hundreds of metres to gain a car length, and the desk
    /// refuses that for the ground it would take — so there is no second rule here about closing speed, and
    /// naming one would make the first useless (SIM-7).
    /// </para>
    /// <para>
    /// <b>And never at a junction</b> (<see cref="OnACarriageway"/>) <b>nor at a crossing</b>
    /// (<see cref="ClearOfThePaint"/>), whoever is asking — the two conditions outside the doors, because
    /// both are about the road rather than about what is on it.
    /// </para>
    /// </remarks>
    public bool WorthGoingRound =>
        OnACarriageway
        && ClearOfThePaint
        && (Urgent
            ? WorthGettingPastOnACall
            : InTheWayIsAnObstruction
              && NobodyEntitledIsInTheWay
              && (Context.HeadwaySpeedMps <= Config.Driving.StopSpeedMps
                  ? AtRest && BlockedS >= Config.Ladder.ObstructionWaitS
                  : HeldBackS >= Config.Ladder.ObstructionWaitS && HeldBackBySomethingSlow));

    /// <summary>
    /// <b>Whether there is no crossing between this car and where it could stop</b> — the paint's own half
    /// of "`E-4` is a manoeuvre of open road", read off the same reach the pace and the stop are.
    /// </summary>
    /// <remarks>
    /// <b>Nothing else refuses this one.</b> A swerve is walked over the ground before the car commits to
    /// it, which is what keeps it off a body it can see; but a walker on a crossing lays the band of the
    /// lane it is <em>standing in</em> (TER-4c.1), so a body two lanes over leaves the shape a clear run and
    /// the ground test says yes. What the driver would then be doing is overtaking a queue that is waiting
    /// for a zebra and crossing paint the people on it are about to reach — so the refusal is here, once,
    /// and it is the first gate rather than a second one (SIM-7). Being on the paint is included: the pace
    /// is owed until the tail is off it (CAR-7b), and a car half over a crossing has nowhere to swerve to.
    /// </remarks>
    public bool ClearOfThePaint => float.IsPositiveInfinity(Context.CrossingAtM);

    /// <summary>
    /// <b>Whether this car is out on a carriageway rather than at a junction</b>: not standing in a box, and
    /// not yet near enough to the next one to be negotiating it. <b>`E-4` is a manoeuvre of a road segment
    /// and of nothing else.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A junction has no centreline to cross and no lane to give back.</b> What licenses a swerve at all
    /// is CAR-6.2b, which licenses crossing the <em>centreline</em>; inside a box there is none, and the
    /// ground on the other side of the car is not an oncoming lane but the other movements through the box,
    /// each of them arbitrated on the assumption that a car crossing follows the join it claimed
    /// (<see cref="World.Road.WayCrossings"/>). A car that swings off its join is not where the town says it
    /// will be, and the pair of movements that read each other's ground read the wrong ground.
    /// </para>
    /// <para>
    /// <b>And it is the one place the claim behind a swerve cannot be laid.</b>
    /// <c>ManeuverDesk.ClaimTheSwerve</c> claims the stretch of the car's own <em>lane</em> the shape leaves
    /// and returns to; inside a box the car is on no lane, so the claim is silently not made and the traffic
    /// behind reads the ground the manoeuvre is swinging through as empty road.
    /// </para>
    /// <para>
    /// <b>The bar is the same one `P-4` hands the junction over at</b> (<see cref="SimConfig.CarJunctionReserveM"/>),
    /// and deliberately: a car near enough to have asked for the box is a car negotiating the box, and the
    /// two are alternatives rather than things to be done at once. A second figure here would be a second
    /// answer to "is this car at a junction yet".
    /// </para>
    /// </remarks>
    public bool OnACarriageway => !InsideTheBox && ToTheBoxM > Config.CarJunctionReserveM;

    /// <summary>
    /// <b>The same question asked by a driver with a blue light on</b> (AMB-4): a queue counts, and there
    /// is no patience to be spent first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both halves are what "can overtake traffic if needed" means.</b> Ordinary traffic waits behind a
    /// queue however long it stands, because the car at its head is held by something that is not this
    /// driver's to drive round; an ambulance is exactly the case where that reasoning stops holding, since
    /// what the queue is waiting for is a light or a turn this driver is not waiting for. And the
    /// obstruction wait is a driver deciding whether the wrong side of the road is worth a few seconds,
    /// which is a question a rescue has already answered.
    /// </para>
    /// <para>
    /// <b>What it does not relax is the two things that keep a swerve safe.</b> Something the book cannot
    /// name is still never driven round — a priority is a rule about who waits, not a licence to pass what
    /// nobody can see — and the shape itself is still walked over the ground and the book before the car
    /// commits to it (`E-4`), which is where a swerve into an occupied lane is actually refused.
    /// </para>
    /// <para>
    /// <b>And it is still worth going round only what is slower</b>, on the same share as everybody else:
    /// a pass that gains nothing costs the oncoming lane for nothing, blue light or no.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <b>And not on the last dozen metres of a leg</b> (<see cref="OnTheFinalApproach"/>). Past the point
    /// the line leaves the road for a bay there is nothing left to gain by getting in front of anybody, and
    /// a driver with no patience to spend swerves round the parked cars beside its own bay for as long as
    /// they are there — which is a rescue that arrives at the hospital and never stops at it.
    /// </remarks>
    public bool WorthGettingPastOnACall =>
        !OnTheFinalApproach
        && Context.Ahead is not (HeadwayKind.Nothing or HeadwayKind.Unknown)
        && Context.HeadwaySpeedMps < PlannedMps * Config.Driving.PassWorthShare;

    /// <summary>
    /// <b>Held below what this road affords by something this car is gaining on</b> — the state
    /// <see cref="HeldBackS"/> is the clock for, and the arithmetic half of the moving door above.
    /// </summary>
    public bool HeldBackBySomethingSlow =>
        Hold is DrivingHold.Headway or DrivingHold.Reserved
        && Context.HeadwaySpeedMps < PlannedMps * Config.Driving.PassWorthShare
        && AlongMps > Context.HeadwaySpeedMps;
}
