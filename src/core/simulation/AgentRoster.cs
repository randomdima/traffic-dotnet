namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// The one place the flat roster index the loop walks is decoded: people first, then cars. An agent's
/// index is its identity for the decision clock's stagger, so the two fleets are laid end to end and
/// neither is renumbered.
/// </summary>
internal readonly struct AgentRoster(int people, int cars)
{
    public int Count => people + cars;

    public int People => people;

    public int Cars => cars;

    public bool IsCar(int agent) => agent >= people;

    /// <summary>Only meaningful when <see cref="IsCar"/> is true.</summary>
    public int CarIndex(int agent) => agent - people;

    public int AgentOfCar(int car) => people + car;

    public int AgentOfPerson(int person) => person;
}
