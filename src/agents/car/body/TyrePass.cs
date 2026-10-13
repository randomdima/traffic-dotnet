using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// <b>What a car's tyres carry from one tick to the next</b>, as spans over whoever keeps it — the fleet's own arrays
/// in the town, a test's in a test: four rims, the scrub each has banked toward a mark, the stretch of mark each has
/// in hand, and the car's own throttle back-off.
/// </summary>
internal readonly ref struct TyreCarry
{
    public readonly Span<float> SpinMps;
    public readonly Span<float> ScrubTravelM;
    public readonly Span<bool> Marking;
    public readonly Span<Vector2> MarkFromM;
    public readonly Span<float> MarkIntensity;

    /// <summary>What is left of the throttle after backing off for a slide (<see cref="TyreCarries.SlipThrottle"/>).</summary>
    public readonly ref float SlipThrottle;

    /// <summary>Whether a wheel the engine turns was past its budget last tick (<see cref="TyreCarries.DrivenSlipping"/>).</summary>
    public readonly ref bool DrivenSlipping;

    public TyreCarry(
        Span<float> spinMps, Span<float> scrubTravelM, Span<bool> marking, Span<Vector2> markFromM, Span<float> markIntensity,
        ref float slipThrottle, ref bool drivenSlipping)
    {
        SpinMps = spinMps;
        ScrubTravelM = scrubTravelM;
        Marking = marking;
        MarkFromM = markFromM;
        MarkIntensity = markIntensity;
        SlipThrottle = ref slipThrottle;
        DrivenSlipping = ref drivenSlipping;
    }

    /// <summary>
    /// Whether all four wheels are at a standstill and carrying nothing over: not turning, not part-way through a
    /// mark, and with no scrub banked toward one.
    /// </summary>
    /// <remarks>
    /// It is the second half of the question asked before the tyres are skipped altogether — the first half being that
    /// the body itself is not moving — and it exists because the four wheels of a standing car are <em>state</em>,
    /// and a wheel still holding some of it is a wheel with something left to do.
    /// </remarks>
    public bool WheelsAtRest()
    {
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            if (SpinMps[wheel] != 0f || Marking[wheel] || ScrubTravelM[wheel] > 0f) return false;
        }

        return true;
    }
}

/// <summary>
/// <b>One car's tyres for one tick, whole</b>: the throttle it may ask for, the standstill it may skip, the ground
/// under each wheel, the four impulses the model spends, the hold of a car its locks keep still, the slide it lifts
/// for next tick, and the marks it leaves — off the state the car carries and nothing else, so the town's tick and
/// the device's kernel (<c>kernels/tyre_pass.glsl</c>) are held to one function.
/// </summary>
/// <remarks>
/// The ground is asked under each wheel and never under the car: a car with two wheels off the road grips
/// asymmetrically, and that asymmetry is a yaw the solver produces out of four impulses at four places rather than
/// anything decided here. <b>What a wheel stands on is whether it was on a way when the car's body was last laid</b>
/// (TER-7b) — the very reading that put the body on the ways it stands on (TER-4c.2), taken once a laying and not a tick.
/// </remarks>
internal static class TyrePass
{
    /// <summary>One car's four patches for this tick, and what each wheel wrote on the ground it stood on.</summary>
    /// <param name="grounds">What a wheel on a way and off one stands on, for the figures as they stand.</param>
    /// <param name="hand">Whether a hand is at the wheel (<see cref="Actions.CarAction.Hand"/>), which the ceiling reads.</param>
    /// <param name="inertiaKgM2">The body's own moment of inertia about its centre, for the hold of a car its locks keep still.</param>
    /// <param name="wheelsOnAWay">Which of its wheels stood on a way when the world last read its covers.</param>
    /// <param name="ground">What each wheel stands on, written unless the car was at rest.</param>
    /// <param name="marks">
    /// The marks laid this tick, at most one a wheel and in wheel order — what the caller files, in that order.
    /// </param>
    /// <param name="marksLaid">How many of <paramref name="marks"/> were written.</param>
    /// <returns>Whether the car was at rest and the tyres had nothing to do (<see cref="AtRest"/>).</returns>
    public static bool Step(
        SimConfig config, in WheelGrounds grounds, in CarBuild car, in CarPose pose, in DriveCommand command,
        bool hand, float inertiaKgM2, byte wheelsOnAWay, in TyreCarry carry, Span<SurfaceUnderWheel> ground,
        Span<WheelImpulse> impulses, Span<TyreScrub> scrub, Span<TyreMark> marks, out int marksLaid)
    {
        marksLaid = 0;

        // Wound before the standstill is answered, because a car parked mid-slide still has a throttle
        // to recover: what it may ask for when it pulls away again is not a thing that happens only
        // while the wheels are turning.
        WindTheSlipThrottle(config, carry);
        if (AtRest(pose, command, carry))
        {
            // Cleared rather than left, because the four from the tick it stopped on would otherwise be spent
            // again every tick.
            impulses.Clear();
            scrub.Clear();
            carry.DrivenSlipping = false;
            return true;
        }

        var ceilingMps2 = DriveCeilingMps2(config, car, hand, pose.AccelerationMps2.Y, carry.SlipThrottle);
        Span<Vector2> atM = stackalloc Vector2[TyreModel.Wheels];
        TyreModel.WheelPointsM(car, pose, atM);
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            ground[wheel] = grounds.Under(wheelsOnAWay, wheel);
        }

        TyreModel.Step(config, car, pose, command, ceilingMps2, atM, ground, carry.SpinMps, config.TickSeconds, impulses, scrub);

        // Held by its locks with nobody on the throttle, a car that its tyres can hold is held still, whole.
        if ((command.LocksEveryWheel || command.Handbrake) && command.ThrottleMps2 == 0f
            && TyreModel.HoldStill(car, pose, inertiaKgM2, atM, ground, config.TickSeconds, impulses, carry.SpinMps))
        {
            scrub.Clear();
        }

        // A wheel the engine is turning that has gone past what it can put down is what the throttle
        // lifts for next tick. An undriven wheel sliding is a corner, not a wheel being over-asked.
        var onThePedal = command.ThrottleMps2 > 0f;
        var slipping = false;
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            slipping |= scrub[wheel].Sliding && onThePedal && IsDriven(car.DrivenFrontShare, wheel);
            if (LayMark(config, car, wheel, atM[wheel], ground[wheel], scrub[wheel], carry, out marks[marksLaid])) marksLaid++;
        }

        carry.DrivenSlipping = slipping;
        return false;
    }

    /// <summary>
    /// Whether this car's tyres have nothing whatever to do this tick, which is the state most of the
    /// town's cars are in most of the time: a body at a dead standstill, nobody on the throttle, and
    /// four wheels holding nothing over.
    /// </summary>
    /// <remarks>
    /// Every figure the model would produce here is exactly zero: a patch with no velocity under it has
    /// no slip on either axis, the pedals ask for nothing, the drag opposes a motion of zero, and a rim
    /// at rest is left where it is. A skipped calculation, not a changed one, since an impulse of nothing
    /// is never handed to the solver anyway (<c>PhysicsWorld.ApplyImpulseAt</c>).
    /// <b>It is what keeps a frozen car frozen</b> (SOL-37): the solver lets go of the last of a standing
    /// car's motion when it freezes it, so from then on this is true, and nothing is spent to wake it.
    /// </remarks>
    public static bool AtRest(in CarPose pose, in DriveCommand command, in TyreCarry carry) =>
        pose.VelocityMps == Vector2.Zero && pose.YawRateRadPerS == 0f && command.ThrottleMps2 == 0f && carry.WheelsAtRest();

    /// <summary>
    /// <b>Whether this tick's <see cref="Step"/> would clear the car's impulses and scrub and change nothing else</b>:
    /// at rest (<see cref="AtRest"/>), with the throttle's back-off wound all the way back and no slide to lift for. A
    /// caller may skip such a car's pass and only clear the two, and the town is the same to the bit — which is what lets
    /// a parked town's tyres cost a comparison a car, on the host and on the device (<c>kernels/tyre_pass.comp</c>) alike.
    /// </summary>
    public static bool Settled(Vector2 velocityMps, float yawRateRadPerS, in DriveCommand command, in TyreCarry carry) =>
        carry.SlipThrottle == 1f && !carry.DrivenSlipping && velocityMps == Vector2.Zero && yawRateRadPerS == 0f
        && command.ThrottleMps2 == 0f && carry.WheelsAtRest();

    /// <summary>
    /// The most a driven axle of this car may be asked for: <b>what the friction circle has left along the roll
    /// once the corner it is actually taking has been paid for</b> (CAR-3b). Past that the engine buys no
    /// acceleration and only takes grip off the turn, which is the whole of why a rear-driven car under
    /// power runs wide. Spent off the throttle's back-off as <see cref="WindTheSlipThrottle"/> left it this tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The corner is the one the tyres are actually turning and not the one the profile planned</b>: the
    /// lateral acceleration the patches themselves put down, lagged exactly as the loads are, so what the
    /// throttle has left is measured against the corner the rubber is paying for.
    /// </para>
    /// <para>
    /// <b>A hand at the wheel is held to the ellipse and to nothing else.</b> The remainder is a fact about
    /// the rubber, so it binds whoever is at the pedals; the traction-control back-off below it —
    /// <see cref="TyreFigures.TractionThrottleFraction"/> and the lift while the tyres report a slide — is a
    /// self-driver keeping its own tyres out of trouble, and flooring it is still the player's to do.
    /// </para>
    /// <para>
    /// <b>A hand's ellipse is its own car's and a self-driver's is still the nominal patch</b>, which is
    /// the one figure left in the tyre path that is not the car's. For a hand the two have to agree or the
    /// ceiling is a second gate on what the model already refuses (SIM-7), and a disagreeing one: a look
    /// grippier than nominal has its throttle shut off at a lateral its own rubber is still holding, gets
    /// it back the moment the car slows for want of it, and hunts between the two for as long as the pedal
    /// is held. The self-driver keeps the nominal patch for the reason the car's decision log gives; a hand
    /// reads no crossings, so nothing about it waits on that.
    /// </para>
    /// </remarks>
    static float DriveCeilingMps2(SimConfig config, in CarBuild car, bool hand, float acrossMps2, float slipThrottle)
    {
        if (hand)
        {
            return TyreModel.DriveLeftMps2(car.GripMps2, acrossMps2);
        }

        var leftMps2 = TyreModel.DriveLeftMps2(config.TyreGripMps2, acrossMps2);
        return leftMps2 * config.Tyre.TractionThrottleFraction * slipThrottle;
    }

    /// <summary>
    /// <b>The throttle's back-off wound on for this tick</b>, every tick and whether or not the wheels turn: lifted while a
    /// driven wheel was past its budget last tick, given back while none was.
    /// </summary>
    static void WindTheSlipThrottle(SimConfig config, in TyreCarry carry) =>
        carry.SlipThrottle = carry.DrivenSlipping
            ? MathF.Max(carry.SlipThrottle - config.TyreSlipBackOffPerTick, config.Tyre.MinSlipThrottleFraction)
            : MathF.Min(carry.SlipThrottle + config.TyreSlipRecoverPerTick, 1f);

    /// <summary>Whether the engine is turning this wheel at all, which is what makes a slide worth lifting for.</summary>
    static bool IsDriven(float drivenFrontShare, int wheel) =>
        wheel < 2 ? drivenFrontShare > 0f : drivenFrontShare < 1f;

    /// <summary>
    /// One wheel's mark for this tick. Nothing is laid until the wheel has travelled a whole segment's
    /// worth, so a mark is a chain of quads along the path the tyre took rather than one stamp per
    /// tick; when the wheel stops working the ground, whatever stretch was in hand is closed off.
    /// </summary>
    /// <returns>Whether a stretch was laid, which is then <paramref name="laid"/>.</returns>
    static bool LayMark(
        SimConfig config, in CarBuild car, int wheel, Vector2 atM, in SurfaceUnderWheel surface, in TyreScrub scrub,
        in TyreCarry carry, out TyreMark laid)
    {
        laid = default;

        // A wheel neither sliding nor ploughing, with nothing banked and no stretch open, cannot write
        // and has nothing to close off: the whole of the rest of this is arithmetic over zeros, and it
        // is what every parked car in the town would otherwise pay four times a tick.
        if (scrub.SlideSpeedMps <= 0f && !scrub.Ploughing && carry.ScrubTravelM[wheel] <= 0f && !carry.Marking[wheel]) return false;

        carry.ScrubTravelM[wheel] = TyreModel.ScrubTravelM(config, carry.ScrubTravelM[wheel], scrub.SlideSpeedMps, config.TickSeconds);

        var intensity = TyreModel.GroundMarkIntensity(config, surface, scrub, carry.ScrubTravelM[wheel]);

        if (intensity <= 0f)
        {
            var closing = carry.Marking[wheel];
            if (closing) laid = new TyreMark(carry.MarkFromM[wheel], atM, car.WheelWidthM, carry.MarkIntensity[wheel], surface.Ploughs, wheel);
            carry.Marking[wheel] = false;
            return closing;
        }

        var lays = false;
        if (!carry.Marking[wheel])
        {
            carry.MarkFromM[wheel] = atM;
        }
        else if (Vector2.DistanceSquared(carry.MarkFromM[wheel], atM) >= config.Marks.SpacingM * config.Marks.SpacingM)
        {
            laid = new TyreMark(carry.MarkFromM[wheel], atM, car.WheelWidthM, intensity, surface.Ploughs, wheel);
            carry.MarkFromM[wheel] = atM;
            lays = true;
        }

        carry.Marking[wheel] = true;
        carry.MarkIntensity[wheel] = intensity;
        return lays;
    }
}
