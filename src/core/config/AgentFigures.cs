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
    /// <b>The longest body any variant in the catalogue wears</b> — <b>what every bay is longer
    /// than</b> (<see cref="SimConfig.CarParkBayLengthM"/>), because a bay too short for
    /// one vehicle in the town is a bay that vehicle parks across.
    /// </summary>
    /// <remarks>
    /// <b>A bound the catalogue is held to and not a reading off it</b>: the plan is laid before a single
    /// car is built and cannot ask the fleet how long it is, so the figure is stated here and
    /// <c>CarCatalogTests</c> fails the build for a variant that outgrows it. It is longer than
    /// <see cref="LengthM"/> because nobody drives the nominal car (CAR-11).
    /// </remarks>
    public float LongestLengthM { get; init; } = 5.95f;
    /// <summary>
    /// <b>The cap a car is governed at, and not a top speed it would reach.</b> 144 km/h: a road vehicle's
    /// limiter, which a variant scales to its own. It is an authored primitive because there is nothing here
    /// for it to be derived from — resistance in this model is the rolling kind, proportional to the load and
    /// flat in the speed (<see cref="SimConfig.PavedDragMps2"/>), so thrust exceeds drag at every speed and a
    /// terminal velocity does not exist. Deriving one needs an aerodynamic term this build does not have.
    /// </summary>
    /// <remarks>
    /// <b>It is load-bearing and not decoration.</b> A car's sight distance is its own stopping distance from
    /// this figure (<see cref="Agents.Car.Body.CarBuild.SightM"/>), which is how far ahead its line is laid,
    /// so a cap set where no road vehicle goes buys every car a lookahead measured in blocks.
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
    /// <b>How stiff a road car is sprung, as the rate at one wheel</b>: what a corner's spring pushes back with per metre it
    /// is compressed, anti-roll bar and all. A variant states its own (<c>wheelRateNPerM</c>) — a van's is twice a
    /// hatchback's — and absent is this one.
    /// </summary>
    /// <remarks>
    /// A raw term of the body on its springs (<see cref="Agents.Car.Body.Suspension"/>, CAR-56): how far a car rolls in a
    /// corner and how often it bounces are what it comes to, and are never stated.
    /// </remarks>
    public float WheelRateNPerM { get; init; } = 30_000f;

    /// <summary>
    /// <b>How hard a road car's damper resists, at one wheel</b>: what it pushes back with per metre a second the wheel is
    /// moving up or down. A variant states its own (<c>wheelDampingNsPerM</c>) — a van's is twice a hatchback's — and
    /// absent is this one.
    /// </summary>
    /// <remarks>
    /// The other raw term of the body on its springs (<see cref="Agents.Car.Body.Suspension"/>): how quickly a dive dies
    /// away and whether it rebounds past level first is what it and the spring come to, and is never stated.
    /// </remarks>
    public float WheelDampingNsPerM { get; init; } = 2_000f;

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
    /// a fleet of bodies shares.
    /// </remarks>
    public float StaticFrontShare { get; init; } = 0.5f;

    public float MaxSteeringDeg { get; init; } = 35.42f;

    /// <summary>The nominal figure the junctions are sized against, and nobody's actual wheelbase.</summary>
    public float WheelbaseM { get; init; } = 2.8f;

    /// <summary>
    /// <b>How hard the drivetrain holds a car back with the throttle shut</b> (CAR-51), as a coefficient of its
    /// weight on the driven axle — engine braking, the raw term (<see cref="SimConfig.CarEngineBrakingMps2"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is what stops a car let off from floating</b>, and it is spent through the driven tyres rather
    /// than beside them: it fades as the pedal goes down and is gone at the floor, so it costs a launch
    /// nothing, where a rolling resistance big enough to do the same job is paid on every start as well.
    /// </para>
    /// <para>
    /// A figure for the car rather than the ground, and a variant's to state when one wants it; the fleet
    /// shares this one.
    /// </para>
    /// </remarks>
    public float EngineBrakingResistance { get; init; } = 0.1f;

    /// <summary>1 is front-wheel drive, 0 rear, ½ all four. A fleet that varies its layouts varies this.</summary>
    public float DrivenFrontShare { get; init; } = 1f;

    /// <summary>
    /// How much wider than the car's own turning circle the turn into a bay is taken, so the steering is
    /// not sitting on its stop for the whole of an arc a car has to hold in both gears. What reads it is the
    /// circle each car's manoeuvre at a bay is shaped on (<c>CarBuild.ParkingTemplateRadiusM</c>).
    /// </summary>
    /// <remarks>
    /// <b>The margin is paid for in street</b> (GEN-4f): a manoeuvre's ground is the body swept down it, so
    /// every centimetre of margin is more of the street held while a car parks or leaves.
    /// </remarks>
    public float ParkingTemplateArcMargin { get; init; } = 1.05f;
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
    /// How much of a tyre's width stands outside the bodywork drawn over it (CAR-12), which is what every
    /// variant's track is authored against. <b>Less than this and the wheel is not there</b>: the art is a
    /// car seen from above, its panels reach the edge of its sheet, and a tyre tucked under them is four
    /// impulses acting on a body nothing on screen accounts for. <b>More and the car stands on outriggers</b>
    /// (the owner, 2026-10-22).
    /// </summary>
    public float ShowsPastTheBodyworkShare { get; init; } = 0.25f;

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
    /// Share of a driven tyre's grip along the roll that a car driving <em>itself</em> will ask for, and
    /// how fast that ask is given up and taken back when the tyres report a slide. Past the patch,
    /// throttle buys no acceleration and only lays rubber. A hand at the wheel gets none of this.
    /// </summary>
    public float TractionThrottleFraction { get; init; } = 0.95f;

    public float SlipBackOffS { get; init; } = 0.15f;
    public float SlipRecoverS { get; init; } = 0.6f;

    /// <summary>Never so little that a car needing power to get anywhere is stranded by its own traction control.</summary>
    public float MinSlipThrottleFraction { get; init; } = 0.2f;

    /// <summary>
    /// The slip angle a tyre does its cornering at, as a tangent: how much sideways creep every unit of
    /// rolling speed is worth before the patch counts as sliding rather than working. ≈ 17°, which at
    /// town speeds is the 1.5–4 m/s an ordinary turn actually carries.
    /// </summary>
    public float WorkingSlipTangent { get; init; } = 0.3f;

    /// <summary>
    /// How much of the demand along the roll may go unmet before the wheel counts as slipping rather
    /// than rolling: a hair, so a demand shaved by floating-point noise at the edge of the ellipse does
    /// not unstick a rolling wheel.
    /// </summary>
    public float AlongSlipEpsilonMps { get; init; } = 0.01f;

    /// <summary>
    /// The share of a car's weight an axle has to stand on to be asked for any of the drive or the engine's hold: an axle
    /// lighter than this is off the ground, and dividing the car's whole figure by its load would ask it for an infinity.
    /// </summary>
    public float LiftedAxleLoadShare { get; init; } = 1e-3f;

    /// <summary>
    /// The rack angle under which both front wheels are turned alike: the two radii of an Ackermann split are kilometres
    /// apart from each other there, and nothing between them is worth a tangent and two arctangents.
    /// </summary>
    public float AckermannLeastSteerRad { get; init; } = 1e-4f;

    /// <summary>
    /// The least circle the inner front wheel is turned for: past full lock the inner wheel's radius would come to nought
    /// or less, and a wheel steered about its own patch turns a right angle.
    /// </summary>
    public float InnerWheelLeastRadiusM { get; init; } = 0.01f;
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
    /// How far the light of a lit lamp spills past its lens onto the bodywork: one standard deviation of the soft
    /// copy of the lens's own shape its glow is (<c>LampSprites.Glow</c>), so the spill follows the lamp's outline.
    /// </summary>
    /// <remarks>
    /// Each glow's quad is its lens and <c>LampSprites.ReachSigmas</c> of this either side, so a longer reach is a
    /// larger quad on every lit lamp: what the glow costs a frame is drawn texels, and this is most of them.
    /// </remarks>
    public float GlowReachM { get; init; } = 0.1f;

    /// <summary>
    /// How strong the spill is: a gain on the lens's own shape blurred, held at full. The glow is light, added onto
    /// the body under it (<c>SheetSource.AddsLight</c>), so full is the lamp's whole colour summed on; past one the
    /// bright part reaches further out on the same quad, so a stronger glow costs no more texels than a weaker one.
    /// </summary>
    public float GlowStrength { get; init; } = 2f;

    /// <summary>
    /// A beacon end's <see cref="GlowReachM"/> and <see cref="GlowStrength"/>: far and strong enough to spill off the roof
    /// onto the glass and the road round the car. A glow is added light, and adds nothing to the white roof most bars
    /// stand on (the owner, 2026-10-23).
    /// </summary>
    public float BeaconReachM { get; init; } = 0.3f;

    public float BeaconGlowStrength { get; init; } = 2.5f;

    /// <summary>
    /// How much of the line ahead is read for the side the turn goes to, and how far that stretch has to
    /// bend before the car says so.
    /// </summary>
    public float TurnAheadM { get; init; } = 25f;

    public float TurnDeg { get; init; } = 20f;

    /// <summary>
    /// How near the junction a car has to be before it states the turn it is taking there (CAR-14.1).
    /// <b>The distance somebody behind is still able to act on it</b> — far enough out that the car behind
    /// has read it before either of them is slowing for the junction, and near enough that what is being
    /// stated is plainly the junction in front rather than one two streets away.
    /// </summary>
    public float JunctionAheadM { get; init; } = 50f;

    /// <summary>
    /// How long before its step back begins a car on a pass says it is coming back in (CAR-14.7): the time the
    /// traffic it rejoins has to read it. Never before its step out is done.
    /// </summary>
    public float StepBackLeadS { get; init; } = 2f;

    /// <summary>What the pedal has to be asking for before the brake lamps are on. Any real pressure, and nothing from the tyres.</summary>
    public float BrakeMps2 { get; init; } = 0.2f;

    /// <summary>The indicator's rate, and the share of each period it is lit for.</summary>
    public float FlashHz { get; init; } = 1.5f;

    public float FlashOnShare { get; init; } = 0.55f;

    /// <summary>
    /// The beacon's loop, one end and then the other, and never dark: the bar takes its two ends end for end — the
    /// colours of a two-colour bar, the burn itself on a bar of one — so a car carrying a priority is lit in every
    /// frame it is in.
    /// </summary>
    /// <remarks><b>Every bar runs it exactly</b> (the owner, 2026-10-23: "all sirens same interval").</remarks>
    public float BeaconPeriodS { get; init; } = 0.5f;

    /// <summary>How long the showroom holds each state of the picked-out car's lamps before the next (CAR-14.9).</summary>
    public float ShowroomStepS { get; init; } = 2f;
}

/// <summary>
/// What a running car's exhaust puts out (CAR-54), and a diesel's stack (TRN-15): how often a puff leaves each pipe, how
/// fast and how long it lasts, how it spreads and how thick it is, and the track of its body's way it is laid along.
/// <b>Nothing here is simulated</b>; a puff is drawn and stands nothing.
/// </summary>
/// <remarks>
/// <b>An engine idling and one asked for full throttle</b> are the two ends of each figure that comes in a pair: a puff is
/// laid at the share of the way between them its driver's throttle was when it left, so a car pulling away puts out more,
/// wider and longer-lived smoke than one at a light, and a diesel pulling hard the most of anything. Where a pipe is is the
/// vehicle's file (<see cref="Agents.Car.Body.CarVariant.ExhaustsM"/>, <c>TrainVehicle.ExhaustsM</c>).
/// </remarks>
internal sealed class SmokeFigures
{
    /// <summary>How long after one puff leaves a pipe the next does off a body standing or crawling. A puff is a quad, so this is most of what smoke costs a frame.</summary>
    public float PuffEveryS { get; init; } = 0.06f;

    /// <summary>
    /// <b>The puffs drawn in between</b> off a car fast enough to string them out: the most one
    /// <see cref="PuffEveryS"/> of exhaust is split into, a power of two, and the widest gap its puffs are left at before
    /// it is split again. Nothing is kept for one: each is laid along the car's track as any puff is, its share of the
    /// exhaust's thickness its share of the time.
    /// </summary>
    public int PuffSplits { get; init; } = 4;

    public float PuffGapM { get; init; } = 0.2f;

    /// <summary>
    /// How long a car's puff is drawn for, idling and at full throttle: clear as it leaves the pipe, thickest
    /// <see cref="ThickestAt"/> of the way through, and gone at the end. No longer than the track reaches back
    /// (<see cref="TrackPoints"/> less one of <see cref="TrackEveryS"/>), which a look refuses.
    /// </summary>
    public float IdleLifeS { get; init; } = 0.4f;

    public float FullLifeS { get; init; } = 0.45f;
    /// <summary>How far through its life a car's puff is thickest, as a share of it: halfway, so the youngest, smallest puffs do not string out behind a car at speed as beads.</summary>
    public float ThickestAt { get; init; } = 0.5f;

    /// <summary>How fast the gas leaves a tailpipe, straight back off the car that blows it, idling and at full throttle.</summary>
    public float IdleOutflowMps { get; init; } = 2f;

    public float FullOutflowMps { get; init; } = 3.5f;

    /// <summary>
    /// <b>The inertia</b>: the time a puff gives up the speed it left the pipe with — the car's own and the outflow — to
    /// the air over (<c>SmokeSprites</c>). By then it is a quarter of the way from that speed to the air's, and it never
    /// runs further from where the air alone would take it than this times the difference: a puff off a car at speed runs
    /// on after it a little and is left behind on the car's own way, which is what lays the trail, and then it goes with
    /// the wind (<c>Wind</c>).
    /// </summary>
    public float DragS { get; init; } = 0.25f;

    /// <summary>How fast a puff is blown across the pipe's line at the most, either way, by the eddies behind the car: what makes a trail a plume and not a string of beads.</summary>
    public float ScatterMps { get; init; } = 0.6f;

    /// <summary>How wide a car's puff is as it leaves the pipe, idling and at full throttle, and how fast it grows across as it mixes into the air.</summary>
    public float IdleBirthWidthM { get; init; } = 0.2f;

    public float FullBirthWidthM { get; init; } = 0.45f;

    public float SpreadMps { get; init; } = 1f;

    /// <summary>How opaque a car's puff is at its thickest off an engine idling, and off one asked for <see cref="FullThrottleMps2"/> or more.</summary>
    public float IdleOpacity { get; init; } = 0.3f;

    public float FullOpacity { get; init; } = 0.45f;

    /// <summary>The throttle a driver asks for at which the exhaust is thickest, a car's and a locomotive's (TRN-15).</summary>
    public float FullThrottleMps2 { get; init; } = 3f;

    public float FullRailThrottleMps2 { get; init; } = 0.5f;

    /// <summary>
    /// <b>A diesel's stack</b> (TRN-15), its pairs idling and at full throttle as a car's: how opaque its puff is at its
    /// thickest, how wide it leaves the stack, how fast it grows and how long it is drawn for — a diesel's engine is a
    /// lorry's six times over, and its soot shows on a pale roof and hangs over the line behind it. It blows straight up, so
    /// it has no outflow along the ground, and is thickest early, rolling dark off the stack: soon enough that a train under
    /// way has not run off its own smoke's root before it shows.
    /// </summary>
    public float StackIdleOpacity { get; init; } = 0.5f;

    public float StackFullOpacity { get; init; } = 0.9f;

    public float StackIdleBirthWidthM { get; init; } = 0.6f;

    public float StackFullBirthWidthM { get; init; } = 1.4f;

    public float StackSpreadMps { get; init; } = 1.2f;

    public float StackIdleLifeS { get; init; } = 1.5f;

    public float StackFullLifeS { get; init; } = 3.5f;

    public float StackThickestAt { get; init; } = 0.05f;

    /// <summary>The grey smoke is drawn, as a share of white: a car's, and a locomotive's stack's (TRN-15), which is a diesel's soot.</summary>
    public float Shade { get; init; } = 0.7f;

    public float StackShade { get; init; } = 0.3f;

    /// <summary>
    /// The points of its way each car keeps for its smoke to be laid along (<c>smoke_track.comp</c>), and how often the
    /// step lays one: the last second at eight a second. A puff's place is in a straight line between two of them, so a
    /// car on a 20 m circle is off its arc by a few centimetres.
    /// </summary>
    public int TrackPoints { get; init; } = 8;

    public float TrackEveryS { get; init; } = 0.125f;

    /// <summary>
    /// And each rail vehicle (TRN-15), for its stack's longer-lived smoke: nearly four seconds at four a second. A train's
    /// curves are wide, so a chord of a quarter second at a train's speed is off its arc by a few centimetres too.
    /// </summary>
    public int StackTrackPoints { get; init; } = 16;

    public float StackTrackEveryS { get; init; } = 0.25f;

    /// <summary>
    /// The speed past which a car is taken to have been put somewhere rather than driven there: its track is begun again
    /// and no smoke is laid along the jump. Well past anything a car in this town drives at.
    /// </summary>
    public float ResetMps { get; init; } = 80f;

    /// <summary>
    /// How far from its car's middle a puff may be and still be drawn: a car further off the view than this has its
    /// smoke left out with it. Past it the trail of a car at town speed is a fading wisp.
    /// </summary>
    public float ReachM { get; init; } = 12f;

    /// <summary>And from a rail vehicle's (TRN-15): a diesel pulling hard at a train's speed lays its soot this far back.</summary>
    public float StackReachM { get; init; } = 120f;
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
    /// <b>The room a pass is asked for beyond the ground the car's body sweeps</b> (CAR-46), ahead and to either
    /// side: what the car may stray off its steps, and what it passes settle towards it, between the tick it asks
    /// and the pass's end.
    /// </summary>
    public float PassSpareM { get; init; } = 0.25f;

    /// <summary>
    /// <b>How often a car waiting on its pass asks for the ground again</b> (CAR-46, CAR-50) — the lane beside, or the
    /// ground behind it to back up over — and looks again at whether what it passes is still there to be passed.
    /// </summary>
    public float PassAskEveryS { get; init; } = 1f;

    /// <summary>
    /// <b>How long a car waits on the pass it decided before it lets it go</b> (CAR-46) and decides again from where it
    /// stands: the ground it committed to was swept once, and a town that has moved on round it is waited out this long.
    /// </summary>
    public float PassPatienceS { get; init; } = 10f;

    /// <summary>
    /// <b>How often a car meaning to move across onto the lane beside looks for the room again</b> (CAR-53) while it
    /// drives on down its own: often enough that a gap going by is seen, since nothing waits for it.
    /// </summary>
    public float LaneSwitchAskEveryS { get; init; } = 0.25f;

    /// <summary>
    /// <b>How often a car waiting on its manoeuvre at a bay shapes it again and asks for its ground</b> (GEN-4f) — each
    /// time a sweep of the whole shape over the atlas and the layer: often enough that a gap in the traffic going by is
    /// seen, since the street does not wait for it.
    /// </summary>
    public float ManoeuvreAskEveryS { get; init; } = 0.25f;

    /// <summary>
    /// <b>How often a car at rest off its line looks for the lane under it again</b> (CAR-9): what it stands on does not
    /// change while it stands, so it is not asked every tick.
    /// </summary>
    public float RejoinLooksEveryS { get; init; } = 1f;

    /// <summary>
    /// <b>The widest a car swings away from a bay before it turns in nose first</b> (GEN-4f), as the angle off its
    /// own line. A car standing nearer the bay than its own circle swings out by the least that lets the turn end
    /// square; one that would have to swing further than this does not nose in from where it is.
    /// </summary>
    public float ParkingSwingMostDeg { get; init; } = 45f;

    /// <summary>
    /// <b>The pace a car turns at in a manoeuvre at a bay</b> (GEN-4f): a walking pace, the one a driver winds the wheel
    /// across at. A turn of a manoeuvre is on the car's own circle, near full lock, and a few metres long; taken any faster
    /// the rack is still winding when the next one begins, and the car stands crooked where the shape ends.
    /// </summary>
    public float ManoeuvringPaceMps { get; init; } = 2f;

    /// <summary>
    /// <b>How far a car at the kerb backs up before it pulls out</b> (GEN-4n), where what stands in front of it leaves
    /// too little room for the S out: a car's bumper's worth, which is what a driver takes back off the car behind.
    /// </summary>
    public float KerbBackUpM { get; init; } = 1f;

    /// <summary>
    /// <b>How far off its place's line a parked car's heading may stand</b> (GEN-4i): past it the car is crooked in its
    /// place, and straightens up before it stands down.
    /// </summary>
    public float ParkedSquareWithinDeg { get; init; } = 3f;

    /// <summary>
    /// <b>And how far from the middle of its place its body may stand</b> (GEN-4i): along it, past which the car moves up or
    /// back to the middle before it stands down — a piece of that move spent this near its end and no further off — and
    /// across it, past which a straightening may not carry it.
    /// </summary>
    public float ParkedMiddleWithinM { get; init; } = 0.15f;

    /// <summary>
    /// <b>How much of its braking a car coming up to something it means to get past slows at</b> (CAR-46): gently,
    /// so it arrives slower and the lane beside has longer to clear before it must stand.
    /// </summary>
    public float WaitingToPassBrakingShare { get; init; } = 0.3f;

    /// <summary>
    /// The same share for braking, which is <b>the one thing a driver aims a whole manoeuvre at</b> and
    /// therefore the one it may spend nearly all of. A corner is held for as long as it lasts and the
    /// margin there is what covers a bump, a camber and the wheel still being turned; a stop is planned,
    /// straight and over in a few seconds, and what is left over is the rolling resistance, which the
    /// tyres spend outside their own budget and hand back as a stop shorter than the plan.
    /// </summary>
    /// <remarks>
    /// It is what sizes every claim on the road, because the ground a car holds is the ground it
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
    /// <b>How long a car nothing else drives stands in a bay before it leaves for another</b> (CAR-8), drawn
    /// between the two each time it parks. The spread is what keeps a car park stood full before the first
    /// tick from emptying in one, and the scale is what keeps a town's cars mostly parked, as a town's are:
    /// at a minute or less every car in Odesa is on the road at once and its arterial junctions lock.
    /// </summary>
    public float ParkedMinS { get; init; } = 30f;

    public float ParkedMaxS { get; init; } = 240f;

    /// <summary>
    /// <b>How far from where it stands a car's next bay is looked for</b> (CAR-8) — a radius, the place drawn
    /// inside it. Most of a town's trips are local; drawn anywhere in the town, every one of them crosses it
    /// down the same few arterials.
    /// </summary>
    public float RoundReachM { get; init; } = 600f;

    /// <summary>
    /// <b>How much of the town does not keep the driver's courtesies</b> (CAR-13) — a habit drawn once per
    /// person and true for the rest of the run, like the one above. It drops nothing while a red is a light's
    /// hold on the road rather than a courtesy (CAR-13.1).
    /// </summary>
    public float RecklessShare { get; init; } = 0.01f;

    /// <summary>
    /// <b>How far short of the end of its grant a car comes to rest</b> (S-2a), whatever ended it — a queue, a
    /// light's bar, a movement it gives way to, a zebra, somebody on foot. It is the whole of the following
    /// distance at rest, and the one figure a car keeps off anything.
    /// </summary>
    public float StandOffM { get; init; } = 1.5f;

    /// <summary>
    /// <b>How long a driver means to hold the speed it is planning for</b> (TER-4c.1) — the middle term of
    /// how far its plan reaches, between getting up to that speed and stopping from it.
    /// </summary>
    /// <remarks>
    /// <b>It is what tells a plan from a commitment, and without it there is no difference.</b> The ground a
    /// car can no longer stop short of is a lead of travel and a stop (<c>CarFollower.SlowsOverM</c>, a fraction of a
    /// second), so a plan measured at the same lead is
    /// exactly that ground again for any car already doing the speed it is planning for — which is every car on
    /// an open road, and the ones whose intentions are worth having planned. <b>What it costs is room</b>: it
    /// is metres planned at every speed by every car in the town, each weighed against every other plan it
    /// meets, so what the town can afford is the soak's answer and not a rule's.
    /// </remarks>
    public float PlannedRunS { get; init; } = 2f;

    /// <summary>
    /// <b>The furthest any plan reaches past where it begins</b> (TER-4c.1), however fast its holder means to go.
    /// </summary>
    /// <remarks>
    /// <b>A car drives to stop by the end of its own plan</b> (S-2), so on an open road this is what bounds its
    /// pace, and with it how long a car caught by an amber takes to reach the bar it cannot stop short of.
    /// </remarks>
    public float PlanMostM { get; init; } = 40f;

    /// <summary>
    /// <b>How many joins that break the line one plan may pass</b> (TER-4c.1) — every turn, and every join that bends
    /// the road on by more than <see cref="JoinBendStepPerM"/>. A join that carries the road straight on is not
    /// counted.
    /// </summary>
    public int PlanMostJoins { get; init; } = 2;

    /// <summary>
    /// <b>How far past what it needs a car asks for its plan</b> (TER-4c.1), so that it keeps what it holds rather than
    /// asking again until it needs more than it has, or has this much twice over to spare (the root decision log,
    /// 2026-10-14). Never past a join its plan may not pass, nor past its line's end.
    /// </summary>
    public float ClaimSlackM { get; init; } = 5f;

    /// <summary>
    /// <b>How far past where its plan begins a car drives before letting go of the ground behind its nose</b>: short of
    /// the shortest car, so the ground it has driven past and still holds is under its own body.
    /// </summary>
    public float TrimSlackM { get; init; } = 2f;

    /// <summary>
    /// <b>How far a join's curvature may depart from the lane it leaves and still carry the road on</b> (TER-4c.1),
    /// as a step in curvature: past it, the join is a turn or a kink and counts against
    /// <see cref="PlanMostJoins"/>.
    /// </summary>
    public float JoinBendStepPerM { get; init; } = 0.05f;

    /// <summary>
    /// How long a pedal takes to travel from one stop to the other, which is what bounds the rate the
    /// commanded acceleration may change at. <b>Without it the pedal is a relay</b>: the profile asks for
    /// whatever closes the speed error in one tick, so an error of a fifth of a metre a second saturates it,
    /// and a car holding a speed snaps between full throttle and full brake several times a second.
    /// </summary>
    /// <remarks>
    /// <b>What it costs is a shade of the stop and never its accuracy</b> — the demand still arrives, a
    /// pedal-travel later — so it is well inside the reaction lead every distance is already measured
    /// through. <b>Spending the braking margin is not held to it</b> (<c>CarFollower.IsAHazard</c>): that is
    /// the one place the pedal is allowed to snap.
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

    /// <summary>How far either side of its last progress a car looks for itself on its own line.</summary>
    public float ProjectionWindowInCarLengths { get; init; } = 2f;

    /// <summary>What the route prices are quoted in, and not any actual car's length.</summary>
    public float NominalCarLengthM { get; init; } = 4.5f;

    /// <summary>A preference between routes, not a time.</summary>
    public float TurnPriceNearSideCarLengths { get; init; } = 0.5f;

    public float TurnPriceAcrossOncomingCarLengths { get; init; } = 4f;

    /// <summary>
    /// <b>What moving across onto the lane beside costs a route</b> (CAR-53), each lane over: a gap in the lane beside
    /// waited for while the car drives on, so dearer than a turn off the kerb and cheaper than one across the stream.
    /// </summary>
    public float LaneSwitchPriceCarLengths { get; init; } = 1f;

    /// <summary>
    /// <b>How much slower than a car means to go the traffic in its lane has to be for it to move across round it</b>
    /// (CAR-53), on a road of more than a lane its way: what goes nearly as fast is followed.
    /// </summary>
    public float SlowerToSwitchMps { get; init; } = 3f;

    /// <summary>
    /// <b>What coming back the other way costs</b> (GEN-4l): a whole park and a whole unpark at a car
    /// park. It is quoted well above three sides of any block in these towns, because turning round is
    /// what a driver does when there is no block to take — a route that prefers it to a loop is a town's
    /// traffic parking in its own streets.
    /// </summary>
    public float TurnPriceComingBackCarLengths { get; init; } = 100f;
}

/// <summary>
/// <b>How long a driver puts up with getting nowhere</b>, and what it does about it: the road priced up
/// and the route laid again, and past that the leg given up (CAR-15). Every figure here is a fraction of
/// something the town already states, so nothing in it is a duration somebody chose.
/// </summary>
internal sealed class DrivePatienceFigures
{
    /// <summary>
    /// The patience a leg is given up after, in light cycles: how long a car covers no ground before the
    /// road is priced up and, past the last reroute, the leg is over. It must sit well above a full red
    /// phase, or every busy junction gets routed around. Both phases share the cycle, so a red is half of
    /// one and this is four.
    /// </summary>
    public float BlockedRoadInLightCycles { get; init; } = 2f;

    /// <summary>How many times one leg may reroute before the road stops being the thing that is wrong with it.</summary>
    public int ReroutesPerLeg { get; init; } = 3;

    /// <summary>
    /// What a blocked stretch is priced at, and how long the mark lives in patiences. Expensive,
    /// never impassable: in a town this small the only road to a place may be the marked one, and a mark
    /// expires so a road nobody has driven since is tried again.
    /// </summary>
    public float BlockedWayPriceInBlockSpacings { get; init; } = 4f;

    public float BlockedWayLifeInBlockedClocks { get; init; } = 2f;

    /// <summary>
    /// How far back a queue at a light reaches, in the waiting car's own lengths: a light's hold that near
    /// is the one wait that spends no patience (TLT-2a), so it has to cover the whole queue and not only its
    /// front.
    /// </summary>
    public float LightQueueInCarLengths { get; init; } = 20f;
}

/// <summary>A walker: its pace, its footing, and how it keeps out of the way.</summary>
internal sealed class PersonFigures
{
    /// <summary>How fast a person actually walks, and how fast they can pivot on the spot. Two facts about people.</summary>
    public float RealWalkSpeedMps { get; init; } = 1.32f;

    public float RealPivotDegPerS { get; init; } = 270f;

    /// <summary>
    /// <b>How much faster than life a walker moves</b> — the figure every other pace here is a real one
    /// multiplied by (<see cref="SimConfig.PersonWalkSpeedMps"/>, <see cref="SimConfig.PersonTurnRateDegPerS"/>).
    /// Two: a brisk jog beside traffic driven at life's pace, where five is a sprint as fast as the cars. A town
    /// watched faster is watched on the clock (<see cref="SimFigures.SoakMaxTimeScale"/>), which scales both.
    /// </summary>
    /// <remarks>
    /// <b>It is kept as a figure because every acceleration in the person model carries its square</b>: a
    /// grip authored at one pace beside a pace that is not put the casualty band below walking speed, where
    /// touching a parked car is a fatal contact. Written down, the two are impossible to author out of step.
    /// </remarks>
    public float PaceScale { get; init; } = 2f;

    public float MassKg { get; init; } = 80f;

    /// <summary>
    /// <b>The ring a walker look walks round on the showroom</b> (PER-30): its radius, and how far round it ahead of
    /// itself the walker aims — near enough that it keeps to the ring, far enough that it never arrives.
    /// </summary>
    public float ShowroomRingM { get; init; } = 3f;

    public float ShowroomRingLeadDeg { get; init; } = 45f;

    /// <summary>
    /// <b>How near its aim a walker's body has to stand for the aim to be under it</b>, as the square of the distance — a
    /// tenth of a millimetre: it stands rather than turning to face a point a rounding away (<c>WalkerFollower.Step</c>).
    /// </summary>
    public float AimUnderfootM2 { get; init; } = 1e-8f;

    /// <summary>
    /// <b>How much of its own diameter a body is carried by a shove at walking pace before its feet have
    /// taken it back</b>, which is what the foot grip is (<see cref="SimConfig.PersonFootGripMps2"/>). A fifth:
    /// a walker bumped is carried well inside its own footprint, which is what makes a crowd on a pavement
    /// jostle like people and not like traffic. Its own stops and starts spend no grip (PER-3).
    /// </summary>
    public float StopsWithinDiameters { get; init; } = 0.2f;

    /// <summary>
    /// The grip a body along the ground has rather than a sole pressed into it — what a casualty slides to
    /// a stop on (<see cref="SimConfig.PersonSlidingGripMps2"/>).
    /// </summary>
    /// <remarks>
    /// <b>A ratio, because both grips have to be scaled by the same thing the pace is.</b> Every
    /// acceleration in the person model carries the square of <see cref="PaceScale"/> — and a sliding grip
    /// authored at one pace beside a foot grip at another puts the casualty band below walking pace, where
    /// touching a parked car is a fatal contact.
    /// </remarks>
    public float SlidingGripInFootGrips { get; init; } = 0.9f;

    /// <summary>
    /// The clear ground a walker keeps between where it will have stopped and whatever it is stopping
    /// behind, in bodies. It is the whole of the following distance at rest, and one body is what a queue
    /// on a pavement actually stands at.
    /// </summary>
    public float StandstillGapInDiameters { get; init; } = 1f;

    /// <summary>
    /// How far along the way in front of itself a walker looks, in bodies — what it aims at, and how far
    /// either side of its own place the way is searched when the solver has pushed it about. It is the
    /// whole of how closely a walk holds the ground's own curve: aimed at the point under its feet a body
    /// never turns, and aimed a stretch ahead it cuts the bend between.
    /// </summary>
    public float WalkAheadInDiameters { get; init; } = 2f;

    /// <summary>
    /// <b>How far past what it needs a walker asks for its walk</b>, in bodies (PER-26), so it keeps what it holds rather
    /// than asking again until it needs more than it has (the root decision log, 2026-10-14). Never onto a zebra's paint
    /// it does not need yet.
    /// </summary>
    public float ClaimSlackInDiameters { get; init; } = 2f;

    /// <summary>
    /// <b>How far past where its walk begins a walker walks before letting go of the ground behind it</b>, in bodies:
    /// no more than half of one, so what it still holds behind its front is in front of its middle (PER-26).
    /// </summary>
    public float TrimSlackInDiameters { get; init; } = 0.5f;

    /// <summary>
    /// The brief random idle before drawing a destination, so a town's worth of people do not all set off
    /// on the same tick. A stagger and not a wait: longer than the decision interval, far shorter than a dwell.
    /// </summary>
    public float StandByIdleMaxS { get; init; } = 1f;

    /// <summary>
    /// The share of car owners a town is laid with already at their wheel and setting off on a driven trip, rather
    /// than indoors part way through a dwell (GEN-7). Drawn per person; none by default.
    /// </summary>
    /// <remarks>
    /// A load figure: a town laid with every owner indoors puts its traffic on the road a dwell at a time, so a
    /// measurement of the tick taken in its first minutes is of a town that is mostly parked.
    /// </remarks>
    public float StartsAtTheWheelShare { get; init; }

    /// <summary>
    /// How far a bay a car is aiming at may stand from where that leg is going, in block spacings — one
    /// block, so a car parks on the destination's own block rather than the next one along, and the walk from
    /// a parked car to its owner's door is a frontage or two (PER-29).
    /// </summary>
    public float WalkWorthInBlockSpacings { get; init; } = 1f;

    /// <summary>
    /// <b>The furthest a trip is walked, in block spacings</b> (PER-29) — a few small blocks. A door further
    /// than this from the person, or from their car, is driven to.
    /// </summary>
    public float WalksNoFurtherInBlockSpacings { get; init; } = 3f;

    /// <summary>
    /// <b>How far from where a person stands their next destination is drawn, in block spacings</b> (PER-9) — a
    /// radius, the place drawn inside it. Most of a town's trips are local: drawn anywhere in the town, every
    /// one of them is driven across it down the same few arterials, as a car's would be without its own reach
    /// (<see cref="DrivingFigures.RoundReachM"/>).
    /// </summary>
    public float TripReachInBlockSpacings { get; init; } = 6f;

    /// <summary>
    /// <b>How long a walker may get no nearer the end of the way it is walking before the leg is given
    /// up</b> (PER-25). What ends one is the line being drawn somewhere else, so this is the time a body
    /// spends being leaned on by a crowd or held against a wall before that is worth doing.
    /// </summary>
    /// <remarks>
    /// <b>It is its own figure and not a driver's patience</b> (<see cref="SimConfig.CarPatienceS"/>), which
    /// is read off the light cycle a driver queues at: nothing about the pavement holds a walker up on
    /// purpose. Too short, it reads an ordinary shove at a doorway as a trip that cannot be finished, and a
    /// town's walkers then spend the run drawing destinations instead of walking to them.
    /// </remarks>
    public float GivesUpAfterS { get; init; } = 20f;

    /// <summary>
    /// <b>How often a walker waiting on the pass it decided asks for the ground again</b> (PER-28), and looks again at
    /// whether the one it passes is still standing where it was.
    /// </summary>
    public float SidestepAskEveryS { get; init; } = 1f;

    /// <summary>
    /// <b>How long a walker waits on the pass it decided before it lets it go</b> (PER-28) and decides again from where
    /// it stands: the ground it committed to was swept once.
    /// </summary>
    public float SidestepPatienceS { get; init; } = 5f;

    public float DiameterInPropDiameters { get; init; } = 0.5f;
    public float ExitSearchRadiusInPropDiameters { get; init; } = 1f;

    /// <summary>
    /// <b>How often a driver refused the ground beside its door asks for it again</b> (PHY-7a): every place in reach is
    /// taken by somebody, which a decision later it almost always still is.
    /// </summary>
    public float ExitAskEveryS { get; init; } = 1f;
}

/// <summary>What a contact has to spend to end a unit. Damage is binary: a car is intact or broken, a person on their feet or down.</summary>
internal sealed class DamageFigures
{
    /// <summary>
    /// <b>How far a contact has to be able to put a body down the road for it to have knocked them
    /// over</b> (PER-23). A distance rather than an energy, because a distance is the thing that can be
    /// looked at. What it costs follows from the mass being moved and the ground it slides on
    /// (<see cref="SimConfig.PersonCasualtyKj"/>) — a grip that carries the square of the walker's pace
    /// (<see cref="PersonFigures.PaceScale"/>), so the same throw costs four times as much at twice the pace.
    /// </summary>
    public float SlideToCasualtyM { get; init; } = 3f;

    public float CarWreckKj { get; init; } = 20f;
}

/// <summary>The rescue: how long each step of a call takes, and what a blue light buys.</summary>
internal sealed class AmbulanceFigures
{
    /// <summary>
    /// <b>The pace a rescue is driven at</b> — 22 m/s, half again what this town's traffic manages on an
    /// ordinary street and a third of what the gearbox would give.
    /// </summary>
    /// <remarks>
    /// <b>It is the one thing a blue light gives back, and it is not a rule about the road.</b> What holds
    /// every other car's speed down here is the corners, the queues, the reds and the crossings, and a
    /// driver exempted from three of those reaches the gear's own cap on the first straight it meets: at
    /// the shipped figures an uncapped rescue crossed River at 75 m/s and wrecked itself, which is a
    /// second casualty rather than a fast ambulance. It sits well above the traffic, so a rescue is held
    /// up by what is in front of it and not by a pace of its own.
    /// </remarks>
    public float CallPaceMps { get; init; } = 22f;

    /// <summary>
    /// <b>How much further a call plans than any other driver</b> (AMB-4.5): the road it means to use and how far
    /// a plan may reach are both this many times what they would be, so what it will cross gives way to it before
    /// it is there. Ground it can no longer stop short of is a fact about its speed and is not stretched.
    /// </summary>
    public float CallReachShare { get; init; } = 2f;

    /// <summary>
    /// <b>How much slower than a call means to go traffic has to be before the call gets past it</b> (AMB-4.4).
    /// Less than this is a car holding the call's own pace on a pedal that hunts about it — a charge following
    /// its leading escort is one — and a pass round it gains nothing and takes the lane ahead from whoever is in it.
    /// </summary>
    public float SlowerToPassMps { get; init; } = 0.5f;

    /// <summary>How long getting a casualty aboard takes, the ambulance standing still at its standoff meanwhile (AMB-6).</summary>
    public float LoadingS { get; init; } = 4f;

    /// <summary>
    /// <b>How far short of the casualty the ambulance is stopped</b> (AMB-10), in car lengths — the standoff
    /// an ambulance is stopped at, measured along the lane the body is lying beside.
    /// </summary>
    /// <remarks>
    /// <b>It keeps the vehicle off the accident.</b> An ambulance that has to be within reach of the body
    /// itself is an ambulance parked on the accident, in the lane it is trying to keep clear for itself; ten
    /// metres back is a vehicle somebody can work round, and the last of the distance is a placement that
    /// nobody walks (AMB-10).
    /// </remarks>
    public float StandoffInCarLengths { get; init; } = 2.5f;

    /// <summary>
    /// How long a casualty is treated inside the hospital before being put back out on the pavement, healed
    /// and free to draw a trip of their own again (AMB-8).
    /// </summary>
    public float TreatmentS { get; init; } = 30f;

    /// <summary>
    /// How near <see cref="StandoffInCarLengths"/>'s own mark the ambulance has to have come to rest before
    /// the loading begins, in car lengths. <b>It is the tolerance on a parking place and not a reach</b>
    /// (AMB-10): what covers the last of the distance to the body is the placement at the end of the loading.
    /// </summary>
    public float SceneReachInCarLengths { get; init; } = 2.5f;

    /// <summary>
    /// How long one leg of a call may run before it is written off, in a driver's own patiences. <b>CAR-15a's
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

/// <summary>The service vehicles: how many a building stands and who rides in them, how near it they wait, and their beat.</summary>
internal sealed class ServiceFigures
{
    /// <summary>
    /// How far from its own building a service vehicle may stand waiting, in block spacings —
    /// <see cref="AmbulanceFigures.HomeWithinBlockSpacings"/> said of a station and a depot.
    /// </summary>
    public float HomeWithinBlockSpacings { get; init; } = 1f;

    /// <summary>
    /// <b>How many vehicles a service building stands</b> (GEN-4k, AMB-2, SRV-2), one to a bay of its apron — a
    /// hospital's ambulances, a station's police cars and a depot's evacuators alike. A depot's apron holds its
    /// yard's slots besides (<see cref="EvacuatorFigures.YardSlots"/>).
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
    /// <b>How many people ride in each service vehicle</b> (SRV-3), in its crew seats and in its building's
    /// uniform. A police car's first is its officer (SRV-11).
    /// </summary>
    public int CrewPerVehicle { get; init; } = 2;

    /// <summary>
    /// <b>How much of a building's fleet drives its district's streets</b> (SRV-5); the rest stand on the apron
    /// until a call takes them.
    /// </summary>
    public float PatrolShare { get; init; } = 0.5f;

    /// <summary>
    /// How long a patrolling vehicle stands on its apron before it first sets out, drawn between the two.
    /// <b>The spread is what keeps a building's fleet off one timetable</b>: stood together before the first tick,
    /// a single interval would send them out of the same gate at the same moment.
    /// </summary>
    public float FirstBeatAfterMinS { get; init; } = 5f;

    public float FirstBeatAfterMaxS { get; init; } = 60f;

    /// <summary>
    /// How long one leg of a beat may run before it is given up and another place drawn, in blocked-road
    /// clocks — <see cref="AmbulanceFigures.GiveUpInBlockedClocks"/> said of a patrol. A patrol has nowhere
    /// it must be, so a leg the traffic will not let through costs it nothing but the next street.
    /// </summary>
    public float GiveUpInBlockedClocks { get; init; } = 10f;

    /// <summary>
    /// <b>How near a place an officer on foot has to be standing to count as there</b> — the post at a closure's
    /// entrance, the door of their own car. An arm's length and a stride.
    /// </summary>
    /// <remarks>
    /// <b>Metres and not car lengths</b>, unlike every reach a vehicle is held to: what this measures is a
    /// person reaching, and a person does not get longer because the town's nominal car does.
    /// </remarks>
    public float CrewReachM { get; init; } = 1.5f;

    /// <summary>
    /// <b>How long an officer who is out has to get back to their seat</b> before being put in it, in
    /// blocked-road clocks (<see cref="SimConfig.ServiceRecallS"/>) — the bound on the one walk a closure's end
    /// waits on (SRV-11).
    /// </summary>
    public float RecallInBlockedClocks { get; init; } = 3f;

    /// <summary>
    /// <b>How many lanes back one closure may reach</b> (SRV-9): the lane a scene stands on and the lanes
    /// upstream that lead nowhere else, walked back to a junction a driver chooses at. A stretch of a street and
    /// never a district, and the room each police car's closure is written into.
    /// </summary>
    public int ClosureMostLanes { get; init; } = 8;

    /// <summary>
    /// <b>How far into a closed lane its officer stands</b> (SRV-9, SRV-11): the mouth, just clear of the box it
    /// leaves, where a car turning in meets them before it meets the lane.
    /// </summary>
    public float OfficerIntoTheLaneM { get; init; } = 1f;

    /// <summary>
    /// <b>Or, where a zebra is painted over the mouth, how far short of the paint they stand</b> (SRV-9): on the
    /// traffic's side of it, so the crossing between them and their car stays the walkers'.
    /// </summary>
    public float OfficerShortOfTheZebraM { get; init; } = 0.5f;

    /// <summary>
    /// <b>How much clear ground the police car keeps behind its tail</b> — to its officer, or to the far edge of a
    /// zebra over the mouth, whichever is further in: a stride, so the car is inside the lane it closes and
    /// stands on nobody's crossing.
    /// </summary>
    public float PoliceCarTailClearM { get; init; } = 1f;

    /// <summary>
    /// How long a closure may stand before it is given up and the lane given back, in blocked-road
    /// clocks (SRV-6). <b>A closure that outlived its scene would hold a street out of the town for the rest
    /// of the run</b>, which is the one failure a closure's claim can cause.
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
/// The recovery: how many wrecks a depot's yard holds, how long the hitch and the workshop take, and what
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
    /// How long getting a wreck onto the hook takes, and again getting it off — <b>the truck standing at its
    /// mark</b>, and the clock only begins once it has stopped within reach (EVA-5, EVA-6). Longer than a
    /// stretcher (<see cref="AmbulanceFigures.LoadingS"/>) because a car has to be winched and a person is
    /// carried.
    /// </summary>
    public float HitchingS { get; init; } = 8f;

    /// <summary>
    /// <b>How long a wreck stands in the yard before it is a car again</b> (EVA-7). It is the hospital's
    /// treatment said of a vehicle (<see cref="AmbulanceFigures.TreatmentS"/>) and deliberately the same
    /// figure: what the town is showing either way is that the thing taken off the street comes back.
    /// </summary>
    public float RepairS { get; init; } = 30f;

    /// <summary>
    /// <b>How near its hitching place the evacuator has to have stopped</b> before the arm is worked, in car
    /// lengths (EVA-5). <b>Tighter than a rescue's standoff and for the opposite reason</b> (AMB-10): an
    /// ambulance stands ten metres off on purpose, and a fork has to be got to the car it is swung onto.
    /// </summary>
    /// <remarks>
    /// <b>It is a tolerance and not the reach itself.</b> The leg is aimed at the exact place the fork takes
    /// hold from (<c>TownWorld.TheHitchingPlaceM</c>), so what this decides is how far off that mark a truck
    /// may settle and still be worked from — and what covers the remainder is the winch, which is the stated
    /// fallback rather than the ordinary answer. Tightened to one car length the shipped maps stopped
    /// recovering anything at all: a truck that is refused its own mark re-lays the leg until the clock ends
    /// the recovery, and a truck that never stops within reach never reaches the winch either.
    /// </remarks>
    public float SceneReachInCarLengths { get; init; } = 2f;

    /// <summary>And how near a free yard slot it has to have got before the wreck can be set down in it.</summary>
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
    /// nominal car and an articulated pair cannot take them as tightly; held to a car's allowance, a tow is
    /// declared lost halfway round every second corner and stopped where it stands.
    /// </summary>
    public float TowedLineAllowance { get; init; } = 2.5f;
}

/// <summary>The trains (TRN-3…12): how fast each kind runs, what it is made up of, and how often a line sends one.</summary>
internal sealed class TrainFigures
{
    /// <summary>An intercity train's pace through the town, as its own driver holds it (TRN-12): a main line's in-town limit, quickened as a walker's is (goals).</summary>
    public float IntercitySpeedMps { get; init; } = 28.6f;

    /// <summary>And a freight train's, whichever it carries.</summary>
    public float FreightSpeedMps { get; init; } = 19.5f;

    /// <summary>
    /// The most a train's own driver lets a curve pull it sideways (TRN-12): what slows it below its pace round a bend, a
    /// main line's comfort limit on a curve laid without cant.
    /// </summary>
    public float CurveSideAccelerationMps2 { get; init; } = 1f;

    /// <summary>How many coaches an intercity locomotive hauls: enough for the train to pass over itself on a loop (RAIL-11).</summary>
    public int IntercityCoaches { get; init; } = 20;

    /// <summary>How many tank wagons an oil train's locomotive hauls: enough to pass over itself on a loop (RAIL-11).</summary>
    public int OilWagons { get; init; } = 41;

    /// <summary>How many open wagons a coal train's locomotive hauls: enough to pass over itself on a loop (RAIL-11).</summary>
    public int CoalWagons { get; init; } = 36;

    /// <summary>Of the trains a line sends, the share that is intercity, and the share that carries oil; the rest carry coal.</summary>
    public float IntercityShare { get; init; } = 0.4f;

    public float OilShare { get; init; } = 0.3f;

    /// <summary>
    /// <b>The least and the most a line waits between one train leaving the map and the next being sent</b> (TRN-6),
    /// drawn evenly between them each time.
    /// </summary>
    public float GapLeastS { get; init; } = 45f;

    public float GapMostS { get; init; } = 150f;

    /// <summary>The same of a line's first train, from the town's first tick: soon, so a town just opened shows one.</summary>
    public float FirstLeastS { get; init; } = 5f;

    public float FirstMostS { get; init; } = 40f;

    /// <summary>
    /// <b>Steel on steel</b> (TRN-8): what a braked wheel slides on, and the most a driven one pulls before it spins —
    /// a wheel's share of its vehicle's weight times this, either way.
    /// </summary>
    public float WheelRailFriction { get; init; } = 0.25f;

    /// <summary>What a rolling wheel loses to the rail, as a coefficient of its load: a small share of a tyre's.</summary>
    public float RollingResistance { get; init; } = 0.0015f;

    /// <summary>How long a wheel off its rail's line takes to be brought back onto it — a flange's, held without limit.</summary>
    public float WheelOntoTheLineS { get; init; } = 0.1f;
}
