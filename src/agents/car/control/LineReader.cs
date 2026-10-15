using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>Where a car's tick found it on its line (<see cref="LineReader.Advance"/>).</summary>
/// <param name="ShiftM">How far the line's metres moved back where the car passed into its next lane, or nought.</param>
internal readonly record struct LineAdvance(float ProgressM, float CoveredM, float ShiftM)
{
    public bool Shifted => ShiftM > 0f;
}

/// <summary>
/// <b>The route's line under a car, read on its tick</b>: where along it the rear axle is, the lane it has left shifted
/// off it, and whether what is left has run short of the car's sight. Arithmetic over the fleet's own line and nothing
/// else, so a tick reads every car at once.
/// </summary>
internal static class LineReader
{
    /// <summary>
    /// Where along its line the car is now (CAR-4a), <b>the line shifted on past the lane the axle has left</b>
    /// (<see cref="CarFleet.ShiftTheLine"/>) and the car found again on what is left of it.
    /// </summary>
    /// <remarks>
    /// <b>Never onto a lane the line lays none of</b>: a leg that stops at the very first metre of its last lane — a
    /// place drawn at a node, a turn-in that begins where the lane does — ends where that lane begins, and a car handed
    /// over onto it there was a line laid again at its own stop, past it by its own nose, and sent round.
    /// </remarks>
    /// <param name="readM">
    /// Where the world read the car along this very line (<c>LineFollowed</c>), which is the projection's own answer to the
    /// bit — or NaN, where the car is projected onto it here.
    /// </param>
    public static LineAdvance Advance(CarFleet cars, int car, Vector2 rearAxleM, float readM)
    {
        ref readonly var build = ref cars.BuildOf(car);
        var lastM = cars.ProgressM[car];
        float progressM;
        if (float.IsNaN(readM))
        {
            progressM = CarFollower.ProgressM(build, cars.LineOf(car), rearAxleM, lastM, ref cars.LineCursor[car]);
        }
        else
        {
            progressM = readM;
            Spline.MoveTheCursor(cars.LineOf(car), lastM, build.ProjectionWindowM, ref cars.LineCursor[car]);
        }

        var coveredM = MathF.Abs(progressM - lastM);

        var line = cars.Line[car];
        var nextLaneStartsM = cars.LaneStartsOf(car)[1];
        if (line.LaneCount < 2 || progressM < nextLaneStartsM || nextLaneStartsM >= line.LengthM - LineTolerance.RoundingM)
        {
            return new LineAdvance(progressM, coveredM, 0f);
        }

        var shiftM = cars.ShiftTheLine(car);
        progressM = CarFollower.ProgressM(
            build, cars.LineOf(car), rearAxleM, MathF.Max(0f, progressM - shiftM), ref cars.LineCursor[car]);
        return new LineAdvance(progressM, coveredM, shiftM);
    }

    /// <summary>
    /// <b>Whether the line has run short of the car's sight</b>, and could be grown — what its next decision lays the rest of,
    /// where road runs on past its end. A line ending where the leg does is not grown, and neither is one already as long
    /// as a line is laid.
    /// </summary>
    public static bool RunsShort(CarFleet cars, int car, float progressM) =>
        cars.Line[car].LengthM - progressM < cars.BuildOf(car).SightM
        && cars.Line[car].LaneCount < LineAssembler.MostLanes
        && !EndsWhereTheLegDoes(cars, car);

    /// <summary>Whether the line ends where the leg does — at the bay it stops for, or the place it was sent to.</summary>
    public static bool EndsWhereTheLegDoes(CarFleet cars, int car) =>
        cars.StopsForBayOf(car) != CarFleet.NoBay || cars.StopsAtItsPlaceOf(car);
}
