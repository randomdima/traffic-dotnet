using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The railway, watched</b> (TRN-3…12, RAIL-1): one town run for a while, and what its trains and crossings did — how
/// many trains came, how long a train was on the map, how many of the crossings' stretches stood held at once, and what
/// the trains and the crossings' arms met.
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
        var plan = Maps.Plan(map, config, World.Statics.ShippedArt.Sizes);
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
        var (armsMet, armsSaid) = (0, new List<string>());
        var firstAtM = Vector2.Zero;
        var watch = new RailWatch(world, config);
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
            }

            watch.Saw(world);
            foreach (ref readonly var touch in world.World.View.TouchesBegun)
            {
                if (touch.First.Kind == BodyKind.Barrier || touch.Second.Kind == BodyKind.Barrier)
                {
                    var (arm, car) = touch.First.Kind == BodyKind.Barrier ? (touch.First, touch.Second) : (touch.Second, touch.First);
                    armsMet++;
                    if (armsSaid.Count < MostMetShown)
                    {
                        var atM = world.Cars.PositionM[car.Index];
                        armsSaid.Add(
                            $"car {car.Index} at {atM.X:F0},{atM.Y:F0}, {world.Cars.VelocityMps[car.Index].Length():F1} m/s, {(tick + 1) * config.TickSeconds:F1} s; " +
                            $"gear {arm.Index}, {world.CrossingGear.Lowered(arm.Index, world.ElapsedS):P0} down");
                    }

                    continue;
                }

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
        Console.WriteLine($"  met by an arm      {armsMet} cars (RAIL-10)");
        foreach (var where in armsSaid) Console.WriteLine($"    {where}");
        var said = new TextBuffer(new char[256]);
        for (var claim = 0; claim < watch.Claims; claim++)
        {
            said.Clear();
            watch.Says(claim, ref said);
            Console.WriteLine($"  {watch.Asks(claim)}: {watch.Verdict(claim)} — {said.Written}");
        }

        for (var reading = 0; reading < watch.Readings; reading++)
        {
            said.Clear();
            watch.Reads(reading, ref said);
            Console.WriteLine($"  {watch.Reading(reading)}: {said.Written}");
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
