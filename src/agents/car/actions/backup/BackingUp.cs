using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>Back up</b> (CAR-50, TER-4c.7): a car standing past where the step out of the pass it decided begins, backing
/// down its own lane in reverse to there over ground asked for behind its tail at the weakest rung there is — and,
/// refused that ground, blocked: a car going nowhere that whatever comes up behind it may get past in turn, asking again
/// on the pass's clock (<see cref="DrivingFigures.PassAskEveryS"/>). Over once it is at rest with the room made, or
/// with nothing left to make room for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a car is ever too near</b>: it keeps room to step out only behind a body already going nowhere when it
/// comes up (<see cref="Overtaking.KeptOffM"/>). Anything else it may get past — a queue making another movement, a
/// car backing out of a bay, the head of a queue then stood down — is waited behind at the stand-off, nearer than any
/// step out from rest clears. A reservation has no across in it (TER-4c), so the whole of the body has to be off its
/// own lane before it comes level with what it passes, however little of the lane that takes up.
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
    /// <summary>
    /// How many pieces the ground a car backs up over is laid in: what it can no longer stop short of, and the rest.
    /// A bound on the table and not a figure behaviour reads.
    /// </summary>
    public const int BackingPieces = 2;

    CarFleet Cars => ground.Cars;

    SimConfig Config => ground.Config;

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
        // Looked round on the pass's clock: what it passes gone, or the room past it held by something it may not pass,
        // and there is nothing to back up for.
        var looks = overtaking.IsTimeToLook(car);
        var stillThere = !looks || overtaking.IsStillThere(car);
        var roomHeld = looks && stillThere && overtaking.IsTheRoomHeld(car);
        var backUpM = stillThere && !roomHeld && overtaking.IsTooNear(car, progressM)
            ? progressM - overtaking.StepsOutAtM(car)
            : 0f;
        var backsUp = backUpM > 0f && Cars.BackRoomM[car] >= backUpM;
        var blocked = backUpM > 0f && !backsUp;
        if (blocked) CarTicksBlocked++;

        // Asked for every rebuild while it rolls back, and on its clock while it is refused.
        var asksM = backsUp || (blocked && looks) ? backUpM : 0f;

        // A car that was backing up and has no more of it to do comes to rest in the gear it is in.
        var stillRollingBack = alongMps < -Config.Driving.StopSpeedMps;
        if (backsUp || stillRollingBack)
        {
            town.SetTheContext(car, progressM, coveredM, stillThere, overtaking.AsideOfItsPassM(car), asksM, blocked);
            DriveBack(ref town, car, Cars.BuildOf(car), pose, Cars.LineOf(car), progressM, backsUp ? backUpM : 0f,
                Cars.GroundCoefficient[car]);
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
    /// <b>The ground this car means to back up over, asked for and laid</b> (TER-4c.7) behind its tail down the lane it
    /// is on, and how much of it was had — after every plan is settled, since it is weaker than all of them but the
    /// plan of somebody queued behind it, which it cuts short.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Kept off the body behind it by the pass's spare</b> (<see cref="DrivingFigures.PassSpareM"/>): the ground is
    /// what it backs up over, and what it may stray backing up is the same room as what it may stray stepping out.
    /// </para>
    /// <para>
    /// <b>What it asked for, or what it can no longer stop short of if that is more</b>: a car rolling back holds the
    /// ground its stop needs whatever it decided, and that ground nothing takes (TER-5e).
    /// </para>
    /// <para>
    /// <b>Its own lane and no other</b>: behind the start of the lane is the box the car came through, which it never
    /// backs into, and a tail already over the lane's end has nothing of the lane behind it.
    /// </para>
    /// </remarks>
    /// <param name="underWay">Whether the car is on the route's line, as the town reads it.</param>
    public void Lay(int car, bool underWay)
    {
        Cars.BackRoomM[car] = float.NaN;
        if (Cars.Action[car] != CarAction.BackUp) return;

        var askedM = Cars.Context[car].BackUpM;
        var backingMps = MathF.Max(0f, -Cars.AlongMps[car]);
        if (askedM <= 0f && backingMps <= Config.Driving.StopSpeedMps) return;
        if (!underWay) return;

        ref readonly var build = ref Cars.BuildOf(car);
        var lane = Cars.LaneOf(car);
        var tailM = Cars.ProgressM[car] - build.TailBehindAxleM - Cars.LaneStartsOf(car)[0];
        if (tailM <= 0f || tailM > ground.Roads.LaneLengthM[lane])
        {
            Cars.BackRoomM[car] = 0f;
            return;
        }

        var brakingMps2 = CarFollower.BrakingMps2(Config, build, Cars.GroundCoefficient[car]);
        var committedM = (backingMps * Config.CarReactionS) + DrivingGround.StoppingM(backingMps, brakingMps2);
        var wantedFromM = tailM - MathF.Max(askedM, committedM);
        var fromM = MathF.Max(0f, wantedFromM);
        var committedFromM = tailM - committedM;

        var occupancy = ground.Occupancy;
        var hold = occupancy.BeginHold(0f);
        if (hold == LaneOccupancy.NoHold) return;

        var way = ground.Ways.OfRoadLane(lane);
        var ask = new PlannedAsk(
            hold, car, LaneRoster.Driving, ClaimPriority.Backing, fromM, Cars.LaneStartsOf(car)[0] + fromM, 0f,
            float.NegativeInfinity, -backingMps);
        var reachM = occupancy.ReachBack(ask, way, fromM, tailM, committedFromM, Config.Driving.PassSpareM, out _);
        occupancy.TakeBack(ask, way, reachM, tailM, committedFromM);
        occupancy.EndHold(hold, float.PositiveInfinity, 0f, LaneClaim.Nothing);

        // Infinite where all of it was had, as a grant nothing cut is (<see cref="CarFleet.AuthorityM"/>): a length
        // carried back and forth would come home a float's grain short of what was asked.
        Cars.BackRoomM[car] = reachM > fromM || fromM > wantedFromM ? tailM - reachM : float.PositiveInfinity;
    }

    /// <summary>
    /// <b>A car driven backing up</b> (CAR-50): down its own line in reverse, the wheel pursuing it behind the car, and
    /// at rest by <paramref name="toGoM"/> behind where it is.
    /// </summary>
    /// <remarks>
    /// <b>The wheel is carried in the frame of the way the car is going</b>, which a reverse command gives out negated:
    /// what the rack starts from is the wheel as it stands, whichever gear it was left in.
    /// </remarks>
    void DriveBack<TTown>(
        ref TTown town, int car, in CarBuild build, in CarPose pose, ReadOnlySpan<ArcSeg> line, float progressM,
        float toGoM, float groundCoefficient)
        where TTown : struct, ICarTown
    {
        var travel = -pose.Forward;
        var alongMps = System.Numerics.Vector2.Dot(pose.VelocityMps, travel);
        var rearAxleM = CarFollower.RearAxleM(build, pose.PositionM, pose.Forward);
        var lookaheadM = CarFollower.LookaheadM(build, MathF.Abs(alongMps), Config.Driving.LookaheadS);

        var wantedRad = CarFollower.Steer(build, line, progressM, rearAxleM, travel, -lookaheadM);
        var steerRad = build.WheelWoundTo(-Cars.Command[car].SteerRad, wantedRad, Config.TickSeconds);
        var lastMps2 = CarFollower.PedalMps2(Cars.Command[car]);
        var targetMps = CarFollower.BackingMps(Config, build, toGoM, alongMps, groundCoefficient, lastMps2);
        var pedals = CarFollower.Pedals(
            Config, build, steerRad, targetMps, alongMps, groundCoefficient, Config.TickSeconds, lastMps2);

        Cars.Command[car] = pedals with { SteerRad = -pedals.SteerRad, Reverse = true };
        Cars.Hold[car] = DrivingHold.BackingUp;
        CarTicksBackingUp++;
        town.Tyres(car, pose);
    }
}
