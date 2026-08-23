namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// The three things a manoeuvre may ask of the town rather than do for itself. <b>Each is an action that
/// needs the whole composition</b> — the route search, the price table, the bay register and the road
/// graph at once — and handing the catalogue a town would make every entry able to reach anything.
/// </summary>
/// <remarks>
/// Everything else an entry does to a car it does through <see cref="ManeuverDesk"/>, which is the
/// driver's own instrument and knows only what a driver knows.
/// </remarks>
internal enum DriveOrder : byte
{
    None,

    /// <summary>
    /// Take the lane the car is actually standing on and lay the route's line forward from it (MAN-3).
    /// What every entry that drove geometry of its own hands back into, since the pose it ends in is
    /// not the pose the plan expected.
    /// </summary>
    TakeTheLaneUnderIt,

    /// <summary>Give up the place this leg holds and claim another near where the car has got to, or refuse.</summary>
    RetargetTheBay,

    /// <summary>Price the stretch the car is blocked entering up, so other drivers route around it, and drop the route in hand.</summary>
    MarkTheWayBlocked,
}

/// <summary>
/// What an entry's <c>Sa</c> came to: whether it may be taken up from here at all, and the one order the
/// town has to carry for it if so.
/// </summary>
/// <remarks>
/// <b>A refusal must not have written anything yet.</b> A rung of the ladder that fails after mutating
/// state leaves the climb unable to continue honestly, so an entry works out whether it can begin
/// <em>before</em> it lays a line — and an order the town cannot carry is a refusal too, which is what
/// lets `E-6` mean "retarget, and if there is nowhere to retarget to, take the next rung".
/// </remarks>
/// <param name="CanEnter">Whether the entry state this manoeuvre requires holds where the car stands.</param>
/// <param name="Order">The one thing the town does on its behalf as it takes up, if any.</param>
/// <param name="Subject">What that order is about — a bay to avoid, a lane. −1 where the order needs no subject.</param>
internal readonly record struct ManeuverStart(bool CanEnter, DriveOrder Order, int Subject)
{
    public static ManeuverStart No => new(false, DriveOrder.None, -1);

    public static ManeuverStart Yes => new(true, DriveOrder.None, -1);

    public static ManeuverStart Ask(DriveOrder order, int subject = -1) => new(true, order, subject);
}
