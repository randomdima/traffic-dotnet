namespace TrafficSimulation.Runtime;

/// <summary>
/// How a finished frame is meant to reach the glass. <b>A want and not a promise</b>: what a driver or
/// a browser actually does with it is the machine's business, and the fallback is always the one every
/// device has.
/// </summary>
/// <remarks>
/// It is named here rather than in the graphics API's own terms because both machines answer to it —
/// the desktop maps it onto a Vulkan present mode, and the browser has only the pacing the compositor
/// gives it, which is <see cref="Fifo"/> whatever is asked for.
/// </remarks>
internal enum Pacing
{
    /// <summary>Every frame shown, none dropped, the rate the display refreshes at. The one mode every device has.</summary>
    Fifo,

    /// <summary>Draw as fast as the town allows and show the newest finished frame, tearing nothing.</summary>
    Mailbox,

    /// <summary>Show it the moment it is done, tearing included. What a frame time is measured under.</summary>
    Immediate,
}
