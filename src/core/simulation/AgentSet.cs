using System.Numerics;

namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// <b>A set of agents, a bit each, walked in order</b>: what a tick walks in place of every agent — those the host holds,
/// those it touched — so the work follows what happened and not how many there are.
/// </summary>
/// <remarks>Walking it reads a word for every 64 agents, and an agent for every one in it.</remarks>
internal sealed class AgentSet(int capacity)
{
    readonly ulong[] _words = new ulong[Math.Max(1, (capacity + 63) / 64)];

    public int Capacity => _words.Length * 64;

    public void Add(int agent) => _words[agent >> 6] |= 1UL << (agent & 63);

    public void Remove(int agent) => _words[agent >> 6] &= ~(1UL << (agent & 63));

    public bool Has(int agent) => (_words[agent >> 6] & (1UL << (agent & 63))) != 0;

    /// <summary>
    /// The 32 agents' bits this one is among, as a device's table of a bit an agent holds them: word <c>agent / 32</c>, bit
    /// <c>agent % 32</c>.
    /// </summary>
    public uint WordOf(int agent) => (uint)(_words[agent >> 6] >> (agent & 32));

    /// <summary>Every agent below <paramref name="count"/> added.</summary>
    public void AddFirst(int count)
    {
        var whole = count >> 6;
        _words.AsSpan(0, whole).Fill(ulong.MaxValue);
        if ((count & 63) != 0) _words[whole] |= (1UL << (count & 63)) - 1;
    }

    public void Clear() => Array.Clear(_words);

    /// <summary>This set made the same as <paramref name="other"/>, of the same capacity.</summary>
    public void CopyFrom(AgentSet other) => other._words.CopyTo(_words, 0);

    /// <summary>Every agent of <paramref name="other"/> added.</summary>
    public void UnionWith(AgentSet other)
    {
        for (var word = 0; word < _words.Length; word++) _words[word] |= other._words[word];
    }

    /// <summary>Every agent not in <paramref name="other"/> taken out.</summary>
    public void IntersectWith(AgentSet other)
    {
        for (var word = 0; word < _words.Length; word++) _words[word] &= other._words[word];
    }

    /// <summary>
    /// The agents below <paramref name="count"/> in this set or in <paramref name="other"/>, in order, written into
    /// <paramref name="into"/>; how many.
    /// </summary>
    public int Union(AgentSet? other, int count, Span<int> into)
    {
        var written = 0;
        var words = (count + 63) >> 6;
        for (var word = 0; word < words; word++)
        {
            var bits = _words[word] | (other?._words[word] ?? 0UL);
            if (word == words - 1 && (count & 63) != 0) bits &= (1UL << (count & 63)) - 1;
            while (bits != 0)
            {
                into[written++] = (word << 6) + BitOperations.TrailingZeroCount(bits);
                bits &= bits - 1;
            }
        }

        return written;
    }

    /// <summary>The agents below <paramref name="count"/> in this set, in order, written into <paramref name="into"/>; how many.</summary>
    public int CopyTo(int count, Span<int> into) => Union(null, count, into);

    /// <summary>The agents below <paramref name="count"/> not in this set, in order, written into <paramref name="into"/>; how many.</summary>
    public int CopyAbsentTo(int count, Span<int> into)
    {
        var written = 0;
        var words = (count + 63) >> 6;
        for (var word = 0; word < words; word++)
        {
            var bits = ~_words[word];
            if (word == words - 1 && (count & 63) != 0) bits &= (1UL << (count & 63)) - 1;
            while (bits != 0)
            {
                into[written++] = (word << 6) + BitOperations.TrailingZeroCount(bits);
                bits &= bits - 1;
            }
        }

        return written;
    }

    /// <summary>How many agents below <paramref name="count"/> the set holds.</summary>
    public int CountBelow(int count)
    {
        var held = 0;
        var words = (count + 63) >> 6;
        for (var word = 0; word < words; word++)
        {
            var bits = _words[word];
            if (word == words - 1 && (count & 63) != 0) bits &= (1UL << (count & 63)) - 1;
            held += BitOperations.PopCount(bits);
        }

        return held;
    }
}
