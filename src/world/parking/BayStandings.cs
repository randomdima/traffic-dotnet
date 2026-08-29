using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Parking;

/// <summary>
/// <b>How much of each of a bay's ways the body standing in that bay holds</b> — the metres at the bay's own
/// end of them that nothing else in the town is driven over, measured once from the table that says what is
/// (`TER-5c`).
/// </summary>
/// <remarks>
/// <para>
/// <b>The bay is what a parked car is standing on, and the table is what says how much of that can be
/// said so.</b> A bay's mouth stands half a metre off the carriageway's own edge and a crossing is measured
/// at a body's width, so the outermost of a bay's depth is ground the lane beside it is driven over: held
/// from the mouth in, every parked car cut the street it was parked beside and every neighbour's way in with
/// it. What is left when that ground is taken off is this, and it is free to take precisely because
/// <b>a stretch nothing crosses can cut nobody's grant</b>.
/// </para>
/// <para>
/// <b>Its own bay's other ways are not counted.</b> A bay's ways serve one car at a time — the one standing
/// in it, or the one that booked it and is on its way — so the ground they share is ground that car is
/// welcome to, and counted it would leave nothing at all: they all end at the same pose.
/// </para>
/// <para>
/// <b>Never less than the body behind the axle</b>, which is the stretch a parked car has always laid and
/// the traffic has always been held off, and <b>never more than the bay is deep behind the axle</b>
/// (<see cref="SimConfig.ParkingStandingGroundM"/>) — past that the block would be a picture of ground the
/// bay has not got. <b>Both figures are the standing's</b> (GEN-4j): a car that backed in has its nose
/// where a car that drove in has its tail, and its axle a wheelbase's half deeper.
/// </para>
/// </remarks>
internal sealed class BayStandings
{
    /// <summary>
    /// The hair a stretch is kept clear of the crossing it butts against. The book's own overlap test is
    /// closed at both ends (<see cref="LaneOccupancy.NextSpokenForOver"/>), so a block beginning exactly
    /// where a crossing ends is a block standing on that crossing.
    /// </summary>
    const float ClearOfTheCrossingM = 0.01f;

    readonly int _firstWay;
    readonly float[] _clearM;
    readonly float[] _deepestM;

    BayStandings(int firstWay, float[] clearM, float[] deepestM)
    {
        _firstWay = firstWay;
        _clearM = clearM;
        _deepestM = deepestM;
    }

    /// <summary>
    /// How far back from the bay's own end of this way the body standing in the bay holds: what the table
    /// leaves free, held to <b>at least the body that is standing there and at most the bay's own depth</b>.
    /// </summary>
    /// <param name="bodyAlongTheWayM">
    /// How much of <em>this</em> car lies along the way — its tail behind the axle where it nosed in, the
    /// whole of its nose where it backed in (GEN-4j). It is the car's own (CAR-11): a long body standing in
    /// a bay reaches further back over the way that reaches it than a short one does, and a stretch sized
    /// for the nominal car would leave the tail of a van in the road with nothing said about it.
    /// </param>
    public float HoldsM(int way, float bodyAlongTheWayM) => Math.Clamp(
        _clearM[way - _firstWay], bodyAlongTheWayM, MathF.Max(bodyAlongTheWayM, _deepestM[way - _firstWay]));

    public static BayStandings Of(BayWays ways, WayCrossings crossings, SimConfig config)
    {
        var clearM = new float[ways.WayCount];
        var deepestM = new float[ways.WayCount];
        for (var at = 0; at < ways.WayCount; at++)
        {
            var way = ways.FirstWay + at;
            var lengthM = ways.LengthM(way);
            var freeM = lengthM;

            foreach (ref readonly var section in crossings.Of(way))
            {
                if (ways.IsBayWay(section.OnWay) && ways.BayOfWay(section.OnWay) == ways.BayOfWay(way)) continue;

                // Every way meets the bay at its far end, so what a section leaves standing is what lies
                // between it and that end.
                freeM = MathF.Min(freeM, lengthM - section.MineToM - ClearOfTheCrossingM);
            }

            // How deep the bay is behind the axle is the standing's (GEN-4j) and the town's: a space is one
            // size whoever is parked in it, and past its own depth the block would be a picture of ground
            // the bay has not got.
            clearM[at] = freeM;
            deepestM[at] = ways.IsNoseIn(way)
                ? config.ParkingStandingGroundM
                : config.ParkingBackedInStandingGroundM;
        }

        return new BayStandings(ways.FirstWay, clearM, deepestM);
    }
}
