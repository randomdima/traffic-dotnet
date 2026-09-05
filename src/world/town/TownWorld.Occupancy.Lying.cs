using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The ground every body stands on, claimed whatever that body is doing</b>: the lane it is
/// nearest, the lane running back the other way where it reaches into it, every join of a junction it is
/// lying in, and — for a car driving a template — the sweep it is committed to making.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>The space this car occupies, taken from its pose and claimed</b> — for every car, on
    /// every way that space touches and nothing else already answers for (TER-4c.2, <see cref="LieUnder"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here asks whether the body is an obstruction</b>, because that is not a fact about the
    /// ground it is standing on: a car halfway across the oncoming lane is on that lane while it is driving
    /// perfectly well, and what the traffic there does about it is that traffic's own business
    /// (<see cref="KindOf"/>). Gated on the driving instead, a body under a hand wrote nothing onto the join
    /// it was sitting in and every car crossing the box read it as empty.
    /// </para>
    /// <para>
    /// <b>It is the ground and never the road ahead.</b> What a body has taken in front of itself is its
    /// committed claim (<see cref="AskForTheGround"/>), which is laid first and covers the ways of that car's
    /// own line; this pass is what covers the rest, and the dedupe is what keeps the two to one stretch apiece
    /// (<see cref="LaneOccupancy.AlreadyHolds"/>).
    /// </para>
    /// <para>
    /// <b>And a body that has not moved lies where it lay</b> (<see cref="LyingClaims"/>). The geometry below
    /// is the same arithmetic over the same numbers for as long as the pose stands still, which for most of
    /// a town's fleet is the whole run.
    /// </para>
    /// </remarks>
    void PlaceTheBody(int car)
    {
        var standingM = Cars.PositionM[car];
        var sweeping = WhereTheTemplateSweepEndsM(car, standingM, out var committedToM, out var committedRad);

        // <b>A car on a bar is laid under the vehicle pulling it</b> (EVA-5): the pair is one movement, so
        // it is one occupant (TER-5c.2) and the ground both of them stand on is held under the
        // one number. Laid under its own instead, the trailer's row cuts its hauler's grant and the tow
        // stops dead at the first metre of road it is standing on; laid not at all, the lane the trailer is
        // swung into as the pair turns is a lane the town cannot see anything in.
        var towedBy = _recovery.OnTheHookOf[car];
        var state = new LyingState(
            standingM, committedToM, Cars.VelocityMps[car], Cars.HeadingRad[car],
            Cars.GroundCoefficient[car], Cars.Variant[car], sweeping, IsUnderWay(car), towedBy);
        if (_lying.Holds(car, state))
        {
            LieWhereItLay(car);
            return;
        }

        _lying.Begin(car, state);
        var standingRad = Cars.HeadingRad[car];
        var underWay = state.UnderWay;
        if (towedBy >= 0)
        {
            // On the terms of the vehicle pulling, because those are the readings the pair already has: its
            // claim carries the ways of the line the two of them are going down, and its crossing is
            // what answers for the box they are being dragged through (<see cref="LieUnder"/>).
            committedToM = standingM;
            committedRad = standingRad;
            underWay = IsUnderWay(towedBy);
        }

        // <b>Every network the town has, one walk apiece</b> (TER-4c.2, <see cref="IWayNetwork"/>). What ground a
        // body is on is a question for the ground and never for what the body was doing: a wreck, a car
        // nobody is in, a body shoved off its own route and the swerve halfway across the oncoming lane are
        // all the same fact to whoever is coming up behind — and so is a car standing in a bay, a car across
        // a bay's mouth and a car that has mounted a kerb.
        LieOnTheRoad(car, standingM, standingRad, committedToM, committedRad, underWay);
        LieOnThePavement(car, standingM, standingRad, committedToM, committedRad);

        _lying.End();
    }

    /// <summary>
    /// Which of the town's cars this body's ground is claimed under — <b>itself, or the vehicle pulling
    /// it</b> (EVA-5). A coupled pair is one movement and so one occupant (TER-5c.2): the truck's own grant
    /// is not cut at the trailer it is dragging, and the traffic behind is held off the pair as one thing.
    /// </summary>
    int LaidAs(int car) => _recovery.OnTheHookOf[car] is var hauler and >= 0 ? hauler : car;

    /// <summary>
    /// The stretches this body laid the last time its pose changed, laid again — the same rows the geometry
    /// would have arrived at, on the same terms.
    /// </summary>
    void LieWhereItLay(int car)
    {
        var rows = _lying.Of(car);
        for (var at = 0; at < rows.Length; at++)
        {
            ref readonly var row = ref rows[at];
            Lay(car, row);
        }
    }

    /// <summary>
    /// One stretch of a body, laid as a claim and into the record of what this body lays — the one place the
    /// two are kept together, so a row that is claimed and not recorded is not a thing that can be
    /// written.
    /// </summary>
    /// <remarks>
    /// <b>Laid on the terms every other body is laid on</b> (<see cref="AskForTheGround"/>): three edges, the
    /// middle one the body itself. What tells a body standing still from one under way here is only how much
    /// of the third edge there is, and for something at rest there is none — which is the whole of why this
    /// is not a mechanism of its own.
    /// </remarks>
    void LieAt(
        int car, int way, float fromM, float standsToM, float toM, float alongMps, float acrossFromM,
        float acrossToM)
    {
        var row = new LyingRow(way, fromM, standsToM, toM, alongMps, acrossFromM, acrossToM);
        _lying.Record(car, row);
        Lay(car, row);
    }

    /// <summary>
    /// One recorded row claimed, unless this body is already holding ground these metres run over
    /// (TER-5c.2) — <b>the one place the dedupe is made</b>, so a row laid from the geometry and the same row
    /// laid again from the record cannot be answered two ways.
    /// </summary>
    /// <remarks>
    /// <b>What it is normally answered by is the car's own committed claim</b>, which was laid first over the
    /// ways of this body's line. So a driver under way keeps the stretch that carries its road, and what this
    /// pass adds is every way that line does not name: the lane it is reaching into, the join it is lying
    /// across on its way somewhere else. Asked and answered under <see cref="LaidAs"/> rather than under the
    /// car, so that a trailer meets its hauler's claim as its own and is deduped against it.
    /// </remarks>
    void Lay(int car, in LyingRow row)
    {
        var occupant = LaidAs(car);
        if (_occupancy.AlreadyHolds(row.Way, row.FromM, row.ToM, occupant)) return;

        _occupancy.ClaimWhereItStands(
            row.Way, row.FromM, row.StandsToM, row.ToM, row.AlongMps, occupant, acrossFromM: row.AcrossFromM,
            acrossToM: row.AcrossToM);
    }

    /// <summary>
    /// <b>Where a car driving a template of its own is committed to being, and how it will be standing
    /// there</b>: the far end of that line, read for the middle of the body rather than for the axle the line
    /// is drawn for, and in whichever gear the line is driven. <b>False where there is no sweep left to
    /// make</b>, and the body's ground is the body where it is now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A template is walked before it is laid and held for as long as it is driven</b> (TER-4c.1). Held
    /// only where the body had got to, the ground a recovery straight was drawn through was ground the
    /// traffic was free to come to rest on — checked once at the moment of laying, and then reversed into.
    /// </para>
    /// <para>
    /// <b>A line already driven out is not a sweep</b>, which is what tells a car working through a template
    /// from one standing at the end of the one it has finished: a parked car keeps the line that put it in
    /// the bay, and a town's parked cars holding a body of road apiece is the whole fleet holding road.
    /// </para>
    /// </remarks>
    bool WhereTheTemplateSweepEndsM(int car, Vector2 standingM, out Vector2 endsM, out float endsRad)
    {
        endsM = standingM;
        endsRad = Cars.HeadingRad[car];

        var line = Cars.Line[car];
        if (line.ArcCount == 0 || line.LaneCount > 0 || !Cars.Driven[car] || Cars.Broken[car]) return false;
        if (line.LengthM <= Cars.ProgressM[car]) return false;

        var at = Spline.SampleAt(Cars.LineArcsOf(car)[..line.ArcCount], line.LengthM);
        endsRad = at.HeadingRad;
        var forward = Heading.Unit(at.HeadingRad);

        endsM = at.PositionM + ((Cars.LineIsReverse[car] ? -forward : forward) * _config.CarCentreAheadOfAxleM);
        return true;
    }

    /// <summary>
    /// A body's ground, laid onto the lane it is nearest and onto every join of a junction it is lying in —
    /// <b>and where it is sweeping a template, the same again for the pose it is committed to, the two
    /// joined on each way they share</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both poses are read and a way is laid from the ends that are on it</b> (TER-5c.2). Which ways a
    /// pose is over is a question about that pose, and the two ends of a sweep are regularly over different
    /// ones: a car swinging into a bay starts on the lane and finishes off it. Laid instead as the corridor
    /// between the two ends wherever either was on a way, that car claimed every metre of lane between where
    /// it stood and where it was going — seven metres of road for a body under four, on a lane it was in the
    /// act of leaving.
    /// </para>
    /// <para>
    /// <b>A body driving a line of the town's own writes only where nothing else already answers for the
    /// ground</b> (SIM-7). Three things do, and each is a way this pass leaves alone:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <b>The ways of its own line</b> carry its committed claim, which is the same body measured from the line
    /// instead of the pose (<see cref="AskForTheGround"/>, <see cref="LaneOccupancy.AlreadyHolds"/>).
    /// </item>
    /// <item>
    /// <b>The joins of a junction</b> are the crossing table's (TER-5c.1): a driver crossing a box is read on
    /// every join its own way is driven over by looking that way up (<see cref="WhereTheGroundIsCrossed"/>),
    /// which is where the right of way is applied. Written onto those joins as a body as well it is the same
    /// refusal made twice — and the second one nobody can give up, since a rank takes a claim and never a
    /// body (TER-5e), so four cars meeting at a crossroads each hold the ground the other three are waiting
    /// for and the box never clears. The arms' own lanes go with them, because a body answering on a lane
    /// past that lane's own end is standing on the box and not on the lane (TER-5d,
    /// <see cref="WayUnder.PastTheEndM"/>).
    /// </item>
    /// <item>
    /// <b>The lane running back against it</b> is the one piece of ground the town has no rule for. Nothing
    /// is ever driven between a carriageway's two lanes (TER-5f), so two bodies meeting there are two bodies
    /// neither of which can be made to give way — and a car that stops while still angled across the line
    /// holds the oncoming lane for the rest of the run. <b>This is a gap and not a mechanism</b>: what should
    /// close it is the bar that decides a car is still on its line at all
    /// (<see cref="OffTheLineAllowanceM"/>), which lets a car sit a full lane's width off it and still count
    /// as driving. A body far enough out to be in the other lane ought to be a body that is no longer under
    /// way — and then it is written here like anything else.
    /// </item>
    /// </list>
    /// <para>
    /// <b>A body that is not driving a line of the town's own has none of those readings</b> — its line says
    /// one thing and its pose another, or it has no line at all — so every way under it is exactly what it
    /// must be written onto.
    /// </para>
    /// </remarks>
    void LieUnder(
        bool onThePavement, int car, Vector2 standingM, float standingRad, Vector2 committedToM,
        float committedRad, bool underWay)
    {
        // <b>The same walk a driver on a template asks with</b> (<see cref="GroundUnder"/>): what a
        // body is written onto and what a manoeuvre reads are one set of ways, or a car could stand
        // somewhere the next car through cannot see it.
        // <b>And every way the box touches</b> (<see cref="RoadGraph.WithinTheBand"/>), because a body that
        // touches a lane is in that lane whoever is coming down it. How much of a nuisance it is there rides
        // on the stretch (<see cref="LaneClaim.AsideM"/>) for the traffic to make of what it will.
        var room = onThePavement ? _pavement.MostWaysUnderAPlace : RoomForTheRoadsGround;
        Span<WayUnder> standing = stackalloc WayUnder[room];
        Span<WayUnder> committed = stackalloc WayUnder[room];
        var sweeping = committedToM != standingM;
        var standingCount = ReadTheGroundUnder(onThePavement, car, standingM, standingRad, underWay, standing);
        var committedCount = sweeping
            ? ReadTheGroundUnder(onThePavement, car, committedToM, committedRad, underWay, committed)
            : 0;

        for (var index = 0; index < standingCount; index++)
        {
            ref readonly var way = ref standing[index];
            var fromM = way.AlongM + way.BackM;
            var toM = way.AlongM + way.AheadM;

            // The same way read at the far end of the sweep, where there is one and where it reaches this
            // way at all: one movement is one stretch (TER-5c.2), and it runs from the earlier of the two
            // poses' own ground to the later.
            var alsoAt = IndexOfTheWay(committed[..committedCount], way.Way);
            if (alsoAt >= 0)
            {
                fromM = MathF.Min(fromM, committed[alsoAt].AlongM + committed[alsoAt].BackM);
                toM = MathF.Max(toM, committed[alsoAt].AlongM + committed[alsoAt].AheadM);
            }

            LieOnTheWay(car, way, fromM, toM, underWay, sweeping);
        }

        // And the ways only the far pose is on, which the standing is no answer for at all: a car halfway
        // out of a bay is committed to ground on the lane its nose has not reached.
        for (var index = 0; index < committedCount; index++)
        {
            ref readonly var way = ref committed[index];
            if (IndexOfTheWay(standing[..standingCount], way.Way) >= 0) continue;

            LieOnTheWay(
                car, way, way.AlongM + way.BackM, way.AlongM + way.AheadM, underWay, sweeping);
        }
    }

    /// <summary>
    /// The ways one pose of this body stands on, less the ones a driver under way already answers for — the
    /// walk and the three exclusions of <see cref="LieUnder"/>'s own remark, made once for each pose.
    /// </summary>
    int ReadTheGroundUnder(
        bool onThePavement, int car, Vector2 atM, float headingRad, bool underWay, Span<WayUnder> into)
    {
        // <b>The box this body stands in, at the heading it is standing at</b>
        // (<see cref="BodyFootprint"/>). Read at one radius instead, the same figure is wrong on both axes at
        // once: a car lying broadside reaches its own length across the lane beside it and its own width
        // along the lane under it, and neither of those is half of anything.
        ref readonly var build = ref Cars.BuildOf(car);
        var forward = Heading.Unit(headingRad);
        var box = new BodyFootprint(build.HalfLengthM, build.FlankM, forward);

        // <b>The paint is the road's and never the walk's</b> (TER-5c.1): a crossing is carriageway a walk
        // runs over, so a car on it is a stretch of the lane and what holds a walker off it is that stretch,
        // looked up where the crossing crosses (<see cref="WhereTheWalkRunsOut"/>). Written here as well, one
        // car holds one piece of ground twice, under two claims free to disagree.
        if (onThePavement)
        {
            return WalkedAlone(
                GroundUnder.At(_pavement, atM, box, _config.CrossesOntoAWayM, into), into);
        }

        var found = TheRoadsGroundUnder(atM, box, into);
        if (!underWay) return found;

        var kept = 0;
        for (var index = 0; index < found; index++)
        {
            ref readonly var way = ref into[index];

            // The three readings a driver under way already has, in the order the remark gives them: the box
            // is the table's, the ground past a lane's own end is the box, and the lane running back against
            // it is the gap nothing resolves. <b>A bay's way goes with the box</b>: what a driver is driven
            // over there is the same table's (<see cref="BayCrossings"/>), so a car working into a bay holds
            // it by the claim on the way it is driving and not twice — which is what the first of the
            // three says of it, since a bay's way is no lane of the road's.
            if (_ways.KindOf(way.Way) != WayKind.Lane || way.PastTheEndM > 0f
                || Vector2.Dot(way.AlongUnit, forward) < 0f)
            {
                continue;
            }

            into[kept++] = way;
        }

        return kept;
    }

    /// <summary>
    /// The ways of that reading a <em>car</em> answers for on the walk, which is every one of them but a
    /// crossing's (TER-5c.1). A body on foot writes the paint like any other way it stands on
    /// (<see cref="StandInTheWay"/>).
    /// </summary>
    int WalkedAlone(int found, Span<WayUnder> into)
    {
        var kept = 0;
        for (var index = 0; index < found; index++)
        {
            var way = into[index].Way;
            var edge = _ways.KindOf(way) == WayKind.Footway
                ? _ways.FootwayOf(way)
                : _pavement.TurnToLane(_ways.MitreOf(way));
            if (_pavement.IsACrossing(edge)) continue;

            into[kept++] = into[index];
        }

        return kept;
    }

    /// <summary>Where one way stands in a reading of the ground, or <c>-1</c> — a walk, since a body is on a handful.</summary>
    static int IndexOfTheWay(ReadOnlySpan<WayUnder> ways, int way)
    {
        for (var index = 0; index < ways.Length; index++)
        {
            if (ways[index].Way == way) return index;
        }

        return -1;
    }

    /// <summary>
    /// A body laid onto one of the town's ways where it stands inside that way's own band, and left off
    /// where it does not.
    /// </summary>
    /// <remarks>
    /// <b>The band and the body's own box</b>, and not how far the body is off the line. A wreck shoved
    /// sideways is still standing in what it was shoved into, and since the claims are the whole of what a
    /// driver looks at (TER-4c), one left out of it here is one nothing can see: the reach a line's own
    /// tolerance allows is a bar on whether a car is still *driving* that line, which is a different
    /// question and a tighter one.
    /// <para>
    /// <b>The stretch is what the body covers of this way and no more</b>
    /// (<see cref="BodyFootprint.CoversOn"/>): the part of its box that is inside this way's band, projected
    /// onto the line — its length where it lies along the way, its width where it lies across one, and a
    /// corner where it clips one at an angle. Laid at a half-length regardless, a car crossing a lane
    /// squarely shut two car lengths of it.
    /// </para>
    /// <para>
    /// <b>A body on a template of its own reaches the pose it is committed to</b>
    /// (<see cref="WhereTheTemplateSweepEndsM"/>) and not only the one it is passing through: the ground a
    /// manoeuvre is about to be on is ground it is holding. <b>On the ways that pose is actually on</b>,
    /// which <see cref="LieUnder"/> is what settles.
    /// </para>
    /// </remarks>
    void LieOnTheWay(
        int car, in WayUnder way, float fromM, float standsToM, bool underWay,
        bool sweeping)
    {
        var alongMps = Vector2.Dot(Cars.VelocityMps[car], way.AlongUnit);

        // <b>And the ground it cannot stop short of, which is the third edge every other body carries</b>
        // (<see cref="AskForTheGround"/>). A body that is not driving a route is not thereby a body that is
        // not going anywhere: one shoved down a lane by a collision, one sliding on a wet corner, one under a
        // hand is on its way somewhere at whatever speed it has, and holding only the metres under it hands
        // the traffic behind the ground it is about to be on.
        //
        // <b>Where it is sweeping a template, that ground is the sweep and is already laid</b>
        // (<see cref="WhereTheTemplateSweepEndsM"/>): the two are one answer to one question — what this body
        // is committed to — read once off the line it is driving and once off the speed it is doing, and
        // taking both is a car holding a swerve's worth of lane twice over.
        var toM = sweeping || underWay
            ? standsToM
            : standsToM + StoppingM(
                alongMps, CarFollower.BrakingMps2(_config, Cars.BuildOf(car), Cars.GroundCoefficient[car]));

        LieAt(car, way.Way, fromM, standsToM, toM, alongMps, way.AcrossFromM, way.AcrossToM);
    }

    /// <summary>
    /// <b>The ways the road numbers that one place stands on</b>: the carriageway's and the bays', which are
    /// two networks (<see cref="IWayNetwork"/>) numbered together — one walk apiece, and <b>the one site
    /// either of them is named</b>. Whatever claims for a body there and whatever reads it therefore ask
    /// the same question, and a network a caller forgot is not a thing that can happen (SIM-7).
    /// </summary>
    /// <remarks>
    /// <b>It is the ground and never the roster.</b> A bay is a lane like any other (GEN-4f), so a person
    /// standing in one is a stretch of that bay's ways exactly as a car standing there is
    /// (<see cref="StandInTheRoad"/>) — asked of the road walk alone, a body in a space was a body the driver
    /// aiming at it could not see, and only cars were ever claimed on the bays' half of the numbering.
    /// </remarks>
    int TheRoadsGroundUnder(Vector2 atM, in BodyFootprint box, Span<WayUnder> into)
    {
        var found = GroundUnder.At(_roads.Ways, atM, box, _config.CrossesOntoAWayM, into);

        return found
               + GroundUnder.At(_bayWays.Ways, atM, box, _config.CrossesOntoAWayM, into[found..]);
    }

    /// <summary>How much room that walk needs, which is both of the road's networks at once.</summary>
    int RoomForTheRoadsGround => MostWaysUnderAPlaceOnTheRoad(_roads.Ways, _bayWays.Ways);

    /// <summary>
    /// <b>The road's ways, wherever this body is standing on them</b> (TER-4c.2): the carriageway under it, and
    /// every way of the bay it is in or across the mouth of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bays are what makes an occupied space a fact the town can read</b> rather than a flag somebody
    /// has to remember to set: a car aiming at a bay drives the way in, and what stops it is the body standing
    /// at the end of it — the same headway that stops it behind anything else. Nothing is released, because
    /// this is laid from the pose every tick like every other claim.
    /// </para>
    /// <para>
    /// <b>And it is a reading of the body and never of the register</b> (`P-2`). A standing is written by the
    /// manoeuvre that made it and given up by the manoeuvre that drives away, so a car taken out of a bay by
    /// anything else — a hand at the wheel, a shunt, an arm — keeps the register's word for where it was; laid
    /// from that it held a bay it might be streets from, and stood on the road holding none of it. The bay a
    /// body is in is the bay it is standing in, which is the same question this walk asks of a lane.
    /// </para>
    /// <para>
    /// <b>It reaches the street exactly as far as the body does.</b> A bay's mouth stands off the
    /// carriageway's own edge, so what is left of a bay's way once the traffic's ground is taken off it
    /// (<see cref="BayCrossings"/>) is more than a parked car covers — and a car parked square in its space
    /// therefore cuts nobody's grant on the road, not because the extent was clamped to say so but because
    /// that is where the car is.
    /// </para>
    /// </remarks>
    void LieOnTheRoad(
        int car, Vector2 standingM, float standingRad, Vector2 committedToM, float committedRad,
        bool underWay) =>
        LieUnder(onThePavement: false, car, standingM, standingRad, committedToM, committedRad, underWay);

    /// <summary>
    /// <b>The footway this body is standing on, claimed on the pavement's own ways</b> (TER-4c.2) — the
    /// stretch's two lanes and the mitres of the corners it is lying over, on the terms every other body on
    /// that network is laid on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the one pass, over the pavement's own geometry</b> (<see cref="IWayNetwork"/>) — the claims
    /// it writes into are the town's one table like every other (<see cref="TownWays"/>). A body holds the
    /// ground it occupies whatever it is and whatever it is doing, so what kind of ground that is is a fact
    /// about the ground and never about the body standing on it — a person in a lane is a stretch of that
    /// lane (<see cref="StandInTheRoad"/>) for exactly the same reason.
    /// </para>
    /// <para>
    /// <b>Nothing here is under way</b>, because no car drives a line of the walking network: the three
    /// readings a driver's own line already answers for on the road answer for nothing here, so every way
    /// under the box is written and the ground the body could not stop short of goes with it.
    /// </para>
    /// <para>
    /// <b>What a walker does about it is the walker's</b> (SIM-7): a car standing across a pavement is
    /// stepped round rather than queued behind (PER-24, <see cref="LaneOccupancy.UnderWay"/>), which is the
    /// same answer given for anything else lying there — and one <em>coming through</em> is waited
    /// for, because going nowhere is the body's own movement and not a property of its claim
    /// (<see cref="IsComingThrough"/>).
    /// </para>
    /// </remarks>
    void LieOnThePavement(
        int car, Vector2 standingM, float standingRad, Vector2 committedToM, float committedRad) =>
        LieUnder(
            onThePavement: true, car, standingM, standingRad, committedToM, committedRad, underWay: false);
}
