namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>OBS-2c — each thing a debug session can be opened for has a switch of its own, and no switch
/// turns on anything a second one owns.</b> Eight checkboxes.
/// </summary>
/// <remarks>
/// <para>
/// <b>OBS-2b — every one of them is off by default.</b> The overlay is instrumentation and is priced
/// on the same footing as what it measures, so a run that was not asked for one draws none of this
/// and pays for none of it.
/// </para>
/// <para>
/// <b>A layer covers one kind of body entirely</b> — its geometry and the manoeuvre it is in alike —
/// because the question a debug session asks is about the body and not about the kind of mark. What
/// belongs to the <em>town</em> rather than to a body is not switched with a body at all, which is
/// what <see cref="Nodes"/> and <see cref="Reservations"/> are for.
/// </para>
/// </remarks>
internal sealed class DebugSwitches
{
    /// <summary>Frame and tick cost, ranked by phase, with the body and agent counts.</summary>
    public bool FrameReadout;

    /// <summary>Everything about cars: their lines, what each was told, and where it must be stopped by.</summary>
    public bool CarLines;

    /// <summary>The same for walkers.</summary>
    public bool WalkerLines;

    /// <summary>
    /// <b>The town's own ground</b>: both networks' global nodes, the links between them and the
    /// movements a junction allows — where anything <em>could</em> go, which is what the router plans over.
    /// </summary>
    /// <remarks>
    /// It is the one layer that does not move once the town is laid, which is why it is the one that is
    /// cached rather than walked every frame.
    /// </remarks>
    public bool Nodes;

    /// <summary>
    /// <b>The two books of who has been granted which stretch of that ground</b>, as a block of the way
    /// each stretch is a stretch of.
    /// </summary>
    /// <remarks>
    /// A reservation is a fact about the <em>ground</em> and not about the body holding it — one body's
    /// stretch is what cuts another's, across both rosters and both networks — so it is switched with the
    /// town rather than with either kind of body (OBS-2c). Held under the car layer it could not show a
    /// walker standing in a lane without the car switch on, which is the reading it exists for. It is
    /// <em>not</em> switched with <see cref="Nodes"/> either: the graphs are the ground a town was laid
    /// with and the blocks are what the tick did to it this frame, and a junction's movements drawn under
    /// every block on them is the picture neither question wants.
    /// </remarks>
    public bool Reservations;

    /// <summary>Every body's collision shape — the one the solver holds, not the one it is drawn at.</summary>
    public bool Collision;

    /// <summary>The measuring tool, which takes the mouse for as long as it is ticked.</summary>
    public bool Ruler;

    /// <summary>
    /// What each shape of the proving ground is costing each drivetrain. <b>It shows on the proving ground
    /// and nowhere else</b>: every other map is a town, and a town has no shapes to name.
    /// </summary>
    /// <remarks>
    /// <b>The one switch that starts on</b>, and it is not really an exception: every other switch draws
    /// something over a town that is worth looking at without it, and this draws the only thing the proving
    /// ground is for. A rig whose read-out has to be found in a settings panel is a rig nobody reads.
    /// </remarks>
    public bool TrackFigures = true;

    /// <summary>Whether anything is drawn about the town's own graphs, which is what decides if they are laid at all.</summary>
    public bool NeedsNetworks => Nodes;

    /// <summary>
    /// A number that changes whenever a switch does. The town's own graphs are re-emitted on it
    /// rather than every tick — re-emitting them for the bodies' sake was the most expensive thing
    /// in the frame at a district framing, and this is the "or a switch does" half of the rule.
    /// </summary>
    public int Generation { get; private set; }

    public void Toggle(ref bool option)
    {
        option = !option;
        Generation++;
    }
}
