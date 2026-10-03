namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>An extract's coastline as the sea inside the map</b>: closed rings in north-up metres off the map's
/// south-west corner. OSM draws a coastline with the land on its left, so each run of it across the map is
/// closed by walking the map's edge clockwise — the way that keeps the sea on the right — to the next run's
/// entry.
/// </summary>
/// <remarks>
/// <b>An island is left out</b>: a coastline closed inside the map is land the water would have to carry as a
/// hole, and the water has none (<see cref="CityPlan.WaterArrays"/>). It stays in the extract.
/// </remarks>
internal static class Coastline
{
    public static List<List<Vector2D>> SeaRings(OsmExtract extract, Vector2D[] upM, double widthM, double heightM)
    {
        var coast = new List<OsmWay>();
        foreach (var way in extract.Ways)
        {
            if (way.Tag("natural") == "coastline") coast.Add(way);
        }

        var runs = new List<List<Vector2D>>();
        foreach (var chain in Chains(coast))
        {
            var closed = chain.Count > 3 && chain[0] == chain[^1];
            if (closed && chain.TrueForAll(node => Inside(upM[node], widthM, heightM))) continue;

            runs.AddRange(Clipped(chain.ConvertAll(node => upM[node]), widthM, heightM));
        }

        var perimeterM = 2 * (widthM + heightM);
        var entries = new (double AlongM, int Run)[runs.Count];
        for (var run = 0; run < runs.Count; run++) entries[run] = (Along(runs[run][0], widthM, heightM), run);

        Array.Sort(entries);

        var rings = new List<List<Vector2D>>();
        var done = new bool[runs.Count];
        for (var first = 0; first < runs.Count; first++)
        {
            if (done[first]) continue;

            var ring = new List<Vector2D>();
            var run = first;
            while (!done[run])
            {
                done[run] = true;
                ring.AddRange(runs[run]);
                var leavesM = Along(runs[run][^1], widthM, heightM);
                var ahead = entries[0];
                foreach (var entry in entries)
                {
                    if (Ahead(entry.AlongM) < Ahead(ahead.AlongM)) ahead = entry;
                }

                ring.AddRange(EdgeCorners(leavesM, ahead.AlongM, widthM, heightM));
                run = ahead.Run;

                double Ahead(double atM) => (((atM - leavesM) % perimeterM) + perimeterM) % perimeterM;
            }

            rings.Add(ring);
        }

        return rings;
    }

    /// <summary>
    /// Every coastline joined end to end into as long a chain of nodes as it makes, starting from the ways no
    /// other leads into, so a coast that crosses the map is one chain from where it enters to where it leaves.
    /// </summary>
    static List<List<int>> Chains(List<OsmWay> coast)
    {
        var starting = new Dictionary<int, int>();
        var ending = new HashSet<int>();
        for (var way = 0; way < coast.Count; way++)
        {
            starting[coast[way].Nodes[0]] = way;
            ending.Add(coast[way].Nodes[^1]);
        }

        var heads = new List<int>();
        for (var way = 0; way < coast.Count; way++)
        {
            if (!ending.Contains(coast[way].Nodes[0])) heads.Add(way);
        }

        for (var way = 0; way < coast.Count; way++) heads.Add(way);

        var used = new bool[coast.Count];
        var chains = new List<List<int>>();
        foreach (var head in heads)
        {
            if (used[head]) continue;

            var chain = new List<int>();
            var way = head;
            while (way >= 0 && !used[way])
            {
                used[way] = true;
                chain.AddRange(coast[way].Nodes.AsSpan(chain.Count == 0 ? 0 : 1));
                way = starting.GetValueOrDefault(coast[way].Nodes[^1], -1);
            }

            chains.Add(chain);
        }

        return chains;
    }

    static bool Inside(Vector2D atM, double widthM, double heightM) =>
        atM.X >= 0 && atM.X <= widthM && atM.Y >= 0 && atM.Y <= heightM;

    /// <summary>The runs of a polyline inside the map, each from where it enters to where it leaves (Liang–Barsky).</summary>
    static List<List<Vector2D>> Clipped(List<Vector2D> chain, double widthM, double heightM)
    {
        var runs = new List<List<Vector2D>>();
        List<Vector2D>? run = null;
        for (var at = 1; at < chain.Count; at++)
        {
            var a = chain[at - 1];
            var b = chain[at];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var (t0, t1) = (0.0, 1.0);
            foreach (var (p, q) in (ReadOnlySpan<(double, double)>)[(-dx, a.X), (dx, widthM - a.X), (-dy, a.Y), (dy, heightM - a.Y)])
            {
                if (p == 0)
                {
                    if (q < 0) (t0, t1) = (1.0, 0.0);
                    continue;
                }

                var r = q / p;
                if (p < 0) t0 = Math.Max(t0, r);
                else t1 = Math.Min(t1, r);
            }

            if (t0 > t1) continue;

            run ??= [new Vector2D(a.X + (t0 * dx), a.Y + (t0 * dy))];
            run.Add(new Vector2D(a.X + (t1 * dx), a.Y + (t1 * dy)));
            if (t1 < 1.0)
            {
                runs.Add(run);
                run = null;
            }
        }

        if (run is not null) runs.Add(run);
        runs.RemoveAll(kept => kept.Count < 2);
        return runs;
    }

    /// <summary>How far clockwise round the map's edge a point on it stands, from the north-west corner (north up).</summary>
    static double Along(Vector2D atM, double widthM, double heightM)
    {
        var (x, y) = (atM.X, atM.Y);
        var (offM, edge) = (heightM - y, 0);
        if (widthM - x < offM) (offM, edge) = (widthM - x, 1);
        if (y < offM) (offM, edge) = (y, 2);
        if (x < offM) edge = 3;

        return edge switch
        {
            0 => x,
            1 => widthM + (heightM - y),
            2 => widthM + heightM + (widthM - x),
            _ => (2 * widthM) + heightM + y,
        };
    }

    /// <summary>The map's corners passed walking clockwise from one place on its edge to another.</summary>
    static List<Vector2D> EdgeCorners(double leavesM, double entersM, double widthM, double heightM)
    {
        var perimeterM = 2 * (widthM + heightM);
        var spanM = Wrapped(entersM - leavesM);
        var corners = new (double AheadM, Vector2D CornerM)[]
        {
            (Wrapped(widthM - leavesM), new Vector2D(widthM, heightM)),
            (Wrapped(widthM + heightM - leavesM), new Vector2D(widthM, 0)),
            (Wrapped((2 * widthM) + heightM - leavesM), new Vector2D(0, 0)),
            (Wrapped(-leavesM), new Vector2D(0, heightM)),
        };
        Array.Sort(corners, (one, other) => one.AheadM.CompareTo(other.AheadM));

        var passed = new List<Vector2D>();
        foreach (var (aheadM, cornerM) in corners)
        {
            if (aheadM > 0 && aheadM < spanM) passed.Add(cornerM);
        }

        return passed;

        double Wrapped(double runM) => ((runM % perimeterM) + perimeterM) % perimeterM;
    }
}
