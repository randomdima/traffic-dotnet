namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>The catalogue's one dispatch.</b> Every entry is a file with three things in it — an entry
/// condition, a procedure and its exits — and this is where the enum meets them. <b>Adding an entry is a
/// file, a page under <c>docs/</c> and one line in each switch here</b>, and never a branch in the
/// middle of a controller.
/// </summary>
/// <remarks>
/// <para>
/// A switch on a contiguous enum is a jump table; the entries are static classes with no instances, so
/// there is no allocation on a hand-over and no dispatch the JIT cannot see through. That is what makes
/// a catalogue of this size affordable in a steady state that allocates nothing.
/// </para>
/// <para>
/// <b>The traits are declared by the entry and not worked out here.</b> The catalogue is what knows
/// which of its entries are negotiating with other agents and which are control loops wearing a
/// decision's clothes; a geometric test in the director would be a second opinion about that, and would
/// have to be kept in step with every entry added afterwards.
/// </para>
/// </remarks>
internal static class ManeuverCatalogue
{
    /// <summary>
    /// <c>Sa</c> and taking up in one call. <b>A refusal must not have written anything</b>: an entry
    /// works out whether it can begin before it lays a line, so a rung of the ladder that refuses leaves
    /// the climb able to continue honestly.
    /// </summary>
    public static ManeuverStart Begin(Maneuver id, in DriveScene scene, ManeuverDesk desk, int subject) => id switch
    {
        Maneuver.LeaveTheBay => P02LeaveTheBay.Begin(scene, desk, subject),
        Maneuver.RunTheLine => P04RunTheLine.Begin(scene, desk, subject),
        Maneuver.HoldAtALine => P06HoldAtALine.Begin(scene, desk, subject),
        Maneuver.TakeTheJunction => P08TakeTheJunction.Begin(scene, desk, subject),
        Maneuver.ParkInTheBay => P14ParkInTheBay.Begin(scene, desk, subject),
        Maneuver.SquareUpInTheBay => P16SquareUpInTheBay.Begin(scene, desk, subject),
        Maneuver.StandParked => P17StandParked.Begin(scene, desk, subject),
        Maneuver.AttendTheScene => P18AttendTheScene.Begin(scene, desk, subject),
        Maneuver.ShuntRound => P19ShuntRound.Begin(scene, desk, subject),
        Maneuver.EmergencyStop => E02EmergencyStop.Begin(scene, desk, subject),
        Maneuver.BackOff => E03BackOff.Begin(scene, desk, subject),
        Maneuver.GoRound => E04GoRound.Begin(scene, desk, subject),
        Maneuver.GiveUpThePlace => E06GiveUpThePlace.Begin(scene, desk, subject),
        Maneuver.Reroute => E07Reroute.Begin(scene, desk, subject),
        Maneuver.ReturnToLegalGround => E08ReturnToLegalGround.Begin(scene, desk, subject),
        Maneuver.SettleForHere => E09SettleForHere.Begin(scene, desk, subject),
        Maneuver.AbandonTheCar => E10AbandonTheCar.Begin(scene, desk, subject),
        _ => ManeuverStart.No,
    };

    /// <summary>
    /// One tick of the entry's own procedure: what it imposes on the car, and how it stands. The time
    /// handed to it is the time since the driver last thought and never one tick — every clock inside
    /// the catalogue is an accumulation of it.
    /// </summary>
    public static ManeuverOutcome Tick(
        Maneuver id, in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits) => id switch
    {
        Maneuver.LeaveTheBay => P02LeaveTheBay.Tick(scene, desk, sinceS, ref limits),
        Maneuver.RunTheLine => P04RunTheLine.Tick(scene, desk, sinceS, ref limits),
        Maneuver.HoldAtALine => P06HoldAtALine.Tick(scene, desk, sinceS, ref limits),
        Maneuver.TakeTheJunction => P08TakeTheJunction.Tick(scene, desk, sinceS, ref limits),
        Maneuver.ParkInTheBay => P14ParkInTheBay.Tick(scene, desk, sinceS, ref limits),
        Maneuver.SquareUpInTheBay => P16SquareUpInTheBay.Tick(scene, desk, sinceS, ref limits),
        Maneuver.StandParked => P17StandParked.Tick(scene, desk, sinceS, ref limits),
        Maneuver.AttendTheScene => P18AttendTheScene.Tick(scene, desk, sinceS, ref limits),
        Maneuver.ShuntRound => P19ShuntRound.Tick(scene, desk, sinceS, ref limits),
        Maneuver.EmergencyStop => E02EmergencyStop.Tick(scene, desk, sinceS, ref limits),
        Maneuver.BackOff => E03BackOff.Tick(scene, desk, sinceS, ref limits),
        Maneuver.GoRound => E04GoRound.Tick(scene, desk, sinceS, ref limits),
        Maneuver.GiveUpThePlace => E06GiveUpThePlace.Tick(scene, desk, sinceS, ref limits),
        Maneuver.Reroute => E07Reroute.Tick(scene, desk, sinceS, ref limits),
        Maneuver.ReturnToLegalGround => E08ReturnToLegalGround.Tick(scene, desk, sinceS, ref limits),
        Maneuver.SettleForHere => E09SettleForHere.Tick(scene, desk, sinceS, ref limits),
        Maneuver.AbandonTheCar => E10AbandonTheCar.Tick(scene, desk, sinceS, ref limits),
        _ => ManeuverOutcome.Running,
    };

    /// <summary>
    /// Must this entry be ticked on every physics tick rather than on the driver's decision clock? Two
    /// kinds say yes: <b>negotiating with something that is itself moving</b>, where a tenth of a second
    /// of staleness is a gap that had already closed, and <b>steering to a pose</b>, which is a control
    /// loop and converges at a sixth of the rate if it is run at a sixth of the rate.
    /// </summary>
    public static bool ThinksEveryTick(Maneuver id) => id switch
    {
        Maneuver.LeaveTheBay => P02LeaveTheBay.ThinksEveryTick,
        Maneuver.RunTheLine => P04RunTheLine.ThinksEveryTick,
        Maneuver.HoldAtALine => P06HoldAtALine.ThinksEveryTick,
        Maneuver.TakeTheJunction => P08TakeTheJunction.ThinksEveryTick,
        Maneuver.ParkInTheBay => P14ParkInTheBay.ThinksEveryTick,
        Maneuver.SquareUpInTheBay => P16SquareUpInTheBay.ThinksEveryTick,
        Maneuver.StandParked => P17StandParked.ThinksEveryTick,
        Maneuver.AttendTheScene => P18AttendTheScene.ThinksEveryTick,
        Maneuver.ShuntRound => P19ShuntRound.ThinksEveryTick,
        Maneuver.EmergencyStop => E02EmergencyStop.ThinksEveryTick,
        Maneuver.BackOff => E03BackOff.ThinksEveryTick,
        Maneuver.GoRound => E04GoRound.ThinksEveryTick,
        Maneuver.GiveUpThePlace => E06GiveUpThePlace.ThinksEveryTick,
        Maneuver.Reroute => E07Reroute.ThinksEveryTick,
        Maneuver.ReturnToLegalGround => E08ReturnToLegalGround.ThinksEveryTick,
        Maneuver.SettleForHere => E09SettleForHere.ThinksEveryTick,
        Maneuver.AbandonTheCar => E10AbandonTheCar.ThinksEveryTick,
        _ => false,
    };

    /// <summary>
    /// Is this entry watched by the stuck fuse? <b>Nearly all are</b> — entries with no watchdog are how
    /// two cars nose to nose hold each other for a whole run. The ones that say no are the ones whose
    /// standing still <em>is</em> the procedure.
    /// </summary>
    public static bool Watched(Maneuver id) => id switch
    {
        Maneuver.LeaveTheBay => P02LeaveTheBay.Watched,
        Maneuver.RunTheLine => P04RunTheLine.Watched,
        Maneuver.HoldAtALine => P06HoldAtALine.Watched,
        Maneuver.TakeTheJunction => P08TakeTheJunction.Watched,
        Maneuver.ParkInTheBay => P14ParkInTheBay.Watched,
        Maneuver.SquareUpInTheBay => P16SquareUpInTheBay.Watched,
        Maneuver.StandParked => P17StandParked.Watched,
        Maneuver.AttendTheScene => P18AttendTheScene.Watched,
        Maneuver.ShuntRound => P19ShuntRound.Watched,
        Maneuver.EmergencyStop => E02EmergencyStop.Watched,
        Maneuver.BackOff => E03BackOff.Watched,
        Maneuver.GoRound => E04GoRound.Watched,
        Maneuver.GiveUpThePlace => E06GiveUpThePlace.Watched,
        Maneuver.Reroute => E07Reroute.Watched,
        Maneuver.ReturnToLegalGround => E08ReturnToLegalGround.Watched,
        Maneuver.SettleForHere => E09SettleForHere.Watched,
        Maneuver.AbandonTheCar => E10AbandonTheCar.Watched,
        _ => false,
    };

    /// <summary>
    /// Is this entry's geometry <b>manoeuvred</b> rather than driven? A template is held to manoeuvring
    /// pace because a car on one has left the lane discipline behind and may be across a lane; a swerve is
    /// the one that has not — it is the road's own line moved two metres sideways, and holding it to a
    /// crawl is what makes an overtake impossible against anything that is moving at all.
    /// </summary>
    public static bool AtManeuveringPace(Maneuver id) => id != Maneuver.GoRound;

    /// <summary>
    /// The bound this entry's standing still is measured against, before the car's own jitter: the short
    /// fuse where the body is across a lane, the blocked-road clock everywhere else.
    /// </summary>
    public static float FuseS(in DriveScene scene) =>
        scene.AcrossALane ? scene.Config.CarShortFuseS : scene.Config.CarBlockedRoadS;
}
