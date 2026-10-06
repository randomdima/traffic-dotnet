using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The solver's step on the device, beside the CPU's, on a running town</b>: every tick the device is handed the
/// table the CPU is about to step, and its own time is read off the queue's timestamps, stage by stage. Every
/// <see cref="CompareEvery"/> ticks the two answers are compared — the contacts pair for pair, and where each body the
/// CPU moved was left.
/// </summary>
/// <remarks>
/// <para>
/// <b>The contacts are the same question</b> and are asked to agree: the device finds every pair the CPU does, and
/// more only where both bodies were frozen on the CPU (SOL-37), which nothing there asks about.
/// </para>
/// <para>
/// <b>The poses are not</b>: the device solves in colours and the CPU in contact order, so a crowded step lands a
/// little apart. What is quoted is how far, and nothing fails on it.
/// </para>
/// </remarks>
internal static class DeviceStepProbe
{
    const int WarmupTicks = 600;
    const int MeasuredTicks = 600;
    const int CompareEvery = 60;

    sealed class Stages
    {
        public readonly double[] SumMs = new double[Enum.GetValues<DeviceSolver.Stage>().Length];
        public readonly double[] MostMs = new double[Enum.GetValues<DeviceSolver.Stage>().Length];
        public int Steps;

        public void Add(DeviceSolver solver)
        {
            Steps++;
            for (var stage = 1; stage < SumMs.Length; stage++)
            {
                var ms = solver.BetweenMs((DeviceSolver.Stage)(stage - 1), (DeviceSolver.Stage)stage);
                SumMs[stage] += ms;
                MostMs[stage] = Math.Max(MostMs[stage], ms);
            }

            var whole = solver.BetweenMs(DeviceSolver.Stage.Began, DeviceSolver.Stage.Integrated);
            SumMs[0] += whole;
            MostMs[0] = Math.Max(MostMs[0], whole);
        }
    }

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        var warmed = Stopwatch.StartNew();
        while (loop.Tick < WarmupTicks) loop.Advance();

        using var vk = Vk.Open("traffic-dotnet", validation: false);
        var physics = world.PhysicsForInstruments;
        using var solver = new DeviceSolver(vk, config, physics.Table);
        var table = physics.Table;
        Console.WriteLine($"device step probe — {map} on {vk.DeviceName}, warmed {WarmupTicks} ticks in {warmed.Elapsed.TotalSeconds:F0} s");
        Console.WriteLine(
            $"bodies {table.Count}, furniture {table.Statics.Length}, window {solver.Window.Width}x{solver.Window.Height} cells, "
            + $"room for {solver.CandidateCapacity} candidates");

        var frozenBefore = new bool[table.Count];
        var compare = false;
        world.BeforeTheSolverSteps = before =>
        {
            var now = before.Table;
            for (var body = 0; body < now.Count; body++) frozenBefore[body] = (now.Flags[body] & BodyFlags.Frozen) != 0;
            solver.Step(now, config.TickSeconds, compare);
        };

        var stages = new Stages();
        var comparisons = new List<string>();
        var apartM = new List<float>();
        var owners = 0L;
        var contacts = 0L;
        var colours = 0;
        var leftOver = 0;
        var overflowed = 0;
        loop.Phases.Reset();
        world.Sub.Reset();
        loop.Timed = world.Timed = true;
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            compare = tick % CompareEvery == 0;
            loop.Advance();
            stages.Add(solver);
            owners += solver.Owners;
            contacts += solver.ContactCount;
            colours = Math.Max(colours, solver.Colours);
            leftOver = Math.Max(leftOver, solver.LeftOver);
            if (solver.Overflowed) overflowed++;
            if (compare) comparisons.Add(Compare(physics, solver, frozenBefore, apartM));
        }

        loop.Timed = world.Timed = false;
        world.BeforeTheSolverSteps = null;

        var cpuSolverMs = loop.Phases.MillisecondsPer(world.Sub.SolverTicks);
        Console.WriteLine();
        Console.WriteLine($"{MeasuredTicks} ticks: {owners / MeasuredTicks} moving bodies and {contacts / MeasuredTicks} contacts a step, "
                          + $"at most {colours} colours, {leftOver} contacts left to one thread, {overflowed} steps out of room");
        Console.WriteLine($"the CPU's solver step: {cpuSolverMs:F3} ms a tick");
        Console.WriteLine($"{"device, ms a step",-20}{"mean",9}{"most",9}");
        foreach (var stage in Enum.GetValues<DeviceSolver.Stage>())
        {
            var label = stage == DeviceSolver.Stage.Began ? "whole step" : $"to {stage.ToString().ToLowerInvariant()}";
            Console.WriteLine($"{label,-20}{stages.SumMs[(int)stage] / stages.Steps,9:F3}{stages.MostMs[(int)stage],9:F3}");
        }

        Console.WriteLine();
        Console.WriteLine("every 60th tick, the device beside the CPU:");
        foreach (var line in comparisons) Console.WriteLine(line);

        apartM.Sort();
        if (apartM.Count == 0) return;

        Console.WriteLine(
            $"where the colours left a moved body against the CPU's order, over {apartM.Count}: "
            + $"median {Mm(apartM[apartM.Count / 2])}, 99th {Mm(apartM[(int)(apartM.Count * 0.99)])}, most {Mm(apartM[^1])}");
    }

    static string Mm(float metres) => $"{metres * 1000f:F3} mm";

    static string Compare(PhysicsWorld physics, DeviceSolver solver, bool[] frozenBefore, List<float> apartM)
    {
        var cpu = physics.Contacts;
        var cpuAt = new Dictionary<ulong, int>(cpu.Count);
        for (var contact = 0; contact < cpu.Count; contact++) cpuAt[Key(cpu.First[contact], cpu.Second[contact])] = contact;

        var found = 0;
        var extraFrozen = 0;
        var extraElse = 0;
        var mostNormal = 0f;
        var mostSeparationM = 0f;
        var points = 0;
        foreach (var (contact, at) in Indexed(solver))
        {
            var key = Key((int)contact.X, (int)contact.Y);
            if (!cpuAt.TryGetValue(key, out var mine))
            {
                if (frozenBefore[contact.X] && frozenBefore[contact.Y]) extraFrozen++;
                else if (frozenBefore[contact.X] && (physics.Table.Flags[(int)contact.Y] & BodyFlags.Static) != 0) extraFrozen++;
                else extraElse++;
                continue;
            }

            found++;
            var normal = solver.ContactNormals[at];
            mostNormal = MathF.Max(mostNormal, Vector2.Distance(cpu.Normal[mine], new Vector2(normal.X, normal.Y)));
            mostSeparationM = MathF.Max(mostSeparationM, MathF.Abs(cpu.SeparationM[mine * 2] - normal.Z));
            if (cpu.Points[mine] != (int)contact.Z) points++;
        }

        var table = physics.Table;
        foreach (var body in table.Moving)
        {
            if (frozenBefore[body]) continue;

            var pose = solver.Pose[body];
            apartM.Add(Vector2.Distance(table.PositionM[body], new Vector2(pose.X, pose.Y)));
        }

        return $"  CPU {cpu.Count} contacts, device {solver.ContactCount}: {found} the same, {cpu.Count - found} missed, "
               + $"{extraFrozen} more among the frozen, {extraElse} more elsewhere, {points} with other points; "
               + $"normals within {mostNormal:E1}, separations within {mostSeparationM * 1000f:F4} mm";
    }

    static IEnumerable<(UInt4 Contact, int At)> Indexed(DeviceSolver solver)
    {
        var all = solver.Contacts.ToArray();
        for (var at = 0; at < all.Length; at++) yield return (all[at], at);
    }

    static ulong Key(int first, int second) => ((ulong)(uint)first << 32) | (uint)second;
}
