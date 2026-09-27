using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

internal sealed partial class GroundShapes
{
    /// <summary>
    /// <b>The driven ground</b>: inside the town's own boundary (<see cref="GroundRings.Carriageway"/>), kerb to
    /// kerb and rounded at the one radius — the rings the carriageway is filled from, and nothing else.
    /// </summary>
    /// <remarks>
    /// <b>Never the lanes.</b> A lane says where a car is meant to drive, which is the drivers' business; the
    /// ground says what a wheel stands on, and that is the tarmac as it is laid — a junction's corner paved
    /// back to its arc included (TER-3c.7, TER-5), which no movement sweeps and every wheel cutting the corner
    /// is on.
    /// </remarks>
    RingSides _carriageway = RingSides.None;

    /// <summary>
    /// <b>And the walk</b>: inside that boundary moved out by <see cref="SimConfig.WalkOuterM"/>
    /// (<see cref="GroundRings.Walk"/>). It encloses the carriageway as the layer drawn under it does, so it is
    /// asked after the carriageway and never instead of it (TER-7b).
    /// </summary>
    RingSides _walk = RingSides.None;

    /// <summary>
    /// The two layers struck off the town's boundary, laid over the lattice that answers which side of them a
    /// point stands on. <b>A town with no driven line has no boundary</b> — the water, asked about before a
    /// road is laid — and nothing is struck for it.
    /// </summary>
    /// <remarks>
    /// <b>The lattice is an index and not a raster</b> (TER-7): it says which pieces of a ring to ask and never
    /// what the ground is, so every answer is the rings' own, exactly.
    /// </remarks>
    void LayTheShell(Paving paving, SimConfig config)
    {
        if (paving.DrivenCount == 0) return;

        var rings = paving.Rings(config);
        _carriageway = RingSides.Of(rings.Carriageway.Rings, config.ShellLevel);
        _walk = RingSides.Of(rings.Walk.Rings, config.ShellLevel);
    }

    /// <summary>The lattice the carriageway is answered off, for a census of what it costs.</summary>
    public RingSides CarriagewaySides => _carriageway;

    /// <summary>The lattice the walk is answered off.</summary>
    public RingSides WalkSides => _walk;
}
