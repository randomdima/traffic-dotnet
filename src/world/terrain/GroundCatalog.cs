using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Terrain;

/// <summary>
/// What each kind of ground declares: who is permitted on it and whether it is directional — and what the two
/// grounds a wheel stands on do to it (TER-7b). The only place a member of <see cref="Ground"/> is named —
/// everything above it asks for a permission set. The kinds it must tell apart are TER-3, and the
/// catalogue is data, so a kind added here is a row rather than a branch anywhere above.
/// </summary>
/// <remarks>
/// The permissions are the catalogue's own; the surfaces are <see cref="SimConfig"/>'s, so a retune
/// is a figure changed in one file and not a code change.
/// </remarks>
internal readonly struct GroundCatalog
{
    /// <summary>Indexed by <see cref="Ground"/>, so a lookup is a load and not a switch.</summary>
    static readonly GroundRules[] RuleTable =
    [
        /* Grass     */ GroundRules.Walkable,
        /* Road      */ GroundRules.Drivable,
        /* Crosswalk */ GroundRules.Walkable | GroundRules.Drivable,
        /* Parking   */ GroundRules.Walkable | GroundRules.Drivable,
        /* Water     */ GroundRules.None,
        /* Sidewalk  */ GroundRules.Walkable | GroundRules.Preferred,
        /* Rail      */ GroundRules.None,
        /* Shoulder  */ GroundRules.Walkable,
    ];

    /// <summary>
    /// <b>The carriageway's surface, under a wheel on a way of the town</b>: a slide alone marks it, at
    /// <see cref="TerrainFigures.PavedMarkFactor"/> of the bar.
    /// </summary>
    public GroundEffect Paved { get; }

    /// <summary>
    /// <b>The grass's, under a wheel anywhere else</b>: the one surface a wheel displaces by rolling over it, and so
    /// the one that marks without anybody sliding — a wheel ploughing turf is not a slide being scored, so it takes
    /// the bar itself.
    /// </summary>
    public GroundEffect Grass { get; }

    public GroundCatalog(SimConfig config)
    {
        // The catalogue is built once with the town, so the trim is spent here and every wheel afterwards
        // reads a figure that has already had it (TrimFigures).
        var dragTrim = config.Trim.RollingResistance;
        Paved = new GroundEffect(
            config.Terrain.PavedCoefficient, config.PavedDragMps2 * dragTrim, config.Terrain.PavedMarkFactor, Ploughs: false);
        Grass = new GroundEffect(config.Terrain.GrassCoefficient, config.GrassDragMps2 * dragTrim, 1f, Ploughs: true);
    }

    public static int Kinds => RuleTable.Length;

    public static GroundRules RulesOf(Ground ground) => RuleTable[(int)ground];

    public static bool Drivable(Ground ground) => (RulesOf(ground) & GroundRules.Drivable) != 0;
}
