using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>Switch</b> (CAR-53, TER-4c.6): a car moving across onto the lane beside it running its way — where its route
/// moves across, or back toward the kerb. <b>Wanted, then looked for while the car drives on down its own lane</b>: at
/// every look a step across drawn from where the car then is, for the pace it is doing, and asked for as a short
/// stretch of the lane beside — the step and the room to stop past it — laid as a plan of its own, kept or withdrawn
/// once, and driven as the car's own line aimed across. Over once the car is on the lane beside,
/// whose line it is then handed (<see cref="ICarTown.TakeTheLaneBeside"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing stops for it but the end of the lane</b>: the car keeps its own plan down its own lane while it looks,
/// and its line runs on beside the route for as long as its lanes carry on beside it. Where they stop going the
/// route's way the line stops too, and the car stands short of there with room to step across from rest.
/// </para>
/// <para>
/// <b>The step is the pass's</b> (<see cref="Overtake"/>): one swing of the wheel across and none back, the shortest
/// the car can drive at the pace it is doing, landing inside the lane it begins on. Its ground is the two lanes it runs
/// down (<see cref="StepGround"/>) — its own up to where it lands and the lane beside on to the room past it — and held at
/// <see cref="ClaimPriority.Committed"/> once begun: a car that has left its lane cannot safely go back.
/// </para>
/// <para>
/// <b>Why it moves across is asked again at every look</b> (<see cref="Wants"/>), and a car that no longer wants to is
/// handed back to following its lane.
/// </para>
/// </remarks>
internal sealed class Switching(DrivingGround ground, CarActions actions)
{
    /// <summary>
    /// How many stretches a step's ground is: the ways of its own line to where it lands, and the lane beside. A bound
    /// on a stack span and not a figure behaviour reads.
    /// </summary>
    const int MostStretchesOfAStep = DrivingGround.MostWaysAlongALine + 1;

    /// <summary>Which way each car means to move across: toward the line its two ways meet on, or toward the kerb.</summary>
    readonly bool[] _inward = new bool[ground.Cars.Capacity];

    /// <summary>How far across the lane it means to move onto stood at its last decision — what it indicates toward (CAR-14.7).</summary>
    readonly float[] _asideM = new float[ground.Cars.Capacity];

    /// <summary>Where along the line the ground a car's step holds past it ends, at the rear axle.</summary>
    readonly float[] _roomToM = new float[ground.Cars.Capacity];

    /// <summary>How far ahead of where it stands a car's step from rest begins and lands — what it stands short of the lane's end for.</summary>
    readonly float[] _fromRestM = new float[ground.Cars.Capacity];

    /// <summary>How long until a car meaning to move across looks for the room again (<see cref="DrivingFigures.LaneSwitchAskEveryS"/>).</summary>
    readonly float[] _looksInS = new float[ground.Cars.Capacity];

    /// <summary>How many steps each car has asked for, and the ask the claims were last laid for — answered once the two agree.</summary>
    readonly long[] _askedAs = new long[ground.Cars.Capacity];

    /// <inheritdoc cref="_askedAs"/>
    readonly long[] _laidFor = new long[ground.Cars.Capacity];

    bool LaidSinceAsked(int car) => _askedAs[car] != 0 && _laidFor[car] == _askedAs[car];

    CarFleet Cars => ground.Cars;

    LaneOccupancy Occupancy => ground.Occupancy;

    RoadGraph Roads => ground.Roads;

    TownWays Ways => ground.Ways;

    SimConfig Config => ground.Config;

    /// <summary>Steps across asked for since the town was laid (CAR-53).</summary>
    public long Asked { get; private set; }

    /// <summary>And withdrawn in the rebuild after, because a body or a pass had the ground by then.</summary>
    public long Withdrawn { get; private set; }

    /// <summary>And driven, the car on the lane beside.</summary>
    public long Made { get; private set; }

    /// <summary>
    /// <b>Whether a car following its route means to move across, and which way</b> (CAR-53), in this order:
    /// <list type="number">
    /// <item><b>Where its line stops because its lanes no longer go the route's way</b> — toward the lane the route goes
    /// on from — <b>or because its lane ends</b> under a toured car, toward the nearest lane beside that carries on.</item>
    /// <item><b>Back toward the kerb</b>, where the lanes there carry its line on as far as it runs, and its route goes on
    /// from there.</item>
    /// </list>
    /// Each only where the lane beside runs on beside every lane of the line the car is driving (<see cref="RunsBeside"/>),
    /// so the line laid down it after lands back on the route. <b>Never to get past what holds it up</b>: whether that is
    /// at rest or slow is what the body is doing, which the claims do not say (TER-4c.5).
    /// </summary>
    public bool Wants(int car, out bool inward)
    {
        var shape = ShapeOfTheRoad(car, out inward);
        if ((shape & RoadShape.StopsToMoveAcross) != 0) return (shape & RoadShape.Wants) != 0;

        inward = false;
        return (shape & RoadShape.WantsWhenFree) != 0;
    }

    /// <summary>
    /// <b>What of <see cref="Wants"/> the road and the route alone answer</b> — the car's line, the lanes beside it and the
    /// route queued past it, which change only where a line or a route is laid.
    /// </summary>
    /// <param name="inward">Where the line stops to move across, which way.</param>
    /// <remarks>
    /// <b>Kept per car until its line or what its route queues next changes</b> (<see cref="KeptShape"/>): the rest is
    /// the road's, which does not.
    /// </remarks>
    public RoadShape ShapeOfTheRoad(int car, out bool inward)
    {
        var key = new KeptShape(
            Cars.LinesLaid[car], Cars.PeekNextRouteLane(car), Cars.PeekRouteLaneAfterNext(car), Cars.HasDestination[car],
            Cars.LineIsReverse[car], RoadShape.None, Inward: false, Kept: true);
        ref var kept = ref _shapes[car];
        if (kept with { Shape = RoadShape.None, Inward = false } == key)
        {
            inward = kept.Inward;
            return kept.Shape;
        }

        var shape = ReadTheShapeOfTheRoad(car, out inward);
        kept = key with { Shape = shape, Inward = inward };
        return shape;
    }

    /// <summary>
    /// <b>What <see cref="ShapeOfTheRoad"/> was last for a car, and what it was read against</b>: the line it had, the two
    /// lanes its route queued next, whether it was going anywhere and in which gear its line is driven.
    /// </summary>
    readonly record struct KeptShape(
        uint Line, int Next, int AfterNext, bool Destination, bool Reverse, RoadShape Shape, bool Inward, bool Kept);

    readonly KeptShape[] _shapes = new KeptShape[ground.Cars.Capacity];

    RoadShape ReadTheShapeOfTheRoad(int car, out bool inward)
    {
        inward = false;
        var lane = Cars.LaneOf(car);
        if (lane == CarFleet.NoLane || Cars.Line[car].LaneCount == 0 || Cars.LineIsReverse[car] || !Roads.IsAStreetLane(lane))
        {
            return RoadShape.None;
        }

        if (TheLineStopsToMoveAcross(car, out inward))
        {
            return RoadShape.StopsToMoveAcross | (RunsBeside(car, inward, out _) ? RoadShape.Wants : RoadShape.None);
        }

        var shape = RoadShape.Asks;
        if (!Roads.LaneCrossesToPass[lane] && RunsBeside(car, true, out _)) shape |= RoadShape.WantsWhenHeldUp;
        if (RunsBeside(car, false, out var lastBeside) && CarriesOnFrom(car, lastBeside)) shape |= RoadShape.WantsWhenFree;
        return shape;
    }

    /// <summary>What of <see cref="Wants"/> the road and the route alone answer (<see cref="ShapeOfTheRoad"/>).</summary>
    [Flags]
    public enum RoadShape : uint
    {
        /// <summary>A car on no street lane, on no line or driving it in reverse, which never wants to move across.</summary>
        None = 0u,

        /// <summary>Whether it is held up is asked, its line not stopping to move across.</summary>
        Asks = 1u,

        /// <summary>Its line stops where its route moves across, or its lane ends.</summary>
        StopsToMoveAcross = 2u,

        /// <summary>And the lanes that way carry its line on: it wants to move across whatever holds it up.</summary>
        Wants = 4u,

        /// <summary>Held up, the lane toward the line is one it moves across onto.</summary>
        WantsWhenHeldUp = 8u,

        /// <summary>Not held up, the lane toward the kerb is one it moves back onto.</summary>
        WantsWhenFree = 16u,
    }

    /// <summary>
    /// <b>A car meaning to move across, on its own clock</b>: let go where it no longer means to, and otherwise a step
    /// drawn from where it stands and asked for every <see cref="DrivingFigures.LaneSwitchAskEveryS"/>.
    /// </summary>
    /// <param name="sinceLastDecisionS">How much of the town's time this decision answers for, which its clock runs by.</param>
    public void Decide(int car, float sinceLastDecisionS) => Take(car, Consider(car, sinceLastDecisionS));

    /// <summary>
    /// <b>What a decision of a car meaning to move across comes to</b>, worked out before any of it is taken
    /// (<see cref="Take"/>): the swept ground of a step drawn is kept — nothing else is changed.
    /// </summary>
    public SwitchLook Consider(int car, float sinceLastDecisionS)
    {
        var pass = Cars.Pass[car];
        var look = new SwitchLook(
            SwitchOutcome.Begun, _inward[car], Turned: false, _asideM[car], _looksInS[car], _fromRestM[car], _roomToM[car], Overtake.None);
        if (pass.Begun) return look;

        // <b>A line that stops for a bay is the leg's own end</b>, which following knows how to come up to.
        if (Cars.StopsForBayOf(car) != CarFleet.NoBay || !Wants(car, out var inward)) return look with { Outcome = SwitchOutcome.LetsGo };

        // Turned to look the other way, a step asked toward the first is let go and the look is due at once.
        var turned = inward != _inward[car];
        if (turned) pass = Overtake.None;

        var looksInS = (turned ? 0f : _looksInS[car]) - sinceLastDecisionS;
        look = look with
        {
            Outcome = SwitchOutcome.Waits, Inward = inward, Turned = turned, AsideM = AsideOnTheLaneBeside(car, inward), LooksInS = looksInS,
        };
        if (looksInS > 0f || pass.Any) return look;

        look = look with { Outcome = SwitchOutcome.Looked, LooksInS = Config.Driving.LaneSwitchAskEveryS };
        var shaped = Shape(car, inward, look.AsideM, Cars.ProgressM[car], MathF.Max(0f, Cars.AlongMps[car]), out var step, ref look);
        if (!shaped) return look;

        return IsUnheld(car, step, inward, look.RoomToM) ? look with { Outcome = SwitchOutcome.Asks, Step = step } : look;
    }

    /// <summary>A decision taken as <see cref="Consider"/> worked it out.</summary>
    public void Take(int car, in SwitchLook look)
    {
        switch (look.Outcome)
        {
            case SwitchOutcome.Begun:
                return;
            case SwitchOutcome.LetsGo:
                actions.Enter(car, CarAction.Follow);
                return;
        }

        if (look.Turned) Cars.Pass[car] = Overtake.None;

        _inward[car] = look.Inward;
        _asideM[car] = look.AsideM;
        _looksInS[car] = look.LooksInS;
        _fromRestM[car] = look.FromRestM;
        _roomToM[car] = look.RoomToM;
        if (look.Outcome != SwitchOutcome.Asks) return;

        Cars.Pass[car] = look.Step;
        _askedAs[car]++;
        Asked++;
    }

    /// <summary>
    /// <b>This tick of a car moving across</b>, once the line under it has been read: driven down its step and handed the
    /// lane beside once it is on it, or driven on down its own lane while it waits — its step begun or withdrawn in the
    /// tick after it was asked for.
    /// </summary>
    public void Tick<TTown>(ref TTown town, int car, in CarPose pose, float progressM, float alongMps)
        where TTown : struct, ICarTown
    {
        var pass = Cars.Pass[car];
        if (pass.Begun)
        {
            if (progressM >= pass.SteppedOutM)
            {
                SteppedOut(ref town, car, progressM);
                town.DriveOnTheLine(car, pose, Cars.ProgressM[car], alongMps);
                return;
            }

            town.DriveOnTheLine(car, pose, progressM, alongMps);
            return;
        }

        if (pass.Any && LaidSinceAsked(car)) KeepOrWithdraw(car, pass, progressM);

        town.DriveOnTheLine(car, pose, progressM, alongMps, passAsideM: _asideM[car], waitAtM: WaitAtM(car, progressM));
    }

    /// <summary>
    /// <b>A car that has stepped out onto the lane beside</b>: handed back to following first, which lets the step's ground
    /// go, and then granted what of it lies ahead, on a line laid down the lane beside (<see cref="ICarTown.TakeTheLaneBeside"/>).
    /// </summary>
    public void SteppedOut<TTown>(ref TTown town, int car, float progressM)
        where TTown : struct, ICarTown
    {
        var pass = Cars.Pass[car];
        var heldAheadM = _roomToM[car] - progressM - ground.LeadingEdgeAheadOfTheAxleM(car);
        actions.Enter(car, CarAction.Follow);
        town.TakeTheLaneBeside(car, Roads.LaneBeside(pass.Lane, _inward[car]), heldAheadM);
        Made++;
    }

    /// <summary>
    /// <b>The ground this car's step will cover, asked as a plan of its own</b> (TER-4c.6): what of its two lanes it has
    /// not yet driven past (<see cref="StepGround"/>), had whole before it is begun and at
    /// <see cref="ClaimPriority.Committed"/> after. A step the car is no longer on its line for, or no longer on the lane
    /// of, is let go.
    /// </summary>
    public void Lay(int car, bool underWay)
    {
        Occupancy.Release(_holds[car]);
        _holds[car] = LaneOccupancy.NoHold;
        var pass = Cars.Pass[car];
        if (Cars.Action[car] != CarAction.Switch || !pass.Any) return;

        if (!underWay || Cars.LaneOf(car) != pass.Lane)
        {
            if (pass.Begun) actions.Enter(car, CarAction.Follow);
            else Cars.Pass[car] = Overtake.None;

            return;
        }

        var hold = _holds[car] = Occupancy.BeginHold(car, LaneRoster.Driving, whole: !pass.Begun);
        var rung = pass.Begun ? ClaimPriority.Committed : ClaimPriority.Firm;
        Span<LineWay> stretches = stackalloc LineWay[MostStretchesOfAStep];
        var count = StepGround(car, pass, _inward[car], Cars.ProgressM[car], _roomToM[car], 0f, stretches);
        foreach (ref readonly var stretch in stretches[..count])
        {
            Occupancy.Ask(hold, stretch.Way, stretch.FromM, stretch.ToM, stretch.LineFromM, rung);
        }

        _laidFor[car] = _askedAs[car];
    }

    /// <summary>The hold each car's step was asked in this rebuild, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    readonly int[] _holds = LaneOccupancy.NoHolds(ground.Cars.Capacity);

    /// <summary>The hold this car's step was asked in this rebuild, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    public int HoldOf(int car) => _holds[car];

    /// <summary>The step measured again from the next lane of the chain, which begins <paramref name="shiftM"/> along the line.</summary>
    public void ShiftTheLine(int car, float shiftM)
    {
        if (Cars.Action[car] != CarAction.Switch || !Cars.Pass[car].Any) return;

        _roomToM[car] -= shiftM;
    }

    /// <summary>
    /// <b>Whether the car's line stops where its route moves across</b>: the next lane the route queues is joined to the
    /// line's last by no connector but reached by moving across onto it or onto a lane beside that joins it, and the line
    /// cannot carry on beside the route from there — and which way. <b>Or where its lane ends</b> under a car going
    /// nowhere in particular (<see cref="TheLaneEnds"/>).
    /// </summary>
    /// <remarks>
    /// <b>A line still running on beside the route is not stopped</b>, however far it is from the lanes the route was
    /// searched down: the car moves across where it has to, and keeps to the kerb until then.
    /// </remarks>
    bool TheLineStopsToMoveAcross(int car, out bool inward)
    {
        inward = false;
        var last = Cars.ChainOf(car)[Cars.Line[car].LaneCount - 1];
        var next = Cars.PeekNextRouteLane(car);
        if (next == CarFleet.NoLane) return !Cars.HasDestination[car] && TheLaneEnds(last, out inward);

        if (Roads.ConnectorBetween(last, next) != RoadGraph.NoConnector
            || !Roads.ReachesBySwitching(last, next, out var beside)
            || Roads.OnBesideTheRoute(last, next, Cars.PeekRouteLaneAfterNext(car), out _) != RoadGraph.NoLane)
        {
            return false;
        }

        inward = Roads.LanesOver(last, beside) > 0;
        return true;
    }

    /// <summary>
    /// <b>Whether a lane is lost at the node it runs into</b> (TER-5j): joined to nothing there, beside a lane running its
    /// way that is — and which way the nearest of those stands. A toured line stops on it (<see cref="LaneTour"/>), and the
    /// car leaves it by moving across, as a route moving across there would have it.
    /// </summary>
    bool TheLaneEnds(int lane, out bool inward)
    {
        inward = false;
        if (Roads.LanesFrom(lane).Length > 0) return false;

        var nearest = int.MaxValue;
        foreach (var side in (ReadOnlySpan<bool>)[true, false])
        {
            for (var beside = Roads.LaneBeside(lane, side); beside != RoadGraph.NoLane; beside = Roads.LaneBeside(beside, side))
            {
                if (Roads.LanesFrom(beside).Length == 0) continue;

                var over = Math.Abs(Roads.LanesOver(lane, beside));
                if (over < nearest)
                {
                    nearest = over;
                    inward = side;
                }

                break;
            }
        }

        return nearest != int.MaxValue;
    }

    /// <summary>
    /// <b>Whether the lanes on one side carry the car's line on beside it</b>: a lane beside each lane of its chain, each
    /// joined to the one beside the lane before — <b>or joined back onto the chain itself</b>, where the road runs on in
    /// fewer lanes. It is the line laid down the lane beside once the car is on it.
    /// </summary>
    /// <param name="lastBeside">The lane beside the chain's last, or <see cref="RoadGraph.NoLane"/> where the lanes beside join it again first.</param>
    bool RunsBeside(int car, bool inward, out int lastBeside)
    {
        var chain = Cars.ChainOf(car)[..Cars.Line[car].LaneCount];
        lastBeside = Roads.LaneBeside(chain[0], inward);
        if (lastBeside == RoadGraph.NoLane) return false;

        for (var slot = 1; slot < chain.Length; slot++)
        {
            if (Roads.ConnectorBetween(lastBeside, chain[slot]) != RoadGraph.NoConnector)
            {
                lastBeside = RoadGraph.NoLane;
                return true;
            }

            var beside = Roads.LaneBeside(chain[slot], inward);
            if (beside == RoadGraph.NoLane || Roads.ConnectorBetween(lastBeside, beside) == RoadGraph.NoConnector) return false;

            lastBeside = beside;
        }

        return true;
    }

    /// <summary>
    /// <b>Whether the route carries on from a lane beside the line's last</b>: the lanes beside join the line again before
    /// its end, or the last of them joins the next lane the route queues, or a lane beside that one — anywhere else the car
    /// would be moving straight back.
    /// </summary>
    /// <remarks>
    /// <b>A line with no route queued past it is the leg's last</b>, ending at the place it is going to, which is no lane to
    /// move off.
    /// </remarks>
    bool CarriesOnFrom(int car, int lastBeside)
    {
        if (lastBeside == RoadGraph.NoLane) return true;

        var next = Cars.PeekNextRouteLane(car);
        if (next == CarFleet.NoLane) return false;

        foreach (var onward in Roads.LanesFrom(lastBeside))
        {
            if (onward == next || Roads.AreSideBySide(onward, next)) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>Where a car meaning to move across for its route stands</b>: short of the line's end by the step it takes from
    /// rest, where its line stops because its lane no longer goes the route's way — and nowhere, where it moves across
    /// for anything else.
    /// </summary>
    float WaitAtM(int car, float progressM)
    {
        if (!TheLineStopsToMoveAcross(car, out _)) return float.PositiveInfinity;

        var toM = Cars.Line[car].LengthM - _fromRestM[car] - Config.Driving.PassSpareM - progressM;
        return toM >= 0f ? toM : float.PositiveInfinity;
    }

    /// <summary>
    /// <b>A step across drawn for a car doing <paramref name="speedMps"/></b>: begun as far ahead of the car as it covers
    /// before the step can be laid and kept, the shortest it can drive at that pace, landing inside the lane it begins on,
    /// and the room to stop in past it at the pace it is driven at — its end and the step from rest kept in
    /// <paramref name="look"/>.
    /// </summary>
    bool Shape(int car, bool inward, float asideM, float progressM, float speedMps, out Overtake step, ref SwitchLook look)
    {
        step = Overtake.None;
        ref readonly var build = ref Cars.BuildOf(car);
        var lane = Cars.LaneOf(car);
        var beside = Roads.LaneBeside(lane, inward);
        if (beside == RoadGraph.NoLane) return false;

        var corneringMps2 = build.PlannedCorneringMps2;
        var outM = progressM + (speedMps * 2f * Config.CarReactionS) + Config.Driving.PassSpareM;
        var landsByM = Cars.LaneEndsOf(car)[0];

        look = look with
        {
            FromRestM = CarFollower.ShapeAPass(build, 0f, asideM, corneringMps2, 0f, out var fromRestM, out _)
                ? fromRestM + Config.Driving.PassSpareM
                : float.PositiveInfinity,
        };

        var lineBend = 0f;
        while (true)
        {
            if (!CarFollower.ShapeAPass(build, speedMps, asideM, corneringMps2, lineBend, out var stepM, out var fromMps))
            {
                return false;
            }

            var steppedOutM = outM + stepM;
            if (steppedOutM > landsByM) return false;

            var outMps = MathF.Max(fromMps, CarFollower.StepMps(build, stepM, asideM, corneringMps2, lineBend));
            var roomM = CarFollower.SlowsOverM(build, outMps) + Config.Driving.StandOffM;
            var roomToM = MathF.Min(steppedOutM + build.NoseAheadOfAxleM + roomM, Cars.Line[car].LengthM);

            var bend = MostBendUnder(car, outM, roomToM, asideM);
            if (float.IsPositiveInfinity(bend)) return false;

            if (bend > lineBend)
            {
                lineBend = bend;
                continue;
            }

            look = look with { RoomToM = roomToM };
            step = new Overtake(
                lane, outM, stepM, float.PositiveInfinity, 0f, asideM, outMps, float.PositiveInfinity, steppedOutM, Begun: false);
            return IsOnTheRoad(car, step, inward, progressM, roomToM);
        }
    }

    /// <summary>
    /// <b>A step's ground, as the two lanes it runs down</b> (TER-4c.6): the ways of its own line from the car's nose to
    /// where the step lands it, and the lane beside from where its tail stands as the step begins to
    /// <paramref name="toLineM"/>, the end of the room past it — each grown by <paramref name="spareM"/> at its ends, and
    /// nothing behind the car standing at <paramref name="progressM"/>.
    /// </summary>
    /// <remarks>
    /// <b>The lane beside is read in step with the lane the step begins on</b>, the two being offsets of one road run the
    /// same way (<see cref="Aside"/>): a metre of the one is the same share of the other's length. A step lands inside the
    /// lane it begins on, and the room past it runs no further than the lane beside does — the line laid down that lane
    /// once the car is on it plans the rest.
    /// </remarks>
    int StepGround(
        int car, in Overtake step, bool inward, float progressM, float toLineM, float spareM, Span<LineWay> into)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var noseM = progressM + build.NoseAheadOfAxleM;
        var landsM = MathF.Min(toLineM, step.SteppedOutM + build.NoseAheadOfAxleM) + spareM;
        var count = noseM < landsM ? ground.WaysAlong(car, noseM, landsM, into[..^1]) : 0;

        var beside = Roads.LaneBeside(step.Lane, inward);
        if (beside == RoadGraph.NoLane) return count;

        var startM = Cars.LaneStartsOf(car)[0];
        var share = Roads.LaneLengthM[beside] / Roads.LaneLengthM[step.Lane];
        var fromLineM = MathF.Max(step.OutM, progressM) - build.TailBehindAxleM - spareM;
        var fromM = Math.Clamp((fromLineM - startM) * share, 0f, Roads.LaneLengthM[beside]);
        var toM = Math.Clamp((toLineM + spareM - startM) * share, 0f, Roads.LaneLengthM[beside]);
        if (toM > fromM) into[count++] = new LineWay(Ways.OfRoadLane(beside), fromM, toM, startM + (fromM / share));

        return count;
    }

    /// <summary>
    /// <b>Whether the ground a step would cover is road a step may be had on</b> (TER-4c.6): clear of every zebra.
    /// </summary>
    bool IsOnTheRoad(int car, in Overtake step, bool inward, float progressM, float toLineM)
    {
        Span<LineWay> stretches = stackalloc LineWay[MostStretchesOfAStep];
        foreach (ref readonly var stretch in stretches[..StepGround(car, step, inward, progressM, toLineM, 0f, stretches)])
        {
            if (ground.CrossesAZebra(stretch.Way, stretch.FromM, stretch.ToM)) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>And whether it is free</b> (TER-4c.6): nobody standing on any of it but the car, grown by the pass's spare.
    /// Whether a plan holds any of it is answered when the step is laid, after every plan of its rung — so traffic coming
    /// up the lane beside, whose plan reaches over it, is let by first.
    /// </summary>
    bool IsUnheld(int car, in Overtake step, bool inward, float toLineM)
    {
        Span<LineWay> stretches = stackalloc LineWay[MostStretchesOfAStep];
        var count = StepGround(car, step, inward, Cars.ProgressM[car], toLineM, Config.Driving.PassSpareM, stretches);
        foreach (ref readonly var stretch in stretches[..count])
        {
            if (Occupancy.AnyBodyOver(stretch.Way, stretch.FromM, stretch.ToM, car, LaneRoster.Driving)) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>A step laid in this rebuild, kept or withdrawn</b> before the car moves across: kept where the whole of its
    /// ground was had (<see cref="DrivingGround.IsKept"/>) — and only while the car can still come up to where it begins.
    /// </summary>
    void KeepOrWithdraw(int car, in Overtake step, float progressM)
    {
        if (progressM <= step.OutM && ground.IsKept(_holds[car]))
        {
            Cars.Pass[car] = step with { Begun = true };
            return;
        }

        Cars.Pass[car] = Overtake.None;
        Withdrawn++;
    }

    /// <summary>
    /// <b>The most a car's line bends under a stretch of it</b> — on its own line or on the lane beside, whichever is
    /// tighter. Infinite where the lane beside runs round the line's own centre.
    /// </summary>
    float MostBendUnder(int car, float fromM, float toM, float asideM)
    {
        var mostBend = 0f;
        var startM = 0f;
        foreach (ref readonly var arc in Cars.LineOf(car))
        {
            var endM = startM + arc.LengthM;
            if (startM >= toM) break;

            if (endM > fromM)
            {
                // A line of curvature k moved a distance d towards its centre bends at k ⁄ (1 − k·d).
                var towardsTheCentre = 1f - (arc.Curvature * asideM);
                if (towardsTheCentre <= 0f) return float.PositiveInfinity;

                mostBend = MathF.Max(mostBend, MathF.Max(MathF.Abs(arc.Curvature), MathF.Abs(arc.Curvature / towardsTheCentre)));
            }

            startM = endM;
        }

        return mostBend;
    }

    /// <summary>How far across the lane beside the car means to move onto stands from where it is — what it indicates toward (CAR-14.7).</summary>
    float AsideOnTheLaneBeside(int car, bool inward)
    {
        var lane = Cars.LaneOf(car);
        var beside = lane == CarFleet.NoLane ? RoadGraph.NoLane : Roads.LaneBeside(lane, inward);
        return beside == RoadGraph.NoLane ? 0f : Aside(lane, beside, Math.Clamp(Cars.ProgressM[car], 0f, Roads.LaneLengthM[lane]));
    }

    /// <summary>
    /// <b>How far across a lane beside stands at a place on a lane</b>, along the driver's right: the nearest point of
    /// its line, sought where the two lengths put it — the two are offsets of one line, run the same way.
    /// </summary>
    float Aside(int lane, int beside, float alongM)
    {
        var lengthM = Roads.LaneLengthM[lane];
        var besideLengthM = Roads.LaneLengthM[beside];
        var on = Spline.SampleAt(Roads.ArcsOf(lane), alongM);
        var windowM = MathF.Abs(besideLengthM - lengthM) + Roads.LaneWidthM[lane] + Roads.LaneWidthM[beside];
        var besideM = Spline.ProjectM(Roads.ArcsOf(beside), on.PositionM, alongM * (besideLengthM / lengthM), windowM);
        return Vector2.Dot(Spline.SampleAt(Roads.ArcsOf(beside), besideM).PositionM - on.PositionM, on.Right);
    }
}
