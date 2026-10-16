using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>The fixture town stood once for a class whose cases only read it</b> — a class fixture, so each class has its own
/// and its cases have it one at a time. Standing the fixture is most of what such a case costs.
/// </summary>
/// <remarks>
/// <b>Nothing handed it may tick it, put a hand on a car, move a body or lay its claims.</b> The selection is the one
/// thing a case may change, and only a case that sets its own before reading it. A case that does more stands its own
/// town.
/// </remarks>
public sealed class StandingTown : IDisposable
{
    internal TownWorld World { get; } = new(Towns.Of(Towns.Fixture), SimConfig.Shipped());

    public void Dispose() => World.Dispose();
}
