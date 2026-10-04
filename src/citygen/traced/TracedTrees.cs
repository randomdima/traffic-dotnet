using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A traced town's trees, where its survey maps them</b> (GEN-57): each a prop of the open country's kind at the
/// size the town's great trees are drawn at, standing where OSM puts it — and none whose crown would reach the ground
/// the traffic drives, or past the map's edge.
/// </summary>
/// <remarks>
/// <para>
/// <b>A tree is a placement and not a picture</b> (GEN-6b): OSM says a tree stands here, and the catalogue picks which
/// tree off its kind and size. At <see cref="CityGenFigures.TracedTreeRadiusM"/> only trees are drawn that size, so a
/// tree is never drawn as a thicket.
/// </para>
/// <para>
/// <b>Off the driven ground by a lattice step</b> (TER-4c.4): the town's furniture stands on no driven ribbon, and a
/// ribbon is read onto a point within half a lattice diagonal of it, so a crown is kept that much clear of every
/// road's carriageway — and of a junction's disc and the widest arm's width beyond it, which is as far as a movement
/// across it reaches.
/// </para>
/// </remarks>
internal static class TracedTrees
{
    /// <summary>How many roads one tree is weighed against at most: more than ever pass within a crown of one place.</summary>
    const int Nearby = 64;

    public static CityPlan.PropArrays Lay(Survey survey, CityPlan.RoadArrays roads, CityPlan.JunctionArrays junctions, SimConfig config)
    {
        var radiusM = config.CityGen.TracedTreeRadiusM;
        var clearM = config.RibbonLatticeStepM;
        var lengthM = new float[roads.Count];
        var widestM = 0f;
        for (var road = 0; road < roads.Count; road++)
        {
            lengthM[road] = Spline.TotalLengthM(roads.SegmentsOf(road));
            widestM = MathF.Max(widestM, roads.WidthM[road]);
        }

        var index = ChainIndex.OfChains(roads.Segments, roads.SegmentOffsets, lengthM, config.Grid.Main);
        var reachOf = Reaches(roads, junctions);
        var discs = Discs(junctions, reachOf, radiusM + clearM, out var cellM);

        var ids = new int[Nearby];
        var alongM = new float[Nearby];
        var kept = new List<Vector2>(survey.TreeM.Length);
        foreach (var treeM in survey.TreeM)
        {
            if (!OnTheMap(treeM) || OnARoad(treeM) || InAJunction(treeM)) continue;

            kept.Add(treeM);
        }

        var radii = new float[kept.Count];
        var kind = new byte[kept.Count];
        Array.Fill(radii, radiusM);
        Array.Fill(kind, (byte)PropKind.WildNature);
        return new CityPlan.PropArrays { CentreM = [.. kept], RadiusM = radii, BearingRad = new float[kept.Count], Kind = kind };

        bool OnTheMap(Vector2 treeM) =>
            treeM.X >= radiusM && treeM.Y >= radiusM && treeM.X <= survey.WidthM - radiusM && treeM.Y <= survey.HeightM - radiusM;

        bool OnARoad(Vector2 treeM)
        {
            var near = index.Near(treeM, (widestM * 0.5f) + radiusM + clearM, ids, alongM);
            for (var at = 0; at < near; at++)
            {
                var road = ids[at];
                var offM = Vector2.Distance(Spline.SampleAt(roads.SegmentsOf(road), alongM[at]).PositionM, treeM);
                if (offM < (roads.WidthM[road] * 0.5f) + radiusM + clearM) return true;
            }

            return false;
        }

        bool InAJunction(Vector2 treeM)
        {
            var (column, row) = ((int)MathF.Floor(treeM.X / cellM), (int)MathF.Floor(treeM.Y / cellM));
            for (var across = column - 1; across <= column + 1; across++)
            {
                for (var down = row - 1; down <= row + 1; down++)
                {
                    if (!discs.TryGetValue((across, down), out var here)) continue;

                    foreach (var junction in here)
                    {
                        if (Vector2.Distance(junctions.CentreM[junction], treeM) < reachOf[junction] + radiusM + clearM) return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// How far a movement across each junction can reach from its centre: its disc, and the widest of its arms beyond
    /// it — a movement leaves from a lane's end at the disc's edge, half that arm's carriageway to one side.
    /// </summary>
    static float[] Reaches(CityPlan.RoadArrays roads, CityPlan.JunctionArrays junctions)
    {
        var reachM = (float[])junctions.RadiusM.Clone();
        var widestM = new float[junctions.Count];
        for (var road = 0; road < roads.Count; road++)
        {
            widestM[roads.FromJunction[road]] = MathF.Max(widestM[roads.FromJunction[road]], roads.WidthM[road]);
            widestM[roads.ToJunction[road]] = MathF.Max(widestM[roads.ToJunction[road]], roads.WidthM[road]);
        }

        for (var junction = 0; junction < reachM.Length; junction++) reachM[junction] += widestM[junction];
        return reachM;
    }

    /// <summary>
    /// Every junction filed under the square cell its centre stands in, a cell as wide as the furthest any reaches and
    /// a crown beyond — so the cells round a tree's own hold every junction it could stand too near.
    /// </summary>
    static Dictionary<(int, int), List<int>> Discs(CityPlan.JunctionArrays junctions, float[] reachM, float beyondM, out float cellM)
    {
        cellM = 1f;
        foreach (var reach in reachM) cellM = MathF.Max(cellM, reach + beyondM);

        var discs = new Dictionary<(int, int), List<int>>();
        for (var junction = 0; junction < junctions.Count; junction++)
        {
            var key = ((int)MathF.Floor(junctions.CentreM[junction].X / cellM), (int)MathF.Floor(junctions.CentreM[junction].Y / cellM));
            if (!discs.TryGetValue(key, out var here)) discs[key] = here = [];
            here.Add(junction);
        }

        return discs;
    }
}
