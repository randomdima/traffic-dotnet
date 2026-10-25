namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// One tick's working set for the fleet's wheels, <see cref="TyreModel.MostWheels"/> of room a car: what each patch is
/// standing on, what it spends, and what it dragged. Laid once with the fleet and cleared per car, never allocated in a
/// tick.
/// </summary>
/// <remarks>
/// It does not survive the tick it was written in. What outlives a tick is on <see cref="CarFleet"/>;
/// what is here is the handover between the tyre model, the body step and the mark layer.
/// </remarks>
internal sealed class WheelScratch(int cars)
{
    const int Stride = TyreModel.MostWheels;

    readonly SurfaceUnderWheel[] _ground = new SurfaceUnderWheel[cars * Stride];
    readonly WheelImpulse[] _impulses = new WheelImpulse[cars * Stride];
    readonly TyreScrub[] _scrub = new TyreScrub[cars * Stride];

    /// <param name="wheels">How many wheels the car stands on (<see cref="CarBuild.WheelCount"/>).</param>
    public Span<SurfaceUnderWheel> GroundUnder(int car, int wheels) => OfCar(_ground, car, wheels);

    /// <inheritdoc cref="GroundUnder"/>
    public Span<WheelImpulse> ImpulsesOf(int car, int wheels) => OfCar(_impulses, car, wheels);

    /// <inheritdoc cref="GroundUnder"/>
    public Span<TyreScrub> ScrubOf(int car, int wheels) => OfCar(_scrub, car, wheels);

    /// <summary>A car with nobody at the pedals spends nothing and drags nothing.</summary>
    public void Clear(int car)
    {
        OfCar(_impulses, car, Stride).Clear();
        OfCar(_scrub, car, Stride).Clear();
    }

    static Span<T> OfCar<T>(T[] all, int car, int wheels) => all.AsSpan(car * Stride, wheels);
}
