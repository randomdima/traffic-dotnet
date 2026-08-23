using System.Numerics;
using TrafficSimulation.Core.Geometry;
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

        // A car standing in a bay is on no lane by construction — a bay stands off the kerb — and a town's
        // parked cars are most of its fleet. Asking the road where each of them is, every tick, for an
        // answer the reach test below would throw away, was the whole cost of this pass. One driving a
        // template out of a bay is a sweep across the lane and is held like any other.
        if (!sweeping && _parking.BayOf(car) >= 0) return;

        // Anything else is wherever it actually lies, which is a question for the road and not for a line
        // it is no longer on: a wreck, a car nobody is in, a body shoved off its own route, and the swerve
        // halfway across the oncoming lane are all the same fact to whoever is coming up behind.
        LieUnder(car, standingM, committedToM);

        // Read from both ends, because the ways under one end of a sweep are regularly not the ways under
        // the other — and from each end the stretch is the whole sweep, so a way both ends are over is laid
        // once and identically (<see cref="LieOnTheWay"/>).
        if (sweeping) LieUnder(car, committedToM, standingM);
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
        var lane = _roads.NearestLane(atM, out var alongM);
        if (lane < 0) return;

        // Half a car either way, which is what a body lying askew covers along a way at worst. It is the
        // conservative reading of a pose the index deliberately does not carry the angle of.
        var halfM = _config.Car.LengthM * 0.5f;
        LieOnTheWay(
            car, _occupancy.WayOfLane(lane), _roads.ArcsOf(lane), alongM, atM, sweptToM,
            _roads.LaneWidthM[lane], halfM);
        LieInTheBox(car, lane, alongM, atM, sweptToM, halfM);
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
    void LieOnTheWay(
        int car, int way, ReadOnlySpan<ArcSeg> arcs, float alongM, Vector2 atM, Vector2 sweptToM, float bandM,
        float halfM)
    {
        if (!RoadGraph.WithinTheBand(arcs, alongM, atM, bandM, _config.Car.WidthM * 0.5f, halfM, out var on))
        {
            return;
        }

        var sweptM = (sweptToM - atM).Length();
        var farM = sweptM <= 0f ? alongM : Spline.ProjectM(arcs, sweptToM, alongM, sweptM);
        var fromM = MathF.Min(alongM, farM) - halfM;
        var toM = MathF.Max(alongM, farM) + halfM;
        if (_occupancy.AlreadyHolds(way, fromM, toM, car)) return;

        _occupancy.Add(
            way, fromM, toM, Vector2.Dot(Cars.VelocityMps[car], on.Direction), car, LaneUse.Obstruction);
    }

    /// <summary>
    /// <b>A body standing in a junction, laid onto every one of that junction's joins it is lying under</b>
    /// — which is the whole of what holds the traffic crossing the box off it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A body in a box is on no lane anybody drives.</b> Past a lane's own setback (TER-5d) the lane's
    /// line runs on into the junction under a movement rather than under itself, and no driver's line is
    /// laid over that stretch (<see cref="WaysAlong"/>) — so a stretch put there is one nothing walks. The
    /// ground a car actually crosses a junction on is the join, and the join is a way of the road like any
    /// other.
    /// </para>
    /// <para>
    /// <b>What a car is crossing on cannot be what answers this.</b> It is given back the moment nobody is
    /// driving it (<see cref="PlaceTheCrossing"/>), and a wreck, a car under a hand and a body
    /// shoved into a box on no movement at all are none of them making anything — so what refuses the
    /// traffic crossing them is the ground they are lying on, and there is nothing else left to say it.
    /// </para>
    /// <para>
    /// <b>Both ends of the nearest lane are asked, and the setbacks are what say which of them can be
    /// it</b> (TER-5d): past one the ground stops being the lane's, and a lane that hands nothing over at
    /// that end still ends at a junction whose other arms are driven across it. A lane shorter than the
    /// junctions either side of it answers to both, which is why this is two questions and not a choice.
    /// </para>
    /// </remarks>
    void LieInTheBox(int car, int nearest, float alongM, Vector2 atM, Vector2 sweptToM, float halfM)
    {
        if (alongM <= _roads.JoinedAtM(nearest))
        {
            LieUnderTheJoins(car, _roads.LaneFromNode[nearest], atM, sweptToM, halfM);
        }

        if (alongM >= _roads.LaneLengthM[nearest] - _roads.LeftAtM(nearest))
        {
            LieUnderTheJoins(car, _roads.LaneToNode[nearest], atM, sweptToM, halfM);
        }
    }

    /// <summary>
    /// The joins of one junction, and this body laid onto each of them it is lying under — <b>except the one
    /// it is crossing</b>, where the ground it holds is the crossing it is making
    /// (<see cref="LayTheCrossing"/>). One body is one stretch of one way (TER-5c.2), and the two readings
    /// are of one piece of ground in two measures: a projection across the box, and the metres of the line
    /// the car was driving down it.
    /// </summary>
    void LieUnderTheJoins(int car, int node, Vector2 atM, Vector2 sweptToM, float halfM)
    {
        foreach (var arriving in _roads.LanesIn(node))
        {
            for (var turn = 0; turn < _roads.TurnsFrom(arriving).Length; turn++)
            {
                var slot = _roads.TurnSlotAt(arriving, turn);
                if (slot == Cars.Crossing[car] && Cars.Driven[car] && !Cars.Broken[car]) continue;

                var arcs = _roads.JoinArcs(slot);
                if (arcs.Length == 0) continue;

                var lengthM = _roads.JoinLengthM(slot);
                LieOnTheWay(
                    car, _occupancy.WayOfTurn(slot), arcs,
                    Spline.ProjectM(arcs, atM, lengthM * 0.5f, lengthM), atM, sweptToM,
                    _roads.LaneWidthM[arriving], halfM);
            }
        }
    }
}
