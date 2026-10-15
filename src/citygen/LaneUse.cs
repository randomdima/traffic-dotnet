namespace TrafficSimulation.CityGen;

/// <summary>A turn at one junction from one road into another.</summary>
internal readonly record struct RoadTurn(int Junction, int FromRoad, int ToRoad);

/// <summary>
/// <b>Which lanes of an arm a movement is made from, and which lanes of the road it takes it reaches</b>
/// (TER-5j): carrying straight on from any of them, turning to the near side from the kerb lane and to the
/// far side from the lane beside the line the two ways meet on — the rule of a carriageway marked with
/// nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where nothing carries straight on, the lanes are shared out between the turns there are</b>: the kerb
/// half to the near side and the other half to the far, the middle lane of an odd count to both, and the
/// whole of them to a turn that is the only kind on offer — so a bend, or a fork into two roads, is driven
/// out of every lane that arrives at it. <b>No lane arrives at a junction that offers it nothing.</b> <b>Nor
/// does a fork carrying straight on into two</b>: its branch nearer the kerb takes the kerb half and the other the
/// rest, as two turns would — each lane carried onto both would cross the next one carried onto the other.
/// </para>
/// <para>
/// <b>A lane making a movement joins the lane of its own number on the road taken</b> (<see cref="Joins"/>), both
/// numbered from the side the movement bears to — the kerb for the near side, the line for the far. So a turn onto
/// a road of one lane is made from the edge lane alone, never from the second lane onto the first, and no two
/// movements off one arm onto one road cross each other.
/// </para>
/// <para>
/// <b>A lane joins one lane, and a lane with no lane of its number there joins nothing</b> (GEN-50): of more lanes onto
/// fewer the ones over end, and of fewer onto more the ones over are reached by none. A car gets off the one and onto the
/// other by moving across (CAR-53). <b>The ground between them is still paved</b> (<see cref="Eases"/>), over the line
/// the lanes' spread would have joined them by.
/// </para>
/// <para>
/// <b>One lane each way is today's town and comes out exactly as it was</b>: every lane is the kerb lane and
/// the inner lane at once, so it is offered every turn its node makes and reaches the one lane each takes.
/// </para>
/// <para>
/// <b>What a node does not make at all</b> — a turn the plan forbids — is the caller's (<c>LaneLines</c>).
/// </para>
/// </remarks>
internal static class LaneUse
{
    /// <summary>
    /// The kinds of turn a node offers the lanes of one arm, before any is given to a lane — and whether straight on
    /// <see cref="Forks"/> into more than one road.
    /// </summary>
    public readonly record struct Offered(bool Straight, bool NearSide, bool FarSide, bool Forks = false)
    {
        public Offered With(LaneTurn turn) => turn switch
        {
            LaneTurn.Straight => this with { Straight = true },
            LaneTurn.NearSide => this with { NearSide = true },
            _ => this with { FarSide = true },
        };
    }

    /// <summary>
    /// Whether the lane <paramref name="fromKerb"/> in from the kerb of an arm of <paramref name="lanes"/> is joined
    /// by a turn of this kind to the lane <paramref name="ontoFromKerb"/> in from the kerb of a road taking it in
    /// <paramref name="ontoLanes"/>: lane for lane, both numbered from the kerb where the movement
    /// <paramref name="bearsToTheKerb"/> and from the line where it bears away from it.
    /// </summary>
    public static bool Joins(
        Offered offered, LaneTurn turn, int fromKerb, int lanes, int ontoFromKerb, int ontoLanes, bool bearsToTheKerb) =>
        Makes(offered, turn, fromKerb, lanes, bearsToTheKerb)
        && FromItsSide(ontoFromKerb, ontoLanes, bearsToTheKerb) == FromItsSide(fromKerb, lanes, bearsToTheKerb);

    /// <summary>
    /// <b>Whether the ground between two lanes is paved though no car is joined over it</b> (<see cref="LaneLines.Tapers"/>):
    /// the lanes making the turn spread over the lanes it reaches in the same order, fewer onto more each fanning out
    /// over its share, more onto fewer merging into theirs. Asked only where lane for lane (<see cref="Joins"/>) leaves
    /// one of the two ending or unreached — which is the caller's (<c>LaneLines</c>).
    /// </summary>
    public static bool Eases(
        Offered offered, LaneTurn turn, int fromKerb, int lanes, int ontoFromKerb, int ontoLanes, bool bearsToTheKerb)
    {
        var at = -1;
        var of = 0;
        for (var lane = 0; lane < lanes; lane++)
        {
            if (!Makes(offered, turn, lane, lanes, bearsToTheKerb)) continue;

            if (lane == fromKerb) at = of;
            of++;
        }

        return at >= 0 && Spread(at, of, ontoFromKerb, ontoLanes);
    }

    static int FromItsSide(int fromKerb, int lanes, bool countedFromTheKerb) =>
        countedFromTheKerb ? fromKerb : lanes - 1 - fromKerb;

    /// <summary>
    /// Whether the <paramref name="at"/>th of the <paramref name="of"/> lanes a movement is made from, counted from
    /// the kerb, reaches a lane of the road taken: its own share of them, in the same order.
    /// </summary>
    static bool Spread(int at, int of, int ontoFromKerb, int ontoLanes)
    {
        var lowest = at * ontoLanes / of;
        var highest = ((((at + 1) * ontoLanes) + of - 1) / of) - 1;
        return ontoFromKerb >= lowest && ontoFromKerb <= highest;
    }

    /// <summary><b>Whether a lane makes a turn of this kind</b>. A turn no lane of the arm makes is not made from it.</summary>
    static bool Makes(Offered offered, LaneTurn turn, int lane, int lanes, bool bearsToTheKerb)
    {
        var (first, last) = MadeFrom(offered, turn, lanes, bearsToTheKerb);
        return lane >= first && lane <= last;
    }

    /// <summary>
    /// The lanes of an arm a turn of this kind is made from, counted in from the kerb, first and last — where straight on
    /// forks, the half on the side the branch <paramref name="bearsToTheKerb"/> or from, as a near-side and far-side turn
    /// share them.
    /// </summary>
    static (int First, int Last) MadeFrom(Offered offered, LaneTurn turn, int lanes, bool bearsToTheKerb) => turn switch
    {
        LaneTurn.Straight when offered.Forks && bearsToTheKerb => (0, ((lanes + 1) / 2) - 1),
        LaneTurn.Straight when offered.Forks => (lanes / 2, lanes - 1),
        LaneTurn.Straight => (0, lanes - 1),
        LaneTurn.NearSide when offered.Straight => (0, 0),
        LaneTurn.NearSide when offered.FarSide => (0, ((lanes + 1) / 2) - 1),
        LaneTurn.NearSide => (0, lanes - 1),
        _ when offered.Straight => (lanes - 1, lanes - 1),
        _ when offered.NearSide => (lanes / 2, lanes - 1),
        _ => (0, lanes - 1),
    };
}
