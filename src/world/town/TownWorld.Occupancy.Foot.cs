using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The walkers' half of the lane index</b>: what a body on the carriageway takes off the traffic, and
/// the ask it makes of the lane in front of it before it steps onto one.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>A person on the carriageway, written into the road's book</b>: the bands of the crossing under
    /// them their body covers, or the stretch of lane a body standing on bare tarmac covers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The two are one fact and are laid together.</b> A walker on a lane cuts the road a driver is
    /// granted wherever it stands, and paint changes only which stretch of road it takes: on a crossing it
    /// is the band of the lane it stands in rather than the stretch of lane its body covers, because a
    /// body crossing may be anywhere along the depth of the paint before a driver reaches it. On bare
    /// tarmac it is the body and nothing more — `P-12` is about paint, and a walker that steps into a lane
    /// where nothing is painted is owed a driver who can stop and no ground beyond itself (`PER-12`).
    /// </para>
    /// <para>
    /// <b>The lane it is standing in, and the one in front of it once that one is granted</b> (`PER-15`,
    /// TER-4c.1). A lane it has cleared is given back the moment it is out of it — a car that has been walked
    /// past has nothing in front of it — and a lane further on than the next was never this body's to ask
    /// for. <b>A zebra is crossed a lane at a time</b>, and each lane of it is asked for on the terms every
    /// other piece of the town is asked for on.
    /// </para>
    /// <para>
    /// <b>It is laid by the body and not searched for by the cars.</b> Where a person is standing is a fact
    /// about that person; asked as a question about a patch of ground it was a proximity query per crossing
    /// per approaching car per tick, and it answered yes for anybody merely walking past a zebra.
    /// </para>
    /// <para>
    /// <b>The band in front is ground the body cannot reach from where it stands, so it is asked for and
    /// answered rather than simply taken</b> (<see cref="MayStepOnto"/>): granted where no car's road is over
    /// it, and refused where one is — which is the whole of what waiting for a gap is, and the whole of what
    /// makes a zebra safe to step onto. <b>Granted, it is this body's</b>: the traffic in that lane is cut at
    /// it and the walker needs nobody's leave to walk into it. A signal a body is refused by (PER-7.3) asks
    /// for nothing at all — a walker on its own red holding the traffic on its green would be the crossing
    /// working backwards.
    /// </para>
    /// </remarks>
    void PlaceTheWalkerOnTheRoad(int person)
    {
        People.RefusedWay[person] = PersonFleet.NoWay;

        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any || People.Dead[person]) return;

        if (!OnACrossing(person, out var edge, out var alongM))
        {
            People.WaitingForLane[person] = PersonFleet.NoLane;
            StandInTheRoad(person);
            return;
        }

        var paintM = PaintClaimM(_bands.CrossingOf(edge));
        var claimM = People.RadiusM[person] * _config.Person.RoadClaimMargin;
        var backM = alongM - claimM;
        var frontM = alongM + claimM;

        // How far in front of itself this body is asking for ground at all. A band further off than that
        // is one it has not asked for: a stride into the near lane is not a reason to stop the traffic in
        // the far one, and what a body may take is what its own ask reaches — the same bar a car's road is
        // held to. At a kerb the body stands short of the way and its own metre is unknown, so the lane it
        // is about to step into is always in reach and PER-15 is what decides it.
        var reachM = float.IsFinite(alongM) ? alongM + WantsAheadM(person) : float.PositiveInfinity;

        var lookedAhead = false;
        foreach (var band in _bands.On(edge))
        {
            // Behind the body and given back: a car that has been walked past has nothing in front of it.
            if (band.ToM < backM) continue;

            // <b>Standing in the lane it was waiting for is the wait being over</b>, and not the traffic
            // having given way: the patience that bought this ground is spent when the body is on it.
            if (band.FromM <= frontM && People.WaitingForLane[person] == band.Lane)
            {
                People.WaitingForLane[person] = PersonFleet.NoLane;
                People.WaitingToCrossS[person] = 0f;
            }

            if (band.FromM > frontM)
            {
                // Ahead of it, and only the next lane: a body asks for the ground it is about to be on and
                // never for the lane after that, exactly as a car asks for the road it can stop in.
                if (lookedAhead || band.FromM > reachM) continue;

                lookedAhead = true;
                if (!MayStepOnto(person, band, paintM))
                {
                    // What it is standing here for, so that the patience it spends is spent on this lane
                    // and given back when it is standing in it.
                    People.WaitingForLane[person] = band.Lane;

                    // And where the walk it is on runs out, which is this same answer said in the other
                    // network's metres (<see cref="WhereTheWalkRunsOut"/>).
                    People.RefusedWay[person] = _footfall.WayOfLane(edge);
                    People.RefusedAtM[person] = band.FromM;
                    continue;
                }
            }

            _occupancy.Add(
                _occupancy.WayOfLane(band.Lane), band.AlongLaneM - paintM, band.AlongLaneM + paintM, 0f,
                person, LaneUse.OnFoot, LaneRoster.Walking);
        }
    }

    /// <summary>
    /// <b>The answer to the ask for the band in front</b> (TER-4c.1): granted where no car's road is over
    /// that band, and refused where one is. <b>The kerb is only where the body happens to be standing when
    /// it asks</b> — one at a lane's edge half way over is asking the same question about the same strip of
    /// road, and the answer cannot turn on which side of the kerb line the asker is.
    /// </summary>
    /// <remarks>
    /// <b>And granted regardless past the patience</b>: PER-15's escape from a crossing that never clears,
    /// wherever the body has got to on it, and the one thing that makes a wait a wait rather than a jam. It
    /// is the single place in the town where ground is taken that somebody else's road is over, so the cars
    /// give way to it — which is what a pedestrian's priority costs, spent by the clock and by nothing else.
    /// </remarks>
    bool MayStepOnto(int person, CrossingBands.Band band, float paintM) =>
        Kerb.BandIsFree(_occupancy, band, paintM)
        || People.WaitingToCrossS[person] >= _config.Person.KerbPatienceS;

    /// <summary>
    /// How much of a lane a body on this crossing's paint is owed, measured along the way the traffic runs:
    /// a stride either side of the paint — what a body covers in the time a driver has to do anything about
    /// it — and the margin a body on a road is owed over that.
    /// </summary>
    float PaintClaimM(int crossing) =>
        ((_plan.Crosswalks.DepthM[crossing] * 0.5f) + _config.PersonDiameterM) * _config.Person.RoadClaimMargin;

    /// <summary>
    /// A body standing on the carriageway with no paint under it, as the stretch of lane it covers. <b>Where
    /// it lies and not where it is going</b> — a walker off the network, one knocked over, one pacing a road
    /// on purpose (`PER-12`) and one a hand is steering are the same fact to whoever is driving up behind.
    /// </summary>
    void StandInTheRoad(int person)
    {
        var positionM = People.PositionM[person];
        if (!_terrain.At(positionM).Drivable) return;

        var lane = _roads.NearestLane(positionM, out var alongM);
        if (lane < 0) return;

        var radiusM = People.RadiusM[person];
        if (!RoadGraph.WithinTheBand(
                _roads.ArcsOf(lane), alongM, positionM, _roads.LaneWidthM[lane], radiusM, radiusM, out var on))
        {
            return;
        }

        var claimM = radiusM * _config.Person.RoadClaimMargin;
        _occupancy.Add(
            _occupancy.WayOfLane(lane), alongM - claimM, alongM + claimM,
            Vector2.Dot(People.VelocityMps[person], on.Direction), person, LaneUse.OnFoot, LaneRoster.Walking);
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
    /// so which side of the road it started from is a fact the book already has.
    /// </remarks>
    bool OnACrossing(int person, out int edge, out float alongM)
    {
        edge = CityPlan.NoRecord;
        alongM = 0f;
        if (!People.Walking[person]) return false;

        var way = People.OnWay[person];
        if (way != PersonFleet.NoWay && _footfall.WayIsLane(way)
            && _bands.CrossingOf(_footfall.WayIndex(way)) >= 0)
        {
            edge = _footfall.WayIndex(way);
            alongM = People.OnWayM[person];
            return true;
        }

        // About to step off. A red is the whole refusal and a body held by one is going nowhere, so it
        // holds no paint; a body waiting for a gap is about to take one and does.
        var ahead = People.CrossingAhead(person);
        if (ahead < 0) return false;
        if (_signals.CrossingIsLit(ahead) && _signals.ForCrossing(ahead, _elapsedS) != SignalColour.Green)
        {
            return false;
        }

        // At the kerb the body stands short of the way's own start, so it covers no band of it however far
        // back it is standing, and the first band is the one in front of it.
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
