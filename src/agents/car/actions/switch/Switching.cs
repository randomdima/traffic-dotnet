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
    internal const int MostRunsOfASwitch = 64;

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

    /// <summary>How many steps each car has asked for, and the ask the claims were last laid for — answered once the two agree.</summary>
    readonly long[] _askedAs = new long[ground.Cars.Capacity];

    /// <inheritdoc cref="_askedAs"/>
    readonly long[] _laidFor = new long[ground.Cars.Capacity];

    /// <summary>Which of each car's asks the claims every reader reads were laid for: kept with the layer they belong to.</summary>
    internal long[] LaidFor => _laidFor;

    bool LaidSinceAsked(int car) => _askedAs[car] != 0 && _laidFor[car] == _askedAs[car];

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

    /// <summary>For the instruments: how far across the lane this car means to move onto stood at its last decision.</summary>
    public float AsideM(int car) => _asideM[car];

    /// <summary>
    /// For the instruments: whether this car's line stops where its route moves across, so that it stands short of the
    /// line's end by its step from rest and the pass's spare (<see cref="WaitAtM"/>) — and that step.
    /// </summary>
    public bool WaitsShortOfTheEnd(int car, out float fromRestM)
    {
        fromRestM = _fromRestM[car];
        return TheLineStopsToMoveAcross(car, out _);
    }

    /// <summary>
    /// <b>Whether a car following its route means to move across, and which way</b> (CAR-53), in this order:
    /// <list type="number">
    /// <item><b>Where its line stops because its lanes no longer go the route's way</b> — toward the lane the route goes
    /// on from — <b>or because its lane ends</b> under a toured car, toward the nearest lane beside that carries on.</item>
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
        var shape = ShapeOfTheRoad(car, out inward);
        if ((shape & RoadShape.StopsToMoveAcross) != 0) return (shape & RoadShape.Wants) != 0;

        var heldUp = (shape & RoadShape.Asks) != 0 && IsHeldUp(car);
        var wants = (shape & (heldUp ? RoadShape.WantsWhenHeldUp : RoadShape.WantsWhenFree)) != 0;
        inward = heldUp && wants;
        return wants;
    }

    /// <summary>
    /// <b>What of <see cref="Wants"/> the road and the route alone answer</b> — the car's line, the lanes beside it and the
    /// route queued past it, which change only where a line or a route is laid — leaving only whether it is held up
    /// (<see cref="IsHeldUp"/>) to the claims.
    /// </summary>
    /// <param name="inward">Where the line stops to move across, which way.</param>
    public RoadShape ShapeOfTheRoad(int car, out bool inward)
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
    /// (<see cref="Take"/>): the swept ground of a step drawn is kept, and the ways it asks the layer about are marked asked,
    /// as a decision leaves them — nothing else is changed.
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
        if (!shaped || !Sweep(car, step, look.RoomToM)) return look;

        // A layer sent back in part may not hold the ground yet: asked at the next decision.
        if (!ground.Knows(_asked, car)) return look with { Outcome = SwitchOutcome.Unknown, LooksInS = 0f };

        return IsUnheld(car) ? look with { Outcome = SwitchOutcome.Asks, Step = step } : look;
    }

    /// <summary>A decision taken as <see cref="Consider"/> worked it out — by the town, or by a device that looked for it.</summary>
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
    /// <b>A decision a device worked out as <see cref="Consider"/> would</b>, taken — the ways its look asked the layer about
    /// marked asked first, as the look marks them (<see cref="DrivingGround.Knows"/>), so the layer goes on holding them.
    /// </summary>
    public void TakeTheLook(int car, in SwitchLook look, ReadOnlySpan<uint> askedAbout)
    {
        foreach (var way in askedAbout) Occupancy.Knows((int)way);
        Take(car, look);
    }

    /// <summary>Which way each car means to move across, as its last decision left it — for a device that looks for it.</summary>
    public bool Inward(int car) => _inward[car];

    /// <summary>Where along its line a car's last look had its step's ground end, and how far ahead its step from rest is — for a device that looks for it.</summary>
    public (float FromRestM, float RoomToM) LastLookOf(int car) => (_fromRestM[car], _roomToM[car]);

    /// <summary>How long until a car meaning to move across looks for the room again, as its last decision left it.</summary>
    public float LooksInS(int car) => _looksInS[car];

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
                SteppedOut(ref town, car, progressM);
                town.DriveOnTheLine(car, pose, Cars.ProgressM[car], alongMps, 0f);
                return;
            }

            town.DriveOnTheLine(car, pose, progressM, alongMps, coveredM);
            return;
        }

        if (pass.Any && LaidSinceAsked(car)) KeepOrWithdraw(car, pass, progressM);

        town.DriveOnTheLine(car, pose, progressM, alongMps, coveredM, passAsideM: _asideM[car], waitAtM: WaitAtM(car, progressM));
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

        _laidFor[car] = _askedAs[car];
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
    public bool TheBodyInTheSwitch(int car, out float inTheWayM, out LaneClaim body, out int on) =>
        Occupancy.FirstBodyIn(_asks.AsSpan(0, AsksOfTheSwitch(car, _asks)), car, out inTheWayM, out body, out on);

    /// <summary>Where <see cref="TheBodyInTheSwitch"/> looks, in the order it looks: each run of the step not yet driven past.</summary>
    /// <param name="into">Room for <see cref="MostAsksOfASwitch"/>.</param>
    public int AsksOfTheSwitch(int car, Span<BodyAsk> into)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var progressM = Cars.ProgressM[car];
        var written = 0;
        foreach (ref readonly var run in _swept.Of(car))
        {
            if (run.LastAtM < progressM) continue;

            var inTheWayM = MathF.Max(progressM, run.FirstAtM - build.FlankM) + build.NoseAheadOfAxleM;
            into[written++] = new BodyAsk(run.Way, run.FromM, run.ToM, OnFoot: false, inTheWayM, FromTheBody: false);
        }

        return written;
    }

    /// <summary>How many stretches one step is looked over for a body (<see cref="AsksOfTheSwitch"/>): every run of it.</summary>
    public const int MostAsksOfASwitch = MostRunsOfASwitch;

    /// <summary>Room for one step's <see cref="AsksOfTheSwitch"/>.</summary>
    readonly BodyAsk[] _asks = new BodyAsk[MostAsksOfASwitch];

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

        var corneringMps2 = CarFollower.CorneringMps2(Config, build);
        var brakingMps2 = CarFollower.BrakingMps2(Config, build);
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
            var roomM = (outMps * Config.CarReactionS) + DrivingGround.StoppingM(outMps, brakingMps2) + Config.Driving.StandOffM;
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
            return IsOnTheRoad(car, step, progressM, roomToM);
        }
    }

    /// <summary>
    /// <b>A step's ground, swept once</b> from where the car stands to <paramref name="toM"/>, the end of the room past it:
    /// what its body covers, for laying and keeping, and the same grown by the pass's spare and run on by the stand-off, for
    /// asking. False where it does not fit in the runs a step may have.
    /// </summary>
    [SkipLocalsInit]
    bool Sweep(int car, in Overtake step, float toM)
    {
        var spanM = Cars.BuildOf(car).LengthM;
        var fromM = Cars.ProgressM[car];
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
    /// <b>A step laid since it was asked for, kept or withdrawn, for a car whose tick runs elsewhere</b> (a device's): what
    /// the car's own <see cref="Tick{TTown}"/> answers before it drives, at where the last tick left it on its line.
    /// </summary>
    public void KeepOrWithdrawTheStep(int car)
    {
        var pass = Cars.Pass[car];
        if (pass.Any && !pass.Begun && LaidSinceAsked(car)) KeepOrWithdraw(car, pass, Cars.ProgressM[car]);
    }

    /// <summary>
    /// <b>A step laid in this rebuild, kept or withdrawn</b> before the car moves across: kept where nothing but the car
    /// itself is on its ground, the one exception a pass asked for on the same tick by a holder numbered after it
    /// (<see cref="LaneOccupancy.KeepsItsPass"/>) — and only while the car can still come up to where it begins.
    /// </summary>
    void KeepOrWithdraw(int car, in Overtake step, float progressM)
    {
        // A layer sent back in part may not hold the ground yet: answered on a later tick.
        if (!ground.Knows(_swept, car)) return;

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
        return ground.Atlas.UnderBox(centreM, forward, halfM.X + (spareM * 0.5f), halfM.Y + spareM, under, Cars.Channels[car]);
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
