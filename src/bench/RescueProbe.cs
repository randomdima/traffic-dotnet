using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>One town's rescue, staged and then watched to its end.</summary>
/// <param name="Hospitals">How many buildings the seed drew as hospitals (AMB-1).</param>
/// <param name="Ambulances">And how many of those actually had a bay near them to stand one in (AMB-2).</param>
/// <param name="NearestM">
/// The closest any answering ambulance ever got to the body. <b>The column that says which half of a
/// failure this is</b>: a call that gave up from two kilometres away is a road nothing could get through,
/// and one that gave up from six metres is a car that arrived and then could not finish arriving.
/// </param>
/// <param name="NearestAtRestM">
/// And the closest it ever got <em>standing still</em>, which is the only kind of closeness the crew can
/// work from (AMB-6). <b>The two apart are the diagnosis</b>: near and never still is a car that drove past
/// its casualty, and never near at all is a road it could not get through.
/// </param>
/// <param name="DoingThere">The catalogue entry in charge at its nearest approach — which entry was holding the car as the body came up.</param>
/// <param name="MostLoadedS">The furthest the crew's own clock ever got, against the interval a loading takes.</param>
/// <param name="InReachS">How long in all it stood where the crew could have worked, and…</param>
/// <param name="TopMpsInReach">…the fastest it was moving while it was there.</param>
/// <param name="OffTheLaneM">How far off its nearest lane's own line the staged casualty lies, since a body the road does not run past is nobody's to fetch.</param>
/// <param name="EndedIn">And the leg it was on when the clock ran out, or <c>Waiting</c> if nobody took it.</param>
internal readonly record struct RescueRow(
    string Map, int Hospitals, int Ambulances, long Raised, long Collected, long Delivered, long GivenUp,
    long DoorsFull, float ReachedInS, float DeliveredInS, float NearestM, float NearestAtRestM,
    Maneuver DoingThere, float MostLoadedS, float InReachS, float TopMpsInReach, float OffTheLaneM,
    RescueStage EndedIn);

/// <summary>
/// <b>The rescue, end to end</b>: somebody is knocked down on a shipped town, and the probe watches
/// whether an ambulance comes, gets them aboard and delivers them through a hospital's door.
/// </summary>
/// <remarks>
/// <para>
/// <b>The casualty is staged through the damage roster and not through a seam of its own.</b> What the
/// probe does to the town is exactly what a car knocking somebody down does — one call
/// to <see cref="TownWorld.Apply"/> with the band the arithmetic would have produced — so a rescue that
/// works here is a rescue that works when a car actually does it. The crash sandbox is where the
/// arithmetic that picks that band is measured; this is what happens afterwards.
/// </para>
/// <para>
/// <b>Three columns and not one</b>, because no one of them means anything alone: raised without
/// collected is an ambulance that never arrived, collected without delivered is one that could not get
/// through a door, and either without the two times beside them says nothing about whether a rescue is
/// quick enough to be worth having.
/// </para>
/// <para>
/// <b>And the columns after them are there to say which failure it was</b>, because a probe that reports
/// only that a rescue did not happen leaves the reader to guess between a road it never got down, a body
/// it drove past and a crew that could not finish. Every one of those has been the answer at least once,
/// and each of them is a different fix.
/// </para>
/// <para>
/// <b>The staged casualty is the one standing nearest a road.</b> An ambulance stops in the carriageway
/// beside a body, so somebody knocked down in the middle of a park is a call nothing can reach — a real
/// state (AMB-9), and not the one this probe is for.
/// </para>
/// </remarks>
internal static class RescueProbe
{
    /// <summary>Long enough for the town to have people out on it before anybody is knocked down.</summary>
    public const int WarmupTicks = 600;

    /// <summary>And long enough for a rescue to cross a city and get back in through a door: ten minutes.</summary>
    public const int MeasuredTicks = 36_000;

    public static void Run(SimConfig config)
    {
        Console.WriteLine(
            $"rescue probe — one staged casualty a town, {MeasuredTicks / config.Sim.TickRateHz:F0} s to answer it; " +
            $"treatment {config.Ambulance.TreatmentS:F0} s, a call gives up at {config.AmbulanceGiveUpS:F0} s");
        Console.WriteLine(
            $"{"map",-10}{"hospitals",11}{"ambulances",12}{"raised",8}{"collected",11}{"delivered",11}" +
            $"{"given up",10}{"door full",11}{"reached s",11}{"door s",9}{"nearest m",11}{"at rest m",11}" +
            $"{"doing there",20}{"loaded s",10}{"in reach s",12}{"top mps",9}{"off lane m",12}{"ended in",14}");

        foreach (var map in ProjectPaths.ShippedMaps())
        {
            var row = Sample(map, config);
            Console.WriteLine(
                $"{row.Map,-10}{row.Hospitals,11}{row.Ambulances,12}{row.Raised,8}{row.Collected,11}" +
                $"{row.Delivered,11}{row.GivenUp,10}{row.DoorsFull,11}{Seconds(row.ReachedInS),11}" +
                $"{Seconds(row.DeliveredInS),9}{Metres(row.NearestM),11}{Metres(row.NearestAtRestM),11}" +
                $"{row.DoingThere,20}{row.MostLoadedS,10:F1}{row.InReachS,12:F1}{row.TopMpsInReach,9:F2}"
                + $"{row.OffTheLaneM,12:F1}{row.EndedIn,14}");
        }

        Console.WriteLine(
            "AMB-8 is met while a staged casualty is being collected and delivered: raised → collected → delivered " +
            "is one whole rescue, and a map with no ambulance on it is a map with no bay near its hospitals.");
    }

    static string Seconds(float s) => float.IsPositiveInfinity(s) ? "—" : $"{s:F1}";

    static string Metres(float m) => float.IsPositiveInfinity(m) ? "—" : $"{m:F1}";

    public static RescueRow Sample(string map, SimConfig config)
    {
        using var world = new TownWorld(TownReader.ReadFile(ProjectPaths.TownFile(map)), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        var casualty = NearestToARoad(world, out var offTheLaneM);
        if (casualty < 0 || world.Ambulances == 0)
        {
            return new RescueRow(
                map, world.Hospitals.Count, world.Ambulances, 0, 0, 0, 0, 0,
                float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity,
                Maneuver.None, 0f, 0f, 0f, 0f, RescueStage.Waiting);
        }

        world.Apply(new BodyTag(BodyKind.Person, casualty), DamageOutcome.Wounded);

        var reachedInS = float.PositiveInfinity;
        var deliveredInS = float.PositiveInfinity;
        var nearestM = float.PositiveInfinity;
        var nearestAtRestM = float.PositiveInfinity;
        var doingThere = Maneuver.None;
        var mostLoadedS = 0f;
        var inReachS = 0f;
        var topMpsInReach = 0f;
        var endedIn = RescueStage.Waiting;
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance(1);
            Watch(
                world, config, casualty, ref nearestM, ref nearestAtRestM, ref doingThere, ref mostLoadedS,
                ref inReachS, ref topMpsInReach, ref endedIn);

            var atS = (tick + 1) * config.TickSeconds;
            if (world.CasualtiesCollected > 0 && float.IsPositiveInfinity(reachedInS)) reachedInS = atS;
            if (world.CasualtiesDelivered == 0) continue;

            deliveredInS = atS;
            break;
        }

        return new RescueRow(
            map, world.Hospitals.Count, world.Ambulances, world.CasualtiesRaised, world.CasualtiesCollected,
            world.CasualtiesDelivered, world.CallsGivenUp, world.DoorsFoundFull, reachedInS, deliveredInS,
            nearestM, nearestAtRestM, doingThere, mostLoadedS, inReachS, topMpsInReach, offTheLaneM, endedIn);
    }

    /// <summary>
    /// <b>How near the crew ever got, and what they were doing when the clock stopped.</b> Read off
    /// whichever ambulance holds this call — the call moves between crews as one gives up and the next
    /// takes it, and what is wanted is the best any of them managed.
    /// </summary>
    /// <remarks>
    /// Taken while the casualty is still in the road. Once they are aboard their position is the car's, and
    /// a distance from a car to its own passenger is zero for the rest of the run.
    /// </remarks>
    static void Watch(
        TownWorld world, SimConfig config, int casualty, ref float nearestM, ref float nearestAtRestM,
        ref Maneuver doingThere, ref float mostLoadedS, ref float inReachS, ref float topMpsInReach,
        ref RescueStage endedIn)
    {
        if (!world.People.Wounded[casualty] || world.People.Inside[casualty].Any) return;

        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Duty.Casualty[car] != casualty) continue;

            endedIn = world.Duty.Stage[car];
            if (world.Duty.LoadedForS[car] > mostLoadedS) mostLoadedS = world.Duty.LoadedForS[car];
            var farM = (world.Cars.PositionM[car] - world.People.PositionM[casualty]).Length();
            if (farM < nearestM)
            {
                nearestM = farM;
                doingThere = world.Cars.Doing[car];
            }

            // The same distance asked only of the ticks the crew could have worked in. The two columns
            // apart are the whole diagnosis: near and never still is a car that drove past its casualty.
            if (world.Cars.VelocityMps[car].Length() <= config.Driving.StopSpeedMps && farM < nearestAtRestM)
            {
                nearestAtRestM = farM;
            }

            // Time spent where the crew could have worked, against the fastest the car was moving there.
            // A long time in reach and a top speed above the stop bar is a car that never stood still.
            if (farM <= config.AmbulanceSceneReachM)
            {
                inReachS += config.TickSeconds;
                topMpsInReach = MathF.Max(topMpsInReach, world.Cars.VelocityMps[car].Length());
            }

            return;
        }
    }

    /// <summary>
    /// The walker standing nearest a lane's own centreline, which is the one a rescue can actually be
    /// staged on. Skips anybody already inside something: a person in a building is not in the road.
    /// </summary>
    static int NearestToARoad(TownWorld world, out float offM)
    {
        var best = -1;
        var bestM = float.PositiveInfinity;
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.Wounded[person]) continue;
            if (world.People.Inside[person].Any) continue;

            var positionM = world.People.PositionM[person];
            var lane = world.Roads.NearestLane(positionM, out var alongM);
            if (lane < 0) continue;

            var farM = (Spline.SampleAt(world.Roads.ArcsOf(lane), alongM).PositionM - positionM).Length();
            if (farM >= bestM) continue;

            best = person;
            bestM = farM;
        }

        offM = bestM;
        return best;
    }
}
