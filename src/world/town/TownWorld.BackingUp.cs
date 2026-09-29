using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A car backing up for the room to step out round what stands in its lane</b> (CAR-50, TER-4c.7): asked for
/// behind its tail at the weakest rung there is, driven in reverse down its own lane — and, not had, a car going
/// nowhere that whatever comes up behind it may get past in turn.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a car is ever too near</b>: it keeps room to step out only behind a body already going nowhere when it
/// comes up (<see cref="KeptOffM"/>). Anything else it may get past — a queue making another movement, a car backing
/// out of a bay, the head of a queue then stood down — is waited behind at the stand-off, nearer than any step out
/// from rest clears. A reservation has no across in it (TER-4c), so the whole of the body has to be off its own lane
/// before it comes level with what it passes, however little of the lane that takes up.
/// </para>
/// <para>
/// <b>Nothing is kept from one tick to the next</b>: how far a car means to back up and whether it is blocked are
/// worked out every tick from where it stands and the pass it asked for, and carried on its
/// <see cref="DriveContext"/> to the rebuild after — which lays the ground and says how much of it was had
/// (<see cref="CarFleet.BackRoomM"/>).
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How many pieces the ground a car backs up over is laid in: what it can no longer stop short of, and the rest.
    /// A bound on the table and not a figure behaviour reads.
    /// </summary>
    const int BackingPieces = 2;

    /// <summary>Car-ticks spent backing up for the room to step out (CAR-50), since the town was laid.</summary>
    public long CarTicksBackingUp { get; private set; }

    /// <summary>
    /// And car-ticks spent blocked: too near what it means to get past to step out, and refused the ground behind it.
    /// </summary>
    public long CarTicksBlocked { get; private set; }

    /// <summary>
    /// <b>How far this car means to back up this tick</b> (CAR-50): as much nearer than it could step out from rest as
    /// it stands to what it has decided to get past, and the pass's spare with it
    /// (<see cref="DrivingFigures.PassSpareM"/>) — zero where it is not too near.
    /// </summary>
    /// <remarks>
    /// <b>Whatever it passes</b>, a queue making another movement as much as a wreck: it has decided, and nothing but
    /// the room refuses the pass (<see cref="AskForAPass"/>).
    /// </remarks>
    /// <param name="tooNearByM">How much nearer than it could step out it stands, as the pass it asked for said.</param>
    float BackUpForM(float tooNearByM) => tooNearByM > 0f ? tooNearByM + _config.Driving.PassSpareM : 0f;

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
    void LayTheCarsBackUp(int car)
    {
        Cars.BackRoomM[car] = float.NaN;
        var askedM = Cars.Context[car].BackUpM;
        var backingMps = MathF.Max(0f, -Cars.AlongMps[car]);
        if (askedM <= 0f && backingMps <= _config.Driving.StopSpeedMps) return;
        if (!IsUnderWay(car) || Cars.LineWayOf(car) != CarFleet.NoWay || Cars.Line[car].LaneCount == 0) return;

        ref readonly var build = ref Cars.BuildOf(car);
        var lane = Cars.LaneOf(car);
        var tailM = Cars.ProgressM[car] - build.TailBehindAxleM - Cars.LaneStartsOf(car)[0];
        if (tailM <= 0f || tailM > _roads.LaneLengthM[lane])
        {
            Cars.BackRoomM[car] = 0f;
            return;
        }

        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);
        var committedM = (backingMps * _config.CarReactionS) + StoppingM(backingMps, brakingMps2);
        var wantedFromM = tailM - MathF.Max(askedM, committedM);
        var fromM = MathF.Max(0f, wantedFromM);
        var committedFromM = tailM - committedM;

        var hold = _occupancy.BeginHold(0f);
        if (hold == LaneOccupancy.NoHold) return;

        var way = _ways.OfRoadLane(lane);
        var ask = new PlannedAsk(
            hold, car, LaneRoster.Driving, ClaimPriority.Backing, fromM, Cars.LaneStartsOf(car)[0] + fromM, 0f,
            float.NegativeInfinity, -backingMps);
        var reachM = _occupancy.ReachBack(ask, way, fromM, tailM, committedFromM, _config.Driving.PassSpareM, out _);
        _occupancy.TakeBack(ask, way, reachM, tailM, committedFromM);
        _occupancy.EndHold(hold, float.PositiveInfinity, 0f, LaneClaim.Nothing);

        // Infinite where all of it was had, as a grant nothing cut is (<see cref="CarFleet.AuthorityM"/>): a length
        // carried back and forth would come home a float's grain short of what was asked.
        Cars.BackRoomM[car] = reachM > fromM || fromM > wantedFromM ? tailM - reachM : float.PositiveInfinity;
    }

    /// <summary>
    /// <b>This tick of a car backing up</b> (CAR-50): down its own line in reverse, the wheel pursuing it behind the
    /// car, and at rest by <paramref name="toGoM"/> behind where it is.
    /// </summary>
    /// <remarks>
    /// <b>The wheel is carried in the frame of the way the car is going</b>, which a reverse command gives out negated
    /// (<see cref="Reversed"/>): what the rack starts from is the wheel as it stands, whichever gear it was left in.
    /// </remarks>
    void DriveBack(
        int car, in CarBuild build, in CarPose pose, ReadOnlySpan<ArcSeg> line, float progressM, float toGoM,
        float groundCoefficient)
    {
        var travel = -pose.Forward;
        var alongMps = Vector2.Dot(pose.VelocityMps, travel);
        var rearAxleM = CarFollower.RearAxleM(build, pose.PositionM, pose.Forward);
        var lookaheadM = CarFollower.LookaheadM(build, MathF.Abs(alongMps), _config.Driving.LookaheadS);

        var wantedRad = CarFollower.Steer(build, line, progressM, rearAxleM, travel, -lookaheadM);
        var steerRad = build.WheelWoundTo(-Cars.Command[car].SteerRad, wantedRad, _config.TickSeconds);
        var lastMps2 = CarFollower.PedalMps2(Cars.Command[car]);
        var targetMps = CarFollower.BackingMps(_config, build, toGoM, alongMps, groundCoefficient, lastMps2);
        var pedals = CarFollower.Pedals(
            _config, build, steerRad, targetMps, alongMps, groundCoefficient, _config.TickSeconds, lastMps2);

        Cars.Command[car] = Reversed(pedals);
        Cars.Hold[car] = DrivingHold.BackingUp;
        CarTicksBackingUp++;
        Tyres(car, pose);
    }
}
