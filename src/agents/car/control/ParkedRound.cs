namespace TrafficSimulation.Agents.Car.Control;

/// <summary>
/// <b>The round a car nothing else drives</b> (CAR-8): it stands a drawn while in a bay, then drives to a
/// free bay near a place drawn from its own stream and parks there. One array per field, keyed by the car,
/// for the reason every errand's duty is: a car's index means the same thing here as everywhere else.
/// </summary>
/// <remarks>
/// <b>It is the errand of a car nobody is in</b>, and the only thing that moves one once a town has bays
/// (CAR-1): the town's walkers walk every trip (PER-11), so without it a car park stood full before the first
/// tick would be the whole of the traffic for the rest of the run.
/// </remarks>
internal sealed class ParkedRound(int cars)
{
    /// <summary>How long the stand it is on lasts, drawn when it came to rest in the bay.</summary>
    public float[] StandS { get; } = new float[cars];

    /// <summary>And how long it has stood so far.</summary>
    public float[] StoodS { get; } = new float[cars];
}
