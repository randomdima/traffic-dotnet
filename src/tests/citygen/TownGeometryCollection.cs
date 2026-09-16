using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>Every class that asks a shared town a geometry question belongs here, and they run one at a time.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>The reason is that the indexes under a town are not re-entrant</b>
/// (<see cref="Core.Geometry.ChainIndex"/>). A town is laid once and handed to every case that asks for it
/// (<see cref="Towns.Of"/>), and the scratch a query uses is the index's own — so two classes are two xUnit
/// collections, they run at once, and the two of them read each other's candidate sets. <b>What comes back
/// then is a perfectly well-formed wrong answer</b>: the boundary of a city came back with two hundred runs
/// open in one run out of several, and closed in the next.
/// </para>
/// <para>
/// <b>The membership test is whether the class asks a town for a geometry index</b> — the merge of the
/// driven lines, the outset of what that merged into, which lines are near a place — and not whether it
/// touches a town at all. A class that only reads what the plan already holds is asking a field and races
/// with nothing.
/// </para>
/// <para>
/// <b>It is the suite's fault and not the index's.</b> The index is built with the town and never written to
/// again, which is what lets a tick read it without a lock; a caller that wants one from two threads wants
/// two indexes, and here the cheaper answer is one thread. A case that really needs its own town has
/// <see cref="Towns.Fresh"/>.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TownGeometryCollection
{
    public const string Name = "a town's geometry";
}
