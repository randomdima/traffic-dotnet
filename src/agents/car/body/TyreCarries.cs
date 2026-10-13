using System.Numerics;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// <b>What every car's tyres carry from one tick to the next</b>: the world's own state, which no driver reads — four
/// wheels to a car, in the order the tyre model works them.
/// </summary>
internal sealed class TyreCarries
{
    public TyreCarries(int cars)
    {
        SpinMps = new float[cars * TyreModel.Wheels];
        ScrubTravelM = new float[cars * TyreModel.Wheels];
        MarkFromM = new Vector2[cars * TyreModel.Wheels];
        MarkIntensity = new float[cars * TyreModel.Wheels];
        Marking = new bool[cars * TyreModel.Wheels];
        SlipThrottle = new float[cars];
        Array.Fill(SlipThrottle, 1f);
        DrivenSlipping = new bool[cars];
    }

    /// <summary>
    /// Each wheel's own rotation, as the speed its tread runs over the ground. The daylight between it and the road
    /// under the patch is what says whether a wheel is spinning, locked, or simply rolling.
    /// </summary>
    public float[] SpinMps { get; }

    /// <summary>How far each tyre has dragged in the slide it is in, capped at the onset distance.</summary>
    public float[] ScrubTravelM { get; }

    /// <summary>Where the stretch of mark being laid started, how dark it is, and whether that wheel is marking at all.</summary>
    public Vector2[] MarkFromM { get; }

    public float[] MarkIntensity { get; }

    public bool[] Marking { get; }

    /// <summary>
    /// What is left of the throttle after backing off for driven wheels already past what they can put down —
    /// traction control off the slide the tyres themselves report. A hand at the wheel gets none of it.
    /// </summary>
    public float[] SlipThrottle { get; }

    /// <summary>Whether a wheel the engine is turning was past its budget last tick.</summary>
    public bool[] DrivenSlipping { get; }

    public Span<float> SpinOf(int car) => SpinMps.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels);

    public TyreCarry Of(int car) => new(
        SpinOf(car), ScrubTravelM.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels),
        Marking.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels), MarkFromM.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels),
        MarkIntensity.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels), ref SlipThrottle[car], ref DrivenSlipping[car]);

    /// <summary>A car's tyres forgotten where it stands: rims stopped, throttle recovered, no mark under way.</summary>
    public void Clear(int car, Vector2 positionM)
    {
        SlipThrottle[car] = 1f;
        DrivenSlipping[car] = false;
        for (var wheel = car * TyreModel.Wheels; wheel < (car + 1) * TyreModel.Wheels; wheel++)
        {
            SpinMps[wheel] = 0f;
            ScrubTravelM[wheel] = 0f;
            MarkFromM[wheel] = positionM;
            MarkIntensity[wheel] = 0f;
            Marking[wheel] = false;
        }
    }
}
