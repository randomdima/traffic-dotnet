namespace TrafficSimulation.Agents.Car.Control;

/// <summary>
/// <b>The round a car nobody owns drives</b> (CAR-8): it stands a drawn while in a bay, then drives to a
/// free bay near a place drawn from its own stream and parks there. One array per field, keyed by the car,
/// for the reason every errand's duty is: a car's index means the same thing here as everywhere else.
/// </summary>
/// <remarks>
/// <b>It is the errand of a car nobody owns</b> — the cars of a town nobody lives in, which nothing else would
/// ever move (CAR-1). A car somebody owns is driven by their trips and waits in its bay between them (PER-29).
/// </remarks>
internal sealed class ParkedRound(int cars)
{
    /// <summary>How long the stand it is on lasts, drawn when it came to rest in the bay.</summary>
    public float[] StandS { get; } = new float[cars];

    /// <summary>And how long it has stood so far.</summary>
    public float[] StoodS { get; } = new float[cars];
}
