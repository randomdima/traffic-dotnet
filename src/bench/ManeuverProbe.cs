using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The manoeuvre trace, read back off a town</b>: where every driver spent its time, how often the
/// escalation ladder was walked and what each rung came to, and the three faults a success count can
/// never show — a pair passing a car back and forth in one spot, an entry nothing ever reaches, and a
/// car standing still that no clock is running for.
/// </summary>
/// <remarks>
/// Taken before the catalogue was finished rather than after it: debugging forty entries without it is
/// the expensive way, and every absence in the last column is a gap named now rather than discovered
/// later.
/// </remarks>
internal static class ManeuverProbe
{
    public const int WarmupTicks = 600;

    /// <summary>A minute, which is the window the trip figures are taken over and the same town twice otherwise.</summary>
    public const int MeasuredTicks = 3_600;

    public static void Run(SimConfig config)
    {
        Console.WriteLine(
            $"manoeuvre trace — {WarmupTicks} warm-up ticks, {MeasuredTicks} measured " +
            $"({MeasuredTicks / config.Sim.TickRateHz} s), {config.Solver.VelocityIterations} solver iterations");

        foreach (var map in Maps.Shipped()) Trace(map, config);

        Console.WriteLine(
            $"The blocked-road clock is {config.CarBlockedRoadS:F0} s and the short fuse a car standing across a lane " +
            $"is measured on is {config.CarShortFuseS:F0} s, each jittered a fifth per car.");
    }

    static void Trace(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Shared.OrdinaryFootprintsM());
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        // The warm-up is not the measurement: what a town does while its people are still walking to
        // their first car is not what it does once it is running.
        world.Trace.Reset();
        var searched = world.RouteSearches;
        var begun = world.Boardings;
        loop.Advance(MeasuredTicks);

        var trace = world.Trace;
        Console.WriteLine();
        Console.WriteLine(
            $"{map} — {trace.CarTicks} car-ticks with somebody at the wheel, " +
            $"{world.CrossingsOnLanes} lane-and-crossing pairs over {plan.Crosswalks.Count} crossings");
        Console.WriteLine($"{"entry",-8}{"share",8}{"entered",10}   what it is");

        for (var entry = 0; entry < Maneuvers.Count; entry++)
        {
            var maneuver = (Maneuver)entry;
            var ticks = trace.TicksIn(maneuver);
            var entries = trace.Entries(maneuver);
            if (ticks == 0 && entries == 0) continue;

            Console.WriteLine(
                $"{Maneuvers.Code(maneuver),-8}{Share(ticks, trace.CarTicks),8:P0}{entries,10}   {maneuver}");
        }

        Console.WriteLine(
            $"reactive: {world.LaddersClimbed} rungs taken — {world.BackOffsTaken} back-offs (E-3), " +
            $"{world.SwervesTaken} swerves (E-4), {world.PlacesGivenUp} places given up (E-6), " +
            $"{world.ReroutesTaken} reroutes (E-7), {world.GroundRecoveries} returns to legal ground (E-8), " +
            $"{world.LegsSettled} settled (E-9), {world.CarsAbandoned} abandoned (E-10)");

        // What a leg spends on finding its way. A leg is routed once and driven, so this is a handful per
        // leg and never a figure that climbs with the junctions the town's cars went through.
        var searches = world.RouteSearches - searched;
        var legs = world.Boardings - begun;
        Console.WriteLine(legs == 0
            ? $"routing: {searches} searches of the driving network, no leg begun to measure them against"
            : $"routing: {searches} searches of the driving network over {legs} legs begun, {searches / (double)legs:F1} each");

        var worst = trace.WorstShuttle();
        Console.WriteLine(worst.Count == 0
            ? "back and forth: no pair handed a car to and fro without it going anywhere"
            : $"back and forth: {Maneuvers.Code(worst.A)} ↔ {Maneuvers.Code(worst.B)} {worst.Count} times in one spot");

        Console.WriteLine(trace.StoodUnclocked == 0
            ? "no car stood still with nothing running for it"
            : $"{trace.StoodUnclocked} car-ticks stood still with no clock running — every one of those is a fault");

        Console.Write("never entered:");
        var absent = 0;
        for (var entry = 1; entry < Maneuvers.Count; entry++)
        {
            if (trace.EverEntered((Maneuver)entry)) continue;

            Console.Write($"{(absent++ > 0 ? "," : string.Empty)} {Maneuvers.Code((Maneuver)entry)}");
        }

        Console.WriteLine(absent == 0 ? " nothing — every entry in the catalogue was reached" : string.Empty);
    }

    static double Share(long part, long whole) => whole == 0 ? 0 : part / (double)whole;
}
