namespace TrafficSimulation.App.Hud;

/// <summary>
/// The run keys as state: what the pace is, whether time is frozen, and whether the agents are being
/// held while their bodies keep stepping.
/// </summary>
/// <remarks>
/// <para>
/// <b>Freeze and hold are two different things and neither is the other.</b> Freeze takes the time
/// scale to zero, so nothing decides, steps, collides or ages. Hold skips the decide loop while the
/// bodies keep stepping — physics, contacts and damage run on — and <b>nothing is unwound</b>: routes,
/// trips and states survive, and no stuck timeout runs up while the town stands still.
/// </para>
/// <para>
/// The pace cap is kept: a time scale that stretches the physics delta integrates the whole simulation
/// more coarsely and manufactures collisions the model never had. The figure is
/// <see cref="Core.Config.SimConfig"/>'s, not this class's.
/// </para>
/// </remarks>
internal sealed class RunState
{
    float _pace = 1f;
    float _paceBeforeFreeze = 1f;

    /// <summary>The pace as a multiple of real time. Zero while frozen, and zero while the town waits.</summary>
    public float TimeScale => WaitingForADriver ? 0f : _pace;

    public bool Frozen => TimeScale <= 0f;

    /// <summary>
    /// <b>DRV-8: the town is standing still because a driver who is not at the keyboard is thinking.</b>
    /// It is a freeze the run asked for rather than one a key pressed, so it leaves the pace where it was
    /// and gives it back the moment that driver says something.
    /// </summary>
    /// <remarks>
    /// <b>Why time and not the agents</b> (see the pair above): a town that went on driving while a bot
    /// decided would charge it for thinking, and the seconds a step asks for are seconds of the town.
    /// Holding the agents instead would stop the traffic and let the bot's own car carry on rolling, which
    /// is the opposite of what is wanted.
    /// </remarks>
    public bool WaitingForADriver { get; set; }

    /// <summary>The agents are not asked to decide, and the hand-driven one still is.</summary>
    /// <remarks>
    /// <b>Held from the moment a town opens, temporarily</b>: what is being looked at while the lane layer
    /// is rebuilt is the ground, and a town that drives itself away from the frame is a town being read
    /// through moving cars. The <c>Pause</c> key still lets go of it, and the default goes back with the
    /// rest of what the rework put down ([known gaps](../../../docs/index.md#known-gaps)).
    /// </remarks>
    public bool AgentsHeld { get; set; } = true;

    public void SetPace(float scale)
    {
        _paceBeforeFreeze = scale;
        _pace = scale;
    }

    public void ToggleFreeze() => _pace = _pace <= 0f ? _paceBeforeFreeze : 0f;
}
