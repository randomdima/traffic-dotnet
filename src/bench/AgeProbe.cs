using System.Diagnostics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What one town's tick costs at fixed ages</b>: windows of a fixed number of ticks at fixed points of the
/// town's own clock, ranked by phase, each closed by a digest of where every body stands.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the before and after of one change</b>, where <see cref="TownProbe"/> is a town's standing cost. That
/// probe's windows are wall time, so a change that makes the tick cheaper is measured further into the town's
/// life, which costs more a tick: the saving is under-read by however much the town aged. These windows are the
/// same ticks of the same town on both sides.
/// </para>
/// <para>
/// <b>The digest says whether both sides were the same town.</b> A change meant to move nothing a body does
/// leaves every digest equal to the bit; a change to behaviour moves them, and then the windows compare towns of
/// the same age rather than the same town.
/// </para>
/// </remarks>
internal static class AgeProbe
{
    /// <summary>Where each window begins on the town's own clock.</summary>
    static readonly float[] WindowAtS = [60f, 180f, 300f];

    /// <summary>How many ticks a window runs: a minute of the town.</summary>
    const int WindowTicks = 3600;

    public static void Run(SimConfig config) => Run(config, "Odesa");

    public static void Run(SimConfig config, string map)
    {
        Warmup.TheProcess(config);

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);

        Console.WriteLine($"age probe — {map}, {world.People.Count} walkers, {world.Cars.Count} cars, {WindowTicks} ticks a window, µs per tick with the timing on");
        Console.WriteLine($"{"from s",8}{"tick",8}{"index",8}{"agents",8}{"walkers",9}{"cars",8}{"bodies",8}{"solver",8}{"contacts",10}{"awake",8}{"frozen",8}{"wall s",8}  digest");

        var started = Stopwatch.GetTimestamp();
        foreach (var fromS in WindowAtS)
        {
            var fromTick = (long)MathF.Round(fromS * config.Sim.TickRateHz);
            while (loop.Tick < fromTick) loop.Advance();

            loop.Phases.Reset();
            world.Sub.Reset();
            loop.Timed = world.Timed = true;
            loop.Advance(WindowTicks);
            loop.Timed = world.Timed = false;

            var phases = loop.Phases;
            var sub = world.Sub;
            Console.WriteLine(
                $"{fromS,8:F0}{Micro(phases, phases.WholeTicks),8:F1}{Micro(phases, phases.IndexTicks),8:F1}" +
                $"{Micro(phases, phases.AgentTicks),8:F1}{Micro(phases, sub.WalkerTicks),9:F1}{Micro(phases, sub.CarTicks),8:F1}" +
                $"{Micro(phases, phases.BodyTicks),8:F1}{Micro(phases, sub.SolverTicks),8:F1}{Micro(phases, phases.ContactTicks),10:F1}" +
                $"{world.IntegratedBodyCount,8}{world.FrozenBodyCount,8}{Stopwatch.GetElapsedTime(started).TotalSeconds,8:F1}  {Digest(world):x16}");
        }
    }

    static double Micro(in PhaseTimes phases, long ticks) => phases.MillisecondsPer(ticks) * 1000d;

    /// <summary>Every body's pose and motion, hashed bit for bit.</summary>
    static ulong Digest(TownWorld world)
    {
        var hash = 14695981039346656037UL;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            Mix(ref hash, world.Cars.PositionM[car].X);
            Mix(ref hash, world.Cars.PositionM[car].Y);
            Mix(ref hash, world.Cars.HeadingRad[car]);
            Mix(ref hash, world.Cars.VelocityMps[car].X);
            Mix(ref hash, world.Cars.VelocityMps[car].Y);
        }

        for (var person = 0; person < world.People.Count; person++)
        {
            Mix(ref hash, world.People.PositionM[person].X);
            Mix(ref hash, world.People.PositionM[person].Y);
        }

        return hash;

        static void Mix(ref ulong into, float value)
        {
            into ^= (uint)BitConverter.SingleToInt32Bits(value);
            into *= 1099511628211UL;
        }
    }
}
