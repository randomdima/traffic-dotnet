using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The ask and the answer</b>: the road each car is committed to, the claim it holds ahead of that, the
/// town's furniture standing in the way of both, and what is left of the road once everything already
/// spoken for has been taken out of it.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>The stretch this car has claimed and is not on yet, laid where its own field says.</summary>
    /// <remarks>
    /// <para>
    /// Re-laid from the car every tick, so nothing is released and nothing leaks: a claim held by a car that
    /// has been wrecked, unmanned or taken over by a hand is gone on the next rebuild without anything
    /// having had to notice it.
    /// </para>
    /// <para>
    /// <b>And never over ground this body is already holding</b> (TER-5c.2). A car waits in the mouth of its
    /// bay on a claim and then leaves on a template, and the sweep that template makes is over the very lane
    /// the claim was taken on (<see cref="PlaceWhatIsNotDriving"/>) — one body, laid twice, in two measures
    /// of one piece of ground. The sweep is the reading taken from the body and it is the one that stands.
    /// </para>
    /// </remarks>
    void PlaceTheClaim(int car)
    {
        var way = Cars.ClaimWay[car];
        if (way < 0) return;

        if (!Cars.Driven[car] || Cars.Broken[car])
        {
            Cars.ClaimWay[car] = CarFleet.NoWay;
            return;
        }

        var fromM = Cars.ClaimFromM[car];
        var toM = Cars.ClaimToM[car];
        if (_occupancy.AlreadyHolds(way, fromM, toM, car)) return;

        _occupancy.Add(way, fromM, toM, Cars.AlongMps[car], car, LaneUse.Claimed);
    }

    /// <summary>
    /// <b>The road this car is committed to, and the car</b>: one stretch, from its own tail through its
    /// nose to where that nose comes to rest if it holds the pedal it has until its next decision and then
    /// stops, plus the gap it keeps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is what the car cannot undo and not what it would like</b> — one reaction interval of ground at
    /// the fastest that interval can leave it doing, and a stop from there. A car planning for a speed it is
    /// nowhere near does not hold the road up to it: the profile's own figure is the ceiling on this and
    /// never the size of it, or a car pulling out of a junction would reserve the two hundred metres its top
    /// speed would take to shed and hold a street shut to do it.
    /// </para>
    /// <para>
    /// <b>And it still leaves the room to pull away</b>, which sizing by the speed the car is doing would
    /// not: a stopped car is committed to whatever a reaction interval of its own acceleration reaches, so
    /// what it asks for grows with the pedal rather than with the speed the pedal has produced. The grant
    /// that comes back off an uncut ask always inverts to at least that same speed, so this bounds nothing
    /// the car could actually have done.
    /// </para>
    /// <para>
    /// <b>And never past the place a rule holds it at</b> — a red, a bar, a crossing it must stop short of, a
    /// box whose ground is somebody's (S-4, TER-4c.1). Ground beyond a stop point is ground the car is not
    /// committed to whatever its pedal is doing, and holding it would queue the traffic behind further up the
    /// road than any of it is going to get — and, where the stop point is a zebra, would hold the paint shut
    /// against the very people the stop was made for.
    /// </para>
    /// <para>
    /// <b>The margin it keeps is part of what it asks for, at both ends of it</b>
    /// (<see cref="SimConfig.CarBodyMarginM"/> in front, <see cref="SimConfig.CarTailMarginM"/> behind). In
    /// front it is clamped with the rest of the ask — added after the clamp it was a margin of ground held
    /// past every bar in the town, and a car stopped a metre short of a crossing held a metre of the
    /// crossing. Behind, it is the ground the book's one-dimensional reading of a swinging body owes whoever
    /// comes next, and <b>it is a stretch of this same reservation on every way the car is on</b> rather than
    /// a claim of its own on a junction's join: one body, one stretch, one piece of ground (TER-5c.2).
    /// </para>
    /// </remarks>
    void AskForTheGround(int car, Span<LineWay> ways)
    {
        // Where this car's ground begins is a fact about the body and is filled for every car with a line,
        // driven or not: what says it asked for no road is that the stretch has no length
        // (<see cref="PastOnTheCrossing"/> reads the near edge of a car that is going nowhere).
        var noseM = Cars.ProgressM[car] + _config.CarNoseAheadOfAxleM;
        Cars.ReserveFromM[car] = noseM - _config.Car.LengthM - _config.CarTailMarginM;
        Cars.ReserveToM[car] = Cars.ReserveFromM[car];
        Cars.AuthorityM[car] = float.PositiveInfinity;
        if (!IsUnderWay(car)) return;

        var brakingMps2 = CarFollower.BrakingMps2(_config, Cars.GroundCoefficient[car]);
        var reachableMps = MathF.Min(
            Cars.PlannedMps[car], Cars.AlongMps[car] + (_config.Car.AccelerationMps2 * _config.CarReactionS));
        var committedM = (reachableMps * _config.CarReactionS) + StoppingM(reachableMps, brakingMps2)
                         + _config.CarBodyMarginM;
        var heldAtM = MathF.Min(Cars.Context[car].StopAtM, Cars.Context[car].CrossingStopM);
        var wantedM = MathF.Max(StoppingM(Cars.AlongMps[car], brakingMps2), MathF.Min(committedM, heldAtM));

        Cars.ReserveToM[car] = noseM + wantedM;

        var count = WaysAlong(car, Cars.ReserveFromM[car], Cars.ReserveToM[car], ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            _occupancy.AddUnderWay(
                way.Way, way.FromM, OnTheWayM(way, noseM), way.ToM, Cars.AlongMps[car], car);
        }
    }

    /// <summary>
    /// Where a place on a line falls in the own metres of one of the ways under it, held to the stretch of
    /// that way the caller is laying. <b>Past the nose the answer is the near edge</b>: a way the body has
    /// not reached carries the grant and none of the car.
    /// </summary>
    static float OnTheWayM(in LineWay way, float lineM) =>
        Math.Clamp(way.FromM + (lineM - way.LineFromM), way.FromM, way.ToM);

    /// <summary>
    /// <b>The town's furniture, projected onto the lanes it stands on</b>, once, before the first tick — so
    /// that what a driver has to be held off is one book and not a book and a ray.
    /// </summary>
    /// <remarks>
    /// <b>The nearest lane and the one running back the other way</b>, and no wider a search than that. A
    /// prop is a street's furniture and a carriageway is two lanes, so those are the ones a thing standing
    /// in the road can be standing in; anything broad enough to reach a third is a town that has built a
    /// wall across its own street, which is <see cref="StandingGround"/>'s stated bound.
    /// </remarks>
    StandingGround StaticsOnTheRoad()
    {
        var into = new StandingGround.Builder();
        for (var prop = 0; prop < _plan.Props.Count; prop++)
        {
            var centreM = _plan.Props.CentreM[prop];
            var radiusM = _plan.Props.RadiusM[prop];

            var lane = _roads.NearestLane(centreM, out _);
            if (lane < 0) continue;

            CoverTheLane(into, lane, centreM, radiusM);

            var back = _roads.LaneReverse[lane];
            if (back >= 0) CoverTheLane(into, back, centreM, radiusM);
        }

        return into.Seal();
    }

    /// <summary>
    /// The stretch of one lane a circle standing beside it covers, or nothing where it stands clear of the
    /// lane's own band.
    /// </summary>
    void CoverTheLane(StandingGround.Builder into, int lane, Vector2 centreM, float radiusM)
    {
        var arcs = _roads.ArcsOf(lane);
        var lengthM = _roads.LaneLengthM[lane];
        var alongM = Spline.ProjectM(arcs, centreM, lengthM * 0.5f, lengthM);
        if (!RoadGraph.WithinTheBand(arcs, alongM, centreM, _roads.LaneWidthM[lane], radiusM, radiusM, out _))
        {
            return;
        }

        into.Add(lane, alongM - radiusM, alongM + radiusM);
    }

    /// <summary>How much road a body doing this speed needs before it can be at rest on the ground it is on.</summary>
    static float StoppingM(float alongMps, float brakingMps2) =>
        alongMps <= 0f ? 0f : alongMps * alongMps / (2f * brakingMps2);

    /// <summary>
    /// <b>What the car actually got</b>: its own stretch, cut at the nearest place anything in front of it
    /// will come to rest, as a distance from its nose to the far end of the room it may stop in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The cut is at the resting place of what is in front and not at its bumper.</b> A stretch is
    /// answered at the near edge of the ground that body has — its own tail less the margin it keeps
    /// (<see cref="SimConfig.CarBodyMarginM"/>), which is what a queue at rest stands at — and what the
    /// ground beyond that edge is worth to this car is that body's own stopping distance — nothing for a wreck or a claim, which are not going
    /// anywhere, and a whole stopping distance for a car at speed. Two cars running at the same speed
    /// therefore keep a standing gap rather than opening out to a stopping distance apiece.
    /// </para>
    /// <para>
    /// <b>The least of them and never the nearest.</b> A car at speed rests further up the road than a
    /// slower car ahead of it, so the stretch whose near edge comes first is regularly not the one that
    /// binds — and cutting at it grants this car the road through whatever is beyond.
    /// </para>
    /// <para>
    /// <b>The credit is worked out against the grip this car has and not the grip that one has.</b> How
    /// fast the thing in front is going is something a driver can see; what it is standing on is not, and
    /// a book that handed out the answer would be handing out a reading no driver could take.
    /// </para>
    /// <para>
    /// <b>And it is cut by the ways this car is driven <em>over</em> as well as by the ways it is
    /// driving</b> (<see cref="WhereTheGroundIsCrossed"/>). A reservation is a lane's, and the ground is the
    /// town's: two ways that meet inside a junction are one piece of the world, so a grant that stopped at
    /// the edge of its own way would be two cars each given the metre they meet on.
    /// </para>
    /// <para>
    /// <b>A car nothing cut is held by nobody</b>, and its grant stays infinite rather than coming back as
    /// the length of its own ask. The ask is what this car is committed to and the profile has already
    /// bound itself to it; handing it back as a limit would make a car alone on an empty road read as one
    /// queueing behind itself.
    /// </para>
    /// <para>
    /// Negative where the car cannot stop in what is left, which is a fact about a contact rather than
    /// about a gap and is left to say so.
    /// </para>
    /// </remarks>
    void GrantTheGround(int car, Span<LineWay> ways)
    {
        if (Cars.ReserveToM[car] <= Cars.ReserveFromM[car]) return;

        var brakingMps2 = CarFollower.BrakingMps2(_config, Cars.GroundCoefficient[car]);
        var grantedToM = float.PositiveInfinity;

        var noseM = Cars.ProgressM[car] + _config.CarNoseAheadOfAxleM;
        var count = WaysAlong(car, Cars.ReserveFromM[car], LookForTheCutToM(car, brakingMps2), ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];

            // In front of the nose and not of the ground this car holds: every stretch begins a margin
            // behind its owner, so a walk taken from the near edge of this car's own would answer with the
            // body behind it (<see cref="LaneOccupancy.NextSpokenFor"/>).
            var at = LaneOccupancy.FromTheStart;
            while (_occupancy.NextSpokenFor(
                       way.Way, OnTheWayM(way, noseM), way.ToM, car, ref at, out var taken))
            {
                // A wreck and a claim are going nowhere, so where they lie is where they rest; a driver at
                // speed is worth its own stopping distance beyond its tail. <b>And a reservation is the one
                // stretch that carries a margin of its own</b> (TER-5c.2), so the cut at one is where its
                // ground begins — where a wreck, a claim, a body on foot and the town's own furniture have
                // none, and this car keeps its own off them.
                var creditM = taken.Use == LaneUse.Reserved
                    ? StoppingM(taken.AlongMps, brakingMps2)
                    : -_config.CarBodyMarginM;

                grantedToM = MathF.Min(grantedToM, way.LineFromM + (taken.FromM - way.FromM) + creditM);
            }

            // A crossing point is a place and has no margin of its own, so the asker's is taken off it here
            // — the one cut in the town that is not made at somebody else's stretch.
            var crossedAtM = WhereTheGroundIsCrossed(car, way);
            if (float.IsFinite(crossedAtM))
            {
                grantedToM = MathF.Min(
                    grantedToM, way.LineFromM + (crossedAtM - way.FromM) - _config.CarBodyMarginM);
            }
        }

        if (float.IsPositiveInfinity(grantedToM)) return;

        Cars.AuthorityM[car] = grantedToM - noseM;
    }

    /// <summary>
    /// <b>The first metre of one of this car's own ways that somebody else's ground is driven over</b>, in
    /// that way's own metres, or infinity where none of it is. The town says once, when it is laid, where
    /// each way through a junction crosses each other one (<see cref="JunctionCrossings"/>); a driver looks
    /// its own way up in that table and reads the far side of every section it finds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the whole of what makes a lane's reservation mean something globally.</b> A stretch of a
    /// way is a piece of the world and not a piece of a lane, and inside a junction the pieces overlap; a
    /// car that only ever read the way it was driving would be granted the metre two lines meet on at the
    /// same time as the car on the other line. What it does not do is take ground it will never be on —
    /// which is what marking the crossed ways did, and what a junction under a fan of one car's claims
    /// looked like.
    /// </para>
    /// <para>
    /// <b>In front means the section's own near edge is</b>, exactly as it does for a stretch
    /// (<see cref="LaneOccupancy.NextSpokenFor"/>): a crossing point behind the car's tail is one this body
    /// has been over, and a grant cut at it would be a car braking for the corner it came in by.
    /// </para>
    /// <para>
    /// <b>The cut is at the near edge of the section and carries no credit past it</b>, where the cut at a
    /// body carries that body's stopping distance. There is nothing on the section to come to rest — it is a
    /// place, and what is standing on it is standing on another way's metres, at a pose and a heading this
    /// car has no reading of.
    /// </para>
    /// <para>
    /// Only a join is asked. Two lanes of the town are laid side by side and end where the box begins
    /// (TER-5d), so the ground where movements meet is a join's and the table has nothing to say about the
    /// rest.
    /// </para>
    /// </remarks>
    float WhereTheGroundIsCrossed(int car, in LineWay way)
    {
        if (_occupancy.WayIsLane(way.Way)) return float.PositiveInfinity;

        var leastM = float.PositiveInfinity;
        foreach (ref readonly var section in _roads.Crossings.Of(_occupancy.WayIndex(way.Way)))
        {
            if (section.MineFromM < way.FromM || section.MineFromM > way.ToM) continue;
            if (section.MineFromM >= leastM) continue;

            if (_occupancy.SpokenForByAnother(
                    _occupancy.WayOfTurn(section.OnTurn), section.FromM, section.ToM, car, out _))
            {
                leastM = section.MineFromM;
            }
        }

        return leastM;
    }

    /// <summary>
    /// <b>How far up the line the cut is looked for, which is not how far the ask reached.</b> What a car
    /// lays into the book is the road it is committed to; what it has to be told about is the road it means
    /// to be keeping — the stop, the gap of a following time in front of that, and a body's length of margin
    /// past it.
    /// </summary>
    /// <remarks>
    /// <b>Looking further can only make a grant smaller</b>, because the grant is the least of everything
    /// found — so nothing about the safety of it turns on this figure and only the fluency does. Looked for
    /// no further than the ask, the grant simply went uncut whenever the car was more than a stopping
    /// distance behind: the profile then held the car off on the headway reading, at the reaction lead,
    /// until it had closed to inside its own reservation — and the pair of them settled into closing up and
    /// falling back rather than into a gap.
    /// </remarks>
    float LookForTheCutToM(int car, float brakingMps2)
    {
        var alongMps = MathF.Max(0f, Cars.AlongMps[car]);
        var noseM = Cars.ProgressM[car] + _config.CarNoseAheadOfAxleM;
        return MathF.Max(
            Cars.ReserveToM[car],
            noseM + StoppingM(alongMps, brakingMps2) + (alongMps * _config.Driving.FollowingHeadwayS)
            + _config.CarBodyMarginM + _config.Car.LengthM);
    }
}
