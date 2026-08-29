namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>AGT-7's closed catalogue</b>: everything a driver does is one of these, and there is no other.
/// A <c>P-</c> entry is planned and chained; an <c>E-</c> entry is never planned — it is triggered by
/// something the plan did not predict, suspends the planned one, runs, and hands control back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every entry has a file of its own</b> under <c>planned/</c> or <c>reactive/</c>, and a page of its
/// own under <c>docs/</c> saying when it is the right thing to do, what it delivers, and what state it
/// starts and ends in. Extending the catalogue is adding those two and one line of
/// <see cref="ManeuverCatalogue"/> — never a branch in the middle of a controller.
/// </para>
/// <para>
/// <b>The numbering has gaps and they stay</b> (`P-1`, `P-3`, `P-5`, `P-7`, `P-9`, `P-10`, `P-12`, `P-13`,
/// `P-15` are the walker's or are retired, and `E-1` and `E-5` are retired): a retired number is never
/// reused, so the codes this enum prints are the brief's own and a reader can look any of them up.
/// </para>
/// <para>
/// The whole catalogue is named here rather than only the part that is built, because
/// <see cref="ManeuverTrace"/> reports which entries were never entered — <b>an entry nothing can reach
/// is a finding</b>, and one that does not exist at all cannot be one.
/// </para>
/// </remarks>
internal enum Maneuver : byte
{
    /// <summary>Not driving at all: no driver in it, or a wreck. Every car starts here.</summary>
    None,

    /// <summary>`P-2` — leave the bay, including the wait for the gap.</summary>
    LeaveTheBay,

    /// <summary>`P-4` — run the line. The default, and the state every other manoeuvre comes back to.</summary>
    RunTheLine,

    /// <summary>`P-6` — hold at a line: come to rest short of a known stop point, wait there, pull away again.</summary>
    HoldAtALine,

    /// <summary>`P-8` — take the junction. One entry for all three movements, named on entry with the junction.</summary>
    TakeTheJunction,

    /// <summary>`P-14` — park in the bay, on the template the leg chose.</summary>
    ParkInTheBay,

    /// <summary>`P-16` — square up in the bay: MAN-6's retry from a different pose, and ladder rung 2.</summary>
    SquareUpInTheBay,

    /// <summary>`P-17` — stand parked: the handbrake held, the bay marked occupied, the driver handed back its trip.</summary>
    StandParked,

    /// <summary>`P-19` — shunt round: come back the other way where no bay is laid to turn in, which is a dead end.</summary>
    ShuntRound,

    /// <summary>`P-18` — attend the scene: come to rest beside a place the car was sent to, and stand there while the crew works.</summary>
    AttendTheScene,

    /// <summary>`E-2` — emergency stop. Frequent use of this is a planning failure and not a safety feature.</summary>
    EmergencyStop,

    /// <summary>`E-3` — back off: the cheapest change of state there is, and re-decide from the new distance.</summary>
    BackOff,

    /// <summary>`E-4` — go round a stationary obstruction. The only overtaking that exists in this town.</summary>
    GoRound,

    /// <summary>`E-6` — give up the target place, and claim another only once a route to it exists.</summary>
    GiveUpThePlace,

    /// <summary>`E-7` — reroute, marking the blocked stretch so other drivers benefit.</summary>
    Reroute,

    /// <summary>`E-8` — return to legal ground. Stop first: a car correcting a violation while still moving acquires a second one.</summary>
    ReturnToLegalGround,

    /// <summary>`E-9` — settle for here, and hand the rest of the trip to the driver's own feet.</summary>
    SettleForHere,

    /// <summary>`E-10` — abandon the car. Nothing else is available.</summary>
    AbandonTheCar,
}

/// <summary>The catalogue's own codes, and the one question about an entry that decides how it is run.</summary>
internal static class Maneuvers
{
    /// <summary>How many entries there are, which is what a trace's matrix is sized by.</summary>
    public static readonly int Count = Enum.GetValues<Maneuver>().Length;

    /// <summary>
    /// A reactive entry suspends a planned one rather than replacing it, so this is what decides whether
    /// the director hands the car back to what it was doing (§1.6) or takes the next step of the plan.
    /// </summary>
    public static bool IsReactive(Maneuver maneuver) => maneuver >= Maneuver.EmergencyStop;

    /// <summary>
    /// The three entries that end a drive leg. <b>Terminal is a property of the entry and not of the
    /// tick it happens on</b>: whoever ordered the leg reads which of the three it was to know whether
    /// the car is parked, stopped somewhere legal, or left where it stands.
    /// </summary>
    public static bool IsTerminal(Maneuver maneuver) =>
        maneuver is Maneuver.StandParked or Maneuver.SettleForHere or Maneuver.AbandonTheCar;

    /// <summary>The brief's own code for an entry — <c>P-4</c>, <c>E-3</c> — which is what a trace prints.</summary>
    public static string Code(Maneuver maneuver) => maneuver switch
    {
        Maneuver.None => "—",
        Maneuver.LeaveTheBay => "P-2",
        Maneuver.RunTheLine => "P-4",
        Maneuver.HoldAtALine => "P-6",
        Maneuver.TakeTheJunction => "P-8",
        Maneuver.ParkInTheBay => "P-14",
        Maneuver.SquareUpInTheBay => "P-16",
        Maneuver.StandParked => "P-17",
        Maneuver.AttendTheScene => "P-18",
        Maneuver.ShuntRound => "P-19",
        Maneuver.EmergencyStop => "E-2",
        Maneuver.BackOff => "E-3",
        Maneuver.GoRound => "E-4",
        Maneuver.GiveUpThePlace => "E-6",
        Maneuver.Reroute => "E-7",
        Maneuver.ReturnToLegalGround => "E-8",
        Maneuver.SettleForHere => "E-9",
        Maneuver.AbandonTheCar => "E-10",
        _ => "?",
    };
}
