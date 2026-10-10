using System.Numerics;
using System.Runtime.InteropServices;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>What a watch over every body asks of each step</b>, handed to a device that sweeps the town itself
/// (<see cref="ITownDevice.WatchesTheTown"/>) — the watch's own figures, which the town neither holds nor reads.
/// </summary>
/// <param name="OverlapAllowanceM">How deep a body may be into anything before its step counts towards a run inside it.</param>
/// <param name="PastTheGrantAllowanceM">
/// How far a car may be past its grant, and go deeper past it in a step, before the step counts towards a run past it.
/// </param>
/// <param name="MovedM">How far a walker afoot has to get from where it last did before it counts as getting anywhere.</param>
internal readonly record struct TownWatchTerms(float OverlapAllowanceM, float PastTheGrantAllowanceM, float MovedM);

/// <summary>
/// <b>One step of the town as a watch over every body reads it</b> (<see cref="ITownDevice.Watched"/>): the deepest any body
/// is into anything and the longest run any has been deeper than the allowance, the furthest any car is past its grant and
/// the longest run any went on deeper past it — each with the lowest-numbered body that had it, walkers first and then
/// cars, or -1 — and the longest any walker afoot has got nowhere, and the metres the driven cars covered in the step.
/// </summary>
/// <remarks>Field for field with <c>app/devicetick/kernels/town_watch.glsl</c>'s <c>WatchedRow</c>.</remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TownWatchedTick(
    float DeepestOverlapM, int DeepestBody, int LongestStuckTicks, int StuckBody, float FurthestPastM, int FurthestPastBody,
    int LongestPastTicks, int PastBody, int LongestStillTicks, float DrivenM, uint Pad0 = 0u, uint Pad1 = 0u);

/// <summary>
/// <b>What a watch over every body has against one of them</b>, as the last step left it (<see cref="ITownDevice.WatchNotes"/>):
/// how deep it is into anything and its run deeper than the allowance, and how far past its grant and its run going deeper.
/// </summary>
/// <remarks>Field for field with <c>app/devicetick/kernels/town_watch.glsl</c>'s <c>WatchNoteRow</c>.</remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TownWatchNote(int Body, float OverlapM, int StuckTicks, float PastM, int PastTicks);

/// <summary>
/// <b>The runs every body has kept so far</b>, handed to a device that takes a watch over (<see cref="ITownDevice.WatchesTheTown"/>)
/// so its first step goes on from them — walkers first, then cars, as <see cref="TownWatchedTick"/> numbers them.
/// </summary>
/// <param name="StuckTicks">Every body's run of steps deeper into anything than the allowance.</param>
/// <param name="PastM">How far past its grant every body was, the last step.</param>
/// <param name="PastTicks">Every body's run of steps going deeper past its grant.</param>
/// <param name="StillFromM">Where every walker last got anywhere from.</param>
/// <param name="StillTicks">Every walker's run of steps afoot getting nowhere.</param>
internal readonly ref struct TownWatchRuns(
    ReadOnlySpan<int> StuckTicks, ReadOnlySpan<float> PastM, ReadOnlySpan<int> PastTicks, ReadOnlySpan<Vector2> StillFromM,
    ReadOnlySpan<int> StillTicks)
{
    public ReadOnlySpan<int> StuckTicks { get; } = StuckTicks;

    public ReadOnlySpan<float> PastM { get; } = PastM;

    public ReadOnlySpan<int> PastTicks { get; } = PastTicks;

    public ReadOnlySpan<Vector2> StillFromM { get; } = StillFromM;

    public ReadOnlySpan<int> StillTicks { get; } = StillTicks;

    /// <summary>How many walkers the runs cover, every one ahead of every car.</summary>
    public int Walkers => StillTicks.Length;

    /// <summary>How many cars the runs cover.</summary>
    public int Cars => StuckTicks.Length - StillTicks.Length;
}
