using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The tier-2 soak stage 4 exits on</b>: a town left to live, and then the one question VER-8 asks
/// of it — <em>does a whole trip complete, end to end, unattended and repeatedly</em>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every column is the count of one thing actually happening</b>, and they are printed together
/// because no one of them means anything alone: trips drawn without arrivals is a town of people
/// walking about with a destination, and arrivals without doors entered is a town whose buildings are
/// all full.
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
            $"{"map",-10}{"walkers",9}{"cars",6}{"drawn",8}{"arrived",9}{"entered",9}{"full",6}" +
            $"{"given up",10}{"set down",10}{"down",6}{"wrecked",9}{"set off",9}{"parked",8}{"legs lost",11}");

        var walkers = 0;
        foreach (var map in Maps.Shipped())
        {
            var sample = Sample(map, config);
            walkers += sample.Walkers;
            Console.WriteLine(
                $"{map,-10}{sample.Walkers,9}{sample.Cars,6}{sample.TripsDrawn,8}{sample.WalkArrivals,9}" +
                $"{sample.BuildingsEntered,9}{sample.DoorsFoundFull,6}{sample.TripsGivenUp,10}" +
                $"{sample.WalkersSetDown,10}{sample.Down,6}{sample.Wrecked,9}{sample.RoundsSetOff,9}" +
                $"{sample.BaysParkedIn,8}{sample.LegsGivenUp,11}");
        }

        Console.WriteLine(
            "VER-8 is met while a town's people are entering doors they walked to: drawn → arrived → entered " +
            "is one whole trip, and the three counts move together or not at all. A car's round is the same " +
            "question asked of the cars (CAR-8): set off → parked, with the legs given up beside it.");

        // <b>A probe that could not stage its scenario says so</b>, because "nothing went wrong" and "nothing
        // happened" are the same row otherwise.
        if (walkers == 0)
        {
            Console.WriteLine(
                "NOT STAGED: no town this build lays stands anybody up, so no trip was drawn. It is the " +
                "roster that is missing and not the trip.");
        }
    }

    /// <param name="TripsDrawn">PER-9's own count: how many times somebody picked somewhere to be.</param>
    /// <param name="WalkArrivals">And how many walks ended where they were going.</param>
    /// <param name="WalkersSetDown">
    /// And how many had to be lifted back onto the pavement (PER-8), which is the count of the times the
    /// town's own ground beat a body rather than anything the walking does.
    /// </param>
    /// <param name="RoundsSetOff">How many times a car on its round left where it stood for a bay (CAR-8).</param>
    /// <param name="BaysParkedIn">And how many times a car came to rest in the bay it was aiming at.</param>
    /// <param name="LegsGivenUp">And how many legs were given up where the car stood, round or errand.</param>
    public readonly record struct TripSample(
        int Walkers, int Cars, long TripsDrawn, long WalkArrivals, long BuildingsEntered, long DoorsFoundFull,
        long TripsGivenUp, long WalkersSetDown, int Down, int Wrecked, long RoundsSetOff, long BaysParkedIn,
        long LegsGivenUp);

    public static TripSample Sample(string map, SimConfig config)
    {
        using var world = new TownWorld(Maps.Plan(map, config, BuildingCatalog.Roofs), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        var drawn = world.TripsDrawn;
        var arrived = world.WalkArrivals;
        var entered = world.BuildingsEntered;
        var full = world.DoorsFoundFull;
        var givenUp = world.TripsGivenUp;
        var setDown = world.WalkersSetDown;
        var setOff = world.RoundsSetOff;
        var parked = world.BaysParkedIn;
        var legsLost = world.LegsGivenUp;

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
            world.People.Count, world.Cars.Count, world.TripsDrawn - drawn, world.WalkArrivals - arrived,
            world.BuildingsEntered - entered, world.DoorsFoundFull - full, world.TripsGivenUp - givenUp,
            world.WalkersSetDown - setDown, down, wrecked, world.RoundsSetOff - setOff,
            world.BaysParkedIn - parked, world.LegsGivenUp - legsLost);
    }
}
