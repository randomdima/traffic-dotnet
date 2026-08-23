using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Foot;

namespace TrafficSimulation.World.Town;

/// <summary>Where a car's command becomes four contact patches: the pose they all read, what each one spends, and what it leaves on the ground.</summary>
internal sealed partial class TownWorld
{
    CarPose PoseOf(int car) => new(
        Cars.PositionM[car], Cars.HeadingRad[car], Cars.VelocityMps[car], Cars.YawRateRadPerS[car],
        Cars.MassKg[car], Cars.AccelerationMps2[car]);

    /// <summary>
    /// One car's four patches for this tick: the ground under each of them, the impulses they spend,
    /// the rims wound on, and what each wheel wrote on the ground it stood on.
    /// </summary>
    /// <remarks>
    /// The terrain is sampled under each wheel and never under the car: a car with two wheels off the
    /// road grips asymmetrically, and that asymmetry is a yaw the solver produces out of four impulses
    /// at four places rather than anything decided here.
    /// </remarks>
    void Tyres(int car, in CarPose pose)
    {
        var command = Cars.Command[car];
        // Taken before the standstill is answered, because a car parked mid-slide still has a throttle
        // to recover: what it may ask for when it pulls away again is not a thing that happens only
        // while the wheels are turning.
        var ceilingMps2 = DriveCeilingMps2(car);
        if (AtRest(car, pose, command))
        {
            Rest(car);
            return;
        }

        var ground = _wheels.GroundUnder(car);
        Span<Vector2> atM = stackalloc Vector2[TyreModel.Wheels];
        TyreModel.WheelPointsM(_config, pose, atM);
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var effect = _terrain.EffectAt(atM[wheel]);
            ground[wheel] = new SurfaceUnderWheel(
                effect.Coefficient, effect.DragMps2, _config.Marks.PowerM2S3 * effect.MarkFactor, effect.Ploughs);
        }

        var scrub = _wheels.ScrubOf(car);
        var drivenFrontShare = Cars.DrivenFrontShare[car];
        TyreModel.Step(
            _config, pose, command, drivenFrontShare, ceilingMps2, atM, ground,
            Cars.WheelSpinOf(car), _config.TickSeconds,
            _wheels.ImpulsesOf(car), scrub);

        // A wheel the engine is turning that has gone past what it can put down is what the throttle
        // lifts for next tick. An undriven wheel sliding is a corner, not a wheel being over-asked.
        var onThePedal = command.ThrottleMps2 > 0f;
        var slipping = false;
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            slipping |= scrub[wheel].Sliding && onThePedal && IsDriven(drivenFrontShare, wheel);
            RollTread(car, wheel);
            LayMark(car, wheel, atM[wheel], ground[wheel], scrub[wheel]);
        }

        Cars.DrivenSlipping[car] = slipping;
    }

    /// <summary>
    /// Whether this car's tyres have nothing whatever to do this tick, which is the state most of the
    /// town's cars are in most of the time: a body at a dead standstill, nobody on the throttle, and
    /// four wheels holding nothing over.
    /// </summary>
    /// <remarks>
    /// Every figure the model would produce here is exactly zero: a patch with no velocity under it has
    /// no slip on either axis, the pedals ask for nothing, the drag opposes a motion of zero, and a rim
    /// at rest is left where it is. A skipped calculation, not a changed one — safe only because nothing
    /// sleeps in this solver and an impulse of nothing is never handed to it anyway
    /// (<c>PhysicsWorld.ApplyImpulseAt</c>).
    /// </remarks>
    bool AtRest(int car, in CarPose pose, in DriveCommand command) =>
        pose.VelocityMps == Vector2.Zero && pose.YawRateRadPerS == 0f && command.ThrottleMps2 == 0f &&
        Cars.WheelsAtRest(car);

    /// <summary>
    /// What a car at a standstill leaves behind it: nothing at all. The impulses are cleared rather than
    /// left, because the four from the tick it stopped on would otherwise be spent again every tick.
    /// </summary>
    void Rest(int car)
    {
        _wheels.Clear(car);
        Cars.DrivenSlipping[car] = false;
    }

    /// <summary>
    /// The most a driven axle of this car may be asked for. A car working its own pedals stops at what
    /// the patch can transmit — past it the engine buys no acceleration and only lays rubber — and lifts
    /// further while its tyres report a slide, which is the difference between a rear-driven car that
    /// turns at speed and one that does not. A hand at the wheel gets none of it.
    /// </summary>
    float DriveCeilingMps2(int car)
    {
        Cars.SlipThrottle[car] = Cars.DrivenSlipping[car]
            ? MathF.Max(Cars.SlipThrottle[car] - (_config.TickSeconds / _config.Tyre.SlipBackOffS), _config.Tyre.MinSlipThrottleFraction)
            : MathF.Min(Cars.SlipThrottle[car] + (_config.TickSeconds / _config.Tyre.SlipRecoverS), 1f);

        return Handed(Roster.AgentOfCar(car))
            ? float.PositiveInfinity
            : _config.Tyre.GripMps2 * _config.Tyre.LongAxisFactor * Cars.GroundCoefficient[car]
                * _config.Tyre.TractionThrottleFraction * Cars.SlipThrottle[car];
    }

    /// <summary>The hardest the tyres could have pushed the body, which is where a manoeuvre stops and a collision begins.</summary>
    float LoadTransferCapMps2 =>
        _config.Tyre.GripMps2 * _config.Tyre.LongAxisFactor * _config.Tyre.LoadTransferInGrips;

    static Vector2 Limit(Vector2 v, float most)
    {
        var length = v.Length();
        return length <= most || length <= 0f ? v : v / length * most;
    }

    /// <summary>Whether the engine is turning this wheel at all, which is what makes a slide worth lifting for.</summary>
    static bool IsDriven(float drivenFrontShare, int wheel) =>
        wheel < 2 ? drivenFrontShare > 0f : drivenFrontShare < 1f;

    /// <summary>
    /// Scroll one tyre's tread by the distance <em>that wheel</em> turned through this tick — its own
    /// rotation and never the car's speed, so a car on its handbrake slides with its tread standing
    /// still and a wheel spinning under a standing car scrolls at the speed the engine is turning it.
    /// </summary>
    void RollTread(int car, int wheel)
    {
        var at = (car * TyreModel.Wheels) + wheel;
        var scrollMps = Cars.WheelSpinMps[at] * _config.Tyre.TreadScrollFactor;
        if (MathF.Abs(scrollMps * _config.TickSeconds) < TreadStillM) return;

        Cars.TreadPhaseM[at] = TyreModel.TreadPhaseM(
            Cars.TreadPhaseM[at], scrollMps, _config.Tyre.TreadPitchM, _config.TickSeconds);
    }

    /// <summary>
    /// Below this much scroll in a tick a tread is standing still as far as the drawing is concerned:
    /// about a thousandth of the pattern's own pitch, which no pixel on any screen can hold apart.
    /// It is worth a branch because a town's parked cars are <em>all</em> of them, four wheels
    /// each, and the phase is drawing state that nothing in the simulation reads.
    /// </summary>
    const float TreadStillM = 1e-4f;

    /// <summary>
    /// One wheel's mark for this tick. Nothing is laid until the wheel has travelled a whole segment's
    /// worth, so a mark is a chain of quads along the path the tyre took rather than one stamp per
    /// tick; when the wheel stops working the ground, whatever stretch was in hand is closed off.
    /// </summary>
    void LayMark(int car, int wheel, Vector2 atM, in SurfaceUnderWheel surface, in TyreScrub scrub)
    {
        var at = (car * TyreModel.Wheels) + wheel;

        // A wheel neither sliding nor ploughing, with nothing banked and no stretch open, cannot write
        // and has nothing to close off: the whole of the rest of this is arithmetic over zeros, and it
        // is what every parked car in the town would otherwise pay four times a tick.
        if (scrub.SlideSpeedMps <= 0f && !scrub.Ploughing && Cars.ScrubTravelM[at] <= 0f && !Cars.Marking[at]) return;

        Cars.ScrubTravelM[at] = TyreModel.ScrubTravelM(_config, Cars.ScrubTravelM[at], scrub.SlideSpeedMps, _config.TickSeconds);

        var intensity = TyreModel.GroundMarkIntensity(_config, surface, scrub, Cars.ScrubTravelM[at]);

        if (intensity <= 0f)
        {
            if (Cars.Marking[at]) Marks.Mark(Cars.MarkFromM[at], atM, _config.Tyre.WheelWidthM, Cars.MarkIntensity[at], surface.Ploughs);
            Cars.Marking[at] = false;
            return;
        }

        if (!Cars.Marking[at])
        {
            Cars.MarkFromM[at] = atM;
        }
        else if (Vector2.DistanceSquared(Cars.MarkFromM[at], atM) >= _config.Marks.SpacingM * _config.Marks.SpacingM)
        {
            Marks.Mark(Cars.MarkFromM[at], atM, _config.Tyre.WheelWidthM, intensity, surface.Ploughs);
            Cars.MarkFromM[at] = atM;
        }

        Cars.Marking[at] = true;
        Cars.MarkIntensity[at] = intensity;
    }
}
