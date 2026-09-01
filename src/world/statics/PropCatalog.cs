using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Statics;

/// <summary>
/// One prop look: the image, the ground its kind belongs on (GEN-6b), the size band it was drawn for, and
/// whether it has a front to be turned by the bearing the plan laid it on.
/// </summary>
internal readonly record struct PropVariant(
    string Id, int Kind, string SpritePath, float DiameterM, bool Turns);

/// <summary>
/// The looks a prop can wear, read from <c>assets/…/Catalog.json</c>, and the two-step rule that picks
/// one.
/// </summary>
/// <remarks>
/// <para>
/// Kind chooses the set, size chooses the band, and one draw chooses inside it — with a band tolerance
/// of <see cref="BandToleranceM"/>. The plan carries a prop's kind and the radius it was laid at; the
/// four bands are what the art was authored in, so a look is only ever stretched by the jitter its own
/// band allows.
/// </para>
/// <para>
/// The draw is a hash of the prop's index and not a stream: the town arrives already laid, so there is
/// no world stream to be at the same point of, and taking one from the simulation's own generator would
/// move the decision stagger and every figure measured beside it.
/// </para>
/// </remarks>
internal sealed class PropCatalog
{
    /// <summary>How far off its authored band a look may be drawn — the bands are 0.5 m apart at the bottom end.</summary>
    public const float BandToleranceM = 0.3f;

    readonly int[] _byKind;
    readonly int[] _kindOffsets;

    PropCatalog(PropVariant[] variants, int[] byKind, int[] kindOffsets)
    {
        Variants = variants;
        _byKind = byKind;
        _kindOffsets = kindOffsets;
    }

    public PropVariant[] Variants { get; }

    public int Count => Variants.Length;

    public static PropCatalog Load()
    {
        var catalogPath = Path.Combine(ProjectPaths.Assets, "world", "prop", "variants", "common", "Catalog.json");
        var entries = AssetJson.Catalog(catalogPath);

        var variants = new List<PropVariant>(entries.Length);
        foreach (var entry in entries) variants.Add(ReadVariant(entry));

        // One pass over the catalogue lays every set out contiguously and in size order, which is what
        // makes a band a range rather than a search: the look is picked in the town's inner loop, once
        // per prop, over ninety-five thousand of them.
        var kinds = variants.Max(variant => variant.Kind) + 1;
        var offsets = new int[kinds + 1];
        foreach (var variant in variants) offsets[variant.Kind + 1]++;
        for (var kind = 0; kind < kinds; kind++) offsets[kind + 1] += offsets[kind];

        var order = new int[variants.Count];
        var next = (int[])offsets.Clone();
        for (var variant = 0; variant < variants.Count; variant++) order[next[variants[variant].Kind]++] = variant;
        for (var kind = 0; kind < kinds; kind++)
        {
            Array.Sort(order, offsets[kind], offsets[kind + 1] - offsets[kind],
                Comparer<int>.Create((left, right) => variants[left].DiameterM.CompareTo(variants[right].DiameterM)));
        }

        return new PropCatalog([.. variants], order, offsets);
    }

    /// <summary>
    /// Which look the prop at <paramref name="index"/> wears. Answers a variant index, always: a kind
    /// with nothing authored in the band falls back to the nearest size in its own set, and a kind with
    /// nothing authored at all to the catalogue's first look, because a prop the town collides with and
    /// nothing draws is the one outcome that reads as a renderer bug.
    /// </summary>
    public int Look(int kind, float diameterM, int index)
    {
        if (kind < 0 || kind + 1 >= _kindOffsets.Length) return 0;

        var from = _kindOffsets[kind];
        var to = _kindOffsets[kind + 1];
        if (from == to) return 0;

        var (bandFrom, bandTo) = Band(from, to, diameterM);
        if (bandFrom == bandTo) return _byKind[Nearest(from, to, diameterM)];

        return _byKind[bandFrom + (int)(Mix((uint)index) % (uint)(bandTo - bandFrom))];
    }

    /// <summary>The run of looks in one kind's set whose authored size is within the tolerance, as a half-open range over the set.</summary>
    (int From, int To) Band(int from, int to, float diameterM)
    {
        var bandFrom = to;
        var bandTo = from;
        for (var slot = from; slot < to; slot++)
        {
            if (MathF.Abs(Variants[_byKind[slot]].DiameterM - diameterM) > BandToleranceM) continue;

            if (slot < bandFrom) bandFrom = slot;
            bandTo = slot + 1;
        }

        return bandFrom <= bandTo ? (bandFrom, bandTo) : (from, from);
    }

    int Nearest(int from, int to, float diameterM)
    {
        var best = from;
        var bestError = float.MaxValue;
        for (var slot = from; slot < to; slot++)
        {
            var error = MathF.Abs(Variants[_byKind[slot]].DiameterM - diameterM);
            if (error >= bestError) continue;

            (best, bestError) = (slot, error);
        }

        return best;
    }

    /// <summary>An avalanche off the prop's own index, so two props side by side do not wear the same look.</summary>
    static uint Mix(uint index)
    {
        index ^= index >> 16;
        index *= 0x7feb352d;
        index ^= index >> 15;
        index *= 0x846ca68b;
        return index ^ (index >> 16);
    }

    static PropVariant ReadVariant(string path)
    {
        var variant = AssetJson.Read(path, PropVariantJson.Default.PropVariantFile);
        return new PropVariant(
            variant.Id, variant.Kind, AssetJson.Beside(path, variant.Sprite), variant.DiameterM, variant.Turns);
    }
}
