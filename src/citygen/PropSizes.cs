namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The looks a town lays its props as</b> (GEN-6e): each look's kind, girth, body and share, in the catalogue's own
/// order, so a prop names the look it wears by its place here and is drawn in the same place.
/// </summary>
/// <remarks>
/// <para>
/// <b>A prop is laid at the size of the look it will wear</b>, so the sizes are the art's and the placement is the
/// plan's — on the terms a building is sized by its roof (<see cref="BuildingSizes"/>). Drawn from a band of the plan's
/// own instead, the plan would state a size the catalogue then answers with the nearest picture it has, stretched to it.
/// </para>
/// <para>
/// <b>A kind's looks are filed narrowest first</b>, each with the running total of its kind's shares up to it, so a draw
/// is one search however many looks there are, and a draw among the looks no wider than a figure (GEN-6g) is the same
/// search over the run's head.
/// </para>
/// <para>
/// <b>It is data crossing a seam and not a type</b> ([the slice map](../../docs/slice-map.md)): the catalogue is
/// <c>World.Statics.PropCatalog</c>, which sits above the plan.
/// </para>
/// </remarks>
internal sealed class PropSizes
{
    /// <summary>How many kinds there are to hold looks for, which is the whole of <see cref="PropKind"/>.</summary>
    const int Kinds = (int)PropKind.UrbanFurniture + 1;

    /// <summary>Every look of a kind, a crowned look of a kind, each a run of looks filed narrowest first.</summary>
    readonly Pool[] _of = new Pool[Kinds];

    readonly Pool[] _crownsOf = new Pool[Kinds];

    /// <param name="kind">The set each look is placed in.</param>
    /// <param name="girthM">The diameter of the disc the town clears ground for, for each look (<c>PropVariant.GirthM</c>).</param>
    /// <param name="bodyM">The diameter of the disc each stands as a body, within its girth (<c>PropVariant.BodyM</c>).</param>
    /// <param name="share">How often each is drawn against the others of its kind (<c>PropVariant.Share</c>); evenly where left out.</param>
    public PropSizes(PropKind[] kind, float[] girthM, float[] bodyM, float[]? share = null)
    {
        share ??= Even(kind.Length);
        if (kind.Length != girthM.Length || kind.Length != bodyM.Length || kind.Length != share.Length)
        {
            throw new ArgumentException("A look's kind, girth, body and share are one list each, as long as the others.");
        }

        // The plan carries a look in sixteen bits (CityPlan.PropArrays.Look).
        if (kind.Length > ushort.MaxValue + 1) throw new ArgumentException($"{kind.Length} looks is more than a plan can name.");

        Kind = kind;
        GirthM = girthM;
        BodyM = bodyM;
        Share = share;
        for (var of = 0; of < Kinds; of++)
        {
            _of[of] = Pool.Of(this, (PropKind)of, crownsOnly: false);
            _crownsOf[of] = Pool.Of(this, (PropKind)of, crownsOnly: true);
        }

        foreach (var of in girthM) WidestM = MathF.Max(WidestM, of);
    }

    /// <summary>No looks at all — a town laid in code, which lays no prop.</summary>
    public static PropSizes None { get; } = new([], [], []);

    public PropKind[] Kind { get; }

    /// <summary>What a prop's ground is cleared to and what it is spaced by (GEN-6a, GEN-6c).</summary>
    public float[] GirthM { get; }

    /// <summary>What a prop along a kerb stands as a body, and nothing else reads.</summary>
    public float[] BodyM { get; }

    /// <summary>How often each look is drawn against the others of its kind (GEN-6e).</summary>
    public float[] Share { get; }

    public int Count => Kind.Length;

    /// <summary>The widest girth of any look: what the scatter's grid is laid to cover (GEN-6c).</summary>
    public float WidestM { get; }

    /// <summary>Whether the art holds any look of a kind.</summary>
    public bool Holds(PropKind kind) => _of[(int)kind].Count > 0;

    /// <summary>
    /// <b>One look of a kind, each as often as its share says</b> (GEN-6e), off one draw in [0, 1) — and −1 where the art
    /// holds none of the kind.
    /// </summary>
    public int Draw(PropKind kind, float u) => _of[(int)kind].Draw(u, float.PositiveInfinity);

    /// <summary>The same, among the looks of the kind no wider than <paramref name="widestM"/> (GEN-6g).</summary>
    public int DrawNoWider(PropKind kind, float u, float widestM) => _of[(int)kind].Draw(u, widestM);

    /// <summary>
    /// <b>One crowned look of a kind</b> (GEN-6f): one standing as a body narrower than its picture, which is what a
    /// trunk under a crown is — and −1 where the art holds none.
    /// </summary>
    public int DrawCrown(PropKind kind, float u) => _crownsOf[(int)kind].Draw(u, float.PositiveInfinity);

    static float[] Even(int count)
    {
        var even = new float[count];
        Array.Fill(even, 1f);
        return even;
    }

    /// <summary>A run of looks filed narrowest first, the ties in the catalogue's order, and the running total of their shares.</summary>
    readonly struct Pool(int[] looks, float[] girthM, float[] upToShare)
    {
        public int Count => looks.Length;

        public static Pool Of(PropSizes sizes, PropKind kind, bool crownsOnly)
        {
            var looks = new List<int>();
            for (var look = 0; look < sizes.Count; look++)
            {
                if (sizes.Kind[look] != kind || (crownsOnly && !(sizes.BodyM[look] < sizes.GirthM[look]))) continue;
                if (!(sizes.Share[look] > 0f)) throw new ArgumentException($"look {look}'s share is {sizes.Share[look]}, and a share is more than nothing.");

                looks.Add(look);
            }

            var filed = looks.ToArray();
            var girthM = new float[filed.Length];
            for (var at = 0; at < filed.Length; at++) girthM[at] = sizes.GirthM[filed[at]];

            // Stable, so two looks of one girth keep the catalogue's order and the town laid hangs on nothing else.
            var order = new int[filed.Length];
            for (var at = 0; at < order.Length; at++) order[at] = at;
            Array.Sort(order, (a, b) => girthM[a] != girthM[b] ? girthM[a].CompareTo(girthM[b]) : a.CompareTo(b));

            var sorted = new int[filed.Length];
            var sortedGirthM = new float[filed.Length];
            var upToShare = new float[filed.Length];
            var total = 0f;
            for (var at = 0; at < order.Length; at++)
            {
                sorted[at] = filed[order[at]];
                sortedGirthM[at] = girthM[order[at]];
                total += sizes.Share[sorted[at]];
                upToShare[at] = total;
            }

            return new(sorted, sortedGirthM, upToShare);
        }

        public int Draw(float u, float widestM)
        {
            var end = Count;
            if (widestM < float.PositiveInfinity)
            {
                end = Array.BinarySearch(girthM, widestM);
                if (end < 0) end = ~end;
                else
                {
                    while (end < Count && girthM[end] <= widestM) end++;
                }
            }

            if (end == 0) return -1;

            var wanted = u * upToShare[end - 1];
            var at = Array.BinarySearch(upToShare, 0, end, wanted);
            at = at < 0 ? ~at : at + 1;
            return looks[Math.Min(at, end - 1)];
        }
    }
}
