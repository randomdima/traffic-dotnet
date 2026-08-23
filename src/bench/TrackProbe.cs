using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What each shape of road costs each kind of car</b>: the speed a shape allows, the ground a stop off
/// it takes and the time getting back up to speed — one block per shape of the proving ground
/// (<see cref="TrackPlan"/>), one row per drivetrain.
/// </summary>
/// <remarks>
/// <para>
/// <b>The cars are the town's cars and the arithmetic is the town's arithmetic.</b> One
/// <see cref="TownWorld"/> is stood on the lap, and the cars on it are already driving: a map with nobody
/// living on it puts its own cars on the road, so what this measures is the same laps anybody opening
/// <c>--map Track</c> is watching. A rig with a movement model of its own would measure the rig.
/// </para>
/// <para>
/// <b>Nothing is staged, including the stop.</b> Fifteen people pace the lap — one at the end of every
/// shape and the rest spread along it — each stepping into the lane at a car that can still stop for it, so
/// a car brakes to rest for a body in front of it, waits for it to walk out again and pulls away. This probe
/// asks the cars for nothing at all: it stands the town up, lets it run, and prints what
/// <see cref="TrackMetrics"/> saw.
/// </para>
/// <para>
/// <b>The six cars differ in one figure only.</b> Footprint, wheelbase, track and mass are the nominal
/// car's for all of them, and the fleet's own drivetrain is what each one is spent through — so a
/// difference between the rear row and the front row is a difference about drive layout and about nothing
/// else.
/// </para>
/// </remarks>
internal static class TrackProbe
{
    /// <summary>Long enough that every car has met every shape at least once before anything is written down.</summary>
    const float WarmupS = 30f;

    /// <summary>
    /// How long the lap is watched for. <b>Watching it for longer buys nothing, and that is a fact about
    /// the lap rather than about this figure</b>: every pace stops whichever car arrives first and lets the
    /// ones behind close up, so within ten minutes the six of them are one platoon and every pass after that
    /// is a pass somebody else was in the way of. What is quoted is what the field gathered while it was
    /// still strung out, which is why a row per drivetrain runs to a handful of passes and not to dozens.
    /// </summary>
    const float MeasuredS = 1200f;

    public static void Run(SimConfig config) => Run(config, TrackCrowd.Pacing);

    /// <summary>
    /// <b>The same lap with the drunks on it</b>, printed in the same columns so the two tables are read
    /// against each other. What differs between them is who is standing on the road and nothing else — so a
    /// column that moves is a fact about being followed and got past rather than about the shapes.
    /// </summary>
    public static void RunTheDrunks(SimConfig config) => Run(config, TrackCrowd.Drunk);

    static void Run(SimConfig config, TrackCrowd crowd)
    {
        var plannedMps2 = CarFollower.BrakingMps2(config, groundCoefficient: 1f);
        var lateralMps2 = config.Tyre.GripMps2 * config.Driving.GripMargin;
        var metrics = Measure(config, crowd);

        Console.WriteLine(
            $"track probe — the {TrackPlan.NameOf(crowd)} lap, {WarmupS:F0} s warm-up and {MeasuredS:F0} s measured at "
            + $"{config.Sim.TickRateHz} Hz, a planned {plannedMps2:F2} m/s² on dry ground");
        Console.WriteLine(
            $"{"",-9}{TrackPlan.Cars} cars, two of each drivetrain, on {TrackPlan.LapM():F0} m of lap cut into "
            + $"{TrackPlan.Roads} roads, with {TrackPlan.Pacers} people "
            + (crowd == TrackCrowd.Drunk
                ? "reeling down it — put down in the carriageway, spread along the lap"
                : "pacing it — one at the end of each shape and the rest spread along the lap"));
        Console.WriteLine(
            $"{"section",-11}{"radius m",9}{"length m",9}{"drive",7}{"passes",8}{"top m/s",9}{"hold m/s",10}" +
            $"{"stops",7}{"slow m",8}{"from m/s",10}{"to m/s",8}{"at m/s²",9}{"out s",7}{"out m",8}" +
            $"{"off-line m",12}");

        for (var shape = 0; shape < metrics.Shapes; shape++)
        {
            var section = metrics.SectionOf(shape);
            Row(
                section.Name, section.RadiusM > 0f ? $"{section.RadiusM:F0}" : "—",
                $"{TrackPlan.LengthM(section.Road):F0}", "all", metrics.Figures(shape));
            for (var drivetrain = 0; drivetrain < TrackMetrics.Drivetrains; drivetrain++)
            {
                Row(
                    string.Empty, string.Empty, string.Empty, TrackMetrics.DrivetrainName(drivetrain),
                    metrics.Figures(shape, drivetrain));
            }
        }

        var fewest = float.MaxValue;
        var most = 0f;
        var laps = 0f;
        for (var car = 0; car < metrics.Cars; car++)
        {
            fewest = MathF.Min(fewest, metrics.Laps(car));
            most = MathF.Max(most, metrics.Laps(car));
            laps += metrics.Laps(car);
        }

        Console.WriteLine(
            $"{"",-9}{laps / MathF.Max(1, metrics.Cars):F1} laps a car on average, {fewest:F1} the fewest and "
            + $"{most:F1} the most.");
        Console.WriteLine(
            $"{"",-9}{metrics.Knocks} of the people were knocked down, {metrics.Killed} of them fatally"
            + (metrics.Knocks > 0
                ? $"; the last was walker {metrics.LastKnock.Person} at {metrics.LastKnock.AtM.X:F0},"
                  + $"{metrics.LastKnock.AtM.Y:F0} at {metrics.LastKnock.AtS:F1} s"
                : string.Empty)
            + $". A lane's own half-width is {config.LaneOffsetM:F2} m.");
        Console.WriteLine(
            $"{"",-9}Getting round it took {metrics.Swerves} swerves (`E-4`) and {metrics.BackOffs} back-offs "
            + $"(`E-3`); {metrics.GivenUp} of the laps were given up on (`E-9`, `E-10`), and "
            + $"{metrics.Wrecked} of the {metrics.Cars} cars ended it wrecked.");
        Console.WriteLine(
            $"{"",-9}A corner of radius R affords sqrt({lateralMps2:F1} R): "
            + $"{Affords(lateralMps2, TrackPlan.Turn180RadiusM)} for the 180, "
            + $"{Affords(lateralMps2, TrackPlan.SnakeRadiusM)} for the snake, "
            + $"{Affords(lateralMps2, TrackPlan.ArcRadiusM)} for the arc, "
            + $"{Affords(lateralMps2, TrackPlan.Turn90RadiusM)} for the 90.");
    }

    static string Affords(float lateralMps2, float radiusM) => $"{MathF.Sqrt(lateralMps2 * radiusM):F1} m/s";

    static void Row(string name, string radiusM, string lengthM, string drivetrain, in SectionFigures figures) =>
        Console.WriteLine(
            $"{name,-11}{radiusM,9}{lengthM,9}{drivetrain,7}{figures.Passes,8}{figures.TopMps,9:F2}" +
            $"{figures.HoldMps,10:F2}{figures.Stops,7}{figures.SlowM,8:F1}{figures.SlowFromMps,10:F2}" +
            $"{figures.SlowToMps,8:F2}{figures.SlowedAtMps2,9:F2}{figures.AccelS,7:F1}{figures.AccelM,8:F0}" +
            $"{figures.OffLineM,12:F3}");

    /// <summary>The lap, driven and watched — what the probe prints and what the town-tier gates assert on.</summary>
    public static TrackMetrics Measure(SimConfig config, TrackCrowd crowd = TrackCrowd.Pacing)
    {
        // The map on disk and not the plan in hand: what is measured is the town every other reader gets,
        // and `TrackPlanTests` is what says the two are the same track.
        using var world = new TownWorld(
            TownReader.ReadFile(ProjectPaths.TownFile(TrackPlan.NameOf(crowd))), config);
        var loop = new SimLoop<TownWorld>(world, config);
        var metrics = new TrackMetrics(config, world);

        loop.Advance((int)MathF.Round(WarmupS / loop.TickSeconds));

        var ticks = (int)MathF.Round(MeasuredS / loop.TickSeconds);
        for (var tick = 0; tick < ticks; tick++)
        {
            loop.Advance();
            metrics.Saw(world);
        }

        return metrics;
    }
}
