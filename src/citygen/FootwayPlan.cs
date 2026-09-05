using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The walking exam</b>: a four by four lattice of junctions with one walk staged at each of them,
/// laid from <see cref="FootwayCards"/> and written out as a map like any other.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the driving exam's question asked of the other agent</b> (<see cref="ExamPlan"/>): not what a
/// car does where roads meet but what somebody on foot does there — round the corner, over the paint, past
/// a body standing about — on ground that was <em>chosen</em> rather than found in a city and hoped for.
/// The two are laid on one lattice (<see cref="ExamGround"/>), so a junction that a walker cannot get round
/// is the same junction a car drove through.
/// </para>
/// <para>
/// <b>Nothing on this map drives, and that is the whole point of it.</b> A walk that fails with traffic on
/// the map leaves a reader unable to say which of the two agents was wrong; what a car owes a crossing is
/// asked, with the traffic staged, on the driving exam's own cards.
/// </para>
/// <para>
/// <b>Every body on it is under an order.</b> This map lays pavement and has nowhere to be on it, so a
/// walker nobody is telling anything draws a destination anywhere on the lattice
/// (<c>TownWorld.WanderInstead</c>) and turns up in somebody else's card — the bodies a card wants
/// standing still are ordered to the ground they are already on.
/// </para>
/// </remarks>
internal static class FootwayPlan
{
    /// <summary>The map's catalogue name.</summary>
    public const string Name = "Footway";

    /// <summary>Where the map's own randomness comes from. Nothing on it is drawn, so it is a constant and says so.</summary>
    const ulong Seed = 0x77616C6B_6578616DUL;

    public static CityPlan Lay(SimConfig config)
    {
        var lattice = FootwayLattice.Of(config);
        return ExamMap.Lay(Name, Seed, lattice.Ground, config, Spawns(lattice));
    }

    /// <summary>
    /// Every body, in card order, so a walker's index is the card that staged it — which is the numbering
    /// <see cref="FootwayLattice.WalkerOf"/> hands back.
    /// </summary>
    static CityPlan.SpawnArrays Spawns(FootwayLattice lattice)
    {
        var kind = new List<byte>();
        var positionM = new List<Vector2>();
        var headingRad = new List<float>();

        for (var card = 0; card < FootwayCards.Count; card++)
        {
            for (var walker = 0; walker < FootwayCards.All[card].Walkers.Length; walker++)
            {
                kind.Add(SpawnKindPerson);
                positionM.Add(lattice.StandM(card, walker));
                headingRad.Add(lattice.FacingRad(card, walker));
            }
        }

        return new CityPlan.SpawnArrays
        {
            Kind = [.. kind], PositionM = [.. positionM], HeadingRad = [.. headingRad],
        };
    }

    const byte SpawnKindPerson = 0;
}
