namespace TrafficSimulation.Agents.Person.Control;

/// <summary>
/// <b>A walker getting past somebody standing on its way</b> (PER-28): a step straight across onto the lane beside,
/// a walk down it until it is clear of them, and a step straight back — measured down its route from where it
/// stood when it asked.
/// </summary>
/// <remarks>
/// <para>
/// <b>Straight across and straight back</b>, because a walker turns where it stands (PER-3): the shortest a pass
/// can be is the lane beside for exactly as long as the one passed is in the way, and no step of it angled along
/// the route.
/// </para>
/// <para>
/// <b>Down the route and not across the town</b>, so a pass runs round a corner as it runs along a street: a
/// walker's route is a chain of ways it takes one after another, and how far down that chain it has got is the
/// one measure that carries on from one way to the next.
/// </para>
/// <para>
/// <b>Asked for, then begun</b>, as a car's pass is (TER-4c.6): laid a rebuild before the walker steps over, so
/// that two asked for over the same ground on one tick are settled before either leaves its way.
/// </para>
/// </remarks>
/// <param name="Slot">Which way of its route the walker was on when it asked.</param>
/// <param name="FromM">And how far along that way it stood — where the pass is measured from.</param>
/// <param name="ClearM">How far down the route its middle is clear of what it passes by the gap it keeps, and it steps back.</param>
/// <param name="AsideM">How far across the lane beside stands, along the walker's right; nought for no pass.</param>
/// <param name="Begun">Whether the pass has been kept past the rebuild it was laid in, and the walker is stepping over.</param>
internal readonly record struct Sidestep(int Slot, float FromM, float ClearM, float AsideM, bool Begun)
{
    public static Sidestep None => default;

    public bool Any => AsideM != 0f;

    /// <summary>
    /// <b>Which leg of the pass a walker is on</b>, from how far down its route it has come and how far across it
    /// stands: across until it is on the lane beside, along it until it is clear, back until it is on its route.
    /// </summary>
    /// <param name="alongWithinM">How near the place it is clear a walker is at it — a tick's walk, since it is walked to exactly.</param>
    /// <param name="acrossWithinM">
    /// And how near a line it is on it — its own radius, since the solver shoves a body about by more than a
    /// tick's walk, and one shoved off the line it had reached would be sent straight back across.
    /// </param>
    public SidestepLeg LegAt(float walkedM, float acrossM, float alongWithinM, float acrossWithinM)
    {
        if (walkedM >= ClearM - alongWithinM) return MathF.Abs(acrossM) <= acrossWithinM ? SidestepLeg.Over : SidestepLeg.Back;

        return MathF.Abs(AsideM - acrossM) <= acrossWithinM ? SidestepLeg.Along : SidestepLeg.Across;
    }
}

/// <summary>The legs of a <see cref="Sidestep"/>, in the order they are walked.</summary>
internal enum SidestepLeg : byte
{
    /// <summary>Straight across onto the lane beside, from where it stands.</summary>
    Across,

    /// <summary>Down the lane beside until it is clear of what it passes.</summary>
    Along,

    /// <summary>Straight back onto its route.</summary>
    Back,

    /// <summary>Back on its route past what it passed, and the pass done.</summary>
    Over,
}
