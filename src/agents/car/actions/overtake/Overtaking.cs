using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using static TrafficSimulation.World.Road.LineWays;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>Overtake</b> (CAR-46, TER-4c.6): a car getting past what stands in its lane — decided once, where it would begin
/// slowing for it, and committed to: its shape and its ground worked out then and never again. Waited for at the place
/// its step out begins, asked for whole on its own clock (<see cref="DrivingFigures.PassAskEveryS"/>), laid as a plan of
/// its own over all the ground it will cover, kept or withdrawn once before the car moves over, and driven as the car's
/// own line aimed across into the lane beside. Over once the car is back in its own lane; let go once what it was
/// getting past is gone, or once it has waited its patience out (<see cref="DrivingFigures.PassPatienceS"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing decides a pass</b> (<see cref="DrawThePass"/>): whether what cut the grant is at rest and going nowhere
/// is what that body is doing, and the claims say only where it is (TER-4c.5). Everything past the decision is here,
/// for the reading that will make it.
/// </para>
/// <para>
/// <b>Nothing here reads another agent</b> (TER-4c.5). What is in the way is a body claim, told apart by where it
/// stands; whether the ground of the pass is free is what is laid on the ways under it.
/// </para>
/// <para>
/// <b>Its ground is the two lanes it runs down</b> (<see cref="PassGround"/>): its own line and the lane running back
/// beside it, from where its step out begins to where it is back in its lane — the lane back asked down its own metres,
/// which run against the car.
/// </para>
/// <para>
/// <b>A pass is never given up once the car has moved over</b>: it cannot safely be, so it is asked for only
/// over ground nobody holds, and once begun it holds that ground at <see cref="ClaimPriority.Committed"/>.
/// </para>
/// <para>
/// <b>A car on a call asks at its call's rung</b> (AMB-4.4), and the paint of a zebra under its pass is claimed whole —
/// a body already on it is waited for short of the paint.
/// </para>
/// <para>
/// <b>Too near to step out, it backs up for the room</b> (CAR-50, <see cref="BackingUp"/>) and comes back to this
/// once it has it.
/// </para>
/// </remarks>
internal sealed class Overtaking(DrivingGround ground, CarActions actions)
{
    /// <summary>
    /// How many bodies one pass may get past at once. A bound on a stack span and not a figure behaviour
    /// reads: past the cars a line laid its sight ahead holds nose to tail, so what ends a long pass — a call's
    /// past a queue at a red — is the line and the ground and never this.
    /// </summary>
    const int MostPassedAtOnce = 24;

    /// <summary>How many times where a step back begins and what it is drawn for are read against each other.</summary>
    const int StepBackReadings = 3;

    /// <summary>
    /// The two shapes a car decides its pass in, both at once: drawn to step out from rest — where it waits and what it
    /// backs up for — and drawn for the pace it came up at, which is had only while the car can still come up to where
    /// that one begins at a pace it may be driven at.
    /// </summary>
    const int FromRest = 0, AtSpeed = 1, Shapes = 2;

    /// <summary>Each car's decided shapes, committed to while it waits for its pass, backs up for it and drives it.</summary>
    readonly Overtake[] _shapes = new Overtake[ground.Cars.Capacity * Shapes];

    /// <summary>
    /// The bodies each decided shape gets past, where they stood — in the room the car holds for its pass
    /// (<see cref="CarActions.PassRooms"/>, <see cref="Table"/>).
    /// </summary>
    readonly BodySeen[] _passed = new BodySeen[actions.PassRooms.Count * Shapes * MostPassedAtOnce];

    readonly int[] _passedCount = new int[actions.PassRooms.Count * Shapes];

    /// <summary>The hold each car's pass was asked in this rebuild, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    readonly int[] _holds = LaneOccupancy.NoHolds(ground.Cars.Capacity);

    /// <summary>Which of its shapes a car asked for — what is laid, kept and driven while it has a pass.</summary>
    readonly int[] _asking = new int[ground.Cars.Capacity];

    /// <summary>How long until a car waiting on its pass looks round again (<see cref="DrivingFigures.PassAskEveryS"/>).</summary>
    readonly float[] _looksInS = new float[ground.Cars.Capacity];

    /// <summary>How long a car has waited on the pass it decided (<see cref="DrivingFigures.PassPatienceS"/>).</summary>
    readonly float[] _waitedS = new float[ground.Cars.Capacity];

    /// <summary>How long until a car refused a pass by the road decides on one again.</summary>
    readonly float[] _decidesInS = new float[ground.Cars.Capacity];

    /// <summary>Whether a car backing up for its room has a look round its next tick is to take (<see cref="TakeTheLook"/>).</summary>
    readonly bool[] _looks = new bool[ground.Cars.Capacity];

    /// <summary>
    /// How many passes each car has asked for, and the ask the claims were last laid for — answered, kept or withdrawn,
    /// once the two agree and not before: the claims are laid once a decision interval.
    /// </summary>
    readonly long[] _askedAs = new long[ground.Cars.Capacity];

    /// <inheritdoc cref="_askedAs"/>
    readonly long[] _laidFor = new long[ground.Cars.Capacity];

    bool LaidSinceAsked(int car) => _askedAs[car] != 0 && _laidFor[car] == _askedAs[car];

    CarFleet Cars => ground.Cars;

    LaneOccupancy Occupancy => ground.Occupancy;

    RoadGraph Roads => ground.Roads;

    SimConfig Config => ground.Config;

    /// <summary>Passes asked for since the town was laid (CAR-46).</summary>
    public long Asked { get; private set; }

    /// <summary>And withdrawn in the rebuild after, because a body or another pass had the ground by then.</summary>
    public long Withdrawn { get; private set; }

    /// <summary>And driven to the end, the car back in its own lane.</summary>
    public long Made { get; private set; }

    /// <summary>
    /// <b>This tick of a car getting past something</b>, once the line under it has been read: driven down its pass and
    /// over once it is back in its lane, or waiting for it — begun or withdrawn in the tick after it was asked for.
    /// Everything else about the pass is the car's decision (<see cref="Decide"/>).
    /// </summary>
    /// <remarks>
    /// <b>The pass ends in the tick the car drives past its end</b>, as a manoeuvre's piece does: past there it is ground
    /// the pass never held, and a car still on a pass there is one standing on ground it has not claimed (TER-4c.8).
    /// </remarks>
    public void Tick<TTown>(ref TTown town, int car, in CarPose pose, float progressM, float alongMps)
        where TTown : struct, ICarTown
    {
        var pass = Cars.Pass[car];
        if (pass.Begun)
        {
            if (progressM >= pass.EndsM) DrivenToItsEnd(car);

            town.DriveOnTheLine(car, pose, progressM, alongMps);
            return;
        }

        // Asked for and laid since: kept, or withdrawn and asked for again when the car next looks.
        if (pass.Any && LaidSinceAsked(car)) KeepOrWithdrawThePass(car, pass);

        WaitAtTheStep(ref town, car, pose, progressM, alongMps);
    }

    /// <summary>A pass driven to its end: made, and the car following again.</summary>
    void DrivenToItsEnd(int car)
    {
        Made++;
        actions.Enter(car, CarAction.Follow);
    }

    /// <summary>
    /// <b>A car waiting for its pass, on its own clock</b>: looked round every <see cref="DrivingFigures.PassAskEveryS"/>
    /// — asked for, backed up for where the car stands past where its step out begins, or let go. A car backing up for
    /// the room is looked round for on the same clock, and the look is read by its next tick (<see cref="TakeTheLook"/>).
    /// </summary>
    /// <param name="sinceLastDecisionS">How much of the town's time this decision answers for, which its clocks run by.</param>
    public void Decide(int car, float sinceLastDecisionS)
    {
        if (Cars.Action[car] == CarAction.BackUp)
        {
            _looks[car] |= IsTimeToLook(car, sinceLastDecisionS);
            return;
        }

        if (Cars.Pass[car].Any) return;

        _waitedS[car] += sinceLastDecisionS;
        if (IsTimeToLook(car, sinceLastDecisionS) && !LookRound(car, Cars.ProgressM[car], Cars.AlongMps[car]))
        {
            actions.Enter(car, CarAction.Follow);
        }
    }

    /// <summary>Whether the car's clock came round for a look since its last tick read it — once.</summary>
    public bool TakeTheLook(int car)
    {
        var looks = _looks[car];
        _looks[car] = false;
        return looks;
    }

    /// <summary>
    /// <b>A car waiting on its pass driven down its line</b>, towards the place its step out from rest begins and no
    /// further (<see cref="DriveContext.WaitAtM"/>) until it has the pass — and indicating towards the lane beside
    /// (CAR-14.7). <b>A car already past that place is driven on its grant alone</b>: stopped short of where it is, it
    /// would stand in the road for a place it has left behind, and come to rest too near it backs up (CAR-50).
    /// </summary>
    public void WaitAtTheStep<TTown>(
        ref TTown town, int car, in CarPose pose, float progressM, float alongMps, float backUpM = 0f,
        bool blocked = false)
        where TTown : struct, ICarTown
    {
        var fromRest = _shapes[Holder(car, FromRest)];
        var waits = !Cars.Pass[car].Begun;
        var toTheStepM = fromRest.OutM - progressM;
        town.DriveOnTheLine(
            car, pose, progressM, alongMps, waits, fromRest.AsideM, backUpM, blocked,
            waits && toTheStepM >= 0f ? toTheStepM : float.PositiveInfinity);
    }

    /// <summary>
    /// <b>A waiting car's look round, once its clock comes round</b>: let go past its patience or where what it passes
    /// has gone; its pass drawn for the pace it came up at asked for while it can still come up to it, and the one from
    /// rest after that; and backing up for the room where it has come to rest past where that one begins (CAR-50).
    /// False where it is let go.
    /// </summary>
    bool LookRound(int car, float progressM, float alongMps)
    {
        if (_waitedS[car] > Config.Driving.PassPatienceS) return false;
        if (!IsStillThere(car)) return false;

        var atSpeed = Holder(car, AtSpeed);
        if (_shapes[atSpeed].Any && CanComeUpTo(car, _shapes[atSpeed], progressM, alongMps))
        {
            Ask(car, AtSpeed, progressM);
            return true;
        }

        if (IsTooNear(car, progressM))
        {
            if (BacksUp(car, progressM, alongMps)) actions.Enter(car, CarAction.BackUp);
            return true;
        }

        if (CanComeUpTo(car, _shapes[Holder(car, FromRest)], progressM, alongMps)) Ask(car, FromRest, progressM);
        return true;
    }

    /// <summary>
    /// <b>Whether a car backs up for the room to step out</b> (CAR-50): at rest past where its step out from rest
    /// begins, with nothing but its nearness refusing the pass.
    /// </summary>
    public bool BacksUp(int car, float progressM, float alongMps) =>
        IsTooNear(car, progressM) && alongMps <= Config.Driving.StopSpeedMps && !IsTheRoomHeld(car);

    /// <summary>
    /// <b>Whether the room past what the car passes is held</b> by a body it has not decided to get past (CAR-50): it
    /// comes back there once that has gone, and until then it waits where it is rather than backing up for a pass it
    /// could not have anyway.
    /// </summary>
    public bool IsTheRoomHeld(int car)
    {
        ref readonly var shape = ref _shapes[Holder(car, FromRest)];
        var reachM = shape.EndsM + Cars.BuildOf(car).NoseAheadOfAxleM + Config.Driving.StandOffM;
        Span<LineWay> ways = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        var count = ground.WaysAlong(car, shape.ClearsM, reachM, ways);
        var clearedM = shape.ClearsM;
        return TheNextBodyPast(car, ways[..count], shape.ClearsM, reachM, Passed(Table(car, FromRest)), ref clearedM);
    }

    /// <summary>One of the car's shapes asked for, where all its ground is free — laid in the rebuild after.</summary>
    void Ask(int car, int shape, float progressM)
    {
        if (!IsThePassUnheld(car, shape, progressM)) return;

        Cars.Pass[car] = _shapes[Holder(car, shape)];
        _asking[car] = shape;
        _askedAs[car]++;
        Asked++;
    }

    /// <summary>
    /// <b>Whether a car can still come up to where a shape of its pass begins at a pace that shape may be driven at</b>
    /// (<see cref="Overtake.OutMps"/>): short of it, and far enough short to slow to that pace by there.
    /// </summary>
    bool CanComeUpTo(int car, in Overtake shape, float progressM, float alongMps)
    {
        var toM = shape.OutM + Config.Driving.PassSpareM - progressM;
        if (toM < 0f) return false;

        var speedMps = MathF.Max(0f, alongMps);
        if (speedMps <= shape.OutMps) return true;

        ref readonly var build = ref Cars.BuildOf(car);
        return CarFollower.SlowsOverM(build, speedMps, shape.OutMps) <= toM;
    }

    /// <summary>Which of the decided shapes one of a car's shapes is (<see cref="_shapes"/>).</summary>
    static int Holder(int car, int shape) => (car * Shapes) + shape;

    /// <summary>
    /// Which holder of the passed table one of a car's shapes is: in the room the car holds for its pass, which it takes
    /// when it draws one and gives back when it leaves the overtake (<see cref="CarActions.PassRooms"/>).
    /// </summary>
    int Table(int car, int shape) => (actions.PassRooms.RoomOf(car) * Shapes) + shape;

    /// <summary>
    /// <b>Whether a car waiting on its pass looks round at this decision</b> — every
    /// <see cref="DrivingFigures.PassAskEveryS"/> from when it decided, whichever of its pass's actions it is in.
    /// </summary>
    bool IsTimeToLook(int car, float sinceLastDecisionS)
    {
        ref var inS = ref _looksInS[car];
        inS -= sinceLastDecisionS;
        if (inS > 0f) return false;

        inS = Config.Driving.PassAskEveryS;
        return true;
    }

    /// <summary>The car looks round at its next decision — one that has just made the room it backed up for.</summary>
    public void LookNow(int car) => _looksInS[car] = 0f;

    /// <summary>
    /// <b>Whether what the car decided to get past is still there to be got past</b>: a body on the way each was met on
    /// still standing where it stood to within the pass's spare — which is where the pass was drawn round it.
    /// </summary>
    public bool IsStillThere(int car)
    {
        var spareM = Config.Driving.PassSpareM;
        foreach (ref readonly var was in Passed(Table(car, FromRest)))
        {
            if (!Occupancy.FirstBodySeen(was.Way, was.FromM, was.ToM, car, LaneRoster.Driving, out var body)
                || MathF.Abs(body.FromM - was.FromM) > spareM || MathF.Abs(body.ToM - was.ToM) > spareM)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the car stands past where its decided step out begins by more than the pass's spare — too near what it
    /// passes to step out, and so backing up for the room (CAR-50).
    /// </summary>
    public bool IsTooNear(int car, float progressM) => progressM > StepsOutAtM(car) + Config.Driving.PassSpareM;

    /// <summary>Where along the line the car's step out from rest begins — where it waits, and what it backs up for.</summary>
    public float StepsOutAtM(int car) => _shapes[Holder(car, FromRest)].OutM;

    /// <summary>How far across the lane beside the car's decided pass takes it — what it indicates towards (CAR-14.7).</summary>
    public float AsideOfItsPassM(int car) => _shapes[Holder(car, FromRest)].AsideM;

    /// <summary>
    /// Whether the car is waiting on or driving a pass decided along another lane than the one it is on — which a line
    /// laid again from elsewhere leaves it, and which is over.
    /// </summary>
    public bool IsOffTheLaneOfItsPass(int car) =>
        Cars.Action[car] is CarAction.Overtake or CarAction.BackUp && Cars.LaneOf(car) != _shapes[Holder(car, FromRest)].Lane;

    /// <summary>
    /// <b>The pass measured again from the next lane of the chain</b>, which begins <paramref name="shiftM"/> along the
    /// line — what a line shifted on by a lane leaves it — the decided shapes, their ground and whatever was asked.
    /// </summary>
    public void ShiftTheLine(int car, int lane, float shiftM)
    {
        if (Cars.Pass[car].Any) Cars.Pass[car] = Cars.Pass[car].From(lane, shiftM);
        if (Cars.Action[car] is not (CarAction.Overtake or CarAction.BackUp)) return;

        for (var shape = 0; shape < Shapes; shape++)
        {
            ref var decided = ref _shapes[Holder(car, shape)];
            if (!decided.Any) continue;

            decided = decided.From(lane, shiftM);
        }
    }

    /// <summary>
    /// <b>The ground this car's pass will cover, asked as a plan of its own</b> (TER-4c.6): the car's own lane up to where
    /// its step out begins, where it has not come up to it yet, and what of the pass's two lanes it has not yet driven
    /// past (<see cref="PassGround"/>) — at <see cref="ClaimPriority.Committed"/> once begun, and at the car's own rung
    /// before. A pass the car is no longer on its line for, or no longer on the lane of, is given up for following.
    /// </summary>
    /// <remarks>
    /// <b>Once begun, whatever refuses it is where the car stops</b>, and the car's plan carries on from this hold
    /// (<see cref="HoldOf"/>).
    /// </remarks>
    /// <param name="underWay">Whether the car is on the route's line, as the town reads it.</param>
    public void Lay(int car, bool underWay)
    {
        Occupancy.Release(_holds[car]);
        _holds[car] = LaneOccupancy.NoHold;
        var pass = Cars.Pass[car];
        if (Cars.Action[car] != CarAction.Overtake || !pass.Any) return;

        if (!underWay || Cars.LaneOf(car) != pass.Lane)
        {
            actions.Enter(car, CarAction.Follow);
            return;
        }

        var progressM = Cars.ProgressM[car];
        var rung = pass.Begun ? ClaimPriority.Committed : Cars.BlueLight[car] ? ClaimPriority.Special : ClaimPriority.Firm;
        var hold = _holds[car] = Occupancy.BeginHold(car, LaneRoster.Driving, whole: !pass.Begun);
        Span<LineWay> approach = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        foreach (ref readonly var way in TheApproach(car, pass, progressM, approach))
        {
            Occupancy.Ask(hold, way.Way, way.FromM, way.ToM, way.LineFromM, rung);
        }

        // The paint of a zebra the pass crosses is held whole, the car stopping short of it where somebody is on it.
        var stopM = ground.StopsShortOfM(car, MathF.Max(progressM, pass.OutM));
        Span<LineWay> own = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        var count = PassGround(car, pass, progressM, pass.EndsM, 0f, own, out var back);
        foreach (ref readonly var way in own[..count])
        {
            Occupancy.Ask(hold, way.Way, way.FromM, way.ToM, way.LineFromM, rung);
            if (ground.CrossesAZebra(way.Way, way.FromM, way.ToM)) AskThePaintItCrosses(hold, way, stopM, rung);
        }

        if (back.ToM > back.FromM)
        {
            Occupancy.AskDown(hold, back.Way, back.FromM, back.ToM, back.LineFromM, rung);
            if (ground.CrossesAZebra(back.Way, back.FromM, back.ToM)) AskThePaintItCrosses(hold, back, stopM, rung);
        }

        _laidFor[car] = _askedAs[car];
    }

    /// <summary>
    /// <b>A pass's ground, as the two lanes it runs down</b> (TER-4c.6): the ways of its own line from where its step out
    /// begins, or the car's tail past there, to where it is back in its lane by <paramref name="toLineM"/> — and the stretch
    /// of the lane running back beside the line's first lane over the same metres (<paramref name="back"/>, its
    /// <see cref="LineWay.LineFromM"/> where its far end falls on the line) — each grown by <paramref name="spareM"/>.
    /// </summary>
    /// <remarks>
    /// <b>The lane back is read in step with the car's</b>, the two running the same stretch the other way round: a metre
    /// of the one is the same share of the other's length, counted from its far end. A pass is drawn inside the line's
    /// first lane.
    /// </remarks>
    int PassGround(
        int car, in Overtake pass, float progressM, float toLineM, float spareM, Span<LineWay> own, out LineWay back)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var fromLineM = MathF.Max(pass.OutM, progressM) - build.TailBehindAxleM - spareM;
        var endLineM = toLineM + build.NoseAheadOfAxleM + spareM;
        var count = ground.WaysAlong(car, fromLineM, endLineM, own);

        back = default;
        var lane = pass.Lane;
        var reverse = Roads.LaneReverse[lane];
        if (reverse < 0) return count;

        var startM = Cars.LaneStartsOf(car)[0];
        var lengthM = Roads.LaneLengthM[lane];
        var share = Roads.LaneLengthM[reverse] / lengthM;
        var laneFromM = Math.Clamp(fromLineM - startM, 0f, lengthM);
        var laneToM = Math.Clamp(endLineM - startM, 0f, lengthM);
        back = new LineWay(
            ground.Ways.OfRoadLane(reverse), (lengthM - laneToM) * share, (lengthM - laneFromM) * share, startM + laneFromM);
        return count;
    }

    /// <summary>The hold this car's pass was asked in this rebuild, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    public int HoldOf(int car) => _holds[car];

    /// <summary>
    /// <b>The car's own lane under it and on to where its body stands at the first station of a shape of its pass</b> —
    /// the ground it asks for with that shape while it has still to come up to where the step out begins, so every
    /// metre it drives from asking to the end of the pass is the pass's. Empty once it has come up to it.
    /// </summary>
    Span<LineWay> TheApproach(int car, in Overtake shape, float progressM, Span<LineWay> into)
    {
        if (progressM >= shape.OutM) return default;

        ref readonly var build = ref Cars.BuildOf(car);
        return into[..ground.WaysAlong(car, progressM - build.TailBehindAxleM, shape.OutM + build.NoseAheadOfAxleM, into)];
    }

    /// <summary>
    /// <b>The paint of every zebra a stretch of a pass crosses, laid whole</b> (TER-4c.6, TER-5c.3): kerb to kerb
    /// on each of its walking lanes, as a car's plan over any of it holds all of it — so nobody on foot steps onto
    /// it in front of the pass, and the pass stops short of anybody already on it. Only a call's pass is ever over one.
    /// </summary>
    void AskThePaintItCrosses(int hold, in LineWay stretch, float stopM, ClaimPriority rung)
    {
        foreach (ref readonly var mark in Occupancy.Marks.Of(stretch.Way))
        {
            if (mark.MineFromM >= stretch.ToM) break;
            if (mark.MineToM <= stretch.FromM || ground.Lines.ZebraOf(mark.OnWay) == RibbonMarks.NoZebra) continue;

            Occupancy.AskSwept(hold, mark.OnWay, mark.FromM, mark.ToM, stopM, rung);
        }
    }

    /// <summary>
    /// Whether the car could pass anything at all where it is: on the route's own line driven forwards, in a
    /// lane that has a lane running back beside it, across a line that is crossed to pass (CAR-6.2b) — a road of
    /// more than a lane each way is got past on over a lane of the car's own way (CAR-53).
    /// </summary>
    bool HasALaneToPassOn(int car)
    {
        if (Cars.Line[car].LaneCount == 0 || Cars.LineIsReverse[car]) return false;

        var lane = Cars.LaneOf(car);
        return lane != CarFleet.NoLane && IsALaneToPassFrom(Roads, lane);
    }

    /// <summary>Whether a car on this lane has a lane running back beside it, across a line that is crossed to pass.</summary>
    public static bool IsALaneToPassFrom(RoadGraph roads, int lane) =>
        roads.LaneReverse[lane] >= 0 && !roads.LaneOverOneLine[lane] && roads.LaneCrossesToPass[lane];

    /// <summary>
    /// <b>A pass laid in this rebuild, kept or withdrawn</b> before the car moves over: kept where the whole of its ground
    /// was had (<see cref="DrivingGround.IsKept"/>).
    /// </summary>
    void KeepOrWithdrawThePass(int car, in Overtake pass)
    {
        if (ground.IsKept(_holds[car]))
        {
            Cars.Pass[car] = pass with { Begun = true };
            return;
        }

        Cars.Pass[car] = Overtake.None;
        Withdrawn++;
    }

    /// <summary>
    /// <b>Whether the car has come to where it would begin slowing for what cut its grant</b> (CAR-46) — the place it
    /// has to choose between stepping out and slowing down, and so where a pass is decided on.
    /// </summary>
    /// <param name="toTheStopM">How far ahead of the nose the car's grant has it stop.</param>
    public bool ComesUpTo(int car, float alongMps, float toTheStopM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var speedMps = MathF.Max(0f, alongMps);
        var slowsFromM = DrivingGround.StoppingM(speedMps, build.PlannedBrakingMps2 * Config.Driving.WaitingToPassBrakingShare)
                         + (speedMps * build.RestingLeadS) + Config.Driving.PassSpareM;
        return toTheStopM <= slowsFromM;
    }

    /// <summary>
    /// <b>A pass decided on, once</b> (CAR-46): where the grant was ended on the car's own line by a body at
    /// <paramref name="cutAtM"/> of <paramref name="cutOn"/> it means to get past, drawn, swept and committed to — what
    /// the car then waits for, backs up for and drives, and never draws again. False where the road refuses it, which is
    /// not asked again before the car would next look round (<see cref="DrivingFigures.PassAskEveryS"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>In two shapes, both drawn now</b> (<see cref="Shape"/>): to step out from rest, where the car waits for its
    /// pass — and, for a car still rolling, for the pace it is doing, which it has only if it gets the pass while it can
    /// still come up to where that one begins. Nothing is drawn again after this.
    /// </para>
    /// <para>
    /// <b>Not decided short of a place in the road it was sent to</b> (AMB-5, EVA-3, SRV-6, CTL-8a): what stands
    /// before it there is what it was sent to, and a pass ending past the place would drive it by.
    /// </para>
    /// <para>
    /// <b>Nothing calls it</b>: which body is one to get past is what the body is doing, which the claims do not say
    /// (TER-4c.5) — <c>docs/tech-debt.md</c>.
    /// </para>
    /// </remarks>
    /// <param name="sinceLastDecisionS">How much of the town's time this decision answers for, which its clock runs by.</param>
    /// <param name="toTheSceneM">How far ahead along the line the place the car was sent to stands, or infinity.</param>
    public bool DrawThePass(
        int car, float sinceLastDecisionS, float progressM, float alongMps, float toTheSceneM, int cutOn, float cutAtM)
    {
        if (!HasALaneToPassOn(car)) return false;

        ref var inS = ref _decidesInS[car];
        inS -= sinceLastDecisionS;
        if (inS > 0f) return false;

        // Refused, it is not drawn again before the car would next look round — and the room it was drawn in is given
        // back, since a car following holds none.
        inS = Config.Driving.PassAskEveryS;
        if (!actions.PassRooms.Take(car, out _)) return false;

        var speedMps = MathF.Max(0f, alongMps);
        if (!(speedMps > Config.Driving.StopSpeedMps
              && Shape(car, AtSpeed, progressM, speedMps, cutOn, cutAtM, out var shape)
              && CanComeUpTo(car, shape, progressM, speedMps)
              && Commit(car, AtSpeed, shape, progressM + toTheSceneM)))
        {
            _shapes[Holder(car, AtSpeed)] = Overtake.None;
        }

        if (!Shape(car, FromRest, progressM, 0f, cutOn, cutAtM, out shape) || !Commit(car, FromRest, shape, progressM + toTheSceneM))
        {
            actions.PassRooms.Free(car);
            return false;
        }

        inS = 0f;
        _looksInS[car] = 0f;
        _waitedS[car] = 0f;
        return true;
    }

    /// <summary>
    /// <b>A drawn shape committed to</b>: its ground on the road a pass may be had on, ending short of the place the
    /// car was sent to, and swept. False where it is not.
    /// </summary>
    /// <param name="drawnAs">Which of the car's shapes it is (<see cref="FromRest"/>, <see cref="AtSpeed"/>).</param>
    bool Commit(int car, int drawnAs, in Overtake shape, float sceneAtM)
    {
        if (sceneAtM < shape.EndsM || !IsThePassOnTheRoad(car, shape, shape.EndsM + Config.Driving.StandOffM)) return false;

        _shapes[Holder(car, drawnAs)] = shape;
        return true;
    }

    /// <summary>
    /// <b>A pass drawn</b> for a car doing <paramref name="speedMps"/>: its step out begun at the last place its body
    /// clears what it passes (<see cref="StepOutLeadM"/>), with the pass's spare, the room past what it passes found,
    /// and every body it passes on the way there kept for the car to look for again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it makes for is the room past what it passes</b>: the first stretch of its own line past it long
    /// enough to come back into — the step back, and the car's own length and stand-off past that. <b>Anything
    /// standing there</b> is where the car comes back once that has gone: it has decided, and waits.
    /// </para>
    /// <para>
    /// <b>The step back begins as soon past what it passes as the body can come back</b>
    /// (<see cref="StepBackTrailM"/>), drawn for the pace the car has picked up by then (<see cref="StepBack"/>): the
    /// pass is laid as though the car pulls away along it, so it never slows for its own pass and is off the lane
    /// beside as soon as that pace takes it.
    /// </para>
    /// </remarks>
    /// <param name="drawnAs">The car's shape this is drawn as, which its bodies passed are kept under.</param>
    bool Shape(int car, int drawnAs, float progressM, float speedMps, int cutOn, float cutAtM, out Overtake pass)
    {
        var holder = Table(car, drawnAs);
        pass = Overtake.None;
        ref readonly var build = ref Cars.BuildOf(car);
        var lane = Cars.LaneOf(car);
        var back = Roads.LaneReverse[lane];
        var noseM = progressM + build.NoseAheadOfAxleM;
        var lineM = Cars.Line[car].LengthM;
        Span<LineWay> ways = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        var count = ground.WaysAlong(car, noseM, lineM, ways);
        if (!OnTheLine(ways[..count], cutOn, cutAtM, out var standsFromM)
            || !Occupancy.FirstBodySeen(cutOn, cutAtM, Occupancy.WayLengthM(cutOn), car, LaneRoster.Driving, out var cutBy))
        {
            return false;
        }

        var asideM = AsideOnTheLaneBeside(lane, back, Math.Clamp(progressM, 0f, Roads.LaneLengthM[lane]));

        var passed = _passed.AsSpan(holder * MostPassedAtOnce, MostPassedAtOnce);
        ref var passedCount = ref _passedCount[holder];
        passedCount = 0;
        var clearsM = noseM;
        if (!Passes(ways[..count], cutBy, passed, ref passedCount, ref clearsM)) return false;

        var corneringMps2 = build.PlannedCorneringMps2;
        var bandM = Roads.LaneWidthM[lane] * 0.5f;
        var lineBend = 0f;
        while (true)
        {
            if (!CarFollower.ShapeAPass(build, speedMps, asideM, corneringMps2, lineBend, out var stepM, out var fromMps)) return false;

            var leadM = StepOutLeadM(build, asideM, stepM, bandM);
            if (float.IsPositiveInfinity(leadM)) return false;

            var outM = standsFromM - leadM - Config.Driving.PassSpareM;
            if (!StepOutAndBack(
                    car, outM, stepM, fromMps, asideM, corneringMps2, lineBend, bandM, clearsM, out var outStepM,
                    out var outMps, out var backM, out var backStepM, out var backMps))
            {
                return false;
            }

            // Standing in the room it comes back into: where the car comes back once that has gone. What it already passes
            // reaching on further over another of its ways moves the room on with it.
            var reachM = backM + backStepM + build.NoseAheadOfAxleM + Config.Driving.StandOffM;
            var clearedM = clearsM;
            TheNextBodyPast(car, ways[..count], clearsM, reachM, passed[..passedCount], ref clearedM);
            if (clearedM > clearsM)
            {
                clearsM = clearedM;
                continue;
            }

            if (reachM > lineM) return false;

            var bend = MostBendUnderThePass(car, outM, reachM, asideM);
            if (float.IsPositiveInfinity(bend)) return false;

            if (bend > lineBend)
            {
                lineBend = bend;
                continue;
            }

            pass = new Overtake(lane, outM, outStepM, backM, backStepM, asideM, outMps, backMps, clearsM, Begun: false);
            return true;
        }
    }

    /// <summary>The bodies one of a car's shapes gets past, where each stood.</summary>
    ReadOnlySpan<BodySeen> Passed(int holder) => _passed.AsSpan(holder * MostPassedAtOnce, _passedCount[holder]);

    /// <summary>
    /// <b>A pass's two steps</b>: out from where it begins over <paramref name="stepM"/>, driven no faster than
    /// that length allows, and back as <see cref="StepBack"/> draws it for the pace the car has at the end of the
    /// step out. False where the line under them bends as tight as the lock.
    /// </summary>
    /// <param name="fromMps">The pace the shortest step out is drawn for, which no step out is driven below.</param>
    bool StepOutAndBack(
        int car, float progressM, float stepM, float fromMps, float asideM, float corneringMps2, float lineBend,
        float bandM, float clearsM, out float outStepM, out float outMps, out float backM, out float backStepM,
        out float backMps)
    {
        outStepM = stepM;
        outMps = MathF.Max(fromMps, CarFollower.StepMps(Cars.BuildOf(car), stepM, asideM, corneringMps2, lineBend));
        var steppedOutMps = MathF.Min(outMps, MathF.Max(fromMps, PicksUpMps(car, fromMps, stepM)));
        return StepBack(
            car, asideM, corneringMps2, lineBend, bandM, clearsM, progressM + stepM, steppedOutMps, out backM,
            out backStepM, out backMps);
    }

    /// <summary>
    /// <b>The step back</b>: begun as soon past what the pass gets past as the body can come back over its own lane
    /// (<see cref="StepBackTrailM"/>), never before the step out is done, and drawn for the pace the car has picked up
    /// by there from <paramref name="steppedOutMps"/> (<see cref="PicksUpMps"/>) — its most, since the step back is
    /// driven no faster than it is drawn for. False where the line under it bends as tight as the lock.
    /// </summary>
    /// <remarks>
    /// <b>Where it begins and what it is drawn for are one answer</b>: drawn for more pace a step is longer, may
    /// begin sooner, and so is drawn for less. They are read against each other <see cref="StepBackReadings"/> times,
    /// which settles them inside what either is worth, and the step last drawn is the one laid.
    /// </remarks>
    bool StepBack(
        int car, float asideM, float corneringMps2, float lineBend, float bandM, float clearsM, float steppedOutM,
        float steppedOutMps, out float backM, out float backStepM, out float backMps)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        backM = MathF.Max(clearsM, steppedOutM);
        backStepM = 0f;
        backMps = steppedOutMps;

        // A trail moves the step back only where it begins it past the end of the step out: that, the trail's own half
        // lattice step, and half a step of slack for the float grain of the body's corners.
        var decidesToM = clearsM - steppedOutM + Config.RibbonLatticeStepM;
        for (var reading = 0; reading < StepBackReadings; reading++)
        {
            var reachedMps = MathF.Max(steppedOutMps, PicksUpMps(car, steppedOutMps, backM - steppedOutM));
            if (!CarFollower.ShapeAPass(build, reachedMps, asideM, corneringMps2, lineBend, out backStepM, out backMps))
            {
                return false;
            }

            backM = MathF.Max(clearsM + StepBackTrailM(build, asideM, backStepM, bandM, decidesToM), steppedOutM);
        }

        return true;
    }

    /// <summary>
    /// <b>What a car doing <paramref name="fromMps"/> has picked up <paramref name="overM"/> further on</b>, pulling
    /// away at its own acceleration — the pass is laid as though it does — up to what the road lets it plan for.
    /// </summary>
    float PicksUpMps(int car, float fromMps, float overM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var roadMps = MathF.Min(build.MaxSpeedMps, Cars.PlannedMps[car]);
        return MathF.Min(roadMps, MathF.Sqrt((fromMps * fromMps) + (2f * build.AccelerationMps2 * MathF.Max(0f, overM))));
    }

    /// <summary>
    /// <b>How far a car's step out has to begin short of what it passes</b>: the furthest along the step that any of
    /// its body — grown by <see cref="DrivingFigures.PassSpareM"/> ahead and to either side, as the pass is asked
    /// for — is still over the lane it leaves, read as the atlas reads it. Infinite where the lane beside is too
    /// narrow for the body ever to be off it.
    /// </summary>
    /// <param name="bandM">How far to the pass's side of the line the lane it leaves reaches.</param>
    float StepOutLeadM(in CarBuild build, float asideM, float stepM, float bandM)
    {
        var acrossM = MathF.Abs(asideM);
        var mostM = float.NegativeInfinity;
        var samples = SamplesOfAStep(stepM);
        for (var sample = 0; sample <= samples; sample++)
        {
            var share = (float)sample / samples;
            if (!OverTheLaneItLeaves(
                    build, share * stepM, acrossM * Overtake.Rise(share), acrossM / stepM * Overtake.RiseSlope(share),
                    bandM, out _, out var toM))
            {
                continue;
            }

            if (sample == samples) return float.PositiveInfinity;

            mostM = MathF.Max(mostM, toM);
        }

        return mostM + (Config.RibbonLatticeStepM * 0.5f);
    }

    /// <summary>
    /// <b>And how soon past what it passes the step back may begin</b>: as far past it as the nearest any of the body
    /// comes back over the lane, behind where the step back begins.
    /// </summary>
    /// <remarks>
    /// <b>Read no further along the step than <paramref name="decidesToM"/></b>, past which no part of the body at a
    /// sample can stand (<see cref="GrownReachM"/>): a nearer point found there would set a trail its caller then
    /// discards. A step drawn at the tyres' very edge on a bending line runs to kilometres, and read whole a lattice
    /// step at a time it is milliseconds of one decision.
    /// </remarks>
    /// <param name="decidesToM">How far along the step the nearest point back over the lane can still decide anything.</param>
    float StepBackTrailM(in CarBuild build, float asideM, float stepM, float bandM, float decidesToM)
    {
        var acrossM = MathF.Abs(asideM);
        var leastM = float.PositiveInfinity;
        var samples = SamplesOfAStep(stepM);
        var readsToM = decidesToM + GrownReachM(build);
        for (var sample = 0; sample <= samples; sample++)
        {
            var share = (float)sample / samples;
            var alongM = share * stepM;
            if (alongM > readsToM) break;

            if (!OverTheLaneItLeaves(
                    build, alongM, acrossM * (1f - Overtake.Rise(share)),
                    -acrossM / stepM * Overtake.RiseSlope(share), bandM, out var fromM, out _))
            {
                continue;
            }

            leastM = MathF.Min(leastM, fromM);
        }

        return -leastM + (Config.RibbonLatticeStepM * 0.5f);
    }

    /// <summary>
    /// <b>The furthest any of a car's collider, grown as <see cref="OverTheLaneItLeaves"/> grows it, stands from its
    /// rear axle</b> whichever way it points — a bound on how far behind a sample's axle the body can reach.
    /// </summary>
    float GrownReachM(in CarBuild build)
    {
        var spareM = Config.Driving.PassSpareM;
        var halfM = build.CollisionSizeM * 0.5f;
        return MathF.Abs(build.CentreAheadOfAxleM + (spareM * 0.5f)) + halfM.X + (spareM * 0.5f) + halfM.Y + spareM;
    }

    /// <summary>How many pieces a step is read in: one a lattice step, which is as finely as the world reads a body.</summary>
    int SamplesOfAStep(float stepM) => Math.Max(1, (int)MathF.Ceiling(stepM / Config.RibbonLatticeStepM));

    /// <summary>
    /// <b>Where a car's body stands over the lane it steps off, at one place on the step</b> — the rear axle
    /// <paramref name="alongM"/> along and <paramref name="acrossM"/> over towards the lane beside, pointed
    /// <paramref name="slope"/> across for a metre along — as the least and most along of the part of its collider,
    /// grown by the pass's spare, short of <paramref name="bandM"/> across. False where none of it is.
    /// </summary>
    bool OverTheLaneItLeaves(
        in CarBuild build, float alongM, float acrossM, float slope, float bandM, out float fromM, out float toM)
    {
        var spareM = Config.Driving.PassSpareM;
        var forward = Vector2.Normalize(new Vector2(1f, slope));
        var side = new Vector2(-forward.Y, forward.X);
        var halfM = build.CollisionSizeM * 0.5f;
        var centreM = new Vector2(alongM, acrossM) + (forward * (build.CentreAheadOfAxleM + (spareM * 0.5f)));
        var lengthwise = forward * (halfM.X + (spareM * 0.5f));
        var sideways = side * (halfM.Y + spareM);

        Span<Vector2> corners = [
            centreM + lengthwise + sideways, centreM + lengthwise - sideways,
            centreM - lengthwise - sideways, centreM - lengthwise + sideways];
        fromM = float.PositiveInfinity;
        toM = float.NegativeInfinity;
        for (var corner = 0; corner < corners.Length; corner++)
        {
            var one = corners[corner];
            var two = corners[(corner + 1) % corners.Length];
            if (one.Y < bandM)
            {
                fromM = MathF.Min(fromM, one.X);
                toM = MathF.Max(toM, one.X);
            }

            if ((one.Y < bandM) == (two.Y < bandM)) continue;

            var crossesM = one.X + ((two.X - one.X) * (bandM - one.Y) / (two.Y - one.Y));
            fromM = MathF.Min(fromM, crossesM);
            toM = MathF.Max(toM, crossesM);
        }

        return toM >= fromM;
    }

    /// <summary>
    /// <b>The most a car's line bends under a stretch of it</b> — on its own line or on the lane beside, whichever
    /// is tighter — which a pass there has to leave room for. Infinite where the lane beside runs round the line's
    /// own centre.
    /// </summary>
    float MostBendUnderThePass(int car, float fromM, float toM, float asideM)
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

    /// <summary>
    /// <b>One more body the pass gets past</b>: the pass then clears it, wherever on the car's line it ends.
    /// </summary>
    static bool Passes(ReadOnlySpan<LineWay> ways, in BodySeen body, Span<BodySeen> passed, ref int count, ref float clearsM)
    {
        if (count == passed.Length || !OnTheLine(ways, body.Way, body.ToM, out var endsM)) return false;

        passed[count++] = body;
        clearsM = MathF.Max(clearsM, endsM);
        return true;
    }

    /// <summary>
    /// <b>Whether a body the car does not pass stands on its own ways over a stretch of its line</b>, other than its own —
    /// the furthest end on those ways of each it does pass, met before, carried out in <paramref name="clearsM"/>.
    /// </summary>
    bool TheNextBodyPast(
        int car, ReadOnlySpan<LineWay> ways, float fromM, float toM, ReadOnlySpan<BodySeen> passed, ref float clearsM)
    {
        foreach (ref readonly var way in ways)
        {
            var wayToM = way.LineFromM + (way.ToM - way.FromM);
            if (way.LineFromM >= toM) break;
            if (wayToM <= fromM) continue;

            var searchFromM = OnTheWayM(way, fromM);
            var searchToM = OnTheWayM(way, toM);
            while (Occupancy.FirstBodySeen(way.Way, searchFromM, searchToM, car, LaneRoster.Driving, out var body))
            {
                if (!body.IsUnderAny(passed)) return true;

                clearsM = MathF.Max(clearsM, OnTheLineM(way, body.ToM));
                searchFromM = body.ToM;
            }
        }

        return false;
    }

    /// <summary>
    /// <b>Whether the ground a pass would cover is road a pass may be had on</b> (TER-4c.6): clear of every zebra, but for
    /// a car on a call, whose rung is above everybody on foot (TER-5g): the paint it crosses is road to it.
    /// </summary>
    bool IsThePassOnTheRoad(int car, in Overtake pass, float toLineM)
    {
        if (Cars.BlueLight[car]) return true;

        Span<LineWay> own = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        var count = PassGround(car, pass, pass.OutM, toLineM, 0f, own, out var back);
        foreach (ref readonly var way in own[..count])
        {
            if (ground.CrossesAZebra(way.Way, way.FromM, way.ToM)) return false;
        }

        return !(back.ToM > back.FromM && ground.CrossesAZebra(back.Way, back.FromM, back.ToM));
    }

    /// <summary>
    /// <b>And whether it is free</b> (TER-4c.6): nobody standing on any of it but the car and what it passes — the car's
    /// own lane up to where its step out begins, and every run of the ground it was decided with. Whether a plan holds
    /// any of it is answered when the pass is laid, which is answered after every plan of its rung.
    /// </summary>
    /// <remarks>
    /// <b>Nobody else is asked for with room to spare</b> (<see cref="DrivingFigures.PassSpareM"/>) — and the ground
    /// is laid and kept without it: a pass that cleared what it passes by a hair was asked for one tick and withdrawn
    /// the next, as that body settled a hair nearer.
    /// </remarks>
    bool IsThePassUnheld(int car, int shape, float progressM)
    {
        var table = Table(car, shape);
        var passed = Passed(table);
        Span<LineWay> approach = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        foreach (ref readonly var way in TheApproach(car, _shapes[Holder(car, shape)], progressM, approach))
        {
            if (Occupancy.AnyBodyOver(way.Way, way.FromM, way.ToM, car, LaneRoster.Driving)) return false;
        }

        ref readonly var decided = ref _shapes[Holder(car, shape)];
        Span<LineWay> stretches = stackalloc LineWay[DrivingGround.MostWaysAlongALine + 1];
        var count = PassGround(
            car, decided, decided.OutM, decided.EndsM + Config.Driving.StandOffM, Config.Driving.PassSpareM,
            stretches[..^1], out var back);
        stretches[count++] = back;
        foreach (ref readonly var run in stretches[..count])
        {
            var fromM = run.FromM;
            while (fromM < run.ToM && Occupancy.FirstBodySeen(run.Way, fromM, run.ToM, car, LaneRoster.Driving, out var body))
            {
                if (!body.IsUnderAny(passed)) return false;

                fromM = body.ToM;
            }
        }

        return true;
    }

    /// <summary>How far across its line this car is aimed at one metre of it: its pass's, once begun, and none otherwise.</summary>
    public float AsideAtM(int car, float atM) => Cars.Pass[car].Begun ? Cars.Pass[car].AsideAtM(atM) : 0f;

    /// <summary>
    /// <b>Where the plan of a car on a pass begins</b>: past the pass, the ground up to there being the pass's own
    /// (TER-4c.6) — and its nose for every other car.
    /// </summary>
    public float PlannedFromM(int car) =>
        Cars.Pass[car].Begun
            ? MathF.Max(Cars.ClaimFromM[car], Cars.Pass[car].EndsM + Cars.BuildOf(car).NoseAheadOfAxleM)
            : Cars.ClaimFromM[car];

    /// <summary>
    /// <b>How far across the lane beside stands at a place on a lane</b>, along the driver's right: the nearest point
    /// of its line, sought where the two lengths put it — the lane back runs the same stretch the other way round.
    /// </summary>
    float AsideOnTheLaneBeside(int lane, int back, float alongM)
    {
        var lengthM = Roads.LaneLengthM[lane];
        var backM = Roads.LaneLengthM[back];
        var on = Spline.SampleAt(Roads.ArcsOf(lane), alongM);
        var windowM = MathF.Abs(backM - lengthM) + Roads.LaneWidthM[lane] + Roads.LaneWidthM[back];
        var besideM = Spline.ProjectM(Roads.ArcsOf(back), on.PositionM, (lengthM - alongM) * (backM / lengthM), windowM);
        return Vector2.Dot(Spline.SampleAt(Roads.ArcsOf(back), besideM).PositionM - on.PositionM, on.Right);
    }
}