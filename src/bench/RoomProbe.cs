using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What a standing town lays into the claims</b>: the holds and pieces every rebuild asked, the body, main and
/// secondary claims they came to and the holders those name — on average and at most — the town's longest way against
/// the decimetres a claim can name, and how the marks that split the plans lie over the town's ways.
/// </summary>
internal static class RoomProbe
{
    const int WarmupTicks = 1200;
    const int MeasuredTicks = 1200;

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        var claims = world.Occupancy;
        var marks = world.Atlas.Marks;
        var rebuilds = 0;
        var layingFrom = world.LayingTicks.ToArray();
        var keptFrom = world.PlansKept.ToArray();
        var walksFrom = world.WalksKept.ToArray();
        var (holds, wishes, bodies, mains, seconds, holders) = (new Tally(), new Tally(), new Tally(), new Tally(), new Tally(), new Tally());
        var (laidBodies, ways) = (new Tally(), new Tally());
        var (driven, drivenAtRest, walking, walkingAtRest) = (new Tally(), new Tally(), new Tally(), new Tally());
        var (carsAsked, askedAtRest, askedFollowing, askedAsBefore, plannedAsBefore) =
            (new Tally(), new Tally(), new Tally(), new Tally(), new Tally());
        var tablesUsed = new Dictionary<string, (string Table, int Used, int Rows, int RowBytes)>();
        var lineArcs = new List<int>();
        var before = new PlanSeen[world.Cars.Count];
        var stopMps = config.Driving.StopSpeedMps;
        var rosters = Enum.GetValues<LaneRoster>();
        var byRoster = rosters.Select(_ => (Holds: new Tally(), Mains: new Tally(), Seconds: new Tally(), Bodies: new Tally())).ToArray();
        var kinds = Enum.GetValues<WayKind>();
        var across = rosters.Select(_ => Enumerable.Range(0, kinds.Length * kinds.Length).Select(_ => new Tally()).ToArray()).ToArray();
        Span<int> acrossNow = stackalloc int[kinds.Length * kinds.Length];
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance();
            if (!world.ClaimsLaidThisTick) continue;

            rebuilds++;
            for (var car = 0; car < world.Cars.Count; car++) lineArcs.Add(world.Cars.Line[car].ArcCount);

            foreach (var table in claims.TablesUsed())
            {
                tablesUsed[table.Table] = tablesUsed.TryGetValue(table.Table, out var seen)
                    ? seen with { Used = Math.Max(seen.Used, table.Used), Rows = table.Rows }
                    : table;
            }

            var laid = claims.Laid();
            holds.Add(claims.HoldCount);
            wishes.Add(claims.WishCount);
            bodies.Add(laid.Bodies);
            mains.Add(laid.Mains);
            seconds.Add(laid.Secondaries);
            holders.Add(laid.Holders);
            laidBodies.Add(claims.BodiesLaid);
            ways.Add(claims.OccupiedWays.Length);
            var (cars, carsAtRest, people, peopleAtRest) = (0, 0, 0, 0);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (!world.Cars.Driven[car]) continue;

                cars++;
                if (world.Cars.VelocityMps[car].Length() <= stopMps) carsAtRest++;
            }

            for (var person = 0; person < world.People.Count; person++)
            {
                if (world.People.Inside[person].Any || !world.People.Walking[person]) continue;

                people++;
                if (world.People.VelocityMps[person].Length() <= stopMps) peopleAtRest++;
            }

            var (atRest, following, asBefore, plannedSame) = (0, 0, 0, 0);
            foreach (var car in world.CarsAsked)
            {
                var seen = PlanSeen.Of(world, car, rebuilds);
                if (world.Cars.VelocityMps[car].Length() > stopMps)
                {
                    before[car] = default;
                    continue;
                }

                atRest++;
                if (world.Cars.Action[car] == CarAction.Follow) following++;
                if (seen.AsBefore(before[car]))
                {
                    asBefore++;
                    if (seen.PlannedAs(before[car])) plannedSame++;
                }

                before[car] = seen;
            }

            carsAsked.Add(world.CarsAsked.Length);
            askedAtRest.Add(atRest);
            askedFollowing.Add(following);
            askedAsBefore.Add(asBefore);
            plannedAsBefore.Add(plannedSame);
            driven.Add(cars);
            drivenAtRest.Add(carsAtRest);
            walking.Add(people);
            walkingAtRest.Add(peopleAtRest);
            for (var at = 0; at < rosters.Length; at++)
            {
                var asked = claims.AskedBy(rosters[at]);
                byRoster[at].Holds.Add(asked.Holds);
                byRoster[at].Mains.Add(asked.Mains);
                byRoster[at].Seconds.Add(asked.Secondaries);
                byRoster[at].Bodies.Add(claims.BodiesLaidBy(rosters[at]));
                acrossNow.Clear();
                claims.SecondariesAskedAcross(rosters[at], acrossNow);
                for (var pair = 0; pair < acrossNow.Length; pair++) across[at][pair].Add(acrossNow[pair]);
            }
        }

        var longestM = 0f;
        for (var way = 0; way < world.Ways.Count; way++) longestM = MathF.Max(longestM, world.Ways.LengthM(way));

        Console.WriteLine(
            $"room probe — {map}: {world.Cars.Count} cars, {world.People.Count} people; {rebuilds} ticks of turns over {MeasuredTicks} ticks " +
            $"after {WarmupTicks}");
        Console.WriteLine($"longest way {longestM:F1} m: {LaneOccupancy.DmOf(longestM):N0} of {ushort.MaxValue:N0} dm");
        var msPer = 1000.0 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, rebuilds);
        var stages = Enum.GetValues<TownWorld.LayingStage>();
        var spentMs = stages.Select(stage => (world.LayingTicks[(int)stage] - layingFrom[(int)stage]) * msPer).ToArray();
        Console.WriteLine(
            $"a tick's turns {spentMs.Sum():F2} ms: " + string.Join(", ", stages.Select((stage, at) => $"{stage} {spentMs[at]:F2}")));
        var kept = world.PlansKept.ToArray();
        var keptPer = Enum.GetValues<PlanKept>().Select(of => (kept[(int)of] - keptFrom[(int)of]) / Math.Max(1, rebuilds)).ToArray();
        Console.WriteLine(
            $"car plans a tick: {keptPer[(int)PlanKept.AsItIs]:N0} kept as they were, " +
            $"{keptPer[(int)PlanKept.ToBeAskedFurther]:N0} asked further, {keptPer[(int)PlanKept.No]:N0} let go of");
        var walks = world.WalksKept.ToArray();
        var walksPer = Enum.GetValues<WalkKept>().Select(of => (walks[(int)of] - walksFrom[(int)of]) / Math.Max(1, rebuilds)).ToArray();
        Console.WriteLine(
            $"walks a tick: {walksPer[(int)WalkKept.AsItIs]:N0} kept as they were, " +
            $"{walksPer[(int)WalkKept.ToBeAskedFurther]:N0} asked further, {walksPer[(int)WalkKept.No]:N0} asked afresh");
        Console.WriteLine($"{"a tick",-12}{"average",10}{"most",10}");
        driven.Print("cars driven", rebuilds);
        drivenAtRest.Print("  at rest", rebuilds);
        carsAsked.Print("cars asked", rebuilds);
        askedAtRest.Print("  at rest", rebuilds);
        askedFollowing.Print("  following", rebuilds);
        askedAsBefore.Print("  as before", rebuilds);
        plannedAsBefore.Print("  same plan", rebuilds);
        walking.Print("walking", rebuilds);
        walkingAtRest.Print("  at rest", rebuilds);
        ways.Print("ways", rebuilds);
        holds.Print("holds", rebuilds);
        wishes.Print("pieces", rebuilds);
        laidBodies.Print("bodies laid", rebuilds);
        bodies.Print("bodies", rebuilds);
        mains.Print("mains", rebuilds);
        seconds.Print("secondaries", rebuilds);
        holders.Print("holders", rebuilds);
        for (var at = 0; at < rosters.Length; at++)
        {
            byRoster[at].Holds.Print($"{rosters[at]} holds", rebuilds);
            byRoster[at].Mains.Print($"  main pieces", rebuilds);
            byRoster[at].Seconds.Print($"  secondary", rebuilds);
            for (var pair = 0; pair < across[at].Length; pair++)
            {
                if (across[at][pair].Any) across[at][pair].Print($"    {kinds[pair / kinds.Length]}→{kinds[pair % kinds.Length]}", rebuilds);
            }

            byRoster[at].Bodies.Print($"  bodies laid", rebuilds);
        }

        Console.WriteLine($"{"claims' tables",-16}{"most used",12}{"rows",12}{"MB",8}{"MB used",9}");
        foreach (var (table, used, rows, rowBytes) in tablesUsed.Values)
        {
            Console.WriteLine(
                $"  {table,-14}{used,12:N0}{rows,12:N0}{rows * (double)rowBytes / 1e6,8:F1}{used * (double)rowBytes / 1e6,9:F1}");
        }

        lineArcs.Sort();
        Console.WriteLine(
            $"a car's line, arcs at every laying: median {At(lineArcs, 0.5)}, p99 {At(lineArcs, 0.99)}, " +
            $"p99.9 {At(lineArcs, 0.999)}, most {lineArcs[^1]}, of {world.Cars.LineArcsOf(0).Length} a car has room for");
        Console.WriteLine($"marks a way (most crossed by one: {marks.MostCrossedByOne}){"",4}{"ways",8}{"median",8}{"p90",6}{"p99",6}{"most",6}");
        foreach (var kind in Enum.GetValues<WayKind>())
        {
            var counts = new List<int>();
            for (var way = 0; way < world.Ways.Count; way++)
            {
                if (world.Ways.KindOf(way) == kind) counts.Add(marks.Of(way).Length);
            }

            if (counts.Count == 0) continue;

            counts.Sort();
            Console.WriteLine(
                $"  {kind,-40}{counts.Count,8}{At(counts, 0.5),8}{At(counts, 0.9),6}{At(counts, 0.99),6}{counts[^1],6}");
        }

        Console.WriteLine($"marks by kinds, metres of mine{"",13}{"marks",8}{"median",8}{"p90",6}{"p99",6}{"most",6}");
        foreach (var from in kinds)
        {
            foreach (var onto in kinds)
            {
                var lengthsDm = new List<int>();
                for (var way = 0; way < world.Ways.Count; way++)
                {
                    if (world.Ways.KindOf(way) != from) continue;

                    foreach (var mark in marks.Of(way))
                    {
                        if (world.Ways.KindOf(mark.OnWay) == onto) lengthsDm.Add((int)((mark.MineToM - mark.MineFromM) * 10f));
                    }
                }

                if (lengthsDm.Count == 0) continue;

                lengthsDm.Sort();
                Console.WriteLine(
                    $"  {$"{from}→{onto}",-40}{lengthsDm.Count,8}{At(lengthsDm, 0.5) / 10f,8:F1}{At(lengthsDm, 0.9) / 10f,6:F1}" +
                    $"{At(lengthsDm, 0.99) / 10f,6:F0}{lengthsDm[^1] / 10f,6:F0}");
            }
        }
    }

    /// <summary>
    /// <b>What a car asked at rest was planned from, and what it came to</b>: one asked at rest at the last laying too,
    /// from the same nose, line, action and planned speed, is asked as before — a plan that could be laid again unread.
    /// </summary>
    readonly record struct PlanSeen(
        int Laying, float FromM, uint Line, CarAction Action, uint Entries, float PlannedMps, float ToM, float CommittedToM)
    {
        public static PlanSeen Of(TownWorld world, int car, int laying)
        {
            var cars = world.Cars;
            return new PlanSeen(
                laying, cars.ClaimFromM[car], cars.LinesLaid[car], cars.Action[car], world.ActionEntriesOf(car),
                cars.PlannedMps[car], cars.ClaimToM[car], cars.CommittedToM[car]);
        }

        public bool AsBefore(in PlanSeen last) =>
            last.Laying > 0 && last.Laying == Laying - 1 && last.FromM == FromM && last.Line == Line && last.Action == Action
            && last.Entries == Entries && last.PlannedMps == PlannedMps;

        public bool PlannedAs(in PlanSeen last) => last.ToM == ToM && last.CommittedToM == CommittedToM;
    }

    static int At(List<int> sorted, double share) => sorted[Math.Min(sorted.Count - 1, (int)(share * sorted.Count))];

    /// <summary>One count over every rebuild: its sum and its most.</summary>
    sealed class Tally
    {
        long _sum;
        int _most;

        public void Add(int count)
        {
            _sum += count;
            _most = Math.Max(_most, count);
        }

        public bool Any => _sum > 0;

        public void Print(string what, int rebuilds) =>
            Console.WriteLine($"{what,-12}{_sum / Math.Max(1, rebuilds),10:N0}{_most,10:N0}");
    }
}
