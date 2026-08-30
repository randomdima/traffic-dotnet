namespace TrafficSimulation.Core.Config;

/// <summary>The nominal car's body: what it is, and what it can ask of itself before the tyres answer.</summary>
/// <remarks>
/// <b>Nobody drives this car.</b> It is what the town is sized against — lane widths, junction radii, bays
/// and the ways laid into them (CAR-11a) — and the figure every variant is resolved against: a variant
/// states its own dimensions and states what it is worth against these, and
/// <see cref="Agents.Car.Body.CarBuild"/> is the car that actually turns up (CAR-11).
/// </remarks>
internal sealed class CarFigures
{
    public float LengthM { get; init; } = 4.0f;
    public float WidthM { get; init; } = 2.0f;
    public float MassKg { get; init; } = 1400f;
    /// <summary>
    /// <b>The cap a car is governed at, and not a top speed it would reach.</b> 144 km/h: a road vehicle's
    /// limiter, which a variant scales to its own. It is an authored primitive because there is nothing here
    /// for it to be derived from — resistance in this model is the rolling kind, proportional to the load and
    /// flat in the speed (<see cref="SimConfig.PavedDragMps2"/>), so thrust exceeds drag at every speed and a
    /// terminal velocity does not exist. Deriving one needs an aerodynamic term this build does not have.
    /// </summary>
    /// <remarks>
    /// <b>It is load-bearing and not decoration.</b> A car's sight distance is its own stopping distance from
    /// this figure (<see cref="Agents.Car.Body.CarBuild.SightM"/>), which is also the ceiling on any
    /// manoeuvre's geometry, so a cap set where no road vehicle goes buys every car a lookahead measured in
    /// blocks and a manoeuvre allowed to reach across the town.
    /// </remarks>
    public float MaxSpeedMps { get; init; } = 40f;

    /// <summary>
    /// Deliberately off the forward cap's scale: it is what a car may be driven backwards at, by a hand at
    /// the wheel or by a driver of its own, and no variant states one of its own.
    /// </summary>
    public float ReverseMaxMps { get; init; } = 8f;

    /// <summary>
    /// <b>How far past its own driven tyres a car's engine reaches</b>, in multiples of what that axle can
    /// put down at the static load (<see cref="Agents.Car.Body.CarBuild.DrivenTractionMps2"/>, CAR-45). One,
    /// which is to say the nominal car's whole pedal is exactly the demand its rubber answers, and a variant
    /// that wants to light its wheels up asks for more than one of them.
    /// </summary>
    /// <remarks>
    /// Authored as a headroom rather than as an acceleration, for the reason
    /// <see cref="BrakePedalInTyreGrips"/> is: a figure in m/s² stays where it was put when the grip beneath
    /// it moves, and a pedal three times past what any tyre can take is a car whose engine figure describes
    /// nothing that ever happens to it — every metre per second it gains is the rubber's.
    /// </remarks>
    public float DrivePedalInDrivenGrips { get; init; } = 1f;

    /// <summary>
    /// <b>How far past its own tyres a car's brakes reach</b>, in multiples of what the rubber holds along
    /// the roll (<see cref="SimConfig.CarBrakingMps2"/>). Three, which is to say the pedal can lock a wheel
    /// at any load a body can put on one — that is what brakes are for, and it is why <b>what stops a car
    /// is the rubber and never the pedal</b>.
    /// </summary>
    /// <remarks>
    /// Authored as a headroom rather than as a deceleration so that it <em>tracks</em>: a figure in m/s²
    /// stays where it was put when the grip beneath it moves, and a pedal that has quietly stopped
    /// out-reaching the tyres is a car braked by its brakes.
    /// </remarks>
    public float BrakePedalInTyreGrips { get; init; } = 3f;

    public float CgHeightM { get; init; } = 0.55f;

    /// <summary>
    /// <b>How much of the nominal car stands on its front axle at rest.</b> An even split, because where
    /// the mass sits along a wheelbase is a fact about <em>a body</em> and this body is the one nobody
    /// drives — half is the only figure that is wrong for every car by the same amount rather than wrong
    /// for most of them by a guess.
    /// </summary>
    /// <remarks>
    /// It is <b>the balance</b>, and everything a car does under power or into a corner is downstream of
    /// it: the driven axle can put down what it is carrying and no more, and the lighter end lets go
    /// first. <b>It belongs to the car and not to the tyre</b>, and it is a variant's to state
    /// (<c>frontWeightShare</c>) — no dial moves the fleet's, because a distribution is not one figure
    /// nineteen bodies share.
    /// </remarks>
    public float StaticFrontShare { get; init; } = 0.5f;

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

    /// <summary>
    /// How much wider than the car's own turning circle a manoeuvring template is drawn, so the steering is
    /// not sitting on its stop for the whole of an arc a car has to hold in both gears.
    /// </summary>
    public float ParkingTemplateArcMargin { get; init; } = 1.1f;
}

/// <summary>The contact patch: what a wheel can put down, how the load moves onto it, and what it draws.</summary>
internal sealed class TyreFigures
{
    /// <summary>
    /// <b>The coefficient of friction between this town's rubber and its tarmac.</b> Dimensionless, and the
    /// raw term: what a car may pull in m/s² is this times gravity (<see cref="SimConfig.TyreGripMps2"/>)
    /// and is derived from it, never the other way about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A road tyre on dry asphalt. Every variant states its own against it (<c>tyreFriction</c>).
    /// </para>
    /// <para>
    /// <b>One coefficient — at any load, and whichever way the car is pointing.</b> Coulomb, so a patch is
    /// worth what it is carrying and a transfer costs the car nothing overall. Both of the refinements that
    /// would change that are real and both are worth about a per cent here: a carcass peaks a few per cent
    /// higher along the roll than across it, and μ falls as the patch is pressed harder. A town watched from
    /// above shows neither, and each of them is somewhere a fudge can be parked and called physics.
    /// </para>
    /// </remarks>
    public float Friction { get; init; } = 0.968f;

    /// <summary>
    /// A town seen from above is given no gravity, but a tyre's load is a weight all the same: this
    /// turns the centre-of-gravity height into the share of the load that moves under braking and
    /// cornering. The physical constant, not a figure anybody chose.
    /// </summary>
    public float StandardGravityMps2 { get; init; } = 9.81f;

    /// <summary>
    /// The nominal tyre as it is drawn and as it marks the ground, along its roll and across it. <b>Every
    /// variant states its own against it</b> (<c>wheelM</c>), because a tyre is bolted to a car.
    /// </summary>
    public float WheelLengthM { get; init; } = 0.62f;

    public float WheelWidthM { get; init; } = 0.22f;

    /// <summary>
    /// How much of a tyre's width has to stand outside the bodywork drawn over it (CAR-12), which is what
    /// every variant's track is authored against. <b>Less than this and the wheel is not there</b>: the art
    /// is a car seen from above, its panels reach the edge of its sheet, and a tyre tucked under them is
    /// four impulses acting on a body nothing on screen accounts for.
    /// </summary>
    public float ShowsPastTheBodyworkShare { get; init; } = 0.4f;

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
    /// The nominal wheel's rotating inertia as the straight-line mass it behaves like (J/r²). It sets how
    /// violently a wheel spins up or locks — against a corner carrying ≈ 350 kg, an engine asking for
    /// more than the patch can transmit lights the tyre up over a fraction of a second. <b>A variant may
    /// state its own</b> (<c>wheelRotatingMassKg</c>).
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
    /// How long the load takes to arrive where the tyres are putting it — long enough that one noisy tick
    /// cannot flick the grip about, which is the whole of what a suspension is in this model.
    /// </summary>
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

/// <summary>
/// What a car's lamps say and when they say it (CAR-14): what lights each of them, the rate the
/// flashing ones flash at, and how much of a light a lit one is.
/// </summary>
/// <remarks>
/// <b>Where a lamp is is not here</b>: a lens is a section of the variant's own picture and is measured
/// off it (<see cref="Agents.Car.Body.CarLens"/>). What is left for this to hold is what every lamp in
/// the town shares — when one comes on, how fast it flashes, and how much of a light it is when it does.
/// </remarks>
internal sealed class LampFigures
{
    /// <summary>
    /// How much wider than its lens the glow around a lit lamp is drawn. It is what carries a lamp at
    /// the height a street is watched from, where the lens itself is a pixel or two.
    /// </summary>
    public float GlowSpread { get; init; } = 3f;

    public float GlowStrength { get; init; } = 0.79f;

    /// <summary>
    /// The smallest lens the glow is sized as though it were. <b>A floor under the light and never under
    /// the lens</b>: the lamp itself is a section of the car's own picture and is drawn at the size the
    /// art draws it, but a lens of four texels would otherwise carry a glow too small to read from the
    /// height a street is watched from.
    /// </summary>
    /// <remarks>
    /// <b>It is the indicator's figure in all but name.</b> Nearly every indicator in the fleet is the
    /// smallest lens its car draws and sits on this floor, where a rear cluster carries its own size and
    /// is two or three times the light — so a turn was the quietest thing a car could say. Set here so
    /// the smallest lens comes out just under a middling rear lamp.
    /// </remarks>
    public float LeastGlowM { get; init; } = 0.18f;

    /// <summary>
    /// How much of the line ahead is read for the turn a car is about to make, and how far that stretch
    /// has to bend before the car says so. Together they are what an indicator <em>means</em>: 20° inside
    /// 25 m is a junction turn or a bay being pulled out of, and a street's own bend is not.
    /// </summary>
    public float TurnAheadM { get; init; } = 25f;

    public float TurnDeg { get; init; } = 20f;

    /// <summary>What the pedal has to be asking for before the brake lamps are on. Any real pressure, and nothing from the tyres.</summary>
    public float BrakeMps2 { get; init; } = 0.2f;

    /// <summary>The indicator's rate, and the share of each period it is lit for.</summary>
    public float FlashHz { get; init; } = 1.5f;

    public float FlashOnShare { get; init; } = 0.55f;

    /// <summary>
    /// The beacon's, which is faster and never dark: the bar takes its two ends end for end — the
    /// colours of a two-colour bar, the burn itself on an amber one — so a car carrying a priority is lit
    /// in every frame it is in.
    /// </summary>
    public float BeaconHz { get; init; } = 2.4f;
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
    /// <b>How much of the fleet backs into parking spaces</b> rather than nosing in (GEN-4j) — a habit drawn
    /// once per car, not a decision taken per bay. A coin, because nothing about this town makes one likelier
    /// than the other, and both shapes have to be driven for either to be worth laying.
    /// </summary>
    public float BacksIntoBaysShare { get; init; } = 0.5f;

    /// <summary>
    /// <b>How much of the town does not keep the driver's courtesies</b> (CAR-13) — a habit drawn once per
    /// person and true for the rest of the run, like the one above.
    /// </summary>
    /// <remarks>
    /// <b>One in a hundred, which is a rate and not a flourish.</b> Below this a shipped map goes whole runs
    /// without one of them meeting a red, and the behaviour is then something only a test has ever seen;
    /// much above it the town stops reading as a town and starts reading as a demolition derby, and every
    /// figure the soak reports is about the derby instead. What it buys at this rate is that the junctions
    /// are occasionally wrong, which is what the rest of the road is built to survive.
    /// </remarks>
    public float RecklessShare { get; init; } = 0.01f;

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
    /// And how long the wheel takes from one lock to the other, which bounds the rate the steering angle
    /// may change at. <b>A wheel that snaps is a car that cannot be driven</b>: an angle asked for and
    /// arriving in the same tick lets a driver put the steering on its stop at speed, which is a lock the
    /// tyres cannot hold and a front axle that spends the whole corner saturated.
    /// </summary>
    /// <remarks>
    /// It binds the follower and the hand alike (CAR-3a) — a rack is a fact about the car and not about
    /// who is turning it — and it is what makes a key press a wheel being wound on rather than a lock
    /// being selected. <b>It is stated lock to lock, so half of it is what a key held from the straight
    /// costs to reach full lock, and the same again is what letting go costs to come back.</b>
    /// </remarks>
    public float WheelTravelS { get; init; } = 0.641f;

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

    /// <summary>
    /// <b>What coming back the other way costs</b> (GEN-4l, `P-19`): a whole park and a whole unpark at a
    /// car park, or a car shunted round on the spot at a dead end. It is quoted well above three sides of
    /// any block in these towns, because turning round is what a driver does when there is no block to
    /// take — a route that prefers it to a loop is a town's traffic parking in its own streets.
    /// </summary>
    public float TurnPriceComingBackCarLengths { get; init; } = 100f;

    /// <summary>
    /// How far round one leg of a turn on the spot goes before the gear changes (`P-19`). A quarter of a
    /// turn or so: short enough that each leg fits across an ordinary street, long enough that half a dozen
    /// of them come round.
    /// </summary>
    public float ShuntSweepDeg { get; init; } = 60f;
}

/// <summary>The escalation ladder: how long a car tolerates being stopped, and what it does about it.</summary>
internal sealed class LadderFigures
{
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

    /// <summary>
    /// How long a car has to get itself round on the spot (`P-19`), in short fuses: a handful of legs at
    /// manoeuvring pace, and past it the ladder rather than a car rocking in a dead end for the rest of the
    /// run.
    /// </summary>
    public float ShuntRoundInShortFuses { get; init; } = 3f;
}

/// <summary>A walker: its pace, its footing, and how it keeps out of the way.</summary>
internal sealed class PersonFigures
{
    /// <summary>How fast a person actually walks, and how fast they can pivot on the spot. Two facts about people.</summary>
    public float RealWalkSpeedMps { get; init; } = 1.32f;

    public float RealPivotDegPerS { get; init; } = 270f;

    /// <summary>
    /// <b>How much faster than life this town is watched</b> — the one design decision in the person model,
    /// and the figure every other pace here is a real one multiplied by
    /// (<see cref="SimConfig.PersonWalkSpeedMps"/>, <see cref="SimConfig.PersonTurnRateDegPerS"/>).
    /// </summary>
    /// <remarks>
    /// <b>It was an unstated factor before it was a figure, and that is what it is for.</b> Distances in
    /// this town are real and its pace is not, so every acceleration in the person model carries the
    /// <em>square</em> of this — and a grip authored at real scale beside a pace that is not put the
    /// casualty band below walking speed, where touching a parked car is a fatal contact. Written down, the
    /// two are impossible to author out of step.
    /// </remarks>
    public float PaceScale { get; init; } = 5f;

    public float MassKg { get; init; } = 80f;

    /// <summary>
    /// <b>How much of its own diameter a body is allowed to take getting under way or coming to rest</b>,
    /// which is what the foot grip is (<see cref="SimConfig.PersonFootGripMps2"/>). A fifth: a walker stops
    /// well inside its own footprint, which is what makes a crowd on a pavement stop like people and not
    /// like traffic.
    /// </summary>
    public float StopsWithinDiameters { get; init; } = 0.2f;

    /// <summary>
    /// The grip a body along the ground has rather than a sole pressed into it — what a casualty slides to
    /// a stop on (<see cref="SimConfig.PersonSlidingGripMps2"/>).
    /// </summary>
    /// <remarks>
    /// <b>A ratio, because both grips have to be scaled by the same thing the pace is.</b> Distances in
    /// this town are real and its pace is five times a real one, so every acceleration in the person model
    /// carries a factor of twenty-five that no figure states — and a sliding grip authored at real scale
    /// beside a foot grip that is not puts the casualty band a third of the way below walking pace, where
    /// touching a parked car is a fatal contact.
    /// </remarks>
    public float SlidingGripInFootGrips { get; init; } = 0.9f;
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
    /// The room between two bodies as one steps round the other (PER-24), in bodies — added to the two
    /// radii, so a quarter is shoulders brushing past at a comfortable distance rather than a second
    /// following gap.
    /// </summary>
    /// <remarks>
    /// <b>It is a step sideways and not a gap kept in front</b>, which is why it is a figure of its own and
    /// a small one: the walk it diverges from is the thing being kept, and a step wide enough to be
    /// generous is a step off the far edge of the pavement.
    /// </remarks>
    public float ShoulderRoomInDiameters { get; init; } = 0.25f;

    /// <summary>
    /// How far past a kerb line the middle of a body may be while stepping round somebody (PER-24), in
    /// bodies. <b>Sized by the step it has to allow and not by taste</b>: a body standing on a pavement
    /// lane's own line is <see cref="SimConfig.WalkingLaneOffsetM"/> from the kerb and the step round it
    /// reaches <see cref="ShoulderRoomInDiameters"/> further than that, so anything under a quarter of a
    /// body sends the commonest step in the town the other way instead. Half of one clears that with room
    /// to spare and still leaves the middle at the channel rather than in the lane.
    /// </summary>
    public float RoadGrazeInDiameters { get; init; } = 0.5f;

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

/// <summary>What a contact has to spend to end a unit. Damage is binary: a car is intact or broken, a person on their feet or down.</summary>
internal sealed class DamageFigures
{
    /// <summary>
    /// <b>How far a contact has to be able to put a body down the road for it to have knocked them
    /// over</b> (PER-23). A distance rather than an energy, because a distance is the thing that can be
    /// looked at: half a metre is a body moved rather than a body brushed. What it costs follows from the
    /// mass being moved and the ground it slides on (<see cref="SimConfig.PersonCasualtyKj"/>).
    /// </summary>
    public float SlideToCasualtyM { get; init; } = 0.5f;

    public float CarWreckKj { get; init; } = 20f;
}

/// <summary>The rescue: how many hospitals a town has, how long each step of a call takes, and what a blue light buys.</summary>
internal sealed class AmbulanceFigures
{
    /// <summary>
    /// <b>How many of a town's buildings are hospitals</b> (AMB-1). It is a share rather than a count so
    /// that a village and a city both come out with a plausible number of them, and it is drawn from the
    /// world seed, so which buildings they are is a fact about the map.
    /// </summary>
    public float HospitalsPerBuilding { get; init; } = 0.02f;

    /// <summary>
    /// The ceiling on that, and the floor under it. <b>A town with a building on it has a hospital</b>,
    /// because a town where nobody can be collected is a town this whole slice does nothing on; and a city
    /// with two thousand buildings does not want forty ambulances standing about in it.
    /// </summary>
    public int MostHospitals { get; init; } = 6;

    /// <summary>
    /// <b>The pace a rescue is driven at</b> — 22 m/s, half again what this town's traffic manages on an
    /// ordinary street and a third of what the gearbox would give.
    /// </summary>
    /// <remarks>
    /// <b>It is the one thing a blue light gives back, and it is not a rule about the road.</b> What holds
    /// every other car's speed down here is the corners, the queues, the reds and the crossings, and a
    /// driver exempted from three of those reaches the gear's own cap on the first straight it meets: at
    /// the shipped figures an uncapped rescue crossed River at 75 m/s and wrecked itself, which is a
    /// second casualty rather than a fast ambulance. It sits well above the traffic so that overtaking is
    /// still worth doing (<see cref="DrivingFigures.PassWorthShare"/>).
    /// </remarks>
    public float CallPaceMps { get; init; } = 22f;

    /// <summary>How long the crew spends getting a casualty aboard, standing still at the vehicle while they do it.</summary>
    public float LoadingS { get; init; } = 4f;

    /// <summary>
    /// <b>How far short of the casualty the ambulance is stopped</b> (AMB-10), in car lengths — the standoff
    /// `P-18` parks at, measured along the lane the body is lying beside.
    /// </summary>
    /// <remarks>
    /// <b>It is what a crew on foot buys.</b> An ambulance that has to be within reach of the body itself is
    /// an ambulance parked on the accident, in the lane it is trying to keep clear for itself; ten metres
    /// back is a vehicle somebody can work round, and the last of the distance is a paramedic's to walk.
    /// </remarks>
    public float StandoffInCarLengths { get; init; } = 2.5f;

    /// <summary>
    /// How long a casualty is treated inside the hospital before being put back out on the pavement, healed
    /// and free to draw a trip of their own again (AMB-8).
    /// </summary>
    public float TreatmentS { get; init; } = 30f;

    /// <summary>
    /// How near <see cref="StandoffInCarLengths"/>'s own mark the ambulance has to have come to rest before
    /// the crew get out, in car lengths. <b>It is the tolerance on a parking place and no longer a reach</b>
    /// (AMB-10): what covers the last of the distance to the body is somebody walking it, so it is as wide
    /// as it ever was and the change is where the mark is rather than how near it has to be got.
    /// </summary>
    public float SceneReachInCarLengths { get; init; } = 2.5f;

    /// <summary>
    /// How long one leg of a call may run before it is written off, in blocked-road clocks. <b>MAN-4's
    /// bound said of a rescue</b>: a body the traffic never lets an ambulance reach must not hold that
    /// ambulance off every later call for the rest of the run.
    /// </summary>
    /// <remarks>
    /// <b>It is a leg and not a rescue</b>, so the clock restarts at the scene and again at the door. Ten
    /// blocked clocks is five minutes at the shipped figures — long enough to cross a city the size of
    /// River at the pace above and queue at both ends of it, and short enough that a call nothing can
    /// answer is given back to the next ambulance rather than held for the rest of the run.
    /// </remarks>
    public float GiveUpInBlockedClocks { get; init; } = 10f;

    /// <summary>
    /// How far from its hospital an ambulance may stand waiting, in block spacings. One block: near enough
    /// that the station and the hospital are the same place to anybody watching.
    /// </summary>
    public float HomeWithinBlockSpacings { get; init; } = 1f;
}

/// <summary>The other service vehicles: how many buildings stand one, and how near it may wait.</summary>
internal sealed class ServiceFigures
{
    /// <summary>
    /// <b>How many of a town's buildings are police stations</b> and how many are depots (SRV-1). Shares
    /// rather than counts, drawn from the world seed, on the terms <see cref="AmbulanceFigures.HospitalsPerBuilding"/>
    /// is: a village and a city both come out with a plausible number of each.
    /// </summary>
    public float StationsPerBuilding { get; init; } = 0.015f;

    public float DepotsPerBuilding { get; init; } = 0.008f;

    /// <summary>
    /// The ceilings on those, and the floor of one under each. <b>A town with a building on it has a
    /// station and a depot</b>, because a service vehicle no shipped map stands is one nothing exercises;
    /// and there are fewer of both than there are hospitals, because a town needs collecting from more
    /// often than it needs clearing.
    /// </summary>
    public int MostStations { get; init; } = 4;

    public int MostDepots { get; init; } = 2;

    /// <summary>
    /// How far from its own building a service vehicle may stand waiting, in block spacings —
    /// <see cref="AmbulanceFigures.HomeWithinBlockSpacings"/> said of a station and a depot.
    /// </summary>
    public float HomeWithinBlockSpacings { get; init; } = 1f;

    /// <summary>
    /// <b>How many bays a hospital's and a police station's apron holds</b> (GEN-4k), and therefore how
    /// many vehicles each of them stands. A depot has no apron and stands its one evacuator in whatever is
    /// near it.
    /// </summary>
    /// <remarks>
    /// Four is a shift rather than a vehicle: one ambulance to a hospital meant a second casualty across
    /// town waited out the first one's whole round trip, and one police car meant a beat that covered one
    /// street of a city. It is also a real cost — four bays taken off the public register at each of them
    /// — which is why the figure is small and why an apron takes only the bays a map actually has
    /// (<c>TownWorld.HoldTheApron</c>).
    /// </remarks>
    public int ApronBays { get; init; } = 4;

    /// <summary>
    /// <b>How many places a patrol visits before it is due back at its station</b> (SRV-5), drawn from one
    /// to this. A beat of several legs is what keeps a police car out on the streets rather than shuttling
    /// to one junction and back, and the return is what keeps its apron in use.
    /// </summary>
    public int MostPlacesOnABeat { get; init; } = 5;

    /// <summary>
    /// How long a police car stands on its apron between beats, drawn between the two. <b>The spread is
    /// what keeps a station's four cars off one timetable</b>: stood together before the first tick, a
    /// single interval would send all four out of the same gate at the same moment for the whole run.
    /// </summary>
    public float RestBetweenBeatsMinS { get; init; } = 20f;

    public float RestBetweenBeatsMaxS { get; init; } = 90f;

    /// <summary>
    /// How long one leg of a beat may run before it is given up and another place drawn, in blocked-road
    /// clocks — <see cref="AmbulanceFigures.GiveUpInBlockedClocks"/> said of a patrol. A patrol has nowhere
    /// it must be, so a leg the traffic will not let through costs it nothing but the next street.
    /// </summary>
    public float GiveUpInBlockedClocks { get; init; } = 10f;

    /// <summary>
    /// <b>How near a thing a crew on foot has to be standing to take hold of it</b> (SRV-3) — a casualty, a
    /// wreck's fork, a yard slot, the door of their own vehicle. An arm's length and a stride.
    /// </summary>
    /// <remarks>
    /// <b>Metres and not car lengths</b>, unlike every reach a vehicle is held to: what this measures is a
    /// person reaching, and a person does not get longer because the town's nominal car does.
    /// </remarks>
    public float CrewReachM { get; init; } = 1.5f;

    /// <summary>
    /// <b>How long a hand who is out has to get back to their seat</b> before they are put in it, in
    /// blocked-road clocks (SRV-3). It is the winch's own argument said of a person: a pavement that will
    /// not give a paramedic back is a vehicle stranded mid-errand, and the fallback is a placement over the
    /// last few metres rather than a call nothing can end.
    /// </summary>
    public float RecallInBlockedClocks { get; init; } = 3f;

    /// <summary>
    /// <b>How much road an officer closes</b> (SRV-6), in car lengths, either side of the scene along the
    /// lane it lies on. Long enough that traffic is stopped well short of somebody working in the road, and
    /// short enough that a closure is one street's business rather than a quarter's.
    /// </summary>
    public float ClosureInCarLengths { get; init; } = 6f;

    /// <summary>
    /// And how far short of the scene the police car itself is parked, in car lengths —
    /// <see cref="AmbulanceFigures.StandoffInCarLengths"/> said of a vehicle whose whole errand is to keep
    /// the ground clear, so it stands further back than the one that has to work there.
    /// </summary>
    public float SceneStandoffInCarLengths { get; init; } = 5f;

    /// <summary>
    /// How long a closure may stand before the officer is recalled and the lane given back, in blocked-road
    /// clocks (SRV-6). <b>A closure that outlived its scene would hold a street out of the town for the rest
    /// of the run</b>, which is the one failure a soft reservation can cause.
    /// </summary>
    /// <remarks>
    /// <b>The same ten clocks every other leg here is bounded by</b>, and deliberately not longer. A closure
    /// only ever buys something while somebody is working at the scene, and every errand that could be
    /// working there is written off at ten (<see cref="AmbulanceFigures.GiveUpInBlockedClocks"/>,
    /// <see cref="GiveUpInBlockedClocks"/>, <see cref="EvacuatorFigures.GiveUpInBlockedClocks"/>) — so past
    /// that the lane is being held for nobody. At twenty, a city whose wrecks its evacuators cannot reach
    /// stood every closure to its full ten minutes.
    /// </remarks>
    public float ClosureInBlockedClocks { get; init; } = 10f;
}

/// <summary>
/// The recovery: how many wrecks a depot's yard holds, how long the crew and the workshop take, and what
/// the bar between an evacuator and the car on its hook is worth.
/// </summary>
internal sealed class EvacuatorFigures
{
    /// <summary>
    /// <b>How many wrecks a depot's yard holds</b> (EVA-2), which is also how many bays it takes off the
    /// public register. Larger than a station's apron because a yard is where wrecks accumulate and an
    /// apron is where one vehicle stands, and small all the same: every slot is a place ordinary traffic
    /// can never park in.
    /// </summary>
    public int YardSlots { get; init; } = 6;

    /// <summary>
    /// How long the recovery man spends getting a wreck onto the hook, and again getting it off — <b>standing
    /// at it on foot</b> (SRV-3), and the clock only begins once he is there. Longer than a stretcher
    /// (<see cref="AmbulanceFigures.LoadingS"/>) because a car has to be winched and a person is carried.
    /// </summary>
    public float HitchingS { get; init; } = 8f;

    /// <summary>
    /// <b>How long a wreck stands in the yard before it is a car again</b> (EVA-7). It is the hospital's
    /// treatment said of a vehicle (<see cref="AmbulanceFigures.TreatmentS"/>) and deliberately the same
    /// figure: what the town is showing either way is that the thing taken off the street comes back.
    /// </summary>
    public float RepairS { get; init; } = 30f;

    /// <summary>
    /// <b>How near its hitching place the evacuator has to have stopped</b> before the man gets out to work
    /// the arm, in car lengths (EVA-5). <b>Tighter than a rescue's standoff and for the opposite reason</b>
    /// (AMB-10): an ambulance stands ten metres off on purpose and sends somebody to walk the rest, and a
    /// fork that cannot reach the car it is being swung onto is a thing nobody on foot can carry to it.
    /// </summary>
    /// <remarks>
    /// <b>It is a tolerance and not the reach itself.</b> The leg is aimed at the exact place the fork takes
    /// hold from (<c>TownWorld.TheHitchingPlaceM</c>), so what this decides is how far off that mark a truck
    /// may settle and still be worked from — and what covers the remainder is the winch, which is the stated
    /// fallback rather than the ordinary answer. Tightened to one car length the shipped maps stopped
    /// recovering anything at all: a truck that is refused its own mark re-lays the leg until the clock ends
    /// the recovery, and a crew that never gets out never reaches the winch either.
    /// </remarks>
    public float SceneReachInCarLengths { get; init; } = 2f;

    /// <summary>And how near a free yard slot it has to have got before the crew can set the wreck down in it.</summary>
    public float YardReachInCarLengths { get; init; } = 4f;

    /// <summary>
    /// How long one leg of a recovery may run before it is written off, in blocked-road clocks —
    /// <see cref="AmbulanceFigures.GiveUpInBlockedClocks"/> said of a tow.
    /// </summary>
    public float GiveUpInBlockedClocks { get; init; } = 10f;

    /// <summary>
    /// <b>How many of those clocks a haul may spend before the wreck is set down where it stands</b>
    /// (EVA-8). A rescue's delivery is never given up because a casualty is aboard and there is nothing
    /// better to do with them; a wreck is different — set down, it is no worse off than where it fell, and
    /// what giving up buys is the town's evacuator back. Three is a quarter of an hour of trying.
    /// </summary>
    public int HaulsBeforeSettingItDown { get; init; } = 3;

    /// <summary>
    /// How long the bar takes to pull its own stretch out. <b>It is the stiffness</b>, in the only units a
    /// coupling solved as an impulse has: seven ticks at the shipped rate, which is rigid to anything
    /// watching and still slack enough that a shunt is absorbed rather than snapping the pair straight.
    /// </summary>
    public float HitchSettleS { get; init; } = 0.12f;

    /// <summary>
    /// The most the bar may be worth, in <em>g</em> on the pair's reduced mass
    /// (<see cref="SimConfig.EvacuatorHitchMostMps2"/>). <b>A ceiling and never a target</b>, on the tyre
    /// model's own terms: what the coupling spends is the lesser of what the drift asks for and this. Two
    /// and a half <em>g</em> is more than a coupling ever needs to hold a steady tow — comfortably over
    /// what the tyres at either end of the bar can put through it — and little enough that a shunt cannot
    /// throw either body across the street, or the reaction at the hook spin the vehicle doing the pulling
    /// off its own line.
    /// </summary>
    /// <remarks>
    /// <b>In grips rather than in m/s², because every figure it is measured against is.</b> It stands
    /// against what the tyres hold, so a coupling authored as a bare deceleration is one that stops
    /// out-reaching them the moment the rubber changes.
    /// </remarks>
    public float HitchMostInGrips { get; init; } = 2.548f;

    /// <summary>
    /// <b>And what share of that the bar may spend sideways</b> — a small one, and the figure that keeps a
    /// tow from being a jack-knife. An impulse across the drawbar acts through a moment arm on both bodies
    /// at once, so it buys several times the yaw the same impulse along the bar buys, and every bit of that
    /// yaw lands on the vehicle with a line to hold. Held down to this, a turn taken too tight for the
    /// trailer stretches the coupling and scrubs the trailer round instead of spinning the truck.
    /// </summary>
    public float HitchSideShare { get; init; } = 0.12f;

    /// <summary>
    /// <b>What share of a towed car's weight is still on its own back wheels</b> (EVA-5). The rest is on the
    /// hook, which is what lifting the nose means: the front pair leaves the ground, the back pair carries
    /// what the bar is not holding up, and that load is the whole of what those two wheels grip with.
    /// </summary>
    public float OnTheTrailerAxleShare { get; init; } = 0.6f;

    /// <summary>
    /// <b>How far inside a towed car's end the fork takes hold</b> (EVA-5), the same figure on every car in
    /// the catalogue and at either end of it. What an underlift goes under is the bodywork; how far behind
    /// that the axle happens to sit is a fact about the car and not about the truck, and holding at the axle
    /// put the fork a hand's breadth further into a van than into a coupé for no reason either could be seen.
    /// </summary>
    public float TowGripInsideTheEndM { get; init; } = 0.5f;

    /// <summary>
    /// <b>How much wider of its line a vehicle with a car on the bar may run</b> before the road calls that
    /// line lost (CAR-10a), as a multiple of what a car is allowed. The town's corners are laid for the
    /// nominal car and an articulated pair cannot take them as tightly; held to a car's allowance, a tow was
    /// declared lost halfway round every second corner and stopped where it stood.
    /// </summary>
    public float TowedLineAllowance { get; init; } = 2.5f;
}
