namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What one car, or one row of the pad, came to</b> — the three radii that can be put beside each other
/// and the two angles that say why they differ.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three radii and not two.</b> <see cref="AskedM"/> is the geometry, which is the truth only while
/// nothing is sliding; <see cref="HeldM"/> is the tightest circle this car's tyres could hold at the speed
/// it was doing, which is the truth once everything is; and <see cref="TurnedM"/> is what it did. A car
/// running wide of the first while sitting on the second is a car obeying its grip, and that is a
/// different finding from a car running wide of both.
/// </para>
/// <para>
/// <b><see cref="CentreAheadM"/> is the other half of the comparison and the half a ratio cannot carry.</b>
/// A radius says how big the circle is; this says where its centre is. The geometry puts it square abeam
/// the rear axle, and every metre it has moved forward of that is the back of the car travelling somewhere
/// its wheels are not pointed.
/// </para>
/// </remarks>
/// <param name="Cars">How many cars this is the mean of — one, for a single car's own figures.</param>
/// <param name="AskedM">The radius the axles ask for, from the middle of the rear axle (<c>TurnCircle</c>).</param>
/// <param name="GripM">
/// The radius this car's own lateral grip affords at the speed it was doing — <c>v² / GripMps2</c>, which
/// is a bound and not a target: a car below its grip is free to turn tighter than this.
/// </param>
/// <param name="HeldM">
/// And therefore the tightest circle it could actually be on: the wider of the two, since neither the
/// geometry nor the grip can be beaten.
/// </param>
/// <param name="CentreAheadM">
/// How far ahead of the rear axle the centre it is really turning about stands. Zero is Ackermann.
/// </param>
/// <param name="FrontSlipDeg">The angle between the front wheels and the ground they are actually crossing.</param>
/// <param name="AtACrawl">
/// What it turned against what its axles asked <b>while it was still slow enough for the two to have to
/// agree</b> — the launch, before the speed built. Zero where the car was never sampled that slowly.
/// </param>
internal readonly record struct SkidpadFigures(
    int Cars,
    float SpeedMps,
    float LateralG,
    float AskedM,
    float GripM,
    float HeldM,
    float TurnedM,
    float CentreAheadM,
    float FrontSlipDeg,
    float AtACrawl)
{
    /// <summary>How many times the circle its own axles asked for it actually turned.</summary>
    public float TimesAsked => AskedM > 0f ? TurnedM / AskedM : 0f;

    /// <summary>
    /// And how many times the tightest circle anything could have held at that speed — <b>the figure that
    /// says whether running wide is a fact about the tyre model or about the pad</b>. One means the car is
    /// on the limit its own grip sets, which is what a car asked for more lock than it has grip should do.
    /// </summary>
    public float TimesHeld => HeldM > 0f ? TurnedM / HeldM : 0f;
}
