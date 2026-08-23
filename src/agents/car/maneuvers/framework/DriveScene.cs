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
/// <b>Two fields are conditional and say so</b>: <see cref="GapIsClear"/> takes a claim on the lane and is
/// only asked while a car is waiting in the mouth of its bay, and <see cref="RouteReversesHere"/> is only
/// meaningful on a route. Both read <c>true</c> and <c>false</c> respectively where they were not asked,
/// which is the answer that makes the entry reading them do nothing.
/// </para>
/// </remarks>
internal readonly record struct DriveScene
{
    /// <summary>Every figure the catalogue reads. <b>A literal in an entry's procedure is a defect.</b></summary>
    public required SimConfig Config { get; init; }

    public required int Car { get; init; }

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

    /// <summary>The bay this leg has reserved, or −1.</summary>
    public required int BayReserved { get; init; }

    /// <summary>Whether the line's last lane is the one the reserved bay is entered from, with the route run out on it.</summary>
    public required bool OnTheFinalApproach { get; init; }

    /// <summary>The lane the car is on, or −1 where it is on none.</summary>
    public required int LaneOn { get; init; }

    /// <summary>Whether the route's next lane is the reverse of the one under the car — `P-11`'s whole <c>Sa</c>.</summary>
    public required bool RouteReversesHere { get; init; }

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

    /// <summary>How long it has been waiting for a gap. It starts below zero: the beat `P-2` takes before looking at all.</summary>
    public required float WaitedS { get; init; }

    /// <summary>
    /// Whether the lane behind the bay is clear for long enough to back onto — asked as a <b>time</b> and
    /// never a distance (§8 rule 8). Only asked while a car is in the mouth of its own bay; <c>true</c>
    /// everywhere else, which is the answer that makes it do nothing.
    /// </summary>
    public required bool GapIsClear { get; init; }

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
    /// The line has been driven to its end and the car has stopped there — the end condition every
    /// template shares, and the one place a manoeuvre driving geometry of its own is done.
    /// </summary>
    public bool LineIsSpent => ToTheEndM <= Config.Driving.StopSpeedMps && AtRest;

    /// <summary>
    /// Whether the body is in somebody else's way where it stands: inside a junction box, or committed to
    /// a template laid across the lane behind it. <b>It is what decides the fuse the car is watched on</b>
    /// — a car in that position is itself the obstruction, and patience is the wrong answer.
    /// </summary>
    public bool AcrossALane =>
        InsideTheBox || (OnATemplate && ProgressM > Config.Car.LengthM * 0.5f);

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
        (NobodyEntitledIsInTheWay && Context.HeadwayM <= Config.Car.LengthM * 2f)
        || (Hold is DrivingHold.Waiting && AcrossALane)
        || Hold is DrivingHold.LostLine
        || OnATemplate;

    /// <summary>
    /// Whether waiting is the right answer for a car that is <b>stuck</b> — `E-1`'s whole scenario, and the
    /// ladder's first rung.
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
    /// <b>Somebody in the lane is not</b> (`PER-12`): a walker is an agent like any other, and one on a
    /// carriageway with nothing painted under it is in the way of a road it was never entitled to. Paint is
    /// where a walker's priority lives, and a car owes a crossing a stop short of it (`P-12`) long before
    /// this question is reached.
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
    /// </remarks>
    public bool WorthGoingRound =>
        InTheWayIsAnObstruction
        && NobodyEntitledIsInTheWay
        && (Context.HeadwaySpeedMps <= Config.Driving.StopSpeedMps
            ? AtRest && BlockedS >= Config.Ladder.ObstructionWaitS
            : HeldBackS >= Config.Ladder.ObstructionWaitS && HeldBackBySomethingSlow);

    /// <summary>
    /// <b>Held below what this road affords by something this car is gaining on</b> — the state
    /// <see cref="HeldBackS"/> is the clock for, and the arithmetic half of the moving door above.
    /// </summary>
    public bool HeldBackBySomethingSlow =>
        Hold is DrivingHold.Headway or DrivingHold.Reserved
        && Context.HeadwaySpeedMps < PlannedMps * Config.Driving.PassWorthShare
        && AlongMps > Context.HeadwaySpeedMps;
}
