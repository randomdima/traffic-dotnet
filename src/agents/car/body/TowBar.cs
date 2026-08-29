using System.Numerics;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// <b>One end of the bar</b>: the point it is anchored at, how fast that point is moving, how far it
/// stands off its own body's centre, and what an impulse there is worth against that body's mass and
/// inertia. Gathered because a coupling is two of these and nothing else.
/// </summary>
internal readonly record struct HookEnd(
    Vector2 AtM, Vector2 VelocityMps, Vector2 ArmM, float InverseKg, float InverseInertia);

/// <summary>
/// <b>A car on the back of another one</b> (EVA-5): where the two bodies are coupled, what the arm between
/// them spends, and the two wheels the towed car is left rolling on. Nothing here touches a body, so the
/// whole of the coupling is judged against arithmetic.
/// </summary>
/// <remarks>
/// <para>
/// <b>The arm is hinged on the tractor's deck and clamped to the car it has lifted</b> — it swings freely at
/// one end and not at all at the other, which is why it is the one part of a vehicle in this town drawn as a
/// picture of its own: it moves against the body it is bolted to, and where it points is what the tow is
/// doing. What that clamp is, in the only terms this engine has, is <see cref="EyeM"/>.
/// </para>
/// <para>
/// <b>It is the tyre model's shape and not a new kind of physics.</b> A tow is two more impulses at two
/// more points — the only actuation this engine has (SOL-3) — spent in the same phase the four patches
/// are, so the solver sees a coupled pair as it sees a car: a body with forces at places on it. There is
/// no constraint row, no joint in the solver and no second integrator.
/// </para>
/// <para>
/// <b>It is a stiff coupling and not a rigid one, and that is a rule rather than a shortcoming.</b> What it
/// asks for is priced at what an impulse at those two points is actually worth (<see cref="EffectiveKg"/>)
/// and then held down to what a coupling may spend — hard along the drawbar, much harder across it. A turn
/// taken tighter than the trailer can follow therefore stretches the bar and scrubs the trailer round,
/// which is what a real one does at walking pace; the alternative is a coupling with the authority to spin
/// the vehicle doing the pulling off its own road, which is what an uncapped one did.
/// </para>
/// <para>
/// <b>And what holds the towed car in line is not the bar at all</b>: it is <see cref="Step"/>, exactly as
/// a trailer is kept straight by its own axle and not by its hitch.
/// </para>
/// </remarks>
internal static class TowBar
{
    /// <summary>The wheels a towed car is left standing on: its own back pair, and no others.</summary>
    public const int Wheels = 2;

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
    /// clamps the front it has lifted, so the arm cannot pivot against the car it has hold of, and holding a
    /// point that far up its own axis is what says so in the only terms this engine has (SOL-3).
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

    /// <summary>
    /// <b>Where a car on the arm stands</b>: how far behind the tractor's middle its own middle is when the
    /// arm is straight and the coupling under no stretch (EVA-5). It is the same distance whichever end the
    /// fork caught, because the fork is the same distance inside either one.
    /// </summary>
    public static float SetDownBehindM(in CarBuild tractor, in CarBuild towed) =>
        -tractor.TowHingeAheadOfCentreM + tractor.TowReachM + towed.TowGripFromTheMiddleM;

    /// <summary>
    /// And how much further back than its own tail a tractor with this car on the arm reaches, which is the
    /// stretch of road the pair asks for as one movement (TER-5c.2).
    /// </summary>
    public static float BehindTheTailM(in CarBuild tractor, in CarBuild towed) =>
        SetDownBehindM(tractor, towed) + towed.HalfLengthM - tractor.HalfLengthM;

    /// <summary>
    /// <b>What the bar is worth this tick</b>, spent <em>on the towed car at its eye</em> and taken back
    /// from the tractor at its hook. One impulse and its opposite: a coupling adds no momentum to the pair,
    /// so whatever hauls the wreck forward slows the vehicle hauling it by exactly as much.
    /// </summary>
    /// <param name="tractor">The vehicle pulling: where the hook is, how fast that point is going, and what an impulse there is worth.</param>
    /// <param name="towed">And the same three of the car on the hook, taken at its eye.</param>
    /// <param name="settleS">
    /// How long the bar is given to pull its own stretch out, which is the whole of its stiffness. Shorter
    /// is more rigid; nothing here divides by it without a caller having set it.
    /// </param>
    /// <param name="mostMps2">
    /// The ceiling on what it may spend along the drawbar, as an acceleration on the car being pulled — a
    /// ceiling and never a target, so an ordinary tow is priced by the drift and only a violent one meets it.
    /// </param>
    /// <param name="sideShare">And what share of that it may spend across the drawbar, which is much less.</param>
    /// <remarks>
    /// <b>Priced along two axes and not as one number</b>, because what an impulse at a point buys depends
    /// on which way it points: pulled along the drawbar it fights both masses, pulled across it it also has
    /// to turn both bodies, and the second is several times heavier than the first. Priced on the masses
    /// alone the sideways half overshoots by whatever the yaw would have absorbed, and the pair spends the
    /// tow snaking — which is what the whole of this decomposition is for.
    /// </remarks>
    public static Vector2 PullNs(
        in HookEnd tractor, in HookEnd towed, float settleS, float mostMps2, float sideShare, float towedKg,
        float dtS)
    {
        if (settleS <= 0f) return Vector2.Zero;

        // Where the held point has to get to, as a speed: the stretch pulled out over the settle interval,
        // less whatever the two ends are already doing to each other. An arm under no stretch and closing at
        // nothing asks for nothing, which is the state a steady tow spends its time in.
        var alongTheBarM = tractor.AtM - towed.AtM;
        var wantMps = (alongTheBarM / settleS) - (towed.VelocityMps - tractor.VelocityMps);

        var along = alongTheBarM.LengthSquared() > 0f
            ? Vector2.Normalize(alongTheBarM)
            : new Vector2(1f, 0f);
        var across = new Vector2(-along.Y, along.X);

        // <b>Capped along the bar and across it separately, and far harder across.</b> Along the drawbar an
        // impulse only has to move two masses; across it, it has to turn two bodies through moment arms of
        // a couple of metres each, so the same number of newtons buys several times the yaw — and the yaw it
        // buys lands on the vehicle doing the pulling, which is the one that has a line to hold. Left on one
        // budget, a turn taken at walking pace spun the tractor off its own road inside two seconds.
        var alongNs = Held(
            along * Vector2.Dot(wantMps, along) * EffectiveKg(tractor, towed, along), mostMps2 * towedKg * dtS);
        var acrossNs = Held(
            across * Vector2.Dot(wantMps, across) * EffectiveKg(tractor, towed, across),
            mostMps2 * sideShare * towedKg * dtS);

        return alongNs + acrossNs;
    }

    /// <summary>One half of the coupling's answer held down to what it may spend, keeping its direction.</summary>
    static Vector2 Held(Vector2 impulseNs, float mostNs)
    {
        var lengthNs = impulseNs.Length();
        return lengthNs <= mostNs || lengthNs <= 0f ? impulseNs : impulseNs / lengthNs * mostNs;
    }

    /// <summary>
    /// <b>What the pair actually weighs to an impulse in this direction at these two points</b> — the two
    /// masses and the two inertias, each through its own moment arm. It is the contact solver's own
    /// arithmetic said of a coupling, because it is the same question about the same two bodies.
    /// </summary>
    static float EffectiveKg(in HookEnd first, in HookEnd second, Vector2 direction)
    {
        var turnA = Cross(first.ArmM, direction);
        var turnB = Cross(second.ArmM, direction);
        var resistance = first.InverseKg + second.InverseKg
            + (first.InverseInertia * turnA * turnA) + (second.InverseInertia * turnB * turnB);

        return resistance > 0f ? 1f / resistance : 0f;
    }

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    /// <summary>
    /// Where the two wheels a towed car still stands on meet the ground — the axle at its far end from the
    /// fork, at the very offsets the four-wheeled model puts them at, because they are the same two wheels.
    /// </summary>
    public static void AxleM(in CarBuild towed, in CarPose pose, int pair, Span<Vector2> into)
    {
        for (var wheel = 0; wheel < Wheels; wheel++)
        {
            var atBody = TyreModel.WheelAtM(towed, pair + wheel);
            into[wheel] = pose.PositionM + (pose.Forward * atBody.X) + (pose.Right * atBody.Y);
        }
    }

    /// <summary>
    /// <b>The two trailer wheels</b>: what a pair of wheels with nothing driving them and nothing braking
    /// them does, which is hold sideways as hard as the ground lets them and give the roll back the rolling
    /// resistance. That lateral hold is what makes a towed car track the one pulling it rather than swing
    /// about behind the bar.
    /// </summary>
    /// <param name="onTheAxleShare">
    /// How much of the towed car's weight is still on these two wheels rather than on the hook. Lifting the
    /// nose is the whole of what a recovery hitch does, and this is that lift expressed as the only thing a
    /// flat world can express it as: the load these patches grip with.
    /// </param>
    /// <remarks>
    /// It is deliberately not <see cref="TyreModel.Step"/> with the pedals set to nothing. That model
    /// carries a rim, a slip budget split between two axes, an engine and a brake, and a towed wreck has
    /// none of them — what is left when they are taken out is the eight lines below, and running the whole
    /// model to reach them would be four patches of arithmetic to produce two.
    /// </remarks>
    public static void Step(
        in CarBuild towed, in CarPose pose, ReadOnlySpan<Vector2> atM, ReadOnlySpan<SurfaceUnderWheel> ground,
        float onTheAxleShare, float dtS, Span<WheelImpulse> into)
    {
        var forward = pose.Forward;
        var right = pose.Right;
        var loadKg = pose.MassKg * onTheAxleShare / Wheels;
        for (var wheel = 0; wheel < Wheels; wheel++)
        {
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

    /// <summary>Where each pair starts in the four the body carries — front right, front left, then the back two.</summary>
    public const int FrontPair = 0;

    public const int RearPair = 2;

    /// <summary>
    /// The pair a towed car is left rolling on, which is always <b>the far one from the fork</b>: the end on
    /// the arm is the end in the air. A car caught by the tail therefore rolls on its own steered pair, which
    /// is why the wheels of anything put on the bar are straightened first (EVA-5).
    /// </summary>
    public static int PairOnTheGround(bool byTheTail) => byTheTail ? FrontPair : RearPair;

    /// <summary>And the one that is up on it, whose patches are spending nothing at all.</summary>
    public static int PairInTheAir(bool byTheTail) => byTheTail ? RearPair : FrontPair;
}
