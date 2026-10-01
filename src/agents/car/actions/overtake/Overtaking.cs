using System.Numerics;
using System.Runtime.CompilerServices;
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
/// its step out begins, asked for whole on its own clock (<see cref="DrivingFigures.PassAskEveryS"/>), laid as a body
/// over all the ground it will cover, kept or withdrawn once before the car moves over, and driven as the car's own line
/// aimed across into the lane beside. Over once the car is back in its own lane; let go once what it was getting past
/// is gone, or once it has waited its patience out (<see cref="DrivingFigures.PassPatienceS"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here reads another agent</b> (TER-4c.5). What is in the way, whether it is at rest and whether it
/// is making this car's own movement are what its body said of itself on the way it cut the grant; whether the
/// ground of the pass is free is what is laid on the ways under it.
/// </para>
/// <para>
/// <b>Its ground is the car's own body swept down the pass and read off the atlas once</b>, as a body's is
/// (TER-4c.2): the collider stood where the pass puts the rear axle and pointed the way it points there, every
/// half a car's width of line from where the step out begins to where it is back in its lane — kept as runs
/// (<see cref="SweptGround"/>), so asking for it, laying it and giving back what the car has driven past are reads of
/// what was swept. So a pass runs through a box as it runs along a street, wherever its ground can be had.
/// </para>
/// <para>
/// <b>A pass is never given up once the car has moved over</b>: it cannot safely be, so it is asked for only
/// over ground nobody holds, and it holds that ground at p0, where nothing takes it.
/// </para>
/// <para>
/// <b>A car on a call asks at its call's rung</b> (AMB-4.4, <see cref="TermsOfThePass"/>): a plan that rung beats
/// holds none of the ground, a queue making the car's own movement is got past like anything else at rest and so is
/// traffic slower than the call (<see cref="MayGetPast"/>), and the paint of a zebra is claimed whole — somebody on
/// foot already on it is waited for short of the paint.
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
    /// How many runs one pass's ground is kept in (<see cref="SweptGround"/>). A bound on the table and not a figure
    /// behaviour reads: a run is one way under a car's length of the pass, so a pass past a queue through a box is
    /// well inside it, and one that is not is a pass the road does not offer.
    /// </summary>
    const int MostRunsOfAPass = 64;

    /// <summary>
    /// The two shapes a car decides its pass in, both at once: drawn to step out from rest — where it waits and what it
    /// backs up for — and drawn for the pace it came up at, which is had only while the car can still come up to where
    /// that one begins at a pace it may be driven at.
    /// </summary>
    const int FromRest = 0, AtSpeed = 1, Shapes = 2;

    /// <summary>Each car's decided shapes, committed to while it waits for its pass, backs up for it and drives it.</summary>
    readonly Overtake[] _shapes = new Overtake[ground.Cars.Capacity * Shapes];

    /// <summary>The ground each decided shape's body sweeps — what is laid, kept, and looked for a body in.</summary>
    readonly SweptGround _swept = new(ground.Cars.Capacity * Shapes, MostRunsOfAPass);

    /// <summary>And the same grown by the pass's spare and run on by the stand-off — what is asked for.</summary>
    readonly SweptGround _asked = new(ground.Cars.Capacity * Shapes, MostRunsOfAPass);

    /// <summary>The bodies each decided shape gets past, and the ways they were met on.</summary>
    readonly LaneClaim[] _passed = new LaneClaim[ground.Cars.Capacity * Shapes * MostPassedAtOnce];

    readonly int[] _passedOn = new int[ground.Cars.Capacity * Shapes * MostPassedAtOnce];

    readonly int[] _passedCount = new int[ground.Cars.Capacity * Shapes];

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
    /// The room each car keeps to step out from rest (<see cref="KeptOffM"/>), and the lane and ground it was drawn for —
    /// the only things it reads that change.
    /// </summary>
    readonly float[] _roomM = new float[ground.Cars.Capacity];

    readonly int[] _roomLane = new int[ground.Cars.Capacity];

    readonly float[] _roomSurface = Unset(ground.Cars.Capacity);

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
    public void Tick<TTown>(ref TTown town, int car, in CarPose pose, float progressM, float alongMps, float coveredM)
        where TTown : struct, ICarTown
    {
        var pass = Cars.Pass[car];
        if (pass.Begun)
        {
            if (progressM >= pass.EndsM)
            {
                Made++;
                actions.Enter(car, CarAction.Follow);
            }

            town.DriveOnTheLine(car, pose, progressM, alongMps, coveredM);
            return;
        }

        // Asked for in the tick before and laid since: kept, or withdrawn and asked for again when the car next looks.
        if (pass.Any) KeepOrWithdrawThePass(car, pass, progressM);

        WaitAtTheStep(ref town, car, pose, progressM, alongMps, coveredM);
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
        ref TTown town, int car, in CarPose pose, float progressM, float alongMps, float coveredM, float backUpM = 0f,
        bool blocked = false)
        where TTown : struct, ICarTown
    {
        var fromRest = _shapes[Holder(car, FromRest)];
        var waits = !Cars.Pass[car].Begun;
        var toTheStepM = fromRest.OutM - progressM;
        town.DriveOnTheLine(
            car, pose, progressM, alongMps, coveredM, waits, fromRest.AsideM, backUpM, blocked,
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
        if (_waitedS[car] > Config.Driving.PassPatienceS || !IsStillThere(car)) return false;

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
    [SkipLocalsInit]
    public bool IsTheRoomHeld(int car)
    {
        var holder = Holder(car, FromRest);
        ref readonly var shape = ref _shapes[holder];
        var reachM = shape.EndsM + Cars.BuildOf(car).NoseAheadOfAxleM + Config.Driving.StandOffM;
        Span<LineWay> ways = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        var count = ground.WaysAlong(car, shape.ClearsM, reachM, ways);
        var clearedM = shape.ClearsM;
        return TheNextBodyPast(car, ways[..count], shape.ClearsM, reachM, Passed(holder), ref clearedM, out _, out _);
    }

    /// <summary>One of the car's shapes asked for, where all its ground is free — laid in the rebuild after.</summary>
    void Ask(int car, int shape, float progressM)
    {
        var at = Holder(car, shape);
        if (!IsThePassUnheld(car, at, progressM)) return;

        Cars.Pass[car] = _shapes[at];
        _asking[car] = shape;
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

        var brakingMps2 = CarFollower.BrakingMps2(Config, Cars.BuildOf(car), Cars.GroundCoefficient[car]);
        var slowsOverM = (speedMps * Config.CarReactionS)
                         + (((speedMps * speedMps) - (shape.OutMps * shape.OutMps)) / (2f * brakingMps2));
        return slowsOverM <= toM;
    }

    /// <summary>Which holder of the shape tables one of a car's shapes is.</summary>
    static int Holder(int car, int shape) => (car * Shapes) + shape;

    /// <summary>The shape of its pass the car asked for — what is laid, kept and driven.</summary>
    int Asking(int car) => Holder(car, _asking[car]);

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
    /// <b>Whether what the car decided to get past is still there to be got past</b>: every body its pass gets past
    /// still on the way it was met on, still one this car may, and still where it was to within the pass's spare —
    /// which is where the pass was drawn round it. <b>Traffic a call gets past moves on</b>, so a pass drawn round it
    /// is let go at the car's next look and decided again from there.
    /// </summary>
    public bool IsStillThere(int car)
    {
        var fromRest = Holder(car, FromRest);
        var passed = Passed(fromRest);
        var passedOn = _passedOn.AsSpan(fromRest * MostPassedAtOnce, passed.Length);
        var spareM = Config.Driving.PassSpareM;
        for (var index = 0; index < passed.Length; index++)
        {
            ref readonly var was = ref passed[index];
            if (!Occupancy.TheBodyOf(passedOn[index], was.Occupant, was.Of, out var body)
                || !MayGetPast(car, body, passedOn[index])
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

        for (var shape = Holder(car, 0); shape < Holder(car, Shapes); shape++)
        {
            if (!_shapes[shape].Any) continue;

            _shapes[shape] = _shapes[shape].From(lane, shiftM);
            _swept.Shift(shape, shiftM);
            _asked.Shift(shape, shiftM);
        }
    }

    /// <summary>
    /// <b>The ground this car's pass will cover, laid as a body</b> (TER-4c.6): the car's own lane up to where its step
    /// out begins, where it has not come up to it yet, and every run of its swept ground it has not yet driven past. A
    /// pass the car is no longer on its line for, or no longer on the lane of, is given up for following.
    /// </summary>
    /// <remarks>
    /// <b>What it has driven over is given back</b> a run at a time, as the car comes past the last station of each.
    /// </remarks>
    /// <param name="underWay">Whether the car is on the route's line, as the town reads it.</param>
    [SkipLocalsInit]
    public void Lay(int car, bool underWay)
    {
        var pass = Cars.Pass[car];
        if (Cars.Action[car] != CarAction.Overtake || !pass.Any) return;

        if (!underWay || Cars.LaneOf(car) != pass.Lane)
        {
            actions.Enter(car, CarAction.Follow);
            return;
        }

        var progressM = Cars.ProgressM[car];
        Span<LineWay> approach = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        foreach (ref readonly var way in TheApproach(car, pass, progressM, approach))
        {
            Occupancy.LayPass(way.Way, way.FromM, way.ToM, 0f, car, LaneRoster.Driving);
        }

        foreach (ref readonly var run in _swept.Of(Asking(car)))
        {
            if (run.LastAtM < progressM) continue;

            var held = ground.HeldAsABody(run.Cover);
            Occupancy.LayPass(held.Way, held.FromM, held.ToM, 0f, car, LaneRoster.Driving);
            LayThePaintItCrosses(car, run.Cover);
        }
    }

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
    /// it in front of the pass. Only a call's pass is ever over one.
    /// </summary>
    void LayThePaintItCrosses(int car, in WayCover swept)
    {
        foreach (ref readonly var mark in Occupancy.Marks.Of(swept.Way))
        {
            if (mark.MineFromM >= swept.ToM) break;
            if (mark.MineToM <= swept.FromM || ground.Lines.ZebraOf(mark.OnWay) == RibbonMarks.NoZebra) continue;

            Occupancy.LayPass(mark.OnWay, mark.FromM, mark.ToM, 0f, car, LaneRoster.Driving);
        }
    }

    /// <summary>
    /// <b>Whether the car may get past what ended its grant</b>: a body it may pass (<see cref="MayGetPast"/>), in a
    /// lane that has a lane running back beside it, on the route's own line driven forwards.
    /// </summary>
    public bool MayGetPastWhatCutIt(int car, out LaneClaim cutBy, out int cutOn)
    {
        cutBy = LaneClaim.Nothing;
        cutOn = LaneOccupancy.NoHold;
        var hold = ground.PlanHold[car];
        if (hold == LaneOccupancy.NoHold || !HasALaneToPassOn(car)) return false;

        Occupancy.HoldEndsAtM(hold, out _, out cutBy);
        cutOn = Occupancy.HoldCutOn(hold);
        return cutBy.Found && MayGetPast(car, cutBy, cutOn);
    }

    /// <summary>
    /// <b>Whether this car may get past a body on one of its ways</b> (<see cref="LaneClaim.MayBePassedBy"/>): at
    /// rest, not a pass, and — unless the car is on a call — not making the car's own movement there. <b>A car on a
    /// call gets past traffic that is moving too, where it is going slower than the call means to</b>
    /// (<see cref="CarFleet.PlannedMps"/>) by more than <see cref="AmbulanceFigures.SlowerToPassMps"/>: what goes as
    /// fast is no hindrance, and an escort held under its charge's pace is never one.
    /// </summary>
    bool MayGetPast(int car, in LaneClaim body, int on) =>
        body.MayBePassedBy(ground.OnwardAlongTheLine(car, on), Cars.BlueLight[car])
        && (body.Still || body.AlongMps < Cars.PlannedMps[car] - Config.Ambulance.SlowerToPassMps);

    /// <summary>
    /// Whether the car could pass anything at all where it is: on the route's own line driven forwards, in a
    /// lane that has a lane running back beside it.
    /// </summary>
    bool HasALaneToPassOn(int car)
    {
        if (Cars.Line[car].LaneCount == 0 || Cars.LineIsReverse[car]) return false;

        var lane = Cars.LaneOf(car);
        return lane != CarFleet.NoLane && Roads.LaneReverse[lane] >= 0 && !Roads.LaneOverOneLine[lane];
    }

    /// <summary>
    /// <b>A pass laid in this rebuild, kept or withdrawn</b> before the car moves over: kept where nothing but the
    /// car itself is on its ground, the one exception a pass asked for on the same tick by a holder numbered after
    /// it (<see cref="LaneOccupancy.KeepsItsPass"/>).
    /// </summary>
    [SkipLocalsInit]
    void KeepOrWithdrawThePass(int car, in Overtake pass, float progressM)
    {
        Span<LineWay> approach = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        var kept = true;
        foreach (ref readonly var way in TheApproach(car, pass, progressM, approach))
        {
            kept &= Occupancy.KeepsItsPass(way.Way, way.FromM, way.ToM, car, LaneRoster.Driving);
        }

        foreach (ref readonly var run in _swept.Of(Asking(car)))
        {
            if (!kept) break;
            if (run.LastAtM < progressM) continue;

            var held = ground.HeldAsABody(run.Cover);
            kept = Occupancy.KeepsItsPass(
                held.Way, run.FromM, run.ToM, held.FromM, held.ToM, car, LaneRoster.Driving, TermsOfThePass(car, run.Cover));
        }

        if (kept)
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
        var brakingMps2 = CarFollower.BrakingMps2(Config, build, Cars.GroundCoefficient[car]);
        var slowsFromM = DrivingGround.StoppingM(speedMps, brakingMps2 * Config.Driving.WaitingToPassBrakingShare)
                         + (speedMps * CarFollower.LeadS(Config, build, brakingMps2)) + Config.Driving.PassSpareM;
        return toTheStopM <= slowsFromM;
    }

    /// <summary>
    /// <b>A pass decided on, once</b> (CAR-46): where the grant was ended on the car's own line by a body it may get
    /// past, drawn, swept and committed to — what the car then waits for, backs up for and drives, and never draws
    /// again. False where the road refuses it, which is not asked again before the car would next look round
    /// (<see cref="DrivingFigures.PassAskEveryS"/>).
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
    /// </remarks>
    /// <param name="sinceLastDecisionS">How much of the town's time this decision answers for, which its clock runs by.</param>
    /// <param name="toTheSceneM">How far ahead along the line the place the car was sent to stands, or infinity.</param>
    public bool DrawThePass(
        int car, float sinceLastDecisionS, float progressM, float alongMps, float toTheSceneM, in LaneClaim cutBy, int cutOn)
    {
        ref var inS = ref _decidesInS[car];
        inS -= sinceLastDecisionS;
        if (inS > 0f) return false;

        // Refused, it is not drawn again before the car would next look round.
        inS = Config.Driving.PassAskEveryS;
        var atSpeed = Holder(car, AtSpeed);
        var speedMps = MathF.Max(0f, alongMps);
        if (!(speedMps > Config.Driving.StopSpeedMps
              && Shape(car, atSpeed, progressM, speedMps, cutBy, cutOn, out var shape)
              && CanComeUpTo(car, shape, progressM, speedMps)
              && Commit(car, atSpeed, shape, progressM + toTheSceneM)))
        {
            _shapes[atSpeed] = Overtake.None;
        }

        var fromRest = Holder(car, FromRest);
        if (!Shape(car, fromRest, progressM, 0f, cutBy, cutOn, out shape) || !Commit(car, fromRest, shape, progressM + toTheSceneM))
        {
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
    bool Commit(int car, int holder, in Overtake shape, float sceneAtM)
    {
        if (sceneAtM < shape.EndsM
            || !IsThePassOnTheRoad(car, shape, shape.OutM, shape.EndsM + Config.Driving.StandOffM)
            || !Sweep(car, holder, shape))
        {
            return false;
        }

        _shapes[holder] = shape;
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
    /// enough to come back into — the step back, and the car's own length and stand-off past that. Another body it
    /// may pass standing in that room is passed too, and the room looked for past it. <b>Anything else standing
    /// there</b> — traffic, a queue — is where the car comes back once that has gone: it has decided, and waits.
    /// </para>
    /// <para>
    /// <b>The step back begins as soon past what it passes as the body can come back</b>
    /// (<see cref="StepBackTrailM"/>), drawn for the pace the car has picked up by then (<see cref="StepBack"/>): the
    /// pass is laid as though the car pulls away along it, so it never slows for its own pass and is off the lane
    /// beside as soon as that pace takes it.
    /// </para>
    /// </remarks>
    /// <param name="holder">The car's shape this is drawn as, which its bodies passed are kept under.</param>
    [SkipLocalsInit]
    bool Shape(int car, int holder, float progressM, float speedMps, in LaneClaim cutBy, int cutOn, out Overtake pass)
    {
        pass = Overtake.None;
        ref readonly var build = ref Cars.BuildOf(car);
        var lane = Cars.LaneOf(car);
        var back = Roads.LaneReverse[lane];
        var noseM = progressM + build.NoseAheadOfAxleM;
        var lineM = Cars.Line[car].LengthM;
        Span<LineWay> ways = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        var count = ground.WaysAlong(car, noseM, lineM, ways);
        if (!OnTheLine(ways[..count], cutOn, cutBy.FromM, out var standsFromM)) return false;

        var asideM = AsideOnTheLaneBeside(lane, back, Math.Clamp(progressM, 0f, Roads.LaneLengthM[lane]));

        var passed = _passed.AsSpan(holder * MostPassedAtOnce, MostPassedAtOnce);
        var passedOn = _passedOn.AsSpan(holder * MostPassedAtOnce, MostPassedAtOnce);
        ref var passedCount = ref _passedCount[holder];
        passedCount = 0;
        var clearsM = noseM;
        if (!Passes(car, ways[..count], cutOn, cutBy, passed, passedOn, ref passedCount, ref clearsM)) return false;

        var corneringMps2 = CarFollower.CorneringMps2(Config, build, Cars.GroundCoefficient[car]);
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
                    out var outMps, out var backM, out var backStepM, out var backMps, out _))
            {
                return false;
            }

            // Standing in the room it comes back into: a body it may pass is passed too, and the room looked for past it;
            // anything else is where the car comes back once that has gone. What it already passes reaching on further
            // over another of its ways moves the room on with it.
            var reachM = backM + backStepM + build.NoseAheadOfAxleM + Config.Driving.StandOffM;
            var clearedM = clearsM;
            var found = TheNextBodyPast(
                car, ways[..count], clearsM, reachM, passed[..passedCount], ref clearedM, out var next, out var nextOn);
            if (clearedM > clearsM)
            {
                clearsM = clearedM;
                continue;
            }

            if (found && MayGetPast(car, next, nextOn))
            {
                if (!Passes(car, ways[..count], nextOn, next, passed, passedOn, ref passedCount, ref clearsM)) return false;

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

    /// <summary>
    /// <b>A decided pass's ground, swept once</b> (<see cref="SweptGround"/>): what its body covers, for laying and
    /// keeping, and the same grown by the spare and run on by the stand-off past its end, for asking — the carriageway
    /// of each, a car's length of stations to a run. False where it does not fit in the runs a pass may have.
    /// </summary>
    [SkipLocalsInit]
    bool Sweep(int car, int holder, in Overtake pass)
    {
        var spanM = Cars.BuildOf(car).LengthM;
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        return SweepInto(_swept, holder, car, pass, pass.EndsM, 0f, spanM, under)
               && SweepInto(
                   _asked, holder, car, pass, pass.EndsM + Config.Driving.StandOffM, Config.Driving.PassSpareM, spanM,
                   under);
    }

    bool SweepInto(
        SweptGround into, int holder, int car, in Overtake pass, float toM, float spareM, float spanM, Span<WayCover> under)
    {
        into.Clear(holder);
        for (var station = 0; station < ground.StationsOfTheSweep(car, pass.OutM, toM); station++)
        {
            var count = UnderTheCarOnThePass(car, pass, pass.OutM, toM, station, spareM, under, out var atM);
            var kept = 0;
            for (var at = 0; at < count; at++)
            {
                if (ground.IsCarriageway(under[at].Way)) under[kept++] = under[at];
            }

            if (!into.Station(holder, atM, spanM, under[..kept])) return false;
        }

        return true;
    }

    /// <summary>The bodies one of a car's shapes gets past, named by occupant and roster.</summary>
    ReadOnlySpan<LaneClaim> Passed(int holder) => _passed.AsSpan(holder * MostPassedAtOnce, _passedCount[holder]);

    /// <summary>
    /// <b>A pass's two steps</b>: out from where it begins over <paramref name="stepM"/>, driven no faster than
    /// that length allows, and back as <see cref="StepBack"/> draws it for the pace the car has at the end of the
    /// step out. False where the line under them bends as tight as the lock.
    /// </summary>
    /// <param name="fromMps">The pace the shortest step out is drawn for, which no step out is driven below.</param>
    /// <param name="mayBeginBackM">Where the step back could begin with the step out done by then.</param>
    bool StepOutAndBack(
        int car, float progressM, float stepM, float fromMps, float asideM, float corneringMps2, float lineBend,
        float bandM, float clearsM, out float outStepM, out float outMps, out float backM, out float backStepM,
        out float backMps, out float mayBeginBackM)
    {
        outStepM = stepM;
        outMps = MathF.Max(fromMps, CarFollower.StepMps(Cars.BuildOf(car), stepM, asideM, corneringMps2, lineBend));
        var steppedOutMps = MathF.Min(outMps, MathF.Max(fromMps, PicksUpMps(car, fromMps, stepM)));
        return StepBack(
            car, asideM, corneringMps2, lineBend, bandM, clearsM, progressM + stepM, steppedOutMps, out backM,
            out backStepM, out backMps, out mayBeginBackM);
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
    /// <param name="mayBeginM">Where it could begin were the step out done by then.</param>
    bool StepBack(
        int car, float asideM, float corneringMps2, float lineBend, float bandM, float clearsM, float steppedOutM,
        float steppedOutMps, out float backM, out float backStepM, out float backMps, out float mayBeginM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        mayBeginM = clearsM;
        backM = MathF.Max(clearsM, steppedOutM);
        backStepM = 0f;
        backMps = steppedOutMps;
        for (var reading = 0; reading < StepBackReadings; reading++)
        {
            var reachedMps = MathF.Max(steppedOutMps, PicksUpMps(car, steppedOutMps, backM - steppedOutM));
            if (!CarFollower.ShapeAPass(build, reachedMps, asideM, corneringMps2, lineBend, out backStepM, out backMps))
            {
                return false;
            }

            mayBeginM = clearsM + StepBackTrailM(build, asideM, backStepM, bandM);
            backM = MathF.Max(mayBeginM, steppedOutM);
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

        return mostM + (ground.Atlas.StepM * 0.5f);
    }

    /// <summary>
    /// <b>And how soon past what it passes the step back may begin</b>: as far past it as the nearest any of the body
    /// comes back over the lane, behind where the step back begins.
    /// </summary>
    float StepBackTrailM(in CarBuild build, float asideM, float stepM, float bandM)
    {
        var acrossM = MathF.Abs(asideM);
        var leastM = float.PositiveInfinity;
        var samples = SamplesOfAStep(stepM);
        for (var sample = 0; sample <= samples; sample++)
        {
            var share = (float)sample / samples;
            if (!OverTheLaneItLeaves(
                    build, share * stepM, acrossM * (1f - Overtake.Rise(share)),
                    -acrossM / stepM * Overtake.RiseSlope(share), bandM, out var fromM, out _))
            {
                continue;
            }

            leastM = MathF.Min(leastM, fromM);
        }

        return -leastM + (ground.Atlas.StepM * 0.5f);
    }

    /// <summary>How many pieces a step is read in: one a lattice step, which is as finely as the atlas reads a body.</summary>
    int SamplesOfAStep(float stepM) => Math.Max(1, (int)MathF.Ceiling(stepM / ground.Atlas.StepM));

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
    /// <b>One more body the pass gets past</b>, where it is one this car may: the pass then clears it, wherever on
    /// the car's line it ends — <b>and, where it is moving, wherever it can come to rest</b>
    /// (<see cref="LaneOccupancy.StopsByM"/>), since the pass laid past it is what it is then held short of.
    /// </summary>
    bool Passes(
        int car, ReadOnlySpan<LineWay> ways, int on, in LaneClaim body, Span<LaneClaim> passed, Span<int> passedOn,
        ref int count, ref float clearsM)
    {
        if (count == passed.Length || !MayGetPast(car, body, on)) return false;

        var endsAtM = body.Still ? body.ToM : Occupancy.StopsByM(on, body);
        if (!OnTheLine(ways, on, endsAtM, out var endsM)) return false;

        passedOn[count] = on;
        passed[count++] = body;
        clearsM = MathF.Max(clearsM, endsM);
        return true;
    }

    /// <summary>
    /// <b>The nearest body on this car's own ways over a stretch of its line</b>, other than its own and what it
    /// already passes — whose furthest end on those ways is carried out in <paramref name="clearsM"/>, since one
    /// body is a stretch of every way it stands on.
    /// </summary>
    /// <remarks>
    /// <b>What it passes is told apart by who it is, and never by where</b>: a metre carried from a way to the line
    /// and back is a float's grain off where it began, and a body read again a grain short of its own end was passed
    /// once for every slot the pass had, and the pass given up.
    /// </remarks>
    bool TheNextBodyPast(
        int car, ReadOnlySpan<LineWay> ways, float fromM, float toM, ReadOnlySpan<LaneClaim> passed, ref float clearsM,
        out LaneClaim body, out int on)
    {
        foreach (ref readonly var way in ways)
        {
            var wayToM = way.LineFromM + (way.ToM - way.FromM);
            if (way.LineFromM >= toM) break;
            if (wayToM <= fromM) continue;

            var searchFromM = OnTheWayM(way, fromM);
            var searchToM = OnTheWayM(way, toM);
            while (Occupancy.AheadBody(way.Way, searchFromM, searchToM, car, out body))
            {
                if (!IsAmong(body, passed))
                {
                    on = way.Way;
                    return true;
                }

                clearsM = MathF.Max(clearsM, OnTheLineM(way, body.ToM));
                searchFromM = body.ToM;
            }
        }

        body = LaneClaim.Nothing;
        on = LaneOccupancy.NoHold;
        return false;
    }

    /// <summary>Whether a body is one of those a pass already gets past, by occupant and roster.</summary>
    static bool IsAmong(in LaneClaim body, ReadOnlySpan<LaneClaim> passed)
    {
        foreach (ref readonly var one in passed)
        {
            if (one.Occupant == body.Occupant && one.Of == body.Of) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>Whether the ground a pass would cover is road a pass may be had on</b> (TER-4c.6): on the carriageway — a
    /// stretch over ground the traffic does not drive is a pass run off the road — and clear of every zebra, but for
    /// a car on a call, whose rung is above everybody on foot (TER-5g): the paint it crosses is road to it.
    /// </summary>
    /// <remarks>
    /// Asked of the body alone, since the spare (<see cref="DrivingFigures.PassSpareM"/>) is room to stray into and
    /// not a place the car is taken.
    /// </remarks>
    [SkipLocalsInit]
    bool IsThePassOnTheRoad(int car, in Overtake pass, float fromM, float toM)
    {
        var overThePaint = Cars.BlueLight[car];
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        for (var station = 0; station < ground.StationsOfTheSweep(car, fromM, toM); station++)
        {
            var count = UnderTheCarOnThePass(car, pass, fromM, toM, station, 0f, under, out _);
            for (var at = 0; at < count; at++)
            {
                ref readonly var cover = ref under[at];
                if (overThePaint)
                {
                    if (!ground.IsCarriageway(cover.Way) && !ground.Lines.IsTheCrossing(cover.Way)) return false;
                }
                else if (!ground.IsCarriageway(cover.Way) || ground.CrossesAZebra(cover.Way, cover.FromM, cover.ToM))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// <b>And whether it is free</b> (TER-4c.6): nobody standing on any of it but the car, and nobody planning it but
    /// what it passes — and, for a car on a call, but what its terms take and wait for (<see cref="TermsOfThePass"/>):
    /// the car's own lane up to where its step out begins, and every run of the ground it was decided with.
    /// </summary>
    /// <remarks>
    /// <b>Nobody else is asked for with room to spare</b> (<see cref="DrivingFigures.PassSpareM"/>) — and the ground
    /// is laid and kept without it: a pass that cleared what it passes by a hair was asked for one tick and withdrawn
    /// the next, as that body settled a hair nearer. For a call's pass the spare is also what keeps the ground it
    /// takes clear of what an oncoming holder can no longer stop short of by the rebuild the pass is laid in.
    /// </remarks>
    [SkipLocalsInit]
    bool IsThePassUnheld(int car, int holder, float progressM)
    {
        var passed = Passed(holder);
        Span<LineWay> approach = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        foreach (ref readonly var way in TheApproach(car, _shapes[holder], progressM, approach))
        {
            if (!Occupancy.IsFreeForAPass(way.Way, way.FromM, way.ToM, car, LaneRoster.Driving, passed)) return false;
        }

        foreach (ref readonly var run in _asked.Of(holder))
        {
            var held = ground.HeldAsABody(run.Cover);
            if (!Occupancy.IsFreeForAPass(
                    held.Way, run.FromM, run.ToM, held.FromM, held.ToM, car, LaneRoster.Driving, passed,
                    TermsOfThePass(car, run.Cover)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <b>Where the nose of a car on a pass has to stop short of a body standing inside what is left of it</b> — the
    /// one thing that ends a pass's ground short of its end, since nothing planned can be laid over it (TER-4c.1):
    /// short of it on the car's own lane before the step out, and short of the first station of the run it stands in
    /// after that.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read where the pass's body goes</b>, and not over the whole of a movement the pass holds: a body on that
    /// movement clear of it is in nobody's way, and is what the pass is getting past where it stands in a box.
    /// </para>
    /// <para>
    /// <b>And short of a zebra's paint while anybody on foot is on any of it</b> — the pass holds it whole, and
    /// somebody already on it crosses — so the car never stands on the paint across the way they walk, each waiting
    /// for the other. <b>A car already over the paint drives on off it</b>, and is held by a body in its way alone.
    /// </para>
    /// </remarks>
    [SkipLocalsInit]
    public bool TheBodyInThePass(int car, out float inTheWayM, out LaneClaim body, out int on)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var progressM = Cars.ProgressM[car];
        Span<LineWay> approach = stackalloc LineWay[DrivingGround.MostWaysAlongALine];
        foreach (ref readonly var way in TheApproach(car, Cars.Pass[car], progressM, approach))
        {
            if (!Occupancy.AheadBody(way.Way, way.FromM, way.ToM, car, out body)) continue;

            on = way.Way;
            inTheWayM = OnTheLineM(way, MathF.Max(way.FromM, body.FromM));
            return true;
        }

        // The paint the car stands over at its first station, and on unbroken from there, is paint it is already on.
        var onThePaintToM = progressM;
        var onThePaint = true;
        foreach (ref readonly var run in _swept.Of(Asking(car)))
        {
            if (run.LastAtM < progressM) continue;

            on = run.Way;
            var inTheWay = Occupancy.AheadBody(run.Way, run.FromM, run.ToM, car, out body);
            if (!inTheWay && ground.CrossesAZebra(run.Way, run.FromM, run.ToM))
            {
                if (onThePaint && run.FirstAtM <= onThePaintToM + build.FlankM)
                {
                    onThePaintToM = MathF.Max(onThePaintToM, run.LastAtM);
                    continue;
                }

                onThePaint = false;
                inTheWay = ground.SomebodyOnThePaint(run.Cover, out body, out on);
            }

            if (!inTheWay) continue;

            inTheWayM = MathF.Max(progressM, run.FirstAtM - build.FlankM) + build.NoseAheadOfAxleM;
            return true;
        }

        inTheWayM = float.PositiveInfinity;
        body = LaneClaim.Nothing;
        on = LaneOccupancy.NoHold;
        return false;
    }

    /// <summary>
    /// <b>What a car's pass is asked and kept on over one stretch of a way it sweeps</b> (<see cref="PassTerms"/>):
    /// for a car on a call, its call's rung (AMB-4.4) and the paint of any zebra there, where somebody on foot is
    /// waited for (<see cref="TheBodyInThePass"/>) rather than refusing the pass; for every other car, neither.
    /// </summary>
    PassTerms TermsOfThePass(int car, in WayCover swept)
    {
        if (!Cars.BlueLight[car]) return PassTerms.Plain;

        var paintFromM = float.PositiveInfinity;
        var paintToM = float.NegativeInfinity;
        foreach (ref readonly var mark in Occupancy.Marks.Of(swept.Way))
        {
            if (mark.MineFromM >= swept.ToM) break;
            if (mark.MineToM <= swept.FromM || ground.Lines.ZebraOf(mark.OnWay) == RibbonMarks.NoZebra) continue;

            paintFromM = MathF.Min(paintFromM, mark.MineFromM);
            paintToM = MathF.Max(paintToM, mark.MineToM);
        }

        return new PassTerms(ClaimPriority.Special, paintFromM, paintToM);
    }

    /// <summary>
    /// <b>The ways under the car at one station of its pass</b>: its collider, stood where the pass puts the rear
    /// axle at that metre of the line and pointed the way the pass points it there — and grown by
    /// <paramref name="spareM"/> ahead and to either side, the ways it could stray into; behind it is the ground it
    /// came from.
    /// </summary>
    int UnderTheCarOnThePass(
        int car, in Overtake pass, float fromM, float toM, int station, float spareM, Span<WayCover> under,
        out float atM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        atM = MathF.Min(toM, fromM + (station * build.FlankM));
        pass.PoseAtM(Cars.LineOf(car), atM, out var axleM, out var forward);

        var halfM = build.CollisionSizeM * 0.5f;
        var centreM = axleM + (forward * (build.CentreAheadOfAxleM + (spareM * 0.5f)));
        return ground.Atlas.UnderBox(centreM, forward, halfM.X + (spareM * 0.5f), halfM.Y + spareM, under);
    }

    /// <summary>
    /// <b>How far short of what cut it a car comes to rest</b> (S-2a): its stand-off, whatever that was — and behind
    /// a body going nowhere (<see cref="LaneClaim.GoesNowhere"/>) that it could get past, no nearer than it needs to
    /// step out round it from a standstill, with the pass's spare (CAR-46).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A queue is waited behind at the stand-off whatever its next movement</b>, so a line of cars at a light
    /// closes up behind one turning off. The room is kept only for what the car may come to pass — <b>and a car on a
    /// call keeps it behind anything at rest it may pass</b>, a queue included (<see cref="MayGetPast"/>).
    /// </para>
    /// <para>
    /// <b>Read on a straight</b>: where the line bends under the step, the step is longer and the car asks from
    /// where it stands, which the ground then answers.
    /// </para>
    /// </remarks>
    /// <param name="cutOn">The way <paramref name="cutBy"/> was met on.</param>
    public float KeptOffM(int car, in LaneClaim cutBy, int cutOn)
    {
        var standOffM = Config.Driving.StandOffM;
        var roomFor = Cars.BlueLight[car] ? MayGetPast(car, cutBy, cutOn) : cutBy.GoesNowhere;
        if (!roomFor || !HasALaneToPassOn(car)) return standOffM;

        return MathF.Max(standOffM, RoomToStepOutM(car));
    }

    /// <summary>
    /// <b>How far short of what it passes a car's nose stands to step out round it from rest</b>, on the lane it is on:
    /// the lead of the shortest step it can drive from a standstill, and the pass's spare — or nothing where no step
    /// takes it off the lane. Drawn once a lane and a ground, which are all it reads that change.
    /// </summary>
    float RoomToStepOutM(int car)
    {
        var lane = Cars.LaneOf(car);
        var surface = Cars.GroundCoefficient[car];
        if (_roomLane[car] == lane && _roomSurface[car] == surface) return _roomM[car];

        ref readonly var build = ref Cars.BuildOf(car);
        var asideM = (Roads.LaneWidthM[lane] + Roads.LaneWidthM[Roads.LaneReverse[lane]]) * 0.5f;
        var corneringMps2 = CarFollower.CorneringMps2(Config, build, surface);
        var leadM = CarFollower.ShapeAPass(build, 0f, asideM, corneringMps2, 0f, out var stepM, out _)
            ? StepOutLeadM(build, asideM, stepM, Roads.LaneWidthM[lane] * 0.5f)
            : float.PositiveInfinity;

        _roomLane[car] = lane;
        _roomSurface[car] = surface;
        return _roomM[car] = float.IsPositiveInfinity(leadM)
            ? float.NegativeInfinity
            : leadM + Config.Driving.PassSpareM - build.NoseAheadOfAxleM;
    }

    static float[] Unset(int count)
    {
        var figures = new float[count];
        Array.Fill(figures, float.NaN);
        return figures;
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
