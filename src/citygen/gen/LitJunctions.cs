using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Which junctions carry lights</b> (TLT-3): a share of those that can, drawn once from the world seed and
/// weighted towards the bigger ones — and where in its cycle each lit one starts.
/// </summary>
/// <remarks>
/// <para>
/// <b>What can carry lights</b> is a junction of <see cref="ArmsLeast"/> arms or more that is not on a
/// roundabout's ring: fewer arms admit no movements to conflict, and a ring's circulating traffic is driven
/// over what is entering by the ranking alone (GEN-19).
/// </para>
/// <para>
/// <b>How many is the brief's</b> (<see cref="TownBrief.UnregulatedJunctionShare"/>), and exactly that many are
/// lit rather than each junction tossed for, so a small town reads as the share it asked for too. <b>Which
/// ones is weighted by the movements each admits</b> — every arm to every other — so a crossroads is lit more
/// often than a tee without any tee being out of the draw. It is a weighted draw without replacement
/// (Efraimidis–Spirakis): every junction draws a key, a uniform draw raised to the inverse of its weight, and
/// the highest keys are lit.
/// </para>
/// </remarks>
internal static class LitJunctions
{
    /// <summary>The fewest arms a junction admitting conflicting movements has (TLT-3).</summary>
    public const int ArmsLeast = 3;

    /// <param name="Lit">Whether each junction carries lights.</param>
    /// <param name="PhaseOffsetS">Where in its cycle each lit junction's clock starts; nought where unlit.</param>
    internal readonly record struct Drawn(bool[] Lit, float[] PhaseOffsetS);

    public static Drawn Draw(TownLayout layout, TownBrief brief, SimConfig config, ref Rng draw)
    {
        var count = layout.NodeM.Count;
        var lit = new bool[count];
        var offsetS = new float[count];
        var arms = layout.Arms();
        var barred = Unlightable(layout);

        var candidates = new List<int>();
        var keys = new List<float>();
        for (var junction = 0; junction < count; junction++)
        {
            if (arms[junction] < ArmsLeast || barred[junction]) continue;

            // The logarithm of u^(1/w), which orders the keys the same way and does not underflow for a heavy
            // junction; one minus the draw keeps u off nought.
            candidates.Add(junction);
            keys.Add(MathF.Log(1f - draw.NextFloat()) / Movements(arms[junction]));
        }

        var order = candidates.ToArray();
        Array.Sort(keys.ToArray(), order);

        var lighting = (int)MathF.Round(order.Length * (1f - brief.UnregulatedJunctionShare));
        for (var at = order.Length - lighting; at < order.Length; at++) lit[order[at]] = true;

        for (var junction = 0; junction < count; junction++)
        {
            if (lit[junction]) offsetS[junction] = draw.NextFloat(0f, config.Signals.CycleS);
        }

        return new Drawn(lit, offsetS);
    }

    /// <summary>How many movements a junction of this many arms admits: every arm to every other.</summary>
    static int Movements(int arms) => arms * (arms - 1);

    /// <summary>The nodes a roundabout's ring stands on. A car park's bays stand on nodes nothing else meets at.</summary>
    static bool[] Unlightable(TownLayout layout)
    {
        var barred = new bool[layout.NodeM.Count];
        foreach (var edge in layout.Edges)
        {
            if (edge.Class != RoadClass.Roundabout) continue;

            barred[edge.From] = true;
            barred[edge.To] = true;
        }

        return barred;
    }
}
