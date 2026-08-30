namespace TrafficSimulation.Core.Config;

/// <summary>
/// Every figure the simulation is parameterised by, loaded once and injected from the composition
/// root. Nothing in behaviour code may hold a number of its own.
/// </summary>
/// <remarks>
/// <para>
/// The shape says which kind a figure is: <b>nested groups are authored</b> — the numbers somebody
/// chose, and the only ones the override file may set — while <b>everything on the root is derived</b>
/// from them (<see cref="SimConfig"/>'s other file). Ratios are normative in form and not in value, so
/// what is authored is the ratio and the metre figure is derived: one constant rescales the whole town.
/// </para>
/// <para>
/// Units are part of every name: <c>…M</c>, <c>…Mps</c>, <c>…Mps2</c>, <c>…Deg</c>, <c>…S</c>,
/// <c>…Kg</c>, <c>…Kj</c>, <c>…Hz</c>, <c>…Px</c>.
/// </para>
/// </remarks>
internal sealed partial class SimConfig
{
    /// <summary>The shipped figures, with the shared override file applied over them.</summary>
    public static SimConfig Load() => SharedFiguresReader.Apply(new SimConfig(), ProjectPaths.SharedFiguresFile);

    /// <summary>The shipped figures alone — for a test or a rig that must not depend on a file.</summary>
    public static SimConfig Shipped() => new();

    public CarFigures Car { get; init; } = new();
    public TyreFigures Tyre { get; init; } = new();
    public LampFigures Lamps { get; init; } = new();
    public DrivingFigures Driving { get; init; } = new();
    public LadderFigures Ladder { get; init; } = new();
    public PersonFigures Person { get; init; } = new();
    public AmbulanceFigures Ambulance { get; init; } = new();
    public ServiceFigures Service { get; init; } = new();
    public EvacuatorFigures Evacuator { get; init; } = new();
    public DamageFigures Damage { get; init; } = new();
    public RoadFigures Road { get; init; } = new();
    public BuildingFigures Building { get; init; } = new();
    public PropFigures Prop { get; init; } = new();
    public SignalFigures Signals { get; init; } = new();
    public TerrainFigures Terrain { get; init; } = new();
    public MarkFigures Marks { get; init; } = new();
    public CityGenFigures CityGen { get; init; } = new();
    public NetworkFigures Network { get; init; } = new();
    public SolverFigures Solver { get; init; } = new();
    public SimFigures Sim { get; init; } = new();
    public ViewFigures View { get; init; } = new();
    public ControlFigures Control { get; init; } = new();

    /// <summary>
    /// <b>The one group a running session may move</b>, and only ever as a share of what is authored above
    /// it. Every trim is 1 unless a debug panel has been opened, so this changes nothing about a shipped
    /// run — see <see cref="TrimFigures"/>.
    /// </summary>
    public TrimFigures Trim { get; init; } = new();
}
