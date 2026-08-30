using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Terrain;

/// <summary>
/// What is answered about a point: which ground it stands on, who is permitted there, what the
/// surface is worth, and which way the lane under it runs (TER-7's classifier half).
/// </summary>
internal readonly record struct GroundSample(Ground Ground, GroundRules Rules, float Coefficient, Vector2 LaneDirection)
{
    public bool Walkable => (Rules & GroundRules.Walkable) != 0;

    public bool Drivable => (Rules & GroundRules.Drivable) != 0;

    public bool Preferred => (Rules & GroundRules.Preferred) != 0;

    public bool Directional => (Rules & GroundRules.Directional) != 0;
}

/// <summary>
/// What one surface does to a body travelling over it: grip, drag and the mark threshold, asked for
/// together because a wheel wants all three about the ground under that one patch.
/// </summary>
internal readonly record struct GroundEffect(float Coefficient, float DragMps2, float MarkFactor, bool Ploughs);

/// <summary>
/// The cell grid as a <b>classifier</b> over the plan's shapes, agreeing with them to within half a
/// cell. It answers <em>may this body stand here</em> and <em>is this a road</em>; it is never what
/// gets drawn, because no arrangement of metre squares is a kerb running at 40°.
/// </summary>
/// <remarks>
/// Continuous position in, no snapping out: the query takes metres and floors them to a cell, and
/// nothing is handed back a snapped position. A point outside the town's own box is answered rather
/// than refused — the nearest cell's ground — because anything can be pushed anywhere and a tick has
/// nowhere to put an exception.
/// </remarks>
internal sealed class TerrainGrid
{
    readonly CityPlan _plan;
    GroundCatalog _catalog;

    public TerrainGrid(CityPlan plan, SimConfig config)
    {
        _plan = plan;
        _catalog = new GroundCatalog(config);
    }

    /// <summary>
    /// What each ground is worth, read again from the figures as they stand now. <b>The grid itself does
    /// not move</b> — the cells are the plan's and a figure cannot relay a town — so this is the whole of
    /// what a changed figure does to the ground under a wheel.
    /// </summary>
    public void FiguresChanged(SimConfig config) => _catalog = new GroundCatalog(config);

    public float CellSizeM => _plan.CellSizeM;

    public GroundSample At(Vector2 pointM)
    {
        var cell = CellIndexAt(pointM);
        var ground = _plan.Cells[cell];
        return new GroundSample(ground, GroundCatalog.RulesOf(ground), _catalog.Coefficient(ground), LaneDirectionOfCell(cell));
    }

    public Ground GroundAt(Vector2 pointM) => _plan.Cells[CellIndexAt(pointM)];

    /// <summary>
    /// The three physics figures under one point. Kept apart from <see cref="At"/> because the two
    /// have different callers: what a wheel wants is what this ground does to it, and what a walker
    /// and the planners want is who is permitted here and which way the lane runs.
    /// </summary>
    public GroundEffect EffectAt(Vector2 pointM) => _catalog.EffectOf(_plan.Cells[CellIndexAt(pointM)]);

    /// <summary>Whether the point is inside the grid at all, for a caller that wants to know before it asks.</summary>
    public bool Contains(Vector2 pointM) =>
        pointM.X >= 0f && pointM.Y >= 0f &&
        pointM.X < _plan.GridWidth * _plan.CellSizeM && pointM.Y < _plan.GridHeight * _plan.CellSizeM;

    public int CellIndexAt(Vector2 pointM)
    {
        var x = Math.Clamp((int)MathF.Floor(pointM.X / _plan.CellSizeM), 0, _plan.GridWidth - 1);
        var y = Math.Clamp((int)MathF.Floor(pointM.Y / _plan.CellSizeM), 0, _plan.GridHeight - 1);
        return y * _plan.GridWidth + x;
    }

    /// <summary>The middle of a cell, which is the point the classifier's own agreement is measured at.</summary>
    public Vector2 CellCentreM(int cell)
    {
        var x = cell % _plan.GridWidth;
        var y = cell / _plan.GridWidth;
        return new Vector2((x + 0.5f) * _plan.CellSizeM, (y + 0.5f) * _plan.CellSizeM);
    }

    /// <summary>
    /// The lane's direction, dequantised from the byte pair the plan holds. It is left unnormalised:
    /// the quantisation is to 1/127 of a unit vector, so it is one already to within the quantiser,
    /// and normalising would turn the zero off the carriageway into a division by nothing.
    /// </summary>
    Vector2 LaneDirectionOfCell(int cell) =>
        new(_plan.LaneDirs[cell * 2] / 127f, _plan.LaneDirs[cell * 2 + 1] / 127f);
}
