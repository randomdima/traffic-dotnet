using System.Numerics;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// <b>A car on the back of another one</b> (EVA-5): where the two bodies are coupled and the axle the towed car is
/// left rolling on. The coupling itself is the solver's link (SOL-38), a pin between the points named here.
/// </summary>
/// <remarks>
/// <para>
/// <b>The arm is hinged on the tractor's deck and clamped to the car it has lifted</b> — it swings freely at
/// one end and not at all at the other, which is why it is the one part of a vehicle in this town drawn as a
/// picture of its own: it moves against the body it is bolted to, and where it points is what the tow is
/// doing. What that clamp is, in terms of a pin free to turn, is <see cref="EyeM"/>.
/// </para>
/// <para>
/// <b>And what holds the towed car in line is not the arm at all</b>: it is <see cref="Step"/>, exactly as
/// a trailer is kept straight by its own axle and not by its hitch.
/// </para>
/// </remarks>
internal static class TowBar
{
    /// <summary>
    /// <b>The hinge the arm turns about</b>, which is a place on the tractor's own deck and not a point in
    /// the air behind it: the arm swings there and the reaction to every newton the tow spends lands there
    /// (<see cref="CarBuild.TowHingeAheadOfCentreM"/>).
    /// </summary>
    public static Vector2 HookM(in CarBuild tractor, Vector2 positionM, Vector2 forward) =>
        positionM + (forward * tractor.TowHingeAheadOfCentreM);

    public static Vector2 HookM(in CarBuild tractor, in CarPose pose) => HookM(tractor, pose.PositionM, pose.Forward);

    /// <summary>
    /// <b>Which way along a towed car's own axis the fork sits</b>: forward when the arm has it by the nose,
    /// backward when it has it by the tail. <b>Everything else about a pair is this one sign</b> — where it
    /// is held, which two wheels are still on the ground, and how far behind the truck it stands (EVA-5).
    /// </summary>
    public static float EndSign(bool byTheTail) => byTheTail ? -1f : 1f;

    /// <summary>
    /// <b>Where the fork takes hold of the car being pulled: just inside whichever end it caught</b>
    /// (<see cref="CarBuild.TowGripFromTheMiddleM"/>). That is what a recovery hitch goes under — that end of
    /// the car up on the fork and its wheels off the ground — so it is where the arm holds the car and where
    /// the picture puts the fork (EVA-5).
    /// </summary>
    public static Vector2 ForkM(in CarBuild towed, Vector2 positionM, Vector2 forward, bool byTheTail) =>
        positionM + (forward * EndSign(byTheTail) * towed.TowGripFromTheMiddleM);

    public static Vector2 ForkM(in CarBuild towed, in CarPose pose, bool byTheTail) =>
        ForkM(towed, pose.PositionM, pose.Forward, byTheTail);

    /// <summary>
    /// <b>And the point on the towed car the coupling actually holds</b>: a whole reach further along its own
    /// centre line, which is where the hinge stands when the arm is straight. <b>It is a point ahead of the
    /// car and not on it</b>, and that is the whole of what makes a tow track rather than crab — the fork
    /// clamps the front it has lifted, so the arm cannot pivot against the car it has hold of, and pinning a
    /// point that far up its own axis to the hinge is what says so with a pin that turns freely (SOL-38).
    /// </summary>
    /// <remarks>
    /// Held at the fork instead, the pair has a hinge at each end and nothing but the trailer's back tyres
    /// deciding which way it points: measured over a straight street, the wreck settled a third of a
    /// right angle off the truck's line and stayed there, which is a tow being dragged sideways.
    /// </remarks>
    public static Vector2 EyeM(in CarBuild towed, Vector2 positionM, Vector2 forward, float reachM, bool byTheTail) =>
        positionM + (forward * EndSign(byTheTail) * (towed.TowGripFromTheMiddleM + reachM));

    public static Vector2 EyeM(in CarBuild towed, in CarPose pose, float reachM, bool byTheTail) =>
        EyeM(towed, pose.PositionM, pose.Forward, reachM, byTheTail);

    /// <summary>The hinge in the tractor's own frame, which is where the solver's pin holds it (SOL-38).</summary>
    public static Vector2 HookOnTheBodyM(in CarBuild tractor) => new(tractor.TowHingeAheadOfCentreM, 0f);

    /// <summary>And the eye in the towed car's own frame, the pin's other end (<see cref="EyeM(in CarBuild, Vector2, Vector2, float, bool)"/>).</summary>
    public static Vector2 EyeOnTheBodyM(in CarBuild towed, float reachM, bool byTheTail) =>
        EyeOnTheBodyM(towed.TowGripFromTheMiddleM, reachM, byTheTail);

    /// <summary>
    /// The eye in any towed body's own frame, off how far from its middle the fork takes hold — a rail vehicle's as much
    /// as a car's, since the arm holds whatever it caught the same way.
    /// </summary>
    public static Vector2 EyeOnTheBodyM(float gripFromTheMiddleM, float reachM, bool byTheTail) =>
        new(EndSign(byTheTail) * (gripFromTheMiddleM + reachM), 0f);

    /// <summary>
    /// <b>How far from a body's middle the fork takes hold</b>: a fixed distance inside either end of it, whatever the body
    /// (<see cref="CarBuild.TowGripFromTheMiddleM"/>).
    /// </summary>
    public static float GripFromTheMiddleM(float lengthM, float insideTheEndM) => (lengthM * 0.5f) - insideTheEndM;

    /// <summary>Where along a towed body's own axis the fork is, from its middle and forward positive.</summary>
    public static float ForkAlongM(float gripFromTheMiddleM, bool byTheTail) => EndSign(byTheTail) * gripFromTheMiddleM;

    /// <summary>
    /// <b>Where a car on the arm stands</b>: how far behind the tractor's middle its own middle is when the
    /// arm is straight and the coupling under no stretch (EVA-5). It is the same distance whichever end the
    /// fork caught, because the fork is the same distance inside either one.
    /// </summary>
    public static float SetDownBehindM(in CarBuild tractor, in CarBuild towed) =>
        -tractor.TowHingeAheadOfCentreM + tractor.TowReachM + towed.TowGripFromTheMiddleM;

    /// <summary>
    /// <b>The trailer wheels</b>: what the wheels of the axle on the ground (<paramref name="axle"/>) do with nothing
    /// driving them and nothing braking them, which is hold sideways as hard as the ground lets them and give the roll
    /// back the rolling resistance; every other wheel is up on the arm and spends nothing. That lateral hold is what
    /// makes a towed car track the one pulling it rather than swing about behind the bar.
    /// </summary>
    /// <param name="atM">Where each of its wheels stands (<see cref="TyreModel.WheelPointsM"/>).</param>
    /// <param name="onTheAxleShare">
    /// How much of the towed car's weight is still on that axle rather than on the hook. Lifting the
    /// nose is the whole of what a recovery hitch does, and this is that lift expressed as the only thing a
    /// flat world can express it as: the load these patches grip with.
    /// </param>
    /// <remarks>
    /// It is deliberately not <see cref="TyreModel.Step"/> with the pedals set to nothing. That model
    /// carries a rim, a slip budget split between two axes, an engine and a brake, and a towed wreck has
    /// none of them — what is left when they are taken out is the lines below.
    /// </remarks>
    public static void Step(
        in CarBuild towed, in CarPose pose, int axle, ReadOnlySpan<Vector2> atM, ReadOnlySpan<SurfaceUnderWheel> ground,
        float onTheAxleShare, float dtS, Span<WheelImpulse> into)
    {
        var forward = pose.Forward;
        var right = pose.Right;
        var down = 0;
        for (var wheel = 0; wheel < towed.WheelCount; wheel++) down += towed.WheelAt(wheel).Axle == axle ? 1 : 0;

        var loadKg = pose.MassKg * onTheAxleShare / down;
        for (var wheel = 0; wheel < towed.WheelCount; wheel++)
        {
            if (towed.WheelAt(wheel).Axle != axle)
            {
                into[wheel] = new WheelImpulse(Vector2.Zero, atM[wheel]);
                continue;
            }

            var fromCentreM = atM[wheel] - pose.PositionM;
            var velocityMps = pose.VelocityMps + (pose.YawRateRadPerS * new Vector2(-fromCentreM.Y, fromCentreM.X));
            var surface = ground[wheel];
            var alongMps = Vector2.Dot(velocityMps, forward);
            var acrossMps = Vector2.Dot(velocityMps, right);

            // The patch holds what it can of the sideways slip and slides where it cannot — the friction
            // ellipse with one axis unused, which is what an unpowered, unbraked wheel is.
            var wantAcrossNs = -acrossMps * loadKg;
            var budgetNs = towed.GripMps2 * surface.Coefficient * loadKg * dtS;
            var acrossNs = MathF.Sign(wantAcrossNs) * MathF.Min(MathF.Abs(wantAcrossNs), budgetNs);

            // And opposes its own roll by the ground's drag, capped at the motion it is opposing so it can
            // never push the wheel backwards.
            var dragNs = surface.DragMps2 <= 0f
                ? 0f
                : -MathF.Sign(alongMps) * MathF.Min(surface.DragMps2 * loadKg * dtS, MathF.Abs(alongMps) * loadKg);

            into[wheel] = new WheelImpulse((right * acrossNs) + (forward * dragNs), atM[wheel]);
        }
    }

    /// <summary>
    /// The axle a towed car is left rolling on, which is always <b>the far one from the fork</b>: the end on
    /// the arm is the end in the air. A car caught by the tail therefore rolls on its own frontmost axle — its steered
    /// pair, which is why the wheels of anything put on the bar are straightened first (EVA-5). A car held at both
    /// ends has no axle down at all, and is not asked this.
    /// </summary>
    public static int AxleOnTheGround(in CarBuild towed, bool byTheTail)
    {
        var at = 0;
        for (var wheel = 1; wheel < towed.WheelCount; wheel++)
        {
            var aheadM = towed.WheelAt(wheel).AtM.X - towed.WheelAt(at).AtM.X;
            if (byTheTail ? aheadM > 0f : aheadM < 0f) at = wheel;
        }

        return (int)towed.WheelAt(at).Axle;
    }
}
