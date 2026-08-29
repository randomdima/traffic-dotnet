using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>Where a body that is not driving a route of its own lies</b>: the lane it is nearest, every join of a
/// junction it is lying in, and — for a car driving a template — the sweep it is committed to making.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// Where a car that is <em>not</em> driving a route of its own stands: on the lane it is nearest and
    /// on every join of a junction it is lying in, as the obstruction it is. <b>A car under way is placed
    /// by its own reservation</b> (<see cref="AskForTheGround"/>), which begins at that car's tail and is
    /// the only stretch it has.
    /// </summary>
    void PlaceWhatIsNotDriving(int car)
    {
        if (IsUnderWay(car)) return;

        var standingM = Cars.PositionM[car];
        var sweeping = WhereTheTemplateSweepEndsM(car, standingM, out var committedToM);

        // A car standing in a bay is on no lane by construction — a bay stands off the kerb — but it is on
        // the bay's own two ways, at the end of the one in and the start of the one out. Both are known
        // without asking the road anything, which is what keeps this off the town's hottest path: a town's
        // parked cars are most of its fleet, and a <see cref="RoadGraph.NearestLane"/> apiece every tick,
        // for an answer the reach test below would throw away, was the whole cost of this pass. One driving
        // the town's own way out is not here at all — it is under way, and its ground is its reservation on
        // that way (<see cref="AskForTheGround"/>).
        var bay = sweeping ? ParkingRegistry.NoBay : _parking.BayOf(car);

        // <b>And a body that has not moved lies where it lay</b> (<see cref="LyingBook"/>). The geometry
        // below is the same arithmetic over the same numbers for as long as the car stands still, which
        // for most of a town's fleet is the whole run.
        // <b>A car on a bar lays nothing of its own</b> (EVA-5). The ground under it is covered by the
        // reservation of the vehicle pulling it, which reaches back over the pair — one movement, one
        // stretch (TER-5c.2). Laid twice, the trailer's own row cuts its hauler's grant and the tow stops
        // dead at the first metre of road it is standing on.
        var towed = _recovery.OnTheHookOf[car] >= 0;
        var state = new LyingState(
            standingM, committedToM, Cars.VelocityMps[car], bay, Cars.MovementWay[car], Cars.Variant[car],
            sweeping, Cars.Driven[car], Cars.Broken[car], towed);
        if (_lying.Holds(car, state))
        {
            LieWhereItLay(car);
            return;
        }

        _lying.Begin(car, state);
        if (towed)
        {
            _lying.End();
            return;
        }

        if (bay >= 0)
        {
            LieInTheBay(car, bay);
            _lying.End();
            return;
        }

        // Anything else is wherever it actually lies, which is a question for the road and not for a line
        // it is no longer on: a wreck, a car nobody is in, a body shoved off its own route, and the swerve
        // halfway across the oncoming lane are all the same fact to whoever is coming up behind.
        LieUnder(car, standingM, committedToM);

        // Read from both ends, because the ways under one end of a sweep are regularly not the ways under
        // the other — and from each end the stretch is the whole sweep, so a way both ends are over is laid
        // once and identically (<see cref="LieOnTheWay"/>).
        if (sweeping) LieUnder(car, committedToM, standingM);

        _lying.End();
    }

    /// <summary>
    /// The stretches this body laid the last time its state changed, laid again — the same rows the
    /// geometry would have arrived at, into a book that holds none of this car's ground yet.
    /// </summary>
    void LieWhereItLay(int car)
    {
        var rows = _lying.Of(car);
        for (var at = 0; at < rows.Length; at++)
        {
            ref readonly var row = ref rows[at];
            _occupancy.AddUnderWay(
                row.Way, row.FromM, row.StandsToM, row.ToM, row.AlongMps, car, LaneUse.Obstruction);
        }
    }

    /// <summary>
    /// One stretch of a stationary body, into the book and into the record of what this body lays — the
    /// one place the two are kept together, so a row that reaches the book and not the record is not a
    /// thing that can be written.
    /// </summary>
    /// <remarks>
    /// <b>Laid on the terms every other body is laid on</b> (<see cref="AskForTheGround"/>): three edges, the
    /// middle one the body itself. What tells a wreck from a driver here is only how much of the third edge
    /// there is, and for something standing still there is none — which is the whole of why this is not a
    /// mechanism of its own.
    /// </remarks>
    void LieAt(int car, int way, float fromM, float standsToM, float toM, float alongMps)
    {
        _lying.Record(car, new LyingRow(way, fromM, standsToM, toM, alongMps));
        _occupancy.AddUnderWay(way, fromM, standsToM, toM, alongMps, car, LaneUse.Obstruction);
    }

    /// <summary>
    /// <b>A car standing in a bay, laid onto the bay's own two ways</b>: the far end of the way in, which is
    /// the pose that way was drawn to, and the near end of the way out, which is the same pose read the
    /// other way round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is what makes an occupied bay a fact the town can read</b> rather than a flag somebody has to
    /// remember to set: a car aiming at this bay drives the way in, and what stops it is the body standing
    /// at the end of it — the same headway that stops it behind anything else. Nothing is released, because
    /// this is laid from the car every tick like every other stretch in the book.
    /// </para>
    /// <para>
    /// <b>It is as much of the bay as the bay has to itself</b> (<see cref="BayStandings"/>) — never less
    /// than the tail of the body, which is all a way drawn to the axle has under it, and never more than the
    /// ground no other way in the town is driven over. Everything of the car that is not behind the axle is
    /// nose-deep in the bay, past the end of every way that reaches it, so the block is the bay's own depth
    /// and not the body's outline.
    /// </para>
    /// <para>
    /// <b>And so it holds none of the street.</b> A bay's mouth is half a metre off the carriageway's own
    /// edge, so the metres of its way that the lane is driven over reach a body's width past the mouth
    /// (<see cref="BayCrossings"/>) — laid from the mouth in regardless, a parked car cut the lane it was
    /// parked beside and every neighbour's way in with it. A parked car cuts nobody's grant on the road,
    /// which is exactly what a car off the carriageway should do.
    /// </para>
    /// </remarks>
    void LieInTheBay(int car, int bay)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        for (var slot = 0; slot < _bayWays.WayCountOf(bay); slot++)
        {
            var way = _bayWays.WayOf(bay, slot);

            // Which end of this body lies along the way is the standing's (GEN-4j), and how much of it
            // there is is the body's own (CAR-11).
            var bodyM = _bayWays.IsNoseIn(way) ? build.TailBehindAxleM : build.NoseAheadOfAxleM;
            var (fromM, toM) = _bayWays.WhereABodyInTheBayStandsM(way, _standings.HoldsM(way, bodyM));

            // <b>Its exact extent, with no margin and no ground ahead</b> — the one body in the town laid
            // otherwise, because a bay is not a lane. What it holds is bounded to the ground no other way is
            // driven over (<see cref="BayStandings"/>), which is what keeps a parked car off the street; a
            // margin either side of that is a parked car cutting the lane it is parked beside.
            LieAt(car, way, fromM, toM, toM, 0f);
        }
    }

    /// <summary>
    /// <b>Where a car driving a template of its own is committed to being</b>: the far end of that line, read
    /// for the middle of the body rather than for the axle the line is drawn for, and in whichever gear the
    /// line is driven. <b>False where there is no sweep left to make</b>, and the body's ground is the body.
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
    bool WhereTheTemplateSweepEndsM(int car, Vector2 standingM, out Vector2 endsM)
    {
        endsM = standingM;

        var line = Cars.Line[car];
        if (line.ArcCount == 0 || line.LaneCount > 0 || !Cars.Driven[car] || Cars.Broken[car]) return false;
        if (line.LengthM <= Cars.ProgressM[car]) return false;

        var at = Spline.SampleAt(Cars.LineArcsOf(car)[..line.ArcCount], line.LengthM);
        var forward = Heading.Unit(at.HeadingRad);

        endsM = at.PositionM + ((Cars.LineIsReverse[car] ? -forward : forward) * _config.CarCentreAheadOfAxleM);
        return true;
    }

    /// <summary>
    /// One end of a body's ground, laid onto the lane that end is nearest and onto every join of a junction
    /// it is lying in — as the whole stretch between the two ends, wherever the reading was taken from.
    /// </summary>
    /// <remarks>
    /// <b>Read from both ends and laid once</b>: which ways an end is over is a question about that end, and
    /// a body askew of the lane it is nearest is on none of it — so an end whose own band test fails is not
    /// an end that can answer for the other. What keeps the two readings to one stretch is the book
    /// (<see cref="LaneOccupancy.AlreadyHolds"/>) and never the order they were taken in.
    /// </remarks>
    void LieUnder(int car, Vector2 atM, Vector2 sweptToM)
    {
        // Half of <em>this</em> car either way, which is what a body lying askew covers along a way at
        // worst. It is the conservative reading of a pose the index deliberately does not carry the angle
        // of, and it is the body's own half-length because a wreck of a truck lies over more of a lane
        // than a wreck of a hatchback (CAR-11).
        var halfM = Cars.BuildOf(car).HalfLengthM;

        // <b>The same walk a driver on a template asks the book with</b> (<see cref="GroundUnder"/>): what a
        // body is written onto and what a manoeuvre reads are one set of ways, or a car could stand
        // somewhere the next car through cannot see it.
        Span<WayUnder> under = stackalloc WayUnder[GroundUnder.MostWaysUnderAPlace(_roads)];
        var count = GroundUnder.At(_roads, _occupancy, atM, Cars.BuildOf(car).FlankM, halfM, under);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref under[index];
            LieOnTheWay(car, way, atM, sweptToM, halfM);
        }
    }

    /// <summary>
    /// A body laid onto one of the town's ways where it stands inside that way's own band, and left off
    /// where it does not.
    /// </summary>
    /// <remarks>
    /// <b>The band and the body's own width</b>, and not how far the body is off the line. A wreck shoved
    /// sideways is still standing in what it was shoved into, and since the book is the whole of what a
    /// driver looks at (TER-4c), one left out of it here is one nothing can see: the reach a line's own
    /// tolerance allows is a bar on whether a car is still *driving* that line, which is a different
    /// question and a tighter one.
    /// <para>
    /// <b>And half a body past either end of the way, which is the same conservative reading taken along
    /// it</b>: the stretch laid below runs half a car either side of where the body projects, so a body
    /// standing further past the end than that is standing on nothing of this way.
    /// </para>
    /// <para>
    /// <b>A body on a template of its own is laid over the whole sweep it is committed to</b>
    /// (<see cref="WhereTheTemplateSweepEndsM"/>) and not only over the pose it is passing through: the
    /// ground a manoeuvre is about to be on is ground it is holding. The stretch reaches no further along
    /// this way than the sweep itself is long, because a template leaves the way it started on and a point
    /// projected onto a way it has left lands wherever that way happens to bend nearest to it.
    /// </para>
    /// <para>
    /// <b>And it is laid once</b> (TER-5c.2), whichever end of the sweep it was read from — the stretch is
    /// the same interval either way round, so the second reading is the first one over again and the book
    /// would count one body as two.
    /// </para>
    /// </remarks>
    void LieOnTheWay(int car, in WayUnder way, Vector2 atM, Vector2 sweptToM, float halfM)
    {
        // <b>Except the one it is crossing on</b>, where the ground it holds is the crossing it is making
        // (<see cref="LayTheMovement"/>). One body is one stretch of one way (TER-5c.2), and the two readings
        // are of one piece of ground in two measures: a projection across the box, and the metres of the line
        // the car was driving down it.
        if (way.Way == Cars.MovementWay[car] && Cars.Driven[car] && !Cars.Broken[car]) return;

        var arcs = LineOfWay(way.Way, out _);
        var alongM = way.AlongM;
        var sweptM = (sweptToM - atM).Length();
        var farM = sweptM <= 0f ? alongM : Spline.ProjectM(arcs, sweptToM, alongM, sweptM);
        var alongMps = Vector2.Dot(Cars.VelocityMps[car], way.AlongUnit);
        var fromM = MathF.Min(alongM, farM) - halfM;
        var standsToM = MathF.Max(alongM, farM) + halfM;

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
        var toM = sweptM > 0f
            ? standsToM
            : standsToM + StoppingM(
                alongMps, CarFollower.BrakingMps2(_config, Cars.BuildOf(car), Cars.GroundCoefficient[car]));
        if (_occupancy.AlreadyHolds(way.Way, fromM, toM, car)) return;

        LieAt(car, way.Way, fromM, standsToM, toM, alongMps);
    }

}
