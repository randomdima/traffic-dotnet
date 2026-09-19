using System.Collections.Concurrent;

namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// <b>A parallel pass over a count, handed out in chunks rather than one item at a time.</b> Every
/// build-time pass in this engine has the same shape — a working set per thread, an answer per item, and
/// an item small enough that the loop's own bookkeeping is not small beside it — and this is where that
/// shape is written.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because <see cref="Parallel.For(int, int, Action{int})"/> charges per iteration, and a town has
/// hundreds of thousands of them.</b> Each iteration costs a delegate call, a range-worker step, a GC poll
/// and an <see cref="Environment.TickCount64"/> read for the loop's timeout — measured at 1171 ms of CPU in
/// the ground stage of a shipped city, against roughly 2.6 s of real work beside it. A chunk pays that once
/// for a few thousand items.
/// </para>
/// <para>
/// <b>Chunks are many and short rather than one a thread</b>: a merge's ribbons differ in cost by orders of
/// magnitude, and a static split leaves one thread holding the expensive one while the rest have finished.
/// Sixteen chunks a processor is short enough that the tail is a sixteenth of a thread's share and long
/// enough that the bookkeeping has disappeared.
/// </para>
/// <para>
/// <b>It promises nothing about the order the items are taken in</b>, so a caller writes into a slot of its
/// own per item and never appends to something shared. That is the same bargain every
/// <see cref="Parallel"/> pass here already keeps.
/// </para>
/// </remarks>
internal static class InChunks
{
    const int ChunksPerProcessor = 16;

    /// <param name="working">A fresh working set, called once per thread the loop actually uses.</param>
    /// <param name="pass">One item, with the working set of the thread it is running on.</param>
    /// <param name="spent">
    /// Each thread's working set once it has no more chunks — for a pass that <em>holds</em> what it found
    /// rather than filing it, because filing it would be several threads writing one list.
    /// </param>
    public static void Over<TWorking>(
        int count, Func<TWorking> working, Action<TWorking, int> pass, Action<TWorking>? spent = null)
    {
        if (count <= 0) return;

        var chunk = Math.Max(1, count / (Environment.ProcessorCount * ChunksPerProcessor));
        Parallel.ForEach(
            Partitioner.Create(0, count, chunk),
            working,
            (range, _, own) =>
            {
                for (var at = range.Item1; at < range.Item2; at++) pass(own, at);

                return own;
            },
            spent ?? (_ => { }));
    }
}
