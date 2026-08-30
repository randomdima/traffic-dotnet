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
        var crowd = new int[people.Count];
        var crowdSize = new int[people.Count];
        var inACrowdTicks = new int[people.Count];
        var heldTicks = new int[people.Count];
        var worstHeldTicks = new int[people.Count];
        var insideTicks = new int[people.Count];
        var longestHold = 0;
        var longestHoldSays = new List<string>();
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

                // <b>The walking side's own stuck, and the one the metres cannot say</b>: a body the book is
                // holding takes no decision at all (PER-13), so nothing runs out for it and the only thing
                // that ever lets it go is whoever is in front of it moving.
                if (!people.IsHeldByTheBook(person, world.StopsInM(person)))
                {
                    heldTicks[person] = 0;
                    continue;
                }

                heldTicks[person]++;
                if (heldTicks[person] > worstHeldTicks[person]) worstHeldTicks[person] = heldTicks[person];

                // <b>And standing inside the gap it keeps behind somebody</b>, which is the other half of the
                // same fault: a grant below nothing cut at another body is one that has come to rest past
                // the near edge of the ground the book gave that body, and a pavement's worth of those is a
                // queue closed up into a heap. <b>Cut at a body and not at a place</b> — a walker held at the
                // edge of a lane the road refused it is standing exactly where it should be.
                if (people.AuthorityM[person] < 0f && people.HeldBy[person] != PersonFleet.NoBody)
                {
                    insideTicks[person]++;
                }
            }

            if (tick % CrowdEvery != 0) continue;

            // <b>Said while the hold is still on</b>, for the reason the heap is: the chain a body was at the
            // back of has unwound by the end of the run, and the head of it — the only body a fix is written
            // from — is by then walking somewhere else.
            for (var person = 0; person < people.Count; person++)
            {
                if (heldTicks[person] <= longestHold) continue;

                longestHold = heldTicks[person];
                longestHoldSays.Clear();
                SayTheHold(people, person, longestHold / config.Sim.TickRateHz, longestHoldSays);
            }

            GatherCrowds(people, TouchingM(config), crowd, crowdSize);
            var head = PersonFleet.NoBody;
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
            if (head == PersonFleet.NoBody) continue;

            biggestCrowdSays.Clear();
            SayTheCrowd(people, crowd, head, biggestCrowdSays);
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
        ReportCrowds(world, config, inACrowdTicks, crowd, crowdSize, biggestCrowd, biggestCrowdTick, biggestCrowdSays);
        ReportHolds(world, config, heldTicks, worstHeldTicks, insideTicks, longestHoldSays);
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
    /// <b>How near two walkers stand when they are in the same heap</b>: well inside the gap a pavement
    /// queue stands at (PER-13, <see cref="SimConfig.PersonStandstillGapM"/>) — half of it, so that a queue
    /// keeping the distance it is supposed to is never counted as a heap and a pair standing at half of it
    /// always is.
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
                $"{people.Stage[person]}, walking {people.Walking[person]}, line " +
                $"{people.WalkedTaken[person]}/{people.WalkedCount[person]}, grant " +
                $"{people.AuthorityM[person]:F2} m at {people.OnWayM[person]:F1} m of way {people.OnWay[person]}, " +
                $"held by {people.HeldBy[person]}, steps round " +
                $"{people.StepsRound[person]}, kerb {people.HeldAtTheKerb[person]}, building " +
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
    /// <b>One hold, followed up the chain to whoever is at the head of it</b>: everybody behind a hold is
    /// held by the hold in front, so the head is the only body a fix can be written from.
    /// </summary>
    static void SayTheHold(PersonFleet people, int person, float heldS, List<string> into)
    {
        var chain = new List<int>();
        var at = person;
        while (at != PersonFleet.NoBody && !chain.Contains(at))
        {
            chain.Add(at);
            at = people.HeldBy[at];
        }

        var root = chain[^1];
        into.Add(
            $"  the longest was walker {person}, {heldS:F0} s so far — behind {chain.Count - 1} " +
            $"{(at == PersonFleet.NoBody ? "as far as" : "round to")} walker {root}");
        into.Add(
            $"    the head is {people.Stage[root]}, walking {people.Walking[root]}, grant " +
            $"{people.AuthorityM[root]:F2} m at {people.OnWayM[root]:F1} m of way {people.OnWay[root]}, " +
            $"steps round {people.StepsRound[root]}, kerb {people.HeldAtTheKerb[root]} for " +
            $"{people.WaitingToCrossS[root]:F1} s, refused way {people.RefusedWay[root]}, crossing ahead " +
            $"{people.CrossingAhead(root)}, line {people.WalkedTaken[root]}/{people.WalkedCount[root]}, " +
            $"runs out {people.WalkedRunsOut[root]}, at ({people.PositionM[root].X:F1}, {people.PositionM[root].Y:F1})");
    }

    /// <summary>
    /// <b>How long the pavement's book held anybody where they stood, and who was holding whom</b>.
    /// </summary>
    /// <remarks>
    /// <b>A walker held by the book takes no decision at all</b> (PER-13): waiting behind a body that is
    /// under way is not being stuck, so the clock that gives a leg up is frozen while it is true. That makes
    /// the length of a hold the whole of the walking side's safety margin — and a hold that comes round on
    /// itself is one no length of clock behind it could ever have cleared, because the two of them are each
    /// waiting for the other to move.
    /// </remarks>
    static void ReportHolds(
        TownWorld world, SimConfig config, int[] heldTicks, int[] worstHeldTicks, int[] insideTicks,
        List<string> longestSays)
    {
        var people = world.People;
        var held = 0;
        var ever = 0;
        var longest = 0;
        for (var person = 0; person < people.Count; person++)
        {
            if (heldTicks[person] >= StillTicks) held++;
            if (worstHeldTicks[person] >= StillTicks) ever++;
            if (worstHeldTicks[person] > longest) longest = worstHeldTicks[person];
        }

        Console.WriteLine();
        Console.WriteLine(
            $"holds — {held} walkers the book was still holding at the end of the run, {ever} held for longer " +
            $"than {StillTicks / config.Sim.TickRateHz} s at a stretch, of {people.Count}");
        Console.WriteLine(
            $"        the longest anybody was held without a decision was " +
            $"{longest / (float)config.Sim.TickRateHz:F0} s of the {MeasuredTicks / config.Sim.TickRateHz} s run");

        var insideTotal = 0L;
        var worstInside = 0;
        for (var person = 0; person < people.Count; person++)
        {
            insideTotal += insideTicks[person];
            if (insideTicks[person] > worstInside) worstInside = insideTicks[person];
        }

        Console.WriteLine(
            $"        walkers stood inside the gap they keep for {100f * insideTotal / (people.Count * (float)MeasuredTicks):F2}% " +
            $"of the run, and the worst-off for {100f * worstInside / MeasuredTicks:F0}% of it");

        // <b>The head of the chain and never the body reporting it</b>: everybody behind a hold is held by
        // the hold in front, so the only one a fix is written from is whoever is at the front of it.
        Console.WriteLine();
        foreach (var says in longestSays) Console.WriteLine(says);

        Console.WriteLine();
        var rings = 0;
        var ring = new List<int>();
        for (var person = 0; person < people.Count; person++)
        {
            ring.Clear();
            var at = person;
            while (at != PersonFleet.NoBody && !ring.Contains(at))
            {
                ring.Add(at);
                at = people.HeldBy[at];
            }

            // <b>Counted once, from the lowest-numbered body in it.</b> Every walker queueing behind a ring
            // runs into the same ring, and every member of one finds it from where it stands.
            if (at == PersonFleet.NoBody || ring.IndexOf(at) != 0 || ring.Any(body => body < person)) continue;

            rings++;
            if (rings > 6) continue;

            Console.WriteLine(
                $"    ring of {ring.Count}: " + string.Join(
                    " -> ", ring.Select(body => $"{body} (way {people.OnWay[body]}, grant {people.AuthorityM[body]:F2} m)")));
        }

        Console.WriteLine($"    {rings} ring(s) of walkers each held by the next");
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
