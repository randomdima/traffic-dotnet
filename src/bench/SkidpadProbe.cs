using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The skidpad, driven</b>: every look the fleet ships, six of each, all of them on full left lock —
/// and what each of them turned against what its own axles asked for.
/// </summary>
/// <remarks>
/// <b>The claims are <see cref="SkidpadWatch"/>'s</b>, which is the same watch a run of <c>--map
/// Skidpad</c> keeps against the map on screen, and the wheels are held over by the town itself
/// (<c>TownWorld.HoldTheWheels</c>) rather than by anything here. This only stands the town, ticks it and
/// prints what the watch came to — so the table and the picture cannot disagree about a circle.
/// </remarks>
internal static class SkidpadProbe
{
    /// <summary>
    /// Three quarters of a minute, of which the first fifteen seconds are the cars settling onto their
    /// circles rather than measured (<c>SkidpadWatch.SettlesInS</c>). What is left is several turns for
    /// every car on the pad, and a mean nobody has to read as one circle.
    /// </summary>
    public const float MeasuredS = 45f;

    public static bool Run(SimConfig config)
    {
        Console.WriteLine(
            $"skidpad probe — {SkidpadPlan.Name}, {MeasuredS:F0} s at {config.Sim.TickRateHz} Hz, "
            + $"{SkidpadPlan.Cars} cars: {SkidpadPlan.Looks} looks over {SkidpadPlan.Runs.Length} pedals, "
            + "every one of them on full left lock");

        var watch = Measure(config);

        Console.WriteLine(
            $"{"",-9}Three radii a row: what the axles ask, what the grip affords at the speed reached, and "
            + "what was turned. A car sitting on the second is obeying its tyres.");
        Console.WriteLine(
            $"{"row",-20}{"cars",6}{"m/s",8}{"lat g",8}{"asked m",10}{"grip m",9}{"turned m",10}" +
            $"{"×asked",8}{"×held",8}{"centre m",10}{"front °",9}{"crawl ×",9}");

        for (var run = 0; run < SkidpadPlan.Runs.Length; run++)
        {
            if (watch.Figures(run, out var figures)) Row(SkidpadPlan.RunName(run), figures);
        }

        if (watch.Figures(SkidpadWatch.AnyRun, out var fleet)) Row("the pad", fleet);

        // A row is sixteen looks averaged, and a look that behaves unlike the rest disappears into that.
        Console.WriteLine();
        Console.WriteLine($"{"",-9}The same ×asked a look at a time, so that one body's figures can be picked out.");
        Console.Write($"{"look",-20}{"grip",6}{"asked m",9}");
        for (var run = 0; run < SkidpadPlan.Runs.Length; run++) Console.Write($"{SkidpadPlan.RunName(run),20}");
        Console.WriteLine($"{"crawl ×",10}");

        for (var look = 0; look < SkidpadPlan.Looks; look++)
        {
            watch.FiguresOfCar(look, out var asked);
            Console.Write($"{watch.LookNameOf(look),-20}{watch.GripMps2Of(look),6:F1}{asked.AskedM,9:F2}");
            var crawlSum = 0f;
            var crawled = 0;
            for (var run = 0; run < SkidpadPlan.Runs.Length; run++)
            {
                var car = (run * SkidpadPlan.Looks) + look;
                if (!watch.FiguresOfCar(car, out var figures))
                {
                    Console.Write($"{"—",20}");
                    continue;
                }

                if (figures.AtACrawl > 0f)
                {
                    crawlSum += figures.AtACrawl;
                    crawled++;
                }

                Console.Write($"{$"{figures.TurnedM:F1} m ({figures.TimesAsked:F2}x)",20}");
            }

            Console.WriteLine($"{(crawled == 0 ? 0f : crawlSum / crawled),10:F2}");
        }

        return ScenarioReport.Print(SkidpadPlan.Name, [watch], MeasuredS);
    }

    static void Row(string name, in SkidpadFigures figures) =>
        Console.WriteLine(
            $"{name,-20}{figures.Cars,6}{figures.SpeedMps,8:F1}{figures.LateralG,8:F2}{figures.AskedM,10:F2}" +
            $"{figures.GripM,9:F1}{figures.TurnedM,10:F1}{figures.TimesAsked,8:F2}{figures.TimesHeld,8:F2}" +
            $"{figures.CentreAheadM,10:F2}{figures.FrontSlipDeg,9:F1}{figures.AtACrawl,9:F2}");

    /// <summary>
    /// The pad, driven and watched — <b>what the probe prints and what the town tier asserts on</b>, which
    /// is one run read twice rather than two runs.
    /// </summary>
    public static SkidpadWatch Measure(SimConfig config)
    {
        // The map on disk and not the plan in hand: what is measured is the town every other reader gets,
        // and the pad is laid from those same figures, so the two cannot drift apart.
        using var world = new TownWorld(Maps.Plan(SkidpadPlan.Name, config, BuildingCatalog.Shared.OrdinaryFootprintsM()), config);
        var loop = new SimLoop<TownWorld>(world, config);
        var watch = new SkidpadWatch(config, world);

        var ticks = (int)MathF.Round(MeasuredS / loop.TickSeconds);
        for (var tick = 0; tick < ticks; tick++)
        {
            loop.Advance();
            watch.Saw(world);
        }

        return watch;
    }
}
