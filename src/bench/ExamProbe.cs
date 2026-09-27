using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Exam;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The scenario map, driven and reported card by card</b>: every card of <see cref="ExamCards"/> staged at
/// once on the map laid for it, and for each whether it passed — and, where it failed, what it expects, what
/// the town did instead, and what held each car it staged.
/// </summary>
/// <remarks>
/// <b>The instrument and the test are one machine</b>: <c>--bench exam</c> prints this table and the exam tier
/// asserts the same verdicts (<see cref="ExamRun"/>), so a card cannot pass in one and fail in the other.
/// </remarks>
internal static class ExamProbe
{
    public static bool Run(SimConfig config)
    {
        using var run = new ExamRun(config);
        var drive = run.Drive;
        var lattice = drive.Lattice;
        var hz = config.Sim.TickRateHz;

        Console.WriteLine(
            $"scenarios — {lattice.Cards} cards on {ExamPlan.Name}, a {ExamLattice.Rows} by {ExamLattice.Columns} "
            + $"lattice, {drive.Ticks} ticks ({drive.Ticks / hz} s), {drive.Cars} cars and {lattice.Walkers} walkers");
        Console.WriteLine();
        Console.WriteLine($"{"card",5}  {"cell",-7}{"family",-13}{"verdict",-9}scenario");

        var passed = 0;
        for (var card = 0; card < lattice.Cards; card++)
        {
            var of = lattice.Card(card);
            var cell = lattice.CellOf(card);
            var wrong = drive.Verdict(card);
            if (wrong is null) passed++;

            Console.WriteLine(
                $"{card,5}  {$"{cell / ExamLattice.Columns},{cell % ExamLattice.Columns}",-7}{of.Family,-13}"
                + $"{(wrong is null ? "passed" : "FAILED"),-9}{of.Name}");
            if (wrong is null) continue;

            Console.WriteLine($"{"",14}expects: {of.Expects} ({of.Rules})");
            Console.WriteLine($"{"",14}found: {wrong}");
            for (var driver = 0; driver < of.Drivers.Length; driver++)
            {
                var log = drive.Car(card, driver);
                var car = drive.Lattice.CarOf(card, driver);
                Console.WriteLine($"{"",14}{Timeline(drive, log, hz)}");
                if (log.ArrivedAt < 0) Console.WriteLine($"{"",16}at the end {Off(drive, card, run.World.Cars.PositionM[car])}");
            }

            for (var walker = 0; walker < of.Walkers.Length; walker++)
            {
                Console.WriteLine($"{"",14}{Timeline(run.World, drive, drive.Walker(card, walker), hz)}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{passed} of {lattice.Cards} cards passed, {lattice.Cards - passed} failed.");

        // And the same run said as claims, which is what a caller reads to decide whether the map passed.
        return ScenarioReport.Print(ExamPlan.Name, [run.Watch], run.World.ElapsedS) && passed == lattice.Cards;
    }

    /// <summary>What one driver of a failed card did and when, in seconds — which is what the finding is read against.</summary>
    static string Timeline(ExamDrive drive, ExamCarLog log, float hz)
    {
        static string At(int tick, float hz) => tick < 0 ? "-" : $"{tick / hz:F1}";

        var told = log.HeldIn;
        var heldBy = log.HeldTicks > 0
            ? $", last held for {log.HeldFor} by {drive.Cut(log.HeldBy)} (headway {told.HeadwayM:F1} m {told.Ahead}, "
              + $"stop {told.StopAtM:F1} m, crossing stop {told.CrossingStopM:F1} m, place {told.PlaceStopM:F1} m)"
            : "";
        return log.Drives.Parked
            ? $"driver {log.Driver} stood in its lane as an obstruction"
            : $"driver {log.Driver} {ExamJudge.Movement(log)}: sent {At(log.StartedAt, hz)} s, on the box "
              + $"{At(log.EnteredAt, hz)}-{At(log.LeftAt, hz)} s, out {At(log.ClearedAt, hz)} s, arrived "
              + $"{At(log.ArrivedAt, hz)} s, held {log.HeldTicks / hz:F1} s, stood {log.StoodTicks / hz:F1} s{heldBy}";
    }

    /// <summary>What one walker of a failed card did and when, and — where it never got over — what its walk was cut at at the end.</summary>
    static string Timeline(TownWorld world, ExamDrive drive, ExamWalkerLog log, float hz)
    {
        static string At(int tick, float hz) => tick < 0 ? "-" : $"{tick / hz:F1}";

        var person = drive.Lattice.WalkerOf(log.Card, log.Walker);
        var hold = world.WalkHold(person);
        var by = LaneClaim.Nothing;
        if (hold != LaneOccupancy.NoHold) world.Occupancy.HoldEndsAtM(hold, out _, out by);
        return $"walker {log.Walker}: sent {At(log.StartedAt, hz)} s, on the paint {At(log.OnThePaintAt, hz)} s, "
               + $"arrived {At(log.ArrivedAt, hz)} s, sent from {Off(drive, log.Card, log.FromM)} to "
               + $"{Off(drive, log.Card, log.ToM)} over paint {log.Band.SpanM:F1} m across, centred "
               + $"{Off(drive, log.Card, log.Band.CentreM)}; at the end {world.People.Stage[person]} "
               + $"{Off(drive, log.Card, world.People.PositionM[person])}, its walk cut by {drive.Cut(by)}";
    }

    /// <summary>Where a body stands off the middle of its card's box, in the card's own frame: east and north of it, in metres.</summary>
    public static string Off(ExamDrive drive, int card, Vector2 atM)
    {
        var offM = atM - drive.Lattice.StageM(card);
        var eastM = Vector2.Dot(offM, drive.Lattice.Outward(card, ExamArm.East));
        var northM = Vector2.Dot(offM, drive.Lattice.Outward(card, ExamArm.North));
        return $"at {eastM:F1} m east, {northM:F1} m north of the box";
    }
}

/// <summary>
/// <b>One exam, run to the end</b>: the map laid, the town stood, and every tick of the exam's window
/// watched — the way the game drives it, so what the probe prints, what the tier asserts and what the panel
/// shows on a run of <c>--map Exam</c> are one staging read three times.
/// </summary>
internal sealed class ExamRun : IDisposable
{
    public ExamRun(SimConfig config)
    {
        World = new TownWorld(Maps.Plan(ExamPlan.Name, config, BuildingCatalog.Roofs), config);
        Watch = ExamWatch.Over(config, World);

        var loop = new SimLoop<TownWorld>(World, config);
        for (var tick = 0; tick < Drive.Ticks; tick++)
        {
            loop.Advance();
            Watch.Saw(World);
        }
    }

    public TownWorld World { get; }

    public ExamWatch Watch { get; }

    public ExamDrive Drive => Watch.Drive;

    public void Dispose() => World.Dispose();
}
