using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>What the town does for a car's action that only the town can</b>: read the route's line under the car and grow
/// it, drive the car by the standing rules (S-1…S-5) and spend what that asks of its tyres, and take the leg's next
/// step when an action is done with the road or the bay. An action decides; this carries it out.
/// </summary>
/// <remarks>
/// <b>Implemented by a struct and taken as a type argument</b>, so every call is bound when the action is compiled
/// for it and none is an interface call on the tick (goals rule 2).
/// </remarks>
internal interface ICarTown
{
    /// <summary>
    /// <b>The route's line under the car, read</b>: where along it the body is, the lanes it has passed shifted off the
    /// chain and the chain grown to its sight. False where the car is off it past what it may be (CAR-10a), which has
    /// handed it to getting back onto its line.
    /// </summary>
    bool ReadTheLine(int car, in CarPose pose, out float progressM, out float alongMps, out float coveredM);

    /// <summary>
    /// <b>A car driven forwards down the route's own line</b>: the junction ahead of it, the paint across it and what the
    /// claims say is down it, with whatever its action says about a pass it is waiting for or the room it means to back
    /// up for.
    /// </summary>
    void DriveOnTheLine(
        int car, in CarPose pose, float progressM, float alongMps, float coveredM, bool waitsToPass = false,
        float passAsideM = 0f, float backUpM = 0f, bool blocked = false, float waitAtM = float.PositiveInfinity);

    /// <summary>What a car on the route's line is told about the world this tick, and the junction ahead of it read.</summary>
    DriveContext SetTheContext(
        int car, float progressM, float coveredM, bool waitsToPass, float passAsideM, float backUpM, bool blocked,
        float waitAtM = float.PositiveInfinity);

    /// <summary>S-1, S-2 and S-5 down any line the car is on, in the gear given: the wheel and the pedals.</summary>
    void Drive(
        int car, in CarBuild build, in CarPose pose, ReadOnlySpan<ArcSeg> line, float progressM, float lengthM,
        in DriveContext context, Vector2 travel, float alongMps, bool reverse);

    /// <summary>A car that is doing nothing this tick, and the one reason it is not.</summary>
    void Hold(int car, in CarPose pose, DrivingHold why);

    /// <summary>The command a car was left with, spent on its four patches.</summary>
    void Tyres(int car, in CarPose pose);

    /// <summary>
    /// <b>How far ahead along the line the place this car was sent to stands</b> — a casualty, a wreck, a scene, a place
    /// a hand named — or infinity.
    /// </summary>
    float ToTheSceneM(int car);

    /// <summary>How far off its line a car may be before the road calls the line lost (CAR-10a).</summary>
    float OffTheLineAllowanceM(int car);

    /// <summary>The ground a car is on, as a coefficient of friction.</summary>
    float GroundCoefficientAt(Vector2 pointM);

    /// <summary>The car in its bay and its leg over: the place its own until something drives it away, and the car stood down in it.</summary>
    void ParkIt(int car, int bay);

    /// <summary>
    /// <b>The road taken from where the car stands</b>: the lane under it followed, or — with no lane under it to take —
    /// the car off its line, at rest until it has one (CAR-9). True where it had a lane.
    /// </summary>
    bool TakeTheRoad(int car);

    /// <summary>
    /// <b>The lane a car at rest off its line is standing on, taken</b> where it stands on one within reach pointing
    /// along it — true where a line was laid over it, false where there is none or it is the line the car already has.
    /// </summary>
    bool Reacquire(int car, Vector2 rearAxleM);
}
