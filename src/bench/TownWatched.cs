namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What a watch over every body asks of each step</b> — the watch's own figures, which the town neither holds nor reads.
/// </summary>
/// <param name="OverlapAllowanceM">How deep a body may be into anything before its step counts towards a run inside it.</param>
/// <param name="PastTheGrantAllowanceM">
/// How far a car may be past its grant, and go deeper past it in a step, before the step counts towards a run past it.
/// </param>
/// <param name="MovedM">How far a walker afoot has to get from where it last did before it counts as getting anywhere.</param>
internal readonly record struct TownWatchTerms(float OverlapAllowanceM, float PastTheGrantAllowanceM, float MovedM);

/// <summary>
/// <b>One step of the town as a watch over every body reads it</b>: the deepest any body
/// is into anything and the longest run any has been deeper than the allowance, the furthest any car is past its grant and
/// the longest run any went on deeper past it — each with the lowest-numbered body that had it, walkers first and then
/// cars, or -1 — and the longest any walker afoot has got nowhere, and the metres the driven cars covered in the step.
/// </summary>
internal readonly record struct TownWatchedTick(
    float DeepestOverlapM, int DeepestBody, int LongestStuckTicks, int StuckBody, float FurthestPastM, int FurthestPastBody,
    int LongestPastTicks, int PastBody, int LongestStillTicks, float DrivenM);

/// <summary>
/// <b>What a watch over every body has against one of them</b>, as the last step left it:
/// how deep it is into anything and its run deeper than the allowance, and how far past its grant and its run going deeper.
/// </summary>
internal readonly record struct TownWatchNote(int Body, float OverlapM, int StuckTicks, float PastM, int PastTicks);
