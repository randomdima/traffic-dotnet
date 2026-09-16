namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Which nodes belong to the same thing</b>, as the disjoint set the layout keeps asking for: what a
/// locality merges into one junction, what is joined to the largest piece of the town, and which arcs are
/// one roundabout's ring.
/// </summary>
/// <remarks>
/// <b>A root array and nothing round it.</b> Every caller already owns an array a node long and wants it
/// renumbered afterwards, so a type here would be a wrapper over the one field it would hold — what is
/// shared is the halving walk, which is the part that is easy to write differently in three places.
/// </remarks>
internal static class Clusters
{
    /// <summary>One root a node, which is every node in a cluster of its own.</summary>
    public static int[] Apart(int nodes)
    {
        var root = new int[nodes];
        for (var node = 0; node < nodes; node++) root[node] = node;
        return root;
    }

    /// <summary>The cluster a node is in, halving the path it walked on the way.</summary>
    public static int Find(int[] root, int node)
    {
        while (root[node] != node)
        {
            root[node] = root[root[node]];
            node = root[node];
        }

        return node;
    }

    /// <summary>And the two clusters joined into one.</summary>
    public static void Union(int[] root, int one, int other)
    {
        one = Find(root, one);
        other = Find(root, other);
        if (one != other) root[other] = one;
    }
}
