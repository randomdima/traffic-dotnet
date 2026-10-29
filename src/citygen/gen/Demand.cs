using System.Numerics;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>How much each road between districts carries, and so how many lanes it is laid with</b> (GEN-67): every pair of
/// districts joined by a demand that grows with both districts' size and falls with the distance between them, routed the
/// shortest way over the roads between districts and along the intercity roads, and the most-loaded share of them laid four
/// lanes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A ranking and not a load.</b> What a demand is worth in cars is nothing the generator knows; what it does know is
/// which roads more of the town's districts reach each other over, which is what a wider road is for. <b>Asked before any
/// road is laid</b>, of the districts and the roads planned between them, because the ground a road keeps is fixed when it
/// is offered.
/// </para>
/// <para>
/// <b>Routed over the roads and not over the districts</b>: a place is where a road ends or another lands on it, so the two
/// halves of a crossroads over an intercity road are both on the way across it, and a road in from an intercity road or
/// across a district carries what comes that way. <b>A district is where its demand starts and ends and never a way
/// through</b>: the first places are the districts, each joined to every place on its roads by the distance its own streets
/// take to there, which no route between two other districts may use.
/// </para>
/// </remarks>
internal static class Demand
{
    /// <summary>One way between two places: the two, what driving it costs in metres, and the road it is a piece of, or −1.</summary>
    internal readonly record struct Link(int From, int To, float CostM, int Road);

    /// <summary>
    /// The load each road carries: the most any piece of it carries. <b>Only the roads are ranked</b> by the caller; a link
    /// along an intercity road or a district's own streets carries demand like any other and is no road of the plan's.
    /// </summary>
    /// <param name="places">How many places there are; the first <c>centreM.Length</c> of them are the districts.</param>
    /// <param name="roads">How many roads the links are pieces of.</param>
    public static float[] Loads(int places, IReadOnlyList<Link> links, int roads, Vector2[] centreM, float[] areaM2)
    {
        var districts = centreM.Length;
        var at = new List<(int Link, int To)>[places];
        for (var place = 0; place < places; place++) at[place] = [];
        for (var link = 0; link < links.Count; link++)
        {
            at[links[link].From].Add((link, links[link].To));
            at[links[link].To].Add((link, links[link].From));
        }

        var carried = new float[links.Count];
        var costM = new float[places];
        var via = new int[places];
        var open = new PriorityQueue<int, float>();
        for (var from = 0; from < districts; from++)
        {
            Array.Fill(costM, float.PositiveInfinity);
            Array.Fill(via, -1);
            costM[from] = 0f;
            open.Enqueue(from, 0f);
            while (open.TryDequeue(out var here, out var hereM))
            {
                if (hereM > costM[here] || (here < districts && here != from)) continue;

                foreach (var (link, to) in at[here])
                {
                    var toM = hereM + links[link].CostM;
                    if (toM >= costM[to]) continue;

                    costM[to] = toM;
                    via[to] = link;
                    open.Enqueue(to, toM);
                }
            }

            for (var to = from + 1; to < districts; to++)
            {
                if (via[to] < 0) continue;

                var apartM = MathF.Max(Vector2.Distance(centreM[from], centreM[to]), 1f);
                var demand = areaM2[from] * areaM2[to] / (apartM * apartM);
                for (var place = to; place != from && via[place] >= 0;)
                {
                    var link = via[place];
                    carried[link] += demand;
                    place = links[link].From == place ? links[link].To : links[link].From;
                }
            }
        }

        var load = new float[roads];
        for (var link = 0; link < links.Count; link++)
        {
            if (links[link].Road >= 0) load[links[link].Road] = MathF.Max(load[links[link].Road], carried[link]);
        }

        return load;
    }

    /// <summary>Which of some loads are the most-loaded share, rounded — the earlier of two as loaded first.</summary>
    public static bool[] Busiest(ReadOnlySpan<float> loads, float share)
    {
        var busiest = new bool[loads.Length];
        var ranked = new int[loads.Length];
        for (var at = 0; at < ranked.Length; at++) ranked[at] = at;

        var copy = loads.ToArray();
        Array.Sort(ranked, (a, b) => (copy[b], a).CompareTo((copy[a], b)));
        var wide = (int)MathF.Round(loads.Length * share);
        for (var at = 0; at < wide; at++) busiest[ranked[at]] = copy[ranked[at]] > 0f;

        return busiest;
    }
}
