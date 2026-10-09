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
/// <b>What the claims' room is laid for against what a standing town lays into it</b>: each term of the room
/// (<see cref="TownWorld"/>'s table) beside the most any one agent laid of it in a rebuild, the slots and holds every
/// rebuild came to, and how the marks that multiply the plans' terms lie over the town's ways.
/// </summary>
/// <remarks>
/// <b>An agent's figures are of one rebuild</b>: its body's slots, the pieces of every hold it laid, the main claims of
/// its longest hold — the ways that hold ran along — and that hold's secondary claims. The spread of what a planning
/// agent laid is over every rebuild it laid any plan in.
/// </remarks>
internal static class RoomProbe
{
    const int WarmupTicks = 1200;
    const int MeasuredTicks = 1200;

    /// <summary>The shares of the spread printed, of every planning agent's every rebuild.</summary>
    static readonly double[] Shares = [0.5, 0.99, 0.999];

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        var claims = world.Occupancy;
        var marks = world.Atlas.Marks;
        var crossed = 1 + marks.MostCrossedByOne;
        var (cars, people) = (new Agents(world.Cars.Capacity), new Agents(world.People.Capacity));
        var (rebuilds, mostSlots, mostHolds, slots, holds) = (0, 0, 0, 0L, 0L);
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance();
            if (!world.ClaimsLaidThisTick) continue;

            rebuilds++;
            (mostSlots, mostHolds) = (Math.Max(mostSlots, claims.SlotCount), Math.Max(mostHolds, claims.HoldCount));
            (slots, holds) = (slots + claims.SlotCount, holds + claims.HoldCount);
            Count(claims, cars, people);
        }

        var carRoom = RibbonAtlas.MostWaysUnderABody + ((DrivingGround.MostWaysAlongALine + 1) * crossed) + (BackingUp.BackingPieces * crossed);
        var walkerRoom = RibbonAtlas.MostWaysUnderABody + 1 + (WalkingGround.MostWaysAlongAWalk * crossed);
        Console.WriteLine(
            $"room probe — {map}: {world.Cars.Count} cars, {world.People.Count} people; {rebuilds} rebuilds over {MeasuredTicks} ticks " +
            $"after {WarmupTicks}");
        Console.WriteLine(
            $"room laid: {claims.Capacity:N0} slots and {claims.HoldCapacity:N0} holds — a car {carRoom}: body {RibbonAtlas.MostWaysUnderABody}, " +
            $"plan {DrivingGround.MostWaysAlongALine + 1} ways x {crossed}, backing {BackingUp.BackingPieces} x {crossed}; a walker {walkerRoom}: " +
            $"body {RibbonAtlas.MostWaysUnderABody + 1}, walk {WalkingGround.MostWaysAlongAWalk} ways x {crossed}");
        Console.WriteLine(
            $"laid a rebuild: slots {slots / Math.Max(1, rebuilds):N0} on average and {mostSlots:N0} at most ({100d * mostSlots / claims.Capacity:F2} % " +
            $"of the room), holds {holds / Math.Max(1, rebuilds):N0} and {mostHolds:N0}");
        Console.WriteLine($"{"",-8}{"body",8}{"passes",8}{"planned",9}{"ways",6}{"marks",7}  planned spread {string.Join(" / ", Shares.Select(s => $"{s:P1}"))}");
        Report("a car", cars, RibbonAtlas.MostWaysUnderABody, carRoom - RibbonAtlas.MostWaysUnderABody, DrivingGround.MostWaysAlongALine + 1);
        Report("a walker", people, RibbonAtlas.MostWaysUnderABody + 1, walkerRoom - RibbonAtlas.MostWaysUnderABody - 1, WalkingGround.MostWaysAlongAWalk);

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
    }

    /// <summary>One rebuild's slots and holds, counted to the agent each is of.</summary>
    static void Count(LaneOccupancy claims, Agents cars, Agents people)
    {
        cars.Begin();
        people.Begin();
        foreach (ref readonly var slot in claims.Slots)
        {
            if (!slot.HasBody || slot.Of == LaneRoster.Signal) continue;

            var of = slot.Of == LaneRoster.Driving ? cars : people;
            if (slot.Passing) of.Passes[slot.Occupant]++;
            else of.Body[slot.Occupant]++;
        }

        var laid = claims.Slots;
        for (var hold = 0; hold < claims.HoldCount; hold++)
        {
            var (first, end) = claims.PiecesOf(hold);
            if (end <= first || laid[first].Of == LaneRoster.Signal) continue;

            var of = laid[first].Of == LaneRoster.Driving ? cars : people;
            var occupant = laid[first].Occupant;
            var main = 0;
            for (var slot = first; slot < end; slot++)
            {
                if (!laid[slot].Secondary) main++;
            }

            of.Planned[occupant] += end - first;
            if (main > of.Ways[occupant]) (of.Ways[occupant], of.Marks[occupant]) = (main, end - first - main);
        }

        cars.End();
        people.End();
    }

    static void Report(string who, Agents agents, int bodyRoom, int plannedRoom, int waysRoom)
    {
        agents.Spread.Sort();
        var spread = string.Join(" / ", Shares.Select(share => agents.Spread.Count == 0 ? "-" : $"{At(agents.Spread, share)}"));
        Console.WriteLine(
            $"{who,-8}{agents.MostBody,8}{agents.MostPasses,8}{agents.MostPlanned,9}{agents.MostWays,6}{agents.MostMarks,7}  {spread}");
        Console.WriteLine($"{"  room",-8}{bodyRoom,8}{"",8}{plannedRoom,9}{waysRoom,6}");
    }

    static int At(List<int> sorted, double share) => sorted[Math.Min(sorted.Count - 1, (int)(share * sorted.Count))];

    /// <summary>One roster's counts of the rebuild being read, and the most any agent of it came to.</summary>
    sealed class Agents(int capacity)
    {
        public readonly int[] Body = new int[capacity];
        public readonly int[] Passes = new int[capacity];
        public readonly int[] Planned = new int[capacity];
        public readonly int[] Ways = new int[capacity];
        public readonly int[] Marks = new int[capacity];
        public readonly List<int> Spread = [];
        public int MostBody, MostPasses, MostPlanned, MostWays, MostMarks;

        public void Begin()
        {
            Array.Clear(Body);
            Array.Clear(Passes);
            Array.Clear(Planned);
            Array.Clear(Ways);
            Array.Clear(Marks);
        }

        public void End()
        {
            for (var agent = 0; agent < Body.Length; agent++)
            {
                MostBody = Math.Max(MostBody, Body[agent]);
                MostPasses = Math.Max(MostPasses, Passes[agent]);
                MostPlanned = Math.Max(MostPlanned, Planned[agent]);
                if (Ways[agent] > MostWays) (MostWays, MostMarks) = (Ways[agent], Marks[agent]);
                if (Planned[agent] > 0) Spread.Add(Planned[agent]);
            }
        }
    }
}
