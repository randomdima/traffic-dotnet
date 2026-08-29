using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// One impulse per wheel, spent from a friction <b>ellipse</b> weighed by the load that corner carries.
/// Turning radius, drift, stopping and every skid are what falls out of it;
/// nothing above it decides how a car moves.
/// </summary>
/// <remarks>
/// <para>
/// The budget is split between the two axes on slip velocities, not force demands: a split on demands
/// weighs a lateral ask by the corner's load and a longitudinal one by the rim — two orders of magnitude
/// apart — and makes braking authority mid-slide depend on the tick rate.
/// </para>
/// <para>
/// The three guards, which must not be removed:
/// <list type="number">
/// <item>a <b>deadband</b> on carried slip: a rolling wheel is set to the speed the road is about to
/// have, so a tick later the tread leads or trails by roughly what the pedals just asked for. Without
/// it a car doing nothing worse than pulling away reports a slide it is not having;</item>
/// <item><b>no overshoot</b> on the rim: where a full-grip impulse would carry the rim past the road
/// speed it is converging on, the crossing <em>is</em> the answer. Without it the friction reverses
/// next tick and the wheel rings about road speed instead of settling onto it;</item>
/// <item>the ellipse boundary is a <b>ceiling and never a target</b>: what is spent is the lesser of
/// what the wheel wants and what the ground affords.</item>
/// </list>
/// </para>
/// <para>Nothing here touches a body, so the whole model is judged against arithmetic.</para>
internal static partial class TyreModel
{
    /// <summary>Front right, front left, rear right, rear left — <c>+y</c> in the body's frame being the driver's side.</summary>
    public const int Wheels = 4;

    /// <summary>
    /// The whole of one car's tyres for one tick: an impulse and a place to spend it per wheel, what
    /// each wheel did to the ground under it, and the four rims wound on.
    /// </summary>
    /// <param name="car">
    /// <b>The car whose tyres these are</b> (CAR-11): where its axles stand under it, how wide its track
    /// is, what its rubber holds and which end its drive is placed on. Every one of them is the variant's,
    /// because a tyre is a fact about a car and not about a fleet.
    /// </param>
    /// <param name="axleDriveCeilingMps2">
    /// The most a <em>driven axle</em> may be asked for, applied after the drive has been divided by the
    /// axle's load because that is the figure a wheel is actually asked for. Infinite for a hand at the
    /// wheel — flooring it is the player's to do.
    /// </param>
    /// <param name="atM">
    /// Where the four patches stand, from <see cref="WheelPointsM"/>. Passed in because the caller has
    /// already had to have it to sample the ground under each wheel.
    /// </param>
    /// <param name="spinMps">
    /// Each wheel's own rotation, as the speed its tread runs over the ground. State: carried between
    /// ticks and wound on here, and the daylight between it and the road under the patch is the
    /// longitudinal slip — wheelspin one way, lock-up the other.
    /// </param>
    public static void Step(
        SimConfig config, in CarBuild car, in CarPose pose, in DriveCommand command, float axleDriveCeilingMps2,
        ReadOnlySpan<Vector2> atM, ReadOnlySpan<SurfaceUnderWheel> ground, Span<float> spinMps, float dtS,
        Span<WheelImpulse> into, Span<TyreScrub> scrub)
    {
        var forward = pose.Forward;
        var drivenFrontShare = car.DrivenFrontShare;

        Span<float> loadFraction = stackalloc float[Wheels];
        Loads(config, car, pose, loadFraction);

        Span<float> steerRad = stackalloc float[Wheels];
        Ackermann(car, command.SteerRad, steerRad);

        // Drive is placed by layout and divided by the load of the axle it is placed on, so a light end
        // of the car spins its wheels rather than pushing the whole of it.
        var frontLoad = loadFraction[0] + loadFraction[1];
        var frontDriveMps2 = NoLargerThan(DriveShare(command, drivenFrontShare, frontLoad), axleDriveCeilingMps2);
        var rearDriveMps2 = NoLargerThan(DriveShare(command, 1f - drivenFrontShare, 1f - frontLoad), axleDriveCeilingMps2);
        var spinAllowanceMps = SpinAllowanceMps(config, Vector2.Dot(pose.VelocityMps, forward));

        for (var wheel = 0; wheel < Wheels; wheel++)
        {
            // The body's motion plus what the yaw rate carries that corner round by.
            var fromCentreM = atM[wheel] - pose.PositionM;
            var velocityMps = pose.VelocityMps + pose.YawRateRadPerS * new Vector2(-fromCentreM.Y, fromCentreM.X);
            // An unturned wheel points where the car does. Adding a zero to the heading cannot move it,
            // so this is the same direction rather than one near it.
            var wheelForward = steerRad[wheel] == 0f
                ? forward
                : Heading.Unit(pose.HeadingRad + steerRad[wheel]);
            var wheelRight = Heading.RightOf(wheelForward);

            var rear = wheel >= 2;
            // The handbrake holds the rear pair while the front keeps rolling and steering, so a car can
            // be turned into its stop; a wreck is locked as a block because nobody is deciding on it.
            var locked = command.LocksEveryWheel || (rear && command.Handbrake);
            var surface = ground[wheel];
            // This car's own rubber on this wheel's own ground: what a variant is worth through a corner
            // is the difference between a supercar and a truck on the same tarmac.
            var acrossGripMps2 = car.GripMps2 * surface.Coefficient;
            var alongGripMps2 = car.LongGripMps2 * surface.Coefficient;
            var loadKg = pose.MassKg * loadFraction[wheel];

            var alongMps = Vector2.Dot(velocityMps, wheelForward);
            var acrossMps = Vector2.Dot(velocityMps, wheelRight);

            // A locked wheel is not turning at all — that is what locked means, and the handbrake holds
            // it against the tyre for as long as it is on.
            if (locked) spinMps[wheel] = 0f;
            var spin = spinMps[wheel];

            // What the patch is asked to absorb, and what that ask is worth as an impulse if it can
            // simply hold it. Two questions: pulling a light rim back into step is a slip of metres a
            // second costing almost nothing, while a car creeping sideways is a slip of centimetres
            // that takes the whole corner's weight to arrest.
            var acrossSlipAskMps = -acrossMps;
            var wantAcrossNs = -acrossMps * loadKg;
            float alongSlipAskMps;
            float wantAlongNs;
            if (locked)
            {
                // Bolted to the car: nothing is turning, so the whole corner's momentum is the slip and
                // arresting it costs the whole corner's mass.
                alongSlipAskMps = -alongMps;
                wantAlongNs = -alongMps * loadKg;
            }
            else
            {
                // A brake can only oppose the roll, never push through zero into reverse. Signed off the
                // road speed rather than the rotation, so a wheel the brake has already stopped still
                // drags the car down instead of quietly asking for nothing.
                var pedalMps = (rear ? rearDriveMps2 : frontDriveMps2) * dtS
                    - MathF.Sign(alongMps) * MathF.Min(command.BrakeMps2 * dtS, MathF.Abs(alongMps));

                // Guard 1: whatever daylight is already open between the tread and the road, less the
                // hair of it this model puts there itself (see the class remarks).
                var carriedMps = spin - alongMps;
                carriedMps = MathF.Sign(carriedMps) * MathF.Max(MathF.Abs(carriedMps) - (alongGripMps2 * dtS), 0f);

                // A gripping wheel transmits the pedals exactly as asked, at the corner's mass. Closing
                // the daylight is the rim's business, and the rim is a fraction of the car's mass, so a
                // spun-up or locked-up wheel is wound back over a few ticks rather than snatching.
                alongSlipAskMps = carriedMps + pedalMps;
                wantAlongNs = pedalMps * loadKg + (spin - alongMps) * SyncMassKg(config, loadKg);
            }

            // The friction ellipse: each axis' slip measured against what that axis can absorb in a
            // tick. Past the ellipse the tyre slides and gets the boundary in the direction it is
            // losing grip; inside it, it holds and is handed what it asked for.
            var askedAlongNs = wantAlongNs;
            var acrossBudgetNs = acrossGripMps2 * loadKg * dtS;
            var alongBudgetNs = alongGripMps2 * loadKg * dtS;
            var tyreNs = Vector2.Zero;
            var sliding = false;
            if (acrossBudgetNs > 0f && alongBudgetNs > 0f)
            {
                var alongShare = alongSlipAskMps / (alongGripMps2 * dtS);
                var acrossShare = acrossSlipAskMps / (acrossGripMps2 * dtS);
                var demand = MathF.Sqrt(alongShare * alongShare + acrossShare * acrossShare);
                sliding = demand > 1f;
                if (sliding)
                {
                    // Guard 3: a tyre asked for less than it could give does not start pushing harder
                    // because the other axis has saturated, which is the normal state of a corner.
                    wantAlongNs = NoLargerThan(alongBudgetNs * alongShare / demand, wantAlongNs);
                    wantAcrossNs = NoLargerThan(acrossBudgetNs * acrossShare / demand, wantAcrossNs);
                }

                tyreNs = wheelForward * wantAlongNs + wheelRight * wantAcrossNs;
            }
            else
            {
                wantAlongNs = 0f;
                wantAcrossNs = 0f;
            }

            // The rolling resistance is spent outside the traction budget, but it is still a force on
            // this corner of the car, and a wheel that is rolling has to follow the road it slows down.
            var dragNs = RollingDrag(velocityMps, wheelForward, alongMps, loadKg, surface.DragMps2, locked, dtS);

            var alongAfterMps = loadKg > 0f ? alongMps + (wantAlongNs + Vector2.Dot(dragNs, wheelForward)) / loadKg : alongMps;
            var acrossAfterMps = loadKg > 0f ? acrossMps + wantAcrossNs / loadKg : acrossMps;
            if (!locked && loadKg > 0f)
            {
                spinMps[wheel] = Rim(
                    config, spin, alongMps, alongAfterMps, wantAlongNs, askedAlongNs, sliding, loadKg,
                    (rear ? rearDriveMps2 : frontDriveMps2), command.BrakeMps2, spinAllowanceMps, dtS);
            }

            into[wheel] = new WheelImpulse(tyreNs + dragNs, atM[wheel]);
            scrub[wheel] = Scrub(
                config, surface, wantAlongNs, wantAcrossNs, dragNs, velocityMps, spinMps[wheel] - alongAfterMps,
                acrossAfterMps, spinMps[wheel], sliding, loadKg, dtS);
        }
    }

    /// <summary>
    /// The rim's own equation, in the order the three act on it: the engine spins it up, the road drags
    /// it by exactly what the patch put down, and the brake takes what it can of whatever is left
    /// turning. All three are violent here, the rim being a fraction of the car's mass. The brake comes
    /// last because it <em>holds</em>: a wheel it has stopped stays stopped against the tyre trying to
    /// wind it back up, which is the difference between a locked wheel and a merely braked one.
    /// </summary>
    static float Rim(
        SimConfig config, float spinMps, float alongMps, float alongAfterMps, float wantAlongNs, float askedAlongNs,
        bool sliding, float loadKg, float driveMps2, float brakeMps2, float spinAllowanceMps, float dtS)
    {
        var rotatingKg = config.Tyre.WheelRotatingMassKg;
        var pedalledMps = spinMps + (driveMps2 * loadKg / rotatingKg * dtS);
        var freeMps = pedalledMps - (wantAlongNs / rotatingKg);

        // Guard 2: where the patch alone would carry the rim past the road speed it is converging on,
        // the crossing is the answer. A pedal may still take it past on purpose — wheelspin and lock-up.
        if ((pedalledMps - alongAfterMps) * (freeMps - alongAfterMps) < 0f) freeMps = alongAfterMps;

        if (brakeMps2 > 0f)
        {
            freeMps -= MathF.Sign(freeMps) * MathF.Min(brakeMps2 * loadKg / rotatingKg * dtS, MathF.Abs(freeMps));
        }

        // The gearing ties a driven wheel to the car under it, so the tread may outrun the ground by
        // the allowance and no more — enough to be a burnout, not enough to be a free-running wheel.
        var capMps = MathF.Abs(alongMps) + spinAllowanceMps;
        var spunMps = Math.Clamp(freeMps, -capMps, capMps);

        // While the patch can take what it is asked for along its roll the wheel is not slipping: it
        // rolls with the road, and that constraint replaces the integration above.
        return !sliding || MathF.Abs(askedAlongNs) <= MathF.Abs(wantAlongNs) + (AlongSlipEpsilonMps * loadKg)
            ? alongAfterMps
            : spunMps;
    }

    /// <summary>
    /// What of the wheel's two slips is a slide rather than the working creep a tyre holds the ground
    /// with, and what that slide is worth as friction power per kg.
    /// </summary>
    /// <remarks>
    /// The sideways allowance is a slip <em>angle</em>, so it is worth whatever the wheel is rolling
    /// at times the angle the tyre works at: a tyre turning under a car at speed carries metres a
    /// second of it and a wheel barely turning carries almost none. That is what tells a corner from a
    /// car being shoved sideways across the road — the second is not cornering on anything, and
    /// neither is a locked wheel, whose rotation is zero by definition.
    /// </remarks>
    static TyreScrub Scrub(
        SimConfig config, in SurfaceUnderWheel surface, float wantAlongNs, float wantAcrossNs, Vector2 dragNs,
        Vector2 velocityMps, float alongSlipMps, float acrossSlipMps, float spinMps, bool sliding, float loadKg, float dtS)
    {
        var perKg = loadKg * dtS;
        if (perKg <= 0f) return default;

        var alongSlideMps = MathF.Max(MathF.Abs(alongSlipMps) - config.Marks.SlipMps, 0f);
        var creepMps = MathF.Max(
            MathF.Min(config.Marks.CorneringSlipMps, MathF.Abs(spinMps) * WorkingSlipTangent), config.Marks.SlipMps);
        var acrossSlideMps = MathF.Max(MathF.Abs(acrossSlipMps) - creepMps, 0f);

        // Only ground that can be displaced is ever asked what a merely rolling wheel did to it, and
        // both of the lengths that answer it are taken here and nowhere else — so on the tarmac the
        // whole of the plough is skipped rather than computed and thrown away.
        var ploughPowerM2S3 = 0f;
        var ploughing = false;
        if (surface.Ploughs)
        {
            var patchSpeedMps = velocityMps.Length();
            ploughPowerM2S3 = dragNs.Length() / perKg * patchSpeedMps;
            // Below the crawl the wheel is standing on the ground rather than crossing it, and standing
            // on grass ploughs nothing.
            ploughing = patchSpeedMps > config.Marks.PloughCrawlMps;
        }

        // Each axis' delivered force spent against the speed that axis is dragging at: a hard brake
        // with a touch of slip sideways is not scouring with all of its force, and a tyre that holds
        // has nothing left over either way and writes nothing — which is the ordinary case, and the
        // one the hypotenuse below is not worth taking for. Only the pair of exact zeros is short-cut,
        // and not the one-axis case: <c>sqrt(x²)</c> is not <c>x</c> to the last bit, and a tyre model
        // that rounds differently on a Tuesday is a town that diverges from the one measured.
        var slideSpeedMps = alongSlideMps == 0f && acrossSlideMps == 0f
            ? 0f
            : MathF.Sqrt((alongSlideMps * alongSlideMps) + (acrossSlideMps * acrossSlideMps));

        return new TyreScrub(
            ((MathF.Abs(wantAlongNs) * alongSlideMps) + (MathF.Abs(wantAcrossNs) * acrossSlideMps)) / perKg,
            ploughPowerM2S3,
            slideSpeedMps,
            ploughing,
            sliding);
    }

    /// <summary>
    /// Resistance to the wheel simply going round: it opposes the roll and nothing else, so a rolling
    /// tyre is free to be carried sideways by its own grip alone. A locked wheel has no roll direction
    /// left, so its rubber drags against whatever way it is being shoved. Capped at the motion it is
    /// opposing, so it can never push the wheel backwards.
    /// </summary>
    static Vector2 RollingDrag(
        Vector2 velocityMps, Vector2 wheelForward, float alongMps, float loadKg, float dragMps2, bool locked, float dtS)
    {
        if (dragMps2 <= 0f) return Vector2.Zero;

        if (!locked)
        {
            return wheelForward * -MathF.Sign(alongMps) *
                MathF.Min(dragMps2 * loadKg * dtS, MathF.Abs(alongMps) * loadKg);
        }

        var speedMps = velocityMps.Length();
        return speedMps <= 0f
            ? Vector2.Zero
            : -velocityMps / speedMps * MathF.Min(dragMps2 * loadKg * dtS, speedMps * loadKg);
    }

    /// <summary>One figure held down to the size of another, keeping its own sign — what the tyre delivers on an axis against what was asked of it, and what an axle is asked for against what it may be.</summary>
    static float NoLargerThan(float value, float most) =>
        MathF.Sign(value) * MathF.Min(MathF.Abs(value), MathF.Abs(most));

    /// <summary>
    /// The mass a slipping wheel and the car it is under pull each other round through — the two
    /// inertias in series, which is nearly all rim, so resynchronising costs the car little and the
    /// wheel everything.
    /// </summary>
    static float SyncMassKg(SimConfig config, float loadKg) =>
        loadKg <= 0f ? 0f : 1f / ((1f / config.Tyre.WheelRotatingMassKg) + (1f / loadKg));

    /// <summary>
    /// <b>What the ellipse has left along the roll once the corner the car is taking has been paid for</b>
    /// (CAR-3b): the most a driven axle may be asked for. Past it the engine buys no acceleration and only
    /// takes grip off the turn, which is the whole of why a car under power runs wide.
    /// </summary>
    /// <param name="acrossMps2">
    /// The lateral acceleration the body is <em>actually</em> carrying and not the one a profile planned —
    /// so a car shoved sideways lifts for it the way it lifts for a bend it steered into.
    /// </param>
    public static float DriveLeftMps2(float longGripMps2, float acrossGripMps2, float acrossMps2)
    {
        if (acrossGripMps2 <= 0f) return 0f;

        var spent = MathF.Abs(acrossMps2) / acrossGripMps2;
        return longGripMps2 * MathF.Sqrt(MathF.Max(0f, 1f - (spent * spent)));
    }

    /// <summary>
    /// How far the tread may outrun the road right now: the standing-start allowance, run down by the
    /// speed the car is already doing. It is the gearbox — the revs in hand to spin a wheel with are
    /// gone by the time the car is moving properly.
    /// </summary>
    public static float SpinAllowanceMps(SimConfig config, float alongMps) =>
        config.Tyre.WheelSpinFadeMps <= 0f
            ? config.Tyre.WheelSpinAllowanceMps
            : config.Tyre.WheelSpinAllowanceMps * Math.Clamp(1f - (MathF.Abs(alongMps) / config.Tyre.WheelSpinFadeMps), 0f, 1f);

    /// <summary>
    /// The slip angle a tyre does its cornering at, as a tangent: how much sideways creep every unit of
    /// rolling speed is worth before the patch counts as sliding rather than working. ≈ 17°, which at
    /// town speeds is the 1.5–4 m/s an ordinary turn actually carries.
    /// </summary>
    const float WorkingSlipTangent = 0.3f;

    /// <summary>
    /// How much of the demand along the roll may go unmet before the wheel counts as slipping rather
    /// than rolling: a hair, so a demand shaved by floating-point noise at the edge of the ellipse does
    /// not unstick a rolling wheel.
    /// </summary>
    const float AlongSlipEpsilonMps = 0.01f;

    static float DriveShare(in DriveCommand command, float share, float axleLoad) =>
        axleLoad > 1e-3f ? command.ThrottleMps2 * command.GearSign * share / axleLoad : 0f;
}
