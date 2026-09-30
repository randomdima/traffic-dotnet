namespace TrafficSimulation.Agents.Ambulance;

/// <summary>
/// <b>What an ambulance is doing about the call it is on</b> (AMB-5). The life cycle as observable
/// states: there is no state an ambulance can be in that this does not name, and every transition
/// between two of them is one line of <c>TownWorld.Ambulance.cs</c>.
/// </summary>
/// <remarks>
/// <b>These are not actions and do not pretend to be.</b> An ambulance drives the legs every other car
/// drives (AGT-7, CAR-15); what is here is the errand those legs are being run for, exactly as
/// <c>TripStage</c> is for a walker. The one thing a rescue asks of the driving is a <em>place</em> to be
/// stopped at, which is a term of the speed profile like any other (<c>DrivingHold.Place</c>) and is what
/// <see cref="Loading"/> is held at.
/// </remarks>
internal enum RescueStage : byte
{
    /// <summary>Standing with nobody to fetch: at its station, or — for one that patrols — before its first beat.</summary>
    Waiting,

    /// <summary>Driving its district's beat with nobody to fetch, blue light out: ordinary traffic (SRV-5).</summary>
    Patrolling,

    /// <summary>Under way to the standoff short of the scene, blue light on: the leg the priority is for.</summary>
    Running,

    /// <summary>Standing at the standoff while the casualty is got aboard — AMB-6, AMB-10.</summary>
    Loading,

    /// <summary>Under way to the hospital with the casualty aboard, blue light still on.</summary>
    Carrying,

    /// <summary>Parked at the hospital, asking the door for a place. Refused is a real state and not a failure (OBJ-5).</summary>
    HandingOver,

    /// <summary>Driving back to its station, blue light out: an ambulance between calls is ordinary traffic.</summary>
    GoingHome,
}

/// <summary>
/// <b>Every ambulance's call, as one array per field</b> — keyed by the car, because an ambulance is a
/// car and a car is an index. Nothing here is a decision; it is what the town wrote down about the
/// errand each ambulance is on.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is laid over the whole fleet rather than over the ambulances alone.</b> Six arrays of a few
/// hundred entries cost nothing and buy the one property that matters: a car's index means the same
/// thing here as it does everywhere else, so nothing has to hold a second numbering and nothing can
/// disagree about which car a call belongs to.
/// </para>
/// <para>
/// <b>A call is bounded</b> (<see cref="SinceS"/>, AMB-9). A casualty the traffic never lets an
/// ambulance reach must not hold that ambulance off every later call for the rest of the run, so the
/// clock runs from the moment the call is taken and the ambulance goes home when it is spent.
/// </para>
/// </remarks>
internal sealed class RescueDuty
{
    public RescueDuty(int cars)
    {
        Stage = new RescueStage[cars];
        Casualty = new int[cars];
        Array.Fill(Casualty, Nobody);
        Hospital = new int[cars];
        Array.Fill(Hospital, NoBuilding);
        HomeBay = new int[cars];
        Array.Fill(HomeBay, NoBay);
        SinceS = new float[cars];
        LoadedForS = new float[cars];
    }

    /// <summary>What this ambulance is doing about its call. <see cref="RescueStage.Waiting"/> for every car that is not one.</summary>
    public RescueStage[] Stage { get; }

    /// <summary>The person this call is for, or <see cref="Nobody"/>. <b>One casualty to a call and one call to a casualty.</b></summary>
    public int[] Casualty { get; }

    /// <summary>The hospital this ambulance belongs to: where a casualty is delivered, and what its station is near.</summary>
    public int[] Hospital { get; }

    /// <summary>The bay it waits in between calls, held for it for the whole run (AMB-2).</summary>
    public int[] HomeBay { get; }

    /// <summary>How long this call has been running, which is the bound that ends an unreachable one.</summary>
    public float[] SinceS { get; }

    /// <summary>And how long the casualty has been being got aboard, the ambulance standing at the place it stopped at.</summary>
    public float[] LoadedForS { get; }

    public const int Nobody = -1;

    public const int NoBuilding = -1;

    public const int NoBay = -1;

    /// <summary>Whether this ambulance is on a call at all, which is every stage but the two it has nobody to fetch in.</summary>
    public bool IsOnACall(int car) => Stage[car] is not (RescueStage.Waiting or RescueStage.Patrolling);

    /// <summary>
    /// Whether it is carrying the priority (AMB-4): <b>the whole of the call from the moment it is taken
    /// until the casualty is through the door</b>, driving legs and scene alike.
    /// </summary>
    /// <remarks>
    /// <b>The light belongs to the errand and not to who is sitting in the vehicle</b> (AMB-4b, SRV-3),
    /// which is what lets one stay on over a vehicle nobody is in. What it costs is stated where it is
    /// spent: an ambulance standing at a scene holds its ground at the emergency rank for as long as the
    /// scene lasts — which is seconds, and bounded by AMB-9 above that.
    /// </remarks>
    public bool IsHurrying(int car) => IsOnACall(car) && Stage[car] is not (RescueStage.HandingOver or RescueStage.GoingHome);

    /// <summary>The call given up or discharged: everything it held, dropped in one place.</summary>
    public void Clear(int car)
    {
        Stage[car] = RescueStage.Waiting;
        Casualty[car] = Nobody;
        SinceS[car] = 0f;
        LoadedForS[car] = 0f;
    }
}
