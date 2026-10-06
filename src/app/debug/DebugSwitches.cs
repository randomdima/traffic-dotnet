namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>OBS-2c — each thing a debug session can be opened for has a switch of its own, and no switch
/// turns on anything a second one owns.</b> One a <see cref="DebugLayer"/>, and the ground's own layers
/// (<see cref="Ground"/>) are not among them.
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
/// what <see cref="Nodes"/> and <see cref="Claims"/> are for.
/// </para>
/// </remarks>
internal sealed class DebugSwitches
{
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
    /// <b>Every claim on that ground</b>, as a block of the way each stretch is a stretch of, whatever kind
    /// of ground that way is.
    /// </summary>
    /// <remarks>
    /// A claim is a fact about the <em>ground</em> and not about the body holding it — one body's
    /// stretch is what cuts another's, across both rosters and every kind of way — so it is switched with the
    /// town rather than with either kind of body (OBS-2c). Held under the car layer it could not show a
    /// walker standing in a lane without the car switch on, which is the reading it exists for. It is
    /// <em>not</em> switched with <see cref="Nodes"/> either: the graphs are the ground a town was laid
    /// with and the blocks are what the tick did to it this frame, and a junction's movements drawn under
    /// every block on them is the picture neither question wants.
    /// </remarks>
    public bool Claims;

    /// <summary>Every body's collision shape — the one the solver holds, not the one it is drawn at.</summary>
    public bool Collision;

    /// <summary>
    /// <b>The circle each car's steering says it is turning</b> (OBS-2j), against the one its tyres are
    /// actually turning. It is a layer of the car's own and not part of <see cref="CarLines"/>: that one
    /// draws what the world did to a driver, and this draws a prediction nothing in the town made.
    /// </summary>
    public bool TurnCircles;

    /// <summary>
    /// <b>The triangles the ground is drawn out of</b> (OBS-2o), as the edges each one has. It is the one
    /// switch here that is about the picture rather than about the town: everything else draws something
    /// the simulation produced, and this draws what the renderer was handed.
    /// </summary>
    public bool Wireframe;

    /// <summary>
    /// <b>The outside of the town's driven ground</b> (OBS-2p), as the boundary of the one shape the
    /// ribbons of every driven line merge into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the town's rather than a body's, like <see cref="Nodes"/>, and it is not switched with that
    /// one: the graphs say where anything may go, and this says where the ground they lay stops — a lane
    /// whose whole line is drawn under it either way.
    /// </para>
    /// <para>
    /// <b>The bands struck off that boundary are drawn under it too</b> (OBS-2u), the town's own and not an
    /// example: what they are read against is the line they were taken from, so a switch of their own would
    /// be a switch that draws half a reading.
    /// </para>
    /// </remarks>
    public bool Perimeter;

    /// <summary>
    /// <b>The ground those lines cover</b> (OBS-2s), as the ribbons the merge behind <see cref="Perimeter"/>
    /// is given.
    /// </summary>
    /// <remarks>
    /// A switch of its own and not part of that one, though the two are one shape: this draws the merge's
    /// input and that one its answer, and a reader with them on together is looking for the places they
    /// disagree. Turned on alone it is the area the town is driven over, which is a reading in itself.
    /// </remarks>
    public bool Ribbons;

    /// <summary>
    /// <b>The lattice the town's geometry is asked over</b> (OBS-2r), and how many lines each of its cells
    /// holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the town's rather than a body's, and it is the second switch here that is about the
    /// <em>machinery</em> rather than about the town — <see cref="Wireframe"/> draws what the renderer was
    /// handed and this draws what a geometry question is narrowed with. Neither is switched with anything
    /// the simulation produced, because neither is a thing the town does.
    /// </para>
    /// <para>
    /// <b>A lattice is read by asking it about one cell</b> (OBS-2t, <see cref="DebugPick"/>), so while it is
    /// ticked the cell under the pointer is what the inspector finds where nothing drawn over it is.
    /// </para>
    /// </remarks>
    public bool Grid;

    /// <summary>
    /// <b>And the lattice the solver's own bodies are binned into</b> (OBS-2x), as the cells of its two
    /// broad-phase grids and how many bodies each holds.
    /// </summary>
    /// <remarks>
    /// A switch of its own and not part of <see cref="Grid"/>, though both draw a lattice: that one is the
    /// index a question about which line is where is narrowed with, and this is the index a question about
    /// what could hit what is narrowed with. Two indexes, laid from different corners over different
    /// things, so one switch drawing both would be a switch that answers two questions at once (OBS-2c).
    /// </remarks>
    public bool SolverGrid;

    /// <summary>The measuring tool, which takes the mouse for as long as it is ticked.</summary>
    public bool Ruler;

    /// <summary>
    /// <b>What the map is zoned for</b> (OBS-2z): every zone of its map washed by its kind and outlined, and the one under
    /// the pointer told — not cached, the wash being as fine as the view.
    /// </summary>
    public bool Zones;

    /// <summary>
    /// <b>The shape the reader strikes off that boundary for themselves</b> (OBS-2w,
    /// <see cref="ShellProbe"/>), at a distance a slider beside it turns. It is a layer of its own and not
    /// part of <see cref="Perimeter"/>: that one draws the town's own layers and this draws one the town
    /// was not laid with, and a shape nobody stands on drawn under that switch would be read for one.
    /// </summary>
    public ShellProbe Shell { get; } = new();

    /// <summary>
    /// <b>And the ground's own layers, which are not layers of this overlay at all</b>
    /// (<see cref="GroundSwitches"/>, OBS-2v): they take the town apart rather than drawing anything over
    /// it, so they start on and are not counted among the checkboxes above.
    /// </summary>
    /// <remarks>
    /// They are held here because the page that draws a switch reads its state from this slice
    /// (<see cref="DebugSwitches"/>'s own docs) and a second object threaded through the interface
    /// alongside this one would be two answers to "what has the reader turned on".
    /// </remarks>
    public GroundSwitches Ground { get; } = new();

    /// <summary>
    /// Whether anything the town holds still is drawn at all, which is what decides whether the cache
    /// behind those layers is laid. Both of them are geometry that does not move once the town is laid.
    /// <b><see cref="SolverGrid"/> is among them for its static half alone</b>: the furniture is indexed
    /// once and cached with the rest, and the moving half of that layer is laid every frame like a claim.
    /// </summary>
    public bool NeedsTownGeometry =>
        Nodes || Perimeter || Ribbons || Wireframe || Grid || SolverGrid || Shell.Drawn;

    /// <summary>
    /// A number that changes whenever a switch does. The town's own graphs are re-emitted on it
    /// rather than every tick — re-emitting them for the bodies' sake was the most expensive thing
    /// in the frame at a district framing, and this is the "or a switch does" half of the rule.
    /// </summary>
    /// <remarks>
    /// <b>The ground's own parts count among them</b> (<see cref="Ground"/>): the wireframe is a picture
    /// of what is being drawn, so a part taken out of the ground stales that cache the way a layer
    /// switched on does. Carried here rather than compared beside this, a caller can hold one number.
    /// <b>And the probe's distance counts too</b> (<see cref="Shell"/>), which is the same statement about
    /// a figure that changes what is drawn rather than whether it is.
    /// </remarks>
    public int Generation => _generation + Ground.Generation + Shell.Generation;

    int _generation;

    public void Toggle(ref bool option)
    {
        option = !option;
        _generation++;
    }

    public void Toggle(DebugLayer layer) => Toggle(ref this[layer]);

    /// <summary>
    /// The switch a layer is. <b>One place, so a row drawn, a word typed and a layer thrown cannot come apart</b>
    /// — they were two switch statements once, and a layer inserted in the middle of the list toggled its
    /// neighbour.
    /// </summary>
    public ref bool this[DebugLayer layer]
    {
        get
        {
            switch (layer)
            {
                case DebugLayer.CarLines: return ref CarLines;
                case DebugLayer.TurnCircles: return ref TurnCircles;
                case DebugLayer.WalkerLines: return ref WalkerLines;
                case DebugLayer.Nodes: return ref Nodes;
                case DebugLayer.Claims: return ref Claims;
                case DebugLayer.Collision: return ref Collision;
                case DebugLayer.SolverGrid: return ref SolverGrid;
                case DebugLayer.Perimeter: return ref Perimeter;
                case DebugLayer.Ribbons: return ref Ribbons;
                case DebugLayer.Grid: return ref Grid;
                case DebugLayer.Shell: return ref Shell.Drawn;
                case DebugLayer.Wireframe: return ref Wireframe;
                case DebugLayer.Zones: return ref Zones;
                default: return ref Ruler;
            }
        }
    }

    /// <summary>Whether any layer drawn over the town is on. The ground's own layers are not among them (OBS-2v).</summary>
    public bool AnyOn
    {
        get
        {
            foreach (var entry in DebugLayers.All)
            {
                if (this[entry.Layer]) return true;
            }

            return false;
        }
    }

    /// <summary>Every layer drawn over the town off at once, which is the town back as it is seen without them.</summary>
    public void AllOff()
    {
        foreach (var entry in DebugLayers.All) this[entry.Layer] = false;

        _generation++;
    }
}
