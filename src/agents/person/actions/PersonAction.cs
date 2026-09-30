namespace TrafficSimulation.Agents.Person.Actions;

/// <summary>
/// <b>What somebody on foot is doing</b> — one action at a time, entered and left explicitly (PER-25b). Each walks
/// only the ground it has claimed, and knows when it is done and what it hands over to.
/// </summary>
internal enum PersonAction : byte
{
    /// <summary>Walking nowhere: waiting, standing by, or stood at a door. Its body is all it holds.</summary>
    Stand,

    /// <summary>Walking the ways of its route, and the hop off the end of them onto its goal (PER-25, PER-26).</summary>
    Walk,

    /// <summary>Getting past somebody standing on its way over the lane beside (PER-28): asked for whole, then walked.</summary>
    Sidestep,

    /// <summary>Off the ground of the way it walks: straight back onto it, over what it claims of the ground between (PER-25).</summary>
    Rejoin,

    /// <summary>An officer on duty walking straight to where the closure puts them (SRV-11).</summary>
    Post,

    /// <summary>A hand on the keys (CTL-6): the hand walks it, and it holds nothing ahead.</summary>
    Hand,

    /// <summary>In a building or a car (PHY-7): no body in the world, nothing in anybody's way.</summary>
    Inside,

    /// <summary>A casualty (PER-18): down in the road until an ambulance has been.</summary>
    Down,
}
