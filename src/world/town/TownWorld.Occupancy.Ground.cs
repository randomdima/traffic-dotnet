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
    /// <b>The claim asked again, now that every body is in the book</b> (TER-5e): given back where a
    /// stronger movement has taken the ground.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A claim is the one hold in the town that can be taken back</b>, because its holder has not reached
    /// it and is not committed to it — which is the whole of what a right of way takes. Answered once, at the
    /// moment it was taken (<see cref="ManeuverDesk"/>), and then re-laid unread every tick, a claim outlived
    /// whatever came for the ground: a road an officer closed across it, a rescue's road, a body shoved onto
    /// it. The stronger movement was not cut and neither was the claim's holder, and the two of them were
    /// given the same metres.
    /// </para>
    /// <para>
    /// <b>An ordinary reservation over it is not this, and neither is a body standing on it</b>
    /// (<see cref="LaneOccupancy.TakesAClaim"/>). A claim lives on ground the traffic is also driving — the
    /// lane a car is queued in, the stretch a swerve swings out of and the body it is swinging round — and
    /// all of those cut the claimant's own grant already (SIM-7).
    /// </para>
    /// <para>
    /// <b>Given back inside the tick that laid it</b> (<see cref="LaneOccupancy.Withdraw"/>), so nothing
    /// granted afterwards is cut at ground whose holder has already let go of it.
    /// </para>
    /// </remarks>
    void AnswerTheClaim(int car)
    {
        var way = Cars.ClaimWay[car];
        if (way < 0) return;

        var mine = RightOfWayOf(car, way);
        var at = LaneOccupancy.FromTheStart;
        while (_occupancy.NextSpokenForOver(way, Cars.ClaimFromM[car], Cars.ClaimToM[car], car, ref at, out var taken))
        {
            if (!LaneOccupancy.TakesAClaim(taken, mine)) continue;

            _occupancy.Withdraw(way, car, LaneUse.Claimed);
            Cars.ClaimWay[car] = CarFleet.NoWay;
            Cars.ClaimWasTaken[car] = true;
            return;
        }
    }

    /// <summary>
    /// <b>A booking a wreck is holding is a place removed from the town</b>, and a wreck is the one way a
    /// leg ends without anybody being left to give it up (<see cref="ManeuverDesk.GiveUpTheBooking"/>).
    /// </summary>
    /// <remarks>
    /// It is the register's own release and not a claim on the road: what a car takes of the bay's way in on
    /// its way there is its reservation, laid and answered like every other (<see cref="AskForTheGround"/>).
    /// </remarks>
    void KeepTheBooking(int car)
    {
        if (Cars.Broken[car]) _parking.Release(car);
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
    /// <b>What it asks for is bounded by the rules and not by the traffic</b>, which is what the answer is
    /// for: the ask is laid before any of them is answered, so a car ahead cannot cut it here without making
    /// the book turn on who was written first. What is left of it once the answer is taken off is
    /// <see cref="CutTheGroundToTheGrant"/>, and that is what the book holds for the rest of the tick.
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
        // (<see cref="PastOnTheMovementM"/> reads the near edge of a car that is going nowhere).
        ref readonly var build = ref Cars.BuildOf(car);
        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
        Cars.ReserveFromM[car] = noseM - build.LengthM - build.TailMarginM - TheTowBehindItM(car);
        Cars.ReserveToM[car] = Cars.ReserveFromM[car];
        Cars.AuthorityM[car] = float.PositiveInfinity;
        Cars.GrantCutBy[car] = HeadwayKind.Nothing;
        if (!IsUnderWay(car)) return;

        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);

        // <b>Bounded by the pedal, because a car may not hold road it could not have driven over</b>
        // (<c>LaneOccupancyInATownTests</c>): the ground asked for is the ground reachable in a reaction
        // time, and a car that reserved for its planned speed from a standstill would be holding street it
        // had no way of reaching. That makes the stretch a function of the engine figure, which is why
        // honest pedals (CAR-45) shortened it across the fleet.
        var reachableMps = MathF.Min(
            Cars.PlannedMps[car], Cars.AlongMps[car] + (build.AccelerationMps2 * _config.CarReactionS));
        var committedM = (reachableMps * _config.CarReactionS) + StoppingM(reachableMps, brakingMps2)
                         + build.BodyMarginM;
        var heldAtM = MathF.Min(Cars.Context[car].StopAtM, Cars.Context[car].CrossingStopM);
        var wantedM = MathF.Max(StoppingM(Cars.AlongMps[car], brakingMps2), MathF.Min(committedM, heldAtM));

        Cars.ReserveToM[car] = noseM + wantedM;

        var count = WaysAlong(car, Cars.ReserveFromM[car], Cars.ReserveToM[car], ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            _occupancy.AddUnderWay(
                way.Way, way.FromM, OnTheWayM(way, noseM), way.ToM, Cars.AlongMps[car], car,
                right: RightOfWayOf(car, way.Way));
        }
    }

    /// <summary>
    /// <b>How far ahead of the rear axle the body's leading edge stands along the line it is driving</b> —
    /// its nose forwards, its tail backwards.
    /// </summary>
    /// <remarks>
    /// A line's metres run in the direction of travel whichever gear it is taken in (<c>CarFleet.AlongMps</c>),
    /// so a reversing body meets the book tail-first and the road it asks for begins there. Read as the nose
    /// either way, a car backing out of a bay asks for the road from three metres behind where it actually
    /// is and hands the metres it is standing on to whoever comes next.
    /// </remarks>
    float LeadingEdgeAheadOfTheAxleM(int car) =>
        Cars.LineIsReverse[car] ? Cars.BuildOf(car).TailBehindAxleM : Cars.BuildOf(car).NoseAheadOfAxleM;

    /// <summary>
    /// <b>How much further back a tow reaches</b> (EVA-5): the arm and the body on the end of it, and
    /// nothing at all for the town's every other car.
    /// </summary>
    /// <remarks>
    /// <b>One movement is one stretch</b> (TER-5c.2). A coupled pair is one thing moving down one road, so
    /// the vehicle pulling asks for the ground both of them stand on and the car on the bar asks for none
    /// (<see cref="PlaceWhatIsNotDriving"/>) — which is also what holds the traffic behind off the trailer
    /// rather than off the truck.
    /// </remarks>
    float TheTowBehindItM(int car)
    {
        var towed = _recovery.Towing[car];
        return towed < 0 ? 0f : TowBar.BehindTheTailM(Cars.BuildOf(car), Cars.BuildOf(towed));
    }

    /// <summary>
    /// Where a place on a line falls in the own metres of one of the ways under it, held to the stretch of
    /// that way the caller is laying. <b>Past the nose the answer is the near edge</b>: a way the body has
    /// not reached carries the grant and none of the car.
    /// </summary>
    static float OnTheWayM(in LineWay way, float lineM) =>
        Math.Clamp(way.FromM + (lineM - way.LineFromM), way.FromM, way.ToM);

    /// <summary>
    /// The same trip home: where a place in one way's own metres falls on the line that ran over it.
    /// <b>The pair of <see cref="OnTheWayM"/></b>, so that an answer carried out and an answer carried back
    /// cannot use two offsets.
    /// </summary>
    static float OnTheLineM(in LineWay way, float wayM) => way.LineFromM + (wayM - way.FromM);

    /// <summary>
    /// <b>The town's furniture, projected onto the lanes it stands on</b>, once, before the first tick — so
    /// that what a driver has to be held off is one book and not a book and a ray.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The nearest lane and the one running back the other way</b>, and no wider a search than that. A
    /// prop is a street's furniture and a carriageway is two lanes, so those are the ones a thing standing
    /// in the road can be standing in; anything broad enough to reach a third is a town that has built a
    /// wall across its own street, which is <see cref="StandingGround"/>'s stated bound.
    /// </para>
    /// <para>
    /// <b>The ground is asked before the road graph is.</b> A town's furniture is tens of thousands of
    /// things and nearly every one of them stands on grass, where the answer is known from the cell it is
    /// on: a prop with no drivable ground under its own footprint cannot lie inside any lane's band, and
    /// searching the network for its nearest lane is the most expensive way there is to find that out. On a
    /// city it is the difference between two seconds of standing a town up and a tenth of one.
    /// </para>
    /// </remarks>
    StandingGround StaticsOnTheRoad()
    {
        var into = new StandingGround.Builder();
        for (var prop = 0; prop < _plan.Props.Count; prop++)
        {
            var centreM = _plan.Props.CentreM[prop];
            var radiusM = _plan.Props.RadiusM[prop];
            if (!TouchesDrivableGround(centreM, radiusM)) continue;

            var lane = _roads.NearestLane(centreM, out _);
            if (lane < 0) continue;

            CoverTheLane(into, lane, centreM, radiusM);

            var back = _roads.LaneReverse[lane];
            if (back >= 0) CoverTheLane(into, back, centreM, radiusM);
        }

        return into.Seal();
    }

    /// <summary>
    /// Whether any of the ground a circle covers is ground a car drives on. <b>Every cell it reaches</b>,
    /// because a prop half a metre off the kerb still has its far edge in the road.
    /// </summary>
    bool TouchesDrivableGround(Vector2 centreM, float radiusM)
    {
        for (var acrossM = -radiusM; acrossM <= radiusM; acrossM += _terrain.CellSizeM)
        {
            for (var alongM = -radiusM; alongM <= radiusM; alongM += _terrain.CellSizeM)
            {
                if (_terrain.At(centreM + new Vector2(alongM, acrossM)).Drivable) return true;
            }
        }

        return _terrain.At(centreM + new Vector2(radiusM, radiusM)).Drivable
               || _terrain.At(centreM + new Vector2(-radiusM, radiusM)).Drivable
               || _terrain.At(centreM + new Vector2(radiusM, -radiusM)).Drivable
               || _terrain.At(centreM + new Vector2(-radiusM, -radiusM)).Drivable;
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
    /// <remarks>
    /// The book's own figure (<see cref="LaneCredit.StoppingM"/>), because what a driver asks for behind
    /// itself and what a walker asks for behind itself are the same arithmetic and must stay one.
    /// </remarks>
    static float StoppingM(float alongMps, float brakingMps2) =>
        LaneCredit.StoppingM(alongMps, brakingMps2);

    /// <summary>
    /// <b>What the car actually got</b>: its own stretch, cut at the near edge of the ground anything in
    /// front of it holds, as a distance from its nose to the far end of the room it may stop in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The cut is at the bumper of what is in front and never past it</b> (TER-4c.1). A stretch is
    /// answered at the near edge of the ground that body has — its own tail less the margin it keeps
    /// (<see cref="SimConfig.CarBodyMarginM"/>), which is what a queue at rest stands at — and there is
    /// nothing beyond that edge for this car to be given, whatever the body in front is doing: the answer is
    /// written back into the book (<see cref="CutTheGroundToTheGrant"/>), so a metre granted past a near edge
    /// is a metre two bodies hold.
    /// </para>
    /// <para>
    /// <b>The least of them and never the nearest.</b> The nearest stretch may be a claim, which the asker
    /// keeps its own margin clear of (<see cref="LaneCredit.AtAPlaceM"/>) and is cut a margin short of — so
    /// the edge that comes first is not always the metre that binds, and cutting at it grants this car the
    /// road through whatever is beyond.
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

        ref readonly var build = ref Cars.BuildOf(car);
        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);
        var grantedToM = float.PositiveInfinity;
        var cutBy = HeadwayKind.Nothing;

        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
        var count = WaysAlong(car, Cars.ReserveFromM[car], LookForTheCutToM(car, brakingMps2), ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];

            // <b>A way the nose has already left is asked nothing.</b> The nose is carried onto a way by
            // clamping (<see cref="OnTheWayM"/>), so on a way that ends behind it the question becomes a
            // window of no length at that way's far edge — and anything reaching that edge answers from
            // behind the body, which is the fault <see cref="WhereTheGroundIsCrossed"/> names: a cut behind
            // the nose is not a shorter grant, it is a grant that has stopped being a distance. It froze a
            // car whose tail was still on a join the car behind it had claimed the far end of, and nothing
            // about the road in front could ever let it go.
            var noseOnTheWayM = OnTheWayM(way, noseM);
            if (noseOnTheWayM >= way.ToM) continue;

            // The terms this car is cut on, which the book applies and does not decide: the ground it keeps
            // off whatever is going nowhere, and the rank it holds this way's ground with.
            var asker = new LaneCredit(build.BodyMarginM, LaneRoster.Driving, RightOfWayOf(car, way.Way));

            // In front of the nose and not of the ground this car holds: every stretch begins a margin
            // behind its owner, so a walk taken from the near edge of this car's own would answer with the
            // body behind it (<see cref="LaneOccupancy.NextSpokenFor"/>).
            var cutM = _occupancy.GrantedOn(way.Way, noseOnTheWayM, way.ToM, car, asker, out var heldBy);
            if (float.IsFinite(cutM) && OnTheLineM(way, cutM) < grantedToM)
            {
                grantedToM = OnTheLineM(way, cutM);
                cutBy = KindOf(heldBy.Use);
            }

            // A crossing point is a place and has no margin of its own, so the asker's is taken off it here
            // — the one cut in the town that is not made at somebody else's stretch, on the same figure.
            var crossedAtM = WhereTheGroundIsCrossed(car, way, noseOnTheWayM, asker.Right, out var by);
            if (float.IsFinite(crossedAtM) && OnTheLineM(way, crossedAtM) + asker.AtAPlaceM < grantedToM)
            {
                grantedToM = OnTheLineM(way, crossedAtM) + asker.AtAPlaceM;
                cutBy = KindOf(by);
            }
        }

        if (float.IsPositiveInfinity(grantedToM)) return;

        Cars.AuthorityM[car] = grantedToM - noseM;
        Cars.GrantCutBy[car] = cutBy;
    }

    /// <summary>
    /// <b>The road the car asked for, brought back to the road it got</b> (TER-4c.1) — so that what the book
    /// holds for the rest of the tick is the answer and not the question.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two bodies cannot be granted one metre, and until this pass they were.</b> The ask is bounded by
    /// the rules that stop the car and by nothing in front of it (<see cref="AskForTheGround"/>); the answer
    /// is where it is cut at the traffic (<see cref="GrantTheGround"/>), and it was written to
    /// <c>CarFleet.AuthorityM</c> and nowhere else. Every other reader of the book — the junction gate, the
    /// manoeuvres, the walkers at a kerb — therefore read the question: a car held at a red still held the
    /// sweep of road beyond it, and the movements that road crossed were refused by ground its holder had
    /// been refused itself, which is a junction that jams with the room to clear it.
    /// </para>
    /// <para>
    /// <b>A pass of its own, after every grant and never inside one.</b> Grants are cut at near edges, which
    /// this does not move — but what ground a movement is driven over is a question about far edges
    /// (<see cref="WhereTheGroundIsCrossed"/>), and cutting one car's stretch while the next car's grant is
    /// still to be taken would make the answer depend on which of them was asked first. That is the whole
    /// reason the asks and the grants are two walks (<see cref="RebuildLaneOccupancy"/>), and this is the
    /// third.
    /// </para>
    /// <para>
    /// <b>And on the join a car is crossing the seam moves rather than the union</b>
    /// (<see cref="ClaimWhatTheAnswerTook"/>). What such a car holds there is its road and the claim beyond
    /// it as one piece of ground; cut without the claim following, the metres between the answer and the ask
    /// fall out of both.
    /// </para>
    /// </remarks>
    void CutTheGroundToTheGrant(int car, Span<LineWay> ways)
    {
        if (Cars.ReserveToM[car] <= Cars.ReserveFromM[car]) return;
        if (float.IsPositiveInfinity(Cars.AuthorityM[car])) return;

        var grantedToM = GroundEndsAtM(car);
        var count = WaysAlong(car, Cars.ReserveFromM[car], Cars.ReserveToM[car], ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            _occupancy.CutTo(way.Way, car, OnTheWayM(way, grantedToM));
        }

        var movementWay = Cars.MovementWay[car];
        if (movementWay != CarFleet.NoWay)
        {
            ClaimWhatTheAnswerTook(car, movementWay, Cars.ReserveToM[car], grantedToM);
        }
    }

    /// <summary>
    /// <b>Where the road this car holds ends on its own line</b> — the ask (<see cref="AskForTheGround"/>)
    /// brought in to the answer (<see cref="GrantTheGround"/>), which is what the book holds once
    /// <see cref="CutTheGroundToTheGrant"/> has run.
    /// </summary>
    /// <remarks>
    /// <b>Never behind the nose.</b> A grant is how far a nose may go and goes to nought, or below it, where
    /// a car is held at a bumper (<see cref="GrantTheGround"/>); the ground under the body is not a grant and
    /// is not the car's to give back.
    /// </remarks>
    public float GroundEndsAtM(int car)
    {
        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
        return MathF.Max(noseM, MathF.Min(Cars.ReserveToM[car], noseM + Cars.AuthorityM[car]));
    }

    /// <summary>
    /// <b>The first metre of one of this car's own ways that somebody else's ground is driven over</b>, in
    /// that way's own metres, or infinity where none of it is. The town says once, when it is laid, where
    /// each way through a junction crosses each other one (<see cref="WayCrossings"/>); a driver looks
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
    /// <b>In front means in front of the nose</b>, exactly as it does for a stretch
    /// (<see cref="LaneOccupancy.NextSpokenFor"/>, which is asked from the same metre): a crossing point the
    /// body is standing over is one it has arrived at, and a grant cut at it would be a car braking for the
    /// corner it came in by. <b>What a grant says is how far a nose may go</b>, so a cut behind that nose is
    /// not a shorter grant — it is a grant that has stopped being a distance, and it read as a car eighteen
    /// metres inside somebody else's road while it was doing nothing but sitting on a junction it had crossed.
    /// </para>
    /// <para>
    /// <b>The cut is at the near edge of the section and carries no credit past it</b>, where the cut at a
    /// body carries that body's stopping distance. There is nothing on the section to come to rest — it is a
    /// place, and what is standing on it is standing on another way's metres, at a pose and a heading this
    /// car has no reading of.
    /// </para>
    /// <para>
    /// <b>Every way is asked, lanes included.</b> A lane is driven over by nothing a junction admits, since
    /// the lanes hand over clear of the box (TER-5d) — but the way into a parking space leaves a lane
    /// part-way along it and sweeps the one running back the other way, so a lane's row is empty on most
    /// streets and is not empty on a street with a car park on it. The walk over an empty row is a bounds
    /// check, which is what makes asking every way affordable on every way every car is on.
    /// </para>
    /// <para>
    /// <b>And ground a greater right of way has taken off somebody is not a cut</b> (TER-5e). A movement
    /// this one is driven over reads its own claim on that ground as given up the moment the stronger
    /// movement asks for it, so the pair of them are cut one way round rather than both — which is the
    /// whole of the difference between a right of way and a deadlock.
    /// </para>
    /// </remarks>
    float WhereTheGroundIsCrossed(
        int car, in LineWay way, float noseOnTheWayM, RightOfWay mine, out LaneUse held)
    {
        held = LaneUse.Reserved;
        var leastM = float.PositiveInfinity;
        foreach (ref readonly var section in _crossings.Of(way.Way))
        {
            if (section.MineFromM < noseOnTheWayM || section.MineFromM > way.ToM) continue;
            if (section.MineFromM >= leastM) continue;

            if (!SomebodyElseHolds(car, section.OnWay, section.FromM, section.ToM, mine, out var by)) continue;

            leastM = section.MineFromM;
            held = by;
        }

        return leastM;
    }

    /// <summary>
    /// <b>Whether a named piece of another way is ground an asker with this right of way is refused by</b>:
    /// everything lying over it that <see cref="LaneOccupancy.Binds"/> says the asker must give way to.
    /// </summary>
    /// <remarks>
    /// <b>Every stretch over the section and not the first one</b>: the first may be a claim this asker
    /// takes, and a car that stopped walking there would be granted the ground through whatever was holding
    /// the same metres behind it.
    /// </remarks>
    bool SomebodyElseHolds(int car, int way, float fromM, float toM, RightOfWay mine, out LaneUse held)
    {
        var at = LaneOccupancy.FromTheStart;
        while (_occupancy.NextSpokenForOver(way, fromM, toM, car, ref at, out var taken))
        {
            if (!LaneOccupancy.Binds(taken, mine)) continue;

            held = taken.Use;
            return true;
        }

        held = LaneUse.Reserved;
        return false;
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
        ref readonly var build = ref Cars.BuildOf(car);
        var alongMps = MathF.Max(0f, Cars.AlongMps[car]);
        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
        return MathF.Max(
            Cars.ReserveToM[car],
            noseM + StoppingM(alongMps, brakingMps2) + (alongMps * _config.Driving.FollowingHeadwayS)
            + build.BodyMarginM + build.LengthM);
    }
}
