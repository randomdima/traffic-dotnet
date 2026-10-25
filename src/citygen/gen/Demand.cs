using System.Numerics;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>How much each road between districts carries, and so how many lanes it is laid with</b> (GEN-67): every pair of
/// districts joined by a demand that grows with both districts' size and falls with the distance between them, routed the
/// shortest way over the roads between districts, and the most-loaded share of them laid four lanes.
/// </summary>
/// <remarks>
/// <b>A ranking and not a load.</b> What a demand is worth in cars is nothing the generator knows; what it does know is
/// which roads more of the town's districts reach each other over, which is what a wider road is for. <b>Asked before any
/// road is laid</b>, of the districts and the links between them, because the ground a road keeps is fixed when it is
/// offered.
/// </remarks>
internal static class Demand
{
    /// <summary>One way between two districts: the two, how long it is, and what driving it costs against its length.</summary>
    internal readonly record struct Link(int A, int B, float LengthM, float Weight);

    /// <summary>
    /// The load each link carries, its own index's. <b>Only the links that are a path's are ranked</b> by the caller; a
    /// link along an intercity road carries demand like any other and is never widened, being as wide as it gets.
    /// </summary>
    public static float[] Loads(IReadOnlyList<Link> links, Vector2[] centreM, float[] areaM2)
    {
        var count = centreM.Length;
        var at = new List<(int Link, int To)>[count];
        for (var district = 0; district < count; district++) at[district] = [];
        for (var link = 0; link < links.Count; link++)
        {
            at[links[link].A].Add((link, links[link].B));
            at[links[link].B].Add((link, links[link].A));
        }

        var load = new float[links.Count];
        var costM = new float[count];
        var via = new int[count];
        var open = new PriorityQueue<int, float>();
        for (var from = 0; from < count; from++)
        {
            Array.Fill(costM, float.PositiveInfinity);
            Array.Fill(via, -1);
            costM[from] = 0f;
            open.Enqueue(from, 0f);
            while (open.TryDequeue(out var here, out var hereM))
            {
                if (hereM > costM[here]) continue;

                foreach (var (link, to) in at[here])
                {
                    var toM = hereM + (links[link].LengthM * links[link].Weight);
                    if (toM >= costM[to]) continue;

                    costM[to] = toM;
                    via[to] = link;
                    open.Enqueue(to, toM);
                }
            }

            for (var to = from + 1; to < count; to++)
            {
                if (via[to] < 0) continue;

                var apartM = MathF.Max(Vector2.Distance(centreM[from], centreM[to]), 1f);
                var demand = areaM2[from] * areaM2[to] / (apartM * apartM);
                for (var district = to; district != from && via[district] >= 0;)
                {
                    var link = via[district];
                    load[link] += demand;
                    district = links[link].A == district ? links[link].B : links[link].A;
                }
            }
        }

        return load;
    }

    /// <summary>Which of some loads are the most-loaded share, rounded.</summary>
    public static bool[] Busiest(ReadOnlySpan<float> loads, float share)
    {
        var busiest = new bool[loads.Length];
        var ranked = new int[loads.Length];
        for (var at = 0; at < ranked.Length; at++) ranked[at] = at;

        var copy = loads.ToArray();
        Array.Sort(ranked, (a, b) => copy[b].CompareTo(copy[a]));
        var wide = (int)MathF.Round(loads.Length * share);
        for (var at = 0; at < wide; at++) busiest[ranked[at]] = copy[ranked[at]] > 0f;

        return busiest;
    }
}
