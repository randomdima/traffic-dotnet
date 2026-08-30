using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Terrain;

/// <summary>
/// What each kind of ground declares: who is permitted on it, whether it is directional, and what it
/// does to a body travelling over it. The only place a member of <see cref="Ground"/> is named —
/// everything above it asks for a permission set.
/// </summary>
/// <remarks>
/// The permissions are the catalogue's own; the coefficient is <see cref="SimConfig"/>'s, so a retune
/// is a figure changed in one file and not a code change. Footway and Sidewalk deliberately declare the
/// same permissions plus Sidewalk's preference: stating them twice with different values is how the two
/// would drift.
/// </remarks>
internal readonly struct GroundCatalog
{
    /// <summary>Indexed by <see cref="Ground"/>, so a lookup is a load and not a switch.</summary>
    static readonly GroundRules[] RuleTable =
    [
        /* Grass        */ GroundRules.Walkable,
        /* Road         */ GroundRules.Drivable | GroundRules.Directional,
        /* Intersection */ GroundRules.Drivable,
        /* Crosswalk    */ GroundRules.Walkable | GroundRules.Drivable | GroundRules.Directional,
        /* Parking      */ GroundRules.Walkable | GroundRules.Drivable,
        /* Water        */ GroundRules.None,
        /* Footway      */ GroundRules.Walkable,
        /* Sidewalk     */ GroundRules.Walkable | GroundRules.Preferred,
    ];

    /// <summary>
    /// The one surface a wheel displaces by rolling over it, and so the one that marks without
    /// anybody sliding. Everything else records a slide and nothing else.
    /// </summary>
    static readonly bool[] PloughTable =
    [
        /* Grass */ true, false, false, false, false, false, false, false,
    ];

    /// <summary>
    /// What water multiplies the mark threshold by: enough that no wheel in the town can work it hard
    /// enough to leave a mark. A surface that keeps a record of a tyre is one that holds still under
    /// it, and open water does not — the alternative spelling, a factor of zero, says the opposite.
    /// </summary>
    const float WaterMarkFactor = 1e6f;

    /// <summary>
    /// The three figures held together rather than in three arrays of their own, because the caller
    /// that matters wants all three about one patch: four wheels a car, every car every tick, and
    /// three tables is three loads and three bounds checks where one row is one.
    /// </summary>
    readonly GroundEffect[] _effects;

    public GroundCatalog(SimConfig config)
    {
        var grass = config.Terrain.GrassCoefficient;
        var paved = config.Terrain.PavedCoefficient;
        var water = config.Terrain.WaterCoefficient;

        // The table is built once with the town, so the trim is spent here and every wheel afterwards
        // reads a figure that has already had it (TrimFigures).
        var dragTrim = config.Trim.RollingResistance;
        var grassDrag = config.GrassDragMps2 * dragTrim;
        var pavedDrag = config.PavedDragMps2 * dragTrim;
        var waterDrag = config.WaterDragMps2 * dragTrim;

        var pavedMark = config.Terrain.PavedMarkFactor;

        _effects = new GroundEffect[RuleTable.Length];
        for (var ground = 0; ground < _effects.Length; ground++)
        {
            var (coefficient, drag, mark) = (Ground)ground switch
            {
                // Grass takes the bar itself: a wheel ploughing turf is not a slide being scored, and the
                // one per cent it used to be shaded by was a factor nobody could see and anybody could tune.
                Ground.Grass => (grass, grassDrag, 1f),
                Ground.Water => (water, waterDrag, WaterMarkFactor),
                _ => (paved, pavedDrag, pavedMark),
            };
            _effects[ground] = new GroundEffect(coefficient, drag, mark, PloughTable[ground]);
        }
    }

    public static int Kinds => RuleTable.Length;

    public static GroundRules RulesOf(Ground ground) => RuleTable[(int)ground];

    public static bool Walkable(Ground ground) => (RulesOf(ground) & GroundRules.Walkable) != 0;

    public static bool Drivable(Ground ground) => (RulesOf(ground) & GroundRules.Drivable) != 0;

    public static bool Preferred(Ground ground) => (RulesOf(ground) & GroundRules.Preferred) != 0;

    public static bool Directional(Ground ground) => (RulesOf(ground) & GroundRules.Directional) != 0;

    /// <summary>
    /// What the surface is worth to a body on it: 1 on anything paved, 0.8 on grass, 0.15 on water.
    /// The <b>grip</b> — the ceiling a tyre or a foot has here — and the walk planner's cost basis.
    /// </summary>
    public float Coefficient(Ground ground) => _effects[(int)ground].Coefficient;

    /// <summary>
    /// The <b>drag</b>: resistance to travelling over this ground, in m/s² against the body's
    /// own motion. It is spent outside the traction budget, so it is never something a tyre could
    /// have cornered on instead.
    /// </summary>
    public float DragMps2(Ground ground) => _effects[(int)ground].DragMps2;

    /// <summary>
    /// The <b>mark threshold</b>, as a factor on <see cref="SimConfig.Marks.PowerM2S3"/>:
    /// how hard this ground has to be worked before it keeps a record of it.
    /// </summary>
    public float MarkFactor(Ground ground) => _effects[(int)ground].MarkFactor;

    /// <summary>All three at once, which is what a wheel asks for and the only caller on a hot path.</summary>
    public GroundEffect EffectOf(Ground ground) => _effects[(int)ground];

    /// <summary>
    /// Whether a wheel rolling over this ground displaces it. On soft ground the rolling resistance
    /// leaves a rut; on a hard surface that same resistance is hysteresis inside the rubber and marks
    /// nothing however fast the car is going, so only a slide marks a road.
    /// </summary>
    public static bool Ploughs(Ground ground) => PloughTable[(int)ground];
}
