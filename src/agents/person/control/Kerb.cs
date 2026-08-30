using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Person.Control;

/// <summary>
/// The question a walker asks before it steps off a kerb (PER-15), and the two different places it waits
/// for the two different answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>A red is not a gap question and no amount of clear road answers it.</b> The signal is asked first
/// and refuses outright; the give-way gap is asked second and is what the patience clock is for. A
/// crossing that never clears is not traffic, it is a jam — and a pedestrian has priority, so past the
/// patience the walker steps out and the cars stop, which is what the crossing is for.
/// </para>
/// <para>
/// <b>The give-way is a reservation and never a prediction.</b> What the walker asks is whether the paint
/// is anybody's — is any lane of it inside the road some driver has already taken — and not how long
/// something would take to arrive. A reservation runs from a car's own tail to where that car is committed
/// to being able to stop, so <em>a car far enough away to stop for this body holds none of the crossing</em>
/// and one that is not, does. The time it would take to get here is a figure the arithmetic behind that
/// reservation has already taken, and asked again here it is the same answer computed twice from staler
/// numbers.
/// </para>
/// <para>
/// <b>The lane it steps into and no further.</b> A zebra is carriageway like the rest of it, so what a
/// body needs before it leaves the kerb is the ground it is about to be standing on; the lanes past that
/// are asked for in turn as it reaches them, by the same question asked of the same book, and a walker held
/// part way over is held by a car committed to a band it has not got to yet rather than by a rule about the
/// whole paint.
/// </para>
/// <para>
/// <b>And granted, the band is this body's</b> (TER-4c.1). Nothing asks again as the foot goes down: the
/// traffic in that lane is cut at the stretch the body has been given, and it is the car arriving after that
/// gives way. The kerb is where this particular asker happens to be standing and never a gate of its own.
/// </para>
/// <para>
/// <b>It is asked of the road's own book and never of the fleet</b> (<see cref="LaneOccupancy"/>). Looking
/// <em>both</em> ways falls out of the lanes rather than out of a radius. Asked of every car in the town it
/// was the same question with a scan of the whole fleet behind it, and it counted cars on other streets
/// that happened to be near.
/// </para>
/// <para>
/// <b>The two waits stand in different places on purpose.</b> A wait for a gap belongs at the kerb — a
/// decision about to be acted on, lasting a second or two, needing the view. A wait for a red belongs at
/// the stand-off, because it lasts a phase with a crowd building behind it.
/// </para>
/// </remarks>
internal static class Kerb
{
    /// <summary>
    /// Whether this walker may begin crossing. <paramref name="waitedS"/> is how long it has been at the
    /// kerb, which only ever answers the gap.
    /// </summary>
    /// <param name="ahead">
    /// The lanes the way this body is about to step onto runs under, in the order it meets them — of which
    /// only the first is this question's.
    /// </param>
    /// <param name="claimM">How much of a lane a body on this paint takes, either side of it.</param>
    public static bool MayBegin(
        SimConfig config, SignalService signals, float timeS, int crossing, float claimM, LaneOccupancy roads,
        ReadOnlySpan<CrossingBands.Band> ahead, float waitedS)
    {
        if (signals.CrossingIsLit(crossing) && signals.ForCrossing(crossing, timeS) != SignalColour.Green) return false;
        if (TheBandItStepsIntoIsFree(roads, ahead, claimM)) return true;

        // <b>Every other agent gives way to a rescue, and a body at a kerb is one of them</b> (AMB-4). The
        // patience is PER-15's escape from a crossing that never clears, and the road of a rescue that is
        // coming through is the one thing it does not escape (<see cref="ARescueIsOver"/>).
        return waitedS >= config.Person.KerbPatienceS && !ARescueIsOver(config, roads, ahead, claimM);
    }

    /// <summary>
    /// Whether the first lane this crossing meets is inside nobody's road. <b>A crossing that runs under
    /// nothing is free</b>: paint over ground no traffic can be on is nothing to wait for.
    /// </summary>
    public static bool TheBandItStepsIntoIsFree(
        LaneOccupancy roads, ReadOnlySpan<CrossingBands.Band> ahead, float claimM) =>
        ahead.Length == 0 || BandIsFree(roads, ahead[0], claimM);

    /// <summary>
    /// Whether one lane's width of a crossing is inside the road anybody has already taken. Looks <b>both
    /// ways</b>, which falls out of a stretch's two lanes running opposite ways: a band is one strip of
    /// carriageway and every direction of traffic over it is in the same book.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The band and not the point.</b> A reservation is a stretch of lane and the paint has a depth, so a
    /// car committed to ground that ends a metre short of the centreline is a car that will be standing on
    /// the near half of the zebra — and asked at the middle alone, that is a crossing this body is waved
    /// onto.
    /// </para>
    /// <para>
    /// <b>It is the same question a body already crossing asks of the lane in front of it</b>, and one
    /// answer rather than two: what a walker at a kerb may step onto and what one halfway over may walk
    /// into is the same strip of road asked about by the same book, and the kerb is only where the body
    /// happens to be standing when it asks.
    /// </para>
    /// </remarks>
    public static bool BandIsFree(LaneOccupancy roads, CrossingBands.Band band, float claimM) =>
        !roads.AnyTrafficOver(roads.WayOfLane(band.Lane), band.AlongLaneM - claimM, band.AlongLaneM + claimM);

    /// <summary>
    /// Whether the lane this body is about to step into is inside the road of <b>a rescue that is coming
    /// through it</b> (AMB-4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A rescue that is not moving is not one</b>, and that is the whole of what makes this an exception
    /// rather than a way of shutting a crossing for good. The exemption is worth what its own justification
    /// is worth — a call lasts seconds, so what is being waited out is going to pass — and an ambulance
    /// sitting over the paint is not passing: it is a stopped car, which is what the walker taking the band
    /// would have made of it anyway. Left unbounded, PER-15's escape never fires at that crossing at all,
    /// and a body halfway over stands in a live carriageway for as long as the ambulance stands in it.
    /// </para>
    /// <para>
    /// <b>The bar is the walker's own pace</b> (<see cref="SimConfig.PersonWalkSpeedMps"/>), which is the
    /// relation rather than a figure: a rescue closing slower than this body can walk is one the body is off
    /// the band well before, so standing aside for it buys the rescue nothing and costs the crossing
    /// everything.
    /// </para>
    /// </remarks>
    public static bool ARescueIsOver(
        SimConfig config, LaneOccupancy roads, ReadOnlySpan<CrossingBands.Band> ahead, float claimM) =>
        ahead.Length > 0 && ARescueIsOver(config, roads, ahead[0], claimM);

    public static bool ARescueIsOver(
        SimConfig config, LaneOccupancy roads, CrossingBands.Band band, float claimM) =>
        roads.AnyRescueOver(
            roads.WayOfLane(band.Lane), band.AlongLaneM - claimM, band.AlongLaneM + claimM,
            config.PersonWalkSpeedMps);
}
