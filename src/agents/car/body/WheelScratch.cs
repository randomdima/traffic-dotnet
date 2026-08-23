namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// One tick's working set for the fleet's wheels, four to a car: what each patch is standing on, what it
/// spends, and what it dragged. Laid once with the fleet and cleared per car, never allocated in a tick.
/// </summary>
/// <remarks>
/// It does not survive the tick it was written in. What outlives a tick is on <see cref="CarFleet"/>;
/// what is here is the handover between the tyre model, the body step and the mark layer.
/// </remarks>
internal sealed class WheelScratch(int cars)
{
    readonly SurfaceUnderWheel[] _ground = new SurfaceUnderWheel[cars * TyreModel.Wheels];
    readonly WheelImpulse[] _impulses = new WheelImpulse[cars * TyreModel.Wheels];
    readonly TyreScrub[] _scrub = new TyreScrub[cars * TyreModel.Wheels];

    public Span<SurfaceUnderWheel> GroundUnder(int car) => Four(_ground, car);

    public Span<WheelImpulse> ImpulsesOf(int car) => Four(_impulses, car);

    public Span<TyreScrub> ScrubOf(int car) => Four(_scrub, car);

    /// <summary>A car with nobody at the pedals spends nothing and drags nothing.</summary>
    public void Clear(int car)
    {
        ImpulsesOf(car).Clear();
        ScrubOf(car).Clear();
    }

    static Span<T> Four<T>(T[] all, int car) => all.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels);
}
