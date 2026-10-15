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

    public void Clear() => Array.Clear(_words);

    /// <summary>Every agent of <paramref name="other"/> added.</summary>
    public void UnionWith(AgentSet other)
    {
        for (var word = 0; word < _words.Length; word++) _words[word] |= other._words[word];
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

    /// <summary>
    /// The first agent of the set at or after <paramref name="from"/> and below <paramref name="count"/>, or −1 — so a
    /// set is walked in order, <c>for (var a = set.Next(0, n); a >= 0; a = set.Next(a + 1, n))</c>, with nothing written.
    /// </summary>
    public int Next(int from, int count)
    {
        if (from >= count) return -1;

        var word = from >> 6;
        var bits = _words[word] & (ulong.MaxValue << (from & 63));
        var words = (count + 63) >> 6;
        while (true)
        {
            if (bits != 0)
            {
                var agent = (word << 6) + BitOperations.TrailingZeroCount(bits);
                return agent < count ? agent : -1;
            }

            if (++word >= words) return -1;

            bits = _words[word];
        }
    }

    /// <summary>The agents below <paramref name="count"/> in this set, in order, written into <paramref name="into"/>; how many.</summary>
    public int CopyTo(int count, Span<int> into) => Union(null, count, into);

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
