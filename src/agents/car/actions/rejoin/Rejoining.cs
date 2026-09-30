using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>Rejoin</b> (CAR-9): a car no longer driving its line stops and takes the lane it is actually standing on — the
/// driver's whole answer to being off its line, and the walker's own (PER-25): get back onto the network by the
/// shortest way there is. Done once the car is back on its line, which it then follows.
/// </summary>
/// <remarks>
/// <b>It claims nothing and is granted nothing</b> (TER-4c.8): it holds the car where it stands, and one that can be
/// given no lane at all covers no ground, which is what the leg's clock is for (CAR-15a). <b>It looks for the lane
/// under it on its own clock</b> (<see cref="DrivingFigures.RejoinLooksEveryS"/>): a car at rest stands on the same
/// ground from one tick to the next.
/// </remarks>
internal sealed class Rejoining(DrivingGround ground, CarActions actions, Following following)
{
    /// <summary>How long until a car at rest off its line looks for the lane under it again.</summary>
    readonly float[] _looksInS = new float[ground.Cars.Capacity];

    CarFleet Cars => ground.Cars;

    /// <summary>
    /// <b>This tick of a car off its line</b>: the line read again, and — back on it — followed from here; otherwise
    /// at rest where it stands (<see cref="LoseTheLine"/>).
    /// </summary>
    public void Tick<TTown>(ref TTown town, int car, in CarPose pose)
        where TTown : struct, ICarTown
    {
        if (!town.ReadTheLine(car, pose, out var progressM, out var alongMps, out var coveredM)) return;

        actions.Enter(car, CarAction.Follow);
        following.Tick(ref town, car, pose, progressM, alongMps, coveredM);
    }

    /// <summary>
    /// <b>A car found off its line</b> (CAR-10a): the junction it was coming to forgotten, the car held, and — at rest —
    /// the lane under it taken, where it stands on one pointing along it.
    /// </summary>
    public void LoseTheLine<TTown>(ref TTown town, int car, in CarPose pose, float alongMps, Vector2 rearAxleM)
        where TTown : struct, ICarTown
    {
        if (Cars.Action[car] != CarAction.Rejoin) _looksInS[car] = 0f;

        actions.Enter(car, CarAction.Rejoin);
        Cars.InsideTheBox[car] = false;
        Cars.LightAheadM[car] = float.PositiveInfinity;
        Cars.ToTheBoxM[car] = float.PositiveInfinity;
        Cars.TurningAtTheBox[car] = false;
        Cars.BoxIsOurs[car] = false;
        town.Hold(car, pose, DrivingHold.LostLine);
        if (MathF.Abs(alongMps) > ground.Config.Driving.StopSpeedMps) return;

        ref var inS = ref _looksInS[car];
        inS -= ground.Config.TickSeconds;
        if (inS > 0f) return;

        inS = ground.Config.Driving.RejoinLooksEveryS;
        if (town.Reacquire(car, rearAxleM)) actions.Enter(car, CarAction.Follow);
    }
}
