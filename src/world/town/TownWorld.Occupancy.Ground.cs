using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The ask and the answer</b>: the road each car is committed to, the town's furniture standing in the
/// way of it, and what is left of that road once everything already spoken for has been taken out of it.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>A bay a wreck has claimed is a place removed from the town</b>, and a wreck is the one way a
    /// leg ends without anybody being left to give it up (<see cref="GiveUpTheBay"/>).
    /// </summary>
    /// <remarks>
    /// It is the register's own release and not a claim on the road: what a car takes of the bay's way in on
    /// its way there is its own claim, laid and answered like every other (<see cref="AskForTheGround"/>).
    /// </remarks>
    void KeepTheBay(int car)
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
    /// never the size of it, or a car pulling out of a junction would claim the two hundred metres its top
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
    /// the answer turn on who was written first. What is left of it once the answer is taken off is
    /// <see cref="CutTheGroundToTheGrant"/>, and that is what the claim holds for the rest of the tick.
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
    /// crossing. Behind, it is the ground a one-dimensional reading of a swinging body owes whoever
    /// comes next, and <b>it is a stretch of this same claim on every way the car is on</b> rather than
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
        Cars.ClaimFromM[car] = noseM - build.LengthM - build.TailMarginM - TheTowBehindItM(car);
        Cars.ClaimToM[car] = Cars.ClaimFromM[car];
        Cars.StatedToM[car] = Cars.ClaimFromM[car];
        Cars.StatedGrantM[car] = float.PositiveInfinity;
        Cars.AuthorityM[car] = float.PositiveInfinity;
        Cars.GrantCutBy[car] = HeadwayKind.Nothing;
        if (!IsUnderWay(car)) return;

        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);

        // <b>Bounded by the pedal, because a car may not hold road it could not have driven over</b>
        // (<c>LaneOccupancyInATownTests</c>): the ground asked for is the ground reachable in a reaction
        // time, and a car that claimed for its planned speed from a standstill would be holding street it
        // had no way of reaching. That makes the stretch a function of the engine figure, which is why
        // honest pedals (CAR-45) shortened it across the fleet.
        var reachableMps = MathF.Min(
            Cars.PlannedMps[car], Cars.AlongMps[car] + (build.AccelerationMps2 * _config.CarReactionS));
        var committedM = (reachableMps * _config.CarReactionS) + StoppingM(reachableMps, brakingMps2)
                         + build.BodyMarginM;
        var heldAtM = MathF.Min(Cars.Context[car].StopAtM, Cars.Context[car].CrossingStopM);
        var wantedM = MathF.Max(StoppingM(Cars.AlongMps[car], brakingMps2), MathF.Min(committedM, heldAtM));

        Cars.ClaimToM[car] = noseM + wantedM;

        // <b>As far as it gets and no further</b> (TER-5c.2): one stretch of road over however many ways the
        // line is numbered in, so a way that would not take the whole of it is where the hold ends. What the
        // body itself covers past that is written from the pose instead (<see cref="PlaceTheBody"/>).
        var count = WaysAlong(car, Cars.ClaimFromM[car], Cars.ClaimToM[car], ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            // <b>At the rung nothing takes</b> (TER-5g): what this claim holds is the body and the road the
            // body can no longer give back, and neither is anybody's to be granted. The movement it is on
            // decides what the car may be granted <em>ahead</em> of this and never what it is standing on.
            var whole = _occupancy.ClaimUnderWay(
                way.Way, way.FromM, OnTheWayM(way, noseM), way.ToM, Cars.AlongMps[car], car);

            if (!whole) break;
        }

        StateTheRoadItMeansToUse(car, noseM, wantedM, heldAtM, brakingMps2, ways);
        StateTheRoadToTheMovement(car, ways);
    }

    /// <summary>
    /// <b>The road beyond the one it is committed to that this car means to use</b> (TER-5g): what it takes
    /// to get up to the speed it is planning for, hold that speed for as long as it says it will
    /// (<see cref="DrivingFigures.StatedRunS"/>), and stop from there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the ask the committed claim deliberately is not</b> (<see cref="AskForTheGround"/>) — and the
    /// difference between the two is the whole of this tier. A driver already <em>reads</em> further up the
    /// road than it writes (<see cref="LookForTheCutToM"/>), so before this every car in the town could see
    /// what the others were committed to and none of them said where it was going. What that costs is a car
    /// pulling out of a side road in front of one coming at speed: nothing on the road said it was coming.
    /// </para>
    /// <para>
    /// <b>What makes it affordable is that it can be taken</b> (<see cref="ClaimPriority.Soft"/>). The
    /// same ask was once held as ground the car was committed to and took a quarter of a kilometre of empty
    /// straight off everybody, because nothing could ask for it back; laid as ground a stronger movement is
    /// entitled to, the same reach costs the traffic that outranks it nothing at all.
    /// </para>
    /// <para>
    /// <b>Bounded by the same rules the committed claim is bounded by</b> — a red, a bar, a crossing, a box
    /// this car has not been given — which is the whole of what "a red light refuses the soft claim" comes
    /// to: a car stopped at a bar states nothing beyond it, so the arm with the green is refused by
    /// nothing the arm with the red is thinking about. A car with a blue light is not stopped by the bar
    /// (AMB-4) and neither is what it states; a car past the point it could stop is stopped by nothing and
    /// neither is what it states.
    /// </para>
    /// <para>
    /// <b>And bounded by the line the car actually has.</b> A line is drawn as far as the car can see
    /// (CAR-11), which is a stop from its own top speed and slightly short of what this ask wants; ground
    /// past the end of it is ground nothing has worked out a route over, so the ask is clamped to the line
    /// rather than the line grown to the ask.
    /// </para>
    /// <para>
    /// <b>A body that is not moving states nothing</b> (TER-5g), and that is the load-bearing half of
    /// this. A stated claim says where a car is <em>going</em>, and a car at rest is going nowhere until it
    /// moves; laid from a standstill it is the pull-away horizon of every car in every queue in the town,
    /// held against every movement those queues cross. The exam is where it shows: a car at a give-way line
    /// held a box shut against the traffic it was itself waiting for, and the weaker movement in seven cards
    /// of thirty-six never went at all. <b>Stopped is a pace and never zero</b>
    /// (<see cref="DrivingFigures.StopSpeedMps"/>), because a car creeping in a queue is doing fractions of
    /// a millimetre a second and would otherwise be stating a road for as long as it sat there.
    /// </para>
    /// <para>
    /// <b>The road to a box this car already holds is not this ask</b> and is stated beside it
    /// (<see cref="StateTheRoadToTheMovement"/>), which is why a body at rest can still be saying something:
    /// this one is where the car is going, and that one is the span between it and ground it has been given.
    /// </para>
    /// <para>
    /// <b>And the way the car is crossing on is stated up to its own claim and no further.</b> What it holds
    /// there is its movement's claim (<see cref="LayTheMovement"/>), so the statement is cut back where the
    /// two meet and a second stretch over the same metres would be this car counted twice (TER-5c.2).
    /// </para>
    /// </remarks>
    void StateTheRoadItMeansToUse(
        int car, float noseM, float committedM, float heldAtM, float brakingMps2, Span<LineWay> ways)
    {
        var alongMps = MathF.Max(0f, Cars.AlongMps[car]);
        if (alongMps <= _config.Driving.StopSpeedMps) return;

        var plannedMps = Cars.PlannedMps[car];

        // Up to the speed the profile is driving towards, a decision interval of it, and a stop from there.
        var uphillM = plannedMps <= alongMps
            ? 0f
            : ((plannedMps * plannedMps) - (alongMps * alongMps)) / (2f * Cars.BuildOf(car).AccelerationMps2);
        var meansToM = uphillM + (plannedMps * _config.Driving.StatedRunS)
                       + StoppingM(plannedMps, brakingMps2) + Cars.BuildOf(car).BodyMarginM;

        Cars.StatedToM[car] = MathF.Min(
            noseM + MathF.Max(committedM, MathF.Min(meansToM, heldAtM)), Cars.Line[car].LengthM);
        if (Cars.StatedToM[car] <= Cars.ClaimToM[car]) return;

        var count = WaysAlong(car, Cars.ClaimToM[car], Cars.StatedToM[car], ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];

            // Its near edge is its own body edge, because there is no body in it: ground somebody has
            // already reached is behind them, and a claim nothing stands in can be cut to nothing.
            // <b>And it stops at the first way that would not take the whole of it</b> (TER-5c.2), like
            // every other stretch laid over a run of ways.
            var whole = _occupancy.ClaimAhead(
                way.Way, way.FromM, way.ToM, Cars.AlongMps[car], car, SoftOf(car, way.Way));

            if (!whole) break;
        }
    }

    /// <summary>
    /// <b>How far ahead of the rear axle the body's leading edge stands along the line it is driving</b> —
    /// its nose forwards, its tail backwards.
    /// </summary>
    /// <remarks>
    /// A line's metres run in the direction of travel whichever gear it is taken in (<c>CarFleet.AlongMps</c>),
    /// so a reversing body is claimed tail-first and the road it asks for begins there. Read as the nose
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
    /// the vehicle pulling asks for the ground both of them stand on — which is also what holds the traffic
    /// behind off the trailer rather than off the truck. What this claim does not reach is ground off
    /// the line the pair is driving, and the trailer's own body covers that under this same number
    /// (<see cref="LaidAs"/>).
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
    /// that what a driver has to be held off is a claim and not a claim and a ray.
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
        // <b>The ground is asked of every prop at once and the lanes are covered a prop at a time.</b> The
        // question above — is any of this thing standing in the road — is asked of standing ground that
        // nothing here writes to, and a city asks it of a hundred thousand things. What is done with a yes
        // writes into the builder, where the order the runs arrive in is the order they are strung, so that
        // half stays the walk it was.
        var touches = new bool[_plan.Props.Count];
        InChunks.Over(
            _plan.Props.Count,
            _terrain.NewScan,
            (scan, prop) => touches[prop] =
                TouchesDrivableGround(scan, _plan.Props.CentreM[prop], _plan.Props.RadiusM[prop]));

        var into = new StandingGround.Builder();
        for (var prop = 0; prop < _plan.Props.Count; prop++)
        {
            if (!touches[prop]) continue;

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
    /// Whether any of the ground a circle covers is ground a car drives on. <b>The whole of what it
    /// reaches</b>, walked a step at a time, because a prop half a metre off the kerb still has its far
    /// edge in the road.
    /// </summary>
    bool TouchesDrivableGround(GroundShapes.Scan scan, Vector2 centreM, float radiusM)
    {
        var stepM = _config.Terrain.GroundStepM;
        for (var acrossM = -radiusM; acrossM <= radiusM; acrossM += stepM)
        {
            for (var alongM = -radiusM; alongM <= radiusM; alongM += stepM)
            {
                if (_terrain.At(scan, centreM + new Vector2(alongM, acrossM)).Drivable) return true;
            }
        }

        return _terrain.At(scan, centreM + new Vector2(radiusM, radiusM)).Drivable
               || _terrain.At(scan, centreM + new Vector2(-radiusM, radiusM)).Drivable
               || _terrain.At(scan, centreM + new Vector2(radiusM, -radiusM)).Drivable
               || _terrain.At(scan, centreM + new Vector2(-radiusM, -radiusM)).Drivable;
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
        if (!RoadGraph.WithinTheBand(
                arcs, alongM, centreM, _roads.LaneWidthM[lane], BodyFootprint.Round(radiusM), 0f, out var reach))
        {
            return;
        }

        into.Add(lane, alongM + reach.BackM, alongM + reach.AheadM);
    }

    /// <summary>How much road a body doing this speed needs before it can be at rest on the ground it is on.</summary>
    /// <remarks>
    /// The claims' own figure (<see cref="LaneCredit.StoppingM"/>), because what a driver asks for behind
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
    /// written back into the claim (<see cref="CutTheGroundToTheGrant"/>), so a metre granted past a near edge
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
    /// driving</b> (<see cref="WhereTheGroundIsCrossed"/>). A claim is a lane's, and the ground is the
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
        if (Cars.ClaimToM[car] <= Cars.ClaimFromM[car]) return;

        ref readonly var build = ref Cars.BuildOf(car);
        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);
        var grantedToM = float.PositiveInfinity;
        var statedToM = float.PositiveInfinity;
        var cutBy = HeadwayKind.Nothing;

        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);

        // <b>Two windows and one walk.</b> The committed claim is answered by what is inside the road it means
        // to be keeping (<see cref="LookForTheCutToM"/>) and the stated claim by everything out to the end
        // of itself, so a cut found only past the first shortens what the car is saying without touching
        // what it is committed to (TER-5g).
        var lookToM = LookForTheCutToM(car, brakingMps2);

        // How far up the line this car asked for road it means to be committed to. The walk below reaches
        // past it — a braking distance, and the statement beyond that — and a way out there is one the car
        // holds nothing on because it asked for nothing there rather than because anything refused it.
        // <b>The committed ask and never the statement</b> (TER-5g): what a car merely states is not a hold,
        // so a window that took it in would cut every grant back to the nose the moment the line ran past
        // the road the car had committed to.
        var laidToM = Cars.ClaimToM[car];
        var count = WaysAlong(car, Cars.ClaimFromM[car], MathF.Max(lookToM, Cars.StatedToM[car]), ways);
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

            // The terms this car is cut on, which the walk applies and does not decide: the ground it keeps
            // off whatever is going nowhere, and the rung it holds this way's ground at.
            var asker = new LaneCredit(build.BodyMarginM, LaneRoster.Driving, AskingRungOn(car, way.Way));

            // In front of the nose and not of the ground this car holds: every stretch begins a margin
            // behind its owner, so a walk taken from the near edge of this car's own would answer with the
            // body behind it (<see cref="LaneOccupancy.NextHeld"/>).
            // <b>Along the way, and never what anybody merely stated there</b> (TER-5g). A soft claim
            // is laid <em>ahead</em> of its holder, so on the road this car is driving it lies over the
            // traffic in front of that holder rather than behind it — and read as a refusal it stops the car
            // in front of a rescue dead, which leaves the rescue behind a body instead of an empty road. Two
            // cars on one way are held apart by the road each was granted (TER-5c) and a soft claim is
            // for the ways they cross.
            var cutM = _occupancy.GrantedOn(way.Way, noseOnTheWayM, way.ToM, car, asker, out var heldBy);
            if (float.IsFinite(cutM)) Cut(OnTheLineM(way, cutM), KindOf(heldBy));

            // <b>And the answer the claim itself came back with</b> (TER-4c.1, TER-5c.1). A stretch is made
            // nobody else's before it goes in — on its own way and on every way that way is driven over
            // (<see cref="LaneOccupancy.AcrossTheWays"/>) — so what is left of it is the road this car was
            // granted, and the ground where two lines meet is in it without this ever having asked a second
            // question about a way the car will not be on.
            //
            // <b>Asked out to what was laid and never to the end of the window.</b> The walk above reaches a
            // braking distance past the ask so that a cut found there can shorten the statement; a way past
            // everything this car laid is one it holds nothing on because it wanted nothing there, and read
            // as a hold cut short it is a car braking for the end of its own sentence.
            var laidOnTheWayM = OnTheWayM(way, laidToM);
            if (laidOnTheWayM > noseOnTheWayM)
            {
                // <b>What it holds and never what it says</b> (TER-5g). A statement is laid over the same
                // ground a moment later and is in the weakest band there is; counted here, a car whose committed
                // road was taken off it at a crossing would read as still holding that road because it was
                // still saying it meant to use it.
                var heldToM = _occupancy.HeldToM(
                    way.Way, noseOnTheWayM, laidOnTheWayM, car, asked: ClaimsAsked.Held);
                if (heldToM < laidOnTheWayM) TheGroundEndsAt(OnTheLineM(way, heldToM));
            }

            // <b>And how far the nose may go over ground this car keeps</b> (TER-4c.1). Holding a crossing is
            // not being able to drive through what is standing on it: a body past the point it could stop
            // gives nothing back (TER-5e) and still has to brake for whatever is in the box, so the grant is
            // cut at what <see cref="LaneOccupancy.Binds"/> says binds — which is the question the ownership
            // above does not ask and must not be made to answer.
            //
            // A crossing point is a place and has no margin of its own, so the asker's is taken off it here
            // — the one cut in the town that is not made at somebody else's stretch, on the same figure.
            var crossedAtM = WhereTheGroundIsCrossed(car, way, noseOnTheWayM, asker.Asking, out var by);
            if (float.IsFinite(crossedAtM)) Cut(OnTheLineM(way, crossedAtM) + asker.AtAPlaceM, KindOf(by));
        }

        void Cut(float atM, HeadwayKind by)
        {
            if (atM < statedToM) statedToM = atM;
            if (atM > lookToM || atM >= grantedToM) return;

            grantedToM = atM;
            cutBy = by;
        }

        // <b>The one bound that is not a look-ahead</b> (TER-4c.1). What the walks above find is ground in
        // front that the car has still to be answered about, and a find past the window it is committed to
        // shortens only what it is saying; this is the answer itself — metres the town has already given
        // somebody else — so the road stops there whether the window reached it or not. Left inside the
        // window, a car held a grant running through ground its own claim had given up.
        void TheGroundEndsAt(float atM)
        {
            if (atM < statedToM) statedToM = atM;
            if (atM >= grantedToM) return;

            grantedToM = atM;
            cutBy = HeadwayKind.Claimed;
        }

        if (float.IsFinite(statedToM)) Cars.StatedGrantM[car] = statedToM - noseM;
        if (float.IsPositiveInfinity(grantedToM)) return;

        Cars.AuthorityM[car] = grantedToM - noseM;
        Cars.GrantCutBy[car] = cutBy;
    }

    /// <summary>
    /// <b>The road the car asked for, brought back to the road it got</b> (TER-4c.1) — so that what the claim
    /// holds for the rest of the tick is the answer and not the question.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two bodies cannot be granted one metre, and until this pass they were.</b> The ask is bounded by
    /// the rules that stop the car and by nothing in front of it (<see cref="AskForTheGround"/>); the answer
    /// is where it is cut at the traffic (<see cref="GrantTheGround"/>), and it was written to
    /// <c>CarFleet.AuthorityM</c> and nowhere else. Every other reader of the claims — the junction gate, the
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
    /// <para>
    /// <b>Every stretch the ask laid and not the one with the body in it</b>
    /// (<see cref="LaneOccupancy.CutTheAskTo"/>). A hold is one run of ways, and on the ways ahead of the nose
    /// it carries no body at all — so an answer scoped to the body's own stretch cut the lane a car was
    /// standing in and left the join past it holding the far end of the ask, with the metres the answer took
    /// belonging to nobody in between.
    /// </para>
    /// <para>
    /// <b>And what it took is stated</b> (<see cref="StateWhatTheAnswerTook"/>), because a hold may not have a
    /// hole in it (TER-5c.2) and the honest name for road a car asked for and did not get is road it means to
    /// use and has not reached.
    /// </para>
    /// </remarks>
    void CutTheGroundToTheGrant(int car, Span<LineWay> ways)
    {
        if (Cars.ClaimToM[car] <= Cars.ClaimFromM[car]) return;

        // <b>The stated claim with it, and answered on its own reach</b> (TER-5g): a car refused the road
        // has stopped saying it is coming, on the tick it was refused. It is the same pass because it is the
        // same fact — what a claim holds is the answer and never the question — and it is a second walk
        // because the two asks are two intervals of the line.
        if (Cars.StatedToM[car] > Cars.ClaimToM[car] && float.IsFinite(Cars.StatedGrantM[car]))
        {
            var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
            var statedToM = MathF.Max(
                noseM, MathF.Min(Cars.StatedToM[car], noseM + Cars.StatedGrantM[car]));

            // <b>Never back past ground this car already holds</b> (TER-5c.2). The answer is about road the
            // car is asking for; the metres between it and a box the gate has already given it are the span
            // that carries one to the other (<see cref="StateTheRoadToTheMovement"/>), and cut at the first
            // body on the paint in front the car held the far side of a junction and not the way in to it.
            var holdsToM = TheMovementHoldEndsAtM(car);
            if (float.IsFinite(holdsToM))
            {
                statedToM = MathF.Max(statedToM, MathF.Min(holdsToM, Cars.StatedToM[car]));
            }

            var stated = WaysAlong(car, Cars.ClaimToM[car], Cars.StatedToM[car], ways);
            for (var index = 0; index < stated; index++)
            {
                ref readonly var way = ref ways[index];
                _occupancy.CutTo(way.Way, car, OnTheWayM(way, statedToM), ClaimsAsked.Stated);
            }
        }

        CutTheRoadToTheGrant(car, ways);
        StateWhatTheAnswerTook(car, ways);
    }

    /// <summary>
    /// The committed claim itself, brought in to the answer on every way it was laid over — and the ground
    /// of the movement beyond it handed the metres that came off.
    /// </summary>
    void CutTheRoadToTheGrant(int car, Span<LineWay> ways)
    {
        if (float.IsPositiveInfinity(Cars.AuthorityM[car])) return;

        var movementWay = Cars.MovementWay[car];
        var grantedToM = GroundEndsAtM(car);

        // Where the cut landed on the way this car is crossing on, kept as the walk makes it rather than
        // worked out again from the line: the ground beyond it is handed to the claim carrying the rest of
        // the same stretch, and two routes to one metre put a hair of road between them.
        var cutOnTheMovementM = float.NaN;

        var count = WaysAlong(car, Cars.ClaimFromM[car], Cars.ClaimToM[car], ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            var cutToM = OnTheWayM(way, grantedToM);
            _occupancy.CutTheAskTo(way.Way, car, way.FromM, way.ToM, cutToM);
            if (way.Way == movementWay) cutOnTheMovementM = cutToM;
        }

        // A road that never reached the movement at all leaves nothing to hand over: what carries the span
        // over those metres is the statement (<see cref="StateTheRoadItMeansToUse"/>), which was answered on
        // its own reach above.
        if (movementWay != CarFleet.NoWay && !float.IsNaN(cutOnTheMovementM))
        {
            ClaimWhatTheAnswerTook(car, movementWay, cutOnTheMovementM);
        }
    }

    /// <summary>
    /// <b>The metres the answer took off the ask, stated</b> (TER-5g, TER-5c.2) — the road this car asked for,
    /// did not get, and is still going down, which is the whole of what the honest name for them is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Without it the answer leaves a hole where it cut.</b> Everything a car holds beyond its own road is
    /// anchored at the far edge of the ask — the statement of where it is going
    /// (<see cref="StateTheRoadItMeansToUse"/>), the span to a box it holds
    /// (<see cref="StateTheRoadToTheMovement"/>), the ground of the movement itself
    /// (<see cref="LayTheMovement"/>) — because at the moment those are laid there is no answer yet
    /// (<see cref="RebuildLaneOccupancy"/>). So the answer moves one edge and leaves every other where the
    /// question put it, and the metres between are a body nothing can be cut at through the middle of.
    /// </para>
    /// <para>
    /// <b>Stated, because that is what they are.</b> The car has been refused them and is not committed to
    /// them, so holding them at the rank its road carries would be the answer handed back as the question. It
    /// is also what makes them affordable: everything stronger takes a soft claim, so the traffic this car was
    /// cut at is refused nothing by the metres it was cut over.
    /// </para>
    /// <para>
    /// <b>One interval of every way, so the statement already there reaches back over them</b>
    /// (<see cref="LaneOccupancy.ReachBackTo"/>) rather than being met by a second stretch at the seam; a way
    /// with no statement on it — the ways the ask ran over before the one it ended on — gets one.
    /// </para>
    /// <para>
    /// <b>Asked of the whole of the ask and not of the metres the grant took</b>, because the answer is not
    /// the only thing that shortens a road: an ask is cut back where it is laid as well
    /// (<see cref="LaneOccupancy.MakeRoomFor"/>), and where it ran onto ground somebody else already held it
    /// went in shorter than the figure the car carries. So the near edges here are <em>found</em>, each
    /// stretch stopping at whatever is behind it, and nothing is worked out from a figure a second time.
    /// </para>
    /// <para>
    /// <b>It is not the pull-away horizon TER-5g withholds from a body at rest.</b> These metres are inside
    /// the road the car asked for, which is bounded by every rule that stops it — a red, a bar, a crossing, a
    /// box it has not been given — so a car waiting at a give-way line states no metre of the box it is
    /// waiting for.
    /// </para>
    /// </remarks>
    void StateWhatTheAnswerTook(int car, Span<LineWay> ways)
    {
        var count = WaysAlong(car, Cars.ClaimFromM[car], Cars.ClaimToM[car], ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            if (_occupancy.ReachBackTo(way.Way, car, way.FromM, ClaimsAsked.Stated)) continue;

            _occupancy.ClaimAhead(
                way.Way, way.FromM, way.ToM, Cars.AlongMps[car], car, SoftOf(car, way.Way),
                takingUpAgain: true);
        }
    }

    /// <summary>
    /// <b>Where the road this car holds ends on its own line</b> — the ask (<see cref="AskForTheGround"/>)
    /// brought in to the answer (<see cref="GrantTheGround"/>), which is what the claim holds once
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
        return MathF.Max(noseM, MathF.Min(Cars.ClaimToM[car], noseM + Cars.AuthorityM[car]));
    }

    /// <summary>
    /// <b>The first metre of one of this car's own ways that somebody else's ground is driven over</b>, in
    /// that way's own metres, or infinity where none of it is — <b>how far the nose may go</b> and never who
    /// holds the metres it stops at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the grant and not the ownership</b> (TER-4c.1, TER-5c.1), and the pair is the same pair a
    /// single way already carries: what a stretch may <em>hold</em> across a crossing is settled when it is
    /// laid (<see cref="LaneOccupancy.AcrossTheWays"/>), and what a nose may <em>reach</em> is this. They part
    /// company at exactly one body, and it is the one that matters: a car past the point it could stop gives
    /// its ground back to nobody (TER-5e) and still has to brake for whatever is standing in the box. Read off
    /// the ownership alone, such a car was cut by nothing at all and drove into it at speed.
    /// </para>
    /// <para>
    /// <b>In front means in front of the nose</b>, exactly as it does for a stretch
    /// (<see cref="LaneOccupancy.NextHeld"/>, which is asked from the same metre): a crossing point the
    /// body is standing over is one it has arrived at, and a grant cut at it would be a car braking for the
    /// corner it came in by. <b>What a grant says is how far a nose may go</b>, so a cut behind that nose is
    /// not a shorter grant — it is a grant that has stopped being a distance, and it read as a car eighteen
    /// metres inside somebody else's road while it was doing nothing but sitting on a junction it had crossed.
    /// </para>
    /// <para>
    /// <b>The cut is at the near edge of the section and carries no credit past it.</b> There is nothing on a
    /// section to come to rest — it is a place, and what is standing on it is standing on another way's
    /// metres, at a pose and a heading this car has no reading of. <b>Nor a margin</b>: the margin a body
    /// keeps off a place is in the section itself (<see cref="LineOverlap.Measure"/>), taken once where the
    /// section was measured rather than once per reader.
    /// </para>
    /// <para>
    /// <b>And ground a stronger movement has taken off somebody is not a cut</b> (TER-5e). A movement
    /// this one is driven over reads its own claim on that ground as given up the moment the stronger
    /// movement asks for it, so the pair of them are cut one way round rather than both — which is the
    /// whole of the difference between a right of way and a deadlock.
    /// </para>
    /// </remarks>
    float WhereTheGroundIsCrossed(
        int car, in LineWay way, float noseOnTheWayM, ClaimPriority mine, out LaneClaim held)
    {
        held = LaneClaim.Nothing;
        var leastM = float.PositiveInfinity;
        foreach (ref readonly var section in _crossings.Of(way.Way))
        {
            if (section.MineFromM < noseOnTheWayM || section.MineFromM > way.ToM) continue;
            if (section.MineFromM >= leastM) continue;

            if (!float.IsFinite(
                    FirstHeldOn(car, section.OnWay, section.FromM, section.ToM, mine, out var by)))
            {
                continue;
            }

            leastM = section.MineFromM;
            held = by;
        }

        return leastM;
    }

    /// <summary>
    /// <b>Where a named piece of one way is first ground an asker with this right of way is refused by</b>,
    /// in that way's own metres and never behind the piece asked about — everything lying over it that
    /// <see cref="LaneOccupancy.Binds"/> says the asker must give way to, and infinity where none of it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every stretch over the piece and not the first one</b>
    /// (<see cref="LaneOccupancy.NextHeldOver"/>): the first may be a claim this asker takes, and a walk
    /// that stopped there would grant the ground through whatever was holding the same metres behind it.
    /// <b>The first that binds is the least</b>, since a way's claims are held in the order their
    /// near edges fall on it, so the walk stops at it rather than carrying a minimum through the rest.
    /// </para>
    /// <para>
    /// <b>One walk, asked as a distance by the grant and as a verdict by the gate</b>
    /// (<see cref="WhereTheGroundIsCrossed"/>, <see cref="FirstHeldOnTheMovementM"/>). Written twice, they
    /// were one loop in two files, free to come to two answers about one piece of ground.
    /// </para>
    /// </remarks>
    /// <param name="crossingOn">
    /// A movement whose own traffic is no answer, or <see cref="CarFleet.NoWay"/> where everything on the
    /// ground is one. A car making the same movement has taken the same sections off the same lines, so read
    /// literally every queue at a junction refuses its own second car — and what holds one of them off the
    /// next is the road each was granted (S-2a), which is a headway and not a crossing.
    /// <para>
    /// <b>What is skipped is a car and never whoever else holds the same number.</b> A piece of ground is a
    /// stretch of any way and not only of a join — the way into a bay sweeps the lane running back the other
    /// way (<see cref="WayCrossings"/>) — and a lane carries the walking roster's bodies and the town's own
    /// furniture beside the traffic. Which roster an occupant is named in is carried by the stretch
    /// (<see cref="LaneClaim.Of"/>), so a walker in the road and a bollard standing in it refuse a movement
    /// like anything else on the ground it crosses.
    /// </para>
    /// <para>
    /// <b>A grant asks with none of it</b>: a car crossing the same box is exactly what a grant on the metres
    /// the two of them share has to be cut at.
    /// </para>
    /// </param>
    float FirstHeldOn(
        int car, int way, float fromM, float toM, ClaimPriority mine, out LaneClaim held,
        int crossingOn = CarFleet.NoWay)
    {
        var at = LaneOccupancy.FromTheStart;
        while (_occupancy.NextHeldOver(
                   way, fromM, toM, car, ref at, out var taken, asked: ClaimsAsked.HeldOrStated))
        {
            var crossingWithUs = crossingOn != CarFleet.NoWay
                                 && taken is { Of: LaneRoster.Driving, Occupant: >= 0 }
                                 && Cars.MovementWay[taken.Occupant] == crossingOn;
            if (crossingWithUs || !LaneOccupancy.Binds(taken, mine)) continue;

            held = taken;
            return MathF.Max(taken.FromM, fromM);
        }

        held = LaneClaim.Nothing;
        return float.PositiveInfinity;
    }

    /// <summary>
    /// <b>How far up the line the cut is looked for, which is not how far the ask reached.</b> What a car
    /// claims is the road it is committed to; what it has to be told about is the road it means
    /// to be keeping — the stop, the gap of a following time in front of that, and a body's length of margin
    /// past it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Looking further can only make a grant smaller</b>, because the grant is the least of everything
    /// found — so nothing about the safety of it turns on this figure and only the fluency does. Looked for
    /// no further than the ask, the grant simply went uncut whenever the car was more than a stopping
    /// distance behind: the profile then held the car off on the headway reading, at the reaction lead,
    /// until it had closed to inside its own claim — and the pair of them settled into closing up and
    /// falling back rather than into a gap.
    /// </para>
    /// <para>
    /// <b>And it is the committed claim's window and not the stated one's</b> (TER-5g). The stated claim
    /// reaches further and is answered over the whole of its own reach
    /// (<see cref="CarFleet.StatedGrantM"/>), but a cut found only out there is a cut on ground this car is
    /// not committed to: folded into the grant, a car would brake for a junction two streets away that the
    /// gate had not yet let it near.
    /// </para>
    /// </remarks>
    float LookForTheCutToM(int car, float brakingMps2)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var alongMps = MathF.Max(0f, Cars.AlongMps[car]);
        var noseM = Cars.ProgressM[car] + LeadingEdgeAheadOfTheAxleM(car);
        return MathF.Max(
            Cars.ClaimToM[car],
            noseM + StoppingM(alongMps, brakingMps2) + (alongMps * _config.Driving.FollowingHeadwayS)
            + build.BodyMarginM + build.LengthM);
    }
}
