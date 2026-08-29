using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The tier-2 soak stage 4 exits on</b>: a town left to live, and then the one question VER-8 asks
/// of it — <em>does a whole trip complete, end to end, unattended and repeatedly</em>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every column is the count of one thing actually happening</b>, and they are printed together
/// because no one of them means anything alone: doors entered without boardings is a town nobody
/// drives in, boardings without bays parked in is a town whose cars set off and never arrive, and
/// trips drawn without either is a town of people walking about with a destination.
/// </para>
/// <para>
/// <b>The trips given up are printed beside them rather than hidden.</b> A leg that fails here is
/// drawn again rather than walked down an escalation ladder — the ladder is M8 — so the ratio of the
/// two is the honest measure of how much of that absence the town is paying for.
/// </para>
/// </remarks>
internal static class TripProbe
{
    public const int WarmupTicks = 600;

    public const int MeasuredTicks = 3_600;

    public static void Run(SimConfig config)
    {
        Console.WriteLine(
            $"trip probe — {WarmupTicks} warm-up ticks, {MeasuredTicks} measured " +
            $"({MeasuredTicks / config.Sim.TickRateHz} s), {config.Solver.VelocityIterations} solver iterations");
        Console.WriteLine(
            $"{"map",-10}{"walkers",9}{"cars",6}{"drawn",8}{"drive",7}{"boarded",9}{"parked",8}{"got out",9}" +
            $"{"entered",9}{"full",6}{"given up",10}{"down",6}{"wrecked",9}");

        foreach (var map in ProjectPaths.ShippedMaps())
        {
            var sample = Sample(map, config);
            Console.WriteLine(
                $"{map,-10}{sample.Walkers,9}{sample.Cars,6}{sample.TripsDrawn,8}{sample.TripsWorthACar,7}" +
                $"{sample.Boardings,9}{sample.BaysParkedIn,8}{sample.Alightings,9}{sample.BuildingsEntered,9}" +
                $"{sample.DoorsFoundFull,6}{sample.TripsGivenUp,10}{sample.Down,6}{sample.Wrecked,9}");
        }

        Console.WriteLine(
            "VER-8 is met while a town's people are entering doors they walked and drove to: boarded → parked → " +
            "got out → entered is one whole trip, and the four counts move together or not at all.");
    }

    /// <param name="TripsDrawn">PER-9's own count: how many times somebody picked somewhere to be.</param>
    /// <param name="TripsWorthACar">And how many of those PER-17 judged worth a car, which is the town's traffic.</param>
    public readonly record struct TripSample(
        int Walkers, int Cars, long TripsDrawn, long TripsWorthACar, long Boardings, long BaysParkedIn,
        long Alightings, long BuildingsEntered, long DoorsFoundFull, long TripsGivenUp, int Down, int Wrecked);

    public static TripSample Sample(string map, SimConfig config)
    {
        using var world = new TownWorld(TownReader.ReadFile(ProjectPaths.TownFile(map)), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        var drawn = world.TripsDrawn;
        var drives = world.TripsWorthACar;
        var boarded = world.Boardings;
        var parked = world.BaysParkedIn;
        var out_ = world.Alightings;
        var entered = world.BuildingsEntered;
        var full = world.DoorsFoundFull;
        var givenUp = world.TripsGivenUp;

        loop.Advance(MeasuredTicks);

        var down = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.Wounded[person]) down++;
        }

        var wrecked = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Broken[car]) wrecked++;
        }

        return new TripSample(
            world.People.Count, world.Cars.Count, world.TripsDrawn - drawn, world.TripsWorthACar - drives,
            world.Boardings - boarded, world.BaysParkedIn - parked, world.Alightings - out_,
            world.BuildingsEntered - entered, world.DoorsFoundFull - full, world.TripsGivenUp - givenUp, down, wrecked);
    }
}
