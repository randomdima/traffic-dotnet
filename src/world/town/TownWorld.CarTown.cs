using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The town's side of every car action</b> (<see cref="ICarTown"/>): the actions decide, and what they ask of the
/// route's line, the standing rules, the tyres and the leg is carried out here.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>The town as an action asks things of it, handed to one by reference so every call is bound when it compiles.</summary>
    readonly struct CarTown(TownWorld town) : ICarTown
    {
        public bool ReadTheLine(int car, in CarPose pose, out float progressM, out float alongMps, out float coveredM) =>
            town.ReadTheLine(car, pose, out progressM, out alongMps, out coveredM);

        public void DriveOnTheLine(
            int car, in CarPose pose, float progressM, float alongMps, float coveredM, bool waitsToPass = false,
            float passAsideM = 0f, float backUpM = 0f, bool blocked = false, float stepOutM = float.PositiveInfinity) =>
            town.DriveOnTheLine(
                car, pose, progressM, alongMps, coveredM, waitsToPass, passAsideM, backUpM, blocked, stepOutM);

        public DriveContext SetTheContext(
            int car, float progressM, float coveredM, bool waitsToPass, float passAsideM, float backUpM, bool blocked,
            float stepOutM = float.PositiveInfinity) =>
            town.SetTheContext(car, progressM, coveredM, waitsToPass, passAsideM, backUpM, blocked, stepOutM);

        public void Drive(
            int car, in CarBuild build, in CarPose pose, ReadOnlySpan<ArcSeg> line, float progressM, float lengthM,
            in DriveContext context, Vector2 travel, float alongMps, bool reverse) =>
            town.Drive(car, build, pose, line, progressM, lengthM, context, travel, alongMps, reverse);

        public void Hold(int car, in CarPose pose, DrivingHold why) => town.Hold(car, pose, why);

        public void Tyres(int car, in CarPose pose) => town.Tyres(car, pose);

        public float ToTheSceneM(int car) => town.ToTheSceneM(car);

        public float OffTheLineAllowanceM(int car) => town.OffTheLineAllowanceM(car);

        public float GroundCoefficientAt(Vector2 pointM) => town._terrain.At(pointM).Coefficient;

        public void ParkIt(int car, int bay) => town.ParkIt(car, bay);

        public bool TakeTheRoad(int car) => town.TakeTheRoad(car);

        public bool Reacquire(int car, Vector2 rearAxleM) => town.Reacquire(car, rearAxleM);
    }
}
