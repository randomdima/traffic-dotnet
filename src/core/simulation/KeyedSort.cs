using System.Numerics;

namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// <b>Rows sorted by a 64-bit key, each row moved with its key</b> — what a build-time pass sorts by, written here rather
/// than taken from the framework's <c>Span&lt;TKey&gt;.Sort(Span&lt;TValue&gt;)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because a page's runtime interprets the framework's</b>: the framework makes its sort helper over a pair of value
/// types by reflection, so the ahead-of-time compiler never sees it, and on a traced city the interpreter running it would be
/// a seventh of a page's open (web log, 2026-10-15). A method of this assembly called with the caller's own types is
/// compiled for them.
/// </para>
/// <para>Not stable: rows of equal keys come out in an order of its own, as the framework's do.</para>
/// </remarks>
internal static class KeyedSort
{
    /// <summary>Below this many rows a run is sorted by insertion.</summary>
    const int InsertionBelow = 16;

    public static void Sort<T>(Span<long> keys, Span<T> rows)
    {
        if (keys.Length != rows.Length) throw new ArgumentException("A key a row.", nameof(rows));
        if (keys.Length < 2) return;

        Intro(keys, rows, 2 * (BitOperations.Log2((uint)keys.Length) + 1));
    }

    /// <summary>
    /// <b>Keys alone, sorted in place</b> — the short runs a build-time pass sorts a great many of, which is what this is
    /// for: a run below <see cref="InsertionBelow"/> by insertion, and a longer one as a heap.
    /// </summary>
    public static void Sort<TKey>(Span<TKey> keys) where TKey : struct, IComparable<TKey>
    {
        if (keys.Length < InsertionBelow)
        {
            for (var at = 1; at < keys.Length; at++)
            {
                var key = keys[at];
                var before = at - 1;
                while (before >= 0 && keys[before].CompareTo(key) > 0)
                {
                    keys[before + 1] = keys[before];
                    before--;
                }

                keys[before + 1] = key;
            }

            return;
        }

        var count = keys.Length;
        for (var at = count >> 1; at >= 1; at--) Sift(keys, at, count);

        for (var at = count; at > 1; at--)
        {
            (keys[0], keys[at - 1]) = (keys[at - 1], keys[0]);
            Sift(keys, 1, at - 1);
        }
    }

    static void Sift<TKey>(Span<TKey> keys, int at, int count) where TKey : struct, IComparable<TKey>
    {
        var key = keys[at - 1];
        while (at <= count >> 1)
        {
            var child = 2 * at;
            if (child < count && keys[child - 1].CompareTo(keys[child]) < 0) child++;
            if (key.CompareTo(keys[child - 1]) >= 0) break;

            keys[at - 1] = keys[child - 1];
            at = child;
        }

        keys[at - 1] = key;
    }

    /// <summary>Quicksort on the smaller side and a loop on the larger, until the depth runs out and a heap takes over.</summary>
    static void Intro<T>(Span<long> keys, Span<T> rows, int depth)
    {
        while (keys.Length > InsertionBelow)
        {
            if (depth-- == 0)
            {
                Heap(keys, rows);
                return;
            }

            var pivot = Partition(keys, rows);
            if (pivot < keys.Length - pivot)
            {
                Intro(keys[..pivot], rows[..pivot], depth);
                keys = keys[(pivot + 1)..];
                rows = rows[(pivot + 1)..];
            }
            else
            {
                Intro(keys[(pivot + 1)..], rows[(pivot + 1)..], depth);
                keys = keys[..pivot];
                rows = rows[..pivot];
            }
        }

        Insertion(keys, rows);
    }

    /// <summary>
    /// The run split about the median of its first, middle and last keys, answering where that key now stands. The
    /// three put in order first are what stop each scan running off the run's ends.
    /// </summary>
    static int Partition<T>(Span<long> keys, Span<T> rows)
    {
        var last = keys.Length - 1;
        var middle = last >> 1;
        InOrder(keys, rows, 0, middle);
        InOrder(keys, rows, 0, last);
        InOrder(keys, rows, middle, last);

        var pivotAt = last - 1;
        Swap(keys, rows, middle, pivotAt);
        var pivot = keys[pivotAt];
        var left = 0;
        var right = pivotAt;
        while (true)
        {
            while (keys[++left] < pivot)
            {
            }

            while (pivot < keys[--right])
            {
            }

            if (left >= right) break;

            Swap(keys, rows, left, right);
        }

        if (left != pivotAt) Swap(keys, rows, left, pivotAt);
        return left;
    }

    static void Heap<T>(Span<long> keys, Span<T> rows)
    {
        var count = keys.Length;
        for (var at = count >> 1; at >= 1; at--) Sift(keys, rows, at, count);

        for (var at = count; at > 1; at--)
        {
            Swap(keys, rows, 0, at - 1);
            Sift(keys, rows, 1, at - 1);
        }
    }

    /// <summary>One row moved down the heap of <paramref name="count"/> to where it belongs, counted from one.</summary>
    static void Sift<T>(Span<long> keys, Span<T> rows, int at, int count)
    {
        var key = keys[at - 1];
        var row = rows[at - 1];
        while (at <= count >> 1)
        {
            var child = 2 * at;
            if (child < count && keys[child - 1] < keys[child]) child++;
            if (key >= keys[child - 1]) break;

            keys[at - 1] = keys[child - 1];
            rows[at - 1] = rows[child - 1];
            at = child;
        }

        keys[at - 1] = key;
        rows[at - 1] = row;
    }

    static void Insertion<T>(Span<long> keys, Span<T> rows)
    {
        for (var at = 1; at < keys.Length; at++)
        {
            var key = keys[at];
            var row = rows[at];
            var before = at - 1;
            while (before >= 0 && keys[before] > key)
            {
                keys[before + 1] = keys[before];
                rows[before + 1] = rows[before];
                before--;
            }

            keys[before + 1] = key;
            rows[before + 1] = row;
        }
    }

    static void InOrder<T>(Span<long> keys, Span<T> rows, int one, int other)
    {
        if (keys[one] > keys[other]) Swap(keys, rows, one, other);
    }

    static void Swap<T>(Span<long> keys, Span<T> rows, int one, int other)
    {
        (keys[one], keys[other]) = (keys[other], keys[one]);
        (rows[one], rows[other]) = (rows[other], rows[one]);
    }
}
