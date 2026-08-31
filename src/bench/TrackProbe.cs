using TrafficSimulation.Agents.Car.Body;
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
/// <para>
/// <b>And the fleet lap is the same instrument asked the opposite question</b>
/// (<see cref="RunTheFleet"/>): one car of every look, differing in every figure a variant states, printed
/// a row per look because a table cut by shape would be answering a question nobody asked there.
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

    /// <summary>
    /// <b>The fleet lap is watched from the standing start and not after a warm-up</b>, which is the whole
    /// of how sixteen cars on one circuit are measured at all. Their tops run from 45 to 109 m/s and no car
    /// may cross the centreline to get past a moving one (CAR-6.2b), so the field ends up one queue behind
    /// the armoured car and every figure after that is the armoured car's. At <c>t=0</c> they stand a
    /// hundred and seventy metres apart with the road in front of each of them, and the pull away from rest
    /// and the first corners are every look's own.
    /// </summary>
    /// <remarks>
    /// <b>The standing start is also the only stop this lap has.</b> Nobody is on foot on it (see
    /// <see cref="TrackLap.Fleet"/>), so what slows a car is the shape it is taking and what is in front of
    /// it — which is the whole point, and which means there is no second chance at a pull away from rest.
    /// </remarks>
    const float FleetWarmupS = 0f;

    const float FleetMeasuredS = 240f;

    public static bool Run(SimConfig config) => Run(config, TrackLap.Pacing);

    /// <summary>
    /// <b>The same lap with the drunks on it</b>, printed in the same columns so the two tables are read
    /// against each other. What differs between them is who is standing on the road and nothing else — so a
    /// column that moves is a fact about being followed and got past rather than about the shapes.
    /// </summary>
    public static bool RunTheDrunks(SimConfig config) => Run(config, TrackLap.Drunk);

    /// <summary>
    /// <b>The same lap with the whole fleet on it</b>, and a row per look rather than per shape — because
    /// what differs here is every figure a variant states, so a table cut by shape would be answering a
    /// question nobody asked. What it says is whether each car anybody may be handed drove the road, stayed
    /// on it, and got away from what stopped it at a rate worth calling driving.
    /// </summary>
    public static bool RunTheFleet(SimConfig config)
    {
        var watch = Measure(config, TrackLap.Fleet);
        var metrics = watch.Metrics;

        Console.WriteLine(
            $"fleet probe — the {TrackPlan.FleetName} lap, {FleetMeasuredS:F0} s measured from the standing start at "
            + $"{config.Sim.TickRateHz} Hz, on the same {TrackPlan.LapM():F0} m of lap the track probe measures");
        Console.WriteLine(
            $"{"",-9}{TrackPlan.FleetCars} cars, one of every look the fleet ships, and nobody on foot "
            + "anywhere on it — so a row that differs from another is a difference about the car");
        Console.WriteLine(
            $"{"look",-18}{"kg",7}{"m",6}{"drive",7}{"laps",7}{"passes",8}{"top m/s",9}{"stops",7}" +
            $"{"slow m/s²",11}{"pulls",7}{"out s",7}{"out m",8}{"best m/s²",11}{"off-line m",12}");

        for (var car = 0; car < metrics.Cars; car++)
        {
            ref readonly var build = ref metrics.BuildOf(car);
            var figures = metrics.FiguresOfCar(car);
            Console.WriteLine(
                $"{metrics.LookOf(car),-18}{build.MassKg,7:F0}{build.LengthM,6:F2}" +
                $"{TrackMetrics.DrivetrainName(TrackMetrics.Drivetrain(build.DrivenFrontShare)),7}" +
                $"{metrics.Laps(car),7:F1}{figures.Passes,8}{metrics.TopMps(car),9:F2}{figures.Stops,7}" +
                $"{figures.SlowedAtMps2,11:F2}{figures.Pulls,7}{figures.AccelS,7:F1}{figures.AccelM,8:F0}" +
                $"{figures.PulledBestMps2,11:F2}{figures.OffLineM,12:F3}");
        }

        Console.WriteLine(
            $"{"",-9}A lane's own half-width is {config.LaneOffsetM:F2} m, and the town calls a car "
            + $"{config.CarOffPathM * 2f:F2} m off its line a car that has lost it.");
        Console.WriteLine(
            $"{"",-9}Getting round it took {metrics.Swerves} swerves (`E-4`) and {metrics.BackOffs} back-offs "
            + $"(`E-3`); {metrics.GivenUp} of the laps were given up on (`E-9`, `E-10`), and "
            + $"{metrics.Wrecked} of the {metrics.Cars} cars ended it wrecked.");

        return ScenarioReport.Print(TrackPlan.FleetName, [watch], metrics.WatchedS);
    }

    static bool Run(SimConfig config, TrackLap lap)
    {
        // The lap's cars are the nominal one (CAR-11a), so the figures this table is read against are its.
        var nominal = CarBuild.Nominal(config, config.Car.DrivenFrontShare);
        var plannedMps2 = CarFollower.BrakingMps2(config, nominal, groundCoefficient: 1f);
        var lateralMps2 = config.TyreGripMps2 * config.Driving.GripMargin;
        var watch = Measure(config, lap);
        var metrics = watch.Metrics;

        Console.WriteLine(
            $"track probe — the {TrackPlan.NameOf(lap)} lap, {WarmupS:F0} s warm-up and {MeasuredS:F0} s measured at "
            + $"{config.Sim.TickRateHz} Hz, a planned {plannedMps2:F2} m/s² on dry ground");
        Console.WriteLine(
            $"{"",-9}{TrackPlan.Cars} cars, two of each drivetrain, on {TrackPlan.LapM():F0} m of lap cut into "
            + $"{TrackPlan.Roads} roads, with {TrackPlan.Pacers} people "
            + (lap == TrackLap.Drunk
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
            $"{"",-9}{metrics.Knocks} of the people were knocked down and left for an ambulance"
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

        return ScenarioReport.Print(TrackPlan.NameOf(lap), [watch], metrics.WatchedS);
    }

    static string Affords(float lateralMps2, float radiusM) => $"{MathF.Sqrt(lateralMps2 * radiusM):F1} m/s";

    static void Row(string name, string radiusM, string lengthM, string drivetrain, in SectionFigures figures) =>
        Console.WriteLine(
            $"{name,-11}{radiusM,9}{lengthM,9}{drivetrain,7}{figures.Passes,8}{figures.TopMps,9:F2}" +
            $"{figures.HoldMps,10:F2}{figures.Stops,7}{figures.SlowM,8:F1}{figures.SlowFromMps,10:F2}" +
            $"{figures.SlowToMps,8:F2}{figures.SlowedAtMps2,9:F2}{figures.AccelS,7:F1}{figures.AccelM,8:F0}" +
            $"{figures.OffLineM,12:F3}");

    /// <summary>
    /// The lap, driven and watched — <b>what the probe prints, what the panel draws and what the town tier
    /// asserts on</b>, which is one lap read three times rather than three laps.
    /// </summary>
    public static LapWatch Measure(SimConfig config, TrackLap lap = TrackLap.Pacing)
    {
        // The map on disk and not the plan in hand: what is measured is the town every other reader gets,
        // and the lap is laid from those same figures, so the two cannot drift apart.
        using var world = new TownWorld(
            TownReader.ReadFile(ProjectPaths.TownFile(TrackPlan.NameOf(lap))), config);
        var loop = new SimLoop<TownWorld>(world, config);
        var watch = lap == TrackLap.Fleet
            ? new FleetWatch(config, world)
            : (LapWatch)new TrackWatch(config, world, lap);

        loop.Advance((int)MathF.Round((lap == TrackLap.Fleet ? FleetWarmupS : WarmupS) / loop.TickSeconds));

        var ticks = (int)MathF.Round((lap == TrackLap.Fleet ? FleetMeasuredS : MeasuredS) / loop.TickSeconds);
        for (var tick = 0; tick < ticks; tick++)
        {
            loop.Advance();
            watch.Saw(world);
        }

        return watch;
    }
}
