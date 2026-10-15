using System.Buffers;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>The value at a rank of a set, exactly as the stable sort <c>Order()</c> places it</b>, found by selection rather
/// than by sorting the whole set.
/// </summary>
/// <remarks>
/// Values the sort holds equal but which differ — −0 and +0, NaNs of two payloads — are placed in their given order,
/// as the stable sort places them, so the answer is its value bit for bit: a −0 is written <c>-0</c>.
/// </remarks>
internal static class Ranked
{
    /// <summary>The value <paramref name="rank"/> places from the least, counting from 0; <paramref name="values"/> is left as given.</summary>
    public static double At(double[] values, int rank)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)rank, (uint)values.Length, nameof(rank));
        var scratch = ArrayPool<double>.Shared.Rent(values.Length);
        try
        {
            var span = scratch.AsSpan(0, values.Length);
            values.CopyTo(span);
            var found = Select(span, rank);

            // Two doubles that compare equal are one value, bit for bit, unless they are zeros or NaNs.
            if (found != 0 && !double.IsNaN(found)) return found;

            var place = 0;
            foreach (var value in values)
            {
                if (value.CompareTo(found) < 0) place++;
            }

            foreach (var value in values)
            {
                if (value.CompareTo(found) != 0) continue;
                if (place == rank) return value;

                place++;
            }

            throw new InvalidOperationException("a selected value that is not among the values");
        }
        finally
        {
            ArrayPool<double>.Shared.Return(scratch);
        }
    }

    /// <summary>Hoare's selection, ordered as <see cref="double.CompareTo(double)"/> orders: NaN least, −0 equal to +0.</summary>
    static double Select(Span<double> values, int rank)
    {
        var (low, high) = (0, values.Length - 1);
        while (low < high)
        {
            var pivot = values[low + ((high - low) / 2)];
            var (i, j) = (low, high);
            while (i <= j)
            {
                while (values[i].CompareTo(pivot) < 0) i++;
                while (values[j].CompareTo(pivot) > 0) j--;
                if (i > j) break;

                (values[i], values[j]) = (values[j], values[i]);
                (i, j) = (i + 1, j - 1);
            }

            if (rank <= j) high = j;
            else if (rank >= i) low = i;
            else break;
        }

        return values[rank];
    }
}
