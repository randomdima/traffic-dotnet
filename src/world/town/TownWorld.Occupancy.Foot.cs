using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary><b>The walkers' half of the lane index</b>: what a body on the carriageway takes off the traffic.</summary>
/// <remarks>
/// It is PER-26's first claim said of the road rather than of the pavement — the ground under the body,
/// on every way of the carriageway its own box is over. <b>Paint buys a walker nothing here</b> (TER-5e):
/// a body on a zebra is a body standing on a lane and holds what it covers of it, which is the same row a
/// wreck, a parked car and somebody knocked down all hold. The traffic is held off it because a body on a
/// lane cuts the road a driver was granted, and by no rule of its own.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>A person on the carriageway</b>, as the stretch of <b>every way it is touching</b>. <b>Where it
    /// lies and not where it is going</b> — a walker crossing on the paint, one off the network, one knocked
    /// over and one a hand is steering are the same fact to whoever is driving up behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same walk a car standing there is written onto</b> (TER-4c.2,
    /// <see cref="TheRoadsGroundUnder"/>): the lane it is in, the lane running back the other way, every join
    /// of a junction it is under, <b>and every way of the bay it is standing in</b>. Asked of the nearest lane
    /// alone, a body inside a box was past the end of every lane there and so on none of them — a person
    /// standing in a junction that no driver crossing it could see; asked of the carriageway alone, a person
    /// in a parking space was ground the driver working into that space could not see either. Which of the
    /// town's networks a piece of ground belongs to is a fact about the ground, and a walk that names them is
    /// the walk every body in the town is laid by.
    /// </para>
    /// <para>
    /// <b>What it holds of each is what its own box covers of that way and not a metre more</b> — the same
    /// reading a car's pose is laid by (<see cref="BodyFootprint.CoversOn"/>), so a body squarely in a lane
    /// holds its width and one that merely clips a way holds the clip. <b>The margin belongs to whoever is
    /// driving at it</b> (SIM-7, <see cref="LaneCredit"/>): a grant already stops the driver's own
    /// <see cref="Agents.Car.Body.CarBuild.BodyMarginM"/> short of anything laid where it lies, and a walker
    /// that widened its own stretch as well would be that gap kept twice — a metre of it on the side that
    /// cannot brake.
    /// </para>
    /// <para>
    /// <b>Which ground a body is on is the band's to say and never the terrain grid's</b> (TER-4c.2, SIM-7).
    /// The grid answers to the cell it is painted at, so a walker within half a cell of a kerb is regularly
    /// standing on a cell the carriageway never reached — asked of it first, a body a stride into the road was
    /// on no lane's claims at all, which is a body stepping out in front of traffic that cannot see it.
    /// </para>
    /// <para>
    /// <b>And no ground beyond itself</b> (`PER-1`). A car lying in a road holds what it could not stop short
    /// of as well, because it is a tonne going somewhere; a walker is owed a driver who can stop and asks for
    /// nothing further, which is the whole of the difference between the two rows.
    /// </para>
    /// </remarks>
    [SkipLocalsInit]
    void StandInTheRoad(int person)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;

        var positionM = People.PositionM[person];
        var radiusM = People.RadiusM[person];

        // <b>On the same terms a car standing there is</b> (<see cref="RoadGraph.WithinTheBand"/>): every way
        // the body touches, each carrying how far aside of that way's line it is standing. The two networks
        // are told apart by which the ground belongs to and never by which kind of body is standing on
        // it (TER-4c) — so a man on the kerbside edge of a lane has claimed it and stops nobody
        // driving down it, which is the same arithmetic that lets a car clip a lane without shutting it.
        Span<WayUnder> under = stackalloc WayUnder[RoomForTheRoadsGround];
        var count = TheRoadsGroundUnder(positionM, BodyFootprint.Round(radiusM), under);

        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref under[index];
            _occupancy.ClaimWhereItStands(
                way.Way, way.AlongM + way.BackM, way.AlongM + way.AheadM, way.AlongM + way.AheadM,
                Vector2.Dot(People.VelocityMps[person], way.AlongUnit), person, of: LaneRoster.Walking,
                acrossFromM: way.AcrossFromM, acrossToM: way.AcrossToM);
        }
    }
}
