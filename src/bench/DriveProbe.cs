using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// What the town's cars are actually doing, printed <b>beside the width of the lane they are meant to
/// be in</b>: how fast they go, how far off their own line they hold, how much of the town is stopped,
/// and how many of them are on ground a car may not be on.
/// </summary>
/// <remarks>
/// <para>
/// It is here because a complaint that can only be argued about is a probe waiting to be written
/// "The cars look like they are cutting the corner" and "the cars are stuck" are both
/// measurements, and the second one especially: <b>a town where every car is stuck against a kerb is
/// very fast indeed</b>, so the tick figure this engine publishes is worth nothing without the census
/// beside it that says the town it ran was a town.
/// </para>
/// <para>
/// <b>Off-line is measured to the rear axle</b> (CAR-4a), because that is the point every line is drawn
/// for; measuring the middle of the body would report the crab as an error.
/// </para>
/// </remarks>
internal static class DriveProbe
{
    public const int WarmupTicks = 600;

    public const int MeasuredTicks = 1800;

    public static void Run(SimConfig config)
    {
        Console.WriteLine($"drive probe — {WarmupTicks} warm-up ticks, {MeasuredTicks} measured, {config.Solver.VelocityIterations} solver iterations");
        Console.WriteLine(
            $"{"map",-10}{"cars",6}{"driven",8}{"lanes",7}{"mean m/s",10}{"top m/s",9}{"off-line m",12}{"worst m",9}{"stopped",9}{"off road",10}" +
            $"{"corner",8}{"line end",10}{"headway",9}{"granted",9}{"waiting",9}{"crossing",10}{"manoeuvre",11}{"lost",7}" +
            $"{"covered m",11}{"stuck",7}{"arrived",9}");

        foreach (var map in ProjectPaths.ShippedMaps())
        {
            var sample = Sample(map, config);
            Console.WriteLine(
                $"{map,-10}{sample.Cars,6}{sample.Driven,8}{sample.Lanes,7}{sample.MeanSpeedMps,10:F2}{sample.TopSpeedMps,9:F2}" +
                $"{sample.MeanOffLineM,12:F3}{sample.WorstOffLineM,9:F2}" +
                $"{sample.StoppedShare,9:P0}{sample.OffRoadShare,10:P0}" +
                $"{sample.Held(DrivingHold.Corner),8:P0}{sample.Held(DrivingHold.LineEnd),10:P0}" +
                $"{sample.Held(DrivingHold.Headway),9:P0}{sample.Held(DrivingHold.Reserved),9:P0}" +
                $"{sample.Held(DrivingHold.Waiting),9:P0}" +
                $"{sample.Held(DrivingHold.Crossing),10:P0}{sample.Held(DrivingHold.Procedure),11:P0}" +
                $"{sample.Held(DrivingHold.LostLine),7:P0}{sample.CoveredM,11:F0}{sample.WentNowhere,7}{sample.Arrived,9}");
        }

        Console.WriteLine(
            $"A lane's own half-width is {config.LaneOffsetM:F2} m: a car holding its line to well inside that is a car in its lane.");
    }

    public readonly record struct DriveSample(
        int Cars, int Driven, int Lanes, double MeanSpeedMps, double TopSpeedMps, double MeanOffLineM, double WorstOffLineM,
        double StoppedShare, double OffRoadShare, long[] Holds, long Samples, double CoveredM, int WentNowhere,
        long Arrived)
    {
        /// <summary>The share of car-ticks this was the term that decided the speed.</summary>
        public double Held(DrivingHold hold) => Samples == 0 ? 0 : Holds[(int)hold] / (double)Samples;
    }

    static int DrivenCount(TrafficSimulation.Agents.Car.Body.CarFleet cars)
    {
        var driven = 0;
        for (var car = 0; car < cars.Count; car++)
        {
            if (cars.Driven[car]) driven++;
        }

        return driven;
    }

    public static DriveSample Sample(string map, SimConfig config)
    {
        var plan = TownReader.ReadFile(ProjectPaths.TownFile(map));
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        var cars = world.Cars;
        var arrivedBefore = world.BaysParkedIn;
        var samples = 0L;
        var stopped = 0L;
        var offRoad = 0L;
        var speedSum = 0.0;
        var topMps = 0.0;
        var offLineSum = 0.0;
        var worstOffLineM = 0.0;
        var holds = new long[Enum.GetValues<DrivingHold>().Length];
        var startedAtM = new Vector2[cars.Count];
        var coveredM = new float[cars.Count];
        for (var car = 0; car < cars.Count; car++) startedAtM[car] = cars.PositionM[car];
        var wasAtM = (Vector2[])startedAtM.Clone();

        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance();
            for (var car = 0; car < cars.Count; car++)
            {
                if (!cars.Driven[car]) continue;

                var forward = new Vector2(MathF.Cos(cars.HeadingRad[car]), MathF.Sin(cars.HeadingRad[car]));
                var alongMps = Math.Abs(Vector2.Dot(cars.VelocityMps[car], forward));
                var rearAxleM = CarFollower.RearAxleM(
                    cars.BuildOf(car), cars.PositionM[car], cars.HeadingRad[car]);

                // The driver's own reading and not a second one taken beside it: a probe that worked this
                // out for itself would report its own arithmetic rather than the figure the car acted on.
                var offLineM = cars.Line[car].ArcCount == 0 ? 0f : cars.OffLineM[car];

                samples++;
                holds[(int)cars.Hold[car]]++;
                coveredM[car] += (cars.PositionM[car] - wasAtM[car]).Length();
                wasAtM[car] = cars.PositionM[car];
                speedSum += alongMps;
                topMps = Math.Max(topMps, alongMps);
                offLineSum += offLineM;
                worstOffLineM = Math.Max(worstOffLineM, offLineM);
                if (alongMps <= config.Driving.StopSpeedMps) stopped++;
                if (!world.Terrain.At(rearAxleM).Drivable) offRoad++;
            }
        }

        var wentNowhere = 0;
        var coveredSum = 0.0;
        for (var car = 0; car < cars.Count; car++)
        {
            if (!cars.Driven[car]) continue;

            coveredSum += coveredM[car];
            if (coveredM[car] < config.Car.LengthM) wentNowhere++;
        }

        var per = Math.Max(1L, samples);
        return new DriveSample(
            cars.Count, DrivenCount(cars), world.Roads.LaneCount, speedSum / per, topMps, offLineSum / per, worstOffLineM,
            stopped / (double)per, offRoad / (double)per, holds, samples,
            DrivenCount(cars) == 0 ? 0 : coveredSum / DrivenCount(cars), wentNowhere,
            world.BaysParkedIn - arrivedBefore);
    }
}
