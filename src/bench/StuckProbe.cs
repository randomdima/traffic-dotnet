using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Who stopped and never started again</b>, over a run long enough for it to matter: every body that
/// has held one spot for longer than the ladder's own longest clock, with the state it is holding it in
/// and what is standing round it.
/// </summary>
/// <remarks>
/// <para>
/// It is the reading <see cref="DriveProbe"/>'s <c>stuck</c> column counts. That column says how many
/// went nowhere, which is the figure that says whether to look; this says <em>which one, in what state,
/// beside whom</em>, which is the only thing a fix can be written from.
/// </para>
/// <para>
/// <b>Still is measured from where the run of stillness began</b> and never tick to tick: a car creeping
/// a centimetre a second is not moving, and a tick-to-tick threshold calls it live for as long as it
/// creeps.
/// </para>
/// </remarks>
internal static class StuckProbe
{
    public const int WarmupTicks = 600;

    /// <summary>Five minutes at the shipped tick rate, which is long enough for every clock in the ladder to run out several times over.</summary>
    public const int MeasuredTicks = 18_000;

    /// <summary>How far a body may drift and still be standing in the same place: a walker's own body, near enough.</summary>
    const float MovedM = 1.0f;

    /// <summary>How long standing still stops being a queue and starts being a fault: a minute.</summary>
    const int StillTicks = 3_600;

    /// <summary>How far round a stuck body is worth reporting, which is a few car lengths.</summary>
    const float NeighbourM = 15f;

    const int Reported = 12;

    public static void Run(SimConfig config) => Run("Odesa", config);

    public static void Run(string map, SimConfig config)
    {
        using var world = new TownWorld(TownReader.ReadFile(ProjectPaths.TownFile(map)), config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance(WarmupTicks);

        var cars = world.Cars;
        var people = world.People;
        var carStillFromM = new Vector2[cars.Count];
        var carStillTicks = new int[cars.Count];
        var carWorstTicks = new int[cars.Count];
        var personStillFromM = new Vector2[people.Count];
        var personStillTicks = new int[people.Count];
        var personWorstTicks = new int[people.Count];

        for (var car = 0; car < cars.Count; car++) carStillFromM[car] = cars.PositionM[car];
        for (var person = 0; person < people.Count; person++) personStillFromM[person] = people.PositionM[person];

        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance();

            for (var car = 0; car < cars.Count; car++)
            {
                if (!Watched(cars, car))
                {
                    carStillTicks[car] = 0;
                    carStillFromM[car] = cars.PositionM[car];
                    continue;
                }

                Step(cars.PositionM[car], ref carStillFromM[car], ref carStillTicks[car], ref carWorstTicks[car]);
            }

            for (var person = 0; person < people.Count; person++)
            {
                if (!Watched(people, person))
                {
                    personStillTicks[person] = 0;
                    personStillFromM[person] = people.PositionM[person];
                    continue;
                }

                Step(
                    people.PositionM[person], ref personStillFromM[person], ref personStillTicks[person],
                    ref personWorstTicks[person]);
            }
        }

        var seconds = MeasuredTicks / config.Sim.TickRateHz;
        Console.WriteLine(
            $"stuck probe — {map}, {WarmupTicks} warm-up ticks, {MeasuredTicks} measured ({seconds} s), " +
            $"still is {MovedM:F1} m for {StillTicks / config.Sim.TickRateHz} s");
        Console.WriteLine(
            $"blocked-road clock {config.CarBlockedRoadS:F0} s, short fuse {config.CarShortFuseS:F0} s, " +
            $"shunt-round clock {config.CarShuntRoundS:F0} s, signal cycle {config.Signals.CycleS:F0} s");

        var down = 0;
        for (var person = 0; person < people.Count; person++)
        {
            if (people.Wounded[person]) down++;
        }

        var wrecked = 0;
        for (var car = 0; car < cars.Count; car++)
        {
            if (cars.Broken[car]) wrecked++;
        }

        Console.WriteLine(
            $"the town arrived at {world.WalkArrivals} walks and {world.BaysParkedIn} bays, gave up " +
            $"{world.WalksGivenUp} walks, and abandoned {world.CarsAbandoned} cars over the run");
        Console.WriteLine(
            $"it cost {down} on the ground and {wrecked} wrecked, over {world.Touches} touches — " +
            $"{world.CasualtiesRaised} raised, {world.CasualtiesCollected} collected, " +
            $"{world.CasualtiesDelivered} delivered, {world.CallsGivenUp} given up");
        Console.WriteLine(
            $"ladder: {world.LaddersClimbed} rungs — {world.BackOffsTaken} back-offs, {world.SwervesTaken} swerves, " +
            $"{world.PlacesGivenUp} places given up, {world.ReroutesTaken} reroutes, " +
            $"{world.GroundRecoveries} ground recoveries, {world.LegsSettled} settled, " +
            $"{world.CarsAbandoned} abandoned");

        ReportCars(world, config, carStillTicks, carWorstTicks);
        ReportPeople(world, config, personStillTicks, personWorstTicks);
    }

    /// <summary>One tick of one body: still while it has not left the spot the run of stillness began at.</summary>
    static void Step(Vector2 atM, ref Vector2 fromM, ref int stillTicks, ref int worstTicks)
    {
        if ((atM - fromM).Length() > MovedM)
        {
            fromM = atM;
            stillTicks = 0;
            return;
        }

        stillTicks++;
        if (stillTicks > worstTicks) worstTicks = stillTicks;
    }

    /// <summary>A car standing still is only a finding while somebody is at the wheel and it is not parked on purpose.</summary>
    static bool Watched(CarFleet cars, int car) =>
        cars.Driven[car] && !cars.Broken[car] && cars.Doing[car] != Maneuver.StandParked;

    /// <summary>And a walker's, while it is on a leg at all: a casualty, a passenger and somebody indoors are all standing still lawfully.</summary>
    static bool Watched(PersonFleet people, int person) =>
        people.Acts(person) && people.Walking[person] && people.Inside[person].Kind == ContainerKind.None;

    static void ReportCars(TownWorld world, SimConfig config, int[] stillTicks, int[] worstTicks)
    {
        var cars = world.Cars;
        var standing = 0;
        var ever = 0;
        var never = 0;
        for (var car = 0; car < cars.Count; car++)
        {
            if (stillTicks[car] >= StillTicks) standing++;
            if (worstTicks[car] >= StillTicks) ever++;
            if (worstTicks[car] >= MeasuredTicks) never++;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"cars — {standing} standing still at the end of the run, {ever} that ever were, of {Driven(cars)} driven");
        var longest = 0;
        for (var car = 0; car < cars.Count; car++) longest = Math.Max(longest, worstTicks[car]);

        Console.WriteLine(
            $"       {never} never moved at all; the longest any one of them held a spot was " +
            $"{longest / (float)config.Sim.TickRateHz:F0} s of the {MeasuredTicks / config.Sim.TickRateHz} s run");

        // What each of them says is holding it, because a queue is a consequence: what is worth reading is
        // the head of one, which is the car nothing in front of it is queueing for.
        var byHold = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var car = 0; car < cars.Count; car++)
        {
            if (stillTicks[car] < StillTicks) continue;

            var key = $"{Maneuvers.Code(cars.Doing[car])} {cars.Hold[car]} cut by {cars.GrantCutBy[car]}";
            byHold[key] = byHold.GetValueOrDefault(key) + 1;
        }

        foreach (var (key, count) in byHold.OrderByDescending(entry => entry.Value))
        {
            Console.WriteLine($"    {count,4}  {key}");
        }

        Cycles(world, stillTicks);

        foreach (var car in Worst(stillTicks, worstTicks, cars.Count, body => cars.GrantCutBy[body] != HeadwayKind.Queue))
        {
            var rearAxleM = CarFollower.RearAxleM(cars.BuildOf(car), cars.PositionM[car], cars.HeadingRad[car]);
            Console.WriteLine();
            Console.WriteLine(
                $"  car {car} at ({cars.PositionM[car].X:F1}, {cars.PositionM[car].Y:F1}) — " +
                $"still {stillTicks[car] / (float)config.Sim.TickRateHz:F0} s now, worst " +
                $"{worstTicks[car] / (float)config.Sim.TickRateHz:F0} s — {DrivingWords.CarName(cars, car)}");
            Console.WriteLine(
                $"    doing {Maneuvers.Code(cars.Doing[car])} for {cars.InManeuverS[car]:F1} s, was " +
                $"{Maneuvers.Code(cars.Was[car])}, suspended {Maneuvers.Code(cars.Suspended[car])}, " +
                $"rung {cars.Rung[car]}, back-offs {cars.BackOffs[car]}, reroutes {cars.Reroutes[car]}, " +
                $"recoveries {cars.Recoveries[car]}");
            Console.WriteLine(
                $"    hold {cars.Hold[car]}, blocked {cars.BlockedS[car]:F1} s, held back {cars.HeldBackS[car]:F1} s, " +
                $"speed {cars.AlongMps[car]:F2} m/s, off-line {cars.OffLineM[car]:F2} m, " +
                $"drivable ground {world.Terrain.At(rearAxleM).Drivable}");
            Console.WriteLine(
                $"    grant {cars.AuthorityM[car]:F2} m cut by {cars.GrantCutBy[car]}, headway " +
                $"{cars.Context[car].HeadwayM:F2} m of {cars.Context[car].Ahead} at " +
                $"{cars.Context[car].HeadwaySpeedMps:F2} m/s, stop at {cars.Context[car].StopAtM:F2} m, " +
                $"crossing stop {cars.Context[car].CrossingStopM:F2} m");
            Console.WriteLine(
                $"    line {cars.Line[car].ArcCount} arcs, progress {cars.ProgressM[car]:F1} m, lane " +
                $"{cars.LaneOf(car)}, line way {cars.LineWay[car]}, movement way {cars.MovementWay[car]}, " +
                $"claim way {cars.ClaimWay[car]}, tail way {cars.TailWay[car]}, box in {cars.ToTheBoxM[car]:F1} m " +
                $"ours {cars.BoxIsOurs[car]}, inside {cars.InsideTheBox[car]}, committed {cars.CommittedToTheBox[car]}, " +
                $"light in {cars.LightAheadM[car]:F1} m");
            Console.WriteLine(
                $"    route {cars.RouteTaken[car]}/{cars.RouteCount[car]} lanes taken, runs out " +
                $"{cars.RouteRunsOut[car]}, destination {cars.HasDestination[car]} " +
                $"({cars.DestinationM[car].X:F1}, {cars.DestinationM[car].Y:F1})");
            Neighbours(world, cars.PositionM[car]);
        }
    }

    static void ReportPeople(TownWorld world, SimConfig config, int[] stillTicks, int[] worstTicks)
    {
        var people = world.People;
        var standing = 0;
        var ever = 0;
        var never = 0;
        for (var person = 0; person < people.Count; person++)
        {
            if (stillTicks[person] >= StillTicks) standing++;
            if (worstTicks[person] >= StillTicks) ever++;
            if (worstTicks[person] >= MeasuredTicks) never++;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"walkers — {standing} standing still at the end of the run, {ever} that ever were, of {people.Count}");
        var longest = 0;
        for (var person = 0; person < people.Count; person++) longest = Math.Max(longest, worstTicks[person]);

        Console.WriteLine(
            $"          {never} never moved at all; the longest any one of them held a spot was " +
            $"{longest / (float)config.Sim.TickRateHz:F0} s of the {MeasuredTicks / config.Sim.TickRateHz} s run");

        var byStage = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var person = 0; person < people.Count; person++)
        {
            if (stillTicks[person] < StillTicks) continue;

            var key = $"{people.Stage[person]} kerb {people.HeldAtTheKerb[person]} " +
                      $"granted {(float.IsFinite(people.AuthorityM[person]) ? "cut" : "clear")}";
            byStage[key] = byStage.GetValueOrDefault(key) + 1;
        }

        foreach (var (key, count) in byStage.OrderByDescending(entry => entry.Value))
        {
            Console.WriteLine($"    {count,4}  {key}");
        }

        foreach (var person in Worst(stillTicks, worstTicks, people.Count, _ => true))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  walker {person} at ({people.PositionM[person].X:F1}, {people.PositionM[person].Y:F1}) — " +
                $"still {stillTicks[person] / (float)config.Sim.TickRateHz:F0} s now, worst " +
                $"{worstTicks[person] / (float)config.Sim.TickRateHz:F0} s");
            Console.WriteLine(
                $"    stage {people.Stage[person]}, timer {people.TimerS[person]:F1} s, walking " +
                $"{people.Walking[person]}, line {people.WalkedTaken[person]}/{people.WalkedCount[person]} taken, " +
                $"runs out {people.WalkedRunsOut[person]}, goal ({people.GoalM[person].X:F1}, " +
                $"{people.GoalM[person].Y:F1}), building {people.DestinationBuilding[person]}, car {people.TripCar[person]}");
            Console.WriteLine(
                $"    grant {people.AuthorityM[person]:F2} m of {people.ReserveAheadM[person]:F2} m asked, way " +
                $"{people.OnWay[person]} at {people.OnWayM[person]:F1} m, steps round {people.StepsRound[person]}, " +
                $"at the kerb {people.HeldAtTheKerb[person]} for {people.WaitingToCrossS[person]:F1} s, " +
                $"lane {people.WaitingForLane[person]}, refused way {people.RefusedWay[person]}, " +
                $"crossing ahead {people.CrossingAhead(person)}, walkable " +
                $"{world.Terrain.At(people.PositionM[person]).Walkable}");
            Neighbours(world, people.PositionM[person]);
        }
    }

    /// <summary>
    /// <b>Who is waiting for whom, and where that comes round on itself</b>: a ring of stuck cars each held
    /// by the next is a deadlock rather than a queue, and it is the one shape no clock behind it can clear.
    /// </summary>
    /// <remarks>
    /// The car in front is found by the geometry rather than read off the book, because the reading a driver
    /// acts on carries the distance and not whose it was. It is a probe's approximation and never a figure
    /// anything drives on: the nearest body sitting within a stride of the gap the driver said it had.
    /// </remarks>
    static void Cycles(TownWorld world, int[] stillTicks)
    {
        var cars = world.Cars;
        var infront = new int[cars.Count];
        Array.Fill(infront, -1);

        for (var car = 0; car < cars.Count; car++)
        {
            if (stillTicks[car] < StillTicks) continue;

            var gapM = cars.Context[car].HeadwayM;
            if (!float.IsFinite(gapM)) continue;

            var forward = new Vector2(MathF.Cos(cars.HeadingRad[car]), MathF.Sin(cars.HeadingRad[car]));
            var bestM = float.PositiveInfinity;
            for (var other = 0; other < cars.Count; other++)
            {
                if (other == car) continue;

                var offM = cars.PositionM[other] - cars.PositionM[car];
                if (Vector2.Dot(offM, forward) <= 0f) continue;

                var missM = MathF.Abs(offM.Length() - gapM - cars.BuildOf(car).NoseAheadOfAxleM);
                if (missM >= bestM || missM > 3f) continue;

                bestM = missM;
                infront[car] = other;
            }
        }

        Console.WriteLine();
        var rings = 0;
        var seen = new bool[cars.Count];
        for (var car = 0; car < cars.Count; car++)
        {
            if (seen[car] || infront[car] < 0) continue;

            var walk = new List<int>();
            var at = car;
            while (at >= 0 && !walk.Contains(at))
            {
                walk.Add(at);
                seen[at] = true;
                at = infront[at];
            }

            if (at < 0 || !walk.Contains(at)) continue;

            var ring = walk.GetRange(walk.IndexOf(at), walk.Count - walk.IndexOf(at));
            rings++;
            if (rings > 6) continue;

            Console.WriteLine($"    ring of {ring.Count}: " + string.Join(
                " -> ", ring.Select(body =>
                    $"{body} ({Maneuvers.Code(cars.Doing[body])} {cars.Hold[body]})")));
        }

        Console.WriteLine($"    {rings} ring(s) of cars each waiting on the next");
    }

    /// <summary>What is standing round the body, because a body that stopped is usually stopped by another one.</summary>
    static void Neighbours(TownWorld world, Vector2 atM)
    {
        var cars = world.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            var awayM = (cars.PositionM[car] - atM).Length();
            if (awayM > NeighbourM || awayM < 0.01f) continue;

            Console.WriteLine(
                $"      car {car} {awayM:F1} m off — {DrivingWords.CarName(cars, car)}, " +
                $"{cars.AlongMps[car]:F2} m/s, grant {cars.AuthorityM[car]:F2} m cut by {cars.GrantCutBy[car]}");
        }

        var people = world.People;
        for (var person = 0; person < people.Count; person++)
        {
            var awayM = (people.PositionM[person] - atM).Length();
            if (awayM > NeighbourM || awayM < 0.01f) continue;

            Console.WriteLine(
                $"      walker {person} {awayM:F1} m off — {people.Stage[person]}, walking {people.Walking[person]}, " +
                $"kerb {people.HeldAtTheKerb[person]}, grant {people.AuthorityM[person]:F2} m");
        }
    }

    /// <summary>
    /// The bodies still standing where they were, worst first — and where none is, the ones that were and got
    /// out of it. <paramref name="worthReading"/> takes the queue out: a car held by the car in front is a
    /// consequence of whatever is at the head of it, and the head is the only one a fix is written from.
    /// </summary>
    static int[] Worst(int[] stillTicks, int[] worstTicks, int count, Func<int, bool> worthReading)
    {
        var found = new List<int>();
        for (var body = 0; body < count; body++)
        {
            if (stillTicks[body] >= StillTicks && worthReading(body)) found.Add(body);
        }

        found.Sort((a, b) => stillTicks[b].CompareTo(stillTicks[a]));
        if (found.Count == 0)
        {
            for (var body = 0; body < count; body++)
            {
                if (worstTicks[body] >= StillTicks) found.Add(body);
            }

            found.Sort((a, b) => worstTicks[b].CompareTo(worstTicks[a]));
        }

        return found.Count > Reported ? found.GetRange(0, Reported).ToArray() : found.ToArray();
    }

    static int Driven(CarFleet cars)
    {
        var driven = 0;
        for (var car = 0; car < cars.Count; car++)
        {
            if (cars.Driven[car]) driven++;
        }

        return driven;
    }
}
