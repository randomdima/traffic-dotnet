using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The walkers' half of the lane index</b>: what a body on the carriageway takes off the traffic, and
/// what one walking at a crossing says it is about to take.
/// </summary>
/// <remarks>
/// It is PER-26's two claims said of the road rather than of the pavement, and it is the same two: the
/// ground under the body at p0, and the ground it is walking at at p9. <b>What makes the second one worth
/// laying is the right of way the paint carries</b> (TER-5e, <see cref="RightOfWay.OnThePaint"/>) — a
/// stated claim binds whatever ranks below it, so ordinary traffic is cut at the band a walker says it is
/// stepping onto and a rescue coming through is not (AMB-4). <b>Nothing waits and nothing is refused</b>:
/// there is no gap to be judged, no patience to be spent and no signal to be read, because a walker on a
/// zebra simply has the ground and the traffic gives it up.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>A person on the carriageway, claimed on the road's own ways</b>: the band of the crossing under
    /// them, or the stretch of lane a body standing on bare tarmac covers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Paint changes only which stretch of road a body takes.</b> On a crossing it is the band of the
    /// lane it stands in rather than the metre its body covers, because a body crossing may be anywhere
    /// along the depth of the paint before a driver reaches it; on bare tarmac it is the body and nothing
    /// more — a right of way is about paint (TER-5e), and a walker that steps into a lane where nothing is
    /// painted is owed a driver who can stop and no ground beyond itself (`PER-1`).
    /// </para>
    /// <para>
    /// <b>It is laid by the body and not searched for by the cars.</b> Where a person is standing is a fact
    /// about that person; asked as a question about a patch of ground it was a proximity query per crossing
    /// per approaching car per tick, and it answered yes for anybody merely walking past a zebra.
    /// </para>
    /// </remarks>
    void HoldTheRoadUnderIt(int person)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;

        // <b>The body where no band says otherwise</b> (TER-4c.2, TER-5c.2). A crossing whose bands are not
        // laid — which is every one of them while the paint's projection onto the lanes under it is an
        // absence rather than a decision ([the known gaps](../../../docs/index.md#known-gaps)) — is ground
        // this body is standing on all the same, and a walker that claimed nothing there is one no driver
        // can see. <b>One or the other and never both</b>: a body holds one metre of one way once.
        if (!OnACrossing(person, out var edge, out var alongM) || _bands.On(edge).Length == 0)
        {
            StandInTheRoad(person);
            return;
        }

        var paintM = PaintClaimM(_bands.CrossingOf(edge));
        var claimM = People.RadiusM[person] * _config.Person.RoadClaimMargin;
        var backM = alongM - claimM;
        var frontM = alongM + claimM;

        foreach (var band in _bands.On(edge))
        {
            // Behind the body and given back: a car that has been walked past has nothing in front of it.
            // And ahead of it, which is the statement below rather than ground this body is on.
            if (band.ToM < backM || band.FromM > frontM) continue;

            HoldTheBand(person, band, paintM);
        }
    }

    /// <summary>
    /// <b>And PER-26's second claim said of the road</b>: the band of the next lane this body's walk steps
    /// onto, stated at p9 with the paint's own right of way. <b>The traffic in that lane is cut at it and
    /// gives way</b> (TER-5e); a rescue coming through outranks it and does not (AMB-4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One band and never two.</b> A zebra is carriageway and is crossed a lane at a time, so what a
    /// body says is that it is stepping into the lane in front of it — a stride into the near lane is not a
    /// reason to stop the traffic in the far one. The lane after that is stated when the body reaches it.
    /// </para>
    /// <para>
    /// <b>Stated and never asked.</b> Nothing answers this and nothing can refuse it: a stated claim is cut
    /// back against anything stronger already on the ground (TER-4c.3), so a band a car is standing on is a
    /// band this statement does not reach — and what stops the walker walking into that car is the car's own
    /// body and the solver, exactly as on any other ground.
    /// </para>
    /// </remarks>
    void StateTheBandAhead(int person)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;
        if (!People.Walking[person]) return;
        if (!OnACrossing(person, out var edge, out var alongM)) return;

        var paintM = PaintClaimM(_bands.CrossingOf(edge));
        var frontM = alongM + (People.RadiusM[person] * _config.Person.RoadClaimMargin);

        // How far in front of itself this body states ground at all, which is the figure the pavement is
        // stated for (<see cref="StatesAheadM"/>). At a kerb the body stands short of the way and its own
        // metre is unknown, so the lane it is about to step into is always within reach of it.
        var reachM = float.IsFinite(alongM) ? alongM + StatesAheadM(person) : float.PositiveInfinity;

        foreach (var band in _bands.On(edge))
        {
            if (band.FromM <= frontM) continue;
            if (band.FromM > reachM) return;

            StateTheBand(person, band, paintM);
            return;
        }
    }

    /// <summary>The band this body is standing in, held on the lane's own way (TER-4c.2).</summary>
    void HoldTheBand(int person, CrossingBands.Band band, float paintM)
    {
        var way = _ways.OfRoadLane(band.Lane);
        _occupancy.ClaimWhereItStands(
            way, band.AlongLaneM - paintM, band.AlongLaneM + paintM, band.AlongLaneM + paintM, 0f, person,
            of: LaneRoster.Walking, right: RightOfWay.OnThePaint);
    }

    /// <summary>And the band in front of it, stated on the same way at p9 (TER-5g).</summary>
    void StateTheBand(int person, CrossingBands.Band band, float paintM)
    {
        var way = _ways.OfRoadLane(band.Lane);
        _occupancy.ClaimAhead(
            way, band.AlongLaneM - paintM, band.AlongLaneM + paintM, 0f, person, ClaimPriority.Soft,
            LaneRoster.Walking, RightOfWay.OnThePaint);
    }

    /// <summary>
    /// How much of a lane a body on this crossing's paint is owed, measured along the way the traffic runs:
    /// a stride either side of the paint — what a body covers in the time a driver has to do anything about
    /// it — and the margin a body on a road is owed over that.
    /// </summary>
    float PaintClaimM(int crossing) =>
        ((_plan.Crosswalks.DepthM[crossing] * 0.5f) + _config.PersonDiameterM) * _config.Person.RoadClaimMargin;

    /// <summary>
    /// A body standing on the carriageway with no paint under it, as the stretch of <b>every way it is
    /// touching</b>. <b>Where it lies and not where it is going</b> — a walker off the network, one knocked
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

    /// <summary>
    /// The way of a crossing this walker is standing on, and how far along it the body stands — or the way
    /// it is about to step onto, at its own start. <b>The paint underfoot comes first</b>: a body halfway
    /// across has the next crossing of its line ahead of it as well, and the one it is standing on is the
    /// one the traffic has to know about.
    /// </summary>
    /// <remarks>
    /// <b>A way and not a crossing</b>, because a lane's band falls at different metres on each of the ways
    /// a zebra is made of (<see cref="CrossingBands"/>) — and it is the way the body is actually walking,
    /// so which side of the road it started from is a fact the claim already carries.
    /// </remarks>
    bool OnACrossing(int person, out int edge, out float alongM)
    {
        edge = CityPlan.NoRecord;
        alongM = 0f;
        if (!People.Walking[person]) return false;

        var way = People.OnWay[person];
        if (way != PersonFleet.NoWay && _ways.KindOf(way) == WayKind.Footway
            && _bands.CrossingOf(_ways.FootwayOf(way)) >= 0)
        {
            edge = _ways.FootwayOf(way);
            alongM = People.OnWayM[person];
            return true;
        }

        // About to step off. At the kerb the body stands short of the way's own start, so it covers no band
        // of it however far back it is standing, and the first band is the one in front of it.
        var ahead = People.CrossingAhead(person);
        if (ahead < 0) return false;

        alongM = float.NegativeInfinity;
        return TheWayItStepsOnto(person, ahead, out edge);
    }

    /// <summary>
    /// Which way of the crossing ahead this walker's own line steps onto, read off that line. <b>The
    /// mitre onto the paint is not it</b>: a corner belongs to the stretch it leads onto and carries that
    /// stretch's crossing, so the way is the first point of the crossing that stands on a lane of it.
    /// </summary>
    bool TheWayItStepsOnto(int person, int crossing, out int edge)
    {
        var crossings = People.WalkedCrossingOf(person);
        var codes = People.WalkedWayOf(person);
        for (var at = People.WalkedAt(person) + 1; at < People.WalkedCount[person]; at++)
        {
            if (crossings[at] != crossing || codes[at] < 0) continue;

            edge = codes[at];
            return true;
        }

        edge = CityPlan.NoRecord;
        return false;
    }
}
