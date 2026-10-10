using System.Numerics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Town;
using Network = TrafficSimulation.App.DeviceTick.DeviceRoutes.Network;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Route requests made the way a standing town makes them</b>, seeded, over either of its networks — and the town's own
/// search of each beside the device's (<see cref="DeviceRoutes"/>), held to it: what <c>--bench deviceroutes</c> and the
/// device suite ask.
/// </summary>
/// <remarks>
/// <b>The town's searches are sized as the device's requests are</b> (<see cref="DeviceRoutes.MostLinks"/>): a route longer
/// than its room is refused, so the room is part of the answer.
/// </remarks>
internal sealed class RouteAsks
{
    /// <summary>One request as the town makes it, barred from the closed links or not (SRV-10).</summary>
    public sealed record Ask(Network Network, RouteEntry[] Entries, RouteGoal[] Goals, bool Closed);

    /// <summary>What the town has priced up and closed on each network.</summary>
    public sealed record Marks(LinkSurcharges Drive, LinkSurcharges Walk, bool[] DriveClosed, bool[] WalkClosed)
    {
        public LinkSurcharges Of(Network network) => network == Network.Driving ? Drive : Walk;

        public bool[] ClosedOf(Network network) => network == Network.Driving ? DriveClosed : WalkClosed;
    }

    readonly TownWorld _town;
    readonly SimConfig _config;
    readonly RouteSearch _drive;
    readonly RouteSearch _walk;
    readonly Vector2 _leastM;
    readonly Vector2 _mostM;

    public RouteAsks(TownWorld town, SimConfig config)
    {
        _town = town;
        _config = config;
        _drive = new RouteSearch(town.Driving.Cells, DeviceRoutes.MostEntries, DeviceRoutes.MostGoals, DeviceRoutes.MostLinks);
        _walk = new RouteSearch(town.Walking.Cells, DeviceRoutes.MostEntries, DeviceRoutes.MostGoals, DeviceRoutes.MostLinks);

        var roads = town.Roads;
        (_leastM, _mostM) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var atM = roads.StartOf(lane).PositionM;
            (_leastM, _mostM) = (Vector2.Min(_leastM, atM), Vector2.Max(_mostM, atM));
        }
    }

    /// <summary>Nothing priced up and nothing closed, the surcharges as large as the device holds.</summary>
    public Marks Unmarked() => new(
        new LinkSurcharges(DeviceRoutes.MostMarks), new LinkSurcharges(DeviceRoutes.MostMarks),
        new bool[_town.Driving.Graph.LinkCount], new bool[_town.Walking.Runs.Graph.LinkCount]);

    /// <summary>
    /// Seeded requests over one network: a car under way on a street lane with every lane abeam of it (CAR-53), or one
    /// pulling out of a bay onto either side of its street, sent to a place in the town; a walker standing somewhere in the
    /// town sent to another.
    /// </summary>
    public List<Ask> Make(Network network, int count, int seed)
    {
        var random = new Random(seed);
        var asks = new List<Ask>(count);
        var entries = new RouteEntry[DeviceRoutes.MostEntries];
        var goals = new RouteGoal[DeviceRoutes.MostGoals];
        while (asks.Count < count)
        {
            var entryCount = network == Network.Driving
                ? DrivingEntries(random, entries)
                : _town.Walking.EntriesNear(SomewhereM(random), entries);
            var goalCount = network == Network.Driving
                ? _town.Driving.GoalsAt(SomewhereM(random), goals)
                : _town.Walking.GoalsAt(SomewhereM(random), goals);
            if (entryCount == 0 || goalCount == 0) continue;

            asks.Add(new Ask(network, entries[..entryCount], goals[..goalCount], Closed: false));
        }

        return asks;
    }

    /// <summary>The town's own search of a request, its answer read off what it returns.</summary>
    public RouteSearch Searched(Ask ask, Marks marks, out int count, out int goalSlot)
    {
        var search = ask.Network == Network.Driving ? _drive : _walk;
        ask.Entries.CopyTo(search.Entries);
        ask.Goals.CopyTo(search.Goals);
        var closed = ask.Closed ? marks.ClosedOf(ask.Network) : default;
        count = search.Plan(ask.Entries.Length, ask.Goals.Length, marks.Of(ask.Network), closed, out goalSlot);
        return search;
    }

    /// <summary>
    /// How a slot's answer on the device parts from the town's own to the same request — its links, how many, the goal,
    /// stopping short and searched over the whole graph, the links settled, the cost to the bit, and the asker and stamp
    /// echoed — or nothing where it is the same.
    /// </summary>
    public string? Differs(Ask ask, Marks marks, DeviceRoutes routes, int slot, int owner, uint stamp)
    {
        var search = Searched(ask, marks, out var count, out var goalSlot);
        var device = routes.Answer(slot);
        var same = routes.StateOf(slot) == DeviceRoutes.Done && device.Owner == (uint)owner && device.Stamp == stamp
                   && device.Count == (uint)count && device.GoalSlot == goalSlot
                   && (device.StopsShort != 0u) == search.StopsShort
                   && (device.OverTheWholeGraph != 0u) == search.OverTheWholeGraph
                   && device.SettledLinks == (uint)search.SettledLinks
                   && BitConverter.SingleToInt32Bits(device.CostM) == BitConverter.SingleToInt32Bits(search.CostM)
                   && routes.LinksOf(slot, (int)device.Count).SequenceEqual(search.Links(count));
        return same
            ? null
            : $"{ask.Network} #{stamp}: town {count} links, goal {goalSlot}, short {search.StopsShort}, whole {search.OverTheWholeGraph}, " +
              $"settled {search.SettledLinks}, {search.CostM} m; device {device}";
    }

    int DrivingEntries(Random random, Span<RouteEntry> into)
    {
        var roads = _town.Roads;
        var driving = _town.Driving;
        var lane = roads.NearestStreetLane(SomewhereM(random), out var alongM);
        if (lane < 0) return 0;

        var entry = driving.EntryOnLane(lane, alongM);
        if (entry.Link == TravelGraph.NoLink) return 0;

        var count = 0;
        into[count++] = entry;
        var reverse = roads.LaneReverse[lane];
        if (random.Next(PullsOutOfABayOneIn) == 0 && reverse >= 0)
        {
            var back = driving.EntryOnLane(reverse, roads.LaneLengthM[reverse] - alongM);
            if (back.Link != TravelGraph.NoLink) into[count++] = back;
            return count;
        }

        foreach (var inward in (ReadOnlySpan<bool>)[true, false])
        {
            for (var beside = roads.LaneBeside(lane, inward); beside != RoadGraph.NoLane && count < into.Length;
                 beside = roads.LaneBeside(beside, inward))
            {
                var abeam = driving.EntryOnLane(beside, alongM * roads.LaneLengthM[beside] / roads.LaneLengthM[lane]);
                if (abeam.Link == TravelGraph.NoLink) continue;

                into[count++] = abeam with { EnterM = DrivingNetwork.SwitchM(roads, _config, lane, beside) };
            }
        }

        return count;
    }

    /// <summary>How many of the car requests are one pulling out of a bay, onto either side of its street.</summary>
    const int PullsOutOfABayOneIn = 3;

    Vector2 SomewhereM(Random random) =>
        _leastM + ((_mostM - _leastM) * new Vector2((float)random.NextDouble(), (float)random.NextDouble()));
}
