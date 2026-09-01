using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Town;

using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>One town's recovery, staged and then watched to its end.</summary>
/// <param name="Depots">How many buildings the seed drew as depots (SRV-1).</param>
/// <param name="Evacuators">And how many of those actually had a bay near them to stand one in (SRV-2).</param>
/// <param name="YardSlots">How many wreck slots those depots' yards found room for (EVA-2), which can be fewer than the figure asks.</param>
/// <param name="NearestM">
/// The closest the answering evacuator ever got to the wreck. <b>The column that says which half of a
/// failure this is</b>: a recovery that gave up from two kilometres away is a road nothing could get
/// through, and one that gave up from six metres is a truck that arrived and could not finish arriving.
/// </param>
/// <param name="NearestAtRestM">And the closest it got standing still, which is the only closeness the crew can work from.</param>
/// <param name="DoingThere">The catalogue entry in charge at its nearest approach.</param>
/// <param name="TowedM">
/// How far the wreck was actually dragged, measured along the ground it covered. <b>The column that says
/// the coupling worked</b>: a tow that reports metres is a bar that held, and one that reports nothing is a
/// wreck left where it lay however many stages the machine went through.
/// </param>
/// <param name="WorstStretchM">
/// And the furthest the bar was ever stretched — how far apart the hook and the eye got. It is the whole
/// quality of the coupling in one number: centimetres is a tow bar, metres is a rope.
/// </param>
/// <param name="EndedIn">The stage the evacuator was in when the clock ran out, or <c>Waiting</c> if nobody took it.</param>
internal readonly record struct RecoveryRow(
    string Map, int Depots, int Evacuators, int YardSlots, long Raised, long Hitched, long Yarded,
    long Restored, long GivenUp, long YardFull, float ReachedInS, float YardedInS, float RestoredInS,
    float NearestM, float NearestAtRestM, Maneuver DoingThere, float TowedM, float WorstStretchM,
    RecoveryStage EndedIn);

/// <summary>
/// <b>The recovery, end to end</b>: a car is wrecked on a shipped town, and the probe watches whether an
/// evacuator comes, gets it onto the bar, drags it to a depot's yard and leaves it there to be a car again.
/// </summary>
/// <remarks>
/// <para>
/// <b>The wreck is staged through the damage roster and not through a seam of its own</b>, exactly as the
/// rescue's casualty is (<see cref="RescueProbe"/>): one call to <see cref="TownWorld.Apply"/> with the
/// band the arithmetic would have produced, so a recovery that works here is one that works when a car
/// actually crashes.
/// </para>
/// <para>
/// <b>Four counts and not one</b>, because no one of them means anything alone: raised without hitched is
/// an evacuator that never arrived, hitched without yarded is a tow that never got home, and yarded without
/// restored is a workshop that never finished. The two coupling columns beside them are what says the drag
/// itself was a tow rather than a shove.
/// </para>
/// <para>
/// <b>The staged wreck is the parked car standing nearest a road.</b> An evacuator stops in the carriageway
/// beside what it has come for, so a car wrecked in the middle of a lot is a recovery nothing can reach —
/// a real state (EVA-8), and not the one this probe is for.
/// </para>
/// </remarks>
internal static class RecoveryProbe
{
    /// <summary>Long enough for the town to be moving before anything is wrecked in it.</summary>
    public const int WarmupTicks = 600;

    /// <summary>
    /// And long enough to fetch a wreck, drag it home and mend it: seven minutes. <b>Shorter than a
    /// rescue's window on purpose</b> — what this probe is for is whether a recovery gets under way and
    /// holds together, and a tow still on the road after seven minutes is a reading about that city's
    /// geometry (EVA-8) that another ten minutes does not change.
    /// </summary>
    public const int MeasuredTicks = 24_000;

    public static void Run(SimConfig config)
    {
        Console.WriteLine(
            $"recovery probe — one staged wreck a town, {MeasuredTicks / config.Sim.TickRateHz:F0} s to answer it; " +
            $"repair {config.Evacuator.RepairS:F0} s, a leg gives up at {config.EvacuatorGiveUpS:F0} s, " +
            $"the arm reaches {ArmReachM(config):F2} m");
        Console.WriteLine(
            $"{"map",-10}{"depots",8}{"evacuators",12}{"slots",7}{"raised",8}{"hitched",9}{"yarded",8}" +
            $"{"restored",10}{"given up",10}{"yard full",11}{"reached s",11}{"yard s",9}{"mended s",10}" +
            $"{"nearest m",11}{"at rest m",11}{"doing there",20}{"towed m",10}{"stretch m",11}{"ended in",13}");

        foreach (var map in ProjectPaths.ShippedMaps())
        {
            var row = Sample(map, config);
            Console.WriteLine(
                $"{row.Map,-10}{row.Depots,8}{row.Evacuators,12}{row.YardSlots,7}{row.Raised,8}{row.Hitched,9}" +
                $"{row.Yarded,8}{row.Restored,10}{row.GivenUp,10}{row.YardFull,11}{Seconds(row.ReachedInS),11}" +
                $"{Seconds(row.YardedInS),9}{Seconds(row.RestoredInS),10}{Metres(row.NearestM),11}" +
                $"{Metres(row.NearestAtRestM),11}{row.DoingThere,20}{row.TowedM,10:F1}{row.WorstStretchM,11:F2}" +
                $"{row.EndedIn,13}");
        }

        Console.WriteLine(
            "EVA-6 is met while a staged wreck is being fetched, towed and set down: raised → hitched → yarded → " +
            "restored is one whole recovery, and a map with no evacuator on it is a map with no bay near its depots.");
    }

    /// <summary>The reach the evacuator's own picture was drawn at, which is the length its tow is held at (EVA-5).</summary>
    static float ArmReachM(SimConfig config) =>
        CarBuild.Of(config, CarCatalog.Shared.Variants[CarCatalog.Shared.Evacuator]).TowReachM;

    static string Seconds(float s) => float.IsPositiveInfinity(s) ? "—" : $"{s:F1}";

    static string Metres(float m) => float.IsPositiveInfinity(m) ? "—" : $"{m:F1}";

    public static RecoveryRow Sample(string map, SimConfig config)
    {
        using var world = new TownWorld(Maps.Plan(map, config, BuildingCatalog.Shared.OrdinaryFootprintsM()), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        var slots = YardSlots(world);
        var wreck = NearestToARoad(world, config);
        if (wreck < 0 || world.Evacuators == 0)
        {
            return new RecoveryRow(
                map, world.Depots.Count, world.Evacuators, slots, 0, 0, 0, 0, 0, 0,
                float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity,
                float.PositiveInfinity, Maneuver.None, 0f, 0f, RecoveryStage.Waiting);
        }

        world.Apply(new BodyTag(BodyKind.Car, wreck), DamageOutcome.Broken);

        var watch = new Watched();
        var reachedInS = float.PositiveInfinity;
        var yardedInS = float.PositiveInfinity;
        var restoredInS = float.PositiveInfinity;
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance(1);
            Watch(world, config, wreck, ref watch);

            var atS = (tick + 1) * config.TickSeconds;
            if (world.WrecksHitched > 0 && float.IsPositiveInfinity(reachedInS)) reachedInS = atS;
            if (world.WrecksYarded > 0 && float.IsPositiveInfinity(yardedInS)) yardedInS = atS;
            if (world.WrecksRestored == 0) continue;

            restoredInS = atS;
            break;
        }

        return new RecoveryRow(
            map, world.Depots.Count, world.Evacuators, slots, world.WrecksRaised, world.WrecksHitched,
            world.WrecksYarded, world.WrecksRestored, world.RecoveriesGivenUp, world.YardsFoundFull,
            reachedInS, yardedInS, restoredInS, watch.NearestM, watch.NearestAtRestM, watch.DoingThere,
            watch.TowedM, watch.WorstStretchM, watch.EndedIn);
    }

    /// <summary>What one tick of watching adds up to, gathered so the sampler passes one thing rather than eight.</summary>
    struct Watched()
    {
        public float NearestM = float.PositiveInfinity;
        public float NearestAtRestM = float.PositiveInfinity;
        public Maneuver DoingThere = Maneuver.None;
        public float TowedM;
        public float WorstStretchM;
        public RecoveryStage EndedIn = RecoveryStage.Waiting;
        public Vector2 WasAtM;
        public bool Moved;
    }

    /// <summary>
    /// <b>How near the crew got, how far the wreck was dragged, and how far the bar was ever stretched.</b>
    /// Read off whichever evacuator holds this wreck, because the recovery moves between crews as one gives
    /// up and the next takes it.
    /// </summary>
    static void Watch(TownWorld world, SimConfig config, int wreck, ref Watched watch)
    {
        // The ground the wreck covered while it was on a bar, and never the ground it covered being shunted
        // about the street by whatever hit it next: what this column has to mean is that the tow moved it.
        var hauler = world.Recovery.OnTheHookOf[wreck];
        var atM = world.Cars.PositionM[wreck];
        if (hauler >= 0)
        {
            if (watch.Moved) watch.TowedM += (atM - watch.WasAtM).Length();
            watch.WasAtM = atM;
            watch.Moved = true;

            var stretchM = TheStretchM(world, hauler, wreck);
            if (stretchM > watch.WorstStretchM) watch.WorstStretchM = stretchM;
        }
        else
        {
            watch.Moved = false;
        }

        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Recovery.Wreck[car] != wreck) continue;

            watch.EndedIn = world.Recovery.Stage[car];
            var farM = (world.Cars.PositionM[car] - atM).Length();
            if (farM < watch.NearestM)
            {
                watch.NearestM = farM;
                watch.DoingThere = world.Cars.Doing[car];
            }

            if (world.Cars.VelocityMps[car].Length() <= config.Driving.StopSpeedMps && farM < watch.NearestAtRestM)
            {
                watch.NearestAtRestM = farM;
            }

            return;
        }
    }

    /// <summary>How far the hinge and the point the arm holds have got apart, which is the coupling's own error.</summary>
    static float TheStretchM(TownWorld world, int hauler, int wreck) =>
        (TowBar.HookM(world.Cars.BuildOf(hauler), PoseOf(world, hauler))
            - TowBar.EyeM(
                world.Cars.BuildOf(wreck), PoseOf(world, wreck), world.Cars.BuildOf(hauler).TowReachM,
                world.Recovery.HeldByTheTail[wreck]))
        .Length();

    /// <summary>The body as the tow bar reads it, off the arrays the fleet already publishes.</summary>
    static CarPose PoseOf(TownWorld world, int car) => new(
        world.Cars.PositionM[car], world.Cars.HeadingRad[car], world.Cars.VelocityMps[car],
        world.Cars.YawRateRadPerS[car], world.Cars.MassKg[car], world.Cars.AccelerationMps2[car]);

    /// <summary>How many yard slots the town's depots actually found room for (EVA-2).</summary>
    static int YardSlots(TownWorld world)
    {
        var slots = 0;
        for (var bay = 0; bay < world.Parking.BayCount; bay++)
        {
            if (world.Parking.HeldFor(bay) == ParkingRegistry.TheYard) slots++;
        }

        return slots;
    }

    /// <summary>
    /// The parked car standing nearest a lane's own centreline, <b>of those within a few blocks of one of
    /// the town's depots</b>. Skips anything already broken, anything with somebody in it, and every service
    /// vehicle: they all break (SRV-4), and wrecking the truck this probe is watching — or the ambulance
    /// that would come for the casualty — is a different experiment from the one it is running.
    /// </summary>
    /// <remarks>
    /// <b>Near a lane and near a depot, and both for the same reason.</b> An evacuator stops in the
    /// carriageway beside what it has come for, so a car wrecked in the middle of a lot is a recovery
    /// nothing can reach; and a town stands two evacuators rather than a hospital's twenty, so one wrecked
    /// on the far side of a city is a leg that spends its whole bound in the traffic. Both are real states
    /// (EVA-8) and neither is the one this probe is for — what it asks is whether a recovery that <em>can</em>
    /// be run is run.
    /// </remarks>
    static int NearestToARoad(TownWorld world, SimConfig config)
    {
        var reachM = config.CityGen.BlockSpacingAlongMinM * BlocksFromADepot;
        var best = -1;
        var bestM = float.PositiveInfinity;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Broken[car] || CarCatalog.Shared.IsService(world.Cars.Variant[car])) continue;
            if (!world.Containment.IsFree(car)) continue;

            var positionM = world.Cars.PositionM[car];
            if (ToTheNearestDepotM(world, positionM) > reachM) continue;

            var lane = world.Roads.NearestLane(positionM, out var alongM);
            if (lane < 0) continue;

            var farM = (Spline.SampleAt(world.Roads.ArcsOf(lane), alongM).PositionM - positionM).Length();
            if (farM >= bestM) continue;

            best = car;
            bestM = farM;
        }

        return best;
    }

    /// <summary>How far this place stands from the nearest depot, or infinity on a map that has none.</summary>
    static float ToTheNearestDepotM(TownWorld world, Vector2 placeM)
    {
        var bestM = float.PositiveInfinity;
        for (var entry = 0; entry < world.Depots.Count; entry++)
        {
            var farM = (world.Plan.Buildings.CentreM[world.Depots.BuildingOf(entry)] - placeM).Length();
            if (farM < bestM) bestM = farM;
        }

        return bestM;
    }

    /// <summary>How far from a depot a staged wreck may stand, in block spacings — near enough that the leg is a recovery and not a crossing of the city.</summary>
    const float BlocksFromADepot = 4f;
}
