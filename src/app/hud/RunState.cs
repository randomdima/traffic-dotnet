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
/// <see cref="Shared.Config.SimConfig"/>'s, not this class's.
/// </para>
/// </remarks>
internal sealed class RunState
{
    float _paceBeforeFreeze = 1f;

    /// <summary>The pace as a multiple of real time. Zero while frozen.</summary>
    public float TimeScale { get; private set; } = 1f;

    public bool Frozen => TimeScale <= 0f;

    /// <summary>The agents are not asked to decide, and the hand-driven one still is.</summary>
    public bool AgentsHeld { get; set; }

    public void SetPace(float scale)
    {
        _paceBeforeFreeze = scale;
        TimeScale = scale;
    }

    public void ToggleFreeze() => TimeScale = Frozen ? _paceBeforeFreeze : 0f;
}
