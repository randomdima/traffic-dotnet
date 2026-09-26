using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What the ribbon atlas came out as</b> (TER-4c.4, TER-5c): how much ground it files, which ways it
/// marks as sharing ground and how many, and the three readings a mark table is judged by — movements that
/// share ground with every other movement of their junction, opposing straights that were linked, and the
/// town's furniture standing on a driven ribbon.
/// </summary>
internal static class RibbonCensus
{
    public static void Run(CityPlan plan, SimConfig config)
    {
        var world = new TownWorld(plan, config, standStatics: false);
        var atlas = world.Atlas;
        var ways = world.Ways;
        var marks = atlas.Marks;

        Console.WriteLine($"ribbons, a point every {atlas.StepM:F2} m — laid in {world.AtlasMs:F0} ms");
        Console.WriteLine($"  {atlas.PointCount} points, {atlas.EntryCount} entries, {atlas.Bytes / 1048576.0:F1} MiB, " +
                          $"at most {atlas.MostWaysAtAPoint} ways over one point");

        const int Kinds = 5;
        var pairs = new int[Kinds, Kinds];
        var marked = new int[Kinds];
        var most = new int[Kinds];
        var counted = new int[Kinds];
        for (var way = 0; way < ways.Count; way++)
        {
            var kind = (int)ways.KindOf(way);
            var sections = marks.Of(way);
            counted[kind]++;
            marked[kind] += sections.Length;
            most[kind] = Math.Max(most[kind], sections.Length);
            foreach (var section in sections)
            {
                if (section.OnWay > way) pairs[kind, (int)ways.KindOf(section.OnWay)]++;
            }
        }

        Console.WriteLine("  marks per way, by kind — mean, most");
        for (var kind = 0; kind < Kinds; kind++)
        {
            if (counted[kind] == 0) continue;

            Console.WriteLine($"    {(WayKind)kind,-10}{counted[kind],7} ways  {(float)marked[kind] / counted[kind],6:F2}  {most[kind],4}");
        }

        Console.WriteLine("  pairs sharing ground, by the two kinds");
        for (var one = 0; one < Kinds; one++)
        {
            for (var other = 0; other < Kinds; other++)
            {
                var count = pairs[one, other] + (one == other ? 0 : pairs[other, one]);
                if (other < one || count == 0) continue;

                Console.WriteLine($"    {(WayKind)one,-10}{(WayKind)other,-10}{count,8}");
            }
        }

        Movements(world);
        Furniture(world, plan);
        Console.WriteLine();
    }

    /// <summary>
    /// <b>The movements a junction's marks leave no way past</b>: those sharing ground with every other
    /// movement of the same junction, and those sharing it with every movement from another arm — and how many
    /// of the junction's opposing straights were linked.
    /// </summary>
    static void Movements(TownWorld world)
    {
        var roads = world.Roads;
        var marks = world.Atlas.Marks;
        var atJunction = new Dictionary<int, List<int>>();
        for (var connector = 0; connector < roads.ConnectorCount; connector++)
        {
            if (roads.ConnectorLengthM(connector) <= 0f) continue;

            var junction = roads.LaneToJunction[roads.ConnectorFrom(connector)];
            if (!atJunction.TryGetValue(junction, out var list)) atJunction[junction] = list = [];
            list.Add(connector);
        }

        var shutAll = 0;
        var shutOthers = 0;
        var movements = 0;
        var opposing = 0;
        var opposingLinked = 0;
        var linkedPerMovement = 0f;
        foreach (var (_, connectors) in atJunction)
        {
            foreach (var one in connectors)
            {
                movements++;
                var linkedAll = 0;
                var fromOthers = 0;
                var linkedOthers = 0;
                foreach (var other in connectors)
                {
                    if (other == one) continue;

                    var linked = Linked(marks, roads.WayOfConnector(one), roads.WayOfConnector(other));
                    if (linked) linkedAll++;
                    if (roads.ConnectorFrom(other) != roads.ConnectorFrom(one))
                    {
                        fromOthers++;
                        if (linked) linkedOthers++;
                    }

                    if (other > one && roads.KindOf(one) == LaneTurn.Straight && roads.KindOf(other) == LaneTurn.Straight
                        && roads.LaneReverse[roads.ConnectorFrom(one)] == roads.ConnectorTo(other))
                    {
                        opposing++;
                        if (linked) opposingLinked++;
                    }
                }

                linkedPerMovement += connectors.Count > 1 ? (float)linkedAll / (connectors.Count - 1) : 0f;
                if (connectors.Count > 1 && linkedAll == connectors.Count - 1) shutAll++;
                if (fromOthers > 0 && linkedOthers == fromOthers) shutOthers++;
            }
        }

        Console.WriteLine($"  movements {movements}: linked to {100f * linkedPerMovement / Math.Max(1, movements):F0} % " +
                          $"of their junction's others on average; {shutAll} linked to every other, " +
                          $"{shutOthers} to every one from another arm");
        Console.WriteLine($"  opposing straights {opposing}, {opposingLinked} of them linked");
    }

    static bool Linked(WayCrossings marks, int one, int other)
    {
        foreach (var section in marks.Of(one))
        {
            if (section.OnWay == other) return true;
        }

        return false;
    }

    /// <summary>The town's furniture standing on ground the traffic drives, which a well-formed town has none of.</summary>
    static void Furniture(TownWorld world, CityPlan plan)
    {
        Span<WayCover> under = stackalloc WayCover[64];
        var standing = 0;
        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            var count = world.Atlas.UnderDisc(plan.Props.CentreM[prop], plan.Props.RadiusM[prop], under);
            for (var at = 0; at < count; at++)
            {
                if (!world.Ways.IsDriven(under[at].Way)) continue;

                standing++;
                break;
            }
        }

        Console.WriteLine($"  props on a driven ribbon: {standing} of {plan.Props.Count}");
    }
}
