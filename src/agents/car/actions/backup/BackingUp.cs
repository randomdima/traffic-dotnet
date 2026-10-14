using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>The ground one car asks to back up over</b> (<see cref="BackingUp.Asks"/>): down <paramref name="Way"/> from
/// <paramref name="FromM"/> up to its tail, all of it its own lane — what it laid of that being how much was had
/// (<see cref="BackingUp.Had"/>).
/// </summary>
/// <param name="WantedFromM">Where it wanted the ground from, before the lane's start held it to <paramref name="FromM"/>.</param>
/// <param name="CommittedFromM">Where the ground it can no longer stop short of begins, below <paramref name="TailM"/>.</param>
/// <param name="LineFromM">Where <paramref name="FromM"/> falls on its line.</param>
internal readonly record struct BackingAsk(
    int Car, int Way, float FromM, float WantedFromM, float TailM, float CommittedFromM, float LineFromM);

/// <summary>
/// <b>Back up</b> (CAR-50, TER-4c.7): a car standing past where the step out of the pass it decided begins, backing
/// down its own lane in reverse to there over ground asked for behind its tail at the weakest rung there is — and,
/// refused that ground, blocked: standing where it is, asking again
/// on the pass's clock (<see cref="DrivingFigures.PassAskEveryS"/>). Over once it is at rest with the room made, or
/// with nothing left to make room for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a car is ever too near</b>: whatever it means to get past is waited behind at the stand-off, nearer than any
/// step out from rest clears. A claim has no across in it (TER-4c), so the whole of the body has to be off its own lane
/// before it comes level with what it passes, however little of the lane that takes up.
/// </para>
/// <para>
/// <b>Where it backs up to is the pass's, decided once</b> (<see cref="Overtaking.StepsOutAtM"/>): the car backs up
/// for the pass it has and never draws another, and the pass is asked for by the overtake once the car is at rest there.
/// </para>
/// <para>
/// <b>What it asks for rides on its <see cref="DriveContext"/> to the rebuild after</b> — which lays the ground and
/// says how much of it was had (<see cref="CarFleet.BackRoomM"/>): every rebuild while it rolls back, and when its
/// clock comes round while it is blocked.
/// </para>
/// </remarks>
internal sealed class BackingUp(DrivingGround ground, CarActions actions, Overtaking overtaking)
{

    CarFleet Cars => ground.Cars;

    SimConfig Config => ground.Config;

    /// <summary>The most ground behind it each car has asked for since the claims were last laid, which laying them reads.</summary>
    readonly float[] _askedM = new float[ground.Cars.Capacity];

    /// <summary>Car-ticks spent backing up for the room to step out (CAR-50), since the town was laid.</summary>
    public long CarTicksBackingUp { get; private set; }

    /// <summary>
    /// And car-ticks spent blocked: too near what it means to get past to step out, and refused the ground behind it.
    /// </summary>
    public long CarTicksBlocked { get; private set; }

    /// <summary>
    /// <b>This tick of a car backing up</b>, once the line under it has been read: down its own lane in reverse to
    /// where its pass's step out begins, and blocked where the ground behind it was refused — then, at rest, handed back
    /// to the overtake with the room made, or to following once what it passes has gone.
    /// </summary>
    public void Tick<TTown>(ref TTown town, int car, in CarPose pose, float progressM, float alongMps, float coveredM)
        where TTown : struct, ICarTown
    {
        // Looked round on the pass's clock, which the car's decision runs: what it passes gone, or the room past it held
        // by something it may not pass, and there is nothing to back up for.
        var looks = overtaking.TakeTheLook(car);
        var stillThere = !looks || overtaking.IsStillThere(car);
        var roomHeld = looks && stillThere && overtaking.IsTheRoomHeld(car);
        var backUpM = stillThere && !roomHeld && overtaking.IsTooNear(car, progressM)
            ? progressM - overtaking.StepsOutAtM(car)
            : 0f;
        var backsUp = backUpM > 0f && Cars.BackRoomM[car] >= backUpM;
        var blocked = backUpM > 0f && !backsUp;
        if (blocked) CarTicksBlocked++;

        // Asked for every rebuild while it rolls back, and on its clock while it is refused — and kept until the claims
        // are next laid, which is not every tick.
        var asksM = backsUp || (blocked && looks) ? backUpM : 0f;
        _askedM[car] = MathF.Max(_askedM[car], asksM);

        // A car that was backing up and has no more of it to do comes to rest in the gear it is in.
        var stillRollingBack = alongMps < -Config.Driving.StopSpeedMps;
        if (backsUp || stillRollingBack)
        {
            var context = town.SetTheContext(car, progressM, coveredM, stillThere, overtaking.AsideOfItsPassM(car), asksM, blocked);
            var travel = -pose.Forward;
            town.Drive(
                car, Cars.BuildOf(car), pose, Cars.LineOf(car), progressM, Cars.Line[car].LengthM, context, travel,
                System.Numerics.Vector2.Dot(pose.VelocityMps, travel), reverse: true, backsUpByM: backsUp ? backUpM : 0f);
            CarTicksBackingUp++;
            return;
        }

        if (!stillThere)
        {
            actions.Enter(car, CarAction.Follow);
            town.DriveOnTheLine(car, pose, progressM, alongMps, coveredM);
            return;
        }

        if (!blocked)
        {
            actions.Enter(car, CarAction.Overtake);
            overtaking.LookNow(car);
        }

        overtaking.WaitAtTheStep(ref town, car, pose, progressM, alongMps, coveredM, asksM, blocked);
    }

    /// <summary>
    /// <b>The ground this car means to back up over, asked for</b> (TER-4c.7) behind its tail down the lane it is on — at
    /// the weakest rung there is, so every plan keeps what it holds of it. How much was had is read once the plans are
    /// answered (<see cref="Read"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it asked for, or what it can no longer stop short of if that is more</b>: a car rolling back holds the
    /// ground its stop needs whatever it decided, at <see cref="ClaimPriority.Committed"/> (TER-5e).
    /// </para>
    /// <para>
    /// <b>Its own lane and no other</b>: behind the start of the lane is the box the car came through, which it never
    /// backs into, and a tail already over the lane's end has nothing of the lane behind it.
    /// </para>
    /// </remarks>
    /// <param name="underWay">Whether the car is on the route's line, as the town reads it.</param>
    public void Lay(int car, bool underWay)
    {
        var occupancy = ground.Occupancy;
        occupancy.Release(_holds[car]);
        _holds[car] = LaneOccupancy.NoHold;
        if (!TakeTheAsk(car, underWay, out var asked)) return;

        var hold = _holds[car] = occupancy.BeginHold(car, LaneRoster.Driving);
        _laid[car] = asked;
        var splitM = Math.Clamp(asked.CommittedFromM, asked.FromM, asked.TailM);
        occupancy.AskDown(hold, asked.Way, splitM, asked.TailM, 0f, ClaimPriority.Committed);
        occupancy.AskDown(hold, asked.Way, asked.FromM, splitM, asked.TailM - splitM, ClaimPriority.Backing);
    }

    /// <summary>
    /// <b>How much of the ground behind it a car backing up had</b>, once the plans are answered: as far down as what
    /// refused it — <b>kept off a body there by the pass's spare</b> (<see cref="DrivingFigures.PassSpareM"/>), since what
    /// it may stray backing up is the same room as what it may stray stepping out.
    /// </summary>
    public void Read(int car)
    {
        var hold = _holds[car];
        if (hold == LaneOccupancy.NoHold) return;

        ref readonly var asked = ref _laid[car];
        var cut = ground.Occupancy.CutOf(hold);
        var reachM = !cut.Refused ? asked.FromM
            : cut.Kind == ClaimKind.Body ? MathF.Min(asked.TailM, cut.AtM + Config.Driving.PassSpareM)
            : cut.AtM;
        Had(asked, reachM);
    }

    /// <summary>The hold each car's backing was asked in this rebuild, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    readonly int[] _holds = LaneOccupancy.NoHolds(ground.Cars.Capacity);

    /// <summary>And what it asked, which <see cref="Read"/> reads back.</summary>
    readonly BackingAsk[] _laid = new BackingAsk[ground.Cars.Capacity];

    /// <summary>
    /// <b>The ground this car asks to back up over this laying, taken</b> — what it asked for since the claims were last
    /// laid cleared, and what it was had left unknown until the ground is laid (<see cref="Had"/>), or nought where the lane
    /// has none of it behind the car. False where it asks for none.
    /// </summary>
    /// <param name="underWay">Whether the car is on the route's line, as the town reads it.</param>
    public bool TakeTheAsk(int car, bool underWay, out BackingAsk ask)
    {
        Cars.BackRoomM[car] = float.NaN;
        var asks = Asks(car, underWay, out ask, out var noneBehind);
        _askedM[car] = 0f;
        if (noneBehind) Cars.BackRoomM[car] = 0f;

        return asks;
    }

    /// <summary>The ground this car would ask to back up over, were the claims laid now — read and never written.</summary>
    /// <param name="underWay">Whether the car is on the route's line, as the town reads it.</param>
    public bool Asks(int car, bool underWay, out BackingAsk ask) => Asks(car, underWay, out ask, out _);

    bool Asks(int car, bool underWay, out BackingAsk ask, out bool noneBehind)
    {
        ask = default;
        noneBehind = false;
        if (Cars.Action[car] != CarAction.BackUp) return false;

        var askedM = _askedM[car];
        var backingMps = MathF.Max(0f, -Cars.AlongMps[car]);
        if (askedM <= 0f && backingMps <= Config.Driving.StopSpeedMps) return false;
        if (!underWay) return false;

        ref readonly var build = ref Cars.BuildOf(car);
        var lane = Cars.LaneOf(car);
        var laneStartM = Cars.LaneStartsOf(car)[0];
        var tailM = Cars.ProgressM[car] - build.TailBehindAxleM - laneStartM;
        if (tailM <= 0f || tailM > ground.Roads.LaneLengthM[lane])
        {
            noneBehind = true;
            return false;
        }

        var committedM = CarFollower.SlowsOverM(build, backingMps);
        var wantedFromM = tailM - MathF.Max(askedM, committedM);
        var fromM = MathF.Max(0f, wantedFromM);
        ask = new BackingAsk(car, ground.Ways.OfRoadLane(lane), fromM, wantedFromM, tailM, tailM - committedM, laneStartM + fromM);
        return true;
    }

    /// <summary>How much of the ground a car asked to back up over it was had, once the ground is laid as far back as <paramref name="reachM"/>.</summary>
    public void Had(in BackingAsk ask, float reachM)
    {
        // Infinite where all of it was had, as a grant nothing cut is (<see cref="CarFleet.AuthorityM"/>): a length
        // carried back and forth would come home a float's grain short of what was asked.
        Cars.BackRoomM[ask.Car] = reachM > ask.FromM || ask.FromM > ask.WantedFromM ? ask.TailM - reachM : float.PositiveInfinity;
    }
}
