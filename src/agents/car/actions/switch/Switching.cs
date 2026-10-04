using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>Switch</b> (CAR-53, TER-4c.6): a car moving across onto the lane beside it running its way — where its route
/// moves across, to get past what holds it up in its lane, or back toward the kerb. <b>Wanted, then looked for while
/// the car drives on down its own lane</b>: at every look a step across drawn from where the car then is, for the pace
/// it is doing, and asked for as a short stretch of the lane beside — the step and the room to stop past it — laid as a
/// body, kept or withdrawn once, and driven as the car's own line aimed across. Over once the car is on the lane beside,
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
/// the car can drive at the pace it is doing, landing inside the lane it begins on. Its ground is the car's own body
/// swept down it and read off the atlas, as a pass's is, and held at p0 — a car that has left its lane cannot safely go
/// back.
/// </para>
/// <para>
/// <b>Why it moves across is asked again at every look</b> (<see cref="Wants"/>), and a car that no longer wants to is
/// handed back to following its lane.
/// </para>
/// </remarks>
internal sealed class Switching(DrivingGround ground, CarActions actions, Overtaking overtaking)
{
    /// <summary>
    /// How many runs one switch's ground is kept in (<see cref="SweptGround"/>). A bound on the table and not a figure
    /// behaviour reads: a run is one way under a car's length of the step, and a step lands inside one lane.
    /// </summary>
    const int MostRunsOfASwitch = 64;

    /// <summary>The ground each car's step sweeps — what is laid, kept, and looked for a body in.</summary>
    readonly SweptGround _swept = new(ground.Cars.Capacity, MostRunsOfASwitch);

    /// <summary>And the same grown by the pass's spare and run on by the stand-off — what is asked for.</summary>
    readonly SweptGround _asked = new(ground.Cars.Capacity, MostRunsOfASwitch);

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

    /// <summary>Whether the claims have been laid since a car asked for its step — when the ask is answered.</summary>
    readonly bool[] _laidSinceAsked = new bool[ground.Cars.Capacity];

    CarFleet Cars => ground.Cars;

    LaneOccupancy Occupancy => ground.Occupancy;

    RoadGraph Roads => ground.Roads;

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
    /// on from.</item>
    /// <item><b>To get past what holds it up</b>, on a road whose line is not crossed to pass (CAR-6.2b): a body at rest
    /// that is not making its own movement, or traffic going slower than the car means to by more than
    /// <see cref="DrivingFigures.SlowerToSwitchMps"/> — over the lane toward the line, and never the kerb's.</item>
    /// <item><b>Back toward the kerb</b>, where nothing holds it up, the lanes there carry its line on as far as it runs,
    /// and its route goes on from there.</item>
    /// </list>
    /// Each only where the lane beside runs on beside every lane of the line the car is driving (<see cref="RunsBeside"/>),
    /// so the line laid down it after lands back on the route.
    /// </summary>
    public bool Wants(int car, out bool inward)
    {
        inward = false;
        var lane = Cars.LaneOf(car);
        if (lane == CarFleet.NoLane || Cars.Line[car].LaneCount == 0 || Cars.LineIsReverse[car] || !Roads.IsAStreetLane(lane))
        {
            return false;
        }

        if (TheLineStopsToMoveAcross(car, out inward)) return RunsBeside(car, inward, out _);

        var heldUp = IsHeldUp(car);
        if (heldUp && !Roads.LaneCrossesToPass[lane] && RunsBeside(car, true, out _))
        {
            inward = true;
            return true;
        }

        inward = false;
        return !heldUp && RunsBeside(car, false, out var lastBeside) && CarriesOnFrom(car, lastBeside);
    }

    /// <summary>
    /// <b>A car meaning to move across, on its own clock</b>: let go where it no longer means to, and otherwise a step
    /// drawn from where it stands and asked for every <see cref="DrivingFigures.LaneSwitchAskEveryS"/>.
    /// </summary>
    /// <param name="sinceLastDecisionS">How much of the town's time this decision answers for, which its clock runs by.</param>
    public void Decide(int car, float sinceLastDecisionS)
    {
        var pass = Cars.Pass[car];
        if (pass.Begun) return;

        // <b>A line that stops for a bay is the leg's own end</b>, which following knows how to come up to.
        if (Cars.StopsForBayOf(car) != CarFleet.NoBay || !Wants(car, out var inward))
        {
            actions.Enter(car, CarAction.Follow);
            return;
        }

        if (inward != _inward[car])
        {
            _inward[car] = inward;
            Cars.Pass[car] = Overtake.None;
            _looksInS[car] = 0f;
        }

        _asideM[car] = AsideOnTheLaneBeside(car);

        ref var inS = ref _looksInS[car];
        inS -= sinceLastDecisionS;
        if (inS > 0f || pass.Any) return;

        inS = Config.Driving.LaneSwitchAskEveryS;
        if (Shape(car, Cars.ProgressM[car], MathF.Max(0f, Cars.AlongMps[car]), out var shape) && Sweep(car, shape)
            && IsUnheld(car))
        {
            Cars.Pass[car] = shape;
            _laidSinceAsked[car] = false;
            Asked++;
        }
    }

    /// <summary>
    /// <b>This tick of a car moving across</b>, once the line under it has been read: driven down its step and handed the
    /// lane beside once it is on it, or driven on down its own lane while it waits — its step begun or withdrawn in the
    /// tick after it was asked for.
    /// </summary>
    public void Tick<TTown>(ref TTown town, int car, in CarPose pose, float progressM, float alongMps, float coveredM)
        where TTown : struct, ICarTown
    {
        var pass = Cars.Pass[car];
        if (pass.Begun)
        {
            if (progressM >= pass.SteppedOutM)
            {
                // Handed back to following first, which lets the step's ground go, and then granted what of it lies ahead.
                var heldAheadM = _roomToM[car] - progressM - ground.LeadingEdgeAheadOfTheAxleM(car);
                actions.Enter(car, CarAction.Follow);
                town.TakeTheLaneBeside(car, Roads.LaneBeside(pass.Lane, _inward[car]), heldAheadM);
                Made++;
                town.DriveOnTheLine(car, pose, Cars.ProgressM[car], alongMps, 0f);
                return;
            }

            town.DriveOnTheLine(car, pose, progressM, alongMps, coveredM);
            return;
        }

        if (pass.Any && _laidSinceAsked[car]) KeepOrWithdraw(car, pass, progressM);

        town.DriveOnTheLine(car, pose, progressM, alongMps, coveredM, passAsideM: _asideM[car], waitAtM: WaitAtM(car, progressM));
    }

    /// <summary>
    /// <b>The ground this car's step will cover, laid as a body</b> (TER-4c.6): every run of its swept ground it has not
    /// yet driven past. A step the car is no longer on its line for, or no longer on the lane of, is let go.
    /// </summary>
    public void Lay(int car, bool underWay)
    {
        var pass = Cars.Pass[car];
        if (Cars.Action[car] != CarAction.Switch || !pass.Any) return;

        if (!underWay || Cars.LaneOf(car) != pass.Lane)
        {
            if (pass.Begun) actions.Enter(car, CarAction.Follow);
            else Cars.Pass[car] = Overtake.None;

            return;
        }

        var progressM = Cars.ProgressM[car];
        foreach (ref readonly var run in _swept.Of(car))
        {
            if (run.LastAtM < progressM) continue;

            var held = ground.HeldAsABody(run.Cover);
            Occupancy.LayPass(held.Way, held.FromM, held.ToM, 0f, car, LaneRoster.Driving);
        }

        _laidSinceAsked[car] = true;
    }

    /// <summary>The step measured again from the next lane of the chain, which begins <paramref name="shiftM"/> along the line.</summary>
    public void ShiftTheLine(int car, float shiftM)
    {
        if (Cars.Action[car] != CarAction.Switch || !Cars.Pass[car].Any) return;

        _swept.Shift(car, shiftM);
        _asked.Shift(car, shiftM);
        _roomToM[car] -= shiftM;
    }

    /// <summary>
    /// <b>Where the nose of a car on its step has to stop short of a body standing inside what is left of it</b> — the
    /// one thing that ends a step's ground short of its end, since nothing planned can be laid over it (TER-4c.1).
    /// </summary>
    public bool TheBodyInTheSwitch(int car, out float inTheWayM, out LaneClaim body, out int on)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var progressM = Cars.ProgressM[car];
        foreach (ref readonly var run in _swept.Of(car))
        {
            if (run.LastAtM < progressM || !Occupancy.AheadBody(run.Way, run.FromM, run.ToM, car, out body)) continue;

            on = run.Way;
            inTheWayM = MathF.Max(progressM, run.FirstAtM - build.FlankM) + build.NoseAheadOfAxleM;
            return true;
        }

        inTheWayM = float.PositiveInfinity;
        body = LaneClaim.Nothing;
        on = LaneOccupancy.NoHold;
        return false;
    }

    /// <summary>
    /// <b>Whether the car's line stops where its route moves across</b>: the next lane the route queues is joined to the
    /// line's last by no connector but reached by moving across onto it or onto a lane beside that joins it, and the line
    /// cannot carry on beside the route from there — and which way.
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
        if (next == CarFleet.NoLane || Roads.ConnectorBetween(last, next) != RoadGraph.NoConnector
            || !Roads.ReachesBySwitching(last, next, out var beside)
            || Roads.OnBesideTheRoute(last, next, Cars.PeekRouteLaneAfterNext(car), out _) != RoadGraph.NoLane)
        {
            return false;
        }

        inward = Roads.LanesOver(last, beside) > 0;
        return true;
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
    /// <b>Whether what ended the car's grant holds it up</b>: a body at rest that is not making the car's own movement,
    /// or traffic going slower than the car means to by more than <see cref="DrivingFigures.SlowerToSwitchMps"/> — once
    /// the car has come to where it would begin slowing for it.
    /// </summary>
    bool IsHeldUp(int car)
    {
        var hold = ground.PlanHold[car];
        if (hold == LaneOccupancy.NoHold) return false;

        Occupancy.HoldEndsAtM(hold, out _, out var cutBy);
        if (!cutBy.Found || !cutBy.HasBody || cutBy.Passing) return false;

        var cutOn = Occupancy.HoldCutOn(hold);
        var slower = cutBy.IsTraffic && cutBy.AlongMps < Cars.PlannedMps[car] - Config.Driving.SlowerToSwitchMps;
        return (cutBy.MayBePassedBy(ground.OnwardAlongTheLine(car, cutOn)) || slower)
               && overtaking.ComesUpTo(car, Cars.AlongMps[car], Cars.Context[car].AuthorityM);
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
    /// and the room to stop in past it at the pace it is driven at — its end kept in <see cref="_roomToM"/>.
    /// </summary>
    bool Shape(int car, float progressM, float speedMps, out Overtake step)
    {
        step = Overtake.None;
        ref readonly var build = ref Cars.BuildOf(car);
        var lane = Cars.LaneOf(car);
        var beside = Roads.LaneBeside(lane, _inward[car]);
        if (beside == RoadGraph.NoLane) return false;

        var asideM = Aside(lane, beside, Math.Clamp(progressM, 0f, Roads.LaneLengthM[lane]));
        var corneringMps2 = CarFollower.CorneringMps2(Config, build, Cars.GroundCoefficient[car]);
        var brakingMps2 = CarFollower.BrakingMps2(Config, build, Cars.GroundCoefficient[car]);
        var outM = progressM + (speedMps * 2f * Config.CarReactionS) + Config.Driving.PassSpareM;
        var landsByM = Cars.LaneEndsOf(car)[0];

        _fromRestM[car] = CarFollower.ShapeAPass(build, 0f, asideM, corneringMps2, 0f, out var fromRestM, out _)
            ? fromRestM + Config.Driving.PassSpareM
            : float.PositiveInfinity;

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
            var roomM = (outMps * Config.CarReactionS) + DrivingGround.StoppingM(outMps, brakingMps2) + Config.Driving.StandOffM;
            var roomToM = MathF.Min(steppedOutM + build.NoseAheadOfAxleM + roomM, Cars.Line[car].LengthM);

            var bend = MostBendUnder(car, outM, roomToM, asideM);
            if (float.IsPositiveInfinity(bend)) return false;

            if (bend > lineBend)
            {
                lineBend = bend;
                continue;
            }

            _roomToM[car] = roomToM;
            step = new Overtake(
                lane, outM, stepM, float.PositiveInfinity, 0f, asideM, outMps, float.PositiveInfinity, steppedOutM, Begun: false);
            return IsOnTheRoad(car, step, progressM, roomToM);
        }
    }

    /// <summary>
    /// <b>A step's ground, swept once</b> from where the car stands to the end of the room past it: what its body covers,
    /// for laying and keeping, and the same grown by the pass's spare and run on by the stand-off, for asking. False where
    /// it does not fit in the runs a step may have.
    /// </summary>
    [SkipLocalsInit]
    bool Sweep(int car, in Overtake step)
    {
        var spanM = Cars.BuildOf(car).LengthM;
        var fromM = Cars.ProgressM[car];
        var toM = _roomToM[car];
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        return SweepInto(_swept, car, step, fromM, toM, 0f, spanM, under)
               && SweepInto(_asked, car, step, fromM, toM, Config.Driving.PassSpareM, spanM, under);
    }

    bool SweepInto(
        SweptGround into, int car, in Overtake step, float fromM, float toM, float spareM, float spanM, Span<WayCover> under)
    {
        into.Clear(car);
        for (var station = 0; station < ground.StationsOfTheSweep(car, fromM, toM); station++)
        {
            var count = UnderTheCar(car, step, fromM, toM, station, spareM, under, out var atM);
            var kept = 0;
            for (var at = 0; at < count; at++)
            {
                if (ground.IsCarriageway(under[at].Way)) under[kept++] = under[at];
            }

            if (!into.Station(car, atM, spanM, under[..kept])) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>Whether the ground a step would cover is road a step may be had on</b> (TER-4c.6): on the carriageway and clear
    /// of every zebra.
    /// </summary>
    [SkipLocalsInit]
    bool IsOnTheRoad(int car, in Overtake step, float fromM, float toM)
    {
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        for (var station = 0; station < ground.StationsOfTheSweep(car, fromM, toM); station++)
        {
            var count = UnderTheCar(car, step, fromM, toM, station, 0f, under, out _);
            for (var at = 0; at < count; at++)
            {
                ref readonly var cover = ref under[at];
                if (!ground.IsCarriageway(cover.Way) || ground.CrossesAZebra(cover.Way, cover.FromM, cover.ToM)) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <b>And whether it is free</b> (TER-4c.6): nobody standing on any of it but the car, and nobody planning it — so
    /// traffic coming up the lane beside, whose plan reaches over it, is let by first.
    /// </summary>
    bool IsUnheld(int car)
    {
        foreach (ref readonly var run in _asked.Of(car))
        {
            var held = ground.HeldAsABody(run.Cover);
            if (!Occupancy.IsFreeForAPass(
                    held.Way, run.FromM, run.ToM, held.FromM, held.ToM, car, LaneRoster.Driving, [], PassTerms.Plain))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <b>A step laid in this rebuild, kept or withdrawn</b> before the car moves across: kept where nothing but the car
    /// itself is on its ground, the one exception a pass asked for on the same tick by a holder numbered after it
    /// (<see cref="LaneOccupancy.KeepsItsPass"/>) — and only while the car can still come up to where it begins.
    /// </summary>
    void KeepOrWithdraw(int car, in Overtake step, float progressM)
    {
        var kept = progressM <= step.OutM;
        foreach (ref readonly var run in _swept.Of(car))
        {
            if (!kept) break;
            if (run.LastAtM < progressM) continue;

            var held = ground.HeldAsABody(run.Cover);
            kept = Occupancy.KeepsItsPass(
                held.Way, run.FromM, run.ToM, held.FromM, held.ToM, car, LaneRoster.Driving, PassTerms.Plain);
        }

        if (kept)
        {
            Cars.Pass[car] = step with { Begun = true };
            return;
        }

        Cars.Pass[car] = Overtake.None;
        Withdrawn++;
    }

    /// <summary>
    /// <b>The ways under the car at one station of its step</b>: its collider, stood where the step puts the rear axle at
    /// that metre of the line and pointed the way the step points it there — and grown by <paramref name="spareM"/> ahead
    /// and to either side.
    /// </summary>
    int UnderTheCar(
        int car, in Overtake step, float fromM, float toM, int station, float spareM, Span<WayCover> under, out float atM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        atM = MathF.Min(toM, fromM + (station * build.FlankM));
        step.PoseAtM(Cars.LineOf(car), atM, out var axleM, out var forward);

        var halfM = build.CollisionSizeM * 0.5f;
        var centreM = axleM + (forward * (build.CentreAheadOfAxleM + (spareM * 0.5f)));
        return ground.Atlas.UnderBox(centreM, forward, halfM.X + (spareM * 0.5f), halfM.Y + spareM, under, Cars.Level[car]);
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
    float AsideOnTheLaneBeside(int car)
    {
        var lane = Cars.LaneOf(car);
        var beside = lane == CarFleet.NoLane ? RoadGraph.NoLane : Roads.LaneBeside(lane, _inward[car]);
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
