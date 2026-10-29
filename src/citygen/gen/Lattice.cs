using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The streets inside each district</b> (GEN-72): a lattice on the district's own bearing at its own spacing, clipped
/// to the ground the district holds, and hung off the district roads it runs up against.
/// </summary>
/// <remarks>
/// <para>
/// <b>A lattice is laid, not searched for.</b> Every point is asked once — is it in this district, on dry ground, clear of
/// every path's corridor — and a point that fails is simply not there. A street joins two of a district's own points only
/// where the ground between them is the district's too, so no street leaves its district, and none crosses a district
/// road: a path carries a node wherever a street meets it.
/// </para>
/// <para>
/// <b>A street hangs off a district road and never off an intercity one</b> (GEN-68): where the next point along would
/// stand in an intercity road's corridor the lattice stops, and whatever that leaves leading nowhere is pruned (GEN-5a).
/// </para>
/// <para>
/// <b>What is left over is deleted rather than joined up</b> — a block cut off is dropped with the rest of its component
/// (<see cref="TownLayout.KeepTheLargestComponent"/>), because a link drawn to reach it would have to cross whatever cut it
/// off.
/// </para>
/// </remarks>
internal static class Lattice
{
    /// <summary>
    /// How far past the arms' least angle a turned stub is turned, so it meets the road clear of the bound rather than on
    /// it, where a float's rounding decides GEN-13. A tolerance, not a figure of the town.
    /// </summary>
    const float PastTheBoundRad = 0.01f;

    /// <summary>
    /// <b>Where along a road a stub leaving a lattice point on a heading meets it</b>, squarely enough to be a junction at
    /// both ends (GEN-13): where the heading itself crosses the road, if it does so at the arms' least angle or more; else
    /// turned toward the road's normal as far as keeps it that far off the lattice's own arms at its start, a sixth of a
    /// turn less the right angle between them — or none, where that is not turn enough.
    /// </summary>
    static float? StubOnto(Polyline line, Vector2 fromM, Vector2 heading, float reachM, float apartRad)
    {
        if (line.Crossed(fromM, fromM + (heading * reachM)) is not { } crossedM) return null;

        var along = line.Heading(crossedM);
        var meetsRad = MathF.Acos(Math.Clamp(MathF.Abs(Vector2.Dot(heading, along)), 0f, 1f));
        if (meetsRad >= apartRad) return crossedM;

        var turnRad = apartRad - meetsRad + PastTheBoundRad;
        if (turnRad > (MathF.PI * 0.5f) - apartRad) return null;

        // Turned the way that opens it from the road: toward the road's normal on the side the heading meets it from.
        var side = -MathF.Sign(Vector2.Dot(heading, along)) * MathF.Sign((heading.X * along.Y) - (heading.Y * along.X));
        var turned = Heading.Unit(MathF.Atan2(heading.Y, heading.X) + (side * turnRad));
        return line.Crossed(fromM, fromM + (turned * reachM));
    }

    /// <summary>
    /// <b>Every node the lattice wants and every street it would lay, without laying one</b> — the points of each
    /// district's own grid, and the node on a district road each stub would meet. Placing is apart from laying so that the
    /// town's nodes are all settled before its first road (<see cref="TownLayout.SettleTheNodes"/>, GEN-16).
    /// </summary>
    /// <param name="corridors">Which path's corridor each cell of the partition stands in, or −1 (<see cref="Corridors"/>).</param>
    public static List<(int From, int To, bool Straight)> Place(
        TownLayout layout, Partition partition, Districts districts, Paths paths, int[] corridors, GroundShapes ground,
        SimConfig config, ulong seed)
    {
        var streets = new List<(int From, int To, bool Straight)>();
        for (var district = 0; district < districts.Count; district++)
        {
            PlaceOne(layout, partition, districts, district, paths, corridors, ground, config, seed, streets);
        }

        return streets;
    }

    /// <summary>
    /// <b>The point of a district's own lattice nearest a place</b>: where a road ending inside the district ends
    /// (<see cref="TownLinks"/>), so the lattice points round it hang their streets onto it square to their own, as they
    /// would onto any district road — and a road stopping between them would be met at no angle a junction takes (GEN-13).
    /// </summary>
    public static Vector2 PointNearest(Partition partition, Districts districts, int district, Vector2 atM)
    {
        var plan = districts[district];
        var along = Heading.Unit(plan.BearingRad);
        var across = Heading.RightOf(along);
        var offM = atM - partition.CentreM[district];
        var u = MathF.Round(Vector2.Dot(offM, along) / plan.BlockAlongM);
        var v = MathF.Round(Vector2.Dot(offM, across) / plan.BlockAcrossM);
        return partition.CentreM[district] + (along * (u * plan.BlockAlongM)) + (across * (v * plan.BlockAcrossM));
    }

    /// <summary>
    /// <b>Which path's corridor each cell of the partition stands in</b> (<see cref="Paths.CorridorM"/>), or −1 — the
    /// ground a lattice stands no node on, and the path a stub stopped short of it meets. Where two corridors overlap, the
    /// path laid first holds the cell.
    /// </summary>
    public static int[] Corridors(Partition partition, Paths paths)
    {
        var grid = partition.Grid;
        var held = new int[grid.Count];
        Array.Fill(held, -1);
        for (var path = 0; path < paths.All.Count; path++)
        {
            var line = paths.All[path].Line;
            var halfM = paths.CorridorM(path);
            for (var point = 0; point + 1 < line.PointsM.Length; point++)
            {
                var at = path;
                grid.Band(line.PointsM[point], line.PointsM[point + 1], halfM, cell =>
                {
                    if (held[cell] < 0) held[cell] = at;
                });
            }
        }

        return held;
    }

    /// <summary>The stream a street's place in its district's ranking is drawn on (GEN-11).</summary>
    const ulong StraightStream = 0x6C61_6964_7374_7200;

    /// <summary>
    /// <b>Which of one district's streets are laid straight</b> (GEN-47, <see cref="LayoutEdge.Straight"/>): its share of
    /// them, rounded, and <b>never all and never none</b> where it lays two, so every district lays streets of both kinds.
    /// </summary>
    /// <remarks>
    /// <b>Which ones is each street's own draw, keyed on the link and not on the walk over the lattice</b> (GEN-11): the
    /// streets are ranked by it and the share taken off the top, and they are handed on in the order they were found,
    /// which is the order they are offered in.
    /// </remarks>
    static void Straighten(
        TownLayout layout, List<(int From, int To)> found, float share, ulong seed,
        List<(int From, int To, bool Straight)> streets)
    {
        var ranked = new (float Draw, int At)[found.Count];
        for (var at = 0; at < found.Count; at++)
        {
            var key = ConnectionPoints.Keyed(layout.NodeM[found[at].From], layout.NodeM[found[at].To]);
            ranked[at] = (new Rng(seed, StraightStream ^ key).NextFloat(), at);
        }

        Array.Sort(ranked);

        var straight = (int)MathF.Round(share * found.Count);
        if (found.Count >= 2) straight = Math.Clamp(straight, 1, found.Count - 1);

        var laidStraight = new bool[found.Count];
        for (var rank = 0; rank < straight; rank++) laidStraight[ranked[rank].At] = true;

        for (var at = 0; at < found.Count; at++) streets.Add((found[at].From, found[at].To, laidStraight[at]));
    }

    /// <summary>
    /// And the streets themselves (<see cref="Settled"/>), offered in the order they were found once every path stands —
    /// each cut where a road joined after the paths crossed it (<see cref="TownLinks.JoinWhatWasLeft"/>) as the two halves
    /// that meet it there — and refused where every other refusal is (<see cref="TownLayout.Join"/>).
    /// </summary>
    public static void Lay(TownLayout layout, List<(int From, int To, bool Straight)> settled)
    {
        foreach (var (from, to, straight) in settled) layout.Join(from, to, RoadClass.Street, straight: straight);
    }

    /// <summary>
    /// The streets placed, on the nodes the settling left (<see cref="TownLayout.SettleTheNodes"/>), in the order they were
    /// found — each one whose ends were never stood or fell into one cluster left out, as no road.
    /// </summary>
    public static List<(int From, int To, bool Straight)> Settled(List<(int From, int To, bool Straight)> streets, int[] moved)
    {
        var settled = new List<(int, int, bool)>(streets.Count);
        foreach (var (from, to, straight) in streets)
        {
            if (from < 0 || to < 0 || moved[from] == moved[to]) continue;

            settled.Add((moved[from], moved[to], straight));
        }

        return settled;
    }

    static void PlaceOne(
        TownLayout layout, Partition partition, Districts districts, int district, Paths paths, int[] corridors,
        GroundShapes ground, SimConfig config, ulong seed, List<(int From, int To, bool Straight)> streets)
    {
        var (weldM, apartRad) = (config.CityGen.DistrictJunctionsApartLeastM, config.ArmsApartMinRad);
        var (stubWeldM, shortestStubInBlocks) = (config.CityGen.StubWeldM, config.CityGen.ShortestStubInBlocks);
        var plan = districts[district];
        var along = Heading.Unit(plan.BearingRad);
        var across = Heading.RightOf(along);
        var originM = partition.CentreM[district];
        // As far as the district's furthest ground, so a long strip is laid to its ends.
        var reach = (int)MathF.Ceiling(partition.ReachM[district] / plan.BlockTightestM) + 1;

        var side = (reach * 2) + 1;
        var node = new int[side * side];
        Array.Fill(node, -1);
        Vector2 PointM(int u, int v) => originM + (along * (u * plan.BlockAlongM)) + (across * (v * plan.BlockAcrossM));

        for (var u = -reach; u <= reach; u++)
        {
            for (var v = -reach; v <= reach; v++)
            {
                var atM = PointM(u, v);
                if (!Stands(atM)) continue;

                node[Slot(u, v)] = layout.AddNode(atM);
            }
        }

        var found = new List<(int From, int To)>();
        for (var u = -reach; u <= reach; u++)
        {
            for (var v = -reach; v <= reach; v++)
            {
                if (node[Slot(u, v)] < 0) continue;

                foreach (var (du, dv) in (ReadOnlySpan<(int, int)>)[(1, 0), (-1, 0), (0, 1), (0, -1)]) Walk(u, v, du, dv);
            }
        }

        Straighten(layout, found, plan.StraightShare, seed, streets);
        return;

        int Slot(int u, int v) => ((v + reach) * side) + u + reach;

        bool Stands(Vector2 atM) =>
            partition.At(atM) == district && ground.At(atM) == Ground.Grass && corridors[partition.Grid.IndexOf(atM)] < 0;

        // <b>From one point toward the next along the lattice, over the district's own ground</b>: a street where the next
        // point stands and nothing lies between — laid from the lesser of the two, so once — and a stub onto the first
        // district road whose corridor lies between, where one does, whether or not the next point stands beyond it. Never
        // onto an intercity road (GEN-68) nor a bridgehead (GEN-14a), and nothing where the walk leaves the district first.
        void Walk(int u, int v, int du, int dv)
        {
            var from = node[Slot(u, v)];
            var fromM = layout.NodeM[from];
            var inside = u + du >= -reach && u + du <= reach && v + dv >= -reach && v + dv <= reach;
            var to = inside ? node[Slot(u + du, v + dv)] : -1;
            var stepM = partition.Grid.CellM * 0.5f;
            var toward = PointM(u + du, v + dv) - PointM(u, v);
            var spanM = toward.Length();
            for (var outM = stepM; outM < spanM; outM += stepM)
            {
                var atM = fromM + (toward * (outM / spanM));
                if (atM.X < 0f || atM.Y < 0f) return;

                var path = corridors[partition.Grid.IndexOf(atM)];
                if (path < 0)
                {
                    if (partition.At(atM) != district) return;

                    continue;
                }

                if (paths.All[path].Class == RoadClass.Intercity) return;

                // <b>A road ending on the lattice's next point stands in for it</b> (TownLinks.Inside): the point is in that
                // road's own corridor, so no node of the lattice stands there and the lattice's line never crosses the road.
                if (paths.EndNear(path, PointM(u + du, v + dv), stubWeldM) is var end and >= 0)
                {
                    if (!paths.OnAnIntercity(end) && !paths.AtABridgehead(end)) found.Add((from, end));

                    return;
                }

                // <b>Where the lattice's own line crosses the road</b>, so the stub runs on along it square to the lattice's
                // other streets (GEN-13) — and none within a district road's spacing of a junction already on it (GEN-68),
                // nor onto where a road between districts meets an intercity road.
                var line = paths.All[path].Line;
                if (StubOnto(line, fromM, toward / spanM, spanM * 2f, apartRad) is not { } crossedM) return;

                var onto = paths.NodeAt(path, crossedM, stubWeldM, weldM);
                if (onto >= 0 && !paths.OnAnIntercity(onto) && !paths.AtABridgehead(onto) && Vector2.Distance(layout.NodeM[onto], fromM) >= plan.BlockTightestM * shortestStubInBlocks)
                {
                    found.Add((from, onto));
                }

                return;
            }

            if (to >= 0 && (du > 0 || dv > 0)) found.Add((from, to));
        }
    }
}
