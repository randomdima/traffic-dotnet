using System.Numerics;
using System.Text;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

using TrafficSimulation.World.Statics;

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
    public const float MovedM = 1.0f;

    /// <summary>How long standing still stops being a queue and starts being a fault: a minute.</summary>
    const int StillTicks = 3_600;

    /// <summary>How far round a stuck body is worth reporting, which is a few car lengths.</summary>
    const float NeighbourM = 15f;

    const int Reported = 12;

    /// <summary>The smallest gathering worth the name: four abreast is a heap and never a queue.</summary>
    const int CrowdOf = 4;

    /// <summary>
    /// How often the crowds are counted — a second at the shipped tick rate, which is often enough to
    /// catch one that forms and clears and rare enough that counting them pair by pair costs nothing.
    /// </summary>
    const int CrowdEvery = 60;

    public static void Run(SimConfig config) => Run("Odesa", config);

    public static void Run(string map, SimConfig config)
    {
        using var world = new TownWorld(Maps.Plan(map, config, BuildingCatalog.Roofs), config);
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
        var crowd = new int[people.Count];
        var crowdSize = new int[people.Count];
        var inACrowdTicks = new int[people.Count];
        var biggestCrowd = 0;
        var biggestCrowdTick = 0;
        var biggestCrowdSays = new List<string>();

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

            if (tick % CrowdEvery != 0) continue;

            GatherCrowds(people, TouchingM(config), crowd, crowdSize);
            var head = Nobody;
            for (var person = 0; person < people.Count; person++)
            {
                var size = crowdSize[RootOf(crowd, person)];
                if (size < CrowdOf) continue;

                inACrowdTicks[person] += CrowdEvery;
                if (size <= biggestCrowd) continue;

                biggestCrowd = size;
                biggestCrowdTick = tick;
                head = RootOf(crowd, person);
            }

            // Said where it stands, because a heap seen at the end of the run is whichever one happened to
            // be standing then: the worst of them formed and cleared while nobody was looking.
            if (head == Nobody) continue;

            biggestCrowdSays.Clear();
            SayTheCrowd(people, crowd, head, biggestCrowdSays);
        }

        var seconds = MeasuredTicks / config.Sim.TickRateHz;
        Console.WriteLine(
            $"stuck probe — {map}, {WarmupTicks} warm-up ticks, {MeasuredTicks} measured ({seconds} s), " +
            $"still is {MovedM:F1} m for {StillTicks / config.Sim.TickRateHz} s");
        Console.WriteLine(
            $"a leg's patience {config.CarPatienceS:F0} s over {config.Patience.ReroutesPerLeg} reroutes, " +
            $"signal cycle {config.Signals.CycleS:F0} s");

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
            $"{world.WalksGivenUp} walks, set {world.WalkersSetDown} of them back on the pavement, and " +
            $"gave up {world.LegsGivenUp} drive legs over the run");
        Console.WriteLine(
            $"it cost {down} on the ground and {wrecked} wrecked, over {world.Touches} touches — " +
            $"{world.CasualtiesRaised} raised, {world.CasualtiesCollected} collected, " +
            $"{world.CasualtiesDelivered} delivered, {world.CallsGivenUp} given up");
        Console.WriteLine(
            $"legs: {world.ReroutesTaken} rerouted, {world.PlacesGivenUp} places given up, " +
            $"{world.LinesReacquired} lines taken again, {world.LegsGivenUp} given up — and " +
            $"{world.HardBrakings} car-ticks spent the braking margin");

        ReportCars(world, config, carStillTicks, carWorstTicks);
        ReportPeople(world, config, personStillTicks, personWorstTicks);
        ReportCrowds(world, config, inACrowdTicks, crowd, crowdSize, biggestCrowd, biggestCrowdTick, biggestCrowdSays);
    }

    /// <summary>No walker at all — what a search of the roster comes back with when it finds nobody.</summary>
    const int Nobody = -1;

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
    static bool Watched(CarFleet cars, int car) => cars.Driven[car] && !cars.Broken[car];

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

            var key = $"{DrivingWords.CarName(cars, car)} cut by {cars.GrantCutBy[car]}";
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
                $"    hold {cars.Hold[car]}, getting nowhere for {world.GettingNowhereForS(car):F1} s, reroutes " +
                $"{cars.Reroutes[car]}, speed {cars.AlongMps[car]:F2} m/s, off-line {cars.OffLineM[car]:F2} m, " +
                $"drivable ground {world.Terrain.At(rearAxleM).Drivable}");
            Console.WriteLine(
                $"    grant {cars.AuthorityM[car]:F2} m cut by {cars.GrantCutBy[car]} {WhatHeld(world, world.DriveHold(car))}, headway " +
                $"{cars.Context[car].HeadwayM:F2} m of {cars.Context[car].Ahead} at " +
                $"{cars.Context[car].HeadwaySpeedMps:F2} m/s, stop at {cars.Context[car].StopAtM:F2} m, " +
                $"crossing stop {cars.Context[car].CrossingStopM:F2} m");
            Console.WriteLine(
                $"    line {cars.Line[car].ArcCount} arcs, progress {cars.ProgressM[car]:F1} m, lane " +
                $"{cars.LaneOf(car)}, line way {cars.LineWay[car]}, plan {cars.ClaimFromM[car]:F1}–{cars.ClaimToM[car]:F1} m " +
                $"committed to {cars.CommittedToM[car]:F1} m, " +
                $"tail way {cars.TailWay[car]}, box in {cars.ToTheBoxM[car]:F1} m " +
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

            var key = $"{people.Stage[person]} walking {people.Walking[person]} " +
                      $"on {(people.OnWay[person] == PersonFleet.NoWay ? "no way" : "a way")}";
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
                $"{people.Walking[person]}, route {people.RouteTaken[person]}/{people.RouteCount[person]} taken, " +
                $"runs out {people.RouteRunsOut[person]}, goal ({people.GoalM[person].X:F1}, " +
                $"{people.GoalM[person].Y:F1}), building {people.DestinationBuilding[person]}");
            var ground = world.Terrain.At(people.PositionM[person]);
            Console.WriteLine(
                $"    way {people.OnWay[person]} at {people.OnWayM[person]:F1} m, {people.OffWayM[person]:F2} m off, " +
                $"crossing {people.OnCrossing[person]}, walkable {ground.Walkable} at {ground.Coefficient:F2}");

            // <b>What it is aiming at and what it is doing about it</b>, which is the difference between a
            // body that has nowhere to go and a body leaning on something that will not move.
            Console.WriteLine(
                $"    aiming {(people.DestinationM[person] - people.PositionM[person]).Length():F2} m away at " +
                $"({people.DestinationM[person].X:F1}, {people.DestinationM[person].Y:F1}), at " +
                $"{people.VelocityMps[person].Length():F2} m/s");
            Console.WriteLine($"    grant {people.GrantM[person]:F2} m {WhatHeld(world, world.WalkHold(person))}");
            Neighbours(world, people.PositionM[person]);
        }
    }

    /// <summary>
    /// <b>How near two walkers stand when they are in the same heap</b>: well inside the gap a walker
    /// states in front of itself (PER-26, <see cref="SimConfig.PersonStandstillGapM"/>) — half of it, so
    /// that bodies at the distance the pavement is laid for are never counted as a heap and a pair
    /// standing at half of it always are.
    /// </summary>
    static float TouchingM(SimConfig config) =>
        config.PersonDiameterM + (config.PersonStandstillGapM * 0.5f);

    /// <summary>
    /// <b>The heaps the walkers are standing in</b>, gathered by nothing but who is touching whom.
    /// </summary>
    /// <remarks>
    /// <b>It is the reading <see cref="Step"/> cannot take.</b> A body in a heap is shoved about by the
    /// bodies round it, so it never holds one spot and never counts as still; what stands where it is, is
    /// the crowd, and the only thing that says so is how many are in it.
    /// </remarks>
    static void GatherCrowds(PersonFleet people, float touchingM, int[] crowd, int[] size)
    {
        for (var person = 0; person < people.Count; person++)
        {
            crowd[person] = person;
            size[person] = 0;
        }

        var touchingSqM = touchingM * touchingM;
        for (var person = 0; person < people.Count; person++)
        {
            if (!InTheStreet(people, person)) continue;

            for (var other = person + 1; other < people.Count; other++)
            {
                if (!InTheStreet(people, other)) continue;
                if ((people.PositionM[person] - people.PositionM[other]).LengthSquared() > touchingSqM) continue;

                var one = RootOf(crowd, person);
                var two = RootOf(crowd, other);
                if (one != two) crowd[one] = two;
            }
        }

        for (var person = 0; person < people.Count; person++)
        {
            if (InTheStreet(people, person)) size[RootOf(crowd, person)]++;
        }
    }

    static int RootOf(int[] crowd, int person)
    {
        while (crowd[person] != person) person = crowd[person] = crowd[crowd[person]];

        return person;
    }

    /// <summary>A body anybody can walk into: on the roster, out of doors, and out of a car.</summary>
    static bool InTheStreet(PersonFleet people, int person) =>
        people.Acts(person) && people.Inside[person].Kind == ContainerKind.None;

    /// <summary>
    /// One heap, member by member: where each of them stands in it and what each says it is doing.
    /// </summary>
    static void SayTheCrowd(PersonFleet people, int[] crowd, int head, List<string> into)
    {
        var atM = people.PositionM[head];
        into.Add($"at ({atM.X:F1}, {atM.Y:F1})");
        for (var person = 0; person < people.Count && into.Count <= Reported; person++)
        {
            if (!InTheStreet(people, person) || RootOf(crowd, person) != head) continue;

            into.Add(
                $"    walker {person} {(people.PositionM[person] - atM).Length():F1} m in — " +
                $"{people.Stage[person]}, walking {people.Walking[person]}, route " +
                $"{people.RouteTaken[person]}/{people.RouteCount[person]}, at " +
                $"{people.OnWayM[person]:F1} m of way {people.OnWay[person]}, building " +
                $"{people.DestinationBuilding[person]}, goal ({people.GoalM[person].X:F1}, {people.GoalM[person].Y:F1})");
        }
    }

    static void ReportCrowds(
        TownWorld world, SimConfig config, int[] inACrowdTicks, int[] crowd, int[] size, int biggest,
        int biggestTick, List<string> biggestSays)
    {
        var people = world.People;
        Console.WriteLine();
        Console.WriteLine(
            $"crowds — a heap is {CrowdOf}+ walkers within {TouchingM(config):F2} m of one another, counted every " +
            $"{CrowdEvery / config.Sim.TickRateHz:F0} s");

        var ever = 0;
        var worst = 0;
        var spentTicks = 0L;
        for (var person = 0; person < people.Count; person++)
        {
            if (inACrowdTicks[person] > 0) ever++;
            if (inACrowdTicks[person] > worst) worst = inACrowdTicks[person];
            spentTicks += inACrowdTicks[person];
        }

        Console.WriteLine(
            $"         {ever} of {people.Count} were in one at some point; the town spent " +
            $"{100f * spentTicks / (people.Count * (float)MeasuredTicks):F1}% of its walking on foot in one, and " +
            $"the worst-off spent {100f * worst / MeasuredTicks:F0}% of the run in one");

        Console.WriteLine();
        Console.WriteLine($"  the biggest was {biggest}, {biggestTick / config.Sim.TickRateHz:F0} s in — as it stood");
        foreach (var says in biggestSays) Console.WriteLine(says);

        GatherCrowds(people, TouchingM(config), crowd, size);
        var standing = new List<int>();
        for (var person = 0; person < people.Count; person++)
        {
            if (size[person] >= CrowdOf) standing.Add(person);
        }

        standing.Sort((a, b) => size[b].CompareTo(size[a]));
        var says2 = new List<string>();
        foreach (var head in standing.Take(3))
        {
            says2.Clear();
            SayTheCrowd(people, crowd, head, says2);
            Console.WriteLine();
            Console.WriteLine($"  a crowd of {size[head]} standing at the end of the run — {says2[0]}");
            for (var line = 1; line < says2.Count; line++) Console.WriteLine(says2[line]);
        }
    }

    /// <summary>
    /// <b>Who is waiting for whom, and where that comes round on itself</b>: a ring of stuck cars each held
    /// by the next is a deadlock rather than a queue, and it is the one shape no clock behind it can clear.
    /// </summary>
    /// <remarks>
    /// The car in front is found by the geometry rather than read off the claims, because the reading a driver
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
                    $"{body} ({DrivingWords.CarName(cars, body)})")));
        }

        Console.WriteLine($"    {rings} ring(s) of cars each waiting on the next");
    }

    /// <summary>
    /// <b>What cut a hold, and where</b> — the reservation it ended at, whose it is, how strong, and on which
    /// way — or that nothing did.
    /// </summary>
    static string WhatHeld(TownWorld world, int hold)
    {
        var endsAtM = world.Occupancy.HoldEndsAtM(hold, out _, out var by);
        if (float.IsPositiveInfinity(endsAtM)) return "(its plan whole)";

        var way = world.Occupancy.HoldCutOn(hold);
        var onWay = way < 0 ? "no way" : $"{world.Ways.KindOf(way)} way {way}";
        return by.Found
            ? $"(a {(by.HasBody ? "body" : by.Linked ? "marked section" : "plan")} of {by.Of} {by.Occupant} at " +
              $"{by.Priority}{(by.OnItsLine ? ", on its line" : "")}, {by.FromM:F1}–{by.ToM:F1} m of {onWay})"
            : $"(a place, on {onWay})";
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
                $"way {people.OnWay[person]} at {people.OnWayM[person]:F1} m");
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
