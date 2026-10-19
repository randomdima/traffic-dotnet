using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The railway, watched</b> (TRN-1…8, RAIL-1): one town run for a while, and what its trains and crossings did — how
/// many trains came, how long a train was on the map, how many of the crossings' stretches stood held at once, and what
/// the trains met.
/// </summary>
/// <remarks>
/// It gates nothing: whether a town's crossings are crossed by anybody while closed is a reading of that town's traffic,
/// and the rules a crossing keeps are the unit tier's (<c>TrainsTests</c>).
/// </remarks>
internal static class TrainProbe
{
    /// <summary>Ten minutes, which is several trains a line at the shipped gaps.</summary>
    const double DefaultSeconds = 600;

    public static void Run(string map, SimConfig config, double seconds)
    {
        var plan = Maps.Plan(map, config, World.Statics.BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        var ticks = (int)((seconds > 0 ? seconds : DefaultSeconds) * config.Sim.TickRateHz);
        var trains = world.Trains;
        var tracks = TrackLine.Of(plan.Rails);
        Console.WriteLine(
            $"train probe — {map}: {plan.Rails.Count} tracks, {trains.Count} trains, {trains.WagonCount} vehicle slots, " +
            $"{world.CrossingStretches} crossing stretches; {ticks / config.Sim.TickRateHz:F0} s");

        var (onTheMapTicks, mostWagons, mostHeld, carsMet, peopleMet, firstOnS) = (0, 0, 0, 0, 0, double.NaN);
        var (mostCarsHeld, mostWalkersHeld) = (0, 0);
        var met = new List<string>();
        var firstAtM = Vector2.Zero;
        var speeds = (Least: float.PositiveInfinity, Most: 0f, Short: 0f, Readings: 0);
        var worstOffM = 0f;
        for (var tick = 0; tick < ticks; tick++)
        {
            loop.Advance(1);
            var wagons = 0;
            foreach (ref readonly var wagon in world.World.View.Wagons)
            {
                if (!wagon.IsInWorld) continue;

                wagons++;
                if (double.IsNaN(firstOnS)) (firstOnS, firstAtM) = ((tick + 1) * config.TickSeconds, wagon.PositionM);
            }

            if (wagons > 0) onTheMapTicks++;
            mostWagons = Math.Max(mostWagons, wagons);
            mostHeld = Math.Max(mostHeld, world.CrossingStretchesHeld);
            if (tick % config.Sim.TickRateHz == 0)
            {
                var (carsHeld, walkersHeld) = world.HeldByACrossing;
                (mostCarsHeld, mostWalkersHeld) = (Math.Max(mostCarsHeld, carsHeld), Math.Max(mostWalkersHeld, walkersHeld));
                ReadTheTrains(world, ref speeds, ref worstOffM);
            }
            foreach (ref readonly var touch in world.World.View.TouchesBegun)
            {
                if (touch.First.Kind != BodyKind.Train && touch.Second.Kind != BodyKind.Train) continue;

                var (wagon, other) = touch.First.Kind == BodyKind.Train ? (touch.First, touch.Second) : (touch.Second, touch.First);
                if (other.Kind == BodyKind.Car) carsMet++;
                if (other.Kind == BodyKind.Person) peopleMet++;
                if (met.Count < MostMetShown && other.Kind is BodyKind.Car or BodyKind.Person)
                {
                    var atM = other.Kind == BodyKind.Car ? world.Cars.PositionM[other.Index] : world.People.PositionM[other.Index];
                    met.Add($"{other.Kind} {other.Index} at {atM.X:F0},{atM.Y:F0}, {(tick + 1) * config.TickSeconds:F1} s; {OnTheTrack(tracks, atM)}, " +
                            $"vehicle {wagon.Index}");
                }
            }
        }

        Console.WriteLine($"  trains sent        {trains.Sent}");
        Console.WriteLine($"  first wagon in     {(double.IsNaN(firstOnS) ? "never" : $"{firstOnS:F1} s at {firstAtM.X:F0},{firstAtM.Y:F0}")}");
        Console.WriteLine($"  a train on the map {onTheMapTicks * config.TickSeconds:F0} s, at most {mostWagons} wagons in the world");
        Console.WriteLine($"  stretches held     at most {mostHeld} at once");
        Console.WriteLine($"  held at a crossing at most {mostCarsHeld} cars and {mostWalkersHeld} walkers at once, read each second");
        Console.WriteLine($"  met by a train     {carsMet} cars, {peopleMet} walkers");
        foreach (var where in met) Console.WriteLine($"    {where}");
        Console.WriteLine(
            $"  locomotive speed   {speeds.Least:F2}–{speeds.Most:F2} m/s, short of its line's by {speeds.Short / Math.Max(1, speeds.Readings):F3} m/s on the mean " +
            $"({speeds.Readings} readings, each second)");
        Console.WriteLine($"  bogie off its line at most {worstOffM:F3} m, read each second");
    }

    /// <summary>
    /// <b>How each running train holds its line's speed and its vehicles their rails</b> (TRN-2, TRN-8): the locomotive's
    /// speed against its line's, and every bogie's pivot's distance off the track, as the world left them.
    /// </summary>
    static void ReadTheTrains(TownWorld world, ref (float Least, float Most, float Short, int Readings) speeds, ref float worstOffM)
    {
        var trains = world.Trains;
        var views = world.World.View.Wagons;
        for (var train = 0; train < trains.Count; train++)
        {
            var vehicles = trains.VehiclesOf(train);
            if (vehicles.Length == 0) continue;

            ref readonly var loco = ref views[vehicles[0]];
            var speedMps = loco.VelocityMps.Length();
            speeds = (MathF.Min(speeds.Least, speedMps), MathF.Max(speeds.Most, speedMps), speeds.Short + (trains.SpeedMps(train) - speedMps), speeds.Readings + 1);
            foreach (var vehicle in vehicles)
            {
                ref readonly var view = ref views[vehicle];
                var forward = new Vector2(MathF.Cos(view.HeadingRad), MathF.Sin(view.HeadingRad));
                foreach (var bogieM in trains.VehicleOf(vehicle).BogiesM)
                {
                    var pivotM = view.PositionM + (forward * bogieM);
                    var nearest = world.Rails.Nearest(pivotM);
                    worstOffM = MathF.Max(worstOffM, Vector2.Distance(world.Rails.Segments[nearest.Segment].PlaceAt(nearest.AlongM), pivotM));
                }
            }
        }
    }

    /// <summary>
    /// How far from the nearest track a place stands, the level the track is at there, and how far along it is to where
    /// that level changes — what tells a meeting on a level crossing from one beside the track or at a bridge's end.
    /// </summary>
    static string OnTheTrack(TrackLine[] tracks, Vector2 atM)
    {
        var (nearest, nearestAlongM, offM) = (0, 0f, float.PositiveInfinity);
        for (var track = 0; track < tracks.Length; track++)
        {
            var alongM = tracks[track].NearestAlongM(atM);
            var fromM = Vector2.Distance(tracks[track].PlaceAt(alongM), atM);
            if (fromM < offM) (nearest, nearestAlongM, offM) = (track, alongM, fromM);
        }

        var line = tracks[nearest];
        var level = line.LevelAt(nearestAlongM);
        var changeM = float.PositiveInfinity;
        for (var point = 1; point < line.AtM.Length; point++)
        {
            if (line.LevelAt(line.AtM[point]) != line.LevelAt(line.AtM[point - 1])) changeM = MathF.Min(changeM, MathF.Abs(line.AtM[point] - nearestAlongM));
        }

        return $"{offM:F1} m off track {nearest}, {(level == TownMap.RailArrays.AtGrade ? "at grade" : "bridge")}, level changes {changeM:F0} m along";
    }

    /// <summary>How many of what the trains met are said where.</summary>
    const int MostMetShown = 12;
}
