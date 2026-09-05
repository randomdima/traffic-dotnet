using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Terrain;

/// <summary>
/// What is answered about a point: which ground it stands on, who is permitted there, and what the
/// surface is worth.
/// </summary>
internal readonly record struct GroundSample(Ground Ground, GroundRules Rules, float Coefficient)
{
    public bool Walkable => (Rules & GroundRules.Walkable) != 0;

    public bool Drivable => (Rules & GroundRules.Drivable) != 0;

    public bool Preferred => (Rules & GroundRules.Preferred) != 0;
}

/// <summary>
/// What one surface does to a body travelling over it: grip, drag and the mark threshold, asked for
/// together because a wheel wants all three about the ground under that one patch.
/// </summary>
internal readonly record struct GroundEffect(float Coefficient, float DragMps2, float MarkFactor, bool Ploughs);

/// <summary>
/// <b>The town's ground as a body meets it</b>: which kind is under a point (<see cref="GroundShapes"/>),
/// who is permitted on that kind and what it is worth (<see cref="GroundCatalog"/>).
/// </summary>
/// <remarks>
/// <b>Two halves and one geometry.</b> The shape half is the plan's own and lives with it, so a town can
/// be asked what is where while it is still being laid; the permission half lives here, because what a
/// kind of ground <em>allows</em> is a rule about agents and the plan does not know what an agent is
/// (TER-2a). Nothing is quantised in either: there is one description of the surface and this reads it
/// (TER-7).
/// </remarks>
internal sealed class GroundLocator
{
    readonly GroundShapes _shapes;
    GroundCatalog _catalog;

    public GroundLocator(CityPlan plan, SimConfig config)
        : this(new GroundShapes(plan.Ground, config), config)
    {
    }

    public GroundLocator(GroundShapes shapes, SimConfig config)
    {
        _shapes = shapes;
        _catalog = new GroundCatalog(config);
    }

    /// <summary>
    /// What each ground is worth, read again from the figures as they stand now. <b>The shapes do not
    /// move</b> — they are the plan's, and a figure cannot relay a town — so this is the whole of what a
    /// changed figure does to the ground under a wheel.
    /// </summary>
    public void FiguresChanged(SimConfig config) => _catalog = new GroundCatalog(config);

    public GroundSample At(Vector2 pointM)
    {
        var ground = _shapes.At(pointM);
        return new GroundSample(ground, GroundCatalog.RulesOf(ground), _catalog.Coefficient(ground));
    }

    public Ground GroundAt(Vector2 pointM) => _shapes.At(pointM);

    /// <summary>
    /// The three physics figures under one point. Kept apart from <see cref="At"/> because the two have
    /// different callers: what a wheel wants is what this ground does to it, and what a walker and the
    /// planners want is who is permitted here.
    /// </summary>
    public GroundEffect EffectAt(Vector2 pointM) => _catalog.EffectOf(_shapes.At(pointM));

    /// <summary>Whether the point is inside the town's own box, for a caller that wants to know before it asks.</summary>
    public bool Contains(Vector2 pointM) => _shapes.Contains(pointM);
}
