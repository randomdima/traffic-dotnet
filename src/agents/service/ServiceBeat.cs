namespace TrafficSimulation.Agents.Service;

/// <summary>
/// <b>Which district each service vehicle keeps to, and whether it drives that district's streets between
/// calls or stands on its apron</b> (SRV-5) — for an ambulance, a police car and an evacuator alike. One array per
/// field keyed by the car, for the reason <see cref="PatrolDuty"/> is.
/// </summary>
internal sealed class ServiceBeat
{
    public ServiceBeat(int cars)
    {
        District = new int[cars];
        Array.Fill(District, NoDistrict);
        Patrols = new bool[cars];
        SetsOutAtS = new float[cars];
    }

    /// <summary>
    /// The district its building stands in (GEN-56), whose streets its beat is drawn from, and
    /// <see cref="NoDistrict"/> for every car that is not a service vehicle.
    /// </summary>
    public int[] District { get; }

    /// <summary>Whether it drives the beat between calls, rather than standing on its apron until one takes it.</summary>
    public bool[] Patrols { get; }

    /// <summary>
    /// When a patrolling vehicle first leaves its apron, on the town's clock — drawn per car, so a building's
    /// fleet stood in one instant does not leave in it.
    /// </summary>
    public float[] SetsOutAtS { get; }

    public const int NoDistrict = -1;

    /// <summary>Whether this vehicle is one that patrols and its first stand is over.</summary>
    public bool IsDue(int car, float nowS) => Patrols[car] && nowS >= SetsOutAtS[car];
}
