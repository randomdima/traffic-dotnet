namespace TrafficSimulation.Core.Config;

/// <summary>The nominal car's body: what it is, and what it can ask of itself before the tyres answer.</summary>
internal sealed class CarFigures
{
    public float LengthM { get; init; } = 4.0f;
    public float WidthM { get; init; } = 2.0f;
    public float MassKg { get; init; } = 1400f;
    public float MaxSpeedMps { get; init; } = 75f;

    /// <summary>Deliberately off the forward cap's scale: it is only ever used for the parking templates.</summary>
    public float ReverseMaxMps { get; init; } = 8f;

    public float AccelerationMps2 { get; init; } = 11.7f;
    public float BrakingMps2 { get; init; } = 27f;
    public float CgHeightM { get; init; } = 0.55f;
    public float MaxSteeringDeg { get; init; } = 35.42f;

    /// <summary>The nominal figure the junctions are sized against, and nobody's actual wheelbase.</summary>
    public float WheelbaseM { get; init; } = 2.8f;

    /// <summary>1 is front-wheel drive, 0 rear, ½ all four. A fleet that varies its layouts varies this.</summary>
    public float DrivenFrontShare { get; init; } = 1f;

    /// <summary>
    /// Whether a car with nobody in it stands on its handbrake, so one shoved by a collision drags
    /// rather than rolling away. Off is a debug switch for looking at the rolling model: nothing then
    /// keeps the town's parked cars where the plan put them.
    /// </summary>
    public bool ParkedHandbrake { get; init; } = true;

    public float ReverseBoundM { get; init; } = 8f;
    public float ReverseTemplateArcMargin { get; init; } = 1.1f;
}

/// <summary>The contact patch: what a wheel can put down, how the load moves onto it, and what it draws.</summary>
internal sealed class TyreFigures
{
    /// <summary>What decides how hard a car can corner, stop and skid before the rubber lets go.</summary>
    public float GripMps2 { get; init; } = 11.4f;

    /// <summary>
    /// What the patch puts down along the roll against what it puts down across it — <b>and so the one
    /// figure that moves a car's stopping distance without moving the speed it takes a corner at</b>.
    /// Braking, driving traction and the load that moves under either are all this same budget, and the
    /// pedal's own cap (<see cref="CarFigures.BrakingMps2"/>) stands well above it: what stops a car is
    /// the rubber and never the brakes.
    /// </summary>
    public float LongAxisFactor { get; init; } = 1.643f;

    /// <summary>
    /// A town seen from above is given no gravity, but a tyre's load is a weight all the same: this
    /// turns the centre-of-gravity height into the share of the load that moves under braking and
    /// cornering. The physical constant, not a figure anybody chose.
    /// </summary>
    public float StandardGravityMps2 { get; init; } = 9.81f;

    /// <summary>One tyre as it is drawn and as it marks the ground, along its roll and across it.</summary>
    public float WheelLengthM { get; init; } = 0.62f;

    public float WheelWidthM { get; init; } = 0.22f;

    /// <summary>
    /// One pitch of tread, which is the period the drawn tread is wrapped into. <b>It is the shipped
    /// picture's own period</b> — the sheet is one pitch laid across the full width of the tyre, so its
    /// aspect carries this figure and a test holds the two to each other. Wrapped into anything else the
    /// pattern snaps back part of a block several times a revolution.
    /// </summary>
    public float TreadPitchM { get; init; } = 0.0928f;

    /// <summary>
    /// How fast the tread pattern scrolls against how fast the wheel is turning, and which way. <b>A
    /// display figure and nothing else.</b> It has to be well under one because the pattern is far finer
    /// than a frame can sample: at town speeds a tyre passes most of a block per frame, which is past
    /// Nyquist, and the eye matches a block advancing 0.9 of a pitch to the next one and sees it crawl
    /// backwards.
    /// </summary>
    public float TreadScrollFactor { get; init; } = -0.225f;

    /// <summary>
    /// One wheel's rotating inertia as the straight-line mass it behaves like (J/r²). It sets how
    /// violently a wheel spins up or locks — against a corner carrying ≈ 350 kg, an engine asking for
    /// more than the patch can transmit lights the tyre up over a fraction of a second.
    /// </summary>
    public float WheelRotatingMassKg { get; init; } = 25f;

    /// <summary>
    /// How far the tread may outrun the road at a standing start, and the speed by which the gearing has
    /// run out of revs to spin it faster. Together they are the gearbox this model does not otherwise
    /// have: a launch lights the tyres up, a car already moving cannot, whatever the pedal says.
    /// </summary>
    public float WheelSpinAllowanceMps { get; init; } = 4f;

    public float WheelSpinFadeMps { get; init; } = 12f;

    /// <summary>
    /// Ceiling on the acceleration the load transfer is read from, in multiples of grip: the tyres
    /// cannot push the body harder than this, so anything beyond it is a collision — and a collision
    /// must not unload all four wheels for a tick.
    /// </summary>
    public float LoadTransferInGrips { get; init; } = 2f;

    /// <summary>
    /// Least a corner may be left carrying. At zero a wheel is not merely light: its whole budget is
    /// zero and it contributes nothing until the load comes back, which is a car that spins after a nudge.
    /// </summary>
    public float MinCornerLoadFraction { get; init; } = 0.05f;

    /// <summary>Long enough that one noisy tick cannot flick the grip about.</summary>
    public float LoadSettleS { get; init; } = 0.08f;

    /// <summary>
    /// Share of a driven tyre's grip along the roll that a car driving <em>itself</em> will ask for, and
    /// how fast that ask is given up and taken back when the tyres report a slide. Past the patch,
    /// throttle buys no acceleration and only lays rubber. A hand at the wheel gets none of this.
    /// </summary>
    public float TractionThrottleFraction { get; init; } = 0.95f;

    public float SlipBackOffS { get; init; } = 0.15f;
    public float SlipRecoverS { get; init; } = 0.6f;

    /// <summary>Never so little that a car needing power to get anywhere is stranded by its own traction control.</summary>
    public float MinSlipThrottleFraction { get; init; } = 0.2f;
}

/// <summary>How a car drives itself: where it aims, what it plans against, and what it gives way to.</summary>
internal sealed class DrivingFigures
{
    /// <summary>How far ahead on its own line the wheel is aimed, as a time. Shorter saws; longer cuts the corner.</summary>
    public float LookaheadS { get; init; } = 0.6f;

    /// <summary>The floor under that lead: a point nearer than the car's own nose is not something to steer at.</summary>
    public float LookaheadFloorInCarLengths { get; init; } = 1f;

    public float LookaheadCeilingInCarLengths { get; init; } = 5f;

    /// <summary>How much of what the tyres can put down the speed profile plans against, leaving the rest for what the plan did not see.</summary>
    public float GripMargin { get; init; } = 0.7f;

    /// <summary>
    /// The same share for braking, which is <b>the one thing a driver aims a whole manoeuvre at</b> and
    /// therefore the one it may spend nearly all of. A corner is held for as long as it lasts and the
    /// margin there is what covers a bump, a camber and the wheel still being turned; a stop is planned,
    /// straight and over in a few seconds, and what is left over is the rolling resistance, which the
    /// tyres spend outside their own budget and hand back as a stop shorter than the plan.
    /// </summary>
    /// <remarks>
    /// It is what sizes every reservation on the road, because the ground a car holds is the ground it
    /// plans to stop in. At <see cref="GripMargin"/> a car doing 150 km/h held 72 m of empty street for a
    /// stop it would have made in 50.
    /// </remarks>
    public float BrakingMargin { get; init; } = 0.95f;

    /// <summary>Below this a car counts as stopped — what an obstruction wait is spent under, and what makes a queue a queue.</summary>
    public float StopSpeedMps { get; init; } = 0.2f;

    /// <summary>
    /// The clear ground a driver keeps between where it will have stopped and whatever it is stopping
    /// behind, before the tail's share of it is taken (<see cref="TailMarginShare"/>). It is the whole of
    /// the following distance at rest.
    /// </summary>
    /// <remarks>
    /// <b>It sets the gap and never lowers the margin.</b> The ground a body keeps around itself is one
    /// figure (<see cref="SimConfig.CarBodyMarginM"/>) and it answers a measured question as well as this
    /// one: below a body's width the soak wrecks cars in junctions, so a fleet asked to queue closer than
    /// that queues at the floor instead.
    /// </remarks>
    public float StandstillGapInCarLengths { get; init; } = 0.5f;

    /// <summary>
    /// What share of <see cref="SimConfig.CarBodyMarginM"/> a body's reservation keeps <em>behind</em> its
    /// tail. The margin in front is the whole of it; the tail carries this much of it.
    /// </summary>
    /// <remarks>
    /// <b>It is the one place the two ends of the margin are not the same figure</b>, and what it buys is the
    /// road behind a car — a stretch begins here, so every metre of it is a metre the traffic behind is
    /// queued out of. What it spends is the cover the book's one-dimensional reading owes at the end that
    /// swings widest, and the soak is what prices it: at 1 Odesa runs a measured minute with nothing
    /// wrecked, and anything under a body's width at the tail wrecks cars (`--bench soak`).
    /// </remarks>
    public float TailMarginShare { get; init; } = 0.6f;

    /// <summary>
    /// <b>The time a driver keeps between itself and whatever cut its grant short</b>, which with
    /// <see cref="SimConfig.CarTailMarginM"/> is the whole of the following distance: the road a car is
    /// granted inverts to the speed it may hold, and this is the lead that inversion is read at. The gap it
    /// settles a queue at is <c>tail margin + v·this</c> whatever the braking figure is — the grip cancels,
    /// because the car in front was credited with its own stopping distance out of the same arithmetic.
    /// </summary>
    /// <remarks>
    /// <b>It is a following time and not a reaction time, and the difference is the whole of fluency.</b>
    /// Read at <see cref="SimConfig.CarReactionS"/> the equilibrium gap is a tenth of a second of travel —
    /// arithmetically safe against a leader braking at the same rate and with nothing whatever left over,
    /// so any leader that brakes harder than the follower planned for propagates as a stop the length of
    /// the queue. A second of travel is what a queue that has to absorb one keeps.
    /// </remarks>
    public float FollowingHeadwayS { get; init; } = 1f;

    /// <summary>
    /// How long a pedal takes to travel from one stop to the other, which is what bounds the rate the
    /// commanded acceleration may change at. <b>Without it the pedal is a relay</b>: the profile asks for
    /// whatever closes the speed error in one tick, so an error of a fifth of a metre a second saturates it,
    /// and a car holding a speed snaps between full throttle and full brake several times a second.
    /// </summary>
    /// <remarks>
    /// <b>What it costs is a shade of the stop and never its accuracy</b> — the demand still arrives, a
    /// pedal-travel later — so it is well inside the reaction lead every distance is already measured
    /// through. `E-2` is not held to it: an emergency is the one place the pedal is allowed to snap.
    /// </remarks>
    public float PedalTravelS { get; init; } = 0.3f;

    /// <summary>
    /// <b>How much slower than its own pace something has to be going before a driver crosses the
    /// centreline to get past it</b> (`E-4`): the share of what this car would be doing with the road to
    /// itself. Above it, following is the cheaper answer and the wrong side of the road buys a few seconds.
    /// </summary>
    /// <remarks>
    /// It is read against the profile's own <c>plannedMps</c> and not against a stated speed, so what
    /// counts as worth passing is the road's answer where the car is: half of what a 40 m/s straight
    /// affords is a different thing from half of what a hairpin does, and a walker in a hairpin is not
    /// holding anybody up.
    /// </remarks>
    public float PassWorthShare { get; init; } = 0.6f;

    /// <summary>How far either side of its last progress a car looks for itself on its own line.</summary>
    public float ProjectionWindowInCarLengths { get; init; } = 2f;

    /// <summary>The cap on how early the junction ahead is reserved, over and above being within stopping distance.</summary>
    public float JunctionReserveInCarLengths { get; init; } = 3f;

    /// <summary>
    /// The pace a crossing is approached at — 8 m/s at the shipped figures, against a town whose cars run
    /// at ten to twenty. Fast enough that a green is not spent crawling, slow enough that the stop for
    /// somebody stepping off a kerb is a stop and not a skid. Lifted where the crossing is lit and red.
    /// </summary>
    public float CrossingPaceInCarLengthsPerS { get; init; } = 2f;

    /// <summary>
    /// How far short of a crossing's paint a yielding car comes to rest. Leaving the crossing clear is
    /// the whole of the figure: stopping on the near edge is stopping where somebody has to walk round
    /// the bonnet.
    /// </summary>
    public float CrossingStandOffInCarWidths { get; init; } = 0.5f;

    /// <summary>What the route prices are quoted in, and not any actual car's length.</summary>
    public float NominalCarLengthM { get; init; } = 4.5f;

    /// <summary>A preference between routes, not a time.</summary>
    public float TurnPriceNearSideCarLengths { get; init; } = 0.5f;

    public float TurnPriceAcrossOncomingCarLengths { get; init; } = 4f;
    public float TurnPriceTurnAroundCarLengths { get; init; } = 20f;
}

/// <summary>The escalation ladder: how long a car tolerates being stopped, and what it does about it.</summary>
internal sealed class LadderFigures
{
    public float GiveWayPatienceLeavingBayS { get; init; } = 20f;
    public float ObstructionWaitS { get; init; } = 3f;

    /// <summary>
    /// How long a reflex keeps its name after the thing that fired it has gone, imposing nothing while it
    /// does. It is what tells one emergency stop from the twenty triggerings a single one is made of in
    /// stop-start traffic, and it is a second — well past the settling the arbitration itself asks for
    /// and well short of anything a car spends holding at something real.
    /// </summary>
    public float ReflexHoldS { get; init; } = 1f;
    public int BackOffAttemptsPerJam { get; init; } = 2;

    /// <summary>
    /// The blocked-road clock, in light cycles: how long a car stands still with no lawful cause before
    /// the ladder is walked. It must sit well above a full red phase, or every busy junction gets routed
    /// around. Both phases share the cycle, so a red is half of one and this is four.
    /// </summary>
    public float BlockedRoadInLightCycles { get; init; } = 2f;

    /// <summary>
    /// The short fuse, in obstruction waits: what a car <em>standing across a lane</em> is measured on
    /// instead, because it is itself the obstruction and patience is the wrong answer.
    /// </summary>
    public float ShortFuseInObstructionWaits { get; init; } = 2f;

    /// <summary>How far a car must actually cover before the ladder rewinds. <b>Road covered, never manoeuvres completed.</b></summary>
    public float RewindInCarLengths { get; init; } = 2f;

    /// <summary>How many times one leg may reroute before the road stops being the thing that is wrong with it.</summary>
    public int ReroutesPerLeg { get; init; } = 3;

    /// <summary>
    /// What a blocked stretch is priced at, and how long the mark lives in blocked clocks. Expensive,
    /// never impassable: in a town this small the only road to a place may be the marked one, and a mark
    /// expires so a road nobody has driven since is tried again.
    /// </summary>
    public float BlockedWayPriceInBlockSpacings { get; init; } = 4f;

    public float BlockedWayLifeInBlockedClocks { get; init; } = 2f;
}

/// <summary>A walker: its pace, its footing, and how it keeps out of the way.</summary>
internal sealed class PersonFigures
{
    /// <summary>A run, roughly five times a real walk: the town is watched at speed.</summary>
    public float WalkSpeedMps { get; init; } = 6.6f;

    /// <summary>
    /// <b>Scaled with the pace, because a body moving five times a real walk turns five times a real
    /// turn</b> — a person's quick pivot is about 270°/s, and this is five of them. It is what lets a
    /// walker turn nearly on the spot, and so what decides how much ground the pavement has to give up at
    /// every corner to be a line the feet can hold (<see cref="SimConfig.WalkerTightestTurnM"/>).
    /// </summary>
    public float TurnRateDegPerS { get; init; } = 1350f;
    public float MassKg { get; init; } = 80f;

    /// <summary>
    /// The relation, not the number: whatever the walk speed is, the grip is what keeps a start and a
    /// stop inside a fifth of the body's own diameter.
    /// </summary>
    public float FootGripMps2 { get; init; } = 110f;

    public float SlidingGripMps2 { get; init; } = 4f;
    public float StumbleWindowS { get; init; } = 0.25f;
    public float RunFactor { get; init; } = 2f;
    public float FleeDistanceM { get; init; } = 8f;

    /// <summary>A quarter of the stretch's band, so each direction has half of it.</summary>
    public float LaneOffsetFraction { get; init; } = 0.25f;

    /// <summary>
    /// The clear ground a walker keeps between where it will have stopped and whatever it is stopping
    /// behind, in bodies. It is the whole of the following distance at rest, and one body is what a queue
    /// on a pavement actually stands at.
    /// </summary>
    public float StandstillGapInDiameters { get; init; } = 1f;

    /// <summary>
    /// How much more of a lane a body on the carriageway claims than it actually covers. <b>A body on a
    /// road is not a body on a pavement</b>: what it is asking for is that nothing arrives where it is
    /// standing, and it is asking it of a driver who is a good deal less able to stop than it is to step.
    /// </summary>
    /// <remarks>
    /// It widens the claim and never the test that decides whether the body is in a lane at all — a walker
    /// on the pavement past a kerb is not on the road, and a margin that put it there would queue a street
    /// behind everybody walking down it.
    /// </remarks>
    public float RoadClaimMargin { get; init; } = 1.15f;

    public float KeepRightHoldM { get; init; } = 0.6f;
    public float SidestepM { get; init; } = 1.2f;
    public float AvoidReachM { get; init; } = 2.5f;
    public float SidestepReachM { get; init; } = 2f;
    public float RedWaitSetbackM { get; init; } = 2f;

    /// <summary>Only traffic spends it.</summary>
    public float KerbPatienceS { get; init; } = 8f;

    /// <summary>
    /// How long one lurch of a body reeling down a carriageway lasts, which with the pace above is how far
    /// it carries — 20 m at the shipped figures. <b>Long enough that a driver behind one is following it
    /// rather than arriving at it</b>, and short enough that where it ends up across the road is not a
    /// decision taken once a minute.
    /// </summary>
    public float LurchS { get; init; } = 3f;

    /// <summary>
    /// How many lurches a body takes, on average, between standing where it is for a beat. <b>It is what
    /// makes a drunk two things to a driver rather than one</b>: something slow to be followed while it is
    /// walking, and something to be got past once it has stopped — and both of those are wanted, so neither
    /// may be so rare that a run never contains it.
    /// </summary>
    public int LurchesPerStand { get; init; } = 4;

    /// <summary>
    /// And how long that stand lasts, drawn up to twice this. <b>It is not
    /// <see cref="StandAboutS"/></b>: a pacer stands until something has come to rest for it and the beat is
    /// only the bound on that, while a body reeling down a road is waiting for nothing at all.
    /// </summary>
    /// <remarks>
    /// <b>It has to be worth more than a driver's obstruction wait</b>
    /// (<see cref="LadderFigures.ObstructionWaitS"/>) or nothing is ever got past, and it has to be worth
    /// well under the blocked-road clock or a road with fifteen of them on it is a road nothing gets down.
    /// </remarks>
    public float LurchStandS { get; init; } = 6f;

    /// <summary>
    /// The brief random idle before drawing a destination, so a town's worth of people do not all set off
    /// on the same tick. A stagger and not a wait: longer than the decision interval, far shorter than a dwell.
    /// </summary>
    public float StandByIdleMaxS { get; init; } = 1f;

    /// <summary>
    /// How long a walker with nowhere to be will stand out in the lane waiting to be stopped for — this much
    /// at least, and up to twice it. <b>It bounds the stand and is never the reason one ends</b>: a body
    /// steps back onto the pavement the moment something has come to rest for it, and steps out again as
    /// soon as the road is clear, so on a road anything drives this figure is never reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it is really for is the road nothing comes down</b>, which would otherwise leave a body
    /// standing in a lane for good. It has to sit under the blocked-road fuse
    /// (<see cref="LadderFigures.BlockedRoadInLightCycles"/>), or a car that arrived at the start of a stand
    /// would walk the ladder around a body that was about to move.
    /// </para>
    /// <para>
    /// <b>Each stand is drawn afresh from the walker's own stream</b>, and that is the load-bearing half. A
    /// lap settles into a period, so a beat any small number of laps is a multiple of meets the same walkers
    /// at the same point of their pacing for ever — with the beat held fixed, two of the five shapes on the
    /// proving ground were blocked on almost every pass and two on almost none.
    /// </para>
    /// </remarks>
    public float StandAboutS { get; init; } = 12f;

    /// <summary>
    /// How far a person will walk when the trip is theirs to choose, in block spacings: one block, so a
    /// destination inside the block the walker is standing in is walked to and anything past it is worth a car.
    /// </summary>
    /// <remarks>
    /// One figure rather than two, deliberately: it also caps the walk a trip <em>hands</em> somebody —
    /// the bay a drive aims at is looked for within it of the destination, a car further off is not this
    /// trip's car, and a leg that would end further away drops the destination instead.
    /// </remarks>
    public float WalkWorthInBlockSpacings { get; init; } = 1f;

    public float DiameterInPropDiameters { get; init; } = 0.5f;
    public float ExitSearchRadiusInPropDiameters { get; init; } = 1f;
}

/// <summary>What a contact has to spend to end a unit. Damage is binary; a person alone has a survivable band.</summary>
internal sealed class DamageFigures
{
    public float PersonShakeKj { get; init; } = 2f;
    public float PersonFatalKj { get; init; } = 6f;
    public float CarWreckKj { get; init; } = 20f;
}
