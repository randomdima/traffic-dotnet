using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The driving exam</b>: a six by six lattice of junctions with one crossing manoeuvre staged at each
/// of them, laid from <see cref="ExamCards"/> and written out as a map like any other.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is laid for the same reason the proving ground is</b> (<see cref="TrackPlan"/>): what is being
/// measured is a movement through a box, so where every car stands and what else is coming has to be
/// <em>chosen</em> rather than found in a city and hoped for. A junction in Odesa is whatever Odesa
/// happens to hold; here it is a card.
/// </para>
/// <para>
/// <b>The ground is the lattice's and the bodies are the cards'</b> (<see cref="ExamGround"/>,
/// <see cref="ExamMap"/>). The walking exam is laid on the same ground from cards of its own
/// (<see cref="FootwayPlan"/>), so what a driver meets at a junction and what a walker meets there is one
/// junction rather than two.
/// </para>
/// <para>
/// <b>One make of car and one build of it.</b> Every car on the map is the nominal car (CAR-11a) wearing
/// one ordinary look, because a card compares one crossing against another and a fleet of different weights
/// and drivetrains would put a second variable inside every comparison. That the whole map is one look is
/// a fact about the map's <em>name</em> where the town stands its fleet up (<see cref="StandsOneLook"/>),
/// since a plan may not read the car catalogue — and the look is an ordinary one rather than the police
/// livery, because in this town a police look is what a police car <em>is</em> (SRV-2, SRV-5) and every one
/// of them belongs to a station.
/// </para>
/// <para>
/// <b>Paint on every arm, and a body at four of them.</b> A crossing on every arm of every junction is
/// the rule a generated town is laid to (TER-6) and it is the rule here, because a block whose pavement
/// nobody may leave is a walking network of islands. What a card picks is therefore not where the paint
/// is but which crossing somebody is standing at, and the four cards about paint are the four crossings
/// that have anybody on them.
/// </para>
/// </remarks>
internal static class ExamPlan
{
    /// <summary>The map's catalogue name, which is also its file's.</summary>
    public const string Name = "Exam";

    /// <summary>
    /// Whether the cars on this map are one look and one build rather than the fleet as it ships — asked of
    /// the map's name, because the name is all a town has to go on when it stands its cars up, and the look
    /// itself belongs to a catalogue this folder may not read.
    /// </summary>
    public static bool StandsOneLook(string name) => string.Equals(name, Name, StringComparison.Ordinal);

    /// <summary>Where the exam's own randomness comes from. Nothing on this map is drawn, so it is a constant and says so.</summary>
    const ulong Seed = 0x6578616D_6A756E63UL;

    public static CityPlan Lay(SimConfig config)
    {
        var lattice = ExamLattice.Of(config);
        return ExamMap.Lay(Name, Seed, lattice.Ground, config, Spawns(lattice));
    }

    /// <summary>
    /// <b>Every car first and in card order</b>, so a card's drivers are a run of the fleet and a car's
    /// index is the card that staged it; then the people, one at the kerb of every crossing a card is
    /// about, in the same order — which is the numbering <see cref="ExamLattice.WalkerOf"/> hands back.
    /// </summary>
    /// <remarks>
    /// <b>Where a body stands is all this decides, and not what it then does.</b> This map lays pavement,
    /// so a walker with nowhere to be wanders the whole lattice rather than pacing the road beside it
    /// (<c>TownWorld.PacesARoad</c>) — the harness orders its walkers over their own paint for that
    /// reason, and a card about paint that trusted the wander would be one asked by luck.
    /// </remarks>
    static CityPlan.SpawnArrays Spawns(ExamLattice lattice)
    {
        var kind = new List<byte>();
        var positionM = new List<Vector2>();
        var headingRad = new List<float>();

        for (var card = 0; card < ExamCards.Count; card++)
        {
            for (var driver = 0; driver < ExamCards.All[card].Drivers.Length; driver++)
            {
                kind.Add(SpawnKindCar);
                positionM.Add(lattice.StandM(card, driver));
                headingRad.Add(lattice.StandHeadingRad(card, driver));
            }
        }

        for (var card = 0; card < ExamCards.Count; card++)
        {
            if (!lattice.Waiting(card, out var standM, out var facingRad)) continue;

            kind.Add(SpawnKindPerson);
            positionM.Add(standM);
            headingRad.Add(facingRad);
        }

        return new CityPlan.SpawnArrays
        {
            Kind = [.. kind], PositionM = [.. positionM], HeadingRad = [.. headingRad],
        };
    }

    const byte SpawnKindPerson = 0;

    const byte SpawnKindCar = 1;
}
