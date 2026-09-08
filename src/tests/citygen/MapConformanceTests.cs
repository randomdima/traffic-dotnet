using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// The shallow bar <b>every town the suite owns</b> is held to, and nothing more: its junctions are
/// junctions, no lit junction is staggered outside its own cycle, it is furnished, nothing is laid on its
/// water and nothing stands off its edge.
/// </summary>
/// <remarks>
/// <para>
/// A whole map is the wrong place to ask a detailed question — "every bridge carries a footway", asked of
/// whatever a town happens to contain, is a different question every time somebody edits the town and is
/// vacuous on a map with no bridge. Detailed geometry is asked of named places on the fixture map.
/// </para>
/// <para>
/// <b>The bar itself is <see cref="Conformance"/>'s</b>, so that <see cref="Tier.Maps"/> can hold a shipped
/// city to the same one without a second copy of it.
/// </para>
/// <para>
/// <b>Drivability is not asked here and used to be.</b> Whether a generated town can be driven round is
/// <see cref="GeneratorTests"/>' over four seeds, of which the suite's own town is one arrangement — asking
/// it again of that one town is the same question with a smaller sample. It stays on
/// <see cref="ShippedCityTests"/>, where the subject is a map somebody authored rather than the generator.
/// </para>
/// <para>
/// One clause of the bar is only half asked here, and it is said rather than left out: <b>the greens
/// themselves</b> need the cycle table and the signal agent, which arrive with the lit town. What a plan
/// alone can answer — that a lit junction carries a phase offset inside the cycle it is staggered against —
/// is what is asserted, and the conflicting-greens half lands with the traffic lights.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P3)]
public class MapConformanceTests
{
    public static TheoryData<string> Maps => Towns.EveryLaidMap();

    [Theory]
    [MemberData(nameof(Maps))]
    public void ItsJunctionsAreJunctions(string map) => Conformance.ItsJunctionsAreJunctions(map);

    [Theory]
    [MemberData(nameof(Maps))]
    public void ALitJunctionIsStaggeredInsideItsOwnCycle(string map) =>
        Conformance.ALitJunctionIsStaggeredInsideItsOwnCycle(map);

    [Theory]
    [MemberData(nameof(Maps))]
    public void ItIsFurnished(string map) => Conformance.ItIsFurnished(map);

    [Theory]
    [MemberData(nameof(Maps))]
    public void NothingIsLaidOnItsWater(string map) => Conformance.NothingIsLaidOnItsWater(map);

    [Theory]
    [MemberData(nameof(Maps))]
    public void NothingItCarriesStandsOffIt(string map) => Conformance.NothingItCarriesStandsOffIt(map);
}
