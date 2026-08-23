using System.Numerics;

namespace TrafficSimulation.World.Town;

/// <summary>What kind of unit the interface has picked out, if any.</summary>
internal enum SelectionKind : byte
{
    None,
    Person,
    Car,
}

/// <summary>
/// CTL-1: the one unit the interface is talking about. A kind and an index into that fleet's roster,
/// and nothing else — a selection that held a reference would outlive the town it was taken on.
/// </summary>
internal readonly record struct Selection(SelectionKind Kind, int Index)
{
    public static Selection Of(SelectionKind kind, int index) => index >= 0 ? new Selection(kind, index) : default;

    public bool Any => Kind != SelectionKind.None && Index >= 0;
}

/// <summary>
/// <b>CTL-5: the hand at the wheel, as one tick's worth of what is being held down.</b> The same
/// record serves both agent kinds, because what a hand does is ask for motion and the two bodies
/// differ in how they answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is pushed through the seam every tick</b> (CTL-6), so the agent loop cannot tell a
/// hand-driven agent from any other. Nothing here drives a body behind its driver's back: what a hand
/// produces is the same command a follower produces, and everything under it — the tyre model, the
/// speed caps, the turning circle, terrain, collisions and damage — is untouched.
/// </para>
/// <para>
/// <b>Nothing held is not the same as no hand.</b> Releasing the keys coasts; it does not hand the
/// unit back (CTL-5b), so <see cref="Held"/> stays true while the wheel is taken and the unit simply
/// asks for nothing.
/// </para>
/// </remarks>
internal readonly record struct HandInput(bool Held, float Throttle, float Steer, bool Handbrake, Vector2 WalkDirection)
{
    /// <summary>Nobody at the wheel: the unit decides for itself again.</summary>
    public static HandInput None => default;
}
