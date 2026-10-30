using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A town drawn and never stepped</b> — what a player of a served town lays of it (MP-5, MP-6): the plan, the lights,
/// the crossings' gear, the wind and the clouds off the same seeds the server laid them from, and the buildings' uses the
/// roofs are drawn by. Everything that moves is the player's replica's.
/// </summary>
/// <remarks>
/// <b>It is laid off a <see cref="TownWorld"/>'s own groundwork, cut short</b> (<see cref="TownWorld.LayingTheScene"/>), so
/// the lights stand where a stepped town's do and turn at the same instants. No fleet, no route network, no atlas and no
/// world is laid: on a traced city those are the open's peak and most of what it keeps.
/// </remarks>
internal sealed class TownScene
{
    public TownScene(
        CityPlan plan, SimConfig config, bool levelled, BuildingUses uses, SignalService signals, SignalHeads heads,
        CrossingGear crossingGear, double stoodMs)
    {
        Plan = plan;
        Levelled = levelled;
        Uses = uses;
        Signals = signals;
        Heads = heads;
        CrossingGear = crossingGear;
        StoodMs = stoodMs;
        Wind = new Wind(config.Wind, plan.Seed);
        Clouds = new Clouds(config.Clouds, Wind, plan.Seed);
        Marks = new DriftMarks(0, 0, StrokeFit.Of(config));
    }

    public CityPlan Plan { get; }

    /// <summary>Whether the town has a level above the ground, which is whether the cars are parted by it.</summary>
    public bool Levelled { get; }

    public BuildingUses Uses { get; }

    public SignalService Signals { get; }

    public SignalHeads Heads { get; }

    public CrossingGear CrossingGear { get; }

    public Wind Wind { get; }

    public Clouds Clouds { get; }

    /// <summary>The marks on the ground: none, since nothing here is driven, but the ground's fill reads a ring of them.</summary>
    public DriftMarks Marks { get; }

    /// <summary>How long it took to lay, plan excluded.</summary>
    public double StoodMs { get; }
}
