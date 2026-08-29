using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// The lit town's soak: every shipped map run with its lights on, and the four signal invariants
/// Sampled every tick rather than looked at afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three of the four are about the table and one is about the traffic.</b> The table's — no bundle
/// publishing conflicting greens, no crossing green against a road that is not fully red — are true by
/// the shape of the cycle and are asserted at tier 1 over a whole cycle; they are sampled here anyway,
/// because a tier-1 proof of a table says nothing about the table the running town is reading.
/// </para>
/// <para>
/// <b>The one about the traffic is the figure that must be zero</b>: a car crossing a painted stop line
/// its approach was showing red at. It is counted where it happens, in the tick that carries the car
/// over the paint, because a sample taken afterwards finds a car past the bar and cannot say what the
/// light was when it went.
/// </para>
/// <para>
/// <b>The walker's half is missing and is a row rather than a silence</b> — "no walker beginning a
/// crossing on a red" needs a walker that crosses on the paint at all, which is the walking route (M6)
/// and the kerb manoeuvre (M8). This engine's walkers wander, so there is no beginning to judge.
/// </para>
/// </remarks>
internal static class SignalProbe
{
    public const int WarmupTicks = 600;

    public const int MeasuredTicks = 3_600;

    public static void Run(SimConfig config)
    {
        Console.WriteLine(
            $"signal probe — {WarmupTicks} warm-up ticks, {MeasuredTicks} measured " +
            $"({MeasuredTicks / config.Sim.TickRateHz} s), sampled every tick");
        Console.WriteLine(
            $"{"map",-10}{"lit",5}{"heads",7}{"crossings",11}{"cars",6}{"conflicting greens",20}" +
            $"{"crossing against a road",24}{"reckless",10}{"red bars crossed",18}{"kerb waits",12}" +
            $"{"begun on red",14}{"given way at a kerb",21}{"crossings given back",22}");

        foreach (var map in ProjectPaths.ShippedMaps())
        {
            var sample = Sample(map, config);
            Console.WriteLine(
                $"{map,-10}{sample.LitJunctions,5}{sample.Heads,7}{sample.LitCrossings,11}{sample.Cars,6}" +
                $"{sample.ConflictingGreens,20}{sample.CrossingAgainstTraffic,24}{sample.RecklessDrivers,10}" +
                $"{sample.RedBarsCrossed,18}{sample.KerbWaits,12}{sample.CrossingsBegunOnRed,14}" +
                $"{sample.GivenWayAtAKerb,21}{sample.CrossingsGivenBack,22}");

            if (sample.RedBarsCrossed > 0)
            {
                var last = sample.LastCrossing;
                Console.WriteLine(
                    $"{"",10}the last of them: car {last.Car} at {last.AtM.X:F0},{last.AtM.Y:F0}, {last.SpeedMps:F2} m/s");
            }
        }

        Console.WriteLine(
            "The two conflict columns must be zero. Red bars crossed is not one of them and never was: a share of " +
            "the town does not keep the rule (CAR-13), and a lit map crosses a handful of bars in a minute without " +
            "them — a shunt over a line, a car committed when the phase turned. It is read beside the reckless " +
            "column. A town with no lit junction proves nothing here and says so in its own row.");
    }

    public readonly record struct SignalSample(
        int LitJunctions, int Heads, int LitCrossings, int Cars, long ConflictingGreens, long CrossingAgainstTraffic,
        int RecklessDrivers, long RedBarsCrossed, RedBarCrossing LastCrossing, long KerbWaits,
        long CrossingsBegunOnRed, long GivenWayAtAKerb, long CrossingsGivenBack);

    public static SignalSample Sample(string map, SimConfig config)
    {
        using var world = new TownWorld(TownReader.ReadFile(ProjectPaths.TownFile(map)), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        var signals = world.Signals;
        var roads = world.Roads;
        var plan = world.Plan;

        var litJunctions = 0;
        for (var junction = 0; junction < signals.JunctionCount; junction++)
        {
            if (signals.Lit(junction)) litJunctions++;
        }

        var litCrossings = 0;
        for (var crossing = 0; crossing < signals.CrossingCount; crossing++)
        {
            if (signals.CrossingIsLit(crossing)) litCrossings++;
        }

        var conflicting = 0L;
        var againstTraffic = 0L;
        var before = world.RedBarCrossings;
        var kerbWaitsBefore = world.KerbWaitsBegun;
        var onRedBefore = world.CrossingsBegunOnRed;
        var gaveWayBefore = world.GaveWayAtAKerb;
        var givenBackBefore = world.CrossingsGivenBack;

        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance();
            var atS = world.ElapsedS;

            for (var junction = 0; junction < signals.JunctionCount; junction++)
            {
                if (!signals.Lit(junction)) continue;

                foreach (var arm in roads.LanesIn(junction))
                {
                    foreach (var other in roads.LanesIn(junction))
                    {
                        if (signals.AxisOfLane(arm) == signals.AxisOfLane(other)) continue;
                        if (signals.ForApproach(arm, atS) == SignalColour.Red) continue;
                        if (signals.ForApproach(other, atS) == SignalColour.Red) continue;

                        conflicting++;
                    }
                }
            }

            for (var crossing = 0; crossing < signals.CrossingCount; crossing++)
            {
                if (signals.ForCrossing(crossing, atS) != SignalColour.Green) continue;

                foreach (var arm in roads.LanesIn(plan.Crosswalks.Junction[crossing]))
                {
                    if (signals.AxisOfLane(arm) != AxisOfCrossing(signals, plan, roads, crossing)) continue;
                    if (signals.ForApproach(arm, atS) == SignalColour.Red) continue;

                    againstTraffic++;
                }
            }
        }

        return new SignalSample(
            litJunctions, world.Heads.Count, litCrossings, world.Cars.Count, conflicting, againstTraffic,
            world.RecklessDrivers, world.RedBarCrossings - before, world.LastRedBarCrossing,
            world.KerbWaitsBegun - kerbWaitsBefore, world.CrossingsBegunOnRed - onRedBefore,
            world.GaveWayAtAKerb - gaveWayBefore, world.CrossingsGivenBack - givenBackBefore);
    }

    /// <summary>The axis a crossing is painted across, read back the way the service assigned it.</summary>
    static int AxisOfCrossing(SignalService signals, CityGen.CityPlan plan, World.Road.RoadGraph roads, int crossing)
    {
        var junction = plan.Crosswalks.Junction[crossing];
        var axis = plan.Crosswalks.Axis[crossing];
        if (junction < 0 || axis.LengthSquared() <= 0f) return SignalService.NoAxis;

        var along = System.Numerics.Vector2.Normalize(axis);
        var best = SignalService.NoAxis;
        var bestAgreement = -1f;
        foreach (var arm in roads.LanesIn(junction))
        {
            var agreement = MathF.Abs(System.Numerics.Vector2.Dot(roads.EndOf(arm).Direction, along));
            if (agreement <= bestAgreement) continue;

            (best, bestAgreement) = (signals.AxisOfLane(arm), agreement);
        }

        return best;
    }
}
